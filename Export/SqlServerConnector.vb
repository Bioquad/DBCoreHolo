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
' Mòdul central de connexió i operacions amb SQL Server.
'
' Responsabilitats:
'   - Construir connection strings (SqlConnectionStringBuilder)
'   - Llistar bases de dades disponibles
'   - Llegir l'estructura completa d'una BD (servidor en xarxa o
'     LocalDB/.mdf — MdfImporter reutilitza LlegirEstructura)
'   - Aplicar el DDL del model a una BD (complet o per diferències,
'     dins d'una transacció — MdfExporter també ho reutilitza)
'   - Enviar parts del model (taules seleccionades + FK)
'
' Tot el T-SQL es genera amb TSqlExporter (font única).
' Requereix: Microsoft.Data.SqlClient 5.2.1
' ============================================================
Public Module SqlServerConnector

    ' Errors SQL Server que es tracten com a "ja existia" (no fatals)
    Private Const ERR_OBJECTE_EXISTEIX As Integer = 2714
    Private Const ERR_INDEX_EXISTEIX As Integer = 1913

    ' ════════════════════════════════════════════════════════
    ' MODEL DE CONNEXIÓ
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
        ''' <summary>
        ''' True → s'accepta qualsevol certificat del servidor (útil amb certificats
        ''' autosignats en xarxes locals). False → el certificat es valida.
        ''' </summary>
        Public Property TrustServerCertificate As Boolean = True

        Public Function BuildConnectionString() As String
            Return Construir(BaseDades)
        End Function

        Public Function BuildMasterConnectionString() As String
            Return Construir("master")
        End Function

        Private Function Construir(bd As String) As String
            ' El builder escapa correctament qualsevol caràcter especial
            ' (p.ex. un ";" dins la contrasenya no pot injectar paràmetres)
            Dim b As New SqlConnectionStringBuilder()
            b.DataSource = If(Port > 0 AndAlso Port <> 1433, Servidor & "," & Port, Servidor)
            If Not String.IsNullOrEmpty(bd) Then b.InitialCatalog = bd
            If AuthWindows Then
                b.IntegratedSecurity = True
            Else
                b.IntegratedSecurity = False
                b.UserID = Usuari
                b.Password = Contrasenya
            End If
            b.ConnectTimeout = Math.Max(1, TimeoutSeg)
            b.Encrypt = If(Encrypt, SqlConnectionEncryptOption.Mandatory, SqlConnectionEncryptOption.Optional)
            b.TrustServerCertificate = TrustServerCertificate
            Return b.ConnectionString
        End Function

        Public Function Copia() As ConnexioServidor
            Return DirectCast(Me.MemberwiseClone(), ConnexioServidor)
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
    ' TEST DE CONNEXIÓ  — retorna "" si OK, o el missatge d'error
    ' ════════════════════════════════════════════════════════
    Public Function TestConnexio(conn As ConnexioServidor) As String
        Try
            Using c As New SqlConnection(conn.BuildConnectionString())
                c.Open()
                Using cmd As New SqlCommand("SELECT @@VERSION", c)
                    cmd.ExecuteScalar()
                End Using
            End Using
            Return ""
        Catch ex As Exception
            Return ex.Message
        End Try
    End Function

    ' ════════════════════════════════════════════════════════
    ' LLISTAR BASES DE DADES D'USUARI DEL SERVIDOR
    ' ════════════════════════════════════════════════════════
    Public Function LlistarBD(conn As ConnexioServidor) As List(Of String)
        Dim llista As New List(Of String)()
        Try
            Using c As New SqlConnection(conn.BuildMasterConnectionString())
                c.Open()
                Using cmd As New SqlCommand(
                    "SELECT name FROM sys.databases " &
                    "WHERE database_id > 4 AND state_desc = 'ONLINE' ORDER BY name;", c)
                    Using rdr As SqlDataReader = cmd.ExecuteReader()
                        Do While rdr.Read()
                            llista.Add(rdr.GetString(0))
                        Loop
                    End Using
                End Using
            End Using
        Catch
            ' Retornem llista buida; l'error es veurà al test de connexió
        End Try
        Return llista
    End Function

    ' ════════════════════════════════════════════════════════
    ' IMPORTAR ESTRUCTURA DES DEL SERVIDOR
    ' ════════════════════════════════════════════════════════
    Public Function ImportarEstructura(conn As ConnexioServidor) As ProyectoBBDD
        Using c As New SqlConnection(conn.BuildConnectionString())
            c.Open()
            Return LlegirEstructura(c, conn.BaseDades)
        End Using
    End Function

    ''' <summary>
    ''' Llegeix taules, columnes, PK (també compostes), UNIQUE i CHECK d'una
    ''' sola columna, descripcions i FK d'una connexió oberta.
    ''' Les FK de diverses columnes no es poden representar al model i
    ''' s'ometen (s'informa a <paramref name="avisos"/> si s'indica).
    ''' </summary>
    Public Function LlegirEstructura(c As SqlConnection, nomProjecte As String,
                                     Optional avisos As List(Of String) = Nothing) As ProyectoBBDD
        Dim p As New ProyectoBBDD()
        p.Nombre   = nomProjecte
        p.MotorSQL = "T-SQL"

        ' object_id → taula   i   object_id|column_id → camp
        Dim taules As New Dictionary(Of Integer, TablaBBDD)()
        Dim camps As New Dictionary(Of String, CampoBBDD)()

        ' ── 1. Taules + descripció ───────────────────────────────
        Executar(c,
            "SELECT t.object_id, s.name, t.name, CAST(ep.value AS NVARCHAR(MAX)) " &
            "FROM sys.tables t " &
            "JOIN sys.schemas s ON s.schema_id = t.schema_id " &
            "LEFT JOIN sys.extended_properties ep " &
            "  ON ep.class = 1 AND ep.major_id = t.object_id AND ep.minor_id = 0 " &
            "  AND ep.name = 'MS_Description' " &
            "WHERE t.is_ms_shipped = 0 " &
            "ORDER BY s.name, t.name;",
            Sub(rdr)
                Dim t As New TablaBBDD()
                t.Id     = p.GetNextTableId()
                t.Schema = rdr.GetString(1)
                t.Nombre = rdr.GetString(2).ToUpper()
                If Not rdr.IsDBNull(3) Then t.Descripcion = rdr.GetString(3)
                taules(rdr.GetInt32(0)) = t
                p.Taules.Add(t)
            End Sub)

        ' ── 2. Columnes ──────────────────────────────────────────
        Executar(c,
            "SELECT c.object_id, c.column_id, c.name, " &
            "       CASE WHEN ty.is_user_defined = 1 THEN TYPE_NAME(c.system_type_id) ELSE ty.name END, " &
            "       c.max_length, c.precision, c.scale, c.is_nullable, " &
            "       c.is_identity, CAST(ic.seed_value AS BIGINT), CAST(ic.increment_value AS BIGINT), " &
            "       c.is_rowguidcol, c.is_filestream, " &
            "       c.is_computed, cc.definition, cc.is_persisted, " &
            "       dc.definition, " &
            "       CASE WHEN c.collation_name <> CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS SYSNAME) " &
            "            THEN c.collation_name END, " &
            "       CAST(ep.value AS NVARCHAR(MAX)) " &
            "FROM sys.columns c " &
            "JOIN sys.tables t  ON t.object_id = c.object_id AND t.is_ms_shipped = 0 " &
            "JOIN sys.types ty  ON ty.user_type_id = c.user_type_id " &
            "LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id " &
            "LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id " &
            "LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id " &
            "LEFT JOIN sys.extended_properties ep " &
            "  ON ep.class = 1 AND ep.major_id = c.object_id AND ep.minor_id = c.column_id " &
            "  AND ep.name = 'MS_Description' " &
            "ORDER BY c.object_id, c.column_id;",
            Sub(rdr)
                Dim t As TablaBBDD = Nothing
                If Not taules.TryGetValue(rdr.GetInt32(0), t) Then Return

                Dim f As New CampoBBDD()
                f.Nombre   = rdr.GetString(2).ToUpper()
                Dim tipus As String = If(rdr.IsDBNull(3), "", rdr.GetString(3))
                f.TipoDato = MapTipus(tipus)

                Dim maxLen As Integer = rdr.GetInt16(4)
                If f.NecessitaLongitud Then
                    If maxLen = -1 Then
                        f.LongitudMax = True
                    ElseIf tipus.Equals("nchar", StringComparison.OrdinalIgnoreCase) OrElse
                           tipus.Equals("nvarchar", StringComparison.OrdinalIgnoreCase) Then
                        f.Longitud = maxLen \ 2
                    Else
                        f.Longitud = maxLen
                    End If
                End If
                If f.NecessitaPrecEsc Then
                    f.Precision = rdr.GetByte(5)
                    f.Escala    = rdr.GetByte(6)
                End If

                f.NotNull = Not rdr.GetBoolean(7)
                If rdr.GetBoolean(8) Then
                    f.EsIdentity        = True
                    f.IdentitySeed      = If(rdr.IsDBNull(9), 1, CInt(rdr.GetInt64(9)))
                    f.IdentityIncrement = If(rdr.IsDBNull(10), 1, CInt(rdr.GetInt64(10)))
                End If
                f.EsRowGuid    = rdr.GetBoolean(11)
                f.EsFileStream = rdr.GetBoolean(12)
                If rdr.GetBoolean(13) Then
                    f.EsCalculado    = True
                    f.FormulaCalculo = TreureParentesisExterns(If(rdr.IsDBNull(14), "", rdr.GetString(14)))
                    f.EsPersistido   = Not rdr.IsDBNull(15) AndAlso rdr.GetBoolean(15)
                End If
                If Not rdr.IsDBNull(16) Then f.DefaultValue = TreureParentesisExterns(rdr.GetString(16))
                If Not rdr.IsDBNull(17) Then f.Collation = rdr.GetString(17)
                If Not rdr.IsDBNull(18) Then f.Descripcion = rdr.GetString(18)

                t.Fields.Add(f)
                camps(rdr.GetInt32(0) & "|" & rdr.GetInt32(1)) = f
            End Sub)

        ' ── 3. PK (simples i compostes) ──────────────────────────
        Executar(c,
            "SELECT ic.object_id, ic.column_id " &
            "FROM sys.indexes i " &
            "JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id " &
            "WHERE i.is_primary_key = 1 " &
            "ORDER BY ic.object_id, ic.key_ordinal;",
            Sub(rdr)
                Dim f As CampoBBDD = Nothing
                If camps.TryGetValue(rdr.GetInt32(0) & "|" & rdr.GetInt32(1), f) Then
                    f.EsPK    = True
                    f.NotNull = True
                End If
            End Sub)

        ' ── 4. UNIQUE d'una sola columna ─────────────────────────
        Executar(c,
            "SELECT i.object_id, MIN(ic.column_id) " &
            "FROM sys.indexes i " &
            "JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id " &
            "  AND ic.is_included_column = 0 " &
            "WHERE i.is_unique_constraint = 1 " &
            "GROUP BY i.object_id, i.index_id HAVING COUNT(*) = 1;",
            Sub(rdr)
                Dim f As CampoBBDD = Nothing
                If camps.TryGetValue(rdr.GetInt32(0) & "|" & rdr.GetInt32(1), f) Then f.EsUnique = True
            End Sub)

        ' ── 5. CHECK de columna ──────────────────────────────────
        Executar(c,
            "SELECT parent_object_id, parent_column_id, definition " &
            "FROM sys.check_constraints WHERE parent_column_id > 0;",
            Sub(rdr)
                Dim f As CampoBBDD = Nothing
                If camps.TryGetValue(rdr.GetInt32(0) & "|" & rdr.GetInt32(1), f) AndAlso
                   String.IsNullOrEmpty(f.CheckExpression) Then
                    f.CheckExpression = TreureParentesisExterns(rdr.GetString(2))
                End If
            End Sub)

        ' ── 6. Foreign Keys ──────────────────────────────────────
        Executar(c,
            "SELECT fk.name, fk.parent_object_id, fkc.parent_column_id, " &
            "       fk.referenced_object_id, fkc.referenced_column_id, " &
            "       fk.delete_referential_action, fk.update_referential_action, " &
            "       fk.is_disabled, fk.is_not_trusted, fk.is_not_for_replication, " &
            "       (SELECT COUNT(*) FROM sys.foreign_key_columns x " &
            "         WHERE x.constraint_object_id = fk.object_id) " &
            "FROM sys.foreign_keys fk " &
            "JOIN sys.foreign_key_columns fkc " &
            "  ON fkc.constraint_object_id = fk.object_id AND fkc.constraint_column_id = 1 " &
            "ORDER BY fk.name;",
            Sub(rdr)
                Dim fkNom As String = rdr.GetString(0)
                If rdr.GetInt32(10) > 1 Then
                    avisos?.Add("FK """ & fkNom & """: és de diverses columnes i no es pot representar al model (s'ha omès).")
                    Return
                End If
                Dim ft As TablaBBDD = Nothing
                Dim tt As TablaBBDD = Nothing
                Dim fFK As CampoBBDD = Nothing
                Dim fPK As CampoBBDD = Nothing
                If Not taules.TryGetValue(rdr.GetInt32(1), ft) OrElse
                   Not taules.TryGetValue(rdr.GetInt32(3), tt) OrElse
                   Not camps.TryGetValue(rdr.GetInt32(1) & "|" & rdr.GetInt32(2), fFK) OrElse
                   Not camps.TryGetValue(rdr.GetInt32(3) & "|" & rdr.GetInt32(4), fPK) Then Return

                fFK.EsFK = True

                Dim rel As New RelacionBBDD()
                rel.Id                = p.GetNextRelId()
                rel.Nombre            = fkNom
                rel.TablaOrigenId     = ft.Id
                rel.CampoFKNombre     = fFK.Nombre
                rel.TablaDestinoId    = tt.Id
                rel.CampoPKNombre     = fPK.Nombre
                rel.TipoRelacion      = CardinalityType.ManyToOne
                rel.OnDelete          = MapAccio(rdr.GetByte(5))
                rel.OnUpdate          = MapAccio(rdr.GetByte(6))
                rel.Disabled          = rdr.GetBoolean(7)
                rel.WithCheck         = If(rdr.GetBoolean(8), WithCheckOption.WithNoCheck, WithCheckOption.WithCheck)
                rel.NotForReplication = rdr.GetBoolean(9)
                rel.CrearIndexFK      = True
                p.Relacions.Add(rel)
            End Sub)

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
        Dim bdCreada As Boolean = False
        Try
            If String.IsNullOrWhiteSpace(conn.BaseDades) Then
                res.MissatgeError = "Cal indicar el nom de la base de dades."
                Return res
            End If

            Using masterConn As New SqlConnection(conn.BuildMasterConnectionString())
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
                        bdCreada = True

                    Case 1  ' DropAndCreate
                        If bdJaExisteix Then EliminarBd(masterConn, conn.BaseDades)
                        CrearBd(masterConn, conn.BaseDades)
                        bdCreada = True

                    Case 2  ' AlterExisting
                        If Not bdJaExisteix Then
                            CrearBd(masterConn, conn.BaseDades)
                            bdCreada = True
                        End If
                End Select
            End Using

            Using destConn As New SqlConnection(conn.BuildConnectionString())
                destConn.Open()
                If mode = 2 AndAlso Not bdCreada Then
                    AplicarDiferencies(destConn, p, res)
                Else
                    AplicarDDLComplet(destConn, p, res)
                End If
            End Using

            res.OK = True
        Catch ex As Exception
            res.MissatgeError = ex.Message
            ' Si hem creat la BD en aquesta mateixa operació, no la deixem a mitges
            If bdCreada Then
                Try
                    SqlConnection.ClearAllPools()
                    Using masterConn As New SqlConnection(conn.BuildMasterConnectionString())
                        masterConn.Open()
                        EliminarBd(masterConn, conn.BaseDades)
                    End Using
                Catch
                End Try
            End If
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
        Dim exp As New TSqlExporter()
        Try
            Using destConn As New SqlConnection(conn.BuildConnectionString())
                destConn.Open()
                EnTransaccio(destConn,
                    Sub(tx)
                        For Each esq As String In TSqlExporter.EsquemesNecessaris(taules)
                            ExecutarDDL(destConn, tx, exp.GenerarCrearEsquema(esq), res)
                        Next
                        For Each t As TablaBBDD In taules
                            If ExecutarDDL(destConn, tx, exp.GenerarTaula(t), res) Then res.TaulesCreades += 1
                        Next

                        For Each r As RelacionBBDD In relacions
                            Dim ft As TablaBBDD = taules.FirstOrDefault(Function(t) t.Id = r.TablaOrigenId)
                            Dim tt As TablaBBDD = taules.FirstOrDefault(Function(t) t.Id = r.TablaDestinoId)
                            If ft IsNot Nothing AndAlso tt IsNot Nothing Then
                                If ExecutarDDL(destConn, tx, exp.GenerarAlterFK(r, ft, tt), res) Then
                                    res.RelacionsCreades += 1
                                End If
                                If r.CrearIndexFK Then CrearIndexSiCal(destConn, tx, r, ft, res)
                            End If
                        Next

                        For Each t As TablaBBDD In taules
                            AplicarExtProps(destConn, tx, t, res)
                        Next
                    End Sub)
            End Using
            res.OK = True
        Catch ex As Exception
            res.MissatgeError = ex.Message
        End Try
        Return res
    End Function

    ' ════════════════════════════════════════════════════════
    ' APLICACIÓ DEL DDL (compartit amb MdfExporter)
    ' Tot s'executa dins d'una transacció: o s'aplica tot o res.
    ' ════════════════════════════════════════════════════════

    ''' <summary>BD buida: crea totes les taules, FK, índexs i descripcions.</summary>
    Friend Sub AplicarDDLComplet(conn As SqlConnection, p As ProyectoBBDD, res As ResultatOperacio)
        Dim exp As New TSqlExporter()
        EnTransaccio(conn,
            Sub(tx)
                For Each esq As String In TSqlExporter.EsquemesNecessaris(p.Taules)
                    ExecutarDDL(conn, tx, exp.GenerarCrearEsquema(esq), res)
                Next
                For Each t As TablaBBDD In p.Taules
                    If ExecutarDDL(conn, tx, exp.GenerarTaula(t), res) Then res.TaulesCreades += 1
                Next
                For Each r As RelacionBBDD In p.Relacions
                    Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
                    Dim tt As TablaBBDD = BuscarTaula(p, r.TablaDestinoId)
                    If ft IsNot Nothing AndAlso tt IsNot Nothing Then
                        If ExecutarDDL(conn, tx, exp.GenerarAlterFK(r, ft, tt), res) Then res.RelacionsCreades += 1
                    End If
                Next
                For Each r As RelacionBBDD In p.Relacions
                    Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
                    If r.CrearIndexFK AndAlso ft IsNot Nothing Then CrearIndexSiCal(conn, tx, r, ft, res)
                Next
                For Each t As TablaBBDD In p.Taules
                    AplicarExtProps(conn, tx, t, res)
                Next
                AplicarExtPropsRelacions(conn, tx, p, res)
            End Sub)
    End Sub

    ''' <summary>
    ''' BD existent: afegeix taules, columnes, FK i índexs que falten i
    ''' actualitza descripcions. NO elimina res del que ja hi ha.
    ''' </summary>
    Friend Sub AplicarDiferencies(conn As SqlConnection, p As ProyectoBBDD, res As ResultatOperacio)
        Dim exp As New TSqlExporter()
        EnTransaccio(conn,
            Sub(tx)
                ' ── Estat actual de la BD ────────────────────────
                Dim taulesExist As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                Dim colsExist As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                Dim fkExist As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

                Executar(conn, tx,
                    "SELECT s.name + '.' + t.name FROM sys.tables t " &
                    "JOIN sys.schemas s ON s.schema_id = t.schema_id;",
                    Sub(rdr) taulesExist.Add(rdr.GetString(0)))
                Executar(conn, tx,
                    "SELECT s.name + '.' + t.name + '.' + c.name FROM sys.columns c " &
                    "JOIN sys.tables t ON t.object_id = c.object_id " &
                    "JOIN sys.schemas s ON s.schema_id = t.schema_id;",
                    Sub(rdr) colsExist.Add(rdr.GetString(0)))
                Executar(conn, tx,
                    "SELECT SCHEMA_NAME(schema_id) + '.' + name FROM sys.foreign_keys;",
                    Sub(rdr) fkExist.Add(rdr.GetString(0)))

                ' ── Esquemes, taules i columnes ──────────────────
                For Each esq As String In TSqlExporter.EsquemesNecessaris(p.Taules)
                    ExecutarDDL(conn, tx, exp.GenerarCrearEsquema(esq), res)
                Next
                For Each t As TablaBBDD In p.Taules
                    Dim key As String = t.SchemaEfectiu & "." & t.Nombre
                    If Not taulesExist.Contains(key) Then
                        If ExecutarDDL(conn, tx, exp.GenerarTaula(t), res) Then res.TaulesCreades += 1
                    Else
                        Dim colsNoves As Integer = 0
                        For Each f As CampoBBDD In t.Fields
                            If Not colsExist.Contains(key & "." & f.Nombre) Then
                                Dim addSql As String = exp.GenerarAddColumn(t, f, res.Advertencies)
                                If ExecutarDDL(conn, tx, addSql, res) Then colsNoves += 1
                            End If
                        Next
                        If colsNoves > 0 Then res.TaulesAlterades += 1
                    End If
                Next

                ' ── Foreign keys noves ───────────────────────────
                For Each r As RelacionBBDD In p.Relacions
                    Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
                    Dim tt As TablaBBDD = BuscarTaula(p, r.TablaDestinoId)
                    If ft Is Nothing OrElse tt Is Nothing Then Continue For
                    If Not fkExist.Contains(ft.SchemaEfectiu & "." & r.Nombre) Then
                        If ExecutarDDL(conn, tx, exp.GenerarAlterFK(r, ft, tt), res) Then res.RelacionsCreades += 1
                    End If
                    If r.CrearIndexFK Then CrearIndexSiCal(conn, tx, r, ft, res)
                Next

                ' ── Descripcions (afegir o actualitzar) ──────────
                For Each t As TablaBBDD In p.Taules
                    AplicarExtProps(conn, tx, t, res)
                Next
                AplicarExtPropsRelacions(conn, tx, p, res)
            End Sub)
    End Sub

    ' ════════════════════════════════════════════════════════
    ' HELPERS INTERNS
    ' ════════════════════════════════════════════════════════

    ''' <summary>
    ''' Executa l'acció dins d'una transacció. Si falla, la desfà i
    ''' rellança l'excepció.
    ''' </summary>
    Private Sub EnTransaccio(conn As SqlConnection, accio As Action(Of SqlTransaction))
        Using tx As SqlTransaction = conn.BeginTransaction()
            Try
                accio(tx)
                tx.Commit()
            Catch
                Try
                    tx.Rollback()
                Catch
                    ' La transacció ja pot haver estat desfeta pel servidor
                End Try
                Throw
            End Try
        End Using
    End Sub

    ''' <summary>
    ''' Executa una sentència DDL. Retorna True si s'ha executat i False si
    ''' s'ha ignorat perquè l'objecte ja existia (queda com a advertència).
    ''' Qualsevol altre error atura el procés.
    ''' </summary>
    Private Function ExecutarDDL(conn As SqlConnection, tx As SqlTransaction,
                                 sql As String, res As ResultatOperacio) As Boolean
        If String.IsNullOrWhiteSpace(sql) Then Return False
        Try
            Using cmd As New SqlCommand(sql, conn, tx)
                cmd.CommandTimeout = 60
                cmd.ExecuteNonQuery()
            End Using
            Return True
        Catch ex As SqlException When ex.Number = ERR_OBJECTE_EXISTEIX OrElse ex.Number = ERR_INDEX_EXISTEIX
            res.Advertencies.Add("Ja existia (ignorat): " & TruncSql(sql))
            Return False
        Catch ex As SqlException
            Throw New Exception("Error SQL " & ex.Number & ": " & ex.Message &
                                Environment.NewLine & "SQL: " & TruncSql(sql), ex)
        End Try
    End Function

    Private Sub Executar(conn As SqlConnection, sql As String, perFila As Action(Of SqlDataReader))
        Executar(conn, Nothing, sql, perFila)
    End Sub

    Private Sub Executar(conn As SqlConnection, tx As SqlTransaction, sql As String,
                         perFila As Action(Of SqlDataReader))
        Using cmd As New SqlCommand(sql, conn, tx)
            cmd.CommandTimeout = 60
            Using rdr As SqlDataReader = cmd.ExecuteReader()
                Do While rdr.Read()
                    perFila(rdr)
                Loop
            End Using
        End Using
    End Sub

    Private Function Escalar(conn As SqlConnection, tx As SqlTransaction, sql As String,
                             ParamArray params() As SqlParameter) As Integer
        Using cmd As New SqlCommand(sql, conn, tx)
            cmd.Parameters.AddRange(params)
            Dim v As Object = cmd.ExecuteScalar()
            Return If(v Is Nothing OrElse IsDBNull(v), 0, Convert.ToInt32(v))
        End Using
    End Function

    Private Sub CrearIndexSiCal(conn As SqlConnection, tx As SqlTransaction,
                                r As RelacionBBDD, ft As TablaBBDD, res As ResultatOperacio)
        Dim n As Integer = Escalar(conn, tx,
            "SELECT COUNT(*) FROM sys.indexes WHERE name = @n AND object_id = OBJECT_ID(@o);",
            New SqlParameter("@n", TSqlExporter.NomIndexFK(r, ft)),
            New SqlParameter("@o", TSqlExporter.NomTaula(ft)))
        If n = 0 Then ExecutarDDL(conn, tx, New TSqlExporter().GenerarIndexFK(r, ft), res)
    End Sub

    ' Afegeix o actualitza les descripcions (MS_Description) d'una taula i els seus camps
    Private Sub AplicarExtProps(conn As SqlConnection, tx As SqlTransaction,
                                t As TablaBBDD, res As ResultatOperacio)
        Dim exp As New TSqlExporter()
        If Not String.IsNullOrEmpty(t.Descripcion) Then
            Dim existeix As Boolean = Escalar(conn, tx,
                "SELECT COUNT(*) FROM sys.extended_properties " &
                "WHERE class = 1 AND name = 'MS_Description' AND major_id = OBJECT_ID(@o) AND minor_id = 0;",
                New SqlParameter("@o", TSqlExporter.NomTaula(t))) > 0
            ExecutarDDL(conn, tx, exp.GenerarExtPropTaula(t, existeix), res)
        End If
        For Each f As CampoBBDD In t.Fields
            If String.IsNullOrEmpty(f.Descripcion) Then Continue For
            Dim existeix As Boolean = Escalar(conn, tx,
                "SELECT COUNT(*) FROM sys.extended_properties " &
                "WHERE class = 1 AND name = 'MS_Description' AND major_id = OBJECT_ID(@o) " &
                "  AND minor_id = COLUMNPROPERTY(OBJECT_ID(@o), @c, 'ColumnId');",
                New SqlParameter("@o", TSqlExporter.NomTaula(t)),
                New SqlParameter("@c", f.Nombre)) > 0
            ExecutarDDL(conn, tx, exp.GenerarExtPropCamp(t, f, existeix), res)
        Next
    End Sub

    Private Sub AplicarExtPropsRelacions(conn As SqlConnection, tx As SqlTransaction,
                                         p As ProyectoBBDD, res As ResultatOperacio)
        Dim exp As New TSqlExporter()
        For Each r As RelacionBBDD In p.Relacions
            If String.IsNullOrEmpty(r.Descripcion) Then Continue For
            Dim ft As TablaBBDD = BuscarTaula(p, r.TablaOrigenId)
            If ft Is Nothing Then Continue For
            Dim existeix As Boolean = Escalar(conn, tx,
                "SELECT COUNT(*) FROM sys.extended_properties ep " &
                "JOIN sys.foreign_keys fk ON fk.object_id = ep.major_id " &
                "WHERE ep.class = 1 AND ep.name = 'MS_Description' AND ep.minor_id = 0 " &
                "  AND fk.name = @n AND fk.parent_object_id = OBJECT_ID(@o);",
                New SqlParameter("@n", r.Nombre),
                New SqlParameter("@o", TSqlExporter.NomTaula(ft))) > 0
            If Not existeix Then ExecutarDDL(conn, tx, exp.GenerarExtPropRelacio(r, ft), res)
        Next
    End Sub

    Private Function BuscarTaula(p As ProyectoBBDD, id As Integer) As TablaBBDD
        For Each t As TablaBBDD In p.Taules
            If t.Id = id Then Return t
        Next
        Return Nothing
    End Function

    Private Function BdExisteix(conn As SqlConnection, nom As String) As Boolean
        Return Escalar(conn, Nothing, "SELECT COUNT(*) FROM sys.databases WHERE name = @n;",
                       New SqlParameter("@n", nom)) > 0
    End Function

    Private Sub CrearBd(conn As SqlConnection, nom As String)
        Using cmd As New SqlCommand("CREATE DATABASE " & TSqlExporter.Q(nom) & ";", conn)
            cmd.CommandTimeout = 120
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Sub EliminarBd(conn As SqlConnection, nom As String)
        ' Desconnectar sessions actives i eliminar
        Dim q As String = TSqlExporter.Q(nom)
        Using cmd As New SqlCommand(
            "ALTER DATABASE " & q & " SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " &
            "DROP DATABASE " & q & ";", conn)
            cmd.CommandTimeout = 120
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    ''' <summary>"((0))" → "(0)" ; "(getdate())" → "getdate()" (un sol nivell).</summary>
    Friend Function TreureParentesisExterns(s As String) As String
        If s Is Nothing Then Return ""
        s = s.Trim()
        If s.Length < 2 OrElse s(0) <> "("c OrElse s(s.Length - 1) <> ")"c Then Return s
        ' Comprovar que el primer "(" tanca just al final
        Dim depth As Integer = 0
        Dim enCadena As Boolean = False
        For i As Integer = 0 To s.Length - 1
            Dim ch As Char = s(i)
            If ch = "'"c Then
                enCadena = Not enCadena
            ElseIf Not enCadena Then
                If ch = "("c Then
                    depth += 1
                ElseIf ch = ")"c Then
                    depth -= 1
                    If depth = 0 AndAlso i < s.Length - 1 Then Return s
                End If
            End If
        Next
        Return s.Substring(1, s.Length - 2).Trim()
    End Function

    Private Function TruncSql(sql As String) As String
        Dim s As String = sql.Trim().Replace(vbCrLf, " ").Replace(vbLf, " ")
        If s.Length > 160 Then Return s.Substring(0, 160) & "…"
        Return s
    End Function

    Friend Function MapTipus(sqlType As String) As DataType
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
            Case "nvarchar", "sysname" : Return DataType.NVarChar
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
