using System.Drawing;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 노드 뷰 또는 클래식 뷰에서 이벤트를 더블클릭했을 때 나타나는 이벤트 상세 속성(이름, 병렬 처리, 인간 반응 지연, 소속 상태) 편집 팝업 대화상자입니다.
/// 라이트 모드 및 다크 모드가 완벽히 적용되며 모든 버튼 텍스트의 줄바꿈이 제거됩니다.
/// </summary>
public class EventEditDialog : Form
{
    private readonly MacroEvent _targetEvent;
    private readonly List<string> _availableStates;

    private TextBox _txtEventName = null!;
    private CustomCheckBox _chkEnabled = null!;
    private CustomCheckBox _chkParallel = null!;
    private TrackBar _sliderHumanizer = null!;
    private NumericUpDown _numHumanizer = null!;
    private Label _lblHumanizerValue = null!;
    private CustomComboBox _cboStateTag = null!;

    public EventEditDialog(MacroEvent targetEvent, IEnumerable<string> availableStates)
    {
        _targetEvent = targetEvent;
        _availableStates = availableStates.ToList();

        InitializeUi();
        LoadEventData();
        AppThemeHelper.ApplyTheme(this, AppSettings.Current.IsDarkMode);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AppThemeHelper.UpdateFormTitleBarTheme(this, AppSettings.Current.IsDarkMode);
    }

    private void InitializeUi()
    {
        bool ko = Localization.IsKorean;
        string evtDisplayName = MacroEventProfile.FormatEventDisplay(_targetEvent, ko);
        Text = ko ? $"⚡ 이벤트 설정: {evtDisplayName}" : $"⚡ Event Settings: {evtDisplayName}";
        Size = new Size(490, 420);
        MinimumSize = new Size(490, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = AppFontHelper.GetEmojiFont(9.5f);

        // 1. 이벤트 이름
        var lblName = new Label
        {
            Text = ko ? "이벤트 이름:" : "Event Name:",
            Left = 20,
            Top = 22,
            Width = 110,
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold)
        };
        _txtEventName = new TextBox
        {
            Left = 140,
            Top = 18,
            Width = 300,
            Font = AppFontHelper.GetDefaultFont(9.5f)
        };

        // 2. 소속 상태
        var lblState = new Label
        {
            Text = ko ? "소속 상태:" : "State:",
            Left = 20,
            Top = 60,
            Width = 110,
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold)
        };
        _cboStateTag = new CustomComboBox
        {
            Left = 140,
            Top = 56,
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = AppFontHelper.GetEmojiFont(9f)
        };
        foreach (var st in _availableStates)
        {
            _cboStateTag.Items.Add(st);
        }

        // 3. 이벤트 활성화 (On/Off)
        _chkEnabled = new CustomCheckBox
        {
            Text = ko ? "✔ 이벤트 활성화 (실행 감지 및 동작 On/Off)" : "✔ Event Enabled (Active in Loop)",
            Left = 20,
            Top = 96,
            Width = 420,
            Font = AppFontHelper.GetEmojiFont(9.5f, FontStyle.Bold)
        };

        // 4. 비동기 동시 실행 (병렬)
        _chkParallel = new CustomCheckBox
        {
            Text = ko ? "⚡ 다른 이벤트와 비동기 동시 실행 (병렬 처리)" : "⚡ Run Concurrently (Parallel Execution)",
            Left = 20,
            Top = 126,
            Width = 420,
            Font = AppFontHelper.GetEmojiFont(9.5f)
        };

        // 5. 인간 반응 속도 지연 (Humanizer)
        var grpHumanizer = new GroupBox
        {
            Text = ko ? "🎲 인간 반응 속도 무작위 지연" : "🎲 Humanizer Jitter Delay",
            Left = 20,
            Top = 165,
            Width = 430,
            Height = 125,
            Font = AppFontHelper.GetEmojiFont(9f, FontStyle.Bold)
        };

