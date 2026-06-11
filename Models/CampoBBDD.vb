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

Public Class CampoBBDD

    ' Identificacio
    Public Property Nombre As String = "NOU_CAMP"
    Public Property NomAlias As String = ""
    Public Property Descripcion As String = ""   ' exportat al DDL (sp_addextendedproperty / COMMENT)
    Public Property Comentari As String = ""      ' nota interna — guardat al .hdb, no exportat al DDL
    Public Property Caption As String = ""
    Public Property FormatDisplay As String = ""
    Public Property Mascara As String = ""

    ' Tipus de dada
    Public Property TipoDato As DataType = DataType.DbInt
    Public Property Longitud As Integer = 0
    Public Property LongitudMax As Boolean = False
    Public Property Precision As Integer = 18
    Public Property Escala As Integer = 0

    ' Flags principals
    Private _esPK As Boolean = False
    Public Property EsPK As Boolean
        Get
            Return _esPK
        End Get
        Set(value As Boolean)
            _esPK = value
            If value Then
                NotNull = True
                _esFK = False
            End If
        End Set
    End Property

    Private _esFK As Boolean = False
    Public Property EsFK As Boolean
        Get
            Return _esFK
        End Get
        Set(value As Boolean)
            If value AndAlso _esPK Then Return
            _esFK = value
        End Set
    End Property

    Public Property NotNull As Boolean = False
    Public Property EsUnique As Boolean = False
    Public Property TieneIndex As Boolean = False

    ' Identity
    Public Property EsIdentity As Boolean = False
    Public Property IdentitySeed As Integer = 1
    Public Property IdentityIncrement As Integer = 1

    ' Flags especials
    Public Property EsRowGuid As Boolean = False
    Public Property EsFileStream As Boolean = False

    ' Columna calculada
    Public Property EsCalculado As Boolean = False
    Public Property FormulaCalculo As String = ""
    Public Property EsPersistido As Boolean = False

    ' Default i Check
    Public Property DefaultValue As String = ""
    Public Property CheckExpression As String = ""

    ' Col.lacio
    Public Property Collation As String = "DATABASE_DEFAULT"

    ' Dynamic Data Masking
    Public Property DataMask As DataMaskFunction = DataMaskFunction.NoMask
    Public Property MaskPrefix As Integer = 0
    Public Property MaskPadding As String = "XXXX"
    Public Property MaskSuffix As Integer = 0

    ' Etiqueta de tipus per mostrar
    Public ReadOnly Property EtiquetaTipus As String
        Get
            Select Case TipoDato
                Case DataType.Bit
                    Return "BIT"
                Case DataType.TinyInt
                    Return "TINYINT"
                Case DataType.SmallInt
                    Return "SMALLINT"
                Case DataType.DbInt
                    Return "INT"
                Case DataType.BigInt
                    Return "BIGINT"
                Case DataType.DbDecimal
                    Return "DECIMAL(" & Precision & "," & Escala & ")"
                Case DataType.DbNumeric
                    Return "NUMERIC(" & Precision & "," & Escala & ")"
                Case DataType.Money
                    Return "MONEY"
                Case DataType.SmallMoney
                    Return "SMALLMONEY"
                Case DataType.DbFloat
                    Return "FLOAT"
                Case DataType.DbReal
                    Return "REAL"
                Case DataType.DbChar
                    Return "CHAR(" & Longitud & ")"
                Case DataType.VarChar
                    If LongitudMax Then Return "VARCHAR(MAX)" Else Return "VARCHAR(" & Longitud & ")"
                Case DataType.VarCharMax
                    Return "VARCHAR(MAX)"
                Case DataType.DbText
                    Return "TEXT"
                Case DataType.NChar
                    Return "NCHAR(" & Longitud & ")"
                Case DataType.NVarChar
                    If LongitudMax Then Return "NVARCHAR(MAX)" Else Return "NVARCHAR(" & Longitud & ")"
                Case DataType.NVarCharMax
                    Return "NVARCHAR(MAX)"
                Case DataType.NText
                    Return "NTEXT"
                Case DataType.DbBinary
                    Return "BINARY(" & Longitud & ")"
                Case DataType.VarBinary
                    If LongitudMax Then Return "VARBINARY(MAX)" Else Return "VARBINARY(" & Longitud & ")"
                Case DataType.VarBinaryMax
                    Return "VARBINARY(MAX)"
                Case DataType.DbImage
                    Return "IMAGE"
                Case DataType.DateOnly
                    Return "DATE"
                Case DataType.TimeOnly
                    Return "TIME"
                Case DataType.DbDateTime
                    Return "DATETIME"
                Case DataType.DateTime2
                    Return "DATETIME2"
                Case DataType.SmallDateTime
                    Return "SMALLDATETIME"
                Case DataType.DateTimeOffset
                    Return "DATETIMEOFFSET"
                Case DataType.DbTimestamp
                    Return "TIMESTAMP"
                Case DataType.UniqueIdentifier
                    Return "UNIQUEIDENTIFIER"
                Case DataType.DbXml
                    Return "XML"
                Case DataType.DbGeography
                    Return "GEOGRAPHY"
                Case DataType.DbGeometry
                    Return "GEOMETRY"
                Case DataType.HierarchyId
                    Return "HIERARCHYID"
                Case DataType.SqlVariant
                    Return "SQL_VARIANT"
                Case DataType.RowVersion
                    Return "ROWVERSION"
                Case Else
                    Return "INT"
            End Select
        End Get
    End Property

    Public ReadOnly Property NecessitaLongitud As Boolean
        Get
            Return TipoDato = DataType.DbChar OrElse
                   TipoDato = DataType.VarChar OrElse
                   TipoDato = DataType.NChar OrElse
                   TipoDato = DataType.NVarChar OrElse
                   TipoDato = DataType.DbBinary OrElse
                   TipoDato = DataType.VarBinary
        End Get
    End Property

    Public ReadOnly Property NecessitaPrecEsc As Boolean
        Get
            Return TipoDato = DataType.DbDecimal OrElse TipoDato = DataType.DbNumeric
        End Get
    End Property

    Public Function Clone() As CampoBBDD
        Return DirectCast(Me.MemberwiseClone(), CampoBBDD)
    End Function

    Public Overrides Function ToString() As String
        Dim s As String = Nombre & " [" & EtiquetaTipus & "]"
        If EsPK Then s = s & " PK"
        If EsFK Then s = s & " FK"
        Return s
    End Function

End Class

