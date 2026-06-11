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
' SqlServerConnector.vb  —  DB-Core Holographic
' Mòdul central de connexió i operacions amb SQL Server en xarxa.
'
' Responsabilitats:
'   - Construir i validar connection strings
'   - Llistar instàncies i bases de dades disponibles
'   - Importar estructura completa d'una BD (igual que MdfImporter
'     però via connexió de xarxa)
'   - Exportar/aplicar DDL al servidor (sense crear .mdf)
'   - Enviar parts del model (taules seleccionades + FK)
'
' Requereix: Microsoft.Data.SqlClient 5.2.1
' ============================================================
Public Module SqlServerConnector

    ' ════════════════════════════════════════════════════════
    ' MODEL DE CONNEXIÓ  —  dades persistibles
    ' ════════════════════════════════════════════════════════
    Public Class ConnexioServidor
        Public Property Servidor    As String = ""
        Public Property Usuari      As String = ""
        Public Property Contrasenya As String = ""
        Public Property BaseDades   As String = ""
        Public Property AuthWindows As Boolean = True
        Public Property Port        As Integer = 1433
        Public Property TimeoutSeg  As Integer = 15
        Public Property Encrypt     As Boolean = False

        Public Function BuildConnectionString() As String
            Dim sb As New StringBuilder()
            sb.Append("Data Source=")
            If Port <> 1433 Then
                sb.Append(Servidor & "," & Port)
            Else
                sb.Append(Servidor)
            End If
            sb.Append(";")
            If Not String.IsNullOrEmpty(BaseDades) Then
                sb.Append("Initial Catalog=" & BaseDades & ";")
            End If
            If AuthWindows Then
                sb.Append("Integrated Security=True;")
            Else
                sb.Append("User Id=" & Usuari & ";Password=" & Contrasenya & ";")
            End If
            sb.Append("Connect Timeout=" & TimeoutSeg & ";")
            sb.Append("Encrypt=" & If(Encrypt, "True", "False") & ";")
            sb.Append("TrustServerCertificate=True;")
            Return sb.ToString()
        End Function

        Public Function BuildMasterConnectionString() As String
            Dim master As New ConnexioServidor()
            master.Servidor    = Me.Servidor
            master.Port        = Me.Port
            master.AuthWindows = Me.AuthWindows
            master.Usuari      = Me.Usuari
            master.Contrasenya = Me.Contrasenya
            master.BaseDades   = "master"
            master.TimeoutSeg  = Me.TimeoutSeg
            master.Encrypt     = Me.Encrypt
            Return master.BuildConnectionString()
        End Function

        Public Function Copia() As ConnexioServidor
            Dim c As New ConnexioServidor()
            c.Servidor    = Me.Servidor
            c.Usuari      = Me.Usuari
            c.Contrasenya = Me.Contrasenya
            c.BaseDades   = Me.BaseDades
            c.AuthWindows = Me.AuthWindows
            c.Port        = Me.Port
            c.TimeoutSeg  = Me.TimeoutSeg
            c.Encrypt      = Me.Encrypt
            Return c
        End Function

        Public Overrides Function ToString() As String
            Return If(AuthWindows,
                      Servidor & " / " & BaseDades & " (Windows Auth)",
                      Servidor & " / " & BaseDades & " (" & Usuari & ")")
        End Function
    End Class

    ' ════════════════════════════════════════════════════════
    ' RESULTAT D'OPERACIÓ
    ' ════════════════════════════════════════════════════════
    Public Class ResultatOperacio
        Public Property OK              As Boolean = False
        Public Property TaulesCreades   As Integer = 0
        Public Property TaulesAlterades As Integer = 0
        Public Property RelacionsCreades As Integer = 0
        Public Property Advertencies    As New List(Of String)()
        Public Property MissatgeError   As String = ""

        Public Function ResumText() As String
            Dim sb As New StringBuilder()
            If OK Then
                sb.AppendLine("Operació completada correctament.")
                If TaulesCreades > 0   Then sb.AppendLine("  Taules creades   : " & TaulesCreades)
                If TaulesAlterades > 0 Then sb.AppendLine("  Taules alterades : " & TaulesAlterades)
                If RelacionsCreades > 0 Then sb.AppendLine("  Relacions creades: " & RelacionsCreades)
            Else
                sb.AppendLine("Error: " & MissatgeError)
            End If
            If Advertencies.Count > 0 Then
                sb.AppendLine("")
                sb.AppendLine("Advertències (" & Advertencies.Count & "):")
                For Each a As String In Advertencies
                    sb.AppendLine("  · " & a)
                Next
            End If
            Return sb.ToString().TrimEnd()
        End Function
    End Class

    ' ════════════════════════════════════════════════════════
    ' TEST DE CONNEXIÓ
    ' ════════════════════════════════════════════════════════
    Public Function TestConnexio(conn As ConnexioServidor) As String
        ' Retorna "" si OK, o el missatge d'error
        Try
            Using c As New SqlConnection(conn.BuildConnectionString())
                c.Open()
                Using cmd As New SqlCommand("SELECT @@VERSION", c)
                    Dim ver As String = CStr(cmd.ExecuteScalar())
                    Return ""   ' OK
                End Using
            End Using
        Catch ex As Exception
            Return ex.Message
        End Try
    End Function

    ' ════════════════════════════════════════════════════════
    ' LLISTAR BASES DE DADES DEL SERVIDOR
    ' ════════════════════════════════════════════════════════
    Public Function LlistarBD(conn As ConnexioServidor) As List(Of String)
        Dim llista As New List(Of String)()
        Try
            Dim masterConn As New ConnexioServidor()
            masterConn.Servidor    = conn.Servidor
            masterConn.Port        = conn.Port
            masterConn.AuthWindows = conn.AuthWindows
            masterConn.Usuari      = conn.Usuari
            masterConn.Contrasenya = conn.Contrasenya
            masterConn.BaseDades   = "master"
            masterConn.TimeoutSeg  = conn.TimeoutSeg
            masterConn.Encrypt     = conn.Encrypt

            Using c As New SqlConnection(masterConn.BuildConnectionString())
                c.Open()
                Dim sql As String =
                    "SELECT name FROM sys.databases " &
                    "WHERE database_id > 4 " &
                    "  AND state_desc = 'ONLINE' " &
                    "ORDER BY name;"
                Using cmd As New SqlCommand(sql, c)
                Using rdr As SqlDataReader = cmd.ExecuteReader()
                    Do While rdr.Read()
                        llista.Add(rdr.GetString(0))
                    Loop
                End Using
                End Using
            End Using
        Catch ex As Exception
            ' Retornem llista buida; l'error es veurà al test
        End Try
        Return llista
    End Function

    ' ════════════════════════════════════════════════════════
    ' IMPORTAR ESTRUCTURA DES DEL SERVIDOR
    ' Llegeix totes les taules, columnes, PKs i FKs
    ' ════════════════════════════════════════════════════════
    Public Function ImportarEstructura(conn As ConnexioServidor) As ProyectoBBDD
        Dim p As New ProyectoBBDD()
        p.Nombre   = conn.BaseDades
        p.MotorSQL = "T-SQL"

        Using c As New SqlConnection(conn.BuildConnectionString())
            c.Open()

            ' ── 1. Taules ────────────────────────────────────────────
            Using cmd As New SqlCommand(
                "SELECT TABLE_SCHEMA, TABLE_NAME " &
                "FROM INFORMATION_SCHEMA.TABLES " &
                "WHERE TABLE_TYPE='BASE TABLE' " &
                "ORDER BY TABLE_SCHEMA, TABLE_NAME;", c)
            Using rdr As SqlDataReader = cmd.ExecuteReader()
                Do While rdr.Read()
                    Dim t As New TablaBBDD()
                    t.Id     = p.GetNextTableId()
                    t.Schema = rdr.GetString(0)
                    t.Nombre = rdr.GetString(1).ToUpper()
                    p.Taules.Add(t)
                Loop
            End Using
            End Using

            ' ── 2. Columnes ──────────────────────────────────────────
            Using cmd As New SqlCommand(
                "SELECT c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, " &
                "       c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH, " &
                "       c.NUMERIC_PRECISION, c.NUMERIC_SCALE, " &
                "       c.IS_NULLABLE, c.COLUMN_DEFAULT, c.ORDINAL_POSITION, " &
                "       COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA+'.'+c.TABLE_NAME)," &
                "           c.COLUMN_NAME,'IsIdentity') AS IS_IDENTITY, " &
                "       CAST(ep.value AS NVARCHAR(MAX)) AS MS_DESC " &
                "FROM INFORMATION_SCHEMA.COLUMNS c " &
                "LEFT JOIN sys.extended_properties ep " &
                "  ON ep.major_id   = OBJECT_ID(c.TABLE_SCHEMA+'.'+c.TABLE_NAME) " &
                "  AND ep.minor_id  = c.ORDINAL_POSITION " &
                "  AND ep.name      = 'MS_Description' " &
                "  AND ep.class     = 1 " &
                "ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;", c)
            Using rdr As SqlDataReader = cmd.ExecuteReader()
                Do While rdr.Read()
                    Dim sch As String = rdr.GetString(0)
                    Dim tnm As String = rdr.GetString(1).ToUpper()
                    Dim t2  As TablaBBDD = Nothing
                    For Each tt As TablaBBDD In p.Taules
                        If tt.Nombre = tnm AndAlso tt.Schema = sch Then t2 = tt : Exit For
                    Next
                    If t2 Is Nothing Then Continue Do

                    Dim f As New CampoBBDD()
                    f.Nombre  = rdr.GetString(2).ToUpper()
                    f.NotNull = (rdr.GetString(7) = "NO")
                    If Not rdr.IsDBNull(8)  Then f.DefaultValue = rdr.GetString(8)
                    If Not rdr.IsDBNull(10) AndAlso rdr.GetInt32(10) = 1 Then
                        f.EsIdentity       = True
                        f.IdentitySeed      = 1
                        f.IdentityIncrement = 1
                    End If
                    If Not rdr.IsDBNull(11) Then f.Descripcion = rdr.GetString(11)
                    f.TipoDato = MapTipus(rdr.GetString(3))
                    If Not rdr.IsDBNull(4) Then
                        Dim ml As Integer = rdr.GetInt32(4)
                        If ml = -1 Then
                            f.LongitudMax = True
                        ElseIf ml > 0 Then
                            f.Longitud = ml
                        End If
                    End If
                    If Not rdr.IsDBNull(5) Then f.Precision = CByte(rdr.GetByte(5))
                    If Not rdr.IsDBNull(6) Then f.Escala    = rdr.GetInt32(6)
                    t2.Fields.Add(f)
                Loop
            End Using
            End Using

            ' ── 3. PKs ───────────────────────────────────────────────
            Using cmd As New SqlCommand(
                "SELECT KU.TABLE_SCHEMA, KU.TABLE_NAME, KU.COLUMN_NAME " &
                "FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC " &
                "JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU " &
                "  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME " &
                "WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY';", c)
            Using rdr As SqlDataReader = cmd.ExecuteReader()
                Do While rdr.Read()
                    Dim sch3 As String = rdr.GetString(0)
                    Dim tnm3 As String = rdr.GetString(1).ToUpper()
                    Dim cnm3 As String = rdr.GetString(2).ToUpper()
                    For Each tt As TablaBBDD In p.Taules
                        If tt.Nombre = tnm3 AndAlso tt.Schema = sch3 Then
                            For Each ff As CampoBBDD In tt.Fields
                                If ff.Nombre = cnm3 Then
                                    ff.EsPK    = True
                                    ff.NotNull = True
                                End If
                            Next
                        End If
                    Next
                Loop
            End Using
            End Using

            ' ── 4. Descripcions de taula ─────────────────────────────
            Using cmd As New SqlCommand(
                "SELECT OBJECT_NAME(ep.major_id), CAST(ep.value AS NVARCHAR(MAX)) " &
                "FROM sys.extended_properties ep " &
                "WHERE ep.class = 1 AND ep.minor_id = 0 AND ep.name = 'MS_Description';", c)
            Using rdr As SqlDataReader = cmd.ExecuteReader()
                Do While rdr.Read()
                    Dim tnm4 As String = rdr.GetString(0).ToUpper()
                    Dim desc4 As String = rdr.GetString(1)
                    For Each tt As TablaBBDD In p.Taules
                        If tt.Nombre = tnm4 Then tt.Descripcion = desc4 : Exit For
                    Next
                Loop
            End Using
            End Using

            ' ── 5. Foreign Keys ──────────────────────────────────────
            Using cmd As New SqlCommand(
                "SELECT FK.name, " &
                "  SCHEMA_NAME(FKT.schema_id), FKT.name, FKC.name, " &
                "  SCHEMA_NAME(PKT.schema_id), PKT.name, PKC.name, " &
                "  FK.delete_referential_action, FK.update_referential_action " &
                "FROM sys.foreign_keys FK " &
                "JOIN sys.tables FKT ON FK.parent_object_id    = FKT.object_id " &
                "JOIN sys.tables PKT ON FK.referenced_object_id = PKT.object_id " &
                "JOIN sys.foreign_key_columns FC " &
                "     ON FK.object_id = FC.constraint_object_id " &
                "JOIN sys.columns FKC " &
                "     ON FC.parent_object_id = FKC.object_id " &
                "     AND FC.parent_column_id = FKC.column_id " &
                "JOIN sys.columns PKC " &
                "     ON FC.referenced_object_id = PKC.object_id " &
                "     AND FC.referenced_column_id = PKC.column_id;", c)
            Using rdr As SqlDataReader = cmd.ExecuteReader()
                Do While rdr.Read()
                    Dim fkNom As String = rdr.GetString(0)
                    Dim fkSch As String = rdr.GetString(1)
                    Dim fkTbl As String = rdr.GetString(2).ToUpper()
                    Dim fkCol As String = rdr.GetString(3).ToUpper()
                    Dim pkSch As String = rdr.GetString(4)
                    Dim pkTbl As String = rdr.GetString(5).ToUpper()
                    Dim pkCol As String = rdr.GetString(6).ToUpper()
                    Dim onDel As Integer = CByte(rdr.GetByte(7))
                    Dim onUpd As Integer = CByte(rdr.GetByte(8))

                    Dim ftId As Integer = -1
                    Dim ttId As Integer = -1
                    For Each tt As TablaBBDD In p.Taules
                        If tt.Nombre = fkTbl AndAlso tt.Schema = fkSch Then ftId = tt.Id
                        If tt.Nombre = pkTbl AndAlso tt.Schema = pkSch Then ttId = tt.Id
                    Next
                    If ftId < 0 OrElse ttId < 0 Then Continue Do

                    For Each tt As TablaBBDD In p.Taules
                        If tt.Id = ftId Then
                            For Each ff As CampoBBDD In tt.Fields
                                If ff.Nombre = fkCol Then ff.EsFK = True
                            Next
                        End If
                    Next

                    Dim rel As New RelacionBBDD()
                    rel.Id             = p.GetNextRelId()
                    rel.Nombre         = fkNom
                    rel.TablaOrigenId  = ftId
                    rel.CampoFKNombre  = fkCol
                    rel.TablaDestinoId = ttId
                    rel.CampoPKNombre  = pkCol
                    rel.TipoRelacion   = CardinalityType.ManyToOne
                    rel.OnDelete       = MapAccio(onDel)
                    rel.OnUpdate       = MapAccio(onUpd)
                    rel.CrearIndexFK   = True
                    p.Relacions.Add(rel)
                Loop
            End Using
            End Using
        End Using

        Return p
    End Function

    ' ════════════════════════════════════════════════════════
    ' EXPORTAR PROJECTE COMPLET AL SERVIDOR
    ' mode: 0=CreateNew, 1=DropAndCreate, 2=AlterExisting
    ' ════════════════════════════════════════════════════════
    Public Function ExportarProjecte(conn As ConnexioServidor,
                                     p As ProyectoBBDD,
                                     mode As Integer) As ResultatOperacio
        Dim res As New ResultatOperacio()
        Try
            ' Connexió al master per gestionar la BD
            Dim masterCs As String = conn.BuildMasterConnectionString()
            Using masterConn As New SqlConnection(masterCs)
                masterConn.Open()

                Dim bdJaExisteix As Boolean = BdExisteix(masterConn, conn.BaseDades)

                Select Case mode
                    Case 0  ' CreateNew
                        If bdJaExisteix Then
                            res.MissatgeError = "La base de dades '" & conn.BaseDades &
                                                "' ja existeix. Usa el mode 'Aplicar diferències'."
                            Return res
                        End If
                        CrearBd(masterConn, conn.BaseDades)

                    Case 1  ' DropAndCreate
                        If bdJaExisteix Then EliminarBd(masterConn, conn.BaseDades)
                        CrearBd(masterConn, conn.BaseDades)

                    Case 2  ' AlterExisting
                        If Not bdJaExisteix Then CrearBd(masterConn, conn.BaseDades)
                End Select
            End Using

            ' Connexió a la BD destí
            Using destConn As New SqlConnection(conn.BuildConnectionString())
                destConn.Open()
                If mode = 2 Then
                    AplicarDiferencies(destConn, p, res)
                Else
                    AplicarDDLComplet(destConn, p, res)
                End If
            End Using

            res.OK = True
        Catch ex As Exception
            res.MissatgeError = ex.Message
        End Try
        Return res
    End Function

    ' ════════════════════════════════════════════════════════
    ' ENVIAR PARTS — taules seleccionades + FK entre elles
    ' ════════════════════════════════════════════════════════
    Public Function EnviarParts(conn As ConnexioServidor,
                                taules As List(Of TablaBBDD),
                                relacions As List(Of RelacionBBDD)) As ResultatOperacio
        Dim res As New ResultatOperacio()
        Try
            Using destConn As New SqlConnection(conn.BuildConnectionString())
                destConn.Open()

                ' Crear taules seleccionades
                For Each t As TablaBBDD In taules
                    Dim sql As String = BuildCreateTable(t)
                    ExecutarDDL(destConn, sql, res)
                    res.TaulesCreades += 1
                Next

                ' Crear FK entre les taules seleccionades
                Dim idsSeleccionats As New HashSet(Of Integer)(taules.Select(Function(t) t.Id))
                For Each r As RelacionBBDD In relacions
                    If idsSeleccionats.Contains(r.TablaOrigenId) AndAlso
                       idsSeleccionats.Contains(r.TablaDestinoId) Then
                        Dim ft As TablaBBDD = taules.FirstOrDefault(Function(t) t.Id = r.TablaOrigenId)
                        Dim tt As TablaBBDD = taules.FirstOrDefault(Function(t) t.Id = r.TablaDestinoId)
                        If ft IsNot Nothing AndAlso tt IsNot Nothing Then
                            ExecutarDDL(destConn, BuildAlterFK(r, ft, tt), res)
                            res.RelacionsCreades += 1
                        End If
                    End If
                Next
            End Using
            res.OK = True
        Catch ex As Exception
            res.MissatgeError = ex.Message
        End Try
        Return res
    End Function

    ' ════════════════════════════════════════════════════════
    ' HELPERS INTERNS
    ' ════════════════════════════════════════════════════════
    Private Function BdExisteix(conn As SqlConnection, nom As String) As Boolean
        Using cmd As New SqlCommand(
            "SELECT COUNT(*) FROM sys.databases WHERE name = @n;", conn)
            cmd.Parameters.AddWithValue("@n", nom)
            Return CInt(cmd.ExecuteScalar()) > 0
        End Using
    End Function

    Private Sub CrearBd(conn As SqlConnection, nom As String)
        ' Nom entre brackets per seguretat; no permet injection per control previ
        Dim sql As String = "CREATE DATABASE [" & nom.Replace("]", "]]") & "];"
        Using cmd As New SqlCommand(sql, conn)
            cmd.CommandTimeout = 60
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Sub EliminarBd(conn As SqlConnection, nom As String)
        ' Desconnectar sessions actives i eliminar
        Dim sql As String =
            "ALTER DATABASE [" & nom.Replace("]", "]]") & "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " &
            "DROP DATABASE [" & nom.Replace("]", "]]") & "];"
        Using cmd As New SqlCommand(sql, conn)
            cmd.CommandTimeout = 60
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Sub AplicarDDLComplet(conn As SqlConnection, p As ProyectoBBDD, res As ResultatOperacio)
        For Each t As TablaBBDD In p.Taules
            ExecutarDDL(conn, BuildCreateTable(t), res)
            res.TaulesCreades += 1
        Next
        For Each r As RelacionBBDD In p.Relacions
            Dim ft As TablaBBDD = p.Taules.FirstOrDefault(Function(t) t.Id = r.TablaOrigenId)
            Dim tt As TablaBBDD = p.Taules.FirstOrDefault(Function(t) t.Id = r.TablaDestinoId)
            If ft IsNot Nothing AndAlso tt IsNot Nothing Then
                ExecutarDDL(conn, BuildAlterFK(r, ft, tt), res)
                res.RelacionsCreades += 1
            End If
        Next
        For Each r As RelacionBBDD In p.Relacions
            If r.CrearIndexFK Then
                Dim ft As TablaBBDD = p.Taules.FirstOrDefault(Function(t) t.Id = r.TablaOrigenId)
                If ft IsNot Nothing Then
                    ExecutarDDL(conn,
                        "CREATE NONCLUSTERED INDEX [IX_" & ft.Nombre & "_" & r.CampoFKNombre & "] " &
                        "ON [" & ft.Schema & "].[" & ft.Nombre & "] ([" & r.CampoFKNombre & "] ASC);", res)
                End If
            End If
        Next
        For Each t As TablaBBDD In p.Taules
            If Not String.IsNullOrEmpty(t.Descripcion) Then
                ExecutarDDL(conn, BuildExtPropTaula(t), res)
            End If
            For Each f As CampoBBDD In t.Fields
                If Not String.IsNullOrEmpty(f.Descripcion) Then
                    ExecutarDDL(conn, BuildExtPropCamp(t, f), res)
                End If
            Next
        Next
    End Sub

    Private Sub AplicarDiferencies(conn As SqlConnection, p As ProyectoBBDD, res As ResultatOperacio)
        ' Taules existents al servidor
        Dim taulesExist As New HashSet(Of String)()
        Using cmd As New SqlCommand(
            "SELECT TABLE_SCHEMA+'.'+TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " &
            "WHERE TABLE_TYPE='BASE TABLE';", conn)
        Using rdr As SqlDataReader = cmd.ExecuteReader()
            Do While rdr.Read() : taulesExist.Add(rdr.GetString(0).ToUpper()) : Loop
        End Using
        End Using

        ' Columnes existents al servidor
        Dim colsExist As New HashSet(Of String)()
        Using cmd As New SqlCommand(
            "SELECT TABLE_SCHEMA+'.'+TABLE_NAME+'.'+COLUMN_NAME " &
            "FROM INFORMATION_SCHEMA.COLUMNS;", conn)
        Using rdr As SqlDataReader = cmd.ExecuteReader()
            Do While rdr.Read() : colsExist.Add(rdr.GetString(0).ToUpper()) : Loop
        End Using
        End Using

        ' FK existents al servidor
        Dim fkExist As New HashSet(Of String)()
        Using cmd As New SqlCommand("SELECT name FROM sys.foreign_keys;", conn)
        Using rdr As SqlDataReader = cmd.ExecuteReader()
            Do While rdr.Read() : fkExist.Add(rdr.GetString(0).ToUpper()) : Loop
        End Using
        End Using

        ' Afegir taules noves
        For Each t As TablaBBDD In p.Taules
            Dim key As String = (t.Schema & "." & t.Nombre).ToUpper()
            If Not taulesExist.Contains(key) Then
                ExecutarDDL(conn, BuildCreateTable(t), res)
                res.TaulesCreades += 1
            Else
                ' Afegir columnes noves
                For Each f As CampoBBDD In t.Fields
                    Dim ckey As String = (t.Schema & "." & t.Nombre & "." & f.Nombre).ToUpper()
                    If Not colsExist.Contains(ckey) Then
                        Dim addSql As String =
                            "ALTER TABLE [" & t.Schema & "].[" & t.Nombre & "] " &
                            "ADD " & BuildFieldDDL(f) & ";"
                        ExecutarDDL(conn, addSql, res)
                        res.TaulesAlterades += 1
                    End If
                Next
            End If
        Next

        ' Afegir FK noves
        For Each r As RelacionBBDD In p.Relacions
            If Not fkExist.Contains(r.Nombre.ToUpper()) Then
                Dim ft As TablaBBDD = p.Taules.FirstOrDefault(Function(t) t.Id = r.TablaOrigenId)
                Dim tt As TablaBBDD = p.Taules.FirstOrDefault(Function(t) t.Id = r.TablaDestinoId)
                If ft IsNot Nothing AndAlso tt IsNot Nothing Then
                    ExecutarDDL(conn, BuildAlterFK(r, ft, tt), res)
                    res.RelacionsCreades += 1
                End If
            End If
        Next
    End Sub

    Private Sub ExecutarDDL(conn As SqlConnection, sql As String, res As ResultatOperacio)
        If String.IsNullOrWhiteSpace(sql) Then Return
        Try
            Using cmd As New SqlCommand(sql, conn)
                cmd.CommandTimeout = 60
                cmd.ExecuteNonQuery()
            End Using
        Catch ex As SqlException
            If ex.Number = 2714 OrElse ex.Number = 1913 Then
                res.Advertencies.Add("Ja existia (ignorat): " & TruncSql(sql))
            Else
                Throw New Exception("Error SQL " & ex.Number & ": " & ex.Message &
                                    Environment.NewLine & "SQL: " & TruncSql(sql), ex)
            End If
        End Try
    End Sub

    Private Function BuildCreateTable(t As TablaBBDD) As String
        Dim exp As New TSqlExporter()
        ' Reutilitzem TSqlExporter internament via reflexió indirecta
        ' però és més net cridar-lo directament
        Return New TSqlExporter().GenerarTaula(t)
    End Function

    Private Function BuildAlterFK(r As RelacionBBDD,
                                   ft As TablaBBDD,
                                   tt As TablaBBDD) As String
        Return New TSqlExporter().GenerarAlterFK(r, ft, tt)
    End Function

    Private Function BuildFieldDDL(f As CampoBBDD) As String
        Return New TSqlExporter().GenerarCamp(f)
    End Function

    Private Function BuildExtPropTaula(t As TablaBBDD) As String
        Return New TSqlExporter().GenerarExtPropTaula(t)
    End Function

    Private Function BuildExtPropCamp(t As TablaBBDD, f As CampoBBDD) As String
        Return New TSqlExporter().GenerarExtPropCamp(t, f)
    End Function

    Private Function TruncSql(sql As String) As String
        Dim s As String = sql.Trim().Replace(Environment.NewLine, " ")
        If s.Length > 120 Then Return s.Substring(0, 120) & "…"
        Return s
    End Function

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
            Case "timestamp", "rowversion" : Return DataType.RowVersion
            Case "uniqueidentifier" : Return DataType.UniqueIdentifier
            Case "xml"              : Return DataType.DbXml
            Case "geography"        : Return DataType.DbGeography
            Case "geometry"         : Return DataType.DbGeometry
            Case "hierarchyid"      : Return DataType.HierarchyId
            Case "sql_variant"      : Return DataType.SqlVariant
            Case Else               : Return DataType.VarChar
        End Select
    End Function

    Private Function MapAccio(v As Integer) As OnDeleteUpdateAction
        Select Case v
            Case 1 : Return OnDeleteUpdateAction.DoCascade
            Case 2 : Return OnDeleteUpdateAction.SetNull
            Case 3 : Return OnDeleteUpdateAction.SetDefault
            Case Else : Return OnDeleteUpdateAction.NoAction
        End Select
    End Function

End Module
