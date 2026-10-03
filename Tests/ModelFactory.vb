Imports System.Collections.Generic

' Projecte d'exemple que cobreix els casos delicats:
' esquema no dbo, PK composta formada per FK, DEFAULT repetit
' en dues taules, CHECK, UNIQUE, descripcions amb cometes i
' accions ON DELETE (inclosa RESTRICT).
Friend Module ModelFactory

    Friend Function Camp(nom As String, tipus As DataType,
                         Optional longitud As Integer = 0,
                         Optional pk As Boolean = False,
                         Optional notNull As Boolean = False) As CampoBBDD
        Dim f As New CampoBBDD()
        f.Nombre = nom
        f.TipoDato = tipus
        f.Longitud = longitud
        f.EsPK = pk
        If notNull Then f.NotNull = True
        Return f
    End Function

    Friend Function ProjecteExemple() As ProyectoBBDD
        Dim p As New ProyectoBBDD()
        p.Nombre = "Botiga"

        ' ── CLIENT (esquema sales) ──────────────────────────────
        Dim client As New TablaBBDD()
        client.Id = p.GetNextTableId()
        client.Nombre = "CLIENT"
        client.Schema = "sales"
        client.Descripcion = "Clients de l'empresa"
        Dim idc As CampoBBDD = Camp("ID_CLIENT", DataType.DbInt, pk:=True)
        idc.EsIdentity = True
        client.Fields.Add(idc)
        Dim nomc As CampoBBDD = Camp("NOM", DataType.NVarChar, 100, notNull:=True)
        nomc.Descripcion = "Nom ""complet"" del client"
        client.Fields.Add(nomc)
        Dim email As CampoBBDD = Camp("EMAIL", DataType.VarChar, 200)
        email.EsUnique = True
        client.Fields.Add(email)
        Dim alta As CampoBBDD = Camp("DATA_ALTA", DataType.DateTime2, notNull:=True)
        alta.DefaultValue = "GETDATE()"
        client.Fields.Add(alta)
        Dim actiu As CampoBBDD = Camp("ACTIU", DataType.Bit, notNull:=True)
        actiu.DefaultValue = "1"
        client.Fields.Add(actiu)
        p.Taules.Add(client)

        ' ── PRODUCTE ────────────────────────────────────────────
        Dim prod As New TablaBBDD()
        prod.Id = p.GetNextTableId()
        prod.Nombre = "PRODUCTE"
        Dim idp As CampoBBDD = Camp("ID_PRODUCTE", DataType.DbInt, pk:=True)
        idp.EsIdentity = True
        prod.Fields.Add(idp)
        prod.Fields.Add(Camp("NOM", DataType.NVarChar, 100, notNull:=True))
        Dim preu As CampoBBDD = Camp("PREU", DataType.DbDecimal, notNull:=True)
        preu.Precision = 10
        preu.Escala = 2
        preu.CheckExpression = "[PREU] >= 0"
        prod.Fields.Add(preu)
        ' Mateix nom de camp i DEFAULT que a CLIENT → els noms DF_ no poden xocar
        Dim alta2 As CampoBBDD = Camp("DATA_ALTA", DataType.DateTime2, notNull:=True)
        alta2.DefaultValue = "GETDATE()"
        prod.Fields.Add(alta2)
        Dim desc As CampoBBDD = Camp("NOTA", DataType.NVarChar, 50)
        desc.DefaultValue = "N'sense -- nota (cap)'"
        prod.Fields.Add(desc)
        p.Taules.Add(prod)

        ' ── COMANDA_LINIA: PK composta formada per dues FK ──────
        Dim linia As New TablaBBDD()
        linia.Id = p.GetNextTableId()
        linia.Nombre = "COMANDA_LINIA"
        Dim lc As CampoBBDD = Camp("ID_CLIENT", DataType.DbInt, pk:=True)
        lc.EsFK = True
        linia.Fields.Add(lc)
        Dim lp As CampoBBDD = Camp("ID_PRODUCTE", DataType.DbInt, pk:=True)
        lp.EsFK = True
        linia.Fields.Add(lp)
        Dim qt As CampoBBDD = Camp("QUANTITAT", DataType.DbInt, notNull:=True)
        qt.DefaultValue = "1"
        linia.Fields.Add(qt)
        p.Taules.Add(linia)

        Dim r1 As New RelacionBBDD()
        r1.Id = p.GetNextRelId()
        r1.Nombre = "FK_COMANDA_LINIA_CLIENT"
        r1.TablaOrigenId = linia.Id
        r1.CampoFKNombre = "ID_CLIENT"
        r1.TablaDestinoId = client.Id
        r1.CampoPKNombre = "ID_CLIENT"
        r1.OnDelete = OnDeleteUpdateAction.DoCascade
        r1.Descripcion = "Línia d'un client"
        p.Relacions.Add(r1)

        Dim r2 As New RelacionBBDD()
        r2.Id = p.GetNextRelId()
        r2.Nombre = "FK_COMANDA_LINIA_PRODUCTE"
        r2.TablaOrigenId = linia.Id
        r2.CampoFKNombre = "ID_PRODUCTE"
        r2.TablaDestinoId = prod.Id
        r2.CampoPKNombre = "ID_PRODUCTE"
        r2.OnDelete = OnDeleteUpdateAction.DoRestrict
        p.Relacions.Add(r2)

        Return p
    End Function

    Friend Function Taula(p As ProyectoBBDD, nom As String) As TablaBBDD
        Return p.Taules.Find(Function(t) t.Nombre = nom)
    End Function

    Friend Function CampDe(t As TablaBBDD, nom As String) As CampoBBDD
        Return t.Fields.Find(Function(f) f.Nombre = nom)
    End Function

End Module
