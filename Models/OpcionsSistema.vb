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
Imports System.IO
Imports Newtonsoft.Json

' ============================================================
' OpcionsSistema.vb — Configuració persistent de l'aplicació
' Es desa a opcions.json al costat de l'executable.
' ============================================================

Public Class OpcionsSistema

    ' ── Aparença ─────────────────────────────────────────────
    Public Property Tema As String = "Taronja"
    Public Property MidaFont As String = "Normal"

    ' ── Visualització ────────────────────────────────────────
    Public Property MostrarEsfera As Boolean = True
    Public Property DistribucioAuto As Boolean = True
    Public Property ModeDebug As Boolean = False

    ' ── Editor ───────────────────────────────────────────────
    Public Property MotorDefecte As String = "T-SQL"
    Public Property EsquemaDefecte As String = "dbo"
    Public Property ValidacioTempsReal As Boolean = True
    Public Property LimitUndo As Integer = 50

    ' ── Auto-desar ───────────────────────────────────────────
    Public Property AutoDesarActiu As Boolean = True
    Public Property AutoDesarMinuts As Integer = 5

    ' ── Rutes ────────────────────────────────────────────────
    Public Property RutaSortida As String = ""

    ' ── Idioma ───────────────────────────────────────────────
    Public Property Idioma As String = "CA"

    ' ── Instància global (carregada en el primer ús) ─────────
    Private Shared _actual As OpcionsSistema
    <JsonIgnore>
    Public Shared ReadOnly Property Actual As OpcionsSistema
        Get
            If _actual Is Nothing Then _actual = Carregar()
            Return _actual
        End Get
    End Property

    ' ────────────────────────────────────────────────────────
    ' Ruta del fitxer opcions.json
    ' Es desa a %AppData%\DBCoreHolographic (sempre escrivible, també
    ' quan l'aplicació s'instal·la a Program Files).
    ' ────────────────────────────────────────────────────────
    Public Shared ReadOnly Property RutaFitxer As String
        Get
            Return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DBCoreHolographic", "opcions.json")
        End Get
    End Property

    ' Ubicació antiga (al costat de l'exe) — només per migrar-ne les opcions
    Private Shared ReadOnly Property RutaAntiga As String
        Get
            Return Path.Combine(System.AppContext.BaseDirectory, "opcions.json")
        End Get
    End Property

    ' ── Carregar des de disc (si no existeix, retorna defaults) ─
    Public Shared Function Carregar() As OpcionsSistema
        For Each ruta As String In {RutaFitxer, RutaAntiga}
            Try
                If File.Exists(ruta) Then
                    Dim json As String = File.ReadAllText(ruta, System.Text.Encoding.UTF8)
                    Dim obj As OpcionsSistema = JsonConvert.DeserializeObject(Of OpcionsSistema)(json)
                    If obj IsNot Nothing Then Return obj
                End If
            Catch
                ' Si el fitxer és corrupte, ignorem i provem el següent / defaults
            End Try
        Next
        Return New OpcionsSistema()
    End Function

    ' ── Desar a disc ─────────────────────────────────────────
    Public Sub Desar()
        Try
            Dim json As String = JsonConvert.SerializeObject(Me, Formatting.Indented)
            ProjectSerializer.EscriureAtomic(RutaFitxer, json)
        Catch
            ' Silent fail — no podem bloquejar l'app per un error d'opcions
        End Try
    End Sub

End Class
