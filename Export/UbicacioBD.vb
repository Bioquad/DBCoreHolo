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
Imports Microsoft.Data.SqlClient

' ============================================================
' UbicacioBD.vb — Una base de dades SQL Server existent, sigui un
' fitxer .mdf del disc (via LocalDB) o una BD d'un servidor.
' Ho fan servir la consulta de dades i la còpia de bases de dades.
' ============================================================
Public Class UbicacioBD

    Public Const LOCALDB As String = "(LocalDB)\MSSQLLocalDB"

    ''' <summary>True → fitxer .mdf (LocalDB); False → BD d'un servidor.</summary>
    Public Property EsFitxer As Boolean
    ''' <summary>Ruta completa del .mdf (quan EsFitxer).</summary>
    Public Property RutaMdf As String = ""
    ''' <summary>Connexió al servidor; BaseDades és el nom de la BD (quan no EsFitxer).</summary>
    Public Property Connexio As SqlServerConnector.ConnexioServidor

    Public Shared Function Fitxer(rutaMdf As String) As UbicacioBD
        Return New UbicacioBD() With {.EsFitxer = True, .RutaMdf = IO.Path.GetFullPath(rutaMdf)}
    End Function

    Public Shared Function Servidor(conn As SqlServerConnector.ConnexioServidor) As UbicacioBD
        Return New UbicacioBD() With {.EsFitxer = False, .Connexio = conn.Copia()}
    End Function

    ''' <summary>Nom de la base de dades (per a un fitxer, el nom del fitxer sense extensió).</summary>
    Public ReadOnly Property NomBD As String
        Get
            If EsFitxer Then Return IO.Path.GetFileNameWithoutExtension(RutaMdf)
            Return If(Connexio Is Nothing, "", Connexio.BaseDades)
        End Get
    End Property

    ''' <summary>Cadena de connexió a la BD master de la instància.</summary>
    Public Function CadenaMaster() As String
        If EsFitxer Then
            Dim b As New SqlConnectionStringBuilder()
            b.DataSource = LOCALDB
            b.InitialCatalog = "master"
            b.IntegratedSecurity = True
            b.ConnectTimeout = 30
            Return b.ConnectionString
        End If
        Return Connexio.BuildMasterConnectionString()
    End Function

    ''' <summary>
    ''' Obre una connexió a la BD. Si és un .mdf, primer mira si LocalDB ja el
    ''' té adjuntat (amb qualsevol nom) i, si no, l'adjunta.
    ''' </summary>
    Public Function ObrirConnexio() As SqlConnection
        Dim cs As String
        If EsFitxer Then
            If Not IO.File.Exists(RutaMdf) Then
                Throw New IO.FileNotFoundException("No s'ha trobat el fitxer: " & RutaMdf)
            End If
            Dim b As New SqlConnectionStringBuilder(CadenaMaster())
            Dim nomAdjuntat As String = NomAdjuntatLocalDb()
            If nomAdjuntat IsNot Nothing Then
                b.InitialCatalog = nomAdjuntat
            Else
                b.AttachDBFilename = RutaMdf
                b.InitialCatalog = NomBD
            End If
            cs = b.ConnectionString
        Else
            cs = Connexio.BuildConnectionString()
        End If
        Dim c As New SqlConnection(cs)
        c.Open()
        Return c
    End Function

    ' Nom amb què LocalDB té adjuntat aquest .mdf, o Nothing
    Private Function NomAdjuntatLocalDb() As String
        Using c As New SqlConnection(CadenaMaster())
            c.Open()
            Using cmd As New SqlCommand(
                "SELECT TOP (1) DB_NAME(database_id) FROM sys.master_files " &
                "WHERE type = 0 AND physical_name = @p;", c)
                cmd.Parameters.AddWithValue("@p", RutaMdf)
                Dim v As Object = cmd.ExecuteScalar()
                Return If(v Is Nothing OrElse IsDBNull(v), Nothing, CStr(v))
            End Using
        End Using
    End Function

    Public Overrides Function ToString() As String
        If EsFitxer Then Return RutaMdf
        Return If(Connexio Is Nothing, "", Connexio.Servidor & " / " & Connexio.BaseDades)
    End Function

End Class
