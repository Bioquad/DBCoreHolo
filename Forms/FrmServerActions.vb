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
Imports System.Threading

' ============================================================
' FrmServerActions.vb  —  DB-Core Holographic
' Diàleg d'accions sobre un servidor SQL Server connectat:
'   - Importar estructura (obre FrmImportSql per previsualitzar)
'   - Exportar projecte complet (CreateNew / AlterExisting)
'   - Enviar parts seleccionades (taules concretes + FK)
' ============================================================
Public Class FrmServerActions
    Inherits HoloForm

    ' ── Accions disponibles ──────────────────────────────────────
    Public Enum Accio
        Importar = 0
        Exportar = 1
        EnviarParts = 2
    End Enum

    ' ── Resultat exposat ─────────────────────────────────────────
    Public Property AccioSeleccionada  As Accio = Accio.Importar
    Public Property ModeExportacio     As Integer = 0   ' 0=CreateNew, 2=AlterExisting
    ' Per EnviarParts: taules i relacions seleccionades
    Public Property TaulesSeleccionades   As List(Of TablaBBDD)
    Public Property RelacionsSeleccionades As List(Of RelacionBBDD)

    ' ── Dades internes ───────────────────────────────────────────
    Private _conn    As SqlServerConnector.ConnexioServidor
    Private _proyecto As ProyectoBBDD

    ' ── Controls ────────────────────────────────────────────────
    Private _lblConnInfo     As Label
    Private _lblStatus       As Label
    Private _pnlExport       As Panel
    Private _pnlParts        As Panel
    Private _rdbCreate       As RadioButton
    Private _rdbAlter        As RadioButton

    ' Parts: checkboxes de taules
    Private _chkTaules()     As CheckBox
    Private _pnlTaulesInner  As Panel
    Private _clipTaules      As Panel
    Private Const ROW_H As Integer = 20
    Private Const BLK_H As Integer = 160

    Public Sub New(conn As SqlServerConnector.ConnexioServidor,
                   proyecto As ProyectoBBDD)
        MyBase.New()
        _conn    = conn
        _proyecto = proyecto

        Me.Text          = Locale.Str("SRV_TITOL")
        Me.ClientSize    = New Size(580, 520)
        Me.StartPosition = FormStartPosition.CenterParent
        AppStyle.AplicarEstilForm(Me)
        ConstruirUI()
    End Sub

    Private Sub ConstruirUI()
        Const W As Integer = 556

        Dim pnlOuter As New Panel()
        pnlOuter.Dock      = DockStyle.Fill
        pnlOuter.BackColor = AppStyle.ColFons
        pnlOuter.Padding   = New Padding(11, 12, 11, 12)

        Dim pnl As New Panel()
        pnl.Dock      = DockStyle.Fill
        pnl.BackColor = AppStyle.ColFons
        pnlOuter.Controls.Add(pnl)

        Dim y As Integer = 0

        ' ── Info connexió ─────────────────────────────────────────
        Dim lblTit As New Label()
        lblTit.Text      = Locale.Str("SRV_TITOL")
        lblTit.ForeColor = AppStyle.ColAccent
        lblTit.Font      = AppStyle.FntTitol
        lblTit.SetBounds(0, y, W, 20)
        pnl.Controls.Add(lblTit)
        y += 22

        _lblConnInfo = New Label()
        _lblConnInfo.Text      = _conn.ToString()
        _lblConnInfo.ForeColor = AppStyle.ColAccentSec
        _lblConnInfo.Font      = AppStyle.FntPetit
        _lblConnInfo.SetBounds(0, y, W, 16)
        pnl.Controls.Add(_lblConnInfo)
        y += 22

        pnl.Controls.Add(CrearSep(0, y, W))
        y += 8

        ' ════════════════════════════════════════════════
        ' BLOC 1 — IMPORTAR
        ' ════════════════════════════════════════════════
        pnl.Controls.Add(CrearTitolBloc(Locale.Str("SRV_BLOC1"), 0, y, W))
        y += 20

        Dim lblImpDesc As New Label()
        lblImpDesc.Text      = Locale.Str("SRV_BLOC1_DESC")
        lblImpDesc.ForeColor = AppStyle.ColTextSec
        lblImpDesc.Font      = AppStyle.FntMoltPetit
        lblImpDesc.SetBounds(0, y, W, 28)
        lblImpDesc.AutoSize  = False
        pnl.Controls.Add(lblImpDesc)
        y += 30

        Dim btnImportar As Button = AppStyle.CrearBotoAccio(Locale.Str("SRV_BTN_IMPORTAR"))
        btnImportar.Width = 200
        btnImportar.SetBounds(0, y, 200, AppStyle.AltBotoNormal)
        AddHandler btnImportar.Click, AddressOf BtnImportar_Click
        pnl.Controls.Add(btnImportar)
        y += 36

        pnl.Controls.Add(CrearSep(0, y, W))
        y += 8

        ' ════════════════════════════════════════════════
        ' BLOC 2 — EXPORTAR PROJECTE
        ' ════════════════════════════════════════════════
        pnl.Controls.Add(CrearTitolBloc(Locale.Str("SRV_BLOC2"), 0, y, W))
        y += 20

        _pnlExport = New Panel()
        _pnlExport.SetBounds(0, y, W, 72)
        _pnlExport.BackColor = AppStyle.ColFonsMig

        Dim lblExpDesc As New Label()
        lblExpDesc.Text      = Locale.Str("SRV_BLOC2_DESC")
        lblExpDesc.ForeColor = AppStyle.ColTextSec
        lblExpDesc.Font      = AppStyle.FntMoltPetit
        lblExpDesc.SetBounds(6, 4, W - 12, 14)
        _pnlExport.Controls.Add(lblExpDesc)

        _rdbCreate = New RadioButton()
        _rdbCreate.Text      = Locale.Str("SRV_RDB_CREATE")
        _rdbCreate.ForeColor = AppStyle.ColTextPrinc
        _rdbCreate.BackColor = Color.Transparent
        _rdbCreate.Font      = AppStyle.FntPetit
        _rdbCreate.Checked   = True
        _rdbCreate.SetBounds(6, 22, 250, 18)
        _pnlExport.Controls.Add(_rdbCreate)

        _rdbAlter = New RadioButton()
        _rdbAlter.Text      = Locale.Str("SRV_RDB_ALTER")
        _rdbAlter.ForeColor = AppStyle.ColAccentSec
        _rdbAlter.BackColor = Color.Transparent
        _rdbAlter.Font      = AppStyle.FntPetit
        _rdbAlter.SetBounds(6, 42, 350, 18)
        _pnlExport.Controls.Add(_rdbAlter)

        Dim btnExportar As Button = AppStyle.CrearBotoAccio(Locale.Str("SRV_BTN_EXPORTAR"))
        btnExportar.Width = 210
        btnExportar.SetBounds(W - 220, 24, 210, AppStyle.AltBotoNormal)
        AddHandler btnExportar.Click, AddressOf BtnExportar_Click
        _pnlExport.Controls.Add(btnExportar)

        pnl.Controls.Add(_pnlExport)
        y += 80

        pnl.Controls.Add(CrearSep(0, y, W))
        y += 8

        ' ════════════════════════════════════════════════
        ' BLOC 3 — ENVIAR PARTS
        ' ════════════════════════════════════════════════
        pnl.Controls.Add(CrearTitolBloc(Locale.Str("SRV_BLOC3"), 0, y, W))
        y += 20

        Dim lblPartsDesc As New Label()
        lblPartsDesc.Text      = Locale.Str("SRV_BLOC3_DESC")
        lblPartsDesc.ForeColor = AppStyle.ColTextSec
        lblPartsDesc.Font      = AppStyle.FntMoltPetit
        lblPartsDesc.SetBounds(0, y, W, 14)
        pnl.Controls.Add(lblPartsDesc)
        y += 18

        ' Botons ràpids ▣ TOT / □ CAP
        Dim btnTot As Button = AppStyle.CrearBotoCapc(Locale.Str("BTN_TOT"))
        btnTot.SetBounds(0, y, 76, AppStyle.AltBotoPetit)
        AddHandler btnTot.Click, Sub(s, e) SetTotTaules(True)
        pnl.Controls.Add(btnTot)

        Dim btnCap As Button = AppStyle.CrearBotoCapc(Locale.Str("BTN_CAP"))
        btnCap.SetBounds(80, y, 76, AppStyle.AltBotoPetit)
        AddHandler btnCap.Click, Sub(s, e) SetTotTaules(False)
        pnl.Controls.Add(btnCap)

        Dim btnEnviar As Button = AppStyle.CrearBotoAccio(Locale.Str("SRV_BTN_ENVIAR"))
        btnEnviar.Width = 170
        btnEnviar.SetBounds(W - 175, y, 170, AppStyle.AltBotoNormal)
        AddHandler btnEnviar.Click, AddressOf BtnEnviarParts_Click
        pnl.Controls.Add(btnEnviar)
        y += 28

        ' ── Llista de taules (clip + inner) ──────────────────────
        Dim btnUp As Button = CrearBotoScroll("▲")
        btnUp.SetBounds(W - 42, y, 18, 18)
        AddHandler btnUp.Click, Sub(s, e) ScrollInner(-3)
        pnl.Controls.Add(btnUp)

        Dim btnDn As Button = CrearBotoScroll("▼")
        btnDn.SetBounds(W - 20, y, 18, 18)
        AddHandler btnDn.Click, Sub(s, e) ScrollInner(3)
        pnl.Controls.Add(btnDn)

        _clipTaules = New Panel()
        _clipTaules.SetBounds(0, y, W, BLK_H)
        _clipTaules.BackColor  = AppStyle.ColFonsMig
        _clipTaules.AutoScroll = False
        AddHandler _clipTaules.MouseWheel, Sub(s, e)
            ScrollInner(If(DirectCast(e, MouseEventArgs).Delta > 0, -3, 3))
        End Sub

        _pnlTaulesInner = New Panel()
        _pnlTaulesInner.SetBounds(0, 0, W - 4, BLK_H)
        _pnlTaulesInner.BackColor  = AppStyle.ColFonsMig
        _pnlTaulesInner.AutoScroll = False
        _clipTaules.Controls.Add(_pnlTaulesInner)
        pnl.Controls.Add(_clipTaules)

        ' ── Label estat ──────────────────────────────────────────
        y += BLK_H + 4
        _lblStatus = New Label()
        _lblStatus.Text      = ""
        _lblStatus.ForeColor = AppStyle.ColTextFeble
        _lblStatus.Font      = AppStyle.FntMoltPetit
        _lblStatus.SetBounds(0, y, W, 14)
        _lblStatus.AutoEllipsis = True
        pnl.Controls.Add(_lblStatus)

        Me.Controls.Add(pnlOuter)

        ' Omplir llista de taules
        OmplirLlistaTaules()
    End Sub

    ' ── Omplir checkboxes de taules ──────────────────────────────
    Private Sub OmplirLlistaTaules()
        _pnlTaulesInner.Controls.Clear()
        Dim nT As Integer = _proyecto.Taules.Count
        ReDim _chkTaules(nT - 1)
        Dim W As Integer = _pnlTaulesInner.Width - 6

        For i As Integer = 0 To nT - 1
            Dim t As TablaBBDD = _proyecto.Taules(i)
            Dim nRel As Integer = 0
            For Each rr As RelacionBBDD In _proyecto.Relacions
                If rr.TablaOrigenId = t.Id OrElse rr.TablaDestinoId = t.Id Then nRel += 1
            Next
            Dim chk As New CheckBox()
            chk.Text      = String.Format("{0}.{1}  ({2}" & Locale.Str("ISQ_CAMPS") & ", {3}" & Locale.Str("SRV_REL") & ")",
                                          t.Schema, t.Nombre, t.Fields.Count, nRel)
            chk.ForeColor = AppStyle.ColTextPrinc
            chk.BackColor = AppStyle.ColFonsMig
            chk.Font      = AppStyle.FntPetit
            chk.Checked   = True
            chk.SetBounds(2, i * ROW_H, W, ROW_H)
            chk.Tag       = i
            _pnlTaulesInner.Controls.Add(chk)
            _chkTaules(i) = chk
        Next i
        _pnlTaulesInner.Height = Math.Max(BLK_H, nT * ROW_H + 4)
    End Sub

    Private Sub SetTotTaules(valor As Boolean)
        If _chkTaules Is Nothing Then Return
        For Each chk As CheckBox In _chkTaules
            If chk IsNot Nothing Then chk.Checked = valor
        Next
    End Sub

    Private Sub ScrollInner(dir As Integer)
        If _pnlTaulesInner Is Nothing OrElse _clipTaules Is Nothing Then Return
        Dim minTop As Integer = Math.Min(0, _clipTaules.Height - _pnlTaulesInner.Height)
        _pnlTaulesInner.Top = Math.Max(minTop, Math.Min(0,
                              _pnlTaulesInner.Top - dir * ROW_H))
    End Sub

    ' ════════════════════════════════════════════════════════════
    ' ACCIÓ 1: IMPORTAR
    ' ════════════════════════════════════════════════════════════
    Private Sub BtnImportar_Click(s As Object, e As EventArgs)
        SetStatus(Locale.Str("SRV_LLEGINT") & _conn.BaseDades & "...", AppStyle.ColTextFeble)
        Dim p As ProyectoBBDD = Nothing
        Dim errMsg As String = ""

        Dim t As New Thread(Sub()
            Try
                p = SqlServerConnector.ImportarEstructura(_conn)
            Catch ex As Exception
                errMsg = ex.Message
            End Try
        End Sub)
        t.IsBackground = True
        t.Start()

        ' Esperar mostrant feedback
        Do While t.IsAlive
            Application.DoEvents()
            Thread.Sleep(50)
        Loop

        If Not String.IsNullOrEmpty(errMsg) Then
            SetStatus("✗  " & errMsg, AppStyle.ColPerill)
            MessageBox.Show(Locale.Str("SRV_ERR_IMPORT") & Environment.NewLine & errMsg,
                            Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If p Is Nothing OrElse p.Taules.Count = 0 Then
            SetStatus(Locale.Str("SRV_CAP_TAULA"), AppStyle.ColTextFeble)
            Return
        End If

        SetStatus(Locale.Str("SRV_LLEGIDES1") & p.Taules.Count & Locale.Str("SRV_LLEGIDES2"),
                  AppStyle.ColAccentSec)

        ' Convertir a ImportResult per reutilitzar FrmImportSql
        Dim parsed As New SqlScriptImporter.ImportResult()
        parsed.Taules.AddRange(p.Taules)
        parsed.Relacions.AddRange(p.Relacions)

        Using fImp As New FrmImportSql(Locale.Str("SRV_PREFIX") & _conn.BaseDades, parsed, _proyecto)
            If fImp.ShowDialog(Me) <> DialogResult.OK Then
                SetStatus(Locale.Str("SRV_IMP_CANCEL"), AppStyle.ColTextFeble)
                Return
            End If
            ' Retornar el resultat al cridant via propietats
            Me.AccioSeleccionada = Accio.Importar
            ' Reutilitzem les propietats de FrmImportSql per passar el resultat
            Me.TaulesSeleccionades    = fImp.ResultTaules
            Me.RelacionsSeleccionades = fImp.ResultRelacions
            ' Mode d'importació
            Dim modeImp As FrmImportSql.ImportMode = fImp.ModeSeleccionat
            Me.Tag = modeImp   ' el cridant ho llegirà
            Me.DialogResult = DialogResult.OK
        End Using
    End Sub

    ' ════════════════════════════════════════════════════════════
    ' ACCIÓ 2: EXPORTAR
    ' ════════════════════════════════════════════════════════════
    Private Sub BtnExportar_Click(s As Object, e As EventArgs)
        Dim mode As Integer = If(_rdbCreate.Checked, 0, 2)
        Dim nomBD As String = _conn.BaseDades

        Dim confirm As DialogResult = MessageBox.Show(
            If(mode = 0,
               Locale.Str("SRV_CONF_C1") & nomBD & Locale.Str("SRV_CONF_C2") &
               Environment.NewLine & Locale.Str("SRV_CONTINUA"),
               Locale.Str("SRV_CONF_A1") & nomBD & Locale.Str("SRV_CONF_A2") &
               Environment.NewLine & Locale.Str("SRV_NO_ELIM")),
            Locale.Str("SRV_CONF_TITOL"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If confirm <> DialogResult.Yes Then Return

        SetStatus(Locale.Str("SRV_EXPORTANT") & nomBD & "...", AppStyle.ColTextFeble)
        Dim res As SqlServerConnector.ResultatOperacio = Nothing

        Dim t As New Thread(Sub()
            res = SqlServerConnector.ExportarProjecte(_conn, _proyecto, mode)
        End Sub)
        t.IsBackground = True
        t.Start()
        Do While t.IsAlive
            Application.DoEvents()
            Thread.Sleep(50)
        Loop

        If res.OK Then
            SetStatus(Locale.Str("SRV_EXPORT_OK"), AppStyle.ColAccentSec)
            MessageBox.Show(res.ResumText(), Locale.Str("SRV_EXPORT_OK_TITOL"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        Else
            SetStatus("✗  " & res.MissatgeError, AppStyle.ColPerill)
            MessageBox.Show(Locale.Str("SRV_ERR_EXPORT") & Environment.NewLine & res.MissatgeError,
                            Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If
    End Sub

    ' ════════════════════════════════════════════════════════════
    ' ACCIÓ 3: ENVIAR PARTS
    ' ════════════════════════════════════════════════════════════
    Private Sub BtnEnviarParts_Click(s As Object, e As EventArgs)
        ' Recollir taules seleccionades
        Dim taulesSel As New List(Of TablaBBDD)()
        If _chkTaules IsNot Nothing Then
            For Each chk As CheckBox In _chkTaules
                If chk IsNot Nothing AndAlso chk.Checked Then
                    taulesSel.Add(_proyecto.Taules(CInt(chk.Tag)))
                End If
            Next
        End If

        If taulesSel.Count = 0 Then
            SetStatus(Locale.Str("SRV_ERR_SEL"), AppStyle.ColPerill)
            Return
        End If

        ' Relacions entre les taules seleccionades
        Dim ids As New HashSet(Of Integer)(taulesSel.Select(Function(tx) tx.Id))
        Dim relsSel As New List(Of RelacionBBDD)(
            _proyecto.Relacions.Where(
                Function(r) ids.Contains(r.TablaOrigenId) AndAlso
                            ids.Contains(r.TablaDestinoId)))

        Dim confirm As DialogResult = MessageBox.Show(
            Locale.Str("SRV_ENV0") & taulesSel.Count & Locale.Str("SRV_ENV1") & relsSel.Count &
            Locale.Str("SRV_ENV2") & _conn.BaseDades & "'." & Environment.NewLine &
            Locale.Str("SRV_ENV3"),
            Locale.Str("SRV_ENV_TITOL"),
            MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If confirm <> DialogResult.Yes Then Return

        SetStatus(Locale.Str("SRV_ENVIANT"), AppStyle.ColTextFeble)
        Dim res As SqlServerConnector.ResultatOperacio = Nothing

        Dim t As New Thread(Sub()
            res = SqlServerConnector.EnviarParts(_conn, taulesSel, relsSel)
        End Sub)
        t.IsBackground = True
        t.Start()
        Do While t.IsAlive
            Application.DoEvents()
            Thread.Sleep(50)
        Loop

        If res.OK Then
            SetStatus(Locale.Str("SRV_ENVIAT_OK") & res.TaulesCreades & Locale.Str("DLG_IMPORT_TAULES") &
                      res.RelacionsCreades & Locale.Str("DLG_IMPORT_RELACIONS"), AppStyle.ColAccentSec)
            MessageBox.Show(res.ResumText(), Locale.Str("SRV_ENVIAT_TITOL"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        Else
            SetStatus("✗  " & res.MissatgeError, AppStyle.ColPerill)
            MessageBox.Show(Locale.Str("SRV_ERR_ENVIAR") & Environment.NewLine & res.MissatgeError,
                            Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If
    End Sub

    Private Sub SetStatus(msg As String, color As Color)
        If _lblStatus IsNot Nothing Then
            _lblStatus.Text      = msg
            _lblStatus.ForeColor = color
        End If
    End Sub

    ' ── Helpers ──────────────────────────────────────────────────
    Private Shared Function CrearTitolBloc(text As String, x As Integer, y As Integer,
                                           w As Integer) As Label
        Dim lbl As New Label()
        lbl.Text      = text
        lbl.ForeColor = AppStyle.ColAccent
        lbl.Font      = AppStyle.FntCapc
        lbl.SetBounds(x, y, w, 18)
        Return lbl
    End Function

    Private Shared Function CrearSep(x As Integer, y As Integer, w As Integer) As Label
        Dim sep As New Label()
        sep.BackColor = Color.FromArgb(40, AppStyle.ColAccent.R,
                                       AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        sep.SetBounds(x, y, w, 1)
        Return sep
    End Function

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

End Class
