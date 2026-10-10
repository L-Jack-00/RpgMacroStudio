using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RpgMacroHarness.AI;
using RpgMacroHarness.Data;
using RpgMacroHarness.Vision;

namespace RpgMacroHarness.UI;

/// <summary>
/// Bring-Your-Own-Key(BYOK) 기반 멀티 AI 프로바이더 연동 및
/// 자연어 프롬프트 / 게임 화면 스크린샷 기반 FSM 매크로 프로필 자동 생성 다이얼로그입니다.
/// </summary>
public class AiAssistantDialog : Form
{
    private readonly Panel _pnlHeader;
    private readonly Label _lblTitle;
    private readonly Label _lblSubtitle;

    // AI 프로바이더 설정 영역
    private readonly GroupBox _grpAiSettings;
    private readonly Label _lblProvider;
    private readonly ComboBox _cboProvider;
    private readonly Label _lblApiKey;
    private readonly TextBox _txtApiKey;
    private readonly Button _btnTogglePassword;
    private readonly Label _lblModel;
    private readonly ComboBox _cboModel; // 직접 타이핑 가능한 DropDown 모드
    private readonly Button _btnFetchModels;
    private readonly Label _lblEndpoint;
    private readonly TextBox _txtEndpoint;

    // 시나리오 입력 영역
    private readonly GroupBox _grpScenario;
    private readonly Label _lblPromptGuide;
    private readonly TextBox _txtPrompt;
    private readonly FlowLayoutPanel _pnlPresetChips;
    private readonly Button _btnCaptureScreen;
    private readonly Button _btnAttachImage;
    private readonly Button _btnRemoveImage;
    private readonly PictureBox _picScreenshotPreview;
    private byte[]? _attachedScreenshotBytes;

    // 생성 제어 및 결과 영역
    private readonly GroupBox _grpResult;
    private readonly Button _btnGenerate;
    private readonly Label _lblStatus;
    private readonly TabControl _tabResult;
    private readonly TabPage _tabSummary;
    private readonly TabPage _tabJson;
    private readonly TextBox _txtSummary;
    private readonly TextBox _txtJson;
    private readonly Panel _pnlBottom;
    private readonly Button _btnApplyToCanvas;
    private readonly Button _btnClose;

