Imports System.Data
Imports System.IO
Imports Microsoft.SqlServer.TransactSql.ScriptDom
Imports Xunit

' Tests de la còpia de bases de dades (generador de DDL a partir del
' catàleg) i del visor de dades. La sintaxi del T-SQL es valida amb
' el parser oficial de SQL Server (ScriptDom).
Public Class GeneradorCatalegTests

    Private Shared Sub AssertSintaxiValida(sql As String)
        Dim parser As New TSql160Parser(True)
        Dim errors As IList(Of ParseError) = Nothing
        Using rd As New StringReader(sql)
            parser.Parse(rd, errors)
        End Using
        Assert.True(errors.Count = 0,
                    String.Join(Environment.NewLine, errors.Select(Function(e) $"L{e.Line}:{e.Column} {e.Message}")) &
                    Environment.NewLine & sql)
    End Sub

    Private Shared Function Col(nom As String, tipus As String, Optional maxLen As Integer = 4,
                                Optional prec As Integer = 10, Optional esc As Integer = 0) As CatColumna
        Return New CatColumna With {.Nom = nom, .Tipus = tipus, .MaxLength = maxLen, .Precisio = prec, .Escala = esc}
    End Function

    Private Shared Function CatalegExemple() As CatalegBD
        Dim cat As New CatalegBD With {.Collation = "Latin1_General_CI_AS"}
        cat.Esquemes.Add("vendes")
        cat.Tipus.Add(New CatTipus With {.Esquema = "dbo", .Nom = "Telefon", .TipusBase = "varchar(20)", .Nullable = False})
        cat.Sequencies.Add(New CatSequencia With {.Esquema = "dbo", .Nom = "SeqFactura", .Tipus = "bigint", .Inici = "1001",
                                                  .Increment = "1", .Minim = "1", .Maxim = "9223372036854775807", .Cache = ""})
        cat.Sequencies.Add(New CatSequencia With {.Esquema = "vendes", .Nom = "SeqDec", .Tipus = "decimal(10,0)", .Inici = "5",
                                                  .Increment = "5", .Minim = "0", .Maxim = "1000", .Cicle = True, .Cache = Nothing})

        Dim client As New CatTaula With {.Esquema = "vendes", .Nom = "Client ]rar"}
        Dim id As CatColumna = Col("Id", "int")
        id.Nullable = False : id.EsIdentity = True : id.IdentitySeed = "100" : id.IdentityIncrement = "5" : id.IdentityUltimValor = "235"
        client.Columnes.Add(id)
        Dim nom As CatColumna = Col("Nom", "nvarchar", 200)
        nom.Nullable = False : nom.Collation = "Latin1_General_CI_AS"
        client.Columnes.Add(nom)
        client.Columnes.Add(Col("Notes", "varchar", -1))
        Dim tel As CatColumna = Col("Telefon", "Telefon")
        tel.EsquemaTipus = "dbo"
        client.Columnes.Add(tel)
        client.Columnes.Add(Col("Saldo", "decimal", 9, 18, 2))
        client.Columnes.Add(Col("Alta", "datetime2", 8, 27, 7))
        client.Columnes.Add(Col("Hora", "time", 5, 16, 3))
        client.Columnes.Add(Col("Ratio", "float", 8, 24))
        Dim guid As CatColumna = Col("Guid", "uniqueidentifier", 16)
        guid.Nullable = False : guid.EsRowGuid = True : guid.DefaultNom = "DF_Client_Guid" : guid.DefaultDefinicio = "(newid())"
        client.Columnes.Add(guid)
        Dim sp As CatColumna = Col("Extra", "int")
        sp.EsSparse = True
        client.Columnes.Add(sp)
        Dim calc As CatColumna = Col("SaldoIva", "decimal")
        calc.EsCalculada = True : calc.Formula = "([Saldo]*(1.21))" : calc.Persistida = True
        client.Columnes.Add(calc)
        client.Columnes.Add(Col("Versio", "timestamp", 8))
        Dim geo As CatColumna = Col("Lloc", "geography", -1)
        geo.EsClr = True
        client.Columnes.Add(geo)
        Dim pk As New CatIndex With {.Nom = "PK_Client", .EsPK = True, .EsUnic = True, .EsClustered = True}
        pk.Columnes.Add(New CatColumnaIndex With {.Nom = "Id"})
        client.Indexs.Add(pk)
        Dim uq As New CatIndex With {.Nom = "UQ_Client_Nom", .EsRestriccioUnica = True, .EsUnic = True}
        uq.Columnes.Add(New CatColumnaIndex With {.Nom = "Nom", .Descendent = True})
        client.Indexs.Add(uq)
        Dim ix As New CatIndex With {.Nom = "IX_Client_Alta", .Filtre = "([Alta] IS NOT NULL)"}
        ix.Columnes.Add(New CatColumnaIndex With {.Nom = "Alta", .Descendent = True})
        ix.Incloses.Add("Nom")
        client.Indexs.Add(ix)
        client.Checks.Add(New CatCheck With {.Nom = "CK_Client_Saldo", .Definicio = "([Saldo]>=(0))", .NoConfiable = True, .Desactivat = True})
        cat.Taules.Add(client)

        Dim linia As New CatTaula With {.Esquema = "dbo", .Nom = "Linia"}
        linia.Columnes.Add(Col("ClientId", "int"))
        linia.Columnes.Add(Col("Num", "smallint", 2))
        Dim pk2 As New CatIndex With {.Nom = "PK_Linia", .EsPK = True, .EsUnic = True}
        pk2.Columnes.Add(New CatColumnaIndex With {.Nom = "ClientId"})
        pk2.Columnes.Add(New CatColumnaIndex With {.Nom = "Num", .Descendent = True})
        linia.Indexs.Add(pk2)
        Dim fk As New CatFK With {.Nom = "FK_Linia_Client", .EsquemaRef = "vendes", .TaulaRef = "Client ]rar",
                                  .OnDelete = "CASCADE", .OnUpdate = "NO ACTION", .NotForReplication = True,
                                  .NoConfiable = True, .Desactivada = True}
        fk.Columnes.Add("ClientId")
        fk.ColumnesRef.Add("Id")
        linia.FKs.Add(fk)
        cat.Taules.Add(linia)

        cat.Moduls.Add(New CatModul With {.Esquema = "dbo", .Nom = "trgLinia", .Tipus = "TR", .TaulaPare = "[dbo].[Linia]",
                                          .Desactivat = True, .QuotedIdentifier = False, .AnsiNulls = False,
                                          .Definicio = "CREATE TRIGGER trgLinia ON dbo.Linia AFTER INSERT AS SET NOCOUNT ON;"})
        cat.Moduls.Add(New CatModul With {.Nom = "trgBD", .Tipus = "TR", .EsTriggerBD = True, .Desactivat = True,
                                          .Definicio = "CREATE TRIGGER trgBD ON DATABASE FOR CREATE_TABLE AS PRINT 1;"})
        cat.Sinonims.Add(New CatSinonim With {.Esquema = "dbo", .Nom = "Clients", .Base = "[vendes].[Client ]]rar]"})
        Return cat
    End Function

    <Fact>
    Public Sub TotesLesSentencies_SonTSqlValides()
        Dim cat As CatalegBD = CatalegExemple()
        For Each e As String In cat.Esquemes
            AssertSintaxiValida(GeneradorCataleg.CrearEsquema(e))
        Next
        For Each t As CatTipus In cat.Tipus
            AssertSintaxiValida(GeneradorCataleg.CrearTipus(t))
        Next
        For Each s As CatSequencia In cat.Sequencies
            AssertSintaxiValida(GeneradorCataleg.CrearSequencia(s))
        Next
        For Each t As CatTaula In cat.Taules
            AssertSintaxiValida(GeneradorCataleg.CrearTaula(t))
            For Each sql As String In GeneradorCataleg.CrearIndexs(t)
                AssertSintaxiValida(sql)
            Next
            For Each ck As CatCheck In t.Checks
                AssertSintaxiValida(GeneradorCataleg.CrearCheck(t, ck))
            Next
            For Each fk As CatFK In t.FKs
                AssertSintaxiValida(GeneradorCataleg.CrearFK(t, fk))
            Next
            AssertSintaxiValida(GeneradorCataleg.SelectCopia(t))
            For Each c As CatColumna In t.Columnes
                If c.EsIdentity Then
                    AssertSintaxiValida(GeneradorCataleg.ReajustarIdentity(t, c, True))
                    AssertSintaxiValida(GeneradorCataleg.ReajustarIdentity(t, c, False))
                End If
            Next
        Next
        For Each m As CatModul In cat.Moduls
            AssertSintaxiValida(GeneradorCataleg.OpcionsModul(m))
            AssertSintaxiValida(GeneradorCataleg.DesactivarTrigger(m))
        Next
        For Each s As CatSinonim In cat.Sinonims
            AssertSintaxiValida(GeneradorCataleg.CrearSinonim(s))
        Next
    End Sub

    <Fact>
    Public Sub CrearTaula_ConservaTipusIRestriccions()
        Dim sql As String = GeneradorCataleg.CrearTaula(CatalegExemple().Taules(0))
        Assert.Contains("CREATE TABLE [vendes].[Client ]]rar]", sql)
        Assert.Contains("[Id] int IDENTITY(100,5) NOT NULL", sql)
        Assert.Contains("[Nom] nvarchar(100) COLLATE Latin1_General_CI_AS NOT NULL", sql)
        Assert.Contains("[Notes] varchar(max) NULL", sql)
        Assert.Contains("[Telefon] [dbo].[Telefon] NULL", sql)
        Assert.Contains("[Saldo] decimal(18,2) NULL", sql)
        Assert.Contains("[Alta] datetime2(7) NULL", sql)
        Assert.Contains("[Ratio] float(24) NULL", sql)
        Assert.Contains("[Guid] uniqueidentifier ROWGUIDCOL NOT NULL CONSTRAINT [DF_Client_Guid] DEFAULT (newid())", sql)
        Assert.Contains("[Extra] int SPARSE NULL", sql)
        Assert.Contains("[SaldoIva] AS ([Saldo]*(1.21)) PERSISTED", sql)
        Assert.Contains("CONSTRAINT [PK_Client] PRIMARY KEY CLUSTERED ([Id] ASC)", sql)
        Assert.Contains("CONSTRAINT [UQ_Client_Nom] UNIQUE NONCLUSTERED ([Nom] DESC)", sql)
        ' Els CHECK i els índexs normals es creen després de les dades
        Assert.DoesNotContain("CHECK", sql)
        Assert.DoesNotContain("IX_Client_Alta", sql)
    End Sub

    <Fact>
    Public Sub SelectCopia_OmetCalculadesIRowversion_ILlegeixClrComBinari()
        Dim sql As String = GeneradorCataleg.SelectCopia(CatalegExemple().Taules(0))
        Assert.DoesNotContain("[SaldoIva]", sql)
        Assert.DoesNotContain("[Versio]", sql)
        Assert.Contains("CAST([Lloc] AS VARBINARY(MAX)) AS [Lloc]", sql)
    End Sub

    <Fact>
    Public Sub IndexsFKICheck_ConservenOpcions()
        Dim cat As CatalegBD = CatalegExemple()
        Dim ix As String = GeneradorCataleg.CrearIndexs(cat.Taules(0)).Single()
        Assert.Equal("CREATE NONCLUSTERED INDEX [IX_Client_Alta] ON [vendes].[Client ]]rar] ([Alta] DESC) INCLUDE ([Nom]) WHERE ([Alta] IS NOT NULL);", ix)
        Dim fk As String = GeneradorCataleg.CrearFK(cat.Taules(1), cat.Taules(1).FKs(0))
        Assert.Contains("WITH NOCHECK ADD CONSTRAINT [FK_Linia_Client] FOREIGN KEY ([ClientId]) REFERENCES [vendes].[Client ]]rar] ([Id]) ON DELETE CASCADE ON UPDATE NO ACTION NOT FOR REPLICATION;", fk)
        Assert.Contains("NOCHECK CONSTRAINT [FK_Linia_Client]", fk)
        Dim ck As String = GeneradorCataleg.CrearCheck(cat.Taules(0), cat.Taules(0).Checks(0))
        Assert.Contains("WITH NOCHECK ADD CONSTRAINT [CK_Client_Saldo] CHECK ([Saldo]>=(0));", ck)
    End Sub

    <Fact>
    Public Sub ReajustarIdentity_ContinuaComAlOrigen()
        Dim t As CatTaula = CatalegExemple().Taules(0)
        Dim id As CatColumna = t.Columnes(0)
        ' Amb files: el següent valor serà 235 + 5
        Assert.Contains("RESEED, 235)", GeneradorCataleg.ReajustarIdentity(t, id, True))
        ' Taula buida: el següent valor és exactament el del RESEED
        Assert.Contains("RESEED, 240)", GeneradorCataleg.ReajustarIdentity(t, id, False))
        id.IdentityUltimValor = Nothing
        Assert.Null(GeneradorCataleg.ReajustarIdentity(t, id, True))
    End Sub

    <Fact>
    Public Sub RutesICollation()
        Assert.Equal("C:\Dades\nou.mdf", CopiaBaseDades.CombinarRuta("C:\Dades", "nou.mdf"))
        Assert.Equal("C:\Dades\nou.mdf", CopiaBaseDades.CombinarRuta("C:\Dades\", "nou.mdf"))
        Assert.Equal("/var/opt/mssql/data/nou.mdf", CopiaBaseDades.CombinarRuta("/var/opt/mssql/data", "nou.mdf"))
        Assert.Equal(Path.Combine("dir", "Botiga_log.ldf"), CopiaBaseDades.RutaLog(Path.Combine("dir", "Botiga.mdf")))
        Assert.Equal(" COLLATE Latin1_General_CI_AS", MdfExporter.ClausulaCollation("Latin1_General_CI_AS"))
        Assert.Equal("", MdfExporter.ClausulaCollation("x; DROP DATABASE y"))
        Assert.Equal("", MdfExporter.ClausulaCollation(Nothing))
    End Sub

End Class

Public Class ConsultaDadesTests

    <Fact>
    Public Sub ConsultaAmbCommit_EsRefusa()
        Assert.Throws(Of InvalidOperationException)(
            Sub() ConsultaDades.ExecutarConsulta(Nothing, "DELETE FROM T; COMMIT", 10))
        Assert.Throws(Of InvalidOperationException)(
            Sub() ConsultaDades.ExecutarConsulta(Nothing, "begin tran; update t set a=1", 10))
    End Sub

    <Fact>
    Public Sub PerMostrar_ConverteixBinarisAText()
        Dim dt As New DataTable("T")
        dt.Columns.Add("Id", GetType(Integer))
        dt.Columns.Add("Dades", GetType(Byte()))
        dt.Rows.Add(1, New Byte() {&HDE, &HAD, &HBE, &HEF})
        dt.Rows.Add(2, DBNull.Value)
        Dim v As DataTable = ConsultaDades.PerMostrar(dt)
        Assert.Equal(GetType(String), v.Columns("Dades").DataType)
        Assert.Equal("0xDEADBEEF", CStr(v.Rows(0)("Dades")))
        Assert.True(IsDBNull(v.Rows(1)("Dades")))
        ' Sense columnes binàries es retorna la mateixa taula
        dt.Columns.Remove("Dades")
        Assert.Same(dt, ConsultaDades.PerMostrar(dt))
    End Sub

    <Fact>
    Public Sub ExportarCsv_EscapaCampsICapcalera()
        Dim dt As New DataTable("T")
        dt.Columns.Add("Nom", GetType(String))
        dt.Columns.Add("Nota", GetType(String))
        dt.Rows.Add("Anna", "té ""cometes""; i punt i coma")
        dt.Rows.Add("Pere", DBNull.Value)
        Dim ruta As String = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") & ".csv")
        Try
            ConsultaDades.ExportarCsv(dt, ruta)
            Dim linies As String() = File.ReadAllLines(ruta)
            Assert.Equal("Nom;Nota", linies(0))
            Assert.Equal("Anna;""té """"cometes""""; i punt i coma""", linies(1))
            Assert.Equal("Pere;", linies(2))
            Assert.Equal(&HEF, File.ReadAllBytes(ruta)(0))   ' BOM UTF-8 per a Excel
        Finally
            If File.Exists(ruta) Then File.Delete(ruta)
        End Try
    End Sub

End Class
