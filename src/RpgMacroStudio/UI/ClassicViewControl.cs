using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 표와 트리가 완벽히 결합된 [클래식 계층 표 뷰 (Hierarchical Tree-Table View)] 컨트롤입니다.
/// 상태 ➔ 이벤트 ➔ 발동 조건 및 행동 커맨드가 정갈한 컬럼 표 안에서 계층적으로 펼쳐집니다.
/// </summary>
public class ClassicViewControl : UserControl
{
    private const int SB_HORZ = 0;
    [DllImport("user32.dll")]
    private static extern int ShowScrollBar(IntPtr hWnd, int wBar, int bShow);

    private MacroEventProfile _profile = new();
    private bool _isUpdatingUi = false;
    private bool _isAdjustingColumns = false;

    public MacroEventProfile Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            RefreshStateTabs();
        }
    }

    public string? SelectedState { get; private set; }
    public MacroEvent? SelectedEvent { get; private set; }

    public event Action? ProfileModified;
    public event Action<string>? RecordUndoRequested;
    public event Action<string?, MacroEvent?>? SelectionChanged;
    public event Action<MacroEvent>? EventEditRequested;
    public event Action? AddNewStateRequested;
    public event Action<string>? RenameStateRequested;
    public event Action<string>? SetInitialStateRequested;
    public event Action<string>? DeleteStateRequested;
    public event Action<MacroEvent>? AddActionRequested;
    public event Action<MacroEvent, MacroAction>? EditActionRequested;
    public event Action<MacroEvent, MacroAction>? DeleteActionRequested;
    public event Action<MacroEvent>? AddTriggerRequested;
    public event Action<MacroEvent, MacroTrigger>? EditTriggerRequested;
    public event Action<MacroEvent, MacroTrigger>? DeleteTriggerRequested;
    public event Action<MacroEvent>? RecordMacroRequested;

    // 접기/펼치기 상태 기억 (기본: 모두 펼침)
    private readonly HashSet<string> _collapsedStates = new();
    private readonly HashSet<string> _collapsedEvents = new();
    private string _activeTabFilter = "ALL_STATES";
    private ImageList _dummyImageList = null!;

    // UI 컨트롤
    private Panel _pnlTabsHeaderContainer = null!;
    private Button _btnTabPrev = null!;
    private Button _btnTabNext = null!;
    private Panel _pnlTabsViewport = null!;
    private FlowLayoutPanel _pnlStateTabs = null!;
    private int _tabScrollOffset = 0;

    private FlowLayoutPanel _pnlStateButtons = null!;
    private readonly ToolTip _toolbarToolTip = new() { ShowAlways = true };
    private Button _btnAddState = null!;
    private Button _btnRenameState = null!;
    private Button _btnSetInitial = null!;
    private Button _btnDeleteState = null!;
    private Button _btnAddEvent = null!;
    private Button _btnDeleteEvent = null!;
    private Button _btnAddTrigger = null!;
    private Button _btnAddAction = null!;

    private Panel _pnlListContainer = null!;
    private DarkListView _lvEvents = null!;
    private DarkScrollBar _scrollEvents = null!;
    private ContextMenuStrip _ctxClassicMenu = null!;
    private ListViewItem? _draggedItem;
    private ListViewItem? _dropTargetItem;
    private bool _dropInsertAfter;

    // 하위 호환성 유지용 플래그 및 더미
    private bool _isTreeViewMode = true;
    public bool IsTreeViewMode
    {
        get => _isTreeViewMode;
        set => SetTreeViewMode(value);
    }

    public void SetTreeViewMode(bool isTree)
    {
        _isTreeViewMode = isTree;
        PopulateHierarchicalTable();
    }

    public ClassicViewControl()
    {
        InitializeComponent();
        ApplyLanguage();
        ApplyTheme(AppSettings.Current.IsDarkMode);
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(13, 17, 24);

        // 1. 상단 상태 탭 바
        _pnlTabsHeaderContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(0),
            BackColor = Color.FromArgb(16, 20, 28)
        };

        _btnTabPrev = new Button
        {
            Text = "◀",
            Dock = DockStyle.Left,
            Width = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 26, 36),
            ForeColor = Color.FromArgb(180, 195, 215),
            Cursor = Cursors.Hand,
            Font = AppFontHelper.GetEmojiFont(8f)
        };
        _btnTabPrev.FlatAppearance.BorderSize = 0;
        _btnTabPrev.Click += (s, e) => ScrollTabs(140);

        _btnTabNext = new Button
        {
            Text = "▶",
            Dock = DockStyle.Right,
            Width = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 26, 36),
            ForeColor = Color.FromArgb(180, 195, 215),
            Cursor = Cursors.Hand,
            Font = AppFontHelper.GetEmojiFont(8f)
        };
        _btnTabNext.FlatAppearance.BorderSize = 0;
        _btnTabNext.Click += (s, e) => ScrollTabs(-140);

        _pnlTabsViewport = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            BackColor = Color.Transparent
        };

        _pnlStateTabs = new FlowLayoutPanel
        {
            Location = new Point(0, 4),
            Height = 30,
            Padding = new Padding(2, 0, 2, 0),
            BackColor = Color.Transparent,
            WrapContents = false,
            AutoScroll = false,
            AutoSize = true
        };
        _pnlTabsViewport.Controls.Add(_pnlStateTabs);

        _pnlTabsHeaderContainer.Controls.Add(_pnlTabsViewport);
        _pnlTabsHeaderContainer.Controls.Add(_btnTabPrev);
        _pnlTabsHeaderContainer.Controls.Add(_btnTabNext);

        // 2. 툴바 버튼
        _pnlStateButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(6, 4, 6, 4),
            BackColor = Color.FromArgb(20, 26, 36),
            WrapContents = false,
            AutoScroll = false
        };

        _btnAddState = CreateModernButton("➕ 상태", () => AddNewStateRequested?.Invoke());
        _btnRenameState = CreateModernButton("✏️ 이름변경", () =>
        {
            if (!string.IsNullOrEmpty(SelectedState)) RenameStateRequested?.Invoke(SelectedState);
        });
        _btnSetInitial = CreateModernButton("🚩 시작지정", () =>
        {
            if (!string.IsNullOrEmpty(SelectedState)) SetInitialStateRequested?.Invoke(SelectedState);
        });
        _btnDeleteState = CreateModernButton("🗑️ 상태삭제", () =>
        {
            if (!string.IsNullOrEmpty(SelectedState)) DeleteStateRequested?.Invoke(SelectedState);
        });

        var separator1 = CreateSeparator();
        _btnAddEvent = CreateModernButton("⚡ 새 이벤트", OnAddEvent);
        _btnDeleteEvent = CreateModernButton("🗑️ 삭제", () => DeleteCurrentSelection());

        var separator2 = CreateSeparator();
        _btnAddTrigger = CreateModernButton("👁️ 조건 추가", () =>
        {
            if (SelectedEvent != null) AddTriggerRequested?.Invoke(SelectedEvent);
        });
        _btnAddAction = CreateModernButton("🎬 행동 추가", () =>
        {
            if (SelectedEvent != null) AddActionRequested?.Invoke(SelectedEvent);
        });

        _pnlStateButtons.Controls.AddRange(new Control[]
        {
            _btnAddState, _btnRenameState, _btnSetInitial, _btnDeleteState,
            separator1,
            _btnAddEvent, _btnDeleteEvent,
            separator2,
            _btnAddTrigger, _btnAddAction
        });

        // 3. 계층형 트리 테이블 ListView
        _dummyImageList = new ImageList { ImageSize = new Size(1, 34) };
        _lvEvents = new DarkListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            BackColor = Color.FromArgb(13, 17, 24),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = AppFontHelper.GetDefaultFont(9.5f),
            OwnerDraw = true,
            AllowDrop = true,
            SmallImageList = _dummyImageList
        };

        // 컬럼 헤더 커스텀 렌더링
        _lvEvents.DrawColumnHeader += (s, e) =>
        {
            bool isDark = AppSettings.Current.IsDarkMode;
            using var bgBrush = new SolidBrush(isDark ? Color.FromArgb(24, 32, 46) : SystemColors.Control);
            using var borderPen = new Pen(isDark ? Color.FromArgb(44, 58, 80) : SystemColors.ControlDark);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
            e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);

            // 마지막 컬럼일 경우, 우측에 남는 빈 헤더 영역이 있다면 동일한 헤더 다크 색상으로 끝까지 채움 (흰색 빈 공간 원천 방지)
            if (e.ColumnIndex == _lvEvents.Columns.Count - 1 && e.Bounds.Right < _lvEvents.ClientSize.Width)
            {
                var extraHeaderRect = new Rectangle(e.Bounds.Right, e.Bounds.Y, _lvEvents.ClientSize.Width - e.Bounds.Right, e.Bounds.Height);
                e.Graphics.FillRectangle(bgBrush, extraHeaderRect);
            }

            using var textBrush = new SolidBrush(isDark ? Color.White : Color.Black);
            using var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = (e.ColumnIndex == 1) ? StringAlignment.Center : StringAlignment.Near
            };
            var textRect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
            e.Graphics.DrawString(e.Header?.Text ?? "", e.Font ?? _lvEvents.Font, textBrush, textRect, sf);
        };

        _lvEvents.DrawItem += (s, e) => { /* DrawSubItem에서 행 전체 렌더링 */ };

        // 서브아이템(행 및 계층) 커스텀 렌더링 (2줄 텍스트 지원)
        _lvEvents.DrawSubItem += (s, e) =>
        {
            if (e.Item == null) return;
            bool isDark = AppSettings.Current.IsDarkMode;
            bool isSelected = e.Item.Selected;

            // 계층 레벨에 따른 스타일 차별화
            bool isStateRow = e.Item.Tag is string;
            bool isEventRow = e.Item.Tag is MacroEvent;

            Color bgColor;
            if (isSelected)
            {
                bgColor = isDark ? Color.FromArgb(41, 65, 95) : Color.FromArgb(215, 230, 248);
            }
            else if (isStateRow)
            {
                bgColor = isDark ? Color.FromArgb(22, 32, 48) : Color.FromArgb(226, 236, 248);
            }
            else if (isEventRow)
            {
                bgColor = (e.ItemIndex % 2 == 0)
                    ? (isDark ? Color.FromArgb(16, 22, 32) : Color.White)
                    : (isDark ? Color.FromArgb(20, 26, 38) : Color.FromArgb(248, 250, 253));
            }
            else
            {
                // 하위 조건 및 행동 행
                bgColor = (e.ItemIndex % 2 == 0)
                    ? (isDark ? Color.FromArgb(12, 16, 24) : Color.FromArgb(252, 253, 255))
                    : (isDark ? Color.FromArgb(15, 20, 30) : Color.FromArgb(245, 248, 252));
            }

            using (var bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            // 선택된 행 좌측 액센트 바
            if (isSelected && e.ColumnIndex == 0)
            {
                using var accentBrush = new SolidBrush(Color.FromArgb(52, 152, 219));
                e.Graphics.FillRectangle(accentBrush, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
            }

            Color textColor;
            if (isSelected)
            {
                textColor = isDark ? Color.White : Color.FromArgb(20, 40, 70);
            }
            else if (isStateRow)
            {
                textColor = isDark ? Color.FromArgb(110, 205, 255) : Color.FromArgb(25, 80, 140);
            }
            else if (isEventRow)
            {
                if (e.Item.Tag is MacroEvent me && !me.IsEnabled)
                {
                    textColor = isDark ? Color.FromArgb(135, 145, 160) : Color.FromArgb(145, 155, 165);
                }
                else
                {
                    textColor = isDark ? Color.FromArgb(235, 240, 250) : Color.FromArgb(30, 40, 55);
                }
            }
            else
            {
                textColor = isDark ? Color.FromArgb(180, 195, 215) : Color.FromArgb(70, 80, 95);
            }

            Font f = e.Item.Font ?? _lvEvents.Font;

            // 1번 컬럼: [상태] 전용 렌더링 (초록볼드체 ● ON / 빨간볼드체 ○ OFF)
            if (e.ColumnIndex == 1)
            {
                string statusText = e.SubItem?.Text ?? "";
                if (statusText == "● ON")
                {
                    textColor = isDark ? Color.FromArgb(46, 204, 113) : Color.FromArgb(39, 174, 96);
                    f = AppFontHelper.GetEmojiFont(9.2f, FontStyle.Bold);
                }
                else if (statusText == "○ OFF")
                {
                    textColor = isDark ? Color.FromArgb(231, 76, 60) : Color.FromArgb(192, 57, 43);
                    f = AppFontHelper.GetEmojiFont(9.2f, FontStyle.Bold);
                }
            }
            // 2번 컬럼: [분류] 전용 깔끔한 색상 렌더링
            else if (e.ColumnIndex == 2)
            {
                textColor = isDark ? Color.FromArgb(100, 180, 240) : Color.FromArgb(41, 128, 185);
            }

            using var textBrush = new SolidBrush(textColor);
            using var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = (e.ColumnIndex == 1 || e.ColumnIndex == 2) ? StringAlignment.Center : StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            };
            if (e.ColumnIndex == 3)
            {
                sf.FormatFlags = 0; // 상세 내용 2줄 자동 줄바꿈 허용
            }

            var textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            e.Graphics.DrawString(e.SubItem?.Text ?? "", f, textBrush, textRect, sf);

            // 마지막 컬럼일 경우, 우측에 남는 빈 행 영역이 있다면 동일한 행 배경 다크 색상으로 끝까지 채움 (흰색 빈 공간 원천 방지)
            if (e.ColumnIndex == _lvEvents.Columns.Count - 1 && e.Bounds.Right < _lvEvents.ClientSize.Width)
            {
                var extraRect = new Rectangle(e.Bounds.Right, e.Bounds.Y, _lvEvents.ClientSize.Width - e.Bounds.Right, e.Bounds.Height);
                using var extraBrush = new SolidBrush(bgColor);
                e.Graphics.FillRectangle(extraBrush, extraRect);
            }

            // 드래그 앤 드롭 행 순서 변경 삽입 가이드 라인 렌더링
            if (_dropTargetItem != null && e.Item == _dropTargetItem)
            {
                int lineY = _dropInsertAfter ? (e.Bounds.Bottom - 2) : e.Bounds.Top;
                using var dropPen = new Pen(Color.FromArgb(52, 152, 219), 2.5f);
                e.Graphics.DrawLine(dropPen, e.Bounds.X, lineY, e.Bounds.Right, lineY);
                if (e.ColumnIndex == 0)
                {
                    using var arrowBrush = new SolidBrush(Color.FromArgb(52, 152, 219));
                    e.Graphics.FillPolygon(arrowBrush, new Point[]
                    {
                        new(e.Bounds.X + 1, lineY - 4),
                        new(e.Bounds.X + 8, lineY),
                        new(e.Bounds.X + 1, lineY + 4)
                    });
                }
            }
        };

        // 컬럼 추가 (핵심 4개 목록: 항목 계층, 상태, 분류, 상세 내용으로 완전 일원화)
        bool isKo = Localization.IsKorean;
        _lvEvents.Columns.Add(isKo ? "항목 / 계층 구조" : "Item / Hierarchy", 260);
        _lvEvents.Columns.Add(isKo ? "상태" : "Status", 70);
        _lvEvents.Columns.Add(isKo ? "분류" : "Category", 95);
        _lvEvents.Columns.Add(isKo ? "상세 내용" : "Detail Content", 360);

        _lvEvents.Resize += (s, e) => AdjustColumnsToFill();

        // 선택 변경 이벤트
        _lvEvents.SelectedIndexChanged += (s, e) =>
        {
            if (_isUpdatingUi || _lvEvents.SelectedItems.Count == 0) return;
            var item = _lvEvents.SelectedItems[0];
            HandleSelection(item.Tag);
        };

        // 클릭 시 접기/펼치기 처리 및 상태 컬럼 클릭 시 온/오프 토글
        _lvEvents.MouseClick += (s, e) =>
        {
            var hit = _lvEvents.HitTest(e.Location);
            if (hit.Item != null)
            {
                if (e.X <= 45) // 화살표 영역 클릭
                {
                    ToggleExpandCollapse(hit.Item.Tag);
                }
                else if (_lvEvents.Columns.Count >= 2 &&
                         e.X >= _lvEvents.Columns[0].Width &&
                         e.X <= (_lvEvents.Columns[0].Width + _lvEvents.Columns[1].Width))
                {
                    // 상태 컬럼(● ON / ○ OFF) 직접 클릭 시 즉시 토글
                    if (hit.Item.Tag is MacroEvent)
                    {
                        hit.Item.Selected = true;
                        ToggleCurrentSelectionEnabled();
                    }
                }
            }
        };

        // 더블클릭 시 편집 또는 접기/펼치기
        _lvEvents.MouseDoubleClick += (s, e) =>
        {
            var hit = _lvEvents.HitTest(e.Location);
            if (hit.Item != null)
            {
                if (hit.Item.Tag is string)
                {
                    ToggleExpandCollapse(hit.Item.Tag);
                }
                else
                {
                    EditCurrentSelection();
                }
            }
        };

        // 키보드 조작
        _lvEvents.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                if (DeleteCurrentSelection())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.F2 || e.KeyCode == Keys.Enter)
            {
                if (EditCurrentSelection())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.KeyCode == Keys.Space)
            {
                if (ToggleCurrentSelectionEnabled())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.Up)
            {
                if (MoveCurrentSelection(-1))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            else if (e.Control && e.KeyCode == Keys.Down)
            {
                if (MoveCurrentSelection(1))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
        };

        // 드래그 앤 드롭 행 순서 변경 이벤트 핸들러
        _lvEvents.ItemDrag += (s, e) =>
        {
            if (e.Item is ListViewItem lvi && lvi.Tag != null)
            {
                _draggedItem = lvi;
                _lvEvents.DoDragDrop(lvi, DragDropEffects.Move);
            }
        };

        _lvEvents.DragEnter += (s, e) =>
        {
            if (e.Data?.GetDataPresent(typeof(ListViewItem)) == true)
            {
                e.Effect = DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        };

        _lvEvents.DragOver += (s, e) =>
        {
            Point pt = _lvEvents.PointToClient(new Point(e.X, e.Y));
            var target = _lvEvents.GetItemAt(pt.X, pt.Y);
            if (target != null && _draggedItem != null && target != _draggedItem && CanDropItem(_draggedItem, target))
            {
                e.Effect = DragDropEffects.Move;
                bool insertAfter = pt.Y > (target.Bounds.Top + target.Bounds.Height / 2);
                if (_dropTargetItem != target || _dropInsertAfter != insertAfter)
                {
                    _dropTargetItem = target;
                    _dropInsertAfter = insertAfter;
                    _lvEvents.Invalidate();
                }
            }
            else
            {
                e.Effect = DragDropEffects.None;
                if (_dropTargetItem != null)
                {
                    _dropTargetItem = null;
                    _lvEvents.Invalidate();
                }
            }
        };

        _lvEvents.DragLeave += (s, e) =>
        {
            if (_dropTargetItem != null)
            {
                _dropTargetItem = null;
                _lvEvents.Invalidate();
            }
        };

        _lvEvents.DragDrop += (s, e) =>
        {
            Point pt = _lvEvents.PointToClient(new Point(e.X, e.Y));
            var target = _lvEvents.GetItemAt(pt.X, pt.Y);
            if (_draggedItem != null && target != null && target != _draggedItem && CanDropItem(_draggedItem, target))
            {
                bool insertAfter = pt.Y > (target.Bounds.Top + target.Bounds.Height / 2);
                ExecuteRowReorder(_draggedItem, target, insertAfter);
            }
            _draggedItem = null;
            _dropTargetItem = null;
            _lvEvents.Invalidate();
        };

        InitContextMenu();
        _lvEvents.ContextMenuStrip = _ctxClassicMenu;

        _pnlListContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(13, 17, 24),
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _scrollEvents = new DarkScrollBar { Dock = DockStyle.Right, Width = 14 };
        _scrollEvents.AttachTo(_lvEvents);

        // WinForms 도킹 순서: Dock=Fill 컨트롤은 항상 컬렉션 맨 앞(Index 0)에 위치해야
        // 다른 Dock=Top/Right 컨트롤들이 먼저 자리를 잡은 후 남은 영역 전체를 차지합니다.
        _pnlListContainer.Controls.Add(_scrollEvents);
        _pnlListContainer.Controls.Add(_lvEvents);
        _lvEvents.BringToFront();

        Controls.Add(_pnlListContainer);
        Controls.Add(_pnlStateButtons);
        Controls.Add(_pnlTabsHeaderContainer);
        _pnlListContainer.BringToFront();

        // 우측 인스펙터 너비 조절 및 창 크기 변경에 따른 반응형 버튼 축소 연동
        Resize += (s, e) =>
        {
            UpdateToolbarButtonsAdaptation();
            AdjustColumnsToFill();
        };
        UpdateToolbarButtonsAdaptation();
    }

    /// <summary>
    /// 오른쪽 인스펙터 패널 너비 및 화면 크기에 적응하여 상단 툴바 버튼을 아이콘 전용/전체 텍스트 모드로 자동 전환합니다.
    /// 공간이 좁아지면 상단 UI처럼 아이콘만 표시하고 마우스 오버 시 툴팁으로 버튼 이름을 노출합니다.
    /// </summary>
    private void UpdateToolbarButtonsAdaptation()
    {
        if (_btnAddState == null || _btnRenameState == null || _btnSetInitial == null || _btnDeleteState == null ||
            _btnAddEvent == null || _btnDeleteEvent == null || _btnAddTrigger == null || _btnAddAction == null) return;

        int availWidth = ClientSize.Width;
        if (availWidth <= 0) return;

        // 전체 8개 버튼과 구분선 2개를 텍스트와 함께 표시하려면 약 730px 이상의 가용 너비가 필요합니다.
        // 인스펙터 패널이 넓어지거나 창이 좁아져 가용 너비가 730px 미만이면 아이콘 모드로 자동 축소합니다.
        bool isCompact = availWidth < 730;
        bool ko = Localization.IsKorean;

        var allButtons = new[]
        {
            _btnAddState, _btnRenameState, _btnSetInitial, _btnDeleteState,
            _btnAddEvent, _btnDeleteEvent, _btnAddTrigger, _btnAddAction
        };

        if (isCompact)
        {
            // 상단 UI처럼 아이콘 크기에 딱 맞춘 슬림 정사각형 규격 (32x28px)으로 실제 버튼 크기 축소
            foreach (var btn in allButtons)
            {
                btn.AutoSize = false;
                btn.Size = new Size(32, 28);
                btn.Padding = new Padding(0);
                btn.Margin = new Padding(0, 0, 3, 0);
            }

            _btnAddState.Text = "➕";
            _btnRenameState.Text = "✏️";
            _btnSetInitial.Text = "🚩";
            _btnDeleteState.Text = "🗑️";
            _btnAddEvent.Text = "⚡";
            _btnDeleteEvent.Text = "🗑️";
            _btnAddTrigger.Text = "👁️";
            _btnAddAction.Text = "🎬";

            _toolbarToolTip.SetToolTip(_btnAddState, ko ? "새 상태 추가 (➕ 상태)" : "Add State");
            _toolbarToolTip.SetToolTip(_btnRenameState, ko ? "상태 이름변경 (✏️ 이름변경)" : "Rename State");
            _toolbarToolTip.SetToolTip(_btnSetInitial, ko ? "시작 상태 지정 (🚩 시작지정)" : "Set Initial State");
            _toolbarToolTip.SetToolTip(_btnDeleteState, ko ? "현재 상태 삭제 (🗑️ 상태삭제)" : "Delete State");
            _toolbarToolTip.SetToolTip(_btnAddEvent, ko ? "새 이벤트 추가 (⚡ 새 이벤트)" : "Add New Event");
            _toolbarToolTip.SetToolTip(_btnDeleteEvent, ko ? "선택 항목 삭제 (🗑️ 삭제)" : "Delete Selection");
            _toolbarToolTip.SetToolTip(_btnAddTrigger, ko ? "조건 추가 (👁️ 조건 추가)" : "Add Trigger");
            _toolbarToolTip.SetToolTip(_btnAddAction, ko ? "행동 추가 (🎬 행동 추가)" : "Add Action");
        }
        else
        {
            // 공간이 충분할 때 원래의 풀 텍스트 자동 크기로 복원
            foreach (var btn in allButtons)
            {
                btn.AutoSize = true;
                btn.Height = 28;
                btn.Padding = new Padding(8, 0, 8, 0);
                btn.Margin = new Padding(0, 0, 4, 0);
            }

            _btnAddState.Text = ko ? "➕ 상태" : "➕ State";
            _btnRenameState.Text = ko ? "✏️ 이름변경" : "✏️ Rename";
            _btnSetInitial.Text = ko ? "🚩 시작지정" : "🚩 Initial";
            _btnDeleteState.Text = ko ? "🗑️ 상태삭제" : "🗑️ Del State";
            _btnAddEvent.Text = ko ? "⚡ 새 이벤트" : "⚡ New Event";
            _btnDeleteEvent.Text = ko ? "🗑️ 삭제" : "🗑️ Delete";
            _btnAddTrigger.Text = ko ? "👁️ 조건 추가" : "👁️ Add Trigger";
            _btnAddAction.Text = ko ? "🎬 행동 추가" : "🎬 Add Action";

            _toolbarToolTip.SetToolTip(_btnAddState, ko ? "새 상태 추가" : "Add State");
            _toolbarToolTip.SetToolTip(_btnRenameState, ko ? "상태 이름변경" : "Rename State");
            _toolbarToolTip.SetToolTip(_btnSetInitial, ko ? "시작 상태 지정" : "Set Initial State");
            _toolbarToolTip.SetToolTip(_btnDeleteState, ko ? "현재 상태 삭제" : "Delete State");
            _toolbarToolTip.SetToolTip(_btnAddEvent, ko ? "새 이벤트 추가" : "Add New Event");
            _toolbarToolTip.SetToolTip(_btnDeleteEvent, ko ? "선택 항목 삭제" : "Delete Selection");
            _toolbarToolTip.SetToolTip(_btnAddTrigger, ko ? "조건 추가" : "Add Trigger");
            _toolbarToolTip.SetToolTip(_btnAddAction, ko ? "행동 추가" : "Add Action");
        }
    }

    private Button CreateModernButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            Height = 28,
            AutoSize = true,
            Margin = new Padding(0, 0, 4, 0),
            Padding = new Padding(8, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = AppFontHelper.GetEmojiFont(9f)
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.Click += (s, e) => onClick();
        return btn;
    }

    private Control CreateSeparator()
    {
        return new Label
        {
            Text = "│",
            Width = 18,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(70, 90, 115)
        };
    }

    private void ScrollTabs(int delta)
    {
        int totalWidth = _pnlStateTabs.Width;
        int viewWidth = _pnlTabsViewport.Width;
        if (totalWidth <= viewWidth)
        {
            _tabScrollOffset = 0;
        }
        else
        {
            int minOffset = viewWidth - totalWidth - 8;
            _tabScrollOffset = Math.Clamp(_tabScrollOffset + delta, minOffset, 0);
        }
        _pnlStateTabs.Location = new Point(_tabScrollOffset, 4);
    }

    private void InitContextMenu()
    {
        _ctxClassicMenu = new ContextMenuStrip();
        bool ko = Localization.IsKorean;

        var itemAddState = new ToolStripMenuItem(ko ? "➕ 새 상태 추가" : "➕ Add State", null, (s, e) => AddNewStateRequested?.Invoke());
        var itemAddEvent = new ToolStripMenuItem(ko ? "⚡ 새 이벤트 추가" : "⚡ Add Event", null, (s, e) => OnAddEvent());
        var itemAddTrigger = new ToolStripMenuItem(ko ? "👁️ 새 발동 조건 추가..." : "👁️ Add Trigger...", null, (s, e) =>
        {
            if (SelectedEvent != null) AddTriggerRequested?.Invoke(SelectedEvent);
        });
        var itemAddAction = new ToolStripMenuItem(ko ? "🎬 새 행동 커맨드 추가..." : "🎬 Add Action...", null, (s, e) =>
        {
            if (SelectedEvent != null) AddActionRequested?.Invoke(SelectedEvent);
        });
        var itemRecord = new ToolStripMenuItem(ko ? "🔴 매크로 즉시 녹화..." : "🔴 Record Macro...", null, (s, e) =>
        {
            if (SelectedEvent != null) RecordMacroRequested?.Invoke(SelectedEvent);
        });

        var sep1 = new ToolStripSeparator();
        var itemEdit = new ToolStripMenuItem(ko ? "✏️ 항목 편집 / 이름 변경 (F2/Enter)" : "✏️ Edit / Rename", null, (s, e) => EditCurrentSelection());
        var itemToggle = new ToolStripMenuItem(ko ? "🔄 활성 / 비활성 토글 (Space)" : "🔄 Toggle Enabled", null, (s, e) => ToggleCurrentSelectionEnabled());
        var sep2 = new ToolStripSeparator();
        var itemMoveUp = new ToolStripMenuItem(ko ? "▲ 위로 이동 (Ctrl+↑)" : "▲ Move Up (Ctrl+↑)", null, (s, e) => MoveCurrentSelection(-1));
        var itemMoveDown = new ToolStripMenuItem(ko ? "▼ 아래로 이동 (Ctrl+↓)" : "▼ Move Down (Ctrl+↓)", null, (s, e) => MoveCurrentSelection(1));
        var sep3 = new ToolStripSeparator();
        var itemDelete = new ToolStripMenuItem(ko ? "🗑️ 삭제 (Delete)" : "🗑️ Delete", null, (s, e) => DeleteCurrentSelection());

        _ctxClassicMenu.Items.AddRange(new ToolStripItem[]
        {
            itemAddState, itemAddEvent, itemAddTrigger, itemAddAction, itemRecord,
            sep1,
            itemEdit, itemToggle,
            sep2,
            itemMoveUp, itemMoveDown,
            sep3,
            itemDelete
        });
    }

    private bool CanDropItem(ListViewItem source, ListViewItem target)
    {
        if (source == target) return false;
        var sTag = source.Tag;
        var tTag = target.Tag;
        if (sTag == null || tTag == null) return false;

        // 1. 행동 커맨드 (Action) 이동: 다른 행동 행이나 이벤트 행에 드롭 가능
        if (sTag is Tuple<MacroEvent, MacroAction>)
        {
            return tTag is Tuple<MacroEvent, MacroAction> || tTag is MacroEvent;
        }

        // 2. 발동 조건 (Trigger) 이동: 다른 조건 행이나 이벤트 행에 드롭 가능
        if (sTag is Tuple<MacroEvent, MacroTrigger>)
        {
            return tTag is Tuple<MacroEvent, MacroTrigger> || tTag is MacroEvent;
        }

        // 3. 이벤트 (Event) 이동: 다른 이벤트 행이나 상태 행에 드롭 가능
        if (sTag is MacroEvent)
        {
            return tTag is MacroEvent || tTag is string;
        }

        return false;
    }

    private void ExecuteRowReorder(ListViewItem source, ListViewItem target, bool insertAfter)
    {
        var sTag = source.Tag;
        var tTag = target.Tag;
        if (sTag == null || tTag == null) return;

        bool ko = Localization.IsKorean;

        // 1. 행동 커맨드 (Action) 순서 변경 / 이동
        if (sTag is Tuple<MacroEvent, MacroAction> srcActTuple)
        {
            var srcEvt = srcActTuple.Item1;
            var act = srcActTuple.Item2;

            if (tTag is Tuple<MacroEvent, MacroAction> dstActTuple)
            {
                var dstEvt = dstActTuple.Item1;
                var dstAct = dstActTuple.Item2;

                if (srcEvt == dstEvt)
                {
                    int oldIdx = srcEvt.Actions.IndexOf(act);
                    int newIdx = srcEvt.Actions.IndexOf(dstAct);
                    if (oldIdx >= 0 && newIdx >= 0 && oldIdx != newIdx)
                    {
                        RecordUndoRequested?.Invoke(ko ? $"행동 순서 변경: [{act.Description}]" : $"Reorder Action: [{act.Description}]");
                        srcEvt.Actions.RemoveAt(oldIdx);
                        if (insertAfter && oldIdx > newIdx) newIdx++;
                        else if (!insertAfter && oldIdx < newIdx) newIdx--;
                        newIdx = Math.Clamp(newIdx, 0, srcEvt.Actions.Count);
                        srcEvt.Actions.Insert(newIdx, act);

                        ProfileModified?.Invoke();
                        PopulateHierarchicalTable();
                    }
                }
                else
                {
                    RecordUndoRequested?.Invoke(ko ? $"행동 이동: [{act.Description}] ➔ [{dstEvt.Name}]" : $"Move Action: [{act.Description}] ➔ [{dstEvt.Name}]");
                    srcEvt.Actions.Remove(act);
                    int insertIdx = dstEvt.Actions.IndexOf(dstAct);
                    if (insertAfter && insertIdx >= 0) insertIdx++;
                    insertIdx = Math.Clamp(insertIdx, 0, dstEvt.Actions.Count);
                    dstEvt.Actions.Insert(insertIdx, act);

                    ProfileModified?.Invoke();
                    PopulateHierarchicalTable();
                }
            }
            else if (tTag is MacroEvent dstEvt)
            {
                RecordUndoRequested?.Invoke(ko ? $"행동 이동: [{act.Description}] ➔ [{dstEvt.Name}]" : $"Move Action: [{act.Description}] ➔ [{dstEvt.Name}]");
                srcEvt.Actions.Remove(act);
                if (insertAfter) dstEvt.Actions.Add(act);
                else dstEvt.Actions.Insert(0, act);

                ProfileModified?.Invoke();
                PopulateHierarchicalTable();
            }
            return;
        }

        // 2. 발동 조건 (Trigger) 순서 변경 / 이동
        if (sTag is Tuple<MacroEvent, MacroTrigger> srcTrgTuple)
        {
            var srcEvt = srcTrgTuple.Item1;
            var trg = srcTrgTuple.Item2;

            if (tTag is Tuple<MacroEvent, MacroTrigger> dstTrgTuple)
            {
                var dstEvt = dstTrgTuple.Item1;
                var dstTrg = dstTrgTuple.Item2;

                if (srcEvt == dstEvt)
                {
                    int oldIdx = srcEvt.Triggers.IndexOf(trg);
                    int newIdx = srcEvt.Triggers.IndexOf(dstTrg);
                    if (oldIdx >= 0 && newIdx >= 0 && oldIdx != newIdx)
                    {
                        RecordUndoRequested?.Invoke(ko ? $"발동 조건 순서 변경: [{trg.GetSummary()}]" : $"Reorder Trigger: [{trg.GetSummary()}]");
                        srcEvt.Triggers.RemoveAt(oldIdx);
                        if (insertAfter && oldIdx > newIdx) newIdx++;
                        else if (!insertAfter && oldIdx < newIdx) newIdx--;
                        newIdx = Math.Clamp(newIdx, 0, srcEvt.Triggers.Count);
                        srcEvt.Triggers.Insert(newIdx, trg);

                        ProfileModified?.Invoke();
                        PopulateHierarchicalTable();
                    }
                }
                else
                {
                    RecordUndoRequested?.Invoke(ko ? $"발동 조건 이동: [{trg.GetSummary()}] ➔ [{dstEvt.Name}]" : $"Move Trigger: [{trg.GetSummary()}] ➔ [{dstEvt.Name}]");
                    srcEvt.Triggers.Remove(trg);
                    int insertIdx = dstEvt.Triggers.IndexOf(dstTrg);
                    if (insertAfter && insertIdx >= 0) insertIdx++;
                    insertIdx = Math.Clamp(insertIdx, 0, dstEvt.Triggers.Count);
                    dstEvt.Triggers.Insert(insertIdx, trg);

                    ProfileModified?.Invoke();
                    PopulateHierarchicalTable();
                }
            }
            else if (tTag is MacroEvent dstEvt)
            {
                RecordUndoRequested?.Invoke(ko ? $"발동 조건 이동: [{trg.GetSummary()}] ➔ [{dstEvt.Name}]" : $"Move Trigger: [{trg.GetSummary()}] ➔ [{dstEvt.Name}]");
                srcEvt.Triggers.Remove(trg);
                if (insertAfter) dstEvt.Triggers.Add(trg);
                else dstEvt.Triggers.Insert(0, trg);

                ProfileModified?.Invoke();
                PopulateHierarchicalTable();
            }
            return;
        }

        // 3. 이벤트 (Event) 순서 변경 / 소속 상태 변경
        if (sTag is MacroEvent srcEvent)
        {
            if (tTag is MacroEvent dstEvent)
            {
                int oldIdx = _profile.Events.IndexOf(srcEvent);
                int newIdx = _profile.Events.IndexOf(dstEvent);
                if (oldIdx >= 0 && newIdx >= 0 && oldIdx != newIdx)
                {
                    RecordUndoRequested?.Invoke(ko ? $"이벤트 순서 변경: [{srcEvent.Name}]" : $"Reorder Event: [{srcEvent.Name}]");
                    _profile.Events.RemoveAt(oldIdx);
                    if (insertAfter && oldIdx > newIdx) newIdx++;
                    else if (!insertAfter && oldIdx < newIdx) newIdx--;
                    newIdx = Math.Clamp(newIdx, 0, _profile.Events.Count);
                    srcEvent.StateTag = dstEvent.StateTag;
                    _profile.Events.Insert(newIdx, srcEvent);

                    ProfileModified?.Invoke();
                    PopulateHierarchicalTable();
                }
            }
            else if (tTag is string dstState)
            {
                string targetStateTag = MacroEventProfile.IsGlobalState(dstState) ? "Global" : dstState;
                if (srcEvent.StateTag != targetStateTag)
                {
                    RecordUndoRequested?.Invoke(ko ? $"이벤트 소속 상태 변경: [{srcEvent.Name}] ➔ [{dstState}]" : $"Move Event State: [{srcEvent.Name}] ➔ [{dstState}]");
                    srcEvent.StateTag = targetStateTag;

                    ProfileModified?.Invoke();
                    PopulateHierarchicalTable();
                }
            }
        }
    }

    public bool MoveCurrentSelection(int delta)
    {
        if (_lvEvents.SelectedItems.Count == 0) return false;
        var tag = _lvEvents.SelectedItems[0].Tag;
        bool ko = Localization.IsKorean;

        // 1. 행동 커맨드
        if (tag is Tuple<MacroEvent, MacroAction> actTuple)
        {
            var evt = actTuple.Item1;
            var act = actTuple.Item2;
            int idx = evt.Actions.IndexOf(act);
            int targetIdx = idx + delta;
            if (targetIdx >= 0 && targetIdx < evt.Actions.Count)
            {
                RecordUndoRequested?.Invoke(ko ? $"행동 순서 변경: [{act.Description}]" : $"Move Action: [{act.Description}]");
                evt.Actions.RemoveAt(idx);
                evt.Actions.Insert(targetIdx, act);
                ProfileModified?.Invoke();
                PopulateHierarchicalTable();
                return true;
            }
            return false;
        }

        // 2. 발동 조건
        if (tag is Tuple<MacroEvent, MacroTrigger> trgTuple)
        {
            var evt = trgTuple.Item1;
            var trg = trgTuple.Item2;
            int idx = evt.Triggers.IndexOf(trg);
            int targetIdx = idx + delta;
            if (targetIdx >= 0 && targetIdx < evt.Triggers.Count)
            {
                RecordUndoRequested?.Invoke(ko ? $"발동 조건 순서 변경: [{trg.GetSummary()}]" : $"Move Trigger: [{trg.GetSummary()}]");
                evt.Triggers.RemoveAt(idx);
                evt.Triggers.Insert(targetIdx, trg);
                ProfileModified?.Invoke();
                PopulateHierarchicalTable();
                return true;
            }
            return false;
        }

        // 3. 이벤트
        if (tag is MacroEvent evtItem)
        {
            int idx = _profile.Events.IndexOf(evtItem);
            int targetIdx = idx + delta;
            if (targetIdx >= 0 && targetIdx < _profile.Events.Count)
            {
                RecordUndoRequested?.Invoke(ko ? $"이벤트 순서 변경: [{evtItem.Name}]" : $"Move Event: [{evtItem.Name}]");
                _profile.Events.RemoveAt(idx);
                _profile.Events.Insert(targetIdx, evtItem);
                ProfileModified?.Invoke();
                PopulateHierarchicalTable();
                return true;
            }
            return false;
        }

        return false;
    }

    private void HandleSelection(object? tag)
    {
        if (tag is string st)
        {
            SelectedState = st;
            SelectedEvent = null;
        }
        else if (tag is MacroEvent me)
        {
            SelectedEvent = me;
            SelectedState = me.StateTag;
        }
        else if (tag is Tuple<MacroEvent, MacroTrigger> trgTuple)
        {
            SelectedEvent = trgTuple.Item1;
            SelectedState = trgTuple.Item1.StateTag;
        }
        else if (tag is Tuple<MacroEvent, MacroAction> actTuple)
        {
            SelectedEvent = actTuple.Item1;
            SelectedState = actTuple.Item1.StateTag;
        }
        SelectionChanged?.Invoke(SelectedState, SelectedEvent);
    }

    public void RefreshSelectedEventItem()
    {
        PopulateHierarchicalTable();
    }

    public void RefreshEventInTree(MacroEvent? evt)
    {
        PopulateHierarchicalTable();
    }

    public void RefreshAll()
    {
        RefreshStateTabs();
    }

    private void ToggleExpandCollapse(object? tag)
    {
        if (tag is string st)
        {
            if (_collapsedStates.Contains(st)) _collapsedStates.Remove(st);
            else _collapsedStates.Add(st);
            PopulateHierarchicalTable();
        }
        else if (tag is MacroEvent me)
        {
            if (_collapsedEvents.Contains(me.Id)) _collapsedEvents.Remove(me.Id);
            else _collapsedEvents.Add(me.Id);
            PopulateHierarchicalTable();
        }
    }

    public void PopulateHierarchicalTable()
    {
        if (_lvEvents == null || _isUpdatingUi) return;
        _isUpdatingUi = true;
        _lvEvents.BeginUpdate();
        try
        {
            // 현재 선택된 태그 기억
            object? prevTag = _lvEvents.SelectedItems.Count > 0 ? _lvEvents.SelectedItems[0].Tag : null;
            _lvEvents.Items.Clear();

            bool ko = Localization.IsKorean;
            var allStates = _profile.States.ToList();
            if (!allStates.Any(s => MacroEventProfile.IsGlobalState(s)))
            {
                allStates.Insert(0, "전역 (Global)");
            }

            // 특정 상태 탭이 선택되어 있다면(전체 보기가 아닌 경우) 해당 상태만 표시
            string? filterState = (_activeTabFilter == "ALL_STATES" || string.IsNullOrEmpty(_activeTabFilter)) ? null : _activeTabFilter;

            foreach (var state in allStates)
            {
                if (filterState != null && state != filterState) continue;

                bool isGlobal = MacroEventProfile.IsGlobalState(state);
                bool isInitial = (state == _profile.InitialState);
                bool isStateCollapsed = _collapsedStates.Contains(state);

                string displayTitle = MacroEventProfile.FormatStateDisplay(state, ko);
                string stateIcon = isGlobal ? "🌐" : (isInitial ? "🚩" : "🏷️");
                string collapseIcon = isStateCollapsed ? "▶ " : "▼ ";

                // 1. 상태 행 (Level 0)
                var lviState = new ListViewItem(collapseIcon + $"{stateIcon} {displayTitle}" + (isInitial ? (ko ? " (시작 상태)" : " (Initial)") : ""))
                {
                    Tag = state,
                    Font = AppFontHelper.GetEmojiFont(9.5f, FontStyle.Bold)
                };
                lviState.SubItems.Add(""); // Col 1: 상태 (공백)
                lviState.SubItems.Add(ko ? "상태 (State)" : "State"); // Col 2: 분류
                lviState.SubItems.Add(isGlobal ? (ko ? "전역 상시 감시 파이프라인" : "Global Watch Pipeline") : (ko ? "상태 전이 진입점" : "State Entry Point")); // Col 3: 상세 내용
                _lvEvents.Items.Add(lviState);

                if (isStateCollapsed) continue;

                var eventsInState = _profile.Events
                    .Where(e => (isGlobal && MacroEventProfile.IsGlobalState(e.StateTag)) ||
                                (!isGlobal && e.StateTag == state))
                    .ToList();

                foreach (var evt in eventsInState)
                {
                    bool isEvtCollapsed = _collapsedEvents.Contains(evt.Id);
                    string evtCollapseIcon = isEvtCollapsed ? "▶ " : "▼ ";
                    string evtName = MacroEventProfile.FormatEventDisplay(evt, ko);

                    // 2. 이벤트 행 (Level 1) - 이름 항목은 순수 이름만, Col 1에 별개의 초록볼드체 ● ON / 빨간볼드체 ○ OFF 표시
                    var lviEvent = new ListViewItem("   " + evtCollapseIcon + $"◈ {evtName}")
                    {
                        Tag = evt,
                        Font = AppFontHelper.GetEmojiFont(9.2f, FontStyle.Bold)
                    };
                    string modeStr = evt.IsParallel ? (ko ? "병렬" : "Parallel") : (ko ? "순차" : "Seq");
                    lviEvent.SubItems.Add(evt.IsEnabled ? "● ON" : "○ OFF"); // Col 1: 상태
                    lviEvent.SubItems.Add(modeStr); // Col 2: 분류
                    lviEvent.SubItems.Add(ko ? $"조건 {evt.Triggers.Count}개, 행동 {evt.Actions.Count}개" : $"{evt.Triggers.Count} triggers, {evt.Actions.Count} actions"); // Col 3: 상세 내용
                    _lvEvents.Items.Add(lviEvent);

                    if (isEvtCollapsed) continue;

                    // 3. 발동 조건 행들 (Level 2)
                    for (int tIdx = 0; tIdx < evt.Triggers.Count; tIdx++)
                    {
                        var trg = evt.Triggers[tIdx];
                        var lviTrg = new ListViewItem($"      ├─ 👁️ [조건 {tIdx + 1}] {trg.GetSummary()}")
                        {
                            Tag = Tuple.Create(evt, trg),
                            Font = AppFontHelper.GetEmojiFont(9f)
                        };
                        lviTrg.SubItems.Add(""); // Col 1: 상태 (하위 항목 공백)
                        lviTrg.SubItems.Add(ko ? "발동 조건" : "Trigger"); // Col 2: 분류
                        lviTrg.SubItems.Add(FormatTriggerDetail(trg, ko)); // Col 3: 상세 내용
                        _lvEvents.Items.Add(lviTrg);
                    }

                    // 4. 행동 커맨드 행들 (Level 2)
                    for (int aIdx = 0; aIdx < evt.Actions.Count; aIdx++)
                    {
                        var act = evt.Actions[aIdx];
                        bool isLast = (aIdx == evt.Actions.Count - 1);
                        string branchLine = isLast ? "└─ " : "├─ ";

                        string actIcon = act.Type switch
                        {
                            ActionType.KeyPress => "⌨️",
                            ActionType.Wait => "⏳",
                            ActionType.MouseClick => "🖱️",
                            ActionType.VariableControl => "🔢",
                            ActionType.ChangeState => "🔄",
                            _ => "⚡"
                        };

                        var lviAct = new ListViewItem($"      {branchLine}{actIcon} [행동 {aIdx + 1}] {act.Description}")
                        {
                            Tag = Tuple.Create(evt, act),
                            Font = AppFontHelper.GetEmojiFont(9f)
                        };
                        lviAct.SubItems.Add(""); // Col 1: 상태 (하위 항목 공백)
                        lviAct.SubItems.Add(ko ? "행동 커맨드" : "Action"); // Col 2: 분류
                        lviAct.SubItems.Add(FormatActionDetail(act, ko)); // Col 3: 상세 내용
                        _lvEvents.Items.Add(lviAct);
                    }

                    if (evt.Triggers.Count == 0 && evt.Actions.Count == 0)
                    {
                        var lviEmpty = new ListViewItem(ko ? "      └─ (조건 및 행동 없음 - 더블클릭하여 추가)" : "      └─ (Empty - Double-click to add)")
                        {
                            Tag = evt,
                            Font = AppFontHelper.GetDefaultFont(9f),
                            ForeColor = Color.Gray
                        };
                        lviEmpty.SubItems.Add(""); // Col 1
                        lviEmpty.SubItems.Add(""); // Col 2
                        lviEmpty.SubItems.Add(""); // Col 3
                        _lvEvents.Items.Add(lviEmpty);
                    }
                }
            }

            // 이전 선택 복원
            if (prevTag != null)
            {
                foreach (ListViewItem it in _lvEvents.Items)
                {
                    if (Equals(it.Tag, prevTag))
                    {
                        it.Selected = true;
                        it.EnsureVisible();
                        break;
                    }
                }
            }
        }
        finally
        {
            _lvEvents.EndUpdate();
            _isUpdatingUi = false;
        }

        AdjustColumnsToFill();
        _scrollEvents?.UpdateMetrics();
    }

    private static string FormatTriggerDetail(MacroTrigger trg, bool ko)
    {
        return trg.Type switch
        {
            TriggerType.TimerInterval => ko ? $"{trg.IntervalMs}ms 주기 타이머 반복 발동" : $"{trg.IntervalMs}ms periodic timer",
            TriggerType.VisionDetection or TriggerType.GaugeBar => ko ? $"색상/게이지 비교 ({trg.GaugeThresholdPercent}% 기준)" : $"Color/Gauge match ({trg.GaugeThresholdPercent}%)",
            TriggerType.ImageDetection => ko ? $"템플릿 이미지 매칭 ({trg.ImageAccuracyPercent}% 정확도)" : $"Template match ({trg.ImageAccuracyPercent}%)",
            TriggerType.VariableCheck => $"[{trg.VariableName}] {trg.VarCompareOp} {trg.VarTargetValue}",
            _ => trg.GetSummary()
        };
    }

    private static string FormatActionDetail(MacroAction act, bool ko)
    {
        return act.Type switch
        {
            ActionType.KeyPress => ko ? $"키: [{AppKeyHelper.FormatKey(act.Key)}], 누름: {act.HoldTimeMs}ms, 후지연: {act.PostDelayMs}ms" : $"Key: [{AppKeyHelper.FormatKey(act.Key)}], Delay: {act.PostDelayMs}ms",
            ActionType.Wait => ko ? $"지연 대기: {act.WaitDurationMs}ms" : $"Wait: {act.WaitDurationMs}ms",
            ActionType.MouseClick => $"{AppKeyHelper.FormatMouseButtonShort(act.MouseButton, ko)} " + (act.TargetMode == MouseTargetMode.TriggerDetected ? (ko ? "[감지위치]" : "[Detected]") : $"({act.CustomX}, {act.CustomY})"),
            ActionType.VariableControl => $"[{act.VariableName}] {act.VariableOp} {act.VariableValue}",
            ActionType.ChangeState => ko ? $"상태 전이 ➔ [{act.TargetState}]" : $"Change State ➔ [{act.TargetState}]",
            _ => act.Description
        };
    }

    public void RefreshStateTabs()
    {
        _isUpdatingUi = true;
        try
        {
            _pnlStateTabs.Controls.Clear();
            bool ko = Localization.IsKorean;

            if (!_profile.States.Any(s => MacroEventProfile.IsGlobalState(s)))
            {
                _profile.States.Insert(0, "전역 (Global)");
            }

            // 1. [전체 계층 보기] 탭
            var btnAll = new Button
            {
                Text = ko ? "🌐 전체 계층 보기" : "🌐 All Hierarchies",
                Tag = "ALL_STATES",
                Height = 28,
                AutoSize = true,
                Margin = new Padding(0, 0, 4, 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = AppFontHelper.GetEmojiFont(9f)
            };
            btnAll.Click += (s, e) =>
            {
                _activeTabFilter = "ALL_STATES";
                SelectedState = "ALL_STATES";
                UpdateTabButtonStyles();
                PopulateHierarchicalTable();
            };
            _pnlStateTabs.Controls.Add(btnAll);

            // 2. 각 상태별 탭
            foreach (var st in _profile.States)
            {
                string displayTitle = MacroEventProfile.FormatStateDisplay(st, ko);
                string title = MacroEventProfile.IsGlobalState(st) ? $"🌐 {displayTitle}" : (st == _profile.InitialState ? $"🚩 {displayTitle}" : $"🏷️ {displayTitle}");
                var btn = new Button
                {
                    Text = title,
                    Tag = st,
                    Height = 28,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 4, 0),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = AppFontHelper.GetEmojiFont(9f)
                };
                btn.Click += (s, e) =>
                {
                    _activeTabFilter = st;
                    SelectedState = st;
                    UpdateTabButtonStyles();
                    PopulateHierarchicalTable();
                };
                _pnlStateTabs.Controls.Add(btn);
            }

            UpdateTabButtonStyles();
        }
        finally
        {
            _isUpdatingUi = false;
        }

        PopulateHierarchicalTable();
    }

    private void UpdateTabButtonStyles(bool? isDarkOverride = null)
    {
        bool isDark = isDarkOverride ?? AppSettings.Current.IsDarkMode;
        string activeState = _activeTabFilter;

        foreach (Control c in _pnlStateTabs.Controls)
        {
            if (c is Button btn && btn.Tag is string st)
            {
                bool isSelected = (st == activeState);
                if (isDark)
                {
                    btn.BackColor = isSelected ? Color.FromArgb(41, 128, 185) : Color.FromArgb(24, 32, 46);
                    btn.ForeColor = isSelected ? Color.White : Color.FromArgb(170, 185, 205);
                    btn.FlatAppearance.BorderColor = isSelected ? Color.FromArgb(52, 152, 219) : Color.FromArgb(45, 58, 78);
                    btn.FlatAppearance.BorderSize = 1;
                }
                else
                {
                    btn.BackColor = isSelected ? Color.White : Color.FromArgb(235, 238, 242);
                    btn.ForeColor = isSelected ? Color.Black : Color.FromArgb(80, 80, 80);
                    btn.FlatAppearance.BorderColor = isSelected ? Color.FromArgb(41, 128, 185) : Color.FromArgb(200, 208, 218);
                    btn.FlatAppearance.BorderSize = 1;
                }
                btn.Font = AppFontHelper.GetEmojiFont(9f, isSelected ? FontStyle.Bold : FontStyle.Regular);
            }
        }
    }

    public void FilterEventsBySelectedState()
    {
        PopulateHierarchicalTable();
    }

    public void PopulateHierarchyTree()
    {
        PopulateHierarchicalTable();
    }

    public void AdjustColumnsToFill()
    {
        if (_isAdjustingColumns) return; // 무한 재귀 및 핑퐁 루프 100% 차단
        if (_lvEvents == null || _lvEvents.Columns.Count < 4) return;
        if (!_lvEvents.IsHandleCreated || _lvEvents.IsDisposed) return;

        int clientW = _lvEvents.ClientSize.Width;
        if (clientW <= 0) return;

        _isAdjustingColumns = true;
        try
        {
            // 고정 컬럼: 상태(Col 1: 68px), 분류(Col 2: 92px)
            int wStatus = 68;
            int wCategory = 92;
            int fixedW = wStatus + wCategory;

            // 가로 스크롤바가 생기지 않도록 4픽셀의 안전 마진(-4)을 확보
            int available = Math.Max(200, clientW - fixedW - 4);

            int wHierarchy;
            int wDetail;

            if (available > 200)
            {
                // 계층 구조에 약 40%, 상세 내용에 나머지 60%
                wHierarchy = Math.Max(180, (int)(available * 0.40f));
                wDetail = Math.Max(80, available - wHierarchy);
            }
            else
            {
                wHierarchy = Math.Max(120, available / 2);
                wDetail = Math.Max(60, available - wHierarchy);
            }

            // 불필요한 Windows 레이아웃 메시지 재발생을 막기 위해 값이 달라졌을 때만 1회 대입
            if (_lvEvents.Columns[0].Width != wHierarchy) _lvEvents.Columns[0].Width = wHierarchy;
            if (_lvEvents.Columns[1].Width != wStatus) _lvEvents.Columns[1].Width = wStatus;
            if (_lvEvents.Columns[2].Width != wCategory) _lvEvents.Columns[2].Width = wCategory;
            if (_lvEvents.Columns[3].Width != wDetail) _lvEvents.Columns[3].Width = wDetail;

            try { ShowScrollBar(_lvEvents.Handle, SB_HORZ, 0); } catch { }
        }
        finally
        {
            _isAdjustingColumns = false;
        }
    }

    public bool DeleteCurrentSelection()
    {
        if (_lvEvents.SelectedItems.Count == 0) return false;
        var tag = _lvEvents.SelectedItems[0].Tag;

        if (tag is Tuple<MacroEvent, MacroAction> actTuple)
        {
            DeleteActionRequested?.Invoke(actTuple.Item1, actTuple.Item2);
            PopulateHierarchicalTable();
            return true;
        }
        if (tag is Tuple<MacroEvent, MacroTrigger> trgTuple)
        {
            DeleteTriggerRequested?.Invoke(trgTuple.Item1, trgTuple.Item2);
            PopulateHierarchicalTable();
            return true;
        }
        if (tag is MacroEvent me)
        {
            SelectedEvent = me;
            SelectedState = me.StateTag;
            OnDeleteEvent();
            return true;
        }
        if (tag is string st && !MacroEventProfile.IsGlobalState(st) && !MacroEventProfile.IsDefaultState(st))
        {
            SelectedState = st;
            DeleteStateRequested?.Invoke(st);
            return true;
        }
        return false;
    }

    public bool EditCurrentSelection()
    {
        if (_lvEvents.SelectedItems.Count == 0) return false;
        var tag = _lvEvents.SelectedItems[0].Tag;

        if (tag is Tuple<MacroEvent, MacroAction> actTuple)
        {
            EditActionRequested?.Invoke(actTuple.Item1, actTuple.Item2);
            return true;
        }
        if (tag is Tuple<MacroEvent, MacroTrigger> trgTuple)
        {
            EditTriggerRequested?.Invoke(trgTuple.Item1, trgTuple.Item2);
            return true;
        }
        if (tag is MacroEvent me)
        {
            EventEditRequested?.Invoke(me);
            return true;
        }
        if (tag is string st && !MacroEventProfile.IsGlobalState(st))
        {
            RenameStateRequested?.Invoke(st);
            return true;
        }
        return false;
    }

    public bool ToggleCurrentSelectionEnabled()
    {
        if (_lvEvents.SelectedItems.Count == 0) return false;
        var tag = _lvEvents.SelectedItems[0].Tag;
        MacroEvent? targetEvt = null;

        if (tag is MacroEvent me) targetEvt = me;
        else if (tag is Tuple<MacroEvent, MacroAction> actTuple) targetEvt = actTuple.Item1;
        else if (tag is Tuple<MacroEvent, MacroTrigger> trgTuple) targetEvt = trgTuple.Item1;

        if (targetEvt != null)
        {
            RecordUndoRequested?.Invoke(Localization.IsKorean ? $"이벤트 '{targetEvt.Name}' 활성화 토글" : $"Toggle '{targetEvt.Name}' Enabled");
            targetEvt.IsEnabled = !targetEvt.IsEnabled;
            ProfileModified?.Invoke();
            PopulateHierarchicalTable();
            return true;
        }
        return false;
    }

    private void OnAddEvent()
    {
        string targetState = (SelectedState == "ALL_STATES" || string.IsNullOrEmpty(SelectedState))
            ? (_profile.InitialState ?? _profile.States.FirstOrDefault() ?? "기본 상태")
            : SelectedState;

        var newEvent = new MacroEvent
        {
            Name = Localization.IsKorean ? $"새 이벤트 {_profile.Events.Count + 1}" : $"New Event {_profile.Events.Count + 1}",
            StateTag = targetState,
            IsEnabled = true,
            IsParallel = false
        };
        RecordUndoRequested?.Invoke(Localization.IsKorean ? $"이벤트 추가: [{newEvent.Name}]" : $"Add Event: [{newEvent.Name}]");
        _profile.Events.Add(newEvent);
        ProfileModified?.Invoke();
        PopulateHierarchicalTable();
    }

    private void OnDeleteEvent()
    {
        if (SelectedEvent == null) return;
        RecordUndoRequested?.Invoke(Localization.IsKorean ? $"이벤트 삭제: [{SelectedEvent.Name}]" : $"Delete Event: [{SelectedEvent.Name}]");
        _profile.Events.Remove(SelectedEvent);
        SelectedEvent = null;
        ProfileModified?.Invoke();
        PopulateHierarchicalTable();
    }

    public void ApplyLanguage()
    {
        bool ko = Localization.IsKorean;
        _btnAddState.Text = ko ? "➕ 상태" : "➕ State";
        _btnRenameState.Text = ko ? "✏️ 이름변경" : "✏️ Rename";
        _btnSetInitial.Text = ko ? "🚩 시작지정" : "🚩 Initial";
        _btnDeleteState.Text = ko ? "🗑️ 상태삭제" : "🗑️ Del State";
        _btnAddEvent.Text = ko ? "⚡ 새 이벤트" : "⚡ New Event";
        _btnDeleteEvent.Text = ko ? "🗑️ 삭제" : "🗑️ Delete";
        _btnAddTrigger.Text = ko ? "👁️ 조건 추가" : "👁️ Add Trigger";
        _btnAddAction.Text = ko ? "🎬 행동 추가" : "🎬 Add Action";

        if (_lvEvents.Columns.Count >= 3)
        {
            _lvEvents.Columns[0].Text = ko ? "항목 / 계층 구조" : "Item / Hierarchy";
            _lvEvents.Columns[1].Text = ko ? "분류" : "Category";
            _lvEvents.Columns[2].Text = ko ? "상세 내용" : "Details";
        }

        RefreshStateTabs();
    }

    public void ApplyTheme(bool isDark)
    {
        BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.FromArgb(242, 245, 250);
        _pnlTabsHeaderContainer.BackColor = isDark ? Color.FromArgb(16, 20, 28) : Color.FromArgb(235, 238, 244);
        _pnlStateButtons.BackColor = isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(228, 232, 240);
        if (_pnlListContainer != null)
        {
            _pnlListContainer.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
        }

        _btnTabPrev.BackColor = isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(228, 232, 240);
        _btnTabPrev.ForeColor = isDark ? Color.FromArgb(180, 195, 215) : Color.FromArgb(50, 60, 75);
        _btnTabNext.BackColor = isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(228, 232, 240);
        _btnTabNext.ForeColor = isDark ? Color.FromArgb(180, 195, 215) : Color.FromArgb(50, 60, 75);

        _lvEvents.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
        _lvEvents.ForeColor = isDark ? Color.White : Color.FromArgb(30, 30, 30);

        Color btnBg = isDark ? Color.FromArgb(28, 36, 50) : Color.FromArgb(240, 243, 248);
        Color btnFg = isDark ? Color.FromArgb(215, 225, 240) : Color.FromArgb(40, 50, 65);
        Color btnBorder = isDark ? Color.FromArgb(45, 58, 78) : Color.FromArgb(195, 205, 218);

        foreach (Control c in _pnlStateButtons.Controls)
        {
            if (c is Button btn)
            {
                btn.BackColor = btnBg;
                btn.ForeColor = btnFg;
                btn.FlatAppearance.BorderColor = btnBorder;
            }
        }

        UpdateTabButtonStyles(isDark);

        if (_lvEvents.IsHandleCreated)
        {
            try
            {
                AppThemeHelper.SetWindowTheme(_lvEvents.Handle, isDark ? "DarkMode_Explorer" : null, null);
                AppThemeHelper.RefreshListViewHeader(_lvEvents, isDark);
            }
            catch { }
        }

        UpdateToolbarButtonsAdaptation();

        if (_scrollEvents != null)
        {
            _scrollEvents.IsDark = isDark;
            _scrollEvents.UpdateMetrics();
        }

        _lvEvents.Invalidate();
    }
}
