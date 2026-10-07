using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RpgMacroHarness.Data;
using RpgMacroHarness.Vision;

namespace RpgMacroHarness.UI;

/// <summary>
/// 3단계 스텝별 개념 튜토리얼 및 5대 실전 템플릿을 생성해주는 도우미 마법사 대화상자입니다.
/// </summary>
public class TemplateWizardDialog : Form
{
    private int _currentStep = 0;
    private const int TotalSteps = 3;

    private readonly Panel _pnlHeader;
    private readonly Label _lblTitle;
    private readonly Label _lblSubtitle;
    private readonly Panel _pnlBody;
    private readonly Panel _pnlFooter;
    private readonly Button _btnPrev;
    private readonly Button _btnNext;
    private readonly Label _lblStepIndicator;

    // 2단계 선택 템플릿 인덱스 (0~4)
    private int _selectedTemplateIndex = 0;

    // 3단계 입력 컨트롤들
    private CustomComboBox _cboKey1 = null!;
    private CustomComboBox _cboKey2 = null!;
    private NumericUpDown _numParam1 = null!;
    private NumericUpDown _numParam2 = null!;
    private Label _lblKey1 = null!;
    private Label _lblKey2 = null!;
    private Label _lblParam1 = null!;
    private Label _lblParam2 = null!;

    public MacroEventProfile? GeneratedProfile { get; private set; }

    public TemplateWizardDialog()
    {
        bool ko = Localization.IsKorean;

        Text = ko ? "RPG 매크로 스튜디오 - ✨ 도우미 마법사" : "RPG Macro Studio - ✨ Helper Wizard";
        Width = 800;
        Height = 620;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = AppFontHelper.GetEmojiFont(9.5f);

        _pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(24, 14, 24, 10)
        };
        _lblTitle = new Label
        {
            AutoSize = true,
            Font = AppFontHelper.GetEmojiFont(14f, FontStyle.Bold),
            Location = new Point(20, 12)
        };
        _lblSubtitle = new Label
        {
            AutoSize = true,
            Font = AppFontHelper.GetDefaultFont(9f),
            ForeColor = Color.Gray,
            Location = new Point(22, 42)
        };
        _pnlHeader.Controls.AddRange(new Control[] { _lblTitle, _lblSubtitle });

