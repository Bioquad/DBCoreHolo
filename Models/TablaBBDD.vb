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

Public Class TablaBBDD

    Public Property Id As Integer = 0
    Public Property Nombre As String = "NOVA_TAULA"
    Public Property Schema As String = "dbo"
    Public Property Descripcion As String = ""   ' exportat al DDL
    Public Property Comentari As String = ""      ' nota interna — guardat al .hdb, no exportat al DDL
    Public Property Fields As New List(Of CampoBBDD)()
    Public Property GrupColor As GroupColor = GroupColor.ColorOrange
    Public Property EsMatrix As Boolean = False
    Public Property PosX As Single = 0
    Public Property PosY As Single = 0
    Public Property PosZ As Single = 0
    Public Property Depth As Single = 1.0F

    Public ReadOnly Property PKField As CampoBBDD
        Get
            For Each f As CampoBBDD In Fields
                If f.EsPK Then Return f
            Next
            Return Nothing
        End Get
    End Property

    ''' <summary>
    ''' Tots els camps que formen la PK (més d'un si la PK és composta),
    ''' en l'ordre en què apareixen a la taula.
    ''' </summary>
    <Newtonsoft.Json.JsonIgnore>
    Public ReadOnly Property PKFields As List(Of CampoBBDD)
        Get
            Return Fields.FindAll(Function(f) f.EsPK)
        End Get
    End Property

    ''' <summary>Esquema efectiu: "dbo" si no se n'ha indicat cap.</summary>
    <Newtonsoft.Json.JsonIgnore>
    Public ReadOnly Property SchemaEfectiu As String
        Get
            Return If(String.IsNullOrWhiteSpace(Schema), "dbo", Schema.Trim())
        End Get
    End Property

    Public ReadOnly Property ColorGL As Color
        Get
            Select Case GrupColor
                Case GroupColor.ColorOrange
                    Return Color.FromArgb(255, 96, 16)
                Case GroupColor.ColorYellow
                    Return Color.FromArgb(255, 200, 0)
                Case GroupColor.ColorGreen
                    Return Color.FromArgb(48, 255, 112)
                Case GroupColor.ColorBlue
                    Return Color.FromArgb(64, 170, 255)
                Case GroupColor.ColorRed
                    Return Color.FromArgb(255, 48, 64)
                Case GroupColor.ColorCyan
                    Return Color.FromArgb(0, 255, 224)
                Case GroupColor.ColorMagenta
                    Return Color.FromArgb(255, 64, 204)
                Case GroupColor.ColorWhite
                    Return Color.FromArgb(187, 187, 187)
                Case Else
                    Return Color.FromArgb(255, 96, 16)
            End Select
        End Get
    End Property

    Public Sub SetPosition(x As Single, y As Single, z As Single)
        PosX = x
        PosY = y
        PosZ = z
    End Sub

    Public Function Clone() As TablaBBDD
        Dim t As TablaBBDD = DirectCast(Me.MemberwiseClone(), TablaBBDD)
        t.Fields = New List(Of CampoBBDD)()
        For Each f As CampoBBDD In Fields
            t.Fields.Add(f.Clone())
        Next
        Return t
    End Function

    Public Overrides Function ToString() As String
        Return Schema & "." & Nombre & " (" & Fields.Count & " camps)"
    End Function

End Class

