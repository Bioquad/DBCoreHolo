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
' FrmOrigenBD.vb — Tria d'una base de dades existent: un fitxer
' .mdf del disc o una base de dades d'un servidor SQL Server.
' Retorna una UbicacioBD (Resultat).
' ============================================================
Public Class FrmOrigenBD
    Inherits HoloForm

    Private _rdbFitxer As RadioButton
    Private _rdbServidor As RadioButton
    Private _txtFitxer As TextBox
    Private _btnExaminar As Button
    Private _btnConnectar As Button
    Private _lblConnexio As Label
    Private _lblError As Label
    Private _conn As SqlServerConnector.ConnexioServidor

    Public Property Resultat As UbicacioBD

    ''' <param name="connPrevia">Connexió a servidor ja coneguda (es proposa per defecte)</param>
    Public Sub New(Optional connPrevia As SqlServerConnector.ConnexioServidor = Nothing,
                   Optional titol As String = Nothing)
        MyBase.New()
        Me.Text = If(titol, Locale.Str("ORI_TITOL"))
        Me.ClientSize = New Size(520, 260)
        AppStyle.AplicarEstilForm(Me)
        _conn = connPrevia
        ConstruirUI()
        If _conn IsNot Nothing Then _rdbServidor.Checked = True
        ActualitzarEstat()
    End Sub

    Private Sub ConstruirUI()
        Const W As Integer = 484

        Dim pnlBotons As New Panel() With {.Dock = DockStyle.Bottom, .Height = 54, .BackColor = AppStyle.ColFonsMig}
        pnlBotons.Controls.Add(New Label() With {.Height = 1, .Dock = DockStyle.Top, .BackColor = AppStyle.ColVora})

        Dim btnOK As Button = AppStyle.CrearBotoAccio(Locale.Str("ORI_ACCEPTAR"))
        btnOK.SetBounds(520 - 160 - 145 - 12, 14, 160, AppStyle.AltBotoNormal)
        AddHandler btnOK.Click, AddressOf BtnOK_Click
        pnlBotons.Controls.Add(btnOK)

        Dim btnCancel As Button = AppStyle.CrearBotoPerill(Locale.Str("BTN_X_CANCEL"))
        btnCancel.SetBounds(520 - 140 - 8, 14, 140, AppStyle.AltBotoNormal)
        AddHandler btnCancel.Click, Sub(s, e) Me.DialogResult = DialogResult.Cancel
        pnlBotons.Controls.Add(btnCancel)

        _lblError = New Label() With {
            .Dock = DockStyle.Bottom, .Height = 20, .ForeColor = AppStyle.ColPerill, .Font = AppStyle.FntPetit,
            .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(10, 0, 0, 0),
            .BackColor = AppStyle.ColFons, .Visible = False}

        Dim pnlOuter As New Panel() With {.Dock = DockStyle.Fill, .BackColor = AppStyle.ColFons,
                                          .Padding = New Padding(17, 14, 17, 6)}
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill, .BackColor = AppStyle.ColFons}
        pnlOuter.Controls.Add(pnl)

        ' ── Fitxer .mdf ─────────────────────────────────────────
        _rdbFitxer = CrearRadio(Locale.Str("ORI_FITXER"))
        _rdbFitxer.Checked = True
        _rdbFitxer.SetBounds(0, 0, W, 20)
        AddHandler _rdbFitxer.CheckedChanged, Sub(s, e) ActualitzarEstat()
        pnl.Controls.Add(_rdbFitxer)

        _txtFitxer = AppStyle.CrearTextBox()
        _txtFitxer.SetBounds(20, 24, W - 20 - 116, 22)
        pnl.Controls.Add(_txtFitxer)

        _btnExaminar = AppStyle.CrearBotoCapc(Locale.Str("ORI_EXAMINAR"))
        _btnExaminar.SetBounds(W - 110, 23, 110, 24)
        AddHandler _btnExaminar.Click, AddressOf BtnExaminar_Click
        pnl.Controls.Add(_btnExaminar)

        ' ── Servidor ────────────────────────────────────────────
        _rdbServidor = CrearRadio(Locale.Str("ORI_SERVIDOR"))
        _rdbServidor.SetBounds(0, 64, W, 20)
        pnl.Controls.Add(_rdbServidor)

        _lblConnexio = New Label() With {.ForeColor = AppStyle.ColAccentSec, .Font = AppStyle.FntPetit,
                                         .BackColor = Color.Transparent, .AutoEllipsis = True,
                                         .TextAlign = ContentAlignment.MiddleLeft}
        _lblConnexio.SetBounds(20, 88, W - 20 - 116, 24)
        pnl.Controls.Add(_lblConnexio)

        _btnConnectar = AppStyle.CrearBotoCapc(Locale.Str("ORI_CONNECTAR"))
        _btnConnectar.SetBounds(W - 110, 88, 110, 24)
        AddHandler _btnConnectar.Click, AddressOf BtnConnectar_Click
        pnl.Controls.Add(_btnConnectar)

        Me.Controls.Add(pnlOuter)
        Me.Controls.Add(_lblError)
        Me.Controls.Add(pnlBotons)
    End Sub

    Private Shared Function CrearRadio(text As String) As RadioButton
        Return New RadioButton() With {.Text = text, .ForeColor = AppStyle.ColTextPrinc,
                                       .BackColor = Color.Transparent, .Font = AppStyle.FntNormal}
    End Function

    Private Sub ActualitzarEstat()
        Dim fitxer As Boolean = _rdbFitxer.Checked
        _txtFitxer.Enabled = fitxer
        _btnExaminar.Enabled = fitxer
        _btnConnectar.Enabled = Not fitxer
        _lblConnexio.Text = If(_conn Is Nothing, Locale.Str("ORI_SENSE_CONN"), _conn.ToString())
    End Sub

    Private Sub BtnExaminar_Click(s As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Filter = Locale.Str("DLG_FILTRE_MDF")
            If _txtFitxer.Text.Trim() <> "" Then
                Try
                    dlg.InitialDirectory = IO.Path.GetDirectoryName(_txtFitxer.Text.Trim())
                Catch
                End Try
            End If
            If dlg.ShowDialog(Me) = DialogResult.OK Then _txtFitxer.Text = dlg.FileName
        End Using
    End Sub

    Private Sub BtnConnectar_Click(s As Object, e As EventArgs)
        Using dlg As New FrmConnectServer(_conn)
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                _conn = dlg.ResultConnexio
                ActualitzarEstat()
            End If
        End Using
    End Sub

    Private Sub BtnOK_Click(s As Object, e As EventArgs)
        _lblError.Visible = False
        If _rdbFitxer.Checked Then
            Dim ruta As String = _txtFitxer.Text.Trim()
            If ruta = "" OrElse Not IO.File.Exists(ruta) Then
                MostrarError(Locale.Str("ORI_ERR_FITXER"))
                Return
            End If
            Resultat = UbicacioBD.Fitxer(ruta)
        Else
            If _conn Is Nothing OrElse String.IsNullOrWhiteSpace(_conn.BaseDades) Then
                MostrarError(Locale.Str("ORI_ERR_SERVIDOR"))
                Return
            End If
            Resultat = UbicacioBD.Servidor(_conn)
        End If
        Me.DialogResult = DialogResult.OK
    End Sub

    Private Sub MostrarError(msg As String)
        _lblError.Text = "⚠  " & msg
        _lblError.Visible = True
    End Sub

    ''' <summary>Connexió de servidor triada (per recordar-la a la propera vegada).</summary>
    Public ReadOnly Property ConnexioServidor As SqlServerConnector.ConnexioServidor
        Get
            Return _conn
        End Get
    End Property

End Class
