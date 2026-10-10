using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;
using RpgMacroHarness.Core;
using RpgMacroHarness.Data;
using RpgMacroHarness.Input;
using RpgMacroHarness.Vision;

namespace RpgMacroHarness.UI;

/// <summary>
/// 유니티/언리얼 스타일의 시각적 FSM 노드 그래프 에디터를 탑재한 메인 창입니다.
/// ComfyUI / Shader Graph 스타일의 노드 인라인 편집 및 인스펙터 최소화(Fold) 기능을 지원합니다.
/// </summary>
public partial class MainForm : Form
{
    private MacroEventProfile _profile = new();
    private Thread? _harnessThread;
    private bool _isMacroRunning = false;
    private EventMacroHarness? _harness;
    private bool _isUpdatingUi = false;

    // 인스펙터 최소화(Fold) 상태 (기본값: 최소화)
    private bool _isInspectorCollapsed = true;

    // 4가지 UI 작업 뷰 모드 통합 관리 (FSM 카드 / 핀 그래프 / 목록 / 트리)
    public AppViewMode CurrentViewMode { get; private set; } = AppViewMode.CanvasPinGraph;
    private AppViewMode _lastGraphicMode = AppViewMode.CanvasPinGraph;
    private AppViewMode _lastClassicMode = AppViewMode.ClassicTree;
    private bool IsClassicMode => CurrentViewMode == AppViewMode.ClassicList || CurrentViewMode == AppViewMode.ClassicTree;

    // 녹화 대상 이벤트 고정 바인딩 (녹화 중 캔버스/목록 클릭으로 인한 선택 유실 방지)
    private MacroEvent? _recordingTargetEvent = null;
    private string? _recordingTargetState = null;

    // 선택 상태 및 이벤트 통합 접근 헬퍼 (노드 뷰와 클래식 뷰 완전 일원화)
    private string? CurrentSelectedState => _recordingTargetState ?? (IsClassicMode ? _classicControl.SelectedState : _canvas.SelectedState);
    private MacroEvent? CurrentSelectedEvent => _recordingTargetEvent ?? (IsClassicMode ? _classicControl.SelectedEvent : _canvas.SelectedEvent);

    // 변경 사항 플래그 및 제목 표시줄 '*' 실시간 연동
    private bool _rawIsDirty = false;
    private bool _isDirty
    {
        get => _rawIsDirty;
        set
        {
            _rawIsDirty = value;
            UpdateWindowTitle();
        }
    }

    private void UpdateWindowTitle()
    {
        string baseTitle = Localization.Get("AppTitle");
        if (Program.IsAdministrator())
        {
            baseTitle += Localization.IsKorean ? " [관리자 권한]" : " [Admin]";
        }
        Text = _rawIsDirty ? "*" + baseTitle : baseTitle;
    }

    // Undo/Redo 매니저
    private readonly UndoRedoManager _undoRedo = new();
    private readonly ToolTip _toolbarToolTip = new();
    private bool _currentIsDark = AppSettings.Current.IsDarkMode;
    private void UpdateImmersiveDarkMode(bool isDark)
    {
        AppThemeHelper.UpdateFormTitleBarTheme(this, isDark);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateImmersiveDarkMode(AppSettings.Current.IsDarkMode);
    }

    // 상단 툴바 컨트롤
    private Panel _topContainer = null!;
    private Panel _pnlTopRight = null!;
    private FlowLayoutPanel _topBar = null!;
    private Button _btnOpen = null!;
    private Button _btnSave = null!;
    private Button _btnUndo = null!;
    private Button _btnRedo = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Button _btnSettings = null!;
    private Button _btnTemplateWizard = null!;
    private Button _btnAiAssistant = null!;
    private Button _btnAutoLayout = null!;
    private Button _btnAddStateNode = null!;
    private Button _btnAddEventToNode = null!;
    private Button _btnToggleInspector = null!;

    // 브라우저 탭 스타일 3대 뷰 전환 버튼
    private Button _btnTabFsm = null!;
    private Button _btnTabPin = null!;
    private Button _btnTabClassic = null!;
    private bool _isCompactToolbar = false;

    private Label _lblEmergency = null!;
    private Label _lblStatus = null!;
    private Label _lblActiveState = null!;

    // 중앙 노드 캔버스, 클래식 리스트 뷰 및 우측 인스펙터
    private SplitContainer _splitMain = null!;
    private NodeCanvasControl _canvas = null!;
    private ClassicViewControl _classicControl = null!;
    private Panel _pnlInspector = null!;

