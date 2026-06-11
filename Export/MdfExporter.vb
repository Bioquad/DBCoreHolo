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
Imports System.Data
Imports System.Collections.Generic
Imports System.Text
Imports Microsoft.Data.SqlClient

' ============================================================
' MdfExporter.vb  —  DB-Core Holographic
' Crea o sobreescriu una base de dades LocalDB a partir del
' model de projecte (ProyectoBBDD) i genera el fitxer .mdf.
'
' Requereix: Microsoft.Data.SqlClient (NuGet)
'
' MODES D'OPERACIÓ (ExportMode)
'   CreateNew    → crea una BD nova; falla si ja existeix
'   DropAndCreate → elimina la BD si existeix i la recrea (DESTRUCTIU)
'   AlterExisting → intenta aplicar diferències (ADD COLUMN, ADD CONSTRAINT)
'                   sobre una BD existent; no elimina el que ja hi ha
' ============================================================
Public Module MdfExporter

    ' ── Enumeració del mode d'exportació ────────────────────────────────────
    Public Enum ExportMode
        CreateNew = 0
        DropAndCreate = 1
        AlterExisting = 2
    End Enum

    ' ── Resultat de l'exportació ─────────────────────────────────────────────
    Public Class ExportResult
        Public Property OK As Boolean = False
        Public Property DbName As String = ""
        Public Property MdfPath As String = ""
        Public Property TaulesCreades As Integer = 0
        Public Property TaulesAlterades As Integer = 0
        Public Property RelacionsCreades As Integer = 0
        Public Property Advertències As New List(Of String)()
        Public Property MissatgeError As String = ""
    End Class

    ' ════════════════════════════════════════════════════════════════════════
    ' MÈTODE PRINCIPAL
    ' mdfDir   : directori on es crearà el fitxer .mdf
    ' dbName   : nom de la base de dades (també el nom del fitxer)
    ' p        : projecte a exportar
    ' mode     : ExportMode
    ' ════════════════════════════════════════════════════════════════════════
    Public Function Exportar(mdfDir As String,
                             dbName As String,
                             p As ProyectoBBDD,
                             mode As ExportMode) As ExportResult

        Dim res As New ExportResult()
        res.DbName  = dbName
        res.MdfPath = IO.Path.Combine(mdfDir, dbName & ".mdf")

        ' Connexió al master per crear/eliminar BD
        Dim masterConn As String =
            "Data Source=(LocalDB)\MSSQLLocalDB;" &
            "Initial Catalog=master;" &
            "Integrated Security=True;Connect Timeout=15;"

        Try
            Using conn As New SqlConnection(masterConn)
                conn.Open()

                Dim exists As Boolean = BdExisteix(conn, dbName)

                Select Case mode
                    Case ExportMode.CreateNew
                        If exists Then
                            res.MissatgeError = $"La base de dades '{dbName}' ja existeix. " &
                                                "Usa el mode DropAndCreate per sobreescriure-la."
                            Return res
                        End If
                        CrearBd(conn, dbName, res.MdfPath)

                    Case ExportMode.DropAndCreate
                        If exists Then
                            EliminarBd(conn, dbName)
                        End If
                        CrearBd(conn, dbName, res.MdfPath)

                    Case ExportMode.AlterExisting
                        If Not exists Then
                            CrearBd(conn, dbName, res.MdfPath)
                        End If
                        ' En mode Alter, la BD ja existeix o acaba de ser creada
                End Select
            End Using

            ' Connexió a la BD de destí
            Dim destConn As String =
                "Data Source=(LocalDB)\MSSQLLocalDB;" &
                "AttachDbFilename=" & res.MdfPath & ";" &
                "Initial Catalog=" & dbName & ";" &
                "Integrated Security=True;Connect Timeout=15;"

            Using conn As New SqlConnection(destConn)
                conn.Open()

                If mode = ExportMode.AlterExisting Then
                    AplicarDiferencies(conn, p, res)
                Else
                    AplicarDDLComplet(conn, p, res)
                End If
            End Using

            res.OK = True

        Catch ex As Exception
            res.MissatgeError = ex.Message
        End Try

        Return res
    End Function

    ' ════════════════════════════════════════════════════════════════════════
    ' HELPERS DE GESTIÓ DE LA BASE DE DADES
    ' ════════════════════════════════════════════════════════════════════════

    Private Function BdExisteix(conn As SqlConnection, dbName As String) As Boolean
        Dim sql As String = "SELECT COUNT(*) FROM sys.databases WHERE name = @n"
        Using cmd As New SqlCommand(sql, conn)
            cmd.Parameters.AddWithValue("@n", dbName)
            Return CInt(cmd.ExecuteScalar()) > 0
        End Using
    End Function

    Private Sub CrearBd(conn As SqlConnection, dbName As String, mdfPath As String)
        ' Assegurem que el directori existeix
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(mdfPath))

        Dim ldfPath As String = IO.Path.Combine(
            IO.Path.GetDirectoryName(mdfPath),
            dbName & "_log.ldf")

        ' Noms de fitxer sense apòstrofe (validació bàsica)
        If mdfPath.Contains("'") OrElse dbName.Contains("'") Then
            Throw New Exception("El nom de la BD o la ruta no pot contenir apòstrofes.")
        End If

        Dim sql As String =
            "CREATE DATABASE [" & dbName & "] ON PRIMARY " &
            "(NAME = N'" & dbName & "', FILENAME = N'" & mdfPath & "', " &
            " SIZE = 8192KB, FILEGROWTH = 65536KB) " &
            "LOG ON " &
            "(NAME = N'" & dbName & "_log', FILENAME = N'" & ldfPath & "', " &
            " SIZE = 8192KB, FILEGROWTH = 65536KB);"

        Using cmd As New SqlCommand(sql, conn)
            cmd.CommandTimeout = 60
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Sub EliminarBd(conn As SqlConnection, dbName As String)
        ' Expulsar connexions actives abans d'eliminar
        Dim sqlKill As String =
            "ALTER DATABASE [" & dbName & "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;"
        Using cmd As New SqlCommand(sqlKill, conn)
            cmd.CommandTimeout = 30
            Try
                cmd.ExecuteNonQuery()
            Catch
                ' Ignorem si no estava en ús
            End Try
        End Using

        Dim sqlDrop As String = "DROP DATABASE [" & dbName & "];"
        Using cmd As New SqlCommand(sqlDrop, conn)
            cmd.CommandTimeout = 30
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    ' ════════════════════════════════════════════════════════════════════════
    ' MODE CreateNew / DropAndCreate — DDL complet des de zero
    ' ════════════════════════════════════════════════════════════════════════

    Private Sub AplicarDDLComplet(conn As SqlConnection, p As ProyectoBBDD, res As ExportResult)
        ' 1. Crear totes les taules
        For Each t As TablaBBDD In p.Taules
            Dim sql As String = BuildCreateTable(t)
            ExecutarDDL(conn, sql, res)
            res.TaulesCreades += 1
        Next

        ' 2. Afegir Foreign Keys
        For Each r As RelacionBBDD In p.Relacions
            Dim ft As TablaBBDD = Nothing
            Dim tt As TablaBBDD = Nothing
            For Each t As TablaBBDD In p.Taules
                If t.Id = r.TablaOrigenId Then ft = t
                If t.Id = r.TablaDestinoId Then tt = t
            Next
            If ft IsNot Nothing AndAlso tt IsNot Nothing Then
                Dim sql As String = BuildAlterFK(r, ft, tt)
                ExecutarDDL(conn, sql, res)
                res.RelacionsCreades += 1
            End If
        Next

        ' 3. Índexos FK opcionals
        For Each r As RelacionBBDD In p.Relacions
            If r.CrearIndexFK Then
                Dim ft As TablaBBDD = Nothing
                For Each t As TablaBBDD In p.Taules
                    If t.Id = r.TablaOrigenId Then ft = t
                Next
                If ft IsNot Nothing Then
                    Dim sql As String =
                        "CREATE NONCLUSTERED INDEX [IX_" & ft.Nombre & "_" & r.CampoFKNombre & "] " &
                        "ON [" & ft.Schema & "].[" & ft.Nombre & "] ([" & r.CampoFKNombre & "] ASC);"
                    ExecutarDDL(conn, sql, res)
                End If
            End If
        Next

        ' 4. Extended properties (MS_Description)
        For Each t As TablaBBDD In p.Taules
            If Not String.IsNullOrEmpty(t.Descripcion) Then
                Dim sql As String = BuildExtPropTaula(t)
                ExecutarDDL(conn, sql, res)
            End If
            For Each f As CampoBBDD In t.Fields
                If Not String.IsNullOrEmpty(f.Descripcion) Then
                    Dim sql As String = BuildExtPropCamp(t, f)
                    ExecutarDDL(conn, sql, res)
                End If
            Next
        Next
    End Sub

    ' ════════════════════════════════════════════════════════════════════════
    ' MODE AlterExisting — aplica només les diferències
    ' Afegeix taules i columnes noves; crea FK noves; actualitza descriptions
    ' NO elimina taules/columnes que ja no estiguin al model
    ' ════════════════════════════════════════════════════════════════════════

    Private Sub AplicarDiferencies(conn As SqlConnection, p As ProyectoBBDD, res As ExportResult)
        ' ── Llegir l'estat actual de la BD ───────────────────────────────────
        Dim taulesExistents As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim colsExistents As New Dictionary(Of String, HashSet(Of String))(StringComparer.OrdinalIgnoreCase)
        Dim fksExistents As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Using cmd As New SqlCommand(
            "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'", conn)
        Using rdr As SqlDataReader = cmd.ExecuteReader()
            Do While rdr.Read()
                Dim key As String = rdr.GetString(0) & "." & rdr.GetString(1).ToUpper()
                taulesExistents.Add(key)
                colsExistents(key) = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Loop
        End Using
        End Using

        Using cmd As New SqlCommand(
            "SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS", conn)
        Using rdr As SqlDataReader = cmd.ExecuteReader()
            Do While rdr.Read()
                Dim key As String = rdr.GetString(0) & "." & rdr.GetString(1).ToUpper()
                If colsExistents.ContainsKey(key) Then
                    colsExistents(key).Add(rdr.GetString(2).ToUpper())
                End If
            Loop
        End Using
        End Using

        Using cmd As New SqlCommand(
            "SELECT name FROM sys.foreign_keys", conn)
        Using rdr As SqlDataReader = cmd.ExecuteReader()
            Do While rdr.Read()
                fksExistents.Add(rdr.GetString(0).ToUpper())
            Loop
        End Using
        End Using

        ' ── Processar taules del model ────────────────────────────────────────
        For Each t As TablaBBDD In p.Taules
            Dim key As String = t.Schema & "." & t.Nombre.ToUpper()
            If Not taulesExistents.Contains(key) Then
                ' Taula nova: crear-la sencera
                ExecutarDDL(conn, BuildCreateTable(t), res)
                res.TaulesCreades += 1
            Else
                ' Taula existent: afegir columnes noves
                Dim colsActuals As HashSet(Of String) = colsExistents(key)
                Dim colsNoves As Integer = 0
                For Each f As CampoBBDD In t.Fields
                    If Not colsActuals.Contains(f.Nombre.ToUpper()) Then
                        ' ADD COLUMN — no es pot afegir NOT NULL sense DEFAULT si la taula té files
                        ' Afegim com NULL primer i llancem advertència
                        Dim addSql As String = BuildAddColumn(t, f, res)
                        If Not String.IsNullOrEmpty(addSql) Then
                            ExecutarDDL(conn, addSql, res)
                            colsNoves += 1
                        End If
                    End If
                Next
                If colsNoves > 0 Then res.TaulesAlterades += 1
            End If
        Next

        ' ── Processar Foreign Keys ────────────────────────────────────────────
        For Each r As RelacionBBDD In p.Relacions
            If Not fksExistents.Contains(r.Nombre.ToUpper()) Then
                Dim ft As TablaBBDD = Nothing
                Dim tt As TablaBBDD = Nothing
                For Each t As TablaBBDD In p.Taules
                    If t.Id = r.TablaOrigenId Then ft = t
                    If t.Id = r.TablaDestinoId Then tt = t
                Next
                If ft IsNot Nothing AndAlso tt IsNot Nothing Then
                    ExecutarDDL(conn, BuildAlterFK(r, ft, tt), res)
                    res.RelacionsCreades += 1
                End If
            End If
        Next

        ' ── Índexos FK ────────────────────────────────────────────────────────
        For Each r As RelacionBBDD In p.Relacions
            If r.CrearIndexFK Then
                Dim ft As TablaBBDD = Nothing
                For Each t As TablaBBDD In p.Taules
                    If t.Id = r.TablaOrigenId Then ft = t
                Next
                If ft IsNot Nothing Then
                    Dim ixNom As String = "IX_" & ft.Nombre & "_" & r.CampoFKNombre
                    Dim ixExisteix As Boolean = False
                    Using cmd As New SqlCommand(
                        "SELECT COUNT(*) FROM sys.indexes WHERE name=@n", conn)
                        cmd.Parameters.AddWithValue("@n", ixNom)
                        ixExisteix = CInt(cmd.ExecuteScalar()) > 0
                    End Using
                    If Not ixExisteix Then
                        Dim sql As String =
                            "CREATE NONCLUSTERED INDEX [" & ixNom & "] " &
                            "ON [" & ft.Schema & "].[" & ft.Nombre & "] " &
                            "([" & r.CampoFKNombre & "] ASC);"
                        ExecutarDDL(conn, sql, res)
                    End If
                End If
            End If
        Next

        ' ── Extended properties ────────────────────────────────────────────────
        For Each t As TablaBBDD In p.Taules
            If Not String.IsNullOrEmpty(t.Descripcion) Then
                ActualitzarExtPropTaula(conn, t, res)
            End If
            For Each f As CampoBBDD In t.Fields
                If Not String.IsNullOrEmpty(f.Descripcion) Then
                    ActualitzarExtPropCamp(conn, t, f, res)
                End If
            Next
        Next
    End Sub

    ' ════════════════════════════════════════════════════════════════════════
    ' CONSTRUCTORS DDL
    ' ════════════════════════════════════════════════════════════════════════

    Private Function BuildCreateTable(t As TablaBBDD) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("CREATE TABLE [" & t.Schema & "].[" & t.Nombre & "] (")

        Dim lines As New List(Of String)()
        For Each f As CampoBBDD In t.Fields
            lines.Add("    " & BuildFieldDDL(f))
        Next

        Dim pk As CampoBBDD = t.PKField
        If pk IsNot Nothing Then
            lines.Add("    CONSTRAINT [PK_" & t.Nombre & "] PRIMARY KEY CLUSTERED ([" & pk.Nombre & "] ASC)")
        End If

        For Each f As CampoBBDD In t.Fields
            If f.EsUnique AndAlso Not f.EsPK Then
                lines.Add("    CONSTRAINT [UQ_" & t.Nombre & "_" & f.Nombre & "] UNIQUE ([" & f.Nombre & "])")
            End If
        Next

        For Each f As CampoBBDD In t.Fields
            If Not String.IsNullOrEmpty(f.CheckExpression) Then
                lines.Add("    CONSTRAINT [CK_" & t.Nombre & "_" & f.Nombre & "] CHECK (" & f.CheckExpression & ")")
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
        Return sb.ToString()
    End Function

    Private Function BuildFieldDDL(f As CampoBBDD) As String
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

        sb.Append(If(f.NotNull, " NOT NULL", " NULL"))

        If Not String.IsNullOrEmpty(f.DefaultValue) Then
            sb.Append(" CONSTRAINT [DF_" & f.Nombre & "] DEFAULT (" & f.DefaultValue & ")")
        End If

        If f.DataMask <> DataMaskFunction.NoMask Then
            sb.Append(" MASKED WITH (FUNCTION = '" & MaskFn(f) & "')")
        End If

        Return sb.ToString()
    End Function

    ' Construeix ADD COLUMN per al mode Alter
    ' Si la columna és NOT NULL i no té DEFAULT, la creem com NULL i avisem
    Private Function BuildAddColumn(t As TablaBBDD, f As CampoBBDD, res As ExportResult) As String
        If f.EsPK Then
            res.Advertències.Add(
                $"[{t.Nombre}].[{f.Nombre}]: no s'ha afegit perquè és PK (no es pot afegir PK a taula existent sense reescriure-la).")
            Return ""
        End If
        If f.EsCalculado Then
            Return $"ALTER TABLE [{t.Schema}].[{t.Nombre}] ADD [{f.Nombre}] AS ({f.FormulaCalculo}){If(f.EsPersistido, " PERSISTED", "")};"
        End If

        Dim sb As New StringBuilder()
        sb.Append($"ALTER TABLE [{t.Schema}].[{t.Nombre}] ADD [{f.Nombre}] ")
        sb.Append(SqlType(f))

        If f.EsIdentity Then
            sb.Append($" IDENTITY({f.IdentitySeed},{f.IdentityIncrement})")
        End If

        ' Si és NOT NULL sense DEFAULT, avisem i afegim com NULL
        If f.NotNull AndAlso String.IsNullOrEmpty(f.DefaultValue) AndAlso Not f.EsIdentity Then
            res.Advertències.Add(
                $"[{t.Nombre}].[{f.Nombre}]: afegida com NULL (era NOT NULL sense DEFAULT; " &
                "si la taula no té files, pots fer-la NOT NULL manualment amb ALTER COLUMN).")
            sb.Append(" NULL")
        ElseIf f.NotNull Then
            sb.Append(" NOT NULL")
        Else
            sb.Append(" NULL")
        End If

        If Not String.IsNullOrEmpty(f.DefaultValue) Then
            sb.Append($" CONSTRAINT [DF_{f.Nombre}] DEFAULT ({f.DefaultValue})")
        End If

        sb.Append(";")
        Return sb.ToString()
    End Function

    Private Function BuildAlterFK(r As RelacionBBDD, ft As TablaBBDD, tt As TablaBBDD) As String
        Dim wc As String = If(r.WithCheck = WithCheckOption.WithNoCheck, "WITH NOCHECK", "WITH CHECK")
        Dim onDel As String = OnActLabel(r.OnDelete)
        Dim onUpd As String = OnActLabel(r.OnUpdate)

        Dim sb As New StringBuilder()
        sb.AppendLine($"ALTER TABLE [{ft.Schema}].[{ft.Nombre}] {wc}")
        sb.AppendLine($"    ADD CONSTRAINT [{r.Nombre}]")
        sb.AppendLine($"    FOREIGN KEY ([{r.CampoFKNombre}])")
        sb.AppendLine($"    REFERENCES [{tt.Schema}].[{tt.Nombre}] ([{r.CampoPKNombre}])")
        sb.AppendLine($"    ON DELETE {onDel}")
        sb.AppendLine($"    ON UPDATE {onUpd}")
        If r.NotForReplication Then sb.AppendLine("    NOT FOR REPLICATION")
        sb.Append(";")
        If r.Disabled Then
            sb.AppendLine()
            sb.Append($"ALTER TABLE [{ft.Schema}].[{ft.Nombre}] NOCHECK CONSTRAINT [{r.Nombre}];")
        End If
        Return sb.ToString()
    End Function

    Private Function BuildExtPropTaula(t As TablaBBDD) As String
        Dim d As String = t.Descripcion.Replace("'", "''")
        Return $"EXEC sys.sp_addextendedproperty " &
               $"@name=N'MS_Description', @value=N'{d}', " &
               $"@level0type=N'SCHEMA', @level0name=N'{t.Schema}', " &
               $"@level1type=N'TABLE', @level1name=N'{t.Nombre}';"
    End Function

    Private Function BuildExtPropCamp(t As TablaBBDD, f As CampoBBDD) As String
        Dim d As String = f.Descripcion.Replace("'", "''")
        Return $"EXEC sys.sp_addextendedproperty " &
               $"@name=N'MS_Description', @value=N'{d}', " &
               $"@level0type=N'SCHEMA', @level0name=N'{t.Schema}', " &
               $"@level1type=N'TABLE', @level1name=N'{t.Nombre}', " &
               $"@level2type=N'COLUMN', @level2name=N'{f.Nombre}';"
    End Function

    ' Actualitza o crea l'extended property de taula en mode Alter
    Private Sub ActualitzarExtPropTaula(conn As SqlConnection, t As TablaBBDD, res As ExportResult)
        Dim existeix As Boolean = False
        Using cmd As New SqlCommand(
            "SELECT COUNT(*) FROM sys.extended_properties ep " &
            "JOIN sys.tables tbl ON ep.major_id = tbl.object_id " &
            "WHERE ep.name='MS_Description' AND ep.minor_id=0 AND tbl.name=@n", conn)
            cmd.Parameters.AddWithValue("@n", t.Nombre)
            existeix = CInt(cmd.ExecuteScalar()) > 0
        End Using

        Dim verb As String = If(existeix, "UPDATE", "ADD")
        Dim d As String = t.Descripcion.Replace("'", "''")
        Dim sql As String =
            $"EXEC sys.sp_{verb}extendedproperty " &
            $"@name=N'MS_Description', @value=N'{d}', " &
            $"@level0type=N'SCHEMA', @level0name=N'{t.Schema}', " &
            $"@level1type=N'TABLE', @level1name=N'{t.Nombre}';"
        ExecutarDDL(conn, sql, res)
    End Sub

    ' Actualitza o crea l'extended property de camp en mode Alter
    Private Sub ActualitzarExtPropCamp(conn As SqlConnection, t As TablaBBDD, f As CampoBBDD, res As ExportResult)
        Dim existeix As Boolean = False
        Using cmd As New SqlCommand(
            "SELECT COUNT(*) FROM sys.extended_properties ep " &
            "JOIN sys.tables tbl ON ep.major_id = tbl.object_id " &
            "JOIN sys.columns col ON ep.major_id = col.object_id AND ep.minor_id = col.column_id " &
            "WHERE ep.name='MS_Description' AND tbl.name=@t AND col.name=@c", conn)
            cmd.Parameters.AddWithValue("@t", t.Nombre)
            cmd.Parameters.AddWithValue("@c", f.Nombre)
            existeix = CInt(cmd.ExecuteScalar()) > 0
        End Using

        Dim verb As String = If(existeix, "UPDATE", "ADD")
        Dim d As String = f.Descripcion.Replace("'", "''")
        Dim sql As String =
            $"EXEC sys.sp_{verb}extendedproperty " &
            $"@name=N'MS_Description', @value=N'{d}', " &
            $"@level0type=N'SCHEMA', @level0name=N'{t.Schema}', " &
            $"@level1type=N'TABLE', @level1name=N'{t.Nombre}', " &
            $"@level2type=N'COLUMN', @level2name=N'{f.Nombre}';"
        ExecutarDDL(conn, sql, res)
    End Sub

    ' ════════════════════════════════════════════════════════════════════════
    ' EXECUTOR SQL — captura errors no fatals com advertències
    ' ════════════════════════════════════════════════════════════════════════

    Private Sub ExecutarDDL(conn As SqlConnection, sql As String, res As ExportResult)
        If String.IsNullOrWhiteSpace(sql) Then Return
        Try
            Using cmd As New SqlCommand(sql, conn)
                cmd.CommandTimeout = 60
                cmd.ExecuteNonQuery()
            End Using
        Catch ex As SqlException
            ' Errors no fatals: objecte ja existeix (2714), índex duplicat (1913)
            If ex.Number = 2714 OrElse ex.Number = 1913 Then
                res.Advertències.Add($"Ja existia (ignorat): {TruncSql(sql)} [{ex.Number}]")
            Else
                ' Error inesperat: relancçem per aturar el procés
                Throw New Exception($"Error SQL {ex.Number}: {ex.Message}" & Environment.NewLine &
                                    "SQL: " & TruncSql(sql), ex)
            End If
        End Try
    End Sub

    Private Function TruncSql(sql As String) As String
        Dim s As String = sql.Trim().Replace(Environment.NewLine, " ")
        If s.Length > 120 Then Return s.Substring(0, 120) & "…"
        Return s
    End Function

    ' ════════════════════════════════════════════════════════════════════════
    ' HELPERS DE MAPPING SQL
    ' ════════════════════════════════════════════════════════════════════════

    Private Function SqlType(f As CampoBBDD) As String
        Select Case f.TipoDato
            Case DataType.Bit             : Return "BIT"
            Case DataType.TinyInt         : Return "TINYINT"
            Case DataType.SmallInt        : Return "SMALLINT"
            Case DataType.DbInt           : Return "INT"
            Case DataType.BigInt          : Return "BIGINT"
            Case DataType.DbDecimal       : Return "DECIMAL(" & f.Precision & "," & f.Escala & ")"
            Case DataType.DbNumeric       : Return "NUMERIC(" & f.Precision & "," & f.Escala & ")"
            Case DataType.Money           : Return "MONEY"
            Case DataType.SmallMoney      : Return "SMALLMONEY"
            Case DataType.DbFloat         : Return "FLOAT"
            Case DataType.DbReal          : Return "REAL"
            Case DataType.DbChar          : Return "CHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarChar
                Return If(f.LongitudMax, "VARCHAR(MAX)", "VARCHAR(" & Math.Max(1, f.Longitud) & ")")
            Case DataType.VarCharMax      : Return "VARCHAR(MAX)"
            Case DataType.DbText          : Return "TEXT"
            Case DataType.NChar           : Return "NCHAR(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.NVarChar
                Return If(f.LongitudMax, "NVARCHAR(MAX)", "NVARCHAR(" & Math.Max(1, f.Longitud) & ")")
            Case DataType.NVarCharMax     : Return "NVARCHAR(MAX)"
            Case DataType.NText           : Return "NTEXT"
            Case DataType.DbBinary        : Return "BINARY(" & Math.Max(1, f.Longitud) & ")"
            Case DataType.VarBinary
                Return If(f.LongitudMax, "VARBINARY(MAX)", "VARBINARY(" & Math.Max(1, f.Longitud) & ")")
            Case DataType.VarBinaryMax    : Return "VARBINARY(MAX)"
            Case DataType.DbImage         : Return "IMAGE"
            Case DataType.DateOnly        : Return "DATE"
            Case DataType.TimeOnly        : Return "TIME"
            Case DataType.DbDateTime      : Return "DATETIME"
            Case DataType.DateTime2       : Return "DATETIME2"
            Case DataType.SmallDateTime   : Return "SMALLDATETIME"
            Case DataType.DateTimeOffset  : Return "DATETIMEOFFSET"
            Case DataType.DbTimestamp     : Return "TIMESTAMP"
            Case DataType.UniqueIdentifier: Return "UNIQUEIDENTIFIER"
            Case DataType.DbXml           : Return "XML"
            Case DataType.DbGeography     : Return "GEOGRAPHY"
            Case DataType.DbGeometry      : Return "GEOMETRY"
            Case DataType.HierarchyId     : Return "HIERARCHYID"
            Case DataType.SqlVariant      : Return "SQL_VARIANT"
            Case DataType.RowVersion      : Return "ROWVERSION"
            Case Else                     : Return "INT"
        End Select
    End Function

    Private Function MaskFn(f As CampoBBDD) As String
        Select Case f.DataMask
            Case DataMaskFunction.DefaultMask : Return "default()"
            Case DataMaskFunction.MaskEmail   : Return "email()"
            Case DataMaskFunction.MaskPartial
                Return "partial(" & f.MaskPrefix & ",""" & f.MaskPadding & """," & f.MaskSuffix & ")"
            Case DataMaskFunction.MaskRandom  : Return "random(1,100)"
            Case Else                         : Return "default()"
        End Select
    End Function

    Private Function OnActLabel(a As OnDeleteUpdateAction) As String
        Select Case a
            Case OnDeleteUpdateAction.DoCascade  : Return "CASCADE"
            Case OnDeleteUpdateAction.SetNull    : Return "SET NULL"
            Case OnDeleteUpdateAction.SetDefault : Return "SET DEFAULT"
            Case OnDeleteUpdateAction.DoRestrict : Return "NO ACTION"
            Case Else                            : Return "NO ACTION"
        End Select
    End Function

End Module
