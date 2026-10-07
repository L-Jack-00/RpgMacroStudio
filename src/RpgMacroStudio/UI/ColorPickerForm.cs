using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 화면 어디든 마우스 클릭 한 번으로 원하는 게이지 색상을 추출할 수 있는 화면 스포이드(Eyedropper) 도구입니다.
/// RoiSelectorForm과 동일한 고신뢰도 ESC 비동기 취소 메커니즘 및 중앙 상단 안내 배너를 제공합니다.
/// 메인 모니터뿐만 아니라 다중 모니터(서브 모니터) 전체 영역을 완벽하게 지원합니다.
/// </summary>
public class ColorPickerForm : Form
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    public Color SelectedColor { get; private set; } = Color.Red;
    private readonly Bitmap _screenBackground;
    private Point _currentMousePos;
    private readonly System.Windows.Forms.Timer _escCheckTimer;
    private readonly Thread _escPollingThread;
    private volatile bool _isEscPolling = true;
    private int _isCancelledOrClosed = 0;

    public ColorPickerForm()
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

        // 10ms 타이머로 ESC 키 비동기 감지
        _escCheckTimer = new System.Windows.Forms.Timer { Interval = 10 };
        _escCheckTimer.Tick += (s, e) =>
        {
            if ((GetAsyncKeyState((int)Keys.Escape) & 0x8000) != 0)
            {
                CancelAndClose();
            }
        };
        _escCheckTimer.Start();

        // 5ms 초고속 백그라운드 스레드로 ESC 키 폴링 (윈도우 포커스 이탈 시에도 100% 즉시 감지)
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

            if (e.X >= 0 && e.X < _screenBackground.Width && e.Y >= 0 && e.Y < _screenBackground.Height)
            {
                SelectedColor = _screenBackground.GetPixel(e.X, e.Y);
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }
            Close();
        }
        else if (e.Button == MouseButtons.Right)
        {
            CancelAndClose();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImage(_screenBackground, 0, 0);

        // 1. 메인 모니터(PrimaryScreen) 중앙 상단 기준 안내문 렌더링 (RoiSelectorForm과 완벽 통일)
        string guide = Localization.IsKorean
            ? "🎨 추출할 게이지 색상을 마우스로 클릭하세요. (취소: [ESC] 또는 마우스 우클릭)"
            : "🎨 Click the gauge color to sample. (Cancel: [ESC] or Right-Click)";
        using var font = AppFontHelper.GetDefaultFont(13, FontStyle.Bold);
        var size = e.Graphics.MeasureString(guide, font);

        var primary = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
        int primaryClientLeft = primary.Left - Bounds.Left;
        int primaryClientTop = primary.Top - Bounds.Top;

        int gx = primaryClientLeft + (primary.Width - (int)size.Width) / 2;
        int gy = primaryClientTop + 40;

        var gRect = new Rectangle(gx - 15, gy - 10, (int)size.Width + 30, (int)size.Height + 20);
        using var gBg = new SolidBrush(Color.FromArgb(220, 30, 30, 30));
        using var gBorder = new Pen(Color.FromArgb(46, 204, 113), 2);
        e.Graphics.FillRectangle(gBg, gRect);
        e.Graphics.DrawRectangle(gBorder, gRect);
        e.Graphics.DrawString(guide, font, Brushes.White, gx, gy);

        // 2. 마우스 커서 부근에 돋보기 및 현재 색상 미리보기 표시
        if (_currentMousePos.X >= 0 && _currentMousePos.X < _screenBackground.Width &&
            _currentMousePos.Y >= 0 && _currentMousePos.Y < _screenBackground.Height)
        {
            var hoverColor = _screenBackground.GetPixel(_currentMousePos.X, _currentMousePos.Y);
            int boxX = _currentMousePos.X + 20;
            int boxY = _currentMousePos.Y + 20;

            if (boxX + 140 > Width) boxX = _currentMousePos.X - 150;
            if (boxY + 75 > Height) boxY = _currentMousePos.Y - 85;

            // 미리보기 상자 배경
            using var tipBg = new SolidBrush(Color.FromArgb(230, 20, 20, 20));
            e.Graphics.FillRectangle(tipBg, boxX, boxY, 140, 65);
            using var tipBorder = new Pen(Color.FromArgb(52, 152, 219), 2);
            e.Graphics.DrawRectangle(tipBorder, boxX, boxY, 140, 65);

            // 색상 타일
            using var colorBrush = new SolidBrush(hoverColor);
            e.Graphics.FillRectangle(colorBrush, boxX + 8, boxY + 8, 32, 32);
            e.Graphics.DrawRectangle(Pens.White, boxX + 8, boxY + 8, 32, 32);

            // RGB 텍스트
            using var smallFont = new Font("Consolas", 9, FontStyle.Bold);
            string hexText = $"#{hoverColor.R:X2}{hoverColor.G:X2}{hoverColor.B:X2}";
            e.Graphics.DrawString(hexText, smallFont, Brushes.Yellow, boxX + 46, boxY + 8);
            e.Graphics.DrawString($"R:{hoverColor.R} G:{hoverColor.G}", smallFont, Brushes.White, boxX + 46, boxY + 26);
            e.Graphics.DrawString($"B:{hoverColor.B}", smallFont, Brushes.White, boxX + 46, boxY + 44);
        }
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
