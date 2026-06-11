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
' FrmExportMdf.vb  —  DB-Core Holographic
' Diàleg per exportar el projecte a un fitxer .mdf via LocalDB
' ============================================================
Public Class FrmExportMdf
    Inherits HoloForm

    ' ── Controls ────────────────────────────────────────────────
    Private _txtDbName  As TextBox
    Private _txtDir     As TextBox
    Private _btnDir     As Button
    Private _cmbMode    As ComboBox
    Private _btnOK      As Button
    Private _btnCancel  As Button
    Private _lblError   As Label

    ' ── Propietats de sortida ────────────────────────────────────
    Public Property DbName As String = ""
    Public Property MdfDir As String = ""
    Public Property Mode   As MdfExporter.ExportMode = MdfExporter.ExportMode.CreateNew

    ' ── Constructor ─────────────────────────────────────────────
    Public Sub New(suggestedName As String, lastDir As String)
        MyBase.New()
        Me.Text          = Locale.Str("EXP_TITOL")
        Me.ClientSize    = New Size(500, 310)
        Me.StartPosition = FormStartPosition.CenterParent
        AppStyle.AplicarEstilForm(Me)
        ConstruirUI(suggestedName, lastDir)
    End Sub

    ' ── Construcció de la UI ─────────────────────────────────────
    Private Sub ConstruirUI(suggestedName As String, lastDir As String)

        ' ── Panel botons (Bottom, 54px) ──────────────────────────
        Dim pnlBotons As New Panel()
        pnlBotons.Dock      = DockStyle.Bottom
        pnlBotons.Height    = 54
        pnlBotons.BackColor = AppStyle.ColFonsMig

        Dim sep As New Label()
        sep.Height    = 1
        sep.Dock      = DockStyle.Top
        sep.BackColor = AppStyle.ColVora
        pnlBotons.Controls.Add(sep)

        _btnOK = AppStyle.CrearBotoAccio(Locale.Str("EXP_BTN_OK"))
        _btnOK.Width  = 160
        _btnOK.Height = AppStyle.AltBotoNormal
        _btnOK.Top    = 14
        _btnOK.Left   = 500 - 160 - 145 - 12
        _btnOK.Anchor = AnchorStyles.Right Or AnchorStyles.Top
        AddHandler _btnOK.Click, AddressOf BtnOK_Click
        pnlBotons.Controls.Add(_btnOK)

        _btnCancel = AppStyle.CrearBotoPerill(Locale.Str("BTN_X_CANCEL"))
        _btnCancel.Width  = 140
        _btnCancel.Height = AppStyle.AltBotoNormal
        _btnCancel.Top    = 14
        _btnCancel.Left   = 500 - 140 - 8
        _btnCancel.Anchor = AnchorStyles.Right Or AnchorStyles.Top
        AddHandler _btnCancel.Click, Sub(s, e) Me.DialogResult = DialogResult.Cancel
        pnlBotons.Controls.Add(_btnCancel)

        ' ── Label error (Bottom, sobre botons) ───────────────────
        _lblError = New Label()
        _lblError.Dock      = DockStyle.Bottom
        _lblError.Height    = 20
        _lblError.ForeColor = AppStyle.ColPerill
        _lblError.Font      = AppStyle.FntPetit
        _lblError.TextAlign = ContentAlignment.MiddleLeft
        _lblError.Padding   = New Padding(10, 0, 0, 0)
        _lblError.BackColor = AppStyle.ColFons
        _lblError.Visible   = False

        ' ── Panell de contingut (Fill) ────────────────────────────
        Dim pnlOuter As New Panel()
        pnlOuter.Dock      = DockStyle.Fill
        pnlOuter.BackColor = AppStyle.ColFons
        pnlOuter.Padding   = New Padding(17, 14, 17, 6)

        Dim pnlCont As New Panel()
        pnlCont.Dock      = DockStyle.Fill
        pnlCont.BackColor = AppStyle.ColFons
        pnlOuter.Controls.Add(pnlCont)

        ' Nom de la BD
        Dim lblDbName As Label = CrearLabel(Locale.Str("EXP_NOM_BD"))
        lblDbName.SetBounds(0, 0, 464, 16)

        _txtDbName = AppStyle.CrearTextBox()
        _txtDbName.Text   = SanitizeDbName(suggestedName)
        _txtDbName.SetBounds(0, 18, 464, 22)

        ' Directori
        Dim lblDir As Label = CrearLabel(Locale.Str("EXP_DIR"))
        lblDir.SetBounds(0, 52, 464, 16)

        _txtDir = AppStyle.CrearTextBox()
        _txtDir.Text = If(String.IsNullOrEmpty(lastDir),
                          IO.Path.Combine(Environment.GetFolderPath(
                              Environment.SpecialFolder.MyDocuments), "DB-Core"),
                          lastDir)
        _txtDir.SetBounds(0, 70, 418, 22)

        _btnDir = AppStyle.CrearBotoCapc("...")
        _btnDir.SetBounds(424, 69, 40, 24)
        AddHandler _btnDir.Click, AddressOf BtnDir_Click

        ' Mode d'exportació
        Dim lblMode As Label = CrearLabel(Locale.Str("EXP_MODE"))
        lblMode.SetBounds(0, 106, 464, 16)

        _cmbMode = AppStyle.CrearComboBox()
        _cmbMode.Items.Add(Locale.Str("EXP_MODE_NOU"))
        _cmbMode.Items.Add(Locale.Str("EXP_MODE_DROP"))
        _cmbMode.Items.Add(Locale.Str("EXP_MODE_DIFF"))
        _cmbMode.SelectedIndex = 0
        _cmbMode.SetBounds(0, 124, 464, 22)

        ' Nota informativa
        Dim lblNota As New Label()
        lblNota.Text      = Locale.Str("EXP_NOTA")
        lblNota.ForeColor = AppStyle.ColTextFeble
        lblNota.Font      = AppStyle.FntMoltPetit
        lblNota.BackColor = Color.Transparent
        lblNota.SetBounds(0, 158, 464, 16)

        pnlCont.Controls.Add(lblDbName)
        pnlCont.Controls.Add(_txtDbName)
        pnlCont.Controls.Add(lblDir)
        pnlCont.Controls.Add(_txtDir)
        pnlCont.Controls.Add(_btnDir)
        pnlCont.Controls.Add(lblMode)
        pnlCont.Controls.Add(_cmbMode)
        pnlCont.Controls.Add(lblNota)

        ' ── Ordre WinForms Dock: Fill primer, Bottom últim ────────
        Me.Controls.Add(pnlOuter)
        Me.Controls.Add(_lblError)
        Me.Controls.Add(pnlBotons)
    End Sub

    ' ── Selecció de directori ────────────────────────────────────
    Private Sub BtnDir_Click(s As Object, e As EventArgs)
        Using dlg As New FolderBrowserDialog()
            dlg.Description         = Locale.Str("EXP_DLG_DIR")
            dlg.SelectedPath        = _txtDir.Text
            dlg.ShowNewFolderButton = True
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                _txtDir.Text = dlg.SelectedPath
            End If
        End Using
    End Sub

    ' ── Validació i acceptació ───────────────────────────────────
    Private Sub BtnOK_Click(s As Object, e As EventArgs)
        _lblError.Visible = False

        Dim nom As String = _txtDbName.Text.Trim()
        Dim dir As String = _txtDir.Text.Trim()

        If String.IsNullOrEmpty(nom) Then
            MostrarError(Locale.Str("EXP_ERR_NOM"))
            Return
        End If
        If nom.IndexOfAny(IO.Path.GetInvalidFileNameChars()) >= 0 OrElse nom.Contains("'") Then
            MostrarError(Locale.Str("EXP_ERR_CHARS"))
            Return
        End If
        If String.IsNullOrEmpty(dir) Then
            MostrarError(Locale.Str("EXP_ERR_DIR"))
            Return
        End If

        Me.DbName = nom
        Me.MdfDir = dir
        Me.Mode   = CType(_cmbMode.SelectedIndex, MdfExporter.ExportMode)
        Me.DialogResult = DialogResult.OK
    End Sub

    ' ── Helpers ─────────────────────────────────────────────────
    Private Sub MostrarError(msg As String)
        _lblError.Text    = "⚠  " & msg
        _lblError.Visible = True
    End Sub

    Private Shared Function CrearLabel(text As String) As Label
        Dim lbl As New Label()
        lbl.Text      = text
        lbl.ForeColor = AppStyle.ColTextFeble
        lbl.Font      = AppStyle.FntMoltPetit
        lbl.Height    = 16
        lbl.BackColor = Color.Transparent
        Return lbl
    End Function

    Private Shared Function SanitizeDbName(nom As String) As String
        If String.IsNullOrWhiteSpace(nom) Then Return "NovaBaseDades"
        Dim chars As Char() = IO.Path.GetInvalidFileNameChars()
        Dim res As String = nom.Trim().Replace(" ", "_")
        For Each c As Char In chars
            res = res.Replace(c.ToString(), "")
        Next c
        Return If(String.IsNullOrEmpty(res), "NovaBaseDades", res)
    End Function

End Class
