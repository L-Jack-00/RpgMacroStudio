using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 다크 모드 시 WM_ERASEBKGND(0x0014)의 흰색 배경 지우기를 원천 차단하고
/// 더블 버퍼링, 다크 스크롤바 테마 및 SysHeader32 헤더 빈 영역 다크 채우기를 완벽 지원하는 공용 ListView입니다.
/// </summary>
public class DarkListView : ListView
{
    private const int LVM_GETHEADER = 0x101F;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_PAINT = 0x000F;
    private const int WM_NCPAINT = 0x0085;
    private const int WM_VSCROLL = 0x0115;
    private const int WM_HSCROLL = 0x0114;
    private const int SB_BOTH = 3;

    [DllImport("user32.dll")]
    private static extern int ShowScrollBar(IntPtr hWnd, int wBar, int bShow);

    public bool HideNativeScrollBar { get; set; } = true;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private HeaderNativeWindow? _headerSubclass;

    public DarkListView()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (HideNativeScrollBar)
            {
                cp.Style &= ~0x00200000; // WS_VSCROLL 제거
                cp.Style &= ~0x00100000; // WS_HSCROLL 제거
            }
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            AppThemeHelper.SetWindowTheme(Handle, AppSettings.Current.IsDarkMode ? "DarkMode_Explorer" : null, null);
            AppThemeHelper.RefreshListViewHeader(this, AppSettings.Current.IsDarkMode);

            IntPtr hHeader = SendMessage(Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (hHeader != IntPtr.Zero && (_headerSubclass == null || _headerSubclass.Handle != hHeader))
            {
                _headerSubclass?.ReleaseHandle();
                _headerSubclass = new HeaderNativeWindow(this, hHeader);
            }

            if (HideNativeScrollBar)
            {
                ShowScrollBar(Handle, SB_BOTH, 0);
            }
        }
        catch { }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _headerSubclass?.ReleaseHandle();
        _headerSubclass = null;
        base.OnHandleDestroyed(e);
    }

    private const int WM_STYLECHANGING = 0x007C;
    private const int GWL_STYLE = -16;
    private const int WS_VSCROLL = 0x00200000;
    private const int WS_HSCROLL = 0x00100000;

    [StructLayout(LayoutKind.Sequential)]
    private struct STYLESTRUCT
    {
        public int styleOld;
        public int styleNew;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ERASEBKGND && m.WParam != IntPtr.Zero)
        {
            using var brush = new SolidBrush(BackColor);
            using var g = Graphics.FromHdc(m.WParam);
            g.FillRectangle(brush, ClientRectangle);
            m.Result = (IntPtr)1;
            return;
        }

        // Windows가 ListView에 WS_VSCROLL / WS_HSCROLL 스타일을 동적 부여하려 할 때 원천 제거
        // (WM_PAINT/WM_NCPAINT 무한 재귀 및 렉/응답없음 없이 OS 기본 스크롤바 생성을 무부하 차단)
        if (HideNativeScrollBar && m.Msg == WM_STYLECHANGING && (int)m.WParam == GWL_STYLE)
        {
            var ss = Marshal.PtrToStructure<STYLESTRUCT>(m.LParam);
            ss.styleNew &= ~(WS_VSCROLL | WS_HSCROLL);
            Marshal.StructureToPtr(ss, m.LParam, false);
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }

    private class HeaderNativeWindow : NativeWindow
    {
        private readonly DarkListView _owner;

        public HeaderNativeWindow(DarkListView owner, IntPtr handle)
        {
            _owner = owner;
            AssignHandle(handle);
        }

        protected override void WndProc(ref Message m)
        {
            bool isDark = AppSettings.Current.IsDarkMode;
            Color headerBg = isDark ? Color.FromArgb(24, 32, 46) : SystemColors.Control;
            Color headerBorder = isDark ? Color.FromArgb(44, 58, 80) : SystemColors.ControlDark;

            if (m.Msg == WM_ERASEBKGND && m.WParam != IntPtr.Zero && isDark)
            {
                using var brush = new SolidBrush(headerBg);
                using var g = Graphics.FromHdc(m.WParam);
                if (GetClientRect(Handle, out var rc))
                {
                    g.FillRectangle(brush, 0, 0, rc.Right - rc.Left, rc.Bottom - rc.Top);
                }
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);

            if (m.Msg == WM_PAINT && isDark && GetClientRect(Handle, out var rcPaint))
            {
                int totalWidth = 0;
                foreach (ColumnHeader col in _owner.Columns)
                {
                    totalWidth += col.Width;
                }
                int headerW = rcPaint.Right - rcPaint.Left;
                int headerH = rcPaint.Bottom - rcPaint.Top;
                if (totalWidth < headerW)
                {
                    using var g = Graphics.FromHwnd(Handle);
                    using var brush = new SolidBrush(headerBg);
                    using var pen = new Pen(headerBorder);
                    var extraRect = new Rectangle(totalWidth, 0, headerW - totalWidth, headerH);
                    g.FillRectangle(brush, extraRect);
                    g.DrawRectangle(pen, extraRect.X, extraRect.Y, extraRect.Width - 1, extraRect.Height - 1);
                }
            }
        }
    }
}
