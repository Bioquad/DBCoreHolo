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
' MySqlExporter.vb — Generador DDL per a MySQL 8 / MariaDB 10.5+
' ============================================================
Public Class MySqlExporter

    ' Avisos de conversió (tipus sense equivalent exacte, etc.)
    Private ReadOnly _avisos As New List(Of String)()

    Public Function Generar(p As ProyectoBBDD) As String
        _avisos.Clear()

        Dim cos As New StringBuilder()
        cos.AppendLine("SET FOREIGN_KEY_CHECKS = 0;")
        cos.AppendLine()
        For Each t As TablaBBDD In p.Taules
            cos.Append(CreateTable(t, p))
        Next
        cos.AppendLine("SET FOREIGN_KEY_CHECKS = 1;")
        cos.AppendLine()

        ' Descripció de relacions com a comentaris (MySQL no té extended properties)
        If p.Relacions.Any(Function(r) Not String.IsNullOrEmpty(r.Descripcion)) Then
            cos.AppendLine("-- ── Descripcions de relacions ──────────────────────")
            For Each r As RelacionBBDD In p.Relacions
                If Not String.IsNullOrEmpty(r.Descripcion) Then
                    cos.AppendLine("-- [FK " & r.Nombre & "] " & UnaLinia(r.Descripcion))
                End If
            Next
            cos.AppendLine()
        End If
        TSqlExporter.AfegirNotesDisseny(cos, p)

        Dim sb As New StringBuilder()
        sb.AppendLine("-- ============================================")
        sb.AppendLine("-- DDL MySQL/MariaDB generat per Holografic DB")
        sb.AppendLine("-- Projecte : " & UnaLinia(p.Nombre))
        sb.AppendLine("-- Motor    : MySQL 8 / MariaDB 10.5+")
        sb.AppendLine("-- Data     : " & DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
        sb.AppendLine("-- ============================================")
        If TeExpressions(p) Then
            _avisos.Add("Les expressions CHECK i de columnes calculades es copien del model: revisa que siguin sintaxi MySQL.")
        End If
        For Each a As String In _avisos.Distinct()
            sb.AppendLine("-- AVÍS: " & a)
        Next
        sb.AppendLine()
        sb.Append(cos)
        Return sb.ToString()
    End Function

    Private Function CreateTable(t As TablaBBDD, p As ProyectoBBDD) As String
        Dim lines As New List(Of String)()

        For Each f As CampoBBDD In t.Fields
            lines.Add("  " & FieldDDL(t, f))
        Next

        ' PRIMARY KEY (simple o composta)
        Dim pks As List(Of CampoBBDD) = t.PKFields
        If pks.Count > 0 Then
            lines.Add("  PRIMARY KEY (" & String.Join(", ", pks.Select(Function(k) Bt(k.Nombre))) & ")")
        End If

        ' UNIQUE
        For Each f As CampoBBDD In t.Fields
            If f.EsUnique AndAlso Not (f.EsPK AndAlso pks.Count = 1) Then
                lines.Add("  UNIQUE KEY " & Bt("UQ_" & t.Nombre & "_" & f.Nombre) & " (" & Bt(f.Nombre) & ")")
            End If
        Next

        ' CHECK (aplicats a partir de MySQL 8.0.16 / MariaDB 10.2)
        For Each f As CampoBBDD In t.Fields
            If Not String.IsNullOrWhiteSpace(f.CheckExpression) Then
                lines.Add("  CONSTRAINT " & Bt("CK_" & t.Nombre & "_" & f.Nombre) &
                          " CHECK (" & ConvertirExpressio(f.CheckExpression.Trim(), Motor.MySql, t) & ")")
            End If
        Next

        ' Índex i FOREIGN KEY inline. L'índex es declara abans de la FK
        ' perquè InnoDB el reutilitzi en lloc de crear-ne un de duplicat.
        For Each r As RelacionBBDD In p.Relacions
            If r.TablaOrigenId <> t.Id Then Continue For
            Dim tt As TablaBBDD = p.Taules.FirstOrDefault(Function(x) x.Id = r.TablaDestinoId)
            If tt Is Nothing Then Continue For

            If r.CrearIndexFK Then
                lines.Add("  KEY " & Bt("IX_" & t.Nombre & "_" & r.CampoFKNombre) & " (" & Bt(r.CampoFKNombre) & ")")
            End If
            lines.Add("  CONSTRAINT " & Bt(r.Nombre) &
                      " FOREIGN KEY (" & Bt(r.CampoFKNombre) & ")" &
                      " REFERENCES " & Bt(tt.Nombre) & " (" & Bt(r.CampoPKNombre) & ")" &
                      " ON DELETE " & OnAction(r.OnDelete, r.Nombre) &
                      " ON UPDATE " & OnAction(r.OnUpdate, r.Nombre))
            If r.Disabled Then
                _avisos.Add("FK """ & r.Nombre & """ està desactivada al model; MySQL no admet FK desactivades i es crea activa.")
            End If
        Next

        Dim opcions As New StringBuilder(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci")
        Dim ident As CampoBBDD = t.Fields.FirstOrDefault(Function(f) f.EsIdentity)
        If ident IsNot Nothing AndAlso ident.IdentitySeed > 1 Then
            opcions.Append(" AUTO_INCREMENT=" & ident.IdentitySeed)
        End If
        If ident IsNot Nothing AndAlso ident.IdentityIncrement <> 1 Then
            _avisos.Add("[" & t.Nombre & "].[" & ident.Nombre & "]: MySQL no admet increment d'AUTO_INCREMENT per columna (auto_increment_increment és global).")
        End If
        If Not String.IsNullOrEmpty(t.Descripcion) Then
            opcions.Append(" COMMENT='" & Lit(t.Descripcion) & "'")
        End If

        Dim sb As New StringBuilder()
        sb.AppendLine("CREATE TABLE " & Bt(t.Nombre) & " (")
        sb.AppendLine(String.Join("," & Environment.NewLine, lines))
        sb.AppendLine(opcions.ToString() & ";")
        sb.AppendLine()
        Return sb.ToString()
    End Function

    Private Function FieldDDL(t As TablaBBDD, f As CampoBBDD) As String
        Dim sb As New StringBuilder()
        sb.Append(Bt(f.Nombre) & " ")

        If f.EsCalculado Then
            sb.Append(SqlType(t, f) & " GENERATED ALWAYS AS (" & ConvertirExpressio(f.FormulaCalculo, Motor.MySql, t) & ")")
            sb.Append(If(f.EsPersistido, " STORED", " VIRTUAL"))
        Else
            sb.Append(SqlType(t, f))
            If f.EsIdentity Then sb.Append(" AUTO_INCREMENT")
            sb.Append(If(f.NotNull OrElse f.EsPK OrElse f.EsIdentity, " NOT NULL", " NULL"))
            If Not String.IsNullOrWhiteSpace(f.DefaultValue) Then
                Dim dv As String = ConvertirDefault(f.DefaultValue, f, Motor.MySql, t)
                ' MySQL exigeix que CURRENT_TIMESTAMP tingui la mateixa precisió que la columna
                If dv = "CURRENT_TIMESTAMP" AndAlso FspMySql(f) > 0 Then
                    dv = "CURRENT_TIMESTAMP(" & FspMySql(f) & ")"
                End If
                sb.Append(" DEFAULT " & dv)
            End If
        End If

        If Not String.IsNullOrEmpty(f.Descripcion) Then
            sb.Append(" COMMENT '" & Lit(f.Descripcion) & "'")
        End If
        Return sb.ToString()
    End Function

    Private Function SqlType(t As TablaBBDD, f As CampoBBDD) As String
        Select Case f.TipoDato
            Case DataType.Bit              : Return "TINYINT(1)"
            Case DataType.TinyInt          : Return "TINYINT UNSIGNED"
            Case DataType.SmallInt         : Return "SMALLINT"
            Case DataType.DbInt            : Return "INT"
            Case DataType.BigInt           : Return "BIGINT"
            Case DataType.DbDecimal        : Return "DECIMAL(" & Prec(f) & "," & Esc(f) & ")"
            Case DataType.DbNumeric        : Return "NUMERIC(" & Prec(f) & "," & Esc(f) & ")"
            Case DataType.Money            : Return "DECIMAL(19,4)"
            Case DataType.SmallMoney       : Return "DECIMAL(10,4)"
            Case DataType.DbFloat          : Return "DOUBLE"
            Case DataType.DbReal           : Return "FLOAT"
            Case DataType.DbChar           : Return "CHAR(" & Math.Min(255, Lon(f)) & ")"
            Case DataType.VarChar          : Return If(f.LongitudMax, "LONGTEXT", "VARCHAR(" & Lon(f) & ")")
            Case DataType.VarCharMax       : Return "LONGTEXT"
            Case DataType.DbText           : Return "TEXT"
            Case DataType.NChar            : Return "CHAR(" & Math.Min(255, Lon(f)) & ") CHARACTER SET utf8mb4"
            Case DataType.NVarChar         : Return If(f.LongitudMax, "LONGTEXT CHARACTER SET utf8mb4", "VARCHAR(" & Lon(f) & ") CHARACTER SET utf8mb4")
            Case DataType.NVarCharMax      : Return "LONGTEXT CHARACTER SET utf8mb4"
            Case DataType.NText            : Return "LONGTEXT CHARACTER SET utf8mb4"
            Case DataType.DbBinary         : Return "BINARY(" & Math.Min(255, Lon(f)) & ")"
            Case DataType.VarBinary        : Return If(f.LongitudMax, "LONGBLOB", "VARBINARY(" & Lon(f) & ")")
            Case DataType.VarBinaryMax     : Return "LONGBLOB"
            Case DataType.DbImage          : Return "LONGBLOB"
            Case DataType.DateOnly         : Return "DATE"
            Case DataType.TimeOnly         : Return "TIME(6)"
            Case DataType.DbDateTime       : Return "DATETIME(3)"
            Case DataType.DateTime2        : Return "DATETIME(6)"
            Case DataType.SmallDateTime    : Return "DATETIME"
            Case DataType.DateTimeOffset
                Avis(t, f, "DATETIMEOFFSET → DATETIME(6): es perd la zona horària")
                Return "DATETIME(6)"
            Case DataType.DbTimestamp, DataType.RowVersion
                Return "TIMESTAMP(6)"
            Case DataType.UniqueIdentifier : Return "CHAR(36)"
            Case DataType.DbXml            : Return "LONGTEXT"
            Case DataType.DbGeography
                Avis(t, f, "GEOGRAPHY → GEOMETRY (indica el SRID si cal, p.ex. 4326)")
                Return "GEOMETRY"
            Case DataType.DbGeometry       : Return "GEOMETRY"
            Case DataType.HierarchyId
                Avis(t, f, "HIERARCHYID no existeix a MySQL → VARCHAR(4000)")
                Return "VARCHAR(4000)"
            Case DataType.SqlVariant
                Avis(t, f, "SQL_VARIANT no existeix a MySQL → TEXT")
                Return "TEXT"
            Case Else                      : Return "INT"
        End Select
    End Function

    Private Function OnAction(a As OnDeleteUpdateAction, nomFk As String) As String
        Select Case a
            Case OnDeleteUpdateAction.DoCascade  : Return "CASCADE"
            Case OnDeleteUpdateAction.SetNull    : Return "SET NULL"
            Case OnDeleteUpdateAction.SetDefault
                _avisos.Add("FK """ & nomFk & """: InnoDB no admet SET DEFAULT → s'exporta com a NO ACTION.")
                Return "NO ACTION"
            Case OnDeleteUpdateAction.DoRestrict : Return "RESTRICT"
            Case Else                            : Return "NO ACTION"
        End Select
    End Function

    ' Precisió de fraccions de segon que SqlType assigna a cada tipus data-hora
    Private Shared Function FspMySql(f As CampoBBDD) As Integer
        Select Case f.TipoDato
            Case DataType.DbDateTime
                Return 3
            Case DataType.DateTime2, DataType.DateTimeOffset, DataType.DbTimestamp, DataType.RowVersion
                Return 6
            Case Else
                Return 0
        End Select
    End Function

    Private Sub Avis(t As TablaBBDD, f As CampoBBDD, msg As String)
        _avisos.Add("[" & t.Nombre & "].[" & f.Nombre & "]: " & msg)
    End Sub

    Private Shared Function Lon(f As CampoBBDD) As Integer
        Return Math.Max(1, f.Longitud)
    End Function

    Private Shared Function Prec(f As CampoBBDD) As Integer
        Return Math.Min(65, Math.Max(1, f.Precision))
    End Function

    Private Shared Function Esc(f As CampoBBDD) As Integer
        Return Math.Min(Math.Min(30, Prec(f)), Math.Max(0, f.Escala))
    End Function

    Private Shared Function Lit(s As String) As String
        Return s.Replace("\", "\\").Replace("'", "''")
    End Function

    ' Backtick-quote per a MySQL (el ` intern es duplica)
    Private Shared Function Bt(nom As String) As String
        Return "`" & If(nom, "").Replace("`", "``") & "`"
    End Function

End Class
