using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 화면 어디든 마우스 클릭 한 번으로 원하는 (X, Y) 좌표를 추출하는 화면 좌표 선택기 도구입니다.
/// RoiSelectorForm 및 ColorPickerForm과 동일한 고신뢰도 ESC 비동기 취소 메커니즘 및 중앙 상단 안내 배너를 제공합니다.
/// 메인 모니터뿐만 아니라 다중 모니터(서브 모니터) 전체 영역을 완벽하게 지원합니다.
/// </summary>
public class CoordinatePickerForm : Form
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    public Point SelectedPoint { get; private set; }
    private readonly Bitmap _screenBackground;
    private Point _currentMousePos;
    private readonly System.Windows.Forms.Timer _escCheckTimer;
    private readonly Thread _escPollingThread;
    private volatile bool _isEscPolling = true;
    private int _isCancelledOrClosed = 0;

    public CoordinatePickerForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;

        var bounds = Bounds;
        _screenBackground = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(_screenBackground))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }

        Shown += (s, e) =>
        {
            SetForegroundWindow(Handle);
            BringWindowToTop(Handle);
            Activate();
            Focus();
        };

        _escCheckTimer = new System.Windows.Forms.Timer { Interval = 10 };
        _escCheckTimer.Tick += (s, e) =>
        {
            if ((GetAsyncKeyState((int)Keys.Escape) & 0x8000) != 0)
            {
                CancelAndClose();
            }
        };
        _escCheckTimer.Start();

        _escPollingThread = new Thread(() =>
        {
            while (_isEscPolling)
            {
                if ((GetAsyncKeyState((int)Keys.Escape) & 0x8000) != 0)
                {
                    try
                    {
                        if (IsHandleCreated && !IsDisposed)
                        {
                            BeginInvoke(new Action(CancelAndClose));
                        }
                    }
                    catch { }
                    break;
                }
                Thread.Sleep(5);
            }
        })
        {
            IsBackground = true
        };
        _escPollingThread.Start();
    }

    private void CancelAndClose()
    {
        if (Interlocked.Exchange(ref _isCancelledOrClosed, 1) != 0) return;
        _isEscPolling = false;

        try { _escCheckTimer.Stop(); } catch { }

        try
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        catch { }
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            CancelAndClose();
            return true;
        }
        return base.ProcessDialogKey(keyData);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Escape)
        {
            CancelAndClose();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            CancelAndClose();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _currentMousePos = e.Location;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (Interlocked.Exchange(ref _isCancelledOrClosed, 1) != 0) return;
            _isEscPolling = false;
            try { _escCheckTimer.Stop(); } catch { }

            // 전역 가상 화면 좌표로 산출
            SelectedPoint = new Point(Bounds.Left + e.X, Bounds.Top + e.Y);
            DialogResult = DialogResult.OK;
            Close();
        }
        else if (e.Button == MouseButtons.Right)
        {
            CancelAndClose();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;

        // 1. 전체 가상 화면 배경 렌더링
        g.DrawImage(_screenBackground, 0, 0);

        // 2. 메인 모니터(PrimaryScreen) 중앙 상단 기준 안내문 렌더링 (RoiSelectorForm, ColorPickerForm과 완벽 통일)
        string guide = Localization.IsKorean
            ? "🎯 원하는 화면 위치를 마우스로 클릭하여 좌표를 지정하세요. (취소: [ESC] 또는 마우스 우클릭)"
            : "🎯 Click anywhere on screen to pick coordinates. (Cancel: [ESC] or Right-Click)";
        using var guideFont = AppFontHelper.GetDefaultFont(13, FontStyle.Bold);
        var guideSize = g.MeasureString(guide, guideFont);

        var primary = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
        int primaryClientLeft = primary.Left - Bounds.Left;
        int primaryClientTop = primary.Top - Bounds.Top;

        int gx = primaryClientLeft + (primary.Width - (int)guideSize.Width) / 2;
        int gy = primaryClientTop + 40;

        var gRect = new Rectangle(gx - 15, gy - 10, (int)guideSize.Width + 30, (int)guideSize.Height + 20);
        using var gBg = new SolidBrush(Color.FromArgb(220, 30, 30, 30));
        using var gBorder = new Pen(Color.FromArgb(46, 204, 113), 2);
        g.FillRectangle(gBg, gRect);
        g.DrawRectangle(gBorder, gRect);
        g.DrawString(guide, guideFont, Brushes.White, gx, gy);

        // 3. 가로/세로 십자선 가이드
        using var crossPen = new Pen(Color.FromArgb(180, 231, 76, 60), 1) { DashStyle = DashStyle.Dash };
        g.DrawLine(crossPen, 0, _currentMousePos.Y, Width, _currentMousePos.Y);
        g.DrawLine(crossPen, _currentMousePos.X, 0, _currentMousePos.X, Height);

        // 4. 커서 옆 전역 좌표 정보 툴팁 박스
        int globalX = Bounds.Left + _currentMousePos.X;
        int globalY = Bounds.Top + _currentMousePos.Y;
        string text = Localization.IsKorean
            ? $"X: {globalX}, Y: {globalY}"
            : $"X: {globalX}, Y: {globalY}";
        using var font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold);
        var size = g.MeasureString(text, font);

        int tipX = _currentMousePos.X + 20;
        int tipY = _currentMousePos.Y + 20;

        if (tipX + size.Width + 20 > Width) tipX = _currentMousePos.X - (int)size.Width - 30;
        if (tipY + size.Height + 20 > Height) tipY = _currentMousePos.Y - (int)size.Height - 30;

        var tipRect = new Rectangle(tipX, tipY, (int)size.Width + 14, (int)size.Height + 10);
        using var bgBrush = new SolidBrush(Color.FromArgb(230, 20, 20, 20));
        using var borderPen = new Pen(Color.FromArgb(52, 152, 219), 2);
        g.FillRectangle(bgBrush, tipRect);
        g.DrawRectangle(borderPen, tipRect);

        using var textBrush = new SolidBrush(Color.White);
        g.DrawString(text, font, textBrush, tipX + 7, tipY + 5);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _isEscPolling = false;
            try { _escCheckTimer.Stop(); } catch { }
            _escCheckTimer.Dispose();
            _screenBackground?.Dispose();
        }
        base.Dispose(disposing);
    }
}
