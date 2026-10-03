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
Imports System.Threading.Tasks
Imports System.Windows.Forms

' ============================================================
' OperacioLlarga.vb — Executa feina lenta (BD, fitxers) fora del
' fil de la interfície.
'
' Mentre dura l'operació el formulari queda desactivat i amb el
' cursor d'espera, de manera que l'usuari no pot tornar a llançar
' l'acció, modificar el model que s'està llegint ni tancar la
' finestra. A diferència del bucle amb Application.DoEvents(), no
' hi ha reentrada d'esdeveniments.
' ============================================================
Public Module OperacioLlarga

    Public Async Function ExecutarAsync(form As Form, accio As Action) As Task
        Dim estavaActiu As Boolean = form.Enabled
        form.UseWaitCursor = True
        form.Enabled = False
        Try
            Await Task.Run(accio)
        Finally
            If Not form.IsDisposed Then
                form.Enabled = estavaActiu
                form.UseWaitCursor = False
            End If
        End Try
    End Function

End Module
