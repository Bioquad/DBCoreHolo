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
Imports System.Runtime.InteropServices

' ================================================================
' HoloForm.vb  —  DB-Core Holographic
' Classe base per a tots els formularis secundaris.
' Proporciona:
'   - Caption (barra de títol) pintada en estil hologràfic
'   - Vores del formulari en taronja fosc
'   - FormBorderStyle.None + lògica de redimensionat i arrossegament
'   - Scrollbars de tots els fills recolorides via WndProc
' ================================================================
Public Class HoloForm
    Inherits Form

    ' ── Constants WinAPI ─────────────────────────────────────────
    Private Const WM_NCHITTEST        As Integer = &H84
    Private Const WM_NCCALCSIZE       As Integer = &H83
    Private Const WM_NCPAINT          As Integer = &H85
    Private Const WM_NCLBUTTONDOWN    As Integer = &HA1
    Private Const WM_SYSCOMMAND       As Integer = &H112
    Private Const SC_MOVE             As Integer = &HF010
    Private Const HTCLIENT            As Integer = 1
    Private Const HTCAPTION           As Integer = 2
    Private Const HTLEFT              As Integer = 10
    Private Const HTRIGHT             As Integer = 11
    Private Const HTTOP               As Integer = 12
    Private Const HTTOPLEFT           As Integer = 13
    Private Const HTTOPRIGHT          As Integer = 14
    Private Const HTBOTTOM            As Integer = 15
    Private Const HTBOTTOMLEFT        As Integer = 16
    Private Const HTBOTTOMRIGHT       As Integer = 17
    Private Const RESIZE_BORDER       As Integer = 6
    Private Const CAPTION_H           As Integer = 28

    ' Arrossegar
    Private _dragging   As Boolean = False
    Private _dragOffset As Point

    ' ── Constructor ──────────────────────────────────────────────
    Public Sub New()
        Me.FormBorderStyle = FormBorderStyle.None
        Me.BackColor       = AppStyle.ColFons
        Me.ForeColor       = AppStyle.ColTextPrinc
        Me.Font            = AppStyle.FntNormal
        Me.StartPosition   = FormStartPosition.CenterParent
        Me.DoubleBuffered  = True
        ' Activar redimensionat via Padding per no solapar contingut
        Me.Padding = New Padding(1, CAPTION_H, 1, 1)
    End Sub

    ' ── Dibuix propi (caption + vores) ───────────────────────────
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        Dim w As Integer = Me.ClientSize.Width
        Dim h As Integer = Me.ClientSize.Height
        Dim ac As Color = AppStyle.ColAccent   ' color accent del tema actiu

        ' Vora exterior
        Using pen As New Pen(Color.FromArgb(180, ac.R, ac.G, ac.B), 1)
            g.DrawRectangle(pen, 0, 0, w - 1, h - 1)
        End Using

        ' Fons caption (degradat fosc)
        Dim capRect As New Rectangle(1, 1, w - 2, CAPTION_H - 1)
        Dim c1 As Color = Color.FromArgb(Math.Min(255, AppStyle.ColFonsCapc.R + 8),
                                          Math.Min(255, AppStyle.ColFonsCapc.G + 2),
                                          AppStyle.ColFonsCapc.B)
        Using brCap As New LinearGradientBrush(capRect, c1, AppStyle.ColFonsMig,
                                               LinearGradientMode.Vertical)
            g.FillRectangle(brCap, capRect)
        End Using

        ' Línia separadora sota el caption
        Using pen As New Pen(Color.FromArgb(120, ac.R, ac.G, ac.B), 1)
            g.DrawLine(pen, 1, CAPTION_H, w - 2, CAPTION_H)
        End Using

        ' Punt de llum a l'esquerra del caption
        Using brGlow As New PathGradientBrush(New Point() {
                New Point(0, 0), New Point(60, 0),
                New Point(30, CAPTION_H), New Point(0, CAPTION_H)})
            brGlow.CenterColor = Color.FromArgb(40, ac.R, ac.G, ac.B)
            brGlow.SurroundColors = New Color() {Color.Transparent,
                Color.Transparent, Color.Transparent, Color.Transparent}
            g.FillRectangle(brGlow, capRect)
        End Using

        ' Text del títol
        Dim titol As String = Me.Text.ToUpper()
        Using fnt As New Font("Courier New", 8.5F, FontStyle.Bold)
            Dim tsz As SizeF = g.MeasureString(titol, fnt)
            Dim ty  As Single = (CAPTION_H - tsz.Height) / 2.0F + 1
            Using brS As New SolidBrush(Color.FromArgb(60, 0, 0, 0))
                g.DrawString(titol, fnt, brS, 14.0F, ty + 1)
            End Using
            Using brT As New SolidBrush(Color.FromArgb(230, AppStyle.ColAccentSec.R,
                                                            AppStyle.ColAccentSec.G,
                                                            AppStyle.ColAccentSec.B))
                g.DrawString(titol, fnt, brT, 14.0F, ty)
            End Using
        End Using

        ' Botó tancar [X]
        Dim closeRect As Rectangle = GetCloseRect()
        Dim mousePos As Point = Me.PointToClient(Control.MousePosition)
        If closeRect.Contains(mousePos) Then
            Using brX As New SolidBrush(Color.FromArgb(80, ac.R \ 2, ac.G \ 4, ac.B \ 4))
                g.FillRectangle(brX, closeRect)
            End Using
        End If
        Using penX As New Pen(Color.FromArgb(180, AppStyle.ColPerill.R,
                                                  AppStyle.ColPerill.G,
                                                  AppStyle.ColPerill.B), 1.5F)
            g.DrawLine(penX, closeRect.X + 6, closeRect.Y + 6,
                       closeRect.Right - 6, closeRect.Bottom - 6)
            g.DrawLine(penX, closeRect.Right - 6, closeRect.Y + 6,
                       closeRect.X + 6, closeRect.Bottom - 6)
        End Using
        Using penXborder As New Pen(Color.FromArgb(40, ac.R, ac.G, ac.B), 1)
            g.DrawRectangle(penXborder, closeRect)
        End Using
    End Sub

    Private Function GetCloseRect() As Rectangle
        Return New Rectangle(Me.ClientSize.Width - 26, 4, 22, CAPTION_H - 8)
    End Function

    ' ── Ratolí: arrossegar per la caption i botó tancar ──────────
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left Then
            If GetCloseRect().Contains(e.Location) Then
                Me.DialogResult = DialogResult.Cancel
                Me.Close()
                Return
            End If
            If e.Y <= CAPTION_H Then
                _dragging = True
                _dragOffset = e.Location
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _dragging Then
            Dim screenPos As Point = Me.PointToScreen(e.Location)
            Me.Location = New Point(screenPos.X - _dragOffset.X,
                                    screenPos.Y - _dragOffset.Y)
        End If
        ' Refrescar per hover del botó X
        Me.Invalidate(New Rectangle(Me.ClientSize.Width - 30, 0, 30, CAPTION_H))
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _dragging = False
    End Sub

    ' ── Redimensionat via hit-test a les vores ───────────────────
    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_NCHITTEST Then
            Dim pt As Point = Me.PointToClient(
                New Point(m.LParam.ToInt32() And &HFFFF,
                          (m.LParam.ToInt32() >> 16) And &HFFFF))
            Dim w As Integer = Me.ClientSize.Width
            Dim h As Integer = Me.ClientSize.Height
            Dim rb As Integer = RESIZE_BORDER

            If pt.X < rb AndAlso pt.Y < rb Then
                m.Result = New IntPtr(HTTOPLEFT) : Return
            ElseIf pt.X > w - rb AndAlso pt.Y < rb Then
                m.Result = New IntPtr(HTTOPRIGHT) : Return
            ElseIf pt.X < rb AndAlso pt.Y > h - rb Then
                m.Result = New IntPtr(HTBOTTOMLEFT) : Return
            ElseIf pt.X > w - rb AndAlso pt.Y > h - rb Then
                m.Result = New IntPtr(HTBOTTOMRIGHT) : Return
            ElseIf pt.X < rb Then
                m.Result = New IntPtr(HTLEFT) : Return
            ElseIf pt.X > w - rb Then
                m.Result = New IntPtr(HTRIGHT) : Return
            ElseIf pt.Y < rb Then
                m.Result = New IntPtr(HTTOP) : Return
            ElseIf pt.Y > h - rb Then
                m.Result = New IntPtr(HTBOTTOM) : Return
            ElseIf pt.Y <= CAPTION_H AndAlso Not GetCloseRect().Contains(pt) Then
                m.Result = New IntPtr(HTCAPTION) : Return
            End If
        End If
        MyBase.WndProc(m)
    End Sub

    ' ── OnLoad: aplicar estil a tots els controls fills ──────────
    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        ' Aplicar el tema actiu (colors + fonts) a tots els controls
        ' d'aquest diàleg. No cridem AplicarTema() complet perquè
        ' no volem propagar a altres formularis des d'aquí.
        AppStyle.AplicarEstilRecursiu(Me)
        ' Aplicar estil de tabs a tots els TabControl fills
        For Each ctrl As Control In Me.Controls
            AplicarEstilTabsRecursiu(ctrl)
        Next
    End Sub

    Private Sub AplicarEstilTabsRecursiu(ctrl As Control)
        If TypeOf ctrl Is TabControl Then
            AppStyle.AplicarEstilTabControl(DirectCast(ctrl, TabControl))
        End If
        For Each child As Control In ctrl.Controls
            AplicarEstilTabsRecursiu(child)
        Next
    End Sub

    ' ── Tecla Escape = Cancel ─────────────────────────────────────
    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.KeyCode = Keys.Escape Then
            Me.DialogResult = DialogResult.Cancel
            Me.Close()
        End If
    End Sub

