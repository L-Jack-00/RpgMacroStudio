using System;
using System.Drawing;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 최초 실행 온보딩 및 사용 가이드를 제공하는 4단계 Get Started 대화상자입니다.
/// </summary>
public class GetStartedDialog : Form
{
    private int _currentPage = 0;
    private const int TotalPages = 4;

    private readonly Panel _pnlHeader;
    private readonly Label _lblTitle;
    private readonly Label _lblSubtitle;
    private readonly Panel _pnlBody;
    private readonly Panel _pnlFooter;
    private readonly Button _btnPrev;
    private readonly Button _btnNext;
    private readonly Label _lblPageIndicator;

    public bool OpenHelperWizardRequested { get; private set; } = false;

    public GetStartedDialog()
    {
        bool ko = Localization.IsKorean;

        Text = ko ? "RPG 매크로 스튜디오 - 시작 가이드" : "RPG Macro Studio - Get Started Guide";
        Width = 740;
        Height = 630;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = AppFontHelper.GetEmojiFont(9.5f);

        // 상단 헤더
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

        // 하단 푸터 (이전 / 다음 / 페이지 표시)
        _pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            Padding = new Padding(20, 12, 20, 12)
        };

        _btnPrev = new Button
        {
            Text = ko ? "◀ 이전" : "◀ Previous",
            Width = 95,
            Height = 34,
            Location = new Point(20, 13),
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetDefaultFont(9.5f)
        };
        _btnPrev.Click += (s, e) => Navigate(-1);

