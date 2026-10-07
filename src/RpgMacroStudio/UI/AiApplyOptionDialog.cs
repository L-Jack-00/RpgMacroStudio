using System;
using System.Drawing;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

public enum AiApplyChoice
{
    Cancel,
    Merge,
    Replace
}

/// <summary>
/// AI 어시스턴트 프로필 적용 시 기존 작업물 보존을 위해
/// '기존 캔버스에 병합(Merge)' vs '현재 프로필 덮어쓰기(Replace)'를 선택하는 안내 대화상자입니다.
/// </summary>
public class AiApplyOptionDialog : Form
{
    public AiApplyChoice Choice { get; private set; } = AiApplyChoice.Cancel;

    private readonly Label _lblTitle;
    private readonly Label _lblDesc;
    private readonly Button _btnMerge;
    private readonly Button _btnReplace;
    private readonly Button _btnCancel;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AppThemeHelper.UpdateFormTitleBarTheme(this, AppSettings.Current.IsDarkMode);
    }

    public AiApplyOptionDialog(int existingEventCount, int existingStateCount)
    {
        bool ko = Localization.IsKorean;

        Text = ko ? "AI 매크로 적용 방식 선택" : "Apply AI Macro Profile";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(520, 310);
        Font = AppFontHelper.GetDefaultFont(9f);

        bool isDark = AppSettings.Current.IsDarkMode;
        BackColor = isDark ? Color.FromArgb(24, 28, 38) : Color.FromArgb(246, 248, 250);
        ForeColor = isDark ? Color.White : Color.FromArgb(30, 30, 30);

        _lblTitle = new Label
        {
            Text = ko ? "✨ AI가 설계한 매크로를 어떻게 적용하시겠습니까?" : "✨ How would you like to apply the AI Macro?",
            Font = AppFontHelper.GetDefaultFont(10.5f, FontStyle.Bold),
            ForeColor = isDark ? Color.FromArgb(230, 235, 245) : Color.FromArgb(20, 30, 45),
            Location = new Point(24, 18),
            Size = new Size(460, 26),
            TextAlign = ContentAlignment.MiddleLeft
        };

        string descText = ko
            ? $"현재 캔버스에 기존 작업물(상태 {existingStateCount}개, 이벤트 {existingEventCount}개)이 있습니다.\n기존 매크로를 안전하게 보존하거나 새로 시작할 수 있습니다."
            : $"Current canvas has existing work ({existingStateCount} states, {existingEventCount} events).\nYou can safely preserve existing work or start fresh.";

        _lblDesc = new Label
        {
            Text = descText,
            Font = AppFontHelper.GetDefaultFont(8.5f),
            ForeColor = isDark ? Color.FromArgb(160, 170, 185) : Color.FromArgb(90, 100, 115),
            Location = new Point(24, 46),
            Size = new Size(460, 36)
        };

        // 1. 병합(Merge) 버튼 (추천/기본)
        _btnMerge = new Button
        {
            Location = new Point(24, 94),
            Size = new Size(456, 58),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            BackColor = isDark ? Color.FromArgb(20, 50, 40) : Color.FromArgb(230, 248, 240),
            ForeColor = isDark ? Color.FromArgb(120, 220, 160) : Color.FromArgb(20, 120, 70),
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold),
            Text = ko
                ? "➕ 기존 캔버스에 병합 (추천 - 새 상태로 추가)\n기존 작업물을 그대로 유지하고, AI가 설계한 상태와 이벤트를 추가합니다."
                : "➕ Merge into Canvas (Recommended - Add as new states)\nKeep existing work and add AI designed states and events."
        };
        _btnMerge.FlatAppearance.BorderSize = 1;
        _btnMerge.FlatAppearance.BorderColor = Color.FromArgb(46, 204, 113);
        _btnMerge.Click += (s, e) =>
        {
            Choice = AiApplyChoice.Merge;
            DialogResult = DialogResult.OK;
            Close();
        };

        // 2. 덮어쓰기(Replace) 버튼
        _btnReplace = new Button
        {
            Location = new Point(24, 160),
            Size = new Size(456, 58),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            BackColor = isDark ? Color.FromArgb(45, 30, 35) : Color.FromArgb(253, 238, 240),
            ForeColor = isDark ? Color.FromArgb(235, 140, 150) : Color.FromArgb(160, 40, 50),
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold),
            Text = ko
                ? "🔄 현재 프로필 덮어쓰기 (새로 시작)\n현재 캔버스의 모든 내용을 비우고 AI 프로필로 완전히 교체합니다."
                : "🔄 Replace Current Profile (Start fresh)\nClear current canvas and replace completely with the AI profile."
        };
        _btnReplace.FlatAppearance.BorderSize = 1;
        _btnReplace.FlatAppearance.BorderColor = isDark ? Color.FromArgb(180, 80, 90) : Color.FromArgb(220, 100, 110);
        _btnReplace.Click += (s, e) =>
        {
            Choice = AiApplyChoice.Replace;
            DialogResult = DialogResult.OK;
            Close();
        };

        // 3. 취소 버튼
        _btnCancel = new Button
        {
            Location = new Point(370, 228),
            Size = new Size(110, 32),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Text = Localization.Get("BtnCommonCancel"),
            DialogResult = DialogResult.Cancel
        };
        _btnCancel.Click += (s, e) =>
        {
            Choice = AiApplyChoice.Cancel;
            DialogResult = DialogResult.Cancel;
            Close();
        };

        CancelButton = _btnCancel;

        Controls.AddRange(new Control[]
        {
            _lblTitle, _lblDesc,
            _btnMerge, _btnReplace,
            _btnCancel
        });

        AppThemeHelper.ApplyTheme(this, isDark);
    }
}
