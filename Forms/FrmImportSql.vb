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
' FrmImportSql.vb  —  DB-Core Holographic
' Diàleg de previsualització i confirmació d'importació SQL
' ============================================================
Public Class FrmImportSql
    Inherits HoloForm

    Public Enum ImportMode
        NouProjecte = 0
        AfegirAlActual = 1
    End Enum

    Public Property ResultTaules    As List(Of TablaBBDD)
    Public Property ResultRelacions As List(Of RelacionBBDD)
    Public Property ModeSeleccionat As ImportMode = ImportMode.NouProjecte

    Private _parsed      As SqlScriptImporter.ImportResult
    Private _projecteAct As ProyectoBBDD
    Private _rutaSql     As String

    Private _lblResum     As Label
    Private _rdbNou       As RadioButton
    Private _rdbMerge     As RadioButton
    Private _chkTaules()   As CheckBox
    Private _chkRelacions() As CheckBox

    ' Panels interiors desplaçables (contingut real)
    Private _pnlTaulesInner   As Panel
    Private _pnlRelacionsInner As Panel
    ' Panels clip (finestra visible)
    Private _clipTaules   As Panel
    Private _clipRelacions As Panel

    Private Const ROW_H   As Integer = 20
    Private Const BLK_H   As Integer = 150  ' alçada visible de cada bloc

    Public Sub New(rutaSql As String,
                   parsed As SqlScriptImporter.ImportResult,
                   projecteActual As ProyectoBBDD)
        MyBase.New()
        _rutaSql     = rutaSql
        _parsed      = parsed
        _projecteAct = projecteActual

        Me.Text          = Locale.Str("ISQ_TITOL")
        Me.ClientSize    = New Size(660, 500)
        Me.StartPosition = FormStartPosition.CenterParent
        AppStyle.AplicarEstilForm(Me)
        ConstruirUI()
        OmplirLlistes()
    End Sub

    Private Sub ConstruirUI()
        Const W As Integer = 636

        Dim pnlOuter As New Panel()
        pnlOuter.Dock      = DockStyle.Fill
        pnlOuter.BackColor = AppStyle.ColFons
        pnlOuter.Padding   = New Padding(11, 10, 11, 10)

        Dim pnl As New Panel()
        pnl.Dock       = DockStyle.Fill
        pnl.BackColor  = AppStyle.ColFons
        pnl.AutoScroll = False
        pnlOuter.Controls.Add(pnl)

        Dim y As Integer = 0

        ' ── Nom fitxer ───────────────────────────────────────────
        Dim lblFitxer As New Label()
        lblFitxer.Text      = IO.Path.GetFileName(_rutaSql)
        lblFitxer.ForeColor = AppStyle.ColAccent
        lblFitxer.Font      = AppStyle.FntTitol
        lblFitxer.SetBounds(0, y, W, 20)
        pnl.Controls.Add(lblFitxer)
        y += 22

        ' ── Resum ────────────────────────────────────────────────
        _lblResum = New Label()
        _lblResum.ForeColor = AppStyle.ColTextSec
        _lblResum.Font      = AppStyle.FntPetit
        _lblResum.SetBounds(0, y, W, 16)
        pnl.Controls.Add(_lblResum)
        y += 20

        ' ── Separador ────────────────────────────────────────────
        Dim sep1 As New Label()
        sep1.BackColor = Color.FromArgb(50, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        sep1.SetBounds(0, y, W, 1)
        pnl.Controls.Add(sep1)
        y += 5

        ' ── Fila MODE + botons IMPORTAR / CANCEL·LAR ─────────────
        Dim lblMode As New Label()
        lblMode.Text      = Locale.Str("ISQ_MODE")
        lblMode.ForeColor = AppStyle.ColTextFeble
        lblMode.Font      = AppStyle.FntMoltPetit
        lblMode.SetBounds(0, y + 3, 44, 14)
        pnl.Controls.Add(lblMode)

        _rdbNou = New RadioButton()
        _rdbNou.Text      = Locale.Str("ISQ_NOU")
        _rdbNou.ForeColor = AppStyle.ColTextPrinc
        _rdbNou.BackColor = Color.Transparent
        _rdbNou.Font      = AppStyle.FntPetit
        _rdbNou.AutoSize  = True
        _rdbNou.Checked   = (_projecteAct Is Nothing)
        _rdbNou.SetBounds(48, y, 160, 20)
        pnl.Controls.Add(_rdbNou)

        _rdbMerge = New RadioButton()
        _rdbMerge.Text      = Locale.Str("ISQ_MERGE")
        _rdbMerge.ForeColor = AppStyle.ColAccentSec
        _rdbMerge.BackColor = Color.Transparent
        _rdbMerge.Font      = AppStyle.FntPetit
        _rdbMerge.AutoSize  = True
        _rdbMerge.Checked   = (_projecteAct IsNot Nothing)
        _rdbMerge.Enabled   = (_projecteAct IsNot Nothing)
        _rdbMerge.SetBounds(214, y, 220, 20)
        pnl.Controls.Add(_rdbMerge)

        Dim btnCancel As Button = AppStyle.CrearBotoPerill(Locale.Str("BTN_X_CANCEL"))
        btnCancel.SetBounds(W - 130, y - 1, 130, 22)
        AddHandler btnCancel.Click, Sub(s, e) Me.DialogResult = DialogResult.Cancel
        pnl.Controls.Add(btnCancel)

        Dim btnImportar As Button = AppStyle.CrearBotoAccio(Locale.Str("ISQ_IMPORTAR"))
        btnImportar.SetBounds(W - 265, y - 1, 130, 22)
        AddHandler btnImportar.Click, AddressOf BtnImportar_Click
        pnl.Controls.Add(btnImportar)
        y += 28

        ' ── Separador ────────────────────────────────────────────
        Dim sep2 As New Label()
        sep2.BackColor = Color.FromArgb(50, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        sep2.SetBounds(0, y, W, 1)
        pnl.Controls.Add(sep2)
        y += 5

        ' ── Botons selecció ràpida ───────────────────────────────
        Dim btnTot As Button = AppStyle.CrearBotoCapc(Locale.Str("BTN_TOT"))
        btnTot.SetBounds(0, y, 76, AppStyle.AltBotoPetit)
        AddHandler btnTot.Click, Sub(s, e) SetTotSeleccionat(True)
        pnl.Controls.Add(btnTot)

        Dim btnCap As Button = AppStyle.CrearBotoCapc(Locale.Str("BTN_CAP"))
        btnCap.SetBounds(80, y, 76, AppStyle.AltBotoPetit)
        AddHandler btnCap.Click, Sub(s, e) SetTotSeleccionat(False)
        pnl.Controls.Add(btnCap)
        y += 26

        ' ════════════════════════════════════════════════════════
        ' BLOC TAULES
        ' ════════════════════════════════════════════════════════
        Dim lblTit As New Label()
        lblTit.Text      = Locale.Str("ISQ_TAULES")
        lblTit.ForeColor = AppStyle.ColAccent
        lblTit.Font      = AppStyle.FntCapc
        lblTit.SetBounds(0, y, W - 46, 18)
        pnl.Controls.Add(lblTit)

        Dim btnTUp As Button = CrearBotoScroll("▲")
        btnTUp.SetBounds(W - 42, y, 18, 18)
        AddHandler btnTUp.Click, Sub(s, e) ScrollInner(_pnlTaulesInner, _clipTaules, -3)
        pnl.Controls.Add(btnTUp)

        Dim btnTDn As Button = CrearBotoScroll("▼")
        btnTDn.SetBounds(W - 20, y, 18, 18)
        AddHandler btnTDn.Click, Sub(s, e) ScrollInner(_pnlTaulesInner, _clipTaules, 3)
        pnl.Controls.Add(btnTDn)
        y += 20

        ' Panel clip (finestra visible)
        _clipTaules = New Panel()
        _clipTaules.SetBounds(0, y, W, BLK_H)
        _clipTaules.BackColor = AppStyle.ColFonsMig
        _clipTaules.AutoScroll = False

        ' Panel interior desplaçable
        _pnlTaulesInner = New Panel()
        _pnlTaulesInner.SetBounds(0, 0, W - 4, BLK_H)
        _pnlTaulesInner.BackColor = AppStyle.ColFonsMig
        _pnlTaulesInner.AutoScroll = False
        AddHandler _clipTaules.MouseWheel, Sub(s, e)
            ScrollInner(_pnlTaulesInner, _clipTaules,
                        If(DirectCast(e, MouseEventArgs).Delta > 0, -3, 3))
        End Sub
        _clipTaules.Controls.Add(_pnlTaulesInner)
        pnl.Controls.Add(_clipTaules)
        y += BLK_H + 6

        ' ════════════════════════════════════════════════════════
        ' BLOC RELACIONS
        ' ════════════════════════════════════════════════════════
        Dim lblTit2 As New Label()
        lblTit2.Text      = Locale.Str("ISQ_RELACIONS")
        lblTit2.ForeColor = AppStyle.ColFK
        lblTit2.Font      = AppStyle.FntCapc
        lblTit2.SetBounds(0, y, W - 46, 18)
        pnl.Controls.Add(lblTit2)

        Dim btnRUp As Button = CrearBotoScroll("▲")
        btnRUp.SetBounds(W - 42, y, 18, 18)
        AddHandler btnRUp.Click, Sub(s, e) ScrollInner(_pnlRelacionsInner, _clipRelacions, -3)
        pnl.Controls.Add(btnRUp)

        Dim btnRDn As Button = CrearBotoScroll("▼")
        btnRDn.SetBounds(W - 20, y, 18, 18)
        AddHandler btnRDn.Click, Sub(s, e) ScrollInner(_pnlRelacionsInner, _clipRelacions, 3)
        pnl.Controls.Add(btnRDn)
        y += 20

        _clipRelacions = New Panel()
        _clipRelacions.SetBounds(0, y, W, BLK_H)
        _clipRelacions.BackColor = AppStyle.ColFonsMig
        _clipRelacions.AutoScroll = False

        _pnlRelacionsInner = New Panel()
        _pnlRelacionsInner.SetBounds(0, 0, W - 4, BLK_H)
        _pnlRelacionsInner.BackColor = AppStyle.ColFonsMig
        _pnlRelacionsInner.AutoScroll = False
        AddHandler _clipRelacions.MouseWheel, Sub(s, e)
            ScrollInner(_pnlRelacionsInner, _clipRelacions,
                        If(DirectCast(e, MouseEventArgs).Delta > 0, -3, 3))
        End Sub
        _clipRelacions.Controls.Add(_pnlRelacionsInner)
        pnl.Controls.Add(_clipRelacions)

        Me.Controls.Add(pnlOuter)
    End Sub

    ' ── Scroll: desplaça el panel interior dins el clip ──────────
    Private Shared Sub ScrollInner(inner As Panel, clip As Panel, dir As Integer)
        If inner Is Nothing OrElse clip Is Nothing Then Return
        Dim minTop As Integer = Math.Min(0, clip.Height - inner.Height)
        Dim newTop As Integer = Math.Max(minTop, Math.Min(0, inner.Top - dir * ROW_H))
        inner.Top = newTop
    End Sub

    ' ── Botó de scroll petit ─────────────────────────────────────
    Private Shared Function CrearBotoScroll(txt As String) As Button
        Dim b As New Button()
        b.Text      = txt
        b.FlatStyle = FlatStyle.Flat
        b.FlatAppearance.BorderColor = AppStyle.ColVoraFeble
        b.FlatAppearance.MouseOverBackColor = AppStyle.ColHover
        b.BackColor = AppStyle.ColFonsMig
        b.ForeColor = AppStyle.ColAccent
        b.Font      = New Font("Courier New", 7, FontStyle.Bold)
        b.Cursor    = Cursors.Hand
        Return b
    End Function

    Private Sub OmplirLlistes()
        Dim nT As Integer = _parsed.Taules.Count
        Dim nR As Integer = _parsed.Relacions.Count
        _lblResum.Text = nT & Locale.Str("DLG_IMPORT_TAULES") & nR & Locale.Str("DLG_IMPORT_RELACIONS")

        ' ── Taules ──────────────────────────────────────────────
        _pnlTaulesInner.Controls.Clear()
        ReDim _chkTaules(nT - 1)
        Dim W As Integer = _pnlTaulesInner.Width - 6

        For i As Integer = 0 To nT - 1
            Dim t As TablaBBDD = _parsed.Taules(i)
            Dim jaSurt As Boolean = (_projecteAct IsNot Nothing) AndAlso
                                    _projecteAct.Taules.Exists(Function(x) x.Nombre = t.Nombre)
            Dim chk As New CheckBox()
            chk.Text      = String.Format("{0}.{1}  ({2}{3}){4}",
                                          t.Schema, t.Nombre, t.Fields.Count,
                                          Locale.Str("ISQ_CAMPS"),
                                          If(jaSurt, Locale.Str("ISQ_EXISTEIX"), ""))
            chk.ForeColor = If(jaSurt, AppStyle.ColTextFeble, AppStyle.ColTextPrinc)
            chk.BackColor = AppStyle.ColFonsMig
            chk.Font      = AppStyle.FntPetit
            chk.Checked   = Not jaSurt
            chk.SetBounds(2, i * ROW_H, W, ROW_H)
            chk.Tag       = i
            Dim iCap As Integer = i
            AddHandler chk.KeyDown, Sub(s2 As Object, ev As KeyEventArgs)
                TeclatCheckBox(ev, iCap, _chkTaules, _pnlTaulesInner, _clipTaules,
                                         _chkRelacions, _pnlRelacionsInner, _clipRelacions)
            End Sub
            AddHandler chk.Enter, Sub(s2 As Object, ev As EventArgs)
                AssegurarVisible(_pnlTaulesInner, _clipTaules, iCap)
            End Sub
            _pnlTaulesInner.Controls.Add(chk)
            _chkTaules(i) = chk
        Next i
        ' Alçada total del panel interior
        _pnlTaulesInner.Height = Math.Max(BLK_H, nT * ROW_H + 4)

        ' ── Relacions ───────────────────────────────────────────
        _pnlRelacionsInner.Controls.Clear()
        ReDim _chkRelacions(nR - 1)

        For i As Integer = 0 To nR - 1
            Dim r As RelacionBBDD = _parsed.Relacions(i)
            Dim nomOri As String = r.TablaOrigenId.ToString()
            Dim nomDes As String = r.TablaDestinoId.ToString()
            For Each t As TablaBBDD In _parsed.Taules
                If t.Id = r.TablaOrigenId Then nomOri = t.Nombre
                If t.Id = r.TablaDestinoId Then nomDes = t.Nombre
            Next
            Dim jaSurt As Boolean = (_projecteAct IsNot Nothing) AndAlso
                                    _projecteAct.Relacions.Exists(Function(x) x.Nombre = r.Nombre)
            Dim chk As New CheckBox()
            chk.Text      = String.Format("{0}  ({1}.{2} → {3}.{4}){5}",
                                          r.Nombre, nomOri, r.CampoFKNombre,
                                          nomDes, r.CampoPKNombre,
                                          If(jaSurt, Locale.Str("ISQ_EXISTEIX"), ""))
            chk.ForeColor = If(jaSurt, AppStyle.ColTextFeble, AppStyle.ColFK)
            chk.BackColor = AppStyle.ColFonsMig
            chk.Font      = AppStyle.FntPetit
            chk.Checked   = Not jaSurt
            chk.SetBounds(2, i * ROW_H, W, ROW_H)
            chk.Tag       = i
            Dim iCap As Integer = i
            AddHandler chk.KeyDown, Sub(s2 As Object, ev As KeyEventArgs)
                TeclatCheckBox(ev, iCap, _chkRelacions, _pnlRelacionsInner, _clipRelacions,
                                         _chkTaules, _pnlTaulesInner, _clipTaules)
            End Sub
            AddHandler chk.Enter, Sub(s2 As Object, ev As EventArgs)
                AssegurarVisible(_pnlRelacionsInner, _clipRelacions, iCap)
            End Sub
            _pnlRelacionsInner.Controls.Add(chk)
            _chkRelacions(i) = chk
        Next i
        _pnlRelacionsInner.Height = Math.Max(BLK_H, nR * ROW_H + 4)
    End Sub

    Private Sub SetTotSeleccionat(valor As Boolean)
        If _chkTaules IsNot Nothing Then
            For Each chk As CheckBox In _chkTaules
                If chk IsNot Nothing Then chk.Checked = valor
            Next
        End If
        If _chkRelacions IsNot Nothing Then
            For Each chk As CheckBox In _chkRelacions
                If chk IsNot Nothing Then chk.Checked = valor
            Next
        End If
    End Sub

    ' ── Assegurar que el checkbox idx és visible dins el clip ────
    Private Shared Sub AssegurarVisible(inner As Panel, clip As Panel, idx As Integer)
        Dim itemTop As Integer = idx * ROW_H
        Dim itemBot As Integer = itemTop + ROW_H
        Dim visTop  As Integer = -inner.Top
        Dim visBot  As Integer = visTop + clip.Height
        If itemTop < visTop Then
            inner.Top = -itemTop
        ElseIf itemBot > visBot Then
            inner.Top = -(itemBot - clip.Height)
        End If
        ' Clamp
        Dim minTop As Integer = Math.Min(0, clip.Height - inner.Height)
        inner.Top = Math.Max(minTop, Math.Min(0, inner.Top))
    End Sub

    ' ── Navegació per teclat ─────────────────────────────────────
    Private Sub TeclatCheckBox(ev As KeyEventArgs,
                                idx As Integer,
                                chks As CheckBox(),
                                inner As Panel, clip As Panel,
                                chksAlt As CheckBox(),
                                innerAlt As Panel, clipAlt As Panel)
        Select Case ev.KeyCode
            Case Keys.Up
                ev.Handled = True : ev.SuppressKeyPress = True
                If idx > 0 Then chks(idx - 1).Focus()

            Case Keys.Down
                ev.Handled = True : ev.SuppressKeyPress = True
                If idx < chks.Length - 1 Then chks(idx + 1).Focus()

            Case Keys.Tab
                ev.Handled = True : ev.SuppressKeyPress = True
                If chksAlt IsNot Nothing AndAlso chksAlt.Length > 0 Then chksAlt(0).Focus()

            Case Keys.Space, Keys.Enter
                ev.Handled = True : ev.SuppressKeyPress = True
                chks(idx).Checked = Not chks(idx).Checked

            Case Keys.Home
                ev.Handled = True : ev.SuppressKeyPress = True
                If chks.Length > 0 Then chks(0).Focus()

            Case Keys.End
                ev.Handled = True : ev.SuppressKeyPress = True
                If chks.Length > 0 Then chks(chks.Length - 1).Focus()

            Case Keys.PageUp
                ev.Handled = True : ev.SuppressKeyPress = True
                Dim visU As Integer = clip.Height \ ROW_H
                chks(Math.Max(0, idx - visU)).Focus()

            Case Keys.PageDown
                ev.Handled = True : ev.SuppressKeyPress = True
                Dim visD As Integer = clip.Height \ ROW_H
                chks(Math.Min(chks.Length - 1, idx + visD)).Focus()
        End Select
    End Sub

    Private Sub BtnImportar_Click(s As Object, e As EventArgs)
        Dim taulesSel As New List(Of TablaBBDD)()
        Dim relsSel   As New List(Of RelacionBBDD)()

        If _chkTaules IsNot Nothing Then
            For Each chk As CheckBox In _chkTaules
                If chk IsNot Nothing AndAlso chk.Checked Then
                    taulesSel.Add(_parsed.Taules(CInt(chk.Tag)))
                End If
            Next
        End If
        If _chkRelacions IsNot Nothing Then
            For Each chk As CheckBox In _chkRelacions
                If chk IsNot Nothing AndAlso chk.Checked Then
                    relsSel.Add(_parsed.Relacions(CInt(chk.Tag)))
                End If
            Next
        End If

        If taulesSel.Count = 0 AndAlso relsSel.Count = 0 Then
            _lblResum.Text      = Locale.Str("ISQ_ERR_SEL")
            _lblResum.ForeColor = AppStyle.ColPerill
            Return
        End If

        Me.ResultTaules    = taulesSel
        Me.ResultRelacions = relsSel
        Me.ModeSeleccionat = If(_rdbMerge.Checked,
                                ImportMode.AfegirAlActual,
                                ImportMode.NouProjecte)
        Me.DialogResult    = DialogResult.OK
    End Sub

End Class
