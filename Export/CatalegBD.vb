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
Imports System.Globalization
Imports System.Text
Imports Microsoft.Data.SqlClient

' ============================================================
' CatalegBD.vb — Estructura completa d'una BD SQL Server llegida
' del catàleg (sys.*) i generació del T-SQL per recrear-la.
'
' A diferència del model del dissenyador (ProyectoBBDD), aquí es
' conserva TOT el que cal per a una còpia fidel: tipus exactes,
' col·lacions, PK/UNIQUE/FK de diverses columnes, índexs amb
' INCLUDE i filtre, CHECK, DEFAULT, seqüències, tipus d'usuari,
' vistes, procediments, funcions, triggers, sinònims i
' propietats esteses.
'
' La lectura (CatalegBD.Llegir) i la generació (GeneradorCataleg)
' estan separades perquè la generació es pugui provar sense
' servidor.
' ============================================================

Public Class CatColumna
    Public Nom As String
    Public Tipus As String                 ' nom del tipus (sistema o d'usuari)
    Public EsquemaTipus As String = Nothing ' només per a tipus d'usuari
    Public MaxLength As Integer
    Public Precisio As Integer
    Public Escala As Integer
    Public Nullable As Boolean = True
    Public EsIdentity As Boolean
    Public IdentitySeed As String = "1"
    Public IdentityIncrement As String = "1"
    Public IdentityUltimValor As String = Nothing   ' NULL si mai s'ha generat cap valor
    Public EsCalculada As Boolean
    Public Formula As String
    Public Persistida As Boolean
    Public EsRowGuid As Boolean
    Public EsSparse As Boolean
    Public Collation As String = Nothing
    Public DefaultNom As String = Nothing
    Public DefaultDefinicio As String = Nothing
    Public EsClr As Boolean                ' geography, geometry, hierarchyid...

    ''' <summary>timestamp/rowversion: no es poden inserir valors.</summary>
    Public ReadOnly Property EsRowVersion As Boolean
        Get
            Return EsquemaTipus Is Nothing AndAlso
                   (String.Equals(Tipus, "timestamp", StringComparison.OrdinalIgnoreCase) OrElse
                    String.Equals(Tipus, "rowversion", StringComparison.OrdinalIgnoreCase))
        End Get
    End Property

    ''' <summary>Es copien les dades d'aquesta columna?</summary>
    Public ReadOnly Property EsCopiable As Boolean
        Get
            Return Not EsCalculada AndAlso Not EsRowVersion
        End Get
    End Property
End Class

Public Class CatColumnaIndex
    Public Nom As String
    Public Descendent As Boolean
End Class

Public Class CatIndex
    Public Nom As String
    Public EsPK As Boolean
    Public EsRestriccioUnica As Boolean
    Public EsUnic As Boolean
    Public EsClustered As Boolean
    Public Columnes As New List(Of CatColumnaIndex)()
    Public Incloses As New List(Of String)()
    Public Filtre As String = Nothing
End Class

Public Class CatCheck
    Public Nom As String
    Public Definicio As String
    Public NoConfiable As Boolean   ' is_not_trusted → WITH NOCHECK
    Public Desactivat As Boolean
End Class

Public Class CatFK
    Public Nom As String
    Public Columnes As New List(Of String)()
    Public EsquemaRef As String
    Public TaulaRef As String
    Public ColumnesRef As New List(Of String)()
    Public OnDelete As String = "NO ACTION"
    Public OnUpdate As String = "NO ACTION"
    Public NotForReplication As Boolean
    Public NoConfiable As Boolean
    Public Desactivada As Boolean
End Class

Public Class CatTaula
    Public ObjectId As Integer
    Public Esquema As String
    Public Nom As String
    Public Columnes As New List(Of CatColumna)()
    Public Indexs As New List(Of CatIndex)()
    Public Checks As New List(Of CatCheck)()
    Public FKs As New List(Of CatFK)()

    Public ReadOnly Property NomComplet As String
        Get
            Return TSqlExporter.Q(Esquema) & "." & TSqlExporter.Q(Nom)
        End Get
    End Property
End Class

Public Class CatModul
    Public Esquema As String
    Public Nom As String
    Public Tipus As String          ' V, P, FN, IF, TF, TR
    Public Definicio As String
    Public QuotedIdentifier As Boolean = True
    Public AnsiNulls As Boolean = True
    Public EsTriggerBD As Boolean    ' trigger DDL a nivell de base de dades
    Public TaulaPare As String = Nothing   ' [esquema].[taula] dels triggers DML
    Public Desactivat As Boolean

    Public ReadOnly Property EsTrigger As Boolean
        Get
            Return Tipus = "TR"
        End Get
    End Property
