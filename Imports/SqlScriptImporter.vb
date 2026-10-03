'  Holographic DB - A 3D visual relational database designer
'  Copyright (C) 2026 Bioquad.github.io
'
'  This program is free software: you can redistribute it and/or modify
'  it under the terms of the GNU General Public License as published by
'  the Free Software Foundation, either version 3 of the License, or
'  (at your option) any later version.
'
'  This program is distributed in the hope that it will be useful,
'  but WITHOUT ANY WARRANTY; without even the implied warranty of
'  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
'  GNU General Public License for more details.
'
'  You should have received a copy of the GNU General Public License
'  along with this program. If not, see <https://www.gnu.org/licenses/>.
'
Imports System.Collections.Generic
Imports System.Text
Imports System.Text.RegularExpressions

' ============================================================
' SqlScriptImporter.vb  —  DB-Core Holographic
' Parseja un script DDL (T-SQL, MySQL/MariaDB o PostgreSQL) i
' retorna taules i relacions.
'
' Suporta:
'   CREATE TABLE [schema].[Nom] ( ... )
'   ALTER TABLE ... ADD [CONSTRAINT x] FOREIGN KEY ... REFERENCES ...
'   FK dins del CREATE TABLE (de taula o de columna: "col INT REFERENCES t(c)")
'   IDENTITY(seed,inc) / AUTO_INCREMENT / SERIAL / GENERATED ... AS IDENTITY
'   PRIMARY KEY (simple i composta), UNIQUE, DEFAULT, CHECK
'   Columnes calculades: AS (expr) [PERSISTED] / GENERATED ALWAYS AS (expr) STORED|VIRTUAL
'
' Els literals de cadena i els comentaris es tracten abans de
' normalitzar, de manera que el seu contingut no es modifica mai.
' ============================================================
Public Module SqlScriptImporter

    ' ── Resultat de la importació ────────────────────────────────
    Public Class ImportResult
        Public Property Taules    As New List(Of TablaBBDD)()
        Public Property Relacions As New List(Of RelacionBBDD)()
        Public Property Errors    As New List(Of String)()
        Public Property Warnings  As New List(Of String)()
    End Class

    Private Enum Dialecte
        TSql
        MySql
        PostgreSql
    End Enum

    ' FK trobada dins d'un CREATE TABLE; es resol quan ja hi ha totes les taules
    Private Class FkPendent
        Public Nom As String
        Public TaulaOrigen As String
        Public CampFK As String
        Public TaulaDesti As String
        Public CampPK As String
        Public OnDelete As String
        Public OnUpdate As String
    End Class

    ' Estat d'una importació (literals emmascarats i FK pendents)
    Private Class Context
        Public ReadOnly Literals As New List(Of String)()
        Public ReadOnly FkPendents As New List(Of FkPendent)()
        Public Dialecte As Dialecte = Dialecte.TSql
    End Class

    Private Const ACCIO As String = "(NO\s+ACTION|CASCADE|SET\s+NULL|SET\s+DEFAULT|RESTRICT)"
    Private Const IDENT As String = "\[?(\w+)\]?"
    Private Const IDENT_QUALIFICAT As String = "(?:\[?\w+\]?\s*\.\s*)?\[?(\w+)\]?"

    ' ════════════════════════════════════════════════════════════
    ' MÈTODE PRINCIPAL
    '   script  : contingut complet del fitxer .sql
    '   idBase  : primer Id de taula disponible (per merge)
    '   relBase : primer Id de relació disponible (per merge)
    ' ════════════════════════════════════════════════════════════
    Public Function Importar(script As String,
                             Optional idBase As Integer = 1,
                             Optional relBase As Integer = 1) As ImportResult
        Dim res As New ImportResult()
        Dim ctx As New Context()

        ' 0. Treure comentaris i emmascarar literals ('...') → «n»
        Dim net As String = EmmascararLiterals(If(script, ""), ctx)

        ' 1. Detectar dialecte i normalitzar a T-SQL
        Dim d As Dialecte = DetectarDialecte(net)
        ctx.Dialecte = d
        net = NormalitzarDialecte(net, d)
        net = Regex.Replace(net, "\s+", " ").Trim()

        ' 2. Extreure blocs CREATE TABLE (inclou FK inline)
        ParseCreateTables(net, res, idBase, ctx)

        ' 3. Extreure ALTER TABLE ... ADD ... (FOREIGN KEY, PRIMARY KEY,
        '    UNIQUE, CHECK, DEFAULT ... FOR, IDENTITY) — p.ex. pg_dump i SSMS
        ParseAlterForeignKeys(net, ctx)
        ParseAlterRestriccions(net, res)

        ' 4. Resoldre totes les FK
        ResoldreFKs(res, relBase, ctx)

        ' 5. Restaurar literals dins de les expressions
        For Each t As TablaBBDD In res.Taules
            For Each f As CampoBBDD In t.Fields
                f.DefaultValue    = Restaurar(f.DefaultValue, ctx)
                f.CheckExpression = Restaurar(f.CheckExpression, ctx)
                f.FormulaCalculo  = Restaurar(f.FormulaCalculo, ctx)
            Next
        Next

        Return res
    End Function

    ' ════════════════════════════════════════════════════════════
    ' ESCÀNER: elimina comentaris i substitueix literals per «n»
    ' ════════════════════════════════════════════════════════════
    Private Function EmmascararLiterals(sql As String, ctx As Context) As String
        Dim sb As New StringBuilder(sql.Length)
        Dim i As Integer = 0
        Do While i < sql.Length
            Dim c As Char = sql(i)
            Dim seg As Char = If(i + 1 < sql.Length, sql(i + 1), ControlChars.NullChar)

            If c = "-"c AndAlso seg = "-"c Then
                ' Comentari de línia
                Do While i < sql.Length AndAlso sql(i) <> ControlChars.Lf AndAlso sql(i) <> ControlChars.Cr
                    i += 1
                Loop
                sb.Append(" "c)
            ElseIf c = "/"c AndAlso seg = "*"c Then
                ' Comentari de bloc
                Dim fi As Integer = sql.IndexOf("*/", i + 2, StringComparison.Ordinal)
                i = If(fi < 0, sql.Length, fi + 2)
                sb.Append(" "c)
            ElseIf c = "'"c Then
                ' Literal: '' dins la cadena és una cometa escapada
                Dim ini As Integer = i
                i += 1
                Do While i < sql.Length
                    If sql(i) = "'"c Then
                        If i + 1 < sql.Length AndAlso sql(i + 1) = "'"c Then
                            i += 2
                            Continue Do
                        End If
                        Exit Do
                    End If
                    i += 1
                Loop
                i = Math.Min(i + 1, sql.Length)
                ctx.Literals.Add(sql.Substring(ini, i - ini))
                sb.Append("«" & (ctx.Literals.Count - 1) & "»")
            Else
                sb.Append(c)
                i += 1
            End If
        Loop
        Return sb.ToString()
    End Function

    Private Function Restaurar(s As String, ctx As Context) As String
        If String.IsNullOrEmpty(s) OrElse s.IndexOf("«"c) < 0 Then Return s
        Return Regex.Replace(s, "«(\d+)»", Function(m) ctx.Literals(Integer.Parse(m.Groups(1).Value)))
    End Function

    ' ════════════════════════════════════════════════════════════
    ' DETECCIÓ I NORMALITZACIÓ DE DIALECTE
    ' ════════════════════════════════════════════════════════════
    Private Function DetectarDialecte(sql As String) As Dialecte
        If sql.Contains("`") OrElse
           Regex.IsMatch(sql, "\bENGINE\s*=|\bAUTO_INCREMENT\b", RegexOptions.IgnoreCase) Then
            Return Dialecte.MySql
        End If
        If Regex.IsMatch(sql, "\b(BIG|SMALL)?SERIAL\b|\bBYTEA\b|\bTIMESTAMPTZ\b|::|\bAS\s+IDENTITY\b|\bCREATE\s+EXTENSION\b",
                         RegexOptions.IgnoreCase) Then
            Return Dialecte.PostgreSql
        End If
        If Regex.IsMatch(sql, "\[\w+\]|\bIDENTITY\s*\(|^\s*GO\s*$|\bNVARCHAR\b",
                         RegexOptions.IgnoreCase Or RegexOptions.Multiline) Then
            Return Dialecte.TSql
        End If
        ' Identificadors entre cometes dobles sense marques T-SQL → PostgreSQL
        If sql.Contains("""") Then Return Dialecte.PostgreSql
        Return Dialecte.TSql
    End Function

    ''' <summary>
    ''' Transforma MySQL i PostgreSQL a sintaxi T-SQL equivalent perquè el
    ''' parser treballi sempre amb una sola sintaxi. Els literals ja estan
    ''' emmascarats, per tant no es poden alterar.
    ''' </summary>
    Private Function NormalitzarDialecte(sql As String, d As Dialecte) As String
        ' Identificadors: `x` i "x" → [x]
        sql = Regex.Replace(sql, "`([^`]+)`", "[$1]")
        sql = Regex.Replace(sql, """([^""]+)""", "[$1]")
        sql = Regex.Replace(sql, "\bIF\s+NOT\s+EXISTS\b", "", RegexOptions.IgnoreCase)

        If d = Dialecte.TSql Then Return sql

        ' ── Comú MySQL / PostgreSQL ─────────────────────────────
        sql = Regex.Replace(sql, "\bINTEGER\b", "INT", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bBOOLEAN\b|\bBOOL\b", "BIT", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bDOUBLE\s+PRECISION\b|\bDOUBLE\b", "FLOAT", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bMEDIUMTEXT\b|\bLONGTEXT\b|\bTINYTEXT\b|\bTEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bMEDIUMBLOB\b|\bLONGBLOB\b|\bTINYBLOB\b|\bBLOB\b|\bBYTEA\b", "VARBINARY(MAX)", RegexOptions.IgnoreCase)
        ' TIMESTAMP de MySQL/PostgreSQL és data-hora (a T-SQL seria ROWVERSION)
        sql = Regex.Replace(sql, "\bTIMESTAMPTZ\b|\bTIMESTAMP\s+WITH\s+TIME\s+ZONE\b", "DATETIMEOFFSET", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bTIMESTAMP\s*(\(\s*\d+\s*\))?(\s+WITHOUT\s+TIME\s+ZONE)?", "DATETIME2", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bDATETIME\s*\(\s*\d+\s*\)", "DATETIME2", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bTIME\s*\(\s*\d+\s*\)", "TIME", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bUUID\b", "UNIQUEIDENTIFIER", RegexOptions.IgnoreCase)

        If d = Dialecte.MySql Then
            sql = Regex.Replace(sql, "\bAUTO_INCREMENT\s*=\s*\d+", "", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\bAUTO_INCREMENT\b", "IDENTITY(1,1)", RegexOptions.IgnoreCase)
            ' Opcions de taula: ) ENGINE=InnoDB ... ;
            sql = Regex.Replace(sql, "\)\s*ENGINE\s*=\s*\w+[^;]*", ")", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\s*(?:DEFAULT\s+)?CHARACTER\s+SET\s*=?\s*\w+", "", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\s*(?:DEFAULT\s+)?CHARSET\s*=?\s*\w+", "", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\s*COLLATE\s*=?\s*\w+", "", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\bTINYINT\s*\(\s*1\s*\)", "BIT", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\b(TINYINT|SMALLINT|MEDIUMINT|INT|BIGINT)\s*\(\s*\d+\s*\)", "$1", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\bMEDIUMINT\b", "INT", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\s+UNSIGNED\b|\s+ZEROFILL\b", "", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\bCOMMENT\s*=?\s*«\d+»", "", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\bON\s+UPDATE\s+CURRENT_TIMESTAMP(\s*\(\s*\d*\s*\))?", "", RegexOptions.IgnoreCase)
        Else
            ' PostgreSQL
            sql = Regex.Replace(sql, "\bBIGSERIAL\b", "BIGINT IDENTITY(1,1)", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\bSMALLSERIAL\b", "SMALLINT IDENTITY(1,1)", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\bSERIAL\b", "INT IDENTITY(1,1)", RegexOptions.IgnoreCase)
            ' GENERATED ... AS IDENTITY [( SEQUENCE NAME x START WITH n INCREMENT BY m ... )]
            sql = Regex.Replace(sql,
                "\bGENERATED\s+(?:ALWAYS|BY\s+DEFAULT)\s+AS\s+IDENTITY(?:\s*\(([^)]*)\))?",
                Function(m As Match) As String
                    Dim opc As String = m.Groups(1).Value
                    Dim ini As Match = Regex.Match(opc, "\bSTART\s+(?:WITH\s+)?(-?\d+)", RegexOptions.IgnoreCase)
                    Dim inc As Match = Regex.Match(opc, "\bINCREMENT\s+(?:BY\s+)?(-?\d+)", RegexOptions.IgnoreCase)
                    Return "IDENTITY(" & If(ini.Success, ini.Groups(1).Value, "1") & "," &
                                         If(inc.Success, inc.Groups(1).Value, "1") & ")"
                End Function, RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\bCHARACTER\s+VARYING\b", "VARCHAR", RegexOptions.IgnoreCase)
            ' Casts  'x'::text  → 'x'
            sql = Regex.Replace(sql, "::\s*\w+(\s+\w+)?(\s*\(\s*\d+\s*\))?", "", RegexOptions.IgnoreCase)
            sql = Regex.Replace(sql, "\bNOT\s+VALID\b", "", RegexOptions.IgnoreCase)
            ' COMMENT ON ... IS '...';
            sql = Regex.Replace(sql, "COMMENT\s+ON\s+[^;]+;", "", RegexOptions.IgnoreCase)
        End If

        Return sql
    End Function

    ' ════════════════════════════════════════════════════════════
    ' PARSE CREATE TABLE
    ' ════════════════════════════════════════════════════════════
    Private Sub ParseCreateTables(sql As String, res As ImportResult, idBase As Integer, ctx As Context)
        Dim ptCreate As New Regex(
            "CREATE\s+TABLE\s+(?:\[?(\w+)\]?\s*\.\s*)?\[?(\w+)\]?\s*\(",
            RegexOptions.IgnoreCase)

        Dim m As Match = ptCreate.Match(sql)
        Do While m.Success
            Dim schema As String = If(String.IsNullOrEmpty(m.Groups(1).Value), "dbo", m.Groups(1).Value)
            ' L'esquema per defecte de PostgreSQL equival a dbo
            If ctx.Dialecte = Dialecte.PostgreSql AndAlso schema.Equals("public", StringComparison.OrdinalIgnoreCase) Then
                schema = "dbo"
            End If
            Dim nom    As String = m.Groups(2).Value.ToUpper()

            Dim startPos As Integer = m.Index + m.Length - 1  ' posició del '('
            Dim cos As String = ExtreureBloc(sql, startPos)

            If String.IsNullOrEmpty(cos) Then
                res.Errors.Add("No s'ha pogut extreure el cos de la taula: " & nom)
                m = m.NextMatch()
                Continue Do
            End If

            If res.Taules.Exists(Function(x) x.Nombre = nom AndAlso
                                     String.Equals(x.Schema, schema, StringComparison.OrdinalIgnoreCase)) Then
                res.Warnings.Add("Taula """ & nom & """ duplicada al script: s'ignora la segona definició.")
                m = m.NextMatch()
                Continue Do
            End If

            Dim t As New TablaBBDD()
            t.Id     = idBase
            t.Nombre = nom
            t.Schema = schema
            idBase  += 1

            ParseCos(cos, t, res, ctx)
            res.Taules.Add(t)

            m = m.NextMatch()
        Loop
    End Sub

    ' ── Extreu el contingut entre el '(' indicat i el ')' balancejat ─
    Private Function ExtreureBloc(sql As String, startParen As Integer) As String
        Dim depth As Integer = 0
        For i As Integer = startParen To sql.Length - 1
            Dim c As Char = sql(i)
            If c = "("c Then
                depth += 1
            ElseIf c = ")"c Then
                depth -= 1
                If depth = 0 Then
                    Return sql.Substring(startParen + 1, i - startParen - 1).Trim()
                End If
            End If
        Next i
        Return ""
    End Function

    ' ── Índex del ')' que tanca el '(' indicat (o el final del text) ─
    Private Function TancamentBloc(sql As String, startParen As Integer) As Integer
        Dim depth As Integer = 0
        For i As Integer = startParen To sql.Length - 1
            If sql(i) = "("c Then
                depth += 1
            ElseIf sql(i) = ")"c Then
                depth -= 1
                If depth = 0 Then Return i
            End If
        Next
        Return sql.Length - 1
    End Function

    ' ── Parseja el cos d'un CREATE TABLE i omple t.Fields ───────
    Private Sub ParseCos(cos As String, t As TablaBBDD, res As ImportResult, ctx As Context)
        Dim parts As List(Of String) = SepararPerComa(cos)
        Dim checksTaula As New List(Of Tuple(Of String, String))()

        ' Primer les columnes, després les restriccions de taula
        ' (una restricció pot aparèixer abans que la columna que referencia)
        Dim restriccions As New List(Of String)()

        For Each part As String In parts
            Dim p As String = part.Trim()
            If String.IsNullOrEmpty(p) Then Continue For
            Dim pUp As String = p.ToUpper()

            If pUp.StartsWith("CONSTRAINT") OrElse pUp.StartsWith("PRIMARY KEY") OrElse
               pUp.StartsWith("UNIQUE") OrElse pUp.StartsWith("FOREIGN KEY") OrElse
               pUp.StartsWith("CHECK") Then
                restriccions.Add(p)
                Continue For
            End If
            If pUp.StartsWith("INDEX") OrElse pUp.StartsWith("KEY") OrElse
               pUp.StartsWith("FULLTEXT") OrElse pUp.StartsWith("SPATIAL") Then
                Continue For  ' ignorem índexos inline
            End If

            Dim f As CampoBBDD = ParseColumna(p, t, res, ctx)
            If f IsNot Nothing Then t.Fields.Add(f)
        Next

        For Each p As String In restriccions
            Dim pUp As String = p.ToUpper()
            ' Nom opcional de la restricció
            Dim nomC As String = ""
            Dim mNom As Match = Regex.Match(p, "^CONSTRAINT\s+" & IDENT & "\s+", RegexOptions.IgnoreCase)
            Dim cosC As String = p
            If mNom.Success Then
                nomC = mNom.Groups(1).Value
                cosC = p.Substring(mNom.Length).Trim()
            End If
            Dim cosUp As String = cosC.ToUpper()

            If cosUp.StartsWith("PRIMARY KEY") Then
                For Each cNom As String In ColumnesEntreParentesis(cosC)
                    Dim f As CampoBBDD = BuscarCamp(t, cNom)
                    If f IsNot Nothing Then
                        f.EsPK    = True
                        f.NotNull = True
                    End If
                Next
            ElseIf cosUp.StartsWith("UNIQUE") Then
                Dim cols As List(Of String) = ColumnesEntreParentesis(cosC)
                If cols.Count = 1 Then
                    Dim f As CampoBBDD = BuscarCamp(t, cols(0))
                    If f IsNot Nothing Then f.EsUnique = True
                ElseIf cols.Count > 1 Then
                    res.Warnings.Add(t.Nombre & ": UNIQUE de diverses columnes (" & String.Join(", ", cols) & ") no es pot representar al model.")
                End If
            ElseIf cosUp.StartsWith("FOREIGN KEY") Then
                ParseFKTaula(cosC, nomC, t, res, ctx)
            ElseIf cosUp.StartsWith("CHECK") Then
                Dim mCk As Match = Regex.Match(cosC, "^CHECK\s*\(", RegexOptions.IgnoreCase)
                Dim expr As String = ExtreureBloc(cosC, mCk.Length - 1)
                AssignarCheck(t, nomC, expr, res)
            End If
        Next
    End Sub

    ' ── Separar per comes de nivell 0 (respecta parèntesis) ─────
    Private Function SepararPerComa(s As String) As List(Of String)
        Dim res As New List(Of String)()
        Dim depth As Integer = 0
        Dim ini   As Integer = 0
        For i As Integer = 0 To s.Length - 1
            Dim c As Char = s(i)
            If c = "("c Then
                depth += 1
            ElseIf c = ")"c Then
                depth -= 1
            ElseIf c = ","c AndAlso depth = 0 Then
                res.Add(s.Substring(ini, i - ini).Trim())
                ini = i + 1
            End If
        Next i
        If ini < s.Length Then res.Add(s.Substring(ini).Trim())
        Return res
    End Function

    ' ── Noms de columna del primer grup "( a, b DESC, [c] )" ─────
    Private Function ColumnesEntreParentesis(s As String) As List(Of String)
        Dim res As New List(Of String)()
        Dim ini As Integer = s.IndexOf("("c)
        If ini < 0 Then Return res
        Dim dins As String = ExtreureBloc(s, ini)
        For Each col As String In dins.Split(","c)
            Dim cNom As String = Regex.Replace(col.Trim(), "\[|\]|\s+ASC\b|\s+DESC\b|\s*\(\s*\d+\s*\)", "",
                                               RegexOptions.IgnoreCase).Trim().ToUpper()
            If cNom.Length > 0 Then res.Add(cNom)
        Next
        Return res
    End Function

    ' ── Parseja una definició de columna ────────────────────────
    Private Function ParseColumna(p As String, t As TablaBBDD, res As ImportResult, ctx As Context) As CampoBBDD
        Dim nomMatch As Match = Regex.Match(p, "^\[?(\w+)\]?\s+", RegexOptions.IgnoreCase)
        If Not nomMatch.Success Then Return Nothing

        Dim nomCol As String = nomMatch.Groups(1).Value.ToUpper()
        Dim resta  As String = p.Substring(nomMatch.Length).Trim()

        Dim f As New CampoBBDD()
        f.Nombre = nomCol

        ' ── Columna calculada T-SQL:  AS (expressió) [PERSISTED] ─
        Dim asMatch As Match = Regex.Match(resta, "^AS\s*\(", RegexOptions.IgnoreCase)
        If asMatch.Success Then
            f.EsCalculado    = True
            f.FormulaCalculo = ExtreureBloc(resta, asMatch.Length - 1)
            f.EsPersistido   = Regex.IsMatch(resta, "\bPERSISTED\b", RegexOptions.IgnoreCase)
            Return f
        End If

        ' ── Tipus de dada ────────────────────────────────────────
        Dim tipMatch As Match = Regex.Match(resta,
            "^\[?(\w+)\]?(\s*\(\s*(?:MAX|\d+)(?:\s*,\s*\d+)?\s*\))?",
            RegexOptions.IgnoreCase)
        If Not tipMatch.Success Then
            res.Warnings.Add("Columna """ & nomCol & """ de " & t.Nombre & ": tipus no reconegut → " & resta.Substring(0, Math.Min(40, resta.Length)))
            Return f
        End If

        Dim tipNom As String = tipMatch.Groups(1).Value.ToLower()
        Dim tipArg As String = If(tipMatch.Groups(2).Success, tipMatch.Groups(2).Value.Trim(), "")
        Dim conegut As Boolean
        f.TipoDato = MapTipus(tipNom, conegut)
        If Not conegut Then
            res.Warnings.Add("Columna """ & nomCol & """ de " & t.Nombre & ": tipus '" & tipNom & "' desconegut → VARCHAR")
        End If

        ' Longitud / precisió / escala
        If Not String.IsNullOrEmpty(tipArg) Then
            Dim inner As String = tipArg.Trim("("c, ")"c, " "c).ToUpper()
            If inner = "MAX" Then
                f.LongitudMax = True
            Else
                Dim nums() As String = inner.Split(","c)
                If nums.Length = 1 Then
                    Dim n As Integer
                    If Integer.TryParse(nums(0).Trim(), n) Then
                        If f.NecessitaPrecEsc Then
                            f.Precision = n
                            f.Escala = 0
                        Else
                            f.Longitud = n
                        End If
                    End If
                ElseIf nums.Length >= 2 Then
                    Dim p1 As Integer, s1 As Integer
                    If Integer.TryParse(nums(0).Trim(), p1) Then f.Precision = p1
                    If Integer.TryParse(nums(1).Trim(), s1) Then f.Escala = s1
                End If
            End If
        ElseIf f.NecessitaLongitud Then
            ' VARCHAR sense longitud: a T-SQL és 1; a PostgreSQL és il·limitat
            f.Longitud = 1
        End If

        resta = resta.Substring(tipMatch.Length).Trim()

        ' ── Columna generada MySQL / PostgreSQL ─────────────────
        Dim genMatch As Match = Regex.Match(resta, "^GENERATED\s+ALWAYS\s+AS\s*\(", RegexOptions.IgnoreCase)
        If genMatch.Success Then
            f.EsCalculado    = True
            f.FormulaCalculo = ExtreureBloc(resta, genMatch.Length - 1)
            f.EsPersistido   = Regex.IsMatch(resta, "\bSTORED\b", RegexOptions.IgnoreCase)
            Return f
        End If

        ' ── IDENTITY ────────────────────────────────────────────
        Dim idMatch As Match = Regex.Match(resta, "\bIDENTITY\s*\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)", RegexOptions.IgnoreCase)
        If idMatch.Success Then
            f.EsIdentity        = True
            f.IdentitySeed      = CInt(idMatch.Groups(1).Value)
            f.IdentityIncrement = CInt(idMatch.Groups(2).Value)
            resta = resta.Remove(idMatch.Index, idMatch.Length).Trim()
        ElseIf Regex.IsMatch(resta, "\bIDENTITY\b", RegexOptions.IgnoreCase) Then
            f.EsIdentity        = True
            f.IdentitySeed      = 1
            f.IdentityIncrement = 1
        End If

        If Regex.IsMatch(resta, "\bROWGUIDCOL\b", RegexOptions.IgnoreCase) Then f.EsRowGuid = True
        If Regex.IsMatch(resta, "\bFILESTREAM\b", RegexOptions.IgnoreCase) Then f.EsFileStream = True

        ' ── NOT NULL ─────────────────────────────────────────────
        If Regex.IsMatch(resta, "\bNOT\s+NULL\b", RegexOptions.IgnoreCase) Then f.NotNull = True

        ' ── PRIMARY KEY / UNIQUE de columna ──────────────────────
        If Regex.IsMatch(resta, "\bPRIMARY\s+KEY\b", RegexOptions.IgnoreCase) Then
            f.EsPK    = True
            f.NotNull = True
        ElseIf Regex.IsMatch(resta, "\bUNIQUE\b", RegexOptions.IgnoreCase) Then
            f.EsUnique = True
        End If

        ' ── DEFAULT ──────────────────────────────────────────────
        Dim dfMatch As Match = Regex.Match(resta, "\bDEFAULT\s*\(", RegexOptions.IgnoreCase)
        If dfMatch.Success Then
            f.DefaultValue = ExtreureBloc(resta, dfMatch.Index + dfMatch.Length - 1)
        Else
            ' MySQL / PostgreSQL: DEFAULT sense parèntesis
            dfMatch = Regex.Match(resta, "\bDEFAULT\s+(N?«\d+»|-?\d+(?:\.\d+)?|\w+(?:\s*\(\s*\))?)", RegexOptions.IgnoreCase)
            If dfMatch.Success Then f.DefaultValue = dfMatch.Groups(1).Value.Trim()
        End If
        If f.DefaultValue.Equals("NULL", StringComparison.OrdinalIgnoreCase) Then f.DefaultValue = ""
        If f.TipoDato = DataType.Bit Then
            If f.DefaultValue.Equals("TRUE", StringComparison.OrdinalIgnoreCase) Then f.DefaultValue = "1"
            If f.DefaultValue.Equals("FALSE", StringComparison.OrdinalIgnoreCase) Then f.DefaultValue = "0"
        End If

        ' ── CHECK de columna ─────────────────────────────────────
        Dim ckMatch As Match = Regex.Match(resta, "\bCHECK\s*\(", RegexOptions.IgnoreCase)
        If ckMatch.Success Then
            f.CheckExpression = ExtreureBloc(resta, ckMatch.Index + ckMatch.Length - 1)
        End If

        ' ── REFERENCES de columna (FK inline) ────────────────────
        Dim refMatch As Match = Regex.Match(resta,
            "\bREFERENCES\s+" & IDENT_QUALIFICAT & "\s*(?:\(\s*" & IDENT & "\s*\))?" &
            "(?:\s+ON\s+DELETE\s+" & ACCIO & ")?(?:\s+ON\s+UPDATE\s+" & ACCIO & ")?",
            RegexOptions.IgnoreCase)
        If refMatch.Success Then
            Dim nomFk As String = ""
            Dim mNomFk As Match = Regex.Match(resta, "\bCONSTRAINT\s+" & IDENT & "\s+(?:FOREIGN\s+KEY\s+)?REFERENCES\b", RegexOptions.IgnoreCase)
            If mNomFk.Success Then nomFk = mNomFk.Groups(1).Value
            ctx.FkPendents.Add(New FkPendent With {
                .Nom = nomFk, .TaulaOrigen = t.Nombre, .CampFK = nomCol,
                .TaulaDesti = refMatch.Groups(1).Value.ToUpper(),
                .CampPK = If(refMatch.Groups(2).Success, refMatch.Groups(2).Value.ToUpper(), ""),
                .OnDelete = refMatch.Groups(3).Value, .OnUpdate = refMatch.Groups(4).Value})
        End If

        ' ── MASKED WITH ─────────────────────────────────────────
        If Regex.IsMatch(resta, "\bMASKED\b", RegexOptions.IgnoreCase) Then
            Dim maskFn As Match = Regex.Match(Restaurar(resta, ctx),
                "FUNCTION\s*=\s*'([^']+)'", RegexOptions.IgnoreCase)
            If maskFn.Success Then
                Dim fn As String = maskFn.Groups(1).Value.ToLower()
                If fn.StartsWith("default") Then
                    f.DataMask = DataMaskFunction.DefaultMask
                ElseIf fn.StartsWith("email") Then
                    f.DataMask = DataMaskFunction.MaskEmail
                ElseIf fn.StartsWith("random") Then
                    f.DataMask = DataMaskFunction.MaskRandom
                ElseIf fn.StartsWith("partial") Then
                    f.DataMask = DataMaskFunction.MaskPartial
                    Dim mp As Match = Regex.Match(fn, "partial\s*\(\s*(\d+)\s*,\s*""([^""]*)""\s*,\s*(\d+)\s*\)")
                    If mp.Success Then
                        f.MaskPrefix  = CInt(mp.Groups(1).Value)
                        f.MaskPadding = maskFn.Groups(1).Value.Substring(
                            maskFn.Groups(1).Value.IndexOf(""""c) + 1, mp.Groups(2).Value.Length)
                        f.MaskSuffix  = CInt(mp.Groups(3).Value)
                    End If
                End If
            End If
        End If

        ' ── COLLATE ─────────────────────────────────────────────
        Dim colMatch As Match = Regex.Match(resta, "\bCOLLATE\s+(\w+)", RegexOptions.IgnoreCase)
        If colMatch.Success Then f.Collation = colMatch.Groups(1).Value

        Return f
    End Function

    ' ── CHECK de taula: s'assigna al camp que referencia ─────────
    Private Sub AssignarCheck(t As TablaBBDD, nomC As String, expr As String, res As ImportResult)
        If String.IsNullOrWhiteSpace(expr) Then Return
        Dim desti As CampoBBDD = Nothing

        ' Convenció pròpia: CK_<TAULA>_<CAMP>
        Dim prefix As String = "CK_" & t.Nombre & "_"
        If nomC.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            desti = BuscarCamp(t, nomC.Substring(prefix.Length))
        End If

        ' Si no, l'únic camp de la taula que apareix a l'expressió
        If desti Is Nothing Then
            Dim candidats As List(Of CampoBBDD) = t.Fields.FindAll(
                Function(f) Regex.IsMatch(expr, "(?<![\w«])\[?" & Regex.Escape(f.Nombre) & "\]?(?![\w»])", RegexOptions.IgnoreCase))
            If candidats.Count = 1 Then desti = candidats(0)
        End If

        If desti Is Nothing OrElse Not String.IsNullOrEmpty(desti.CheckExpression) Then
            res.Warnings.Add(t.Nombre & ": CHECK " & If(nomC, "") & " de diverses columnes o ambigu; no s'ha importat.")
            Return
        End If
        desti.CheckExpression = expr
    End Sub

    ' ════════════════════════════════════════════════════════════
    ' FOREIGN KEYS
    ' ════════════════════════════════════════════════════════════

    ' FOREIGN KEY (col) REFERENCES taula (col) [ON DELETE ..] [ON UPDATE ..]  (dins CREATE TABLE)
    Private Sub ParseFKTaula(cosC As String, nomC As String, t As TablaBBDD, res As ImportResult, ctx As Context)
        Dim m As Match = Regex.Match(cosC,
            "^FOREIGN\s+KEY\s*\(([^)]*)\)\s*REFERENCES\s+" & IDENT_QUALIFICAT & "\s*\(([^)]*)\)" &
            "(?:\s+ON\s+DELETE\s+" & ACCIO & ")?(?:\s+ON\s+UPDATE\s+" & ACCIO & ")?",
            RegexOptions.IgnoreCase)
        If Not m.Success Then
            res.Warnings.Add(t.Nombre & ": FOREIGN KEY no reconeguda → " & cosC.Substring(0, Math.Min(60, cosC.Length)))
            Return
        End If
        Dim colsFK As String() = m.Groups(1).Value.Split(","c)
        Dim colsPK As String() = m.Groups(3).Value.Split(","c)
        If colsFK.Length <> 1 OrElse colsPK.Length <> 1 Then
            res.Warnings.Add(t.Nombre & ": FK """ & nomC & """ de diverses columnes no es pot representar al model.")
            Return
        End If
        ctx.FkPendents.Add(New FkPendent With {
            .Nom = nomC, .TaulaOrigen = t.Nombre, .CampFK = NetejarNom(colsFK(0)),
            .TaulaDesti = m.Groups(2).Value.ToUpper(), .CampPK = NetejarNom(colsPK(0)),
            .OnDelete = m.Groups(4).Value, .OnUpdate = m.Groups(5).Value})
    End Sub

    ' ALTER TABLE [s].[T] [WITH CHECK|NOCHECK] ADD [CONSTRAINT [FK]] FOREIGN KEY (...) REFERENCES ...
    Private Sub ParseAlterForeignKeys(sql As String, ctx As Context)
        Dim ptFK As New Regex(
            "ALTER\s+TABLE\s+(?:ONLY\s+)?" & IDENT_QUALIFICAT & "\s+" &
            "(?:WITH\s+\w+\s+)?ADD\s+(?:CONSTRAINT\s+" & IDENT & "\s+)?" &
            "FOREIGN\s+KEY\s*\(\s*\[?(\w+)\]?\s*\)\s*" &
            "REFERENCES\s+" & IDENT_QUALIFICAT & "\s*\(\s*\[?(\w+)\]?\s*\)" &
            "(?:\s+ON\s+DELETE\s+" & ACCIO & ")?" &
            "(?:\s+ON\s+UPDATE\s+" & ACCIO & ")?",
            RegexOptions.IgnoreCase)

        Dim m As Match = ptFK.Match(sql)
        Do While m.Success
            ctx.FkPendents.Add(New FkPendent With {
                .TaulaOrigen = m.Groups(1).Value.ToUpper(),
                .Nom         = m.Groups(2).Value,
                .CampFK      = m.Groups(3).Value.ToUpper(),
                .TaulaDesti  = m.Groups(4).Value.ToUpper(),
                .CampPK      = m.Groups(5).Value.ToUpper(),
                .OnDelete    = m.Groups(6).Value,
                .OnUpdate    = m.Groups(7).Value})
            m = m.NextMatch()
        Loop
    End Sub

    ''' <summary>
    ''' Restriccions afegides fora del CREATE TABLE:
    '''   ALTER TABLE t ADD [CONSTRAINT x] PRIMARY KEY|UNIQUE (...) | CHECK (...)
    '''   ALTER TABLE t ADD [CONSTRAINT x] DEFAULT (...) FOR col          (SSMS)
    '''   ALTER TABLE t ALTER COLUMN c ADD IDENTITY(s,i)                   (pg_dump, ja normalitzat)
    ''' </summary>
    Private Sub ParseAlterRestriccions(sql As String, res As ImportResult)
        Dim ptAdd As New Regex(
            "ALTER\s+TABLE\s+(?:ONLY\s+)?" & IDENT_QUALIFICAT & "\s+(?:WITH\s+\w+\s+)?" &
            "ADD\s+(?:CONSTRAINT\s+" & IDENT & "\s+)?(PRIMARY\s+KEY|UNIQUE|CHECK|DEFAULT)\b",
            RegexOptions.IgnoreCase)
        Dim m As Match = ptAdd.Match(sql)
        Do While m.Success
            Dim t As TablaBBDD = res.Taules.Find(Function(x) x.Nombre = m.Groups(1).Value.ToUpper())
            Dim tipus As String = Regex.Replace(m.Groups(3).Value.ToUpper(), "\s+", " ")
            ' El parèntesi ha de seguir la paraula clau (com a molt CLUSTERED/NONCLUSTERED)
            Dim mObre As Match = Regex.Match(sql.Substring(m.Index + m.Length),
                                             "^\s*(?:NONCLUSTERED\s*|CLUSTERED\s*)?\(", RegexOptions.IgnoreCase)
            Dim obre As Integer = If(mObre.Success, m.Index + m.Length + mObre.Length - 1, -1)
            If t IsNot Nothing AndAlso obre >= 0 Then
                Dim dins As String = ExtreureBloc(sql, obre)
                Select Case tipus
                    Case "PRIMARY KEY"
                        For Each cNom As String In ColumnesEntreParentesis(sql.Substring(obre))
                            Dim f As CampoBBDD = BuscarCamp(t, cNom)
                            If f IsNot Nothing Then
                                f.EsPK = True
                                f.NotNull = True
                            End If
                        Next
                    Case "UNIQUE"
                        Dim cols As List(Of String) = ColumnesEntreParentesis(sql.Substring(obre))
                        If cols.Count = 1 AndAlso BuscarCamp(t, cols(0)) IsNot Nothing Then
                            BuscarCamp(t, cols(0)).EsUnique = True
                        End If
                    Case "CHECK"
                        AssignarCheck(t, m.Groups(2).Value, dins, res)
                    Case "DEFAULT"
                        Dim mFor As Match = Regex.Match(sql.Substring(TancamentBloc(sql, obre) + 1),
                                                        "^\s*FOR\s+" & IDENT, RegexOptions.IgnoreCase)
                        If mFor.Success Then
                            Dim f As CampoBBDD = BuscarCamp(t, mFor.Groups(1).Value)
                            If f IsNot Nothing Then f.DefaultValue = dins
                        End If
                End Select
            End If
            m = m.NextMatch()
        Loop

        Dim ptIdent As New Regex(
            "ALTER\s+TABLE\s+(?:ONLY\s+)?" & IDENT_QUALIFICAT & "\s+ALTER\s+COLUMN\s+" & IDENT &
            "\s+ADD\s+IDENTITY\s*\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)", RegexOptions.IgnoreCase)
        m = ptIdent.Match(sql)
        Do While m.Success
            Dim t As TablaBBDD = res.Taules.Find(Function(x) x.Nombre = m.Groups(1).Value.ToUpper())
            Dim f As CampoBBDD = If(t Is Nothing, Nothing, BuscarCamp(t, m.Groups(2).Value))
            If f IsNot Nothing Then
                f.EsIdentity = True
                f.IdentitySeed = CInt(m.Groups(3).Value)
                f.IdentityIncrement = CInt(m.Groups(4).Value)
            End If
            m = m.NextMatch()
        Loop
    End Sub

    Private Sub ResoldreFKs(res As ImportResult, relBase As Integer, ctx As Context)
        For Each fk As FkPendent In ctx.FkPendents
            Dim tOrigen As TablaBBDD = res.Taules.Find(Function(x) x.Nombre = fk.TaulaOrigen)
            Dim tDesti  As TablaBBDD = res.Taules.Find(Function(x) x.Nombre = fk.TaulaDesti)
            Dim nom As String = If(String.IsNullOrEmpty(fk.Nom), "FK_" & fk.TaulaOrigen & "_" & fk.CampFK, fk.Nom)

            If tOrigen Is Nothing Then
                res.Warnings.Add("FK """ & nom & """: taula origen """ & fk.TaulaOrigen & """ no trobada al script.")
                Continue For
            End If
            If tDesti Is Nothing Then
                res.Warnings.Add("FK """ & nom & """: taula destí """ & fk.TaulaDesti & """ no trobada al script.")
                Continue For
            End If

            ' REFERENCES t  (sense columna) → la PK de la taula destí
            Dim campPK As String = fk.CampPK
            If String.IsNullOrEmpty(campPK) AndAlso tDesti.PKField IsNot Nothing Then campPK = tDesti.PKField.Nombre

            Dim fFK As CampoBBDD = BuscarCamp(tOrigen, fk.CampFK)
            If fFK Is Nothing OrElse BuscarCamp(tDesti, campPK) Is Nothing Then
                res.Warnings.Add("FK """ & nom & """: columna no trobada (" & fk.CampFK & " → " & campPK & ").")
                Continue For
            End If
            If res.Relacions.Exists(Function(r) r.TablaOrigenId = tOrigen.Id AndAlso r.CampoFKNombre = fFK.Nombre AndAlso
                                                r.TablaDestinoId = tDesti.Id) Then
                Continue For  ' ja definida (p.ex. inline i també amb ALTER TABLE)
            End If

            fFK.EsFK = True

            Dim rel As New RelacionBBDD()
            rel.Id             = relBase
            rel.Nombre         = nom
            rel.TablaOrigenId  = tOrigen.Id
            rel.CampoFKNombre  = fFK.Nombre
            rel.TablaDestinoId = tDesti.Id
            rel.CampoPKNombre  = campPK
            rel.TipoRelacion   = CardinalityType.ManyToOne
            rel.OnDelete       = MapAccio(fk.OnDelete)
            rel.OnUpdate       = MapAccio(fk.OnUpdate)
            rel.CrearIndexFK   = True
            relBase           += 1
            res.Relacions.Add(rel)
        Next
    End Sub

    ' ════════════════════════════════════════════════════════════
    ' HELPERS
    ' ════════════════════════════════════════════════════════════
    Private Function NetejarNom(s As String) As String
        Return s.Trim().Trim("["c, "]"c).Trim().ToUpper()
    End Function

    Private Function BuscarCamp(t As TablaBBDD, nom As String) As CampoBBDD
        Return t.Fields.Find(Function(f) String.Equals(f.Nombre, nom, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Function MapTipus(sqlType As String, ByRef conegut As Boolean) As DataType
        conegut = True
        Select Case sqlType.ToLower()
            Case "bit"              : Return DataType.Bit
            Case "tinyint"          : Return DataType.TinyInt
            Case "smallint"         : Return DataType.SmallInt
            Case "int"              : Return DataType.DbInt
            Case "bigint"           : Return DataType.BigInt
            Case "decimal", "dec"   : Return DataType.DbDecimal
            Case "numeric"          : Return DataType.DbNumeric
            Case "money"            : Return DataType.Money
            Case "smallmoney"       : Return DataType.SmallMoney
            Case "float"            : Return DataType.DbFloat
            Case "real"             : Return DataType.DbReal
            Case "char", "character" : Return DataType.DbChar
            Case "varchar"          : Return DataType.VarChar
            Case "text"             : Return DataType.DbText
            Case "nchar"            : Return DataType.NChar
            Case "nvarchar", "sysname" : Return DataType.NVarChar
            Case "ntext"            : Return DataType.NText
            Case "binary"           : Return DataType.DbBinary
            Case "varbinary"        : Return DataType.VarBinary
            Case "image"            : Return DataType.DbImage
            Case "date"             : Return DataType.DateOnly
            Case "time"             : Return DataType.TimeOnly
            Case "datetime"         : Return DataType.DbDateTime
            Case "datetime2"        : Return DataType.DateTime2
            Case "smalldatetime"    : Return DataType.SmallDateTime
            Case "datetimeoffset"   : Return DataType.DateTimeOffset
            Case "timestamp", "rowversion" : Return DataType.RowVersion
            Case "uniqueidentifier" : Return DataType.UniqueIdentifier
            Case "xml"              : Return DataType.DbXml
            Case "geography"        : Return DataType.DbGeography
            Case "geometry"         : Return DataType.DbGeometry
            Case "hierarchyid"      : Return DataType.HierarchyId
            Case "sql_variant"      : Return DataType.SqlVariant
            Case "json"             : Return DataType.NVarChar
            Case "year"             : Return DataType.SmallInt
            Case Else
                conegut = False
                Return DataType.VarChar
        End Select
    End Function

    ' ── Mapeig ON DELETE/UPDATE text → enum ─────────────────────
    Private Function MapAccio(v As String) As OnDeleteUpdateAction
        Select Case If(v, "").ToUpper().Replace(" ", "")
            Case "CASCADE"      : Return OnDeleteUpdateAction.DoCascade
            Case "SETNULL"      : Return OnDeleteUpdateAction.SetNull
            Case "SETDEFAULT"   : Return OnDeleteUpdateAction.SetDefault
            Case "RESTRICT"     : Return OnDeleteUpdateAction.DoRestrict
            Case Else           : Return OnDeleteUpdateAction.NoAction
        End Select
    End Function

End Module
