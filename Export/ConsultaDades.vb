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
Imports System.Data
Imports System.Globalization
Imports System.Text
Imports Microsoft.Data.SqlClient

' ============================================================
' ConsultaDades.vb — Lectura de dades d'una BD existent (fitxer
' .mdf o servidor) per al visor de dades.
'
' Tot és només de lectura: les consultes SQL lliures s'executen
' dins d'una transacció que sempre es desfà (ROLLBACK), de manera
' que cap INSERT/UPDATE/DELETE/DDL no pot modificar la BD.
' ============================================================
Public Module ConsultaDades

    Public Class ObjecteDades
        Public Property Esquema As String
        Public Property Nom As String
        Public Property EsVista As Boolean
        Public Property FilesAprox As Long

        Public ReadOnly Property NomComplet As String
            Get
                Return TSqlExporter.Q(Esquema) & "." & TSqlExporter.Q(Nom)
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return Esquema & "." & Nom & If(EsVista, "  (vista)", "  (" & FilesAprox.ToString("N0", CultureInfo.CurrentCulture) & ")")
        End Function
    End Class

    Public Class ResultatConsulta
        Public Property Taules As New List(Of DataTable)()
        ''' <summary>True si algun resultat s'ha tallat al límit de files.</summary>
        Public Property Truncat As Boolean
        Public Property FilesAfectades As Integer = -1
    End Class

    ''' <summary>Taules i vistes d'usuari, amb el nombre aproximat de files de cada taula.</summary>
    Public Function LlistarObjectes(c As SqlConnection) As List(Of ObjecteDades)
        Dim res As New List(Of ObjecteDades)()
        Using cmd As New SqlCommand(
            "SELECT SCHEMA_NAME(o.schema_id), o.name, CASE WHEN o.type = 'V' THEN 1 ELSE 0 END, " &
            "       ISNULL((SELECT SUM(p.rows) FROM sys.partitions p " &
            "               WHERE p.object_id = o.object_id AND p.index_id IN (0, 1)), 0) " &
            "FROM sys.objects o WHERE o.is_ms_shipped = 0 AND o.type IN ('U', 'V') " &
            "ORDER BY CASE WHEN o.type = 'V' THEN 1 ELSE 0 END, 1, 2;", c)
            Using r As SqlDataReader = cmd.ExecuteReader()
                Do While r.Read()
                    res.Add(New ObjecteDades With {
                        .Esquema = r.GetString(0), .Nom = r.GetString(1),
                        .EsVista = r.GetInt32(2) = 1, .FilesAprox = Convert.ToInt64(r.GetValue(3))})
                Loop
            End Using
        End Using
        Return res
    End Function

    ''' <summary>
    ''' SELECT TOP (n) de totes les columnes. Les columnes de tipus CLR
    ''' (geography, geometry, hierarchyid) es llegeixen com a text perquè
    ''' no calgui cap assemblat addicional al client.
    ''' </summary>
    Public Function SelectObjecte(c As SqlConnection, obj As ObjecteDades) As String
        Dim cols As New List(Of String)()
        Using cmd As New SqlCommand(
            "SELECT c.name, ty.is_assembly_type FROM sys.columns c " &
            "JOIN sys.types ty ON ty.user_type_id = c.user_type_id " &
            "WHERE c.object_id = OBJECT_ID(@o) ORDER BY c.column_id;", c)
            cmd.Parameters.AddWithValue("@o", obj.NomComplet)
            Using r As SqlDataReader = cmd.ExecuteReader()
                Do While r.Read()
                    Dim q As String = TSqlExporter.Q(r.GetString(0))
                    cols.Add(If(r.GetBoolean(1), q & ".ToString() AS " & q, q))
                Loop
            End Using
        End Using
        If cols.Count = 0 Then Throw New InvalidOperationException("No s'ha trobat l'objecte " & obj.NomComplet & ".")
        Return "SELECT TOP (@n) " & String.Join(", ", cols) & " FROM " & obj.NomComplet & ";"
    End Function

    Public Function CarregarObjecte(c As SqlConnection, obj As ObjecteDades, maxFiles As Integer) As DataTable
        Using cmd As New SqlCommand(SelectObjecte(c, obj), c)
            cmd.CommandTimeout = 300
            cmd.Parameters.AddWithValue("@n", Math.Max(1, maxFiles))
            Using r As SqlDataReader = cmd.ExecuteReader()
                Dim truncat As Boolean
                Dim dt As DataTable = LlegirResultat(r, maxFiles, truncat)
                dt.TableName = obj.Esquema & "." & obj.Nom
                Return dt
            End Using
        End Using
    End Function

    Public Function ComptarFiles(c As SqlConnection, obj As ObjecteDades) As Long
        Using cmd As New SqlCommand("SELECT COUNT_BIG(*) FROM " & obj.NomComplet & ";", c)
            cmd.CommandTimeout = 300
            Return Convert.ToInt64(cmd.ExecuteScalar())
        End Using
    End Function

    ''' <summary>
    ''' Executa una consulta lliure en mode només lectura (transacció que es
    ''' desfà sempre). Retorna tots els conjunts de resultats, cadascun limitat
    ''' a <paramref name="maxFiles"/> files.
    ''' </summary>
    Public Function ExecutarConsulta(c As SqlConnection, sql As String, maxFiles As Integer) As ResultatConsulta
        ' Un COMMIT dins la consulta confirmaria la transacció i trencaria el mode
        ' només lectura: es refusen les ordres de control de transaccions
        If Text.RegularExpressions.Regex.IsMatch(sql, "\b(COMMIT|ROLLBACK|SAVE\s+TRAN(SACTION)?|BEGIN\s+(DISTRIBUTED\s+)?TRAN(SACTION)?)\b",
                                                  Text.RegularExpressions.RegexOptions.IgnoreCase) Then
            Throw New InvalidOperationException(
                "La consulta conté ordres de transacció (COMMIT, ROLLBACK, BEGIN TRAN...). " &
                "El visor és només de lectura i no les permet.")
        End If

        Dim res As New ResultatConsulta()
        Using tx As SqlTransaction = c.BeginTransaction(IsolationLevel.ReadCommitted)
            Try
                Using cmd As New SqlCommand(sql, c, tx)
                    cmd.CommandTimeout = 300
                    Using r As SqlDataReader = cmd.ExecuteReader()
                        Dim n As Integer = 1
                        Do
                            If r.FieldCount > 0 Then
                                Dim truncat As Boolean
                                Dim dt As DataTable = LlegirResultat(r, maxFiles, truncat)
                                dt.TableName = "Resultat " & n
                                res.Taules.Add(dt)
                                res.Truncat = res.Truncat OrElse truncat
                                n += 1
                            End If
                        Loop While r.NextResult()
                        res.FilesAfectades = r.RecordsAffected
                    End Using
                End Using
            Finally
                Try
                    tx.Rollback()
                Catch
                    ' El servidor ja pot haver desfet la transacció per un error
                End Try
            End Try
        End Using
        Return res
    End Function

    ' Llegeix el resultat actual del reader fins a maxFiles files
    Private Function LlegirResultat(r As SqlDataReader, maxFiles As Integer, ByRef truncat As Boolean) As DataTable
        Dim dt As New DataTable()
        dt.Locale = CultureInfo.InvariantCulture
        For i As Integer = 0 To r.FieldCount - 1
            Dim nom As String = r.GetName(i)
            If String.IsNullOrEmpty(nom) Then nom = "(columna " & (i + 1) & ")"
            Dim base As String = nom, k As Integer = 2
            Do While dt.Columns.Contains(nom)
                nom = base & " (" & k & ")"
                k += 1
            Loop
            dt.Columns.Add(nom, r.GetFieldType(i))
        Next
        Dim valors(r.FieldCount - 1) As Object
        truncat = False
        Do While r.Read()
            If dt.Rows.Count >= maxFiles Then
                truncat = True
                Exit Do
            End If
            r.GetValues(valors)
            dt.Rows.Add(valors)
        Loop
        Return dt
    End Function

    ''' <summary>
    ''' Còpia per mostrar en una graella: els valors binaris es converteixen a
    ''' text hexadecimal (la graella intentaria mostrar-los com a imatges).
    ''' </summary>
    Public Function PerMostrar(dt As DataTable) As DataTable
        Dim teBinari As Boolean = False
        For Each col As DataColumn In dt.Columns
            If col.DataType Is GetType(Byte()) Then teBinari = True
        Next
        If Not teBinari Then Return dt

        Dim res As New DataTable(dt.TableName) With {.Locale = dt.Locale}
        For Each col As DataColumn In dt.Columns
            res.Columns.Add(col.ColumnName, If(col.DataType Is GetType(Byte()), GetType(String), col.DataType))
        Next
        For Each fila As DataRow In dt.Rows
            Dim valors(dt.Columns.Count - 1) As Object
            For i As Integer = 0 To dt.Columns.Count - 1
                Dim v As Object = fila(i)
                valors(i) = If(TypeOf v Is Byte(), Hex(DirectCast(v, Byte()), 64), v)
            Next
            res.Rows.Add(valors)
        Next
        Return res
    End Function

    Friend Function Hex(b As Byte(), max As Integer) As String
        Dim sb As New StringBuilder("0x")
        For i As Integer = 0 To Math.Min(b.Length, max) - 1
            sb.Append(b(i).ToString("X2", CultureInfo.InvariantCulture))
        Next
        If b.Length > max Then sb.Append("… (" & b.Length & " bytes)")
        Return sb.ToString()
    End Function

    ''' <summary>Exporta a CSV (UTF-8 amb BOM, compatible amb Excel).</summary>
    Public Sub ExportarCsv(dt As DataTable, ruta As String, Optional separador As String = ";")
        Dim sb As New StringBuilder()
        Dim cap As New List(Of String)()
        For Each col As DataColumn In dt.Columns
            cap.Add(CampCsv(col.ColumnName, separador))
        Next
        sb.AppendLine(String.Join(separador, cap))
        For Each fila As DataRow In dt.Rows
            Dim camps As New List(Of String)()
            For Each col As DataColumn In dt.Columns
                camps.Add(CampCsv(ValorText(fila(col)), separador))
            Next
            sb.AppendLine(String.Join(separador, camps))
        Next
        IO.File.WriteAllText(ruta, sb.ToString(), New UTF8Encoding(True))
    End Sub

    Private Function ValorText(v As Object) As String
        If v Is Nothing OrElse IsDBNull(v) Then Return ""
        If TypeOf v Is Byte() Then Return Hex(DirectCast(v, Byte()), Integer.MaxValue)
        If TypeOf v Is DateTime Then Return DirectCast(v, DateTime).ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture).TrimEnd("."c)
        If TypeOf v Is IFormattable Then Return DirectCast(v, IFormattable).ToString(Nothing, CultureInfo.CurrentCulture)
        Return v.ToString()
    End Function

    Private Function CampCsv(s As String, separador As String) As String
        If s.Contains(separador) OrElse s.Contains("""") OrElse s.Contains(vbCr) OrElse s.Contains(vbLf) Then
            Return """" & s.Replace("""", """""") & """"
        End If
        Return s
    End Function

End Module
