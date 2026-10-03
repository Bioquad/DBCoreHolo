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
Imports Microsoft.Data.SqlClient

' ============================================================
' MdfExporter.vb  —  DB-Core Holographic
' Crea o actualitza una base de dades LocalDB a partir del model
' (ProyectoBBDD) i en genera el fitxer .mdf.
'
' El DDL es genera amb TSqlExporter i s'aplica amb el mateix codi
' que el desplegament a servidor (SqlServerConnector), dins d'una
' transacció.
'
' MODES D'OPERACIÓ (ExportMode)
'   CreateNew     → crea una BD nova; falla si ja existeix
'   DropAndCreate → elimina la BD si existeix i la recrea (DESTRUCTIU)
'   AlterExisting → aplica diferències (taules/columnes/FK noves) sobre
'                   la BD existent; no elimina res del que ja hi ha
' ============================================================
Public Module MdfExporter

    Private Const LOCALDB As String = "(LocalDB)\MSSQLLocalDB"

    Public Enum ExportMode
        CreateNew = 0
        DropAndCreate = 1
        AlterExisting = 2
    End Enum

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
    ' mdfDir : directori on es crearà el fitxer .mdf
    ' dbName : nom de la base de dades (també el nom del fitxer)
    ' ════════════════════════════════════════════════════════════════════════
    Public Function Exportar(mdfDir As String,
                             dbName As String,
                             p As ProyectoBBDD,
                             mode As ExportMode) As ExportResult

        Dim res As New ExportResult()
        res.DbName = dbName
        Dim bdCreada As Boolean = False

        Try
            ValidarNom(dbName)
            res.MdfPath = IO.Path.GetFullPath(IO.Path.Combine(mdfDir, dbName & ".mdf"))
            Dim ldfPath As String = IO.Path.Combine(IO.Path.GetDirectoryName(res.MdfPath), dbName & "_log.ldf")

            Using conn As New SqlConnection(CadenaConnexio("master"))
                conn.Open()
                Dim exists As Boolean = BdExisteix(conn, dbName)

                Select Case mode
                    Case ExportMode.CreateNew
                        If exists Then
                            res.MissatgeError = $"La base de dades '{dbName}' ja existeix. " &
                                                "Usa el mode DropAndCreate per sobreescriure-la."
                            Return res
                        End If
                        If IO.File.Exists(res.MdfPath) Then
                            res.MissatgeError = $"El fitxer '{res.MdfPath}' ja existeix. " &
                                                "Usa el mode DropAndCreate per sobreescriure'l."
                            Return res
                        End If
                        CrearBd(conn, dbName, res.MdfPath, ldfPath)
                        bdCreada = True

                    Case ExportMode.DropAndCreate
                        If exists Then EliminarBd(conn, dbName)
                        ' Fitxers orfes d'una exportació anterior (BD ja no registrada)
                        If IO.File.Exists(res.MdfPath) Then IO.File.Delete(res.MdfPath)
                        If IO.File.Exists(ldfPath) Then IO.File.Delete(ldfPath)
                        CrearBd(conn, dbName, res.MdfPath, ldfPath)
                        bdCreada = True

                    Case ExportMode.AlterExisting
                        If Not exists Then
                            If IO.File.Exists(res.MdfPath) Then
                                ' El .mdf existeix però LocalDB no el té registrat: l'adjuntem
                                AdjuntarBd(conn, dbName, res.MdfPath)
                            Else
                                CrearBd(conn, dbName, res.MdfPath, ldfPath)
                                bdCreada = True
                            End If
                        End If
                End Select
            End Using

            Dim r As New SqlServerConnector.ResultatOperacio()
            Using conn As New SqlConnection(CadenaConnexio(dbName))
                conn.Open()
                If mode = ExportMode.AlterExisting AndAlso Not bdCreada Then
                    SqlServerConnector.AplicarDiferencies(conn, p, r)
                Else
                    SqlServerConnector.AplicarDDLComplet(conn, p, r)
                End If
            End Using

            res.TaulesCreades = r.TaulesCreades
            res.TaulesAlterades = r.TaulesAlterades
            res.RelacionsCreades = r.RelacionsCreades
            res.Advertències.AddRange(r.Advertencies)
            res.OK = True

        Catch ex As Exception
            res.MissatgeError = ex.Message
            ' No deixem una BD a mig crear
            If bdCreada Then
                Try
                    SqlConnection.ClearAllPools()
                    Using conn As New SqlConnection(CadenaConnexio("master"))
                        conn.Open()
                        EliminarBd(conn, dbName)
                    End Using
                Catch
                End Try
            End If
        End Try

        Return res
    End Function

    ' ════════════════════════════════════════════════════════════════════════
    ' HELPERS
    ' ════════════════════════════════════════════════════════════════════════

    Private Function CadenaConnexio(bd As String) As String
        Dim b As New SqlConnectionStringBuilder()
        b.DataSource = LOCALDB
        b.InitialCatalog = bd
        b.IntegratedSecurity = True
        b.ConnectTimeout = 30
        Return b.ConnectionString
    End Function

    Private Sub ValidarNom(dbName As String)
        If String.IsNullOrWhiteSpace(dbName) Then
            Throw New ArgumentException("Cal indicar el nom de la base de dades.")
        End If
        If dbName.IndexOfAny(IO.Path.GetInvalidFileNameChars()) >= 0 OrElse
           dbName.Contains("'") OrElse dbName.Contains("]") Then
            Throw New ArgumentException("El nom de la BD conté caràcters no permesos.")
        End If
    End Sub

    Private Function Lit(s As String) As String
        Return s.Replace("'", "''")
    End Function

    Private Function BdExisteix(conn As SqlConnection, dbName As String) As Boolean
        Using cmd As New SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name = @n", conn)
            cmd.Parameters.AddWithValue("@n", dbName)
            Return CInt(cmd.ExecuteScalar()) > 0
        End Using
    End Function

    Private Sub CrearBd(conn As SqlConnection, dbName As String, mdfPath As String, ldfPath As String)
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(mdfPath))
        Dim sql As String =
            "CREATE DATABASE " & TSqlExporter.Q(dbName) & " ON PRIMARY " &
            "(NAME = N'" & Lit(dbName) & "', FILENAME = N'" & Lit(mdfPath) & "', " &
            " SIZE = 8192KB, FILEGROWTH = 65536KB) " &
            "LOG ON " &
            "(NAME = N'" & Lit(dbName & "_log") & "', FILENAME = N'" & Lit(ldfPath) & "', " &
            " SIZE = 8192KB, FILEGROWTH = 65536KB);"
        Using cmd As New SqlCommand(sql, conn)
            cmd.CommandTimeout = 120
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Sub AdjuntarBd(conn As SqlConnection, dbName As String, mdfPath As String)
        Dim sql As String =
            "CREATE DATABASE " & TSqlExporter.Q(dbName) & " ON " &
            "(FILENAME = N'" & Lit(mdfPath) & "') FOR ATTACH;"
        Using cmd As New SqlCommand(sql, conn)
            cmd.CommandTimeout = 120
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Sub EliminarBd(conn As SqlConnection, dbName As String)
        Dim q As String = TSqlExporter.Q(dbName)
        ' Expulsar connexions actives abans d'eliminar
        Using cmd As New SqlCommand("ALTER DATABASE " & q & " SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", conn)
            cmd.CommandTimeout = 60
            Try
                cmd.ExecuteNonQuery()
            Catch
                ' Ignorem si no estava en ús
            End Try
        End Using
        Using cmd As New SqlCommand("DROP DATABASE " & q & ";", conn)
            cmd.CommandTimeout = 60
            cmd.ExecuteNonQuery()
        End Using
    End Sub

End Module
