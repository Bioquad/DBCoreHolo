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
Imports Microsoft.Data.SqlClient

' ============================================================
' MdfImporter.vb  —  DB-Core Holographic
' Importa l'estructura d'un fitxer .mdf via SQL Server LocalDB
' Requereix: Microsoft.Data.SqlClient (NuGet)
' ============================================================
Public Module MdfImporter

    Public Function Importar(mdfPath As String) As ProyectoBBDD
        Dim dbName As String = IO.Path.GetFileNameWithoutExtension(mdfPath)
        Dim connStr As String =
            "Data Source=(LocalDB)\MSSQLLocalDB;" &
            "AttachDbFilename=" & mdfPath & ";" &
            "Initial Catalog=" & dbName & ";" &
            "Integrated Security=True;Connect Timeout=15;"

        Dim p As New ProyectoBBDD()
        p.Nombre   = dbName
        p.MotorSQL = "T-SQL"

        Using conn As New SqlConnection(connStr)
            conn.Open()

            ' ── 1. Llegir taules ─────────────────────────────────────────
            Dim sqlTaules As String =
                "SELECT TABLE_SCHEMA, TABLE_NAME " &
                "FROM INFORMATION_SCHEMA.TABLES " &
                "WHERE TABLE_TYPE = 'BASE TABLE' " &
                "ORDER BY TABLE_SCHEMA, TABLE_NAME;"

            Using cmd As New SqlCommand(sqlTaules, conn)
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

            ' ── 2. Llegir columnes ────────────────────────────────────────
            Dim sqlCols As String =
                "SELECT c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, " &
                "       c.DATA_TYPE, c.CHARACTER_MAXIMUM_LENGTH, " &
                "       c.NUMERIC_PRECISION, c.NUMERIC_SCALE, " &
                "       c.IS_NULLABLE, c.COLUMN_DEFAULT, c.ORDINAL_POSITION, " &
                "       COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA+'.' +c.TABLE_NAME), " &
                "           c.COLUMN_NAME, 'IsIdentity') AS IS_IDENTITY " &
                "FROM INFORMATION_SCHEMA.COLUMNS c " &
                "ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;"

            Using cmd As New SqlCommand(sqlCols, conn)
            Using rdr As SqlDataReader = cmd.ExecuteReader()
                Do While rdr.Read()
                    Dim schema2 As String = rdr.GetString(0)
                    Dim tnom2   As String = rdr.GetString(1).ToUpper()
                    Dim t2 As TablaBBDD = Nothing
                    For Each tt2 As TablaBBDD In p.Taules
                        If tt2.Nombre = tnom2 AndAlso tt2.Schema = schema2 Then t2 = tt2
                    Next
                    If t2 Is Nothing Then Continue Do

                    Dim f As New CampoBBDD()
                    f.Nombre  = rdr.GetString(2).ToUpper()
                    f.NotNull = (rdr.GetString(7) = "NO")
                    If Not rdr.IsDBNull(8) Then f.DefaultValue = rdr.GetString(8)
                    If Not rdr.IsDBNull(10) AndAlso rdr.GetInt32(10) = 1 Then
                        f.EsIdentity       = True
                        f.IdentitySeed      = 1
                        f.IdentityIncrement = 1
                    End If
                    f.TipoDato = MapTipus(rdr.GetString(3))
                    If Not rdr.IsDBNull(4) Then
                        Dim maxLen As Integer = rdr.GetInt32(4)
                        If maxLen = -1 Then
                            f.LongitudMax = True
                        ElseIf maxLen > 0 Then
                            f.Longitud = maxLen
                        End If
                    End If
                    If Not rdr.IsDBNull(5) Then f.Precision = CByte(rdr.GetByte(5))
                    If Not rdr.IsDBNull(6) Then f.Escala = CInt(rdr.GetInt32(6))
                    t2.Fields.Add(f)
                Loop
            End Using
            End Using

            ' ── 3. Identificar PKs ────────────────────────────────────────
            Dim sqlPK As String =
                "SELECT KU.TABLE_SCHEMA, KU.TABLE_NAME, KU.COLUMN_NAME " &
                "FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC " &
                "JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU " &
                "  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME " &
                "WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY';"

            Using cmd As New SqlCommand(sqlPK, conn)
            Using rdr As SqlDataReader = cmd.ExecuteReader()
                Do While rdr.Read()
                    Dim sch3 As String = rdr.GetString(0)
                    Dim tnm3 As String = rdr.GetString(1).ToUpper()
                    Dim cnm3 As String = rdr.GetString(2).ToUpper()
                    For Each tt3 As TablaBBDD In p.Taules
                        If tt3.Nombre = tnm3 AndAlso tt3.Schema = sch3 Then
                            For Each ff3 As CampoBBDD In tt3.Fields
                                If ff3.Nombre = cnm3 Then
                                    ff3.EsPK   = True
                                    ff3.NotNull = True
                                End If
                            Next
                        End If
                    Next
                Loop
            End Using
            End Using

            ' ── 4. Llegir FKs ─────────────────────────────────────────────
            Dim sqlFK As String =
                "SELECT FK.name, " &
                "  SCHEMA_NAME(FKT.schema_id), FKT.name, FKC.name, " &
                "  SCHEMA_NAME(PKT.schema_id), PKT.name, PKC.name, " &
                "  FK.delete_referential_action, FK.update_referential_action " &
                "FROM sys.foreign_keys FK " &
                "JOIN sys.tables FKT ON FK.parent_object_id = FKT.object_id " &
                "JOIN sys.tables PKT ON FK.referenced_object_id = PKT.object_id " &
                "JOIN sys.foreign_key_columns FC " &
                "     ON FK.object_id = FC.constraint_object_id " &
                "JOIN sys.columns FKC " &
                "     ON FC.parent_object_id = FKC.object_id " &
                "     AND FC.parent_column_id = FKC.column_id " &
                "JOIN sys.columns PKC " &
                "     ON FC.referenced_object_id = PKC.object_id " &
                "     AND FC.referenced_column_id = PKC.column_id;"

            Using cmd As New SqlCommand(sqlFK, conn)
            Using rdr As SqlDataReader = cmd.ExecuteReader()
                Do While rdr.Read()
                    Dim fkNom    As String = rdr.GetString(0)
                    Dim fkSch    As String = rdr.GetString(1)
                    Dim fkTbl    As String = rdr.GetString(2).ToUpper()
                    Dim fkCol    As String = rdr.GetString(3).ToUpper()
                    Dim pkSch    As String = rdr.GetString(4)
                    Dim pkTbl    As String = rdr.GetString(5).ToUpper()
                    Dim pkCol    As String = rdr.GetString(6).ToUpper()
                    Dim onDel    As Integer = CByte(rdr.GetByte(7))
                    Dim onUpd    As Integer = CByte(rdr.GetByte(8))

                    Dim ftId As Integer = -1
                    Dim ttId As Integer = -1
                    For Each tt4 As TablaBBDD In p.Taules
                        If tt4.Nombre = fkTbl AndAlso tt4.Schema = fkSch Then ftId = tt4.Id
                        If tt4.Nombre = pkTbl AndAlso tt4.Schema = pkSch Then ttId = tt4.Id
                    Next
                    If ftId < 0 OrElse ttId < 0 Then Continue Do

                    ' Marcar camp com FK
                    For Each tt4 As TablaBBDD In p.Taules
                        If tt4.Id = ftId Then
                            For Each ff4 As CampoBBDD In tt4.Fields
                                If ff4.Nombre = fkCol Then ff4.EsFK = True
                            Next
                        End If
                    Next

                    Dim rel As New RelacionBBDD()
                    rel.Id              = p.GetNextRelId()
                    rel.Nombre          = fkNom
                    rel.TablaOrigenId   = ftId
                    rel.CampoFKNombre   = fkCol
                    rel.TablaDestinoId  = ttId
                    rel.CampoPKNombre   = pkCol
                    rel.TipoRelacion    = CardinalityType.ManyToOne
                    rel.OnDelete        = MapAccio(onDel)
                    rel.OnUpdate        = MapAccio(onUpd)
                    rel.CrearIndexFK    = True
                    p.Relacions.Add(rel)
                Loop
            End Using
            End Using
        End Using

        Return p
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
            Case "timestamp"        : Return DataType.DbTimestamp
            Case "uniqueidentifier" : Return DataType.UniqueIdentifier
            Case "xml"              : Return DataType.DbXml
            Case "sql_variant"      : Return DataType.SqlVariant
            Case "rowversion"       : Return DataType.RowVersion
            Case "hierarchyid"      : Return DataType.HierarchyId
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
