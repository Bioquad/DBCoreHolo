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
Imports Microsoft.Data.SqlClient

' ============================================================
' CopiaBaseDades.vb — Còpia completa d'una base de dades SQL Server
' (estructura, vistes/procediments/funcions/triggers i totes les
' dades) des d'un .mdf del disc o d'un servidor, cap a un .mdf nou
' o una BD nova d'un servidor. No cal obrir-la al dissenyador.
'
' Dos mètodes, escollits automàticament:
'   1. Mateixa instància SQL Server (p.ex. .mdf → .mdf amb LocalDB,
'      o dues BD del mateix servidor):
'        BACKUP ... COPY_ONLY  +  RESTORE ... WITH MOVE
'      → còpia exacta de tot (inclosos usuaris, permisos, etc.).
'   2. Instàncies diferents:
'        estructura llegida del catàleg (CatalegBD) i recreada,
'        dades copiades amb SqlBulkCopy, i després índexs, CHECK,
'        FK, triggers, sinònims i propietats esteses.
'
' Mai sobreescriu res: si la BD o els fitxers de destí ja existeixen,
' s'atura. Si la còpia falla, la BD de destí creada s'elimina.
' ============================================================
Public Module CopiaBaseDades

    Public Class ResultatCopia
        Public Property OK As Boolean
        Public Property Metode As String = ""
        Public Property Taules As Integer
        Public Property Files As Long
        Public Property Objectes As Integer
        Public Property Advertencies As New List(Of String)()
        Public Property MissatgeError As String = ""
    End Class

    Public Function Copiar(origen As UbicacioBD, desti As UbicacioBD,
                           Optional progres As IProgress(Of String) = Nothing) As ResultatCopia
        Dim res As New ResultatCopia()
        ' Un .mdf de destí queda desadjuntat al final, de manera que el nom intern
        ' a LocalDB no importa: se'n fa servir un de temporal per no xocar amb
        ' l'origen (p.ex. copiar C:\a\Botiga.mdf a D:\b\Botiga.mdf)
        Dim nomDesti As String = If(desti.EsFitxer,
                                    desti.NomBD & "_copia_" & Guid.NewGuid().ToString("N").Substring(0, 8),
                                    desti.NomBD)
        Dim bdCreada As Boolean = False

        Try
            MdfExporter.ValidarNom(desti.NomBD)
            If desti.EsFitxer AndAlso Not desti.RutaMdf.EndsWith(".mdf", StringComparison.OrdinalIgnoreCase) Then
                Throw New ArgumentException("El fitxer de destí ha de tenir l'extensió .mdf.")
            End If

            Informar(progres, "Connectant amb l'origen: " & origen.ToString())
            Using src As SqlConnection = origen.ObrirConnexio()
                Dim nomOrigen As String = CStr(Escalar(src, Nothing, "SELECT DB_NAME();"))

                Informar(progres, "Connectant amb el destí: " & desti.ToString())
                Using dstMaster As New SqlConnection(desti.CadenaMaster())
                    dstMaster.Open()

                    If BdExisteix(dstMaster, nomDesti) Then
                        Throw New InvalidOperationException("Ja existeix una base de dades anomenada '" & nomDesti &
                                                            "' al destí. Tria un altre nom.")
                    End If
                    If desti.EsFitxer Then
                        For Each f As String In {desti.RutaMdf, RutaLog(desti.RutaMdf)}
                            If IO.File.Exists(f) Then
                                Throw New InvalidOperationException("El fitxer '" & f & "' ja existeix. Tria un altre nom.")
                            End If
                        Next
                    End If

                    Dim fet As Boolean = False
                    If IdInstancia(dstMaster) = IdInstancia(src) Then
                        Try
                            res.Metode = "BACKUP / RESTORE"
                            bdCreada = True
                            CopiarAmbBackup(dstMaster, nomOrigen, nomDesti, desti, res, progres)
                            fet = True
                        Catch ex As SqlException
                            ' Sense permisos de BACKUP/RESTORE o ruta inaccessible: es fa per transferència
                            res.Advertencies.Add("No s'ha pogut fer BACKUP/RESTORE (" & ex.Message &
                                                 "); s'ha copiat per transferència.")
                            Informar(progres, "BACKUP/RESTORE no disponible: es copia per transferència.")
                            If BdExisteix(dstMaster, nomDesti) Then MdfExporter.EliminarBd(dstMaster, nomDesti)
                            bdCreada = False
                        End Try
                    End If

                    If Not fet Then
                        res.Metode = "Transferència (estructura + SqlBulkCopy)"
                        Informar(progres, "Llegint l'estructura de l'origen...")
                        Dim cat As CatalegBD = CatalegBD.Llegir(src)
                        res.Advertencies.AddRange(cat.Avisos)

                        Informar(progres, "Creant la base de dades de destí...")
                        If desti.EsFitxer Then
                            MdfExporter.CrearBd(dstMaster, nomDesti, desti.RutaMdf, RutaLog(desti.RutaMdf), cat.Collation)
                        Else
                            Executar(dstMaster, Nothing, "CREATE DATABASE " & TSqlExporter.Q(nomDesti) &
                                     MdfExporter.ClausulaCollation(cat.Collation) & ";")
                        End If
                        bdCreada = True

                        Using dst As New SqlConnection(CadenaBd(desti, nomDesti))
                            dst.Open()
                            Transferir(cat, src, dst, res, progres)
                        End Using
                    End If
                End Using
            End Using

            ' Resum del resultat
            Using dst As New SqlConnection(CadenaBd(desti, nomDesti))
                dst.Open()
                res.Taules = CInt(Escalar(dst, Nothing, "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0;"))
                res.Files = Convert.ToInt64(Escalar(dst, Nothing,
                    "SELECT ISNULL(SUM(p.rows), 0) FROM sys.partitions p " &
                    "JOIN sys.tables t ON t.object_id = p.object_id AND t.is_ms_shipped = 0 " &
                    "WHERE p.index_id IN (0, 1);"))
                res.Objectes = CInt(Escalar(dst, Nothing,
                    "SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped = 0 AND type IN ('V','P','FN','IF','TF','TR','SN','SO');"))
            End Using

            ' A partir d'aquí la còpia ja és correcta: res del que segueix la invalida
            res.OK = True
            bdCreada = False

            ' Un .mdf de destí es desadjunta perquè quedi com a fitxer independent
            If desti.EsFitxer Then
                Informar(progres, "Desadjuntant el fitxer de LocalDB...")
                Try
                    SqlConnection.ClearAllPools()
                    Using m As New SqlConnection(desti.CadenaMaster())
                        m.Open()
                        Dim q As String = TSqlExporter.Q(nomDesti)
                        ' Tancar altres connexions; es torna a MULTI_USER perquè el
                        ' mode d'accés queda desat dins del fitxer
                        Executar(m, Nothing, "ALTER DATABASE " & q & " SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " &
                                             "ALTER DATABASE " & q & " SET MULTI_USER;")
                        Using cmd As New SqlCommand("sys.sp_detach_db", m)
                            cmd.CommandType = CommandType.StoredProcedure
                            cmd.Parameters.AddWithValue("@dbname", nomDesti)
                            cmd.ExecuteNonQuery()
                        End Using
                    End Using
                Catch ex As SqlException
                    res.Advertencies.Add("La còpia és correcta, però no s'ha pogut desadjuntar de LocalDB (" & ex.Message &
                                         "). Continua adjuntada amb el nom '" & nomDesti & "'.")
                End Try
            End If

            Informar(progres, "Còpia completada.")
        Catch ex As Exception
            res.OK = False
            res.MissatgeError = ex.Message
            Informar(progres, "ERROR: " & ex.Message)
            If bdCreada Then
                Try
                    SqlConnection.ClearAllPools()
                    Using m As New SqlConnection(desti.CadenaMaster())
                        m.Open()
                        If BdExisteix(m, nomDesti) Then MdfExporter.EliminarBd(m, nomDesti)
                    End Using
                    Informar(progres, "S'ha eliminat la còpia incompleta.")
                Catch
                End Try
            End If
        End Try
        Return res
    End Function

    Private Function CadenaBd(desti As UbicacioBD, nomBd As String) As String
        Dim b As New SqlConnectionStringBuilder(desti.CadenaMaster())
        b.InitialCatalog = nomBd
        Return b.ConnectionString
    End Function

    Private Function BdExisteix(master As SqlConnection, nom As String) As Boolean
        Return CInt(Escalar(master, Nothing, "SELECT COUNT(*) FROM sys.databases WHERE name = @n;",
                            New SqlParameter("@n", nom))) > 0
    End Function

    ' ════════════════════════════════════════════════════════
    ' MÈTODE 1: BACKUP / RESTORE (mateixa instància)
    ' ════════════════════════════════════════════════════════
    Private Sub CopiarAmbBackup(master As SqlConnection, nomOrigen As String, nomDesti As String,
                                desti As UbicacioBD, res As ResultatCopia, progres As IProgress(Of String))

        ' Directoris de destí (rutes del servidor)
        Dim dirDades As String
        Dim dirLog As String
        If desti.EsFitxer Then
            dirDades = IO.Path.GetDirectoryName(desti.RutaMdf)
            IO.Directory.CreateDirectory(dirDades)
            dirLog = dirDades
        Else
            dirDades = DirectoriServidor(master, "InstanceDefaultDataPath", nomOrigen, 0)
            dirLog = DirectoriServidor(master, "InstanceDefaultLogPath", nomOrigen, 1)
        End If

        Dim bak As String = CombinarRuta(dirDades, nomDesti & "_" & Guid.NewGuid().ToString("N") & ".bak")
        Try
            Informar(progres, "Fent una còpia de seguretat (COPY_ONLY) de '" & nomOrigen & "'...")
            Executar(master, Nothing, "BACKUP DATABASE @db TO DISK = @bak WITH COPY_ONLY, INIT, FORMAT;",
                     New SqlParameter("@db", nomOrigen), New SqlParameter("@bak", bak))

            ' Fitxers lògics de la còpia
            Dim fitxers As New List(Of Tuple(Of String, String, String))()   ' lògic, físic, tipus
            Using cmd As New SqlCommand("RESTORE FILELISTONLY FROM DISK = @bak;", master)
                cmd.Parameters.AddWithValue("@bak", bak)
                cmd.CommandTimeout = 0
                Using r As SqlDataReader = cmd.ExecuteReader()
                    Do While r.Read()
                        fitxers.Add(Tuple.Create(CStr(r("LogicalName")), CStr(r("PhysicalName")), CStr(r("Type"))))
                    Loop
                End Using
            End Using

            ' RESTORE ... WITH MOVE de cada fitxer a la seva nova ruta
            Dim sql As New Text.StringBuilder("RESTORE DATABASE @nou FROM DISK = @bak WITH ")
            Dim params As New List(Of SqlParameter) From {New SqlParameter("@nou", nomDesti), New SqlParameter("@bak", bak)}
            Dim nDades As Integer = 0, nLog As Integer = 0, nAltres As Integer = 0
            For i As Integer = 0 To fitxers.Count - 1
                Dim nouFitxer As String
                Select Case fitxers(i).Item3
                    Case "D"
                        nouFitxer = If(nDades = 0,
                                       If(desti.EsFitxer, desti.RutaMdf, CombinarRuta(dirDades, nomDesti & ".mdf")),
                                       CombinarRuta(dirDades, nomDesti & "_" & nDades & ".ndf"))
                        nDades += 1
                    Case "L"
                        nouFitxer = If(nLog = 0,
                                       If(desti.EsFitxer, RutaLog(desti.RutaMdf), CombinarRuta(dirLog, nomDesti & "_log.ldf")),
                                       CombinarRuta(dirLog, nomDesti & "_log" & nLog & ".ldf"))
                        nLog += 1
                    Case Else   ' FILESTREAM (S) o full-text (F): directoris
                        nAltres += 1
                        nouFitxer = CombinarRuta(dirDades, nomDesti & "_" & fitxers(i).Item3.ToLowerInvariant() & nAltres)
                End Select
                If i > 0 Then sql.Append(", ")
                sql.Append("MOVE @l" & i & " TO @p" & i)
                params.Add(New SqlParameter("@l" & i, fitxers(i).Item1))
                params.Add(New SqlParameter("@p" & i, nouFitxer))
            Next
            sql.Append(", RECOVERY;")

            Informar(progres, "Restaurant com a '" & nomDesti & "'...")
            Executar(master, Nothing, sql.ToString(), params.ToArray())
        Finally
            EsborrarCopiaTemporal(master, bak, res)
        End Try
    End Sub

    Private Sub EsborrarCopiaTemporal(master As SqlConnection, bak As String, res As ResultatCopia)
        ' Si el servidor és aquesta mateixa màquina (LocalDB), s'esborra directament
        Try
            If IO.File.Exists(bak) Then
                IO.File.Delete(bak)
                Return
            End If
        Catch
        End Try
        ' Servidor remot: xp_delete_files (SQL Server 2019+) si hi ha permisos
        Try
            Executar(master, Nothing, "EXEC master.sys.xp_delete_files @f;", New SqlParameter("@f", bak))
            Return
        Catch
        End Try
        res.Advertencies.Add("No s'ha pogut esborrar la còpia temporal del servidor: " & bak)
    End Sub

    ' Directori per defecte de la instància (o el del fitxer de la BD d'origen)
    Private Function DirectoriServidor(master As SqlConnection, propietat As String,
                                       nomOrigen As String, tipusFitxer As Integer) As String
        Dim v As Object = Escalar(master, Nothing,
            "SELECT CAST(SERVERPROPERTY(@p) AS NVARCHAR(4000));", New SqlParameter("@p", propietat))
        If v IsNot Nothing AndAlso Not IsDBNull(v) AndAlso CStr(v).Trim() <> "" Then Return CStr(v)
        Dim fisic As String = CStr(Escalar(master, Nothing,
            "SELECT TOP (1) physical_name FROM sys.master_files WHERE database_id = DB_ID(@n) AND type = @t;",
            New SqlParameter("@n", nomOrigen), New SqlParameter("@t", tipusFitxer)))
        Dim sep As Integer = Math.Max(fisic.LastIndexOf("\"c), fisic.LastIndexOf("/"c))
        Return fisic.Substring(0, sep + 1)
    End Function

    ' Combina rutes del servidor (pot ser Windows "\" o Linux "/")
    Friend Function CombinarRuta(dir As String, fitxer As String) As String
        Dim sep As String = If(dir.Contains("/") AndAlso Not dir.Contains("\"), "/", "\")
        Return If(dir.EndsWith("\") OrElse dir.EndsWith("/"), dir, dir & sep) & fitxer
    End Function

    Friend Function RutaLog(rutaMdf As String) As String
        Return IO.Path.Combine(IO.Path.GetDirectoryName(rutaMdf),
                               IO.Path.GetFileNameWithoutExtension(rutaMdf) & "_log.ldf")
    End Function

    ' ════════════════════════════════════════════════════════
    ' MÈTODE 2: TRANSFERÈNCIA (instàncies diferents)
    ' ════════════════════════════════════════════════════════
    Private Sub Transferir(cat As CatalegBD, src As SqlConnection, dst As SqlConnection,
                           res As ResultatCopia, progres As IProgress(Of String))
        ' 1. Esquemes, tipus i seqüències
        For Each e As String In cat.Esquemes
            Executar(dst, Nothing, GeneradorCataleg.CrearEsquema(e))
        Next
        For Each t As CatTipus In cat.Tipus
            Executar(dst, Nothing, GeneradorCataleg.CrearTipus(t))
        Next
        For Each s As CatSequencia In cat.Sequencies
            Executar(dst, Nothing, GeneradorCataleg.CrearSequencia(s))
        Next

        ' 2. Taules i mòduls (excepte triggers). Una vista pot dependre d'una
        '    altra, o una columna calculada d'una funció: es repeteix fins que
        '    ja no es pot crear res més.
        Informar(progres, "Creant " & cat.Taules.Count & " taules i " & cat.Moduls.Count & " objectes programables...")
        Dim pendents As New List(Of Object)()
        pendents.AddRange(cat.Taules)
        pendents.AddRange(cat.Moduls.FindAll(Function(m) Not m.EsTrigger))
        Dim errors As Dictionary(Of Object, String) = CrearAmbReintents(dst, pendents)
        For Each kv As KeyValuePair(Of Object, String) In errors
            If TypeOf kv.Key Is CatTaula Then
                Throw New Exception("No s'ha pogut crear la taula " & DirectCast(kv.Key, CatTaula).NomComplet & ": " & kv.Value)
            End If
            Dim m As CatModul = DirectCast(kv.Key, CatModul)
            res.Advertencies.Add("No s'ha pogut crear " & m.Esquema & "." & m.Nom & ": " & kv.Value)
        Next

        ' 3. Dades
        For Each t As CatTaula In cat.Taules
            Dim sel As String = GeneradorCataleg.SelectCopia(t)
            If sel Is Nothing Then Continue For
            Informar(progres, "Copiant dades de " & t.NomComplet & "...")
            Using cmd As New SqlCommand(sel, src)
                cmd.CommandTimeout = 0
                Using r As SqlDataReader = cmd.ExecuteReader()
                    Using bulk As New SqlBulkCopy(dst, SqlBulkCopyOptions.KeepIdentity Or SqlBulkCopyOptions.KeepNulls Or
                                                       SqlBulkCopyOptions.TableLock, Nothing)
                        bulk.DestinationTableName = t.NomComplet
                        bulk.BulkCopyTimeout = 0
                        bulk.BatchSize = 5000
                        bulk.EnableStreaming = True
                        For Each c As CatColumna In t.Columnes
                            If c.EsCopiable Then bulk.ColumnMappings.Add(c.Nom, c.Nom)
                        Next
                        bulk.WriteToServer(r)
                    End Using
                End Using
            End Using

            ' Comptador IDENTITY igual que a l'origen
            For Each c As CatColumna In t.Columnes
                If Not c.EsIdentity Then Continue For
                Dim teFiles As Boolean = CInt(Escalar(dst, Nothing,
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM " & t.NomComplet & ") THEN 1 ELSE 0 END;")) = 1
                Dim reseed As String = GeneradorCataleg.ReajustarIdentity(t, c, teFiles)
                If reseed IsNot Nothing Then Executar(dst, Nothing, reseed)
            Next
        Next

        ' 4. Índexs, CHECK i FK (després de les dades: més ràpid i amb validació)
        Informar(progres, "Creant índexs, restriccions CHECK i claus foranes...")
        For Each t As CatTaula In cat.Taules
            For Each sql As String In GeneradorCataleg.CrearIndexs(t)
                Executar(dst, Nothing, sql)
            Next
        Next
        For Each t As CatTaula In cat.Taules
            For Each ck As CatCheck In t.Checks
                Executar(dst, Nothing, GeneradorCataleg.CrearCheck(t, ck))
            Next
        Next
        For Each t As CatTaula In cat.Taules
            For Each fk As CatFK In t.FKs
                Executar(dst, Nothing, GeneradorCataleg.CrearFK(t, fk))
            Next
        Next

        ' 5. Triggers (al final, perquè no s'executin durant la còpia)
        For Each m As CatModul In cat.Moduls.FindAll(Function(x) x.EsTrigger)
            Try
                Executar(dst, Nothing, GeneradorCataleg.OpcionsModul(m))
                Executar(dst, Nothing, m.Definicio)
                If m.Desactivat Then Executar(dst, Nothing, GeneradorCataleg.DesactivarTrigger(m))
            Catch ex As SqlException
                res.Advertencies.Add("No s'ha pogut crear el trigger " & m.Nom & ": " & ex.Message)
            End Try
        Next

        ' 6. Sinònims i propietats esteses
        For Each s As CatSinonim In cat.Sinonims
            Try
                Executar(dst, Nothing, GeneradorCataleg.CrearSinonim(s))
            Catch ex As SqlException
                res.Advertencies.Add("No s'ha pogut crear el sinònim " & s.Nom & ": " & ex.Message)
            End Try
        Next
        For Each p As CatPropietat In cat.Propietats
            Try
                Using cmd As New SqlCommand("sys.sp_addextendedproperty", dst)
                    cmd.CommandType = CommandType.StoredProcedure
                    cmd.Parameters.AddWithValue("@name", p.Nom)
                    cmd.Parameters.Add(New SqlParameter("@value", SqlDbType.Variant) With {.Value = If(p.Valor, DBNull.Value)})
                    cmd.Parameters.AddWithValue("@level0type", "SCHEMA")
                    cmd.Parameters.AddWithValue("@level0name", p.Esquema)
                    cmd.Parameters.AddWithValue("@level1type", p.TipusObjecte)
                    cmd.Parameters.AddWithValue("@level1name", p.Objecte)
                    If p.Columna IsNot Nothing Then
                        cmd.Parameters.AddWithValue("@level2type", "COLUMN")
                        cmd.Parameters.AddWithValue("@level2name", p.Columna)
                    End If
                    cmd.ExecuteNonQuery()
                End Using
            Catch ex As SqlException
                res.Advertencies.Add("Propietat " & p.Nom & " de " & p.Objecte & ": " & ex.Message)
            End Try
        Next
    End Sub

    ''' <summary>
    ''' Crea taules i mòduls tolerant l'ordre de dependències: el que falla es
    ''' torna a provar mentre en cada volta se n'hagi creat algun. Retorna els
    ''' que no s'han pogut crear amb l'últim error.
    ''' </summary>
    Private Function CrearAmbReintents(dst As SqlConnection, pendents As List(Of Object)) As Dictionary(Of Object, String)
        Dim errors As New Dictionary(Of Object, String)()
        Do
            errors.Clear()
            Dim creats As Integer = 0
            For Each obj As Object In pendents.ToArray()
                Try
                    If TypeOf obj Is CatTaula Then
                        Executar(dst, Nothing, GeneradorCataleg.CrearTaula(DirectCast(obj, CatTaula)))
                    Else
                        Dim m As CatModul = DirectCast(obj, CatModul)
                        Executar(dst, Nothing, GeneradorCataleg.OpcionsModul(m))
                        Executar(dst, Nothing, m.Definicio)
                    End If
                    pendents.Remove(obj)
                    creats += 1
                Catch ex As SqlException
                    errors(obj) = ex.Message
                End Try
            Next
            If pendents.Count = 0 OrElse creats = 0 Then Exit Do
        Loop
        Return errors
    End Function

    ' ════════════════════════════════════════════════════════
    ' HELPERS
    ' ════════════════════════════════════════════════════════

    ' Identifica la instància: dues connexions al mateix procés SQL Server
    ' poden fer BACKUP/RESTORE entre elles
    Private Function IdInstancia(c As SqlConnection) As String
        Return CStr(Escalar(c, Nothing,
            "SELECT CAST(SERVERPROPERTY('ServerName') AS NVARCHAR(256)) + N'|' + " &
            "CAST(SERVERPROPERTY('ProcessID') AS NVARCHAR(20));"))
    End Function

    Private Sub Informar(progres As IProgress(Of String), msg As String)
        progres?.Report(msg)
    End Sub

    Private Sub Executar(c As SqlConnection, tx As SqlTransaction, sql As String, ParamArray params() As SqlParameter)
        Using cmd As New SqlCommand(sql, c, tx)
            cmd.CommandTimeout = 0
            cmd.Parameters.AddRange(params)
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Function Escalar(c As SqlConnection, tx As SqlTransaction, sql As String, ParamArray params() As SqlParameter) As Object
        Using cmd As New SqlCommand(sql, c, tx)
            cmd.CommandTimeout = 300
            cmd.Parameters.AddRange(params)
            Return cmd.ExecuteScalar()
        End Using
    End Function

End Module
