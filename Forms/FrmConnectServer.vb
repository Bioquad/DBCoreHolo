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
' FrmConnectServer.vb  —  DB-Core Holographic
' Diàleg de connexió a SQL Server en xarxa.
' Permet configurar servidor, autenticació i base de dades,
' testejar la connexió i llistar les BD disponibles.
' ============================================================
Public Class FrmConnectServer
    Inherits HoloForm

    ' ── Resultat exposat al cridant ──────────────────────────────
    Public Property ResultConnexio As SqlServerConnector.ConnexioServidor

    ' ── Controls ────────────────────────────────────────────────
    Private _txtServidor    As TextBox
    Private _txtPort        As TextBox
    Private _rdbWindows     As RadioButton
    Private _rdbSql         As RadioButton
    Private _txtUsuari      As TextBox
    Private _txtPass        As TextBox
    Private _lblUsuari      As Label
    Private _lblPass        As Label
    Private _cmbBD          As ComboBox
    Private _btnLlistarBD   As Button
    Private _chkEncrypt     As CheckBox
    Private _lblStatus      As Label
    Private _btnTest        As Button
    Private _btnConnectar   As Button
    Private _btnCancel      As Button

    ' Connexió entrada (per preomplir si hi havia una connexió prèvia)
    Private _connPrevia As SqlServerConnector.ConnexioServidor

    Public Sub New(Optional connPrevia As SqlServerConnector.ConnexioServidor = Nothing)
        MyBase.New()
        _connPrevia = connPrevia

        Me.Text          = Locale.Str("CON_TITOL")
        Me.ClientSize    = New Size(520, 390)
        Me.StartPosition = FormStartPosition.CenterParent
        AppStyle.AplicarEstilForm(Me)
        ConstruirUI()
        PreOmplir()
    End Sub

    Private Sub ConstruirUI()
        Const W As Integer = 496
        Const XL As Integer = 0
        Const XR As Integer = 260

        Dim pnlOuter As New Panel()
        pnlOuter.Dock      = DockStyle.Fill
        pnlOuter.BackColor = AppStyle.ColFons
        pnlOuter.Padding   = New Padding(11, 12, 11, 12)

        Dim pnl As New Panel()
        pnl.Dock      = DockStyle.Fill
        pnl.BackColor = AppStyle.ColFons
        pnlOuter.Controls.Add(pnl)

        Dim y As Integer = 0

        ' ── Capçalera ────────────────────────────────────────────
        Dim lblTit As New Label()
        lblTit.Text      = Locale.Str("CON_CAPC")
        lblTit.ForeColor = AppStyle.ColAccent
        lblTit.Font      = AppStyle.FntTitol
        lblTit.SetBounds(XL, y, W, 20)
        pnl.Controls.Add(lblTit)
        y += 26

        ' ── Servidor ─────────────────────────────────────────────
        pnl.Controls.Add(CrearLabel(Locale.Str("CON_SERVIDOR"), XL, y, 220))
        pnl.Controls.Add(CrearLabel(Locale.Str("CON_PORT"), XR, y, 100))
        y += 16

        _txtServidor = AppStyle.CrearTextBox()
        _txtServidor.SetBounds(XL, y, 224, 22)
        _txtServidor.Text = "localhost"
        pnl.Controls.Add(_txtServidor)

        _txtPort = AppStyle.CrearTextBox()
        _txtPort.SetBounds(XR, y, 90, 22)
        _txtPort.Text = "1433"
        pnl.Controls.Add(_txtPort)
        y += 30

        ' ── Separador ────────────────────────────────────────────
        pnl.Controls.Add(CrearSep(XL, y, W))
        y += 8

        ' ── Autenticació ─────────────────────────────────────────
        pnl.Controls.Add(CrearLabel(Locale.Str("CON_AUTH"), XL, y, W))
        y += 16

        _rdbWindows = New RadioButton()
        _rdbWindows.Text      = Locale.Str("CON_AUTH_WIN")
        _rdbWindows.ForeColor = AppStyle.ColTextPrinc
        _rdbWindows.BackColor = Color.Transparent
        _rdbWindows.Font      = AppStyle.FntPetit
        _rdbWindows.Checked   = True
        _rdbWindows.SetBounds(XL, y, 230, 20)
        AddHandler _rdbWindows.CheckedChanged, AddressOf Auth_Changed
        pnl.Controls.Add(_rdbWindows)

        _rdbSql = New RadioButton()
        _rdbSql.Text      = Locale.Str("CON_AUTH_SQL")
        _rdbSql.ForeColor = AppStyle.ColTextPrinc
        _rdbSql.BackColor = Color.Transparent
        _rdbSql.Font      = AppStyle.FntPetit
        _rdbSql.SetBounds(XR, y, 230, 20)
        AddHandler _rdbSql.CheckedChanged, AddressOf Auth_Changed
        pnl.Controls.Add(_rdbSql)
        y += 26

        _lblUsuari = CrearLabel(Locale.Str("CON_USUARI"), XL, y, 220)
        pnl.Controls.Add(_lblUsuari)
        _lblPass = CrearLabel(Locale.Str("CON_PASS"), XR, y, 220)
        pnl.Controls.Add(_lblPass)
        y += 16

        _txtUsuari = AppStyle.CrearTextBox()
        _txtUsuari.SetBounds(XL, y, 224, 22)
        _txtUsuari.Enabled = False
        pnl.Controls.Add(_txtUsuari)

        _txtPass = AppStyle.CrearTextBox()
        _txtPass.SetBounds(XR, y, 224, 22)
        _txtPass.PasswordChar = "●"c
        _txtPass.Enabled = False
        pnl.Controls.Add(_txtPass)
        y += 30

        ' ── Separador ────────────────────────────────────────────
        pnl.Controls.Add(CrearSep(XL, y, W))
        y += 8

        ' ── Base de dades ─────────────────────────────────────────
        pnl.Controls.Add(CrearLabel(Locale.Str("CON_BD"), XL, y, 380))
        y += 16

        _cmbBD = AppStyle.CrearComboBox()
        _cmbBD.SetBounds(XL, y, 380, 22)
        _cmbBD.DropDownStyle = ComboBoxStyle.DropDown
        pnl.Controls.Add(_cmbBD)

        _btnLlistarBD = AppStyle.CrearBotoCapc(Locale.Str("CON_LLISTAR"))
        _btnLlistarBD.SetBounds(386, y, 110, 22)
        AddHandler _btnLlistarBD.Click, AddressOf BtnLlistarBD_Click
        pnl.Controls.Add(_btnLlistarBD)
        y += 30

        ' ── Opcions addicionals ───────────────────────────────────
        _chkEncrypt = AppStyle.CrearCheckBox(Locale.Str("CON_ENCRYPT"))
        _chkEncrypt.SetBounds(XL, y, 300, 20)
        pnl.Controls.Add(_chkEncrypt)
        y += 28

        ' ── Separador ────────────────────────────────────────────
        pnl.Controls.Add(CrearSep(XL, y, W))
        y += 8

        ' ── Label d'estat ────────────────────────────────────────
        _lblStatus = New Label()
        _lblStatus.Text      = ""
        _lblStatus.ForeColor = AppStyle.ColTextFeble
        _lblStatus.Font      = AppStyle.FntMoltPetit
        _lblStatus.SetBounds(XL, y, W, 16)
        _lblStatus.AutoEllipsis = True
        pnl.Controls.Add(_lblStatus)
        y += 20

        ' ── Botons d'acció ───────────────────────────────────────
        _btnTest = AppStyle.CrearBotoCapc(Locale.Str("CON_TEST"))
        _btnTest.Width  = 100
        _btnTest.Height = 24
        _btnTest.SetBounds(XL, y, 100, 24)
        AddHandler _btnTest.Click, AddressOf BtnTest_Click
        pnl.Controls.Add(_btnTest)

        _btnCancel = AppStyle.CrearBotoPerill(Locale.Str("BTN_X_CANCEL"))
        _btnCancel.Width  = 130
        _btnCancel.Height = 24
        _btnCancel.SetBounds(W - 130, y, 130, 24)
        AddHandler _btnCancel.Click, Sub(s, e) Me.DialogResult = DialogResult.Cancel
        pnl.Controls.Add(_btnCancel)

        _btnConnectar = AppStyle.CrearBotoAccio(Locale.Str("CON_CONNECTAR"))
        _btnConnectar.Width  = 140
        _btnConnectar.Height = 24
        _btnConnectar.SetBounds(W - 275, y, 140, 24)
        AddHandler _btnConnectar.Click, AddressOf BtnConnectar_Click
        pnl.Controls.Add(_btnConnectar)

        Me.Controls.Add(pnlOuter)
    End Sub

    Private Sub PreOmplir()
        If _connPrevia Is Nothing Then Return
        _txtServidor.Text = _connPrevia.Servidor
        _txtPort.Text     = _connPrevia.Port.ToString()
        _cmbBD.Text       = _connPrevia.BaseDades
        _chkEncrypt.Checked = _connPrevia.Encrypt
        If _connPrevia.AuthWindows Then
            _rdbWindows.Checked = True
        Else
            _rdbSql.Checked  = True
            _txtUsuari.Text  = _connPrevia.Usuari
            ' No preomplim la contrasenya per seguretat
        End If
    End Sub

    ' ── Toggle camps usuari/pass ─────────────────────────────────
    Private Sub Auth_Changed(s As Object, e As EventArgs)
        Dim sqlAuth As Boolean = _rdbSql.Checked
        _txtUsuari.Enabled  = sqlAuth
        _txtPass.Enabled    = sqlAuth
        _lblUsuari.ForeColor = If(sqlAuth, AppStyle.ColTextSec, AppStyle.ColTextFeble)
        _lblPass.ForeColor   = If(sqlAuth, AppStyle.ColTextSec, AppStyle.ColTextFeble)
    End Sub

    ' ── Construir ConnexioServidor des dels controls ─────────────
    Private Function ConstruirConnexio() As SqlServerConnector.ConnexioServidor
        Dim c As New SqlServerConnector.ConnexioServidor()
        c.Servidor    = _txtServidor.Text.Trim()
        c.BaseDades   = _cmbBD.Text.Trim()
        c.AuthWindows = _rdbWindows.Checked
        c.Usuari      = _txtUsuari.Text.Trim()
        c.Contrasenya = _txtPass.Text
        c.Encrypt     = _chkEncrypt.Checked
        Dim p As Integer = 1433
        If Integer.TryParse(_txtPort.Text.Trim(), p) Then c.Port = p
        Return c
    End Function

    ' ── Test de connexió ─────────────────────────────────────────
    Private Sub BtnTest_Click(s As Object, e As EventArgs)
        SetStatus(Locale.Str("CON_CONNECTANT"), AppStyle.ColTextFeble)
        _btnTest.Enabled      = False
        _btnConnectar.Enabled = False
        Application.DoEvents()

        Dim conn As SqlServerConnector.ConnexioServidor = ConstruirConnexio()
        Dim err As String = ""

        Dim t As New Thread(Sub()
            err = SqlServerConnector.TestConnexio(conn)
        End Sub)
        t.IsBackground = True
        t.Start()
        t.Join(TimeSpan.FromSeconds(conn.TimeoutSeg + 2))

        If t.IsAlive Then
            t.Interrupt()
            err = Locale.Str("CON_TIMEOUT1") & conn.TimeoutSeg & "s)."
        End If

        If String.IsNullOrEmpty(err) Then
            SetStatus(Locale.Str("CON_OK") & conn.Servidor, AppStyle.ColAccentSec)
        Else
            SetStatus("✗  " & err, AppStyle.ColPerill)
        End If

        _btnTest.Enabled      = True
        _btnConnectar.Enabled = True
    End Sub

    ' ── Llistar bases de dades ───────────────────────────────────
    Private Sub BtnLlistarBD_Click(s As Object, e As EventArgs)
        SetStatus(Locale.Str("CON_LLEGINT_BD"), AppStyle.ColTextFeble)
        _btnLlistarBD.Enabled = False
        Application.DoEvents()

        Dim conn As SqlServerConnector.ConnexioServidor = ConstruirConnexio()
        Dim llista As List(Of String) = Nothing

        Dim t As New Thread(Sub()
            llista = SqlServerConnector.LlistarBD(conn)
        End Sub)
        t.IsBackground = True
        t.Start()
        t.Join(TimeSpan.FromSeconds(conn.TimeoutSeg + 2))

        _btnLlistarBD.Enabled = True

        If llista IsNot Nothing AndAlso llista.Count > 0 Then
            Dim textAntic As String = _cmbBD.Text
            _cmbBD.Items.Clear()
            For Each bd As String In llista
                _cmbBD.Items.Add(bd)
            Next
            If Not String.IsNullOrEmpty(textAntic) Then _cmbBD.Text = textAntic
            SetStatus(llista.Count & Locale.Str("CON_BD_TROBADES"), AppStyle.ColAccentSec)
        Else
            SetStatus(Locale.Str("CON_BD_ERROR"), AppStyle.ColPerill)
        End If
    End Sub

    ' ── Connectar ────────────────────────────────────────────────
    Private Sub BtnConnectar_Click(s As Object, e As EventArgs)
        Dim conn As SqlServerConnector.ConnexioServidor = ConstruirConnexio()

        If String.IsNullOrEmpty(conn.Servidor) Then
            SetStatus(Locale.Str("CON_ERR_SERVIDOR"), AppStyle.ColPerill)
            Return
        End If
        If String.IsNullOrEmpty(conn.BaseDades) Then
            SetStatus(Locale.Str("CON_ERR_BD"), AppStyle.ColPerill)
            Return
        End If
        If Not conn.AuthWindows AndAlso String.IsNullOrEmpty(conn.Usuari) Then
            SetStatus(Locale.Str("CON_ERR_USUARI"), AppStyle.ColPerill)
            Return
        End If

        SetStatus(Locale.Str("CON_VERIFICANT"), AppStyle.ColTextFeble)
        _btnConnectar.Enabled = False
        Application.DoEvents()

        Dim err As String = ""
        Dim t As New Thread(Sub()
            err = SqlServerConnector.TestConnexio(conn)
        End Sub)
        t.IsBackground = True
        t.Start()
        t.Join(TimeSpan.FromSeconds(conn.TimeoutSeg + 2))
        If t.IsAlive Then
            t.Interrupt()
            err = Locale.Str("CON_TIMEOUT2")
        End If

        _btnConnectar.Enabled = True

        If Not String.IsNullOrEmpty(err) Then
            SetStatus("✗  " & err, AppStyle.ColPerill)
            Return
        End If

        Me.ResultConnexio = conn
        Me.DialogResult   = DialogResult.OK
    End Sub

    Private Sub SetStatus(msg As String, color As Color)
        _lblStatus.Text      = msg
        _lblStatus.ForeColor = color
    End Sub

    ' ── Helpers UI ───────────────────────────────────────────────
    Private Shared Function CrearLabel(text As String, x As Integer, y As Integer,
                                       w As Integer) As Label
        Dim lbl As New Label()
        lbl.Text      = text
        lbl.ForeColor = AppStyle.ColTextFeble
        lbl.Font      = AppStyle.FntMoltPetit
        lbl.SetBounds(x, y, w, 14)
        lbl.BackColor = Color.Transparent
        Return lbl
    End Function

    Private Shared Function CrearSep(x As Integer, y As Integer, w As Integer) As Label
        Dim sep As New Label()
        sep.BackColor = Color.FromArgb(40, AppStyle.ColAccent.R,
                                       AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        sep.SetBounds(x, y, w, 1)
        Return sep
    End Function

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.KeyCode = Keys.Escape Then Me.DialogResult = DialogResult.Cancel
        If e.KeyCode = Keys.Enter   Then BtnConnectar_Click(Me, EventArgs.Empty)
        MyBase.OnKeyDown(e)
    End Sub

End Class