End Class

Public Class CatSequencia
    Public Esquema As String
    Public Nom As String
    Public Tipus As String
    Public Inici As String
    Public Increment As String
    Public Minim As String
    Public Maxim As String
    Public Cicle As Boolean
    Public Cache As String = Nothing   ' Nothing → NO CACHE ; "" → CACHE per defecte
End Class

Public Class CatTipus
    Public Esquema As String
    Public Nom As String
    Public TipusBase As String
    Public Nullable As Boolean = True
End Class

Public Class CatSinonim
    Public Esquema As String
    Public Nom As String
    Public Base As String
End Class

Public Class CatPropietat
    Public Nom As String
    Public Valor As Object
    Public Esquema As String
    Public TipusObjecte As String   ' TABLE, VIEW, PROCEDURE, FUNCTION
    Public Objecte As String
    Public Columna As String = Nothing
End Class

Public Class CatalegBD
    Public Collation As String
    Public Esquemes As New List(Of String)()
    Public Tipus As New List(Of CatTipus)()
    Public Sequencies As New List(Of CatSequencia)()
    Public Taules As New List(Of CatTaula)()
    Public Moduls As New List(Of CatModul)()
    Public Sinonims As New List(Of CatSinonim)()
    Public Propietats As New List(Of CatPropietat)()
    ''' <summary>Elements que no es poden copiar (s'informen a l'usuari).</summary>
    Public Avisos As New List(Of String)()

    ' ════════════════════════════════════════════════════════
    ' LECTURA DEL CATÀLEG (SQL Server 2012 o posterior)
    ' ════════════════════════════════════════════════════════
    Public Shared Function Llegir(c As SqlConnection) As CatalegBD
        Dim cat As New CatalegBD()
        cat.Collation = CStr(Escalar(c, "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS NVARCHAR(128));"))

        ' ── Esquemes d'usuari (no dbo, sys, guest, INFORMATION_SCHEMA ni rols) ──
        Llegir(c, "SELECT name FROM sys.schemas WHERE schema_id BETWEEN 5 AND 16383 ORDER BY name;",
               Sub(r) cat.Esquemes.Add(r.GetString(0)))

        ' ── Tipus d'usuari ───────────────────────────────────────
        Llegir(c,
            "SELECT SCHEMA_NAME(t.schema_id), t.name, TYPE_NAME(t.system_type_id), t.max_length, " &
            "       t.precision, t.scale, t.is_nullable, t.is_table_type, t.is_assembly_type " &
            "FROM sys.types t WHERE t.is_user_defined = 1 ORDER BY t.name;",
            Sub(r)
                Dim nom As String = r.GetString(0) & "." & r.GetString(1)
                If r.GetBoolean(7) Then
                    cat.Avisos.Add("Tipus de taula " & nom & ": no es copia (els procediments que el facin servir poden fallar).")
                ElseIf r.GetBoolean(8) Then
                    cat.Avisos.Add("Tipus CLR " & nom & ": no es copia.")
                Else
                    cat.Tipus.Add(New CatTipus With {
                        .Esquema = r.GetString(0), .Nom = r.GetString(1),
                        .TipusBase = GeneradorCataleg.TipusSql(r.GetString(2), r.GetInt16(3), r.GetByte(4), r.GetByte(5)),
                        .Nullable = r.GetBoolean(6)})
                End If
            End Sub)

        ' ── Seqüències ───────────────────────────────────────────
        ' SELECT * perquè last_used_value només existeix a partir de SQL Server 2017
        Llegir(c,
            "SELECT SCHEMA_NAME(s.schema_id) AS esquema_, TYPE_NAME(s.user_type_id) AS tipus_, s.* " &
            "FROM sys.sequences s ORDER BY s.name;",
            Sub(r)
                Dim sq As New CatSequencia With {
                    .Esquema = r.GetString(r.GetOrdinal("esquema_")),
                    .Nom = r.GetString(r.GetOrdinal("name")),
                    .Tipus = r.GetString(r.GetOrdinal("tipus_")),
                    .Increment = Num(r("increment")),
                    .Minim = Num(r("minimum_value")),
                    .Maxim = Num(r("maximum_value")),
                    .Cicle = CBool(r("is_cycling"))}
                If CBool(r("is_cached")) Then
                    sq.Cache = If(IsDBNull(r("cache_size")), "", Num(r("cache_size")))
                End If
                If sq.Tipus = "decimal" OrElse sq.Tipus = "numeric" Then
                    sq.Tipus &= "(" & CInt(r("precision")) & ",0)"
                End If
                ' Valor amb què ha de continuar la còpia
                Dim inici As Decimal = Convert.ToDecimal(r("current_value"), CultureInfo.InvariantCulture)
                Dim teUltim As Boolean = False
                For i As Integer = 0 To r.FieldCount - 1
                    If r.GetName(i) = "last_used_value" Then teUltim = True
                Next
                If teUltim Then
                    If IsDBNull(r("last_used_value")) Then
                        inici = Convert.ToDecimal(r("start_value"), CultureInfo.InvariantCulture)
                    Else
                        inici = Convert.ToDecimal(r("last_used_value"), CultureInfo.InvariantCulture) +
                                Convert.ToDecimal(r("increment"), CultureInfo.InvariantCulture)
                    End If
                End If
                Dim mn As Decimal = Convert.ToDecimal(r("minimum_value"), CultureInfo.InvariantCulture)
                Dim mx As Decimal = Convert.ToDecimal(r("maximum_value"), CultureInfo.InvariantCulture)
                If inici < mn OrElse inici > mx Then inici = Convert.ToDecimal(r("current_value"), CultureInfo.InvariantCulture)
                sq.Inici = inici.ToString(CultureInfo.InvariantCulture)
                cat.Sequencies.Add(sq)
            End Sub)

        ' ── Taules ───────────────────────────────────────────────
        Dim perId As New Dictionary(Of Integer, CatTaula)()
        Llegir(c,
            "SELECT t.object_id, SCHEMA_NAME(t.schema_id), t.name, " &
            "       CAST(OBJECTPROPERTY(t.object_id, 'TableIsMemoryOptimized') AS INT), " &
            "       CAST(OBJECTPROPERTY(t.object_id, 'TableTemporalType') AS INT) " &
            "FROM sys.tables t WHERE t.is_ms_shipped = 0 ORDER BY 2, 3;",
            Sub(r)
                Dim t As New CatTaula With {.ObjectId = r.GetInt32(0), .Esquema = r.GetString(1), .Nom = r.GetString(2)}
                If Not r.IsDBNull(3) AndAlso r.GetInt32(3) = 1 Then
                    cat.Avisos.Add(t.NomComplet & ": taula en memòria (In-Memory OLTP); es copia com a taula normal.")
                End If
                If Not r.IsDBNull(4) AndAlso r.GetInt32(4) = 2 Then
                    cat.Avisos.Add(t.NomComplet & ": taula temporal; es copia amb les dades però sense SYSTEM_VERSIONING.")
                End If
                perId(t.ObjectId) = t
                cat.Taules.Add(t)
            End Sub)

        ' ── Columnes ─────────────────────────────────────────────
        Llegir(c,
            "SELECT c.object_id, c.name, ty.name, CASE WHEN ty.is_user_defined = 1 THEN SCHEMA_NAME(ty.schema_id) END, " &
            "       c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity, " &
            "       CONVERT(NVARCHAR(50), ic.seed_value), CONVERT(NVARCHAR(50), ic.increment_value), " &
            "       CONVERT(NVARCHAR(50), ic.last_value), " &
            "       c.is_computed, cc.definition, cc.is_persisted, c.is_rowguidcol, c.is_sparse, " &
            "       c.collation_name, dc.name, dc.definition, ty.is_assembly_type, c.is_filestream, " &
            "       CASE WHEN ty.is_user_defined = 1 AND ty.is_assembly_type = 0 THEN TYPE_NAME(ty.system_type_id) END " &
            "FROM sys.columns c " &
            "JOIN sys.tables t ON t.object_id = c.object_id AND t.is_ms_shipped = 0 " &
            "JOIN sys.types ty ON ty.user_type_id = c.user_type_id " &
            "LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id " &
            "LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id " &
            "LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id " &
            "ORDER BY c.object_id, c.column_id;",
            Sub(r)
                Dim t As CatTaula = Nothing
                If Not perId.TryGetValue(r.GetInt32(0), t) Then Return
                Dim col As New CatColumna With {
                    .Nom = r.GetString(1), .Tipus = r.GetString(2),
                    .MaxLength = r.GetInt16(4), .Precisio = r.GetByte(5), .Escala = r.GetByte(6),
                    .Nullable = r.GetBoolean(7), .EsIdentity = r.GetBoolean(8),
                    .EsCalculada = r.GetBoolean(12), .EsRowGuid = r.GetBoolean(15), .EsSparse = r.GetBoolean(16),
                    .EsClr = r.GetBoolean(20)}
                ' Els tipus CLR (geography, geometry, hierarchyid) són "de sistema" però
                ' tenen is_user_defined = 0; els d'usuari porten esquema
                If Not r.IsDBNull(3) Then
                    If col.EsClr Then
                        cat.Avisos.Add(t.NomComplet & "." & col.Nom & ": tipus CLR d'usuari; no es pot copiar.")
                    End If
                    col.EsquemaTipus = r.GetString(3)
                End If
                If col.EsIdentity Then
                    If Not r.IsDBNull(9) Then col.IdentitySeed = r.GetString(9)
                    If Not r.IsDBNull(10) Then col.IdentityIncrement = r.GetString(10)
                    If Not r.IsDBNull(11) Then col.IdentityUltimValor = r.GetString(11)
                End If
                If col.EsCalculada Then
                    col.Formula = If(r.IsDBNull(13), "", r.GetString(13))
                    col.Persistida = Not r.IsDBNull(14) AndAlso r.GetBoolean(14)
                End If
                If Not r.IsDBNull(17) Then col.Collation = r.GetString(17)
                If Not r.IsDBNull(18) Then
                    col.DefaultNom = r.GetString(18)
                    col.DefaultDefinicio = r.GetString(19)
                End If
                If r.GetBoolean(21) Then
                    cat.Avisos.Add(t.NomComplet & "." & col.Nom & ": FILESTREAM; es copia com a VARBINARY(MAX) normal.")
                End If
                t.Columnes.Add(col)
            End Sub)

        ' ── Índexs, PK i UNIQUE ──────────────────────────────────
        Dim indexs As New Dictionary(Of String, CatIndex)()
        Llegir(c,
            "SELECT i.object_id, i.index_id, i.name, i.type, i.is_primary_key, i.is_unique_constraint, " &
            "       i.is_unique, i.filter_definition " &
            "FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id AND t.is_ms_shipped = 0 " &
            "WHERE i.type > 0 AND i.is_hypothetical = 0 " &
            "ORDER BY i.object_id, i.index_id;",
            Sub(r)
                Dim t As CatTaula = Nothing
                If Not perId.TryGetValue(r.GetInt32(0), t) Then Return
                Dim tipus As Integer = r.GetByte(3)
                If tipus <> 1 AndAlso tipus <> 2 Then
                    cat.Avisos.Add(t.NomComplet & ": índex " & r.GetString(2) &
                                   " (XML, espacial, columnstore o hash) no es copia.")
                    Return
                End If
                Dim ix As New CatIndex With {
                    .Nom = r.GetString(2), .EsClustered = (tipus = 1),
                    .EsPK = r.GetBoolean(4), .EsRestriccioUnica = r.GetBoolean(5), .EsUnic = r.GetBoolean(6),
                    .Filtre = If(r.IsDBNull(7), Nothing, r.GetString(7))}
                t.Indexs.Add(ix)
                indexs(r.GetInt32(0) & "|" & r.GetInt32(1)) = ix
            End Sub)
        Llegir(c,
            "SELECT ic.object_id, ic.index_id, c.name, ic.is_descending_key, ic.is_included_column " &
            "FROM sys.index_columns ic " &
            "JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id " &
            "ORDER BY ic.object_id, ic.index_id, ic.is_included_column, ic.key_ordinal, ic.index_column_id;",
            Sub(r)
                Dim ix As CatIndex = Nothing
                If Not indexs.TryGetValue(r.GetInt32(0) & "|" & r.GetInt32(1), ix) Then Return
                If r.GetBoolean(4) Then
                    ix.Incloses.Add(r.GetString(2))
                Else
                    ix.Columnes.Add(New CatColumnaIndex With {.Nom = r.GetString(2), .Descendent = r.GetBoolean(3)})
                End If
            End Sub)

        ' ── CHECK ────────────────────────────────────────────────
        Llegir(c,
            "SELECT parent_object_id, name, definition, is_not_trusted, is_disabled " &
            "FROM sys.check_constraints ORDER BY parent_object_id, name;",
            Sub(r)
                Dim t As CatTaula = Nothing
                If Not perId.TryGetValue(r.GetInt32(0), t) Then Return
                t.Checks.Add(New CatCheck With {.Nom = r.GetString(1), .Definicio = r.GetString(2),
                                                .NoConfiable = r.GetBoolean(3), .Desactivat = r.GetBoolean(4)})
            End Sub)

        ' ── Foreign keys (també de diverses columnes) ────────────
        Dim fks As New Dictionary(Of Integer, CatFK)()
        Llegir(c,
            "SELECT fk.object_id, fk.parent_object_id, fk.name, SCHEMA_NAME(rt.schema_id), rt.name, " &
            "       fk.delete_referential_action_desc, fk.update_referential_action_desc, " &
            "       fk.is_not_for_replication, fk.is_not_trusted, fk.is_disabled " &
            "FROM sys.foreign_keys fk JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id " &
            "ORDER BY fk.parent_object_id, fk.name;",
            Sub(r)
                Dim t As CatTaula = Nothing
                If Not perId.TryGetValue(r.GetInt32(1), t) Then Return
                Dim fk As New CatFK With {
                    .Nom = r.GetString(2), .EsquemaRef = r.GetString(3), .TaulaRef = r.GetString(4),
                    .OnDelete = r.GetString(5).Replace("_", " "), .OnUpdate = r.GetString(6).Replace("_", " "),
                    .NotForReplication = r.GetBoolean(7), .NoConfiable = r.GetBoolean(8), .Desactivada = r.GetBoolean(9)}
                t.FKs.Add(fk)
                fks(r.GetInt32(0)) = fk
            End Sub)
        Llegir(c,
            "SELECT fkc.constraint_object_id, pc.name, rc.name " &
            "FROM sys.foreign_key_columns fkc " &
            "JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id " &
            "JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id " &
            "ORDER BY fkc.constraint_object_id, fkc.constraint_column_id;",
            Sub(r)
                Dim fk As CatFK = Nothing
                If Not fks.TryGetValue(r.GetInt32(0), fk) Then Return
                fk.Columnes.Add(r.GetString(1))
                fk.ColumnesRef.Add(r.GetString(2))
            End Sub)

        ' ── Vistes, procediments, funcions i triggers ────────────
        Llegir(c,
            "SELECT o.type, SCHEMA_NAME(o.schema_id), o.name, m.definition, m.uses_quoted_identifier, m.uses_ansi_nulls, " &
            "       NULL, NULL, CAST(0 AS BIT) " &
            "FROM sys.objects o LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id " &
            "WHERE o.is_ms_shipped = 0 AND o.type IN ('V', 'P', 'FN', 'IF', 'TF') " &
            "UNION ALL " &
            "SELECT 'TR', SCHEMA_NAME(pt.schema_id), tr.name, m.definition, m.uses_quoted_identifier, m.uses_ansi_nulls, " &
            "       SCHEMA_NAME(pt.schema_id), pt.name, tr.is_disabled " &
            "FROM sys.triggers tr " &
            "LEFT JOIN sys.sql_modules m ON m.object_id = tr.object_id " &
            "LEFT JOIN sys.objects pt ON pt.object_id = tr.parent_id AND tr.parent_class = 1 " &
            "WHERE tr.is_ms_shipped = 0 AND tr.type = 'TR';",
            Sub(r)
                Dim tipus As String = r.GetString(0).Trim()
                Dim m As New CatModul With {
                    .Tipus = tipus, .Esquema = If(r.IsDBNull(1), Nothing, r.GetString(1)), .Nom = r.GetString(2),
                    .QuotedIdentifier = r.IsDBNull(4) OrElse r.GetBoolean(4),
                    .AnsiNulls = r.IsDBNull(5) OrElse r.GetBoolean(5),
                    .Desactivat = r.GetBoolean(8)}
                If tipus = "TR" Then
                    ' Sense objecte pare → trigger DDL de base de dades (el pare
                    ' d'un trigger DML pot ser una taula o una vista)
                    If r.IsDBNull(7) Then
                        m.EsTriggerBD = True
                    Else
                        m.TaulaPare = TSqlExporter.Q(r.GetString(6)) & "." & TSqlExporter.Q(r.GetString(7))
                    End If
                End If
                If r.IsDBNull(3) Then
                    cat.Avisos.Add("Objecte " & If(m.Esquema, "") & "." & m.Nom & ": està xifrat (WITH ENCRYPTION); no es pot copiar.")
                    Return
                End If
                m.Definicio = r.GetString(3)
                cat.Moduls.Add(m)
            End Sub)

        ' ── Sinònims ─────────────────────────────────────────────
        Llegir(c, "SELECT SCHEMA_NAME(schema_id), name, base_object_name FROM sys.synonyms ORDER BY name;",
               Sub(r) cat.Sinonims.Add(New CatSinonim With {.Esquema = r.GetString(0), .Nom = r.GetString(1), .Base = r.GetString(2)}))

        ' ── Propietats esteses d'objectes i columnes ─────────────
        Llegir(c,
            "SELECT ep.name, ep.value, SCHEMA_NAME(o.schema_id), o.name, o.type, c.name " &
            "FROM sys.extended_properties ep " &
            "JOIN sys.objects o ON o.object_id = ep.major_id AND o.is_ms_shipped = 0 " &
            "LEFT JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id " &
            "WHERE ep.class = 1 AND o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF') " &
            "  AND (ep.minor_id = 0 OR c.column_id IS NOT NULL);",
            Sub(r)
                Dim tipusObj As String
                Select Case r.GetString(4).Trim()
                    Case "U" : tipusObj = "TABLE"
                    Case "V" : tipusObj = "VIEW"
                    Case "P" : tipusObj = "PROCEDURE"
                    Case Else : tipusObj = "FUNCTION"
                End Select
                cat.Propietats.Add(New CatPropietat With {
                    .Nom = r.GetString(0), .Valor = r.GetValue(1), .Esquema = r.GetString(2),
                    .TipusObjecte = tipusObj, .Objecte = r.GetString(3),
                    .Columna = If(r.IsDBNull(5), Nothing, r.GetString(5))})
            End Sub)

        ' ── Elements que no es copien ────────────────────────────
        If CInt(Escalar(c, "SELECT COUNT(*) FROM sys.database_principals WHERE principal_id > 4 AND is_fixed_role = 0 AND type IN ('S','U','G','R','E','X')")) > 0 Then
            cat.Avisos.Add("Usuaris, rols i permisos de la BD: no es copien (depenen dels logins de cada servidor).")
        End If
        If CInt(Escalar(c, "SELECT COUNT(*) FROM sys.assemblies WHERE is_user_defined = 1")) > 0 Then
            cat.Avisos.Add("Assemblats CLR: no es copien.")
        End If
        If CInt(Escalar(c, "SELECT COUNT(*) FROM sys.fulltext_catalogs")) > 0 Then
            cat.Avisos.Add("Catàlegs i índexs de text complet (full-text): no es copien.")
        End If

        Return cat
    End Function

    Private Shared Sub Llegir(c As SqlConnection, sql As String, perFila As Action(Of SqlDataReader))
        Using cmd As New SqlCommand(sql, c)
            cmd.CommandTimeout = 300
            Using r As SqlDataReader = cmd.ExecuteReader()
                Do While r.Read()
                    perFila(r)
                Loop
            End Using
        End Using
    End Sub

    Private Shared Function Escalar(c As SqlConnection, sql As String) As Object
        Using cmd As New SqlCommand(sql, c)
            cmd.CommandTimeout = 300
            Return cmd.ExecuteScalar()
        End Using
    End Function

    Private Shared Function Num(v As Object) As String
        Return Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
    End Function

End Class

' ════════════════════════════════════════════════════════════════
' GENERACIÓ DEL T-SQL A PARTIR DEL CATÀLEG
' Cada funció retorna una sentència (o lot) executable directament.
' ════════════════════════════════════════════════════════════════
Public Module GeneradorCataleg

    Private Function Q(nom As String) As String
        Return TSqlExporter.Q(nom)
    End Function

    Private Function Lit(s As String) As String
        Return If(s, "").Replace("'", "''")
    End Function

    ''' <summary>Especificació de tipus: varchar(50), nvarchar(max), decimal(10,2), datetime2(7)...</summary>
    Public Function TipusSql(tipus As String, maxLength As Integer, precisio As Integer, escala As Integer) As String
        Dim t As String = tipus.ToLowerInvariant()
        Select Case t
            Case "varchar", "char", "varbinary", "binary"
                Return t & "(" & If(maxLength = -1, "max", maxLength.ToString(CultureInfo.InvariantCulture)) & ")"
            Case "nvarchar", "nchar"
                Return t & "(" & If(maxLength = -1, "max", (maxLength \ 2).ToString(CultureInfo.InvariantCulture)) & ")"
            Case "decimal", "numeric"
                Return t & "(" & precisio & "," & escala & ")"
            Case "datetime2", "time", "datetimeoffset"
                Return t & "(" & escala & ")"
            Case "float"
                Return If(precisio = 53 OrElse precisio = 0, "float", "float(" & precisio & ")")
            Case Else
                Return t
        End Select
    End Function

    Public Function CrearEsquema(nom As String) As String
        Return "IF SCHEMA_ID(N'" & Lit(nom) & "') IS NULL EXEC(N'CREATE SCHEMA " & Lit(Q(nom)) & "');"
    End Function

    Public Function CrearTipus(t As CatTipus) As String
        Return "CREATE TYPE " & Q(t.Esquema) & "." & Q(t.Nom) & " FROM " & t.TipusBase &
               If(t.Nullable, " NULL", " NOT NULL") & ";"
    End Function

    Public Function CrearSequencia(s As CatSequencia) As String
        Dim sb As New StringBuilder()
        sb.Append("CREATE SEQUENCE " & Q(s.Esquema) & "." & Q(s.Nom) & " AS " & s.Tipus)
        sb.Append(" START WITH " & s.Inici & " INCREMENT BY " & s.Increment)
        sb.Append(" MINVALUE " & s.Minim & " MAXVALUE " & s.Maxim)
        sb.Append(If(s.Cicle, " CYCLE", " NO CYCLE"))
        If s.Cache Is Nothing Then
            sb.Append(" NO CACHE")
        ElseIf s.Cache = "" Then
            sb.Append(" CACHE")
        Else
            sb.Append(" CACHE " & s.Cache)
        End If
        sb.Append(";")
        Return sb.ToString()
    End Function

    Public Function DefinicioColumna(c As CatColumna) As String
        Dim sb As New StringBuilder(Q(c.Nom))
        If c.EsCalculada Then
            sb.Append(" AS " & c.Formula)
            If c.Persistida Then sb.Append(" PERSISTED")
            Return sb.ToString()
        End If

        If c.EsquemaTipus IsNot Nothing Then
            sb.Append(" " & Q(c.EsquemaTipus) & "." & Q(c.Tipus))
        Else
            sb.Append(" " & TipusSql(c.Tipus, c.MaxLength, c.Precisio, c.Escala))
        End If
        If c.EsSparse Then sb.Append(" SPARSE")
        If c.Collation IsNot Nothing AndAlso c.EsquemaTipus Is Nothing Then sb.Append(" COLLATE " & c.Collation)
        If c.EsIdentity Then sb.Append(" IDENTITY(" & c.IdentitySeed & "," & c.IdentityIncrement & ")")
        If c.EsRowGuid Then sb.Append(" ROWGUIDCOL")
        sb.Append(If(c.Nullable, " NULL", " NOT NULL"))
        If c.DefaultDefinicio IsNot Nothing Then
            sb.Append(" CONSTRAINT " & Q(c.DefaultNom) & " DEFAULT " & c.DefaultDefinicio)
        End If
        Return sb.ToString()
    End Function

    Private Function LlistaColumnesIndex(ix As CatIndex) As String
        Dim parts As New List(Of String)()
        For Each col As CatColumnaIndex In ix.Columnes
            parts.Add(Q(col.Nom) & If(col.Descendent, " DESC", " ASC"))
        Next
        Return "(" & String.Join(", ", parts) & ")"
    End Function

    ''' <summary>CREATE TABLE amb columnes, DEFAULT, PK i UNIQUE (els CHECK, FK i
    ''' índexs es creen després de copiar les dades).</summary>
    Public Function CrearTaula(t As CatTaula) As String
        Dim linies As New List(Of String)()
        For Each c As CatColumna In t.Columnes
            linies.Add("    " & DefinicioColumna(c))
        Next
        For Each ix As CatIndex In t.Indexs
            If ix.EsPK OrElse ix.EsRestriccioUnica Then
                linies.Add("    CONSTRAINT " & Q(ix.Nom) & If(ix.EsPK, " PRIMARY KEY", " UNIQUE") &
                           If(ix.EsClustered, " CLUSTERED ", " NONCLUSTERED ") & LlistaColumnesIndex(ix))
            End If
        Next
        Return "CREATE TABLE " & t.NomComplet & " (" & Environment.NewLine &
               String.Join("," & Environment.NewLine, linies) & Environment.NewLine & ");"
    End Function

    ''' <summary>Índexs que no són PK ni UNIQUE de restricció.</summary>
    Public Function CrearIndexs(t As CatTaula) As List(Of String)
        Dim res As New List(Of String)()
        For Each ix As CatIndex In t.Indexs
            If ix.EsPK OrElse ix.EsRestriccioUnica OrElse ix.Columnes.Count = 0 Then Continue For
            Dim sb As New StringBuilder("CREATE ")
            If ix.EsUnic Then sb.Append("UNIQUE ")
            sb.Append(If(ix.EsClustered, "CLUSTERED", "NONCLUSTERED"))
            sb.Append(" INDEX " & Q(ix.Nom) & " ON " & t.NomComplet & " " & LlistaColumnesIndex(ix))
            If ix.Incloses.Count > 0 Then
                sb.Append(" INCLUDE (" & String.Join(", ", ix.Incloses.ConvertAll(Function(n) Q(n))) & ")")
            End If
            If ix.Filtre IsNot Nothing Then sb.Append(" WHERE " & ix.Filtre)
            sb.Append(";")
            res.Add(sb.ToString())
        Next
        Return res
    End Function

    Public Function CrearCheck(t As CatTaula, ck As CatCheck) As String
        Dim sql As String = "ALTER TABLE " & t.NomComplet & If(ck.NoConfiable, " WITH NOCHECK", " WITH CHECK") &
                            " ADD CONSTRAINT " & Q(ck.Nom) & " CHECK " & ck.Definicio & ";"
        If ck.Desactivat Then sql &= Environment.NewLine & "ALTER TABLE " & t.NomComplet & " NOCHECK CONSTRAINT " & Q(ck.Nom) & ";"
        Return sql
    End Function

    Public Function CrearFK(t As CatTaula, fk As CatFK) As String
        Dim sb As New StringBuilder()
        sb.Append("ALTER TABLE " & t.NomComplet & If(fk.NoConfiable, " WITH NOCHECK", " WITH CHECK"))
        sb.Append(" ADD CONSTRAINT " & Q(fk.Nom) & " FOREIGN KEY (")
        sb.Append(String.Join(", ", fk.Columnes.ConvertAll(Function(n) Q(n))))
        sb.Append(") REFERENCES " & Q(fk.EsquemaRef) & "." & Q(fk.TaulaRef) & " (")
        sb.Append(String.Join(", ", fk.ColumnesRef.ConvertAll(Function(n) Q(n))) & ")")
        sb.Append(" ON DELETE " & fk.OnDelete & " ON UPDATE " & fk.OnUpdate)
        If fk.NotForReplication Then sb.Append(" NOT FOR REPLICATION")
        sb.Append(";")
        If fk.Desactivada Then
            sb.Append(Environment.NewLine & "ALTER TABLE " & t.NomComplet & " NOCHECK CONSTRAINT " & Q(fk.Nom) & ";")
        End If
        Return sb.ToString()
    End Function

    ''' <summary>Opcions de sessió amb què es va crear el mòdul (cal un lot separat).</summary>
    Public Function OpcionsModul(m As CatModul) As String
        Return "SET QUOTED_IDENTIFIER " & If(m.QuotedIdentifier, "ON", "OFF") & "; " &
               "SET ANSI_NULLS " & If(m.AnsiNulls, "ON", "OFF") & ";"
    End Function

    Public Function DesactivarTrigger(m As CatModul) As String
        If m.EsTriggerBD Then Return "DISABLE TRIGGER " & Q(m.Nom) & " ON DATABASE;"
        Return "DISABLE TRIGGER " & Q(m.Esquema) & "." & Q(m.Nom) & " ON " & m.TaulaPare & ";"
    End Function

    Public Function CrearSinonim(s As CatSinonim) As String
        Return "CREATE SYNONYM " & Q(s.Esquema) & "." & Q(s.Nom) & " FOR " & s.Base & ";"
    End Function

    Public Function SelectCopia(t As CatTaula) As String
        Dim cols As New List(Of String)()
        For Each c As CatColumna In t.Columnes
            If Not c.EsCopiable Then Continue For
            ' Els tipus CLR es llegeixen com a binari: el client no necessita
            ' Microsoft.SqlServer.Types i el servidor de destí el reconverteix
            cols.Add(If(c.EsClr, "CAST(" & Q(c.Nom) & " AS VARBINARY(MAX)) AS " & Q(c.Nom), Q(c.Nom)))
        Next
        If cols.Count = 0 Then Return Nothing
        Return "SELECT " & String.Join(", ", cols) & " FROM " & t.NomComplet & ";"
    End Function

    ''' <summary>
    ''' Reajusta el comptador IDENTITY perquè el següent valor sigui el mateix que
    ''' tindria l'origen. Retorna Nothing si l'origen no ha generat mai cap valor.
    ''' </summary>
    Public Function ReajustarIdentity(t As CatTaula, c As CatColumna, teFiles As Boolean) As String
        If c.IdentityUltimValor Is Nothing Then Return Nothing
        Dim ultim As Decimal = Decimal.Parse(c.IdentityUltimValor, CultureInfo.InvariantCulture)
        ' Si la taula és buida, el següent valor serà exactament el del RESEED
        If Not teFiles Then ultim += Decimal.Parse(c.IdentityIncrement, CultureInfo.InvariantCulture)
        Return "DBCC CHECKIDENT (N'" & Lit(t.NomComplet) & "', RESEED, " &
               ultim.ToString(CultureInfo.InvariantCulture) & ") WITH NO_INFOMSGS;"
    End Function

End Module
