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
Imports SkiaSharp

' ============================================================
' SphereRenderer.vb  —  DB-Core Holographic
' Motor: SkiaSharp 2.88.8  (Google/Microsoft, MIT, durable)
'
' API SkiaSharp 2.88 correcta (verificada):
'   canvas.DrawText(text As String, x As Single, y As Single,
'                  font As SKFont, paint As SKPaint)
'   canvas.DrawRoundRect(rect As SKRoundRect, paint As SKPaint)
'   canvas.DrawRect(rect As SKRect, paint As SKPaint)
'   canvas.DrawLine(x0,y0,x1,y1, paint)
'   canvas.DrawCircle(cx,cy,r, paint)
'   paint.MeasureText(text As String) As Single   <- amplada
'   canvas.Clear(color As SKColor)
' ============================================================

Public Class SphereRenderer

    ' ── Propietats publiques ─────────────────────────────────────────────
    Public Property RotX       As Single  = 0.3F
    Public Property RotY       As Single  = 0.1F
    Public Property Zoom       As Single  = 1.0F
    Public Property VelX       As Single  = 0.0F
    Public Property VelY       As Single  = 0.0F
    Public Property OffX       As Single  = 0.0F
    Public Property OffY       As Single  = 0.0F
    Public Property SelTaulaId As Integer = -1
    Public Property SelRelacioId As Integer = -1
    Public Property ShowSphere As Boolean = True
    ' Taules amb infraccions MER — parpadegen en vermell
    Public Property TaulesError As New HashSet(Of Integer)()
    Public Property ErrorBlink  As Boolean = False   ' alterna cada 400ms

    ' ────────────────────────────────────────────────────────────────────
    ' RENDER PRINCIPAL
    ' ────────────────────────────────────────────────────────────────────
    Public Sub Render(canvas As SKCanvas, w As Integer, h As Integer,
                      proyecto As ProyectoBBDD)
        canvas.Clear(New SKColor(AppStyle.ColFons.R, AppStyle.ColFons.G, AppStyle.ColFons.B))
        If proyecto Is Nothing Then Return

        Dim cx As Single = w / 2.0F
        Dim cy As Single = h / 2.0F

        ' ── Gradient radial de fons: tonalitat del tema al centre ───────
        ' Radi = 45% de la dimensió menor, molt subtil (alpha 18)
        Dim gradR As Single = Math.Min(w, h) * 0.45F
        Dim ac As Color = AppStyle.ColAccent
        Using shader As SKShader = SKShader.CreateRadialGradient(
                New SKPoint(cx, cy), gradR,
                New SKColor() {
                    New SKColor(ac.R, ac.G, ac.B, 18),
                    New SKColor(ac.R, ac.G, ac.B, 0)
                },
                New Single() {0.0F, 1.0F},
                SKShaderTileMode.Clamp)
            Using pGrad As New SKPaint()
                pGrad.Shader = shader
                canvas.DrawRect(0, 0, w, h, pGrad)
            End Using
        End Using

        If ShowSphere Then _DibuixEsfera(canvas, cx, cy, proyecto)
        _DibuixRelacions(canvas, cx, cy, proyecto)
        _DibuixTaules(canvas, cx, cy, proyecto)
    End Sub

    ' ────────────────────────────────────────────────────────────────────
    ' MATHS 3D  (idèntic a la simulació HTML)
    ' ────────────────────────────────────────────────────────────────────
    Private Sub RotP(x As Single, y As Single, z As Single,
                     ByRef ox As Single, ByRef oy As Single, ByRef oz As Single)
        Dim cosX As Single = CSng(Math.Cos(RotX))
        Dim sinX As Single = CSng(Math.Sin(RotX))
        Dim cosY As Single = CSng(Math.Cos(RotY))
        Dim sinY As Single = CSng(Math.Sin(RotY))
        Dim y1 As Single = y * cosX - z * sinX
        Dim z1 As Single = y * sinX + z * cosX
        ox = x * cosY + z1 * sinY
        oy = y1
        oz = -x * sinY + z1 * cosY
    End Sub

    Private Function Prj(rx As Single, ry As Single, rz As Single,
                         cx As Single, cy As Single,
                         ByRef pt As SKPoint) As Boolean
        Dim fov As Single = 500.0F / Zoom
        Dim d   As Single = fov + rz + 400.0F
        If d < 5.0F Then Return False
        Dim s As Single = fov / d
        pt = New SKPoint(cx + rx * s + OffX, cy - ry * s + OffY)
        Return True
    End Function

    ' PrjSafe: sempre retorna un punt vàlid, fins i tot si la taula és darrere
    ' o molt lluny. Usat per a les línies de relació perquè mai desapareguin.
    Private Function PrjSafe(rx As Single, ry As Single, rz As Single,
                              cx As Single, cy As Single,
                              ByRef pt As SKPoint) As Boolean
        Dim fov As Single = 500.0F / Zoom
        Dim d   As Single = fov + rz + 400.0F
        If d < 0.5F Then d = 0.5F   ' clampeja en lloc de descartar
        Dim s As Single = fov / d
        pt = New SKPoint(cx + rx * s + OffX, cy - ry * s + OffY)
        Return True
    End Function

    Private Function Sc(rz As Single) As Single
        Dim fov As Single = 500.0F / Zoom
        Dim d   As Single = fov + rz + 400.0F
        If d < 5.0F Then Return 0
        Return fov / d
    End Function

    ' ────────────────────────────────────────────────────────────────────
    ' ESFERA
    ' ────────────────────────────────────────────────────────────────────
    Private Sub _DibuixEsfera(canvas As SKCanvas, cx As Single, cy As Single,
                               p As ProyectoBBDD)
        Dim R As Single = CalcRadi(p)
        Using paint As New SKPaint()
            paint.IsAntialias = True
            paint.Style       = SKPaintStyle.Stroke
            paint.Color       = New SKColor(AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B, 22)
            paint.StrokeWidth = 0.7F

            Const SEG  As Integer = 48
            Const PAR  As Integer = 8
            Const MER  As Integer = 12

            ' Paral·lels
            For i As Integer = 1 To PAR
                Dim phi As Double = (CDbl(i) / CDbl(PAR + 1)) * Math.PI
                Dim rr  As Single = R * CSng(Math.Sin(phi))
                Dim ry0 As Single = R * CSng(Math.Cos(phi))
                Using path As New SKPath()
                    Dim first As Boolean = True
                    For j As Integer = 0 To SEG
                        Dim th As Double = (CDbl(j) / CDbl(SEG)) * Math.PI * 2.0
                        Dim ex As Single, ey As Single, ez As Single
                        RotP(rr * CSng(Math.Cos(th)), ry0, rr * CSng(Math.Sin(th)), ex, ey, ez)
                        Dim pt As SKPoint
                        If Prj(ex, ey, ez, cx, cy, pt) Then
                            If first Then path.MoveTo(pt) Else path.LineTo(pt)
                            first = False
                        End If
                    Next j
                    canvas.DrawPath(path, paint)
                End Using
            Next i

            ' Meridians
            For i As Integer = 0 To MER - 1
                Dim th As Double = (CDbl(i) / CDbl(MER)) * Math.PI * 2.0
                Using path As New SKPath()
                    Dim first As Boolean = True
                    For j As Integer = 0 To SEG
                        Dim phi As Double = (CDbl(j) / CDbl(SEG)) * Math.PI
                        Dim ex As Single, ey As Single, ez As Single
                        RotP(R * CSng(Math.Sin(phi) * Math.Cos(th)),
                             R * CSng(Math.Cos(phi)),
                             R * CSng(Math.Sin(phi) * Math.Sin(th)),
                             ex, ey, ez)
                        Dim pt As SKPoint
                        If Prj(ex, ey, ez, cx, cy, pt) Then
                            If first Then path.MoveTo(pt) Else path.LineTo(pt)
                            first = False
                        End If
                    Next j
                    canvas.DrawPath(path, paint)
                End Using
            Next i
        End Using
    End Sub

    ' ────────────────────────────────────────────────────────────────────
    ' RELACIONS  (suporta auto-relació reflexiva i múltiples per parell)
    ' ────────────────────────────────────────────────────────────────────
    Private Sub _DibuixRelacions(canvas As SKCanvas, cx As Single, cy As Single,
                                  p As ProyectoBBDD)
        If p.Relacions.Count = 0 Then Return

        ' Comptar relacions per parell de taules (per calcular offsets)
        Dim grupCount As New Dictionary(Of String, Integer)()
        Dim grupIdx   As New Dictionary(Of Integer, Integer)()
        For Each r As RelacionBBDD In p.Relacions
            Dim kk As String = Math.Min(r.TablaOrigenId, r.TablaDestinoId) & "_" &
                               Math.Max(r.TablaOrigenId, r.TablaDestinoId)
            If Not grupCount.ContainsKey(kk) Then grupCount(kk) = 0
            grupIdx(r.Id) = grupCount(kk)
            grupCount(kk) += 1
        Next

        Using pLin As New SKPaint() : pLin.IsAntialias = True : pLin.Style = SKPaintStyle.Stroke
        Using pPnt As New SKPaint() : pPnt.IsAntialias = True : pPnt.Style = SKPaintStyle.Fill
        Using pTxt As New SKPaint() : pTxt.IsAntialias = True
        Using fCard As New SKFont(SKTypeface.FromFamilyName("Courier New"), 7.5F)

            For Each r As RelacionBBDD In p.Relacions
                Dim ft As TablaBBDD = Nothing
                Dim tt As TablaBBDD = Nothing
                For Each t As TablaBBDD In p.Taules
                    If t.Id = r.TablaOrigenId  Then ft = t
                    If t.Id = r.TablaDestinoId Then tt = t
                Next
                If ft Is Nothing OrElse tt Is Nothing Then Continue For

                Dim sel As Boolean = (r.Id = SelRelacioId)
                Dim c   As Color   = ft.ColorGL

                ' ── AUTO-RELACIÓ REFLEXIVA ───────────────────────────────
                If r.TablaOrigenId = r.TablaDestinoId Then
                    Dim ex As Single, ey As Single, ez As Single
                    RotP(ft.PosX, ft.PosY, ft.PosZ, ex, ey, ez)
                    Dim pt As SKPoint
                    If Not PrjSafe(ex, ey, ez, cx, cy, pt) Then Continue For
                    Dim sc2 As Single = Sc(ez)
                    If sc2 < 0.05F Then Continue For
                    Dim hw As Single = CalcularHalfW(ft) * sc2
                    Dim loopR As Single = hw * 0.55F
                    Dim loopCx As Single = pt.X + hw * 0.85F
                    Dim loopCy As Single = pt.Y - loopR
                    pLin.Color       = New SKColor(c.R, c.G, c.B, If(sel, CByte(230), CByte(160)))
                    pLin.StrokeWidth = If(sel, 2.5F, 1.4F)
                    canvas.DrawCircle(loopCx, loopCy, loopR, pLin)
                    pPnt.Color = New SKColor(c.R, c.G, c.B, If(sel, CByte(255), CByte(190)))
                    canvas.DrawCircle(loopCx, loopCy + loopR, If(sel, 4.0F, 2.5F), pPnt)
                    pTxt.Color = New SKColor(c.R, c.G, c.B, If(sel, CByte(220), CByte(130)))
                    Using fLp As New SKFont(SKTypeface.FromFamilyName("Courier New"),
                                            If(sel, 7.5F, 6.5F))
                        canvas.DrawText(r.CardinalityLabel, loopCx + loopR + 2, loopCy, fLp, pTxt)
                    End Using
                    ' Indicador múltiples
                    Dim kSelf As String = r.TablaOrigenId & "_" & r.TablaOrigenId
                    If grupCount.ContainsKey(kSelf) AndAlso grupCount(kSelf) > 1 Then
                        Using pmul As New SKPaint()
                            pmul.IsAntialias = True : pmul.Style = SKPaintStyle.Fill
                            pmul.Color = New SKColor(AppStyle.ColPK.R, AppStyle.ColPK.G, AppStyle.ColPK.B, 200)
                            canvas.DrawCircle(loopCx - loopR - 4, loopCy, 3.5F, pmul)
                        End Using
                    End If
                    Continue For
                End If

                ' ── RELACIÓ NORMAL ───────────────────────────────────────
                Dim ax As Single, ay As Single, az As Single
                Dim bx As Single, by As Single, bz As Single
                RotP(ft.PosX, ft.PosY, ft.PosZ, ax, ay, az)
                RotP(tt.PosX, tt.PosY, tt.PosZ, bx, by, bz)
                Dim pA As SKPoint, pB As SKPoint
                If Not PrjSafe(ax, ay, az, cx, cy, pA) Then Continue For
                If Not PrjSafe(bx, by, bz, cx, cy, pB) Then Continue For

                Dim kPair As String = Math.Min(r.TablaOrigenId, r.TablaDestinoId) & "_" &
                                      Math.Max(r.TablaOrigenId, r.TablaDestinoId)
                Dim nPair As Integer = If(grupCount.ContainsKey(kPair), grupCount(kPair), 1)
                Dim iRel  As Integer = If(grupIdx.ContainsKey(r.Id), grupIdx(r.Id), 0)
                Dim perp  As Single  = 0.0F
                If nPair > 1 Then perp = (iRel - (nPair - 1) / 2.0F) * 10.0F

                Dim ddx As Single = pB.X - pA.X
                Dim ddy As Single = pB.Y - pA.Y
                Dim dlen As Single = CSng(Math.Sqrt(ddx * ddx + ddy * ddy))
                Dim nx As Single = If(dlen > 0.001F, -ddy / dlen, 0.0F)
                Dim ny As Single = If(dlen > 0.001F,  ddx / dlen, 0.0F)

                Dim qA As New SKPoint(pA.X + nx * perp, pA.Y + ny * perp)
                Dim qB As New SKPoint(pB.X + nx * perp, pB.Y + ny * perp)

                pLin.Color       = New SKColor(c.R, c.G, c.B, If(sel, CByte(220), CByte(140)))
                pLin.StrokeWidth = If(sel, 2.2F, 1.1F)
                canvas.DrawLine(qA.X, qA.Y, qB.X, qB.Y, pLin)

                Dim mx As Single = (qA.X + qB.X) / 2.0F
                Dim my As Single = (qA.Y + qB.Y) / 2.0F
                pPnt.Color = New SKColor(c.R, c.G, c.B, If(sel, CByte(255), CByte(180)))
                canvas.DrawCircle(mx, my, If(sel, 4.5F, 2.8F), pPnt)

                ' Indicador "+" si múltiples relacions
                If nPair > 1 Then
                    Using pmul As New SKPaint()
                        pmul.IsAntialias = True : pmul.Style = SKPaintStyle.Fill
                        pmul.Color = New SKColor(AppStyle.ColPK.R, AppStyle.ColPK.G, AppStyle.ColPK.B, 200)
                        canvas.DrawCircle(mx + 7, my - 7, 3.5F, pmul)
                    End Using
                End If

                If sel Then
                    pTxt.Color = New SKColor(c.R, c.G, c.B, 220)
                    canvas.DrawText(r.CardinalityLabel, mx + 6, my - 3, fCard, pTxt)
                Else
                    pTxt.Color = New SKColor(c.R, c.G, c.B, 120)
                    Using fCardSm As New SKFont(SKTypeface.FromFamilyName("Courier New"), 6.5F)
                        canvas.DrawText(r.CardinalityLabel, mx + 5, my - 2, fCardSm, pTxt)
                    End Using
                End If
            Next

        End Using : End Using : End Using : End Using
    End Sub

    ' ────────────────────────────────────────────────────────────────────
    ' TAULES (depth sort)
    ' ────────────────────────────────────────────────────────────────────

    ' Retorna si la taula tId esta directament relacionada amb la taula seleccionada
    Private Function EstaRelacionada(tId As Integer, p As ProyectoBBDD) As Boolean
        If SelTaulaId < 0 Then Return False
        For Each r As RelacionBBDD In p.Relacions
            If (r.TablaOrigenId = SelTaulaId AndAlso r.TablaDestinoId = tId) OrElse
               (r.TablaDestinoId = SelTaulaId AndAlso r.TablaOrigenId = tId) Then
                Return True
            End If
        Next
        Return False
    End Function

    Private Sub _DibuixTaules(canvas As SKCanvas, cx As Single, cy As Single,
                               p As ProyectoBBDD)
        If p.Taules.Count = 0 Then Return

        ' Depth sort: de Z menor (lluny) a Z major (prop)
        Dim sorted As New System.Collections.Generic.List(Of (Z As Single, T As TablaBBDD))()
        For Each t As TablaBBDD In p.Taules
            Dim ex As Single, ey As Single, ez As Single
            RotP(t.PosX, t.PosY, t.PosZ, ex, ey, ez)
            sorted.Add((ez, t))
        Next
        sorted.Sort(Function(a, b) a.Z.CompareTo(b.Z))

        For Each item In sorted
            Dim ex As Single, ey As Single, ez As Single
            RotP(item.T.PosX, item.T.PosY, item.T.PosZ, ex, ey, ez)
            Dim pt As SKPoint
            If Not Prj(ex, ey, ez, cx, cy, pt) Then Continue For
            Dim sBase As Single = Sc(ez)
            If sBase < 0.05F Then Continue For

            ' Factor de mida visual: sel=×1.5, relacionada=×1.25, resta=×1.0
            Dim sFactor As Single = 1.0F
            If item.T.Id = SelTaulaId Then
                sFactor = 1.5F
            ElseIf EstaRelacionada(item.T.Id, p) Then
                sFactor = 1.25F
            End If

            _DibuixTaula(canvas, item.T, pt, sBase * sFactor)
        Next
    End Sub

    Private Sub _DibuixTaula(canvas As SKCanvas, t As TablaBBDD,
                              centre As SKPoint, s As Single)
        Dim sel  As Boolean = (t.Id = SelTaulaId)
        Dim cBase As Color = t.ColorGL

        ' Atenuació per taules no-hub del grup (Depth 0.75 = atenuat, 1.0 = hub/normal)
        ' Barrejar el color del grup amb gris fosc proporcional a (1 - Depth)
        Dim atFactor As Single = If(t.Depth >= 1.0F, 1.0F, t.Depth * 0.85F + 0.15F)
        Dim c As Color = Color.FromArgb(
            CInt(cBase.R * atFactor),
            CInt(cBase.G * atFactor),
            CInt(cBase.B * atFactor))

        ' ── Amplada fixa calculada a s=1 (no canvia amb el zoom) ────────
        ' La font s'escala amb s, però l'amplada de la caixa és constant.
        Dim halfW   As Single = CalcularHalfW(t) * s
        Dim altCapc As Single = 16.8F * s
        Dim altCamp As Single = 13.2F * s
        Dim altTot  As Single = altCapc + t.Fields.Count * altCamp + 4.8F * s
        Dim x0  As Single = centre.X - halfW
        Dim y0  As Single = centre.Y - altTot / 2.0F
        Dim rw  As Single = halfW * 2.0F
        Dim rad As Single = 3.0F * s

        Dim rMain As New SKRoundRect(New SKRect(x0, y0, x0 + rw, y0 + altTot), rad, rad)
        Dim rCapc As New SKRoundRect(New SKRect(x0, y0, x0 + rw, y0 + altCapc), rad, rad)

        ' ── Fons ────────────────────────────────────────────────────────
        Using paint As New SKPaint()
            paint.IsAntialias = True
            paint.Style = SKPaintStyle.Fill
            paint.Color = New SKColor(c.R, c.G, c.B, If(sel, CByte(60), CByte(38)))
            canvas.DrawRoundRect(rMain, paint)
        End Using

        ' ── Capçalera ────────────────────────────────────────────────────
        Using paint As New SKPaint()
            paint.IsAntialias = True
            paint.Style = SKPaintStyle.Fill
            paint.Color = New SKColor(c.R, c.G, c.B, 70)
            canvas.DrawRoundRect(rCapc, paint)
            ' Cobrir cantonades inferiors de la capçalera
            Dim rCbot As SKRect = New SKRect(x0, y0 + rad, x0 + rw, y0 + altCapc)
            canvas.DrawRect(rCbot, paint)
        End Using

        ' ── Contorn ──────────────────────────────────────────────────────
        Using paint As New SKPaint()
            paint.IsAntialias = True
            paint.Style       = SKPaintStyle.Stroke
            paint.StrokeWidth = If(sel, 2.0F, 1.0F)
            paint.Color = New SKColor(c.R, c.G, c.B, If(sel, CByte(255), CByte(200)))
            canvas.DrawRoundRect(rMain, paint)
        End Using

        ' ── Línia separadora capçalera ────────────────────────────────────
        Using paint As New SKPaint()
            paint.Style       = SKPaintStyle.Stroke
            paint.StrokeWidth = 0.7F
            paint.Color       = New SKColor(c.R, c.G, c.B, 140)
            canvas.DrawLine(x0, y0 + altCapc, x0 + rw, y0 + altCapc, paint)
        End Using

        ' ── Glow (seleccionada) ───────────────────────────────────────────
        If sel Then
            Using paint As New SKPaint()
                paint.IsAntialias = True
                paint.Style       = SKPaintStyle.Stroke
                paint.StrokeWidth = 5.0F
                paint.Color       = New SKColor(c.R, c.G, c.B, 40)
                paint.MaskFilter  = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6.0F)
                canvas.DrawRoundRect(rMain, paint)
            End Using
        End If

        ' ── Nom de la taula ───────────────────────────────────────────────
        If s >= 0.2F Then
            Dim sz As Single = Math.Max(7.0F, 11.0F * s)
            Using fnt As New SKFont(
                    SKTypeface.FromFamilyName("Courier New",
                        SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                    sz)
            Using paint As New SKPaint()
                paint.IsAntialias = True
                paint.Color = New SKColor(c.R, c.G, c.B, If(sel, CByte(255), CByte(220)))
                Dim tw As Single = paint.MeasureText(t.Nombre)
                ' Retallar el títol dins la capçalera
                canvas.Save()
                canvas.ClipRect(New SKRect(x0 + 1, y0, x0 + rw - 1, y0 + altCapc))
                canvas.DrawText(t.Nombre,
                                centre.X - tw / 2.0F,
                                y0 + altCapc * 0.72F,
                                fnt, paint)
                canvas.Restore()
            End Using : End Using
        End If

        ' ── Camps ────────────────────────────────────────────────────────
        If s >= 0.35F AndAlso t.Fields.Count > 0 Then
            Dim sz As Single = Math.Max(6.0F, 8.5F * s)
            Using fnt As New SKFont(
                    SKTypeface.FromFamilyName("Courier New"),
                    sz)
            Using pCamp  As New SKPaint() : pCamp.IsAntialias = True
            Using pTipus As New SKPaint() : pTipus.IsAntialias = True
                                            pTipus.Color = New SKColor(AppStyle.ColType.R, AppStyle.ColType.G, AppStyle.ColType.B, 170)

                Dim maxVis As Integer = t.Fields.Count   ' mostrar TOTS els camps

                ' Retallar el text estrictament dins la caixa
                canvas.Save()
                canvas.ClipRect(New SKRect(x0 + 1, y0 + altCapc, x0 + rw - 1, y0 + altTot - 1))

                For i As Integer = 0 To maxVis - 1
                    Dim f      As CampoBBDD = t.Fields(i)
                    Dim yLine  As Single    = y0 + altCapc + i * altCamp

                    ' Línia separadora
                    If i > 0 Then
                        Using pl As New SKPaint()
                            pl.Style       = SKPaintStyle.Stroke
                            pl.StrokeWidth = 0.4F
                            pl.Color       = New SKColor(c.R, c.G, c.B, 35)
                            canvas.DrawLine(x0 + 2, yLine, x0 + rw - 2, yLine, pl)
                        End Using
                    End If

                    ' Indicador PK (franja esquerra groga)
                    If f.EsPK Then
                        Using pp As New SKPaint()
                            pp.Style = SKPaintStyle.Fill
                            pp.Color = New SKColor(AppStyle.ColPK.R, AppStyle.ColPK.G, AppStyle.ColPK.B, 100)
                            canvas.DrawRect(
                                New SKRect(x0, yLine, x0 + 3.5F * s, yLine + altCamp),
                                pp)
                        End Using
                    End If

                    ' Color text
                    If f.EsPK Then
                        pCamp.Color = New SKColor(AppStyle.ColPK.R, AppStyle.ColPK.G, AppStyle.ColPK.B, 220)
                    ElseIf f.EsFK Then
                        pCamp.Color = New SKColor(AppStyle.ColFK.R, AppStyle.ColFK.G, AppStyle.ColFK.B, 200)
                    Else
                        pCamp.Color = New SKColor(AppStyle.ColTextSec.R, AppStyle.ColTextSec.G, AppStyle.ColTextSec.B, 190)
                    End If

                    Dim prefix  As String  = If(f.EsPK, "K ", If(f.EsFK, "F ", "  "))
                    Dim nomText As String  = prefix & f.Nombre
                    Dim yBase   As Single  = yLine + altCamp * 0.75F
                    canvas.DrawText(nomText, x0 + 5.0F * s, yBase, fnt, pCamp)

                    ' Tipus SQL (dreta)
                    If s >= 0.5F Then
                        Dim tw As Single = pTipus.MeasureText(f.EtiquetaTipus)
                        canvas.DrawText(f.EtiquetaTipus,
                                        x0 + rw - tw - 4.0F * s,
                                        yBase, fnt, pTipus)
                    End If
                Next i

                canvas.Restore()

            End Using : End Using : End Using
        End If

        ' ── Contorn d'error MER (parpadeig vermell) ──────────────
        If TaulesError.Contains(t.Id) AndAlso ErrorBlink Then
            Dim hw As Single = CalcularHalfW(t) * s * 1.08F
            Dim hh As Single = (16.8F + t.Fields.Count * 13.2F + 4.8F) * s / 2.0F * 1.08F
            Dim rErr As New SKRoundRect(
                New SKRect(centre.X - hw, centre.Y - hh, centre.X + hw, centre.Y + hh),
                4 * s, 4 * s)
            Using pErr As New SKPaint()
                pErr.IsAntialias = True
                pErr.Style       = SKPaintStyle.Stroke
                pErr.StrokeWidth = 2.5F * s
                pErr.Color       = New SKColor(255, 60, 30, 220)
            End Using
            ' Segon pas: glow exterior
            Using pGlow As New SKPaint()
                pGlow.IsAntialias  = True
                pGlow.Style        = SKPaintStyle.Stroke
                pGlow.StrokeWidth  = 5.0F * s
                pGlow.Color        = New SKColor(255, 40, 0, 60)
                canvas.DrawRoundRect(rErr, pGlow)
            End Using
            Using pErr2 As New SKPaint()
                pErr2.IsAntialias = True
                pErr2.Style       = SKPaintStyle.Stroke
                pErr2.StrokeWidth = 2.5F * s
                pErr2.Color       = New SKColor(255, 60, 30, 220)
                canvas.DrawRoundRect(rErr, pErr2)
            End Using
        End If
    End Sub

    ' Exposat públicament per a MnuVeureTot (càlcul del bounding box)
    Public Function CalcularHalfWPublic(t As TablaBBDD) As Single
        Return CalcularHalfW(t)
    End Function

    Public Function CalcScalePublic(rx As Single, ry As Single, rz As Single) As Single
        Dim fov As Single = 500.0F / Zoom
        Dim d   As Single = fov + rz + 400.0F
        Return fov / d
    End Function

    ' ── Mètodes públics per a l'òrbita pivot (usats per FrmDesigner) ─────
    Public Sub RotPPublic(x As Single, y As Single, z As Single,
                          ByRef ox As Single, ByRef oy As Single, ByRef oz As Single)
        RotP(x, y, z, ox, oy, oz)
    End Sub

    Public Function PrjPublic(rx As Single, ry As Single, rz As Single,
                               cx As Single, cy As Single,
                               ByRef pt As SkiaSharp.SKPoint) As Boolean
        Return PrjSafe(rx, ry, rz, cx, cy, pt)
    End Function

    ''' <summary>
    ''' Desprojecta el cursor 2D a un punt 3D de l'escena.
    ''' Cerca la taula més propera al cursor i usa la seva profunditat,
    ''' o calcula un punt a la superfície de l'esfera si no n'hi ha cap.
    ''' </summary>
    Public Function UnprojectCursor(mx As Integer, my As Integer,
                                     w As Integer, h As Integer,
                                     p As ProyectoBBDD,
                                     ByRef wx As Single, ByRef wy As Single,
                                     ByRef wz As Single) As Boolean
        If p Is Nothing OrElse p.Taules.Count = 0 Then Return False
        Dim cx As Single = w / 2.0F
        Dim cy As Single = h / 2.0F
        Dim fov As Single = 500.0F / Zoom

        ' Buscar la taula visualment més propera al cursor
        Dim bestDist As Single = Single.MaxValue
        Dim bestT    As TablaBBDD = Nothing
        For Each t As TablaBBDD In p.Taules
            Dim ex As Single, ey As Single, ez As Single
            RotP(t.PosX, t.PosY, t.PosZ, ex, ey, ez)
            Dim pt As New SkiaSharp.SKPoint()
            If Not Prj(ex, ey, ez, cx, cy, pt) Then Continue For
            Dim d As Single = CSng(Math.Sqrt((mx - pt.X) * (mx - pt.X) +
                                             (my - pt.Y) * (my - pt.Y)))
            If d < bestDist Then
                bestDist = d
                bestT = t
            End If
        Next

        If bestT IsNot Nothing AndAlso bestDist < 200.0F Then
            wx = bestT.PosX
            wy = bestT.PosY
            wz = bestT.PosZ
            Return True
        End If

        ' Si no hi ha cap taula a prop: punt a la superfície de l'esfera
        ' Calcular punt 3D a partir del raig de càmera
        Dim R As Single = CalcRadi(p)
        Dim ndcX As Single = (mx - cx - OffX) / fov
        Dim ndcY As Single = -(my - cy - OffY) / fov
        ' Normalitzar a la superfície de l'esfera
        Dim len As Single = CSng(Math.Sqrt(ndcX * ndcX + ndcY * ndcY + 1.0F))
        Dim rDirX As Single = ndcX / len
        Dim rDirY As Single = ndcY / len
        Dim rDirZ As Single = 1.0F / len
        ' Inversa de la rotació per obtenir la direcció en espai món
        Dim cosX As Single = CSng(Math.Cos(-RotX))
        Dim sinX As Single = CSng(Math.Sin(-RotX))
        Dim cosY As Single = CSng(Math.Cos(-RotY))
        Dim sinY As Single = CSng(Math.Sin(-RotY))
        Dim y1 As Single = rDirY * cosX - rDirZ * sinX
        Dim z1 As Single = rDirY * sinX + rDirZ * cosX
        Dim wdX As Single = rDirX * cosY + z1 * sinY
        Dim wdY As Single = y1
        Dim wdZ As Single = -rDirX * sinY + z1 * cosY
        wx = wdX * R
        wy = wdY * R
        wz = wdZ * R
        Return True
    End Function

    ' ────────────────────────────────────────────────────────────────────
    ' CALCULAR AMPLADA DINÀMICA D'UNA TAULA (compartit per render i hit-test)
    ' ────────────────────────────────────────────────────────────────────
    ' Calcula l'amplada base de la caixa a escala s=1.0.
    ' Els cridadors multipliquen el resultat per s per obtenir l'amplada real.
    Private Function CalcularHalfW(t As TablaBBDD) As Single
        Dim hw As Single = 55.0F  ' mínim base

        Using p As New SKPaint()
            p.IsAntialias = True

            ' Títol (Bold)
            p.Typeface = SKTypeface.FromFamilyName("Courier New",
                SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            p.TextSize = 11.0F
            Dim wTit As Single = p.MeasureText(t.Nombre)
            hw = Math.Max(hw, wTit / 2.0F + 10.0F)

            ' Camps (Normal)
            p.Typeface = SKTypeface.FromFamilyName("Courier New")
            p.TextSize = 8.5F
            For Each f As CampoBBDD In t.Fields
                Dim prefix As String = If(f.EsPK, "K ", If(f.EsFK, "F ", "  "))
                Dim wNom   As Single = p.MeasureText(prefix & f.Nombre)
                Dim wTipus As Single = p.MeasureText(f.EtiquetaTipus)
                hw = Math.Max(hw, (5.0F + wNom + 6.0F + wTipus + 4.0F) / 2.0F + 2.0F)
            Next
        End Using
        Return hw
    End Function

    ' ────────────────────────────────────────────────────────────────────
    ' HIT-TEST
    ' ────────────────────────────────────────────────────────────────────
    Public Function HitTestTaula(mx As Integer, my As Integer,
                                  w As Integer, h As Integer,
                                  p As ProyectoBBDD) As Integer
        If p Is Nothing Then Return -1
        Dim cx As Single = w / 2.0F
        Dim cy As Single = h / 2.0F
        Dim bestId As Integer = -1
        Dim bestZ  As Single  = Single.MinValue
        For Each t As TablaBBDD In p.Taules
            Dim ex As Single, ey As Single, ez As Single
            RotP(t.PosX, t.PosY, t.PosZ, ex, ey, ez)
            Dim pt As SKPoint
            If Not Prj(ex, ey, ez, cx, cy, pt) Then Continue For
            Dim s As Single = Sc(ez)
            If s < 0.05F Then Continue For
            ' Aplicar mateix factor de mida visual que en render
            Dim sFactor As Single = 1.0F
            If t.Id = SelTaulaId Then
                sFactor = 1.5F
            ElseIf EstaRelacionada(t.Id, p) Then
                sFactor = 1.25F
            End If
            Dim se As Single = s * sFactor
            Dim hw As Single = CalcularHalfW(t) * se
            Dim hh As Single = (16.8F + t.Fields.Count * 13.2F + 4.8F) * se / 2.0F
            If Math.Abs(mx - pt.X) < hw AndAlso Math.Abs(my - pt.Y) < hh Then
                If ez > bestZ Then
                    bestZ  = ez
                    bestId = t.Id
                End If
            End If
        Next
        Return bestId
    End Function

    ' Retorna l'Id de la relació que és a menys de 8px del punt (mx, my)
    Public Function HitTestRelacio(mx As Integer, my As Integer,
                                    w As Integer, h As Integer,
                                    p As ProyectoBBDD) As Integer
        If p Is Nothing Then Return -1
        Dim cx As Single = w / 2.0F
        Dim cy As Single = h / 2.0F
        Const DIST As Single = 10.0F
        Dim bestId As Integer = -1
        Dim bestD  As Single  = Single.MaxValue
        For Each r As RelacionBBDD In p.Relacions
            Dim ft As TablaBBDD = Nothing
            Dim tt As TablaBBDD = Nothing
            For Each t As TablaBBDD In p.Taules
                If t.Id = r.TablaOrigenId  Then ft = t
                If t.Id = r.TablaDestinoId Then tt = t
            Next
            If ft Is Nothing OrElse tt Is Nothing Then Continue For

            ' Auto-relació: hit sobre el bucle circular
            If r.TablaOrigenId = r.TablaDestinoId Then
                Dim ex As Single, ey As Single, ez As Single
                RotP(ft.PosX, ft.PosY, ft.PosZ, ex, ey, ez)
                Dim pt As SKPoint
                If Not Prj(ex, ey, ez, cx, cy, pt) Then Continue For
                Dim sc2 As Single = Sc(ez)
                If sc2 < 0.05F Then Continue For
                Dim hw2 As Single = CalcularHalfW(ft) * sc2
                Dim loopR2 As Single = hw2 * 0.55F
                Dim loopCx2 As Single = pt.X + hw2 * 0.85F
                Dim loopCy2 As Single = pt.Y - loopR2
                ' Distància al centre del bucle
                Dim dLoop As Single = CSng(Math.Abs(
                    Math.Sqrt((mx - loopCx2) * (mx - loopCx2) +
                              (my - loopCy2) * (my - loopCy2)) - loopR2))
                If dLoop < DIST AndAlso dLoop < bestD Then
                    bestD  = dLoop
                    bestId = r.Id
                End If
                Continue For
            End If

            ' Relació normal: distància punt-segment
            Dim ax As Single, ay As Single, az As Single
            Dim bx As Single, by As Single, bz As Single
            RotP(ft.PosX, ft.PosY, ft.PosZ, ax, ay, az)
            RotP(tt.PosX, tt.PosY, tt.PosZ, bx, by, bz)
            Dim pA As SKPoint, pB As SKPoint
            If Not Prj(ax, ay, az, cx, cy, pA) Then Continue For
            If Not Prj(bx, by, bz, cx, cy, pB) Then Continue For
            Dim ddx As Single = pB.X - pA.X
            Dim ddy As Single = pB.Y - pA.Y
            Dim lenSq As Single = ddx * ddx + ddy * ddy
            Dim d As Single
            If lenSq < 0.001F Then
                d = CSng(Math.Sqrt((mx - pA.X) * (mx - pA.X) + (my - pA.Y) * (my - pA.Y)))
            Else
                Dim t2 As Single = ((mx - pA.X) * ddx + (my - pA.Y) * ddy) / lenSq
                t2 = Math.Max(0.0F, Math.Min(1.0F, t2))
                Dim ppx As Single = pA.X + t2 * ddx
                Dim ppy As Single = pA.Y + t2 * ddy
                d = CSng(Math.Sqrt((mx - ppx) * (mx - ppx) + (my - ppy) * (my - ppy)))
            End If
            If d < DIST AndAlso d < bestD Then
                bestD  = d
                bestId = r.Id
            End If
        Next
        Return bestId
    End Function

    ' Retorna totes les relacions que connecten les taules A i B (o auto-relació A=A)
    Public Shared Function RelacionsEntreTaules(p As ProyectoBBDD,
                                                 idA As Integer,
                                                 idB As Integer) As List(Of RelacionBBDD)
        Dim res As New List(Of RelacionBBDD)()
        For Each r As RelacionBBDD In p.Relacions
            If (r.TablaOrigenId = idA AndAlso r.TablaDestinoId = idB) OrElse
               (r.TablaOrigenId = idB AndAlso r.TablaDestinoId = idA) Then
                res.Add(r)
            End If
        Next
        Return res
    End Function

    ' Centra el holograma resetejant rotació i calculant zoom per veure totes les taules
    Public Sub CentrarHolograma(p As ProyectoBBDD)
        RotX = 0.2F
        RotY = 0.0F
        VelX = 0.0F
        VelY = 0.0F
        OffX = 0.0F
        OffY = 0.0F
        Zoom = 1.0F
    End Sub

    ' ────────────────────────────────────────────────────────────────────
    ' UTILITATS COMPARTIDES
    ' ────────────────────────────────────────────────────────────────────
    Public Shared Function CalcRadi(p As ProyectoBBDD) As Single
        If p Is Nothing OrElse p.Taules.Count = 0 Then Return 200.0F
        Dim maxR As Single = 0
        For Each t As TablaBBDD In p.Taules
            Dim r As Single = CSng(Math.Sqrt(
                t.PosX * t.PosX + t.PosY * t.PosY + t.PosZ * t.PosZ))
            If r > maxR Then maxR = r
        Next
        Return Math.Max(200.0F, maxR * 1.08F)
    End Function

    Public Shared Sub DistribuirTaules(p As ProyectoBBDD)
        Dim n As Integer = p.Taules.Count
        If n = 0 Then Return

        ' Separar taules amb posició de les que necessiten posició nova
        Dim tNoves As New List(Of TablaBBDD)()
        For Each t As TablaBBDD In p.Taules
            If t.PosX = 0.0F AndAlso t.PosY = 0.0F AndAlso t.PosZ = 0.0F Then
                tNoves.Add(t)
            End If
        Next
        If tNoves.Count = 0 Then Return

        Dim R As Single = Math.Max(200.0F, 110.0F + n * 22.0F)
        Dim nNoves As Integer = tNoves.Count

        ' Distribuïm les taules noves en posicions de Fibonacci entre si,
        ' desfasades respecte les existents per evitar col·lisions.
        ' Desfasament: nombre de taules existents per saltar punts ja ocupats.
        Dim nExist As Integer = n - nNoves
        For i As Integer = 0 To nNoves - 1
            Dim t As TablaBBDD = tNoves(i)
            ' Índex global desplaçat per les existents
            Dim ig As Integer = nExist + i
            Dim phi   As Single = CSng(Math.Acos(1.0 - 2.0 * (ig + 0.5) / n))
            Dim theta As Single = CSng(Math.PI * (1.0 + Math.Sqrt(5.0)) * ig)
            Dim rr    As Single = R * If(t.Depth > 0.0F, t.Depth, 1.0F)
            t.PosX = rr * CSng(Math.Sin(phi) * Math.Cos(theta))
            t.PosY = rr * CSng(Math.Cos(phi))
            t.PosZ = rr * CSng(Math.Sin(phi) * Math.Sin(theta))
            ' Garantir que no acabi exactament a (0,0,0) — desplaçament mínim
            If t.PosX = 0.0F AndAlso t.PosY = 0.0F AndAlso t.PosZ = 0.0F Then
                t.PosY = R * If(t.Depth > 0.0F, t.Depth, 1.0F)
            End If
        Next i
    End Sub

    Public Shared Sub RedistribuirTotes(p As ProyectoBBDD)
        Dim n As Integer = p.Taules.Count
        If n = 0 Then Return
        Dim R As Single = Math.Max(200.0F, 110.0F + n * 22.0F)
        For i As Integer = 0 To n - 1
            Dim t     As TablaBBDD = p.Taules(i)
            Dim phi   As Single    = CSng(Math.Acos(1.0 - 2.0 * (i + 0.5) / n))
            Dim theta As Single    = CSng(Math.PI * (1.0 + Math.Sqrt(5.0)) * i)
            Dim rr    As Single    = R * t.Depth
            t.PosX = rr * CSng(Math.Sin(phi) * Math.Cos(theta))
            t.PosY = rr * CSng(Math.Cos(phi))
            t.PosZ = rr * CSng(Math.Sin(phi) * Math.Sin(theta))
        Next i
    End Sub

End Class
