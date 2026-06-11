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
Imports System.Windows.Forms
Imports System.Drawing

' ============================================================
' FrmOpcions.vb — Configuració de l'aplicació DB-Core
' Hereda HoloForm (caption hologràfica, vora taronja, Escape, X)
' Seccions: APARENÇA · IDIOMA
' ============================================================

Public Class FrmOpcions
    Inherits HoloForm

    Private _cmbTema       As ComboBox
    Private _cmbMidaFont   As ComboBox
    Private _chkEsfera     As CheckBox
    Private _chkDebug      As CheckBox
    Private _cmbIdioma     As ComboBox
    Private _lblErr        As Label
    Private _btnAplicar    As Button

    ' Opcions globals accessibles des de qualsevol part de l'app
    Public Shared Opcions As OpcionsSistema = OpcionsSistema.Carregar()

    Private Const FW  As Integer = 480
    Private Const COL As Integer = 180
    Private Const CTL As Integer = 220
    Private Const MX  As Integer = 14

    Public Sub New()
        Me.Text       = Locale.Str("OPC_TITOL")
        Me.ClientSize = New Size(FW, 385)
        ConstruirUI()
        CarregarValors()
    End Sub

    ' ────────────────────────────────────────────────────────
    Private Sub ConstruirUI()

        ' ── Panel botons (ancla a baix) ──────────────────────────────
        Dim pnlBtns As New Panel()
        pnlBtns.BackColor = Color.FromArgb(8, 2, 0)
        pnlBtns.Dock      = DockStyle.Bottom
        pnlBtns.Height    = 46
        AddHandler pnlBtns.Paint, Sub(s2 As Object, ev As PaintEventArgs)
            Using pen As New Pen(Color.FromArgb(40, 255, 96, 16), 1)
                ev.Graphics.DrawLine(pen, 0, 0, pnlBtns.Width, 0)
            End Using
        End Sub

        Dim btnOK As Button = AppStyle.CrearBotoAccio(Locale.Str("OPC_BTN_OK"))
        btnOK.Location = New Point(8, 9)
        btnOK.Width    = 110
        AddHandler btnOK.Click, AddressOf BtnOK_Click
        pnlBtns.Controls.Add(btnOK)

        _btnAplicar = AppStyle.CrearBotoAccio(Locale.Str("OPC_BTN_APLICAR"))
        _btnAplicar.Location  = New Point(126, 9)
        _btnAplicar.Width     = 100
        _btnAplicar.ForeColor = AppStyle.ColAccentSec
        AddHandler _btnAplicar.Click, AddressOf BtnAplicar_Click
        pnlBtns.Controls.Add(_btnAplicar)

        Dim btnCancel As Button = AppStyle.CrearBotoAccio(Locale.Str("OPC_BTN_CANCEL"))
        btnCancel.Location     = New Point(234, 9)
        btnCancel.Width        = 110
        btnCancel.ForeColor    = AppStyle.ColPerill
        btnCancel.DialogResult = DialogResult.Cancel
        AddHandler btnCancel.Click, Sub(s As Object, e As EventArgs)
            Me.DialogResult = DialogResult.Cancel
            Me.Close()
        End Sub
        pnlBtns.Controls.Add(btnCancel)
        Me.CancelButton = btnCancel
        Me.Controls.Add(pnlBtns)

        ' ── Label error ──────────────────────────────────────────────
        _lblErr = New Label()
        _lblErr.Font      = AppStyle.FntMoltPetit
        _lblErr.ForeColor = AppStyle.ColPerill
        _lblErr.BackColor = Color.FromArgb(20, 255, 0, 0)
        _lblErr.Dock      = DockStyle.Bottom
        _lblErr.Height    = 18
        _lblErr.TextAlign = ContentAlignment.MiddleLeft
        _lblErr.Padding   = New Padding(8, 0, 0, 0)
        _lblErr.Visible   = False
        Me.Controls.Add(_lblErr)

        ' ── Panel de contingut (Fill) ────────────────────────────────
        Dim pnlCos As New Panel()
        pnlCos.Dock      = DockStyle.Fill
        pnlCos.BackColor = Color.Transparent
        Me.Controls.Add(pnlCos)

        Dim y As Integer = 10

        ' ═══════════════════════════════════════════
        '  SECCIÓ: APARENÇA
        ' ═══════════════════════════════════════════
        SEC(pnlCos, Locale.Str("OPC_APARENCA"), y) : y += 22

        FL(pnlCos, Locale.Str("OPC_TEMA"), MX, y + 4)
        _cmbTema = FC(pnlCos, MX + COL, y, CTL)
        _cmbTema.Items.AddRange(New Object() {
            Locale.Str("OPC_TEMA_TAR"),
            Locale.Str("OPC_TEMA_VER"),
            Locale.Str("OPC_TEMA_CIA"),
            Locale.Str("OPC_TEMA_BLA")
        })
        y += 28

        FL(pnlCos, Locale.Str("OPC_FONT"), MX, y + 4)
        _cmbMidaFont = FC(pnlCos, MX + COL, y, CTL)
        _cmbMidaFont.Items.AddRange(New Object() {
            Locale.Str("OPC_FONT_P"),
            Locale.Str("OPC_FONT_N"),
            Locale.Str("OPC_FONT_G")
        })
        y += 28

        LIN(pnlCos, y) : y += 10

        _chkEsfera = FK(pnlCos, Locale.Str("OPC_ESFERA_CHK"), MX + COL, y)
        FL(pnlCos, Locale.Str("OPC_ESFERA"), MX, y + 2)
        y += 26

        _chkDebug = FK(pnlCos, Locale.Str("OPC_DEBUG_CHK"), MX + COL, y)
        FL(pnlCos, Locale.Str("OPC_DEBUG"), MX, y + 2)
        y += 26

        y += 8
        LIN(pnlCos, y) : y += 8

        ' ═══════════════════════════════════════════
        '  SECCIÓ: IDIOMA
        ' ═══════════════════════════════════════════
        SEC(pnlCos, Locale.Str("OPC_IDIOMA"), y) : y += 22

        FL(pnlCos, Locale.Str("OPC_IDIOMA_LBL"), MX, y + 4)
        _cmbIdioma = FC(pnlCos, MX + COL, y, CTL)
        _cmbIdioma.Items.AddRange(New Object() {
            Locale.Str("OPC_IDIOMA_EN"),
            Locale.Str("OPC_IDIOMA_CA"),
            Locale.Str("OPC_IDIOMA_ES")
        })
        y += 28

        ' Nota informativa sota el combo
        Dim lblNota As New Label()
        lblNota.Text      = Locale.Str("OPC_IDIOMA_NOTA")
        lblNota.Font      = New Font("Courier New", 6.8F, FontStyle.Italic)
        lblNota.ForeColor = Color.FromArgb(70, AppStyle.ColAccent.R,
                                               AppStyle.ColAccent.G,
                                               AppStyle.ColAccent.B)
        lblNota.BackColor = Color.Transparent
        lblNota.Location  = New Point(MX + COL, y)
        lblNota.Size      = New Size(CTL + 20, 28)
        pnlCos.Controls.Add(lblNota)
        y += 30

    End Sub

    ' ────────────────────────────────────────────────────────
    Private Sub CarregarValors()
        SelCombo(_cmbTema,
                 New String() {"Taronja", "Verd", "Cian", "Blanc"},
                 FrmOpcions.Opcions.Tema)
        SelCombo(_cmbMidaFont,
                 New String() {"Petita", "Normal", "Gran"},
                 FrmOpcions.Opcions.MidaFont)
        _chkEsfera.Checked = FrmOpcions.Opcions.MostrarEsfera
        _chkDebug.Checked  = FrmOpcions.Opcions.ModeDebug
        SelCombo(_cmbIdioma,
                 New String() {"EN", "CA", "ES"},
                 FrmOpcions.Opcions.Idioma)
        ' Desactivar les opcions no implementades
        _cmbIdioma.Enabled = True
    End Sub

    ' Llegeix els controls i aplica les opcions en calent (sense tancar).
    ' Retorna True si alguna opció ha canviat respecte l'estat anterior.
    Private Function AplicarCanvis() As Boolean
        Dim op As OpcionsSistema = FrmOpcions.Opcions

        Dim temaNou    As String  = LlegCombo(_cmbTema,
                                              New String() {"Taronja", "Verd", "Cian", "Blanc"},
                                              "Taronja")
        Dim fontNova   As String  = LlegCombo(_cmbMidaFont,
                                              New String() {"Petita", "Normal", "Gran"},
                                              "Normal")
        Dim esferaNova As Boolean = _chkEsfera.Checked
        Dim debugNou   As Boolean = _chkDebug.Checked
        Dim idiomaNou  As String  = LlegCombo(_cmbIdioma,
                                              New String() {"EN", "CA", "ES"},
                                              "CA")

        _lblErr.Visible = False

        Dim haCanviat As Boolean = (temaNou    <> op.Tema)           OrElse
                                   (fontNova   <> op.MidaFont)       OrElse
                                   (esferaNova <> op.MostrarEsfera)  OrElse
                                   (debugNou   <> op.ModeDebug)      OrElse
                                   (idiomaNou  <> op.Idioma)

        op.Tema          = temaNou
        op.MidaFont      = fontNova
        op.MostrarEsfera = esferaNova
        op.ModeDebug     = debugNou
        op.Idioma        = idiomaNou

        AppStyle.AplicarTema(op.Tema, op.MidaFont)

        ' Aplicar idioma + opcions a TOTS els formularis oberts
        Dim formes As New List(Of Form)
        For Each f As Form In Application.OpenForms
            formes.Add(f)
        Next
        For Each f As Form In formes
            Dim des As FrmDesigner = TryCast(f, FrmDesigner)
            If des IsNot Nothing Then
                des.AplicarOpcions()
                des.AplicarIdioma()
                Continue For
            End If
            Dim men As FrmMainMenu = TryCast(f, FrmMainMenu)
            If men IsNot Nothing Then
                men.AplicarIdioma()
                Continue For
            End If
        Next

        ' Actualitzar títol del propi formulari
        Me.Text = Locale.Str("OPC_TITOL")
        Me.Invalidate()

        Return haCanviat
    End Function

    Private Sub BtnAplicar_Click(s As Object, e As EventArgs)
        AplicarCanvis()
        ' Desar a disc però NO tancar el formulari
        FrmOpcions.Opcions.Desar()
        ' Feedback visual: deshabilitar el botó uns instants
        _btnAplicar.Enabled   = False
        _btnAplicar.Text      = Locale.Str("OPC_BTN_APLICAT")
        _btnAplicar.ForeColor = AppStyle.ColPK
        Dim t As New System.Windows.Forms.Timer()
        t.Interval = 1200
        AddHandler t.Tick, Sub(st As Object, et As EventArgs)
            t.Stop()
            If Not Me.IsDisposed Then
                _btnAplicar.Enabled   = True
                _btnAplicar.Text      = Locale.Str("OPC_BTN_APLICAR")
                _btnAplicar.ForeColor = AppStyle.ColAccentSec
            End If
        End Sub
        t.Start()
    End Sub

    Private Sub BtnOK_Click(s As Object, e As EventArgs)
        AplicarCanvis()
        FrmOpcions.Opcions.Desar()
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

    ' ────────────────────────────────────────────────────────
    ' HELPERS DE LAYOUT
    ' ────────────────────────────────────────────────────────
    Private Shared Sub FL(parent As Control, text As String, x As Integer, y As Integer)
        Dim l As New Label()
        l.Text      = text
        l.Location  = New Point(x, y)
        l.AutoSize  = True
        l.Font      = AppStyle.FntMoltPetit
        l.ForeColor = Color.FromArgb(160, 255, 96, 16)
        l.BackColor = Color.Transparent
        parent.Controls.Add(l)
    End Sub

    Private Shared Function FC(parent As Control, x As Integer, y As Integer,
                                w As Integer) As ComboBox
        Dim c As ComboBox = AppStyle.CrearComboBox()
        c.Location = New Point(x, y)
        c.Size     = New Size(w, 22)
        parent.Controls.Add(c)
        Return c
    End Function

    Private Shared Function FK(parent As Control, text As String,
                                x As Integer, y As Integer) As CheckBox
        Dim c As CheckBox = AppStyle.CrearCheckBox(text)
        c.Location = New Point(x, y)
        c.AutoSize = True
        parent.Controls.Add(c)
        Return c
    End Function

    Private Shared Sub SEC(parent As Control, text As String, y As Integer)
        Dim l As New Label()
        l.Text      = "── " & text & " "
        l.Font      = AppStyle.FntMoltPetit
        l.ForeColor = AppStyle.ColAccent
        l.BackColor = Color.Transparent
        l.Location  = New Point(MX, y)
        l.AutoSize  = True
        parent.Controls.Add(l)
    End Sub

    Private Shared Sub LIN(parent As Control, y As Integer)
        Dim p As New Panel()
        p.BackColor = Color.FromArgb(45, 255, 96, 16)
        p.Location  = New Point(MX, y)
        p.Size      = New Size(FW - MX * 2, 1)
        parent.Controls.Add(p)
    End Sub

    Private Shared Sub SelCombo(cmb As ComboBox, claus() As String, valor As String)
        For i As Integer = 0 To claus.Length - 1
            If String.Equals(claus(i), valor, StringComparison.OrdinalIgnoreCase) Then
                cmb.SelectedIndex = i : Return
            End If
        Next
        cmb.SelectedIndex = 0
    End Sub

    Private Shared Function LlegCombo(cmb As ComboBox, claus() As String,
                                       def As String) As String
        Dim idx As Integer = cmb.SelectedIndex
        If idx >= 0 AndAlso idx < claus.Length Then Return claus(idx)
        Return def
    End Function

End Class
