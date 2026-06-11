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
Imports System.Windows.Forms

' ============================================================
' AppStyle.vb — Estil centralitzat DB-Core Holographic
' v2: fonts mutables + paletes de tema + AplicarTema()
' ============================================================

Public Module AppStyle

    ' ── Estat intern del tema actiu ──────────────────────────────────────
    Private _basePt   As Single  = 8.5F   ' mida base (Normal)
    Private _temaActiu As String = "Taronja"

    ' ── Paleta de colors (recalculada per AplicarTema) ───────────────────
    Public ColFons       As Color = Color.FromArgb(5, 1, 0)
    Public ColFonsMig    As Color = Color.FromArgb(14, 4, 0)
    Public ColFonsCapc   As Color = Color.FromArgb(20, 6, 0)
    Public ColFonsGrid   As Color = Color.FromArgb(10, 3, 0)
    Public ColFonsInput  As Color = Color.FromArgb(18, 5, 0)
    Public ColVora       As Color = Color.FromArgb(255, 96, 16)
    Public ColVoraFeble  As Color = Color.FromArgb(80, 255, 96, 16)
    Public ColVoraMolt   As Color = Color.FromArgb(30, 255, 96, 16)
    Public ColTextPrinc  As Color = Color.FromArgb(232, 224, 208)
    Public ColTextSec    As Color = Color.FromArgb(200, 180, 140)
    Public ColTextFeble  As Color = Color.FromArgb(140, 110, 70)
    Public ColAccent     As Color = Color.FromArgb(255, 96, 16)
    Public ColAccentSec  As Color = Color.FromArgb(255, 160, 64)
    Public ColPK         As Color = Color.FromArgb(255, 200, 60)
    Public ColFK         As Color = Color.FromArgb(100, 136, 210)
    Public ColType       As Color = Color.FromArgb(255, 128, 48)
    Public ColPerill     As Color = Color.FromArgb(255, 80, 48)
    Public ColHover      As Color = Color.FromArgb(42, 12, 0)
    Public ColStatusBar  As Color = Color.FromArgb(0, 0, 0)

    ' ── Fonts mutables (es recrean quan canvia la mida) ──────────────────
    Public FntNormal    As Font = New Font("Courier New", 8.5F)
    Public FntPetit     As Font = New Font("Courier New", 7.5F)
    Public FntMoltPetit As Font = New Font("Courier New", 7.0F)
    Public FntTitol     As Font = New Font("Courier New", 9.0F, FontStyle.Bold)
    Public FntCapc      As Font = New Font("Courier New", 8.0F, FontStyle.Bold)
    Public FntMenu      As Font = New Font("Courier New", 8.5F)
    Public FntGran      As Font = New Font("Courier New", 11.0F, FontStyle.Bold)

    ' ── Mides estàndard ──────────────────────────────────────────────────
    Public Const AltBotoNormal  As Integer = 26
    Public Const AltBotoPetit   As Integer = 22
    Public Const AltCapcalera   As Integer = 24
    Public Const AltColCapc     As Integer = 18
    Public Const AltFilaCamp    As Integer = 18
    Public Const AltStatusBar   As Integer = 16
    Public Const AltPanellInf   As Integer = 220
    Public Const AmpBotoNormal  As Integer = 155
    Public Const AmpBotoPetit   As Integer = 76

    ' ════════════════════════════════════════════════════════════════════
    '  APLICAR TEMA COMPLET (crida principal des de FrmOpcions / inici)
    '  Recalcula colors + fonts i propaga a tots els formularis oberts.
    ' ════════════════════════════════════════════════════════════════════
    Public Sub AplicarTema(tema As String, midaFont As String)
        _temaActiu = tema
        _basePt    = MidaFontPt(midaFont)

        RecalcularColors(tema)
        RecalcularFonts(_basePt)

        ' Copiar la llista primer — Application.OpenForms no es pot
        ' iterar de forma segura mentre els formularis estan actius.
        Dim formes As New List(Of Form)
        For Each f As Form In Application.OpenForms
            formes.Add(f)
        Next

        For Each f As Form In formes
            f.SuspendLayout()
            AplicarEstilRecursiu(f)
            f.ResumeLayout(False)
            f.Refresh()   ' síncron: força repintat immediat de tot el formulari
        Next
    End Sub

    ' ── Recalcular paleta segons tema ────────────────────────────────────
    Private Sub RecalcularColors(tema As String)
        Select Case tema
            Case "Verd"   ' Matrix
                ColFons      = Color.FromArgb(0, 5, 0)
                ColFonsMig   = Color.FromArgb(0, 14, 0)
                ColFonsCapc  = Color.FromArgb(0, 20, 0)
                ColFonsGrid  = Color.FromArgb(0, 10, 0)
                ColFonsInput = Color.FromArgb(0, 18, 0)
                ColVora      = Color.FromArgb(0, 200, 60)
                ColVoraFeble = Color.FromArgb(80, 0, 200, 60)
                ColVoraMolt  = Color.FromArgb(30, 0, 200, 60)
                ColTextPrinc = Color.FromArgb(180, 255, 180)
                ColTextSec   = Color.FromArgb(140, 210, 140)
                ColTextFeble = Color.FromArgb(80, 140, 80)
                ColAccent    = Color.FromArgb(0, 220, 60)
                ColAccentSec = Color.FromArgb(80, 255, 100)
                ColPK        = Color.FromArgb(160, 255, 100)
                ColFK        = Color.FromArgb(80, 180, 255)
                ColType      = Color.FromArgb(0, 200, 80)
                ColPerill    = Color.FromArgb(255, 80, 48)
                ColHover     = Color.FromArgb(0, 42, 0)
                ColStatusBar = Color.FromArgb(0, 0, 0)

            Case "Cian"   ' Tron
                ColFons      = Color.FromArgb(0, 4, 8)
                ColFonsMig   = Color.FromArgb(0, 12, 20)
                ColFonsCapc  = Color.FromArgb(0, 18, 28)
                ColFonsGrid  = Color.FromArgb(0, 8, 14)
                ColFonsInput = Color.FromArgb(0, 14, 22)
                ColVora      = Color.FromArgb(0, 180, 220)
                ColVoraFeble = Color.FromArgb(80, 0, 180, 220)
                ColVoraMolt  = Color.FromArgb(30, 0, 180, 220)
                ColTextPrinc = Color.FromArgb(200, 240, 255)
                ColTextSec   = Color.FromArgb(160, 210, 230)
                ColTextFeble = Color.FromArgb(90, 140, 160)
                ColAccent    = Color.FromArgb(0, 200, 240)
                ColAccentSec = Color.FromArgb(80, 220, 255)
                ColPK        = Color.FromArgb(140, 240, 255)
                ColFK        = Color.FromArgb(100, 136, 210)
                ColType      = Color.FromArgb(0, 180, 220)
                ColPerill    = Color.FromArgb(255, 80, 48)
                ColHover     = Color.FromArgb(0, 22, 40)
                ColStatusBar = Color.FromArgb(0, 0, 0)

            Case "Blanc"  ' High Contrast
                ColFons      = Color.FromArgb(12, 12, 12)
                ColFonsMig   = Color.FromArgb(28, 28, 28)
                ColFonsCapc  = Color.FromArgb(40, 40, 40)
                ColFonsGrid  = Color.FromArgb(18, 18, 18)
                ColFonsInput = Color.FromArgb(32, 32, 32)
                ColVora      = Color.FromArgb(200, 200, 200)
                ColVoraFeble = Color.FromArgb(80, 200, 200, 200)
                ColVoraMolt  = Color.FromArgb(30, 200, 200, 200)
                ColTextPrinc = Color.FromArgb(240, 240, 240)
                ColTextSec   = Color.FromArgb(200, 200, 200)
                ColTextFeble = Color.FromArgb(140, 140, 140)
                ColAccent    = Color.FromArgb(220, 220, 220)
                ColAccentSec = Color.FromArgb(255, 255, 255)
                ColPK        = Color.FromArgb(255, 220, 80)
                ColFK        = Color.FromArgb(120, 160, 255)
                ColType      = Color.FromArgb(200, 200, 200)
                ColPerill    = Color.FromArgb(255, 80, 48)
                ColHover     = Color.FromArgb(50, 50, 50)
                ColStatusBar = Color.FromArgb(0, 0, 0)

            Case Else     ' Taronja (default)
                ColFons      = Color.FromArgb(5, 1, 0)
                ColFonsMig   = Color.FromArgb(14, 4, 0)
                ColFonsCapc  = Color.FromArgb(20, 6, 0)
                ColFonsGrid  = Color.FromArgb(10, 3, 0)
                ColFonsInput = Color.FromArgb(18, 5, 0)
                ColVora      = Color.FromArgb(255, 96, 16)
                ColVoraFeble = Color.FromArgb(80, 255, 96, 16)
                ColVoraMolt  = Color.FromArgb(30, 255, 96, 16)
                ColTextPrinc = Color.FromArgb(232, 224, 208)
                ColTextSec   = Color.FromArgb(200, 180, 140)
                ColTextFeble = Color.FromArgb(140, 110, 70)
                ColAccent    = Color.FromArgb(255, 96, 16)
                ColAccentSec = Color.FromArgb(255, 160, 64)
                ColPK        = Color.FromArgb(255, 200, 60)
                ColFK        = Color.FromArgb(100, 136, 210)
                ColType      = Color.FromArgb(255, 128, 48)
                ColPerill    = Color.FromArgb(255, 80, 48)
                ColHover     = Color.FromArgb(42, 12, 0)
                ColStatusBar = Color.FromArgb(0, 0, 0)
        End Select
    End Sub

    ' ── Recalcular fonts segons mida base ────────────────────────────────
    Private Sub RecalcularFonts(base_ As Single)
        ' Disposar les anteriors per evitar memory leaks
        DisposeFont(FntNormal)
        DisposeFont(FntPetit)
        DisposeFont(FntMoltPetit)
        DisposeFont(FntTitol)
        DisposeFont(FntCapc)
        DisposeFont(FntMenu)
        DisposeFont(FntGran)

        ' Delta entre mides: Normal=base, Petit=base-1, MoltPetit=base-1.5, etc.
        FntNormal    = New Font("Courier New", base_)
        FntPetit     = New Font("Courier New", base_ - 1.0F)
        FntMoltPetit = New Font("Courier New", base_ - 1.5F)
        FntTitol     = New Font("Courier New", base_ + 0.5F, FontStyle.Bold)
        FntCapc      = New Font("Courier New", base_ - 0.5F, FontStyle.Bold)
        FntMenu      = New Font("Courier New", base_)
        FntGran      = New Font("Courier New", base_ + 2.5F, FontStyle.Bold)
    End Sub

    Private Sub DisposeFont(f As Font)
        Try
            If f IsNot Nothing Then f.Dispose()
        Catch
        End Try
    End Sub

    Private Function MidaFontPt(midaFont As String) As Single
        Select Case midaFont
            Case "Petita" : Return 7.5F
            Case "Gran"   : Return 9.5F
            Case Else     : Return 8.5F  ' Normal
        End Select
    End Function

    ' ════════════════════════════════════════════════════════════════════
    '  APLICAR ESTIL RECURSIVAMENT
    '  Propaga colors i font actuals a tots els controls d'un contenidor.
    ' ════════════════════════════════════════════════════════════════════
    Public Sub AplicarEstilRecursiu(ctrl As Control)
        ' Controls amb Tag NOESTIL gestionen els seus colors manualment
        Dim ctrlTag As String = TryCast(ctrl.Tag, String)
        If ctrlTag IsNot Nothing AndAlso ctrlTag.Contains("NOESTIL") Then Return
        ' SKControl (canvas OpenGL/Skia) — no tocar, té el seu propi repintat
        If ctrl.GetType().Name = "SKControl" Then Return

        Select Case True
            Case TypeOf ctrl Is MenuStrip
                Dim ms As MenuStrip = DirectCast(ctrl, MenuStrip)
                ms.BackColor = ColFonsMig
                ms.ForeColor = ColAccent
                ms.Font      = FntMenu
                AplicarEstilToolStripItems(ms.Items)

            Case TypeOf ctrl Is ToolStrip
                ctrl.BackColor = ColFonsMig
                ctrl.ForeColor = ColAccent
                ctrl.Font      = FntMenu

            Case TypeOf ctrl Is TextBox
                ctrl.BackColor = ColFonsInput
                ctrl.ForeColor = ColTextPrinc
                ctrl.Font      = FntNormal

            Case TypeOf ctrl Is RichTextBox
                ctrl.BackColor = ColFonsInput
                ctrl.ForeColor = ColAccentSec
                ctrl.Font      = FntNormal

            Case TypeOf ctrl Is ComboBox
                ctrl.BackColor = ColFonsInput
                ctrl.ForeColor = ColTextPrinc
                ctrl.Font      = FntNormal

            Case TypeOf ctrl Is Button
                Dim btn As Button = DirectCast(ctrl, Button)
                btn.BackColor = ColFonsMig
                btn.ForeColor = ColTextPrinc
                btn.Font      = FntNormal
                btn.FlatAppearance.BorderColor        = ColVoraFeble
                btn.FlatAppearance.MouseOverBackColor = ColHover

            Case TypeOf ctrl Is CheckBox
                ctrl.BackColor = Color.Transparent
                ctrl.ForeColor = ColTextSec
                ctrl.Font      = FntNormal

            Case TypeOf ctrl Is TabPage
                ctrl.BackColor = ColFonsMig
                ctrl.ForeColor = ColAccent
                ctrl.Font      = FntPetit

            Case TypeOf ctrl Is TabControl
                ctrl.BackColor = ColFons
                ctrl.ForeColor = ColAccent
                ctrl.Font      = FntPetit

            Case TypeOf ctrl Is ListView
                ctrl.BackColor = ColFonsGrid
                ctrl.ForeColor = ColTextPrinc
                ctrl.Font      = FntPetit

            Case TypeOf ctrl Is Panel, TypeOf ctrl Is FlowLayoutPanel
                ctrl.BackColor = ColFons
                ctrl.ForeColor = ColTextPrinc
                ctrl.Font      = FntNormal

            Case TypeOf ctrl Is Label
                ' Mantenir BackColor transparent si ja ho era
                If ctrl.BackColor = Color.Transparent Then
                    ctrl.BackColor = Color.Transparent
                Else
                    ctrl.BackColor = ColFons
                End If
                ctrl.ForeColor = ColTextPrinc
                ctrl.Font      = FntNormal

            Case Else
                ctrl.BackColor = ColFons
                ctrl.ForeColor = ColTextPrinc
                ctrl.Font      = FntNormal
        End Select

        ' Recursió als fills
        For Each child As Control In ctrl.Controls
            AplicarEstilRecursiu(child)
        Next
    End Sub

    ' Propaga colors als ToolStripItem (MenuStrip items) — no són Controls
    Private Sub AplicarEstilToolStripItems(items As ToolStripItemCollection)
        For Each item As ToolStripItem In items
            item.BackColor = ColFonsMig
            item.ForeColor = ColAccent
            item.Font      = FntMenu
            Dim tmi As ToolStripMenuItem = TryCast(item, ToolStripMenuItem)
            If tmi IsNot Nothing AndAlso tmi.DropDownItems.Count > 0 Then
                AplicarEstilToolStripItems(tmi.DropDownItems)
            End If
        Next
    End Sub

    ' ── Aplicar estil a TabControl (OwnerDrawFixed) ───────────────────
    Public Sub AplicarEstilTabControl(tc As TabControl)
        tc.DrawMode = TabDrawMode.OwnerDrawFixed
        tc.BackColor = ColFons
        tc.ForeColor = ColAccent
        AddHandler tc.DrawItem, Sub(s As Object, ev As DrawItemEventArgs)
            Dim g As Drawing.Graphics = ev.Graphics
            Dim tab As TabPage = tc.TabPages(ev.Index)
            Dim sel As Boolean = (ev.Index = tc.SelectedIndex)
            Dim bg As Color = If(sel, ColFonsCapc, Color.FromArgb(10, 2, 0))
            Dim fg As Color = If(sel, ColAccent, ColTextFeble)
            Using br As New Drawing.SolidBrush(bg)
                g.FillRectangle(br, ev.Bounds)
            End Using
            Using br As New Drawing.SolidBrush(fg)
                g.DrawString(tab.Text, FntMoltPetit, br,
                             ev.Bounds.Left + 4, ev.Bounds.Top + 3)
            End Using
        End Sub
    End Sub

    ' ── Dibuixar línia separadora ─────────────────────────────────────
    Public Sub DibuixarLiniaSep(g As Drawing.Graphics, y As Integer, w As Integer)
        Using pen As New Drawing.Pen(Color.FromArgb(40, ColAccent.R, ColAccent.G, ColAccent.B), 1)
            g.DrawLine(pen, 0, y, w, y)
        End Using
    End Sub

    ' ── Dibuixar vora de formulari ────────────────────────────────────
    Public Sub DibuixarVoraForm(g As Drawing.Graphics, w As Integer, h As Integer)
        Using pen As New Drawing.Pen(Color.FromArgb(ColVora.R, ColVora.G, ColVora.B), 1.5F)
            g.DrawRectangle(pen, 1, 1, w - 3, h - 3)
        End Using
    End Sub

    ' ── Aplicar estil a formulari (compat. amb crides antigues) ──────
    Public Sub AplicarEstilForm(f As Form)
        f.BackColor = ColFons
        f.ForeColor = ColTextPrinc
        f.Font      = FntNormal
    End Sub

    ' ── Factories (usen sempre les variables mutables actuals) ────────

    Public Function CrearBotoAccio(text As String) As Button
        Dim b As New Button()
        b.Text = text
        b.Font = FntNormal
        b.ForeColor = ColTextPrinc
        b.BackColor = ColFonsMig
        b.FlatStyle = FlatStyle.Flat
        b.FlatAppearance.BorderColor = ColVoraFeble
        b.FlatAppearance.MouseOverBackColor = ColHover
        b.FlatAppearance.BorderSize = 1
        b.Size = New Size(AmpBotoNormal, AltBotoNormal)
        b.Cursor = Cursors.Hand
        b.TextAlign = ContentAlignment.MiddleCenter
        Return b
    End Function

    Public Function CrearBotoCapc(text As String) As Button
        Dim b As New Button()
        b.Text = text
        b.Font = FntMoltPetit
        b.ForeColor = ColTextSec
        b.BackColor = ColFonsMig
        b.FlatStyle = FlatStyle.Flat
        b.FlatAppearance.BorderColor = ColVoraMolt
        b.FlatAppearance.MouseOverBackColor = ColHover
        b.FlatAppearance.BorderSize = 1
        b.Size = New Size(AmpBotoPetit, AltBotoPetit)
        b.Cursor = Cursors.Hand
        b.TextAlign = ContentAlignment.MiddleCenter
        Return b
    End Function

    Public Function CrearBotoPerill(text As String) As Button
        Dim b As Button = CrearBotoCapc(text)
        b.ForeColor = ColPerill
        b.FlatAppearance.BorderColor = Color.FromArgb(60, ColPerill.R, ColPerill.G, ColPerill.B)
        Return b
    End Function

    Public Function CrearLabelTitol(text As String) As Label
        Dim l As New Label()
        l.Text = text
        l.Font = FntCapc
        l.ForeColor = ColPK
        l.BackColor = ColFonsCapc
        l.TextAlign = ContentAlignment.MiddleLeft
        l.Padding = New Padding(6, 0, 0, 0)
        l.Height = AltCapcalera
        Return l
    End Function

    Public Function CrearLabelCol(text As String) As Label
        Dim l As New Label()
        l.Text = text
        l.Font = FntMoltPetit
        l.ForeColor = Color.FromArgb(180, ColAccent.R, ColAccent.G, ColAccent.B)
        l.BackColor = Color.FromArgb(8, 2, 0)
        l.TextAlign = ContentAlignment.MiddleLeft
        l.Padding = New Padding(3, 0, 0, 0)
        Return l
    End Function

    Public Function CrearPanelCapc() As Panel
        Dim p As New Panel()
        p.BackColor = ColFonsCapc
        p.Height = AltCapcalera
        p.Dock = DockStyle.Top
        Return p
    End Function

    Public Function CrearPanelColCapc() As Panel
        Dim p As New Panel()
        p.BackColor = Color.FromArgb(8, 2, 0)
        p.Height = AltColCapc
        p.Dock = DockStyle.Top
        Return p
    End Function

    Public Function CrearListView() As ListView
        Dim lv As New ListView()
        lv.View = View.Details
        lv.FullRowSelect = True
        lv.GridLines = False
        lv.BackColor = ColFonsGrid
        lv.ForeColor = ColTextPrinc
        lv.Font = FntPetit
        lv.BorderStyle = BorderStyle.None
        lv.HeaderStyle = ColumnHeaderStyle.Nonclickable
        lv.MultiSelect = False
        lv.OwnerDraw = True
        Return lv
    End Function

    Public Function CrearTextBox() As TextBox
        Dim t As New TextBox()
        t.Font = FntNormal
        t.BackColor = ColFonsInput
        t.ForeColor = ColTextPrinc
        t.BorderStyle = BorderStyle.FixedSingle
        Return t
    End Function

    Public Function CrearComboBox() As ComboBox
        Dim c As New ComboBox()
        c.Font = FntNormal
        c.BackColor = ColFonsInput
        c.ForeColor = ColTextPrinc
        c.FlatStyle = FlatStyle.Flat
        c.DropDownStyle = ComboBoxStyle.DropDownList
        Return c
    End Function

    Public Function CrearCheckBox(text As String) As CheckBox
        Dim c As New CheckBox()
        c.Text = text
        c.Font = FntNormal
        c.ForeColor = ColTextSec
        c.BackColor = Color.Transparent
        Return c
    End Function

End Module
