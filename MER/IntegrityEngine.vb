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
Imports System.Collections.Generic

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

        Dim ft As TablaBBDD = Nothing
        Dim tt As TablaBBDD = Nothing

        For Each t As TablaBBDD In taules
            If t.Id = fromId Then ft = t
            If t.Id = toId Then tt = t
        Next

        If ft Is Nothing Then
            errs.Add(Locale.Str("MER_ORIGEN_NO_EXISTEIX"))
            Return errs
        End If
        If tt Is Nothing Then
            errs.Add(Locale.Str("MER_DESTI_NO_EXISTEIX"))
            Return errs
        End If
        ' Auto-relació permesa (reflexiva): fromId = toId es vàlid

        Dim ffk As CampoBBDD = Nothing
        Dim fpk As CampoBBDD = Nothing

        For Each f As CampoBBDD In ft.Fields
            If f.Nombre = fkNom Then ffk = f
        Next
        For Each f As CampoBBDD In tt.Fields
            If f.Nombre = pkNom Then fpk = f
        Next

        If ffk Is Nothing Then
            errs.Add(String.Format(Locale.Str("MER_FK_NO_EXISTEIX"), fkNom, ft.Nombre))
        End If

        ' En auto-relació, el camp PK és a la mateixa taula
        Dim taulaDesti As TablaBBDD = If(fromId = toId, ft, tt)
        For Each f As CampoBBDD In taulaDesti.Fields
            If f.Nombre = pkNom Then fpk = f
        Next

        If fpk Is Nothing Then
            errs.Add(String.Format(Locale.Str("MER_PK_NO_EXISTEIX"), pkNom, taulaDesti.Nombre))
        ElseIf Not fpk.EsPK Then
            errs.Add(String.Format(Locale.Str("MER_NO_ES_PK"), pkNom, taulaDesti.Nombre))
        End If

        If ffk IsNot Nothing AndAlso fpk IsNot Nothing Then
            If ffk.TipoDato <> fpk.TipoDato Then
                errs.Add(String.Format(Locale.Str("MER_TIPUS_INCOMPAT"), ffk.TipoDato.ToString(), fpk.TipoDato.ToString()))
            End If
        End If

        If tipus = CardinalityType.ManyToMany Then
            errs.Add(Locale.Str("MER_MM_PROHIBIT"))
        End If

        For Each r As RelacionBBDD In relacions
            If r.Id <> excloudId AndAlso
               r.TablaOrigenId = fromId AndAlso
               r.TablaDestinoId = toId AndAlso
               r.CampoFKNombre = fkNom Then
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

        Dim t As TablaBBDD = Nothing
        For Each x As TablaBBDD In taules
            If x.Id = taulaId Then t = x
        Next
        If t Is Nothing Then Return errs

        For Each r As RelacionBBDD In relacions
            If r.TablaDestinoId = taulaId Then
                Dim ft As TablaBBDD = Nothing
                For Each x As TablaBBDD In taules
                    If x.Id = r.TablaOrigenId Then ft = x
                Next
                If ft IsNot Nothing Then
                    errs.Add(String.Format(Locale.Str("MER_REF_ELIMINAR"), ft.Nombre, r.CampoFKNombre, t.Nombre))
                End If
            End If
        Next

        Return errs
    End Function

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
                        If r.TablaOrigenId = t.Id AndAlso r.CampoFKNombre = f.Nombre Then
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
            Dim ft As TablaBBDD = Nothing
            Dim tt As TablaBBDD = Nothing
            For Each t As TablaBBDD In proyecto.Taules
                If t.Id = r.TablaOrigenId Then ft = t
                If t.Id = r.TablaDestinoId Then tt = t
            Next
            If ft Is Nothing OrElse tt Is Nothing Then
                errs.Add(String.Format(Locale.Str("MER_REL_ORFE"), r.Nombre))
            End If
        Next

        Return errs
    End Function

End Module

