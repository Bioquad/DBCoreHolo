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

' ============================================================
' TSqlExporter.vb — Generador DDL per a SQL Server (T-SQL)
'
' És l'ÚNICA font de T-SQL de l'aplicació: l'utilitzen
'   - l'exportació de scripts .sql            (Generar)
'   - la creació de fitxers .mdf via LocalDB  (MdfExporter)
'   - el desplegament a servidors en xarxa    (SqlServerConnector)
'
' Els mètodes Generar* retornen sentències SENSE separadors "GO",
' perquè es puguin executar directament amb SqlCommand. Només
' Generar() (script complet) afegeix els "GO" entre lots.
'
' Convencions de noms de restriccions (únics dins l'esquema):
'   PK_<Taula>   UQ_<Taula>_<Camp>   CK_<Taula>_<Camp>
'   DF_<Taula>_<Camp>   IX_<Taula>_<CampFK>
' ============================================================
Public Class TSqlExporter

    ' ════════════════════════════════════════════════════════
    ' SCRIPT COMPLET
    ' ════════════════════════════════════════════════════════
    Public Function Generar(p As ProyectoBBDD) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("-- ============================================")
        sb.AppendLine("-- DDL T-SQL generat per Holografic DB")
        sb.AppendLine("-- Projecte : " & UnaLinia(p.Nombre))
        sb.AppendLine("-- Motor    : SQL Server (T-SQL)")
        sb.AppendLine("-- Data     : " & DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
        sb.AppendLine("-- ============================================")
        sb.AppendLine()

        For Each esq As String In EsquemesNecessaris(p.Taules)
            AfegirLot(sb, GenerarCrearEsquema(esq))
        Next

        For Each t As TablaBBDD In p.Taules
            AfegirLot(sb, GenerarTaula(t))
        Next

        For Each r As RelacionBBDD In p.Relacions
            Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
            Dim tt As TablaBBDD = BuscarTaula(p, r.TablaDestinoId)
            If ft IsNot Nothing AndAlso tt IsNot Nothing Then
                AfegirLot(sb, GenerarAlterFK(r, ft, tt))
            End If
        Next

        For Each r As RelacionBBDD In p.Relacions
            If r.CrearIndexFK Then
                Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
                If ft IsNot Nothing Then AfegirLot(sb, GenerarIndexFK(r, ft))
            End If
        Next

        For Each t As TablaBBDD In p.Taules
            If Not String.IsNullOrEmpty(t.Descripcion) Then
                AfegirLot(sb, GenerarExtPropTaula(t))
            End If
            For Each f As CampoBBDD In t.Fields
                If Not String.IsNullOrEmpty(f.Descripcion) Then
                    AfegirLot(sb, GenerarExtPropCamp(t, f))
                End If
            Next
        Next

        ' Descripció de relacions (via extended property a nivell de constraint)
        For Each r As RelacionBBDD In p.Relacions
            If Not String.IsNullOrEmpty(r.Descripcion) Then
                Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
                If ft IsNot Nothing Then AfegirLot(sb, GenerarExtPropRelacio(r, ft))
            End If
        Next

        AfegirNotesDisseny(sb, p)
        Return sb.ToString()
    End Function

    ' ════════════════════════════════════════════════════════
    ' SENTÈNCIES INDIVIDUALS (sense GO)
    ' ════════════════════════════════════════════════════════

    ''' <summary>CREATE TABLE complet amb PK (simple o composta), UNIQUE i CHECK.</summary>
    Public Function GenerarTaula(t As TablaBBDD) As String
        Dim lines As New List(Of String)()

        For Each f As CampoBBDD In t.Fields
            lines.Add("    " & GenerarCamp(t, f))
        Next

        Dim pks As List(Of CampoBBDD) = t.PKFields
        If pks.Count > 0 Then
            Dim cols As New List(Of String)()
            For Each pk As CampoBBDD In pks
                cols.Add(Q(pk.Nombre) & " ASC")
            Next
            lines.Add("    CONSTRAINT " & Q("PK_" & t.Nombre) &
                      " PRIMARY KEY CLUSTERED (" & String.Join(", ", cols) & ")")
        End If

        For Each f As CampoBBDD In t.Fields
            If f.EsUnique AndAlso Not (f.EsPK AndAlso pks.Count = 1) Then
                lines.Add("    CONSTRAINT " & Q("UQ_" & t.Nombre & "_" & f.Nombre) &
                          " UNIQUE (" & Q(f.Nombre) & ")")
            End If
        Next

        For Each f As CampoBBDD In t.Fields
            If Not String.IsNullOrWhiteSpace(f.CheckExpression) Then
                lines.Add("    CONSTRAINT " & Q("CK_" & t.Nombre & "_" & f.Nombre) &
                          " CHECK (" & f.CheckExpression.Trim() & ")")
            End If
        Next

        Dim sb As New StringBuilder()
        sb.AppendLine("CREATE TABLE " & NomTaula(t) & " (")
        sb.AppendLine(String.Join("," & Environment.NewLine, lines))
        sb.Append(");")
        Return sb.ToString()
    End Function

    ''' <summary>Definició d'una columna (per a CREATE TABLE o ALTER TABLE ... ADD).</summary>
    Public Function GenerarCamp(t As TablaBBDD, f As CampoBBDD) As String
        Dim sb As New StringBuilder()
        sb.Append(Q(f.Nombre) & " ")

        If f.EsCalculado Then
            sb.Append("AS (" & f.FormulaCalculo & ")")
            If f.EsPersistido Then sb.Append(" PERSISTED")
            Return sb.ToString()
        End If

        sb.Append(GenerarTipus(f))

        If f.EsFileStream Then sb.Append(" FILESTREAM")
        If EsTipusText(f.TipoDato) AndAlso Not f.Collation Is Nothing AndAlso
           Not f.Collation.Trim().Equals("DATABASE_DEFAULT", StringComparison.OrdinalIgnoreCase) Then
            ' Només noms de col·lació vàlids (lletres, xifres i _)
            sb.Append(MdfExporter.ClausulaCollation(f.Collation.Trim()))
        End If
        If f.DataMask <> DataMaskFunction.NoMask Then
            sb.Append(" MASKED WITH (FUNCTION = '" & MaskFn(f) & "')")
        End If
        If f.EsIdentity Then
            sb.Append(" IDENTITY(" & f.IdentitySeed & "," & f.IdentityIncrement & ")")
        End If
        If f.EsRowGuid Then sb.Append(" ROWGUIDCOL")

        sb.Append(If(f.NotNull OrElse f.EsPK OrElse f.EsIdentity, " NOT NULL", " NULL"))

        If Not String.IsNullOrWhiteSpace(f.DefaultValue) Then
            sb.Append(" CONSTRAINT " & Q(NomDefault(t, f)) & " DEFAULT (" & f.DefaultValue.Trim() & ")")
        End If

        Return sb.ToString()
    End Function

    ''' <summary>
    ''' ALTER TABLE ... ADD per afegir una columna a una taula existent.
    ''' Retorna "" si no es pot afegir (PK). Si és NOT NULL sense DEFAULT
    ''' s'afegeix com a NULL i s'avisa (la taula pot tenir files).
    ''' </summary>
    Public Function GenerarAddColumn(t As TablaBBDD, f As CampoBBDD, avisos As List(Of String)) As String
        If f.EsPK Then
            avisos.Add($"[{t.Nombre}].[{f.Nombre}]: no s'ha afegit perquè és PK (no es pot afegir PK a taula existent sense reescriure-la).")
            Return ""
        End If

        Dim def As String = GenerarCamp(t, f)
        If Not f.EsCalculado AndAlso f.NotNull AndAlso
           String.IsNullOrWhiteSpace(f.DefaultValue) AndAlso Not f.EsIdentity Then
            avisos.Add($"[{t.Nombre}].[{f.Nombre}]: afegida com NULL (era NOT NULL sense DEFAULT; " &
                       "si la taula no té files, pots fer-la NOT NULL manualment amb ALTER COLUMN).")
            Dim nn As CampoBBDD = f.Clone()
            nn.NotNull = False
            def = GenerarCamp(t, nn)
        End If

        Return "ALTER TABLE " & NomTaula(t) & " ADD " & def & ";"
    End Function

    Public Function GenerarAlterFK(r As RelacionBBDD, ft As TablaBBDD, tt As TablaBBDD) As String
        Dim wc As String = If(r.WithCheck = WithCheckOption.WithNoCheck, "WITH NOCHECK", "WITH CHECK")
        Dim sb As New StringBuilder()
        sb.AppendLine("ALTER TABLE " & NomTaula(ft) & " " & wc)
        sb.AppendLine("    ADD CONSTRAINT " & Q(r.Nombre))
        sb.AppendLine("    FOREIGN KEY (" & Q(r.CampoFKNombre) & ")")
        sb.AppendLine("    REFERENCES " & NomTaula(tt) & " (" & Q(r.CampoPKNombre) & ")")
        sb.AppendLine("    ON DELETE " & AccioSql(r.OnDelete))
        sb.Append("    ON UPDATE " & AccioSql(r.OnUpdate))
        If r.NotForReplication Then
            sb.AppendLine()
            sb.Append("    NOT FOR REPLICATION")
        End If
        sb.Append(";")
        If r.Disabled Then
            sb.AppendLine()
            sb.Append("ALTER TABLE " & NomTaula(ft) & " NOCHECK CONSTRAINT " & Q(r.Nombre) & ";")
        End If
        Return sb.ToString()
    End Function

    ''' <summary>Esquemes (diferents de dbo) que han d'existir abans de crear les taules.</summary>
    Public Shared Function EsquemesNecessaris(taules As IEnumerable(Of TablaBBDD)) As List(Of String)
        Dim res As New List(Of String)()
        For Each t As TablaBBDD In taules
            Dim esq As String = t.SchemaEfectiu
            If Not esq.Equals("dbo", StringComparison.OrdinalIgnoreCase) AndAlso
               Not res.Exists(Function(x) x.Equals(esq, StringComparison.OrdinalIgnoreCase)) Then
                res.Add(esq)
            End If
        Next
        Return res
    End Function

    ''' <summary>Crea l'esquema si no existeix (CREATE SCHEMA ha d'anar sol en un lot: per això EXEC).</summary>
    Public Function GenerarCrearEsquema(esquema As String) As String
        Return "IF SCHEMA_ID(N'" & Lit(esquema) & "') IS NULL EXEC(N'CREATE SCHEMA " &
               Lit(Q(esquema)) & "');"
    End Function

    Public Shared Function NomIndexFK(r As RelacionBBDD, ft As TablaBBDD) As String
        Return "IX_" & ft.Nombre & "_" & r.CampoFKNombre
    End Function

    Public Function GenerarIndexFK(r As RelacionBBDD, ft As TablaBBDD) As String
        Return "CREATE NONCLUSTERED INDEX " & Q(NomIndexFK(r, ft)) &
               " ON " & NomTaula(ft) & " (" & Q(r.CampoFKNombre) & " ASC);"
    End Function

    ''' <param name="actualitzar">True → sp_updateextendedproperty (la propietat ja existeix)</param>
    Public Function GenerarExtPropTaula(t As TablaBBDD, Optional actualitzar As Boolean = False) As String
        If String.IsNullOrEmpty(t.Descripcion) Then Return ""
        Return ExtProp(actualitzar, t.Descripcion, t.SchemaEfectiu, "TABLE", t.Nombre, Nothing, Nothing)
    End Function

    Public Function GenerarExtPropCamp(t As TablaBBDD, f As CampoBBDD, Optional actualitzar As Boolean = False) As String
        If String.IsNullOrEmpty(f.Descripcion) Then Return ""
        Return ExtProp(actualitzar, f.Descripcion, t.SchemaEfectiu, "TABLE", t.Nombre, "COLUMN", f.Nombre)
    End Function

    Public Function GenerarExtPropRelacio(r As RelacionBBDD, ft As TablaBBDD) As String
        If String.IsNullOrEmpty(r.Descripcion) Then Return ""
        Return ExtProp(False, r.Descripcion, ft.SchemaEfectiu, "TABLE", ft.Nombre, "CONSTRAINT", r.Nombre)
    End Function

    Public Function GenerarTipus(f As CampoBBDD) As String
        Select Case f.TipoDato
            Case DataType.Bit              : Return "BIT"
            Case DataType.TinyInt          : Return "TINYINT"
            Case DataType.SmallInt         : Return "SMALLINT"
            Case DataType.DbInt            : Return "INT"
            Case DataType.BigInt           : Return "BIGINT"
            Case DataType.DbDecimal        : Return "DECIMAL(" & Prec(f) & "," & Esc(f) & ")"
            Case DataType.DbNumeric        : Return "NUMERIC(" & Prec(f) & "," & Esc(f) & ")"
            Case DataType.Money            : Return "MONEY"
            Case DataType.SmallMoney       : Return "SMALLMONEY"
            Case DataType.DbFloat          : Return "FLOAT"
            Case DataType.DbReal           : Return "REAL"
            Case DataType.DbChar           : Return "CHAR(" & Lon(f, 8000) & ")"
            Case DataType.VarChar          : Return If(f.LongitudMax, "VARCHAR(MAX)", "VARCHAR(" & Lon(f, 8000) & ")")
            Case DataType.VarCharMax       : Return "VARCHAR(MAX)"
            Case DataType.DbText           : Return "TEXT"
            Case DataType.NChar            : Return "NCHAR(" & Lon(f, 4000) & ")"
            Case DataType.NVarChar         : Return If(f.LongitudMax, "NVARCHAR(MAX)", "NVARCHAR(" & Lon(f, 4000) & ")")
            Case DataType.NVarCharMax      : Return "NVARCHAR(MAX)"
            Case DataType.NText            : Return "NTEXT"
            Case DataType.DbBinary         : Return "BINARY(" & Lon(f, 8000) & ")"
            Case DataType.VarBinary        : Return If(f.LongitudMax, "VARBINARY(MAX)", "VARBINARY(" & Lon(f, 8000) & ")")
            Case DataType.VarBinaryMax     : Return "VARBINARY(MAX)"
            Case DataType.DbImage          : Return "IMAGE"
            Case DataType.DateOnly         : Return "DATE"
            Case DataType.TimeOnly         : Return "TIME"
            Case DataType.DbDateTime       : Return "DATETIME"
            Case DataType.DateTime2        : Return "DATETIME2"
            Case DataType.SmallDateTime    : Return "SMALLDATETIME"
            Case DataType.DateTimeOffset   : Return "DATETIMEOFFSET"
            Case DataType.DbTimestamp      : Return "TIMESTAMP"
            Case DataType.UniqueIdentifier : Return "UNIQUEIDENTIFIER"
            Case DataType.DbXml            : Return "XML"
            Case DataType.DbGeography      : Return "GEOGRAPHY"
            Case DataType.DbGeometry       : Return "GEOMETRY"
            Case DataType.HierarchyId      : Return "HIERARCHYID"
            Case DataType.SqlVariant       : Return "SQL_VARIANT"
            Case DataType.RowVersion       : Return "ROWVERSION"
            Case Else                      : Return "INT"
        End Select
    End Function

    ' ════════════════════════════════════════════════════════
    ' HELPERS PÚBLICS
    ' ════════════════════════════════════════════════════════

    ''' <summary>Identificador entre claudàtors, amb "]" escapat.</summary>
    Public Shared Function Q(nom As String) As String
        Return "[" & If(nom, "").Replace("]", "]]") & "]"
    End Function

    ''' <summary>[esquema].[taula]</summary>
    Public Shared Function NomTaula(t As TablaBBDD) As String
        Return Q(t.SchemaEfectiu) & "." & Q(t.Nombre)
    End Function

    Public Shared Function NomDefault(t As TablaBBDD, f As CampoBBDD) As String
        Return "DF_" & t.Nombre & "_" & f.Nombre
    End Function

    ''' <summary>Acció ON DELETE/UPDATE vàlida a SQL Server (RESTRICT no existeix → NO ACTION).</summary>
    Public Shared Function AccioSql(a As OnDeleteUpdateAction) As String
        Select Case a
            Case OnDeleteUpdateAction.DoCascade  : Return "CASCADE"
            Case OnDeleteUpdateAction.SetNull    : Return "SET NULL"
            Case OnDeleteUpdateAction.SetDefault : Return "SET DEFAULT"
            Case Else                            : Return "NO ACTION"
        End Select
    End Function

    ' ════════════════════════════════════════════════════════
    ' HELPERS PRIVATS
    ' ════════════════════════════════════════════════════════

    Private Shared Sub AfegirLot(sb As StringBuilder, sql As String)
        If String.IsNullOrWhiteSpace(sql) Then Return
        sb.AppendLine(sql)
        sb.AppendLine("GO")
        sb.AppendLine()
    End Sub

    Private Shared Function BuscarTaula(p As ProyectoBBDD, id As Integer) As TablaBBDD
        For Each t As TablaBBDD In p.Taules
            If t.Id = id Then Return t
        Next
        Return Nothing
    End Function

    Private Shared Function ExtProp(actualitzar As Boolean, valor As String,
                                    schema As String, tipus1 As String, nom1 As String,
                                    tipus2 As String, nom2 As String) As String
        Dim sb As New StringBuilder()
        sb.Append("EXEC sys.sp_" & If(actualitzar, "update", "add") & "extendedproperty ")
        sb.Append("@name=N'MS_Description', @value=N'" & Lit(valor) & "', ")
        sb.Append("@level0type=N'SCHEMA', @level0name=N'" & Lit(schema) & "', ")
        sb.Append("@level1type=N'" & tipus1 & "', @level1name=N'" & Lit(nom1) & "'")
        If tipus2 IsNot Nothing Then
            sb.Append(", @level2type=N'" & tipus2 & "', @level2name=N'" & Lit(nom2) & "'")
        End If
        sb.Append(";")
        Return sb.ToString()
    End Function

    Private Shared Function Lit(s As String) As String
        Return If(s, "").Replace("'", "''")
    End Function

    Private Shared Function UnaLinia(s As String) As String
        Return If(s, "").Replace(vbCrLf, " ").Replace(vbCr, " ").Replace(vbLf, " ")
    End Function

    Private Shared Function Lon(f As CampoBBDD, max As Integer) As Integer
        Return Math.Min(max, Math.Max(1, f.Longitud))
    End Function

    Private Shared Function Prec(f As CampoBBDD) As Integer
        Return Math.Min(38, Math.Max(1, f.Precision))
    End Function

    Private Shared Function Esc(f As CampoBBDD) As Integer
        Return Math.Min(Prec(f), Math.Max(0, f.Escala))
    End Function

    Private Shared Function EsTipusText(dt As DataType) As Boolean
        Select Case dt
            Case DataType.DbChar, DataType.VarChar, DataType.VarCharMax, DataType.DbText,
                 DataType.NChar, DataType.NVarChar, DataType.NVarCharMax, DataType.NText
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function MaskFn(f As CampoBBDD) As String
        Select Case f.DataMask
            Case DataMaskFunction.MaskEmail
                Return "email()"
            Case DataMaskFunction.MaskPartial
                Dim pad As String = If(f.MaskPadding, "").Replace("""", "").Replace("'", "''")
                Return "partial(" & f.MaskPrefix & ",""" & pad & """," & f.MaskSuffix & ")"
            Case DataMaskFunction.MaskRandom
                Return "random(1,100)"
            Case Else
                Return "default()"
        End Select
    End Function

    ' ── Bloc de notes internes (Comentari) ── no és SQL, s'exporta com a comentaris
    Friend Shared Sub AfegirNotesDisseny(sb As StringBuilder, p As ProyectoBBDD)
        Dim hasNotes As Boolean = p.Taules.Any(Function(t) Not String.IsNullOrEmpty(t.Comentari)) OrElse
                                  p.Taules.Any(Function(t) t.Fields.Any(Function(f) Not String.IsNullOrEmpty(f.Comentari))) OrElse
                                  p.Relacions.Any(Function(r) Not String.IsNullOrEmpty(r.Comentari))
        If Not hasNotes Then Return
        sb.AppendLine("-- ══════════════════════════════════════════════════")
        sb.AppendLine("-- NOTES DE DISSENY  (notes internes, no són SQL)")
        sb.AppendLine("-- ══════════════════════════════════════════════════")
        For Each t As TablaBBDD In p.Taules
            If Not String.IsNullOrEmpty(t.Comentari) Then
                sb.AppendLine("-- [TAULA " & t.Nombre & "] " & UnaLinia(t.Comentari))
            End If
            For Each f As CampoBBDD In t.Fields
                If Not String.IsNullOrEmpty(f.Comentari) Then
                    sb.AppendLine("--   [CAMP " & t.Nombre & "." & f.Nombre & "] " & UnaLinia(f.Comentari))
                End If
            Next
        Next
        For Each r As RelacionBBDD In p.Relacions
            If Not String.IsNullOrEmpty(r.Comentari) Then
                sb.AppendLine("-- [RELACIO " & r.Nombre & "] " & UnaLinia(r.Comentari))
            End If
        Next
        sb.AppendLine("-- ══════════════════════════════════════════════════")
        sb.AppendLine()
    End Sub

End Class
