Imports System.Drawing
Imports Xunit

Public Class OrganitzadorGrupsTests

    Private Shared Function Mida(t As TablaBBDD) As SizeF
        Dim maxLen As Integer = t.Nombre.Length
        For Each f As CampoBBDD In t.Fields
            maxLen = Math.Max(maxLen, f.Nombre.Length + 12)
        Next
        Return New SizeF(Math.Max(55.0F, maxLen * 5.1F / 2 + 12), (16.8F + t.Fields.Count * 13.2F + 4.8F) / 2)
    End Function

    ' 12 mòduls de mida variable units entre ells, una estrella i taules soles
    Private Shared Function EsquemaGran() As ProyectoBBDD
        Dim rnd As New Random(7)
        Dim p As New ProyectoBBDD()
        Dim hubs As New List(Of Integer)()
        For m As Integer = 0 To 11
            Dim ids As New List(Of Integer)()
            For i As Integer = 0 To 3 + (m * 7) Mod 12 - 1
                Dim t As New TablaBBDD With {.Id = p.GetNextTableId(), .Nombre = "M" & m & "_TAULA_" & i}
                For f As Integer = 0 To 2 + rnd.Next(9) : t.Fields.Add(New CampoBBDD With {.Nombre = "CAMP_" & f}) : Next
                p.Taules.Add(t) : ids.Add(t.Id)
                If i > 0 Then p.Relacions.Add(New RelacionBBDD With {.Id = p.GetNextRelId(), .TablaOrigenId = t.Id,
                                                                      .TablaDestinoId = ids(If(rnd.Next(3) = 0, rnd.Next(i), 0))})
            Next
            hubs.Add(ids(0))
            If m > 0 Then p.Relacions.Add(New RelacionBBDD With {.Id = p.GetNextRelId(), .TablaOrigenId = ids(1 Mod ids.Count),
                                                                  .TablaDestinoId = hubs(rnd.Next(m))})
        Next
        For i As Integer = 0 To 7
            Dim t As New TablaBBDD With {.Id = p.GetNextTableId(), .Nombre = "SOLA_" & i}
            t.Fields.Add(New CampoBBDD With {.Nombre = "ID"})
            p.Taules.Add(t)
        Next
        Return p
    End Function

    Private Shared Function Calcular(p As ProyectoBBDD) As OrganitzadorGrups.Resultat
        Return OrganitzadorGrups.Calcular(p.Taules, p.Relacions, AddressOf Mida)
    End Function

    <Fact>
    Public Sub CapTaulaEsEncavalca()
        Dim p As ProyectoBBDD = EsquemaGran()
        Dim res As OrganitzadorGrups.Resultat = Calcular(p)
        For a As Integer = 0 To p.Taules.Count - 1
            For b As Integer = a + 1 To p.Taules.Count - 1
                Dim ta As TablaBBDD = p.Taules(a), tb As TablaBBDD = p.Taules(b)
                Dim pa As Single() = res.Posicions(ta.Id), pb As Single() = res.Posicions(tb.Id)
                Dim solapX As Boolean = Math.Abs(pa(0) - pb(0)) < Mida(ta).Width + Mida(tb).Width
                Dim solapY As Boolean = Math.Abs(pa(1) - pb(1)) < Mida(ta).Height + Mida(tb).Height
                Assert.False(solapX AndAlso solapY, ta.Nombre & " s'encavalca amb " & tb.Nombre)
            Next
        Next
    End Sub

    <Fact>
    Public Sub EsDeterminista()
        Dim r1 As OrganitzadorGrups.Resultat = Calcular(EsquemaGran())
        Dim r2 As OrganitzadorGrups.Resultat = Calcular(EsquemaGran())
        For Each kv As KeyValuePair(Of Integer, Single()) In r1.Posicions
            For k As Integer = 0 To 2
                Assert.Equal(kv.Value(k), r2.Posicions(kv.Key)(k))
            Next
            Assert.Equal(r1.Colors(kv.Key), r2.Colors(kv.Key))
        Next
    End Sub

    <Fact>
    Public Sub TaulesSoles_SotaElConjuntIBlanques()
        Dim p As ProyectoBBDD = EsquemaGran()
        Dim res As OrganitzadorGrups.Resultat = Calcular(p)
        Dim minConnectades As Single = Single.MaxValue, maxSoles As Single = Single.MinValue
        For Each t As TablaBBDD In p.Taules
            Dim y As Single = res.Posicions(t.Id)(1)
            If t.Nombre.StartsWith("SOLA_") Then
                Assert.Equal(-1, res.Grup(t.Id))
                Assert.Equal(GroupColor.ColorWhite, res.Colors(t.Id))
                maxSoles = Math.Max(maxSoles, y + Mida(t).Height)
            Else
                Assert.True(res.Grup(t.Id) >= 0)
                minConnectades = Math.Min(minConnectades, y - Mida(t).Height)
            End If
        Next
        Assert.True(maxSoles < minConnectades)   ' Y creix cap amunt
    End Sub

    <Fact>
    Public Sub GrupsRelacionats_ColorsDiferents_IConjuntCompacte()
        Dim p As ProyectoBBDD = EsquemaGran()
        Dim res As OrganitzadorGrups.Resultat = Calcular(p)
        Assert.True(res.NombreGrups >= 6, "grups: " & res.NombreGrups)
        For Each r As RelacionBBDD In p.Relacions
            If res.Grup(r.TablaOrigenId) <> res.Grup(r.TablaDestinoId) Then
                Assert.NotEqual(res.Colors(r.TablaOrigenId), res.Colors(r.TablaDestinoId))
            End If
        Next
        ' Compacitat: la superfície ocupada és com a molt 8 vegades la de les caixes
        Dim areaCaixes As Double = 0
        Dim x0 As Single = Single.MaxValue, x1 As Single = Single.MinValue, y0 As Single = Single.MaxValue, y1 As Single = Single.MinValue
        For Each t As TablaBBDD In p.Taules
            Dim m As SizeF = Mida(t), pos As Single() = res.Posicions(t.Id)
            areaCaixes += 4.0 * m.Width * m.Height
            x0 = Math.Min(x0, pos(0) - m.Width) : x1 = Math.Max(x1, pos(0) + m.Width)
            y0 = Math.Min(y0, pos(1) - m.Height) : y1 = Math.Max(y1, pos(1) + m.Height)
        Next
        Assert.True((x1 - x0) * (y1 - y0) < 8 * areaCaixes, "massa espai buit")
        ' Centrat a l'origen
        Assert.True(Math.Abs((x0 + x1) / 2) < 1 AndAlso Math.Abs((y0 + y1) / 2) < 1)
    End Sub

    <Fact>
    Public Sub DosModulsSenseRelacio_SonGrupsDiferentsIPropers()
        Dim p As New ProyectoBBDD()
        For m As Integer = 0 To 1
            Dim centre As New TablaBBDD With {.Id = p.GetNextTableId(), .Nombre = "C" & m}
            p.Taules.Add(centre)
            For i As Integer = 0 To 3
                Dim t As New TablaBBDD With {.Id = p.GetNextTableId(), .Nombre = "C" & m & "_" & i}
                p.Taules.Add(t)
                p.Relacions.Add(New RelacionBBDD With {.Id = p.GetNextRelId(), .TablaOrigenId = t.Id, .TablaDestinoId = centre.Id})
            Next
        Next
        Dim res As OrganitzadorGrups.Resultat = Calcular(p)
        Assert.Equal(2, res.NombreGrups)
        Assert.NotEqual(res.Grup(1), res.Grup(6))
        ' Els dos grups no queden allunyats: distància entre centres < 6 amplades de taula
        Dim d As Double = Math.Sqrt((res.Posicions(1)(0) - res.Posicions(6)(0)) ^ 2 + (res.Posicions(1)(1) - res.Posicions(6)(1)) ^ 2)
        Assert.True(d < 6 * 2 * 55, "distància: " & d)
    End Sub

    <Fact>
    Public Sub SenseTaules_NoFalla()
        Dim res As OrganitzadorGrups.Resultat = OrganitzadorGrups.Calcular(New List(Of TablaBBDD)(), New List(Of RelacionBBDD)(), AddressOf Mida)
        Assert.Empty(res.Posicions)
        Dim nomesSoles As New ProyectoBBDD()
        nomesSoles.Taules.Add(New TablaBBDD With {.Id = 1, .Nombre = "A"})
        nomesSoles.Taules.Add(New TablaBBDD With {.Id = 2, .Nombre = "B"})
        Assert.Equal(2, Calcular(nomesSoles).Posicions.Count)
    End Sub

    <Fact>
    Public Sub CmdOrganitzar_DesferIRefer()
        Dim t As New TablaBBDD With {.Id = 1, .Nombre = "T", .PosX = 1, .PosY = 2, .PosZ = 3,
                                     .GrupColor = GroupColor.ColorOrange, .Depth = 1.0F}
        Dim cmd As New CmdOrganitzar()
        cmd.Afegir(t, 10, 20, 0, GroupColor.ColorBlue, 0.75F)
        cmd.Execute()
        Assert.True(t.PosX = 10 AndAlso t.PosY = 20 AndAlso t.PosZ = 0)
        Assert.Equal(GroupColor.ColorBlue, t.GrupColor)
        Assert.Equal(0.75F, t.Depth)
        cmd.Undo()
        Assert.True(t.PosX = 1 AndAlso t.PosY = 2 AndAlso t.PosZ = 3)
        Assert.Equal(GroupColor.ColorOrange, t.GrupColor)
        Assert.Equal(1.0F, t.Depth)
    End Sub

End Class
