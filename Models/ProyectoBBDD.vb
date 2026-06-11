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

Public Class ProyectoBBDD

    Public Property Nombre As String = "NouProjecte"
    Public Property MotorSQL As String = "T-SQL"
    Public Property DataCreacio As DateTime = DateTime.Now
    Public Property DataModificacio As DateTime = DateTime.Now
    Public Property CameraRotX As Single = 0.3F
    Public Property CameraRotY As Single = 0.1F
    Public Property CameraZoom As Single = 1.0F
    Public Property CameraOffX As Single = 0.0F   ' pan horitzontal de la càmera
    Public Property CameraOffY As Single = 0.0F   ' pan vertical de la càmera
    Public Property Taules As New List(Of TablaBBDD)()
    Public Property Relacions As New List(Of RelacionBBDD)()

    ' Públics per serialització JSON — garanteix IDs únics en carregar
    Public Property NextRelId As Integer = 0
    Public Property NextTableId As Integer = 0

    Public Function GetNextRelId() As Integer
        NextRelId += 1
        Return NextRelId
    End Function

    Public Function GetNextTableId() As Integer
        NextTableId += 1
        Return NextTableId
    End Function

    ''' <summary>
    ''' Recalcula els NextTableId i NextRelId basant-se en els IDs existents.
    ''' Cridat després de deserialitzar per garantir IDs únics.
    ''' </summary>
    Public Sub RecalcularIds()
        Dim maxT As Integer = 0
        For Each t As TablaBBDD In Taules
            If t.Id > maxT Then maxT = t.Id
        Next
        If maxT > NextTableId Then NextTableId = maxT

        Dim maxR As Integer = 0
        For Each r As RelacionBBDD In Relacions
            If r.Id > maxR Then maxR = r.Id
        Next
        If maxR > NextRelId Then NextRelId = maxR
    End Sub

    Public Shared Function NouProjecte() As ProyectoBBDD
        Return New ProyectoBBDD()
    End Function

End Class

