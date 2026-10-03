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
Imports System.Drawing

' ============================================================
' OrganitzadorGrups.vb — Distribució automàtica de les taules
' ("Organitzar per grups").
'
'   1. Detecta grups (comunitats) al graf de relacions amb Label
'      Propagation determinista: el mateix model dona sempre el
'      mateix resultat.
'   2. Col·loca cada grup al costat del grup amb què té més
'      relacions, i les taules del grup al voltant del seu hub.
'   3. Refina amb un model de forces: les relacions atrauen, les
'      taules es repel·leixen segons la seva mida real, cada grup
'      es manté compacte i una gravetat suau evita que cap grup
'      quedi lluny de la resta.
'   4. Elimina qualsevol encavalcament de caixes.
'   5. Les taules sense relacions van en una graella compacta
'      just a sota del conjunt.
'
' No modifica el model: retorna les posicions i colors calculats
' (el dissenyador els aplica amb animació i es poden desfer).
' Treballa en el pla XY (Z = 0): vist de front, el diagrama es
' llegeix com un pla net; en girar-lo continua sent hologràfic.
' ============================================================
Public Module OrganitzadorGrups

    ' Separació mínima entre caixes (unitats del món, a escala 1)
    Public Const MARGE_X As Single = 34.0F
    Public Const MARGE_Y As Single = 26.0F

    ' Resolució de Louvain: > 1 afavoreix grups més petits (mòduls units per
    ' una sola relació queden com a grups separats però propers)
    Private Const RESOLUCIO As Double = 1.2

    Public Class Resultat
        ''' <summary>Id de taula → {X, Y, Z}</summary>
        Public ReadOnly Posicions As New Dictionary(Of Integer, Single())()
        ''' <summary>Id de taula → índex de grup (−1 = taula sense relacions)</summary>
        Public ReadOnly Grup As New Dictionary(Of Integer, Integer)()
        Public ReadOnly Colors As New Dictionary(Of Integer, GroupColor)()
        ''' <summary>Taula central (amb més relacions) de cada grup</summary>
        Public ReadOnly Hubs As New HashSet(Of Integer)()
        Public NombreGrups As Integer
    End Class

    Private ReadOnly Paleta As GroupColor() = {
        GroupColor.ColorRed, GroupColor.ColorBlue, GroupColor.ColorGreen,
        GroupColor.ColorYellow, GroupColor.ColorCyan, GroupColor.ColorMagenta,
        GroupColor.ColorOrange}

    ''' <param name="mitjaMida">Mida de cada taula com a mitja amplada i mitja alçada
    ''' (unitats del món a escala 1, les mateixes que fa servir el renderer).</param>
    Public Function Calcular(taules As IList(Of TablaBBDD), relacions As IList(Of RelacionBBDD),
                             mitjaMida As Func(Of TablaBBDD, SizeF)) As Resultat
        Dim res As New Resultat()
        Dim n As Integer = taules.Count
        If n = 0 Then Return res

        ' ── Graf no dirigit ─────────────────────────────────────
        Dim idx As New Dictionary(Of Integer, Integer)()
        For i As Integer = 0 To n - 1 : idx(taules(i).Id) = i : Next
        Dim veins(n - 1) As HashSet(Of Integer)
        For i As Integer = 0 To n - 1 : veins(i) = New HashSet(Of Integer)() : Next
        For Each r As RelacionBBDD In relacions
            Dim a As Integer, b As Integer
            If Not idx.TryGetValue(r.TablaOrigenId, a) OrElse Not idx.TryGetValue(r.TablaDestinoId, b) Then Continue For
            If a = b Then Continue For
            veins(a).Add(b) : veins(b).Add(a)
        Next
        Dim arestes As New List(Of Integer())()
        For a As Integer = 0 To n - 1
            For Each b As Integer In veins(a)
                If a < b Then arestes.Add({a, b})
            Next
        Next

        Dim hw(n - 1) As Single, hh(n - 1) As Single, radi(n - 1) As Single
        For i As Integer = 0 To n - 1
            Dim m As SizeF = mitjaMida(taules(i))
            hw(i) = Math.Max(20.0F, m.Width)
            hh(i) = Math.Max(12.0F, m.Height)
            radi(i) = CSng(Math.Sqrt(hw(i) * hw(i) + hh(i) * hh(i)))
        Next

        ' ── 1. Grups ────────────────────────────────────────────
        Dim grup() As Integer = DetectarGrups(n, veins, taules)
        Dim nGrups As Integer = 0
        For i As Integer = 0 To n - 1
            If grup(i) >= nGrups Then nGrups = grup(i) + 1
        Next
        res.NombreGrups = nGrups

        Dim membres(Math.Max(nGrups, 1) - 1) As List(Of Integer)
        For g As Integer = 0 To membres.Length - 1 : membres(g) = New List(Of Integer)() : Next
        Dim aillades As New List(Of Integer)()
        For i As Integer = 0 To n - 1
            If grup(i) < 0 Then aillades.Add(i) Else membres(grup(i)).Add(i)
        Next

        ' Hub de cada grup: la taula amb més relacions
        Dim hub(Math.Max(nGrups, 1) - 1) As Integer
        For g As Integer = 0 To nGrups - 1
            hub(g) = membres(g)(0)
            For Each i As Integer In membres(g)
                If veins(i).Count > veins(hub(g)).Count Then hub(g) = i
            Next
        Next

        ' Relacions entre grups (pes = nombre de relacions)
        Dim pesGrups As New Dictionary(Of Long, Integer)()
        For Each e As Integer() In arestes
            Dim ga As Integer = grup(e(0)), gb As Integer = grup(e(1))
            If ga = gb Then Continue For
            Dim k As Long = CLng(Math.Min(ga, gb)) * 100000L + Math.Max(ga, gb)
            pesGrups(k) = If(pesGrups.ContainsKey(k), pesGrups(k), 0) + 1
        Next

        ' ── 2. Posicions inicials ───────────────────────────────
        Dim x(n - 1) As Single, y(n - 1) As Single
        Dim radiGrup(Math.Max(nGrups, 1) - 1) As Single
        For g As Integer = 0 To nGrups - 1
            Dim area As Single = 0
            For Each i As Integer In membres(g)
                area += (2 * hw(i) + MARGE_X * 2) * (2 * hh(i) + MARGE_Y * 2)
            Next
            radiGrup(g) = CSng(Math.Sqrt(area / Math.PI))
        Next

        Dim centreX(Math.Max(nGrups, 1) - 1) As Single, centreY(Math.Max(nGrups, 1) - 1) As Single
        Dim angleOr As Double = Math.PI * (3.0 - Math.Sqrt(5.0))   ' angle daurat
        For g As Integer = 0 To nGrups - 1
            If g = 0 Then
                Continue For
            End If
            Dim pare As Integer = 0, millorPes As Integer = 0
            For g2 As Integer = 0 To g - 1
                Dim k As Long = CLng(g2) * 100000L + g
                Dim p As Integer = If(pesGrups.ContainsKey(k), pesGrups(k), 0)
                If p > millorPes Then millorPes = p : pare = g2
            Next
            ' Primer angle lliure al voltant del grup pare, cada cop una mica més lluny
            Dim trobat As Boolean = False
            Dim dist As Single = radiGrup(pare) + radiGrup(g) + MARGE_X * 2
            For anell As Integer = 0 To 40
                For pas As Integer = 0 To 23
                    Dim ang As Double = g * angleOr + pas * (Math.PI * 2 / 24)
                    Dim cx As Single = centreX(pare) + dist * CSng(Math.Cos(ang))
                    Dim cy As Single = centreY(pare) + dist * CSng(Math.Sin(ang))
                    Dim lliure As Boolean = True
                    For g2 As Integer = 0 To g - 1
                        Dim dx As Single = cx - centreX(g2), dy As Single = cy - centreY(g2)
                        If Math.Sqrt(dx * dx + dy * dy) < radiGrup(g) + radiGrup(g2) + MARGE_X Then lliure = False : Exit For
                    Next
                    If lliure Then
                        centreX(g) = cx : centreY(g) = cy : trobat = True
                        Exit For
                    End If
                Next
                If trobat Then Exit For
                dist += Math.Max(radiGrup(g), 40.0F) * 0.5F
            Next
        Next

        ' Taules de cada grup: hub al centre i la resta en espiral, en ordre
        ' de recorregut des del hub (les veïnes queden a prop)
        For g As Integer = 0 To nGrups - 1
            Dim ordre As List(Of Integer) = OrdreBfs(hub(g), veins, membres(g))
            Dim cel As Single = 0
            For Each i As Integer In membres(g)
                cel += radi(i)
            Next
            cel = cel / membres(g).Count * 1.6F
            For j As Integer = 0 To ordre.Count - 1
                Dim i As Integer = ordre(j)
                Dim rr As Single = cel * CSng(Math.Sqrt(j))
                Dim ang As Double = j * angleOr
                x(i) = centreX(g) + rr * CSng(Math.Cos(ang))
                y(i) = centreY(g) + rr * CSng(Math.Sin(ang))
            Next
        Next

        ' ── 3. Model de forces ──────────────────────────────────
        If nGrups > 0 Then RefinarAmbForces(n, x, y, radi, grup, arestes, nGrups)

        ' ── 4. Sense encavalcaments ─────────────────────────────
        Dim connectades As New List(Of Integer)()
        For i As Integer = 0 To n - 1
            If grup(i) >= 0 Then connectades.Add(i)
        Next
        EliminarEncavalcaments(connectades, x, y, hw, hh)

        ' ── 5. Taules sense relacions: graella sota el conjunt ──
        If aillades.Count > 0 Then
            aillades.Sort(Function(a, b) String.Compare(taules(a).Nombre, taules(b).Nombre, StringComparison.OrdinalIgnoreCase))
            Dim minX As Single = 0, maxX As Single = 0, minY As Single = 0
            If connectades.Count > 0 Then
                minX = Single.MaxValue : maxX = Single.MinValue : minY = Single.MaxValue
                For Each i As Integer In connectades
                    minX = Math.Min(minX, x(i) - hw(i))
                    maxX = Math.Max(maxX, x(i) + hw(i))
                    minY = Math.Min(minY, y(i) - hh(i))
                Next
            End If
            ' Amplada de fila: la del conjunt, o una graella gairebé quadrada
            Dim ampMitja As Single = 0
            For Each i As Integer In aillades
                ampMitja += 2 * hw(i) + MARGE_X
            Next
            ampMitja /= aillades.Count
            Dim ampFila As Single = Math.Max(maxX - minX,
                                             ampMitja * CSng(Math.Ceiling(Math.Sqrt(aillades.Count))))
            Dim x0 As Single = If(connectades.Count > 0, minX, -ampFila / 2)
            Dim cursorX As Single = x0
            Dim cursorY As Single = If(connectades.Count > 0, minY - MARGE_Y * 3, 0)
            Dim altFila As Single = 0
            For Each i As Integer In aillades
                If cursorX > x0 AndAlso cursorX + 2 * hw(i) > x0 + ampFila Then
                    cursorX = x0
                    cursorY -= altFila + MARGE_Y
                    altFila = 0
                End If
                x(i) = cursorX + hw(i)
                y(i) = cursorY - hh(i)          ' Y creix cap amunt: la fila baixa
                cursorX += 2 * hw(i) + MARGE_X
                altFila = Math.Max(altFila, 2 * hh(i))
            Next
        End If

        ' ── Centrar a l'origen ──────────────────────────────────
        Dim bx0 As Single = Single.MaxValue, bx1 As Single = Single.MinValue
        Dim by0 As Single = Single.MaxValue, by1 As Single = Single.MinValue
        For i As Integer = 0 To n - 1
            bx0 = Math.Min(bx0, x(i) - hw(i)) : bx1 = Math.Max(bx1, x(i) + hw(i))
            by0 = Math.Min(by0, y(i) - hh(i)) : by1 = Math.Max(by1, y(i) + hh(i))
        Next
        Dim ox As Single = (bx0 + bx1) / 2, oy As Single = (by0 + by1) / 2

        ' ── Colors: grups propers o relacionats amb colors diferents ──
        Dim colorGrup() As Integer = AssignarColors(nGrups, membres, x, y, radi, pesGrups)

        For i As Integer = 0 To n - 1
            Dim id As Integer = taules(i).Id
            res.Posicions(id) = {x(i) - ox, y(i) - oy, 0.0F}
            res.Grup(id) = grup(i)
            res.Colors(id) = If(grup(i) < 0, GroupColor.ColorWhite, Paleta(colorGrup(grup(i))))
        Next
        For g As Integer = 0 To nGrups - 1
            res.Hubs.Add(taules(hub(g)).Id)
        Next
        Return res
    End Function

    ' ════════════════════════════════════════════════════════
    ' DETECCIÓ DE GRUPS (mètode de Louvain, determinista)
    ' Agrupa les taules maximitzant la modularitat: moltes relacions
    ' dins de cada grup i poques entre grups.
    ' ════════════════════════════════════════════════════════
    Private Function DetectarGrups(n As Integer, veins As HashSet(Of Integer)(),
                                   taules As IList(Of TablaBBDD)) As Integer()
        ' Ordre fix: més relacions primer, i després per nom
        Dim ordre As New List(Of Integer)()
        For i As Integer = 0 To n - 1
            If veins(i).Count > 0 Then ordre.Add(i)
        Next
        ordre.Sort(Function(a, b)
                       Dim c As Integer = veins(b).Count.CompareTo(veins(a).Count)
                       If c <> 0 Then Return c
                       Return String.Compare(taules(a).Nombre, taules(b).Nombre, StringComparison.OrdinalIgnoreCase)
                   End Function)

        Dim etiqueta(n - 1) As Integer
        For i As Integer = 0 To n - 1 : etiqueta(i) = i : Next
        If ordre.Count = 0 Then
            Dim buit(n - 1) As Integer
            For i As Integer = 0 To n - 1 : buit(i) = -1 : Next
            Return buit
        End If

        ' Graf del nivell actual: nodes 0..k-1 (al principi, les taules amb relacions)
        Dim nodeDe(n - 1) As Integer             ' taula → node del nivell actual
        For i As Integer = 0 To n - 1 : nodeDe(i) = -1 : Next
        For k As Integer = 0 To ordre.Count - 1 : nodeDe(ordre(k)) = k : Next
        Dim adj As New List(Of Dictionary(Of Integer, Double))()
        For k As Integer = 0 To ordre.Count - 1
            Dim d As New Dictionary(Of Integer, Double)()
            For Each v As Integer In veins(ordre(k))
                d(nodeDe(v)) = 1.0
            Next
            adj.Add(d)
        Next

        For nivell As Integer = 1 To 20
            Dim nn As Integer = adj.Count
            Dim grau(nn - 1) As Double, m2 As Double = 0
            For k As Integer = 0 To nn - 1
                For Each kv As KeyValuePair(Of Integer, Double) In adj(k)
                    grau(k) += kv.Value
                Next
                m2 += grau(k)
            Next
            If m2 <= 0 Then Exit For

            Dim com(nn - 1) As Integer, tot(nn - 1) As Double
            For k As Integer = 0 To nn - 1 : com(k) = k : tot(k) = grau(k) : Next

            Dim algunMoviment As Boolean = False
            For passada As Integer = 1 To 50
                Dim mogut As Boolean = False
                For k As Integer = 0 To nn - 1
                    Dim actual As Integer = com(k)
                    tot(actual) -= grau(k)
                    ' Pes cap a cada comunitat veïna
                    Dim pesA As New Dictionary(Of Integer, Double)()
                    For Each kv As KeyValuePair(Of Integer, Double) In adj(k)
                        If kv.Key = k Then Continue For
                        Dim c As Integer = com(kv.Key)
                        pesA(c) = If(pesA.ContainsKey(c), pesA(c), 0) + kv.Value
                    Next
                    Dim millor As Integer = actual
                    Dim millorGuany As Double = If(pesA.ContainsKey(actual), pesA(actual), 0) - RESOLUCIO * tot(actual) * grau(k) / m2
                    For Each kv As KeyValuePair(Of Integer, Double) In pesA
                        Dim guany As Double = kv.Value - RESOLUCIO * tot(kv.Key) * grau(k) / m2
                        If guany > millorGuany + 0.000000001 OrElse
                           (Math.Abs(guany - millorGuany) <= 0.000000001 AndAlso kv.Key < millor AndAlso guany > 0) Then
                            millor = kv.Key : millorGuany = guany
                        End If
                    Next
                    com(k) = millor
                    tot(millor) += grau(k)
                    If millor <> actual Then mogut = True : algunMoviment = True
                Next
                If Not mogut Then Exit For
            Next
            If Not algunMoviment Then Exit For

            ' Agregar: cada comunitat passa a ser un node del nivell següent
            Dim nou As New Dictionary(Of Integer, Integer)()
            For k As Integer = 0 To nn - 1
                If Not nou.ContainsKey(com(k)) Then nou(com(k)) = nou.Count
            Next
            Dim adj2 As New List(Of Dictionary(Of Integer, Double))()
            For c As Integer = 0 To nou.Count - 1 : adj2.Add(New Dictionary(Of Integer, Double)()) : Next
            For k As Integer = 0 To nn - 1
                Dim ck As Integer = nou(com(k))
                For Each kv As KeyValuePair(Of Integer, Double) In adj(k)
                    Dim cj As Integer = nou(com(kv.Key))
                    adj2(ck)(cj) = If(adj2(ck).ContainsKey(cj), adj2(ck)(cj), 0) + kv.Value
                Next
            Next
            For i As Integer = 0 To n - 1
                If nodeDe(i) >= 0 Then nodeDe(i) = nou(com(nodeDe(i)))
            Next
            adj = adj2
            If nou.Count = nn Then Exit For
        Next

        For i As Integer = 0 To n - 1
            etiqueta(i) = If(nodeDe(i) >= 0, nodeDe(i), -1)
        Next

        ' Una etiqueta pot quedar repartida en trossos no connectats: cada tros és un grup
        Dim grup(n - 1) As Integer
        For i As Integer = 0 To n - 1 : grup(i) = -2 : Next
        Dim trossos As New List(Of List(Of Integer))()
        For Each inici As Integer In ordre
            If grup(inici) <> -2 Then Continue For
            Dim tros As New List(Of Integer)()
            Dim cua As New Queue(Of Integer)()
            cua.Enqueue(inici) : grup(inici) = -3
            Do While cua.Count > 0
                Dim u As Integer = cua.Dequeue()
                tros.Add(u)
                For Each v As Integer In veins(u)
                    If grup(v) = -2 AndAlso etiqueta(v) = etiqueta(inici) Then grup(v) = -3 : cua.Enqueue(v)
                Next
            Loop
            trossos.Add(tros)
        Next

        ' Grups d'una sola taula: s'uneixen al grup del veí amb més relacions
        ' (una taula penjada no ha de quedar sola i lluny)
        Dim grupDe(n - 1) As Integer
        For t As Integer = 0 To trossos.Count - 1
            For Each i As Integer In trossos(t)
                grupDe(i) = t
            Next
        Next
        For t As Integer = 0 To trossos.Count - 1
            If trossos(t).Count <> 1 Then Continue For
            Dim i As Integer = trossos(t)(0)
            Dim desti As Integer = -1
            For Each v As Integer In veins(i)
                If desti < 0 OrElse trossos(grupDe(v)).Count > trossos(grupDe(desti)).Count Then desti = v
            Next
            If desti >= 0 AndAlso grupDe(desti) <> t Then
                Dim gd As Integer = grupDe(desti)
                trossos(gd).Add(i)
                trossos(t).Clear()
                grupDe(i) = gd
            End If
        Next

        ' Numerar per mida (el grup més gran és el 0)
        trossos.RemoveAll(Function(l) l.Count = 0)
        trossos.Sort(Function(a, b)
                         Dim c As Integer = b.Count.CompareTo(a.Count)
                         Return If(c <> 0, c, a.Min().CompareTo(b.Min()))
                     End Function)
        For i As Integer = 0 To n - 1 : grup(i) = -1 : Next
        For t As Integer = 0 To trossos.Count - 1
            For Each i As Integer In trossos(t)
                grup(i) = t
            Next
        Next
        Return grup
    End Function

    ''' <summary>
    ''' Colors per grup un cop col·locats: dos grups no poden compartir color si
    ''' estan relacionats o si a la pantalla queden a prop l'un de l'altre.
    ''' </summary>
    Private Function AssignarColors(nGrups As Integer, membres As List(Of Integer)(),
                                    x() As Single, y() As Single, radi() As Single,
                                    pesGrups As Dictionary(Of Long, Integer)) As Integer()
        Dim colorGrup(Math.Max(nGrups, 1) - 1) As Integer
        Dim cx(Math.Max(nGrups, 1) - 1) As Single, cy(Math.Max(nGrups, 1) - 1) As Single
        Dim r(Math.Max(nGrups, 1) - 1) As Single
        For g As Integer = 0 To nGrups - 1
            For Each i As Integer In membres(g)
                cx(g) += x(i) : cy(g) += y(i)
            Next
            cx(g) /= membres(g).Count : cy(g) /= membres(g).Count
            For Each i As Integer In membres(g)
                r(g) = CSng(Math.Max(r(g), Math.Sqrt((x(i) - cx(g)) ^ 2 + (y(i) - cy(g)) ^ 2) + radi(i)))
            Next
        Next
        For g As Integer = 0 To nGrups - 1
            Dim usats As New HashSet(Of Integer)()
            For g2 As Integer = 0 To g - 1
                Dim k As Long = CLng(g2) * 100000L + g
                Dim d As Double = Math.Sqrt((cx(g) - cx(g2)) ^ 2 + (cy(g) - cy(g2)) ^ 2)
                If pesGrups.ContainsKey(k) OrElse d < r(g) + r(g2) + MARGE_X * 6 Then usats.Add(colorGrup(g2))
            Next
            colorGrup(g) = g Mod Paleta.Length
            For intent As Integer = 0 To Paleta.Length - 1
                Dim cand As Integer = (g + intent) Mod Paleta.Length
                If Not usats.Contains(cand) Then colorGrup(g) = cand : Exit For
            Next
        Next
        Return colorGrup
    End Function

    Private Function OrdreBfs(inici As Integer, veins As HashSet(Of Integer)(), membres As List(Of Integer)) As List(Of Integer)
        Dim dins As New HashSet(Of Integer)(membres)
        Dim vist As New HashSet(Of Integer) From {inici}
        Dim res As New List(Of Integer)()
        Dim cua As New Queue(Of Integer)()
        cua.Enqueue(inici)
        Do While cua.Count > 0
            Dim u As Integer = cua.Dequeue()
            res.Add(u)
            Dim vs As New List(Of Integer)(veins(u))
            vs.Sort()
            For Each v As Integer In vs
                If dins.Contains(v) AndAlso vist.Add(v) Then cua.Enqueue(v)
            Next
        Loop
        For Each i As Integer In membres
            If Not vist.Contains(i) Then res.Add(i)
        Next
        Return res
    End Function

    ' ════════════════════════════════════════════════════════
    ' MODEL DE FORCES
    ' ════════════════════════════════════════════════════════
    Private Sub RefinarAmbForces(n As Integer, x() As Single, y() As Single, radi() As Single,
                                 grup() As Integer, arestes As List(Of Integer()), nGrups As Integer)
        Dim actius As New List(Of Integer)()
        For i As Integer = 0 To n - 1
            If grup(i) >= 0 Then actius.Add(i)
        Next
        If actius.Count < 2 Then Return

        Dim radiMitja As Single = 0
        For Each i As Integer In actius
            radiMitja += radi(i)
        Next
        radiMitja /= actius.Count

        Const ITERACIONS As Integer = 300
        Dim tempInicial As Single = radiMitja * 1.5F
        Dim fx(n - 1) As Single, fy(n - 1) As Single
        Dim gx(nGrups - 1) As Single, gy(nGrups - 1) As Single, gn(nGrups - 1) As Integer

        For iter As Integer = 0 To ITERACIONS - 1
            Dim temp As Single = tempInicial * (1.0F - CSng(iter) / ITERACIONS) + 1.0F
            Array.Clear(fx, 0, n) : Array.Clear(fy, 0, n)

            ' Centroides (de cada grup i global)
            Array.Clear(gx, 0, nGrups) : Array.Clear(gy, 0, nGrups) : Array.Clear(gn, 0, nGrups)
            Dim cx As Single = 0, cy As Single = 0
            For Each i As Integer In actius
                gx(grup(i)) += x(i) : gy(grup(i)) += y(i) : gn(grup(i)) += 1
                cx += x(i) : cy += y(i)
            Next
            cx /= actius.Count : cy /= actius.Count

            ' Repulsió entre taules (més forta entre grups diferents)
            For a As Integer = 0 To actius.Count - 1
                Dim i As Integer = actius(a)
                For b As Integer = a + 1 To actius.Count - 1
                    Dim j As Integer = actius(b)
                    Dim dx As Single = x(i) - x(j), dy As Single = y(i) - y(j)
                    Dim d2 As Single = dx * dx + dy * dy
                    If d2 < 0.01F Then
                        dx = 0.1F * (1 + (i Mod 7)) : dy = 0.1F * (1 + (j Mod 5)) : d2 = dx * dx + dy * dy
                    End If
                    Dim s As Single = radi(i) + radi(j)
                    If grup(i) <> grup(j) Then s *= 1.25F
                    If d2 > 6.25F * s * s Then Continue For
                    Dim d As Single = CSng(Math.Sqrt(d2))
                    Dim f As Single = 0.22F * s * s / d2          ' ∝ 1/d
                    fx(i) += dx * f : fy(i) += dy * f
                    fx(j) -= dx * f : fy(j) -= dy * f
                Next
            Next

            ' Molles a les relacions: longitud ideal segons la mida de les caixes
            For Each e As Integer() In arestes
                Dim i As Integer = e(0), j As Integer = e(1)
                Dim dx As Single = x(j) - x(i), dy As Single = y(j) - y(i)
                Dim d As Single = CSng(Math.Max(Math.Sqrt(dx * dx + dy * dy), 0.1))
                Dim mateix As Boolean = grup(i) = grup(j)
                Dim ideal As Single = (radi(i) + radi(j)) * If(mateix, 0.9F, 1.4F)
                Dim k As Single = If(mateix, 0.12F, 0.05F)
                Dim f As Single = k * (d - ideal) / d
                fx(i) += dx * f : fy(i) += dy * f
                fx(j) -= dx * f : fy(j) -= dy * f
            Next

            ' Cohesió de grup i gravetat cap al centre: cap grup s'allunya
            For Each i As Integer In actius
                Dim g As Integer = grup(i)
                fx(i) += 0.05F * (gx(g) / gn(g) - x(i)) + 0.012F * (cx - x(i))
                fy(i) += 0.05F * (gy(g) / gn(g) - y(i)) + 0.012F * (cy - y(i))
            Next

            ' Desplaçament limitat per la temperatura (es va refredant)
            For Each i As Integer In actius
                Dim l As Single = CSng(Math.Sqrt(fx(i) * fx(i) + fy(i) * fy(i)))
                If l < 0.0001F Then Continue For
                Dim m As Single = Math.Min(l, temp) / l
                x(i) += fx(i) * m : y(i) += fy(i) * m
            Next
        Next
    End Sub

    ''' <summary>
    ''' Separa les caixes que s'encavalquen (més el marge), movent cada parella
    ''' pel camí més curt. Es repeteix fins que no n'hi ha cap.
    ''' </summary>
    Friend Sub EliminarEncavalcaments(ids As List(Of Integer), x() As Single, y() As Single,
                                      hw() As Single, hh() As Single)
        For passada As Integer = 0 To 400
            Dim algun As Boolean = False
            For a As Integer = 0 To ids.Count - 1
                Dim i As Integer = ids(a)
                For b As Integer = a + 1 To ids.Count - 1
                    Dim j As Integer = ids(b)
                    Dim dx As Single = x(j) - x(i), dy As Single = y(j) - y(i)
                    Dim px As Single = hw(i) + hw(j) + MARGE_X - Math.Abs(dx)
                    Dim py As Single = hh(i) + hh(j) + MARGE_Y - Math.Abs(dy)
                    If px <= 0 OrElse py <= 0 Then Continue For
                    algun = True
                    ' Empènyer per l'eix de menys penetració (en proporció a la forma)
                    If px / (hw(i) + hw(j)) < py / (hh(i) + hh(j)) Then
                        Dim s As Single = If(dx > 0 OrElse (dx = 0 AndAlso j > i), 1.0F, -1.0F)
                        x(i) -= s * (px / 2 + 0.5F) : x(j) += s * (px / 2 + 0.5F)
                    Else
                        Dim s As Single = If(dy > 0 OrElse (dy = 0 AndAlso j > i), 1.0F, -1.0F)
                        y(i) -= s * (py / 2 + 0.5F) : y(j) += s * (py / 2 + 0.5F)
                    End If
                Next
            Next
            If Not algun Then Exit For
        Next
    End Sub

End Module
