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
Imports System.Text.RegularExpressions

' ============================================================
' DialecteSql.vb — Utilitats compartides pels exportadors
' MySQL i PostgreSQL (traducció de valors DEFAULT escrits amb
' sintaxi T-SQL al model).
' ============================================================
Friend Module DialecteSql

    Friend Enum Motor
        MySql
        PostgreSql
    End Enum

    ''' <summary>
    ''' Tradueix un DEFAULT del model (sintaxi T-SQL) al motor indicat.
    ''' Retorna l'expressió ja preparada per posar després de "DEFAULT ".
    ''' </summary>
    Friend Function ConvertirDefault(valor As String, f As CampoBBDD, m As Motor,
                                     Optional t As TablaBBDD = Nothing) As String
        Dim v As String = ConvertirExpressio(SqlServerConnector.TreureParentesisExterns(valor), m, t)

        Dim up As String = v.Trim().ToUpperInvariant()

        ' Booleans: BIT 0/1 → FALSE/TRUE a PostgreSQL
        If m = Motor.PostgreSql AndAlso f.TipoDato = DataType.Bit Then
            Dim b As String = up.Trim("("c, ")"c, " "c)
            If b = "1" OrElse b = "'1'" Then Return "TRUE"
            If b = "0" OrElse b = "'0'" Then Return "FALSE"
        End If

        Select Case up
            Case "GETDATE()", "SYSDATETIME()", "CURRENT_TIMESTAMP", "SYSDATETIMEOFFSET()"
                Return "CURRENT_TIMESTAMP"
            Case "GETUTCDATE()", "SYSUTCDATETIME()"
                Return If(m = Motor.MySql, "(UTC_TIMESTAMP())", "(NOW() AT TIME ZONE 'UTC')")
            Case "NEWID()", "NEWSEQUENTIALID()"
                Return If(m = Motor.MySql, "(UUID())", "gen_random_uuid()")
        End Select

        ' Literal numèric o de cadena: tal qual
        If Regex.IsMatch(v, "^\s*-?\d+(\.\d+)?\s*$") OrElse Regex.IsMatch(v, "^\s*'([^']|'')*'\s*$") OrElse
           up = "NULL" OrElse up = "TRUE" OrElse up = "FALSE" Then
            Return v.Trim()
        End If

        ' Qualsevol altra expressió: entre parèntesis (MySQL 8.0.13+ ho exigeix)
        Return "(" & v.Trim() & ")"
    End Function

    ''' <summary>
    ''' Adapta una expressió del model (CHECK, columna calculada, DEFAULT)
    ''' escrita en T-SQL: [identificador] → `identificador` / "identificador"
    ''' i N'text' → 'text'. El contingut dels literals no es toca.
    ''' </summary>
    Friend Function ConvertirExpressio(expr As String, m As Motor,
                                       Optional t As TablaBBDD = Nothing) As String
        If String.IsNullOrEmpty(expr) Then Return expr
        Dim obre As String = If(m = Motor.MySql, "`", """")
        ' Recorre literals ('...'), identificadors entre claudàtors o cometes
        ' i paraules; els literals es deixen intactes.
        Return Regex.Replace(expr,
            "(?:(?<!\w)N)?('(?:[^']|'')*')|\[([^\]]+)\]|(""[^""]*""|`[^`]*`)|\b([A-Za-z_]\w*)\b",
            Function(x As Match) As String
                If x.Groups(1).Success Then Return x.Groups(1).Value
                If x.Groups(2).Success Then Return obre & NomColumna(x.Groups(2).Value, t) & obre
                If x.Groups(3).Success Then Return x.Value
                ' PostgreSQL passa a minúscules els noms sense cometes: es
                ' citen les columnes de la taula perquè coincideixin
                If m = Motor.PostgreSql AndAlso t IsNot Nothing Then
                    Dim f As CampoBBDD = t.Fields.Find(
                        Function(c) String.Equals(c.Nombre, x.Value, StringComparison.OrdinalIgnoreCase))
                    If f IsNot Nothing Then Return obre & f.Nombre & obre
                End If
                Return x.Value
            End Function)
    End Function

    Private Function NomColumna(nom As String, t As TablaBBDD) As String
        If t Is Nothing Then Return nom
        Dim f As CampoBBDD = t.Fields.Find(Function(c) String.Equals(c.Nombre, nom, StringComparison.OrdinalIgnoreCase))
        Return If(f IsNot Nothing, f.Nombre, nom)
    End Function

    Friend Function TeExpressions(p As ProyectoBBDD) As Boolean
        For Each t As TablaBBDD In p.Taules
            For Each f As CampoBBDD In t.Fields
                If f.EsCalculado OrElse Not String.IsNullOrWhiteSpace(f.CheckExpression) Then Return True
            Next
        Next
        Return False
    End Function

    Friend Function UnaLinia(s As String) As String
        Return If(s, "").Replace(vbCrLf, " ").Replace(vbCr, " ").Replace(vbLf, " ")
    End Function

End Module
