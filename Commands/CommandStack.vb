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


'  Imports System.Drawing
Imports System.Collections.Generic


Public Interface ICmd
    Sub Execute()
    Sub Undo()
    ReadOnly Property Desc As String
End Interface

Public Module CommandStack

    Private ReadOnly _undo As New LinkedList(Of ICmd)()
    Private ReadOnly _redo As New Stack(Of ICmd)()
    Private Const MAX As Integer = 50

    ' Event que es dispara quan el model es modifica (Push, PushSilent, Undo, Redo)
    Public Event Modificat()

    Public ReadOnly Property CanUndo As Boolean
        Get
            Return _undo.Count > 0
        End Get
    End Property

    Public ReadOnly Property CanRedo As Boolean
        Get
            Return _redo.Count > 0
        End Get
    End Property

    Public ReadOnly Property LastDesc As String
        Get
            If _undo.Count = 0 Then Return ""
            Return _undo.Last.Value.Desc
        End Get
    End Property

    Public Sub Push(c As ICmd)
        c.Execute()
        _undo.AddLast(c)
        If _undo.Count > MAX Then _undo.RemoveFirst()
        _redo.Clear()
        RaiseEvent Modificat()
    End Sub

    Public Sub UndoLast()
        If _undo.Count = 0 Then Return
        Dim c As ICmd = _undo.Last.Value
        _undo.RemoveLast()
        c.Undo()
        _redo.Push(c)
        RaiseEvent Modificat()
    End Sub

    Public Sub RedoLast()
        If _redo.Count = 0 Then Return
        Dim c As ICmd = _redo.Pop()
        c.Execute()
        _undo.AddLast(c)
        RaiseEvent Modificat()
    End Sub

    ''' <summary>
    ''' Afegeix una comanda a l'historial sense executar-la.
    ''' Usar quan l'acció ja ha ocorregut (p.ex. drag de ratolí) i només
    ''' necessitem registrar-la per a l'Undo posterior.
    ''' </summary>
    Public Sub PushSilent(c As ICmd)
        _undo.AddLast(c)
        If _undo.Count > MAX Then _undo.RemoveFirst()
        _redo.Clear()
        RaiseEvent Modificat()
    End Sub

    Public Sub Clear()
        _undo.Clear()
        _redo.Clear()
    End Sub

End Module

Public Class CmdAfegirTaula
    Implements ICmd

    Private ReadOnly _llista As List(Of TablaBBDD)
    Private ReadOnly _t As TablaBBDD

    Public Sub New(llista As List(Of TablaBBDD), t As TablaBBDD)
        _llista = llista
        _t = t
    End Sub

    Public ReadOnly Property Desc As String Implements ICmd.Desc
        Get
            Return String.Format(Locale.Str("CMD_AFEGIR_TAULA"), _t.Nombre)
        End Get
    End Property

    Public Sub Execute() Implements ICmd.Execute
        _llista.Add(_t)
    End Sub

    Public Sub Undo() Implements ICmd.Undo
        _llista.Remove(_t)
    End Sub

End Class

Public Class CmdEliminarTaula
    Implements ICmd

    Private ReadOnly _taules As List(Of TablaBBDD)
    Private ReadOnly _relacions As List(Of RelacionBBDD)
    Private ReadOnly _t As TablaBBDD
    Private ReadOnly _relsAfect As New List(Of RelacionBBDD)()
    Private ReadOnly _idx As Integer

    Public Sub New(taules As List(Of TablaBBDD), relacions As List(Of RelacionBBDD), t As TablaBBDD)
        _taules = taules
        _relacions = relacions
        _t = t
        _idx = taules.IndexOf(t)
        ' Snapshot de les relacions afectades en el moment de crear la comanda.
        ' Fem una còpia de la llista per evitar desfasament si el graf canvia
        ' entre la construcció i l'Execute (p.ex. en sequences d'undo encadenades).
        For Each r As RelacionBBDD In relacions
            If r.TablaOrigenId = t.Id OrElse r.TablaDestinoId = t.Id Then
                _relsAfect.Add(r)
            End If
        Next
    End Sub

    Public ReadOnly Property Desc As String Implements ICmd.Desc
        Get
            Return String.Format(Locale.Str("CMD_ELIM_TAULA"), _t.Nombre)
        End Get
    End Property

    Public Sub Execute() Implements ICmd.Execute
        For Each r As RelacionBBDD In _relsAfect
            _relacions.Remove(r)
        Next
        _taules.Remove(_t)
    End Sub

    Public Sub Undo() Implements ICmd.Undo
        If _idx >= 0 AndAlso _idx <= _taules.Count Then
            _taules.Insert(_idx, _t)
        Else
            _taules.Add(_t)
        End If
        For Each r As RelacionBBDD In _relsAfect
            _relacions.Add(r)
        Next
    End Sub

End Class

Public Class CmdMoureTaula
    Implements ICmd

    Private ReadOnly _t  As TablaBBDD
    Private ReadOnly _ox As Single   ' posició ORIGINAL (per Undo)
    Private ReadOnly _oy As Single
    Private ReadOnly _oz As Single
    Private ReadOnly _nx As Single   ' posició NOVA (per Execute/Redo)
    Private ReadOnly _ny As Single
    Private ReadOnly _nz As Single

    ''' <summary>
    ''' ox/oy/oz = posició original ABANS del drag
    ''' nx/ny/nz = posició nova DESPRÉS del drag (posició actual de la taula)
    ''' </summary>
    Public Sub New(t As TablaBBDD, ox As Single, oy As Single, oz As Single,
                   nx As Single, ny As Single, nz As Single)
        _t = t
        _ox = ox : _oy = oy : _oz = oz
        _nx = nx : _ny = ny : _nz = nz
    End Sub

    Public ReadOnly Property Desc As String Implements ICmd.Desc
        Get
            Return String.Format(Locale.Str("CMD_MOURE_TAULA"), _t.Nombre)
        End Get
    End Property

    Public Sub Execute() Implements ICmd.Execute
        _t.SetPosition(_nx, _ny, _nz)   ' anar a posició nova
    End Sub

    Public Sub Undo() Implements ICmd.Undo
        _t.SetPosition(_ox, _oy, _oz)   ' tornar a posició original
    End Sub

End Class

Public Class CmdAfegirRelacio
    Implements ICmd

    Private ReadOnly _llista As List(Of RelacionBBDD)
    Private ReadOnly _r As RelacionBBDD
    Private ReadOnly _campFK As CampoBBDD

    Public Sub New(llista As List(Of RelacionBBDD), r As RelacionBBDD, campFK As CampoBBDD)
        _llista = llista
        _r = r
        _campFK = campFK
    End Sub

    Public ReadOnly Property Desc As String Implements ICmd.Desc
        Get
            Return String.Format(Locale.Str("CMD_CREAR_REL"), _r.Nombre)
        End Get
    End Property

    Public Sub Execute() Implements ICmd.Execute
        _llista.Add(_r)
        If _campFK IsNot Nothing Then _campFK.EsFK = True
    End Sub

    Public Sub Undo() Implements ICmd.Undo
        _llista.Remove(_r)
        If _campFK IsNot Nothing Then _campFK.EsFK = False
    End Sub

End Class

Public Class CmdEliminarRelacio
    Implements ICmd

    Private ReadOnly _llista As List(Of RelacionBBDD)
    Private ReadOnly _r As RelacionBBDD
    Private ReadOnly _campFK As CampoBBDD

    Public Sub New(llista As List(Of RelacionBBDD), r As RelacionBBDD, campFK As CampoBBDD)
        _llista = llista
        _r = r
        _campFK = campFK
    End Sub

    Public ReadOnly Property Desc As String Implements ICmd.Desc
        Get
            Return String.Format(Locale.Str("CMD_ELIM_REL"), _r.Nombre)
        End Get
    End Property

    Public Sub Execute() Implements ICmd.Execute
        _llista.Remove(_r)
        If _campFK IsNot Nothing Then _campFK.EsFK = False
    End Sub

    Public Sub Undo() Implements ICmd.Undo
        _llista.Add(_r)
        If _campFK IsNot Nothing Then _campFK.EsFK = True
    End Sub

End Class


' ════════════════════════════════════════════════════════════════
' CmdEliminarCamp — elimina un camp d'una taula amb suport d'Undo.
' L'Undo reinsereix el camp a la seva posició original.
' ════════════════════════════════════════════════════════════════
Public Class CmdEliminarCamp
    Implements ICmd

    Private ReadOnly _taula As TablaBBDD
    Private ReadOnly _camp  As CampoBBDD
    Private ReadOnly _idx   As Integer

    Public Sub New(taula As TablaBBDD, idx As Integer)
        _taula = taula
        _idx   = idx
        _camp  = taula.Fields(idx)
    End Sub

    Public ReadOnly Property Desc As String Implements ICmd.Desc
        Get
            Return String.Format(Locale.Str("CMD_ELIM_CAMP"), _camp.Nombre)
        End Get
    End Property

    Public Sub Execute() Implements ICmd.Execute
        _taula.Fields.Remove(_camp)
    End Sub

    Public Sub Undo() Implements ICmd.Undo
        Dim pos As Integer = Math.Min(_idx, _taula.Fields.Count)
        _taula.Fields.Insert(pos, _camp)
    End Sub

End Class
