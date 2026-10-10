using System.Drawing;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 유니티 Shader Graph / ComfyUI 스타일의 시각적 FSM 상태 노드 캔버스 컨트롤입니다.
/// 노드 카드 내부에서 조건(Trigger)과 결과(Action)를 칩 형태로 시각화하고,
/// 인라인 추가/수정/삭제/녹화 조작 및 베지어 곡선 전이선 연결, 줌/팬을 지원합니다.
/// (모듈 분할: Rendering, Layout, Interactions, Widgets partial classes로 구성)
/// </summary>
public partial class NodeCanvasControl : Control
{
    private MacroEventProfile _profile = new();
    public MacroEventProfile Profile
    {
        get => _profile;
        set
        {
            string? prevSelectedEventId = SelectedEvent?.Id;
            _profile = value;
            EnsureNodePositions();
            _pinGraph = PinGraphConverter.FromProfile(_profile);

            // Undo/Redo 역직렬화 후 SelectedEvent 객체 재바인딩
            if (!string.IsNullOrEmpty(prevSelectedEventId))
            {
                SelectedEvent = _profile.Events.FirstOrDefault(e => e.Id == prevSelectedEventId);
            }
            else
            {
                SelectedEvent = null;
            }
            SelectedTriggerChip = null;
            SelectedActionChip = null;
            Invalidate();
        }
    }

    private PinGraphModel _pinGraph = new();
    public PinGraphModel PinGraph
    {
        get => _pinGraph;
        set
        {
            _pinGraph = value;
            Invalidate();
        }
    }

    public bool IsPinGraphMode { get; set; } = true; // 완전 자유형 핀 그래프 기본 활성화
    public string? SelectedGraphNodeId { get; set; }
    public HashSet<string> SelectedGraphNodeIds { get; } = new();
    public GraphNode? SelectedGraphNode
    {
        get => string.IsNullOrEmpty(SelectedGraphNodeId) ? null : PinGraph.FindNode(SelectedGraphNodeId);
        set
        {
            SelectedGraphNodeId = value?.Id;
            if (value != null && !SelectedGraphNodeIds.Contains(value.Id))
            {
                SelectedGraphNodeIds.Clear();
                SelectedGraphNodeIds.Add(value.Id);
            }
            else if (value == null)
            {
                SelectedGraphNodeIds.Clear();
            }
        }
    }
    public GraphPin? HoveredPin { get; private set; }
    public GraphPin? DraggingSourcePin { get; private set; }
    public GraphNode? DraggingSourceNode { get; private set; }
    public PointF DraggingCurrentWorldPt { get; private set; }
    public bool IsPinDragging { get; private set; }
    public event Action<GraphNode?>? GraphNodeSelected;

    // 뷰포트 변환 (Pan & Zoom: FSM 뷰와 핀 그래프 뷰 독립 보존)
    private PointF _fsmPanOffset = new(50, 50);
    private float _fsmZoom = 1.0f;
    private PointF _pinGraphPanOffset = new(50, 50);
    private float _pinGraphZoom = 1.0f;

    public PointF PanOffset
    {
        get => IsPinGraphMode ? _pinGraphPanOffset : _fsmPanOffset;
        set
        {
            if (IsPinGraphMode) _pinGraphPanOffset = value;
            else _fsmPanOffset = value;
        }
    }

    public float Zoom
    {
        get => IsPinGraphMode ? _pinGraphZoom : _fsmZoom;
        set
        {
            if (IsPinGraphMode) _pinGraphZoom = value;
            else _fsmZoom = value;
        }
    }

    // 테마 설정
    public bool IsDarkMode { get; private set; } = true;

    public void ApplyTheme(bool isDark)
    {
        IsDarkMode = isDark;
        BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.FromArgb(232, 242, 252);
        Invalidate();
    }

    // 선택 상태
    public string? SelectedState { get; set; }
    public MacroEvent? SelectedEvent { get; set; }

    public void SelectState(string? state)
    {
        SelectedState = state;
        SelectedStates.Clear();
        if (!string.IsNullOrEmpty(state))
        {
            SelectedStates.Add(state);
        }
        Invalidate();
    }

    // 런타임 활성 상태 (실행 중 빛나는 노드)
    private string? _activeState;
    public string? ActiveState
    {
        get => _activeState;
        set
        {
            if (_activeState != value)
            {
                _activeState = value;
                Invalidate();
            }
        }
    }

