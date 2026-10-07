using System.Drawing;
using System.Windows.Forms;
using RpgMacroHarness.Data;
using RpgMacroHarness.Input;

namespace RpgMacroHarness.UI;

/// <summary>
/// 프로그램 설정 (음소거, 한국어/영어 언어 변경, 전역 단축키, 버전 체인지로그) 다이얼로그입니다.
/// </summary>
public class SettingsForm : Form
{
    private CustomCheckBox _chkMute = null!;
    private CustomComboBox _cboLanguage = null!;
    private CustomRadioButton _rbDarkTheme = null!;
    private CustomRadioButton _rbLightTheme = null!;

    // 전역 단축키 콤보박스
    private CustomComboBox _cboRecordKey = null!;
    private CustomComboBox _cboStartPauseKey = null!;
    private CustomComboBox _cboStopKey = null!;
    private CustomComboBox _cboEmergencyKey = null!;

    public bool LanguageChanged { get; private set; } = false;
    public bool ThemeChanged { get; private set; } = false;
    public bool HotkeysChanged { get; private set; } = false;

    public SettingsForm()
    {
        InitializeComponent();
        LoadCurrentSettings();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AppThemeHelper.UpdateFormTitleBarTheme(this, AppSettings.Current.IsDarkMode);
    }

    private void InitializeComponent()
    {
        Text = Localization.Get("SettingsTitle");
        Size = new Size(520, 710);
        MinimumSize = new Size(520, 710);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = AppFontHelper.GetEmojiFont(9.5f);

        // 1. 소리/음소거 설정
        var grpSound = new GroupBox
        {
            Text = Localization.Get("GrpSound"),
            Left = 20,
            Top = 12,
            Width = 465,
            Height = 65,
            Padding = new Padding(12)
        };

        _chkMute = new CustomCheckBox
        {
            Text = Localization.Get("ChkMute"),
            Left = 20,
            Top = 26,
            Width = 420,
            Checked = AppSettings.Current.MuteSound
        };
        grpSound.Controls.Add(_chkMute);

        // 2. 다국어(언어) 설정
        var grpLang = new GroupBox
        {
            Text = Localization.Get("GrpLanguage"),
            Left = 20,
            Top = 84,
            Width = 465,
            Height = 65,
            Padding = new Padding(12)
        };

        _cboLanguage = new CustomComboBox
        {
            Left = 20,
            Top = 24,
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cboLanguage.Items.AddRange(new object[]
        {
            "🇰🇷 한국어",
            "🇺🇸 English"
        });
        _cboLanguage.SelectedIndex = 0;

        grpLang.Controls.Add(_cboLanguage);

        // 3. UI 테마 설정 (라이트 / 다크)
        var grpTheme = new GroupBox
        {
            Text = Localization.Get("GrpTheme"),
            Left = 20,
            Top = 156,
            Width = 465,
            Height = 65,
            Padding = new Padding(12)
        };

        _rbDarkTheme = new CustomRadioButton
        {
            Text = Localization.Get("ThemeDark"),
            Left = 20,
            Top = 26,
            Width = 150,
            Checked = true
        };

        _rbLightTheme = new CustomRadioButton
        {
            Text = Localization.Get("ThemeLight"),
            Left = 180,
            Top = 26,
            Width = 150
        };

        grpTheme.Controls.AddRange(new Control[] { _rbDarkTheme, _rbLightTheme });

        // 4. 전역 단축키 설정
        var grpHotkeys = new GroupBox
        {
            Text = Localization.Get("GrpHotkeys"),
            Left = 20,
            Top = 228,
            Width = 465,
            Height = 180,
            Padding = new Padding(12)
        };

        string[] keyOptions = new[]
        {
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "Insert", "Home", "PageUp", "Delete", "End", "PageDown", "Pause", "Scroll"
        };

        var lblRecord = new Label { Text = Localization.Get("LblRecordHotkey"), Left = 20, Top = 30, Width = 160 };
        _cboRecordKey = new CustomComboBox { Left = 190, Top = 26, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboRecordKey.Items.AddRange(keyOptions);

        var lblStartPause = new Label { Text = Localization.Get("LblStartPauseHotkey"), Left = 20, Top = 66, Width = 160 };
        _cboStartPauseKey = new CustomComboBox { Left = 190, Top = 62, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboStartPauseKey.Items.AddRange(keyOptions);

        var lblStop = new Label { Text = Localization.Get("LblStopHotkey"), Left = 20, Top = 102, Width = 160 };
        _cboStopKey = new CustomComboBox { Left = 190, Top = 98, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboStopKey.Items.AddRange(keyOptions);

        var lblEmerg = new Label { Text = Localization.Get("LblEmergencyHotkey"), Left = 20, Top = 138, Width = 160 };
        _cboEmergencyKey = new CustomComboBox { Left = 190, Top = 134, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboEmergencyKey.Items.AddRange(keyOptions);

        grpHotkeys.Controls.AddRange(new Control[]
        {
            lblRecord, _cboRecordKey,
            lblStartPause, _cboStartPauseKey,
            lblStop, _cboStopKey,
            lblEmerg, _cboEmergencyKey
        });

        // 5. 크레딧 및 프로그램 정보
        var grpCredits = new GroupBox
        {
            Text = Localization.Get("GrpCredits"),
            Left = 20,
            Top = 418,
            Width = 465,
            Height = 185,
            Padding = new Padding(12)
        };

        string appName = "RPG Macro Studio";

        var lblAppHeader = new Label
        {
            Text = appName,
            Font = AppFontHelper.GetDefaultFont(11.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(41, 128, 185),
            Left = 20,
            Top = 24,
            AutoSize = true
        };

        var lblVersion = new Label
        {
            Text = $"Version {VersionInfo.CurrentVersion} (Official Release)",
            ForeColor = Color.DarkSlateGray,
            Left = 20,
            Top = 50,
            AutoSize = true
        };

        var btnReopenGetStarted = new Button
        {
            Text = Localization.Get("BtnReopenGetStarted"),
            Left = 20,
            Top = 76,
            Width = 205,
            Height = 29,
            BackColor = Color.FromArgb(41, 128, 185),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetDefaultFont(8.5f, FontStyle.Bold)
        };
        btnReopenGetStarted.Click += (s, e) =>
        {
            using var dlg = new GetStartedDialog();
            dlg.ShowDialog(this);
        };

        var btnViewChangelog = new Button
        {
            Text = Localization.Get("BtnViewChangelog"),
            Left = 235,
            Top = 76,
            Width = 195,
            Height = 29,
            BackColor = Color.FromArgb(236, 240, 241),
            ForeColor = Color.FromArgb(44, 62, 80),
            FlatStyle = FlatStyle.Standard,
            Font = AppFontHelper.GetDefaultFont(8.5f, FontStyle.Regular)
        };
        btnViewChangelog.Click += (s, e) =>
        {
            using var dlg = new ChangelogDialog();
            dlg.ShowDialog(this);
        };

        var lblSeparator = new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Height = 2,
            Width = 420,
            Left = 20,
            Top = 115
        };

        var lblCreditHeader = new Label
        {
            Text = "Created & Developed by:",
            Font = AppFontHelper.GetDefaultFont(8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(44, 62, 80),
            Left = 20,
            Top = 124,
            AutoSize = true
        };

        var lblAuthor = new Label
        {
            Text = "L. Jack (sbk4992@gmail.com) with Google Antigravity",
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(39, 174, 96),
            Left = 20,
            Top = 146,
            AutoSize = true
        };

        grpCredits.Controls.AddRange(new Control[]
        {
            lblAppHeader, lblVersion, btnReopenGetStarted, btnViewChangelog, lblSeparator, lblCreditHeader, lblAuthor
        });

        // 6. 하단 버튼 (저장 / 취소)
        var btnSave = new Button
        {
            Text = Localization.Get("BtnSaveSettings"),
            Left = 265,
            Top = 608,
            Width = 110,
            Height = 36,
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold)
        };
        btnSave.Click += (s, e) => SaveSettings();

        var btnCancel = new Button
        {
            Text = Localization.Get("BtnCancelSettings"),
            DialogResult = DialogResult.Cancel,
            Left = 385,
            Top = 608,
            Width = 100,
            Height = 36,
            Font = AppFontHelper.GetDefaultFont(9.5f)
        };

        Controls.AddRange(new Control[]
        {
            grpSound, grpLang, grpTheme, grpHotkeys, grpCredits, btnSave, btnCancel
        });

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        btnCancel.Click += (s, e) =>
        {
            AppThemeHelper.NotifyThemePreview(AppSettings.Current.IsDarkMode);
        };
        FormClosed += (s, e) =>
        {
            if (DialogResult != DialogResult.OK)
            {
                AppThemeHelper.NotifyThemePreview(AppSettings.Current.IsDarkMode);
            }
        };

        _rbDarkTheme.CheckedChanged += (s, e) =>
        {
            if (_rbDarkTheme.Checked)
            {
                AppThemeHelper.ApplyTheme(this, true);
                AppThemeHelper.NotifyThemePreview(true);
            }
        };
        _rbLightTheme.CheckedChanged += (s, e) =>
        {
            if (_rbLightTheme.Checked)
            {
                AppThemeHelper.ApplyTheme(this, false);
                AppThemeHelper.NotifyThemePreview(false);
            }
        };

        AppThemeHelper.ApplyTheme(this, AppSettings.Current.IsDarkMode);
    }

    private void LoadCurrentSettings()
    {
        _chkMute.Checked = AppSettings.Current.MuteSound;
        _cboLanguage.SelectedIndex = (AppSettings.Current.Language == "en") ? 1 : 0;

        if (AppSettings.Current.IsDarkMode)
            _rbDarkTheme.Checked = true;
        else
            _rbLightTheme.Checked = true;

        if (_cboRecordKey.Items.Contains(AppSettings.Current.RecordHotkey))
            _cboRecordKey.SelectedItem = AppSettings.Current.RecordHotkey;
        else
            _cboRecordKey.SelectedItem = "F9";

        if (_cboStartPauseKey.Items.Contains(AppSettings.Current.StartPauseHotkey))
            _cboStartPauseKey.SelectedItem = AppSettings.Current.StartPauseHotkey;
        else
            _cboStartPauseKey.SelectedItem = "F10";

        if (_cboStopKey.Items.Contains(AppSettings.Current.StopHotkey))
            _cboStopKey.SelectedItem = AppSettings.Current.StopHotkey;
        else
            _cboStopKey.SelectedItem = "F11";

        if (_cboEmergencyKey.Items.Contains(AppSettings.Current.EmergencyStopHotkey))
            _cboEmergencyKey.SelectedItem = AppSettings.Current.EmergencyStopHotkey;
        else
            _cboEmergencyKey.SelectedItem = "F12";
    }

    private void SaveSettings()
    {
        string rk = _cboRecordKey.SelectedItem?.ToString() ?? "F9";
        string spk = _cboStartPauseKey.SelectedItem?.ToString() ?? "F10";
        string sk = _cboStopKey.SelectedItem?.ToString() ?? "F11";
        string ek = _cboEmergencyKey.SelectedItem?.ToString() ?? "F12";

        // 4개 단축키 상호 중복 검증
        var keys = new[] { rk, spk, sk, ek };
        if (keys.Distinct().Count() < keys.Length)
        {
            MessageBox.Show(Localization.Get("ErrDuplicateHotkey"), Localization.Get("SettingsTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string oldLang = AppSettings.Current.Language;
        string newLang = (_cboLanguage.SelectedIndex == 1) ? "en" : "ko";

        string oldTheme = AppSettings.Current.ThemeMode;
        string newTheme = _rbLightTheme.Checked ? "Light" : "Dark";

        bool hotkeysChanged = (AppSettings.Current.RecordHotkey != rk) ||
                              (AppSettings.Current.StartPauseHotkey != spk) ||
                              (AppSettings.Current.StopHotkey != sk) ||
                              (AppSettings.Current.EmergencyStopHotkey != ek);

        AppSettings.Current.MuteSound = _chkMute.Checked;
        AppSettings.Current.Language = newLang;
        AppSettings.Current.ThemeMode = newTheme;
        AppSettings.Current.RecordHotkey = rk;
        AppSettings.Current.StartPauseHotkey = spk;
        AppSettings.Current.StopHotkey = sk;
        AppSettings.Current.EmergencyStopHotkey = ek;
        AppSettings.Save();

        GlobalHotkeyService.UpdateKeysFromSettings();

        LanguageChanged = (oldLang != newLang);
        ThemeChanged = (oldTheme != newTheme);
        HotkeysChanged = hotkeysChanged;

        DialogResult = DialogResult.OK;
        Close();
    }
}
