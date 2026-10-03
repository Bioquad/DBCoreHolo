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
Imports System.Collections.Generic
Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.Data.SqlClient

' ============================================================
' FrmConsultaDades.vb — Visor de dades d'una base de dades
' existent (fitxer .mdf o servidor):
'   - Llista de taules i vistes; es poden triar una o diverses i
'     cadascuna s'obre en una pestanya amb les seves files.
'   - Pestanya "Consulta SQL" per escriure consultes lliures
'     (SELECT, JOIN...) en mode només lectura.
'   - Exportació de la pestanya activa a CSV.
' Clic amb el botó del mig sobre una pestanya per tancar-la.
' ============================================================
Public Class FrmConsultaDades
    Inherits HoloForm

    Private ReadOnly _ub As UbicacioBD
    Private _lstObjectes As CheckedListBox
    Private _txtLimit As TextBox
    Private _tabs As TabControl
    Private _tabSql As TabPage
    Private _txtSql As TextBox
    Private _tabsResultat As TabControl
    Private _lblEstat As Label

    Public Sub New(ub As UbicacioBD)
        MyBase.New()
        _ub = ub
        Me.Text = Locale.Str("VIS_TITOL") & ub.ToString()
        Me.Size = New Size(1100, 680)
        Me.MinimumSize = New Size(760, 420)
        Me.KeyPreview = True
        AppStyle.AplicarEstilForm(Me)
        ConstruirUI()
    End Sub

    ' ════════════════════════════════════════════════════════
    ' INTERFÍCIE
    ' ════════════════════════════════════════════════════════
    Private Sub ConstruirUI()
        ' ── Barra inferior ──────────────────────────────────────
        Dim pnlBaix As New Panel() With {.Dock = DockStyle.Bottom, .Height = 40, .BackColor = AppStyle.ColFonsMig}
        _lblEstat = New Label() With {.Dock = DockStyle.Fill, .ForeColor = AppStyle.ColTextFeble, .Font = AppStyle.FntPetit,
                                      .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(10, 0, 0, 0),
                                      .AutoEllipsis = True}
        Dim btnTancar As Button = AppStyle.CrearBotoPerill(Locale.Str("BTN_TANCAR"))
        btnTancar.Dock = DockStyle.Right
        btnTancar.Width = 110
        AddHandler btnTancar.Click, Sub(s, e) Me.Close()
        Dim btnCsv As Button = AppStyle.CrearBotoCapc(Locale.Str("VIS_EXPORTAR"))
        btnCsv.Dock = DockStyle.Right
        btnCsv.Width = 130
        AddHandler btnCsv.Click, AddressOf BtnCsv_Click
        pnlBaix.Controls.Add(_lblEstat)
        pnlBaix.Controls.Add(btnCsv)
        pnlBaix.Controls.Add(btnTancar)

        ' ── Panell esquerre: taules i vistes ───────────────────
        Dim pnlEsq As New Panel() With {.Dock = DockStyle.Left, .Width = 280, .BackColor = AppStyle.ColFons,
                                        .Padding = New Padding(8, 8, 4, 8)}
        Dim lblObj As New Label() With {.Text = Locale.Str("VIS_OBJECTES"), .Dock = DockStyle.Top, .Height = 20,
                                        .ForeColor = AppStyle.ColAccent, .Font = AppStyle.FntCapc}
        _lstObjectes = New CheckedListBox() With {
            .Dock = DockStyle.Fill, .CheckOnClick = True, .IntegralHeight = False,
            .BackColor = AppStyle.ColFonsMig, .ForeColor = AppStyle.ColTextPrinc,
            .BorderStyle = BorderStyle.None, .Font = AppStyle.FntPetit}
        AddHandler _lstObjectes.DoubleClick, AddressOf LstObjectes_DoubleClick

        Dim pnlAccions As New Panel() With {.Dock = DockStyle.Bottom, .Height = 92, .BackColor = AppStyle.ColFons}
        Dim btnTotes As Button = AppStyle.CrearBotoCapc(Locale.Str("VIS_TOTES"))
        btnTotes.SetBounds(0, 6, 128, 24)
        AddHandler btnTotes.Click, Sub(s, e) MarcarTotes(True)
        Dim btnCap As Button = AppStyle.CrearBotoCapc(Locale.Str("VIS_CAP"))
        btnCap.SetBounds(136, 6, 128, 24)
        AddHandler btnCap.Click, Sub(s, e) MarcarTotes(False)
        Dim lblLimit As New Label() With {.Text = Locale.Str("VIS_LIMIT"), .ForeColor = AppStyle.ColTextFeble,
                                          .Font = AppStyle.FntPetit, .TextAlign = ContentAlignment.MiddleLeft}
        lblLimit.SetBounds(0, 36, 128, 22)
        _txtLimit = AppStyle.CrearTextBox()
        _txtLimit.Text = "1000"
        _txtLimit.SetBounds(136, 36, 128, 22)
        Dim btnConsultar As Button = AppStyle.CrearBotoAccio(Locale.Str("VIS_CONSULTAR"))
        btnConsultar.SetBounds(0, 64, 264, 26)
        AddHandler btnConsultar.Click, AddressOf BtnConsultar_Click
        pnlAccions.Controls.AddRange({btnTotes, btnCap, lblLimit, _txtLimit, btnConsultar})

        pnlEsq.Controls.Add(_lstObjectes)
        pnlEsq.Controls.Add(pnlAccions)
        pnlEsq.Controls.Add(lblObj)

        ' ── Pestanyes de resultats ──────────────────────────────
        _tabs = New TabControl() With {.Dock = DockStyle.Fill}
        AddHandler _tabs.MouseUp, AddressOf Tabs_MouseUp
        AddHandler _tabs.SelectedIndexChanged, Sub(s, e) ActualitzarEstatPestanya()

        _tabSql = New TabPage(Locale.Str("VIS_SQL_TAB"))
        ' La mida s'ha de fixar abans de SplitterDistance: amb la mida per defecte
        ' (150x100) un valor de 150 és fora de rang i llança una excepció
        Dim split As New SplitContainer()
        split.Size = New Size(800, 500)
        split.Orientation = Orientation.Horizontal
        split.SplitterDistance = 150
        split.Dock = DockStyle.Fill
        split.BackColor = AppStyle.ColFons
        _txtSql = New TextBox() With {.Multiline = True, .Dock = DockStyle.Fill, .ScrollBars = ScrollBars.Both,
                                      .WordWrap = False, .AcceptsTab = True, .Font = New Font("Courier New", 10),
                                      .BackColor = AppStyle.ColFonsMig, .ForeColor = AppStyle.ColAccentSec,
                                      .BorderStyle = BorderStyle.None}
        Dim pnlSqlBotons As New Panel() With {.Dock = DockStyle.Bottom, .Height = 32, .BackColor = AppStyle.ColFons}
        Dim btnExec As Button = AppStyle.CrearBotoAccio(Locale.Str("VIS_EXECUTAR"))
        btnExec.SetBounds(4, 4, 150, 24)
        AddHandler btnExec.Click, AddressOf BtnExecutar_Click
        Dim lblNota As New Label() With {.Text = Locale.Str("VIS_SQL_NOTA"), .ForeColor = AppStyle.ColTextFeble,
                                         .Font = AppStyle.FntMoltPetit, .TextAlign = ContentAlignment.MiddleLeft,
                                         .AutoSize = False}
        lblNota.SetBounds(164, 4, 600, 24)
        pnlSqlBotons.Controls.AddRange({btnExec, lblNota})
        split.Panel1.Controls.Add(_txtSql)
        split.Panel1.Controls.Add(pnlSqlBotons)
        _tabsResultat = New TabControl() With {.Dock = DockStyle.Fill}
        split.Panel2.Controls.Add(_tabsResultat)
        _tabSql.Controls.Add(split)
        _tabs.TabPages.Add(_tabSql)

        Me.Controls.Add(_tabs)
        Me.Controls.Add(pnlEsq)
        Me.Controls.Add(pnlBaix)
        AppStyle.AplicarEstilTabControl(_tabs)
        AppStyle.AplicarEstilTabControl(_tabsResultat)
    End Sub

    Protected Overrides Async Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        SetEstat(Locale.Str("VIS_CARREGANT"))
        Dim objectes As List(Of ConsultaDades.ObjecteDades) = Nothing
        Dim err As String = Nothing
        Await OperacioLlarga.ExecutarAsync(Me,
            Sub()
                Try
                    Using c As SqlConnection = _ub.ObrirConnexio()
                        objectes = ConsultaDades.LlistarObjectes(c)
                    End Using
                Catch ex As Exception
                    err = ex.Message
                End Try
            End Sub)
        If err IsNot Nothing Then
            SetEstat("✗  " & err, True)
            MessageBox.Show(err, Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        _lstObjectes.Items.Clear()
        For Each o As ConsultaDades.ObjecteDades In objectes
            _lstObjectes.Items.Add(o)
        Next
        SetEstat(_ub.ToString() & "  —  " & objectes.Count & " " & Locale.Str("VIS_OBJECTES").ToLowerInvariant())
    End Sub

    ' ════════════════════════════════════════════════════════
    ' ACCIONS
    ' ════════════════════════════════════════════════════════
    Private Sub BtnConsultar_Click(s As Object, e As EventArgs)
        Dim sel As New List(Of ConsultaDades.ObjecteDades)()
        For Each o As Object In _lstObjectes.CheckedItems
            sel.Add(DirectCast(o, ConsultaDades.ObjecteDades))
        Next
        If sel.Count = 0 AndAlso _lstObjectes.SelectedItem IsNot Nothing Then
            sel.Add(DirectCast(_lstObjectes.SelectedItem, ConsultaDades.ObjecteDades))
        End If
        Consultar(sel)
    End Sub

    ' Doble clic: obre només l'element clicat (encara que n'hi hagi d'altres marcats)
    Private Sub LstObjectes_DoubleClick(s As Object, e As EventArgs)
        If _lstObjectes.SelectedItem Is Nothing Then Return
        Consultar(New List(Of ConsultaDades.ObjecteDades) From {
            DirectCast(_lstObjectes.SelectedItem, ConsultaDades.ObjecteDades)})
    End Sub

    Private Async Sub Consultar(sel As List(Of ConsultaDades.ObjecteDades))
        If sel.Count = 0 Then
            SetEstat(Locale.Str("VIS_ERR_SEL"), True)
            Return
        End If

        Dim limit As Integer = LlegirLimit()
        Dim resultats As New List(Of Tuple(Of ConsultaDades.ObjecteDades, DataTable, Long))()
        Dim err As String = Nothing
        SetEstat(Locale.Str("VIS_CARREGANT"))
        Await OperacioLlarga.ExecutarAsync(Me,
            Sub()
                Try
                    Using c As SqlConnection = _ub.ObrirConnexio()
                        For Each o As ConsultaDades.ObjecteDades In sel
                            Dim dt As DataTable = ConsultaDades.CarregarObjecte(c, o, limit)
                            resultats.Add(Tuple.Create(o, dt, ConsultaDades.ComptarFiles(c, o)))
                        Next
                    End Using
                Catch ex As Exception
                    err = ex.Message
                End Try
            End Sub)

        Dim primera As TabPage = Nothing
        For Each r As Tuple(Of ConsultaDades.ObjecteDades, DataTable, Long) In resultats
            Dim tp As TabPage = ObrirPestanya(r.Item1.Esquema & "." & r.Item1.Nom, r.Item2, r.Item3)
            If primera Is Nothing Then primera = tp
        Next
        If primera IsNot Nothing Then _tabs.SelectedTab = primera
        If err IsNot Nothing Then
            SetEstat("✗  " & err, True)
        Else
            ActualitzarEstatPestanya()
        End If
    End Sub

    Private Async Sub BtnExecutar_Click(s As Object, e As EventArgs)
        Dim sql As String = If(_txtSql.SelectionLength > 0, _txtSql.SelectedText, _txtSql.Text)
        If String.IsNullOrWhiteSpace(sql) Then Return
        Dim limit As Integer = LlegirLimit()
        Dim res As ConsultaDades.ResultatConsulta = Nothing
        Dim err As String = Nothing
        SetEstat(Locale.Str("VIS_CARREGANT"))
        Await OperacioLlarga.ExecutarAsync(Me,
            Sub()
                Try
                    Using c As SqlConnection = _ub.ObrirConnexio()
                        res = ConsultaDades.ExecutarConsulta(c, sql, limit)
                    End Using
                Catch ex As Exception
                    err = ex.Message
                End Try
            End Sub)

        _tabsResultat.TabPages.Clear()
        If err IsNot Nothing Then
            Dim msg As String = err
            If err.Contains("Microsoft.SqlServer.Types") OrElse err.Contains("UDT") Then
                msg &= Environment.NewLine & Locale.Str("VIS_ERR_CLR")
            End If
            SetEstat("✗  " & err, True)
            MessageBox.Show(msg, Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        For Each dt As DataTable In res.Taules
            Dim tp As New TabPage(dt.TableName & " (" & dt.Rows.Count & ")") With {.Tag = dt}
            tp.Controls.Add(CrearGraella(dt))
            _tabsResultat.TabPages.Add(tp)
        Next
        If res.Taules.Count = 0 Then
            SetEstat(If(res.FilesAfectades >= 0,
                        String.Format(Locale.Str("VIS_AFECTADES"), res.FilesAfectades),
                        Locale.Str("VIS_SENSE_RESULTAT")))
        ElseIf res.Truncat Then
            SetEstat(String.Format(Locale.Str("VIS_TRUNCAT"), limit), True)
        Else
            SetEstat(res.Taules(0).TableName & ": " & res.Taules(0).Rows.Count)
        End If
    End Sub

    Private Sub BtnCsv_Click(s As Object, e As EventArgs)
        Dim dt As DataTable = TaulaActiva()
        If dt Is Nothing OrElse dt.Columns.Count = 0 Then
            SetEstat(Locale.Str("VIS_ERR_SENSE_DADES"), True)
            Return
        End If
        Using dlg As New SaveFileDialog()
            dlg.Filter = "CSV (*.csv)|*.csv"
            dlg.FileName = SenseCaractersInvalids(dt.TableName) & ".csv"
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                ConsultaDades.ExportarCsv(dt, dlg.FileName)
                SetEstat(Locale.Str("STATUS_EXPORTAT") & IO.Path.GetFileName(dlg.FileName))
            Catch ex As Exception
                MessageBox.Show(ex.Message, Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.KeyCode = Keys.F5 Then
            If _tabs.SelectedTab Is _tabSql Then
                BtnExecutar_Click(Me, EventArgs.Empty)
            Else
                BtnConsultar_Click(Me, EventArgs.Empty)
            End If
            e.Handled = True
        End If
        MyBase.OnKeyDown(e)
    End Sub

    ' Clic amb el botó del mig: tanca la pestanya (excepte la de SQL)
    Private Sub Tabs_MouseUp(s As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Middle Then Return
        For i As Integer = 0 To _tabs.TabCount - 1
            If _tabs.GetTabRect(i).Contains(e.Location) AndAlso _tabs.TabPages(i) IsNot _tabSql Then
                Dim tp As TabPage = _tabs.TabPages(i)
                _tabs.TabPages.Remove(tp)
                tp.Dispose()
                Exit For
            End If
        Next
    End Sub

    ' ════════════════════════════════════════════════════════
    ' HELPERS
    ' ════════════════════════════════════════════════════════
    Private Function ObrirPestanya(nom As String, dt As DataTable, total As Long) As TabPage
        ' Si ja hi és, es reemplaça el contingut
        Dim tp As TabPage = Nothing
        For Each t As TabPage In _tabs.TabPages
            If TypeOf t.Tag Is DataTable AndAlso DirectCast(t.Tag, DataTable).TableName = nom Then tp = t
        Next
        If tp Is Nothing Then
            tp = New TabPage()
            _tabs.TabPages.Add(tp)
        Else
            For Each c As Control In tp.Controls
                c.Dispose()
            Next
            tp.Controls.Clear()
        End If
        tp.Text = nom & " (" & dt.Rows.Count & ")"
        tp.Tag = dt
        tp.ToolTipText = String.Format(Locale.Str("VIS_FILES"), dt.Rows.Count.ToString("N0"), total.ToString("N0"))
        tp.Controls.Add(CrearGraella(dt))
        Return tp
    End Function

    Private Function CrearGraella(dt As DataTable) As DataGridView
        Dim g As New DataGridView() With {
            .Dock = DockStyle.Fill, .ReadOnly = True, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False,
            .AllowUserToResizeRows = False, .RowHeadersVisible = False, .BorderStyle = BorderStyle.None,
            .SelectionMode = DataGridViewSelectionMode.CellSelect, .EnableHeadersVisualStyles = False,
            .BackgroundColor = AppStyle.ColFons, .GridColor = Opac(AppStyle.ColVoraFeble, AppStyle.ColFonsMig),
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            .ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText}
        g.DefaultCellStyle.BackColor = AppStyle.ColFonsMig
        g.DefaultCellStyle.ForeColor = AppStyle.ColTextPrinc
        g.DefaultCellStyle.SelectionBackColor = AppStyle.ColHover
        g.DefaultCellStyle.SelectionForeColor = AppStyle.ColAccent
        g.DefaultCellStyle.Font = AppStyle.FntPetit
        g.DefaultCellStyle.NullValue = "NULL"
        g.AlternatingRowsDefaultCellStyle.BackColor = AppStyle.ColFons
        g.ColumnHeadersDefaultCellStyle.BackColor = AppStyle.ColFonsMig
        g.ColumnHeadersDefaultCellStyle.ForeColor = AppStyle.ColAccent
        g.ColumnHeadersDefaultCellStyle.Font = AppStyle.FntPetit
        AddHandler g.DataError, Sub(s As Object, ev As DataGridViewDataErrorEventArgs) ev.ThrowException = False
        g.DataSource = ConsultaDades.PerMostrar(dt)
        AddHandler g.DataBindingComplete,
            Sub(s As Object, ev As DataGridViewBindingCompleteEventArgs)
                g.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells)
                For Each col As DataGridViewColumn In g.Columns
                    If col.Width > 400 Then col.Width = 400
                Next
            End Sub
        Return g
    End Function

    ' DataGridView no admet colors amb transparència (GridColor, fons...):
    ' es barreja el color amb el fons per obtenir el mateix to, opac
    Private Shared Function Opac(c As Color, fons As Color) As Color
        Dim a As Single = c.A / 255.0F
        Return Color.FromArgb(255,
                              CInt(c.R * a + fons.R * (1 - a)),
                              CInt(c.G * a + fons.G * (1 - a)),
                              CInt(c.B * a + fons.B * (1 - a)))
    End Function

    Private Function TaulaActiva() As DataTable
        If _tabs.SelectedTab Is _tabSql Then
            Return If(_tabsResultat.SelectedTab Is Nothing, Nothing, TryCast(_tabsResultat.SelectedTab.Tag, DataTable))
        End If
        Return If(_tabs.SelectedTab Is Nothing, Nothing, TryCast(_tabs.SelectedTab.Tag, DataTable))
    End Function

    Private Sub ActualitzarEstatPestanya()
        If _tabs.SelectedTab IsNot Nothing AndAlso _tabs.SelectedTab IsNot _tabSql Then
            SetEstat(_tabs.SelectedTab.ToolTipText)
        End If
    End Sub

    Private Sub MarcarTotes(valor As Boolean)
        For i As Integer = 0 To _lstObjectes.Items.Count - 1
            _lstObjectes.SetItemChecked(i, valor)
        Next
    End Sub

    Private Function LlegirLimit() As Integer
        Dim n As Integer
        If Not Integer.TryParse(_txtLimit.Text.Trim(), n) OrElse n < 1 Then n = 1000
        n = Math.Min(n, 1000000)
        _txtLimit.Text = n.ToString()
        Return n
    End Function

    Private Sub SetEstat(msg As String, Optional esError As Boolean = False)
        _lblEstat.Text = msg
        _lblEstat.ForeColor = If(esError, AppStyle.ColPerill, AppStyle.ColTextFeble)
    End Sub

    Private Shared Function SenseCaractersInvalids(nom As String) As String
        For Each c As Char In IO.Path.GetInvalidFileNameChars()
            nom = nom.Replace(c, "_"c)
        Next
        Return If(nom = "", "dades", nom)
    End Function

End Class