    private bool _isGenerating = false;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// AI가 최종 생성 및 무결성 검증을 완료한 매크로 프로필 객체
    /// </summary>
    public MacroEventProfile? GeneratedProfile { get; private set; }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AppThemeHelper.UpdateFormTitleBarTheme(this, AppSettings.Current.IsDarkMode);
    }

    public AiAssistantDialog()
    {
        bool ko = Localization.IsKorean;

        Text = Localization.Get("AiDialogTitle");
        Width = 920;
        Height = 720;
        MinimumSize = new Size(880, 680);
        StartPosition = FormStartPosition.CenterParent;
        Font = AppFontHelper.GetEmojiFont(9f);
        DoubleBuffered = true;

        bool isDark = AppSettings.Current.IsDarkMode;
        BackColor = isDark ? Color.FromArgb(24, 28, 38) : Color.FromArgb(246, 248, 250);
        ForeColor = isDark ? Color.White : Color.FromArgb(30, 30, 30);

        // -------------------------------------------------------------
        // 1. 헤더 영역
        // -------------------------------------------------------------
        _pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(20, 10, 20, 8),
            BackColor = isDark ? Color.FromArgb(18, 22, 30) : Color.FromArgb(235, 238, 242)
        };

        _lblTitle = new Label
        {
            Text = ko ? "✨ 차세대 AI 매크로 어시스턴트" : "✨ Next-Gen AI Macro Assistant",
            Font = AppFontHelper.GetDefaultFont(13f, FontStyle.Bold),
            ForeColor = isDark ? Color.FromArgb(230, 235, 245) : Color.FromArgb(20, 30, 45),
            AutoSize = true,
            Location = new Point(18, 10)
        };

        _lblSubtitle = new Label
        {
            Text = ko
                ? "자연어 시나리오와 게임 화면을 기반으로 FSM 상태, 다중 AND 트리거, 안티밴 지연이 완비된 매크로 프로필을 전자동으로 설계합니다."
                : "Automatically designs robust FSM macro profiles with states, multi-AND triggers, and anti-ban jitter using natural language.",
            Font = AppFontHelper.GetDefaultFont(8.5f),
            ForeColor = isDark ? Color.FromArgb(160, 170, 185) : Color.FromArgb(90, 100, 115),
            AutoSize = true,
            Location = new Point(19, 36)
        };

        _pnlHeader.Controls.AddRange(new Control[] { _lblTitle, _lblSubtitle });

        // -------------------------------------------------------------
        // 2. AI 프로바이더 설정 영역
        // -------------------------------------------------------------
        _grpAiSettings = new GroupBox
        {
            Text = ko ? "⚙️ AI 연결 설정 (Bring-Your-Own-Key / 개인 API 키)" : "⚙️ AI Connection Settings (BYOK)",
            Location = new Point(18, 74),
            Width = 868,
            Height = 115,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = isDark ? Color.FromArgb(220, 225, 235) : Color.FromArgb(40, 45, 55)
        };

        _lblProvider = new Label { Text = Localization.Get("AiProviderLabel"), Location = new Point(16, 26), AutoSize = true };
        _cboProvider = new ComboBox
        {
            Location = new Point(105, 23),
            Width = 240,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cboProvider.Items.AddRange(new object[]
        {
            "Google Gemini (추천/무료티어)",
            "OpenAI (GPT-4o / GPT-4o-mini)",
            "Anthropic Claude (Claude 3.5 Sonnet)",
            "DeepSeek (V3 / R1)",
            "Ollama (로컬 무료/오프라인)",
            "사용자 정의 / OpenAI 호환 (Custom API)",
            "[테스트 하네스] Mock 엔진 (오프라인)"
        });

        _lblApiKey = new Label { Text = Localization.Get("AiApiKeyLabel"), Location = new Point(365, 26), AutoSize = true };
        _txtApiKey = new TextBox
        {
            Location = new Point(480, 23),
            Width = 330,
            UseSystemPasswordChar = true
        };

        _btnTogglePassword = new Button
        {
            Text = "👁️",
            Location = new Point(814, 22),
            Width = 38,
            Height = 24,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(9f)
        };
        _btnTogglePassword.Click += (s, e) =>
        {
            _txtApiKey.UseSystemPasswordChar = !_txtApiKey.UseSystemPasswordChar;
        };

        _lblModel = new Label { Text = Localization.Get("AiModelLabel"), Location = new Point(16, 58), AutoSize = true };
        _cboModel = new ComboBox
        {
            Location = new Point(105, 55),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDown // 자유 타이핑 허용!
        };

        _btnFetchModels = new Button
        {
            Text = Localization.Get("AiFetchModels"),
            Location = new Point(310, 54),
            Width = 145,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(8.5f)
        };
        _btnFetchModels.Click += async (s, e) => await FetchModelsAsync();

        _lblEndpoint = new Label { Text = Localization.Get("AiEndpointLabel"), Location = new Point(470, 58), AutoSize = true };
        _txtEndpoint = new TextBox
        {
            Location = new Point(570, 55),
            Width = 282
        };

        var lblSecurityNote = new Label
        {
            Text = ko
                ? "🔒 안내: 입력하신 API 키는 개발자 서버를 거치지 않고, 오직 사용자 PC에서 AI 공식 서버로 직접 HTTPS 통신합니다."
                : "🔒 Notice: Your API key connects directly from your PC to the official AI server. No developer servers involved.",
            Location = new Point(16, 88),
            AutoSize = true,
            Font = AppFontHelper.GetDefaultFont(7.8f),
            ForeColor = isDark ? Color.FromArgb(140, 180, 140) : Color.FromArgb(30, 120, 60)
        };

        _grpAiSettings.Controls.AddRange(new Control[]
        {
            _lblProvider, _cboProvider, _lblApiKey, _txtApiKey, _btnTogglePassword,
            _lblModel, _cboModel, _btnFetchModels, _lblEndpoint, _txtEndpoint,
            lblSecurityNote
        });

        // -------------------------------------------------------------
        // 3. 시나리오 입력 영역
        // -------------------------------------------------------------
        _grpScenario = new GroupBox
        {
            Text = ko ? "📝 매크로 시나리오 지시사항" : "📝 Macro Scenario Requirements",
            Location = new Point(18, 196),
            Width = 868,
            Height = 220,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = isDark ? Color.FromArgb(220, 225, 235) : Color.FromArgb(40, 45, 55)
        };

        _lblPromptGuide = new Label
        {
            Text = Localization.Get("AiPromptLabel"),
            Location = new Point(16, 22),
            AutoSize = true,
            Font = AppFontHelper.GetDefaultFont(9f, FontStyle.Bold)
        };

        _txtPrompt = new TextBox
        {
            Location = new Point(16, 44),
            Width = 660,
            Height = 110,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = AppFontHelper.GetDefaultFont(9.5f),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // 추천 예시 칩 버튼 바
        _pnlPresetChips = new FlowLayoutPanel
        {
            Location = new Point(16, 160),
            Width = 660,
            Height = 32,
            WrapContents = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        void AppendPromptText(string textToAppend)
        {
            if (string.IsNullOrWhiteSpace(_txtPrompt.Text))
            {
                _txtPrompt.Text = textToAppend;
            }
            else
            {
                _txtPrompt.Text = _txtPrompt.Text.TrimEnd() + Environment.NewLine + Environment.NewLine + textToAppend;
            }
            _txtPrompt.SelectionStart = _txtPrompt.Text.Length;
            _txtPrompt.ScrollToCaret();
            _txtPrompt.Focus();
        }

        var btnChip1 = CreateChipButton(ko ? "💡 체력 물약 & 버프" : "💡 Potion & Buff", () =>
        {
            AppendPromptText(ko
                ? "체력(HP) 게이지가 30% 이하로 떨어지면 1번 키로 물약을 마시고, 60초마다 2번 키로 버프 스킬을 시전해줘. 마우스 클릭 오차와 30ms 지연 난수화를 적용해줘."
                : "Drink potion with key '1' when HP is below 30%, and cast buff skill with key '2' every 60 seconds with anti-ban jitter.");
        });

        var btnChip2 = CreateChipButton(ko ? "💡 사냥 & 마을 귀환 FSM" : "💡 Hunting & Town FSM", () =>
        {
            AppendPromptText(ko
                ? "시작 상태에서 Space 키로 사냥터로 이동하고, '사냥 상태'에서는 A키와 S키로 몬스터를 공격해줘. 만약 '체력물약수량' 변수가 0 이하가 되면 B키(귀환석)를 누르고 '마을 상점 상태'로 전이해줘."
                : "Move to hunting field from initial state, attack with 'A' and 'S' in 'Hunt' state. When potion count drops to 0, use 'B' and change state to 'Town Shop'.");
        });

        var btnChip3 = CreateChipButton(ko ? "💡 보스 감지 긴급 회피" : "💡 Boss Detection Dodge", () =>
        {
            AppendPromptText(ko
                ? "전역 상태에서 화면 특정 영역에 보스 몬스터가 출현하거나 체력이 15% 이하가 되면 즉시 Space(회피기)를 2회 연타하고 비상 탈출해줘."
                : "In Global state, if boss appears or HP is below 15%, double-tap Space for dodge and perform emergency escape.");
        });

        _pnlPresetChips.Controls.AddRange(new Control[] { btnChip1, btnChip2, btnChip3 });

        // 화면 캡처 및 이미지 첨부 패널
        _btnCaptureScreen = new Button
        {
            Text = Localization.Get("AiCaptureScreen"),
            Location = new Point(688, 44),
            Width = 164,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = AppFontHelper.GetEmojiFont(9f)
        };
        _btnCaptureScreen.Click += (s, e) => CaptureCurrentScreen();

        _btnAttachImage = new Button
        {
            Text = Localization.Get("AiLoadImage"),
            Location = new Point(688, 78),
            Width = 164,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Font = AppFontHelper.GetEmojiFont(9f)
        };
        _btnAttachImage.Click += (s, e) => AttachImageFromFile();

        _btnRemoveImage = new Button
        {
            Text = Localization.Get("AiRemoveImage"),
            Location = new Point(688, 112),
            Width = 164,
            Height = 26,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Visible = false
        };
        _btnRemoveImage.Click += (s, e) => RemoveAttachedImage();

        _picScreenshotPreview = new PictureBox
        {
            Location = new Point(688, 142),
            Width = 164,
            Height = 65,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Visible = false
        };

        _grpScenario.Controls.AddRange(new Control[]
        {
            _lblPromptGuide, _txtPrompt, _pnlPresetChips,
            _btnCaptureScreen, _btnAttachImage, _btnRemoveImage, _picScreenshotPreview
        });

        // -------------------------------------------------------------
        // 4. 생성 제어 및 결과 영역
        // -------------------------------------------------------------
        _grpResult = new GroupBox
        {
            Text = ko ? "📋 생성 결과 및 미리보기" : "📋 Generation Result & Preview",
            Location = new Point(18, 424),
            Width = 868,
            Height = 205,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = isDark ? Color.FromArgb(220, 225, 235) : Color.FromArgb(40, 45, 55)
        };

        _btnGenerate = new Button
        {
            Text = Localization.Get("AiGenerateBtn"),
            Location = new Point(16, 22),
            Width = 240,
            Height = 36,
            BackColor = Color.FromArgb(41, 128, 185),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnGenerate.Click += async (s, e) => await StartGenerationAsync();

        _lblStatus = new Label
        {
            Text = Localization.Get("AiStatusReady"),
            Location = new Point(270, 31),
            AutoSize = true,
            Font = AppFontHelper.GetDefaultFont(9.5f),
            ForeColor = isDark ? Color.FromArgb(170, 180, 195) : Color.FromArgb(80, 90, 100)
        };

        _tabResult = new TabControl
        {
            Location = new Point(16, 66),
            Width = 836,
            Height = 125,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        _tabSummary = new TabPage(ko ? "구조 요약" : "Summary");
        _tabJson = new TabPage("JSON");

        _txtSummary = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = AppFontHelper.GetDefaultFont(9.5f)
        };
        _tabSummary.Controls.Add(_txtSummary);

        _txtJson = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 9f)
        };
        _tabJson.Controls.Add(_txtJson);

        _tabResult.TabPages.AddRange(new TabPage[] { _tabSummary, _tabJson });
        _grpResult.Controls.AddRange(new Control[] { _btnGenerate, _lblStatus, _tabResult });

        // -------------------------------------------------------------
        // 5. 하단 버튼 바
        // -------------------------------------------------------------
        _pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(20, 8, 20, 10),
            BackColor = isDark ? Color.FromArgb(18, 22, 30) : Color.FromArgb(235, 238, 242)
        };

        _btnApplyToCanvas = new Button
        {
            Text = Localization.Get("AiApplyToCanvas"),
            Width = 260,
            Height = 36,
            Location = new Point(20, 7),
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(10.5f, FontStyle.Bold),
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _btnApplyToCanvas.FlatAppearance.BorderSize = 0;
        _btnApplyToCanvas.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 204, 113);
        _btnApplyToCanvas.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 132, 73);

        // 눈에 확 띄는 선명한 에메랄드 녹색 및 활성화 테두리 하이라이트 커스텀 렌더링
        _btnApplyToCanvas.Paint += (s, e) =>
        {
            var btn = (Button)s!;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color bg = btn.Enabled
                ? (btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position)) ? Color.FromArgb(46, 204, 113) : Color.FromArgb(39, 174, 96))
                : Color.FromArgb(32, 58, 44);
            Color fg = btn.Enabled ? Color.White : Color.FromArgb(140, 175, 155);

            using var brush = new SolidBrush(bg);
            g.FillRectangle(brush, btn.ClientRectangle);

            if (btn.Enabled)
            {
                using var pen = new Pen(Color.FromArgb(70, 230, 140), 1.8f);
                g.DrawRectangle(pen, 1, 1, btn.Width - 2, btn.Height - 2);
            }

            TextRenderer.DrawText(g, btn.Text, btn.Font, btn.ClientRectangle, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        _btnApplyToCanvas.MouseEnter += (s, e) => _btnApplyToCanvas.Invalidate();
        _btnApplyToCanvas.MouseLeave += (s, e) => _btnApplyToCanvas.Invalidate();
        _btnApplyToCanvas.EnabledChanged += (s, e) => _btnApplyToCanvas.Invalidate();

        _btnApplyToCanvas.Click += (s, e) =>
        {
            if (GeneratedProfile != null)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        _btnClose = new Button
        {
            Text = ko ? "닫기" : "Close",
            Width = 100,
            Height = 34,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(_pnlBottom.Width - 120, 8),
            FlatStyle = FlatStyle.Flat
        };
        _btnClose.Click += (s, e) => Close();

        _pnlBottom.Controls.AddRange(new Control[] { _btnApplyToCanvas, _btnClose });
        _pnlBottom.Resize += (s, e) =>
        {
            _btnClose.Left = _pnlBottom.ClientSize.Width - _btnClose.Width - 20;
        };

        Controls.AddRange(new Control[]
        {
            _grpResult,
            _grpScenario,
            _grpAiSettings,
            _pnlBottom,
            _pnlHeader
        });

        _cboProvider.SelectedIndexChanged += (s, e) => OnProviderChanged();
        LoadSavedSettings();

        AppThemeHelper.ApplyTheme(this, isDark);
        AppThemeHelper.ThemePreviewChanged += HandleThemePreview;
        FormClosed += (s, e) => AppThemeHelper.ThemePreviewChanged -= HandleThemePreview;
    }

    private void HandleThemePreview(bool isDark)
    {
        if (IsDisposed) return;
        AppThemeHelper.ApplyTheme(this, isDark);
        _pnlHeader.BackColor = isDark ? Color.FromArgb(18, 22, 30) : Color.FromArgb(235, 238, 242);
        _lblTitle.ForeColor = isDark ? Color.FromArgb(230, 235, 245) : Color.FromArgb(20, 30, 45);
        _lblSubtitle.ForeColor = isDark ? Color.FromArgb(160, 175, 195) : Color.FromArgb(80, 95, 110);
        _pnlBottom.BackColor = isDark ? Color.FromArgb(18, 22, 30) : Color.FromArgb(235, 238, 242);
    }

    private Button CreateChipButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 26,
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetEmojiFont(8.5f),
            Cursor = Cursors.Hand
        };
        btn.Click += (s, e) => onClick();
        return btn;
    }

    private void OnProviderChanged()
    {
        var provider = GetSelectedProviderType();
        _cboModel.Items.Clear();

        var presets = AiProviderConfig.GetPresetModels(provider);
        foreach (var p in presets) _cboModel.Items.Add(p);

        if (_cboModel.Items.Count > 0)
        {
            _cboModel.SelectedIndex = 0;
        }

        // 엔드포인트 기본값 표시
        var tempCfg = new AiProviderConfig { ProviderType = provider };
        _txtEndpoint.Text = tempCfg.GetEffectiveEndpoint();

        // Custom 또는 Ollama인 경우 엔드포인트 강조
        bool needsCustomEndpoint = (provider == AiProviderType.CustomOpenAiCompatible || provider == AiProviderType.Ollama);
        _txtEndpoint.Enabled = true;

        if (provider == AiProviderType.MockTest)
        {
            _txtApiKey.Text = "NOT-REQUIRED-MOCK";
            _txtApiKey.Enabled = false;
        }
        else
        {
            _txtApiKey.Enabled = true;
            if (_txtApiKey.Text == "NOT-REQUIRED-MOCK") _txtApiKey.Text = "";
        }
    }

    private AiProviderType GetSelectedProviderType()
    {
        return _cboProvider.SelectedIndex switch
        {
            0 => AiProviderType.Gemini,
            1 => AiProviderType.OpenAI,
            2 => AiProviderType.Claude,
            3 => AiProviderType.DeepSeek,
            4 => AiProviderType.Ollama,
            5 => AiProviderType.CustomOpenAiCompatible,
            6 => AiProviderType.MockTest,
            _ => AiProviderType.Gemini
        };
    }

    private void LoadSavedSettings()
    {
        string saved = AppSettings.Current.AiProvider;
        _cboProvider.SelectedIndex = saved switch
        {
            "Gemini" => 0,
            "OpenAI" => 1,
            "Claude" => 2,
            "DeepSeek" => 3,
            "Ollama" => 4,
            "CustomOpenAiCompatible" => 5,
            "MockTest" => 6,
            _ => 0
        };

        if (!string.IsNullOrEmpty(AppSettings.Current.AiApiKey))
        {
            _txtApiKey.Text = AppSettings.Current.AiApiKey;
        }

        if (!string.IsNullOrEmpty(AppSettings.Current.AiModelName))
        {
            _cboModel.Text = AppSettings.Current.AiModelName;
        }

        if (!string.IsNullOrEmpty(AppSettings.Current.AiCustomEndpoint))
        {
            _txtEndpoint.Text = AppSettings.Current.AiCustomEndpoint;
        }
    }

    private void SaveCurrentSettings()
    {
        var provider = GetSelectedProviderType();
        AppSettings.Current.AiProvider = provider.ToString();
        AppSettings.Current.AiApiKey = _txtApiKey.Text.Trim();
        AppSettings.Current.AiModelName = _cboModel.Text.Trim();
        AppSettings.Current.AiCustomEndpoint = _txtEndpoint.Text.Trim();
        AppSettings.Save();
    }

    private async Task FetchModelsAsync()
    {
        var provider = GetSelectedProviderType();
        var cfg = new AiProviderConfig
        {
            ProviderType = provider,
            ApiKey = _txtApiKey.Text.Trim(),
            CustomEndpoint = _txtEndpoint.Text.Trim()
        };

        _btnFetchModels.Enabled = false;
        _btnFetchModels.Text = "⏳ 조회 중...";

        try
        {
            var models = await cfg.FetchAvailableModelsAsync();
            if (models != null && models.Count > 0)
            {
                _cboModel.Items.Clear();
                foreach (var m in models) _cboModel.Items.Add(m);
                _cboModel.SelectedIndex = 0;
                MessageBox.Show(
                    Localization.IsKorean ? $"최신 모델 목록 {models.Count}개를 성공적으로 조회했습니다." : $"Successfully fetched {models.Count} models.",
                    "AI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Fetch Models Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnFetchModels.Enabled = true;
            _btnFetchModels.Text = Localization.Get("AiFetchModels");
        }
    }

    private void CaptureCurrentScreen()
    {
        try
        {
            // 대화창을 잠시 숨기고 화면 캡처
            Hide();
            Thread.Sleep(200);

            var bounds = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
            using var bmp = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }

            Show();

            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            // AI Vision 전송용 고해상도 최적화 (1280px 리사이즈 + JPEG 80% 압축)
            _attachedScreenshotBytes = AiProfileGenerator.OptimizeImageForAi(ms.ToArray());

            _picScreenshotPreview.Image = new Bitmap(bmp);
            _picScreenshotPreview.Visible = true;
            _btnRemoveImage.Visible = true;
        }
        catch (Exception ex)
        {
            Show();
            MessageBox.Show($"화면 캡처 실패: {ex.Message}", "Capture Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AttachImageFromFile()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "이미지 파일 (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|모든 파일 (*.*)|*.*",
            Title = "게임 화면 스크린샷 선택"
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                byte[] raw = File.ReadAllBytes(ofd.FileName);
                // AI Vision 전송용 고해상도 최적화 (1280px 리사이즈 + JPEG 80% 압축)
                _attachedScreenshotBytes = AiProfileGenerator.OptimizeImageForAi(raw);
                using var ms = new MemoryStream(_attachedScreenshotBytes);
                _picScreenshotPreview.Image = Image.FromStream(ms);
                _picScreenshotPreview.Visible = true;
                _btnRemoveImage.Visible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이미지 로드 실패: {ex.Message}", "Load Image Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void RemoveAttachedImage()
    {
        _attachedScreenshotBytes = null;
        _picScreenshotPreview.Image?.Dispose();
        _picScreenshotPreview.Image = null;
        _picScreenshotPreview.Visible = false;
        _btnRemoveImage.Visible = false;
    }

    private async Task StartGenerationAsync()
    {
        if (_isGenerating) return;

        string prompt = _txtPrompt.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            MessageBox.Show(
                Localization.IsKorean ? "원하시는 매크로 시나리오 지시사항을 입력해주세요." : "Please enter macro scenario requirements.",
                "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtPrompt.Focus();
            return;
        }

        SaveCurrentSettings();

        var provider = GetSelectedProviderType();
        var cfg = new AiProviderConfig
        {
            ProviderType = provider,
            ApiKey = _txtApiKey.Text.Trim(),
            ModelName = _cboModel.Text.Trim(),
            CustomEndpoint = _txtEndpoint.Text.Trim()
        };

        if (provider != AiProviderType.MockTest && provider != AiProviderType.Ollama && string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            MessageBox.Show(
                Localization.IsKorean ? "API 키를 입력해주세요. (설정창에 입력한 키는 사용자 PC에만 저장됩니다.)" : "Please enter your API Key.",
                "API Key Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtApiKey.Focus();
            return;
        }

        _isGenerating = true;
        _btnGenerate.Enabled = false;
        _btnApplyToCanvas.Enabled = false;
        _lblStatus.Text = Localization.Get("AiStatusGenerating");
        _lblStatus.ForeColor = Color.FromArgb(41, 128, 185);

        _cts = new CancellationTokenSource();

        // 경과 시간 실시간 타이머
        int elapsedSec = 0;
        using var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) =>
        {
            elapsedSec++;
            if (_isGenerating)
            {
                string baseText = Localization.Get("AiStatusGenerating");
                _lblStatus.Text = $"{baseText} ({elapsedSec}초 경과)";
            }
        };
        timer.Start();

        try
        {
            var client = AiProviderClientFactory.CreateClient(cfg);
            var generator = new AiProfileGenerator(client);

            var result = await Task.Run(() => generator.GenerateProfileAsync(prompt, _attachedScreenshotBytes, _cts.Token));

            timer.Stop();

            if (result.Success && result.Profile != null)
            {
                GeneratedProfile = result.Profile;
                var p = result.Profile;
                _lblStatus.Text = Localization.IsKorean
                    ? $"✅ 프로필 설계 완료 (상태 {p.States.Count}개, 이벤트 {p.Events.Count}개) ➔ 아래 [🚀 현재 캔버스에 즉시 적용]을 눌러주세요."
                    : $"✅ Generation Complete ({p.States.Count} states, {p.Events.Count} events) ➔ Click [🚀 Apply Directly to Canvas] below.";
                _lblStatus.ForeColor = Color.FromArgb(39, 174, 96);

                // 요약 표시
                string summaryText = $"""
[프로필 기본 정보]
• 프로필 이름: {p.ProfileName}
• 시작 상태: {p.InitialState}
• 검사 주기: {p.CheckIntervalMs}ms
• 긴급 비상정지 키: {p.EmergencyStopKey}

[FSM 상태 목록 ({p.States.Count}개)]
{string.Join(", ", p.States)}

[이벤트 목록 ({p.Events.Count}개)]
""";
                foreach (var evt in p.Events)
                {
                    summaryText += $"\n▶ [{evt.StateTag}] {evt.Name} (트리거 {evt.Triggers.Count}개, 액션 {evt.Actions.Count}개)";
                }

                if (p.Variables.Count > 0)
                {
                    summaryText += "\n\n[블랙보드 변수 목록]\n";
                    foreach (var kvp in p.Variables)
                    {
                        summaryText += $"• {kvp.Key} = {kvp.Value}\n";
                    }
                }

                _txtSummary.Text = summaryText.Trim();
                _txtJson.Text = result.RawJson;

                _btnApplyToCanvas.Enabled = true;
                _tabResult.SelectedTab = _tabSummary;
            }
            else
            {
                _lblStatus.Text = $"❌ 생성 실패: {result.ErrorMessage}";
                _lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
                MessageBox.Show(result.ErrorMessage, "AI Generation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            timer.Stop();
            string errMsg = ex.Message;
            if (ex is TimeoutException || errMsg.Contains("Timeout") || errMsg.Contains("canceled"))
            {
                errMsg = Localization.IsKorean
                    ? "AI 서버의 응답 대기 시간이 초과되었습니다.\n\n안내 사항:\n1. 네트워크 연결 상태를 확인하거나 잠시 후 다시 시도해 주세요.\n2. 화면 캡처 이미지가 첨부되어 있다면 이미지를 제거하고 시도해 보세요.\n3. 다른 모델이나 API 제공자를 선택해 보세요."
                    : "AI server response timed out.\n\nTips:\n1. Check your network connection and retry shortly.\n2. Remove attached screenshot and try text-only.\n3. Select a different model or provider.";
            }

            _lblStatus.Text = Localization.IsKorean ? "❌ 서버 응답 시간 초과" : "❌ Server Response Timed Out";
            _lblStatus.ForeColor = Color.FromArgb(192, 57, 43);
            MessageBox.Show(errMsg, "AI Server Timeout", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            timer.Stop();
            _isGenerating = false;
            _btnGenerate.Enabled = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _picScreenshotPreview.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
