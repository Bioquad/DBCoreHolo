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
Imports System.Drawing
Imports System.Collections.Generic
Imports System.Text


Public Class TSqlExporter

    Public Function Generar(p As ProyectoBBDD) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("-- ============================================")
        sb.AppendLine("-- DDL T-SQL generat per Holografic DB")
        sb.AppendLine("-- Projecte : " & p.Nombre)
        sb.AppendLine("-- Motor    : SQL Server (T-SQL)")
        sb.AppendLine("-- Data     : " & DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
        sb.AppendLine("-- ============================================")
        sb.AppendLine()

        For Each t As TablaBBDD In p.Taules
            sb.Append(CreateTable(t))
        Next

        For Each r As RelacionBBDD In p.Relacions
            Dim ft As TablaBBDD = Nothing
            Dim tt As TablaBBDD = Nothing
            For Each t As TablaBBDD In p.Taules
                If t.Id = r.TablaOrigenId Then ft = t
                If t.Id = r.TablaDestinoId Then tt = t
            Next
            If ft IsNot Nothing AndAlso tt IsNot Nothing Then
                sb.Append(AlterFK(r, ft, tt))
            End If
        Next

        For Each r As RelacionBBDD In p.Relacions
            If r.CrearIndexFK Then
                Dim ft As TablaBBDD = Nothing
                For Each t As TablaBBDD In p.Taules
                    If t.Id = r.TablaOrigenId Then ft = t
                Next
                If ft IsNot Nothing Then
                    sb.AppendLine("CREATE NONCLUSTERED INDEX [IX_" & ft.Nombre & "_" & r.CampoFKNombre & "]")
                    sb.AppendLine("    ON [dbo].[" & ft.Nombre & "] ([" & r.CampoFKNombre & "] ASC);")
                    sb.AppendLine("GO")
                    sb.AppendLine()
                End If
            End If
        Next

        For Each t As TablaBBDD In p.Taules
            ' Descripció de taula
            If Not String.IsNullOrEmpty(t.Descripcion) Then
                Dim d As String = t.Descripcion.Replace("'", "''")
                sb.AppendLine("EXEC sp_addextendedproperty")
                sb.AppendLine("    @name=N'MS_Description', @value=N'" & d & "',")
                sb.AppendLine("    @level0type=N'SCHEMA', @level0name=N'" & t.Schema & "',")
                sb.AppendLine("    @level1type=N'TABLE',  @level1name=N'" & t.Nombre & "';")
                sb.AppendLine("GO")
                sb.AppendLine()
            End If
            ' Descripció de camps
            For Each f As CampoBBDD In t.Fields
                If Not String.IsNullOrEmpty(f.Descripcion) Then
                    sb.Append(ExtendedProp(t.Nombre, f.Nombre, f.Descripcion))
                End If
            Next
        Next

        ' Descripció de relacions (via extended property a nivell de constraint)
        For Each r As RelacionBBDD In p.Relacions
            If Not String.IsNullOrEmpty(r.Descripcion) Then
                Dim ft As TablaBBDD = Nothing
                For Each t As TablaBBDD In p.Taules
                    If t.Id = r.TablaOrigenId Then ft = t : Exit For
                Next
                If ft IsNot Nothing Then
                    Dim d As String = r.Descripcion.Replace("'", "''")
                    sb.AppendLine("EXEC sp_addextendedproperty")
                    sb.AppendLine("    @name=N'MS_Description', @value=N'" & d & "',")
                    sb.AppendLine("    @level0type=N'SCHEMA', @level0name=N'" & ft.Schema & "',")
                    sb.AppendLine("    @level1type=N'TABLE',  @level1name=N'" & ft.Nombre & "',")
                    sb.AppendLine("    @level2type=N'CONSTRAINT', @level2name=N'" & r.Nombre & "';")
                    sb.AppendLine("GO")
                    sb.AppendLine()
                End If
            End If
        Next

        ' ── Bloc de notes internes (Comentari) ── no és SQL estàndard,
        '    s'exporta com a comentaris de bloc al final del script
        Dim hasNotes As Boolean = p.Taules.Any(Function(t) Not String.IsNullOrEmpty(t.Comentari)) OrElse
                                  p.Taules.Any(Function(t) t.Fields.Any(Function(f) Not String.IsNullOrEmpty(f.Comentari))) OrElse
                                  p.Relacions.Any(Function(r) Not String.IsNullOrEmpty(r.Comentari))
        If hasNotes Then
            sb.AppendLine("-- ══════════════════════════════════════════════════")
            sb.AppendLine("-- NOTES DE DISSENY  (notes internes, no són SQL)")
            sb.AppendLine("-- ══════════════════════════════════════════════════")
            For Each t As TablaBBDD In p.Taules
                If Not String.IsNullOrEmpty(t.Comentari) Then
                    sb.AppendLine("-- [TAULA " & t.Nombre & "] " & t.Comentari.Replace(Environment.NewLine, " "))
                End If
                For Each f As CampoBBDD In t.Fields
                    If Not String.IsNullOrEmpty(f.Comentari) Then
                        sb.AppendLine("--   [CAMP " & t.Nombre & "." & f.Nombre & "] " & f.Comentari.Replace(Environment.NewLine, " "))
                    End If
                Next
            Next
            For Each r As RelacionBBDD In p.Relacions
                If Not String.IsNullOrEmpty(r.Comentari) Then
                    sb.AppendLine("-- [RELACIO " & r.Nombre & "] " & r.Comentari.Replace(Environment.NewLine, " "))
                End If
            Next
            sb.AppendLine("-- ══════════════════════════════════════════════════")
            sb.AppendLine()
        End If

        Return sb.ToString()
    End Function

    Private Function CreateTable(t As TablaBBDD) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("CREATE TABLE [dbo].[" & t.Nombre & "] (")

        Dim lines As New List(Of String)()

        For Each f As CampoBBDD In t.Fields
            lines.Add("    " & FieldDDL(f))
        Next

        Dim pk As CampoBBDD = t.PKField
        If pk IsNot Nothing Then
            lines.Add("    ,CONSTRAINT [PK_" & t.Nombre & "] PRIMARY KEY CLUSTERED ([" & pk.Nombre & "] ASC)")
        End If

        For Each f As CampoBBDD In t.Fields
            If f.EsUnique AndAlso Not f.EsPK Then
                lines.Add("    ,CONSTRAINT [UQ_" & t.Nombre & "_" & f.Nombre & "] UNIQUE ([" & f.Nombre & "])")
            End If
        Next

        For Each f As CampoBBDD In t.Fields
            If Not String.IsNullOrEmpty(f.CheckExpression) Then
                lines.Add("    ,CONSTRAINT [CK_" & t.Nombre & "_" & f.Nombre & "] CHECK (" & f.CheckExpression & ")")
            End If
        Next

        For i As Integer = 0 To lines.Count - 1
            If i < lines.Count - 1 Then
                sb.AppendLine(lines(i) & ",")
            Else
                sb.AppendLine(lines(i))
            End If
        Next

        sb.AppendLine(");")
        sb.AppendLine("GO")
        sb.AppendLine()
        Return sb.ToString()
    End Function

    Private Function FieldDDL(f As CampoBBDD) As String
        Dim sb As New StringBuilder()
        sb.Append("[" & f.Nombre & "] ")

        If f.EsCalculado Then
            sb.Append("AS (" & f.FormulaCalculo & ")")
            If f.EsPersistido Then sb.Append(" PERSISTED")
            Return sb.ToString()
        End If

        sb.Append(SqlType(f))

        If f.EsIdentity Then
            sb.Append(" IDENTITY(" & f.IdentitySeed & "," & f.IdentityIncrement & ")")
        End If
        If f.EsRowGuid Then sb.Append(" ROWGUIDCOL")
        If f.EsFileStream Then sb.Append(" FILESTREAM")

        If f.NotNull Then
            sb.Append(" NOT NULL")
        Else
            sb.Append(" NULL")
        End If

        If Not String.IsNullOrEmpty(f.DefaultValue) Then
            sb.Append(" CONSTRAINT [DF_" & f.Nombre & "] DEFAULT (" & f.DefaultValue & ")")
        End If

        If f.DataMask <> DataMaskFunction.NoMask Then
            sb.Append(" MASKED WITH (FUNCTION = '" & MaskFn(f) & "')")
        End If

        Return sb.ToString()
    End Function

    Private Function SqlType(f As CampoBBDD) As String
        Select Case f.TipoDato
            Case DataType.Bit
                Return "BIT"
            Case DataType.TinyInt
                Return "TINYINT"
            Case DataType.SmallInt
                Return "SMALLINT"
            Case DataType.DbInt
                Return "INT"
            Case DataType.BigInt
                Return "BIGINT"
            Case DataType.DbDecimal
                Return "DECIMAL(" & f.Precision & "," & f.Escala & ")"
            Case DataType.DbNumeric
                Return "NUMERIC(" & f.Precision & "," & f.Escala & ")"
            Case DataType.Money
                Return "MONEY"
            Case DataType.SmallMoney
                Return "SMALLMONEY"
            Case DataType.DbFloat
                Return "FLOAT"
            Case DataType.DbReal
                Return "REAL"
            Case DataType.DbChar
                Return "CHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarChar
                If f.LongitudMax Then Return "VARCHAR(MAX)" Else Return "VARCHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarCharMax
                Return "VARCHAR(MAX)"
            Case DataType.DbText
                Return "TEXT"
            Case DataType.NChar
                Return "NCHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.NVarChar
                If f.LongitudMax Then Return "NVARCHAR(MAX)" Else Return "NVARCHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.NVarCharMax
                Return "NVARCHAR(MAX)"
            Case DataType.NText
                Return "NTEXT"
            Case DataType.DbBinary
                Return "BINARY(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarBinary
                If f.LongitudMax Then Return "VARBINARY(MAX)" Else Return "VARBINARY(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarBinaryMax
                Return "VARBINARY(MAX)"
            Case DataType.DbImage
                Return "IMAGE"
            Case DataType.DateOnly
                Return "DATE"
            Case DataType.TimeOnly
                Return "TIME"
            Case DataType.DbDateTime
                Return "DATETIME"
            Case DataType.DateTime2
                Return "DATETIME2"
            Case DataType.SmallDateTime
                Return "SMALLDATETIME"
            Case DataType.DateTimeOffset
                Return "DATETIMEOFFSET"
            Case DataType.DbTimestamp
                Return "TIMESTAMP"
            Case DataType.UniqueIdentifier
                Return "UNIQUEIDENTIFIER"
            Case DataType.DbXml
                Return "XML"
            Case DataType.DbGeography
                Return "GEOGRAPHY"
            Case DataType.DbGeometry
                Return "GEOMETRY"
            Case DataType.HierarchyId
                Return "HIERARCHYID"
            Case DataType.SqlVariant
                Return "SQL_VARIANT"
            Case DataType.RowVersion
                Return "ROWVERSION"
            Case Else
                Return "INT"
        End Select
    End Function

    Private Function MaskFn(f As CampoBBDD) As String
        Select Case f.DataMask
            Case DataMaskFunction.DefaultMask
                Return "default()"
            Case DataMaskFunction.MaskEmail
                Return "email()"
            Case DataMaskFunction.MaskPartial
                Return "partial(" & f.MaskPrefix & ",""" & f.MaskPadding & """," & f.MaskSuffix & ")"
            Case DataMaskFunction.MaskRandom
                Return "random(1,100)"
            Case Else
                Return "default()"
        End Select
    End Function

    Private Function AlterFK(r As RelacionBBDD, ft As TablaBBDD, tt As TablaBBDD) As String
        Dim sb As New StringBuilder()
        Dim wc As String = "WITH CHECK"
        If r.WithCheck = WithCheckOption.WithNoCheck Then wc = "WITH NOCHECK"

        sb.AppendLine("ALTER TABLE [dbo].[" & ft.Nombre & "] " & wc)
        sb.AppendLine("    ADD CONSTRAINT [" & r.Nombre & "]")
        sb.AppendLine("    FOREIGN KEY ([" & r.CampoFKNombre & "])")
        sb.AppendLine("    REFERENCES [dbo].[" & tt.Nombre & "] ([" & r.CampoPKNombre & "])")
        sb.AppendLine("    ON DELETE " & r.OnDeleteLabel)
        sb.AppendLine("    ON UPDATE " & r.OnUpdateLabel)
        If r.NotForReplication Then sb.AppendLine("    NOT FOR REPLICATION")
        sb.AppendLine(";")
        sb.AppendLine("GO")
        If r.Disabled Then
            sb.AppendLine("ALTER TABLE [dbo].[" & ft.Nombre & "] NOCHECK CONSTRAINT [" & r.Nombre & "];")
            sb.AppendLine("GO")
        End If
        sb.AppendLine()
        Return sb.ToString()
    End Function

    Private Function ExtendedProp(tNom As String, cNom As String, desc As String) As String
        Dim d As String = desc.Replace("'", "''")
        Dim sb As New StringBuilder()
        sb.AppendLine("EXEC sp_addextendedproperty")
        sb.AppendLine("    @name=N'MS_Description', @value=N'" & d & "',")
        sb.AppendLine("    @level0type=N'SCHEMA', @level0name=N'dbo',")
        sb.AppendLine("    @level1type=N'TABLE',  @level1name=N'" & tNom & "',")
        sb.AppendLine("    @level2type=N'COLUMN', @level2name=N'" & cNom & "';")
        sb.AppendLine("GO")
        sb.AppendLine()
        Return sb.ToString()
    End Function

    ' ── Mètodes públics per SqlServerConnector ─────────────────
    Public Function GenerarTaula(t As TablaBBDD) As String
        Return CreateTable(t)
    End Function

    Public Function GenerarAlterFK(r As RelacionBBDD,
                                    ft As TablaBBDD,
                                    tt As TablaBBDD) As String
        Return AlterFK(r, ft, tt)
    End Function

    Public Function GenerarCamp(f As CampoBBDD) As String
        Return FieldDDL(f)
    End Function

    Public Function GenerarExtPropTaula(t As TablaBBDD) As String
        If String.IsNullOrEmpty(t.Descripcion) Then Return ""
        Dim d As String = t.Descripcion.Replace("'", "''")
        Return "EXEC sp_addextendedproperty " &
               "@name=N'MS_Description', @value=N'" & d & "', " &
               "@level0type=N'SCHEMA', @level0name=N'" & t.Schema & "', " &
               "@level1type=N'TABLE', @level1name=N'" & t.Nombre & "';"
    End Function

    Public Function GenerarExtPropCamp(t As TablaBBDD, f As CampoBBDD) As String
        If String.IsNullOrEmpty(f.Descripcion) Then Return ""
        Return ExtendedProp(t.Nombre, f.Nombre, f.Descripcion)
    End Function

End Class