End Class


' ================================================================
' HoloScrollBar — Panel personalitzat que substitueix ScrollBar
' per poder-lo pintar en l'estil hologràfic (taronja/negre)
' ================================================================
Public Class HoloVScrollBar
    Inherits Panel

    Private _min As Integer = 0
    Private _max As Integer = 100
    Private _val As Integer = 0
    Private _pageSize As Integer = 10
    Private _dragging As Boolean = False
    Private _dragStartY As Integer = 0
    Private _dragStartVal As Integer = 0

    Public Shadows Event Scroll(sender As Object, value As Integer)

    Public Property Minimum As Integer
        Get
            Return _min
        End Get
        Set(v As Integer)
            _min = v
            Invalidate()
        End Set
    End Property

    Public Property Maximum As Integer
        Get
            Return _max
        End Get
        Set(v As Integer)
            _max = v
            Invalidate()
        End Set
    End Property

    Public Property Value As Integer
        Get
            Return _val
        End Get
        Set(v As Integer)
            _val = Math.Max(_min, Math.Min(_max - _pageSize, v))
            Invalidate()
        End Set
    End Property

    Public Property LargeChange As Integer
        Get
            Return _pageSize
        End Get
        Set(v As Integer)
            _pageSize = v
            Invalidate()
        End Set
    End Property

    Public Sub New()
        Me.Width = 12
        Me.DoubleBuffered = True
        Me.Cursor = Cursors.Default
    End Sub

    Private Function GetThumbRect() As Rectangle
        Dim trackH As Integer = Me.Height - 4
        Dim range   As Integer = Math.Max(1, _max - _min)
        Dim thumbH  As Integer = Math.Max(20, CInt(trackH * _pageSize / range))
        Dim thumbY  As Integer = 2 + CInt((_val - _min) / CSng(range - _pageSize) * (trackH - thumbH))
        thumbY = Math.Max(2, Math.Min(trackH - thumbH + 2, thumbY))
        Return New Rectangle(2, thumbY, Me.Width - 4, thumbH)
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        Dim ac As Color = AppStyle.ColAccent
        ' Fons track
        g.Clear(AppStyle.ColFons)
        ' Track subtil
        Using pen As New Pen(Color.FromArgb(20, ac.R, ac.G, ac.B), 1)
            g.DrawRectangle(pen, 1, 1, Me.Width - 3, Me.Height - 3)
        End Using
        ' Thumb
        Dim tr As Rectangle = GetThumbRect()
        Using br As New LinearGradientBrush(tr,
                Color.FromArgb(100, ac.R, ac.G, ac.B),
                Color.FromArgb(60, ac.R * 7 \ 10, ac.G * 6 \ 10, ac.B),
                LinearGradientMode.Horizontal)
            g.FillRectangle(br, tr)
        End Using
        Using pen As New Pen(Color.FromArgb(160, ac.R, ac.G, ac.B), 1)
            g.DrawRectangle(pen, tr)
        End Using
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            Dim tr As Rectangle = GetThumbRect()
            If tr.Contains(e.Location) Then
                _dragging = True
                _dragStartY = e.Y
                _dragStartVal = _val
            Else
                ' Clic fora del thumb = pàgina
                If e.Y < tr.Top Then
                    Value -= _pageSize
                Else
                    Value += _pageSize
                End If
                RaiseEvent Scroll(Me, _val)
            End If
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If _dragging Then
            Dim trackH  As Integer = Me.Height - 4
            Dim range   As Integer = Math.Max(1, _max - _min - _pageSize)
            Dim thumbH  As Integer = Math.Max(20, CInt(trackH * _pageSize / (_max - _min)))
            Dim dy      As Integer = e.Y - _dragStartY
            Dim delta   As Integer = CInt(dy * range / CSng(trackH - thumbH))
            Value = _dragStartVal + delta
            RaiseEvent Scroll(Me, _val)
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        _dragging = False
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        Value -= Math.Sign(e.Delta) * _pageSize \ 3
        RaiseEvent Scroll(Me, _val)
    End Sub

End Class
