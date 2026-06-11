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

Public Class RelacionBBDD

    Public Property Id As Integer = 0
    Public Property Nombre As String = ""
    Public Property Descripcion As String = ""   ' exportat al DDL
    Public Property Comentari As String = ""      ' nota interna — guardat al .hdb, no exportat al DDL
    Public Property Caption As String = ""
    Public Property TablaOrigenId As Integer = -1
    Public Property CampoFKNombre As String = ""
    Public Property TablaDestinoId As Integer = -1
    Public Property CampoPKNombre As String = ""
    Public Property TipoRelacion As CardinalityType = CardinalityType.OneToMany
    Public Property OnDelete As OnDeleteUpdateAction = OnDeleteUpdateAction.NoAction
    Public Property OnUpdate As OnDeleteUpdateAction = OnDeleteUpdateAction.NoAction
    Public Property WithCheck As WithCheckOption = WithCheckOption.WithCheck
    Public Property NotForReplication As Boolean = False
    Public Property Disabled As Boolean = False
    Public Property CrearIndexFK As Boolean = True
    Public Property ExtendedDesc As String = ""
    Public Property ColorHex As String = ""

    Public ReadOnly Property CardinalityLabel As String
        Get
            Select Case TipoRelacion
                Case CardinalityType.OneToOne
                    Return "1:1"
                Case CardinalityType.OneToMany
                    Return "1:M"
                Case CardinalityType.ManyToOne
                    Return "M:1"
                Case CardinalityType.ManyToMany
                    Return "M:M"
                Case Else
                    Return "1:M"
            End Select
        End Get
    End Property

    Public ReadOnly Property OnDeleteLabel As String
        Get
            Return GetActionLabel(OnDelete)
        End Get
    End Property

    Public ReadOnly Property OnUpdateLabel As String
        Get
            Return GetActionLabel(OnUpdate)
        End Get
    End Property

    Private Shared Function GetActionLabel(a As OnDeleteUpdateAction) As String
        Select Case a
            Case OnDeleteUpdateAction.DoCascade
                Return "CASCADE"
            Case OnDeleteUpdateAction.SetNull
                Return "SET NULL"
            Case OnDeleteUpdateAction.SetDefault
                Return "SET DEFAULT"
            Case OnDeleteUpdateAction.DoRestrict
                Return "RESTRICT"
            Case Else
                Return "NO ACTION"
        End Select
    End Function

    Public Function Clone() As RelacionBBDD
        Return DirectCast(Me.MemberwiseClone(), RelacionBBDD)
    End Function

    Public Overrides Function ToString() As String
        Return Nombre & " [" & CardinalityLabel & "]"
    End Function

End Class

