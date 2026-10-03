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
Imports System.Text
Imports System.Collections.Generic

' ============================================================
' PostgreSqlExporter.vb — Generador DDL per a PostgreSQL 13+
' ============================================================
Public Class PostgreSqlExporter

    Private ReadOnly _avisos As New List(Of String)()
    Private _calPostGis As Boolean = False

    Public Function Generar(p As ProyectoBBDD) As String
        _avisos.Clear()
        _calPostGis = False

        Dim cos As New StringBuilder()

        For Each t As TablaBBDD In p.Taules
            cos.Append(CreateTable(t))
        Next

        For Each r As RelacionBBDD In p.Relacions
            Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
            Dim tt As TablaBBDD = BuscarTaula(p, r.TablaDestinoId)
            If ft IsNot Nothing AndAlso tt IsNot Nothing Then cos.Append(AlterFK(r, ft, tt))
        Next

        ' INDEX per FK
        For Each r As RelacionBBDD In p.Relacions
            If Not r.CrearIndexFK Then Continue For
            Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
            If ft Is Nothing Then Continue For
            cos.AppendLine("CREATE INDEX " & Q("IX_" & ft.Nombre & "_" & r.CampoFKNombre) &
                           " ON " & Q(ft.Nombre) & " (" & Q(r.CampoFKNombre) & ");")
            cos.AppendLine()
        Next

        ' COMMENT ON TABLE / COLUMN
        For Each t As TablaBBDD In p.Taules
            If Not String.IsNullOrEmpty(t.Descripcion) Then
                cos.AppendLine("COMMENT ON TABLE " & Q(t.Nombre) & " IS '" & Lit(t.Descripcion) & "';")
            End If
            For Each f As CampoBBDD In t.Fields
                If Not String.IsNullOrEmpty(f.Descripcion) Then
                    cos.AppendLine("COMMENT ON COLUMN " & Q(t.Nombre) & "." & Q(f.Nombre) &
                                   " IS '" & Lit(f.Descripcion) & "';")
                End If
            Next
        Next
        cos.AppendLine()

        ' Descripció de relacions
        For Each r As RelacionBBDD In p.Relacions
            If String.IsNullOrEmpty(r.Descripcion) Then Continue For
            Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
            If ft Is Nothing Then Continue For
            cos.AppendLine("COMMENT ON CONSTRAINT " & Q(r.Nombre) & " ON " & Q(ft.Nombre) &
                           " IS '" & Lit(r.Descripcion) & "';")
        Next
        cos.AppendLine()

        TSqlExporter.AfegirNotesDisseny(cos, p)

        Dim sb As New StringBuilder()
        sb.AppendLine("-- ============================================")
        sb.AppendLine("-- DDL PostgreSQL generat per Holografic DB")
        sb.AppendLine("-- Projecte : " & UnaLinia(p.Nombre))
        sb.AppendLine("-- Motor    : PostgreSQL 13+")
        sb.AppendLine("-- Data     : " & DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
        sb.AppendLine("-- ============================================")
        If TeExpressions(p) Then
            _avisos.Add("Les expressions CHECK i de columnes calculades es copien del model: revisa que siguin sintaxi PostgreSQL.")
        End If
        For Each a As String In _avisos.Distinct()
            sb.AppendLine("-- AVÍS: " & a)
        Next
        sb.AppendLine()
        If _calPostGis Then
            sb.AppendLine("-- Els tipus GEOGRAPHY/GEOMETRY requereixen l'extensió PostGIS")
            sb.AppendLine("CREATE EXTENSION IF NOT EXISTS postgis;")
            sb.AppendLine()
        End If
        sb.Append(cos)
        Return sb.ToString()
    End Function

    Private Function CreateTable(t As TablaBBDD) As String
        Dim lines As New List(Of String)()

        For Each f As CampoBBDD In t.Fields
            lines.Add("  " & FieldDDL(t, f))
        Next

        Dim pks As List(Of CampoBBDD) = t.PKFields
        If pks.Count > 0 Then
            lines.Add("  CONSTRAINT " & Q("PK_" & t.Nombre) & " PRIMARY KEY (" &
                      String.Join(", ", pks.Select(Function(k) Q(k.Nombre))) & ")")
        End If

        For Each f As CampoBBDD In t.Fields
            If f.EsUnique AndAlso Not (f.EsPK AndAlso pks.Count = 1) Then
                lines.Add("  CONSTRAINT " & Q("UQ_" & t.Nombre & "_" & f.Nombre) & " UNIQUE (" & Q(f.Nombre) & ")")
            End If
        Next

        For Each f As CampoBBDD In t.Fields
            If Not String.IsNullOrWhiteSpace(f.CheckExpression) Then
                lines.Add("  CONSTRAINT " & Q("CK_" & t.Nombre & "_" & f.Nombre) &
                          " CHECK (" & ConvertirExpressio(f.CheckExpression.Trim(), Motor.PostgreSql, t) & ")")
            End If
        Next

        Dim sb As New StringBuilder()
        sb.AppendLine("CREATE TABLE " & Q(t.Nombre) & " (")
        sb.AppendLine(String.Join("," & Environment.NewLine, lines))
        sb.AppendLine(");")
        sb.AppendLine()
        Return sb.ToString()
    End Function

    Private Function FieldDDL(t As TablaBBDD, f As CampoBBDD) As String
        Dim sb As New StringBuilder()
        sb.Append(Q(f.Nombre) & " " & SqlType(t, f))

        If f.EsCalculado Then
            ' PostgreSQL (fins a la v17) només admet columnes generades STORED
            If Not f.EsPersistido Then
                Avis(t, f, "columna calculada no persistida → STORED (PostgreSQL només admet STORED)")
            End If
            sb.Append(" GENERATED ALWAYS AS (" & ConvertirExpressio(f.FormulaCalculo, Motor.PostgreSql, t) & ") STORED")
            Return sb.ToString()
        End If

        If f.EsIdentity Then
            If EsEnter(f.TipoDato) Then
                sb.Append(" GENERATED BY DEFAULT AS IDENTITY (START WITH " & f.IdentitySeed &
                          " INCREMENT BY " & f.IdentityIncrement & ")")
            Else
                Avis(t, f, "IDENTITY només és vàlid en tipus enters; s'ha omès")
            End If
        End If

        sb.Append(If(f.NotNull OrElse f.EsPK OrElse f.EsIdentity, " NOT NULL", " NULL"))

        If Not String.IsNullOrWhiteSpace(f.DefaultValue) AndAlso Not f.EsIdentity Then
            sb.Append(" DEFAULT " & ConvertirDefault(f.DefaultValue, f, Motor.PostgreSql, t))
        End If

        Return sb.ToString()
    End Function

    Private Function SqlType(t As TablaBBDD, f As CampoBBDD) As String
        Select Case f.TipoDato
            Case DataType.Bit              : Return "BOOLEAN"
            Case DataType.TinyInt          : Return "SMALLINT"
            Case DataType.SmallInt         : Return "SMALLINT"
            Case DataType.DbInt            : Return "INTEGER"
            Case DataType.BigInt           : Return "BIGINT"
            Case DataType.DbDecimal        : Return "DECIMAL(" & Prec(f) & "," & Esc(f) & ")"
            Case DataType.DbNumeric        : Return "NUMERIC(" & Prec(f) & "," & Esc(f) & ")"
            Case DataType.Money            : Return "NUMERIC(19,4)"
            Case DataType.SmallMoney       : Return "NUMERIC(10,4)"
            Case DataType.DbFloat          : Return "DOUBLE PRECISION"
            Case DataType.DbReal           : Return "REAL"
            Case DataType.DbChar           : Return "CHAR(" & Lon(f) & ")"
            Case DataType.VarChar          : Return If(f.LongitudMax, "TEXT", "VARCHAR(" & Lon(f) & ")")
            Case DataType.VarCharMax       : Return "TEXT"
            Case DataType.DbText           : Return "TEXT"
            Case DataType.NChar            : Return "CHAR(" & Lon(f) & ")"
            Case DataType.NVarChar         : Return If(f.LongitudMax, "TEXT", "VARCHAR(" & Lon(f) & ")")
            Case DataType.NVarCharMax      : Return "TEXT"
            Case DataType.NText            : Return "TEXT"
            Case DataType.DbBinary, DataType.VarBinary, DataType.VarBinaryMax, DataType.DbImage
                Return "BYTEA"
            Case DataType.DateOnly         : Return "DATE"
            Case DataType.TimeOnly         : Return "TIME"
            Case DataType.DbDateTime       : Return "TIMESTAMP(3)"
            Case DataType.DateTime2        : Return "TIMESTAMP(6)"
            Case DataType.SmallDateTime    : Return "TIMESTAMP(0)"
            Case DataType.DateTimeOffset   : Return "TIMESTAMPTZ"
            Case DataType.DbTimestamp, DataType.RowVersion
                Avis(t, f, "ROWVERSION/TIMESTAMP de SQL Server no té equivalent → BYTEA (usa xmin o un trigger si cal control de versions)")
                Return "BYTEA"
            Case DataType.UniqueIdentifier : Return "UUID"
            Case DataType.DbXml            : Return "XML"
            Case DataType.DbGeography
                _calPostGis = True
                Return "GEOGRAPHY"
            Case DataType.DbGeometry
                _calPostGis = True
                Return "GEOMETRY"
            Case DataType.HierarchyId
                Avis(t, f, "HIERARCHYID no existeix a PostgreSQL → TEXT (o l'extensió ltree)")
                Return "TEXT"
            Case DataType.SqlVariant
                Avis(t, f, "SQL_VARIANT no existeix a PostgreSQL → TEXT")
                Return "TEXT"
            Case Else                      : Return "INTEGER"
        End Select
    End Function

    Private Function AlterFK(r As RelacionBBDD, ft As TablaBBDD, tt As TablaBBDD) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("ALTER TABLE " & Q(ft.Nombre))
        sb.AppendLine("  ADD CONSTRAINT " & Q(r.Nombre))
        sb.AppendLine("  FOREIGN KEY (" & Q(r.CampoFKNombre) & ")")
        sb.AppendLine("  REFERENCES " & Q(tt.Nombre) & " (" & Q(r.CampoPKNombre) & ")")
        sb.AppendLine("  ON DELETE " & OnAction(r.OnDelete))
        sb.Append("  ON UPDATE " & OnAction(r.OnUpdate))
        ' WITH NOCHECK de SQL Server ≈ NOT VALID (no valida les files existents)
        If r.WithCheck = WithCheckOption.WithNoCheck Then sb.Append(" NOT VALID")
        sb.AppendLine(";")
        If r.Disabled Then
            _avisos.Add("FK """ & r.Nombre & """ està desactivada al model; PostgreSQL no admet FK desactivades i es crea activa.")
        End If
        sb.AppendLine()
        Return sb.ToString()
    End Function

    Private Shared Function OnAction(a As OnDeleteUpdateAction) As String
        Select Case a
            Case OnDeleteUpdateAction.DoCascade  : Return "CASCADE"
            Case OnDeleteUpdateAction.SetNull    : Return "SET NULL"
            Case OnDeleteUpdateAction.SetDefault : Return "SET DEFAULT"
            Case OnDeleteUpdateAction.DoRestrict : Return "RESTRICT"
            Case Else                            : Return "NO ACTION"
        End Select
    End Function

    Private Sub Avis(t As TablaBBDD, f As CampoBBDD, msg As String)
        _avisos.Add("[" & t.Nombre & "].[" & f.Nombre & "]: " & msg)
    End Sub

    Private Shared Function BuscarTaula(p As ProyectoBBDD, id As Integer) As TablaBBDD
        For Each t As TablaBBDD In p.Taules
            If t.Id = id Then Return t
        Next
        Return Nothing
    End Function

    Private Shared Function EsEnter(dt As DataType) As Boolean
        Return dt = DataType.TinyInt OrElse dt = DataType.SmallInt OrElse
               dt = DataType.DbInt OrElse dt = DataType.BigInt
    End Function

    Private Shared Function Lon(f As CampoBBDD) As Integer
        Return Math.Max(1, f.Longitud)
    End Function

    Private Shared Function Prec(f As CampoBBDD) As Integer
        Return Math.Min(1000, Math.Max(1, f.Precision))
    End Function

    Private Shared Function Esc(f As CampoBBDD) As Integer
        Return Math.Min(Prec(f), Math.Max(0, f.Escala))
    End Function

    Private Shared Function Lit(s As String) As String
        Return s.Replace("'", "''")
    End Function

    ' Double-quote per a PostgreSQL (la " interna es duplica)
    Private Shared Function Q(nom As String) As String
        Return """" & If(nom, "").Replace("""", """""") & """"
    End Function

End Class
