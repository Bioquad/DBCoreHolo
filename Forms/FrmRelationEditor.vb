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
Imports System.Collections.Generic

' =======================================================================
' FrmRelationEditor.vb  —  DB-Core Holographic
' Editor de relació FK entre taules
' Extret de FrmDesigner.vb
' =======================================================================
Public Class FrmRelationEditor
    Inherits HoloForm

    Public Property ResultRelacio As RelacionBBDD

    Private ReadOnly _p As ProyectoBBDD
    Private ReadOnly _editant As RelacionBBDD
    Private _loading As Boolean = True

    Private _cboTO As ComboBox
    Private _cboFK As ComboBox
    Private _cboTD As ComboBox
    Private _cboPK As ComboBox
    Private _cboTipus As ComboBox
    Private _cboOD As ComboBox
    Private _cboOU As ComboBox
    Private _cboWC As ComboBox
    Private _txtNom As TextBox
    Private _txtDesc As TextBox
    Private _txtComentari As TextBox
    Private _chkNFR As CheckBox
    Private _chkDis As CheckBox
    Private _chkIdx As CheckBox
    Private _lblErr As Label

    Public Sub New(p As ProyectoBBDD,
                   Optional editant As RelacionBBDD = Nothing,
                   Optional fromId As Integer = -1)
        _p = p
        _editant = editant
        Me.Text = If(editant Is Nothing, Locale.Str("BTN_NOVA_RELACIO"), Locale.Str("BTN_EDITAR") & " " & Locale.Str("BTN_NOVA_RELACIO"))
        Me.ClientSize = New Size(510, 620)
        Me.StartPosition = FormStartPosition.CenterParent
        ConstruirUI()

        ' Omplir llista de taules (sempre, tots els casos)
        _loading = True
        For Each t As TablaBBDD In p.Taules
            _cboTO.Items.Add(t)
            _cboTD.Items.Add(t)
        Next
        _loading = False

        If editant IsNot Nothing Then
            ' CAS 1: Editar relació existent
            CarregarRelacio(editant)
        ElseIf fromId >= 0 Then
            ' CAS 2: Nova relació des d'una taula seleccionada
            For i As Integer = 0 To _cboTO.Items.Count - 1
                If DirectCast(_cboTO.Items(i), TablaBBDD).Id = fromId Then
                    _loading = True
                    _cboTO.SelectedIndex = i
                    _loading = False
                    OmplirCampsFK()
                    Exit For
                End If
            Next
        Else
            ' CAS 3: Nova relació sense cap taula seleccionada
            _cboTO.SelectedIndex = -1
            _cboTD.SelectedIndex = -1
        End If
    End Sub

    Private Sub ConstruirUI()
        AppStyle.AplicarEstilForm(Me)

        ' ── Panell botons (ancla a baix) ─────────────────────────────────
        Dim pnlBtns As New Panel()
        pnlBtns.BackColor = AppStyle.ColFonsMig
        pnlBtns.Dock = DockStyle.Bottom
        pnlBtns.Height = 46
        AddHandler pnlBtns.Paint, Sub(s As Object, ev As PaintEventArgs)
            Using pen As New Pen(Color.FromArgb(40, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B), 1)
                ev.Graphics.DrawLine(pen, 0, 0, pnlBtns.Width, 0)
            End Using
        End Sub

        Dim btnOK As Button = AppStyle.CrearBotoAccio(Locale.Str("DLG_BTN_OK"))
        btnOK.Location = New Point(8, 9)
        btnOK.Width = 130
        AddHandler btnOK.Click, AddressOf BtnOK_Click
        pnlBtns.Controls.Add(btnOK)

        Dim btnHeredar As Button = AppStyle.CrearBotoAccio(Locale.Str("BTN_HEREDAR_CAMP"))
        btnHeredar.Location = New Point(146, 9)
        btnHeredar.Width = 160
        btnHeredar.ForeColor = AppStyle.ColAccentSec
        AddHandler btnHeredar.Click, AddressOf BtnHeredarCamp_Click
        pnlBtns.Controls.Add(btnHeredar)

        Dim btnCancel As Button = AppStyle.CrearBotoAccio(Locale.Str("DLG_BTN_CANCEL"))
        btnCancel.Location = New Point(314, 9)
        btnCancel.Width = 130
        btnCancel.ForeColor = AppStyle.ColPerill
        btnCancel.DialogResult = DialogResult.Cancel
        pnlBtns.Controls.Add(btnCancel)
        Me.CancelButton = btnCancel
        Me.Controls.Add(pnlBtns)

        ' ── Label error (sobre els botons) ───────────────────────────────
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

        ' ── Àrea de contingut (scroll) ───────────────────────────────────
        Dim pnlScroll As New Panel()
        pnlScroll.Dock = DockStyle.Fill
        pnlScroll.BackColor = AppStyle.ColFons
        pnlScroll.AutoScroll = True
        Me.Controls.Add(pnlScroll)

        ' ── Contingut interior (posició absoluta dins el panel de scroll) ─
        Dim pnlInner As New Panel()
        pnlInner.BackColor = AppStyle.ColFons
        pnlInner.Width = 488
        pnlInner.Height = 460
        pnlScroll.Controls.Add(pnlInner)

        Dim y As Integer = 10

        ' ── Fila 1: Taula filla (té la FK) + Camp FK ─────────────────────
        AddLbl(pnlInner, Locale.Str("REL_TAULA_FILLA"), 10, y)
        AddLbl(pnlInner, Locale.Str("REL_CAMP_FK"), 240, y)
        y += 16
        _cboTO = AddCbo(pnlInner, 10, y, 222)
        _cboTO.DisplayMember = "Nombre"
        AddHandler _cboTO.SelectedIndexChanged, AddressOf CboTO_Changed
        _cboFK = AddCbo(pnlInner, 240, y, 220)
        _cboFK.DisplayMember = "Nombre"
        y += 30

        ' ── Fila 2: Taula mare (té la PK referenciada) + Camp PK ─────────
        AddLbl(pnlInner, Locale.Str("REL_TAULA_MARE"), 10, y)
        AddLbl(pnlInner, Locale.Str("REL_CAMP_PK"), 240, y)
        y += 16
        _cboTD = AddCbo(pnlInner, 10, y, 222)
        _cboTD.DisplayMember = "Nombre"
        AddHandler _cboTD.SelectedIndexChanged, AddressOf CboTD_Changed
        _cboPK = AddCbo(pnlInner, 240, y, 220)
        _cboPK.DisplayMember = "Nombre"
        y += 30

        ' ── Separador ────────────────────────────────────────────────────
        Dim sep1 As New Label()
        sep1.Location = New Point(10, y) : sep1.Size = New Size(450, 1)
        sep1.BackColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        pnlInner.Controls.Add(sep1)
        y += 8

        ' ── Fila 3: Tipus + ON DELETE + ON UPDATE ─────────────────────────
        AddLbl(pnlInner, Locale.Str("REL_CARDINALITAT"), 10, y)
        AddLbl(pnlInner, Locale.Str("REL_ON_DELETE"), 130, y)
        AddLbl(pnlInner, Locale.Str("REL_ON_UPDATE"), 280, y)
        y += 16
        _cboTipus = AddCbo(pnlInner, 10, y, 110)
        For Each v As String In {"1:1", "1:M", "M:1"}
            _cboTipus.Items.Add(v)
        Next
        _cboTipus.SelectedIndex = 1
        _cboOD = AddCbo(pnlInner, 130, y, 140)
        For Each v As String In {"NO ACTION", "CASCADE", "SET NULL", "SET DEFAULT", "RESTRICT"}
            _cboOD.Items.Add(v)
        Next
        _cboOD.SelectedIndex = 0
        _cboOU = AddCbo(pnlInner, 280, y, 180)
        For Each v As String In {"NO ACTION", "CASCADE", "SET NULL", "SET DEFAULT", "RESTRICT"}
            _cboOU.Items.Add(v)
        Next
        _cboOU.SelectedIndex = 0
        y += 30

        ' ── Fila 4: WITH CHECK + NOM CONSTRAINT ──────────────────────────
        AddLbl(pnlInner, Locale.Str("REL_WITH_CHECK"), 10, y)
        AddLbl(pnlInner, Locale.Str("REL_NOM_CONSTRAINT"), 180, y)
        y += 16
        _cboWC = AddCbo(pnlInner, 10, y, 162)
        For Each v As String In {"WITH CHECK", "WITH NOCHECK"}
            _cboWC.Items.Add(v)
        Next
        _cboWC.SelectedIndex = 0
        _txtNom = AddTxt(pnlInner, 180, y, 280)
        y += 30

        ' ── Separador ────────────────────────────────────────────────────
        Dim sep2 As New Label()
        sep2.Location = New Point(10, y) : sep2.Size = New Size(450, 1)
        sep2.BackColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        pnlInner.Controls.Add(sep2)
        y += 8

        ' ── Fila 5: Checkboxes ───────────────────────────────────────────
        _chkNFR = AddChk(pnlInner, Locale.Str("REL_CHK_NFR"), 10, y)
        _chkDis = AddChk(pnlInner, Locale.Str("REL_CHK_DISABLED"), 210, y)
        y += 24
        _chkIdx = AddChk(pnlInner, Locale.Str("REL_CHK_INDEX"), 10, y)
        _chkIdx.Checked = True
        y += 28

        ' ── Fila 6: Descripció ───────────────────────────────────────────
        AddLbl(pnlInner, Locale.Str("REL_DESCRIPCIO"), 10, y)
        y += 16
        _txtDesc = New TextBox()
        _txtDesc.Location = New Point(10, y)
        _txtDesc.Size = New Size(450, 52)
        _txtDesc.Multiline = True
        _txtDesc.ScrollBars = ScrollBars.Vertical
        _txtDesc.BackColor = AppStyle.ColFonsInput
        _txtDesc.ForeColor = AppStyle.ColTextPrinc
        _txtDesc.Font = AppStyle.FntNormal
        _txtDesc.BorderStyle = BorderStyle.FixedSingle
        pnlInner.Controls.Add(_txtDesc)
        y += 60

        ' ── Separador ────────────────────────────────────────────────────
        Dim sep3 As New Label()
        sep3.Location = New Point(10, y) : sep3.Size = New Size(450, 1)
        sep3.BackColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        pnlInner.Controls.Add(sep3)
        y += 8

        ' ── Fila 7: Comentari intern ──────────────────────────────────────
        AddLbl(pnlInner, Locale.Str("REL_COMENTARI"), 10, y)
        y += 16
        _txtComentari = New TextBox()
        _txtComentari.Location = New Point(10, y)
        _txtComentari.Size = New Size(450, 52)
        _txtComentari.Multiline = True
        _txtComentari.ScrollBars = ScrollBars.Vertical
        _txtComentari.BackColor = Color.FromArgb(8, 20, 8)
        _txtComentari.ForeColor = Color.FromArgb(120, 200, 80)
        _txtComentari.Font = AppStyle.FntNormal
        _txtComentari.BorderStyle = BorderStyle.FixedSingle
        pnlInner.Controls.Add(_txtComentari)
        y += 60

        pnlInner.Height = y + 10
    End Sub

    ' ── Helpers d'estil intern ───────────────────────────────────────────
    Private Sub AddLbl(parent As Control, text As String, x As Integer, y As Integer)
        Dim l As New Label()
        l.Text = text
        l.Location = New Point(x, y)
        l.AutoSize = True
        l.Font = AppStyle.FntMoltPetit
        l.ForeColor = Color.FromArgb(170, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        l.BackColor = Color.Transparent
        parent.Controls.Add(l)
    End Sub

    Private Function AddTxt(parent As Control, x As Integer, y As Integer, w As Integer) As TextBox
        Dim t As New TextBox()
        t.Location = New Point(x, y)
        t.Size = New Size(w, 20)
        t.Font = AppStyle.FntNormal
        t.BackColor = AppStyle.ColFonsInput
        t.ForeColor = AppStyle.ColTextPrinc
        t.BorderStyle = BorderStyle.FixedSingle
        parent.Controls.Add(t)
        Return t
    End Function

    Private Function AddCbo(parent As Control, x As Integer, y As Integer, w As Integer) As ComboBox
        Dim c As New ComboBox()
        c.Location = New Point(x, y)
        c.Size = New Size(w, 20)
        c.Font = AppStyle.FntNormal
        c.BackColor = AppStyle.ColFonsInput
        c.ForeColor = AppStyle.ColTextPrinc
        c.FlatStyle = FlatStyle.Flat
        c.DropDownStyle = ComboBoxStyle.DropDownList
        parent.Controls.Add(c)
        Return c
    End Function

    Private Function AddChk(parent As Control, text As String, x As Integer, y As Integer) As CheckBox
        Dim c As New CheckBox()
        c.Text = text
        c.Location = New Point(x, y)
        c.AutoSize = True
        c.Font = AppStyle.FntNormal
        c.ForeColor = AppStyle.ColTextSec
        c.BackColor = Color.Transparent
        parent.Controls.Add(c)
        Return c
    End Function

    ''' <summary>
    ''' Preselecciona el camp FK i la taula mare quan ve del rubber-band drag.
    ''' S'ha de cridar ABANS de ShowDialog.
    ''' </summary>
    ''' <summary>
    ''' Preselecciona el camp origen (qualsevol) com a FK i la taula mare destí.
    ''' El camp destí mostrarà sols les PKs de la taula mare.
    ''' S'ha de cridar ABANS de ShowDialog.
    ''' </summary>
    Public Sub PreselectFK(campFKNom As String, taulaMareId As Integer)
        ' Seleccionar el camp origen al combo FK
        For i As Integer = 0 To _cboFK.Items.Count - 1
            Dim f As CampoBBDD = TryCast(_cboFK.Items(i), CampoBBDD)
            If f IsNot Nothing AndAlso f.Nombre.ToUpper() = campFKNom.ToUpper() Then
                _loading = True
                _cboFK.SelectedIndex = i
                _loading = False
                Exit For
            End If
        Next
        ' Seleccionar la taula mare — OmplirCampsPK mostrarà sols les PKs
        For i As Integer = 0 To _cboTD.Items.Count - 1
            Dim t As TablaBBDD = TryCast(_cboTD.Items(i), TablaBBDD)
            If t IsNot Nothing AndAlso t.Id = taulaMareId Then
                _loading = True
                _cboTD.SelectedIndex = i
                _loading = False
                OmplirCampsPK()
                GenNom()
                Exit For
            End If
        Next
    End Sub

    Private Sub CboTO_Changed(s As Object, e As EventArgs)
        If _loading Then Return
        OmplirCampsFK()
        GenNom()
    End Sub

    Private Sub CboTD_Changed(s As Object, e As EventArgs)
        If _loading Then Return
        OmplirCampsPK()
        GenNom()
    End Sub

    Private Sub OmplirCampsFK()
        _cboFK.Items.Clear()
        Dim t As TablaBBDD = TryCast(_cboTO.SelectedItem, TablaBBDD)
        If t IsNot Nothing Then
            For Each f As CampoBBDD In t.Fields
                _cboFK.Items.Add(f)
            Next
            If _cboFK.Items.Count > 0 Then _cboFK.SelectedIndex = 0
        End If
    End Sub

    Private Sub OmplirCampsPK()
        _cboPK.Items.Clear()
        Dim t As TablaBBDD = TryCast(_cboTD.SelectedItem, TablaBBDD)
        If t IsNot Nothing Then
            For Each f As CampoBBDD In t.Fields
                If f.EsPK Then _cboPK.Items.Add(f)
            Next
            If _cboPK.Items.Count = 0 Then
                For Each f As CampoBBDD In t.Fields
                    _cboPK.Items.Add(f)
                Next
            End If
            If _cboPK.Items.Count > 0 Then _cboPK.SelectedIndex = 0
        End If
    End Sub

    Private Sub GenNom()
        If _editant IsNot Nothing Then Return
        Dim ft As TablaBBDD = TryCast(_cboTO.SelectedItem, TablaBBDD)
        Dim tt As TablaBBDD = TryCast(_cboTD.SelectedItem, TablaBBDD)
        If ft IsNot Nothing AndAlso tt IsNot Nothing Then
            _txtNom.Text = "FK_" & ft.Nombre & "_" & tt.Nombre
        End If
    End Sub

    Private Sub CarregarRelacio(r As RelacionBBDD)
        ' Seleccionar taula origen → omple _cboFK manualment (no via event, _loading=True)
        For i As Integer = 0 To _cboTO.Items.Count - 1
            Dim t As TablaBBDD = DirectCast(_cboTO.Items(i), TablaBBDD)
            If t.Id = r.TablaOrigenId Then
                _cboTO.SelectedIndex = i
                ' Omplir camps FK de la taula origen
                _cboFK.Items.Clear()
                For Each f As CampoBBDD In t.Fields
                    _cboFK.Items.Add(f)
                Next
                Exit For
            End If
        Next
        ' Seleccionar taula destí → omple _cboPK manualment
        For i As Integer = 0 To _cboTD.Items.Count - 1
            Dim t As TablaBBDD = DirectCast(_cboTD.Items(i), TablaBBDD)
            If t.Id = r.TablaDestinoId Then
                _cboTD.SelectedIndex = i
                ' Omplir camps PK de la taula destí (preferentment els PK)
                _cboPK.Items.Clear()
                For Each f As CampoBBDD In t.Fields
                    If f.EsPK Then _cboPK.Items.Add(f)
                Next
                If _cboPK.Items.Count = 0 Then
                    For Each f As CampoBBDD In t.Fields
                        _cboPK.Items.Add(f)
                    Next
                End If
                Exit For
            End If
        Next
        ' Ara seleccionar el camp FK correcte
        For i As Integer = 0 To _cboFK.Items.Count - 1
            Dim f As CampoBBDD = DirectCast(_cboFK.Items(i), CampoBBDD)
            If f.Nombre = r.CampoFKNombre Then
                _cboFK.SelectedIndex = i
                Exit For
            End If
        Next
        ' Seleccionar el camp PK correcte
        For i As Integer = 0 To _cboPK.Items.Count - 1
            Dim f As CampoBBDD = DirectCast(_cboPK.Items(i), CampoBBDD)
            If f.Nombre = r.CampoPKNombre Then
                _cboPK.SelectedIndex = i
                Exit For
            End If
        Next
        SelIdx2(_cboTipus, r.CardinalityLabel)
        SelIdx2(_cboOD, r.OnDeleteLabel)
        SelIdx2(_cboOU, r.OnUpdateLabel)
        Dim wc As String = If(r.WithCheck = WithCheckOption.WithCheck, "WITH CHECK", "WITH NOCHECK")
        SelIdx2(_cboWC, wc)
        _txtNom.Text = r.Nombre
        _txtDesc.Text = r.Descripcion
        _txtComentari.Text = r.Comentari
        _chkNFR.Checked = r.NotForReplication
        _chkDis.Checked = r.Disabled
        _chkIdx.Checked = r.CrearIndexFK
    End Sub

    Private Sub SelIdx2(cbo As ComboBox, v As String)
        For i As Integer = 0 To cbo.Items.Count - 1
            If cbo.Items(i).ToString() = v Then
                cbo.SelectedIndex = i
                Return
            End If
        Next
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        If _txtComentari IsNot Nothing Then
            _txtComentari.BackColor = Color.FromArgb(8, 20, 8)
            _txtComentari.ForeColor = Color.FromArgb(120, 200, 80)
        End If
        If _lblErr IsNot Nothing Then
            _lblErr.ForeColor = AppStyle.ColPerill
            _lblErr.BackColor = Color.FromArgb(20, 255, 0, 0)
        End If
    End Sub

    Private Sub BtnHeredarCamp_Click(s As Object, e As EventArgs)
        ' Valida que hi hagi camp PK i taula filla seleccionats
        Dim ft As TablaBBDD = TryCast(_cboTO.SelectedItem, TablaBBDD)
        Dim tt As TablaBBDD = TryCast(_cboTD.SelectedItem, TablaBBDD)
        Dim pk As CampoBBDD = TryCast(_cboPK.SelectedItem, CampoBBDD)

        If ft Is Nothing OrElse tt Is Nothing OrElse pk Is Nothing Then
            _lblErr.Text = Locale.Str("REL_HEREDAR_SEL")
            _lblErr.Visible = True
            Return
        End If

        ' Nom del camp heretat: NomPK_NomTaulaMare
        Dim nomNou As String = (pk.Nombre & "_" & tt.Nombre).ToUpper()

        ' Comprovar que no existeix ja un camp amb aquest nom a la taula filla
        For Each f As CampoBBDD In ft.Fields
            If f.Nombre.ToUpper() = nomNou Then
                ' Ja existeix — simplement seleccionem-lo al combo FK i continuem
                For i As Integer = 0 To _cboFK.Items.Count - 1
                    Dim cf As CampoBBDD = TryCast(_cboFK.Items(i), CampoBBDD)
                    If cf IsNot Nothing AndAlso cf.Nombre.ToUpper() = nomNou Then
                        _cboFK.SelectedIndex = i
                        _lblErr.Visible = False
                        Return
                    End If
                Next
                Return
            End If
        Next

        ' Clonar el camp PK amb el nou nom i marcar-lo com FK
        Dim nouCamp As New CampoBBDD()
        nouCamp.Nombre          = nomNou
        nouCamp.TipoDato        = pk.TipoDato
        nouCamp.Longitud        = pk.Longitud
        nouCamp.LongitudMax     = pk.LongitudMax
        nouCamp.Precision       = pk.Precision
        nouCamp.Escala          = pk.Escala
        nouCamp.NotNull         = True
        nouCamp.EsPK            = False
        nouCamp.EsFK            = True
        nouCamp.EsIdentity      = False   ' mai identity en un FK heretat
        nouCamp.Descripcion     = Locale.Str("REL_HEREDAR_DESC") & tt.Nombre & "." & pk.Nombre

        ' Afegir a la taula filla
        ft.Fields.Add(nouCamp)

        ' Actualitzar el combo FK i seleccionar el nou camp
        OmplirCampsFK()
        For i As Integer = 0 To _cboFK.Items.Count - 1
            Dim cf As CampoBBDD = TryCast(_cboFK.Items(i), CampoBBDD)
            If cf IsNot Nothing AndAlso cf.Nombre = nomNou Then
                _cboFK.SelectedIndex = i
                Exit For
            End If
        Next

        _lblErr.Visible = False
        GenNom()
    End Sub

    Private Sub BtnOK_Click(s As Object, e As EventArgs)
        _lblErr.Visible = False
        Dim ft As TablaBBDD = TryCast(_cboTO.SelectedItem, TablaBBDD)
        Dim tt As TablaBBDD = TryCast(_cboTD.SelectedItem, TablaBBDD)
        Dim fk As CampoBBDD = TryCast(_cboFK.SelectedItem, CampoBBDD)
        Dim pk As CampoBBDD = TryCast(_cboPK.SelectedItem, CampoBBDD)

        If ft Is Nothing OrElse tt Is Nothing OrElse fk Is Nothing OrElse pk Is Nothing Then
            _lblErr.Text = Locale.Str("REL_SELECCIONA")
            _lblErr.Visible = True
            Return
        End If

        Dim tipus As CardinalityType
        Select Case If(_cboTipus.SelectedItem IsNot Nothing, _cboTipus.SelectedItem.ToString(), "1:M")
            Case "1:1" : tipus = CardinalityType.OneToOne
            Case "M:1" : tipus = CardinalityType.ManyToOne
            Case "M:M" : tipus = CardinalityType.ManyToMany
            Case Else  : tipus = CardinalityType.OneToMany
        End Select

        Dim exId As Integer = -1
        If _editant IsNot Nothing Then exId = _editant.Id

        Dim errs As List(Of String) = IntegrityEngine.ValidarRelacio(
            _p.Taules, _p.Relacions,
            ft.Id, tt.Id, fk.Nombre, pk.Nombre, tipus, exId)

        If errs.Count > 0 Then
            _lblErr.Text = "! " & errs(0)
            _lblErr.Visible = True
            Return
        End If

        Dim r As RelacionBBDD
        If _editant IsNot Nothing Then
            r = _editant
        Else
            r = New RelacionBBDD()
            r.Id = _p.GetNextRelId()
        End If

        r.TablaOrigenId = ft.Id
        r.CampoFKNombre = fk.Nombre
        r.TablaDestinoId = tt.Id
        r.CampoPKNombre = pk.Nombre
        r.TipoRelacion = tipus

        Dim odStr As String = If(_cboOD.SelectedItem IsNot Nothing, _cboOD.SelectedItem.ToString(), "NO ACTION")
        Select Case odStr
            Case "CASCADE"     : r.OnDelete = OnDeleteUpdateAction.DoCascade
            Case "SET NULL"    : r.OnDelete = OnDeleteUpdateAction.SetNull
            Case "SET DEFAULT" : r.OnDelete = OnDeleteUpdateAction.SetDefault
            Case "RESTRICT"    : r.OnDelete = OnDeleteUpdateAction.DoRestrict
            Case Else          : r.OnDelete = OnDeleteUpdateAction.NoAction
        End Select

        Dim ouStr As String = If(_cboOU.SelectedItem IsNot Nothing, _cboOU.SelectedItem.ToString(), "NO ACTION")
        Select Case ouStr
            Case "CASCADE"     : r.OnUpdate = OnDeleteUpdateAction.DoCascade
            Case "SET NULL"    : r.OnUpdate = OnDeleteUpdateAction.SetNull
            Case "SET DEFAULT" : r.OnUpdate = OnDeleteUpdateAction.SetDefault
            Case "RESTRICT"    : r.OnUpdate = OnDeleteUpdateAction.DoRestrict
            Case Else          : r.OnUpdate = OnDeleteUpdateAction.NoAction
        End Select

        Dim wcStr As String = If(_cboWC.SelectedItem IsNot Nothing, _cboWC.SelectedItem.ToString(), "WITH CHECK")
        If wcStr = "WITH CHECK" Then
            r.WithCheck = WithCheckOption.WithCheck
        Else
            r.WithCheck = WithCheckOption.WithNoCheck
        End If

        r.Nombre = _txtNom.Text
        r.Descripcion = _txtDesc.Text
        r.Comentari = _txtComentari.Text
        r.NotForReplication = _chkNFR.Checked
        r.Disabled = _chkDis.Checked
        r.CrearIndexFK = _chkIdx.Checked

        ResultRelacio = r
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

End Class
