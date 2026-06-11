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
Imports System.Text.RegularExpressions

' ============================================================
' SqlScriptImporter.vb  —  DB-Core Holographic
' Parseja un script DDL T-SQL i retorna un ProyectoBBDD.
'
' Suporta:
'   CREATE TABLE [schema].[Nom] ( ... )
'   ALTER TABLE ... ADD CONSTRAINT [FK_...] FOREIGN KEY ...
'       REFERENCES ... ON DELETE ... ON UPDATE ...
'   IDENTITY(seed,inc), NOT NULL, NULL
'   CONSTRAINT [PK_...] PRIMARY KEY (col)
'   CONSTRAINT [UQ_...] UNIQUE (col)
'   CONSTRAINT [DF_...] DEFAULT (val)
'   CONSTRAINT [CK_...] CHECK (expr)
'   Tipus SQL Server complets (varchar(n), decimal(p,s), varchar(max)...)
'
' Tolerant: ignora comentaris --, /* */, GO, USE, SET, IF, sp_add...
' ============================================================
Public Module SqlScriptImporter

    ' ── Resultat de la importació ────────────────────────────────
    Public Class ImportResult
        Public Property Taules    As New List(Of TablaBBDD)()
        Public Property Relacions As New List(Of RelacionBBDD)()
        Public Property Errors    As New List(Of String)()
        Public Property Warnings  As New List(Of String)()
    End Class

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

        ' 0. Detectar dialecte i normalitzar a T-SQL
        Dim net As String = NetScript(NormalitzarDialecte(script))

        ' 1. Extreure blocs CREATE TABLE
        ParseCreateTables(net, res, idBase)

        ' 2. Extreure ALTER TABLE ... ADD CONSTRAINT FK
        ParseForeignKeys(net, res, relBase)

        Return res
    End Function

    ' ════════════════════════════════════════════════════════════
    ' NORMALITZADOR DE DIALECTE
    ' Transforma MySQL i PostgreSQL a sintaxi T-SQL equivalent
    ' perquè el parser existent no necessiti canvis.
    ' ════════════════════════════════════════════════════════════
    Private Function NormalitzarDialecte(sql As String) As String
        ' ── MySQL / MariaDB ──────────────────────────────────────
        ' Backtics → claudàtors
        sql = Regex.Replace(sql, "`(\w+)`", "[$1]")
        ' AUTO_INCREMENT → IDENTITY(1,1)
        sql = Regex.Replace(sql, "\bAUTO_INCREMENT\b", "IDENTITY(1,1)", RegexOptions.IgnoreCase)
        ' ENGINE=InnoDB ... → ignorar (eliminar fins al ; o fi de línia)
        sql = Regex.Replace(sql, "\)\s*ENGINE\s*=\s*\w+[^;]*", ")", RegexOptions.IgnoreCase)
        ' COMMENT='text' inline en columna → capturar com a descripció (guardarem al Warning per ara)
        ' (no podem injectar al model aquí — el parser de columna ho farà)
        ' CHARACTER SET utf8mb4 → ignorar
        sql = Regex.Replace(sql, "\s*CHARACTER\s+SET\s+\w+", "", RegexOptions.IgnoreCase)
        ' COLLATE xxx → ignorar
        sql = Regex.Replace(sql, "\s*COLLATE\s+\w+", "", RegexOptions.IgnoreCase)
        ' TINYINT(1) → BIT
        sql = Regex.Replace(sql, "\bTINYINT\s*\(\s*1\s*\)", "BIT", RegexOptions.IgnoreCase)
        ' UNSIGNED → ignorar
        sql = Regex.Replace(sql, "\s*UNSIGNED\b", "", RegexOptions.IgnoreCase)
        ' LONGTEXT → NVARCHAR(MAX)
        sql = Regex.Replace(sql, "\bLONGTEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase)
        ' LONGBLOB → VARBINARY(MAX)
        sql = Regex.Replace(sql, "\bLONGBLOB\b", "VARBINARY(MAX)", RegexOptions.IgnoreCase)
        ' MEDIUMTEXT / TEXT → NVARCHAR(MAX)
        sql = Regex.Replace(sql, "\bMEDIUMTEXT\b|\bTEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase)
        ' MEDIUMBLOB / BLOB → VARBINARY(MAX)
        sql = Regex.Replace(sql, "\bMEDIUMBLOB\b|\bBLOB\b", "VARBINARY(MAX)", RegexOptions.IgnoreCase)
        ' INDEX `nom` (col) inline dins CREATE TABLE → ignorar
        sql = Regex.Replace(sql, ",\s*(?:UNIQUE\s+)?(?:KEY|INDEX)\s+`?\w+`?\s*\([^)]+\)", "", RegexOptions.IgnoreCase)
        ' IF NOT EXISTS → ignorar
        sql = Regex.Replace(sql, "\bIF\s+NOT\s+EXISTS\b", "", RegexOptions.IgnoreCase)

        ' ── PostgreSQL ───────────────────────────────────────────
        ' Cometes dobles → claudàtors
        sql = Regex.Replace(sql, """(\w+)""", "[$1]")
        ' SERIAL → INT IDENTITY(1,1)
        sql = Regex.Replace(sql, "\bBIGSERIAL\b", "BIGINT IDENTITY(1,1)", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bSMALLSERIAL\b", "SMALLINT IDENTITY(1,1)", RegexOptions.IgnoreCase)
        sql = Regex.Replace(sql, "\bSERIAL\b", "INT IDENTITY(1,1)", RegexOptions.IgnoreCase)
        ' GENERATED ALWAYS AS IDENTITY → IDENTITY(1,1)
        sql = Regex.Replace(sql, "\bGENERATED\s+ALWAYS\s+AS\s+IDENTITY\b", "IDENTITY(1,1)", RegexOptions.IgnoreCase)
        ' BOOLEAN → BIT
        sql = Regex.Replace(sql, "\bBOOLEAN\b|\bBOOL\b", "BIT", RegexOptions.IgnoreCase)
        ' BYTEA → VARBINARY(MAX)
        sql = Regex.Replace(sql, "\bBYTEA\b", "VARBINARY(MAX)", RegexOptions.IgnoreCase)
        ' TIMESTAMPTZ → DATETIMEOFFSET
        sql = Regex.Replace(sql, "\bTIMESTAMPTZ\b", "DATETIMEOFFSET", RegexOptions.IgnoreCase)
        ' TIMESTAMP(n) → DATETIME2
        sql = Regex.Replace(sql, "\bTIMESTAMP\s*\(\s*\d+\s*\)", "DATETIME2", RegexOptions.IgnoreCase)
        ' TIMESTAMP → DATETIME2
        sql = Regex.Replace(sql, "\bTIMESTAMP\b", "DATETIME2", RegexOptions.IgnoreCase)
        ' DOUBLE PRECISION → FLOAT
        sql = Regex.Replace(sql, "\bDOUBLE\s+PRECISION\b", "FLOAT", RegexOptions.IgnoreCase)
        ' UUID → UNIQUEIDENTIFIER
        sql = Regex.Replace(sql, "\bUUID\b", "UNIQUEIDENTIFIER", RegexOptions.IgnoreCase)
        ' COMMENT ON TABLE/COLUMN ... → ignorar (s'eliminen amb els comentaris)
        sql = Regex.Replace(sql, "COMMENT\s+ON\s+(?:TABLE|COLUMN)[^;]+;", "", RegexOptions.IgnoreCase)
        ' INTEGER → INT (sinònim)
        sql = Regex.Replace(sql, "\bINTEGER\b", "INT", RegexOptions.IgnoreCase)

        Return sql
    End Function

    ' ════════════════════════════════════════════════════════════
    ' NETEJA DEL SCRIPT
    ' ════════════════════════════════════════════════════════════
    Private Function NetScript(sql As String) As String
        ' Eliminar comentaris de bloc  /* ... */
        sql = Regex.Replace(sql, "/\*.*?\*/", " ", RegexOptions.Singleline)
        ' Eliminar comentaris de línia  -- ...
        sql = Regex.Replace(sql, "--[^\r\n]*", " ")
        ' Normalitzar salts de línia
        sql = sql.Replace(vbCrLf, " ").Replace(vbCr, " ").Replace(vbLf, " ")
        ' Comprimir espais múltiples
        sql = Regex.Replace(sql, "\s+", " ")
        Return sql.Trim()
    End Function

    ' ════════════════════════════════════════════════════════════
    ' PARSE CREATE TABLE
    ' ════════════════════════════════════════════════════════════
    Private Sub ParseCreateTables(sql As String, res As ImportResult, idBase As Integer)
        ' Regex per trobar CREATE TABLE [schema].[nom] ( ... )
        ' Captura el cos entre parèntesis balancejats amb SubBloc()
        Dim ptCreate As New Regex(
            "CREATE\s+TABLE\s+(?:\[?(\w+)\]?\s*\.\s*)?\[?(\w+)\]?\s*\(",
            RegexOptions.IgnoreCase)

        Dim m As Match = ptCreate.Match(sql)
        Do While m.Success
            Dim schema As String = If(String.IsNullOrEmpty(m.Groups(1).Value), "dbo", m.Groups(1).Value)
            Dim nom    As String = m.Groups(2).Value.ToUpper()

            ' Extreure el cos de la taula (entre parèntesis balancejats)
            Dim startPos As Integer = m.Index + m.Length - 1  ' posició del '('
            Dim cos As String = ExtreureBloc(sql, startPos)

            If String.IsNullOrEmpty(cos) Then
                res.Errors.Add("No s'ha pogut extreure el cos de la taula: " & nom)
                m = m.NextMatch()
                Continue Do
            End If

            Dim t As New TablaBBDD()
            t.Id     = idBase
            t.Nombre = nom
            t.Schema = schema
            idBase  += 1

            ParseCos(cos, t, res)
            res.Taules.Add(t)

            m = m.NextMatch()
        Loop
    End Sub

    ' ── Extreu el contingut entre el primer '(' i el ')' balancejat ─
    Private Function ExtreureBloc(sql As String, startParen As Integer) As String
        Dim depth As Integer = 0
        Dim ini   As Integer = startParen
        For i As Integer = startParen To sql.Length - 1
            Dim c As Char = sql(i)
            If c = "("c Then
                depth += 1
            ElseIf c = ")"c Then
                depth -= 1
                If depth = 0 Then
                    ' Retorna el contingut interior (sense els parèntesis externs)
                    Return sql.Substring(ini + 1, i - ini - 1).Trim()
                End If
            End If
        Next i
        Return ""
    End Function

    ' ── Parseja el cos d'un CREATE TABLE i omple t.Fields ───────
    Private Sub ParseCos(cos As String, t As TablaBBDD, res As ImportResult)
        ' Separar per comes al nivell 0 (ignorant comes dins parèntesis)
        Dim parts As List(Of String) = SepararPerComa(cos)

        For Each part As String In parts
            Dim p As String = part.Trim()
            If String.IsNullOrEmpty(p) Then Continue For

            Dim pUp As String = p.ToUpper()

            ' ── Constraint de taula ──────────────────────────────
            If pUp.StartsWith("CONSTRAINT") Then
                ParseConstraintTaula(p, t, res)
                Continue For
            End If
            If pUp.StartsWith("PRIMARY KEY") Then
                ' PRIMARY KEY sense nom de constraint
                ParsePKInline(p, t)
                Continue For
            End If
            If pUp.StartsWith("UNIQUE") Then
                ParseUniqueInline(p, t)
                Continue For
            End If
            If pUp.StartsWith("INDEX") OrElse pUp.StartsWith("KEY") Then
                Continue For  ' ignorem índexos inline
            End If
            If pUp.StartsWith("CHECK") Then
                Continue For  ' CHECK a nivell de taula: ignorem
            End If

            ' ── Definició de columna ─────────────────────────────
            Dim f As CampoBBDD = ParseColumna(p, t, res)
            If f IsNot Nothing Then
                t.Fields.Add(f)
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
        If ini < s.Length Then
            res.Add(s.Substring(ini).Trim())
        End If
        Return res
    End Function

    ' ── Parseja una definició de columna ────────────────────────
    Private Function ParseColumna(p As String, t As TablaBBDD, res As ImportResult) As CampoBBDD
        ' Extreure nom de columna: [NOM] o NOM
        Dim nomMatch As Match = Regex.Match(p, "^\[?(\w+)\]?\s+", RegexOptions.IgnoreCase)
        If Not nomMatch.Success Then Return Nothing

        Dim nomCol As String = nomMatch.Groups(1).Value.ToUpper()
        Dim resta  As String = p.Substring(nomMatch.Length).Trim()

        Dim f As New CampoBBDD()
        f.Nombre = nomCol

        ' ── Columna calculada:  AS (expressió) ──────────────────
        Dim asMatch As Match = Regex.Match(resta, "^AS\s*\(", RegexOptions.IgnoreCase)
        If asMatch.Success Then
            Dim exprBloc As String = ExtreureBloc(resta, asMatch.Index + asMatch.Length - 1)
            f.EsCalculado    = True
            f.FormulaCalculo = exprBloc
            If Regex.IsMatch(resta, "\bPERSISTED\b", RegexOptions.IgnoreCase) Then
                f.EsPersistido = True
            End If
            Return f
        End If

        ' ── Tipus de dada ────────────────────────────────────────
        Dim tipMatch As Match = Regex.Match(resta,
            "^(\w+)(\s*\(\s*(?:MAX|\d+)(?:\s*,\s*\d+)?\s*\))?",
            RegexOptions.IgnoreCase)
        If Not tipMatch.Success Then
            res.Warnings.Add("Columna """ & nomCol & """ de " & t.Nombre & ": tipus no reconegut → " & resta.Substring(0, Math.Min(40, resta.Length)))
            Return f
        End If

        Dim tipNom As String = tipMatch.Groups(1).Value.ToLower()
        Dim tipArg As String = If(tipMatch.Groups(2).Success, tipMatch.Groups(2).Value.Trim(), "")
        f.TipoDato = MapTipus(tipNom)

        ' Longitud / precisió / escala
        If Not String.IsNullOrEmpty(tipArg) Then
            Dim inner As String = tipArg.Trim("("c, ")"c, " "c).ToUpper()
            If inner = "MAX" Then
                f.LongitudMax = True
            Else
                Dim nums() As String = inner.Split(","c)
                If nums.Length = 1 Then
                    Dim n As Integer
                    If Integer.TryParse(nums(0).Trim(), n) Then f.Longitud = n
                ElseIf nums.Length >= 2 Then
                    Dim p1 As Integer, s1 As Integer
                    If Integer.TryParse(nums(0).Trim(), p1) Then f.Precision = p1
                    If Integer.TryParse(nums(1).Trim(), s1) Then f.Escala = s1
                End If
            End If
        End If

        resta = resta.Substring(tipMatch.Length).Trim()

        ' ── IDENTITY ────────────────────────────────────────────
        Dim idMatch As Match = Regex.Match(resta, "\bIDENTITY\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.IgnoreCase)
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

        ' ── ROWGUIDCOL ──────────────────────────────────────────
        If Regex.IsMatch(resta, "\bROWGUIDCOL\b", RegexOptions.IgnoreCase) Then
            f.EsRowGuid = True
        End If

        ' ── NOT NULL / NULL ──────────────────────────────────────
        If Regex.IsMatch(resta, "\bNOT\s+NULL\b", RegexOptions.IgnoreCase) Then
            f.NotNull = True
        End If

        ' ── CONSTRAINT DEFAULT ───────────────────────────────────
        Dim dfMatch As Match = Regex.Match(resta,
            "CONSTRAINT\s+\[?\w+\]?\s+DEFAULT\s*\(([^)]*(?:\([^)]*\)[^)]*)*)\)",
            RegexOptions.IgnoreCase)
        If Not dfMatch.Success Then
            ' DEFAULT sense nom de constraint
            dfMatch = Regex.Match(resta, "\bDEFAULT\s*\(([^)]*(?:\([^)]*\)[^)]*)*)\)", RegexOptions.IgnoreCase)
        End If
        If dfMatch.Success Then
            f.DefaultValue = dfMatch.Groups(1).Value.Trim()
        End If

        ' ── CONSTRAINT CHECK ────────────────────────────────────
        Dim ckMatch As Match = Regex.Match(resta,
            "CONSTRAINT\s+\[?\w+\]?\s+CHECK\s*\((.+)\)",
            RegexOptions.IgnoreCase)
        If ckMatch.Success Then
            f.CheckExpression = ckMatch.Groups(1).Value.Trim()
        End If

        ' ── MASKED WITH ─────────────────────────────────────────
        If Regex.IsMatch(resta, "\bMASKED\b", RegexOptions.IgnoreCase) Then
            Dim maskFn As Match = Regex.Match(resta,
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
                End If
            End If
        End If

        Return f
    End Function

    ' ── Parseja CONSTRAINT [nom] ... a nivell de taula ──────────
    Private Sub ParseConstraintTaula(p As String, t As TablaBBDD, res As ImportResult)
        ' CONSTRAINT [PK_xxx] PRIMARY KEY [CLUSTERED] ([col] ASC)
        Dim pkMatch As Match = Regex.Match(p,
            "CONSTRAINT\s+\[?(\w+)\]?\s+PRIMARY\s+KEY[^(]*\(([^)]+)\)",
            RegexOptions.IgnoreCase)
        If pkMatch.Success Then
            ' Extreure noms de columnes (pot ser composita, agafem la primera per ara)
            Dim cols() As String = pkMatch.Groups(2).Value.Split(","c)
            For Each col As String In cols
                Dim cNom As String = Regex.Replace(col.Trim(), "\[|\]|\s+ASC|\s+DESC", "",
                                                    RegexOptions.IgnoreCase).Trim().ToUpper()
                For Each f As CampoBBDD In t.Fields
                    If f.Nombre = cNom Then
                        f.EsPK    = True
                        f.NotNull = True
                    End If
                Next
            Next
            Return
        End If

        ' CONSTRAINT [UQ_xxx] UNIQUE ([col])
        Dim uqMatch As Match = Regex.Match(p,
            "CONSTRAINT\s+\[?(\w+)\]?\s+UNIQUE[^(]*\(([^)]+)\)",
            RegexOptions.IgnoreCase)
        If uqMatch.Success Then
            Dim cols() As String = uqMatch.Groups(2).Value.Split(","c)
            For Each col As String In cols
                Dim cNom As String = Regex.Replace(col.Trim(), "\[|\]|\s+ASC|\s+DESC", "",
                                                    RegexOptions.IgnoreCase).Trim().ToUpper()
                For Each f As CampoBBDD In t.Fields
                    If f.Nombre = cNom Then f.EsUnique = True
                Next
            Next
            Return
        End If

        ' CONSTRAINT [CK_xxx] CHECK (expr) — ignorem per ara
        ' CONSTRAINT [DF_xxx] DEFAULT (val) ON [col] — poc freqüent, ignorem
    End Sub

    ' ── PRIMARY KEY inline sense nom de constraint ──────────────
    Private Sub ParsePKInline(p As String, t As TablaBBDD)
        Dim m As Match = Regex.Match(p, "\(([^)]+)\)", RegexOptions.IgnoreCase)
        If Not m.Success Then Return
        Dim cols() As String = m.Groups(1).Value.Split(","c)
        For Each col As String In cols
            Dim cNom As String = Regex.Replace(col.Trim(), "\[|\]|\s+ASC|\s+DESC", "",
                                                RegexOptions.IgnoreCase).Trim().ToUpper()
            For Each f As CampoBBDD In t.Fields
                If f.Nombre = cNom Then
                    f.EsPK    = True
                    f.NotNull = True
                End If
            Next
        Next
    End Sub

    ' ── UNIQUE inline sense nom de constraint ────────────────────
    Private Sub ParseUniqueInline(p As String, t As TablaBBDD)
        Dim m As Match = Regex.Match(p, "\(([^)]+)\)", RegexOptions.IgnoreCase)
        If Not m.Success Then Return
        Dim cols() As String = m.Groups(1).Value.Split(","c)
        For Each col As String In cols
            Dim cNom As String = Regex.Replace(col.Trim(), "\[|\]|\s+ASC|\s+DESC", "",
                                                RegexOptions.IgnoreCase).Trim().ToUpper()
            For Each f As CampoBBDD In t.Fields
                If f.Nombre = cNom Then f.EsUnique = True
            Next
        Next
    End Sub

    ' ════════════════════════════════════════════════════════════
    ' PARSE FOREIGN KEYS
    ' ALTER TABLE [s].[T] WITH CHECK ADD CONSTRAINT [FK_...]
    '     FOREIGN KEY ([col]) REFERENCES [s].[T2] ([col2])
    '     ON DELETE ... ON UPDATE ...
    ' ════════════════════════════════════════════════════════════
    Private Sub ParseForeignKeys(sql As String, res As ImportResult, relBase As Integer)
        Dim ptFK As New Regex(
            "ALTER\s+TABLE\s+(?:\[?\w+\]?\s*\.\s*)?\[?(\w+)\]?\s+" &
            "(?:WITH\s+\w+\s+)?ADD\s+CONSTRAINT\s+\[?(\w+)\]?\s+" &
            "FOREIGN\s+KEY\s*\(\[?(\w+)\]?\)\s+" &
            "REFERENCES\s+(?:\[?\w+\]?\s*\.\s*)?\[?(\w+)\]?\s*\(\[?(\w+)\]?\)" &
            "(?:\s+ON\s+DELETE\s+(NO\s+ACTION|CASCADE|SET\s+NULL|SET\s+DEFAULT|RESTRICT))?" &
            "(?:\s+ON\s+UPDATE\s+(NO\s+ACTION|CASCADE|SET\s+NULL|SET\s+DEFAULT|RESTRICT))?",
            RegexOptions.IgnoreCase)

        Dim m As Match = ptFK.Match(sql)
        Do While m.Success
            Dim fkTaula  As String = m.Groups(1).Value.ToUpper()
            Dim fkNom    As String = m.Groups(2).Value
            Dim fkCol    As String = m.Groups(3).Value.ToUpper()
            Dim pkTaula  As String = m.Groups(4).Value.ToUpper()
            Dim pkCol    As String = m.Groups(5).Value.ToUpper()
            Dim onDel    As String = If(m.Groups(6).Success, m.Groups(6).Value.Trim().ToUpper(), "NO ACTION")
            Dim onUpd    As String = If(m.Groups(7).Success, m.Groups(7).Value.Trim().ToUpper(), "NO ACTION")

            ' Buscar les taules per nom
            Dim tOrigen  As TablaBBDD = Nothing
            Dim tDesti   As TablaBBDD = Nothing
            For Each t As TablaBBDD In res.Taules
                If t.Nombre = fkTaula Then tOrigen = t
                If t.Nombre = pkTaula  Then tDesti  = t
            Next
            If tOrigen Is Nothing Then
                res.Warnings.Add("FK """ & fkNom & """: taula origen """ & fkTaula & """ no trobada al script.")
                m = m.NextMatch()
                Continue Do
            End If
            If tDesti Is Nothing Then
                res.Warnings.Add("FK """ & fkNom & """: taula destí """ & pkTaula & """ no trobada al script.")
                m = m.NextMatch()
                Continue Do
            End If

            ' Marcar el camp FK
            For Each f As CampoBBDD In tOrigen.Fields
                If f.Nombre = fkCol Then f.EsFK = True
            Next

            Dim rel As New RelacionBBDD()
            rel.Id             = relBase
            rel.Nombre         = fkNom
            rel.TablaOrigenId  = tOrigen.Id
            rel.CampoFKNombre  = fkCol
            rel.TablaDestinoId = tDesti.Id
            rel.CampoPKNombre  = pkCol
            rel.TipoRelacion   = CardinalityType.ManyToOne
            rel.OnDelete       = MapAccio(onDel)
            rel.OnUpdate       = MapAccio(onUpd)
            rel.CrearIndexFK   = True
            relBase           += 1

            res.Relacions.Add(rel)
            m = m.NextMatch()
        Loop
    End Sub

    ' ════════════════════════════════════════════════════════════
    ' MAPEIG DE TIPUS SQL → DataType
    ' ════════════════════════════════════════════════════════════
    Private Function MapTipus(sqlType As String) As DataType
        Select Case sqlType.ToLower()
            Case "bit"              : Return DataType.Bit
            Case "tinyint"          : Return DataType.TinyInt
            Case "smallint"         : Return DataType.SmallInt
            Case "int"              : Return DataType.DbInt
            Case "bigint"           : Return DataType.BigInt
            Case "decimal"          : Return DataType.DbDecimal
            Case "numeric"          : Return DataType.DbNumeric
            Case "money"            : Return DataType.Money
            Case "smallmoney"       : Return DataType.SmallMoney
            Case "float"            : Return DataType.DbFloat
            Case "real"             : Return DataType.DbReal
            Case "char"             : Return DataType.DbChar
            Case "varchar"          : Return DataType.VarChar
            Case "text"             : Return DataType.DbText
            Case "nchar"            : Return DataType.NChar
            Case "nvarchar"         : Return DataType.NVarChar
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
            Case "timestamp",
                 "rowversion"       : Return DataType.RowVersion
            Case "uniqueidentifier" : Return DataType.UniqueIdentifier
            Case "xml"              : Return DataType.DbXml
            Case "geography"        : Return DataType.DbGeography
            Case "geometry"         : Return DataType.DbGeometry
            Case "hierarchyid"      : Return DataType.HierarchyId
            Case "sql_variant"      : Return DataType.SqlVariant
            Case "double"           : Return DataType.DbFloat
            Case "integer"          : Return DataType.DbInt
            Case "boolean", "bool"  : Return DataType.Bit
            Case "uuid"             : Return DataType.UniqueIdentifier
            Case "bytea"            : Return DataType.VarBinary
            Case "serial"           : Return DataType.DbInt
            Case "bigserial"        : Return DataType.BigInt
            Case "smallserial"      : Return DataType.SmallInt
            Case Else               : Return DataType.VarChar
        End Select
    End Function

    ' ── Mapeig ON DELETE/UPDATE text → enum ─────────────────────
    Private Function MapAccio(v As String) As OnDeleteUpdateAction
        Select Case v.Replace(" ", "")
            Case "CASCADE"      : Return OnDeleteUpdateAction.DoCascade
            Case "SETNULL"      : Return OnDeleteUpdateAction.SetNull
            Case "SETDEFAULT"   : Return OnDeleteUpdateAction.SetDefault
            Case "RESTRICT"     : Return OnDeleteUpdateAction.DoRestrict
            Case Else           : Return OnDeleteUpdateAction.NoAction
        End Select
    End Function

End Module
