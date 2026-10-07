using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

public class ChangelogDialog : Form
{
    private DarkScrollBar? _scrollLog;
    private RichTextBox? _txtLog;
    private Panel? _pnlLogBorder;
    private Panel? _pnlLogInner;
    private Label? _lblHeader;
    private Button? _btnCopy;
    private Button? _btnClose;

    public ChangelogDialog()
    {
        InitializeUi();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AppThemeHelper.UpdateFormTitleBarTheme(this, AppSettings.Current.IsDarkMode);
    }

    private void InitializeUi()
    {
        bool isDark = AppSettings.Current.IsDarkMode;
        Text = Localization.Get("Changelog_Title");
        ClientSize = new Size(600, 540);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(248, 249, 250);
        Font = AppFontHelper.GetEmojiFont(9.5f, FontStyle.Regular);

        _lblHeader = new Label
        {
            Text = $"{VersionInfo.AppName} v{VersionInfo.CurrentVersion}",
            Font = AppFontHelper.GetDefaultFont(12f, FontStyle.Bold),
            ForeColor = isDark ? Color.FromArgb(225, 235, 248) : Color.FromArgb(30, 41, 59),
            Location = new Point(20, 16),
            AutoSize = true
        };

        // 텍스트 박스 컨테이너 패널 (테마 테두리 및 일체형 스크롤바 배치)
        _pnlLogBorder = new Panel
        {
            Location = new Point(20, 48),
            Size = new Size(560, 420),
            BackColor = isDark ? Color.FromArgb(44, 58, 80) : Color.FromArgb(205, 212, 222),
            Padding = new Padding(1)
        };

        _pnlLogInner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White
        };
        _pnlLogBorder.Controls.Add(_pnlLogInner);

        _txtLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White,
            ForeColor = isDark ? Color.FromArgb(215, 225, 240) : Color.FromArgb(35, 40, 50),
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.None,
            Font = AppFontHelper.GetEmojiFont(9.25f, FontStyle.Regular)
        };

        _scrollLog = new DarkScrollBar
        {
            Dock = DockStyle.Right,
            Width = 14,
            IsDark = isDark
        };

        // WinForms 도킹 순서 보장:
        // Dock=Right 스크롤바를 먼저 컨테이너에 배치하고, Dock=Fill 텍스트박스를 나중에 배치하여 BringToFront() 호출.
        // 이를 통해 스크롤바가 우측 14px를 안정적으로 확보하고 텍스트박스가 남은 영역 전체를 차지함.
        _pnlLogInner.Controls.Add(_scrollLog);
        _pnlLogInner.Controls.Add(_txtLog);
        _txtLog.BringToFront();

        _scrollLog.AttachTo(_txtLog);

        var sb = new StringBuilder();
        bool ko = Localization.IsKorean;
        foreach (var entry in VersionInfo.History)
        {
            string title = ko ? entry.TitleKo : entry.TitleEn;
            sb.AppendLine($"■ v{entry.Version} ({entry.ReleaseDate}) - {title}");
            var changes = ko ? entry.ChangesKo : entry.ChangesEn;
            foreach (var change in changes)
            {
                sb.AppendLine($"   • {change}");
            }
            sb.AppendLine();
        }
        _txtLog.Text = sb.ToString();
        _scrollLog.RefreshMetrics();

        _btnCopy = new Button
        {
            Text = Localization.IsKorean ? "📋 복사" : "📋 Copy",
            Location = new Point(360, 482),
            Size = new Size(105, 36),
            BackColor = isDark ? Color.FromArgb(41, 128, 185) : Color.FromArgb(52, 73, 94),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnCopy.FlatAppearance.BorderSize = 0;
        _btnCopy.Click += (s, e) =>
        {
            try
            {
                if (_txtLog != null)
                {
                    Clipboard.SetText(_txtLog.Text);
                    MessageBox.Show(this, Localization.IsKorean ? "패치노트가 클립보드에 복사되었습니다." : "Patch notes copied to clipboard.",
                                    Localization.Get("TitleNotice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch { }
        };

        _btnClose = new Button
        {
            Text = Localization.Get("Btn_Close"),
            Location = new Point(475, 482),
            Size = new Size(105, 36),
            DialogResult = DialogResult.OK,
            BackColor = isDark ? Color.FromArgb(64, 80, 100) : Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnClose.FlatAppearance.BorderSize = 0;

        Controls.Add(_lblHeader);
        Controls.Add(_pnlLogBorder);
        Controls.Add(_btnCopy);
        Controls.Add(_btnClose);

        AcceptButton = _btnClose;

        AppThemeHelper.ApplyTheme(this, isDark);

        AppThemeHelper.ThemePreviewChanged += HandleThemePreview;
        FormClosed += (s, e) => AppThemeHelper.ThemePreviewChanged -= HandleThemePreview;
        Shown += (s, e) => _scrollLog?.RefreshMetrics();
    }

    private void HandleThemePreview(bool isDark)
    {
        if (IsDisposed) return;
        AppThemeHelper.ApplyTheme(this, isDark);
        if (_lblHeader != null) _lblHeader.ForeColor = isDark ? Color.FromArgb(225, 235, 248) : Color.FromArgb(30, 41, 59);
        if (_pnlLogBorder != null) _pnlLogBorder.BackColor = isDark ? Color.FromArgb(44, 58, 80) : Color.FromArgb(205, 212, 222);
        if (_pnlLogInner != null) _pnlLogInner.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
        if (_txtLog != null)
        {
            _txtLog.BackColor = isDark ? Color.FromArgb(13, 17, 24) : Color.White;
            _txtLog.ForeColor = isDark ? Color.FromArgb(215, 225, 240) : Color.FromArgb(35, 40, 50);
        }
        if (_scrollLog != null)
        {
            _scrollLog.IsDark = isDark;
            _scrollLog.RefreshMetrics();
        }
    }
}
