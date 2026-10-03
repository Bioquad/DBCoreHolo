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
Imports System.Drawing
Imports System.Drawing.Drawing2D

' =======================================================================
' FrmFieldEditor.vb  —  DB-Core Holographic
' Editor de camp de taula (pestanyes: Basic / Avançat / Metadades)
' Extret de FrmDesigner.vb
' =======================================================================
Public Class FrmFieldEditor
    Inherits HoloForm

    Public Property ResultCamp As CampoBBDD

    Private ReadOnly _taula As TablaBBDD
    Private ReadOnly _editant As CampoBBDD
    Private _loading As Boolean = True

    Private _tabs As TabControl
    Private _txtNom As TextBox
    Private _cboTipus As ComboBox
    Private _txtLong As TextBox
    Private _chkMax As CheckBox
    Private _txtPrec As TextBox
    Private _txtEsc As TextBox
    Private _chkPK As CheckBox
    Private _chkFK As CheckBox
    Private _chkNN As CheckBox
    Private _chkUQ As CheckBox
    Private _chkIdx As CheckBox
    Private _chkId As CheckBox
    Private _txtSeed As TextBox
    Private _txtInc As TextBox
    Private _cboDefault As ComboBox
    Private _txtAlias As TextBox
    Private _txtMascara As TextBox
    Private _txtCheck As TextBox
    Private _cboCollation As ComboBox
    Private _cboMask As ComboBox
    Private _txtMPref As TextBox
    Private _txtMPad As TextBox
    Private _txtMSuf As TextBox
    Private _chkCalc As CheckBox
    Private _txtFormula As TextBox
    Private _chkPersisted As CheckBox
    Private _chkRG As CheckBox
    Private _chkFS As CheckBox
    Private _txtDesc As TextBox
    Private _txtComentari As TextBox
    Private _txtCaption As TextBox
    Private _cboFormat As ComboBox
    Private _lblErr As Label

    Private Const FW As Integer = 560
    Private Const TW As Integer = 526

    Public Sub New(camp As CampoBBDD, taula As TablaBBDD)
        _taula = taula
        _editant = camp
        Me.Text = If(camp Is Nothing, Locale.Str("CAMP_NOU") & taula.Nombre, Locale.Str("CAMP_EDITAR") & camp.Nombre)
        Me.ClientSize = New Size(FW, 580)
        Me.StartPosition = FormStartPosition.CenterParent
        ConstruirUI()
        If camp IsNot Nothing Then CarregarCamp(camp)
        _loading = False
        ActualitzarVis()
    End Sub

    Private Sub ConstruirUI()
        ' ── Panell inferior botons (ancla a baix) ────────────────────────
        Dim pnlBtns As New Panel()
        pnlBtns.BackColor = AppStyle.ColFonsMig
        pnlBtns.Dock = DockStyle.Bottom
        pnlBtns.Height = 46
        AddHandler pnlBtns.Paint, Sub(s2 As Object, ev As PaintEventArgs)
            Using pen As New Pen(Color.FromArgb(40, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B), 1)
                ev.Graphics.DrawLine(pen, 0, 0, pnlBtns.Width, 0)
            End Using
        End Sub

        Dim btnOK As Button = AppStyle.CrearBotoAccio(Locale.Str("DLG_BTN_OK"))
        btnOK.Location = New Point(8, 9)
        btnOK.Width = 130
        AddHandler btnOK.Click, AddressOf BtnOK_Click
        pnlBtns.Controls.Add(btnOK)

        Dim btnCancel As Button = AppStyle.CrearBotoAccio(Locale.Str("DLG_BTN_CANCEL"))
        btnCancel.Location = New Point(146, 9)
        btnCancel.Width = 120
        btnCancel.ForeColor = AppStyle.ColPerill
        btnCancel.DialogResult = DialogResult.Cancel
        pnlBtns.Controls.Add(btnCancel)
        Me.CancelButton = btnCancel

        Me.Controls.Add(pnlBtns)

        ' ── Label error (ancla a baix, sobre els botons) ──────────────────
        _lblErr = New Label()
        _lblErr.Font = AppStyle.FntMoltPetit
        _lblErr.ForeColor = AppStyle.ColPerill
        _lblErr.BackColor = Color.FromArgb(20, 255, 0, 0)
        _lblErr.Dock = DockStyle.Bottom
        _lblErr.Height = 18
        _lblErr.TextAlign = ContentAlignment.MiddleLeft
        _lblErr.Padding = New Padding(8, 0, 0, 0)
        _lblErr.Visible = False
        Me.Controls.Add(_lblErr)

        ' ── TabControl (omplir la resta) ──────────────────────────────────
        _tabs = New TabControl()
        _tabs.Dock = DockStyle.Fill
        _tabs.Font = AppStyle.FntPetit
        _tabs.Padding = New Drawing.Point(8, 4)

        Dim tB As New TabPage(Locale.Str("FLD_TAB_BASIC"))
        Dim tA As New TabPage(Locale.Str("FLD_TAB_AVANCAT"))
        Dim tM As New TabPage(Locale.Str("FLD_TAB_METADADES"))
        For Each tp As TabPage In {tB, tA, tM}
            tp.BackColor = AppStyle.ColFonsMig
            tp.ForeColor = AppStyle.ColAccent
        Next
        _tabs.Controls.Add(tB)
        _tabs.Controls.Add(tA)
        _tabs.Controls.Add(tM)
        Me.Controls.Add(_tabs)

        ' ════════════════ BASIC ════════════════════════════════════════
        Dim y As Integer = 8
        FL(tB, Locale.Str("FLD_NOM"), 8, y)
        _txtNom = FT(tB, 8, y + 15, TW - 20)
        y += 40

        FL(tB, Locale.Str("FLD_TIPUS"), 8, y)
        _cboTipus = FC(tB, 8, y + 15, TW - 20)
        OmplirTipus(_cboTipus)
        AddHandler _cboTipus.SelectedIndexChanged, AddressOf OnTipusChanged
        y += 40

        FL(tB, Locale.Str("FLD_LONGITUD"), 8, y)
        _txtLong = FT(tB, 8, y + 15, 70)
        _chkMax = FK(tB, "MAX", 84, y + 17)
        FL(tB, Locale.Str("FLD_PRECISIO"), 200, y)
        _txtPrec = FT(tB, 200, y + 15, 65)
        FL(tB, Locale.Str("FLD_ESCALA"), 280, y)
        _txtEsc = FT(tB, 280, y + 15, 65)
        y += 40

        _chkPK  = FK(tB, "PK",       8,   y)
        _chkFK  = FK(tB, "FK",       62,  y)
        _chkNN  = FK(tB, "NOT NULL", 116, y)
        _chkUQ  = FK(tB, "UNIQUE",   215, y)
        _chkIdx = FK(tB, "INDEX",    292, y)
        AddHandler _chkPK.CheckedChanged, AddressOf OnPKChanged
        y += 28

        _chkId = FK(tB, Locale.Str("FLD_IDENTITY"), 8, y)
        FL(tB, "SEED", 120, y + 2)
        _txtSeed = FT(tB, 120, y, 50)
        _txtSeed.Text = "1"
        FL(tB, "INC.", 184, y + 2)
        _txtInc = FT(tB, 184, y, 50)
        _txtInc.Text = "1"
        AddHandler _chkId.CheckedChanged, AddressOf OnIdChanged
        y += 30

        FL(tB, Locale.Str("FLD_DEFAULT"), 8, y)
        _cboDefault = FC(tB, 8, y + 15, TW - 20)
        _cboDefault.DropDownStyle = ComboBoxStyle.DropDown
        _cboDefault.Items.Add("")
        _cboDefault.Items.Add("NULL")
        _cboDefault.Items.Add("0")
        _cboDefault.Items.Add("GETDATE()")
        _cboDefault.Items.Add("GETUTCDATE()")
        _cboDefault.Items.Add("NEWID()")
        _cboDefault.Items.Add("1")
        y += 40

        FL(tB, Locale.Str("FLD_ALIAS"), 8, y)
        _txtAlias = FT(tB, 8, y + 15, 240)
        FL(tB, Locale.Str("FLD_MASCARA"), 256, y)
        _txtMascara = FT(tB, 256, y + 15, TW - 268)
        y += 40

        FL(tB, Locale.Str("FLD_FORMAT"), 8, y)
        _cboFormat = FC(tB, 8, y + 15, TW - 20)
        _cboFormat.DropDownStyle = ComboBoxStyle.DropDown
        _cboFormat.Items.Add("")
        _cboFormat.Items.Add("#,##0")
        _cboFormat.Items.Add("#,##0.00")
        _cboFormat.Items.Add("0.00%")
        _cboFormat.Items.Add("dd/MM/yyyy")
        _cboFormat.Items.Add("dd/MM/yyyy HH:mm")
        _cboFormat.Items.Add("HH:mm:ss")

        ' ════════════════ AVANCAT ═══════════════════════════════════════
        y = 8
        FL(tA, Locale.Str("FLD_CHECK"), 8, y)
        _txtCheck = FT(tA, 8, y + 15, TW - 20)
        y += 40

        FL(tA, Locale.Str("FLD_COLLACIO"), 8, y)
        _cboCollation = FC(tA, 8, y + 15, TW - 20)
        _cboCollation.Items.Add("DATABASE_DEFAULT")
        _cboCollation.Items.Add("SQL_Latin1_General_CP1_CI_AS")
        _cboCollation.Items.Add("SQL_Latin1_General_CP1_CS_AS")
        _cboCollation.Items.Add("Latin1_General_CI_AS")
        _cboCollation.Items.Add("Modern_Spanish_CI_AS")
        _cboCollation.Items.Add("French_CI_AS")
        _cboCollation.Items.Add("Chinese_PRC_CI_AS")
        _cboCollation.Items.Add("Japanese_CI_AS")
        _cboCollation.Items.Add("Arabic_CI_AS")
        _cboCollation.Items.Add("Cyrillic_General_CI_AS")
        _cboCollation.SelectedIndex = 0
        y += 40

        FL(tA, Locale.Str("FLD_DDM"), 8, y)
        _cboMask = FC(tA, 8, y + 15, TW - 20)
        _cboMask.Items.Add("none")
        _cboMask.Items.Add("default()")
        _cboMask.Items.Add("partial(prefix,pad,suffix)")
        _cboMask.Items.Add("email()")
        _cboMask.Items.Add("random(low,high)")
        _cboMask.SelectedIndex = 0
        AddHandler _cboMask.SelectedIndexChanged, AddressOf OnMaskChanged
        y += 40

        FL(tA, "PREFIX", 8, y)
        _txtMPref = FT(tA, 8, y + 15, 55)
        _txtMPref.Text = "0"
        FL(tA, "PADDING", 76, y)
        _txtMPad = FT(tA, 76, y + 15, 100)
        _txtMPad.Text = "XXXX"
        FL(tA, "SUFFIX", 190, y)
        _txtMSuf = FT(tA, 190, y + 15, 55)
        _txtMSuf.Text = "4"
        y += 40

        _chkCalc = FK(tA, Locale.Str("FLD_CALC"), 8, y)
        AddHandler _chkCalc.CheckedChanged, AddressOf OnCalcChanged
        y += 24
        FL(tA, "FORMULA", 8, y)
        _txtFormula = FT(tA, 8, y + 15, TW - 20)
        y += 40
        _chkPersisted = FK(tA, "PERSISTED", 8, y)
        y += 24
        _chkRG = FK(tA, Locale.Str("FLD_ROWGUID"), 8, y)
        y += 24
        _chkFS = FK(tA, Locale.Str("FLD_FILESTREAM"), 8, y)

        ' ════════════════ METADADES ══════════════════════════════════════
        y = 8
        FL(tM, Locale.Str("FLD_CAPTION"), 8, y)
        _txtCaption = FT(tM, 8, y + 15, TW - 20)
        y += 40

        ' FORMAT DE VISUALITZACIO duplicat a METADADES per comoditat
        FL(tM, Locale.Str("FLD_FORMAT_INFO"), 8, y)
        y += 20

        ' Separador
        Dim sepM As New Label()
        sepM.Location = New Point(8, y)
        sepM.Size = New Size(TW - 16, 1)
        sepM.BackColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        tM.Controls.Add(sepM)
        y += 8

        FL(tM, Locale.Str("FLD_DESCRIPCIO"), 8, y)
        y += 15
        _txtDesc = New TextBox()
        _txtDesc.Location = New Point(8, y)
        _txtDesc.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Top
        _txtDesc.Size = New Size(TW - 16, 60)
        _txtDesc.Multiline = True
        _txtDesc.ScrollBars = ScrollBars.Vertical
        _txtDesc.BackColor = AppStyle.ColFonsInput
        _txtDesc.ForeColor = AppStyle.ColTextPrinc
        _txtDesc.Font = AppStyle.FntNormal
        _txtDesc.BorderStyle = BorderStyle.FixedSingle
        tM.Controls.Add(_txtDesc)
        y += 70

        ' Separador
        Dim sepM2 As New Label()
        sepM2.Location = New Point(8, y)
        sepM2.Size = New Size(TW - 16, 1)
        sepM2.BackColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        tM.Controls.Add(sepM2)
        y += 8

        FL(tM, Locale.Str("FLD_COMENTARI"), 8, y)
        y += 15
        _txtComentari = New TextBox()
        _txtComentari.Location = New Point(8, y)
        _txtComentari.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or
                               AnchorStyles.Top Or AnchorStyles.Bottom
        _txtComentari.Size = New Size(TW - 16, 220)
        _txtComentari.Multiline = True
        _txtComentari.ScrollBars = ScrollBars.Vertical
        _txtComentari.BackColor = Color.FromArgb(8, 20, 8)
        _txtComentari.ForeColor = Color.FromArgb(120, 200, 80)
        _txtComentari.Font = AppStyle.FntNormal
        _txtComentari.BorderStyle = BorderStyle.FixedSingle
        tM.Controls.Add(_txtComentari)
    End Sub

    Private Sub FL(parent As Control, text As String, x As Integer, y As Integer)
        Dim l As New Label()
        l.Text = text
        l.Location = New Point(x, y)
        l.AutoSize = True
        l.Font = AppStyle.FntMoltPetit
        l.ForeColor = Color.FromArgb(160, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        l.BackColor = Color.Transparent
        parent.Controls.Add(l)
    End Sub

    Private Function FT(parent As Control, x As Integer, y As Integer, w As Integer) As TextBox
        Dim t As TextBox = AppStyle.CrearTextBox()
        t.Location = New Point(x, y)
        t.Size = New Size(w, 20)
        parent.Controls.Add(t)
        Return t
    End Function

    Private Function FC(parent As Control, x As Integer, y As Integer, w As Integer) As ComboBox
        Dim c As ComboBox = AppStyle.CrearComboBox()
        c.Location = New Point(x, y)
        c.Size = New Size(w, 22)
        parent.Controls.Add(c)
        Return c
    End Function

    Private Function FK(parent As Control, text As String, x As Integer, y As Integer) As CheckBox
        Dim c As CheckBox = AppStyle.CrearCheckBox(text)
        c.Location = New Point(x, y)
        c.AutoSize = True
        parent.Controls.Add(c)
        Return c
    End Function

    Private Sub OmplirTipus(cbo As ComboBox)
        Dim items() As String = {
            "-- ENTERS --", "BIT", "TINYINT", "SMALLINT", "INT", "BIGINT",
            "-- DECIMALS --", "DECIMAL(p,s)", "NUMERIC(p,s)", "MONEY", "SMALLMONEY",
            "-- DECIMALS APROX. --", "FLOAT", "REAL",
            "-- ALFANUMERIC --", "CHAR(n)", "VARCHAR(n)", "VARCHAR(MAX)", "TEXT",
            "-- UNICODE --", "NCHAR(n)", "NVARCHAR(n)", "NVARCHAR(MAX)", "NTEXT",
            "-- BINARI --", "BINARY(n)", "VARBINARY(n)", "VARBINARY(MAX)", "IMAGE",
            "-- DATA / HORA --", "DATE", "TIME", "DATETIME", "DATETIME2",
            "SMALLDATETIME", "DATETIMEOFFSET", "TIMESTAMP",
            "-- ESPECIALS --", "UNIQUEIDENTIFIER", "XML", "GEOGRAPHY",
            "GEOMETRY", "HIERARCHYID", "SQL_VARIANT", "ROWVERSION"
        }
        For Each s As String In items
            cbo.Items.Add(s)
        Next
        For i As Integer = 0 To cbo.Items.Count - 1
            If cbo.Items(i).ToString() = "INT" Then
                cbo.SelectedIndex = i
                Exit For
            End If
        Next
    End Sub

    Private Sub CarregarCamp(f As CampoBBDD)
        _txtNom.Text = f.Nombre
        SelTipus(f.TipoDato)
        _txtLong.Text = f.Longitud.ToString()
        _chkMax.Checked = f.LongitudMax
        _txtPrec.Text = f.Precision.ToString()
        _txtEsc.Text = f.Escala.ToString()
        _chkPK.Checked = f.EsPK
        _chkFK.Checked = f.EsFK
        _chkNN.Checked = f.NotNull
        _chkUQ.Checked = f.EsUnique
        _chkIdx.Checked = f.TieneIndex
        _chkId.Checked = f.EsIdentity
        _txtSeed.Text = f.IdentitySeed.ToString()
        _txtInc.Text = f.IdentityIncrement.ToString()
        _cboDefault.Text = f.DefaultValue
        _txtAlias.Text = f.NomAlias
        _txtMascara.Text = f.Mascara
        _txtCheck.Text = f.CheckExpression
        ' Una col·lació importada que no és a la llista s'hi afegeix per no perdre-la
        If Not String.IsNullOrWhiteSpace(f.Collation) AndAlso Not _cboCollation.Items.Contains(f.Collation) Then
            _cboCollation.Items.Add(f.Collation)
        End If
        SelIdx(_cboCollation, f.Collation)
        _cboMask.SelectedIndex = CInt(f.DataMask)
        _txtMPref.Text = f.MaskPrefix.ToString()
        _txtMPad.Text = f.MaskPadding
        _txtMSuf.Text = f.MaskSuffix.ToString()
        _chkCalc.Checked = f.EsCalculado
        _txtFormula.Text = f.FormulaCalculo
        _chkPersisted.Checked = f.EsPersistido
        _chkRG.Checked = f.EsRowGuid
        _chkFS.Checked = f.EsFileStream
        _txtDesc.Text = f.Descripcion
        _txtComentari.Text = f.Comentari
        _txtCaption.Text = f.Caption
        _cboFormat.Text = f.FormatDisplay
    End Sub

    Private Sub SelTipus(dt As DataType)
        Dim d As String = ""
        Select Case dt
            Case DataType.Bit : d = "BIT"
            Case DataType.TinyInt : d = "TINYINT"
            Case DataType.SmallInt : d = "SMALLINT"
            Case DataType.DbInt : d = "INT"
            Case DataType.BigInt : d = "BIGINT"
            Case DataType.DbDecimal : d = "DECIMAL(p,s)"
            Case DataType.DbNumeric : d = "NUMERIC(p,s)"
            Case DataType.Money : d = "MONEY"
            Case DataType.SmallMoney : d = "SMALLMONEY"
            Case DataType.DbFloat : d = "FLOAT"
            Case DataType.DbReal : d = "REAL"
            Case DataType.DbChar : d = "CHAR(n)"
            Case DataType.VarChar : d = "VARCHAR(n)"
            Case DataType.VarCharMax : d = "VARCHAR(MAX)"
            Case DataType.DbText : d = "TEXT"
            Case DataType.NChar : d = "NCHAR(n)"
            Case DataType.NVarChar : d = "NVARCHAR(n)"
            Case DataType.NVarCharMax : d = "NVARCHAR(MAX)"
            Case DataType.NText : d = "NTEXT"
            Case DataType.DbBinary : d = "BINARY(n)"
            Case DataType.VarBinary : d = "VARBINARY(n)"
            Case DataType.VarBinaryMax : d = "VARBINARY(MAX)"
            Case DataType.DbImage : d = "IMAGE"
            Case DataType.DateOnly : d = "DATE"
            Case DataType.TimeOnly : d = "TIME"
            Case DataType.DbDateTime : d = "DATETIME"
            Case DataType.DateTime2 : d = "DATETIME2"
            Case DataType.SmallDateTime : d = "SMALLDATETIME"
            Case DataType.DateTimeOffset : d = "DATETIMEOFFSET"
            Case DataType.DbTimestamp : d = "TIMESTAMP"
            Case DataType.UniqueIdentifier : d = "UNIQUEIDENTIFIER"
            Case DataType.DbXml : d = "XML"
            Case DataType.DbGeography : d = "GEOGRAPHY"
            Case DataType.DbGeometry : d = "GEOMETRY"
            Case DataType.HierarchyId : d = "HIERARCHYID"
            Case DataType.SqlVariant : d = "SQL_VARIANT"
            Case DataType.RowVersion : d = "ROWVERSION"
        End Select
        SelIdx(_cboTipus, d)
    End Sub

    Private Sub SelIdx(cbo As ComboBox, v As String)
        For i As Integer = 0 To cbo.Items.Count - 1
            If cbo.Items(i).ToString() = v Then
                cbo.SelectedIndex = i
                Return
            End If
        Next
    End Sub

    Private Function GetTipus() As DataType
        If _cboTipus.SelectedItem Is Nothing Then Return DataType.DbInt
        Select Case _cboTipus.SelectedItem.ToString()
            Case "BIT" : Return DataType.Bit
            Case "TINYINT" : Return DataType.TinyInt
            Case "SMALLINT" : Return DataType.SmallInt
            Case "INT" : Return DataType.DbInt
            Case "BIGINT" : Return DataType.BigInt
            Case "DECIMAL(p,s)" : Return DataType.DbDecimal
            Case "NUMERIC(p,s)" : Return DataType.DbNumeric
            Case "MONEY" : Return DataType.Money
            Case "SMALLMONEY" : Return DataType.SmallMoney
            Case "FLOAT" : Return DataType.DbFloat
            Case "REAL" : Return DataType.DbReal
            Case "CHAR(n)" : Return DataType.DbChar
            Case "VARCHAR(n)" : Return DataType.VarChar
            Case "VARCHAR(MAX)" : Return DataType.VarCharMax
            Case "TEXT" : Return DataType.DbText
            Case "NCHAR(n)" : Return DataType.NChar
            Case "NVARCHAR(n)" : Return DataType.NVarChar
            Case "NVARCHAR(MAX)" : Return DataType.NVarCharMax
            Case "NTEXT" : Return DataType.NText
            Case "BINARY(n)" : Return DataType.DbBinary
            Case "VARBINARY(n)" : Return DataType.VarBinary
            Case "VARBINARY(MAX)" : Return DataType.VarBinaryMax
            Case "IMAGE" : Return DataType.DbImage
            Case "DATE" : Return DataType.DateOnly
            Case "TIME" : Return DataType.TimeOnly
            Case "DATETIME" : Return DataType.DbDateTime
            Case "DATETIME2" : Return DataType.DateTime2
            Case "SMALLDATETIME" : Return DataType.SmallDateTime
            Case "DATETIMEOFFSET" : Return DataType.DateTimeOffset
            Case "TIMESTAMP" : Return DataType.DbTimestamp
            Case "UNIQUEIDENTIFIER" : Return DataType.UniqueIdentifier
            Case "XML" : Return DataType.DbXml
            Case "GEOGRAPHY" : Return DataType.DbGeography
            Case "GEOMETRY" : Return DataType.DbGeometry
            Case "HIERARCHYID" : Return DataType.HierarchyId
            Case "SQL_VARIANT" : Return DataType.SqlVariant
            Case "ROWVERSION" : Return DataType.RowVersion
            Case Else : Return DataType.DbInt
        End Select
    End Function

    Private Sub ActualitzarVis()
        If _loading Then Return
        Dim dt As DataType = GetTipus()
        Dim needL As Boolean = (dt = DataType.DbChar OrElse dt = DataType.VarChar OrElse
                                dt = DataType.NChar OrElse dt = DataType.NVarChar OrElse
                                dt = DataType.DbBinary OrElse dt = DataType.VarBinary)
        Dim needP As Boolean = (dt = DataType.DbDecimal OrElse dt = DataType.DbNumeric)
        _txtLong.Enabled = needL
        _chkMax.Enabled = needL
        _txtPrec.Enabled = needP
        _txtEsc.Enabled = needP
        _txtSeed.Enabled = _chkId.Checked
        _txtInc.Enabled = _chkId.Checked
        _txtFormula.Enabled = _chkCalc.Checked
        _chkPersisted.Enabled = _chkCalc.Checked
        Dim maskP As Boolean = (_cboMask.SelectedIndex = 2)
        _txtMPref.Enabled = maskP
        _txtMPad.Enabled = maskP
        _txtMSuf.Enabled = maskP
    End Sub

    Private Sub OnTipusChanged(s As Object, e As EventArgs)
        ActualitzarVis()
    End Sub

    Private Sub OnPKChanged(s As Object, e As EventArgs)
        ' Un camp pot ser PK i FK alhora (p.ex. PK composta d'una taula intermèdia)
        If _chkPK.Checked Then _chkNN.Checked = True
    End Sub

    Private Sub OnIdChanged(s As Object, e As EventArgs)
        ActualitzarVis()
    End Sub

    Private Sub OnCalcChanged(s As Object, e As EventArgs)
        ActualitzarVis()
    End Sub

    Private Sub OnMaskChanged(s As Object, e As EventArgs)
        ActualitzarVis()
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        If _tabs IsNot Nothing Then
            AppStyle.AplicarEstilTabControl(_tabs)
            For Each tp As TabPage In _tabs.TabPages
                tp.BackColor = AppStyle.ColFonsMig
                tp.ForeColor = AppStyle.ColAccent
                AppStyle.AplicarEstilRecursiu(tp)
            Next
        End If
        ' Restaurar colors del comentari (AplicarEstilRecursiu els sobreescriu)
        If _txtComentari IsNot Nothing Then
            _txtComentari.BackColor = Color.FromArgb(8, 20, 8)
            _txtComentari.ForeColor = Color.FromArgb(120, 200, 80)
        End If
    End Sub

    Private Sub BtnOK_Click(s As Object, e As EventArgs)
        _lblErr.Visible = False
        If String.IsNullOrWhiteSpace(_txtNom.Text) Then
            _lblErr.Text = Locale.Str("CAMP_NOM_OBLIG")
            _lblErr.Visible = True
            Return
        End If

        Dim f As CampoBBDD
        If _editant IsNot Nothing Then
            f = _editant.Clone()
        Else
            f = New CampoBBDD()
        End If

        f.Nombre = _txtNom.Text.Trim().ToUpper()
        f.TipoDato = GetTipus()

        Dim lv As Integer = 0
        Integer.TryParse(_txtLong.Text, lv)
        f.Longitud = lv
        f.LongitudMax = _chkMax.Checked

        Dim pv As Integer = 18
        Integer.TryParse(_txtPrec.Text, pv)
        f.Precision = pv

        Dim ev As Integer = 0
        Integer.TryParse(_txtEsc.Text, ev)
        f.Escala = ev

        f.EsPK = _chkPK.Checked
        f.EsFK = _chkFK.Checked
        f.NotNull = _chkNN.Checked
        f.EsUnique = _chkUQ.Checked
        f.TieneIndex = _chkIdx.Checked
        f.EsIdentity = _chkId.Checked

        Dim sv As Integer = 1
        Integer.TryParse(_txtSeed.Text, sv)
        f.IdentitySeed = sv

        Dim iv As Integer = 1
        Integer.TryParse(_txtInc.Text, iv)
        f.IdentityIncrement = iv

        f.DefaultValue = _cboDefault.Text
        f.NomAlias = _txtAlias.Text
        f.Mascara = _txtMascara.Text
        f.CheckExpression = _txtCheck.Text

        If _cboCollation.SelectedItem IsNot Nothing Then
            f.Collation = _cboCollation.SelectedItem.ToString()
        End If

        f.DataMask = CType(_cboMask.SelectedIndex, DataMaskFunction)

        Dim mpv As Integer = 0
        Integer.TryParse(_txtMPref.Text, mpv)
        f.MaskPrefix = mpv
        f.MaskPadding = _txtMPad.Text

        Dim msv As Integer = 0
        Integer.TryParse(_txtMSuf.Text, msv)
        f.MaskSuffix = msv

        f.EsCalculado = _chkCalc.Checked
        f.FormulaCalculo = _txtFormula.Text
        f.EsPersistido = _chkPersisted.Checked
        f.EsRowGuid = _chkRG.Checked
        f.EsFileStream = _chkFS.Checked
        f.Descripcion = _txtDesc.Text
        f.Comentari = _txtComentari.Text
        f.Caption = _txtCaption.Text
        f.FormatDisplay = _cboFormat.Text

        ResultCamp = f
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

End Class
