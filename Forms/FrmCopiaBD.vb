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
Imports System.Threading.Tasks
Imports System.Windows.Forms

' ============================================================
' FrmCopiaBD.vb — Còpia completa d'una base de dades (estructura,
' vistes, procediments, funcions, triggers i totes les dades) des
' d'un .mdf o d'un servidor cap a un .mdf nou o una BD nova d'un
' servidor. No cal obrir la base de dades al dissenyador.
' ============================================================
Public Class FrmCopiaBD
    Inherits HoloForm

    Private _origen As UbicacioBD
    Private _connDesti As SqlServerConnector.ConnexioServidor
    Private _connRecordada As SqlServerConnector.ConnexioServidor

    Private _lblOrigen As Label
    Private _rdbFitxer As RadioButton
    Private _rdbServidor As RadioButton
    Private _txtNom As TextBox
    Private _txtCarpeta As TextBox
    Private _btnCarpeta As Button
    Private _lblServidor As Label
    Private _btnServidor As Button
    Private _txtLog As TextBox
    Private _btnCopiar As Button
    Private _btnTancar As Button
    Private _ocupat As Boolean

    Public Sub New(Optional origen As UbicacioBD = Nothing,
                   Optional connServidor As SqlServerConnector.ConnexioServidor = Nothing)
        MyBase.New()
        Me.Text = Locale.Str("COP_TITOL")
        Me.ClientSize = New Size(640, 560)
        Me.MinimumSize = New Size(640, 500)
        AppStyle.AplicarEstilForm(Me)
        _connRecordada = connServidor
        ConstruirUI()
        If origen IsNot Nothing Then EstablirOrigen(origen)
        ActualitzarEstat()
    End Sub

    Private Sub ConstruirUI()
        Const W As Integer = 604

        ' ── Botons ──────────────────────────────────────────────
        Dim pnlBotons As New Panel() With {.Dock = DockStyle.Bottom, .Height = 54, .BackColor = AppStyle.ColFonsMig}
        pnlBotons.Controls.Add(New Label() With {.Height = 1, .Dock = DockStyle.Top, .BackColor = AppStyle.ColVora})
        _btnCopiar = AppStyle.CrearBotoAccio(Locale.Str("COP_COPIAR"))
        _btnCopiar.SetBounds(640 - 160 - 145 - 12, 14, 160, AppStyle.AltBotoNormal)
        _btnCopiar.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        AddHandler _btnCopiar.Click, AddressOf BtnCopiar_Click
        _btnTancar = AppStyle.CrearBotoPerill(Locale.Str("BTN_TANCAR"))
        _btnTancar.SetBounds(640 - 140 - 8, 14, 140, AppStyle.AltBotoNormal)
        _btnTancar.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        AddHandler _btnTancar.Click, Sub(s, e) Me.Close()
        pnlBotons.Controls.AddRange({_btnCopiar, _btnTancar})

        Dim pnlOuter As New Panel() With {.Dock = DockStyle.Fill, .BackColor = AppStyle.ColFons,
                                          .Padding = New Padding(17, 12, 17, 8)}
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill, .BackColor = AppStyle.ColFons}
        pnlOuter.Controls.Add(pnl)

        ' ── ORIGEN ──────────────────────────────────────────────
        pnl.Controls.Add(Titol(Locale.Str("COP_ORIGEN"), 0))
        _lblOrigen = New Label() With {.ForeColor = AppStyle.ColAccentSec, .Font = AppStyle.FntPetit,
                                       .AutoEllipsis = True, .TextAlign = ContentAlignment.MiddleLeft,
                                       .BackColor = Color.Transparent, .Text = Locale.Str("ORI_SENSE_CONN")}
        _lblOrigen.SetBounds(0, 22, W - 120, 24)
        Dim btnOrigen As Button = AppStyle.CrearBotoCapc(Locale.Str("COP_TRIAR"))
        btnOrigen.SetBounds(W - 110, 22, 110, 24)
        AddHandler btnOrigen.Click, AddressOf BtnOrigen_Click
        pnl.Controls.AddRange({_lblOrigen, btnOrigen})

        ' ── DESTÍ ───────────────────────────────────────────────
        pnl.Controls.Add(Titol(Locale.Str("COP_DESTI"), 60))
        pnl.Controls.Add(Etiqueta(Locale.Str("COP_NOM"), 82))
        _txtNom = AppStyle.CrearTextBox()
        _txtNom.SetBounds(0, 100, W, 22)
        pnl.Controls.Add(_txtNom)

        _rdbFitxer = Radio(Locale.Str("COP_DESTI_FITXER"), 132)
        _rdbFitxer.Checked = True
        AddHandler _rdbFitxer.CheckedChanged, Sub(s, e) ActualitzarEstat()
        pnl.Controls.Add(_rdbFitxer)
        pnl.Controls.Add(Etiqueta(Locale.Str("COP_CARPETA"), 156, 20))
        _txtCarpeta = AppStyle.CrearTextBox()
        _txtCarpeta.Text = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DB-Core")
        _txtCarpeta.SetBounds(20, 174, W - 20 - 50, 22)
        _btnCarpeta = AppStyle.CrearBotoCapc("...")
        _btnCarpeta.SetBounds(W - 44, 173, 44, 24)
        AddHandler _btnCarpeta.Click, AddressOf BtnCarpeta_Click
        pnl.Controls.AddRange({_txtCarpeta, _btnCarpeta})

        _rdbServidor = Radio(Locale.Str("COP_DESTI_SRV"), 208)
        pnl.Controls.Add(_rdbServidor)
        _lblServidor = New Label() With {.ForeColor = AppStyle.ColAccentSec, .Font = AppStyle.FntPetit,
                                         .AutoEllipsis = True, .TextAlign = ContentAlignment.MiddleLeft,
                                         .BackColor = Color.Transparent}
        _lblServidor.SetBounds(20, 232, W - 20 - 120, 24)
        _btnServidor = AppStyle.CrearBotoCapc(Locale.Str("ORI_CONNECTAR"))
        _btnServidor.SetBounds(W - 110, 232, 110, 24)
        AddHandler _btnServidor.Click, AddressOf BtnServidor_Click
        pnl.Controls.AddRange({_lblServidor, _btnServidor})

        Dim lblNota As New Label() With {.Text = Locale.Str("COP_NOTA"), .ForeColor = AppStyle.ColTextFeble,
                                         .Font = AppStyle.FntMoltPetit, .BackColor = Color.Transparent}
        lblNota.SetBounds(0, 266, W, 30)
        pnl.Controls.Add(lblNota)

        ' ── Registre del procés ─────────────────────────────────
        _txtLog = New TextBox() With {.Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Vertical,
                                      .BackColor = AppStyle.ColFonsMig, .ForeColor = AppStyle.ColAccentSec,
                                      .Font = New Font("Courier New", 8), .BorderStyle = BorderStyle.None,
                                      .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right}
        _txtLog.SetBounds(0, 300, W, 150)
        pnl.Controls.Add(_txtLog)

        Me.Controls.Add(pnlOuter)
        Me.Controls.Add(pnlBotons)
    End Sub

    Private Shared Function Titol(text As String, y As Integer) As Label
        Dim l As New Label() With {.Text = text, .ForeColor = AppStyle.ColAccent, .Font = AppStyle.FntCapc,
                                   .BackColor = Color.Transparent}
        l.SetBounds(0, y, 400, 18)
        Return l
    End Function

    Private Shared Function Etiqueta(text As String, y As Integer, Optional x As Integer = 0) As Label
        Dim l As New Label() With {.Text = text, .ForeColor = AppStyle.ColTextFeble, .Font = AppStyle.FntMoltPetit,
                                   .BackColor = Color.Transparent}
        l.SetBounds(x, y, 500, 16)
        Return l
    End Function

    Private Shared Function Radio(text As String, y As Integer) As RadioButton
        Dim r As New RadioButton() With {.Text = text, .ForeColor = AppStyle.ColTextPrinc,
                                         .BackColor = Color.Transparent, .Font = AppStyle.FntNormal}
        r.SetBounds(0, y, 500, 20)
        Return r
    End Function

    Private Sub ActualitzarEstat()
        Dim fitxer As Boolean = _rdbFitxer.Checked
        _txtCarpeta.Enabled = fitxer AndAlso Not _ocupat
        _btnCarpeta.Enabled = fitxer AndAlso Not _ocupat
        _btnServidor.Enabled = Not fitxer AndAlso Not _ocupat
        _lblServidor.Text = If(_connDesti Is Nothing, Locale.Str("ORI_SENSE_CONN"),
                               _connDesti.Servidor & If(_connDesti.AuthWindows, "  (Windows Auth)", "  (" & _connDesti.Usuari & ")"))
        _btnCopiar.Enabled = Not _ocupat
        _btnTancar.Enabled = Not _ocupat
    End Sub

    Private Sub EstablirOrigen(ub As UbicacioBD)
        _origen = ub
        _lblOrigen.Text = ub.ToString()
        _txtNom.Text = ub.NomBD & "_copia"
    End Sub

    Private Sub BtnOrigen_Click(s As Object, e As EventArgs)
        Using dlg As New FrmOrigenBD(_connRecordada)
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                EstablirOrigen(dlg.Resultat)
                If dlg.ConnexioServidor IsNot Nothing Then _connRecordada = dlg.ConnexioServidor
            End If
        End Using
    End Sub

    Private Sub BtnCarpeta_Click(s As Object, e As EventArgs)
        Using dlg As New FolderBrowserDialog()
            dlg.SelectedPath = _txtCarpeta.Text
            dlg.ShowNewFolderButton = True
            If dlg.ShowDialog(Me) = DialogResult.OK Then _txtCarpeta.Text = dlg.SelectedPath
        End Using
    End Sub

    Private Sub BtnServidor_Click(s As Object, e As EventArgs)
        Using dlg As New FrmConnectServer(If(_connDesti, _connRecordada), nomesServidor:=True)
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                _connDesti = dlg.ResultConnexio
                ActualitzarEstat()
            End If
        End Using
    End Sub

    Private Async Sub BtnCopiar_Click(s As Object, e As EventArgs)
        If _origen Is Nothing Then
            Afegir("⚠  " & Locale.Str("COP_ERR_ORIGEN"))
            Return
        End If
        Dim nom As String = _txtNom.Text.Trim()
        Dim desti As UbicacioBD
        If _rdbFitxer.Checked Then
            If nom = "" OrElse _txtCarpeta.Text.Trim() = "" Then
                Afegir("⚠  " & Locale.Str("COP_ERR_DESTI"))
                Return
            End If
            Try
                desti = UbicacioBD.Fitxer(IO.Path.Combine(_txtCarpeta.Text.Trim(), nom & ".mdf"))
            Catch ex As Exception
                Afegir("⚠  " & ex.Message)
                Return
            End Try
        Else
            If nom = "" OrElse _connDesti Is Nothing Then
                Afegir("⚠  " & Locale.Str("COP_ERR_DESTI"))
                Return
            End If
            Dim c As SqlServerConnector.ConnexioServidor = _connDesti.Copia()
            c.BaseDades = nom
            desti = UbicacioBD.Servidor(c)
        End If

        _txtLog.Clear()
        _ocupat = True
        ActualitzarEstat()
        UseWaitCursor = True
        Dim progres As New Progress(Of String)(AddressOf Afegir)
        Dim origen As UbicacioBD = _origen
        Dim res As CopiaBaseDades.ResultatCopia = Nothing
        Try
            res = Await Task.Run(Function() CopiaBaseDades.Copiar(origen, desti, progres))
        Finally
            _ocupat = False
            UseWaitCursor = False
            ActualitzarEstat()
        End Try

        If res.OK Then
            Dim msg As String = String.Format(Locale.Str("COP_RESUM"), res.Metode, res.Taules,
                                              res.Files.ToString("N0"), res.Objectes) &
                                Environment.NewLine & desti.ToString()
            If res.Advertencies.Count > 0 Then
                Afegir(Locale.Str("COP_AVISOS"))
                For Each a As String In res.Advertencies
                    Afegir("  · " & a)
                Next
                msg &= Environment.NewLine & Environment.NewLine & Locale.Str("COP_AVISOS") & " " & res.Advertencies.Count
            End If
            MessageBox.Show(msg, Locale.Str("COP_OK"), MessageBoxButtons.OK, MessageBoxIcon.Information)
        Else
            MessageBox.Show(Locale.Str("COP_ERR") & Environment.NewLine & res.MissatgeError,
                            Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If
    End Sub

    Private Sub Afegir(linia As String)
        _txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") & "  " & linia & Environment.NewLine)
    End Sub

    ' No es pot tancar mentre es copia (la còpia continuaria sense control)
    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If _ocupat Then
            e.Cancel = True
            Return
        End If
        MyBase.OnFormClosing(e)
    End Sub

End Class
