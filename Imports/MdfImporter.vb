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
' MdfImporter.vb  —  DB-Core Holographic
' Importa l'estructura d'un fitxer .mdf via SQL Server LocalDB.
' La lectura de l'estructura és la mateixa que la d'un servidor en
' xarxa (SqlServerConnector.LlegirEstructura).
' Requereix: Microsoft.Data.SqlClient (NuGet)
' ============================================================
Public Module MdfImporter

    Public Function Importar(mdfPath As String) As ProyectoBBDD
        Dim dbName As String = IO.Path.GetFileNameWithoutExtension(mdfPath)
        Dim b As New SqlConnectionStringBuilder()
        b.DataSource = "(LocalDB)\MSSQLLocalDB"
        b.AttachDBFilename = IO.Path.GetFullPath(mdfPath)
        b.InitialCatalog = dbName
        b.IntegratedSecurity = True
        b.ConnectTimeout = 30

        Using conn As New SqlConnection(b.ConnectionString)
            conn.Open()
            Return SqlServerConnector.LlegirEstructura(conn, dbName)
        End Using
    End Function

End Module