        _sliderHumanizer = new TrackBar
        {
            Left = 15,
            Top = 26,
            Width = 240,
            Minimum = 0,
            Maximum = 54,
            Value = 27,
            TickFrequency = 5
        };
        _numHumanizer = new NumericUpDown
        {
            Left = 265,
            Top = 28,
            Width = 60,
            Minimum = 0,
            Maximum = 54,
            Value = 27,
            Font = AppFontHelper.GetDefaultFont(9f)
        };
        _lblHumanizerValue = new Label
        {
            Text = ko ? "±27ms 지연" : "±27ms delay",
            Left = 330,
            Top = 30,
            AutoSize = true,
            Font = AppFontHelper.GetDefaultFont(9f)
        };

        _sliderHumanizer.Scroll += (s, e) =>
        {
            _numHumanizer.Value = _sliderHumanizer.Value;
            _lblHumanizerValue.Text = ko ? $"±{_sliderHumanizer.Value}ms 지연" : $"±{_sliderHumanizer.Value}ms delay";
        };
        _numHumanizer.ValueChanged += (s, e) =>
        {
            _sliderHumanizer.Value = (int)_numHumanizer.Value;
            _lblHumanizerValue.Text = ko ? $"±{_sliderHumanizer.Value}ms 지연" : $"±{_sliderHumanizer.Value}ms delay";
        };

        var lblHint = new Label
        {
            Text = ko ? "※ 행동 사이사이에 0~54ms 범위의 무작위 미세 지연을 삽입하여 탐지를 방지합니다." : "※ Adds randomized jitter between actions to mimic natural human input.",
            Left = 15,
            Top = 78,
            Width = 400,
            Height = 36,
            ForeColor = Color.DarkGray,
            Font = AppFontHelper.GetDefaultFont(8.2f)
        };
        grpHumanizer.Controls.AddRange(new Control[] { _sliderHumanizer, _numHumanizer, _lblHumanizerValue, lblHint });

        // 6. 확인 / 취소 버튼
        var btnSave = new Button
        {
            Text = ko ? "확인" : "OK",
            DialogResult = DialogResult.OK,
            Left = 250,
            Top = 310,
            Width = 95,
            Height = 34,
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            Font = AppFontHelper.GetDefaultFont(9.5f, FontStyle.Bold),
            AutoEllipsis = true
        };
        btnSave.Click += (s, e) => SaveEventData();

        var btnCancel = new Button
        {
            Text = ko ? "취소" : "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 355,
            Top = 310,
            Width = 95,
            Height = 34,
            Font = AppFontHelper.GetDefaultFont(9.5f),
            AutoEllipsis = true
        };

        Controls.AddRange(new Control[]
        {
            lblName, _txtEventName,
            lblState, _cboStateTag,
            _chkEnabled,
            _chkParallel,
            grpHumanizer,
            btnSave, btnCancel
        });

        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private void LoadEventData()
    {
        _txtEventName.Text = MacroEventProfile.FormatEventDisplay(_targetEvent, Localization.IsKorean);
        _chkEnabled.Checked = _targetEvent.IsEnabled;
        _chkParallel.Checked = _targetEvent.IsParallel;
        int jitter = Math.Clamp(_targetEvent.HumanizerJitterMs, 0, 54);
        _sliderHumanizer.Value = jitter;
        _numHumanizer.Value = jitter;
        _lblHumanizerValue.Text = Localization.IsKorean ? $"±{jitter}ms 지연" : $"±{jitter}ms delay";

        if (!string.IsNullOrEmpty(_targetEvent.StateTag) && _cboStateTag.Items.Contains(_targetEvent.StateTag))
        {
            _cboStateTag.SelectedItem = _targetEvent.StateTag;
        }
        else if (_cboStateTag.Items.Count > 0)
        {
            _cboStateTag.SelectedIndex = 0;
        }
    }

    private void SaveEventData()
    {
        string newName = _txtEventName.Text.Trim();
        if (!string.IsNullOrEmpty(newName))
        {
            _targetEvent.Name = newName;
        }
        _targetEvent.IsEnabled = _chkEnabled.Checked;
        _targetEvent.IsParallel = _chkParallel.Checked;
        _targetEvent.HumanizerJitterMs = (int)_numHumanizer.Value;
        if (_cboStateTag.SelectedItem is string st && !string.IsNullOrEmpty(st))
        {
            _targetEvent.StateTag = st;
        }
    }
}