    // 슬림 사이드바 (인스펙터 최소화 시 펼치기 버튼만 깔끔하게 유지)
    private Panel _slimSideBar = null!;
    private readonly ToolTip _slimToolTip = new();
    private Button _btnSlimExpand = null!;

    // 우측 인스펙터 컨트롤들
    private Panel _inspectorHeaderBar = null!;
    private Label _lblInspectorTitle = null!;
    private Button _btnCollapseInspector = null!;
    private GroupBox _grpStateProps = null!;
    private TextBox _txtStateName = null!;
    private Button _btnSetInitial = null!;
    private Button _btnDeleteState = null!;

    private GroupBox _grpEventProps = null!;
    private CustomCheckBox _chkEventEnabled = null!;
    private Label _lblEvtName = null!;
    private TextBox _txtEventName = null!;
    private CustomCheckBox _chkParallel = null!;
    private Label _lblHumanizerDelay = null!;
    private TrackBar _sliderHumanizerDelay = null!;
    private Label _lblTriggerHeader = null!;
    private DarkListView _lvTriggers = null!;
    private Button _btnAddTrigger = null!;
    private Button _btnEditTrigger = null!;
    private Button _btnDeleteTrigger = null!;

    private Label _lblActionHeader = null!;
    private DarkListView _lvActions = null!;
    private Button _btnAddAction = null!;
    private Button _btnEditAction = null!;
    private Button _btnDeleteAction = null!;
    private Button _btnMoveUpAction = null!;
    private Button _btnMoveDownAction = null!;
    private Button _btnRecordMacro = null!;
    private readonly MacroRecorder _macroRecorder = new();

    private DarkScrollBar _scrollInspector = null!;
    private Panel _pnlInspectorContent = null!;

    // 하단 로그창
    private Panel _bottomBar = null!;
    private Panel _logContainer = null!;
    private Label _lblLogTitle = null!;
    private Button _btnToggleLog = null!;
    private bool _isLogCollapsed = false;
    private ListBox _logBox = null!;
    private DarkScrollBar _scrollLog = null!;