        _pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(20, 12, 20, 12)
        };

        _btnPrev = new Button
        {
            Text = ko ? "◀ 이전 단계" : "◀ Back",
            Width = 110,
            Height = 34,
            Location = new Point(20, 13),
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetDefaultFont(9.5f)
        };
        _btnPrev.Click += (s, e) => Navigate(-1);

        _lblStepIndicator = new Label
        {
            AutoSize = false,
            Width = 200,
            Height = 34,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(300, 13),
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold),
            ForeColor = Color.Gray
        };

        _btnNext = new Button
        {
            Text = ko ? "다음 단계 ▶" : "Next Step ▶",
            Width = 110,
            Height = 34,
            Location = new Point(660, 13),
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetDefaultFont(9.5f)
        };
        _btnNext.Click += (s, e) => Navigate(1);

        _pnlFooter.Controls.AddRange(new Control[] { _btnPrev, _lblStepIndicator, _btnNext });

        _pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16)
        };

        Controls.Add(_pnlBody);
        Controls.Add(_pnlFooter);
        Controls.Add(_pnlHeader);

        RenderCurrentStep();
        ApplyTheme();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AppThemeHelper.UpdateFormTitleBarTheme(this, AppSettings.Current.IsDarkMode);
    }

    private void Navigate(int delta)
    {
        int newStep = _currentStep + delta;
        if (newStep >= 0 && newStep < TotalSteps)
        {
            _currentStep = newStep;
            RenderCurrentStep();
            ApplyTheme();
        }
    }

    private void RenderCurrentStep()
    {
        bool ko = Localization.IsKorean;
        _pnlBody.Controls.Clear();
        _lblStepIndicator.Text = ko ? $"[단계 {_currentStep + 1} / {TotalSteps}]" : $"[Step {_currentStep + 1} / {TotalSteps}]";
        _btnPrev.Enabled = (_currentStep > 0);
        _btnNext.Visible = (_currentStep < TotalSteps - 1);

        switch (_currentStep)
        {
            case 0:
                RenderStep1_Tutorial(ko);
                break;
            case 1:
                RenderStep2_ChooseTemplate(ko);
                break;
            case 2:
                RenderStep3_ConfigureAndCreate(ko);
                break;
        }
    }

    private void RenderStep1_Tutorial(bool ko)
    {
        _lblTitle.Text = ko ? "📖 1단계: FSM 핵심 개념 30초 튜토리얼" : "📖 Step 1: FSM Core Concepts Tutorial";
        _lblSubtitle.Text = ko ? "RPG 매크로 스튜디오를 움직이는 5대 핵심 요소를 쉽게 이해해보세요" : "Understand the 5 fundamental building blocks of RPG Macro Studio";

        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(5)
        };
        for (int i = 0; i < 5; i++) container.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

        container.Controls.Add(CreateConceptRow(
            "🏷️ " + (ko ? "상태 (State) = 캐릭터의 '상황'" : "State = Character's Situation"),
            ko ? "사냥터에 있을 때, 마을에 있을 때, 사망했을 때를 딱딱 나눕니다. 사냥 중일 때 마을 규칙이 엉켜서 발동하는 것을 원천 차단합니다." : "Segments logic into states (Hunting, Town, Dead). Prevents town actions from firing while fighting."
        ), 0, 0);

        container.Controls.Add(CreateConceptRow(
            "👁️ " + (ko ? "조건 (Trigger) = 컴퓨터의 '눈'" : "Trigger = Computer's Eyes"),
            ko ? "화면을 보고 판단하는 기준입니다. (체력 게이지가 30% 이하인가?, 특정 몬스터/아이콘이 떴는가?, 10초가 지났는가?)" : "Criteria evaluated by vision or timers (e.g., HP bar < 30%, icon matched, interval elapsed)."
        ), 0, 1);

        container.Controls.Add(CreateConceptRow(
            "✋ " + (ko ? "행동 (Action) = 컴퓨터의 '손'" : "Action = Computer's Hands"),
            ko ? "조건이 만족되었을 때 실행할 동작입니다. (키보드 키 누름, 마우스 좌표 클릭, 몇 초 대기, 다른 상태로 전이 등)" : "Execution commands when conditions are met (Key presses, clicks, delays, state transitions)."
        ), 0, 2);

        container.Controls.Add(CreateConceptRow(
            "⚡ " + (ko ? "병렬 이벤트 (Parallel) = '멀티태스킹'" : "Parallel = True Multitasking"),
            ko ? "공격 키를 계속 누르고 있는 도중에도, 피가 깎이면 멈추지 않고 즉시 물약을 먹을 수 있게 독립적으로 돌아가는 파이프라인입니다." : "Independent event line running concurrently so potion drinking triggers immediately without interrupting regular attacks."
        ), 0, 3);

        container.Controls.Add(CreateConceptRow(
            "🔢 " + (ko ? "변수 (Variable) = '숫자 카운터/스위치'" : "Variable = Counter & Switch"),
            ko ? "포션을 마신 횟수를 세거나 던전 바퀴 수를 누적하여, 10번 돌면 마을로 복귀하게 만드는 영리한 계산기입니다." : "Accumulates counts (e.g. potions consumed, dungeon runs) to trigger return-to-town states."
        ), 0, 4);

        _pnlBody.Controls.Add(container);
    }

    private void RenderStep2_ChooseTemplate(bool ko)
    {
        _lblTitle.Text = ko ? "📦 2단계: 실전 매크로 템플릿 선택" : "📦 Step 2: Choose Preset Template";
        _lblSubtitle.Text = ko ? "가장 자주 사용되는 5대 표준 시나리오 중 하나를 선택하세요" : "Select one of the 5 battle-tested real-world macro templates";

        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };

        string[] titles = new[]
        {
            "🛡️ " + (ko ? "기본 사냥 & 자동 물약 (추천)" : "Basic Hunting & Auto Potion (Recommended)"),
            "🔄 " + (ko ? "주기적 버프 스킬 유지" : "Periodic Buff Skill Keeper"),
            "🎣 " + (ko ? "반응형 자동 낚시 / 채집" : "Responsive Auto Fishing / Gathering"),
            "🏃 " + (ko ? "사냥터 ➔ 마을 왕복 FSM 상태전이" : "Hunting ➔ Town Roundtrip FSM"),
            "⚡ " + (ko ? "단순 반복 키/마우스 루프" : "Simple Repetitive Keystroke Loop")
        };

        string[] descs = new[]
        {
            ko 
                ? "사냥 상태에서 공격 키를 주기적으로 반복하면서, 체력바가 기준치 이하로 떨어지면 물약 키를 즉시 병렬 복용하는 가장 대표적인 매크로입니다."
                : "Repeats attack skills periodically while running an independent parallel trigger that drinks HP potions when health drops.",
            ko
                ? "전투나 이동 중 X초마다 잊지 않고 버프 스킬 키를 눌러 버프 상태를 상시 유지하는 타이머 기반 템플릿입니다."
                : "Timer-based pipeline that refreshes buff skills every X seconds during routine operations.",
            ko
                ? "찌를 던진 후 대기하다가, 화면에 입질(느낌표, 물방울, 특정 아이콘)이 감지되면 챔질 키를 누르고 다시 낚시를 시작하는 비전 템플릿입니다."
                : "Vision-based template that waits for an icon or float alert to pop up on screen, then hits the hook key.",
            ko
                ? "사냥 중 물약이 소진되거나 위험해지면 마을 복귀 스킬을 쓰고 '마을' 상태로 전이하여 휴식한 뒤 재출발하는 본격 뇌 분리형 FSM 템플릿입니다."
                : "Full FSM demo that switches from 'Hunting' state to 'Town' state upon danger, rests, and transitions back.",
            ko
                ? "복잡한 조건 없이 지정한 키나 마우스 클릭을 정해진 간격마다 무한히 누르는 가장 기초적이고 가벼운 오토 매크로입니다."
                : "The simplest unconditional macro that fires designated keystrokes or mouse clicks at fixed intervals."
        };

        var radioButtons = new List<CustomRadioButton>();
        int top = 10;
        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var card = new Panel
            {
                Top = top,
                Left = 10,
                Width = 720,
                Height = 68,
                Cursor = Cursors.Hand,
                Padding = new Padding(12, 8, 12, 8)
            };

            var rb = new CustomRadioButton
            {
                Text = titles[i],
                Font = AppFontHelper.GetEmojiFont(10.5f, FontStyle.Bold),
                Checked = (_selectedTemplateIndex == idx),
                Dock = DockStyle.Top,
                Height = 26,
                Cursor = Cursors.Hand,
                AutoCheck = false // 수동 상호 배제 제어로 패널 분리 환경에서도 완벽한 단일 선택 보장
            };
            radioButtons.Add(rb);

            rb.Click += (s, e) => SelectTemplate(idx, pnl, radioButtons);

            var descLbl = new Label
            {
                Text = descs[i],
                Font = AppFontHelper.GetDefaultFont(8.8f),
                ForeColor = Color.Gray,
                Dock = DockStyle.Fill
            };

            card.Click += (s, e) => SelectTemplate(idx, pnl, radioButtons);
            descLbl.Click += (s, e) => SelectTemplate(idx, pnl, radioButtons);

            card.Controls.Add(descLbl);
            card.Controls.Add(rb);
            card.Tag = idx;
            pnl.Controls.Add(card);

            top += 76;
        }

        HighlightSelectedTemplateCard(pnl);
        _pnlBody.Controls.Add(pnl);
    }

    private void SelectTemplate(int selectedIdx, Panel container, List<CustomRadioButton> radioButtons)
    {
        _selectedTemplateIndex = selectedIdx;
        for (int j = 0; j < radioButtons.Count; j++)
        {
            radioButtons[j].Checked = (j == selectedIdx);
        }
        HighlightSelectedTemplateCard(container);
    }

    private void HighlightSelectedTemplateCard(Panel container)
    {
        bool isDark = AppSettings.Current.IsDarkMode;
        foreach (Control c in container.Controls)
        {
            if (c is Panel card && card.Tag is int idx)
            {
                bool isSel = (idx == _selectedTemplateIndex);
                if (isSel)
                {
                    card.BackColor = isDark ? Color.FromArgb(30, 48, 75) : Color.FromArgb(220, 235, 252);
                }
                else
                {
                    card.BackColor = isDark ? Color.FromArgb(22, 28, 40) : Color.FromArgb(240, 244, 250);
                }
            }
        }
    }

    private void RenderStep3_ConfigureAndCreate(bool ko)
    {
        _lblTitle.Text = ko ? "⚙️ 3단계: 간이 설정 & 프로필 생성" : "⚙️ Step 3: Quick Setup & Generate";
        _lblSubtitle.Text = ko ? "내 단축키에 맞추어 빈칸 몇 가지만 콕콕 채우고 Studio에 바로 로드하세요" : "Fill in your favorite keycaps and intervals, then load into Studio";

        var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 20, 10) };

        var lblSelectedTitle = new Label
        {
            Font = AppFontHelper.GetEmojiFont(11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(41, 128, 185),
            Dock = DockStyle.Top,
            Height = 30
        };

        var grpInputs = new GroupBox
        {
            Text = ko ? "🔑 핵심 파라미터 간이 설정" : "🔑 Quick Parameters",
            Dock = DockStyle.Top,
            Height = 190,
            Padding = new Padding(16, 20, 16, 16),
            Font = AppFontHelper.GetDefaultFont(9.5f)
        };

        string[] presetKeys = new[]
        {
            "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "D0",
            "Space", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "Q", "W", "E", "R", "T", "Y", "A", "S", "D", "F", "G", "Z", "X", "C", "V", "B",
            "Tab", "Enter", "ShiftKey", "ControlKey", "Menu", "Escape", "Insert", "Delete",
            "None"
        };

        _lblKey1 = new Label { Left = 20, Top = 34, Width = 150, AutoSize = false };
        _cboKey1 = new CustomComboBox { Left = 180, Top = 30, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboKey1.Items.AddRange(presetKeys);

        _lblKey2 = new Label { Left = 20, Top = 72, Width = 150, AutoSize = false };
        _cboKey2 = new CustomComboBox { Left = 180, Top = 68, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboKey2.Items.AddRange(presetKeys);

        _lblParam1 = new Label { Left = 360, Top = 34, Width = 170, AutoSize = false };
        _numParam1 = new NumericUpDown { Left = 540, Top = 30, Width = 130, Minimum = 1, Maximum = 999999 };

        _lblParam2 = new Label { Left = 360, Top = 72, Width = 170, AutoSize = false };
        _numParam2 = new NumericUpDown { Left = 540, Top = 68, Width = 130, Minimum = 1, Maximum = 999999 };

        var lblHint = new Label
        {
            Text = ko
                ? "💡 생성 후 Studio 캔버스에서 노드와 와이어를 언제든 자유롭게 추가·수정할 수 있습니다.\n" +
                  "   (체력 게이지 위치나 아이콘 이미지는 Studio의 스포이드 및 영역 선택 도구로 캡처하시면 됩니다.)"
                : "💡 You can freely customize all nodes and bezier wires in Studio canvas after generation.\n" +
                  "   (Gauge coordinates and template images can be captured easily with Studio's built-in picker.)",
            Left = 20,
            Top = 118,
            Width = 660,
            Height = 48,
            ForeColor = Color.DarkSlateGray,
            Font = AppFontHelper.GetDefaultFont(8.8f)
        };

        grpInputs.Controls.AddRange(new Control[]
        {
            _lblKey1, _cboKey1, _lblKey2, _cboKey2,
            _lblParam1, _numParam1, _lblParam2, _numParam2,
            lblHint
        });

        // 템플릿별 기본값 세팅
        ConfigureInputsForTemplate(_selectedTemplateIndex, ko, lblSelectedTitle);

        // 생성 버튼 컨테이너
        var pnlBtn = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 25, 20, 10) };
        var btnCreate = new Button
        {
            Text = ko 
                ? "🎉 프로필 생성 및 Studio로 불러오기\n(캔버스에 스마트 노드 그래프 즉시 로드)" 
                : "🎉 Generate Profile & Load into Studio\n(Instantly visualizes FSM nodes in canvas)",
            Font = AppFontHelper.GetEmojiFont(11f, FontStyle.Bold),
            Height = 70,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCreate.FlatAppearance.BorderSize = 0;
        btnCreate.Click += (s, e) => ExecuteProfileCreation(ko);

        pnlBtn.Controls.Add(btnCreate);

        pnl.Controls.Add(pnlBtn);
        pnl.Controls.Add(grpInputs);
        pnl.Controls.Add(lblSelectedTitle);

        _pnlBody.Controls.Add(pnl);
    }

    private void ConfigureInputsForTemplate(int idx, bool ko, Label titleLabel)
    {
        switch (idx)
        {
            case 0: // 기본 사냥 & 자동 물약
                titleLabel.Text = ko ? "선택됨: 🛡️ [기본 사냥 & 자동 물약]" : "Selected: 🛡️ [Basic Hunting & Auto Potion]";
                _lblKey1.Text = ko ? "기본 공격 키:" : "Attack Key:";
                _cboKey1.Text = "D1"; // 숫자 1
                _lblKey2.Text = ko ? "체력 물약 키:" : "Potion Key:";
                _cboKey2.Text = "D2"; // 숫자 2
                _lblParam1.Text = ko ? "공격 반복 간격 (ms):" : "Attack Interval (ms):";
                _numParam1.Value = 350;
                _lblParam2.Text = ko ? "물약 기준 체력 (%):" : "Potion HP Threshold (%):";
                _numParam2.Value = 35;
                break;

            case 1: // 주기적 버프 유지
                titleLabel.Text = ko ? "선택됨: 🔄 [주기적 버프 스킬 유지]" : "Selected: 🔄 [Periodic Buff Keeper]";
                _lblKey1.Text = ko ? "주 공격/이동 키:" : "Main Action Key:";
                _cboKey1.Text = "Space";
                _lblKey2.Text = ko ? "버프 스킬 키:" : "Buff Skill Key:";
                _cboKey2.Text = "F1";
                _lblParam1.Text = ko ? "버프 시전 주기 (초):" : "Buff Interval (sec):";
                _numParam1.Value = 15;
                _lblParam2.Text = ko ? "공격 반복 주기 (ms):" : "Attack Delay (ms):";
                _numParam2.Value = 400;
                break;

            case 2: // 반응형 낚시
                titleLabel.Text = ko ? "선택됨: 🎣 [반응형 자동 낚시 / 채집]" : "Selected: 🎣 [Responsive Fishing / Gathering]";
                _lblKey1.Text = ko ? "찌 던지기 키:" : "Cast Line Key:";
                _cboKey1.Text = "Space";
                _lblKey2.Text = ko ? "입질 챔질 키:" : "Hook Key:";
                _cboKey2.Text = "Space";
                _lblParam1.Text = ko ? "입질 감지 주기 (ms):" : "Detection Scan (ms):";
                _numParam1.Value = 150;
                _lblParam2.Text = ko ? "챔질 후 회수 대기 (ms):" : "Post-Hook Delay (ms):";
                _numParam2.Value = 1500;
                break;

            case 3: // 사냥터 ➔ 마을 왕복
                titleLabel.Text = ko ? "선택됨: 🏃 [사냥터 ➔ 마을 왕복 FSM]" : "Selected: 🏃 [Hunting ➔ Town Roundtrip FSM]";
                _lblKey1.Text = ko ? "사냥터 공격 키:" : "Hunting Attack Key:";
                _cboKey1.Text = "D1";
                _lblKey2.Text = ko ? "마을 귀환 스킬 키:" : "Town Return Key:";
                _cboKey2.Text = "B";
                _lblParam1.Text = ko ? "마을 체류/휴식 초 (초):" : "Town Rest Time (sec):";
                _numParam1.Value = 5;
                _lblParam2.Text = ko ? "위험 체력 기준 (%):" : "Emergency HP Threshold (%):";
                _numParam2.Value = 20;
                break;

            case 4: // 단순 반복 루프
                titleLabel.Text = ko ? "선택됨: ⚡ [단순 반복 키/마우스 루프]" : "Selected: ⚡ [Simple Repetitive Keystroke Loop]";
                _lblKey1.Text = ko ? "반복 누를 키:" : "Repeated Key:";
                _cboKey1.Text = "Space";
                _lblKey2.Text = ko ? "(보조 키 옵션):" : "(Secondary Key):";
                _cboKey2.Text = "None";
                _lblParam1.Text = ko ? "입력 간격 주기 (ms):" : "Keystroke Delay (ms):";
                _numParam1.Value = 500;
                _lblParam2.Text = ko ? "키 누름 유지 (ms):" : "Key Hold Time (ms):";
                _numParam2.Value = 40;
                break;
        }
    }

    private void ExecuteProfileCreation(bool ko)
    {
        GeneratedProfile = BuildProfileForTemplate(_selectedTemplateIndex, ko);
        DialogResult = DialogResult.OK;
        Close();
    }

    private MacroEventProfile BuildProfileForTemplate(int idx, bool ko)
    {
        var profile = new MacroEventProfile
        {
            EmergencyStopKey = "F12",
            CheckIntervalMs = 100
        };
        profile.States.Clear();
        profile.Events.Clear();

        string key1 = string.IsNullOrWhiteSpace(_cboKey1.Text) ? "Space" : _cboKey1.Text.Trim();
        string key2 = string.IsNullOrWhiteSpace(_cboKey2.Text) ? "D1" : _cboKey2.Text.Trim();
        int param1 = (int)_numParam1.Value;
        int param2 = (int)_numParam2.Value;

        switch (idx)
        {
            case 0: // 기본 사냥 & 자동 물약
                profile.ProfileName = ko ? "기본 사냥 & 자동 물약 프로필" : "Hunting & Auto Potion Profile";
                profile.States.Add("전역 (Global)");
                profile.States.Add(ko ? "사냥 중 (Hunting)" : "Hunting");
                profile.InitialState = ko ? "사냥 중 (Hunting)" : "Hunting";

                // 이벤트 1: 물약 복용 (병렬)
                var evtPotion = new MacroEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ko ? $"체력 {param2}% 이하 시 물약 사용" : $"Use Potion when HP < {param2}%",
                    StateTag = ko ? "사냥 중 (Hunting)" : "Hunting",
                    IsEnabled = true,
                    IsParallel = true
                };
                evtPotion.Triggers.Add(new MacroTrigger
                {
                    Type = TriggerType.GaugeBar,
                    GaugeColor = GaugeColorType.RedHp,
                    ComparisonOp = ComparisonOperator.LessThan,
                    GaugeThresholdPercent = param2,
                    GaugeRoi = new RoiConfig { X = 100, Y = 50, Width = 200, Height = 20 }
                });
                evtPotion.Actions.Add(new MacroAction
                {
                    Type = ActionType.KeyPress,
                    Key = key2,
                    HoldTimeMs = 35,
                    PostDelayMs = 250,
                    Description = ko ? $"물약 키 [{key2}] 입력" : $"Press Potion Key [{key2}]"
                });
                profile.Events.Add(evtPotion);

                // 이벤트 2: 기본 공격 (순차 루프)
                var evtAttack = new MacroEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ko ? $"기본 사냥 공격 루프" : $"Routine Attack Loop",
                    StateTag = ko ? "사냥 중 (Hunting)" : "Hunting",
                    IsEnabled = true,
                    IsParallel = false
                };
                evtAttack.Triggers.Add(new MacroTrigger { Type = TriggerType.Always });
                evtAttack.Actions.Add(new MacroAction
                {
                    Type = ActionType.KeyPress,
                    Key = key1,
                    HoldTimeMs = 35,
                    PostDelayMs = param1,
                    Description = ko ? $"공격 키 [{key1}] 입력" : $"Press Attack Key [{key1}]"
                });
                profile.Events.Add(evtAttack);
                break;

            case 1: // 주기적 버프 스킬 유지
                profile.ProfileName = ko ? "주기적 버프 스킬 유지 프로필" : "Periodic Buff Skill Profile";
                profile.States.Add("전역 (Global)");
                profile.States.Add(ko ? "전투 모드" : "Combat");
                profile.InitialState = ko ? "전투 모드" : "Combat";

                // 버프 이벤트 (타이머 트리거 + 병렬)
                var evtBuff = new MacroEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ko ? $"{param1}초마다 버프 갱신" : $"Refresh Buff every {param1}s",
                    StateTag = ko ? "전투 모드" : "Combat",
                    IsEnabled = true,
                    IsParallel = true
                };
                evtBuff.Triggers.Add(new MacroTrigger
                {
                    Type = TriggerType.TimerInterval,
                    IntervalMs = param1 * 1000
                });
                evtBuff.Actions.Add(new MacroAction
                {
                    Type = ActionType.KeyPress,
                    Key = key2,
                    HoldTimeMs = 40,
                    PostDelayMs = 300,
                    Description = ko ? $"버프 스킬 [{key2}] 시전" : $"Cast Buff [{key2}]"
                });
                profile.Events.Add(evtBuff);

                // 일반 액션
                var evtCombat = new MacroEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ko ? "기본 전투/이동 루프" : "Basic Combat Loop",
                    StateTag = ko ? "전투 모드" : "Combat",
                    IsEnabled = true,
                    IsParallel = false
                };
                evtCombat.Triggers.Add(new MacroTrigger { Type = TriggerType.Always });
                evtCombat.Actions.Add(new MacroAction
                {
                    Type = ActionType.KeyPress,
                    Key = key1,
                    HoldTimeMs = 35,
                    PostDelayMs = param2,
                    Description = ko ? $"주 동작 [{key1}] 실행" : $"Execute [{key1}]"
                });
                profile.Events.Add(evtCombat);
                break;

            case 2: // 반응형 자동 낚시
                profile.ProfileName = ko ? "반응형 자동 낚시 프로필" : "Auto Fishing Profile";
                profile.States.Add("전역 (Global)");
                profile.States.Add(ko ? "낚시 중" : "Fishing");
                profile.InitialState = ko ? "낚시 중" : "Fishing";

                var evtFish = new MacroEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ko ? "입질 반응 챔질 & 재투척" : "Hook & Recast on Alert",
                    StateTag = ko ? "낚시 중" : "Fishing",
                    IsEnabled = true,
                    IsParallel = false
                };
                evtFish.Triggers.Add(new MacroTrigger
                {
                    Type = TriggerType.ImageDetection,
                    ImageAccuracyPercent = 85.0
                });
                evtFish.Actions.Add(new MacroAction
                {
                    Type = ActionType.KeyPress,
                    Key = key2,
                    HoldTimeMs = 40,
                    PostDelayMs = param2,
                    Description = ko ? $"입질 감지! 챔질 [{key2}]" : $"Hook [{key2}]"
                });
                evtFish.Actions.Add(new MacroAction
                {
                    Type = ActionType.KeyPress,
                    Key = key1,
                    HoldTimeMs = 40,
                    PostDelayMs = 500,
                    Description = ko ? $"낚시대 재투척 [{key1}]" : $"Recast [{key1}]"
                });
                profile.Events.Add(evtFish);
                break;

            case 3: // 사냥터 ➔ 마을 왕복 FSM
                profile.ProfileName = ko ? "사냥터-마을 왕복 FSM 프로필" : "Hunting-Town Roundtrip FSM Profile";
                profile.States.Add("전역 (Global)");
                profile.States.Add(ko ? "사냥터 (Hunting)" : "Hunting");
                profile.States.Add(ko ? "마을 (Town)" : "Town");
                profile.InitialState = ko ? "사냥터 (Hunting)" : "Hunting";

                // 사냥터 상태: 위험 시 마을 귀환
                var evtDanger = new MacroEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ko ? $"체력 {param2}% 미만 시 마을 귀환" : $"Return Town on HP < {param2}%",
                    StateTag = ko ? "사냥터 (Hunting)" : "Hunting",
                    IsEnabled = true
                };
                evtDanger.Triggers.Add(new MacroTrigger
                {
                    Type = TriggerType.GaugeBar,
                    GaugeColor = GaugeColorType.RedHp,
                    ComparisonOp = ComparisonOperator.LessThan,
                    GaugeThresholdPercent = param2,
                    GaugeRoi = new RoiConfig { X = 100, Y = 50, Width = 200, Height = 20 }
                });
                evtDanger.Actions.Add(new MacroAction
                {
                    Type = ActionType.KeyPress,
                    Key = key2,
                    HoldTimeMs = 40,
                    PostDelayMs = 1000,
                    Description = ko ? $"귀환 스킬 [{key2}] 사용" : $"Use Return [{key2}]"
                });
                evtDanger.Actions.Add(new MacroAction
                {
                    Type = ActionType.ChangeState,
                    TargetState = ko ? "마을 (Town)" : "Town",
                    Description = ko ? "상태 전이 ➔ [마을]" : "Change State ➔ [Town]"
                });
                profile.Events.Add(evtDanger);

                // 사냥터 상태: 기본 사냥
                var evtHunt = new MacroEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ko ? "몬스터 사냥 루프" : "Monster Hunting Loop",
                    StateTag = ko ? "사냥터 (Hunting)" : "Hunting",
                    IsEnabled = true
                };
                evtHunt.Triggers.Add(new MacroTrigger { Type = TriggerType.Always });
                evtHunt.Actions.Add(new MacroAction
                {
                    Type = ActionType.KeyPress,
                    Key = key1,
                    HoldTimeMs = 35,
                    PostDelayMs = 350,
                    Description = ko ? $"공격 [{key1}]" : $"Attack [{key1}]"
                });
                profile.Events.Add(evtHunt);

                // 마을 상태: 체류 후 사냥터 재출발
                var evtTown = new MacroEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ko ? $"{param1}초 휴식 후 사냥터 재출발" : $"Rest {param1}s and Depart",
                    StateTag = ko ? "마을 (Town)" : "Town",
                    IsEnabled = true
                };
                evtTown.Triggers.Add(new MacroTrigger
                {
                    Type = TriggerType.TimerInterval,
                    IntervalMs = param1 * 1000
                });
                evtTown.Actions.Add(new MacroAction
                {
                    Type = ActionType.ChangeState,
                    TargetState = ko ? "사냥터 (Hunting)" : "Hunting",
                    Description = ko ? "상태 전이 ➔ [사냥터]" : "Change State ➔ [Hunting]"
                });
                profile.Events.Add(evtTown);
                break;

            case 4: // 단순 반복 루프
                profile.ProfileName = ko ? "단순 반복 키 입력 프로필" : "Simple Repetitive Keystroke Profile";
                profile.States.Add("전역 (Global)");
                profile.States.Add(ko ? "반복 실행" : "Repeating");
                profile.InitialState = ko ? "반복 실행" : "Repeating";

                var evtLoop = new MacroEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = ko ? $"키 [{key1}] 반복 입력" : $"Repeat Key [{key1}]",
                    StateTag = ko ? "반복 실행" : "Repeating",
                    IsEnabled = true
                };
                evtLoop.Triggers.Add(new MacroTrigger { Type = TriggerType.Always });
                evtLoop.Actions.Add(new MacroAction
                {
                    Type = ActionType.KeyPress,
                    Key = key1,
                    HoldTimeMs = param2,
                    PostDelayMs = param1,
                    Description = ko ? $"[{key1}] 입력 ({param1}ms 주기)" : $"Press [{key1}]"
                });
                profile.Events.Add(evtLoop);
                break;
        }

        return profile;
    }

    private Control CreateConceptRow(string title, string desc)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 4),
            Padding = new Padding(10, 6, 10, 6)
        };
        var lblT = new Label
        {
            Text = title,
            Font = AppFontHelper.GetEmojiFont(10.5f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 22
        };
        var lblD = new Label
        {
            Text = desc,
            Font = AppFontHelper.GetDefaultFont(9f),
            ForeColor = Color.Gray,
            Dock = DockStyle.Fill
        };
        pnl.Controls.Add(lblD);
        pnl.Controls.Add(lblT);
        return pnl;
    }

    private void ApplyTheme()
    {
        bool isDark = AppSettings.Current.IsDarkMode;
        AppThemeHelper.ApplyTheme(this, isDark);

        BackColor = isDark ? Color.FromArgb(18, 24, 38) : Color.FromArgb(248, 250, 252);
        ForeColor = isDark ? Color.White : Color.FromArgb(30, 30, 30);
        _pnlHeader.BackColor = isDark ? Color.FromArgb(24, 32, 48) : Color.FromArgb(235, 240, 248);
        _pnlFooter.BackColor = isDark ? Color.FromArgb(24, 32, 48) : Color.FromArgb(235, 240, 248);

        _btnPrev.BackColor = isDark ? Color.FromArgb(35, 45, 65) : Color.FromArgb(220, 225, 235);
        _btnPrev.ForeColor = isDark ? Color.White : Color.Black;
        _btnNext.BackColor = isDark ? Color.FromArgb(41, 128, 185) : Color.FromArgb(52, 152, 219);
        _btnNext.ForeColor = Color.White;
    }
}
