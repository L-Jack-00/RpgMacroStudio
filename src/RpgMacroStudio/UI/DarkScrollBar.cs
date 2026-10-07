using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RpgMacroHarness.UI;

public enum ScrollOrientation
{
    Vertical,
    Horizontal
}

/// <summary>
/// 다크 모드 및 라이트 모드에 최적화된 고품질 벡터 커스텀 스크롤바입니다. (수직 및 수평 가로 스크롤 완벽 지원)
/// Windows 10/11의 OS 기본 스크롤바가 다크 모드에서도 하얗게 떠서 부자연스러운 현상을 완벽히 해결합니다.
/// </summary>
public class DarkScrollBar : Control
{
    private const int EM_LINESCROLL = 0x00B6;
    private const int EM_GETLINECOUNT = 0x00BA;
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    private ScrollOrientation _orientation = ScrollOrientation.Vertical;
    private int _min = 0;
    private int _max = 100;
    private int _value = 0;
    private int _largeChange = 10;
    private int _smallChange = 1;

    private bool _isHovered = false;
    private bool _isDragging = false;
    private int _dragStartX = 0;
    private int _dragStartY = 0;
    private int _dragStartValue = 0;

    private bool _isDark = true;
    private RichTextBox? _attachedRtb;
    private bool _isSyncing = false;

    public event EventHandler? ValueChanged;

