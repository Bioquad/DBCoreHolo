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


Public Enum DataType
    Bit = 0
    TinyInt = 1
    SmallInt = 2
    DbInt = 3
    BigInt = 4
    DbDecimal = 5
    DbNumeric = 6
    Money = 7
    SmallMoney = 8
    DbFloat = 9
    DbReal = 10
    DbChar = 11
    VarChar = 12
    VarCharMax = 13
    DbText = 14
    NChar = 15
    NVarChar = 16
    NVarCharMax = 17
    NText = 18
    DbBinary = 19
    VarBinary = 20
    VarBinaryMax = 21
    DbImage = 22
    DateOnly = 23
    TimeOnly = 24
    DbDateTime = 25
    DateTime2 = 26
    SmallDateTime = 27
    DateTimeOffset = 28
    DbTimestamp = 29
    UniqueIdentifier = 30
    DbXml = 31
    DbGeography = 32
    DbGeometry = 33
    HierarchyId = 34
    SqlVariant = 35
    RowVersion = 36
End Enum

Public Enum CardinalityType
    OneToOne = 0
    OneToMany = 1
    ManyToOne = 2
    ManyToMany = 3
End Enum

Public Enum OnDeleteUpdateAction
    NoAction = 0
    DoCascade = 1
    SetNull = 2
    SetDefault = 3
    DoRestrict = 4
End Enum

Public Enum WithCheckOption
    WithCheck = 0
    WithNoCheck = 1
End Enum

Public Enum DataMaskFunction
    NoMask = 0
    DefaultMask = 1
    MaskPartial = 2
    MaskEmail = 3
    MaskRandom = 4
End Enum

Public Enum GroupColor
    ColorOrange = 0
    ColorYellow = 1
    ColorGreen = 2
    ColorBlue = 3
    ColorRed = 4
    ColorCyan = 5
    ColorMagenta = 6
    ColorWhite = 7
End Enum

