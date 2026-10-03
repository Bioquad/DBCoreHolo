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

' ============================================================
' IntegrityEngine.vb — Regles del Model Entitat-Relació
' Els noms de taules i camps es comparen sense distingir
' majúscules (com fa SQL Server amb la col·lació per defecte).
' ============================================================
Public Module IntegrityEngine

    Public Function ValidarRelacio(
        taules As List(Of TablaBBDD),
        relacions As List(Of RelacionBBDD),
        fromId As Integer,
        toId As Integer,
        fkNom As String,
        pkNom As String,
        tipus As CardinalityType,
        Optional excloudId As Integer = -1) As List(Of String)

        Dim errs As New List(Of String)()

        Dim ft As TablaBBDD = BuscarTaula(taules, fromId)
        Dim tt As TablaBBDD = BuscarTaula(taules, toId)

        If ft Is Nothing Then
            errs.Add(Locale.Str("MER_ORIGEN_NO_EXISTEIX"))
            Return errs
        End If
        If tt Is Nothing Then
            errs.Add(Locale.Str("MER_DESTI_NO_EXISTEIX"))
            Return errs
        End If
        ' Auto-relació permesa (reflexiva): fromId = toId és vàlid

        Dim ffk As CampoBBDD = BuscarCamp(ft, fkNom)
        Dim fpk As CampoBBDD = BuscarCamp(tt, pkNom)

        If ffk Is Nothing Then
            errs.Add(String.Format(Locale.Str("MER_FK_NO_EXISTEIX"), fkNom, ft.Nombre))
        End If

        If fpk Is Nothing Then
            errs.Add(String.Format(Locale.Str("MER_PK_NO_EXISTEIX"), pkNom, tt.Nombre))
        ElseIf Not fpk.EsPK Then
            errs.Add(String.Format(Locale.Str("MER_NO_ES_PK"), pkNom, tt.Nombre))
        ElseIf tt.PKFields.Count > 1 AndAlso Not fpk.EsUnique Then
            ' Una FK d'una sola columna no pot referenciar una part d'una PK composta
            errs.Add(String.Format(Locale.Str("MER_PK_COMPOSTA"), pkNom, tt.Nombre))
        End If

        If ffk IsNot Nothing AndAlso fpk IsNot Nothing Then
            Dim errTipus As String = CompararTipus(ffk, fpk)
            If errTipus IsNot Nothing Then errs.Add(errTipus)
        End If

        If tipus = CardinalityType.ManyToMany Then
            errs.Add(Locale.Str("MER_MM_PROHIBIT"))
        End If

        For Each r As RelacionBBDD In relacions
            If r.Id <> excloudId AndAlso
               r.TablaOrigenId = fromId AndAlso
               r.TablaDestinoId = toId AndAlso
               IgualNom(r.CampoFKNombre, fkNom) Then
                errs.Add(Locale.Str("MER_REL_DUPLICADA"))
                Exit For
            End If
        Next

        Return errs
    End Function

    Public Function ValidarEliminarTaula(
        taules As List(Of TablaBBDD),
        relacions As List(Of RelacionBBDD),
        taulaId As Integer) As List(Of String)

        Dim errs As New List(Of String)()
        Dim t As TablaBBDD = BuscarTaula(taules, taulaId)
        If t Is Nothing Then Return errs

        For Each r As RelacionBBDD In relacions
            If r.TablaDestinoId = taulaId Then
                Dim ft As TablaBBDD = BuscarTaula(taules, r.TablaOrigenId)
                If ft IsNot Nothing Then
                    errs.Add(String.Format(Locale.Str("MER_REF_ELIMINAR"), ft.Nombre, r.CampoFKNombre, t.Nombre))
                End If
            End If
        Next

        Return errs
    End Function

    ''' <summary>
    ''' Valida tot el model. Els missatges que afecten una taula comencen per
    ''' "[TAULA" perquè el dissenyador pugui fer parpellejar la taula.
    ''' </summary>
    Public Function ValidarModelComplet(proyecto As ProyectoBBDD) As List(Of String)
        Dim errs As New List(Of String)()

        For Each t As TablaBBDD In proyecto.Taules
            If t.PKField Is Nothing Then
                errs.Add(String.Format(Locale.Str("MER_SENSE_PK"), t.Nombre))
            End If
            For Each f As CampoBBDD In t.Fields
                If f.EsFK Then
                    Dim trobat As Boolean = False
                    For Each r As RelacionBBDD In proyecto.Relacions
                        If r.TablaOrigenId = t.Id AndAlso IgualNom(r.CampoFKNombre, f.Nombre) Then
                            trobat = True
                            Exit For
                        End If
                    Next
                    If Not trobat Then
                        errs.Add(String.Format(Locale.Str("MER_FK_SENSE_REL"), t.Nombre, f.Nombre))
                    End If
                End If
            Next
        Next

        For Each r As RelacionBBDD In proyecto.Relacions
            Dim ft As TablaBBDD = BuscarTaula(proyecto.Taules, r.TablaOrigenId)
            Dim tt As TablaBBDD = BuscarTaula(proyecto.Taules, r.TablaDestinoId)
            If ft Is Nothing OrElse tt Is Nothing Then
                errs.Add(String.Format(Locale.Str("MER_REL_ORFE"), r.Nombre))
                Continue For
            End If

            Dim ffk As CampoBBDD = BuscarCamp(ft, r.CampoFKNombre)
            Dim fpk As CampoBBDD = BuscarCamp(tt, r.CampoPKNombre)
            If ffk Is Nothing OrElse fpk Is Nothing Then Continue For

            ' El tipus de la FK ha de coincidir exactament amb el referenciat
            If CompararTipus(ffk, fpk) IsNot Nothing Then
                errs.Add(String.Format(Locale.Str("MER_REL_TIPUS"),
                                       ft.Nombre, ffk.Nombre, ffk.EtiquetaTipus,
                                       tt.Nombre, fpk.Nombre, fpk.EtiquetaTipus))
            End If

            ' SET NULL sobre una columna NOT NULL fallaria en crear la FK
            If (r.OnDelete = OnDeleteUpdateAction.SetNull OrElse r.OnUpdate = OnDeleteUpdateAction.SetNull) AndAlso
               (ffk.NotNull OrElse ffk.EsPK) Then
                errs.Add(String.Format(Locale.Str("MER_SETNULL_NOTNULL"), ft.Nombre, ffk.Nombre, r.Nombre))
            End If
        Next

        Return errs
    End Function

    ' ════════════════════════════════════════════════════════
    ' HELPERS
    ' ════════════════════════════════════════════════════════

    ''' <summary>
    ''' Retorna Nothing si els tipus són compatibles per a una FK; si no,
    ''' el missatge d'error. SQL Server exigeix mateix tipus, longitud i
    ''' precisió/escala (errors 1778 i 1753).
    ''' </summary>
    Private Function CompararTipus(ffk As CampoBBDD, fpk As CampoBBDD) As String
        If TipusBase(ffk) <> TipusBase(fpk) Then
            Return String.Format(Locale.Str("MER_TIPUS_INCOMPAT"), ffk.TipoDato.ToString(), fpk.TipoDato.ToString())
        End If
        If Not String.Equals(ffk.EtiquetaTipus, fpk.EtiquetaTipus, StringComparison.OrdinalIgnoreCase) Then
            Return String.Format(Locale.Str("MER_LONG_INCOMPAT"), ffk.EtiquetaTipus, fpk.EtiquetaTipus)
        End If
        Return Nothing
    End Function

    ' VARCHAR + LongitudMax i VARCHAR(MAX) són el mateix tipus
    Private Function TipusBase(f As CampoBBDD) As DataType
        Select Case f.TipoDato
            Case DataType.VarCharMax   : Return DataType.VarChar
            Case DataType.NVarCharMax  : Return DataType.NVarChar
            Case DataType.VarBinaryMax : Return DataType.VarBinary
            Case Else                  : Return f.TipoDato
        End Select
    End Function

    Private Function IgualNom(a As String, b As String) As Boolean
        Return String.Equals(a, b, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function BuscarTaula(taules As List(Of TablaBBDD), id As Integer) As TablaBBDD
        For Each t As TablaBBDD In taules
            If t.Id = id Then Return t
        Next
        Return Nothing
    End Function

    Private Function BuscarCamp(t As TablaBBDD, nom As String) As CampoBBDD
        For Each f As CampoBBDD In t.Fields
            If IgualNom(f.Nombre, nom) Then Return f
        Next
        Return Nothing
    End Function

End Module
