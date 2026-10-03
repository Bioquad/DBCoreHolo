Imports System.IO
Imports Xunit

Public Class IntegrityEngineTests

    <Fact>
    Public Sub ProjecteExemple_EsValid()
        Assert.Empty(IntegrityEngine.ValidarModelComplet(ProjecteExemple()))
    End Sub

    <Fact>
    Public Sub LongitudDiferent_EsError()
        Dim p As ProyectoBBDD = ProjecteExemple()
        Dim client As TablaBBDD = Taula(p, "CLIENT")
        Dim t As New TablaBBDD() With {.Id = p.GetNextTableId(), .Nombre = "X"}
        t.Fields.Add(Camp("ID", DataType.DbInt, pk:=True))
        t.Fields.Add(Camp("EMAIL_CLIENT", DataType.VarChar, 50))
        client.Fields.Find(Function(f) f.Nombre = "EMAIL").EsPK = False
        p.Taules.Add(t)
        ' Referència a un camp VARCHAR(200) des d'un VARCHAR(50)
        Dim pk As CampoBBDD = Camp("CODI", DataType.VarChar, 200, pk:=True)
        Dim d As New TablaBBDD() With {.Id = p.GetNextTableId(), .Nombre = "D"}
        d.Fields.Add(pk)
        p.Taules.Add(d)
        Dim errs As List(Of String) = IntegrityEngine.ValidarRelacio(
            p.Taules, p.Relacions, t.Id, d.Id, "email_client", "codi", CardinalityType.ManyToOne)
        Assert.Single(errs)
        Assert.Contains("VARCHAR(50)", errs(0))
    End Sub

    <Fact>
    Public Sub ReferenciarPartDePKComposta_EsError()
        Dim p As ProyectoBBDD = ProjecteExemple()
        Dim t As New TablaBBDD() With {.Id = p.GetNextTableId(), .Nombre = "Y"}
        t.Fields.Add(Camp("ID", DataType.DbInt, pk:=True))
        t.Fields.Add(Camp("ID_CLIENT", DataType.DbInt))
        p.Taules.Add(t)
        Dim errs As List(Of String) = IntegrityEngine.ValidarRelacio(
            p.Taules, p.Relacions, t.Id, Taula(p, "COMANDA_LINIA").Id, "ID_CLIENT", "ID_CLIENT", CardinalityType.ManyToOne)
        Assert.Single(errs)
    End Sub

    <Fact>
    Public Sub SetNullSobreNotNull_EsError()
        Dim p As ProyectoBBDD = ProjecteExemple()
        p.Relacions(0).OnDelete = OnDeleteUpdateAction.SetNull
        Dim errs As List(Of String) = IntegrityEngine.ValidarModelComplet(p)
        Assert.Single(errs)
        Assert.StartsWith("[COMANDA_LINIA.ID_CLIENT]", errs(0))
    End Sub

End Class

Public Class ModelTests

    <Fact>
    Public Sub UnCampPotSerPKiFK()
        Dim f As New CampoBBDD()
        f.EsPK = True
        f.EsFK = True
        Assert.True(f.EsPK)
        Assert.True(f.EsFK)
        Assert.True(f.NotNull)
    End Sub

    <Fact>
    Public Sub CommandStack_RespectaElLimit()
        CommandStack.Clear()
        CommandStack.Limit = 3
        Dim llista As New List(Of TablaBBDD)()
        For i As Integer = 1 To 5
            CommandStack.Push(New CmdAfegirTaula(llista, New TablaBBDD() With {.Id = i}))
        Next
        Dim desfets As Integer = 0
        Do While CommandStack.CanUndo
            CommandStack.UndoLast()
            desfets += 1
        Loop
        Assert.Equal(3, desfets)
        Assert.Equal(2, llista.Count)
        CommandStack.Clear()
        CommandStack.Limit = 50
    End Sub

End Class

Public Class ProjectSerializerTests

    <Fact>
    Public Sub DesarICarregar_ConservaElModel()
        Dim ruta As String = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") & ".hdb")
        Try
            ProjectSerializer.Desar(ProjecteExemple(), ruta)
            ProjectSerializer.Desar(ProjecteExemple(), ruta)   ' sobreescriptura atòmica
            Assert.False(File.Exists(ruta & ".tmp"))
            Dim p As ProyectoBBDD = ProjectSerializer.Carregar(ruta)
            Assert.Equal(3, p.Taules.Count)
            Assert.Equal(2, Taula(p, "COMANDA_LINIA").PKFields.Count)
            Assert.True(CampDe(Taula(p, "COMANDA_LINIA"), "ID_PRODUCTE").EsFK)
            Assert.Equal("sales", Taula(p, "CLIENT").Schema)
        Finally
            If File.Exists(ruta) Then File.Delete(ruta)
        End Try
    End Sub

    <Fact>
    Public Sub Carregar_FitxerSenseLlistes_NoFalla()
        Dim ruta As String = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") & ".hdb")
        Try
            File.WriteAllText(ruta, "{ ""Nombre"": ""Buit"", ""Taules"": null, ""Relacions"": null }")
            Dim p As ProyectoBBDD = ProjectSerializer.Carregar(ruta)
            Assert.Empty(p.Taules)
            Assert.Empty(p.Relacions)
        Finally
            If File.Exists(ruta) Then File.Delete(ruta)
        End Try
    End Sub

End Class
