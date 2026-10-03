Imports System.IO
Imports Microsoft.SqlServer.TransactSql.ScriptDom
Imports Xunit

' Valida amb el parser oficial de SQL Server (ScriptDom) que tot el
' T-SQL generat és sintàcticament correcte, tant l'script complet com
' les sentències individuals que executa el desplegament a servidor.
Public Class TSqlSintaxiTests

    Private Shared Sub AssertSintaxiValida(sql As String)
        Dim parser As New TSql160Parser(initialQuotedIdentifiers:=True)
        Dim errors As IList(Of ParseError) = Nothing
        Using rd As New StringReader(sql)
            parser.Parse(rd, errors)
        End Using
        Dim msg As String = String.Join(Environment.NewLine,
            errors.Select(Function(e) $"L{e.Line}:{e.Column} {e.Message}")) &
            Environment.NewLine & sql
        Assert.True(errors.Count = 0, msg)
    End Sub

    Private Shared Function ProjecteComplet() As ProyectoBBDD
        Dim p As ProyectoBBDD = ProjecteExemple()
        Dim t As TablaBBDD = Taula(p, "PRODUCTE")
        For Each dt As DataType In [Enum].GetValues(GetType(DataType))
            If dt = DataType.DbTimestamp OrElse dt = DataType.RowVersion Then Continue For
            t.Fields.Add(New CampoBBDD() With {.Nombre = "C_" & dt.ToString().ToUpper(), .TipoDato = dt, .Longitud = 20})
        Next
        t.Fields.Add(New CampoBBDD() With {.Nombre = "VERSIO", .TipoDato = DataType.RowVersion})
        t.Fields.Add(New CampoBBDD() With {.Nombre = "PREU_IVA", .EsCalculado = True,
                                           .FormulaCalculo = "[PREU] * 1.21", .EsPersistido = True})
        Dim mask As New CampoBBDD() With {.Nombre = "TELEFON", .TipoDato = DataType.VarChar, .Longitud = 20,
                                          .DataMask = DataMaskFunction.MaskPartial, .MaskPrefix = 2,
                                          .MaskPadding = "XXXX", .MaskSuffix = 1, .Collation = "Latin1_General_CI_AS"}
        Taula(p, "CLIENT").Fields.Add(mask)
        Dim guid As New CampoBBDD() With {.Nombre = "ROW_GUID", .TipoDato = DataType.UniqueIdentifier,
                                          .EsRowGuid = True, .NotNull = True, .DefaultValue = "NEWID()"}
        Taula(p, "CLIENT").Fields.Add(guid)
        p.Relacions(1).Disabled = True
        p.Relacions(1).NotForReplication = True
        p.Relacions(1).WithCheck = WithCheckOption.WithNoCheck
        Return p
    End Function

    <Fact>
    Public Sub Parser_DetectaLesComesDoblesDeLaVersioAnterior()
        Dim parser As New TSql160Parser(True)
        Dim errors As IList(Of ParseError) = Nothing
        Using rd As New StringReader("CREATE TABLE [X] ([A] INT NOT NULL," & vbLf & "    ,CONSTRAINT [PK_X] PRIMARY KEY ([A]));")
            parser.Parse(rd, errors)
        End Using
        Assert.NotEmpty(errors)
    End Sub

    <Fact>
    Public Sub ScriptComplet_EsTSqlValid()
        AssertSintaxiValida(New TSqlExporter().Generar(ProjecteComplet()))
    End Sub

    <Fact>
    Public Sub SentenciesDelDesplegament_SonTSqlValides()
        Dim p As ProyectoBBDD = ProjecteComplet()
        Dim exp As New TSqlExporter()
        For Each esq As String In TSqlExporter.EsquemesNecessaris(p.Taules)
            AssertSintaxiValida(exp.GenerarCrearEsquema(esq))
        Next
        For Each t As TablaBBDD In p.Taules
            AssertSintaxiValida(exp.GenerarTaula(t))
            AssertSintaxiValida(exp.GenerarExtPropTaula(t, actualitzar:=True))
            For Each f As CampoBBDD In t.Fields
                If Not f.EsPK Then AssertSintaxiValida(exp.GenerarAddColumn(t, f, New List(Of String)()))
            Next
        Next
        For Each r As RelacionBBDD In p.Relacions
            Dim ft As TablaBBDD = p.Taules.Find(Function(x) x.Id = r.TablaOrigenId)
            Dim tt As TablaBBDD = p.Taules.Find(Function(x) x.Id = r.TablaDestinoId)
            AssertSintaxiValida(exp.GenerarAlterFK(r, ft, tt))
            AssertSintaxiValida(exp.GenerarIndexFK(r, ft))
            AssertSintaxiValida(exp.GenerarExtPropRelacio(r, ft))
        Next
    End Sub

End Class