    public MainForm()
    {
        InitializeComponent();
        SetLogCollapsed(AppSettings.Current.IsLogCollapsed);
        SwitchViewMode(AppViewMode.CanvasPinGraph);
        ApplyLanguage();
        ApplyTheme(AppSettings.Current.IsDarkMode);
        InitGlobalHotkeys();
        LoadDefaultProfile();

        AppThemeHelper.ThemePreviewChanged += isDark =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { Invoke(new Action(() => ApplyTheme(isDark))); } catch { }
        };
    }


    private void InitializeComponent()
    {
        UpdateWindowTitle();
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }

        Size = new Size(Math.Max(1180, AppSettings.Current.WindowWidth), Math.Max(740, AppSettings.Current.WindowHeight));
        MinimumSize = new Size(1000, 680);

        if (AppSettings.Current.WindowX >= 0 && AppSettings.Current.WindowY >= 0)
        {
            var rect = new Rectangle(AppSettings.Current.WindowX, AppSettings.Current.WindowY, Width, Height);
            if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect)))
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(AppSettings.Current.WindowX, AppSettings.Current.WindowY);
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
        }

        if (AppSettings.Current.IsWindowMaximized)
        {
            WindowState = FormWindowState.Maximized;
        }

        Font = AppFontHelper.GetEmojiFont(9f);

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        DoubleBuffered = true;

        Resize += (s, e) => UpdateResponsiveToolbar();

        // -------------------------------------------------------------
        // 상단 툴바 컨테이너 (좌측 도구 모음 + 우측 인스펙터 토글 화살표)
        // -------------------------------------------------------------
        _topContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(246, 248, 250)
        };

        _pnlTopRight = new Panel
        {
            Dock = DockStyle.Right,
            Width = 46,
            Height = 44,
            BackColor = Color.Transparent,
            Padding = new Padding(6, 6, 6, 6)
        };

        _topBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 44,
            BackColor = Color.FromArgb(246, 248, 250),
            Padding = new Padding(6, 6, 6, 6),
            WrapContents = false,
            AutoScroll = false
        };

        _btnOpen = new Button { Text = "📂", Width = 34, Height = 30, Margin = new Padding(0, 0, 4, 0), FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(10f) };
        _btnOpen.Click += (s, e) => OpenProfile();
        _toolbarToolTip.SetToolTip(_btnOpen, Localization.Get("TooltipOpen"));

        _btnSave = new Button { Text = "💾", Width = 34, Height = 30, Margin = new Padding(0, 0, 4, 0), FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(10f) };
        _btnSave.Click += (s, e) => SaveProfile();
        _toolbarToolTip.SetToolTip(_btnSave, Localization.Get("TooltipSave"));

        _btnUndo = new Button { Text = "↶", Width = 34, Height = 30, Margin = new Padding(0, 0, 4, 0), Enabled = false, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetSymbolFont(10f, FontStyle.Bold) };
        _btnUndo.Click += (s, e) => PerformUndo();
        _btnUndo.Paint += (s, e) => AppThemeHelper.DrawCustomDisabledButton(_btnUndo, e, _currentIsDark);
        _toolbarToolTip.SetToolTip(_btnUndo, Localization.Get("TooltipUndo"));

        _btnRedo = new Button { Text = "↷", Width = 34, Height = 30, Margin = new Padding(0, 0, 8, 0), Enabled = false, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetSymbolFont(10f, FontStyle.Bold) };
        _btnRedo.Click += (s, e) => PerformRedo();
        _btnRedo.Paint += (s, e) => AppThemeHelper.DrawCustomDisabledButton(_btnRedo, e, _currentIsDark);
        _toolbarToolTip.SetToolTip(_btnRedo, Localization.Get("TooltipRedo"));

        _btnStart = new Button { Text = Localization.Get("BtnStart"), MinimumSize = new Size(80, 30), AutoSize = true, Padding = new Padding(8, 0, 8, 0), Margin = new Padding(0, 0, 4, 0), BackColor = Color.FromArgb(39, 174, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold) };
        _btnStart.Click += (s, e) => HandleStartPause();

        _btnStop = new Button { Text = Localization.Get("BtnStop"), MinimumSize = new Size(80, 30), AutoSize = true, Padding = new Padding(8, 0, 8, 0), Margin = new Padding(0, 0, 6, 0), BackColor = Color.FromArgb(192, 57, 43), ForeColor = Color.White, Enabled = false, FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold) };
        _btnStop.Click += (s, e) => StopMacro();

        _btnAddStateNode = new Button { Text = "➕", Width = 34, Height = 30, Margin = new Padding(0, 0, 4, 0), FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(10f) };
        _btnAddStateNode.Click += (s, e) => AddNewStateNode();
        _toolbarToolTip.SetToolTip(_btnAddStateNode, Localization.IsKorean ? "새 상태 노드 추가" : "Add New State Node");

        _btnAddEventToNode = new Button { Text = "⚡", Width = 34, Height = 30, Margin = new Padding(0, 0, 4, 0), FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(10f) };
        _btnAddEventToNode.Click += (s, e) => AddNewEventToSelectedState();
        _toolbarToolTip.SetToolTip(_btnAddEventToNode, Localization.IsKorean ? "새 이벤트 추가" : "Add New Event");

        _btnAutoLayout = new Button { Text = "📐", Width = 34, Height = 30, Margin = new Padding(0, 0, 8, 0), FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(10f) };
        _btnAutoLayout.Click += (s, e) => 
        {
            RecordUndo(Localization.IsKorean ? "노드 자동 정렬" : "Auto Layout");
            if (CurrentViewMode == AppViewMode.CanvasPinGraph)
            {
                _canvas.AutoLayoutPinGraph();
            }
            else if (CurrentViewMode == AppViewMode.CanvasFsm)
            {
                _canvas.AutoLayoutNodes();
            }
            SyncAndRefreshAllViews();
        };
        _toolbarToolTip.SetToolTip(_btnAutoLayout, Localization.IsKorean ? "노드 자동 정렬" : "Auto Layout Nodes");

        // 브라우저 탭 스타일 3대 뷰 전환 버튼 (탭 순서: 🔀 핀 모드 -> 📦 FSM 모드 -> ☰ 클래식 모드)
        _btnTabPin = new Button
        {
            Height = 30,
            AutoSize = true,
            Padding = new Padding(8, 0, 8, 0),
            Margin = new Padding(0, 0, 2, 0),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnTabPin.Click += (s, e) => SwitchViewMode(AppViewMode.CanvasPinGraph);
        SetupTabButtonPaint(_btnTabPin, () => CurrentViewMode == AppViewMode.CanvasPinGraph, () => Localization.IsKorean ? "🔀 핀 모드" : "🔀 Pin Mode", () => "🔀");

        _btnTabFsm = new Button
        {
            Height = 30,
            AutoSize = true,
            Padding = new Padding(8, 0, 8, 0),
            Margin = new Padding(0, 0, 2, 0),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnTabFsm.Click += (s, e) => SwitchViewMode(AppViewMode.CanvasFsm);
        SetupTabButtonPaint(_btnTabFsm, () => CurrentViewMode == AppViewMode.CanvasFsm, () => Localization.IsKorean ? "📦 FSM 모드" : "📦 FSM Mode", () => "📦");

        _btnTabClassic = new Button
        {
            Height = 30,
            AutoSize = true,
            Padding = new Padding(8, 0, 8, 0),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnTabClassic.Click += (s, e) => SwitchViewMode(AppViewMode.ClassicTree);
        SetupTabButtonPaint(_btnTabClassic, () => IsClassicMode, () => Localization.IsKorean ? "☰ 클래식 모드" : "☰ Classic Mode", () => "☰");

        _btnTemplateWizard = new Button { Text = "✨", Width = 34, Height = 30, Margin = new Padding(0, 0, 4, 0), FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(10f) };
        _btnTemplateWizard.Click += (s, e) => ShowTemplateWizard();
        _toolbarToolTip.SetToolTip(_btnTemplateWizard, Localization.IsKorean ? "도우미 마법사 (실전 템플릿 간이 생성)" : "Helper Wizard");

        _btnAiAssistant = new Button { Text = "🧠", Width = 34, Height = 30, Margin = new Padding(0, 0, 4, 0), FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(10f) };
        _btnAiAssistant.Click += (s, e) => ShowAiAssistantDialog();
        _toolbarToolTip.SetToolTip(_btnAiAssistant, Localization.IsKorean ? "AI 매크로 어시스턴트 (자연어 자동 생성)" : "AI Macro Assistant");

        _btnSettings = new Button { Text = "⚙️", Width = 34, Height = 30, Margin = new Padding(0, 0, 10, 0), FlatStyle = FlatStyle.Flat, Font = AppFontHelper.GetEmojiFont(10f) };
        _btnSettings.Click += (s, e) => OpenSettings();
        _toolbarToolTip.SetToolTip(_btnSettings, Localization.IsKorean ? "프로그램 환경설정 및 패치노트" : "Settings & Patch Notes");

        // 우측 끝에 고정 배치되는 상황별 화살표 인스펙터 토글 버튼 (접힘 시 ◀ 펼치기, 펼침 시 ▶ 접기)
        _btnToggleInspector = new Button
        {
            Text = "◀",
            Width = 34,
            Height = 30,
            Location = new Point(6, 6),
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnToggleInspector.Click += (s, e) => SetInspectorCollapsed(!_isInspectorCollapsed);
        _toolbarToolTip.SetToolTip(_btnToggleInspector, Localization.IsKorean ? "인스펙터 속성창 열기" : "Open Inspector Panel");
        _pnlTopRight.Controls.Add(_btnToggleInspector);

        _lblEmergency = new Label { Text = Localization.Get("EmergencyStop"), AutoSize = true, Margin = new Padding(8, 3, 10, 0), ForeColor = Color.DarkRed, Font = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold) };
        _lblStatus = new Label { Text = Localization.Get("StatusWaiting"), AutoSize = true, Margin = new Padding(0, 3, 10, 0), ForeColor = Color.DarkSlateGray, Font = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold) };
        _lblActiveState = new Label { Text = $"{Localization.Get("ActiveStatePrefix")} [{MacroEventProfile.FormatStateDisplay(_profile.InitialState, Localization.IsKorean)}]", AutoSize = true, Margin = new Padding(0, 3, 6, 0), ForeColor = Color.FromArgb(41, 128, 185), Font = AppFontHelper.GetDefaultFont(9f, FontStyle.Bold) };

        _topBar.Controls.AddRange(new Control[]
        {
            _btnOpen, _btnSave, _btnUndo, _btnRedo, _btnStart, _btnStop,
            _btnAddStateNode, _btnAddEventToNode, _btnAutoLayout,
            _btnTabPin, _btnTabFsm, _btnTabClassic,
            _btnTemplateWizard, _btnAiAssistant, _btnSettings
        });

        // 우측 도킹 패널을 먼저 올려놓아 DockStyle.Right가 우측 끝을 차지하고, _topBar가 Fill로 채움
        _topContainer.Controls.Add(_topBar);
        _topContainer.Controls.Add(_pnlTopRight);

        // -------------------------------------------------------------
        // 하단 실시간 로그창 (Log 타이틀 및 축소/토글 버튼 + 상태 현황 뱃지)
        // -------------------------------------------------------------
        _bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 130, Padding = new Padding(8, 4, 8, 6), BackColor = Color.FromArgb(246, 248, 250) };
        var logHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 24,
            BackColor = Color.Transparent,
            WrapContents = false,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = new Padding(2, 0, 2, 0)
        };
        _lblLogTitle = new Label { Text = "Log", AutoSize = true, Margin = new Padding(0, 3, 2, 0), Font = AppFontHelper.GetDefaultFont(9f, FontStyle.Bold) };
        _btnToggleLog = new Button
        {
            Text = "▼",
            Size = new Size(20, 20),
            Margin = new Padding(2, 1, 14, 0),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = AppFontHelper.GetEmojiFont(7.5f)
        };
        _btnToggleLog.FlatAppearance.BorderSize = 0;
        _btnToggleLog.Click += (s, e) => ToggleLogPanel();

        logHeader.Controls.AddRange(new Control[] { _lblLogTitle, _btnToggleLog, _lblEmergency, _lblStatus, _lblActiveState });

        _logContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _logBox = new ListBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 18,
            IntegralHeight = false,
            Font = AppFontHelper.GetDefaultFont(9f),
            Location = new Point(0, 0),
            Size = new Size(_logContainer.Width + SystemInformation.VerticalScrollBarWidth + 8, Math.Max(10, _logContainer.Height))
        };
        _logBox.DrawItem += (s, e) =>
        {
            if (e.Index < 0 || e.Index >= _logBox.Items.Count) return;
            bool isDark = AppSettings.Current.IsDarkMode;
            Color bgColor = (e.State & DrawItemState.Selected) != 0
                ? (isDark ? Color.FromArgb(41, 65, 95) : Color.FromArgb(215, 230, 248))
                : (isDark ? Color.FromArgb(13, 17, 24) : Color.White);
            using var bgBrush = new SolidBrush(bgColor);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            Color textColor = (e.State & DrawItemState.Selected) != 0
                ? (isDark ? Color.White : Color.FromArgb(20, 40, 70))
                : (isDark ? Color.FromArgb(235, 240, 245) : Color.FromArgb(20, 20, 20));

            using var textBrush = new SolidBrush(textColor);
            string text = _logBox.Items[e.Index]?.ToString() ?? "";
            var textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height);
            AppFontHelper.DrawMixedString(e.Graphics, text, 9f, textBrush, textRect, StringAlignment.Near, StringAlignment.Center, FontStyle.Regular);
        };

        _logContainer.Resize += (s, e) =>
        {
            _logBox.SetBounds(0, 0, _logContainer.ClientSize.Width + SystemInformation.VerticalScrollBarWidth + 8, _logContainer.ClientSize.Height);
        };
        _logContainer.Controls.Add(_logBox);

        _scrollLog = new DarkScrollBar { Dock = DockStyle.Right, Width = 14 };
        _scrollLog.AttachTo(_logBox);

        _bottomBar.Controls.Add(logHeader);
        _bottomBar.Controls.Add(_scrollLog);
        _bottomBar.Controls.Add(_logContainer);
        _logContainer.BringToFront();

        // -------------------------------------------------------------
        // 중앙 SplitContainer (노드 캔버스 / 클래식 뷰 + 우측 패널)
        // -------------------------------------------------------------
        _splitMain = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 5,
            BackColor = Color.FromArgb(220, 224, 230)
        };
        _splitMain.SplitterMoved += (s, e) =>
        {
            ResizeInspectorContents();
            if (_classicControl != null && _classicControl.Visible)
            {
                _classicControl.Invalidate();
            }
        };

        // 좌측 1: 시각적 노드 캔버스 (기본 뷰)
        _canvas = new NodeCanvasControl { Dock = DockStyle.Fill };
        _canvas.SelectionChanged += (st, ev) =>
        {
            if (!IsClassicMode) OnCanvasSelectionChanged(st, ev);
        };
        _canvas.ProfileModified += () =>
        {
            _isDirty = true;
            SyncAndRefreshAllViews();
        };
        _canvas.RecordUndoRequested += actionName => RecordUndo(actionName);
        _canvas.StateDoubleClicked += state => RenameState(state);
        _canvas.EventEditRequested += evt => OpenEventEditDialog(evt);
        _canvas.GraphNodeSelected += node => OnPinGraphNodeSelected(node);

        // 좌측 2: 클래식 리스트 뷰 (토글 시 전환)
        _classicControl = new ClassicViewControl { Dock = DockStyle.Fill, Visible = false };
        _classicControl.ProfileModified += () => { _isDirty = true; SyncAndRefreshAllViews(); };
        _classicControl.RecordUndoRequested += actionName => RecordUndo(actionName);
        _classicControl.AddNewStateRequested += AddNewStateNode;
        _classicControl.RenameStateRequested += state => RenameState(state);
        _classicControl.SetInitialStateRequested += state => SetSpecificAsInitialState(state);
        _classicControl.DeleteStateRequested += state => DeleteSpecificState(state);
        _classicControl.SelectionChanged += (st, ev) =>
        {
            if (IsClassicMode) OnCanvasSelectionChanged(st, ev);
        };
        _classicControl.EventEditRequested += evt => OpenEventEditDialog(evt);
        _classicControl.AddActionRequested += evt => AddSpecificAction(evt);
        _classicControl.EditActionRequested += (evt, act) => EditSpecificAction(evt, act);
        _classicControl.DeleteActionRequested += (evt, act) => DeleteSpecificAction(evt, act);
        _classicControl.AddTriggerRequested += evt => AddSpecificTrigger(evt);
        _classicControl.EditTriggerRequested += (evt, trg) => EditSpecificTrigger(evt, trg);
        _classicControl.DeleteTriggerRequested += (evt, trg) => DeleteSpecificTrigger(evt, trg);
        _classicControl.RecordMacroRequested += evt => StartMacroRecordingForEvent(evt);
        _classicControl.CopyRequested += () => CopyCurrentSelectionToClipboard();
        _classicControl.PasteRequested += () => PasteFromClipboard();
        _classicControl.AddEventRequested += state => AddEventToSpecificState(state);
        _classicControl.DeleteEventRequested += (state, evt) => DeleteSpecificEvent(state, evt);

        _splitMain.Panel1.Controls.Add(_classicControl);
        _splitMain.Panel1.Controls.Add(_canvas);

        // 노드 인라인 조작 델리게이트 연동
        _canvas.TriggerEditRequested += (evt, trg) => EditSpecificTrigger(evt, trg);
        _canvas.TriggerAddRequested += evt => AddSpecificTrigger(evt);
        _canvas.TriggerDeleteRequested += (evt, trg) => DeleteSpecificTrigger(evt, trg);
        _canvas.ActionEditRequested += (evt, act) => EditSpecificAction(evt, act);
        _canvas.ActionAddRequested += evt => AddSpecificAction(evt);
        _canvas.RecordMacroRequested += evt => StartMacroRecordingForEvent(evt);
        _canvas.ActionDeleteRequested += (evt, act) => DeleteSpecificAction(evt, act);
        _canvas.EventDeleteRequested += (state, evt) => DeleteSpecificEvent(state, evt);
        _canvas.AddEventToStateRequested += state => AddEventToSpecificState(state);
        _canvas.CopyRequested += () => CopyCurrentSelectionToClipboard();
        _canvas.PasteRequested += () => PasteFromClipboard();
        _canvas.PinGraphNodeEditRequested += node =>
        {
            if (node == null) return;
            var evt = _profile.Events.FirstOrDefault(e => node.Id.Contains(e.Id));
            if (node.TriggerData != null && evt != null)
            {
                EditSpecificTrigger(evt, node.TriggerData);
            }
            else if (node.ActionData != null && evt != null)
            {
                EditSpecificAction(evt, node.ActionData);
            }
            else if (node.NodeType == GraphNodeType.Start && !string.IsNullOrEmpty(node.StateTag) && !MacroEventProfile.IsGlobalState(node.StateTag))
            {
                RenameState(node.StateTag);
            }
        };

        // FSM 상태 및 인스펙터 조작 델리게이트 연동
        _canvas.AddNewStateRequested += AddNewStateNode;
        _canvas.RenameStateRequested += state => RenameState(state);
        _canvas.SetInitialStateRequested += state => SetSpecificAsInitialState(state);
        _canvas.DeleteStateRequested += state => DeleteSpecificState(state);
        _canvas.DeleteMultipleStatesRequested += states => DeleteMultipleStates(states);
        _canvas.ToggleInspectorRequested += () => SetInspectorCollapsed(!_isInspectorCollapsed);
        _canvas.IsInspectorExpandedFunc = () => !_isInspectorCollapsed;

        InitCanvasContextMenu();
        _splitMain.Panel1.Controls.Add(_canvas);

        // 우측 패널 구성: 인스펙터 패널 & 슬림 사이드바
        BuildInspectorPanel();
        BuildSlimSideBar();

        _scrollInspector = new DarkScrollBar { Dock = DockStyle.Right, Width = 14 };
        _scrollInspector.ValueChanged += (s, e) =>
        {
            if (_pnlInspectorContent != null)
            {
                _pnlInspectorContent.Top = -_scrollInspector.Value;
            }
        };

        _splitMain.Panel2.Controls.Add(_pnlInspector);
        _splitMain.Panel2.Controls.Add(_scrollInspector);
        _splitMain.Panel2.Controls.Add(_slimSideBar);

        Controls.Add(_splitMain);
        Controls.Add(_bottomBar);
        Controls.Add(_topContainer);

        EnableDoubleBuffered(_splitMain.Panel1);
        EnableDoubleBuffered(_splitMain.Panel2);
        EnableDoubleBuffered(_topContainer);
        EnableDoubleBuffered(_topBar);
        EnableDoubleBuffered(_bottomBar);
        EnableDoubleBuffered(_pnlInspector);

        _splitMain.BringToFront();

        // 프로그램의 기본값: 우측 UI 최소화 상태
        SetInspectorCollapsed(true);
    }


    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isDirty)
        {
            var res = MessageBox.Show(Localization.Get("PromptUnsavedChanges"), Localization.Get("TitleUnsavedChanges"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
                SaveProfile();
            }
            else if (res == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
        }

        if (!e.Cancel)
        {
            if (WindowState == FormWindowState.Normal)
            {
                AppSettings.Current.WindowWidth = Width;
                AppSettings.Current.WindowHeight = Height;
                AppSettings.Current.WindowX = Location.X;
                AppSettings.Current.WindowY = Location.Y;
                AppSettings.Current.IsWindowMaximized = false;
            }
            else if (WindowState == FormWindowState.Maximized)
            {
                AppSettings.Current.IsWindowMaximized = true;
                if (RestoreBounds.Width > 0 && RestoreBounds.Height > 0)
                {
                    AppSettings.Current.WindowWidth = RestoreBounds.Width;
                    AppSettings.Current.WindowHeight = RestoreBounds.Height;
                    AppSettings.Current.WindowX = RestoreBounds.X;
                    AppSettings.Current.WindowY = RestoreBounds.Y;
                }
            }

            if (_splitMain != null && _splitMain.SplitterDistance > 100)
            {
                AppSettings.Current.SplitterDistance = _splitMain.SplitterDistance;
            }

            AppSettings.Save();
        }

        GlobalHotkeyService.Stop();
        _macroRecorder.Dispose();
        _harness?.Stop();
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S))
        {
            SaveProfile();
            return true;
        }
        if (keyData == (Keys.Control | Keys.O))
        {
            OpenProfile();
            return true;
        }
        if (keyData == (Keys.Control | Keys.Z))
        {
            PerformUndo();
            return true;
        }
        if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z))
        {
            PerformRedo();
            return true;
        }
        if (keyData == (Keys.Control | Keys.C))
        {
            if (ActiveControl is not TextBoxBase && !(ActiveControl is ContainerControl cc && cc.ActiveControl is TextBoxBase))
            {
                if (CopyCurrentSelectionToClipboard()) return true;
            }
        }
        if (keyData == (Keys.Control | Keys.V))
        {
            if (ActiveControl is not TextBoxBase && !(ActiveControl is ContainerControl cc && cc.ActiveControl is TextBoxBase))
            {
                if (PasteFromClipboard()) return true;
            }
        }
        if (keyData == Keys.Delete)
        {
            if (ActiveControl is not TextBoxBase && !(ActiveControl is ContainerControl cc && cc.ActiveControl is TextBoxBase))
            {
                if (IsClassicMode ? _classicControl.DeleteCurrentSelection() : _canvas.DeleteCurrentSelection())
                {
                    return true;
                }
            }
        }
        if (keyData == Keys.F2 || keyData == Keys.Enter)
        {
            if (ActiveControl is not TextBoxBase && !(ActiveControl is ContainerControl cc && cc.ActiveControl is TextBoxBase))
            {
                if (IsClassicMode ? _classicControl.EditCurrentSelection() : _canvas.EditCurrentSelection())
                {
                    return true;
                }
            }
        }
        if (keyData == Keys.Space)
        {
            if (ActiveControl is not TextBoxBase && !(ActiveControl is ContainerControl cc && cc.ActiveControl is TextBoxBase) && !(ActiveControl is ButtonBase))
            {
                if (IsClassicMode ? _classicControl.ToggleCurrentSelectionEnabled() : _canvas.ToggleCurrentSelectionEnabled())
                {
                    return true;
                }
            }
        }
        if (keyData == Keys.Escape)
        {
            if (!IsClassicMode && (ActiveControl == _canvas || ActiveControl is not TextBoxBase))
            {
                _canvas.DeselectAll();
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void SetupTabButtonPaint(Button btn, Func<bool> isActiveFunc, Func<string> fullTextFunc, Func<string> iconTextFunc)
    {
        btn.Paint += (s, e) =>
        {
            bool isActive = isActiveFunc();
            bool isDark = AppSettings.Current.IsDarkMode;
            Color activeBg = isDark ? Color.FromArgb(41, 128, 185) : Color.FromArgb(52, 152, 219);
            Color activeFg = Color.White;
            Color inactiveBg = isDark ? Color.FromArgb(26, 34, 48) : Color.FromArgb(232, 236, 241);
            Color inactiveFg = isDark ? Color.FromArgb(190, 202, 218) : Color.FromArgb(70, 80, 95);
            Color borderColor = isActive 
                ? (isDark ? Color.FromArgb(52, 152, 219) : Color.FromArgb(41, 128, 185)) 
                : (isDark ? Color.FromArgb(44, 58, 80) : Color.FromArgb(200, 208, 218));

            Color bg = isActive ? activeBg : inactiveBg;
            Color fg = isActive ? activeFg : inactiveFg;

            Color parentBg = btn.Parent?.BackColor ?? (isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(246, 248, 250));
            e.Graphics.Clear(parentBg);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 상단 양쪽 모서리 둥근 탭 패스 생성 (반경 5px)
            float r = 5f;
            float w = btn.Width - 1f;
            float h = btn.Height;

            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                // 좌하단 -> 좌상단 곡선 -> 상단 직선 -> 우상단 곡선 -> 우하단
                path.AddLine(0.5f, h, 0.5f, r);
                path.AddArc(0.5f, 0.5f, r * 2, r * 2, 180, 90);
                path.AddLine(r, 0.5f, w - r, 0.5f);
                path.AddArc(w - r * 2, 0.5f, r * 2, r * 2, 270, 90);
                path.AddLine(w, r, w, h);

                // 탭 내부 채우기 (하단까지 꽉 채움)
                using (var fillBrush = new SolidBrush(bg))
                {
                    e.Graphics.FillPath(fillBrush, path);
                }

                // 탭 상단/좌/우 테두리 그리기
                using (var borderPen = new Pen(borderColor, 1f))
                {
                    e.Graphics.DrawPath(borderPen, path);
                }

                // 비활성 탭인 경우 하단 툴바 구분선 유지, 활성 탭인 경우 하단 선을 완전히 없애 메인 뷰와 일체화
                if (!isActive)
                {
                    using var baselinePen = new Pen(borderColor, 1f);
                    e.Graphics.DrawLine(baselinePen, 0, h - 1, w, h - 1);
                }
            }

            // 텍스트/이모지 렌더링
            using var textBrush = new SolidBrush(fg);
            if (_isCompactToolbar)
            {
                using var iconFont = AppFontHelper.GetEmojiFont(10f, FontStyle.Regular);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(iconTextFunc(), iconFont, textBrush, new RectangleF(0, 0, btn.Width, btn.Height - 1), sf);
            }
            else
            {
                string full = fullTextFunc();
                var bounds = new RectangleF(4, 0, btn.Width - 8, btn.Height - 1);
                AppFontHelper.DrawMixedString(e.Graphics, full, 9f, textBrush, bounds, StringAlignment.Center, StringAlignment.Center, isActive ? FontStyle.Bold : FontStyle.Regular);
            }
        };
    }

    private static void EnableDoubleBuffered(Control control)
    {
        try
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });
        }
        catch { }
    }

}
