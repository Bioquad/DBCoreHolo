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

Imports System.Drawing
Imports System.Collections.Generic
Imports Newtonsoft.Json
Imports System.IO
Imports System.Text

' ============================================================
' ProjectSerializer.vb  —  Holographic DB
' Gestiona: Desar/Carregar .hdb  +  Fitxers recents (JSON a AppData)
' ============================================================
Public Module ProjectSerializer

    Private ReadOnly _cfg As New JsonSerializerSettings()
    Private Const MAX_RECENTS As Integer = 8
    Private ReadOnly _recentsPath As String =
        IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DBCoreHolographic", "recents.json")

    Sub New()
        _cfg.Formatting = Formatting.Indented
        _cfg.NullValueHandling = NullValueHandling.Ignore
        _cfg.DefaultValueHandling = DefaultValueHandling.Include
    End Sub

    ' ── DESAR ────────────────────────────────────────────────────────────
    Public Sub Desar(p As ProyectoBBDD, ruta As String)
        p.DataModificacio = DateTime.Now
        p.RecalcularIds()
        Dim json As String = JsonConvert.SerializeObject(p, _cfg)
        EscriureAtomic(ruta, json)
        AfegirARecents(ruta)
    End Sub

    ''' <summary>
    ''' Escriu el contingut en un fitxer temporal al mateix directori i després
    ''' el reemplaça d'un sol cop. Si l'aplicació es tanca a mitja escriptura,
    ''' el fitxer original queda intacte.
    ''' </summary>
    Public Sub EscriureAtomic(ruta As String, contingut As String)
        Dim dir As String = Path.GetDirectoryName(Path.GetFullPath(ruta))
        If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
        Dim tmp As String = ruta & ".tmp"
        File.WriteAllText(tmp, contingut, Encoding.UTF8)
        If File.Exists(ruta) Then
            File.Replace(tmp, ruta, Nothing)
        Else
            File.Move(tmp, ruta)
        End If
    End Sub

    ' ── CARREGAR ─────────────────────────────────────────────────────────
    Public Function Carregar(ruta As String) As ProyectoBBDD
        If Not File.Exists(ruta) Then
            Throw New FileNotFoundException(Locale.Str("SER_NO_FITXER") & ruta)
        End If
        Dim json As String = File.ReadAllText(ruta, Encoding.UTF8)
        Dim p As ProyectoBBDD = JsonConvert.DeserializeObject(Of ProyectoBBDD)(json, _cfg)
        If p Is Nothing Then
            Throw New InvalidDataException(Locale.Str("SER_FORMAT_INVALID"))
        End If
        ' Inicialitzar llistes null (compatibilitat) ABANS de recórrer-les
        If p.Taules Is Nothing Then p.Taules = New List(Of TablaBBDD)()
        If p.Relacions Is Nothing Then p.Relacions = New List(Of RelacionBBDD)()
        p.Taules.RemoveAll(Function(t) t Is Nothing)
        p.Relacions.RemoveAll(Function(r) r Is Nothing)
        For Each t As TablaBBDD In p.Taules
            If t.Fields Is Nothing Then t.Fields = New List(Of CampoBBDD)()
            t.Fields.RemoveAll(Function(f) f Is Nothing)
        Next
        ' Recalcular IDs — evita col·lisions si el fitxer és d'una versió anterior
        p.RecalcularIds()
        AfegirARecents(ruta)
        Return p
    End Function

    ' ── FITXERS RECENTS ──────────────────────────────────────────────────
    Public Sub AfegirARecents(ruta As String)
        Try
            Dim recents As List(Of String) = ObtenirRecents()
            recents.RemoveAll(Function(r) r.Equals(ruta, StringComparison.OrdinalIgnoreCase))
            recents.Insert(0, ruta)
            If recents.Count > MAX_RECENTS Then recents = recents.GetRange(0, MAX_RECENTS)
            IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(_recentsPath))
            EscriureAtomic(_recentsPath, JsonConvert.SerializeObject(recents, Formatting.Indented))
        Catch
        End Try
    End Sub

    Public Function ObtenirRecents() As List(Of String)
        Try
            If Not File.Exists(_recentsPath) Then Return New List(Of String)()
            Dim json As String = File.ReadAllText(_recentsPath, Encoding.UTF8)
            Dim tots As List(Of String) = JsonConvert.DeserializeObject(Of List(Of String))(json)
            If tots Is Nothing Then Return New List(Of String)()
            ' Filtrar els que ja no existeixen al disc
            Return tots.FindAll(Function(r) File.Exists(r))
        Catch
            Return New List(Of String)()
        End Try
    End Function

    Public Sub EsborrarRecents()
        Try
            If File.Exists(_recentsPath) Then File.Delete(_recentsPath)
        Catch
        End Try
    End Sub

    Public Function EsVdbValid(ruta As String) As Boolean
        Try
            If Not File.Exists(ruta) Then Return False
            Dim json As String = File.ReadAllText(ruta, Encoding.UTF8)
            Return json.Contains("""Taules""") OrElse json.Contains("""Nombre""")
        Catch
            Return False
        End Try
    End Function

End Module
