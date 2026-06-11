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
' PostgreSqlExporter.vb — Generador DDL per a PostgreSQL
' Holographic DB — Alfa 0.24
' ============================================================
Public Class PostgreSqlExporter

    Public Function Generar(p As ProyectoBBDD) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("-- ============================================")
        sb.AppendLine("-- DDL PostgreSQL generat per Holografic DB")
        sb.AppendLine("-- Projecte : " & p.Nombre)
        sb.AppendLine("-- Motor    : PostgreSQL")
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

        ' INDEX per FK
        For Each r As RelacionBBDD In p.Relacions
            If r.CrearIndexFK Then
                Dim ft As TablaBBDD = Nothing
                For Each t As TablaBBDD In p.Taules
                    If t.Id = r.TablaOrigenId Then ft = t : Exit For
                Next
                If ft IsNot Nothing Then
                    sb.AppendLine("CREATE INDEX ""IX_" & ft.Nombre & "_" & r.CampoFKNombre & """")
                    sb.AppendLine("  ON " & Q(ft.Nombre) & " (" & Q(r.CampoFKNombre) & " ASC);")
                    sb.AppendLine()
                End If
            End If
        Next

        ' COMMENT ON COLUMN i COMMENT ON TABLE
        For Each t As TablaBBDD In p.Taules
            If Not String.IsNullOrEmpty(t.Descripcion) Then
                Dim d As String = t.Descripcion.Replace("'", "''")
                sb.AppendLine("COMMENT ON TABLE " & Q(t.Nombre) & " IS '" & d & "';")
                sb.AppendLine()
            End If
            For Each f As CampoBBDD In t.Fields
                If Not String.IsNullOrEmpty(f.Descripcion) Then
                    Dim d As String = f.Descripcion.Replace("'", "''")
                    sb.AppendLine("COMMENT ON COLUMN " & Q(t.Nombre) & "." & Q(f.Nombre) & " IS '" & d & "';")
                End If
            Next
        Next

        ' Descripció de relacions (COMMENT ON CONSTRAINT — PostgreSQL 11+)
        For Each r As RelacionBBDD In p.Relacions
            If Not String.IsNullOrEmpty(r.Descripcion) Then
                Dim ft As TablaBBDD = Nothing
                For Each t As TablaBBDD In p.Taules
                    If t.Id = r.TablaOrigenId Then ft = t : Exit For
                Next
                If ft IsNot Nothing Then
                    Dim d As String = r.Descripcion.Replace("'", "''")
                    sb.AppendLine("COMMENT ON CONSTRAINT """ & r.Nombre & """ ON " & Q(ft.Nombre) & " IS '" & d & "';")
                    sb.AppendLine()
                End If
            End If
        Next

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

    Private Function CreateTable(t As TablaBBDD) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("CREATE TABLE " & Q(t.Nombre) & " (")

        Dim lines As New List(Of String)()

        For Each f As CampoBBDD In t.Fields
            lines.Add("  " & FieldDDL(f, t.Nombre))
        Next

        ' PRIMARY KEY
        Dim pk As CampoBBDD = t.PKField
        If pk IsNot Nothing Then
            lines.Add("  CONSTRAINT ""PK_" & t.Nombre & """ PRIMARY KEY (" & Q(pk.Nombre) & ")")
        End If

        ' UNIQUE
        For Each f As CampoBBDD In t.Fields
            If f.EsUnique AndAlso Not f.EsPK Then
                lines.Add("  CONSTRAINT ""UQ_" & t.Nombre & "_" & f.Nombre & """ UNIQUE (" & Q(f.Nombre) & ")")
            End If
        Next

        ' CHECK
        For Each f As CampoBBDD In t.Fields
            If Not String.IsNullOrEmpty(f.CheckExpression) Then
                lines.Add("  CONSTRAINT ""CK_" & t.Nombre & "_" & f.Nombre & """ CHECK (" & f.CheckExpression & ")")
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
        sb.AppendLine()
        Return sb.ToString()
    End Function

    Private Function FieldDDL(f As CampoBBDD, tNom As String) As String
        Dim sb As New StringBuilder()
        sb.Append(Q(f.Nombre) & " ")

        If f.EsCalculado Then
            sb.Append(SqlType(f) & " GENERATED ALWAYS AS (" & f.FormulaCalculo & ")")
            If f.EsPersistido Then sb.Append(" STORED")
            Return sb.ToString()
        End If

        ' SERIAL / BIGSERIAL si és identity
        If f.EsIdentity Then
            Select Case f.TipoDato
                Case DataType.BigInt : sb.Append("BIGSERIAL")
                Case DataType.SmallInt : sb.Append("SMALLSERIAL")
                Case Else : sb.Append("SERIAL")
            End Select
        Else
            sb.Append(SqlType(f))
        End If

        If f.NotNull Then sb.Append(" NOT NULL") Else sb.Append(" NULL")

        If Not String.IsNullOrEmpty(f.DefaultValue) Then
            Dim dv As String = f.DefaultValue
            ' Adaptar funcions T-SQL a PostgreSQL
            dv = dv.Replace("GETDATE()", "NOW()").Replace("GETUTCDATE()", "NOW() AT TIME ZONE 'UTC'")
            dv = dv.Replace("NEWID()", "gen_random_uuid()")
            sb.Append(" DEFAULT " & dv)
        End If

        Return sb.ToString()
    End Function

    Private Function SqlType(f As CampoBBDD) As String
        Select Case f.TipoDato
            Case DataType.Bit           : Return "BOOLEAN"
            Case DataType.TinyInt       : Return "SMALLINT"
            Case DataType.SmallInt      : Return "SMALLINT"
            Case DataType.DbInt         : Return "INTEGER"
            Case DataType.BigInt        : Return "BIGINT"
            Case DataType.DbDecimal     : Return "DECIMAL(" & f.Precision & "," & f.Escala & ")"
            Case DataType.DbNumeric     : Return "NUMERIC(" & f.Precision & "," & f.Escala & ")"
            Case DataType.Money         : Return "NUMERIC(19,4)"
            Case DataType.SmallMoney    : Return "NUMERIC(10,4)"
            Case DataType.DbFloat       : Return "DOUBLE PRECISION"
            Case DataType.DbReal        : Return "REAL"
            Case DataType.DbChar        : Return "CHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarChar       : If f.LongitudMax Then Return "TEXT" Else Return "VARCHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarCharMax    : Return "TEXT"
            Case DataType.DbText        : Return "TEXT"
            Case DataType.NChar         : Return "CHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.NVarChar      : If f.LongitudMax Then Return "TEXT" Else Return "VARCHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.NVarCharMax   : Return "TEXT"
            Case DataType.NText         : Return "TEXT"
            Case DataType.DbBinary      : Return "BYTEA"
            Case DataType.VarBinary     : Return "BYTEA"
            Case DataType.VarBinaryMax  : Return "BYTEA"
            Case DataType.DbImage       : Return "BYTEA"
            Case DataType.DateOnly      : Return "DATE"
            Case DataType.TimeOnly      : Return "TIME"
            Case DataType.DbDateTime    : Return "TIMESTAMP"
            Case DataType.DateTime2     : Return "TIMESTAMP(6)"
            Case DataType.SmallDateTime : Return "TIMESTAMP"
            Case DataType.DateTimeOffset: Return "TIMESTAMPTZ"
            Case DataType.DbTimestamp   : Return "TIMESTAMP"
            Case DataType.UniqueIdentifier: Return "UUID"
            Case DataType.DbXml         : Return "XML"
            Case DataType.SqlVariant    : Return "TEXT"
            Case DataType.RowVersion    : Return "BYTEA"
            Case Else                   : Return "INTEGER"
        End Select
    End Function

    Private Function AlterFK(r As RelacionBBDD, ft As TablaBBDD, tt As TablaBBDD) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("ALTER TABLE " & Q(ft.Nombre))
        sb.AppendLine("  ADD CONSTRAINT """ & r.Nombre & """")
        sb.AppendLine("  FOREIGN KEY (" & Q(r.CampoFKNombre) & ")")
        sb.AppendLine("  REFERENCES " & Q(tt.Nombre) & " (" & Q(r.CampoPKNombre) & ")")
        sb.AppendLine("  ON DELETE " & OnAction(r.OnDelete))
        sb.AppendLine("  ON UPDATE " & OnAction(r.OnUpdate) & ";")
        If r.Disabled Then
            sb.AppendLine("-- (constraint disabled — no equivalent directe en PostgreSQL)")
        End If
        sb.AppendLine()
        Return sb.ToString()
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

    ' Double-quote per a PostgreSQL
    Private Function Q(nom As String) As String
        Return """" & nom & """"
    End Function

End Class