        _lblPageIndicator = new Label
        {
            AutoSize = false,
            Width = 100,
            Height = 34,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(310, 13),
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold),
            ForeColor = Color.Gray
        };

        _btnNext = new Button
        {
            Text = ko ? "다음 ▶" : "Next ▶",
            Width = 95,
            Height = 34,
            Location = new Point(590, 13),
            FlatStyle = FlatStyle.Flat,
            Font = AppFontHelper.GetDefaultFont(9.5f)
        };
        _btnNext.Click += (s, e) => Navigate(1);

        _pnlFooter.Controls.AddRange(new Control[] { _btnPrev, _lblPageIndicator, _btnNext });

        // 본문 컨텐츠 패널
        _pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16)
        };

        Controls.Add(_pnlBody);
        Controls.Add(_pnlFooter);
        Controls.Add(_pnlHeader);

        RenderCurrentPage();
        ApplyTheme();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AppThemeHelper.UpdateFormTitleBarTheme(this, AppSettings.Current.IsDarkMode);
    }

    private void Navigate(int delta)
    {
        int newPage = _currentPage + delta;
        if (newPage >= 0 && newPage < TotalPages)
        {
            _currentPage = newPage;
            RenderCurrentPage();
            ApplyTheme();
        }
    }

    private void RenderCurrentPage()
    {
        bool ko = Localization.IsKorean;
        _pnlBody.Controls.Clear();
        _lblPageIndicator.Text = $"{_currentPage + 1} / {TotalPages}";
        _btnPrev.Enabled = (_currentPage > 0);
        _btnNext.Visible = (_currentPage < TotalPages - 1);

        switch (_currentPage)
        {
            case 0:
                RenderPage1_Welcome(ko);
                break;
            case 1:
                RenderPage2_FourViews(ko);
                break;
            case 2:
                RenderPage3_Hotkeys(ko);
                break;
            case 3:
                RenderPage4_ReadyAndWizard(ko);
                break;
        }
    }

    private void RenderPage1_Welcome(bool ko)
    {
        _lblTitle.Text = ko ? "👋 환영합니다! RPG 매크로 스튜디오" : "👋 Welcome to RPG Macro Studio";
        _lblSubtitle.Text = ko ? "스마트 FSM 시각화 노드 엔진으로 설계하는 전문가용 매크로" : "Design macros visually with smart FSM node engine";

        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(10)
        };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        container.Controls.Add(CreateFeatureCard(
            "🛡️ " + (ko ? "안전한 표준 입력 & 비전 감지" : "Safe Standard Input & Vision"),
            ko 
                ? "프로그램 메모리 변조나 시스템 후킹을 일체 사용하지 않습니다. 인간과 100% 동일한 Windows 표준 입력(키/마우스)과 비전 색상/이미지 감지만을 사용하여 안전합니다."
                : "Zero memory hooking or tampering. Operates purely through standard Windows input and OpenCV vision detection like a human player."
        ), 0, 0);

        container.Controls.Add(CreateFeatureCard(
            "🧩 " + (ko ? "FSM(유한 상태 머신) 아키텍처" : "FSM (Finite State Machine) Logic"),
            ko
                ? "단순 무한 반복 매크로가 아닙니다! 캐릭터의 상황(사냥터, 마을, 사망, 휴식)에 따라 최적의 규칙만 돌아가는 뇌(상태) 분리형 스마트 구조를 지원합니다."
                : "Not just a dumb infinite loop. Divides logic into dedicated states (Hunting, Town, Resting) that only trigger relevant rules per situation."
        ), 0, 1);

        container.Controls.Add(CreateFeatureCard(
            "⚡ " + (ko ? "강력한 병렬 멀티태스킹" : "High-Performance Parallel Execution"),
            ko
                ? "사냥 공격 키를 누르는 도중에도, 체력이 떨어지면 즉시 포션을 마시는 [병렬 이벤트] 멀티태스킹 파이프라인을 지원합니다."
                : "Supports parallel execution pipelines so potion drinking can instantly trigger without interrupting routine attack keystrokes."
        ), 0, 2);

        _pnlBody.Controls.Add(container);
    }

    private void RenderPage2_FourViews(bool ko)
    {
        _lblTitle.Text = ko ? "🎨 3가지 핵심 작업 뷰 모드 소개" : "🎨 3 Unified Workspace Views";
        _lblSubtitle.Text = ko ? "상단 툴바에서 내 작업 스타일에 맞는 뷰를 언제든 1초 만에 전환하세요" : "Switch seamlessly anytime from the top toolbar to suit your workflow";

        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(5)
        };
        for (int i = 0; i < 3; i++) container.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

        container.Controls.Add(CreateViewCard(
            "📦 " + (ko ? "FSM 카드 뷰" : "FSM Card View"),
            ko ? "상태별 카드 블록을 드래그하고 캔버스에 직관적으로 배치하는 대표 시각적 뷰입니다." : "Default visual card view for dragging and organizing state blocks."
        ), 0, 0);

        container.Controls.Add(CreateViewCard(
            "🔀 " + (ko ? "핀 그래프 뷰 (ComfyUI / 블루프린트 스타일)" : "Pin Graph View (ComfyUI Style)"),
            ko ? "조건과 행동 노드를 베지어 곡선 와이어(선)로 자유자재로 연결하는 최신 노드 그래프 뷰입니다." : "Modern node graph with bezier wires connecting condition and action pins."
        ), 0, 1);

        container.Controls.Add(CreateViewCard(
            "☰ " + (ko ? "클래식 계층 표 뷰 (트리-테이블 통합 뷰)" : "Classic Hierarchical Table View"),
            ko ? "표(Table)의 정돈된 가독성과 트리(Tree)의 계층 구조를 결합하여 상태-이벤트-조건-행동을 한눈에 관리합니다." : "Combines table columns and tree hierarchy to inspect States, Events, Triggers, and Actions."
        ), 0, 2);

        _pnlBody.Controls.Add(container);
    }

    private void RenderPage3_Hotkeys(bool ko)
    {
        _lblTitle.Text = ko ? "⌨️ 기본 조작법 및 필수 단축키" : "⌨️ Essential Controls & Shortcuts";
        _lblSubtitle.Text = ko ? "손가락 기억만으로 모든 뷰에서 100% 동일하게 동작하는 조작 체계" : "Consistent universal shortcuts that work identically across all 3 views";

        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };

        var lblHeader1 = new Label
        {
            Text = ko ? "🔴 매크로 실행 및 안전 제어 (전역 단축키)" : "🔴 Macro Execution & Safety (Global Hotkeys)",
            Font = AppFontHelper.GetEmojiFont(10f, FontStyle.Bold),
            AutoSize = true,
            Top = 5,
            Left = 10
        };
        pnl.Controls.Add(lblHeader1);

        int top = 30;
        string recKey = string.IsNullOrEmpty(AppSettings.Current.RecordHotkey) ? "F9" : AppSettings.Current.RecordHotkey;
        string startKey = string.IsNullOrEmpty(AppSettings.Current.StartPauseHotkey) ? "F10" : AppSettings.Current.StartPauseHotkey;
        string stopKey = string.IsNullOrEmpty(AppSettings.Current.StopHotkey) ? "F11" : AppSettings.Current.StopHotkey;
        string emgKey = string.IsNullOrEmpty(AppSettings.Current.EmergencyStopHotkey) ? "F12" : AppSettings.Current.EmergencyStopHotkey;

        top = AddHotkeyRow(pnl, recKey, ko ? "키/마우스 입력 실시간 녹화 시작 / 중지" : "Start / Stop Live Macro Recording", top, ko);
        top = AddHotkeyRow(pnl, startKey, ko ? "매크로 시작 / 일시정지 (화면 감시 및 루프 가동)" : "Start / Pause Macro Loop", top, ko);
        top = AddHotkeyRow(pnl, stopKey, ko ? "매크로 완전 정지 (초기 대기 상태로 리셋)" : "Stop Macro Completely (Reset to Initial)", top, ko);
        top = AddHotkeyRow(pnl, emgKey, ko ? "🚨 긴급 비상 정지 (즉시 모든 입력 하드웨어 차단 및 엔진 동결)" : "🚨 Emergency Stop (Instantly freezes all inputs)", top, ko);

        top += 10;
        var lblHeader2 = new Label
        {
            Text = ko ? "🖱️ 3대 뷰 공통 편집 단축키" : "🖱️ Universal Editing Shortcuts",
            Font = AppFontHelper.GetEmojiFont(10f, FontStyle.Bold),
            AutoSize = true,
            Top = top,
            Left = 10
        };
        pnl.Controls.Add(lblHeader2);
        top += 25;

        top = AddHotkeyRow(pnl, "Space", ko ? "선택된 이벤트의 활성 / 비활성(On/Off) 즉시 토글" : "Toggle Event Enabled / Disabled", top, ko);
        top = AddHotkeyRow(pnl, "F2 / Enter", ko ? "선택된 노드 / 이벤트 속성 상세 편집창 열기" : "Edit Selected Node or Event", top, ko);
        top = AddHotkeyRow(pnl, "Delete", ko ? "선택된 상태 / 이벤트 / 조건 / 행동 / 와이어 삭제" : "Delete Selected Item", top, ko);
        top = AddHotkeyRow(pnl, "Esc", ko ? "모든 선택 해제 및 스포이드 화면 캡처 취소" : "Deselect All / Cancel Picker", top, ko);
        top = AddHotkeyRow(pnl, "Ctrl + Z / Y", ko ? "실행 취소(Undo) / 다시 실행(Redo)" : "Undo / Redo History", top, ko);
        top = AddHotkeyRow(pnl, ko ? "휠 / 우클릭" : "Wheel / Right", ko ? "캔버스 화면 확대·축소(Zoom) 및 화면 이동(Pan)" : "Canvas Zoom (Wheel) & Pan (Right Drag)", top, ko);

        _pnlBody.Controls.Add(pnl);
    }

    private void RenderPage4_ReadyAndWizard(bool ko)
    {
        _lblTitle.Text = ko ? "🚀 이제 시작할 준비가 되었습니다!" : "🚀 You Are Ready to Begin!";
        _lblSubtitle.Text = ko ? "아래 버튼을 선택하여 원하는 방식으로 매크로 제작을 시작하세요" : "Choose how you would like to get started below";

        var pnl = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(24, 16, 24, 16)
        };

        var lblGuide = new Label
        {
            Text = ko
                ? "노드 개념이나 복잡한 설정이 아직 낯설게 느껴지시나요?\n" +
                  "자주 쓰이는 [기본 사냥 & 물약], [주기적 버프], [자동 낚시] 등 5대 실전 매크로를\n" +
                  "빈칸 몇 개만 채워 30초 만에 만들어주는 [도우미 마법사]를 먼저 체험해 보세요!"
                : "Feeling new to nodes and FSM logic?\n" +
                  "Try our [Helper Wizard] to generate 5 popular ready-to-use templates\n" +
                  "(Auto Hunting, Buffs, Fishing) in just 30 seconds!",
            Font = AppFontHelper.GetDefaultFont(10f),
            Height = 75,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.TopCenter
        };
        pnl.Controls.Add(lblGuide);

        var btnContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 150,
            Padding = new Padding(30, 8, 30, 8)
        };

        // 도우미 마법사 버튼 (강조)
        var btnWizard = new Button
        {
            Text = ko ? "✨ 도우미 마법사로 시작하기 (추천)\n30초 만에 실전 템플릿 프로필 완성" : "✨ Start with Helper Wizard (Recommended)\nCreate ready-to-use templates in 30s",
            Font = AppFontHelper.GetEmojiFont(10.5f, FontStyle.Bold),
            Height = 68,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(41, 128, 185),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Tag = "PrimaryBlueButton"
        };
        btnWizard.FlatAppearance.BorderSize = 0;
        btnWizard.Click += (s, e) =>
        {
            CompleteGetStarted();
            OpenHelperWizardRequested = true;
            DialogResult = DialogResult.OK;
            Close();
        };

        var spacer = new Panel { Dock = DockStyle.Top, Height = 14 };

        // 바로 Studio 시작하기 버튼
        var btnStudio = new Button
        {
            Text = ko ? "🚀 Studio 바로 시작하기 (빈 캔버스에서 직접 작업)" : "🚀 Launch Studio Directly",
            Font = AppFontHelper.GetEmojiFont(10f),
            Height = 50,
            Dock = DockStyle.Top,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnStudio.Click += (s, e) =>
        {
            CompleteGetStarted();
            OpenHelperWizardRequested = false;
            DialogResult = DialogResult.OK;
            Close();
        };

        // WinForms Top 도킹 순서: Studio -> Spacer -> Wizard 순으로 Add하여 위에서부터 Wizard -> Spacer -> Studio로 배치
        btnContainer.Controls.Add(btnStudio);
        btnContainer.Controls.Add(spacer);
        btnContainer.Controls.Add(btnWizard);
        pnl.Controls.Add(btnContainer);

        _pnlBody.Controls.Add(pnl);
    }

    private void CompleteGetStarted()
    {
        AppSettings.Current.HasCompletedGetStarted = true;
        AppSettings.Save();
    }

    private Control CreateFeatureCard(string title, string desc)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(12)
        };
        var lblT = new Label
        {
            Text = title,
            Font = AppFontHelper.GetEmojiFont(10.5f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 24
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

    private Control CreateViewCard(string title, string desc)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(10, 8, 10, 8)
        };
        var lblT = new Label
        {
            Text = title,
            Font = AppFontHelper.GetEmojiFont(10f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 22
        };
        var lblD = new Label
        {
            Text = desc,
            Font = AppFontHelper.GetDefaultFont(8.8f),
            ForeColor = Color.Gray,
            Dock = DockStyle.Fill
        };
        pnl.Controls.Add(lblD);
        pnl.Controls.Add(lblT);
        return pnl;
    }

    private int AddHotkeyRow(Panel parent, string keyText, string desc, int top, bool ko)
    {
        var badge = new Label
        {
            Text = keyText,
            Font = AppFontHelper.GetDefaultFont(9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Width = 90,
            Height = 26,
            Top = top,
            Left = 14,
            Tag = "KeycapBadge"
        };
        // 라이트 모드/다크 모드 모두에서 단축키 텍스트가 100% 또렷하게 보이도록 커스텀 키캡 렌더링
        badge.Paint += (s, e) =>
        {
            bool isDark = AppSettings.Current.IsDarkMode;
            var g = e.Graphics;
            var rect = new Rectangle(0, 0, badge.Width - 1, badge.Height - 1);
            Color bg = isDark ? Color.FromArgb(44, 62, 80) : Color.FromArgb(230, 236, 245);
            Color border = isDark ? Color.FromArgb(70, 95, 125) : Color.FromArgb(180, 195, 215);
            Color fg = isDark ? Color.White : Color.FromArgb(24, 38, 58);

            using var brush = new SolidBrush(bg);
            using var pen = new Pen(border);
            using var textBrush = new SolidBrush(fg);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            g.FillRectangle(brush, rect);
            g.DrawRectangle(pen, rect);
            g.DrawString(badge.Text, badge.Font, textBrush, rect, sf);
        };

        var descLbl = new Label
        {
            Text = desc,
            Font = AppFontHelper.GetDefaultFont(9f),
            Top = top + 4,
            Left = 115,
            AutoSize = true
        };
        parent.Controls.Add(badge);
        parent.Controls.Add(descLbl);
        return top + 34;
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
