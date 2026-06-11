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
' MySqlExporter.vb — Generador DDL per a MySQL / MariaDB
' Holographic DB — Alfa 0.24
' ============================================================
Public Class MySqlExporter

    Public Function Generar(p As ProyectoBBDD) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("-- ============================================")
        sb.AppendLine("-- DDL MySQL/MariaDB generat per Holografic DB")
        sb.AppendLine("-- Projecte : " & p.Nombre)
        sb.AppendLine("-- Motor    : MySQL / MariaDB")
        sb.AppendLine("-- Data     : " & DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
        sb.AppendLine("-- ============================================")
        sb.AppendLine()
        sb.AppendLine("SET FOREIGN_KEY_CHECKS = 0;")
        sb.AppendLine()

        For Each t As TablaBBDD In p.Taules
            sb.Append(CreateTable(t, p))
        Next

        sb.AppendLine("SET FOREIGN_KEY_CHECKS = 1;")
        sb.AppendLine()

        ' Descripció de relacions com a comentaris (MySQL no té extended properties)
        Dim hasRelDesc As Boolean = p.Relacions.Any(Function(r) Not String.IsNullOrEmpty(r.Descripcion))
        If hasRelDesc Then
            sb.AppendLine("-- ── Descripcions de relacions ──────────────────────")
            For Each r As RelacionBBDD In p.Relacions
                If Not String.IsNullOrEmpty(r.Descripcion) Then
                    sb.AppendLine("-- [FK " & r.Nombre & "] " & r.Descripcion.Replace(Environment.NewLine, " "))
                End If
            Next
            sb.AppendLine()
        End If

        ' ── Bloc de notes internes (Comentari)
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

    Private Function CreateTable(t As TablaBBDD, p As ProyectoBBDD) As String
        Dim sb As New StringBuilder()
        Dim nomT As String = Bt(t.Nombre)
        sb.AppendLine("CREATE TABLE " & nomT & " (")

        Dim lines As New List(Of String)()

        For Each f As CampoBBDD In t.Fields
            lines.Add("  " & FieldDDL(f))
        Next

        ' PRIMARY KEY
        Dim pk As CampoBBDD = t.PKField
        If pk IsNot Nothing Then
            lines.Add("  PRIMARY KEY (" & Bt(pk.Nombre) & ")")
        End If

        ' UNIQUE
        For Each f As CampoBBDD In t.Fields
            If f.EsUnique AndAlso Not f.EsPK Then
                lines.Add("  UNIQUE KEY `UQ_" & t.Nombre & "_" & f.Nombre & "` (" & Bt(f.Nombre) & ")")
            End If
        Next

        ' FOREIGN KEYS inline
        For Each r As RelacionBBDD In p.Relacions
            If r.TablaOrigenId = t.Id Then
                Dim tt As TablaBBDD = Nothing
                For Each x As TablaBBDD In p.Taules
                    If x.Id = r.TablaDestinoId Then tt = x : Exit For
                Next
                If tt IsNot Nothing Then
                    Dim line As String = "  CONSTRAINT `" & r.Nombre & "`" &
                                        " FOREIGN KEY (" & Bt(r.CampoFKNombre) & ")" &
                                        " REFERENCES " & Bt(tt.Nombre) & " (" & Bt(r.CampoPKNombre) & ")"
                    line &= " ON DELETE " & OnAction(r.OnDelete)
                    line &= " ON UPDATE " & OnAction(r.OnUpdate)
                    lines.Add(line)
                End If
            End If
        Next

        For i As Integer = 0 To lines.Count - 1
            If i < lines.Count - 1 Then
                sb.AppendLine(lines(i) & ",")
            Else
                sb.AppendLine(lines(i))
            End If
        Next

        sb.AppendLine(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci" &
                      If(Not String.IsNullOrEmpty(t.Descripcion),
                         " COMMENT='" & t.Descripcion.Replace("'", "''") & "'", "") & ";")
        sb.AppendLine()

        ' INDEX per FK
        For Each r As RelacionBBDD In p.Relacions
            If r.TablaOrigenId = t.Id AndAlso r.CrearIndexFK Then
                sb.AppendLine("CREATE INDEX `IX_" & t.Nombre & "_" & r.CampoFKNombre & "`")
                sb.AppendLine("  ON " & Bt(t.Nombre) & " (" & Bt(r.CampoFKNombre) & " ASC);")
                sb.AppendLine()
            End If
        Next

        Return sb.ToString()
    End Function

    Private Function FieldDDL(f As CampoBBDD) As String
        Dim sb As New StringBuilder()
        sb.Append(Bt(f.Nombre) & " ")

        If f.EsCalculado Then
            ' MySQL: columna generada
            sb.Append(SqlType(f) & " GENERATED ALWAYS AS (" & f.FormulaCalculo & ")")
            If f.EsPersistido Then sb.Append(" STORED") Else sb.Append(" VIRTUAL")
            Return sb.ToString()
        End If

        sb.Append(SqlType(f))

        If f.EsIdentity Then sb.Append(" AUTO_INCREMENT")

        If f.NotNull Then sb.Append(" NOT NULL") Else sb.Append(" NULL")

        If Not String.IsNullOrEmpty(f.DefaultValue) Then
            sb.Append(" DEFAULT " & f.DefaultValue)
        End If

        ' COMMENT per a descripció del camp
        If Not String.IsNullOrEmpty(f.Descripcion) Then
            Dim d As String = f.Descripcion.Replace("'", "''")
            sb.Append(" COMMENT '" & d & "'")
        End If

        Return sb.ToString()
    End Function

    Private Function SqlType(f As CampoBBDD) As String
        Select Case f.TipoDato
            Case DataType.Bit          : Return "TINYINT(1)"
            Case DataType.TinyInt      : Return "TINYINT UNSIGNED"
            Case DataType.SmallInt     : Return "SMALLINT"
            Case DataType.DbInt        : Return "INT"
            Case DataType.BigInt       : Return "BIGINT"
            Case DataType.DbDecimal    : Return "DECIMAL(" & f.Precision & "," & f.Escala & ")"
            Case DataType.DbNumeric    : Return "NUMERIC(" & f.Precision & "," & f.Escala & ")"
            Case DataType.Money        : Return "DECIMAL(19,4)"
            Case DataType.SmallMoney   : Return "DECIMAL(10,4)"
            Case DataType.DbFloat      : Return "DOUBLE"
            Case DataType.DbReal       : Return "FLOAT"
            Case DataType.DbChar       : Return "CHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarChar      : If f.LongitudMax Then Return "LONGTEXT" Else Return "VARCHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarCharMax   : Return "LONGTEXT"
            Case DataType.DbText       : Return "TEXT"
            Case DataType.NChar        : Return "CHAR(" & Math.Max(1, f.Longitud) & ") CHARACTER SET utf8mb4"
            Case DataType.NVarChar     : If f.LongitudMax Then Return "LONGTEXT CHARACTER SET utf8mb4" Else Return "VARCHAR(" & Math.Max(1, f.Longitud) & ") CHARACTER SET utf8mb4"
            Case DataType.NVarCharMax  : Return "LONGTEXT CHARACTER SET utf8mb4"
            Case DataType.NText        : Return "TEXT CHARACTER SET utf8mb4"
            Case DataType.DbBinary     : Return "BINARY(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarBinary    : If f.LongitudMax Then Return "LONGBLOB" Else Return "VARBINARY(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarBinaryMax : Return "LONGBLOB"
            Case DataType.DbImage      : Return "LONGBLOB"
            Case DataType.DateOnly     : Return "DATE"
            Case DataType.TimeOnly     : Return "TIME"
            Case DataType.DbDateTime   : Return "DATETIME"
            Case DataType.DateTime2    : Return "DATETIME(6)"
            Case DataType.SmallDateTime: Return "DATETIME"
            Case DataType.DateTimeOffset: Return "DATETIME(6)"
            Case DataType.DbTimestamp  : Return "TIMESTAMP"
            Case DataType.UniqueIdentifier: Return "CHAR(36)"
            Case DataType.DbXml        : Return "TEXT"
            Case DataType.SqlVariant   : Return "TEXT"
            Case DataType.RowVersion   : Return "TIMESTAMP"
            Case Else                  : Return "INT"
        End Select
    End Function

    Private Function OnAction(a As OnDeleteUpdateAction) As String
        Select Case a
            Case OnDeleteUpdateAction.DoCascade  : Return "CASCADE"
            Case OnDeleteUpdateAction.SetNull    : Return "SET NULL"
            Case OnDeleteUpdateAction.SetDefault : Return "SET DEFAULT"
            Case OnDeleteUpdateAction.DoRestrict : Return "RESTRICT"
            Case Else                            : Return "NO ACTION"
        End Select
    End Function

    ' Backtic-quote per a MySQL
    Private Function Bt(nom As String) As String
        Return "`" & nom & "`"
    End Function

End Class