    public string? ActiveActionEvtId { get; private set; }
    public int ActiveActionIndex { get; private set; } = -1;

    public void SetActiveAction(string? evtId, int actIdx)
    {
        if (ActiveActionEvtId != evtId || ActiveActionIndex != actIdx)
        {
            ActiveActionEvtId = evtId;
            ActiveActionIndex = actIdx;
            Invalidate();
        }
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string EventId, int TriggerIndex), DateTime> _firedTriggers = new();

    public void NotifyTriggerFired(string evtId, int triggerIdx)
    {
        _firedTriggers[(evtId, triggerIdx)] = DateTime.Now;
        Invalidate();
    }

    // 마우스 인터랙션 상태
    private bool _isPanning = false;
    private Point _lastMousePos;
    private string? _draggingState;
    private PointF _dragNodeOffset;
    private bool _hasRecordedDragUndo = false;
    private readonly Dictionary<string, PointF> _multiDragInitialOffsets = new();

    // 우클릭 제어 (키 업 시에만 팝업, 클릭 대상 하이라이트)
    private bool _isRightDown = false;
    private Point _rightMouseDownScreenPt;
    private PointF _rightMouseDownWorldPt;

    // 포트 연결 드래그
    private bool _isConnecting = false;
    private MacroEvent? _connectingEvent;
    private PointF _connectingStartPt;
    private PointF _connectingCurrentWorldPt;
    private string? _connectingTargetHoverState;


    // 빈 공간 마키 드래그 영역 선택
    private bool _isMarqueeSelecting = false;
    private PointF _marqueeStartWorldPt;
    private PointF _marqueeCurrentWorldPt;
    public readonly HashSet<string> SelectedStates = new();

    // 템플릿 이미지 썸네일 캐시
    private readonly Dictionary<string, Image> _thumbnailCache = new();

    // 선택된 칩 정보
    public (MacroEvent Event, MacroTrigger Trigger)? SelectedTriggerChip { get; private set; }
    public (MacroEvent Event, MacroAction Action)? SelectedActionChip { get; private set; }

    // 이벤트 델리게이트
    public event Action<string?, MacroEvent?>? SelectionChanged;
    public event Action? ProfileModified;
    public event Action<string>? RecordUndoRequested;
    public event Action<string>? StateDoubleClicked;

    // FSM 상태 노드 조작 델리게이트
    public event Action? AddNewStateRequested;
    public event Action<string>? RenameStateRequested;
    public event Action<string>? SetInitialStateRequested;
    public event Action<string>? DeleteStateRequested;
    public event Action<List<string>>? DeleteMultipleStatesRequested;
    public event Action? ToggleInspectorRequested;
    public Func<bool>? IsInspectorExpandedFunc;

    // 노드 인라인 ComfyUI 스타일 인터랙션 델리게이트
    public event Action<MacroEvent, MacroTrigger>? TriggerEditRequested;
    public event Action<MacroEvent>? TriggerAddRequested;
    public event Action<MacroEvent, MacroTrigger>? TriggerDeleteRequested;
    public event Action<MacroEvent, MacroAction>? ActionEditRequested;
    public event Action<MacroEvent>? ActionAddRequested;
    public event Action<MacroEvent>? RecordMacroRequested;
    public event Action<MacroEvent, MacroAction>? ActionDeleteRequested;
    public event Action<string, MacroEvent>? EventDeleteRequested;
    public event Action<string>? AddEventToStateRequested;
    public event Action<MacroEvent>? EventEditRequested;
    public event Action? CopyRequested;
    public event Action? PasteRequested;

    // 런타임 펄스 애니메이션용 타이머
    private readonly System.Windows.Forms.Timer _animTimer;
    private float _pulsePhase = 0f;

    public NodeCanvasControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);

        BackColor = Color.FromArgb(24, 26, 32); // 모던 다크 블루프린트 배경

        _animTimer = new System.Windows.Forms.Timer { Interval = 40 }; // 25 FPS
        _animTimer.Tick += (s, e) =>
        {
            bool hasNoNodes = IsPinGraphMode && (_profile == null || _profile.Events.Count == 0 || PinGraph.Nodes.Count(n => n.NodeType != GraphNodeType.Start) == 0);
            if (!string.IsNullOrEmpty(_activeState) || IsPinDragging || hasNoNodes)
            {
                _pulsePhase = (_pulsePhase + 0.15f) % (float)(Math.PI * 2);
                Invalidate();
            }
        };
        _animTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
