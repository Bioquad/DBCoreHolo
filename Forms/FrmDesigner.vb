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
Imports SkiaSharp
Imports SkiaSharp.Views.Desktop
Imports SkiaSharp.Views.WindowsForms


' =======================================================================
' FrmDesigner - Finestra principal de disseny 3D
' =======================================================================
Public Class FrmDesigner
    Inherits Form

    Private _glControl As SKControl
    Private _renderer As New SphereRenderer()
    Private WithEvents _renderTimer As New System.Windows.Forms.Timer()
    Private WithEvents _errorTimer  As New System.Windows.Forms.Timer()
    Private _proyecto As ProyectoBBDD
    Private _glReady As Boolean = False

    Private _menuStrip As MenuStrip
    Private _lblStatus As Label
    Private _txtCerca As TextBox

    Private _pnlBottom As Panel
    Private _pnlBuit As Panel
    Private _pnlTaula As Panel
    Private _pnlRelacio As Panel
    Private _lvCamps       As ListView
    Private _flCamps       As FlowLayoutPanel
    Private _lblNomTaula   As Label
    Private _txtDescTaula      As TextBox
    Private _txtComentariTaula As TextBox
    Private _lblInfoRelacio As Label

    Private _orbit    As Boolean = False
    Private _dragTaula As Boolean = False
    Private _pan      As Boolean = False
    Private _midDownX    As Integer = 0
    Private _midDownY    As Integer = 0
    Private _midDownTime As Long = 0
    Private _selRect     As Boolean = False
    Private _campSelIdx    As Integer = -1   ' camp sota el cursor (hover)
    Private _campClickIdx  As Integer = -1   ' camp seleccionat per clic (persistent)
    Private _lateralSelIdx As Integer = -1
    Private _grupSel     As New List(Of Integer)()   ' IDs de taules del grup seleccionat
    Private _taulaGrupId As New Dictionary(Of Integer, Integer)()  ' taulaId -> grupId
    Private _grupPosOld  As New Dictionary(Of Integer, Single())()  ' posicions originals del grup
    Private _selStartX As Integer = 0
    Private _selStartY As Integer = 0
    Private _selEndX   As Integer = 0
    Private _selEndY   As Integer = 0
    Private _lastMX As Integer = 0
    Private _lastClickTime As Long = 0
    Private _lastClickRid  As Integer = -1
    Private _mniLateral    As ToolStripMenuItem = Nothing
    Private _mniEsfera     As ToolStripMenuItem = Nothing
    Private _tipLateral    As New ToolTip()          ' tooltip compartit per a la llista lateral
    ' Pivot Sketchup: punt 3D al voltant del qual orbitarem (botó dret)
    Private _pivotX As Single = 0.0F
    Private _pivotY As Single = 0.0F
    Private _pivotZ As Single = 0.0F
    Private _hasPivot As Boolean = False
    Private _lastMY As Integer = 0
    Private _velX As Single = 0
    Private _velY As Single = 0
    Private _dragId As Integer = -1
    Private _dragOldX As Single
    Private _dragOldY As Single
    Private _dragOldZ As Single

    ' Rubber-band FK: arrossegar camp FK cap a taula destí
    Private _dragFK        As Boolean = False
    Private _dragFKTaulaId As Integer = -1
    Private _dragFKCampNom As String  = ""
    Private _dragFKCursor  As Point   = Point.Empty
    Private _dragEsPK      As Boolean = False
    Private _dragHoverTid  As Integer = -1  ' taula sota el cursor durant rubber-band

    ' Cursor invisible per al rubber-band (1x1 transparent)
    Private Shared ReadOnly _cursorBuit As Cursor = CrearCursorBuit()
    Private Shared Function CrearCursorBuit() As Cursor
        ' Crea un cursor de 32x32 completament transparent
        Dim bmp As New System.Drawing.Bitmap(32, 32)
        Dim g As System.Drawing.Graphics = System.Drawing.Graphics.FromImage(bmp)
        g.Clear(System.Drawing.Color.Transparent)
        g.Dispose()
        Dim hCursor As IntPtr = bmp.GetHicon()
        bmp.Dispose()
        Return New Cursor(hCursor)
    End Function

    Private WithEvents _autoTimer As New System.Windows.Forms.Timer()
    Private _rutaFitxer As String = ""
    Private _connServidor As SqlServerConnector.ConnexioServidor = Nothing
    Private _modificat As Boolean = False   ' hi ha canvis sense desar

    ' Panell lateral
    Private _pnlLateral   As Panel
    Private _tabLateral   As TabControl   ' conservat per compatibilitat
    Private _lvTaules     As ListView     ' conservat per compatibilitat
    Private _lvRelacions  As ListView     ' conservat per compatibilitat
    Private _txtFiltLateral As TextBox
    Private _lblDetall    As Label
    ' Panell lateral redissenyat
    Private _pnlListaTaules   As FlowLayoutPanel
    Private _pnlListaRelacions As FlowLayoutPanel
    Private _btnTabTaules     As Button
    Private _btnTabRelacions  As Button
    Private _pnlTabContent    As Panel
    Private _tabActiu         As Integer = 0  ' 0=taules, 1=relacions

    Public Property RutaFitxer As String
        Get
            Return _rutaFitxer
        End Get
        Set(value As String)
            _rutaFitxer = value
            ActualitzarTitol()
        End Set
    End Property

    Public Sub New(p As ProyectoBBDD, motor As String)
        _proyecto = p
        _proyecto.MotorSQL = motor
        Me.Text = "Holografic DB"
        Me.FormBorderStyle = FormBorderStyle.None
        Me.WindowState = FormWindowState.Maximized
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = AppStyle.ColFons
        Me.KeyPreview = True
        ConstruirUI()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        AddHandler CommandStack.Modificat, AddressOf OnModelModificat
        AplicarOpcions()
    End Sub

    Private Sub OnModelModificat()
        MarcarModificat()
    End Sub

    ' ── Aplica totes les opcions persistides al dissenyador ──────────────
    ' Es crida al constructor i cada cop que l'usuari desa opcions noves.
    Public Sub AplicarOpcions()
        Dim op As OpcionsSistema = FrmOpcions.Opcions

        ' Colors + fonts: AppStyle.AplicarTema recorre tots els OpenForms
        AppStyle.AplicarTema(op.Tema, op.MidaFont)

        ' Límit de l'historial Desfer/Refer
        CommandStack.Limit = op.LimitUndo

        ' Auto-desar
        _autoTimer.Stop()
        If op.AutoDesarActiu Then
            _autoTimer.Interval = Math.Max(1, op.AutoDesarMinuts) * 60000
            _autoTimer.Start()
        End If

        ' Esfera: sincronitzar renderer + text del menú
        If _renderer IsNot Nothing Then
            _renderer.ShowSphere = op.MostrarEsfera
            If _mniEsfera IsNot Nothing Then
                _mniEsfera.Text = If(op.MostrarEsfera,
                                     Locale.Str("MNU_ESFERA_ON"),
                                     Locale.Str("MNU_ESFERA_OFF"))
            End If
            If _glControl IsNot Nothing Then _glControl.Invalidate()
        End If
    End Sub

    ' ── Actualitza tots els textos de la UI amb l'idioma actiu ──────────
    ' Es crida des de FrmOpcions quan canvia l'idioma.
    Public Sub AplicarIdioma()
        If _menuStrip Is Nothing Then Return

        ' Reconstruir tots els items del MenuStrip
        _menuStrip.Items.Clear()

        Dim mArxiu As New ToolStripMenuItem(Locale.Str("MNU_ARXIU"))
        mArxiu.ForeColor = AppStyle.ColAccentSec
        AfegirSubMenu(mArxiu, Locale.Str("MNU_NOU"),        AddressOf MnuNou_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_OBRIR"),      AddressOf MnuObrir_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_DESAR"),      AddressOf MnuDesar_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_DESAR_COM"),  AddressOf MnuDesarCom_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_IMPORTAR_HDB"), AddressOf MnuObrir_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_IMPORTAR_SQL"), AddressOf MnuImportSql_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_IMPORTAR_MDF"), AddressOf MnuImportMdf_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_EXPORTAR"),     AddressOf MnuExport_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_EXPORTAR_MYSQL"), AddressOf MnuExportMysql_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_EXPORTAR_PG"),  AddressOf MnuExportPg_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_EXPORTAR_MDF"), AddressOf MnuExportMdf_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_SERVIDOR"),     AddressOf MnuServidor_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_ACCIONS_SRV"),  AddressOf MnuAccionsServidor_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_CONSULTAR_DADES"), AddressOf MnuConsultarDades_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_COPIAR_BD"),       AddressOf MnuCopiarBD_Click)
        _menuStrip.Items.Add(mArxiu)

        Dim mModel As New ToolStripMenuItem(Locale.Str("MNU_MODEL"))
        mModel.ForeColor = AppStyle.ColAccentSec
        AfegirSubMenu(mModel, Locale.Str("MNU_NOVA_TAULA"),   AddressOf MnuNovaTaula_Click)
        AfegirSubMenu(mModel, Locale.Str("MNU_NOVA_MATRIU"),  AddressOf MnuNovaMatriu_Click)
        mModel.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mModel, Locale.Str("MNU_NOVA_RELACIO"), AddressOf MnuNovaRelacio_Click)
        mModel.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mModel, Locale.Str("MNU_GENERAR_DDL"),  AddressOf MnuDDL_Click)
        AfegirSubMenu(mModel, Locale.Str("MNU_VALIDAR"),      AddressOf MnuValidar_Click)
        mModel.DropDownItems.Add(New ToolStripSeparator())
        _mniEsfera = New ToolStripMenuItem(
            If(_renderer IsNot Nothing AndAlso _renderer.ShowSphere,
               Locale.Str("MNU_ESFERA_ON"), Locale.Str("MNU_ESFERA_OFF")))
        _mniEsfera.ForeColor = AppStyle.ColTextSec
        _mniEsfera.BackColor = AppStyle.ColFonsMig
        _mniEsfera.Font = AppStyle.FntPetit
        AddHandler _mniEsfera.Click, AddressOf MnuToggleEsfera_Click
        mModel.DropDownItems.Add(New ToolStripSeparator())
        mModel.DropDownItems.Add(_mniEsfera)
        _mniLateral = New ToolStripMenuItem(
            If(_pnlLateral IsNot Nothing AndAlso _pnlLateral.Visible,
               Locale.Str("MNU_LLISTA_ON"), Locale.Str("MNU_LLISTA_OFF")))
        _mniLateral.ForeColor = AppStyle.ColTextPrinc
        _mniLateral.BackColor = AppStyle.ColFonsMig
        _mniLateral.Font = AppStyle.FntPetit
        _mniLateral.CheckOnClick = False
        AddHandler _mniLateral.Click, AddressOf MnuToggleLateral_Click
        mModel.DropDownItems.Add(_mniLateral)
        _menuStrip.Items.Add(mModel)

        Dim mVista As New ToolStripMenuItem(Locale.Str("MNU_VISTA"))
        mVista.ForeColor = AppStyle.ColAccentSec
        AfegirSubMenu(mVista, Locale.Str("MNU_VEURE_TOT"),       AddressOf MnuVeureTot_Click)
        AfegirSubMenu(mVista, Locale.Str("MNU_ORGANITZAR_SEL"),  AddressOf MnuOrganitzarSel_Click)
        AfegirSubMenu(mVista, Locale.Str("MNU_ORGANITZAR"),      AddressOf MnuOrganitzar_Click)
        _menuStrip.Items.Add(mVista)

        Dim mEditar As New ToolStripMenuItem(Locale.Str("MNU_EDITAR"))
        mEditar.ForeColor = AppStyle.ColAccentSec
        AfegirSubMenu(mEditar, Locale.Str("MNU_DESFER"), AddressOf MnuUndo_Click)
        AfegirSubMenu(mEditar, Locale.Str("MNU_REFER"),  AddressOf MnuRedo_Click)
        _menuStrip.Items.Add(mEditar)

        ' Boto tancar alineat a la dreta
        Dim btnXR As New ToolStripMenuItem("[ X ]  SORTIR")
        btnXR.ForeColor = AppStyle.ColPerill
        btnXR.BackColor = AppStyle.ColFonsMig
        btnXR.Font = New Font("Courier New", 8, FontStyle.Bold)
        btnXR.Alignment = ToolStripItemAlignment.Right
        AddHandler btnXR.Click, Sub(s As Object, e As EventArgs) Me.Close()
        _menuStrip.Items.Add(btnXR)

        ' Panell lateral
        If _btnTabTaules    IsNot Nothing Then _btnTabTaules.Text    = Locale.Str("LAT_TAULES")
        If _btnTabRelacions IsNot Nothing Then _btnTabRelacions.Text = Locale.Str("LAT_RELACIONS")
        If _txtFiltLateral  IsNot Nothing AndAlso
           (_txtFiltLateral.Text = "" OrElse
            _txtFiltLateral.Text = "Cercar..."  OrElse
            _txtFiltLateral.Text = "Search..."  OrElse
            _txtFiltLateral.Text = "Buscar...") Then
            _txtFiltLateral.Text = Locale.Str("LAT_CERCA")
        End If

        ' Panell inferior buit
        If _pnlBuit IsNot Nothing Then
            For Each ctrl As Control In _pnlBuit.Controls
                Dim btn As Button = TryCast(ctrl, Button)
                If btn Is Nothing Then Continue For
                Select Case True
                    Case btn.Text.Contains("+") OrElse btn.Text.Contains("NEW TABLE") OrElse
                         btn.Text.Contains("NOVA TAULA") OrElse btn.Text.Contains("NUEVA TABLA")
                        btn.Text = Locale.Str("BTN_NOVA_TAULA")
                    Case btn.Text.Contains(">") OrElse btn.Text.Contains("RELACI")
                        btn.Text = Locale.Str("BTN_NOVA_RELACIO")
                End Select
            Next
        End If

        ' Actualitzar llistes laterals
        ActualitzarPanellLateral()

        SetStatus(Locale.Str("STATUS_ACTIU"))
    End Sub

    Private Sub ConstruirUI()
        ' Configurar tooltip lateral: apareix ràpid, dura prou
        _tipLateral.InitialDelay = 400
        _tipLateral.ReshowDelay = 200
        _tipLateral.AutoPopDelay = 8000
        _tipLateral.ShowAlways = True

        ' MenuStrip
        _menuStrip = New MenuStrip()
        _menuStrip.BackColor = AppStyle.ColFonsMig
        _menuStrip.ForeColor = AppStyle.ColAccentSec
        _menuStrip.Font = New Font("Courier New", 8)
        _menuStrip.Height = 22

        Dim mArxiu As New ToolStripMenuItem(Locale.Str("MNU_ARXIU"))
        mArxiu.ForeColor = AppStyle.ColAccentSec
        AfegirSubMenu(mArxiu, Locale.Str("MNU_NOU"), AddressOf MnuNou_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_OBRIR"), AddressOf MnuObrir_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_DESAR"), AddressOf MnuDesar_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_DESAR_COM"), AddressOf MnuDesarCom_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_IMPORTAR_HDB"),   AddressOf MnuObrir_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_IMPORTAR_SQL"),   AddressOf MnuImportSql_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_IMPORTAR_MDF"),   AddressOf MnuImportMdf_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_EXPORTAR"),       AddressOf MnuExport_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_EXPORTAR_MYSQL"), AddressOf MnuExportMysql_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_EXPORTAR_PG"),    AddressOf MnuExportPg_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_EXPORTAR_MDF"),   AddressOf MnuExportMdf_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_SERVIDOR"),     AddressOf MnuServidor_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_ACCIONS_SRV"),  AddressOf MnuAccionsServidor_Click)
        mArxiu.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mArxiu, Locale.Str("MNU_CONSULTAR_DADES"), AddressOf MnuConsultarDades_Click)
        AfegirSubMenu(mArxiu, Locale.Str("MNU_COPIAR_BD"),       AddressOf MnuCopiarBD_Click)
        _menuStrip.Items.Add(mArxiu)

        Dim mModel As New ToolStripMenuItem(Locale.Str("MNU_MODEL"))
        mModel.ForeColor = AppStyle.ColAccentSec
        AfegirSubMenu(mModel, Locale.Str("MNU_NOVA_TAULA"), AddressOf MnuNovaTaula_Click)
        AfegirSubMenu(mModel, Locale.Str("MNU_NOVA_MATRIU"), AddressOf MnuNovaMatriu_Click)
        mModel.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mModel, Locale.Str("MNU_NOVA_RELACIO"), AddressOf MnuNovaRelacio_Click)
        mModel.DropDownItems.Add(New ToolStripSeparator())
        AfegirSubMenu(mModel, Locale.Str("MNU_GENERAR_DDL"), AddressOf MnuDDL_Click)
        AfegirSubMenu(mModel, Locale.Str("MNU_VALIDAR"), AddressOf MnuValidar_Click)
        mModel.DropDownItems.Add(New ToolStripSeparator())
        _mniEsfera = New ToolStripMenuItem(Locale.Str("MNU_ESFERA_ON"))
        _mniEsfera.ForeColor = AppStyle.ColTextPrinc
        _mniEsfera.BackColor = AppStyle.ColFonsMig
        _mniEsfera.Font = New Font("Courier New", 8)
        AddHandler _mniEsfera.Click, AddressOf MnuToggleEsfera_Click
        mModel.DropDownItems.Add(_mniEsfera)
        mModel.DropDownItems.Add(New ToolStripSeparator())
        ' Item checkable per mostrar/amagar el panell lateral
        _mniLateral = New ToolStripMenuItem(Locale.Str("MNU_LLISTA_ON"))
        _mniLateral.ForeColor = AppStyle.ColTextPrinc
        _mniLateral.BackColor = AppStyle.ColFonsMig
        _mniLateral.Font = New Font("Courier New", 8)
        _mniLateral.CheckOnClick = False
        AddHandler _mniLateral.Click, AddressOf MnuToggleLateral_Click
        mModel.DropDownItems.Add(_mniLateral)
        _menuStrip.Items.Add(mModel)

        ' Menú Vista (nou)
        Dim mVista As New ToolStripMenuItem(Locale.Str("MNU_VISTA"))
        mVista.ForeColor = AppStyle.ColAccentSec
        AfegirSubMenu(mVista, Locale.Str("MNU_VEURE_TOT"),      AddressOf MnuVeureTot_Click)
        AfegirSubMenu(mVista, Locale.Str("MNU_ORGANITZAR_SEL"), AddressOf MnuOrganitzarSel_Click)
        AfegirSubMenu(mVista, Locale.Str("MNU_ORGANITZAR"),     AddressOf MnuOrganitzar_Click)
        _menuStrip.Items.Add(mVista)

        Dim mEditar As New ToolStripMenuItem(Locale.Str("MNU_EDITAR"))
        mEditar.ForeColor = AppStyle.ColAccentSec
        AfegirSubMenu(mEditar, Locale.Str("MNU_DESFER"), AddressOf MnuUndo_Click)
        AfegirSubMenu(mEditar, Locale.Str("MNU_REFER"), AddressOf MnuRedo_Click)
        _menuStrip.Items.Add(mEditar)

        ' Caixa de cerca
        Dim wrapTxt As New ToolStripControlHost(New TextBox())
        Dim txb As TextBox = DirectCast(wrapTxt.Control, TextBox)
        txb.Width = 130
        txb.Font = New Font("Courier New", 8)
        txb.BackColor = AppStyle.ColFonsInput
        txb.ForeColor = AppStyle.ColAccentSec
        txb.BorderStyle = BorderStyle.FixedSingle
        AddHandler txb.TextChanged, AddressOf TxtCerca_Changed
        _txtCerca = txb
        _menuStrip.Items.Add(wrapTxt)

        ' Boto tancar alineat a la dreta
        Dim btnX As New ToolStripMenuItem("[ X ]  SORTIR")
        btnX.ForeColor = AppStyle.ColPerill
        btnX.BackColor = AppStyle.ColFonsMig
        btnX.Font = New Font("Courier New", 8, FontStyle.Bold)
        btnX.Alignment = ToolStripItemAlignment.Right
        AddHandler btnX.Click, Sub(s As Object, e As EventArgs) Me.Close()
        _menuStrip.Items.Add(btnX)

        Me.Controls.Add(_menuStrip)
        Me.MainMenuStrip = _menuStrip

        ' Crear components (sense afegir al formulari encara)
        ConstruirPanellInferior()    ' crea _pnlBottom
        ConstruirPanellLateral()     ' crea _pnlLateral

        ' GLControl (Fill)
        _glControl = New SKControl()
        _glControl.Dock = DockStyle.Fill
        _glControl.BackColor = AppStyle.ColFons
        AddHandler _glControl.PaintSurface, AddressOf SK_PaintSurface
        AddHandler _glControl.Resize, AddressOf GL_Resize
        AddHandler _glControl.MouseDown, AddressOf GL_MouseDown
        AddHandler _glControl.MouseMove, AddressOf GL_MouseMove
        AddHandler _glControl.MouseUp, AddressOf GL_MouseUp
        AddHandler _glControl.MouseWheel, AddressOf GL_MouseWheel

        ' WinForms Dock: processa en ordre INVERS d'addició.
        ' Per que Bottom ocupi tota l'amplada → afegir-lo ÚLTIM.
        ' Fill s'afegeix primer (processa últim → omple el que queda).
        Me.Controls.Add(_glControl)    ' Fill: processa últim
        Me.Controls.Add(_pnlLateral)   ' Left: processa segon
        Me.Controls.Add(_pnlBottom)    ' Bottom: processa primer → tota l'amplada
    End Sub

    ' ================================================================
    ' PANELL LATERAL ESQUERRA (220px) — disseny manual, sense ListView
    ' ================================================================
    Private Sub ConstruirPanellLateral()
        Const AmpLat As Integer = 220
        _pnlLateral = New Panel()
        _pnlLateral.BackColor = AppStyle.ColFonsMig
        _pnlLateral.Width = AmpLat
        _pnlLateral.Dock = DockStyle.Left
        _pnlLateral.Padding = New Padding(0)
        AddHandler _pnlLateral.Paint, Sub(s2 As Object, ev As PaintEventArgs)
            Using pen As New Pen(Color.FromArgb(40, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B), 1)
                ev.Graphics.DrawLine(pen, _pnlLateral.Width - 1, 0,
                                     _pnlLateral.Width - 1, _pnlLateral.Height)
            End Using
        End Sub

        ' ── Capçalera: buscador + X ──────────────────────────────
        Dim pSearch As New Panel()
        pSearch.BackColor = AppStyle.ColFonsMig
        pSearch.Height = 26
        pSearch.Dock = DockStyle.Top
        Dim btnX As New Button()
        btnX.Text = "X" : btnX.Font = AppStyle.FntMoltPetit
        btnX.ForeColor = AppStyle.ColAccent
        btnX.BackColor = AppStyle.ColFonsInput
        btnX.FlatStyle = FlatStyle.Flat
        btnX.FlatAppearance.BorderSize = 0
        btnX.Size = New Size(20, 20)
        btnX.Location = New Point(AmpLat - 23, 3)
        btnX.Cursor = Cursors.Hand
        AddHandler btnX.Click, AddressOf BtnTancarLateral_Click
        pSearch.Controls.Add(btnX)
        _txtFiltLateral = New TextBox()
        _txtFiltLateral.Font = AppStyle.FntMoltPetit
        _txtFiltLateral.BackColor = AppStyle.ColFonsInput
        _txtFiltLateral.ForeColor = AppStyle.ColTextSec
        _txtFiltLateral.BorderStyle = BorderStyle.None
        _txtFiltLateral.Text = Locale.Str("LAT_CERCA")
        _txtFiltLateral.Location = New Point(6, 5)
        _txtFiltLateral.Width = AmpLat - 32
        AddHandler _txtFiltLateral.Enter, Sub(s2 As Object, ev As EventArgs)
            If _txtFiltLateral.Text = Locale.Str("LAT_CERCA") Then _txtFiltLateral.Text = ""
        End Sub
        AddHandler _txtFiltLateral.Leave, Sub(s2 As Object, ev As EventArgs)
            If _txtFiltLateral.Text = "" Then _txtFiltLateral.Text = Locale.Str("LAT_CERCA")
        End Sub
        AddHandler _txtFiltLateral.TextChanged, AddressOf TxtFiltLateral_Changed
        pSearch.Controls.Add(_txtFiltLateral)

        ' ── Tabs manuals (botons) ────────────────────────────────
        Dim pTabs As New Panel()
        pTabs.BackColor = AppStyle.ColFonsMig
        pTabs.Height = 22
        pTabs.Dock = DockStyle.Top
        _btnTabTaules = New Button()
        _btnTabTaules.Text = Locale.Str("LAT_TAULES")
        _btnTabTaules.Font = AppStyle.FntMoltPetit
        _btnTabTaules.ForeColor = AppStyle.ColAccentSec
        _btnTabTaules.BackColor = AppStyle.ColFonsCapc
        _btnTabTaules.FlatStyle = FlatStyle.Flat
        _btnTabTaules.FlatAppearance.BorderSize = 0
        _btnTabTaules.Size = New Size(AmpLat \ 2, 22)
        _btnTabTaules.Location = New Point(0, 0)
        _btnTabTaules.Cursor = Cursors.Hand
        AddHandler _btnTabTaules.Click, Sub(s2 As Object, ev As EventArgs)
            _tabActiu = 0 : ActualitzarTabsVisuals() : ActualitzarPanellLateral()
        End Sub
        pTabs.Controls.Add(_btnTabTaules)
        _btnTabRelacions = New Button()
        _btnTabRelacions.Text = Locale.Str("LAT_RELACIONS")
        _btnTabRelacions.Font = AppStyle.FntMoltPetit
        _btnTabRelacions.ForeColor = AppStyle.ColTextFeble
        _btnTabRelacions.BackColor = AppStyle.ColFonsMig
        _btnTabRelacions.FlatStyle = FlatStyle.Flat
        _btnTabRelacions.FlatAppearance.BorderSize = 0
        _btnTabRelacions.Size = New Size(AmpLat - AmpLat \ 2, 22)
        _btnTabRelacions.Location = New Point(AmpLat \ 2, 0)
        _btnTabRelacions.Cursor = Cursors.Hand
        AddHandler _btnTabRelacions.Click, Sub(s2 As Object, ev As EventArgs)
            _tabActiu = 1 : ActualitzarTabsVisuals() : ActualitzarPanellLateral()
        End Sub
        pTabs.Controls.Add(_btnTabRelacions)

        ' Àrea de detall (inferior) — oculta per defecte, es mostra amb contingut
        Dim pDetall As New Panel()
        pDetall.BackColor = AppStyle.ColFonsMig
        pDetall.Height = 0
        pDetall.Dock = DockStyle.Bottom
        AddHandler pDetall.Paint, Sub(s2 As Object, ev As PaintEventArgs)
            Using pen As New Pen(Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B), 1)
                ev.Graphics.DrawLine(pen, 0, 0, pDetall.Width, 0)
            End Using
        End Sub
        _lblDetall = New Label()
        _lblDetall.Font = AppStyle.FntMoltPetit
        _lblDetall.ForeColor = AppStyle.ColTextSec
        _lblDetall.BackColor = Color.Transparent
        _lblDetall.Dock = DockStyle.Fill
        _lblDetall.TextAlign = ContentAlignment.TopLeft
        _lblDetall.Padding = New Padding(4, 3, 3, 3)
        _lblDetall.AutoEllipsis = True
        pDetall.Controls.Add(_lblDetall)

        ' ── Àrea de contingut (scroll + FlowLayout) ──────────────
        _pnlTabContent = New Panel()
        _pnlTabContent.BackColor = AppStyle.ColFonsMig
        _pnlTabContent.Dock = DockStyle.Fill
        _pnlTabContent.AutoScroll = True

        ' FlowLayoutPanel sense scrollbar del sistema
        _pnlListaTaules = New FlowLayoutPanel()
        _pnlListaTaules.BackColor = AppStyle.ColFonsMig
        _pnlListaTaules.FlowDirection = FlowDirection.TopDown
        _pnlListaTaules.WrapContents = False
        _pnlListaTaules.AutoScroll = False
        _pnlListaTaules.Padding = New Padding(0)
        _pnlListaTaules.Width = AmpLat - 20

        _pnlListaRelacions = New FlowLayoutPanel()
        _pnlListaRelacions.BackColor = AppStyle.ColFonsMig
        _pnlListaRelacions.FlowDirection = FlowDirection.TopDown
        _pnlListaRelacions.WrapContents = False
        _pnlListaRelacions.AutoScroll = False
        _pnlListaRelacions.Padding = New Padding(0)
        _pnlListaRelacions.Width = AmpLat - 20
        _pnlListaRelacions.Visible = False

        ' Contenidor amb scroll manual (Panel amb clip)
        Dim pClip As New Panel()
        pClip.BackColor = AppStyle.ColFonsMig
        pClip.Dock = DockStyle.Fill
        pClip.AutoScroll = False
        pClip.Tag = "clip"
        pClip.Controls.Add(_pnlListaRelacions)
        pClip.Controls.Add(_pnlListaTaules)

        ' Botons de scroll: ▲ amunt, ▼ avall
        Dim pScroll As New Panel()
        pScroll.BackColor = AppStyle.ColFonsMig
        pScroll.Width = 20
        pScroll.Dock = DockStyle.Right
        Dim btnUp As New Button()
        btnUp.Text = "▲"
        btnUp.Font = New Font("Arial", 7)
        btnUp.ForeColor = AppStyle.ColAccent
        btnUp.BackColor = AppStyle.ColFonsMig
        btnUp.FlatStyle = FlatStyle.Flat
        btnUp.FlatAppearance.BorderColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        btnUp.FlatAppearance.BorderSize = 1
        btnUp.Size = New Size(18, 22)
        btnUp.Location = New Point(0, 0)
        btnUp.Cursor = Cursors.Hand
        AddHandler btnUp.Click, Sub(s2 As Object, ev As EventArgs)
            Dim fl As FlowLayoutPanel = If(_tabActiu = 0, _pnlListaTaules, _pnlListaRelacions)
            fl.Top = Math.Min(0, fl.Top + 48)
        End Sub
        Dim btnDn As New Button()
        btnDn.Text = "▼"
        btnDn.Font = New Font("Arial", 7)
        btnDn.ForeColor = AppStyle.ColAccent
        btnDn.BackColor = AppStyle.ColFonsMig
        btnDn.FlatStyle = FlatStyle.Flat
        btnDn.FlatAppearance.BorderColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        btnDn.FlatAppearance.BorderSize = 1
        btnDn.Size = New Size(18, 22)
        btnDn.Location = New Point(0, 24)
        btnDn.Cursor = Cursors.Hand
        AddHandler btnDn.Click, Sub(s2 As Object, ev As EventArgs)
            Dim fl As FlowLayoutPanel = If(_tabActiu = 0, _pnlListaTaules, _pnlListaRelacions)
            Dim clip2 As Panel = TryCast(fl.Parent, Panel)
            If clip2 Is Nothing Then Return
            Dim minTop As Integer = Math.Min(0, clip2.Height - fl.Height)
            fl.Top = Math.Max(minTop, fl.Top - 48)
        End Sub
        pScroll.Controls.Add(btnUp)
        pScroll.Controls.Add(btnDn)

        ' Scroll amb roda del ratolí
        AddHandler pClip.MouseWheel, Sub(s2 As Object, ev As MouseEventArgs)
            Dim fl As FlowLayoutPanel = If(_tabActiu = 0, _pnlListaTaules, _pnlListaRelacions)
            Dim clip2 As Panel = TryCast(fl.Parent, Panel)
            If clip2 Is Nothing Then Return
            Dim delta As Integer = If(ev.Delta > 0, 32, -32)
            Dim minTop As Integer = Math.Min(0, clip2.Height - fl.Height)
            fl.Top = Math.Max(minTop, Math.Min(0, fl.Top + delta))
        End Sub

        _pnlTabContent.Controls.Add(pScroll)
        _pnlTabContent.Controls.Add(pClip)

        ' Ordre dock (Bottom primer, Top después, Fill al final)
        _pnlLateral.Controls.Add(_pnlTabContent)
        _pnlLateral.Controls.Add(pDetall)
        _pnlLateral.Controls.Add(pTabs)
        _pnlLateral.Controls.Add(pSearch)
        ' _pnlLateral s'afegeix al formulari des de InitializeComponent, en ordre Dock correcte
    End Sub

    Private Sub ActualitzarTabsVisuals()
        If _btnTabTaules Is Nothing Then Return
        _btnTabTaules.ForeColor = If(_tabActiu = 0, AppStyle.ColAccentSec, AppStyle.ColTextFeble)
        _btnTabTaules.BackColor = If(_tabActiu = 0, AppStyle.ColFonsCapc, AppStyle.ColFonsMig)
        _btnTabRelacions.ForeColor = If(_tabActiu = 1, AppStyle.ColAccentSec, AppStyle.ColTextFeble)
        _btnTabRelacions.BackColor = If(_tabActiu = 1, AppStyle.ColFonsCapc, AppStyle.ColFonsMig)
        _pnlListaTaules.Visible = (_tabActiu = 0)
        _pnlListaRelacions.Visible = (_tabActiu = 1)
    End Sub


    Private Function CrearLvLateral() As ListView
        Dim lv As New ListView()
        lv.View = View.Details
        lv.FullRowSelect = True
        lv.GridLines = False
        lv.BackColor = AppStyle.ColFonsMig
        lv.ForeColor = AppStyle.ColTextSec
        lv.Font = AppStyle.FntMoltPetit
        lv.BorderStyle = BorderStyle.None
        lv.HeaderStyle = ColumnHeaderStyle.None
        lv.MultiSelect = False
        lv.Scrollable = True
        lv.Dock = DockStyle.Fill
        lv.HideSelection = False
        lv.OwnerDraw = True
        ' DrawColumnHeader obligatori quan OwnerDraw=True
        AddHandler lv.DrawColumnHeader, Sub(s2 As Object, ev As DrawListViewColumnHeaderEventArgs)
            ev.DrawDefault = False
        End Sub
        ' DrawItem: pinta el fons de la fila
        AddHandler lv.DrawItem, Sub(s2 As Object, ev As DrawListViewItemEventArgs)
            Dim bg As Color
            If ev.Item.Selected Then
                bg = AppStyle.ColHover
            ElseIf ev.ItemIndex Mod 2 = 0 Then
                bg = AppStyle.ColFonsMig
            Else
                bg = AppStyle.ColFonsMig
            End If
            Using br As New SolidBrush(bg)
                ev.Graphics.FillRectangle(br, ev.Bounds)
            End Using
            ' Línia inferior subtil
            Using pen As New Pen(Color.FromArgb(15, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B))
                ev.Graphics.DrawLine(pen, ev.Bounds.Left, ev.Bounds.Bottom - 1,
                                     ev.Bounds.Right, ev.Bounds.Bottom - 1)
            End Using
        End Sub
        ' DrawSubItem: pinta el text amb el color de l'item
        AddHandler lv.DrawSubItem, Sub(s2 As Object, ev As DrawListViewSubItemEventArgs)
            Dim fg As Color = ev.Item.ForeColor
            If ev.Item.Selected Then
                ' Quan seleccionat: text lleugerament més brillant
                fg = Color.FromArgb(
                    Math.Min(255, CInt(fg.R) + 40),
                    Math.Min(255, CInt(fg.G) + 30),
                    Math.Min(255, CInt(fg.B) + 20))
            End If
            Dim rect As Rectangle = ev.Bounds
            rect.X += 3
            rect.Width -= 3
            Using br As New SolidBrush(fg)
                Dim sf As New StringFormat()
                sf.LineAlignment = StringAlignment.Center
                sf.Trimming = StringTrimming.EllipsisCharacter
                sf.FormatFlags = StringFormatFlags.NoWrap
                ev.Graphics.DrawString(ev.SubItem.Text, ev.Item.ListView.Font, br, rect, sf)
            End Using
        End Sub
        Return lv
    End Function

    Private Sub AplicarEstilTabs(tc As TabControl)
        tc.DrawMode = TabDrawMode.OwnerDrawFixed
        tc.ItemSize = New Size(tc.Width \ 2, 18)
        AddHandler tc.DrawItem, Sub(s2 As Object, ev As DrawItemEventArgs)
            Dim sel2 As Boolean = (ev.Index = tc.SelectedIndex)
            Dim bg2 As Color = If(sel2, AppStyle.ColFonsCapc, AppStyle.ColFonsMig)
            Dim fg2 As Color = If(sel2, AppStyle.ColAccent, AppStyle.ColTextFeble)
            ev.Graphics.FillRectangle(New SolidBrush(bg2), ev.Bounds)
            If sel2 Then
                Using pb As New Pen(Color.FromArgb(80, 255, 96, 16), 1)
                    ev.Graphics.DrawLine(pb, ev.Bounds.Left, ev.Bounds.Bottom - 1,
                                         ev.Bounds.Right - 1, ev.Bounds.Bottom - 1)
                End Using
            End If
            Dim txt2 As String = tc.TabPages(ev.Index).Text
            Dim sf2 As New StringFormat()
            sf2.Alignment = StringAlignment.Center
            sf2.LineAlignment = StringAlignment.Center
            ev.Graphics.DrawString(txt2, AppStyle.FntMoltPetit, New SolidBrush(fg2), ev.Bounds, sf2)
        End Sub
    End Sub

    Public Sub ActualitzarPanellLateral()
        If _pnlListaTaules Is Nothing OrElse _proyecto Is Nothing Then Return

        ' Mantenir _taulaGrupId sincronitzat amb l'estat actual del graf.
        ' Així la selecció rectangular funciona correctament fins i tot quan
        ' s'han afegit/eliminat taules o relacions sense passar per OrganitzarPerGrups.
        RecalcularGrupId()

        Dim filtre As String = ""
        If _txtFiltLateral IsNot Nothing Then
            filtre = _txtFiltLateral.Text.Trim()
            If filtre = Locale.Str("LAT_CERCA") Then filtre = ""
        End If

        ' Calcular relacions per taula
        Dim nRelPer As New Dictionary(Of Integer, Integer)()
        For Each t As TablaBBDD In _proyecto.Taules
            Dim cnt As Integer = 0
            For Each r As RelacionBBDD In _proyecto.Relacions
                If r.TablaOrigenId = t.Id OrElse r.TablaDestinoId = t.Id Then cnt += 1
            Next
            nRelPer(t.Id) = cnt
        Next

        ' Ordenar: primer aïllades (0 rel), després de més a menys relacions
        Dim taulesOrd As New List(Of TablaBBDD)(_proyecto.Taules)
        taulesOrd.Sort(Function(a2, b2)
            Dim na As Integer = nRelPer(a2.Id)
            Dim nb As Integer = nRelPer(b2.Id)
            If na = 0 AndAlso nb > 0 Then Return -1
            If na > 0 AndAlso nb = 0 Then Return 1
            Return nb.CompareTo(na)
        End Function)

        ' ── Llista de taules ────────────────────────────────────
        _pnlListaTaules.SuspendLayout()
        _pnlListaTaules.Controls.Clear()
        _lateralSelIdx = -1
        Dim darrereraAillada As Boolean = False
        For Each t As TablaBBDD In taulesOrd
            If filtre.Length > 0 AndAlso Not t.Nombre.ToUpper().Contains(filtre.ToUpper()) Then Continue For
            Dim nRel As Integer = nRelPer(t.Id)
            Dim cg As Color = t.ColorGL
            Dim r2 As Integer = Math.Max(160, CInt(cg.R))
            Dim g2 As Integer = Math.Max(If(cg.R < 100, 140, 80), CInt(cg.G))
            Dim b2 As Integer = Math.Max(If(cg.B > 100, 160, 60), CInt(cg.B))
            Dim fg As Color = Color.FromArgb(Math.Min(255, r2), Math.Min(255, g2), Math.Min(255, b2))
            Dim tidLocal As Integer = t.Id
            Dim lbl As New Label()
            lbl.Text = "  " & t.Nombre & " (" & t.Fields.Count & "/" & nRel & ")"
            lbl.Font = AppStyle.FntMoltPetit
            lbl.ForeColor = fg
            lbl.BackColor = AppStyle.ColFonsMig
            lbl.AutoSize = False
            lbl.Width = 196
            lbl.Height = 17
            lbl.Padding = New Padding(0)
            lbl.AutoEllipsis = True
            lbl.Cursor = Cursors.Hand
            lbl.Tag = tidLocal
            _tipLateral.SetToolTip(lbl, t.Schema & "." & t.Nombre &
                                   " — " & t.Fields.Count & Locale.Str("DET_CAMPS") & ", " & nRel & Locale.Str("DLG_IMPORT_RELACIONS") &
                                   If(Not String.IsNullOrEmpty(t.Descripcion), vbCrLf & t.Descripcion, ""))
            AddHandler lbl.Click, Sub(s2 As Object, ev As EventArgs)
                Dim tsel As TablaBBDD = Nothing
                For Each tt As TablaBBDD In _proyecto.Taules
                    If tt.Id = CInt(DirectCast(s2, Label).Tag) Then tsel = tt
                Next
                If tsel Is Nothing Then Return
                _renderer.SelTaulaId = tsel.Id : _renderer.SelRelacioId = -1
                MostraPanellTaula(tsel) : CentrarEnPosicio(tsel.PosX, tsel.PosY, tsel.PosZ)
                MarcaSeleccionatLateral(DirectCast(s2, Label))
                _glControl.Invalidate()
            End Sub
            AddHandler lbl.MouseEnter, Sub(s2 As Object, ev As EventArgs)
                Dim l2 As Label = DirectCast(s2, Label)
                _campSelIdx = -1 : _campClickIdx = -1
                _lateralSelIdx = _pnlListaTaules.Controls.IndexOf(l2)
                If CInt(l2.Tag) <> _renderer.SelTaulaId Then
                    l2.BackColor = AppStyle.ColHover
                End If
            End Sub
            AddHandler lbl.MouseLeave, Sub(s2 As Object, ev As EventArgs)
                Dim l2 As Label = DirectCast(s2, Label)
                If CInt(l2.Tag) <> _renderer.SelTaulaId Then
                    l2.BackColor = AppStyle.ColFonsMig
                End If
            End Sub
            ' Separador entre aïllades i relacionades
            Dim nRelT As Integer = nRelPer(t.Id)
            If darrereraAillada AndAlso nRelT > 0 Then
                Dim sep As New Label()
                sep.Height = 6
                sep.Width = 196
                sep.BackColor = AppStyle.ColFonsMig
                sep.AutoSize = False
                Dim sepBorder As New Label()
                sepBorder.Height = 1
                sepBorder.Width = 196
                sepBorder.BackColor = Color.FromArgb(40, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
                sepBorder.AutoSize = False
                _pnlListaTaules.Controls.Add(sep)
                _pnlListaTaules.Controls.Add(sepBorder)
                darrereraAillada = False
            End If
            If nRelT = 0 Then darrereraAillada = True
            _pnlListaTaules.Controls.Add(lbl)
        Next
        _pnlListaTaules.ResumeLayout()
        Dim totalH As Integer = 0
        For Each ctrl As Control In _pnlListaTaules.Controls : totalH += ctrl.Height : Next
        _pnlListaTaules.Height = totalH + 4
        _pnlListaTaules.Top = 0   ' reset scroll

        ' ── Llista de relacions ──────────────────────────────────
        _pnlListaRelacions.SuspendLayout()
        _pnlListaRelacions.Controls.Clear()
        For Each r As RelacionBBDD In _proyecto.Relacions
            If filtre.Length > 0 AndAlso Not r.Nombre.ToUpper().Contains(filtre.ToUpper()) Then Continue For
            Dim ftColor As Color = AppStyle.ColTextFeble
            For Each t As TablaBBDD In _proyecto.Taules
                If t.Id = r.TablaOrigenId Then
                    Dim cg As Color = t.ColorGL
                    ftColor = Color.FromArgb(Math.Max(150, CInt(cg.R)),
                                             Math.Max(80, CInt(cg.G)),
                                             Math.Max(50, CInt(cg.B)))
                    Exit For
                End If
            Next
            Dim ridLocal As Integer = r.Id
            Dim lbl As New Label()
            lbl.Text = "  " & r.Nombre & " [" & r.CardinalityLabel & "]"
            lbl.Font = AppStyle.FntMoltPetit
            lbl.ForeColor = ftColor
            lbl.BackColor = AppStyle.ColFonsMig
            lbl.AutoSize = False
            lbl.Width = 196
            lbl.Height = 17
            lbl.AutoEllipsis = True
            lbl.Cursor = Cursors.Hand
            lbl.Tag = ridLocal
            ' Tooltip amb nom complet i detall origen→destí
            Dim ftNom As String = ""
            Dim ttNom As String = ""
            For Each t As TablaBBDD In _proyecto.Taules
                If t.Id = r.TablaOrigenId Then ftNom = t.Nombre
                If t.Id = r.TablaDestinoId Then ttNom = t.Nombre
            Next
            _tipLateral.SetToolTip(lbl, r.Nombre & " [" & r.CardinalityLabel & "]" & vbCrLf &
                                   ftNom & "." & r.CampoFKNombre & " → " & ttNom & "." & r.CampoPKNombre)
            AddHandler lbl.Click, Sub(s2 As Object, ev As EventArgs)
                Dim rsel As RelacionBBDD = Nothing
                For Each rr As RelacionBBDD In _proyecto.Relacions
                    If rr.Id = CInt(DirectCast(s2, Label).Tag) Then rsel = rr
                Next
                If rsel Is Nothing Then Return
                _renderer.SelRelacioId = rsel.Id : _renderer.SelTaulaId = -1
                MostraPanellRelacio(rsel)
                MarcaSeleccionatLateral(DirectCast(s2, Label))
                _glControl.Invalidate()
            End Sub
            AddHandler lbl.MouseEnter, Sub(s2 As Object, ev As EventArgs)
                Dim l2 As Label = DirectCast(s2, Label)
                _campSelIdx = -1 : _campClickIdx = -1
                _lateralSelIdx = _pnlListaRelacions.Controls.IndexOf(l2)
                If CInt(l2.Tag) <> _renderer.SelRelacioId Then
                    l2.BackColor = AppStyle.ColHover
                End If
            End Sub
            AddHandler lbl.MouseLeave, Sub(s2 As Object, ev As EventArgs)
                Dim l2 As Label = DirectCast(s2, Label)
                If CInt(l2.Tag) <> _renderer.SelRelacioId Then
                    l2.BackColor = AppStyle.ColFonsMig
                End If
            End Sub
            _pnlListaRelacions.Controls.Add(lbl)
        Next
        _pnlListaRelacions.ResumeLayout()
        _pnlListaRelacions.Height = _pnlListaRelacions.Controls.Count * 17 + 4
        _pnlListaRelacions.Top = 0   ' reset scroll
        ActualitzarTabsVisuals()
    End Sub

    Private Sub MarcaSeleccionatLateral(lblSel As Label)
        ' Marcar l'element seleccionat i desmarcar els altres
        Dim pnl As FlowLayoutPanel = If(_tabActiu = 0, _pnlListaTaules, _pnlListaRelacions)
        For Each ctrl As Control In pnl.Controls
            ctrl.BackColor = AppStyle.ColFonsMig
        Next
        lblSel.BackColor = AppStyle.ColHover
    End Sub

        ' (relacions ara en _pnlListaRelacions - veure ActualitzarPanellLateral)

    Private Sub TxtFiltLateral_Changed(s As Object, e As EventArgs)
        ActualitzarPanellLateral()
    End Sub

    Private Sub LvLateral_SelChanged(s As Object, e As EventArgs)
        If _lvTaules.SelectedItems.Count = 0 Then Return
        Dim tid As Integer = CInt(_lvTaules.SelectedItems(0).Tag)
        Dim t As TablaBBDD = Nothing
        For Each x As TablaBBDD In _proyecto.Taules
            If x.Id = tid Then t = x
        Next
        If t Is Nothing Then Return
        _renderer.SelTaulaId = tid
        _renderer.SelRelacioId = -1
        MostraPanellTaula(t)
        ' Centrar la taula a l'holograma
        CentrarEnPosicio(t.PosX, t.PosY, t.PosZ)
        ' Detall
        Dim sb As New System.Text.StringBuilder()
        sb.AppendLine(t.Schema & "." & t.Nombre)
        sb.AppendLine(t.Fields.Count & Locale.Str("DET_CAMPS"))
        Dim nRel As Integer = 0
        For Each r As RelacionBBDD In _proyecto.Relacions
            If r.TablaOrigenId = t.Id OrElse r.TablaDestinoId = t.Id Then nRel += 1
        Next
        sb.AppendLine(nRel & Locale.Str("DLG_IMPORT_RELACIONS"))
        If Not String.IsNullOrEmpty(t.Descripcion) Then sb.AppendLine(t.Descripcion)
        _lblDetall.Text = sb.ToString()
        _glControl.Invalidate()
    End Sub

    Private Sub LvLateral_DblClick(s As Object, e As EventArgs)
        If _lvTaules.SelectedItems.Count = 0 Then Return
        Dim tid As Integer = CInt(_lvTaules.SelectedItems(0).Tag)
        Dim t As TablaBBDD = Nothing
        For Each x As TablaBBDD In _proyecto.Taules
            If x.Id = tid Then t = x
        Next
        If t IsNot Nothing Then ObrirEditorCamp(t, -1)
    End Sub

    Private Sub LvRelLateral_SelChanged(s As Object, e As EventArgs)
        If _lvRelacions.SelectedItems.Count = 0 Then Return
        Dim rid As Integer = CInt(_lvRelacions.SelectedItems(0).Tag)
        Dim r As RelacionBBDD = Nothing
        For Each x As RelacionBBDD In _proyecto.Relacions
            If x.Id = rid Then r = x
        Next
        If r Is Nothing Then Return
        _renderer.SelRelacioId = rid
        _renderer.SelTaulaId = -1
        MostraPanellRelacio(r)
        ' Centrar al punt mig entre les dues taules de la relació
        Dim ft As TablaBBDD = Nothing
        Dim tt As TablaBBDD = Nothing
        For Each t As TablaBBDD In _proyecto.Taules
            If t.Id = r.TablaOrigenId Then ft = t
            If t.Id = r.TablaDestinoId Then tt = t
        Next
        If ft IsNot Nothing AndAlso tt IsNot Nothing Then
            CentrarEnPosicio((ft.PosX + tt.PosX) / 2.0F,
                             (ft.PosY + tt.PosY) / 2.0F,
                             (ft.PosZ + tt.PosZ) / 2.0F)
        ElseIf ft IsNot Nothing Then
            CentrarEnPosicio(ft.PosX, ft.PosY, ft.PosZ)
        End If
        ' Detall
        Dim txtDet As String = r.Nombre & vbCrLf &
                               r.CardinalityLabel & vbCrLf &
                               If(ft IsNot Nothing, ft.Nombre, "?") & "." & r.CampoFKNombre &
                               " → " &
                               If(tt IsNot Nothing, tt.Nombre, "?") & "." & r.CampoPKNombre
        _lblDetall.Text = txtDet
        _glControl.Invalidate()
    End Sub

    Private Sub LvRelLateral_DblClick(s As Object, e As EventArgs)
        If _lvRelacions.SelectedItems.Count = 0 Then Return
        Dim rid As Integer = CInt(_lvRelacions.SelectedItems(0).Tag)
        Dim r As RelacionBBDD = Nothing
        For Each x As RelacionBBDD In _proyecto.Relacions
            If x.Id = rid Then r = x
        Next
        If r IsNot Nothing Then ObrirEditorRelacio(r)
    End Sub

    ''' <summary>
    ''' Rota la càmera per centrar el punt 3D (px, py, pz) al mig de la pantalla.
    ''' Calcula RotY i RotX per apuntar directament al punt i reseteja OffX/OffY.
    ''' </summary>
    Private Sub CentrarEnPosicio(px As Single, py As Single, pz As Single)
        ' RotY: angle horitzontal. El punt rotacionat ha de tenir ox = 0
        ' Després de RotY: ox = px*cosY + pz*sinY = 0  → RotY = -atan2(px, pz)
        _renderer.RotY = -CSng(Math.Atan2(px, pz))

        ' Amb el nou RotY, calcular la component Y rotacionada per trobar RotX
        Dim cosY As Single = CSng(Math.Cos(_renderer.RotY))
        Dim sinY As Single = CSng(Math.Sin(_renderer.RotY))
        Dim z1 As Single = -px * sinY + pz * cosY   ' z après RotY

        ' RotX: angle vertical. oy = py*cosX - z1*sinX = 0 → RotX = atan2(py, z1)
        _renderer.RotX = CSng(Math.Atan2(py, z1))

        ' Aturar inèrcia i resetar offset
        _renderer.VelX = 0.0F
        _renderer.VelY = 0.0F
        _renderer.OffX = 0.0F
        _renderer.OffY = 0.0F
    End Sub

    ' ================================================================
    ' VISTA — Veure tot i Organitzar per grups
    ' ================================================================

    Private Sub MnuVeureTot_Click(s As Object, e As EventArgs)
        If _proyecto Is Nothing OrElse _proyecto.Taules.Count = 0 Then Return
        Dim w As Single = _glControl.Width
        Dim h As Single = _glControl.Height
        Dim cx As Single = w / 2.0F
        Dim cy As Single = h / 2.0F

        ' Guardar estat actual
        Dim savedZoom As Single = _renderer.Zoom
        Dim savedOffX As Single = _renderer.OffX
        Dim savedOffY As Single = _renderer.OffY

        ' Calcular bounding box amb Zoom=1 i OffX/OffY=0
        ' usant exactament la mateixa fórmula de projecció del renderer:
        '   fov = 500/Zoom,  d = fov+rz+400,  s = fov/d
        '   screenX = cx + rx*s + OffX
        '   screenY = cy - ry*s + OffY
        _renderer.Zoom = 1.0F
        _renderer.OffX = 0.0F
        _renderer.OffY = 0.0F

        Dim minX As Single = Single.MaxValue
        Dim maxX As Single = Single.MinValue
        Dim minY As Single = Single.MaxValue
        Dim maxY As Single = Single.MinValue

        For Each t As TablaBBDD In _proyecto.Taules
            Dim ex As Single, ey As Single, ez As Single
            _renderer.RotPPublic(t.PosX, t.PosY, t.PosZ, ex, ey, ez)
            Dim pt As New SkiaSharp.SKPoint()
            If Not _renderer.PrjPublic(ex, ey, ez, cx, cy, pt) Then Continue For

            ' Escala en el punt (s = fov/d, Zoom=1 → fov=500)
            Dim fov As Single = 500.0F
            Dim d   As Single = fov + ez + 400.0F
            If d < 0.5F Then d = 0.5F
            Dim sc  As Single = fov / d

            ' Amplada i alçada visuals de la caixa
            Dim hw As Single = _renderer.CalcularHalfWPublic(t) * sc
            Dim hh As Single = (16.8F + t.Fields.Count * 13.2F + 4.8F) * sc / 2.0F

            minX = Math.Min(minX, pt.X - hw)
            maxX = Math.Max(maxX, pt.X + hw)
            minY = Math.Min(minY, pt.Y - hh)
            maxY = Math.Max(maxY, pt.Y + hh)
        Next

        ' Restaurar estat
        _renderer.Zoom = savedZoom
        _renderer.OffX = savedOffX
        _renderer.OffY = savedOffY

        Dim sceneW As Single = maxX - minX
        Dim sceneH As Single = maxY - minY
        If sceneW < 1 OrElse sceneH < 1 Then Return

        ' Factor de zoom per que tot càpiga amb 10% de marge
        Dim fitX As Single = (w * 0.90F) / sceneW
        Dim fitY As Single = (h * 0.90F) / sceneH
        Dim zoomNou As Single = Math.Min(fitX, fitY)
        If zoomNou < 0.001F Then zoomNou = 0.001F
        If zoomNou > 50.0F  Then zoomNou = 50.0F

        ' Centrar: amb Zoom=1 i OffX/OffY=0, el centre del bbox és (bcx, bcy).
        ' Quan apliquem zoomNou, la projecció escala les coordenades relatives
        ' al centre de pantalla: screenX_nou = cx + (screenX_vell - cx) * zoomNou
        ' Per tant centre del bbox amb nou zoom = cx + (bcx - cx) * zoomNou
        ' Volem que aquest centre = cx → OffX = cx - [cx + (bcx-cx)*zoomNou]
        '                                      = -(bcx - cx) * zoomNou
        Dim bcx As Single = (minX + maxX) / 2.0F
        Dim bcy As Single = (minY + maxY) / 2.0F
        _renderer.OffX = -(bcx - cx) * zoomNou
        _renderer.OffY = -(bcy - cy) * zoomNou
        _renderer.Zoom = zoomNou
        _renderer.VelX = 0.0F
        _renderer.VelY = 0.0F
        _velX = 0.0F
        _velY = 0.0F

        _glControl.Invalidate()
        SetStatus(Locale.Str("STATUS_VISTA_TOT"))
    End Sub

    Private Sub MnuOrganitzar_Click(s As Object, e As EventArgs)
        If _proyecto Is Nothing OrElse _proyecto.Taules.Count = 0 Then Return
        Dim res As DialogResult = MessageBox.Show(
            Locale.Str("CONF_ORGANITZAR_MSG"),
            Locale.Str("CONF_ORGANITZAR_TITOL"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2)
        If res <> DialogResult.Yes Then Return
        OrganitzarPerGrups()
        ActualitzarPanellLateral()
        _glControl.Invalidate()
        SetStatus(Locale.Str("STATUS_ORGANITZAT"))
    End Sub

    Private Sub MnuOrganitzarSel_Click(s As Object, e As EventArgs)
        If _proyecto Is Nothing OrElse _proyecto.Taules.Count = 0 Then Return
        If _grupSel.Count = 0 AndAlso _renderer.SelTaulaId < 0 Then
            SetStatus(Locale.Str("STATUS_SEL_TAULA_GRUP"))
            Return
        End If
        OrganitzarGrupSeleccionat()
        ActualitzarPanellLateral()
        _glControl.Invalidate()
        SetStatus(Locale.Str("STATUS_ORGANITZAT_SEL"))
    End Sub

    ''' <summary>
    ''' Organitza en cercle Fibonacci només les taules del grup seleccionat (_grupSel),
    ''' o si no hi ha grup, les taules relacionades amb la taula seleccionada.
    ''' No toca cap altra taula del projecte.
    ''' El hub (taula amb més relacions) queda al centre; la resta s'distribueix al voltant.
    ''' Assigna un color de grup únic (no usat per taules externes al subconjunt).
    ''' </summary>
    Private Sub OrganitzarGrupSeleccionat()
        ' Recollir IDs del subconjunt a organitzar
        Dim ids As New List(Of Integer)()
        If _grupSel.Count > 0 Then
            ids.AddRange(_grupSel)
        ElseIf _renderer.SelTaulaId >= 0 Then
            ' Taula seleccionada + les relacionades directament
            ids.Add(_renderer.SelTaulaId)
            For Each r As RelacionBBDD In _proyecto.Relacions
                If r.TablaOrigenId = _renderer.SelTaulaId AndAlso Not ids.Contains(r.TablaDestinoId) Then
                    ids.Add(r.TablaDestinoId)
                ElseIf r.TablaDestinoId = _renderer.SelTaulaId AndAlso Not ids.Contains(r.TablaOrigenId) Then
                    ids.Add(r.TablaOrigenId)
                End If
            Next
        End If
        If ids.Count = 0 Then Return

        ' Obtenir objectes TablaBBDD del subconjunt
        Dim subTaules As New List(Of TablaBBDD)()
        For Each t As TablaBBDD In _proyecto.Taules
            If ids.Contains(t.Id) Then subTaules.Add(t)
        Next
        Dim m As Integer = subTaules.Count
        If m = 0 Then Return

        ' ── Escollir color únic per al grup ──────────────────────────────
        ' Palette ordenada per preferència visual (evitar blanc per a grups petits)
        Dim pal() As GroupColor = {
            GroupColor.ColorOrange, GroupColor.ColorBlue,   GroupColor.ColorGreen,
            GroupColor.ColorYellow, GroupColor.ColorCyan,   GroupColor.ColorRed,
            GroupColor.ColorMagenta, GroupColor.ColorWhite}
        ' Colors usats per taules FORA del subconjunt
        Dim usats As New HashSet(Of GroupColor)()
        For Each t As TablaBBDD In _proyecto.Taules
            If Not ids.Contains(t.Id) Then usats.Add(t.GrupColor)
        Next
        ' Escollir el primer color lliure; si tots ocupats, reutilitzar el primer
        Dim colorTriat As GroupColor = pal(0)
        For Each gc As GroupColor In pal
            If Not usats.Contains(gc) Then
                colorTriat = gc
                Exit For
            End If
        Next
        ' Aplicar color a totes les taules del subconjunt
        For Each t As TablaBBDD In subTaules
            t.GrupColor = colorTriat
        Next

        ' ── Trobar hub = la del subconjunt amb més relacions totals ──────
        Dim hub As TablaBBDD = subTaules(0)
        Dim hubRel As Integer = -1
        For Each t As TablaBBDD In subTaules
            Dim cnt As Integer = 0
            For Each r As RelacionBBDD In _proyecto.Relacions
                If r.TablaOrigenId = t.Id OrElse r.TablaDestinoId = t.Id Then cnt += 1
            Next
            If cnt > hubRel Then hubRel = cnt : hub = t
        Next

        ' Centre = posició actual del hub (no mou el hub, distribueix al voltant)
        Dim cx3 As Single = hub.PosX
        Dim cy3 As Single = hub.PosY
        Dim cz3 As Single = hub.PosZ

        ' Radi de distribució: proporcional al nombre de taules del subconjunt
        Const AMP As Single = 121.0F
        Dim rLocal As Single = If(m <= 2, AMP * 1.4F, AMP * 1.8F * CSng(Math.Sqrt(m - 1)) / CSng(Math.Sqrt(m)))

        ' Distribuir les taules no-hub en cercle Fibonacci al voltant del hub
        ' en el pla XY (mantenint Z del hub)
        Dim altres As New List(Of TablaBBDD)()
        For Each t As TablaBBDD In subTaules
            If t.Id <> hub.Id Then altres.Add(t)
        Next

        For j As Integer = 0 To altres.Count - 1
            Dim ang As Single = CSng(Math.PI * (1.0 + Math.Sqrt(5.0)) * (j + 1))
            Dim r2  As Single = rLocal * CSng(Math.Sqrt(CDbl(j + 1) / CDbl(altres.Count)))
            altres(j).PosX = cx3 + r2 * CSng(Math.Cos(ang))
            altres(j).PosY = cy3 + r2 * CSng(Math.Sin(ang))
            altres(j).PosZ = cz3
            altres(j).Depth = 0.75F
        Next
        hub.Depth = 1.0F

        ' Sincronitzar _taulaGrupId
        RecalcularGrupId()
    End Sub

    Private Sub OrganitzarPerGrups()
        Dim taules As List(Of TablaBBDD) = _proyecto.Taules
        Dim relacs  As List(Of RelacionBBDD) = _proyecto.Relacions
        Dim n As Integer = taules.Count
        If n = 0 Then Return

        Dim idxOf As New Dictionary(Of Integer, Integer)()
        For i As Integer = 0 To n - 1 : idxOf(taules(i).Id) = i : Next

        ' Comptar relacions per taula
        Dim relCnt As New Dictionary(Of Integer, Integer)()
        For Each t2 As TablaBBDD In taules
            Dim cnt As Integer = 0
            For Each r2 As RelacionBBDD In relacs
                If r2.TablaOrigenId = t2.Id OrElse r2.TablaDestinoId = t2.Id Then cnt += 1
            Next
            relCnt(t2.Id) = cnt
        Next

        ' Adjacencia
        Dim veins(n - 1) As HashSet(Of Integer)
        For i As Integer = 0 To n - 1 : veins(i) = New HashSet(Of Integer)() : Next
        For Each relC As RelacionBBDD In relacs
            If Not idxOf.ContainsKey(relC.TablaOrigenId) Then Continue For
            If Not idxOf.ContainsKey(relC.TablaDestinoId) Then Continue For
            Dim ia As Integer = idxOf(relC.TablaOrigenId)
            Dim ib As Integer = idxOf(relC.TablaDestinoId)
            If ia <> ib Then veins(ia).Add(ib) : veins(ib).Add(ia)
        Next

        ' Label Propagation (seed fix)
        Dim lbl(n - 1) As Integer
        For i As Integer = 0 To n - 1 : lbl(i) = i : Next
        Dim rnd As New Random(42)
        For iter As Integer = 0 To 50
            Dim ordre As Integer() = System.Linq.Enumerable.Range(0, n).ToArray()
            For i As Integer = n - 1 To 1 Step -1
                Dim j As Integer = rnd.Next(i + 1)
                Dim tmp As Integer = ordre(i) : ordre(i) = ordre(j) : ordre(j) = tmp
            Next
            Dim canvi As Boolean = False
            For Each i As Integer In ordre
                If veins(i).Count = 0 Then Continue For
                Dim freq As New Dictionary(Of Integer, Integer)()
                For Each v As Integer In veins(i)
                    Dim lv As Integer = lbl(v)
                    If Not freq.ContainsKey(lv) Then freq(lv) = 0
                    freq(lv) += 1
                Next
                Dim bestL As Integer = lbl(i) : Dim bestF As Integer = 0
                For Each kv As KeyValuePair(Of Integer, Integer) In freq
                    If kv.Value > bestF Then bestF = kv.Value : bestL = kv.Key
                Next
                If bestL <> lbl(i) Then lbl(i) = bestL : canvi = True
            Next
            If Not canvi Then Exit For
        Next

        ' Grups ordenats per mida desc
        Dim freqG As New Dictionary(Of Integer, Integer)()
        For i As Integer = 0 To n - 1
            If veins(i).Count = 0 Then Continue For
            If Not freqG.ContainsKey(lbl(i)) Then freqG(lbl(i)) = 0
            freqG(lbl(i)) += 1
        Next
        Dim sortedG As New List(Of KeyValuePair(Of Integer, Integer))(freqG)
        sortedG.Sort(Function(a2, b2) b2.Value.CompareTo(a2.Value))
        Dim l2g As New Dictionary(Of Integer, Integer)()
        For gi As Integer = 0 To sortedG.Count - 1 : l2g(sortedG(gi).Key) = gi : Next
        Dim nGrupsRels As Integer = sortedG.Count
        Dim AILLADES As Integer = nGrupsRels
        Dim nGrups As Integer = nGrupsRels + 1

        Dim taulaGrup(n - 1) As Integer
        For i As Integer = 0 To n - 1
            taulaGrup(i) = If(veins(i).Count = 0, AILLADES, l2g(lbl(i)))
        Next
        Dim gLlistes(nGrups - 1) As List(Of Integer)
        For gi As Integer = 0 To nGrups - 1 : gLlistes(gi) = New List(Of Integer)() : Next
        For i As Integer = 0 To n - 1 : gLlistes(taulaGrup(i)).Add(i) : Next
        If gLlistes(AILLADES).Count = 0 Then nGrups = nGrupsRels

        ' Colors greedy
        Dim pal() As GroupColor = {
            GroupColor.ColorRed,    GroupColor.ColorBlue,  GroupColor.ColorGreen,
            GroupColor.ColorYellow, GroupColor.ColorCyan,  GroupColor.ColorMagenta,
            GroupColor.ColorOrange, GroupColor.ColorWhite}
        Dim gAdj As New Dictionary(Of Integer, HashSet(Of Integer))()
        For gi As Integer = 0 To nGrups - 1 : gAdj(gi) = New HashSet(Of Integer)() : Next
        For Each relAdj As RelacionBBDD In relacs
            If Not idxOf.ContainsKey(relAdj.TablaOrigenId) Then Continue For
            If Not idxOf.ContainsKey(relAdj.TablaDestinoId) Then Continue For
            Dim ga As Integer = taulaGrup(idxOf(relAdj.TablaOrigenId))
            Dim gb As Integer = taulaGrup(idxOf(relAdj.TablaDestinoId))
            If ga <> gb Then gAdj(ga).Add(gb) : gAdj(gb).Add(ga)
        Next
        Dim gCol(nGrups - 1) As Integer
        For gi As Integer = 0 To nGrups - 1
            Dim used As New HashSet(Of Integer)()
            For Each nb As Integer In gAdj(gi)
                If nb < gi Then used.Add(gCol(nb))
            Next
            Dim col As Integer = 0
            While used.Contains(col) AndAlso col < pal.Length - 1 : col += 1 : End While
            gCol(gi) = col
        Next
        If nGrups > nGrupsRels Then gCol(AILLADES) = 7

        ' Hub i colors
        Dim hubOf(nGrups - 1) As Integer
        Dim hubC(nGrups - 1)  As Integer
        For i As Integer = 0 To nGrups - 1 : hubC(i) = -1 : Next
        For i As Integer = 0 To n - 1
            Dim gi As Integer = taulaGrup(i)
            If relCnt(taules(i).Id) > hubC(gi) Then
                hubC(gi) = relCnt(taules(i).Id) : hubOf(gi) = i
            End If
        Next
        For i As Integer = 0 To n - 1
            Dim gi As Integer = taulaGrup(i)
            taules(i).GrupColor = pal(gCol(gi) Mod pal.Length)
            taules(i).Depth = If(i = hubOf(gi), 1.0F, 0.75F)
        Next

        ' ── POSICIONS ────────────────────────────────────────────────
        ' Amplada real d'una taula en unitats 3D:
        '   67.2 * (fov+400)/fov = 67.2 * 900/500 = 121 unitats
        Const AMP As Single = 121.0F

        ' Distància entre taules del mateix grup: 1 amplada
        Dim dT As Single = AMP * 1.8F    ' 1.8 amplades entre taules del grup

        ' Distància entre centres de grups: 4 amplades + radi grup mes gran
        ' (calculem el radi real despres de distribuir les taules)

        ' ── Distribuir taules de cada grup en Fibonacci sphere local ──
        ' Cada grup té el seu mini-Fibonacci centrat a (0,0,0)
        ' Radi local: creix amb nombre de taules del grup
        Dim posXr(n - 1) As Single
        Dim posYr(n - 1) As Single
        Dim posZr(n - 1) As Single
        Dim gRadiReal(nGrupsRels - 1) As Single

        For gi As Integer = 0 To nGrupsRels - 1
            Dim idxList As List(Of Integer) = gLlistes(gi)
            Dim m As Integer = idxList.Count
            If m = 0 Then Continue For

            ' Ordenar: hub primer, resta per relacions desc
            Dim sorted As New List(Of Integer)(idxList)
            sorted.Sort(Function(a2, b2) relCnt(taules(b2).Id).CompareTo(relCnt(taules(a2).Id)))
            Dim hIdx As Integer = hubOf(gi)
            sorted.Remove(hIdx) : sorted.Insert(0, hIdx)

            ' Radi fix: totes les taules a 1 amplada del hub, sempre
            Dim rLocal As Single = If(m = 1, 0.0F, dT)

            For j As Integer = 0 To m - 1
                Dim idx As Integer = sorted(j)
                If j = 0 Then
                    posXr(idx) = 0.0F : posYr(idx) = 0.0F : posZr(idx) = 0.0F
                Else
                    ' Fibonacci 2D en el pla XY (Z=0 sempre)
                    Dim ang2D As Single = CSng(Math.PI * (1.0 + Math.Sqrt(5.0)) * j)
                    Dim r2D   As Single = rLocal * CSng(Math.Sqrt(CSng(j) / CSng(m - 1)))
                    posXr(idx) = r2D * CSng(Math.Cos(ang2D))
                    posYr(idx) = r2D * CSng(Math.Sin(ang2D))
                    posZr(idx) = 0.0F
                End If
            Next

            ' Radi real del grup = distancia maxima al hub
            Dim rmax2 As Single = 0
            For Each idx As Integer In idxList
                Dim d2 As Single = CSng(Math.Sqrt(posXr(idx)*posXr(idx)+posYr(idx)*posYr(idx)+posZr(idx)*posZr(idx)))
                If d2 > rmax2 Then rmax2 = d2
            Next
            gRadiReal(gi) = rmax2
        Next

        ' ── Centres de grups en Fibonacci sphere principal ────────────
        ' Distancia entre centres = 4 amplades + radi dels dos grups
        Dim radMax3 As Single = 0
        For gi As Integer = 0 To nGrupsRels - 1
            If gRadiReal(gi) > radMax3 Then radMax3 = gRadiReal(gi)
        Next
        ' Separació entre grups: diàmetre del grup mes gran + 2 amplades de marge
        Dim mMax As Integer = 1
        For gi As Integer = 0 To nGrupsRels - 1
            If gLlistes(gi).Count > mMax Then mMax = gLlistes(gi).Count
        Next
        Dim radiGrupMax As Single = If(mMax <= 1, dT, CSng(Math.Sqrt(mMax)) * dT * 0.6F)
        Dim dGrups As Single = radiGrupMax * 2.0F + AMP * 2.0F

        ' Rgrups fix = dGrups
        Dim Rgrups As Single = If(nGrupsRels <= 1, 0.0F, dGrups)

        ' Centres de grups en Fibonacci 2D (XY), Z=0 per evitar deformació perspectiva
        Dim gcx2(nGrupsRels - 1) As Single
        Dim gcy2(nGrupsRels - 1) As Single
        Dim gcz2(nGrupsRels - 1) As Single
        For gi As Integer = 0 To nGrupsRels - 1
            If nGrupsRels = 1 Then
                gcx2(0) = 0 : gcy2(0) = 0 : gcz2(0) = 0
            Else
                Dim phi4   As Single = CSng(Math.Acos(1.0 - 2.0 * (gi + 0.5) / nGrupsRels))
                Dim theta4 As Single = CSng(Math.PI * (1.0 + Math.Sqrt(5.0)) * gi)
                ' XY: distribució Fibonacci completa
                gcx2(gi) = Rgrups * CSng(Math.Sin(phi4) * Math.Cos(theta4))
                gcy2(gi) = Rgrups * CSng(Math.Cos(phi4))
                ' Z: limitat a ±15% del radi — profunditat suau, mai distorsiona
                Dim rawZ As Single = Rgrups * CSng(Math.Sin(phi4) * Math.Sin(theta4))
                gcz2(gi) = Math.Max(-Rgrups * 0.15F, Math.Min(Rgrups * 0.15F, rawZ))
            End If
        Next

        ' Aplicar: posicions taules = centre grup + offset local en pla XY
        ' Z local: petita variació per donar profunditat visual sense deformar
        For gi As Integer = 0 To nGrupsRels - 1
            For Each idx As Integer In gLlistes(gi)
                taules(idx).PosX = gcx2(gi) + posXr(idx)
                taules(idx).PosY = gcy2(gi) + posYr(idx)
                taules(idx).PosZ = gcz2(gi)   ' Z del centre, taules del grup al mateix pla
                taules(idx).Depth = If(idx = hubOf(gi), 1.0F, 0.75F)
            Next
        Next

        ' Taules aillades: columna a la dreta
        If nGrups > nGrupsRels AndAlso gLlistes(AILLADES).Count > 0 Then
            Dim ailList As List(Of Integer) = gLlistes(AILLADES)
            Dim colXa As Single = Rgrups + radMax3 + AMP * 4.0F
            Dim espVa As Single = AMP * 1.2F
            Dim totHa As Single = (ailList.Count - 1) * espVa
            For j As Integer = 0 To ailList.Count - 1
                taules(ailList(j)).PosX = colXa
                taules(ailList(j)).PosY = -totHa/2.0F + j*espVa
                taules(ailList(j)).PosZ = 0.0F
            Next
        End If

        ' ── Sincronitzar _taulaGrupId amb els grups calculats ──────────
        ' Permet que la selecció rectangular identifiqui correctament els grups.
        _taulaGrupId.Clear()
        For i As Integer = 0 To n - 1
            _taulaGrupId(taules(i).Id) = taulaGrup(i)
        Next
    End Sub

    ''' <summary>
    ''' Recalcula _taulaGrupId.
    ''' Un grup és un conjunt de taules del MATEIX COLOR connectades entre elles
    ''' (directament o transitivament). Les relacions entre taules de colors
    ''' diferents s'ignoren per al càlcul de grups, ja que l'usuari usa el color
    ''' per distingir clústers lògics independents.
    ''' Taules aïllades (sense cap relació) → grup -1.
    ''' </summary>
    Private Sub RecalcularGrupId()
        Dim taules As List(Of TablaBBDD) = _proyecto.Taules
        Dim relacs  As List(Of RelacionBBDD) = _proyecto.Relacions
        Dim n As Integer = taules.Count
        _taulaGrupId.Clear()
        If n = 0 Then Return

        Dim idxOf As New Dictionary(Of Integer, Integer)()
        For i As Integer = 0 To n - 1 : idxOf(taules(i).Id) = i : Next

        ' Union-Find sobre relacions del MATEIX COLOR
        Dim parent(n - 1) As Integer
        For i As Integer = 0 To n - 1 : parent(i) = i : Next

        For Each r As RelacionBBDD In relacs
            If Not idxOf.ContainsKey(r.TablaOrigenId) Then Continue For
            If Not idxOf.ContainsKey(r.TablaDestinoId) Then Continue For
            Dim ia As Integer = idxOf(r.TablaOrigenId)
            Dim ib As Integer = idxOf(r.TablaDestinoId)
            ' Ignorar relacions entre taules de colors diferents
            If taules(ia).GrupColor <> taules(ib).GrupColor Then Continue For
            ' Find amb path compression
            Dim ra As Integer = ia
            While parent(ra) <> ra : ra = parent(ra) : End While
            Dim rb As Integer = ib
            While parent(rb) <> rb : rb = parent(rb) : End While
            If ra <> rb Then parent(ra) = rb
        Next

        ' Comprimir camins i normalitzar a grupIds seqüencials
        Dim root2gid As New Dictionary(Of Integer, Integer)()
        Dim nextGid As Integer = 0
        ' Taules amb relacions del mateix color → grup per component
        ' Taules sense cap relació del mateix color → grup -1
        Dim teRelacioMateixColor(n - 1) As Boolean
        For Each r As RelacionBBDD In relacs
            If Not idxOf.ContainsKey(r.TablaOrigenId) Then Continue For
            If Not idxOf.ContainsKey(r.TablaDestinoId) Then Continue For
            Dim ia As Integer = idxOf(r.TablaOrigenId)
            Dim ib As Integer = idxOf(r.TablaDestinoId)
            If taules(ia).GrupColor = taules(ib).GrupColor Then
                teRelacioMateixColor(ia) = True
                teRelacioMateixColor(ib) = True
            End If
        Next

        For i As Integer = 0 To n - 1
            If Not teRelacioMateixColor(i) Then
                _taulaGrupId(taules(i).Id) = -1   ' aïllada (sense relacions del mateix color)
            Else
                Dim root As Integer = i
                While parent(root) <> root : root = parent(root) : End While
                parent(i) = root
                If Not root2gid.ContainsKey(root) Then
                    root2gid(root) = nextGid
                    nextGid += 1
                End If
                _taulaGrupId(taules(i).Id) = root2gid(root)
            End If
        Next
    End Sub

    Private Sub AfegirSubMenu(parent As ToolStripMenuItem, text As String, handler As EventHandler)
        Dim item As New ToolStripMenuItem(text)
        item.ForeColor = AppStyle.ColTextPrinc
        item.BackColor = AppStyle.ColFonsMig
        item.Font = New Font("Courier New", 8)
        AddHandler item.Click, handler
        parent.DropDownItems.Add(item)
    End Sub

    Private Sub ConstruirPanellInferior()
        ' ================================================================
        ' Layout del panell inferior (alçada total = AppStyle.AltPanellInf)
        '
        ' [0..AltCapcalera-1]  Capçalera taula / relacio / (buit)
        ' [AltCapcalera..AltCapcalera+AltColCapc-1]  Capçalera columnes
        ' [AltCapcalera+AltColCapc..total-AltStatusBar-1]  ListView camps
        ' [total-AltStatusBar..total]  Barra d'estat
        '
        ' Botons de capçalera dins la fila de capçalera (no tapen el grid)
        ' ================================================================

        Dim altTotal As Integer = AppStyle.AltPanellInf   ' 200 px
        Dim altCapc  As Integer = AppStyle.AltCapcalera   '  24 px
        Dim altCols  As Integer = AppStyle.AltColCapc     '  18 px
        Dim altSt    As Integer = AppStyle.AltStatusBar   '  16 px
        Dim altGrid  As Integer = altTotal - altCapc - altCols - altSt  ' 142 px (~7 files)

        ' ── Contenidor principal ──────────────────────────────────────
        _pnlBottom = New Panel()
        _pnlBottom.BackColor = AppStyle.ColFonsMig
        _pnlBottom.Dock = DockStyle.Bottom
        _pnlBottom.Height = altTotal

        ' ── Barra d'estat (ancla a baix) ─────────────────────────────
        _lblStatus = New Label()
        _lblStatus.Font = AppStyle.FntMoltPetit
        _lblStatus.ForeColor = AppStyle.ColAccent
        _lblStatus.BackColor = AppStyle.ColStatusBar
        _lblStatus.Text = Locale.Str("STATUS_ACTIU")
        _lblStatus.Dock = DockStyle.Bottom
        _lblStatus.Height = altSt
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft
        _lblStatus.Padding = New Padding(8, 0, 0, 0)
        _pnlBottom.Controls.Add(_lblStatus)

        ' ================================================================
        ' SUB-PANELL: sense seleccio
        ' ================================================================
        _pnlBuit = New Panel()
        _pnlBuit.BackColor = Color.Transparent
        _pnlBuit.Dock = DockStyle.Fill

        ' Botons centrats verticalment en l'espai disponible
        Dim yBtn As Integer = (altTotal - altSt - AppStyle.AltBotoNormal) \ 2
        Dim b1 As Button = AppStyle.CrearBotoAccio(Locale.Str("BTN_NOVA_TAULA"))
        b1.Location = New Point(10, yBtn)
        AddHandler b1.Click, AddressOf BtnNovaTaula_Click
        _pnlBuit.Controls.Add(b1)

        Dim b3 As Button = AppStyle.CrearBotoAccio(Locale.Str("BTN_NOVA_RELACIO"))
        b3.Location = New Point(10 + AppStyle.AmpBotoNormal + 8, yBtn)
        AddHandler b3.Click, AddressOf BtnNovaRelacio_Click
        _pnlBuit.Controls.Add(b3)

        _pnlBottom.Controls.Add(_pnlBuit)

        ' ================================================================
        ' SUB-PANELL: taula seleccionada
        ' ================================================================
        _pnlTaula = New Panel()
        _pnlTaula.BackColor = Color.Transparent
        _pnlTaula.Dock = DockStyle.Fill
        _pnlTaula.Visible = False

        ' --- Fila 1: capçalera taula (nom + botons) ---
        Dim pCapcTaula As New Panel()
        pCapcTaula.BackColor = AppStyle.ColFonsCapc
        pCapcTaula.Height = 30   ' 30px per encabir botons 24px amb marge vertical
        pCapcTaula.Dock = DockStyle.Top
        ' Linia inferior capçalera
        AddHandler pCapcTaula.Paint, Sub(s As Object, ev As PaintEventArgs)
            Using pen As New Pen(Color.FromArgb(40, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B), 1)
                ev.Graphics.DrawLine(pen, 0, pCapcTaula.Height - 1, pCapcTaula.Width, pCapcTaula.Height - 1)
            End Using
        End Sub

        ' Label nom taula — clic sobre ell activa edició inline
        _lblNomTaula = New Label()
        _lblNomTaula.Font = AppStyle.FntCapc
        _lblNomTaula.ForeColor = AppStyle.ColPK
        _lblNomTaula.BackColor = Color.Transparent
        _lblNomTaula.Location = New Point(6, 0)
        _lblNomTaula.Size = New Size(260, 30)
        _lblNomTaula.TextAlign = ContentAlignment.MiddleLeft
        _lblNomTaula.Cursor = Cursors.IBeam

        ' TextBox ocult que apareix quan es fa clic al label
        Dim txbNom As New TextBox()
        txbNom.Font = AppStyle.FntCapc
        txbNom.ForeColor = AppStyle.ColPK
        txbNom.BackColor = AppStyle.ColFonsInput
        txbNom.BorderStyle = BorderStyle.FixedSingle
        txbNom.Location = New Point(6, (30 - 20) \ 2)
        txbNom.Size = New Size(254, 20)
        txbNom.Visible = False
        txbNom.Name = "txbNomTaula"
        pCapcTaula.Controls.Add(txbNom)

        ' Confirmar edició (Enter o pèrdua de focus)
        Dim confirmarEdicio As Action = Sub()
            Dim t As TablaBBDD = Nothing
            For Each x As TablaBBDD In _proyecto.Taules
                If x.Id = _renderer.SelTaulaId Then t = x : Exit For
            Next
            If t IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(txbNom.Text) Then
                t.Nombre = txbNom.Text.Trim().ToUpper()
                MarcarModificat()
                _lblNomTaula.Text = "  " & t.Schema & "." & t.Nombre
                Dim lCC As Label = TryCast(_lblNomTaula.Tag, Label)
                If lCC IsNot Nothing Then lCC.Text = t.Fields.Count & Locale.Str("LBL_CAMPS")
                ActualitzarPanellLateral()
                _glControl.Invalidate()
            End If
            txbNom.Visible = False
            _lblNomTaula.Visible = True
        End Sub

        AddHandler txbNom.KeyDown, Sub(s As Object, ev As KeyEventArgs)
            If ev.KeyCode = Keys.Return Then
                confirmarEdicio()
                ev.Handled = True
                ev.SuppressKeyPress = True
            ElseIf ev.KeyCode = Keys.Escape Then
                txbNom.Visible = False
                _lblNomTaula.Visible = True
            End If
        End Sub
        AddHandler txbNom.LostFocus, Sub(s As Object, ev As EventArgs)
            If txbNom.Visible Then confirmarEdicio()
        End Sub

        ' Clic al label → activar edició inline
        AddHandler _lblNomTaula.Click, Sub(s As Object, ev As EventArgs)
            Dim t As TablaBBDD = Nothing
            For Each x As TablaBBDD In _proyecto.Taules
                If x.Id = _renderer.SelTaulaId Then t = x : Exit For
            Next
            If t Is Nothing Then Return
            txbNom.Text = t.Nombre
            txbNom.SelectAll()
            _lblNomTaula.Visible = False
            txbNom.Visible = True
            txbNom.Focus()
        End Sub

        pCapcTaula.Controls.Add(_lblNomTaula)

        ' Botons de capçalera (tots a la mateixa fila, separats per 4px)
        ' Usem mida 185×24px amb FntMoltPetit — garanteix que [ > ] NOVA RELACIÓ hi capi
        Const AMP_BTN As Integer = 185
        Const ALT_BTN As Integer = 24
        Dim bInfos() As String = {
            Locale.Str("BTN_AFEGIR_CAMP"),
            Locale.Str("BTN_RELACIO"),
            Locale.Str("BTN_COLOR"),
            Locale.Str("BTN_ELIMINAR_CAMP"),
            Locale.Str("BTN_ELIMINAR_TAULA")
        }
        Dim bHandlers() As EventHandler = {
            AddressOf BtnAfegirCamp_Click,
            AddressOf BtnNouaRelacio_Click,
            AddressOf BtnColorTaula_Click,
            AddressOf BtnEliminarCamp_Click,
            AddressOf BtnEliminar_Click
        }
        Dim bxCapc As Integer = 270
        Dim yCapcBtn As Integer = (30 - ALT_BTN) \ 2   ' centrat dins 30px
        For i As Integer = 0 To bInfos.Length - 1
            Dim bp As New Button()
            bp.Text = bInfos(i)
            bp.Font = AppStyle.FntMoltPetit
            bp.AutoEllipsis = False
            bp.AutoSize = False
            bp.FlatStyle = FlatStyle.Flat
            bp.Cursor = Cursors.Hand
            bp.TextAlign = ContentAlignment.MiddleCenter
            bp.Size = New Size(AMP_BTN, ALT_BTN)
            If bInfos(i) = Locale.Str("BTN_ELIMINAR_TAULA") OrElse
               bInfos(i) = Locale.Str("BTN_ELIMINAR_CAMP") Then
                bp.ForeColor = AppStyle.ColPerill
                bp.BackColor = AppStyle.ColFonsMig
                bp.FlatAppearance.BorderColor = Color.FromArgb(60, AppStyle.ColPerill.R, AppStyle.ColPerill.G, AppStyle.ColPerill.B)
            Else
                bp.ForeColor = AppStyle.ColTextPrinc
                bp.BackColor = AppStyle.ColFonsMig
                bp.FlatAppearance.BorderColor = AppStyle.ColVoraFeble
            End If
            bp.FlatAppearance.MouseOverBackColor = AppStyle.ColHover
            bp.FlatAppearance.BorderSize = 1
            bp.Location = New Point(bxCapc, yCapcBtn)
            AddHandler bp.Click, bHandlers(i)
            pCapcTaula.Controls.Add(bp)
            bxCapc += AMP_BTN + 4
        Next

        ' Label compte camps (a la dreta)
        Dim lblCC As New Label()
        lblCC.Name = "lblCompteCamps"
        lblCC.Font = AppStyle.FntMoltPetit
        lblCC.ForeColor = AppStyle.ColTextFeble
        lblCC.BackColor = Color.Transparent
        lblCC.TextAlign = ContentAlignment.MiddleRight
        lblCC.Dock = DockStyle.Right
        lblCC.Width = 80
        pCapcTaula.Controls.Add(lblCC)
        _lblNomTaula.Tag = lblCC   ' guardem referencia per actualitzar-la

        ' --- Fila 1b: descripció de la taula (editable) ---
        Dim pDesc As New Panel()
        pDesc.BackColor = AppStyle.ColFonsMig
        pDesc.Height = 22
        pDesc.Dock = DockStyle.Top
        AddHandler pDesc.Paint, Sub(s As Object, ev As PaintEventArgs)
            Using pen As New Pen(Color.FromArgb(25, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B), 1)
                ev.Graphics.DrawLine(pen, 0, pDesc.Height - 1, pDesc.Width, pDesc.Height - 1)
            End Using
        End Sub

        ' Label Locale.Str("LBL_DESC") ancorat a l'esquerra
        Dim pDescLeft As New Panel()
        pDescLeft.BackColor = Color.Transparent
        pDescLeft.Dock = DockStyle.Left
        pDescLeft.Width = 44
        Dim lblDescPfx As New Label()
        lblDescPfx.Text = Locale.Str("LBL_DESC")
        lblDescPfx.Font = AppStyle.FntMoltPetit
        lblDescPfx.ForeColor = AppStyle.ColTextFeble
        lblDescPfx.BackColor = Color.Transparent
        lblDescPfx.Dock = DockStyle.Fill
        lblDescPfx.TextAlign = ContentAlignment.MiddleLeft
        lblDescPfx.Padding = New Padding(6, 0, 0, 0)
        pDescLeft.Controls.Add(lblDescPfx)
        pDesc.Controls.Add(pDescLeft)

        ' TextBox amb Dock=Fill: ocupa TOT l'espai restant fins a la dreta
        _txtDescTaula = New TextBox()
        _txtDescTaula.Font = AppStyle.FntMoltPetit
        _txtDescTaula.BackColor = AppStyle.ColFonsMig
        _txtDescTaula.ForeColor = AppStyle.ColTextSec
        _txtDescTaula.BorderStyle = BorderStyle.None
        _txtDescTaula.Dock = DockStyle.Fill
        _txtDescTaula.TextAlign = HorizontalAlignment.Left
        AddHandler _txtDescTaula.TextChanged, Sub(s As Object, ev As EventArgs)
            If _proyecto Is Nothing Then Return
            Dim tsel As TablaBBDD = Nothing
            For Each x As TablaBBDD In _proyecto.Taules
                If x.Id = _renderer.SelTaulaId Then tsel = x
            Next
            If tsel IsNot Nothing Then
                tsel.Descripcion = _txtDescTaula.Text
                MarcarModificat()
            End If
        End Sub
        AddHandler _txtDescTaula.Leave, Sub(s As Object, ev As EventArgs)
            If _proyecto Is Nothing Then Return
            Dim tsel As TablaBBDD = Nothing
            For Each x As TablaBBDD In _proyecto.Taules
                If x.Id = _renderer.SelTaulaId Then tsel = x
            Next
            If tsel IsNot Nothing Then tsel.Descripcion = _txtDescTaula.Text
        End Sub
        pDesc.Controls.Add(_txtDescTaula)

        ' --- Fila descripció 2: Comentari intern (fons verd fosc) ---
        Dim pComt As New Panel()
        pComt.BackColor = Color.FromArgb(8, 20, 8)
        pComt.Height = 22
        pComt.Dock = DockStyle.Top
        AddHandler pComt.Paint, Sub(s As Object, ev As PaintEventArgs)
            Using pen As New Pen(Color.FromArgb(25, 60, 100, 40), 1)
                ev.Graphics.DrawLine(pen, 0, pComt.Height - 1, pComt.Width, pComt.Height - 1)
            End Using
        End Sub
        Dim pComtLeft As New Panel()
        pComtLeft.BackColor = Color.Transparent
        pComtLeft.Dock = DockStyle.Left
        pComtLeft.Width = 44
        Dim lblComtPfx As New Label()
        lblComtPfx.Text = Locale.Str("LBL_NOTA")
        lblComtPfx.Font = AppStyle.FntMoltPetit
        lblComtPfx.ForeColor = Color.FromArgb(80, 160, 60)
        lblComtPfx.BackColor = Color.Transparent
        lblComtPfx.Dock = DockStyle.Fill
        lblComtPfx.TextAlign = ContentAlignment.MiddleLeft
        lblComtPfx.Padding = New Padding(6, 0, 0, 0)
        pComtLeft.Controls.Add(lblComtPfx)
        pComt.Controls.Add(pComtLeft)
        _txtComentariTaula = New TextBox()
        _txtComentariTaula.Font = AppStyle.FntMoltPetit
        _txtComentariTaula.BackColor = Color.FromArgb(8, 20, 8)
        _txtComentariTaula.ForeColor = Color.FromArgb(120, 200, 80)
        _txtComentariTaula.BorderStyle = BorderStyle.None
        _txtComentariTaula.Dock = DockStyle.Fill
        _txtComentariTaula.TextAlign = HorizontalAlignment.Left
        AddHandler _txtComentariTaula.Leave, Sub(s As Object, ev As EventArgs)
            If _proyecto Is Nothing Then Return
            Dim tsel As TablaBBDD = Nothing
            For Each x As TablaBBDD In _proyecto.Taules
                If x.Id = _renderer.SelTaulaId Then tsel = x
            Next
            If tsel IsNot Nothing Then
                tsel.Comentari = _txtComentariTaula.Text
                MarcarModificat()
            End If
        End Sub
        pComt.Controls.Add(_txtComentariTaula)
        ' El Fill s'afegeix al final del codi pero és el primer control a processar

        ' --- Fila 2: capçalera columnes del grid ---
        Dim pCols As New Panel()
        pCols.BackColor = AppStyle.ColFonsMig
        pCols.Height = altCols
        pCols.Dock = DockStyle.Top
        pCols.Tag  = "CAPCOLUMNES_NOESTIL"

        ' Definicio columnes: (text, ample)
        Dim colDefs()() As Object = {
            New Object() {"#", 14},
            New Object() {Locale.Str("COL_NOM"), 110},
            New Object() {Locale.Str("COL_TIPUS"), 105},
            New Object() {Locale.Str("COL_LONG"), 40},
            New Object() {"PK", 26},
            New Object() {"FK", 26},
            New Object() {"NN", 26},
            New Object() {"UQ", 26},
            New Object() {"ID", 26},
            New Object() {Locale.Str("COL_ALIAS"), 80},
            New Object() {Locale.Str("COL_MASCARA"), 80},
            New Object() {Locale.Str("COL_FORMAT"), 80},
            New Object() {Locale.Str("COL_DESC"), 0}
        }
        Dim xCol As Integer = 0
        For Each cd As Object() In colDefs
            Dim lc As New Label()
            lc.Text = cd(0).ToString()
            lc.Font = AppStyle.FntMoltPetit
            lc.ForeColor = Color.FromArgb(170, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
            lc.BackColor = Color.Transparent
            lc.TextAlign = ContentAlignment.MiddleLeft
            lc.Padding = New Padding(3, 0, 0, 0)
            Dim amp As Integer = CInt(cd(1))
            If amp = 0 Then
                ' Ultima columna: omplir la resta
                lc.Location = New Point(xCol, 0)
                lc.Size = New Size(2000, altCols)  ' es tallara pel panel
            Else
                lc.Location = New Point(xCol, 0)
                lc.Size = New Size(amp, altCols)
                xCol += amp
            End If
            pCols.Controls.Add(lc)
        Next

        ' --- Fila 3: Grid de camps amb FlowLayoutPanel (estil aplicació) ---
        ' Panel contenidor amb clip per scroll manual
        Dim pGridClip As New Panel()
        pGridClip.BackColor = AppStyle.ColFonsMig
        pGridClip.Dock = DockStyle.Fill
        pGridClip.AutoScroll = False

        _flCamps = New FlowLayoutPanel()
        _flCamps.Tag = "NOESTIL"
        _flCamps.BackColor = AppStyle.ColFonsMig
        _flCamps.FlowDirection = FlowDirection.TopDown
        _flCamps.WrapContents = False
        _flCamps.AutoScroll = False
        _flCamps.Padding = New Padding(0)
        _flCamps.Width = 900

        ' Scroll amb roda del ratolí
        AddHandler pGridClip.MouseWheel, Sub(s As Object, ev As MouseEventArgs)
            Dim delta As Integer = If(ev.Delta > 0, 18, -18)
            Dim minTop As Integer = Math.Min(0, pGridClip.Height - _flCamps.Height)
            _flCamps.Top = Math.Max(minTop, Math.Min(0, _flCamps.Top + delta))
        End Sub

        ' Botons de scroll ▲▼ a la dreta
        Dim pGridScroll As New Panel()
        pGridScroll.BackColor = AppStyle.ColFonsMig
        pGridScroll.Width = 20
        pGridScroll.Dock = DockStyle.Right
        Dim bGUp As New Button()
        bGUp.Text = "▲" : bGUp.Font = New Font("Arial", 7)
        bGUp.ForeColor = AppStyle.ColAccent
        bGUp.BackColor = AppStyle.ColFonsMig
        bGUp.FlatStyle = FlatStyle.Flat
        bGUp.FlatAppearance.BorderColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        bGUp.Size = New Size(18, 22) : bGUp.Location = New Point(0, 0)
        AddHandler bGUp.Click, Sub(s As Object, ev As EventArgs)
            _flCamps.Top = Math.Min(0, _flCamps.Top + 18)
        End Sub
        Dim bGDn As New Button()
        bGDn.Text = "▼" : bGDn.Font = New Font("Arial", 7)
        bGDn.ForeColor = AppStyle.ColAccent
        bGDn.BackColor = AppStyle.ColFonsMig
        bGDn.FlatStyle = FlatStyle.Flat
        bGDn.FlatAppearance.BorderColor = Color.FromArgb(30, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B)
        bGDn.Size = New Size(18, 22) : bGDn.Location = New Point(0, 24)
        AddHandler bGDn.Click, Sub(s As Object, ev As EventArgs)
            Dim minTop As Integer = Math.Min(0, pGridClip.Height - _flCamps.Height)
            _flCamps.Top = Math.Max(minTop, _flCamps.Top - 18)
        End Sub
        pGridScroll.Controls.Add(bGUp)
        pGridScroll.Controls.Add(bGDn)

        pGridClip.Controls.Add(pGridScroll)
        pGridClip.Controls.Add(_flCamps)

        ' Mantenir _lvCamps per compatibilitat (ocult)
        _lvCamps = New ListView()
        _lvCamps.Visible = False
        AddHandler _lvCamps.DoubleClick, AddressOf LvCamps_DblClick

        ' Afegir controls en ordre correcte per Dock
        _pnlTaula.Controls.Add(pGridClip)
        _pnlTaula.Controls.Add(pCols)
        _pnlTaula.Controls.Add(pComt)
        _pnlTaula.Controls.Add(pDesc)
        _pnlTaula.Controls.Add(pCapcTaula)

        _pnlBottom.Controls.Add(_pnlTaula)

        ' ================================================================
        ' SUB-PANELL: relacio seleccionada
        ' ================================================================
        _pnlRelacio = New Panel()
        _pnlRelacio.BackColor = Color.Transparent
        _pnlRelacio.Dock = DockStyle.Fill
        _pnlRelacio.Visible = False

        Dim pCapcRel As New Panel()
        pCapcRel.BackColor = AppStyle.ColFonsCapc
        pCapcRel.Height = altCapc
        pCapcRel.Dock = DockStyle.Top

        Dim bEdit As Button = AppStyle.CrearBotoCapc(Locale.Str("BTN_EDITAR"))
        bEdit.Location = New Point(6, (altCapc - AppStyle.AltBotoPetit) \ 2)
        AddHandler bEdit.Click, AddressOf BtnEditarRelacio_Click
        pCapcRel.Controls.Add(bEdit)

        Dim bElimR As Button = AppStyle.CrearBotoPerill(Locale.Str("BTN_ELIMINAR_TAULA"))
        bElimR.Location = New Point(6 + AppStyle.AmpBotoPetit + 4, (altCapc - AppStyle.AltBotoPetit) \ 2)
        AddHandler bElimR.Click, AddressOf BtnEliminarRelacio_Click
        pCapcRel.Controls.Add(bElimR)

        Dim bTancar As Button = AppStyle.CrearBotoCapc(Locale.Str("BTN_TANCAR"))
        bTancar.Location = New Point(6 + (AppStyle.AmpBotoPetit + 4) * 2, (altCapc - AppStyle.AltBotoPetit) \ 2)
        AddHandler bTancar.Click, AddressOf BtnTancarRelacio_Click
        pCapcRel.Controls.Add(bTancar)

        _pnlRelacio.Controls.Add(pCapcRel)

        _lblInfoRelacio = New Label()
        _lblInfoRelacio.Font = AppStyle.FntNormal
        _lblInfoRelacio.ForeColor = AppStyle.ColAccentSec
        _lblInfoRelacio.BackColor = Color.Transparent
        _lblInfoRelacio.Dock = DockStyle.Fill
        _lblInfoRelacio.TextAlign = ContentAlignment.TopLeft
        _lblInfoRelacio.Padding = New Padding(10, 6, 6, 6)
        _pnlRelacio.Controls.Add(_lblInfoRelacio)
        _pnlBottom.Controls.Add(_pnlRelacio)

        ' _pnlBottom s'afegeix al formulari des de InitializeComponent, en ordre Dock correcte
    End Sub

    Private Function AfegirBotoPanel(parent As Panel, text As String, x As Integer, y As Integer) As Button
        Dim b As Button = AppStyle.CrearBotoAccio(text)
        b.Location = New Point(x, y)
        parent.Controls.Add(b)
        Return b
    End Function

    Private Function AfegirBotoPetit(parent As Panel, text As String, x As Integer, y As Integer) As Button
        Dim b As Button = AppStyle.CrearBotoCapc(text)
        b.Location = New Point(x, y)
        parent.Controls.Add(b)
        Return b
    End Function

    ' OpenGL events
    Private Sub SK_PaintSurface(s As Object, e As SKPaintSurfaceEventArgs)
        Dim canvas As SKCanvas = e.Surface.Canvas
        Dim w As Integer = e.Info.Width
        Dim h As Integer = e.Info.Height
        _renderer.Render(canvas, w, h, _proyecto)

        ' ── Rectangle de selecció ───────────────────────────────────
        If _selRect Then
            Dim rx1s As Single = Math.Min(_selStartX, _selEndX)
            Dim rx2s As Single = Math.Max(_selStartX, _selEndX)
            Dim ry1s As Single = Math.Min(_selStartY, _selEndY)
            Dim ry2s As Single = Math.Max(_selStartY, _selEndY)
            ' Fons semitransparent blanc
            Using pFill As New SKPaint()
                pFill.Style = SKPaintStyle.Fill
                pFill.Color = New SKColor(255, 255, 255, 25)
                canvas.DrawRect(rx1s, ry1s, rx2s - rx1s, ry2s - ry1s, pFill)
            End Using
            ' Vora blanca puntejada
            Using pBorder As New SKPaint()
                pBorder.IsAntialias = True
                pBorder.Style = SKPaintStyle.Stroke
                pBorder.Color = New SKColor(255, 255, 255, 200)
                pBorder.StrokeWidth = 1.5F
                pBorder.PathEffect = SKPathEffect.CreateDash(New Single() {6, 3}, 0)
                canvas.DrawRect(rx1s, ry1s, rx2s - rx1s, ry2s - ry1s, pBorder)
            End Using
        End If

        ' ── Indicador de grup seleccionat (contorn lluminós) ────────
        If _grupSel.Count > 0 Then
            Dim cx4 As Single = w / 2.0F
            Dim cy4 As Single = h / 2.0F
            Using pGrup As New SKPaint()
                pGrup.IsAntialias = True
                pGrup.Style = SKPaintStyle.Stroke
                pGrup.Color = New SKColor(255, 255, 255, 120)
                pGrup.StrokeWidth = 2.0F
                pGrup.PathEffect = SKPathEffect.CreateDash(New Single() {4, 2}, 0)
                For Each t As TablaBBDD In _proyecto.Taules
                    If Not _grupSel.Contains(t.Id) Then Continue For
                    Dim ex4 As Single, ey4 As Single, ez4 As Single
                    _renderer.RotPPublic(t.PosX, t.PosY, t.PosZ, ex4, ey4, ez4)
                    Dim pt4 As New SKPoint()
                    If Not _renderer.PrjPublic(ex4, ey4, ez4, cx4, cy4, pt4) Then Continue For
                    Dim sc4 As Single = 500.0F / _renderer.Zoom / (500.0F / _renderer.Zoom + ez4 + 400.0F)
                    Dim hw4 As Single = 67.2F * sc4 * 1.5F   ' sFactor seleccionat
                    Dim hh4 As Single = (16.8F + t.Fields.Count * 13.2F + 4.8F) * sc4 * 1.5F / 2.0F
                    canvas.DrawRect(pt4.X - hw4, pt4.Y - hh4, hw4 * 2, hh4 * 2, pGrup)
                Next
            End Using
        End If

        ' ── Highlight taula sota el cursor durant rubber-band ───────
        If _dragFK AndAlso _dragHoverTid >= 0 AndAlso _dragHoverTid <> _dragFKTaulaId Then
            For Each t As TablaBBDD In _proyecto.Taules
                If t.Id <> _dragHoverTid Then Continue For
                Dim cx2 As Single = w / 2.0F
                Dim cy2 As Single = h / 2.0F
                Dim rx2 As Single, ry2 As Single, rz2 As Single
                _renderer.RotPPublic(t.PosX, t.PosY, t.PosZ, rx2, ry2, rz2)
                Dim pt2 As SKPoint
                If Not _renderer.PrjPublic(rx2, ry2, rz2, cx2, cy2, pt2) Then Exit For
                Dim sH  As Single = _renderer.CalcScalePublic(rx2, ry2, rz2)
                Dim hwH As Single = _renderer.CalcularHalfWPublic(t) * sH * 1.25F
                Dim hhH As Single = (16.8F + t.Fields.Count * 13.2F + 4.8F) * sH / 2.0F * 1.25F
                Dim rH  As New SKRoundRect(
                    New SKRect(pt2.X - hwH, pt2.Y - hhH, pt2.X + hwH, pt2.Y + hhH), 5 * sH, 5 * sH)
                ' Color del halo = color propi de la taula
                Dim cT As Color = t.ColorGL
                Using pGH As New SKPaint()
                    pGH.IsAntialias = True
                    pGH.Style       = SKPaintStyle.Stroke
                    pGH.StrokeWidth = 8.0F * sH
                    pGH.Color       = New SKColor(CByte(cT.R), CByte(cT.G), CByte(cT.B), 40)
                    canvas.DrawRoundRect(rH, pGH)
                End Using
                Using pH As New SKPaint()
                    pH.IsAntialias = True
                    pH.Style       = SKPaintStyle.Stroke
                    pH.StrokeWidth = 2.0F * sH
                    pH.Color       = New SKColor(CByte(cT.R), CByte(cT.G), CByte(cT.B), 220)
                    canvas.DrawRoundRect(rH, pH)
                End Using
                Exit For
            Next
        End If

        ' ── Rubber-band FK ──────────────────────────────────────────
        If _dragFK AndAlso _dragFKCursor <> Point.Empty Then
            ' Origen: centre baix del panell inferior (aproximació visual)
            Dim srcX As Single = w / 2.0F
            Dim srcY As Single = h - 10.0F
            Dim dstX As Single = _dragFKCursor.X
            Dim dstY As Single = _dragFKCursor.Y
            ' Glow exterior
            Using pGlow As New SKPaint()
                pGlow.IsAntialias = True
                pGlow.Style       = SKPaintStyle.Stroke
                pGlow.StrokeWidth = 5.0F
                pGlow.Color       = New SKColor(100, 180, 255, 40)
                canvas.DrawLine(srcX, srcY, dstX, dstY, pGlow)
            End Using
            ' Línia principal blava cian puntejada
            Using pLine As New SKPaint()
                pLine.IsAntialias = True
                pLine.Style       = SKPaintStyle.Stroke
                pLine.StrokeWidth = 1.5F
                pLine.Color       = New SKColor(100, 200, 255, 220)
                pLine.PathEffect  = SKPathEffect.CreateDash(New Single() {8, 4}, 0)
                canvas.DrawLine(srcX, srcY, dstX, dstY, pLine)
            End Using
            ' Punt al cursor
            Using pDot As New SKPaint()
                pDot.IsAntialias = True
                pDot.Style       = SKPaintStyle.Fill
                pDot.Color       = New SKColor(100, 220, 255, 240)
                canvas.DrawCircle(dstX, dstY, 5.0F, pDot)
            End Using
            ' Etiqueta del camp FK
            Using pTxt As New SKPaint()
                pTxt.IsAntialias = True
                pTxt.Color       = New SKColor(100, 220, 255, 200)
            Using fnt As New SKFont(SKTypeface.FromFamilyName("Courier New"), 10)
                canvas.DrawText(_dragFKCampNom, dstX + 10, dstY - 6, fnt, pTxt)
            End Using
            End Using
        End If

        If Not _glReady Then
            _glReady = True
            ' Restaurar estat complet de la càmera des del projecte
            _renderer.RotX = _proyecto.CameraRotX
            _renderer.RotY = _proyecto.CameraRotY
            _renderer.Zoom = _proyecto.CameraZoom
            _renderer.OffX = _proyecto.CameraOffX
            _renderer.OffY = _proyecto.CameraOffY
            SphereRenderer.DistribuirTaules(_proyecto)
            _renderTimer.Interval = 33
            _renderTimer.Start()

            ' Timer parpadejament MER errors (400ms)
            _errorTimer.Interval = 400
            _errorTimer.Start()
            ActualitzarTitol()
            ActualitzarPanellLateral()
            SetStatus(Locale.Str("STATUS_LLEST") & _proyecto.MotorSQL)
        End If
    End Sub

    Private Sub RenderTimer_Tick(s As Object, e As EventArgs) Handles _renderTimer.Tick
        ' Inèrcia de rotació: aplicar velocitat i frenar (separada del dibuix)
        If Not _orbit Then
            If Math.Abs(_velX) > 0.0001F OrElse Math.Abs(_velY) > 0.0001F Then
                _renderer.RotY += _velX
                _renderer.RotX += _velY
                If _renderer.RotX > 1.5F Then _renderer.RotX = 1.5F
                If _renderer.RotX < -1.5F Then _renderer.RotX = -1.5F
                _velX *= 0.92F
                _velY *= 0.92F
                If Math.Abs(_velX) < 0.0001F Then _velX = 0
                If Math.Abs(_velY) < 0.0001F Then _velY = 0
            End If
        End If
        ' Mode debug: mostrar coordenades de càmera i taula seleccionada
        If FrmOpcions.Opcions.ModeDebug Then
            Dim selT As TablaBBDD = Nothing
            If _renderer.SelTaulaId >= 0 Then
                For Each tt As TablaBBDD In _proyecto.Taules
                    If tt.Id = _renderer.SelTaulaId Then selT = tt : Exit For
                Next
            End If
            Dim dbg As String = String.Format(
                "DBG  RotX:{0:F3}  RotY:{1:F3}  Zoom:{2:F2}  OffX:{3:F0}  OffY:{4:F0}",
                _renderer.RotX, _renderer.RotY, _renderer.Zoom,
                _renderer.OffX, _renderer.OffY)
            If selT IsNot Nothing Then
                dbg &= String.Format("   SEL:{0}  ({1:F1},{2:F1},{3:F1})",
                                     selT.Nombre, selT.PosX, selT.PosY, selT.PosZ)
            End If
            SetStatus(dbg)
        End If
        _glControl.Invalidate()
    End Sub

    Private Sub GL_Resize(s As Object, e As EventArgs)
        _glControl.Invalidate()
    End Sub

    Private Sub GL_MouseDown(s As Object, e As MouseEventArgs)
        _lastMX = e.X
        _lastMY = e.Y

        ' Boto central (roda) — centrar holograma
        If e.Button = MouseButtons.Middle Then
            _pan = True
            _midDownX = e.X
            _midDownY = e.Y
            _midDownTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            Return
        End If

        If e.Button = MouseButtons.Right Then
            _orbit = True
            ' Calcular pivot: desprojectar el pixel del cursor a coordenades 3D
            ' Busquem si hi ha una taula prop del cursor per fer pivot sobre ella
            Dim pidTaula As Integer = _renderer.HitTestTaula(
                e.X, e.Y, _glControl.Width, _glControl.Height, _proyecto)
            If pidTaula >= 0 Then
                For Each tt As TablaBBDD In _proyecto.Taules
                    If tt.Id = pidTaula Then
                        _pivotX = tt.PosX : _pivotY = tt.PosY : _pivotZ = tt.PosZ
                        _hasPivot = True
                        Exit For
                    End If
                Next
            Else
                ' Pivot al punt 3D sota el cursor interpolant la profunditat
                _hasPivot = _renderer.UnprojectCursor(
                    e.X, e.Y, _glControl.Width, _glControl.Height,
                    _proyecto, _pivotX, _pivotY, _pivotZ)
            End If
            Return
        End If

        If e.Button = MouseButtons.Left Then
            _selRect = False   ' sempre netejar el rectangle en nou clic
            Dim tid As Integer = _renderer.HitTestTaula(e.X, e.Y, _glControl.Width, _glControl.Height, _proyecto)
            If tid >= 0 Then
                ' ── Ctrl+clic: toggle la taula dins/fora de _grupSel ──────
                If (Control.ModifierKeys And Keys.Control) <> 0 Then
                    If _grupSel.Contains(tid) Then
                        _grupSel.Remove(tid)
                        ' Si el grup queda buit, neteja tot
                        If _grupSel.Count = 0 Then
                            _renderer.SelTaulaId = -1
                            MostraPanellBuit()
                        Else
                            ' Actualitza hub (la taula amb més relacions del grup restant)
                            Dim hubId As Integer = _grupSel(0)
                            Dim hubRelN As Integer = -1
                            For Each x As TablaBBDD In _proyecto.Taules
                                If Not _grupSel.Contains(x.Id) Then Continue For
                                Dim cnt As Integer = 0
                                For Each r2 As RelacionBBDD In _proyecto.Relacions
                                    If r2.TablaOrigenId = x.Id OrElse r2.TablaDestinoId = x.Id Then cnt += 1
                                Next
                                If cnt > hubRelN Then hubRelN = cnt : hubId = x.Id
                            Next
                            _renderer.SelTaulaId = hubId
                            Dim hubT2 As TablaBBDD = Nothing
                            For Each x As TablaBBDD In _proyecto.Taules
                                If x.Id = hubId Then hubT2 = x : Exit For
                            Next
                            If hubT2 IsNot Nothing Then MostraPanellTaula(hubT2)
                        End If
                    Else
                        _grupSel.Add(tid)
                        _renderer.SelTaulaId = tid
                        Dim tCtrl As TablaBBDD = Nothing
                        For Each x As TablaBBDD In _proyecto.Taules
                            If x.Id = tid Then tCtrl = x : Exit For
                        Next
                        If tCtrl IsNot Nothing Then MostraPanellTaula(tCtrl)
                    End If
                    SetStatus(_grupSel.Count & Locale.Str("STATUS_GRUPS_SEL"))
                    _glControl.Invalidate()
                    Return
                End If

                ' ── Clic normal: selecció simple (sense Ctrl) ────────────
                ' Si la taula no és del grup seleccionat, netejar selecció de grup
                If _grupSel.Count > 0 AndAlso Not _grupSel.Contains(tid) Then
                    _grupSel.Clear()
                    _grupPosOld.Clear()
                End If
                _renderer.SelTaulaId = tid
                _renderer.SelRelacioId = -1
                _dragTaula = True
                _dragId = tid
                Dim t As TablaBBDD = Nothing
                For Each x As TablaBBDD In _proyecto.Taules
                    If x.Id = tid Then t = x
                Next
                If t IsNot Nothing Then
                    _dragOldX = t.PosX
                    _dragOldY = t.PosY
                    _dragOldZ = t.PosZ
                End If
                MostraPanellTaula(t)
                _glControl.Invalidate()
                Return
            End If
            ' Hit-test sobre relació
            Dim rid As Integer = _renderer.HitTestRelacio(e.X, e.Y, _glControl.Width, _glControl.Height, _proyecto)
            If rid >= 0 Then
                ' Comprovar si hi ha múltiples relacions en la zona
                Dim rBase As RelacionBBDD = Nothing
                For Each x As RelacionBBDD In _proyecto.Relacions
                    If x.Id = rid Then rBase = x
                Next
                If rBase IsNot Nothing Then
                    Dim relacionsZona As List(Of RelacionBBDD) = SphereRenderer.RelacionsEntreTaules(
                        _proyecto, rBase.TablaOrigenId, rBase.TablaDestinoId)
                    If relacionsZona.Count > 1 Then
                        ' Mostrar popup per escollir
                        Dim ctx As New ContextMenuStrip()
                        ctx.BackColor = AppStyle.ColFonsMig
                        ctx.ForeColor = AppStyle.ColAccentSec
                        ctx.Font = AppStyle.FntMoltPetit
                        For Each rz As RelacionBBDD In relacionsZona
                            Dim rzLocal As RelacionBBDD = rz
                            Dim mi As New ToolStripMenuItem(rzLocal.Nombre & "  [" & rzLocal.CardinalityLabel & "]")
                            mi.BackColor = AppStyle.ColFonsMig
                            mi.ForeColor = AppStyle.ColAccentSec
                            AddHandler mi.Click, Sub(ss As Object, ee As EventArgs)
                                _renderer.SelRelacioId = rzLocal.Id
                                _renderer.SelTaulaId = -1
                                MostraPanellRelacio(rzLocal)
                                _glControl.Invalidate()
                            End Sub
                            ctx.Items.Add(mi)
                        Next
                        ctx.Show(_glControl, New Point(e.X, e.Y))
                        Return
                    End If
                End If
                ' Detectar doble clic (dos clics en < 400ms sobre la mateixa relació)
                Dim nowMs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                If _lastClickRid = rid AndAlso (nowMs - _lastClickTime) < 400 Then
                    ' Doble clic -> obrir editor
                    _lastClickRid = -1
                    _renderer.SelRelacioId = rid
                    _renderer.SelTaulaId = -1
                    ObrirEditorRelacio(rBase)
                    _glControl.Invalidate()
                    Return
                End If
                _lastClickTime = nowMs
                _lastClickRid  = rid
                _renderer.SelTaulaId = -1
                _renderer.SelRelacioId = rid
                MostraPanellRelacio(rBase)
                _glControl.Invalidate()
                Return
            End If
            ' Clic sobre fons buit: netejar grup seleccionat i iniciar rectangle
            _grupSel.Clear()
            _grupPosOld.Clear()
            _selRect = True
            _selStartX = e.X : _selEndX = e.X
            _selStartY = e.Y : _selEndY = e.Y
            _renderer.SelTaulaId = -1
            _renderer.SelRelacioId = -1
            MostraPanellBuit()
            _glControl.Invalidate()
        End If
    End Sub

    Private Sub GL_MouseMove(s As Object, e As MouseEventArgs)
        ' Rubber-band FK actiu
        If _dragFK Then
            _dragFKCursor = e.Location
            ' Detectar taula sota el cursor per destacar-la
            Dim hoverTid As Integer = _renderer.HitTestTaula(e.X, e.Y, _glControl.Width, _glControl.Height, _proyecto)
            If hoverTid <> _dragHoverTid Then
                _dragHoverTid = hoverTid
            End If
            _glControl.Invalidate()
            Return
        End If
        Dim dx As Integer = e.X - _lastMX
        Dim dy As Integer = e.Y - _lastMY

        If _orbit Then
            Dim dRotY As Single = -dx * 0.004F
            Dim dRotX As Single = -dy * 0.004F
            _velX = dRotY   ' per inèrcia en deixar anar

            If _hasPivot Then
                ' Òrbita Sketchup: rotar al voltant del pivot 3D
                ' 1. Desplaçar l'origen perquè el pivot quedi al centre
                ' 2. Aplicar la rotació
                ' 3. Compensar l'offset 2D perquè el pivot projectat quedi al seu lloc
                Dim cx2 As Single = _glControl.Width  / 2.0F
                Dim cy2 As Single = _glControl.Height / 2.0F
                ' Projecció del pivot ABANS de rotar
                Dim px1 As Single, py1 As Single, pz1 As Single
                _renderer.RotPPublic(_pivotX, _pivotY, _pivotZ, px1, py1, pz1)
                Dim screenPivotBefore As New SkiaSharp.SKPoint()
                Dim hadPivot As Boolean = _renderer.PrjPublic(px1, py1, pz1, cx2, cy2, screenPivotBefore)
                ' Rotar
                _renderer.RotY += dRotY
                _renderer.RotX += dRotX
                ' Clamp RotX
                If _renderer.RotX > 1.5F Then _renderer.RotX = 1.5F
                If _renderer.RotX < -1.5F Then _renderer.RotX = -1.5F
                ' Projecció del pivot DESPRÉS de rotar
                Dim px2 As Single, py2 As Single, pz2 As Single
                _renderer.RotPPublic(_pivotX, _pivotY, _pivotZ, px2, py2, pz2)
                Dim screenPivotAfter As New SkiaSharp.SKPoint()
                Dim hadPivot2 As Boolean = _renderer.PrjPublic(px2, py2, pz2, cx2, cy2, screenPivotAfter)
                ' Compensar: ajustar OffX/OffY perquè el pivot projectat quedi al mateix lloc
                If hadPivot AndAlso hadPivot2 Then
                    _renderer.OffX += screenPivotBefore.X - screenPivotAfter.X
                    _renderer.OffY += screenPivotBefore.Y - screenPivotAfter.Y
                End If
            Else
                _renderer.RotY += dRotY
                _renderer.RotX += dRotX
                If _renderer.RotX > 1.5F Then _renderer.RotX = 1.5F
                If _renderer.RotX < -1.5F Then _renderer.RotX = -1.5F
            End If
            _lastMX = e.X
            _lastMY = e.Y
            _glControl.Invalidate()
            Return
        End If

        If _pan Then
            _renderer.OffX += dx
            _renderer.OffY += dy
            _lastMX = e.X
            _lastMY = e.Y
            _glControl.Invalidate()
            Return
        End If

        If _selRect Then
            _selEndX = e.X
            _selEndY = e.Y
            _glControl.Invalidate()
            Return
        End If

        If _dragTaula AndAlso _dragId >= 0 Then
            Dim t As TablaBBDD = Nothing
            For Each x As TablaBBDD In _proyecto.Taules
                If x.Id = _dragId Then t = x
            Next
            If t IsNot Nothing Then
                ' Si la taula és part d'un grup seleccionat, moure tot el grup
                Dim esDelGrup As Boolean = (_grupSel.Count > 0 AndAlso _grupSel.Contains(t.Id))
                ' Guardar posicions originals del grup en el primer frame de drag
                If esDelGrup AndAlso _grupPosOld.Count = 0 Then
                    For Each tg2 As TablaBBDD In _proyecto.Taules
                        If _grupSel.Contains(tg2.Id) Then
                            _grupPosOld(tg2.Id) = New Single() {tg2.PosX, tg2.PosY, tg2.PosZ}
                        End If
                    Next
                End If
                ' Escala: convertir píxels de pantalla a unitats 3D
                Dim fov As Single = 500.0F / _renderer.Zoom
                ' Distància aproximada de la taula al pla de projecció
                Dim cosX As Single = CSng(Math.Cos(_renderer.RotX))
                Dim sinX As Single = CSng(Math.Sin(_renderer.RotX))
                Dim cosY As Single = CSng(Math.Cos(_renderer.RotY))
                Dim sinY As Single = CSng(Math.Sin(_renderer.RotY))
                ' Profunditat aproximada de la taula (ez rotada)
                Dim ez As Single = -t.PosX * sinY +
                                   (t.PosY * sinX + t.PosZ * cosX) * cosY
                Dim dist As Single = fov + ez + 400.0F
                If dist < 10.0F Then dist = 10.0F
                Dim sc As Single = dist / fov   ' unitats 3D per píxel

                ' Vector DRET de pantalla en espai 3D (columna 1 de R = Ry*Rx):
                '   right = (cosY, sinX*sinY, -cosX*sinY)
                ' Vector AMUNT de pantalla en espai 3D (columna 2 de R = Ry*Rx):
                '   up    = (0,    cosX,      -sinX)
                ' Nota: dy pantalla positiu = avall = up negatiu
                Dim rightX As Single =  cosY
                Dim rightY As Single =  sinX * sinY
                Dim rightZ As Single = -cosX * sinY

                Dim upX As Single = 0.0F
                Dim upY As Single =  cosX
                Dim upZ As Single = -sinX

                Dim dxW As Single = CSng(dx) * sc
                Dim dyW As Single = CSng(dy) * sc   ' dy positiu = mou cap avall

                ' Moviment base
                Dim moveX As Single = rightX * dxW - upX * dyW
                Dim moveY As Single = rightY * dxW - upY * dyW
                Dim moveZ As Single = rightZ * dxW - upZ * dyW

                ' Atracció magnètica suau cap a taules relacionades:
                ' Si el vector de moviment apunta cap a una taula relacionada,
                ' amplificar la component en aquella direcció.
                Dim magX As Single = 0.0F
                Dim magY As Single = 0.0F
                Dim magZ As Single = 0.0F
                Const MAG_FACTOR As Single = 0.35F   ' intensitat de l'atracció
                Const MAG_ANGLE  As Double = 0.65    ' cosinus mínim (~49°) per activar

                For Each r As RelacionBBDD In _proyecto.Relacions
                    Dim relId As Integer = -1
                    If r.TablaOrigenId = _dragId Then relId = r.TablaDestinoId
                    If r.TablaDestinoId = _dragId Then relId = r.TablaOrigenId
                    If relId < 0 OrElse relId = _dragId Then Continue For
                    Dim tr As TablaBBDD = Nothing
                    For Each tt As TablaBBDD In _proyecto.Taules
                        If tt.Id = relId Then tr = tt
                    Next
                    If tr Is Nothing Then Continue For

                    ' Vector des de la taula arrossegada cap a la relacionada
                    Dim toX As Single = tr.PosX - t.PosX
                    Dim toY As Single = tr.PosY - t.PosY
                    Dim toZ As Single = tr.PosZ - t.PosZ
                    Dim toLen As Single = CSng(Math.Sqrt(toX*toX + toY*toY + toZ*toZ))
                    If toLen < 0.01F Then Continue For
                    toX /= toLen : toY /= toLen : toZ /= toLen

                    ' Vector de moviment normalitzat
                    Dim mLen As Single = CSng(Math.Sqrt(moveX*moveX + moveY*moveY + moveZ*moveZ))
                    If mLen < 0.001F Then Continue For
                    Dim mNX As Single = moveX/mLen
                    Dim mNY As Single = moveY/mLen
                    Dim mNZ As Single = moveZ/mLen

                    ' Producte escalar (cosinus de l'angle entre moviment i direcció cap a relacionada)
                    Dim dot As Double = mNX*toX + mNY*toY + mNZ*toZ

                    ' Només amplificar si anem aproximadament cap a la taula relacionada
                    If dot > MAG_ANGLE Then
                        Dim amp As Single = CSng(dot - MAG_ANGLE) * MAG_FACTOR * mLen
                        magX += toX * amp
                        magY += toY * amp
                        magZ += toZ * amp
                    End If
                Next

                If esDelGrup Then
                    ' Moure totes les taules del grup
                    For Each tg As TablaBBDD In _proyecto.Taules
                        If _grupSel.Contains(tg.Id) Then
                            tg.PosX += moveX + magX
                            tg.PosY += moveY + magY
                            tg.PosZ += moveZ + magZ
                        End If
                    Next
                Else
                    t.PosX += moveX + magX
                    t.PosY += moveY + magY
                    t.PosZ += moveZ + magZ
                End If
            End If
            _lastMX = e.X
            _lastMY = e.Y
            _glControl.Invalidate()
        End If
    End Sub

    Private Sub GL_MouseUp(s As Object, e As MouseEventArgs)
        ' ── Rubber-band FK: soltar sobre una taula ──────────────
        If _dragFK AndAlso e.Button = MouseButtons.Left Then
            _dragFK = False
            _glControl.Capture = False
            Dim tid As Integer = _renderer.HitTestTaula(e.X, e.Y, _glControl.Width, _glControl.Height, _proyecto)
            If tid >= 0 AndAlso tid <> _dragFKTaulaId Then
                ' Taula actual = filla (conté el camp origen)
                ' Taula destí  = mare (ha de tenir PK per referenciar)
                Dim dlg As New FrmRelationEditor(_proyecto, Nothing, _dragFKTaulaId)
                dlg.PreselectFK(_dragFKCampNom, tid)
                If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.ResultRelacio IsNot Nothing Then
                    Dim ft As TablaBBDD = Nothing
                    For Each t As TablaBBDD In _proyecto.Taules
                        If t.Id = dlg.ResultRelacio.TablaOrigenId Then ft = t
                    Next
                    Dim fk As CampoBBDD = Nothing
                    If ft IsNot Nothing Then
                        For Each f As CampoBBDD In ft.Fields
                            If f.Nombre = dlg.ResultRelacio.CampoFKNombre Then fk = f
                        Next
                    End If
                    CommandStack.Push(New CmdAfegirRelacio(_proyecto.Relacions, dlg.ResultRelacio, fk))
                    MarcarModificat()
                    ActualitzarPanellLateral()
                    RefrescarPanellInferior()   ' el flag FK del camp acaba de canviar
                    _glControl.Invalidate()
                End If
            End If
            _dragFKTaulaId = -1
            _dragFKCampNom = ""
            _dragHoverTid  = -1
            _glControl.Cursor = Cursors.Default   ' restaurar cursor
            _glControl.Invalidate()
            Return
        End If

        If e.Button = MouseButtons.Right Then
            _orbit = False
            _hasPivot = False
        End If

        If e.Button = MouseButtons.Middle Then
            _pan = False
            ' Si no s'ha mogut el ratolí = clic simple = centrar holograma
            Dim midElapsed As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _midDownTime
            If midElapsed < 300 Then   ' menys de 300ms = clic simple = centrar
                ' Reset complet: posició, velocitat, offset
                _renderer.RotX  = 0.2F
                _renderer.RotY  = 0.0F
                _renderer.Zoom  = 1.0F
                _renderer.OffX  = 0.0F
                _renderer.OffY  = 0.0F
                _renderer.VelX  = 0.0F
                _renderer.VelY  = 0.0F
                _velX = 0.0F
                _velY = 0.0F
                _glControl.Invalidate()
                SetStatus(Locale.Str("STATUS_CENTRAT"))
            End If
        End If

        If e.Button = MouseButtons.Left Then
            If _selRect Then
                _selRect = False
                ' Calcular rectangle de selecció en coordenades de pantalla
                Dim rx1 As Integer = Math.Min(_selStartX, _selEndX)
                Dim rx2 As Integer = Math.Max(_selStartX, _selEndX)
                Dim ry1 As Integer = Math.Min(_selStartY, _selEndY)
                Dim ry2 As Integer = Math.Max(_selStartY, _selEndY)
                If rx2 - rx1 > 5 AndAlso ry2 - ry1 > 5 Then
                    ' Trobar totes les taules dins del rectangle
                    ' Recalcular grups ara per garantir dades fresques
                    RecalcularGrupId()
                    Dim taulesSel As New List(Of Integer)()
                    Dim cx3 As Single = _glControl.Width  / 2.0F
                    Dim cy3 As Single = _glControl.Height / 2.0F
                    For Each t As TablaBBDD In _proyecto.Taules
                        Dim ex3 As Single, ey3 As Single, ez3 As Single
                        _renderer.RotPPublic(t.PosX, t.PosY, t.PosZ, ex3, ey3, ez3)
                        Dim pt3 As New SkiaSharp.SKPoint()
                        If Not _renderer.PrjPublic(ex3, ey3, ez3, cx3, cy3, pt3) Then Continue For
                        If pt3.X >= rx1 AndAlso pt3.X <= rx2 AndAlso
                           pt3.Y >= ry1 AndAlso pt3.Y <= ry2 Then
                            taulesSel.Add(t.Id)
                        End If
                    Next

                    ' Filtrar per grup majoritari (Label Propagation): de totes les taules
                    ' dins el rectangle, agafar només les del grupId amb més representants.
                    ' En cas d'empat, guanya el grup amb la taula més endavant (Z major).
                    If taulesSel.Count > 0 Then
                        Dim grupFreq As New Dictionary(Of Integer, Integer)()
                        Dim grupZMax As New Dictionary(Of Integer, Single)()
                        For Each t As TablaBBDD In _proyecto.Taules
                            If Not taulesSel.Contains(t.Id) Then Continue For
                            Dim gid As Integer = If(_taulaGrupId.ContainsKey(t.Id), _taulaGrupId(t.Id), -999)
                            If Not grupFreq.ContainsKey(gid) Then
                                grupFreq(gid) = 0
                                grupZMax(gid) = Single.MinValue
                            End If
                            grupFreq(gid) += 1
                            Dim ez3b As Single, ey3b As Single, ez3bz As Single
                            _renderer.RotPPublic(t.PosX, t.PosY, t.PosZ, ez3b, ey3b, ez3bz)
                            If ez3bz > grupZMax(gid) Then grupZMax(gid) = ez3bz
                        Next

                        ' Escollir el grupId amb més taules; en cas d'empat, el més endavant
                        Dim bestGrupId As Integer = -999
                        Dim bestCount As Integer = -1
                        Dim bestZ As Single = Single.MinValue
                        For Each kv As KeyValuePair(Of Integer, Integer) In grupFreq
                            If kv.Value > bestCount OrElse
                               (kv.Value = bestCount AndAlso grupZMax(kv.Key) > bestZ) Then
                                bestCount = kv.Value
                                bestGrupId = kv.Key
                                bestZ = grupZMax(kv.Key)
                            End If
                        Next

                        ' Recollir NOMÉS les taules del grup majoritari dins el rectangle
                        If bestGrupId <> -999 Then
                            _grupSel.Clear()
                            Dim hubT As TablaBBDD = Nothing
                            Dim hubRel As Integer = -1
                            For Each t As TablaBBDD In _proyecto.Taules
                                Dim gid As Integer = If(_taulaGrupId.ContainsKey(t.Id), _taulaGrupId(t.Id), -999)
                                If gid = bestGrupId AndAlso taulesSel.Contains(t.Id) Then
                                    _grupSel.Add(t.Id)
                                    Dim nRel As Integer = 0
                                    For Each r2 As RelacionBBDD In _proyecto.Relacions
                                        If r2.TablaOrigenId = t.Id OrElse r2.TablaDestinoId = t.Id Then nRel += 1
                                    Next
                                    If nRel > hubRel Then hubRel = nRel : hubT = t
                                End If
                            Next
                            If hubT Is Nothing Then
                                For Each t As TablaBBDD In _proyecto.Taules
                                    If _grupSel.Contains(t.Id) Then hubT = t : Exit For
                                Next
                            End If
                            If hubT IsNot Nothing Then
                                _renderer.SelTaulaId = hubT.Id
                                MostraPanellTaula(hubT)
                                SetStatus(_grupSel.Count & Locale.Str("STATUS_GRUPS_SEL"))
                            End If
                            _glControl.Invalidate()
                        End If
                    End If
                End If
            End If
            If _dragTaula AndAlso _dragId >= 0 Then
                If _grupSel.Count > 0 AndAlso _grupPosOld.Count > 0 Then
                    ' Undo per al drag de grup: registrar una comanda per cada taula.
                    ' Usem PushSilent perquè el moviment ja ha ocorregut durant el drag;
                    ' no volem que Execute() torni a moure les taules.
                    For Each tg3 As TablaBBDD In _proyecto.Taules
                        If _grupSel.Contains(tg3.Id) AndAlso _grupPosOld.ContainsKey(tg3.Id) Then
                            Dim oldPos() As Single = _grupPosOld(tg3.Id)
                            If tg3.PosX <> oldPos(0) OrElse tg3.PosY <> oldPos(1) OrElse tg3.PosZ <> oldPos(2) Then
                                CommandStack.PushSilent(New CmdMoureTaula(tg3,
                                    oldPos(0), oldPos(1), oldPos(2),
                                    tg3.PosX, tg3.PosY, tg3.PosZ))
                            End If
                        End If
                    Next
                    _grupPosOld.Clear()
                Else
                    Dim t As TablaBBDD = Nothing
                    For Each x As TablaBBDD In _proyecto.Taules
                        If x.Id = _dragId Then t = x
                    Next
                    If t IsNot Nothing Then
                        If t.PosX <> _dragOldX OrElse t.PosY <> _dragOldY OrElse t.PosZ <> _dragOldZ Then
                            ' PushSilent: el drag ja ha posicionat la taula, no repetir Execute()
                            CommandStack.PushSilent(New CmdMoureTaula(t,
                                _dragOldX, _dragOldY, _dragOldZ,
                                t.PosX, t.PosY, t.PosZ))
                        End If
                    End If
                End If
            End If
            _dragTaula = False
        End If
    End Sub

    Private Sub GL_MouseWheel(s As Object, e As MouseEventArgs)
        ' Roda AMUNT (Delta > 0) = zoom IN, roda AVALL (Delta < 0) = zoom OUT
        Dim factor As Single = If(e.Delta > 0, 1.0F / 1.15F, 1.15F)
        Dim zoomVell As Single = _renderer.Zoom
        Dim zoomNou  As Single = zoomVell * factor
        If zoomNou < 0.001F Then zoomNou = 0.001F : factor = zoomNou / zoomVell
        If zoomNou > 50.0F  Then zoomNou = 50.0F  : factor = zoomNou / zoomVell

        Dim cx As Single = _glControl.Width  / 2.0F
        Dim cy As Single = _glControl.Height / 2.0F

        ' Compensar OffX/OffY perquè el punt sota el cursor NO es mogui.
        ' La projecció és perspectiva:  s = fov/(fov + rz + 400)  amb  fov = 500/Zoom,
        ' de manera que l'escala de pantalla NO canvia linealment amb el zoom
        ' (l'aproximació 1/factor sobrecompensava i la vista "relliscava").
        ' Es calcula la relació d'escala EXACTA al pla de referència rz = 0
        ' (centre de l'esfera, pivot de rotació):
        '   s_vell = f0/(f0+400)   s_nou = f1/(f1+400)   ratio = s_nou/s_vell
        ' Derivació (screenX = cx + rx*s + OffX, screenX fix):
        '   OffX_nou = OffX_vell + (cursor - cx - OffX_vell) * (1 - ratio)
        Dim f0 As Single = 500.0F / zoomVell
        Dim f1 As Single = 500.0F / zoomNou
        Dim ratio As Single = (f1 * (f0 + 400.0F)) / (f0 * (f1 + 400.0F))
        _renderer.OffX += (e.X - cx - _renderer.OffX) * (1.0F - ratio)
        _renderer.OffY += (e.Y - cy - _renderer.OffY) * (1.0F - ratio)
        _renderer.Zoom = zoomNou
        _glControl.Invalidate()
    End Sub

    ' Panells inferiors
    Private Sub MostraPanellBuit()
        _pnlBuit.Visible = True
        _pnlTaula.Visible = False
        _pnlRelacio.Visible = False
    End Sub

    Private Sub MostraPanellTaula(t As TablaBBDD)
        If t Is Nothing Then MostraPanellBuit() : Return
        _pnlBuit.Visible = False
        _pnlTaula.Visible = True
        _pnlRelacio.Visible = False
        ' Sincronitzar selecció a la llista lateral
        If _pnlListaTaules IsNot Nothing Then
            _tabActiu = 0 : ActualitzarTabsVisuals()
            For Each ctrl As Control In _pnlListaTaules.Controls
                Dim lbl2 As Label = TryCast(ctrl, Label)
                If lbl2 IsNot Nothing Then
                    Dim selec As Boolean = (CInt(lbl2.Tag) = t.Id)
                    lbl2.BackColor = If(selec, AppStyle.ColHover, AppStyle.ColFonsMig)
                    ' Scroll fins al label seleccionat
                    If selec Then
                        Dim clip3 As Panel = TryCast(_pnlListaTaules.Parent, Panel)
                        If clip3 IsNot Nothing Then
                            Dim targetTop As Integer = -(lbl2.Top - clip3.Height \ 2)
                            Dim minTop As Integer = Math.Min(0, clip3.Height - _pnlListaTaules.Height)
                            _pnlListaTaules.Top = Math.Max(minTop, Math.Min(0, targetTop))
                        End If
                    End If
                End If
            Next
        End If
        _lblNomTaula.Text = "  " & t.Schema & "." & t.Nombre
        If _txtDescTaula IsNot Nothing Then _txtDescTaula.Text = If(t.Descripcion IsNot Nothing, t.Descripcion, "")
        If _txtComentariTaula IsNot Nothing Then _txtComentariTaula.Text = If(t.Comentari IsNot Nothing, t.Comentari, "")
        ' Actualitzar label de compte de camps (guardat al Tag)
        Dim lblCC As Label = TryCast(_lblNomTaula.Tag, Label)
        If lblCC IsNot Nothing Then lblCC.Text = t.Fields.Count & Locale.Str("LBL_CAMPS")
        RecarregarLvCamps(t)
    End Sub

    Private Sub MostraPanellRelacio(r As RelacionBBDD)
        If r Is Nothing Then MostraPanellBuit() : Return
        _pnlBuit.Visible = False
        _pnlTaula.Visible = False
        _pnlRelacio.Visible = True
        ' Sincronitzar selecció a la llista lateral
        If _pnlListaRelacions IsNot Nothing Then
            _tabActiu = 1 : ActualitzarTabsVisuals()
            For Each ctrl As Control In _pnlListaRelacions.Controls
                Dim lbl2 As Label = TryCast(ctrl, Label)
                If lbl2 IsNot Nothing Then
                    Dim selec As Boolean = (CInt(lbl2.Tag) = r.Id)
                    lbl2.BackColor = If(selec, AppStyle.ColHover, AppStyle.ColFonsMig)
                    If selec Then
                        Dim clip3 As Panel = TryCast(_pnlListaRelacions.Parent, Panel)
                        If clip3 IsNot Nothing Then
                            Dim targetTop As Integer = -(lbl2.Top - clip3.Height \ 2)
                            Dim minTop As Integer = Math.Min(0, clip3.Height - _pnlListaRelacions.Height)
                            _pnlListaRelacions.Top = Math.Max(minTop, Math.Min(0, targetTop))
                        End If
                    End If
                End If
            Next
        End If
        Dim ft As TablaBBDD = Nothing
        Dim tt As TablaBBDD = Nothing
        For Each t As TablaBBDD In _proyecto.Taules
            If t.Id = r.TablaOrigenId Then ft = t
            If t.Id = r.TablaDestinoId Then tt = t
        Next
        Dim sft As String = If(ft IsNot Nothing, ft.Nombre, "?")
        Dim stt As String = If(tt IsNot Nothing, tt.Nombre, "?")
        _lblInfoRelacio.Text =
            Locale.Str("REL_INFO_REL") & r.Nombre & "  [" & r.CardinalityLabel & "]" & Environment.NewLine &
            Locale.Str("REL_INFO_ORIGEN") & sft & " . " & r.CampoFKNombre & Environment.NewLine &
            Locale.Str("REL_INFO_DESTI") & stt & " . " & r.CampoPKNombre & Environment.NewLine &
            "ON DELETE: " & r.OnDeleteLabel & "   ON UPDATE: " & r.OnUpdateLabel
    End Sub

    Private Sub RecarregarLvCamps(t As TablaBBDD)
        If _flCamps Is Nothing Then Return
        _flCamps.SuspendLayout()
        _flCamps.Controls.Clear()
        _flCamps.Top = 0
        _campSelIdx = -1 : _campClickIdx = -1

        For i As Integer = 0 To t.Fields.Count - 1
            Dim f As CampoBBDD = t.Fields(i)
            Dim iLocal As Integer = i

            ' Colors per fila
            Dim fg As Color = If(f.EsPK, AppStyle.ColPK,
                                 If(f.EsFK, AppStyle.ColFK, AppStyle.ColTextPrinc))
            Dim bg As Color = ColBgCamp(f, i)

            ' Text de la fila
            Dim flags As String = ""
            If f.EsPK Then flags &= "PK "
            If f.EsFK Then flags &= "FK "
            If f.NotNull Then flags &= "NN "
            If f.EsUnique Then flags &= "UQ "
            If f.EsIdentity Then flags &= "ID "

            Dim longStr   As String = If(f.Longitud > 0, f.Longitud.ToString(), "")
            Dim aliasStr  As String = If(Not String.IsNullOrEmpty(f.NomAlias), f.NomAlias, "")
            Dim mascaraStr As String = If(Not String.IsNullOrEmpty(f.Mascara), f.Mascara, "")
            Dim formatStr As String = If(Not String.IsNullOrEmpty(f.FormatDisplay), f.FormatDisplay, "")
            Dim descStr   As String = If(Not String.IsNullOrEmpty(f.Descripcion), f.Descripcion, "")
            Dim txt As String = String.Format("{0,2}  {1,-20} {2,-14} {3,5}  {4,-12} {5,-14} {6,-14} {7,-14} {8}",
                i + 1,
                f.Nombre,
                f.EtiquetaTipus,
                longStr,
                flags.Trim(),
                aliasStr,
                mascaraStr,
                formatStr,
                descStr)

            Dim lbl As New Label()
            lbl.Text = txt
            lbl.Font = AppStyle.FntPetit
            lbl.ForeColor = fg
            lbl.BackColor = bg
            lbl.AutoSize = False
            lbl.Width = Math.Max(900, _flCamps.Width - 4)
            lbl.Height = 17
            lbl.Padding = New Padding(2, 0, 0, 0)
            lbl.Tag = iLocal
            lbl.Cursor = Cursors.Hand
            ' ── Rubber-band FK: mantenir premut >300ms per iniciar drag ──
            ' Clic ràpid = selecció normal / doble clic = editor
            ' Mantenir premut = inicia rubber-band cap a taula destí
            Dim _rbTimer As New System.Windows.Forms.Timer()
            _rbTimer.Interval = 300
            Dim _rbPending As Boolean = False

            AddHandler lbl.MouseDown, Sub(s As Object, ev As MouseEventArgs)
                If ev.Button <> MouseButtons.Left Then Return
                Dim tRB As TablaBBDD = Nothing
                For Each x As TablaBBDD In _proyecto.Taules
                    If x.Id = _renderer.SelTaulaId Then tRB = x : Exit For
                Next
                If tRB Is Nothing Then Return
                Dim fi2 As Integer = CInt(DirectCast(s, Label).Tag)
                If fi2 < 0 OrElse fi2 >= tRB.Fields.Count Then Return
                _dragFKTaulaId = tRB.Id
                _dragFKCampNom = tRB.Fields(fi2).Nombre
                _rbPending = True
                _rbTimer.Start()
            End Sub

            AddHandler lbl.MouseUp, Sub(s As Object, ev As MouseEventArgs)
                ' Deixar anar abans dels 300ms = clic normal, cancel·lar drag
                _rbTimer.Stop()
                If _rbPending Then
                    _rbPending = False
                    _dragFK = False
                    _dragFKTaulaId = -1
                    _dragFKCampNom = ""
                End If
            End Sub

            AddHandler _rbTimer.Tick, Sub(s As Object, ev As EventArgs)
                _rbTimer.Stop()
                _rbPending = False
                If _dragFKTaulaId < 0 Then Return
                ' 300ms premut → activar rubber-band
                _dragFK   = True
                _dragEsPK = False
                ' Cursor invisible mentre dura el rubber-band
                _glControl.Cursor = _cursorBuit
                lbl.Capture = False
                _glControl.Capture = True
            End Sub

            AddHandler lbl.DoubleClick, Sub(s As Object, ev As EventArgs)
                _rbTimer.Stop()
                _rbPending = False
                _dragFK = False
                Dim tsel As TablaBBDD = Nothing
                For Each x As TablaBBDD In _proyecto.Taules
                    If x.Id = _renderer.SelTaulaId Then tsel = x
                Next
                If tsel Is Nothing Then Return
                Dim fi As Integer = CInt(DirectCast(s, Label).Tag)
                If fi >= 0 AndAlso fi < tsel.Fields.Count Then
                    ObrirEditorCamp(tsel, fi)
                End If
            End Sub

            AddHandler lbl.Click, Sub(s As Object, ev As EventArgs)
                Dim fi2 As Integer = CInt(DirectCast(s, Label).Tag)
                _campClickIdx = fi2
            End Sub

            AddHandler lbl.MouseEnter, Sub(s As Object, ev As EventArgs)
                _lateralSelIdx = -1
                Dim fi2 As Integer = CInt(DirectCast(s, Label).Tag)
                If _campSelIdx >= 0 AndAlso _campSelIdx <> fi2 AndAlso
                   _campSelIdx < _flCamps.Controls.Count Then
                    Dim lAnt As Label = TryCast(_flCamps.Controls(_campSelIdx), Label)
                    If lAnt IsNot Nothing Then
                        Dim tAct As TablaBBDD = Nothing
                        For Each xx As TablaBBDD In _proyecto.Taules
                            If xx.Id = _renderer.SelTaulaId Then tAct = xx : Exit For
                        Next
                        If tAct IsNot Nothing AndAlso _campSelIdx < tAct.Fields.Count Then
                            lAnt.BackColor = ColBgCamp(tAct.Fields(_campSelIdx), _campSelIdx)
                        End If
                    End If
                End If
                _campSelIdx = fi2
                DirectCast(s, Label).BackColor = AppStyle.ColHover
            End Sub
            AddHandler lbl.MouseLeave, Sub(s As Object, ev As EventArgs)
                Dim fi2 As Integer = CInt(DirectCast(s, Label).Tag)
                If fi2 = _campSelIdx Then _campSelIdx = -1 : _campClickIdx = -1
                Dim tActiva As TablaBBDD = Nothing
                For Each x As TablaBBDD In _proyecto.Taules
                    If x.Id = _renderer.SelTaulaId Then tActiva = x : Exit For
                Next
                If tActiva Is Nothing OrElse fi2 < 0 OrElse fi2 >= tActiva.Fields.Count Then Return
                DirectCast(s, Label).BackColor = ColBgCamp(tActiva.Fields(fi2), fi2)
            End Sub
            _flCamps.Controls.Add(lbl)
        Next i

        _flCamps.Height = t.Fields.Count * 17 + 4
        _flCamps.ResumeLayout()
    End Sub

    Private Sub LvCamps_DblClick(s As Object, e As EventArgs)
        ' El doble clic ara es gestiona als labels de _flCamps
    End Sub

    ' Menu handlers
    Private Sub MnuNou_Click(s As Object, e As EventArgs)
        If Not ConfirmarDescartarCanvis() Then Return
        _proyecto = ProyectoBBDD.NouProjecte()
        _rutaFitxer = ""
        _modificat = False
        CommandStack.Clear()
        SphereRenderer.DistribuirTaules(_proyecto)
        MostraPanellBuit()
        _glControl.Invalidate()
        ActualitzarTitol()
        SetStatus(Locale.Str("STATUS_NOU"))
    End Sub

    Private Sub MnuObrir_Click(s As Object, e As EventArgs)
        If Not ConfirmarDescartarCanvis() Then Return
        Using dlg As New OpenFileDialog()
            dlg.Filter = Locale.Str("DLG_FILTRE_HDB")
            If dlg.ShowDialog() = DialogResult.OK Then
                Try
                    _proyecto = ProjectSerializer.Carregar(dlg.FileName)
                    _rutaFitxer = dlg.FileName
                    _modificat = False
                    CommandStack.Clear()
                    SphereRenderer.DistribuirTaules(_proyecto)
                    MostraPanellBuit()
                    ActualitzarPanellLateral()
                    _glControl.Invalidate()
                    ActualitzarTitol()
                    SetStatus(Locale.Str("STATUS_CARREGAT") & IO.Path.GetFileName(dlg.FileName))
                Catch ex As Exception
                    MessageBox.Show(Locale.Str("ERR_PREFIX") & ex.Message, Locale.Str("DLG_ERROR"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

    Private Sub MnuDesar_Click(s As Object, e As EventArgs)
        If String.IsNullOrEmpty(_rutaFitxer) Then
            MnuDesarCom_Click(s, e)
            Return
        End If
        Try
            ' Guardar estat de la càmera al projecte
            _proyecto.CameraRotX = _renderer.RotX
            _proyecto.CameraRotY = _renderer.RotY
            _proyecto.CameraZoom = _renderer.Zoom
            _proyecto.CameraOffX = _renderer.OffX
            _proyecto.CameraOffY = _renderer.OffY
            ProjectSerializer.Desar(_proyecto, _rutaFitxer)
            _modificat = False
            ActualitzarTitol()
            SetStatus(Locale.Str("STATUS_DESAT") & DateTime.Now.ToString("HH:mm:ss"))
        Catch ex As Exception
            MessageBox.Show(Locale.Str("ERR_PREFIX") & ex.Message, Locale.Str("DLG_ERROR"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub MnuDesarCom_Click(s As Object, e As EventArgs)
        Using dlg As New SaveFileDialog()
            dlg.Filter = "Holografic DB (*.hdb)|*.hdb"
            dlg.FileName = _proyecto.Nombre
            If dlg.ShowDialog() = DialogResult.OK Then
                _rutaFitxer = dlg.FileName
                MnuDesar_Click(s, e)
                ActualitzarTitol()
            End If
        End Using
    End Sub

    Private Sub MnuExport_Click(s As Object, e As EventArgs)
        Using dlg As New SaveFileDialog()
            dlg.Filter = "SQL Script (*.sql)|*.sql"
            dlg.FileName = _proyecto.Nombre & ".sql"
            If dlg.ShowDialog() = DialogResult.OK Then
                Dim sql As String
                Select Case _proyecto.MotorSQL
                    Case "MySQL"      : sql = New MySqlExporter().Generar(_proyecto)
                    Case "PostgreSQL" : sql = New PostgreSqlExporter().Generar(_proyecto)
                    Case Else         : sql = New TSqlExporter().Generar(_proyecto)
                End Select
                EscriureScriptSql(dlg.FileName, sql)
            End If
        End Using
    End Sub

    Private Sub MnuExportMysql_Click(s As Object, e As EventArgs)
        Using dlg As New SaveFileDialog()
            dlg.Filter = "SQL Script (*.sql)|*.sql"
            dlg.FileName = _proyecto.Nombre & "_mysql.sql"
            If dlg.ShowDialog() = DialogResult.OK Then
                Dim sql As String = New MySqlExporter().Generar(_proyecto)
                EscriureScriptSql(dlg.FileName, sql)
            End If
        End Using
    End Sub

    Private Sub MnuExportPg_Click(s As Object, e As EventArgs)
        Using dlg As New SaveFileDialog()
            dlg.Filter = "SQL Script (*.sql)|*.sql"
            dlg.FileName = _proyecto.Nombre & "_postgresql.sql"
            If dlg.ShowDialog() = DialogResult.OK Then
                Dim sql As String = New PostgreSqlExporter().Generar(_proyecto)
                EscriureScriptSql(dlg.FileName, sql)
            End If
        End Using
    End Sub

    ' Desa un script SQL generat i informa de l'error si no es pot escriure
    Private Sub EscriureScriptSql(ruta As String, sql As String)
        Try
            IO.File.WriteAllText(ruta, sql, System.Text.Encoding.UTF8)
            SetStatus(Locale.Str("STATUS_EXPORTAT") & IO.Path.GetFileName(ruta))
        Catch ex As Exception
            MessageBox.Show(Locale.Str("ERR_PREFIX") & ex.Message, Locale.Str("DLG_ERROR"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Sub MnuImportMdf_Click(s As Object, e As EventArgs)
        ' Reutilitza el flux existent d'importació .mdf
        ' (substitueix el projecte actual: cal confirmar si hi ha canvis)
        If Not ConfirmarDescartarCanvis() Then Return
        Dim lastDir As String = If(String.IsNullOrEmpty(_rutaFitxer),
                                   Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                   IO.Path.GetDirectoryName(_rutaFitxer))
        Using dlg As New OpenFileDialog()
            dlg.Filter = Locale.Str("DLG_FILTRE_MDF")
            dlg.InitialDirectory = lastDir
            dlg.Title = Locale.Str("MNU_IMPORTAR_MDF")
            If dlg.ShowDialog() = DialogResult.OK Then
                Try
                    Dim ruta As String = dlg.FileName
                    Dim proj As ProyectoBBDD = Nothing
                    Await OperacioLlarga.ExecutarAsync(Me, Sub() proj = MdfImporter.Importar(ruta))
                    If proj IsNot Nothing Then
                        _proyecto = proj
                        _rutaFitxer = ""
                        CommandStack.Clear()
                        MarcarModificat()
                        ActualitzarPanellLateral()
                        _glControl.Invalidate()
                        SetStatus(Locale.Str("STATUS_IMPORTAT") & IO.Path.GetFileName(dlg.FileName))
                    End If
                Catch ex As Exception
                    MessageBox.Show(ex.Message, Locale.Str("ERR_IMPORT_MDF_TITOL"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

    Private Async Sub MnuExportMdf_Click(s As Object, e As EventArgs)
        Dim lastDir As String = If(String.IsNullOrEmpty(_rutaFitxer),
                                   Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                   IO.Path.GetDirectoryName(_rutaFitxer))
        Using dlg As New FrmExportMdf(_proyecto.Nombre, lastDir)
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                Dim res As MdfExporter.ExportResult = Nothing
                Dim dir As String = dlg.MdfDir, nomBd As String = dlg.DbName
                Dim mode As MdfExporter.ExportMode = dlg.Mode
                Await OperacioLlarga.ExecutarAsync(Me, Sub() res = MdfExporter.Exportar(dir, nomBd, _proyecto, mode))
                If res.OK Then
                    SetStatus(Locale.Str("STATUS_EXPORTAT_MDF") & res.MdfPath)
                    MessageBox.Show(
                        Locale.Str("DLG_EXPORT_OK") & Environment.NewLine &
                        Locale.Str("DLG_EXPORT_TAULES") & res.TaulesCreades & Environment.NewLine &
                        Locale.Str("DLG_EXPORT_RELS") & res.RelacionsCreades & Environment.NewLine &
                        Locale.Str("DLG_EXPORT_FITXER") & res.MdfPath,
                        Locale.Str("DLG_EXPORT_MDF_TITOL"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information)
                Else
                    MessageBox.Show(Locale.Str("ERR_EXPORT_MDF") & Environment.NewLine & res.MissatgeError,
                                    Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                End If
            End If
        End Using
    End Sub

    ' ── Consultar les dades d'una BD existent (fitxer o servidor) ─
    Private Sub MnuConsultarDades_Click(s As Object, e As EventArgs)
        Using dlg As New FrmOrigenBD(_connServidor)
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            If dlg.ConnexioServidor IsNot Nothing Then _connServidor = dlg.ConnexioServidor
            Using frm As New FrmConsultaDades(dlg.Resultat)
                frm.ShowDialog(Me)
            End Using
        End Using
    End Sub

    ' ── Copiar una BD sencera (estructura, objectes i dades) ─────
    Private Sub MnuCopiarBD_Click(s As Object, e As EventArgs)
        Using frm As New FrmCopiaBD(Nothing, _connServidor)
            frm.ShowDialog(Me)
        End Using
    End Sub

    ' ── Connectar a servidor SQL Server ─────────────────────────
    Private Sub MnuServidor_Click(s As Object, e As EventArgs)
        Using dlg As New FrmConnectServer(_connServidor)
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                _connServidor = dlg.ResultConnexio
                SetStatus(Locale.Str("STATUS_CONNECTAT") & _connServidor.ToString())
                ' Actualitzar l'ítem del menú per reflectir l'estat
                ActualitzarMenuServidor()
            End If
        End Using
    End Sub

    ' ── Accions al servidor (importar / exportar / enviar parts) ─
    Private Sub MnuAccionsServidor_Click(s As Object, e As EventArgs)
        If _connServidor Is Nothing Then
            ' Obrir primer el diàleg de connexió
            Using dlgConn As New FrmConnectServer(Nothing)
                If dlgConn.ShowDialog(Me) <> DialogResult.OK Then Return
                _connServidor = dlgConn.ResultConnexio
                SetStatus(Locale.Str("STATUS_CONNECTAT") & _connServidor.ToString())
                ActualitzarMenuServidor()
            End Using
        End If

        Using dlg As New FrmServerActions(_connServidor, _proyecto)
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            ' Processar el resultat de la importació si és el cas
            If dlg.AccioSeleccionada = FrmServerActions.Accio.Importar Then
                Dim modeImp As FrmImportSql.ImportMode =
                    If(TypeOf dlg.Tag Is FrmImportSql.ImportMode,
                       DirectCast(dlg.Tag, FrmImportSql.ImportMode),
                       FrmImportSql.ImportMode.NouProjecte)
                If modeImp = FrmImportSql.ImportMode.AfegirAlActual Then
                    ' Merge al projecte actual
                    Dim nomAId As New Dictionary(Of String, Integer)()
                    For Each tx As TablaBBDD In _proyecto.Taules
                        If Not nomAId.ContainsKey(tx.Nombre) Then nomAId(tx.Nombre) = tx.Id
                    Next
                    Dim parserAReal As New Dictionary(Of Integer, Integer)()
                    For Each t As TablaBBDD In dlg.TaulesSeleccionades
                        Dim idParser As Integer = t.Id
                        If _proyecto.Taules.Exists(Function(x) x.Nombre = t.Nombre) Then
                            parserAReal(idParser) = nomAId(t.Nombre)
                        Else
                            Dim idNou As Integer = _proyecto.GetNextTableId()
                            t.Id = idNou
                            parserAReal(idParser) = idNou
                            nomAId(t.Nombre) = idNou
                            _proyecto.Taules.Add(t)
                        End If
                    Next
                    For Each r As RelacionBBDD In dlg.RelacionsSeleccionades
                        If _proyecto.Relacions.Exists(Function(x) x.Nombre = r.Nombre) Then Continue For
                        If parserAReal.ContainsKey(r.TablaOrigenId)  Then r.TablaOrigenId  = parserAReal(r.TablaOrigenId)
                        If parserAReal.ContainsKey(r.TablaDestinoId) Then r.TablaDestinoId = parserAReal(r.TablaDestinoId)
                        r.Id = _proyecto.GetNextRelId()
                        _proyecto.Relacions.Add(r)
                    Next
                    SphereRenderer.DistribuirTaules(_proyecto)
                    _proyecto.RecalcularIds()
                Else
                    ' Nou projecte
                    Dim p As New ProyectoBBDD()
                    p.Nombre   = _connServidor.BaseDades
                    p.MotorSQL = "T-SQL"
                    Dim mapId As New Dictionary(Of Integer, Integer)()
                    Dim idT As Integer = 1
                    For Each t As TablaBBDD In dlg.TaulesSeleccionades
                        mapId(t.Id) = idT : t.Id = idT : idT += 1
                        p.Taules.Add(t)
                    Next
                    Dim idR As Integer = 1
                    For Each r As RelacionBBDD In dlg.RelacionsSeleccionades
                        r.Id = idR : idR += 1
                        If mapId.ContainsKey(r.TablaOrigenId)  Then r.TablaOrigenId  = mapId(r.TablaOrigenId)
                        If mapId.ContainsKey(r.TablaDestinoId) Then r.TablaDestinoId = mapId(r.TablaDestinoId)
                        p.Relacions.Add(r)
                    Next
                    p.RecalcularIds()
                    SphereRenderer.DistribuirTaules(p)
                    _proyecto = p
                    _rutaFitxer = ""
                    CommandStack.Clear()
                End If
                _glControl.Invalidate()
                ActualitzarPanellLateral()
                SetStatus(Locale.Str("STATUS_IMPORT_SRV") & _proyecto.Taules.Count &
                          Locale.Str("DLG_IMPORT_TAULES") & _proyecto.Relacions.Count & Locale.Str("DLG_IMPORT_RELACIONS"))
            End If
        End Using
    End Sub

    ' ── Actualitza l'ítem de menú del servidor ───────────────────
    Private Sub ActualitzarMenuServidor()
        If _menuStrip Is Nothing Then Return
        For Each top As ToolStripItem In _menuStrip.Items
            Dim tmi As ToolStripMenuItem = TryCast(top, ToolStripMenuItem)
            If tmi Is Nothing Then Continue For
            For Each sub_ As ToolStripItem In tmi.DropDownItems
                Dim smi As ToolStripMenuItem = TryCast(sub_, ToolStripMenuItem)
                If smi IsNot Nothing AndAlso smi.Text.StartsWith(Locale.Str("MNU_SERVIDOR").Split(" "c)(0)) Then
                    If _connServidor IsNot Nothing Then
                        smi.Text      = "✔ " & _connServidor.Servidor & " / " & _connServidor.BaseDades
                        smi.ForeColor = AppStyle.ColAccentSec
                    Else
                        smi.Text      = Locale.Str("MNU_SERVIDOR")
                        smi.ForeColor = AppStyle.ColTextPrinc
                    End If
                End If
            Next
        Next
    End Sub

    Private Sub MnuImportSql_Click(s As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Filter   = Locale.Str("DLG_FILTRE_SQL")
            dlg.Title    = Locale.Str("DLG_IMPORTSQL_TITOL")
            dlg.FileName = ""
            If dlg.ShowDialog() <> DialogResult.OK Then Return
            Dim ruta As String = dlg.FileName
            Try
                Dim sql As String = IO.File.ReadAllText(ruta, System.Text.Encoding.UTF8)
                ' IDs base per al merge: continuar des dels màxims existents
                Dim idBase  As Integer = _proyecto.NextTableId
                Dim relBase As Integer = _proyecto.NextRelId
                Dim parsed As SqlScriptImporter.ImportResult =
                    SqlScriptImporter.Importar(sql, idBase, relBase)
                If parsed.Taules.Count = 0 Then
                    MessageBox.Show(Locale.Str("DLG_CAP_TAULA_MSG"),
                                    Locale.Str("DLG_CAP_TAULA_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
                Using fImp As New FrmImportSql(ruta, parsed, _proyecto)
                    If fImp.ShowDialog(Me) <> DialogResult.OK Then Return
                    If fImp.ModeSeleccionat = FrmImportSql.ImportMode.AfegirAlActual Then
                        ' ── MERGE: afegir al projecte actual ────────────────
                        ' Pas 1: mapa nomTaula→Id de TOTES les taules existents al projecte
                        Dim nomAId As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                        For Each tx As TablaBBDD In _proyecto.Taules
                            If Not nomAId.ContainsKey(tx.Nombre) Then nomAId(tx.Nombre) = tx.Id
                        Next

                        ' Pas 2: mapa idParser→idReal per a TOTES les taules del parser
                        '  (tant les seleccionades com les no seleccionades, per resoldre FK)
                        Dim parserAReal As New Dictionary(Of Integer, Integer)()
                        For Each pt As TablaBBDD In parsed.Taules
                            If nomAId.ContainsKey(pt.Nombre) Then
                                ' La taula ja existeix al projecte: mapem directament
                                parserAReal(pt.Id) = nomAId(pt.Nombre)
                            End If
                        Next

                        ' Pas 3: afegir NOMÉS les taules seleccionades que no existeixin
                        Dim afegides As Integer = 0
                        For Each t As TablaBBDD In fImp.ResultTaules
                            Dim idParser As Integer = t.Id
                            If nomAId.ContainsKey(t.Nombre) Then
                                ' Ja existeix — assegurar que el mapa la té
                                If Not parserAReal.ContainsKey(idParser) Then
                                    parserAReal(idParser) = nomAId(t.Nombre)
                                End If
                            Else
                                ' Nova: incrementar correctament amb GetNextTableId()
                                Dim idNou As Integer = _proyecto.GetNextTableId()
                                t.Id = idNou
                                parserAReal(idParser) = idNou
                                nomAId(t.Nombre) = idNou
                                _proyecto.Taules.Add(t)
                                afegides += 1
                            End If
                        Next

                        ' Pas 4: afegir relacions resolent els IDs via parserAReal
                        Dim relAfeg As Integer = 0
                        For Each r As RelacionBBDD In fImp.ResultRelacions
                            ' Resoldre IDs PRIMER
                            Dim origenResolt As Integer = r.TablaOrigenId
                            Dim destiResolt  As Integer = r.TablaDestinoId
                            If parserAReal.ContainsKey(r.TablaOrigenId)  Then origenResolt = parserAReal(r.TablaOrigenId)
                            If parserAReal.ContainsKey(r.TablaDestinoId) Then destiResolt  = parserAReal(r.TablaDestinoId)
                            ' Saltar duplicats per nom O per combinació taula+camp
                            Dim esDup As Boolean = _proyecto.Relacions.Exists(
                                Function(x) x.Nombre = r.Nombre OrElse
                                            (x.TablaOrigenId  = origenResolt AndAlso
                                             x.TablaDestinoId = destiResolt  AndAlso
                                             x.CampoFKNombre  = r.CampoFKNombre))
                            If esDup Then Continue For
                            r.TablaOrigenId  = origenResolt
                            r.TablaDestinoId = destiResolt
                            r.Id = _proyecto.GetNextRelId()
                            _proyecto.Relacions.Add(r)
                            relAfeg += 1
                        Next

                        ' Pas 5: distribuir posicions 3D per les taules noves
                        SphereRenderer.DistribuirTaules(_proyecto)
                        _proyecto.RecalcularIds()
                        MarcarModificat()
                        _glControl.Invalidate()
                        ActualitzarPanellLateral()
                        SetStatus(String.Format(Locale.Str("STATUS_MERGE_FMT"), afegides, relAfeg))
                    Else
                        ' ── NOU PROJECTE: substituir ─────────────────────────
                        Dim p As New ProyectoBBDD()
                        p.Nombre   = IO.Path.GetFileNameWithoutExtension(ruta)
                        p.MotorSQL = "T-SQL"
                        ' Pas 1: afegir les taules seleccionades amb IDs nous correlatius
                        Dim mapId As New Dictionary(Of Integer, Integer)()
                        Dim idT As Integer = 1
                        For Each t As TablaBBDD In fImp.ResultTaules
                            mapId(t.Id) = idT
                            t.Id = idT : idT += 1
                            p.Taules.Add(t)
                        Next
                        ' Pas 2: completar el mapa amb les taules del parser NO seleccionades
                        '  (per resoldre FK que les referencien)
                        For Each pt As TablaBBDD In parsed.Taules
                            If Not mapId.ContainsKey(pt.Id) Then
                                ' Buscar si hi ha una taula seleccionada amb el mateix nom
                                Dim trobada As TablaBBDD = fImp.ResultTaules.Find(
                                    Function(x) x.Nombre = pt.Nombre)
                                If trobada IsNot Nothing Then
                                    mapId(pt.Id) = trobada.Id
                                End If
                            End If
                        Next
                        ' Pas 3: afegir relacions resolent IDs
                        Dim idR As Integer = 1
                        For Each r As RelacionBBDD In fImp.ResultRelacions
                            Dim origenNou As Integer = r.TablaOrigenId
                            Dim destiNou  As Integer = r.TablaDestinoId
                            If mapId.ContainsKey(r.TablaOrigenId)  Then origenNou = mapId(r.TablaOrigenId)
                            If mapId.ContainsKey(r.TablaDestinoId) Then destiNou  = mapId(r.TablaDestinoId)
                            r.Id = idR : idR += 1
                            r.TablaOrigenId  = origenNou
                            r.TablaDestinoId = destiNou
                            p.Relacions.Add(r)
                        Next
                        p.RecalcularIds()
                        SphereRenderer.DistribuirTaules(p)
                        _proyecto   = p
                        _rutaFitxer = ""
                        CommandStack.Clear()
                        MarcarModificat()
                        _glControl.Invalidate()
                        ActualitzarPanellLateral()
                        SetStatus(Locale.Str("STATUS_IMPORTAT") & p.Taules.Count & Locale.Str("DLG_IMPORT_TAULES") & p.Relacions.Count & Locale.Str("DLG_IMPORT_RELACIONS"))
                    End If
                End Using
            Catch ex As Exception
                MessageBox.Show(Locale.Str("ERR_IMPORT_FITXER") & Environment.NewLine & ex.Message,
                                Locale.Str("DLG_ERROR_TITOL"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub MnuNovaTaula_Click(s As Object, e As EventArgs)
        NovaTaula(False)
    End Sub

    Private Sub MnuNovaMatriu_Click(s As Object, e As EventArgs)
        NovaTaula(True)
    End Sub

    Private Sub MnuNovaRelacio_Click(s As Object, e As EventArgs)
        ObrirEditorRelacio(Nothing)
    End Sub

    Private Sub MnuDDL_Click(s As Object, e As EventArgs)
        Dim sql As String
        Select Case _proyecto.MotorSQL
            Case "MySQL"
                sql = New MySqlExporter().Generar(_proyecto)
            Case "PostgreSQL"
                sql = New PostgreSqlExporter().Generar(_proyecto)
            Case Else   ' "T-SQL" i qualsevol altre → T-SQL per defecte
                sql = New TSqlExporter().Generar(_proyecto)
        End Select
        Dim f As New FrmDDLPreview(sql)
        f.ShowDialog(Me)
    End Sub

    Private Sub MnuValidar_Click(s As Object, e As EventArgs)
        Dim errs As List(Of String) = IntegrityEngine.ValidarModelComplet(_proyecto)
        If errs.Count = 0 Then
            MessageBox.Show(Locale.Str("DLG_MODEL_OK"), Locale.Str("DLG_VALIDACIO_OK"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        Else
            MessageBox.Show(String.Join(Environment.NewLine, errs),
                            errs.Count & Locale.Str("DLG_VAL_ERRORS"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Sub MnuToggleEsfera_Click(s As Object, e As EventArgs)
        _renderer.ShowSphere = Not _renderer.ShowSphere
        ' Sincronitzar amb opcions persistides
        FrmOpcions.Opcions.MostrarEsfera = _renderer.ShowSphere
        FrmOpcions.Opcions.Desar()
        ' Actualitzar el text del menú
        Dim item As ToolStripMenuItem = TryCast(s, ToolStripMenuItem)
        If item IsNot Nothing Then
            item.Text = If(_renderer.ShowSphere, Locale.Str("MNU_ESFERA_ON"), Locale.Str("MNU_ESFERA_OFF"))
        End If
        _glControl.Invalidate()
        SetStatus(Locale.Str("STATUS_ESFERA_" & If(_renderer.ShowSphere, "ON", "OFF")))
    End Sub

    Private Sub MnuToggleLateral_Click(s As Object, e As EventArgs)
        TogglePanellLateral()
    End Sub

    Private Sub BtnTancarLateral_Click(s As Object, e As EventArgs)
        TogglePanellLateral()
    End Sub

    Private Sub TogglePanellLateral()
        If _pnlLateral Is Nothing Then Return
        Dim visible As Boolean = Not _pnlLateral.Visible
        _pnlLateral.Visible = visible
        ' Actualitzar text del menú
        If _mniLateral IsNot Nothing Then
            _mniLateral.Text = If(visible, Locale.Str("MNU_LLISTA_ON"), Locale.Str("MNU_LLISTA_OFF"))
        End If
        ' Reajustar posició i mida del glControl
        Const AmpLat As Integer = 220
        Dim xStart As Integer = If(visible, AmpLat, 0)
        If _glControl IsNot Nothing Then
            _glControl.Location = New Point(xStart, 22)
            _glControl.Size = New Size(Me.ClientSize.Width - xStart,
                                       Me.ClientSize.Height - 22 - AppStyle.AltPanellInf)
            _glControl.Invalidate()
        End If
        SetStatus(Locale.Str("STATUS_LLISTA_" & If(visible, "ON", "OFF")))
    End Sub

    Private Sub MnuUndo_Click(s As Object, e As EventArgs)
        CommandStack.UndoLast()
        ' No cridem DistribuirTaules: respectem les posicions restaurades per la comanda Undo
        ActualitzarPanellLateral()
        RefrescarPanellInferior()
        _glControl.Invalidate()
        SetStatus(Locale.Str("STATUS_DESFER") & CommandStack.LastDesc)
    End Sub

    Private Sub MnuRedo_Click(s As Object, e As EventArgs)
        CommandStack.RedoLast()
        ' No cridem DistribuirTaules: respectem les posicions restaurades per la comanda Redo
        ActualitzarPanellLateral()
        RefrescarPanellInferior()
        _glControl.Invalidate()
        SetStatus(Locale.Str("STATUS_REFER"))
    End Sub

    ' Botons panell inferior
    Private Sub BtnNovaTaula_Click(s As Object, e As EventArgs)
        NovaTaula(False)
    End Sub

    Private Sub BtnNovaMatriu_Click(s As Object, e As EventArgs)
        NovaTaula(True)
    End Sub

    Private Sub BtnNovaRelacio_Click(s As Object, e As EventArgs)
        ObrirEditorRelacio(Nothing)
    End Sub

    Private Sub BtnAfegirCamp_Click(s As Object, e As EventArgs)
        Dim t As TablaBBDD = Nothing
        For Each x As TablaBBDD In _proyecto.Taules
            If x.Id = _renderer.SelTaulaId Then t = x
        Next
        If t Is Nothing Then Return
        ObrirEditorCamp(t, -1)
    End Sub

    Private Sub BtnNouaRelacio_Click(s As Object, e As EventArgs)
        ObrirEditorRelacio(Nothing, _renderer.SelTaulaId)
    End Sub

    Private Sub BtnAgrupar_Click(s As Object, e As EventArgs)
        ' Agrupa les taules directament relacionades al voltant de la seleccionada
        Dim selId As Integer = _renderer.SelTaulaId
        If selId < 0 Then Return
        Dim centre As TablaBBDD = Nothing
        For Each t As TablaBBDD In _proyecto.Taules
            If t.Id = selId Then centre = t
        Next
        If centre Is Nothing Then Return

        ' Recollir IDs de taules relacionades
        Dim relIds As New List(Of Integer)()
        For Each rel2 As RelacionBBDD In _proyecto.Relacions
            If rel2.TablaOrigenId = selId AndAlso rel2.TablaDestinoId <> selId Then
                If Not relIds.Contains(rel2.TablaDestinoId) Then relIds.Add(rel2.TablaDestinoId)
            End If
            If rel2.TablaDestinoId = selId AndAlso rel2.TablaOrigenId <> selId Then
                If Not relIds.Contains(rel2.TablaOrigenId) Then relIds.Add(rel2.TablaOrigenId)
            End If
        Next

        If relIds.Count = 0 Then
            SetStatus(Locale.Str("STATUS_SENSE_REL"))
            Return
        End If

        ' Calcular el radi de distribució al voltant del centre
        Dim R As Single = CSng(Math.Sqrt(centre.PosX * centre.PosX +
                                          centre.PosY * centre.PosY +
                                          centre.PosZ * centre.PosZ))
        If R < 50 Then R = 200.0F
        ' Distància entre la taula centre i les relacionades (20% del radi)
        Dim clusterR As Single = R * 0.22F

        ' Normalitzar la direcció del centre
        Dim nx As Single = If(R > 0.01F, centre.PosX / R, 0)
        Dim ny As Single = If(R > 0.01F, centre.PosY / R, 1)
        Dim nz As Single = If(R > 0.01F, centre.PosZ / R, 0)

        ' Construir dos vectors perpendiculars al vector del centre (base ortonormal)
        Dim tx As Single, ty As Single, tz As Single
        If Math.Abs(nx) < 0.9F Then
            ' cross(n, X)
            tx = 0 : ty = nz : tz = -ny
        Else
            ' cross(n, Y)
            tx = -nz : ty = 0 : tz = nx
        End If
        Dim tLen As Single = CSng(Math.Sqrt(tx*tx + ty*ty + tz*tz))
        tx /= tLen : ty /= tLen : tz /= tLen
        ' segon vector perpendicular: cross(n, t)
        Dim bx As Single = ny*tz - nz*ty
        Dim by As Single = nz*tx - nx*tz
        Dim bz As Single = nx*ty - ny*tx

        ' Distribuir les taules relacionades en un cercle al voltant del centre
        Dim n As Integer = relIds.Count
        For i As Integer = 0 To n - 1
            Dim tid As Integer = relIds(i)
            Dim t2 As TablaBBDD = Nothing
            For Each tt As TablaBBDD In _proyecto.Taules
                If tt.Id = tid Then t2 = tt
            Next
            If t2 Is Nothing Then Continue For
            Dim angle As Single = CSng(2.0 * Math.PI * i / n)
            Dim cos2 As Single = CSng(Math.Cos(angle))
            Dim sin2 As Single = CSng(Math.Sin(angle))
            ' Posició en el pla tangent al voltant del centre
            t2.PosX = centre.PosX + (tx*cos2 + bx*sin2) * clusterR
            t2.PosY = centre.PosY + (ty*cos2 + by*sin2) * clusterR
            t2.PosZ = centre.PosZ + (tz*cos2 + bz*sin2) * clusterR
            ' Projectar de nou a la superfície de l'esfera (mantenir profunditat)
            Dim pLen As Single = CSng(Math.Sqrt(t2.PosX*t2.PosX + t2.PosY*t2.PosY + t2.PosZ*t2.PosZ))
            If pLen > 0.01F Then
                Dim rr As Single = R * t2.Depth
                t2.PosX = t2.PosX / pLen * rr
                t2.PosY = t2.PosY / pLen * rr
                t2.PosZ = t2.PosZ / pLen * rr
            End If
        Next
        _glControl.Invalidate()
        SetStatus(relIds.Count & Locale.Str("STATUS_AGRUPADES"))
    End Sub

    Private Sub BtnColorTaula_Click(s As Object, e As EventArgs)
        ' Si hi ha grup seleccionat, fa rotar el color a TOTES les taules del grup
        ' (escull el color de la primera i el propaga a la resta en un sol clic)
        Dim targets As New List(Of TablaBBDD)()
        If _grupSel.Count > 0 Then
            For Each x As TablaBBDD In _proyecto.Taules
                If _grupSel.Contains(x.Id) Then targets.Add(x)
            Next
        Else
            For Each x As TablaBBDD In _proyecto.Taules
                If x.Id = _renderer.SelTaulaId Then targets.Add(x) : Exit For
            Next
        End If
        If targets.Count = 0 Then Return
        ' Rotar color a partir del color actual del primer element
        Dim nouColor As Integer = (CInt(targets(0).GrupColor) + 1) Mod 8
        Dim gc As GroupColor = CType(nouColor, GroupColor)
        For Each t As TablaBBDD In targets
            t.GrupColor = gc
        Next
        RecalcularGrupId()
        MarcarModificat()
        _glControl.Invalidate()
    End Sub

    Private Sub BtnProfunditat_Click(s As Object, e As EventArgs)
        Dim t As TablaBBDD = Nothing
        For Each x As TablaBBDD In _proyecto.Taules
            If x.Id = _renderer.SelTaulaId Then t = x
        Next
        If t Is Nothing Then Return
        If t.Depth > 0.7F Then
            t.Depth = 0.5F
        Else
            t.Depth = 1.0F
        End If
        Dim R As Single = SphereRenderer.CalcRadi(_proyecto)
        Dim d As Single = CSng(Math.Sqrt(t.PosX * t.PosX + t.PosY * t.PosY + t.PosZ * t.PosZ))
        If d > 0 Then
            Dim scale As Single = R * t.Depth / d
            t.PosX *= scale
            t.PosY *= scale
            t.PosZ *= scale
        End If
        _glControl.Invalidate()
        SetStatus(t.Nombre & If(t.Depth < 0.7F, Locale.Str("STATUS_INTERIOR"), Locale.Str("STATUS_EXTERIOR")))
    End Sub

    Private Sub BtnAcceptar_Click(s As Object, e As EventArgs)
        ' Desar el projecte si hi ha ruta, sinó obrir "Desar com"
        If Not String.IsNullOrEmpty(_rutaFitxer) Then
            MnuDesar_Click(s, e)
        Else
            MnuDesarCom_Click(s, e)
        End If
    End Sub

    Private Sub BtnRenombrar_Click(s As Object, e As EventArgs)
        ' Mantingut per compatibilitat — ara el renombrat és inline al label
        Dim t As TablaBBDD = Nothing
        For Each x As TablaBBDD In _proyecto.Taules
            If x.Id = _renderer.SelTaulaId Then t = x
        Next
        If t Is Nothing Then Return
        Dim nou As String = InputBox(Locale.Str("DLG_RENOMBRAR"), Locale.Str("DLG_RENOMBRAR_TITOL"), t.Nombre)
        If Not String.IsNullOrWhiteSpace(nou) Then
            t.Nombre = nou
            MarcarModificat()
            _lblNomTaula.Text = "  " & t.Schema & "." & t.Nombre
            _glControl.Invalidate()
        End If
    End Sub

    Private Sub BtnEliminarCamp_Click(s As Object, e As EventArgs)
        Dim t As TablaBBDD = Nothing
        For Each x As TablaBBDD In _proyecto.Taules
            If x.Id = _renderer.SelTaulaId Then t = x
        Next

        ' Validació estricta de l'índex abans de continuar
        If t Is Nothing OrElse _campClickIdx < 0 OrElse _campClickIdx >= t.Fields.Count Then
            SetStatus(Locale.Str("STATUS_SEL_CAMP"))
            Return
        End If

        Dim f As CampoBBDD = t.Fields(_campClickIdx)

        ' Un camp PK referenciat per relacions entrants no es pot eliminar
        If f.EsPK Then
            For Each r As RelacionBBDD In _proyecto.Relacions
                If r.TablaDestinoId = t.Id AndAlso r.CampoPKNombre = f.Nombre Then
                    MessageBox.Show(Locale.Str("DLG_NO_ELIM_CAMP_PK") & r.Nombre,
                                    Locale.Str("DLG_NO_ELIMINAR"),
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
            Next
        End If

        ' Un camp FK amb relació definida tampoc — cal eliminar primer la relació
        For Each r As RelacionBBDD In _proyecto.Relacions
            If r.TablaOrigenId = t.Id AndAlso r.CampoFKNombre = f.Nombre Then
                MessageBox.Show(Locale.Str("DLG_NO_ELIM_CAMP_FK") & r.Nombre,
                                Locale.Str("DLG_NO_ELIMINAR"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
        Next

        ' Confirmació de l'usuari
        If MessageBox.Show(Locale.Str("DLG_ELIM_CAMP") & f.Nombre & Locale.Str("DLG_ELIM_CAMP2"),
                           Locale.Str("DLG_CONFIRMAR"),
                           MessageBoxButtons.YesNo) <> DialogResult.Yes Then Return

        ' Eliminar amb suport d'Undo/Redo
        CommandStack.Push(New CmdEliminarCamp(t, _campClickIdx))
        _campClickIdx = -1

        RecarregarLvCamps(t)
        ActualitzarPanellLateral()
        _glControl.Invalidate()
        SetStatus(Locale.Str("STATUS_CAMP_ELM"))
    End Sub

    Private Sub BtnEliminar_Click(s As Object, e As EventArgs)
        Dim t As TablaBBDD = Nothing
        For Each x As TablaBBDD In _proyecto.Taules
            If x.Id = _renderer.SelTaulaId Then t = x
        Next
        If t Is Nothing Then Return
        Dim errs As List(Of String) = IntegrityEngine.ValidarEliminarTaula(_proyecto.Taules, _proyecto.Relacions, t.Id)
        If errs.Count > 0 Then
            MessageBox.Show(errs(0), Locale.Str("DLG_NO_ELIMINAR"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If MessageBox.Show(Locale.Str("DLG_ELIM_TAULA") & t.Nombre & Locale.Str("DLG_ELIM_CONF2"), Locale.Str("DLG_CONFIRMAR"),
                           MessageBoxButtons.YesNo) = DialogResult.Yes Then
            CommandStack.Push(New CmdEliminarTaula(_proyecto.Taules, _proyecto.Relacions, t))
            _renderer.SelTaulaId = -1
            MostraPanellBuit()
            ActualitzarPanellLateral()
            _glControl.Invalidate()
            SetStatus(Locale.Str("STATUS_TAULA_ELM"))
        End If
    End Sub

    Private Sub BtnEditarRelacio_Click(s As Object, e As EventArgs)
        Dim r As RelacionBBDD = Nothing
        For Each x As RelacionBBDD In _proyecto.Relacions
            If x.Id = _renderer.SelRelacioId Then r = x
        Next
        ObrirEditorRelacio(r)
    End Sub

    Private Sub BtnEliminarRelacio_Click(s As Object, e As EventArgs)
        Dim r As RelacionBBDD = Nothing
        For Each x As RelacionBBDD In _proyecto.Relacions
            If x.Id = _renderer.SelRelacioId Then r = x
        Next
        If r Is Nothing Then Return
        If MessageBox.Show(Locale.Str("DLG_ELIM_RELACIO") & r.Nombre & Locale.Str("DLG_ELIM_CONF2"), Locale.Str("DLG_CONFIRMAR"),
                           MessageBoxButtons.YesNo) = DialogResult.Yes Then
            Dim ft As TablaBBDD = Nothing
            For Each t As TablaBBDD In _proyecto.Taules
                If t.Id = r.TablaOrigenId Then ft = t
            Next
            Dim fk As CampoBBDD = Nothing
            If ft IsNot Nothing Then
                For Each f As CampoBBDD In ft.Fields
                    If f.Nombre = r.CampoFKNombre Then fk = f
                Next
            End If
            CommandStack.Push(New CmdEliminarRelacio(_proyecto.Relacions, r, fk))
            _renderer.SelRelacioId = -1
            MostraPanellBuit()
            ActualitzarPanellLateral()
            _glControl.Invalidate()
            SetStatus(Locale.Str("STATUS_RELACIO_ELM"))
        End If
    End Sub

    Private Sub BtnTancarRelacio_Click(s As Object, e As EventArgs)
        _renderer.SelRelacioId = -1
        MostraPanellBuit()
    End Sub

    Private Sub TxtCerca_Changed(s As Object, e As EventArgs)
        Dim txt As String = _txtCerca.Text.ToLower()
        If String.IsNullOrEmpty(txt) Then Return
        For Each t As TablaBBDD In _proyecto.Taules
            If t.Nombre.ToLower().Contains(txt) Then
                _renderer.SelTaulaId = t.Id
                MostraPanellTaula(t)
                _glControl.Invalidate()
                Exit For
            End If
        Next
    End Sub

    Private Sub NovaTaula(esMatrix As Boolean)
        Dim id As Integer = _proyecto.GetNextTableId()
        Dim t As New TablaBBDD()
        t.Id = id
        t.Nombre = If(esMatrix, "MATRIU", "TAULA") & "_" & id
        t.EsMatrix = esMatrix
        Dim f As New CampoBBDD()
        f.Nombre = "ID_" & id
        f.TipoDato = DataType.DbInt
        f.EsPK = True
        f.NotNull = True
        f.EsIdentity = True
        t.Fields.Add(f)
        CommandStack.Push(New CmdAfegirTaula(_proyecto.Taules, t))
        SphereRenderer.DistribuirTaules(_proyecto)
        _renderer.SelTaulaId = id
        MostraPanellTaula(t)
        ' Refresh() forca redibux immediat (no esperem el missatge WM_PAINT)
        _glControl.Invalidate()
        SetStatus(Locale.Str("STATUS_TAULA_AFG"))
    End Sub

    Private Sub ObrirEditorCamp(t As TablaBBDD, idx As Integer)
        Dim camp As CampoBBDD = Nothing
        If idx >= 0 Then camp = t.Fields(idx)
        Dim dlg As New FrmFieldEditor(camp, t)
        If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.ResultCamp IsNot Nothing Then
            If idx >= 0 Then
                t.Fields(idx) = dlg.ResultCamp
            Else
                t.Fields.Add(dlg.ResultCamp)
            End If
            MarcarModificat()
            MostraPanellTaula(t)   ' refresca nom, comptador de camps i llista
            _glControl.Invalidate()
        End If
    End Sub

    Private Sub ObrirEditorRelacio(r As RelacionBBDD, Optional fromId As Integer = -1)
        ' En mode edició, recordar el camp FK original abans que el diàleg el modifiqui
        Dim oldOrigenId As Integer = If(r IsNot Nothing, r.TablaOrigenId, -1)
        Dim oldFkNom As String = If(r IsNot Nothing, r.CampoFKNombre, "")

        Dim dlg As New FrmRelationEditor(_proyecto, r, fromId)
        If dlg.ShowDialog(Me) = DialogResult.OK AndAlso dlg.ResultRelacio IsNot Nothing Then
            Dim rNou As RelacionBBDD = dlg.ResultRelacio
            If r Is Nothing Then
                ' Relació nova: l'alta marca el camp FK via CmdAfegirRelacio (amb Undo)
                Dim ft As TablaBBDD = Nothing
                For Each t As TablaBBDD In _proyecto.Taules
                    If t.Id = rNou.TablaOrigenId Then ft = t
                Next
                Dim fk As CampoBBDD = Nothing
                If ft IsNot Nothing Then
                    For Each f As CampoBBDD In ft.Fields
                        If f.Nombre = rNou.CampoFKNombre Then fk = f
                    Next
                End If
                CommandStack.Push(New CmdAfegirRelacio(_proyecto.Relacions, rNou, fk))
                SetStatus(Locale.Str("STATUS_RELACIO_CRE") & rNou.Nombre)
            Else
                ' Relació editada: mantenir coherent el flag EsFK si el camp ha canviat
                If oldOrigenId >= 0 AndAlso
                   (oldOrigenId <> rNou.TablaOrigenId OrElse oldFkNom <> rNou.CampoFKNombre) Then

                    ' Desmarcar l'antic només si cap altra relació l'utilitza
                    Dim usatPerAltres As Boolean = False
                    For Each rr As RelacionBBDD In _proyecto.Relacions
                        If rr.Id <> rNou.Id AndAlso
                           rr.TablaOrigenId = oldOrigenId AndAlso
                           rr.CampoFKNombre = oldFkNom Then
                            usatPerAltres = True : Exit For
                        End If
                    Next
                    If Not usatPerAltres Then
                        For Each tt As TablaBBDD In _proyecto.Taules
                            If tt.Id = oldOrigenId Then
                                For Each ff As CampoBBDD In tt.Fields
                                    If ff.Nombre = oldFkNom Then ff.EsFK = False
                                Next
                                Exit For
                            End If
                        Next
                    End If

                    ' Marcar el nou camp FK
                    For Each tt As TablaBBDD In _proyecto.Taules
                        If tt.Id = rNou.TablaOrigenId Then
                            For Each ff As CampoBBDD In tt.Fields
                                If ff.Nombre = rNou.CampoFKNombre Then ff.EsFK = True
                            Next
                            Exit For
                        End If
                    Next
                End If
                MarcarModificat()
                SetStatus(Locale.Str("STATUS_RELACIO_EDT") & rNou.Nombre)
            End If
            ActualitzarPanellLateral()
            RefrescarPanellInferior()   ' actualitza la llista de camps (flag FK) o la fitxa de relació
            _glControl.Invalidate()
        End If
    End Sub

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message,
                                               keyData As Keys) As Boolean
        Dim key As Keys = keyData And Keys.KeyCode

        ' Regla: _lateralSelIdx >= 0  → navegació al lateral
        '        _lateralSelIdx  < 0  → navegació als camps (si n'hi ha)
        ' Les dues s'exclouen mútuament via MouseEnter de cada zona.

        If key = Keys.Up OrElse key = Keys.Down Then
            If _lateralSelIdx >= 0 AndAlso
               _pnlLateral IsNot Nothing AndAlso _pnlLateral.Visible Then
                NavegarLateral(If(key = Keys.Down, 1, -1))
                Return True
            ElseIf _flCamps IsNot Nothing AndAlso _flCamps.Visible AndAlso
                   _renderer IsNot Nothing AndAlso _renderer.SelTaulaId >= 0 Then
                Dim tsel As TablaBBDD = Nothing
                For Each x As TablaBBDD In _proyecto.Taules
                    If x.Id = _renderer.SelTaulaId Then tsel = x : Exit For
                Next
                If tsel IsNot Nothing AndAlso tsel.Fields.Count > 0 Then
                    Dim idxNou As Integer
                    If key = Keys.Up Then
                        idxNou = If(_campSelIdx <= 0, tsel.Fields.Count - 1, _campSelIdx - 1)
                    Else
                        idxNou = If(_campSelIdx >= tsel.Fields.Count - 1, 0, _campSelIdx + 1)
                    End If
                    NavegarCamp(tsel, idxNou)
                    Return True
                End If
            End If
        End If

        If key = Keys.Return Then
            If _lateralSelIdx >= 0 AndAlso
               _pnlLateral IsNot Nothing AndAlso _pnlLateral.Visible Then
                SeleccionarLateral()
                Return True
            ElseIf _campSelIdx >= 0 AndAlso
                   _flCamps IsNot Nothing AndAlso _flCamps.Visible AndAlso
                   _renderer IsNot Nothing AndAlso _renderer.SelTaulaId >= 0 Then
                Dim tsel2 As TablaBBDD = Nothing
                For Each x As TablaBBDD In _proyecto.Taules
                    If x.Id = _renderer.SelTaulaId Then tsel2 = x : Exit For
                Next
                If tsel2 IsNot Nothing AndAlso _campSelIdx < tsel2.Fields.Count Then
                    ObrirEditorCamp(tsel2, _campSelIdx)
                    Return True
                End If
            End If
        End If

        If key = Keys.Delete Then
            If _renderer IsNot Nothing Then
                ' Supr sobre camp seleccionat → eliminar camp
                If _campClickIdx >= 0 AndAlso _renderer.SelTaulaId >= 0 Then
                    BtnEliminarCamp_Click(Nothing, Nothing)
                    Return True
                End If
                ' Supr sobre relació seleccionada → eliminar relació
                If _renderer.SelRelacioId >= 0 Then
                    BtnEliminarRelacio_Click(Nothing, Nothing)
                    Return True
                End If
                ' Supr sobre taula seleccionada → eliminar taula
                If _renderer.SelTaulaId >= 0 Then
                    BtnEliminar_Click(Nothing, Nothing)
                    Return True
                End If
            End If
        End If

        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.KeyCode = Keys.Insert Then
            NovaTaula(False)
        ElseIf e.KeyCode = Keys.Z AndAlso e.Control Then
            MnuUndo_Click(Nothing, Nothing)
        ElseIf e.KeyCode = Keys.Y AndAlso e.Control Then
            MnuRedo_Click(Nothing, Nothing)
        ElseIf e.KeyCode = Keys.S AndAlso e.Control AndAlso e.Shift Then
            MnuDesarCom_Click(Nothing, Nothing)
        ElseIf e.KeyCode = Keys.S AndAlso e.Control Then
            MnuDesar_Click(Nothing, Nothing)
        ElseIf e.KeyCode = Keys.O AndAlso e.Control Then
            MnuObrir_Click(Nothing, Nothing)
        ElseIf e.KeyCode = Keys.N AndAlso e.Control Then
            MnuNou_Click(Nothing, Nothing)
        End If
    End Sub

    ' ── Scroll mínim perquè un element d'un panell clipat quedi visible ─────────
    '    fl és el panell interior (Top <= 0) dins d'un Panel clip pare.
    Private Sub AssegurarVisible(fl As Control, item As Control)
        Dim pClip As Panel = TryCast(fl.Parent, Panel)
        If pClip Is Nothing Then Return
        Dim minTop As Integer = Math.Min(0, pClip.Height - fl.Height)
        Dim yDalt As Integer = fl.Top + item.Top          ' vora superior dins el clip
        Dim yBaix As Integer = yDalt + item.Height        ' vora inferior dins el clip
        If yDalt < 0 Then
            fl.Top = Math.Min(0, fl.Top - yDalt)
        ElseIf yBaix > pClip.Height Then
            fl.Top = Math.Max(minTop, fl.Top - (yBaix - pClip.Height))
        End If
    End Sub

    ' ── Refresca el panell inferior segons l'element seleccionat actual ─────────
    Private Sub RefrescarPanellInferior()
        If _renderer Is Nothing Then Return
        If _renderer.SelTaulaId >= 0 Then
            For Each t As TablaBBDD In _proyecto.Taules
                If t.Id = _renderer.SelTaulaId Then
                    MostraPanellTaula(t)
                    Return
                End If
            Next
            ' La taula seleccionada ja no existeix (p. ex. Refer d'una eliminació)
            _renderer.SelTaulaId = -1
            MostraPanellBuit()
        ElseIf _renderer.SelRelacioId >= 0 Then
            For Each r As RelacionBBDD In _proyecto.Relacions
                If r.Id = _renderer.SelRelacioId Then
                    MostraPanellRelacio(r)
                    Return
                End If
            Next
            ' La relació seleccionada ja no existeix
            _renderer.SelRelacioId = -1
            MostraPanellBuit()
        End If
    End Sub

    ' ── Navegació llista lateral ────────────────────────────────────────────────
    Private Sub NavegarLateral(delta As Integer)
        Dim fl As FlowLayoutPanel = If(_tabActiu = 0, _pnlListaTaules, _pnlListaRelacions)
        If fl Is Nothing OrElse fl.Controls.Count = 0 Then Return

        ' Treure hover de l'element actual
        If _lateralSelIdx >= 0 AndAlso _lateralSelIdx < fl.Controls.Count Then
            Dim lAnt As Label = TryCast(fl.Controls(_lateralSelIdx), Label)
            If lAnt IsNot Nothing Then lAnt.BackColor = AppStyle.ColFonsMig
        End If

        ' Avançar delta posicions saltant separadors (labels sense Tag Integer)
        Dim idxNou As Integer = _lateralSelIdx
        Dim intents As Integer = 0
        Do
            idxNou += delta
            If idxNou >= fl.Controls.Count Then idxNou = 0
            If idxNou < 0 Then idxNou = fl.Controls.Count - 1
            Dim l As Label = TryCast(fl.Controls(idxNou), Label)
            If l IsNot Nothing AndAlso l.Tag IsNot Nothing AndAlso TypeOf l.Tag Is Integer Then
                Exit Do   ' trobat label navegable
            End If
            intents += 1
            If intents > fl.Controls.Count Then Return
        Loop

        ' Activar el nou element
        Dim lNou As Label = DirectCast(fl.Controls(idxNou), Label)
        lNou.BackColor = AppStyle.ColHover
        _lateralSelIdx = idxNou

        ' Scroll mínim perquè l'element seleccionat quedi sempre visible
        ' (gestiona també el salt de l'últim al primer i viceversa)
        AssegurarVisible(fl, lNou)
    End Sub

    Private Sub SeleccionarLateral()
        Dim fl As FlowLayoutPanel = If(_tabActiu = 0, _pnlListaTaules, _pnlListaRelacions)
        If fl Is Nothing Then Return
        For Each ctrl As Control In fl.Controls
            Dim l As Label = TryCast(ctrl, Label)
            If l IsNot Nothing AndAlso l.BackColor = AppStyle.ColHover Then
                ' Simular clic
                If _tabActiu = 0 Then
                    Dim tid As Integer = CInt(l.Tag)
                    Dim tsel As TablaBBDD = Nothing
                    For Each tt As TablaBBDD In _proyecto.Taules
                        If tt.Id = tid Then tsel = tt : Exit For
                    Next
                    If tsel IsNot Nothing Then
                        _renderer.SelTaulaId = tsel.Id : _renderer.SelRelacioId = -1
                        MostraPanellTaula(tsel) : CentrarEnPosicio(tsel.PosX, tsel.PosY, tsel.PosZ)
                        MarcaSeleccionatLateral(l) : _glControl.Invalidate()
                    End If
                Else
                    Dim rid As Integer = CInt(l.Tag)
                    Dim rsel As RelacionBBDD = Nothing
                    For Each rr As RelacionBBDD In _proyecto.Relacions
                        If rr.Id = rid Then rsel = rr : Exit For
                    Next
                    If rsel IsNot Nothing Then
                        _renderer.SelRelacioId = rsel.Id : _renderer.SelTaulaId = -1
                        MostraPanellRelacio(rsel)
                        MarcaSeleccionatLateral(l) : _glControl.Invalidate()
                    End If
                End If
                Exit For
            End If
        Next
    End Sub

    ' ── Navegació camps panell inferior ─────────────────────────────────────────
    Private Sub NavegarCamp(t As TablaBBDD, idxNou As Integer)
        If _campSelIdx >= 0 AndAlso _campSelIdx < _flCamps.Controls.Count Then
            Dim lAnt As Label = TryCast(_flCamps.Controls(_campSelIdx), Label)
            If lAnt IsNot Nothing AndAlso _campSelIdx < t.Fields.Count Then
                lAnt.BackColor = ColBgCamp(t.Fields(_campSelIdx), _campSelIdx)
            End If
        End If
        _campSelIdx = idxNou
        If idxNou >= 0 AndAlso idxNou < _flCamps.Controls.Count Then
            Dim lNou As Label = TryCast(_flCamps.Controls(idxNou), Label)
            If lNou IsNot Nothing Then
                lNou.BackColor = AppStyle.ColHover
                ' Scroll mínim perquè el camp seleccionat quedi sempre visible
                ' (gestiona també el salt de l'últim al primer i viceversa)
                AssegurarVisible(_flCamps, lNou)
            End If
        End If
    End Sub

    Private Shared Function ColBgCamp(f As CampoBBDD, idx As Integer) As Color
        If f.EsPK Then
            Return Color.FromArgb(
                Math.Min(255, AppStyle.ColFons.R + CInt(AppStyle.ColPK.R * 0.12)),
                Math.Min(255, AppStyle.ColFons.G + CInt(AppStyle.ColPK.G * 0.12)),
                Math.Min(255, AppStyle.ColFons.B + CInt(AppStyle.ColPK.B * 0.12)))
        ElseIf f.EsFK Then
            Return Color.FromArgb(
                Math.Min(255, AppStyle.ColFons.R + CInt(AppStyle.ColFK.R * 0.08)),
                Math.Min(255, AppStyle.ColFons.G + CInt(AppStyle.ColFK.G * 0.08)),
                Math.Min(255, AppStyle.ColFons.B + CInt(AppStyle.ColFK.B * 0.08)))
        Else
            Return If(idx Mod 2 = 0, AppStyle.ColFonsGrid, AppStyle.ColFonsMig)
        End If
    End Function

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        Dim areaH As Integer = Me.ClientSize.Height - 22 - AppStyle.AltPanellInf
        If areaH < 50 Then areaH = 50
        Const AmpLat As Integer = 220
        If _glControl IsNot Nothing Then
            Dim xStart As Integer = If(_pnlLateral IsNot Nothing AndAlso _pnlLateral.Visible, AmpLat, 0)
            _glControl.Location = New Point(xStart, 22)
            _glControl.Size = New Size(Me.ClientSize.Width - xStart, areaH)
            _glControl.Invalidate()
        End If

    End Sub

    Private Sub AutoTimer_Tick(s As Object, e As EventArgs) Handles _autoTimer.Tick
        If Not FrmOpcions.Opcions.AutoDesarActiu Then Return
        If String.IsNullOrEmpty(_rutaFitxer) Then Return
        If Not _modificat Then Return          ' res a desar
        If Not Me.Enabled Then Return          ' hi ha una operació llarga en curs
        Try
            ProjectSerializer.Desar(_proyecto, _rutaFitxer)
            _modificat = False
            ActualitzarTitol()
            SetStatus(Locale.Str("STATUS_AUTODESAT") & DateTime.Now.ToString("HH:mm:ss"))
        Catch ex As Exception
            SetStatus(Locale.Str("STATUS_AUTO_ERR") & ex.Message)
        End Try
    End Sub

    Private Sub ActualitzarTitol()
        Dim nom As String = Locale.Str("TITLE_SENSE_TITOL")
        If Not String.IsNullOrEmpty(_rutaFitxer) Then nom = IO.Path.GetFileName(_rutaFitxer)
        Dim prefix As String = If(_modificat, "* ", "")
        Me.Text = "Holografic DB — " & prefix & nom
    End Sub


    ' ── OwnerDraw ListView de camps ──────────────────────────────────────
    Private Sub LvCamps_DrawHeader(s As Object, e As DrawListViewColumnHeaderEventArgs)
        e.Graphics.FillRectangle(New SolidBrush(AppStyle.ColFonsMig), e.Bounds)
        Using pen As New Pen(Color.FromArgb(25, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B))
            e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1)
        End Using
        Using br As New SolidBrush(Color.FromArgb(160, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B))
            Dim sf As New StringFormat()
            sf.Alignment = StringAlignment.Near
            sf.LineAlignment = StringAlignment.Center
            sf.Trimming = StringTrimming.EllipsisCharacter
            Dim r = New Rectangle(e.Bounds.X + 3, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height)
            e.Graphics.DrawString(e.Header.Text, AppStyle.FntMoltPetit, br, r, sf)
        End Using
    End Sub

    Private Sub LvCamps_DrawItem(s As Object, e As DrawListViewItemEventArgs)
        Dim esPK As Boolean = (e.Item.SubItems.Count > 4 AndAlso e.Item.SubItems(4).Text = "S")
        ' Determinar color de fons de la fila (SEMPRE fosc, mai groc)
        Dim bg As Color
        If e.Item.Selected Then
            bg = AppStyle.ColHover   ' seleccionada: taronja molt fosc
        ElseIf e.ItemIndex Mod 2 = 0 Then
            bg = AppStyle.ColFonsGrid         ' RGB(10,3,0)
        Else
            bg = AppStyle.ColFonsInput     ' fila imparell lleugerament diferent
        End If
        ' Fons complet de la fila (sobreescriu qualsevol color sistema)
        Using brBg As New SolidBrush(bg)
            e.Graphics.FillRectangle(brBg, e.Bounds)
        End Using
        ' Franja esquerra PK (groga, 3px, no cobreix el text)
        If esPK Then
            Using brPK As New SolidBrush(Color.FromArgb(200, AppStyle.ColPK.R, AppStyle.ColPK.G, AppStyle.ColPK.B))
                e.Graphics.FillRectangle(brPK,
                    New Rectangle(e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height))
            End Using
        End If
        ' Línia inferior de separació
        Using pen As New Pen(Color.FromArgb(22, AppStyle.ColAccent.R, AppStyle.ColAccent.G, AppStyle.ColAccent.B))
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1,
                                e.Bounds.Right, e.Bounds.Bottom - 1)
        End Using
    End Sub

    Private Sub LvCamps_DrawSubItem(s As Object, e As DrawListViewSubItemEventArgs)
        Dim esPK = (e.Item.SubItems.Count > 4 AndAlso e.Item.SubItems(4).Text = "S")
        Dim esFK = (e.Item.SubItems.Count > 5 AndAlso e.Item.SubItems(5).Text = "S")
        Dim col As Color
        Select Case e.ColumnIndex
            Case 1  ' Nom
                col = If(esPK, AppStyle.ColPK, AppStyle.ColTextPrinc)
            Case 2  ' Tipus
                col = AppStyle.ColType
            Case 4  ' PK
                col = AppStyle.ColPK
            Case 5  ' FK
                col = AppStyle.ColFK
            Case 6, 7, 8  ' NN, UQ, ID
                col = AppStyle.ColTextFeble
            Case Else
                col = AppStyle.ColTextSec
        End Select
        Using br As New SolidBrush(col)
            Dim sf As New StringFormat()
            sf.Alignment = If(e.ColumnIndex >= 4 AndAlso e.ColumnIndex <= 8,
                StringAlignment.Center, StringAlignment.Near)
            sf.LineAlignment = StringAlignment.Center
            sf.Trimming = StringTrimming.EllipsisCharacter
            Dim r = New Rectangle(e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 3, e.Bounds.Height)
            e.Graphics.DrawString(e.SubItem.Text, AppStyle.FntPetit, br, r, sf)
        End Using
    End Sub



    Private Sub SetStatus(msg As String)
        If _lblStatus IsNot Nothing Then _lblStatus.Text = "  " & msg
    End Sub

    ' Marca el projecte com a modificat
    Public Sub MarcarModificat()
        If Not _modificat Then
            _modificat = True
            ActualitzarTitol()
        End If
        ' Validació MER en temps real
        ValidarMER()
    End Sub

    Private Sub ValidarMER()
        If _proyecto Is Nothing OrElse _renderer Is Nothing Then Return
        Dim errs As List(Of String) = IntegrityEngine.ValidarModelComplet(_proyecto)
        _renderer.TaulesError.Clear()
        ' Extreure IDs de taules infractores dels missatges d'error
        For Each err As String In errs
            ' Format: "[NOM_TAULA] ..." o "[NOM_TAULA.CAMP] ..."
            Dim m As System.Text.RegularExpressions.Match =
                System.Text.RegularExpressions.Regex.Match(err, "^\[(\w+)")
            If m.Success Then
                Dim nomErr As String = m.Groups(1).Value.ToUpper()
                For Each t As TablaBBDD In _proyecto.Taules
                    If t.Nombre.ToUpper() = nomErr Then
                        _renderer.TaulesError.Add(t.Id)
                        Exit For
                    End If
                Next
            End If
        Next
    End Sub

    Private Sub _errorTimer_Tick(s As Object, e As EventArgs) Handles _errorTimer.Tick
        If _renderer Is Nothing OrElse _renderer.TaulesError.Count = 0 Then Return
        _renderer.ErrorBlink = Not _renderer.ErrorBlink
        _glControl.Invalidate()
    End Sub

    ' Pregunta si es vol desar. True = continuar, False = cancel.lar
    Private Function ConfirmarDescartarCanvis() As Boolean
        If Not _modificat Then Return True
        Dim nomDoc As String = If(String.IsNullOrEmpty(_rutaFitxer),
                                  _proyecto.Nombre & Locale.Str("DLG_SENSE_DESAR"),
                                  IO.Path.GetFileName(_rutaFitxer))
        Dim res As DialogResult = MessageBox.Show(
            Locale.Str("DLG_DESAR_Q1") & nomDoc & Locale.Str("DLG_DESAR_Q2"),
            Locale.Str("DLG_DESAR_TITOL"),
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1)
        Select Case res
            Case DialogResult.Yes
                MnuDesar_Click(Nothing, Nothing)
                ' Si l'usuari ha cancel·lat "Desar com" o el desat ha fallat,
                ' els canvis continuen pendents: no es pot continuar
                Return Not _modificat
            Case DialogResult.No
                Return True
            Case Else
                Return False
        End Select
    End Function

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not ConfirmarDescartarCanvis() Then
            e.Cancel = True
            Return
        End If
        _renderTimer.Stop()
        RemoveHandler CommandStack.Modificat, AddressOf OnModelModificat
        MyBase.OnFormClosing(e)
    End Sub

End Class
