using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 일반 사용자가 좌표를 숫자로 일일이 입력하지 않고,
/// 마우스 드래그 한 번으로 감지 영역(ROI)을 손쉽게 지정할 수 있는 반투명 캡처 창입니다.
/// 메인 모니터뿐만 아니라 다중 모니터(서브 모니터) 전체 영역을 완벽하게 지원합니다.
/// </summary>
public class RoiSelectorForm : Form
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    public Rectangle SelectedRoi { get; private set; }

    private Point _startPoint;
    private Rectangle _dragRect;
    private bool _isSelecting = false;
    private readonly Bitmap _screenBackground;
    private readonly System.Windows.Forms.Timer _escCheckTimer;
    private readonly Thread _escPollingThread;
    private volatile bool _isEscPolling = true;

    public RoiSelectorForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        KeyPreview = true;

        // 모든 모니터를 포함하는 가상 화면 전체를 배경으로 캡처
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

    private int _isCancelledOrClosed = 0;

    private void CancelAndClose()
    {
        if (Interlocked.Exchange(ref _isCancelledOrClosed, 1) != 0) return;
        try
        {
            _isEscPolling = false;
            _escCheckTimer?.Stop();
            Capture = false;
            DialogResult = DialogResult.Cancel;
            Close();
        }
        catch { }
    }

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _isEscPolling = false;
        _escCheckTimer?.Stop();
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_KEYDOWN || m.Msg == WM_SYSKEYDOWN)
        {
            if ((int)m.WParam == (int)Keys.Escape)
            {
                CancelAndClose();
                return;
            }
        }
        base.WndProc(ref m);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Escape)
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isSelecting = true;
            _startPoint = e.Location;
            _dragRect = new Rectangle(e.Location, Size.Empty);
        }
        else if (e.Button == MouseButtons.Right)
        {
            CancelAndClose();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isSelecting)
        {
            int x = Math.Min(_startPoint.X, e.X);
            int y = Math.Min(_startPoint.Y, e.Y);
            int width = Math.Abs(e.X - _startPoint.X);
            int height = Math.Abs(e.Y - _startPoint.Y);

            _dragRect = new Rectangle(x, y, width, height);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_isSelecting)
        {
            _isSelecting = false;
            _escCheckTimer.Stop();

            if (_dragRect.Width > 5 && _dragRect.Height > 5)
            {
                // 가상 화면 전역 좌표(글로벌 X, Y)로 변환
                SelectedRoi = new Rectangle(Bounds.Left + _dragRect.X, Bounds.Top + _dragRect.Y, _dragRect.Width, _dragRect.Height);
                DialogResult = DialogResult.OK;
            }
            else
            {
                // 드래그하지 않고 단일 클릭한 경우: 클릭 위치 기준 1x1 크기로 설정
                SelectedRoi = new Rectangle(Bounds.Left + _startPoint.X, Bounds.Top + _startPoint.Y, 1, 1);
                DialogResult = DialogResult.OK;
            }
            Close();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImage(_screenBackground, 0, 0);

        // 전체 화면을 어둡게 처리 (드래그 영역 강조)
        using (var darkBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
        {
            e.Graphics.FillRectangle(darkBrush, ClientRectangle);
        }

        if (_isSelecting && _dragRect.Width > 0 && _dragRect.Height > 0)
        {
            // 선택된 영역은 원래 화면 밝기로 복원
            e.Graphics.DrawImage(_screenBackground, _dragRect, _dragRect, GraphicsUnit.Pixel);

            // 빨간색 테두리 사각형 렌더링
            using var borderPen = new Pen(Color.FromArgb(231, 76, 60), 2)
            {
                DashStyle = DashStyle.Solid
            };
            e.Graphics.DrawRectangle(borderPen, _dragRect);

            // 전역 좌표 및 크기 안내 텍스트 표시
            int globalX = Bounds.Left + _dragRect.X;
            int globalY = Bounds.Top + _dragRect.Y;
            string infoText = Localization.IsKorean
                ? $"영역: {globalX}, {globalY} ({_dragRect.Width} x {_dragRect.Height})  [ESC/우클릭] 취소"
                : $"Region: {globalX}, {globalY} ({_dragRect.Width} x {_dragRect.Height})  [ESC/Right-Click] Cancel";
            using var font = AppFontHelper.GetDefaultFont(10, FontStyle.Bold);
            var textSize = e.Graphics.MeasureString(infoText, font);

            int textX = _dragRect.X;
            int textY = _dragRect.Y - (int)textSize.Height - 6;
            if (textY < 0) textY = _dragRect.Bottom + 6;

            var bgRect = new Rectangle(textX, textY, (int)textSize.Width + 10, (int)textSize.Height + 6);
            using var textBgBrush = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
            e.Graphics.FillRectangle(textBgBrush, bgRect);
            e.Graphics.DrawString(infoText, font, Brushes.White, textX + 5, textY + 3);
        }
        else
        {
            // 드래그 전 기본 안내문: 메인 모니터(PrimaryScreen) 중앙 상단 기준으로 렌더링
            string guide = Localization.IsKorean
                ? "마우스 왼쪽 버튼을 드래그하여 감지할 화면 영역을 지정하세요. (클릭 시 1x1 | 취소: [ESC] 또는 우클릭)"
                : "Drag left mouse button to select region. (Click for 1x1 | Cancel: [ESC] or Right-Click)";
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
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _escCheckTimer.Stop();
            _escCheckTimer.Dispose();
            _screenBackground.Dispose();
        }
        base.Dispose(disposing);
    }
}
