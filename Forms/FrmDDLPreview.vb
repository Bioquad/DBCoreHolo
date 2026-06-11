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

' =======================================================================
' FrmDDLPreview.vb  —  DB-Core Holographic
' Vista prèvia i exportació del DDL T-SQL generat
' Extret de FrmDesigner.vb
' =======================================================================
Public Class FrmDDLPreview
    Inherits HoloForm

    Private ReadOnly _sql As String

    Public Sub New(sql As String)
        _sql = sql
        Me.Text = Locale.Str("DDL_TITOL")
        Me.Size = New Size(740, 580)

        Dim rtb As New RichTextBox()
        rtb.Dock = DockStyle.Fill
        rtb.Text = sql
        rtb.Font = New Font("Courier New", 9)
        rtb.BackColor = AppStyle.ColFonsMig
        rtb.ForeColor = AppStyle.ColAccentSec
        rtb.ReadOnly = True
        rtb.BorderStyle = BorderStyle.None
        rtb.ScrollBars = RichTextBoxScrollBars.Vertical
        rtb.WordWrap = False
        Me.Controls.Add(rtb)

        Dim pnl As New Panel()
        pnl.Dock = DockStyle.Bottom
        pnl.Height = 36
        pnl.BackColor = AppStyle.ColFonsMig

        Dim bCopy As New Button()
        bCopy.Text = Locale.Str("DDL_COPIAR")
        bCopy.Location = New Point(8, 5)
        bCopy.Size = New Size(80, 24)
        bCopy.Font = New Font("Courier New", 8)
        bCopy.BackColor = AppStyle.ColFonsInput
        bCopy.ForeColor = AppStyle.ColTextPrinc
        bCopy.FlatStyle = FlatStyle.Flat
        bCopy.FlatAppearance.BorderColor = Color.FromArgb(100, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        AddHandler bCopy.Click, Sub(sx As Object, ex As EventArgs) Clipboard.SetText(sql)
        pnl.Controls.Add(bCopy)

        Dim bSave As New Button()
        bSave.Text = Locale.Str("DDL_DESAR")
        bSave.Location = New Point(96, 5)
        bSave.Size = New Size(100, 24)
        bSave.Font = New Font("Courier New", 8)
        bSave.BackColor = AppStyle.ColFonsInput
        bSave.ForeColor = AppStyle.ColTextPrinc
        bSave.FlatStyle = FlatStyle.Flat
        bSave.FlatAppearance.BorderColor = Color.FromArgb(100, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        AddHandler bSave.Click, Sub(sx As Object, ex As EventArgs)
            Using dlg As New SaveFileDialog()
                dlg.Filter = "SQL (*.sql)|*.sql"
                dlg.FileName = "script.sql"
                If dlg.ShowDialog() = DialogResult.OK Then
                    IO.File.WriteAllText(dlg.FileName, sql, System.Text.Encoding.UTF8)
                End If
            End Using
        End Sub
        pnl.Controls.Add(bSave)

        Dim bClose As New Button()
        bClose.Text = Locale.Str("BTN_TANCAR")
        bClose.Location = New Point(204, 5)
        bClose.Size = New Size(80, 24)
        bClose.Font = New Font("Courier New", 8)
        bClose.BackColor = AppStyle.ColFonsInput
        bClose.ForeColor = AppStyle.ColTextSec
        bClose.FlatStyle = FlatStyle.Flat
        bClose.FlatAppearance.BorderColor = Color.FromArgb(80, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        bClose.DialogResult = DialogResult.OK
        pnl.Controls.Add(bClose)

        Me.Controls.Add(pnl)
    End Sub

End Class