    public ScrollOrientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation != value)
            {
                _orientation = value;
                Invalidate();
            }
        }
    }

    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark != value)
            {
                _isDark = value;
                Invalidate();
            }
        }
    }

    public int Minimum
    {
        get => _min;
        set
        {
            _min = value;
            if (_value < _min) _value = _min;
            Invalidate();
        }
    }

    public int Maximum
    {
        get => _max;
        set
        {
            _max = Math.Max(_min, value);
            if (_value > _max) _value = _max;
            Invalidate();
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Clamp(value, _min, _max);
            if (_value != clamped)
            {
                _value = clamped;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int LargeChange
    {
        get => _largeChange;
        set
        {
            _largeChange = Math.Max(1, value);
            Invalidate();
        }
    }

    public int SmallChange
    {
        get => _smallChange;
        set => _smallChange = Math.Max(1, value);
    }

    public DarkScrollBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.Opaque, true);
        Width = 14;
        Cursor = Cursors.Default;
    }

    private void UpdateRtbMetrics()
    {
        if (_attachedRtb == null || !_attachedRtb.IsHandleCreated || _attachedRtb.IsDisposed) return;
        int totalLines = (int)SendMessage(_attachedRtb.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero);
        if (totalLines <= 0) totalLines = Math.Max(1, _attachedRtb.Lines.Length);

        int fontH = Math.Max(1, _attachedRtb.Font.Height);
        int visibleLines = Math.Max(1, _attachedRtb.ClientSize.Height / fontH);

        Maximum = Math.Max(0, totalLines - visibleLines);
        LargeChange = visibleLines;

        int firstLine = (int)SendMessage(_attachedRtb.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
        _isSyncing = true;
        Value = firstLine;
        _isSyncing = false;
        try { ShowScrollBar(_attachedRtb.Handle, SB_VERT, false); } catch { }
        try { SetWindowTheme(_attachedRtb.Handle, _isDark ? "DarkMode_Explorer" : null, null); } catch { }
    }

    /// <summary>
    /// RichTextBox의 기본 하얀색 Win32 스크롤바를 숨기고 본 DarkScrollBar에 양방향 완벽 동기화 바인딩합니다.
    /// </summary>
    public void AttachTo(RichTextBox rtb)
    {
        _attachedRtb = rtb;
        rtb.ScrollBars = RichTextBoxScrollBars.None;

        rtb.HandleCreated += (s, e) => UpdateRtbMetrics();
        rtb.SizeChanged += (s, e) => UpdateRtbMetrics();
        rtb.TextChanged += (s, e) => UpdateRtbMetrics();
        rtb.VisibleChanged += (s, e) => UpdateRtbMetrics();
        rtb.VScroll += (s, e) =>
        {
            if (_isSyncing || _attachedRtb == null) return;
            int firstLine = (int)SendMessage(_attachedRtb.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
            _isSyncing = true;
            Value = firstLine;
            _isSyncing = false;
            ShowScrollBar(_attachedRtb.Handle, SB_VERT, false);
        };

        rtb.MouseWheel += (s, e) =>
        {
            int linesToScroll = -(e.Delta / 120) * 3;
            Value += linesToScroll;
            if (e is HandledMouseEventArgs hme) hme.Handled = true;
        };

        ValueChanged += (s, e) =>
        {
            if (_isSyncing || _attachedRtb == null || !_attachedRtb.IsHandleCreated) return;
            int currentLine = (int)SendMessage(_attachedRtb.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
            int diff = Value - currentLine;
            if (diff != 0)
            {
                SendMessage(_attachedRtb.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)diff);
            }
        };

        if (rtb.IsHandleCreated)
        {
            UpdateRtbMetrics();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
    private const int SB_VERT = 1;
    private const int LVM_FIRST = 0x1000;
    private const int LVM_GETTOPINDEX = LVM_FIRST + 39;
    private const int LVM_GETCOUNTPERPAGE = LVM_FIRST + 40;
    private const int LVM_SCROLL = LVM_FIRST + 20;

    private ListBox? _attachedLb;
    private ListView? _attachedLv;

    /// <summary>
    /// ListBox의 기본 하얀색 Win32 스크롤바를 숨기고 본 DarkScrollBar에 양방향 완벽 동기화 바인딩합니다.
    /// </summary>
    public void AttachTo(ListBox lb)
    {
        _attachedLb = lb;
        lb.HandleCreated += (s, e) => UpdateMetrics();
        lb.SizeChanged += (s, e) => UpdateMetrics();
        lb.SelectedIndexChanged += (s, e) => UpdateMetrics();
        lb.MouseWheel += (s, e) =>
        {
            int step = -(e.Delta / 120) * 3;
            Value += step;
            if (e is HandledMouseEventArgs hme) hme.Handled = true;
        };

        ValueChanged += (s, e) =>
        {
            if (_isSyncing || _attachedLb == null || !_attachedLb.IsHandleCreated || _attachedLb.Items.Count == 0) return;
            int targetIdx = Math.Clamp(Value, 0, _attachedLb.Items.Count - 1);
            if (_attachedLb.TopIndex != targetIdx)
            {
                _attachedLb.TopIndex = targetIdx;
            }
        };

        if (lb.IsHandleCreated) UpdateMetrics();
    }

    /// <summary>
    /// ListView의 기본 하얀색/OS 스크롤바를 숨기고 본 스크롤바에 양방향 완벽 동기화 바인딩합니다.
    /// 라이트 모드 및 다크 모드 모두에서 일관된 미려한 14px 슬림 벡터 스크롤바를 보장합니다.
    /// </summary>
    public void AttachTo(ListView lv)
    {
        _attachedLv = lv;
        lv.HandleCreated += (s, e) => UpdateMetrics();
        lv.SizeChanged += (s, e) => UpdateMetrics();
        lv.SelectedIndexChanged += (s, e) => UpdateMetrics();
        lv.MouseWheel += (s, e) =>
        {
            int step = -(e.Delta / 120) * 3;
            Value += step;
            if (e is HandledMouseEventArgs hme) hme.Handled = true;
        };

        ValueChanged += (s, e) =>
        {
            if (_isSyncing || _attachedLv == null || !_attachedLv.IsHandleCreated || _attachedLv.Items.Count == 0) return;
            int currentTop = (int)SendMessage(_attachedLv.Handle, LVM_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero);
            int diff = Value - currentTop;
            if (diff != 0)
            {
                int itemH = 24;
                if (_attachedLv.Items.Count > 0)
                {
                    try
                    {
                        var rect = _attachedLv.GetItemRect(0);
                        if (rect.Height > 0) itemH = rect.Height;
                    }
                    catch { }
                }
                SendMessage(_attachedLv.Handle, LVM_SCROLL, IntPtr.Zero, (IntPtr)(diff * itemH));
                ShowScrollBar(_attachedLv.Handle, SB_VERT, false);
            }
        };

        if (lv.IsHandleCreated) UpdateMetrics();
    }

    /// <summary>
    /// 바인딩된 컨트롤(RichTextBox, ListBox, ListView 등)의 항목 및 크기 변경 사항에 맞춰 스크롤바 메트릭을 강제 동기화합니다.
    /// </summary>
    public void RefreshMetrics()
    {
        if (_attachedRtb != null && _attachedRtb.IsHandleCreated && !_attachedRtb.IsDisposed)
        {
            UpdateRtbMetrics();
        }
        else
        {
            UpdateMetrics();
        }
    }

    /// <summary>
    /// 바인딩된 ListBox 또는 ListView의 항목 변경 사항에 맞춰 스크롤바 메트릭을 동기화합니다.
    /// </summary>
    public void UpdateMetrics()
    {
        if (_attachedLb != null && _attachedLb.IsHandleCreated && !_attachedLb.IsDisposed)
        {
            ShowScrollBar(_attachedLb.Handle, SB_VERT, false);
            int totalItems = _attachedLb.Items.Count;
            int itemH = Math.Max(1, _attachedLb.ItemHeight);
            int visibleItems = Math.Max(1, _attachedLb.ClientSize.Height / itemH);

            Maximum = Math.Max(0, totalItems - visibleItems);
            LargeChange = visibleItems;

            _isSyncing = true;
            Value = _attachedLb.TopIndex;
            _isSyncing = false;
        }
        else if (_attachedLv != null && _attachedLv.IsHandleCreated && !_attachedLv.IsDisposed)
        {
            ShowScrollBar(_attachedLv.Handle, SB_VERT, false);
            int totalItems = _attachedLv.Items.Count;
            int countPerPage = (int)SendMessage(_attachedLv.Handle, LVM_GETCOUNTPERPAGE, IntPtr.Zero, IntPtr.Zero);
            countPerPage = Math.Max(1, countPerPage);

            Maximum = Math.Max(0, totalItems - countPerPage);
            LargeChange = countPerPage;

            int topIndex = (int)SendMessage(_attachedLv.Handle, LVM_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero);
            _isSyncing = true;
            Value = topIndex;
            _isSyncing = false;
        }
    }

    /// <summary>
    /// Panel(AutoScroll)의 기본 하얀색 Win32 스크롤바를 숨기고 본 DarkScrollBar에 양방향 완벽 동기화 바인딩합니다.
    /// </summary>
    public void AttachTo(Panel pnl)
    {
        void UpdateMetrics()
        {
            if (!pnl.IsHandleCreated || pnl.IsDisposed) return;
            ShowScrollBar(pnl.Handle, SB_VERT, false);
            int contentH = pnl.DisplayRectangle.Height;
            int clientH = pnl.ClientSize.Height;

            Maximum = Math.Max(0, contentH - clientH);
            LargeChange = Math.Max(1, clientH);

            _isSyncing = true;
            Value = -pnl.AutoScrollPosition.Y;
            _isSyncing = false;
        }

        pnl.HandleCreated += (s, e) => UpdateMetrics();
        pnl.SizeChanged += (s, e) => UpdateMetrics();
        pnl.Scroll += (s, e) =>
        {
            if (_isSyncing) return;
            _isSyncing = true;
            Value = -pnl.AutoScrollPosition.Y;
            _isSyncing = false;
            ShowScrollBar(pnl.Handle, SB_VERT, false);
        };
        pnl.MouseWheel += (s, e) =>
        {
            Value -= (e.Delta / 120) * 48;
            if (e is HandledMouseEventArgs hme) hme.Handled = true;
        };

        ValueChanged += (s, e) =>
        {
            if (_isSyncing || !pnl.IsHandleCreated) return;
            pnl.AutoScrollPosition = new Point(-pnl.AutoScrollPosition.X, Value);
            ShowScrollBar(pnl.Handle, SB_VERT, false);
        };

        if (pnl.IsHandleCreated) UpdateMetrics();
    }

    private Rectangle GetThumbRect()
    {
        int range = _max - _min;
        if (range <= 0) return Rectangle.Empty;

        if (_orientation == ScrollOrientation.Horizontal)
        {
            int trackW = Width;
            int thumbW = Math.Max(24, (int)((float)_largeChange / (range + _largeChange) * trackW));
            int availableTrack = trackW - thumbW;
            int thumbX = (int)((float)(_value - _min) / range * availableTrack);
            return new Rectangle(thumbX, 3, thumbW, Math.Max(4, Height - 6));
        }
        else
        {
            int trackH = Height;
            int thumbH = Math.Max(24, (int)((float)_largeChange / (range + _largeChange) * trackH));
            int availableTrack = trackH - thumbH;
            int thumbY = (int)((float)(_value - _min) / range * availableTrack);
            return new Rectangle(3, thumbY, Math.Max(4, Width - 6), thumbH);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // 트랙 배경색
        Color trackColor = _isDark ? AppThemeHelper.Palette.Dark.ScrollTrack : AppThemeHelper.Palette.Light.ScrollTrack;
        using (var trackBrush = new SolidBrush(trackColor))
        {
            g.FillRectangle(trackBrush, ClientRectangle);
        }

        var thumbRect = GetThumbRect();
        if (thumbRect.IsEmpty || thumbRect.Width <= 0 || thumbRect.Height <= 0) return;

        // 썸(Thumb) 색상
        Color thumbColor;
        if (_isDark)
        {
            thumbColor = _isDragging ? AppThemeHelper.Palette.Dark.ScrollThumbDrag :
                         _isHovered ? AppThemeHelper.Palette.Dark.ScrollThumbHover :
                         AppThemeHelper.Palette.Dark.ScrollThumb;
        }
        else
        {
            thumbColor = _isDragging ? AppThemeHelper.Palette.Light.ScrollThumbDrag :
                         _isHovered ? AppThemeHelper.Palette.Light.ScrollThumbHover :
                         AppThemeHelper.Palette.Light.ScrollThumb;
        }

        using var path = CreateRoundedRectanglePath(thumbRect, 4);
        using var thumbBrush = new SolidBrush(thumbColor);
        g.FillPath(thumbBrush, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
        if (r <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
        path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
        path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            var thumbRect = GetThumbRect();
            if (thumbRect.Contains(e.Location))
            {
                _isDragging = true;
                _dragStartX = e.X;
                _dragStartY = e.Y;
                _dragStartValue = _value;
                Capture = true;
                Invalidate();
            }
            else
            {
                if (_orientation == ScrollOrientation.Horizontal)
                {
                    if (e.X < thumbRect.X)
                    {
                        Value -= _largeChange;
                    }
                    else if (e.X > thumbRect.Right)
                    {
                        Value += _largeChange;
                    }
                }
                else
                {
                    if (e.Y < thumbRect.Y)
                    {
                        Value -= _largeChange;
                    }
                    else if (e.Y > thumbRect.Bottom)
                    {
                        Value += _largeChange;
                    }
                }
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging)
        {
            int range = _max - _min;
            if (_orientation == ScrollOrientation.Horizontal)
            {
                int trackW = Width;
                int thumbW = Math.Max(24, (int)((float)_largeChange / (range + _largeChange) * trackW));
                int availableTrack = trackW - thumbW;

                if (availableTrack > 0 && range > 0)
                {
                    int deltaX = e.X - _dragStartX;
                    float deltaVal = (float)deltaX / availableTrack * range;
                    Value = (int)(_dragStartValue + deltaVal);
                }
            }
            else
            {
                int trackH = Height;
                int thumbH = Math.Max(24, (int)((float)_largeChange / (range + _largeChange) * trackH));
                int availableTrack = trackH - thumbH;

                if (availableTrack > 0 && range > 0)
                {
                    int deltaY = e.Y - _dragStartY;
                    float deltaVal = (float)deltaY / availableTrack * range;
                    Value = (int)(_dragStartValue + deltaVal);
                }
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            Capture = false;
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int delta = -(e.Delta / 120) * 3;
        Value += delta;
    }
}
