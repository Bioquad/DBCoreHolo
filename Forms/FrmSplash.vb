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


Public Class FrmSplash
    Inherits Form

    Private WithEvents _timer As New System.Windows.Forms.Timer()
    Private _step As Integer = 0
    Private _statusMsg As String = ""

    Private ReadOnly Property Msgs() As String()
        Get
            Return New String() {
                Locale.Str("SPL_MSG1"),
                Locale.Str("SPL_MSG2"),
                Locale.Str("SPL_MSG3"),
                Locale.Str("SPL_MSG4"),
                Locale.Str("SPL_MSG5")
            }
        End Get
    End Property

    Public Sub New()
        Me.Text = "Holografic DB"
        Me.Size = New Size(660, 380)
        Me.FormBorderStyle = FormBorderStyle.None
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = AppStyle.ColFons
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or
                    ControlStyles.UserPaint Or
                    ControlStyles.DoubleBuffer, True)
        _timer.Interval = 500
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        ' Aplicar tema i llegir idioma persistits
        AppStyle.AplicarTema(FrmOpcions.Opcions.Tema, FrmOpcions.Opcions.MidaFont)
        _statusMsg = Locale.Str("SPL_INIT")
        _timer.Start()
    End Sub

    Private Sub Timer_Tick(sender As Object, e As EventArgs) Handles _timer.Tick
        _step += 1
        Dim m() As String = Msgs()
        If _step <= m.Length Then
            _statusMsg = m(_step - 1)
        End If
        Me.Invalidate()

        If _step >= m.Length Then
            _timer.Stop()
            Dim menu As New FrmMainMenu()
            menu.Show()
            Me.Close()
        End If
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim w As Integer = Me.ClientSize.Width
        Dim h As Integer = Me.ClientSize.Height

        ' Fons
        Using br As New SolidBrush(AppStyle.ColFons)
            g.FillRectangle(br, 0, 0, w, h)
        End Using

        ' Vora taronja
        Using pen As New Pen(AppStyle.ColVora, 1.5F)
            g.DrawRectangle(pen, 1, 1, w - 3, h - 3)
        End Using

        ' Cercle decoratiu
        Using br As New SolidBrush(Color.FromArgb(10, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B))
            g.FillEllipse(br, w \ 2 - 160, h \ 2 - 160, 320, 320)
        End Using

        ' Titol
        Using fnt As New Font("Courier New", 52, FontStyle.Bold)
            Using br As New SolidBrush(AppStyle.ColAccent)
                Dim sz As SizeF = g.MeasureString("HOLOGRAFIC", fnt)
                g.DrawString("HOLOGRAFIC", fnt, br, CSng((w - sz.Width) / 2), CSng(h / 2 - 130))
            End Using
        End Using

        ' Subtitol
        Using fnt As New Font("Courier New", 22, FontStyle.Bold)
            Using br As New SolidBrush(AppStyle.ColAccentSec)
                Dim sz As SizeF = g.MeasureString("DB", fnt)
                g.DrawString("DB", fnt, br, CSng((w - sz.Width) / 2), CSng(h / 2 - 50))
            End Using
        End Using

        ' Linia
        Using pen As New Pen(Color.FromArgb(60, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B), 1)
            g.DrawLine(pen, 100, CSng(h / 2 + 10), w - 100, CSng(h / 2 + 10))
        End Using

        ' Status
        Using fnt As New Font("Courier New", 9)
            Using br As New SolidBrush(AppStyle.ColTextSec)
                Dim sz As SizeF = g.MeasureString(_statusMsg, fnt)
                g.DrawString(_statusMsg, fnt, br, CSng((w - sz.Width) / 2), CSng(h / 2 + 24))
            End Using
        End Using

        ' Barra de progres
        Dim barX As Integer = 80
        Dim barY As Integer = h - 60
        Dim barW As Integer = w - 160
        Dim barH As Integer = 5
        Using pen As New Pen(Color.FromArgb(50, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B), 1)
            g.DrawRectangle(pen, barX, barY, barW, barH)
        End Using
        If _step > 0 Then
            Dim filled As Integer = CInt(barW * _step / CDbl(Msgs().Length))
            If filled > 2 Then
                Using br As New SolidBrush(AppStyle.ColAccent)
                    g.FillRectangle(br, barX + 1, barY + 1, filled - 2, barH - 2)
                End Using
            End If
        End If
    End Sub

    ' Permet moure la finestra
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

