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
Imports System.Windows.Forms

Module Program
    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        ' Usar ApplicationContext per controlar el cicle de vida
        ' L'aplicació no tanca fins que tots els formularis estiguin tancats
        Application.Run(New AppContext())
    End Sub
End Module

Public Class AppContext
    Inherits ApplicationContext

    Private _splash As FrmSplash

    Public Sub New()
        _splash = New FrmSplash()
        AddHandler _splash.FormClosed, AddressOf OnSplashClosed
        _splash.Show()
    End Sub

    Private Sub OnSplashClosed(sender As Object, e As FormClosedEventArgs)
        ' El splash s'ha tancat - no fer res, el FrmMainMenu ja esta obert
        ' Si no hi ha cap formulari obert, l'ApplicationContext surt sol
    End Sub
End Class
