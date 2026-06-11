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
Imports System.Drawing.Drawing2D


Public Class FrmMainMenu
    Inherits Form

    Private _pnlMain As Panel
    Private _pnlCarregar As Panel
    Private _pnlNou As Panel
    Private _pnlRecents As Panel
    Private WithEvents _cursorTimer As New System.Windows.Forms.Timer()
    Private _cursorVisible As Boolean = True

    Public Sub New()
        Me.Text = "Holografic DB"
        Me.Size = New Size(860, 580)
        Me.FormBorderStyle = FormBorderStyle.None
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = AppStyle.ColFons
        AppStyle.AplicarEstilRecursiu(Me)
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.UserPaint Or
                    ControlStyles.DoubleBuffer, True)
        Me.KeyPreview = True
        ConstruirPanells()
        _cursorTimer.Interval = 530
        _cursorTimer.Start()
    End Sub

    Private Sub ConstruirPanells()
        Dim cx As Integer = (860 - 440) \ 2

        _pnlMain = New Panel()
        _pnlMain.BackColor = Color.Transparent
        _pnlMain.Size = New Size(440, 320)
        _pnlMain.Location = New Point(cx, 220)

        AfegirLabel(_pnlMain, Locale.Str("MENU_SELECCIONA"), 0, 0)
        AfegirBoto(_pnlMain, Locale.Str("MENU_CARREGAR"), 0, 22, AddressOf Btn1_Click)
        AfegirBoto(_pnlMain, Locale.Str("MENU_NOU"), 0, 58, AddressOf Btn2_Click)
        AfegirBoto(_pnlMain, Locale.Str("MENU_OPCIONS"), 0, 94, AddressOf Btn3_Click)
        AfegirBoto(_pnlMain, Locale.Str("MENU_SORTIR"), 0, 130, AddressOf Btn4_Click)
        Me.Controls.Add(_pnlMain)

        _pnlCarregar = New Panel()
        _pnlCarregar.BackColor = Color.Transparent
        _pnlCarregar.Size = New Size(440, 450)
        _pnlCarregar.Location = New Point(cx, 140)
        _pnlCarregar.Visible = False

        AfegirLabel(_pnlCarregar, Locale.Str("MENU_CARREGAR_TITOL"), 0, 0)
        AfegirBoto(_pnlCarregar, Locale.Str("MENU_VDB"), 0, 22, AddressOf BtnVdb_Click)
        AfegirBoto(_pnlCarregar, Locale.Str("MENU_MDF"), 0, 58, AddressOf BtnMdf_Click)
        AfegirBoto(_pnlCarregar, Locale.Str("MENU_SQL"), 0, 94, AddressOf BtnSql_Click)
        AfegirBoto(_pnlCarregar, Locale.Str("MENU_SERVER"), 0, 130, AddressOf BtnServer_Click)
        AfegirBoto(_pnlCarregar, Locale.Str("MENU_TORNAR"), 0, 172, AddressOf BtnTornar_Click)

        ' Secció RECENTS (dinàmica, es construeix al mostrar el panel)
        _pnlRecents = New Panel()
        _pnlRecents.BackColor = Color.Transparent
        _pnlRecents.Location = New Point(0, 220)
        _pnlRecents.Size = New Size(440, 200)
        _pnlCarregar.Controls.Add(_pnlRecents)

        Me.Controls.Add(_pnlCarregar)

        _pnlNou = New Panel()
        _pnlNou.BackColor = Color.Transparent
        _pnlNou.Size = New Size(440, 320)
        _pnlNou.Location = New Point(cx, 220)
        _pnlNou.Visible = False

        AfegirLabel(_pnlNou, Locale.Str("MENU_MOTOR_TITOL"), 0, 0)
        AfegirBotoTag(_pnlNou, "[ A ]  SQL SERVER  (T-SQL)", 0, 22, "T-SQL", AddressOf BtnMotor_Click)
        AfegirBotoTag(_pnlNou, "[ B ]  MYSQL / MARIADB", 0, 58, "MySQL", AddressOf BtnMotor_Click)
        AfegirBotoTag(_pnlNou, "[ C ]  POSTGRESQL", 0, 94, "PostgreSQL", AddressOf BtnMotor_Click)
        AfegirBotoTag(_pnlNou, "[ D ]  ANSI SQL (GENERIC)", 0, 130, "ANSI", AddressOf BtnMotor_Click)
        AfegirBoto(_pnlNou, Locale.Str("MENU_TORNAR"), 0, 172, AddressOf BtnTornar_Click)
        Me.Controls.Add(_pnlNou)
    End Sub

    Private Sub AfegirLabel(parent As Panel, text As String, x As Integer, y As Integer)
        Dim l As New Label()
        l.Text = text
        l.Font = AppStyle.FntNormal
        l.ForeColor = AppStyle.ColTextFeble
        l.BackColor = Color.Transparent
        l.AutoSize = True
        l.Location = New Point(x, y)
        parent.Controls.Add(l)
    End Sub

    Private Function AfegirBoto(parent As Panel, text As String, x As Integer, y As Integer,
                                 handler As EventHandler) As Button
        Dim b As New Button()
        b.Text = text
        b.Font = AppStyle.FntNormal
        b.ForeColor = AppStyle.ColTextPrinc
        b.BackColor = AppStyle.ColFonsMig
        b.FlatStyle = FlatStyle.Flat
        b.Size = New Size(440, AppStyle.AltBotoNormal + 4)
        b.Location = New Point(x, y)
        b.TextAlign = ContentAlignment.MiddleLeft
        b.Padding = New Padding(10, 0, 0, 0)
        b.Cursor = Cursors.Hand
        b.FlatAppearance.BorderColor = AppStyle.ColVoraFeble
        b.FlatAppearance.BorderSize = 1
        b.FlatAppearance.MouseOverBackColor = AppStyle.ColHover
        AddHandler b.Click, handler
        parent.Controls.Add(b)
        Return b
    End Function

    Private Function AfegirBotoTag(parent As Panel, text As String, x As Integer, y As Integer,
                                    tag As String, handler As EventHandler) As Button
        Dim b As Button = AfegirBoto(parent, text, x, y, handler)
        b.Tag = tag
        Return b
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim w As Integer = Me.ClientSize.Width
        Dim h As Integer = Me.ClientSize.Height

        Using br As New SolidBrush(AppStyle.ColFons)
            g.FillRectangle(br, 0, 0, w, h)
        End Using

        Using pen As New Pen(AppStyle.ColVora, 1.5F)
            g.DrawRectangle(pen, 1, 1, w - 3, h - 3)
        End Using

        Using br As New SolidBrush(Color.FromArgb(7, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B))
            g.FillEllipse(br, w \ 2 - 220, h \ 2 - 220, 440, 440)
        End Using

        Using pen As New Pen(Color.FromArgb(40, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B), 1)
            g.DrawLine(pen, 80, 140, w - 80, 140)
        End Using

        Using fnt As New Font("Courier New", 13, FontStyle.Bold)
            Using br As New SolidBrush(AppStyle.ColAccent)
                Dim txt As String = Locale.Str("MENU_TITOL")
                Dim sz As SizeF = g.MeasureString(txt, fnt)
                g.DrawString(txt, fnt, br, CSng((w - sz.Width) / 2), 50)
            End Using
        End Using

        Using fnt As New Font("Courier New", 8)
            Using br As New SolidBrush(Color.FromArgb(50, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B))
                Dim txt As String = "(C) 2026 HOLOGRAFIC DB / MOTOR MER Alfa 0.24"
                Dim sz As SizeF = g.MeasureString(txt, fnt)
                g.DrawString(txt, fnt, br, CSng((w - sz.Width) / 2), 80)
            End Using
        End Using

        If _cursorVisible Then
            Using fnt As New Font("Courier New", 11)
                Using br As New SolidBrush(AppStyle.ColAccent)
                    g.DrawString("> _", fnt, br, 20, h - 35)
                End Using
            End Using
        End If
    End Sub

    Private Sub CursorTimer_Tick(sender As Object, e As EventArgs) Handles _cursorTimer.Tick
        _cursorVisible = Not _cursorVisible
        Me.Invalidate(New Rectangle(15, Me.ClientSize.Height - 45, 80, 30))
    End Sub

    Private Sub MostrarPanel(pnl As Panel)
        _pnlMain.Visible = False
        _pnlCarregar.Visible = False
        _pnlNou.Visible = False
        pnl.Visible = True
        ' Quan s'obre el panell de carregar, refrescar la llista de recents
        If pnl Is _pnlCarregar Then ConstruirRecents()
    End Sub

    Private Sub ConstruirRecents()
        If _pnlRecents Is Nothing Then Return
        _pnlRecents.Controls.Clear()

        Dim recents As List(Of String) = ProjectSerializer.ObtenirRecents()
        If recents.Count = 0 Then
            Dim lbl As New Label()
            lbl.Text = Locale.Str("MENU_SENSE_RECENTS")
            lbl.Font = AppStyle.FntMoltPetit
            lbl.ForeColor = AppStyle.ColTextFeble
            lbl.AutoSize = True
            lbl.Location = New Point(2, 6)
            _pnlRecents.Controls.Add(lbl)
            Return
        End If

        ' Capçalera "RECENTS:"
        Dim lblTit As New Label()
        lblTit.Text = Locale.Str("MENU_RECENTS")
        lblTit.Font = AppStyle.FntMoltPetit
        lblTit.ForeColor = AppStyle.ColTextFeble
        lblTit.AutoSize = True
        lblTit.Location = New Point(2, 2)
        _pnlRecents.Controls.Add(lblTit)

        Dim y As Integer = 20
        For i As Integer = 0 To Math.Min(recents.Count - 1, MAX_RECENTS_VISIBLE - 1)
            Dim ruta As String = recents(i)
            Dim nom As String = IO.Path.GetFileName(ruta)
            Dim dir As String = IO.Path.GetDirectoryName(ruta)
            ' Truncar directori si és molt llarg
            If dir.Length > 32 Then dir = "..." & dir.Substring(dir.Length - 32)

            Dim btn As New Button()
            btn.Text = "  " & nom & "  ·  " & dir
            btn.Font = AppStyle.FntPetit
            btn.ForeColor = AppStyle.ColAccentSec
            btn.BackColor = AppStyle.ColFonsMig
            btn.FlatStyle = FlatStyle.Flat
            btn.FlatAppearance.BorderColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
            btn.FlatAppearance.BorderSize = 1
            btn.FlatAppearance.MouseOverBackColor = AppStyle.ColHover
            btn.TextAlign = ContentAlignment.MiddleLeft
            btn.Size = New Size(440, 22)
            btn.Location = New Point(0, y)
            btn.Cursor = Cursors.Hand
            btn.Tag = ruta
            AddHandler btn.Click, AddressOf BtnRecent_Click
            _pnlRecents.Controls.Add(btn)
            y += 26
        Next

        _pnlRecents.Height = y + 4
    End Sub

    Private Sub Btn1_Click(s As Object, e As EventArgs)
        MostrarPanel(_pnlCarregar)
    End Sub

    Private Sub Btn2_Click(s As Object, e As EventArgs)
        ' Submenú de motors ocult — s'obre directament amb T-SQL per defecte
        Dim p As ProyectoBBDD = ProyectoBBDD.NouProjecte()
        p.MotorSQL = "T-SQL"
        ObrirDissenyador(p, "T-SQL", "")
    End Sub

    Private Sub Btn3_Click(s As Object, e As EventArgs)
        Using frm As New FrmOpcions()
            frm.ShowDialog(Me)
        End Using
    End Sub

    Private Sub Btn4_Click(s As Object, e As EventArgs)
        Application.Exit()
    End Sub

    Private Sub BtnTornar_Click(s As Object, e As EventArgs)
        MostrarPanel(_pnlMain)
    End Sub

    Private Const MAX_RECENTS_VISIBLE As Integer = 6

    Private Sub BtnRecent_Click(s As Object, e As EventArgs)
        Dim btn As Button = TryCast(s, Button)
        If btn Is Nothing OrElse btn.Tag Is Nothing Then Return
        Dim ruta As String = btn.Tag.ToString()
        If Not IO.File.Exists(ruta) Then
            MessageBox.Show(Locale.Str("DLG_FITXER_NO") & Environment.NewLine & ruta,
                            Locale.Str("DLG_FITXER_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
            ProjectSerializer.AfegirARecents(ruta)  ' El traurà ja que no existeix
            ConstruirRecents()
            Return
        End If
        Try
            Dim p As ProyectoBBDD = ProjectSerializer.Carregar(ruta)
            ObrirDissenyador(p, p.MotorSQL, ruta)
        Catch ex As Exception
            MessageBox.Show(Locale.Str("MENU_ERR_CARREGAR") & ex.Message, Locale.Str("DLG_ERROR_TITOL"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnVdb_Click(s As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Filter = Locale.Str("DLG_FILTRE_HDB")
            If dlg.ShowDialog() = DialogResult.OK Then
                Try
                    Dim p As ProyectoBBDD = ProjectSerializer.Carregar(dlg.FileName)
                    ObrirDissenyador(p, p.MotorSQL, dlg.FileName)
                Catch ex As Exception
                    MessageBox.Show(Locale.Str("MENU_ERR_CARREGAR") & ex.Message, Locale.Str("DLG_ERROR_TITOL"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

    Private Sub BtnMdf_Click(s As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Filter = "SQL Server Database (*.mdf)|*.mdf|Tots els fitxers (*.*)|*.*"
            dlg.Title = "Seleccionar fitxer .mdf"
            If dlg.ShowDialog() = DialogResult.OK Then
                Dim mdfPath As String = dlg.FileName
                ' Mostrar progrés
                Dim fWait As New Form()
                fWait.Text = Locale.Str("MENU_CONNECTANT")
                fWait.Size = New Size(360, 100)
                fWait.FormBorderStyle = FormBorderStyle.FixedDialog
                fWait.StartPosition = FormStartPosition.CenterParent
                fWait.BackColor = AppStyle.ColFons
                Dim lblW As New Label()
                lblW.Text = Locale.Str("MENU_LLEGINT") & IO.Path.GetFileName(mdfPath)
                lblW.ForeColor = AppStyle.ColAccent
                lblW.Font = AppStyle.FntNormal
                lblW.Dock = DockStyle.Fill
                lblW.TextAlign = ContentAlignment.MiddleCenter
                fWait.Controls.Add(lblW)
                fWait.Show()
                Application.DoEvents()
                Try
                    Dim p As ProyectoBBDD = MdfImporter.Importar(mdfPath)
                    fWait.Close()
                    MessageBox.Show(
                        Locale.Str("DLG_IMPORTAT") & Environment.NewLine &
                        p.Taules.Count & Locale.Str("DLG_IMPORT_TAULES") & p.Relacions.Count & Locale.Str("DLG_IMPORT_RELACIONS"),
                        Locale.Str("DLG_IMPORT_TITOL"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information)
                    ObrirDissenyador(p, "T-SQL", "")
                Catch ex As Exception
                    fWait.Close()
                    Dim msg As String = ex.Message
                    If msg.Contains("LocalDB") OrElse msg.Contains("provider") Then
                        msg = Locale.Str("MENU_LOCALDB_MSG") & Environment.NewLine &
                              Locale.Str("MENU_LOCALDB_DL") & Environment.NewLine & Environment.NewLine & ex.Message
                    End If
                    MessageBox.Show(msg, Locale.Str("MENU_ERR_MDF_TITOL"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

    Private Sub BtnSql_Click(s As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Filter   = Locale.Str("DLG_FILTRE_SQL")
            dlg.Title    = Locale.Str("DLG_IMPORTSQL_TITOL")
            dlg.FileName = ""
            If dlg.ShowDialog() <> DialogResult.OK Then Return
            Dim ruta As String = dlg.FileName
            Try
                Dim sql As String = IO.File.ReadAllText(ruta, System.Text.Encoding.UTF8)
                Dim parsed As SqlScriptImporter.ImportResult = SqlScriptImporter.Importar(sql, 1, 1)
                If parsed.Taules.Count = 0 Then
                    MessageBox.Show(Locale.Str("DLG_CAP_TAULA_MSG"),
                                    Locale.Str("DLG_CAP_TAULA_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
                Using fImp As New FrmImportSql(ruta, parsed, Nothing)
                    If fImp.ShowDialog() <> DialogResult.OK Then Return
                    Dim p As ProyectoBBDD = ProyectoBBDD.NouProjecte()
                    p.Nombre   = IO.Path.GetFileNameWithoutExtension(ruta)
                    p.MotorSQL = "T-SQL"
                    Dim idT As Integer = 1
                    Dim idR As Integer = 1
                    Dim mapId As New Dictionary(Of Integer, Integer)()
                    For Each t As TablaBBDD In fImp.ResultTaules
                        mapId(t.Id) = idT
                        t.Id = idT : idT += 1
                        p.Taules.Add(t)
                    Next
                    For Each pt As TablaBBDD In parsed.Taules
                        If Not mapId.ContainsKey(pt.Id) Then
                            Dim trobada As TablaBBDD = fImp.ResultTaules.Find(
                                Function(x) x.Nombre = pt.Nombre)
                            If trobada IsNot Nothing Then mapId(pt.Id) = trobada.Id
                        End If
                    Next
                    For Each r As RelacionBBDD In fImp.ResultRelacions
                        Dim origenNou As Integer = If(mapId.ContainsKey(r.TablaOrigenId),  mapId(r.TablaOrigenId),  r.TablaOrigenId)
                        Dim destiNou  As Integer = If(mapId.ContainsKey(r.TablaDestinoId), mapId(r.TablaDestinoId), r.TablaDestinoId)
                        r.Id = idR : idR += 1
                        r.TablaOrigenId  = origenNou
                        r.TablaDestinoId = destiNou
                        p.Relacions.Add(r)
                    Next
                    p.RecalcularIds()
                    ObrirDissenyador(p, "T-SQL", "")
                End Using
            Catch ex As Exception
                MessageBox.Show(Locale.Str("MENU_ERR_LLEGIR") & Environment.NewLine & ex.Message,
                                Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub BtnServer_Click(s As Object, e As EventArgs)
        Using dlgConn As New FrmConnectServer(Nothing)
            If dlgConn.ShowDialog() <> DialogResult.OK Then Return
            Dim conn As SqlServerConnector.ConnexioServidor = dlgConn.ResultConnexio
            Try
                Dim p As ProyectoBBDD = Nothing
                Dim errMsg As String = ""
                Dim t As New System.Threading.Thread(Sub()
                    Try
                        p = SqlServerConnector.ImportarEstructura(conn)
                    Catch ex As Exception
                        errMsg = ex.Message
                    End Try
                End Sub)
                t.IsBackground = True
                t.Start()
                Do While t.IsAlive
                    Application.DoEvents()
                    System.Threading.Thread.Sleep(50)
                Loop
                If Not String.IsNullOrEmpty(errMsg) Then
                    MessageBox.Show(Locale.Str("SRV_ERR_IMPORT") & Environment.NewLine & errMsg,
                                    Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return
                End If
                If p Is Nothing OrElse p.Taules.Count = 0 Then
                    MessageBox.Show(Locale.Str("MENU_BD_BUIDA_MSG"),
                                    Locale.Str("MENU_BD_BUIDA_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
                Dim parsed2 As New SqlScriptImporter.ImportResult()
                parsed2.Taules.AddRange(p.Taules)
                parsed2.Relacions.AddRange(p.Relacions)
                Using fImp As New FrmImportSql(Locale.Str("SRV_PREFIX") & conn.BaseDades, parsed2, Nothing)
                    If fImp.ShowDialog() <> DialogResult.OK Then Return
                    Dim proj As New ProyectoBBDD()
                    proj.Nombre   = conn.BaseDades
                    proj.MotorSQL = "T-SQL"
                    Dim mapId As New Dictionary(Of Integer, Integer)()
                    Dim idT As Integer = 1
                    For Each tbl As TablaBBDD In fImp.ResultTaules
                        mapId(tbl.Id) = idT : tbl.Id = idT : idT += 1
                        proj.Taules.Add(tbl)
                    Next
                    Dim idR As Integer = 1
                    For Each r As RelacionBBDD In fImp.ResultRelacions
                        Dim origenNou As Integer = If(mapId.ContainsKey(r.TablaOrigenId),  mapId(r.TablaOrigenId),  r.TablaOrigenId)
                        Dim destiNou  As Integer = If(mapId.ContainsKey(r.TablaDestinoId), mapId(r.TablaDestinoId), r.TablaDestinoId)
                        r.Id = idR : idR += 1
                        r.TablaOrigenId  = origenNou
                        r.TablaDestinoId = destiNou
                        proj.Relacions.Add(r)
                    Next
                    proj.RecalcularIds()
                    ObrirDissenyador(proj, "T-SQL", "")
                End Using
            Catch ex As Exception
                MessageBox.Show(Locale.Str("ERR_PREFIX") & ex.Message, Locale.Str("DLG_ERROR_TITOL"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub BtnMotor_Click(s As Object, e As EventArgs)
        Dim btn As Button = DirectCast(s, Button)
        Dim motor As String = "T-SQL"
        If btn.Tag IsNot Nothing Then motor = btn.Tag.ToString()
        Dim p As ProyectoBBDD = ProyectoBBDD.NouProjecte()
        p.MotorSQL = motor
        ObrirDissenyador(p, motor, "")
    End Sub

    ' Reconstrueix tots els panells amb els textos de l'idioma actiu.
    ' Es crida des de FrmOpcions quan canvia l'idioma.
    Public Sub AplicarIdioma()
        Me.Controls.Clear()
        ConstruirPanells()
        AppStyle.AplicarEstilRecursiu(Me)
        Me.Invalidate()
    End Sub

    Private Sub ObrirDissenyador(p As ProyectoBBDD, motor As String, ruta As String)
        _cursorTimer.Stop()
        Dim d As New FrmDesigner(p, motor)
        d.RutaFitxer = ruta
        AddHandler d.FormClosed, Sub(s As Object, e As FormClosedEventArgs)
            _cursorTimer.Start()
            Me.Show()
        End Sub
        Me.Hide()
        d.Show()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        Select Case e.KeyCode
            Case Keys.D1, Keys.NumPad1
                MostrarPanel(_pnlCarregar)
            Case Keys.D2, Keys.NumPad2
                MostrarPanel(_pnlNou)
            Case Keys.D4, Keys.NumPad4
                Application.Exit()
            Case Keys.Escape
                MostrarPanel(_pnlMain)
        End Select
    End Sub

    Private _dragging As Boolean = False
    Private _dragPt As Point

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            _dragging = True
            _dragPt = e.Location
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If _dragging Then
            Dim d As Point = Me.PointToScreen(e.Location)
            Me.Location = New Point(d.X - _dragPt.X, d.Y - _dragPt.Y)
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        _dragging = False
    End Sub

End Class

