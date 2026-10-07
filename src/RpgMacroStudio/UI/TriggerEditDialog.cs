using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RpgMacroHarness.Data;
using RpgMacroHarness.Vision;

namespace RpgMacroHarness.UI;

/// <summary>
/// 단일 발동 조건을 편집하는 다이얼로그 (통합 비전 감지, 타이머, 변수 비교, 상시)
/// 색상/이미지 일치 및 불일치, 좌우 반전 감지까지 완벽하게 지원합니다.
/// </summary>
public class TriggerEditDialog : Form
{
    public MacroTrigger TriggerData { get; private set; }

    private CustomComboBox _cboType = null!;

    // 통합 비전 감지 패널
    private Panel _pnlVision = null!;
    private CustomComboBox _cboVisionTarget = null!;
    private CustomComboBox _cboVisionMatchMode = null!;

    // 비전 - 색상 섹션
    private Panel _pnlColorSection = null!;
    private Button _btnPickColor = null!;
    private Panel _pnlColorSample = null!;
    private Label _lblColorHex = null!;
    private CustomComboBox _cboGaugeOp = null!;
    private NumericUpDown _numColorAccuracy = null!;
    private TrackBar _sliderColorAccuracy = null!;
    private NumericUpDown _numGaugeThresh = null!;
    private TrackBar _sliderGaugeThresh = null!;
    private Button _btnGaugeRoi = null!;
    private Label _lblGaugeRoi = null!;

    // 비전 - 이미지 섹션
    private Panel _pnlImageSection = null!;
    private TextBox _txtImagePath = null!;
    private Button _btnBrowseImg = null!;
    private PictureBox _picPreview = null!;
    private NumericUpDown _numImageAccuracy = null!;
    private TrackBar _sliderImageAccuracy = null!;
    private CustomCheckBox _chkAllowFlipHorizontal = null!;
    private Button _btnImgRoi = null!;
    private Label _lblImageRoi = null!;

    // 타이머 패널
    private Panel _pnlTimer = null!;
    private NumericUpDown _numIntervalMs = null!;

    // 변수 비교 패널
    private Panel _pnlVariable = null!;
    private TextBox _txtVarName = null!;
    private CustomComboBox _cboVarComp = null!;
    private NumericUpDown _numVarTarget = null!;

    // 상시 패널
    private Panel _pnlAlways = null!;

    public TriggerEditDialog(MacroTrigger? existing = null)
    {
        TriggerData = existing ?? new MacroTrigger();
        InitializeComponent();
        LoadTriggerData();
        AppThemeHelper.ApplyTheme(this, AppSettings.Current.IsDarkMode);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AppThemeHelper.UpdateFormTitleBarTheme(this, AppSettings.Current.IsDarkMode);
    }

    private void InitializeComponent()
    {
        bool ko = Localization.IsKorean;

        Text = ko ? "발동 조건 설정" : "Trigger Condition Settings";
        Size = new Size(540, 520);
        MinimumSize = new Size(540, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = AppFontHelper.GetEmojiFont(9.5f);

        var lblType = new Label { Text = ko ? "조건 유형:" : "Condition Type:", Left = 20, Top = 20, Width = 85 };
        _cboType = new CustomComboBox { Left = 110, Top = 17, Width = 385, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboType.Items.AddRange(new object[]
        {
            ko ? "⏱️ 주기적 타이머 실행" : "⏱️ Periodic Timer",
            ko ? "🎯 비전 감지 (색깔 / 이미지)" : "🎯 Vision Detection (Color / Image)",
            ko ? "🔢 변수/스위치 값 비교" : "🔢 Variable/Switch Compare",
            ko ? "🔄 무조건 상시 실행" : "🔄 Always Active"
        });
        _cboType.SelectedIndex = 0;
        _cboType.SelectedIndexChanged += (s, e) => UpdatePanels();

        // 1. 통합 비전 감지 패널
        _pnlVision = new Panel { Left = 20, Top = 58, Width = 485, Height = 370, Visible = false };

        var lblTarget = new Label { Text = ko ? "감지 대상:" : "Target:", Left = 0, Top = 6, Width = 75 };
        _cboVisionTarget = new CustomComboBox { Left = 80, Top = 3, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboVisionTarget.Items.AddRange(new object[]
        {
            ko ? "🎨 색상 (스포이드)" : "🎨 Color (Eyedropper)",
            ko ? "🖼️ 이미지 템플릿" : "🖼️ Image Template"
        });
        _cboVisionTarget.SelectedIndex = 0;
        _cboVisionTarget.SelectedIndexChanged += (s, e) => UpdateVisionSections();

        var lblMode = new Label { Text = ko ? "판정 방식:" : "Evaluation:", Left = 245, Top = 6, Width = 75 };
        _cboVisionMatchMode = new CustomComboBox { Left = 325, Top = 3, Width = 155, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboVisionMatchMode.Items.AddRange(new object[]
        {
            ko ? "✅ 일치 시 발동" : "✅ Trigger on Match",
            ko ? "❌ 불일치 시 발동" : "❌ Trigger on Mismatch"
        });
        _cboVisionMatchMode.SelectedIndex = 0;

        _pnlVision.Controls.AddRange(new Control[] { lblTarget, _cboVisionTarget, lblMode, _cboVisionMatchMode });

        // 1-A. 색상 상세 섹션
        _pnlColorSection = new Panel { Left = 0, Top = 38, Width = 485, Height = 325 };
        _btnPickColor = new Button { Text = ko ? "🎨 화면 색상 추출 (스포이드)" : "🎨 Pick Color", Left = 0, Top = 5, Width = 200, Height = 30 };
        _btnPickColor.Click += (s, e) => PickColor();

        _pnlColorSample = new Panel { Left = 210, Top = 6, Width = 28, Height = 28, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Red };
        _lblColorHex = new Label { Text = "#FF0000", Left = 245, Top = 12, Width = 85, Font = new Font("Consolas", 10, FontStyle.Bold) };

        var lblGOp = new Label { Text = ko ? "조건식:" : "Operator:", Left = 335, Top = 12, Width = 55 };
        _cboGaugeOp = new CustomComboBox { Left = 390, Top = 8, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboGaugeOp.Items.AddRange(new object[]
        {
            ko ? "< 미만" : "< Less",
            ko ? "<= 이하" : "<= Less/Eq",
            ko ? "> 초과" : "> Greater",
            ko ? ">= 이상" : ">= Greater/Eq"
        });
        _cboGaugeOp.SelectedIndex = 0;

        var lblColAcc = new Label { Text = ko ? "색상 정확도:" : "Color Acc:", Left = 0, Top = 50, AutoSize = true };
        _numColorAccuracy = new NumericUpDown { Left = 98, Top = 47, Width = 62, Minimum = 0, Maximum = 100, DecimalPlaces = 1, Value = 85 };
        _sliderColorAccuracy = new TrackBar { Left = 165, Top = 43, Width = 180, Minimum = 0, Maximum = 100, Value = 85, TickFrequency = 10 };
        _sliderColorAccuracy.Scroll += (s, e) => { _numColorAccuracy.Value = _sliderColorAccuracy.Value; };
        _numColorAccuracy.ValueChanged += (s, e) => { _sliderColorAccuracy.Value = Math.Clamp((int)_numColorAccuracy.Value, 0, 100); };

        var lblGThresh = new Label { Text = ko ? "기준 잔여량:" : "Threshold:", Left = 0, Top = 90, AutoSize = true };
        _numGaugeThresh = new NumericUpDown { Left = 98, Top = 87, Width = 62, Minimum = 0, Maximum = 100, DecimalPlaces = 1, Value = 40 };
        _sliderGaugeThresh = new TrackBar { Left = 165, Top = 83, Width = 180, Minimum = 0, Maximum = 100, Value = 40, TickFrequency = 10 };
        _sliderGaugeThresh.Scroll += (s, e) => { _numGaugeThresh.Value = _sliderGaugeThresh.Value; };
        _numGaugeThresh.ValueChanged += (s, e) => { _sliderGaugeThresh.Value = Math.Clamp((int)_numGaugeThresh.Value, 0, 100); };

        _btnGaugeRoi = new Button { Text = ko ? "🎯 감지 영역 지정" : "🎯 Select Region (ROI)", Left = 0, Top = 135, Width = 200, Height = 32 };
        _btnGaugeRoi.Click += (s, e) => SelectGaugeRoi();
        _lblGaugeRoi = new Label { Text = ko ? "영역: 지정 필요" : "ROI: Required", Left = 210, Top = 142, Width = 265, AutoEllipsis = true, ForeColor = Color.DarkSlateGray };

        _pnlColorSection.Controls.AddRange(new Control[]
        {
            _btnPickColor, _pnlColorSample, _lblColorHex, lblGOp, _cboGaugeOp,
            lblColAcc, _numColorAccuracy, _sliderColorAccuracy,
            lblGThresh, _numGaugeThresh, _sliderGaugeThresh,
            _btnGaugeRoi, _lblGaugeRoi
        });
        _pnlVision.Controls.Add(_pnlColorSection);

        // 1-B. 이미지 상세 섹션
        _pnlImageSection = new Panel { Left = 0, Top = 38, Width = 485, Height = 325, Visible = false };
        _btnBrowseImg = new Button { Text = ko ? "이미지 선택..." : "Browse...", Left = 0, Top = 5, Width = 105, Height = 30 };
        _btnBrowseImg.Click += (s, e) => SelectImage();

        _picPreview = new PictureBox
        {
            Left = _pnlImageSection.Width - 95,
            Top = 5,
            Width = 90,
            Height = 60,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom
        };
        _txtImagePath = new TextBox { Left = 115, Top = 9, Width = _picPreview.Left - 125, ReadOnly = true };

        var lblAcc = new Label { Text = ko ? "일치 정확도:" : "Match Acc:", Left = 0, Top = 50, AutoSize = true };
        _numImageAccuracy = new NumericUpDown { Left = 98, Top = 47, Width = 62, Minimum = 0, Maximum = 100, DecimalPlaces = 1, Value = 85 };
        _sliderImageAccuracy = new TrackBar { Left = 165, Top = 43, Width = 180, Minimum = 0, Maximum = 100, Value = 85, TickFrequency = 10 };
        _sliderImageAccuracy.Scroll += (s, e) => { _numImageAccuracy.Value = _sliderImageAccuracy.Value; };
        _numImageAccuracy.ValueChanged += (s, e) => { _sliderImageAccuracy.Value = Math.Clamp((int)_numImageAccuracy.Value, 0, 100); };

        _chkAllowFlipHorizontal = new CustomCheckBox
        {
            Text = ko ? "🔄 이미지 좌우 반전 포함 감지" : "🔄 Include Horizontally Flipped Match",
            Left = 0,
            Top = 85,
            Width = 450,
            AutoSize = true
        };

        _btnImgRoi = new Button { Text = ko ? "🎯 탐색 영역 지정" : "🎯 Select Search Region", Left = 0, Top = 120, Width = 200, Height = 32 };
        _btnImgRoi.Click += (s, e) => SelectImageRoi();
        _lblImageRoi = new Label { Text = ko ? "영역: 전체 화면" : "ROI: Full Screen", Left = 210, Top = 127, Width = 265, AutoEllipsis = true, ForeColor = Color.DarkSlateGray };

        _pnlImageSection.Controls.AddRange(new Control[]
        {
            _btnBrowseImg, _txtImagePath, _picPreview,
            lblAcc, _numImageAccuracy, _sliderImageAccuracy,
            _chkAllowFlipHorizontal,
            _btnImgRoi, _lblImageRoi
        });
        _pnlVision.Controls.Add(_pnlImageSection);

        // 2. 타이머 패널
        _pnlTimer = new Panel { Left = 20, Top = 58, Width = 485, Height = 370, Visible = false };
        var lblTimer = new Label { Text = ko ? "반복 주기 (ms):" : "Interval (ms):", Left = 0, Top = 20, Width = 110 };
        _numIntervalMs = new NumericUpDown { Left = 120, Top = 17, Width = 130, Minimum = 50, Maximum = 3600000, Value = 3000 };
        var lblTimerHint = new Label { Text = ko ? "※ 예: 3000 = 3초마다 발동" : "※ e.g. 3000 = every 3 seconds", Left = 265, Top = 20, Width = 200, ForeColor = Color.DarkGray };
        _pnlTimer.Controls.AddRange(new Control[] { lblTimer, _numIntervalMs, lblTimerHint });

        // 3. 변수 비교 패널
        _pnlVariable = new Panel { Left = 20, Top = 58, Width = 485, Height = 370, Visible = false };
        var lblVName = new Label { Text = ko ? "검사할 변수:" : "Variable:", Left = 0, Top = 15, Width = 90 };
        _txtVarName = new TextBox { Text = "변수1", Left = 100, Top = 12, Width = 160 };

        var lblVComp = new Label { Text = ko ? "비교 연산자:" : "Comparison:", Left = 0, Top = 55, Width = 90 };
        _cboVarComp = new CustomComboBox { Left = 100, Top = 52, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboVarComp.Items.AddRange(new object[]
        {
            ko ? "== (같음)" : "== (Equal)",
            ko ? "!= (다름)" : "!= (Not Equal)",
            ko ? "> (초과)" : "> (Greater)",
            ko ? ">= (이상)" : ">= (Greater/Eq)",
            ko ? "< (미만)" : "< (Less)",
            ko ? "<= (이하)" : "<= (Less/Eq)"
        });
        _cboVarComp.SelectedIndex = 0;

        var lblVVal = new Label { Text = ko ? "목표 기준값:" : "Target Value:", Left = 0, Top = 95, Width = 90 };
        _numVarTarget = new NumericUpDown { Left = 100, Top = 92, Width = 120, Minimum = -999999, Maximum = 999999, DecimalPlaces = 2, Value = 1 };

        var lblVarCheckHint = new Label
        {
            Text = ko 
                ? "💡 특정 변수의 현재 값을 검사하여 조건이 일치할 때만 발동합니다. (ON/OFF 스위치는 1 검사 등)"
                : "💡 Evaluates to true only when the variable matches the target condition.",
            Left = 0,
            Top = 135,
            Width = 450,
            Height = 45,
            ForeColor = Color.DarkSlateGray
        };
        _pnlVariable.Controls.AddRange(new Control[] { lblVName, _txtVarName, lblVComp, _cboVarComp, lblVVal, _numVarTarget, lblVarCheckHint });

        // 4. 상시 패널
        _pnlAlways = new Panel { Left = 20, Top = 58, Width = 485, Height = 370, Visible = false };
        var lblAlwaysHint = new Label
        {
            Text = ko ? "💡 별도의 조건 없이 항상 참(True)으로 평가되는 기본 조건입니다." : "💡 Always evaluates to true unconditionally.",
            Left = 0,
            Top = 20,
            AutoSize = true,
            ForeColor = Color.DarkSlateGray
        };
        _pnlAlways.Controls.Add(lblAlwaysHint);

        // 하단 확인 / 취소 버튼
        var btnOk = new Button
        {
            Text = ko ? "확인" : "OK",
            DialogResult = DialogResult.OK,
            Left = 300,
            Top = 435,
            Width = 95,
            Height = 35,
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold)
        };
        btnOk.Click += (s, e) => SaveTriggerData();

        var btnCancel = new Button
        {
            Text = ko ? "취소" : "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 405,
            Top = 435,
            Width = 90,
            Height = 35
        };

        Controls.AddRange(new Control[]
        {
            lblType, _cboType,
            _pnlVision, _pnlTimer, _pnlVariable, _pnlAlways,
            btnOk, btnCancel
        });

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        AppThemeHelper.ApplyTheme(this, AppSettings.Current.IsDarkMode);
    }

    private void UpdatePanels()
    {
        _pnlTimer.Visible = _cboType.SelectedIndex == 0;
        _pnlVision.Visible = _cboType.SelectedIndex == 1;
        _pnlVariable.Visible = _cboType.SelectedIndex == 2;
        _pnlAlways.Visible = _cboType.SelectedIndex == 3;
    }

    private void UpdateVisionSections()
    {
        bool isColor = _cboVisionTarget.SelectedIndex == 0;
        _pnlColorSection.Visible = isColor;
        _pnlImageSection.Visible = !isColor;
    }

    private void PickColor()
    {
        WindowState = FormWindowState.Minimized;
        Thread.Sleep(300);

        using var picker = new ColorPickerForm();
        if (picker.ShowDialog() == DialogResult.OK)
        {
            var col = picker.SelectedColor;
            string hex = $"#{col.R:X2}{col.G:X2}{col.B:X2}";
            TriggerData.CustomColorHex = hex;
            TriggerData.GaugeColor = GaugeColorType.CustomEyedropper;
            _pnlColorSample.BackColor = col;
            _lblColorHex.Text = hex;
        }

        WindowState = FormWindowState.Normal;
    }

    private void SelectGaugeRoi()
    {
        WindowState = FormWindowState.Minimized;
        Thread.Sleep(300);

        using var roiForm = new RoiSelectorForm();
        if (roiForm.ShowDialog() == DialogResult.OK)
        {
            var r = roiForm.SelectedRoi;
            TriggerData.GaugeRoi = new RoiConfig { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height };
            _lblGaugeRoi.Text = $"ROI: [{r.X}, {r.Y}, {r.Width}x{r.Height}]";
        }

        WindowState = FormWindowState.Normal;
    }

    private void SelectImage()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = Localization.IsKorean 
                ? "이미지 파일 (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|모든 파일 (*.*)|*.*"
                : "Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
            Title = Localization.IsKorean ? "감지할 템플릿 이미지 선택" : "Select Template Image"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _txtImagePath.Text = dlg.FileName;
            TriggerData.ImagePath = dlg.FileName;
            try
            {
                byte[] bytes = File.ReadAllBytes(dlg.FileName);
                TriggerData.ImageBase64 = Convert.ToBase64String(bytes);
                using var ms = new MemoryStream(bytes);
                _picPreview.Image = Image.FromStream(ms);
            }
            catch { }
        }
    }

    private void SelectImageRoi()
    {
        WindowState = FormWindowState.Minimized;
        Thread.Sleep(300);

        using var roiForm = new RoiSelectorForm();
        if (roiForm.ShowDialog() == DialogResult.OK)
        {
            var r = roiForm.SelectedRoi;
            TriggerData.ImageRoi = new RoiConfig { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height };
            _lblImageRoi.Text = $"ROI: [{r.X}, {r.Y}, {r.Width}x{r.Height}]";
        }

        WindowState = FormWindowState.Normal;
    }

    private void LoadTriggerData()
    {
        if (TriggerData.Type == TriggerType.VisionDetection || TriggerData.Type == TriggerType.GaugeBar || TriggerData.Type == TriggerType.ImageDetection)
        {
            _cboType.SelectedIndex = 1;
            bool isImage = (TriggerData.Type == TriggerType.ImageDetection) || 
                           (TriggerData.Type == TriggerType.VisionDetection && TriggerData.VisionTarget == VisionTargetType.Image);
            _cboVisionTarget.SelectedIndex = isImage ? 1 : 0;
            _cboVisionMatchMode.SelectedIndex = (TriggerData.VisionMatchMode == VisionMatchMode.Mismatch) ? 1 : 0;
        }
        else
        {
            _cboType.SelectedIndex = TriggerData.Type switch
            {
                TriggerType.TimerInterval => 0,
                TriggerType.VariableCheck => 2,
                TriggerType.Always => 3,
                _ => 1
            };
        }

        // Color Settings
        Color sampleColor = Color.Red;
        try
        {
            if (!string.IsNullOrEmpty(TriggerData.CustomColorHex))
                sampleColor = ColorTranslator.FromHtml(TriggerData.CustomColorHex);
        }
        catch { }
        _pnlColorSample.BackColor = sampleColor;
        _lblColorHex.Text = TriggerData.CustomColorHex ?? "#E74C3C";
        _cboGaugeOp.SelectedIndex = TriggerData.ComparisonOp switch
        {
            ComparisonOperator.LessThanOrEqual => 1,
            ComparisonOperator.GreaterThan => 2,
            ComparisonOperator.GreaterThanOrEqual => 3,
            _ => 0
        };
        _numColorAccuracy.Value = (decimal)Math.Clamp(TriggerData.ColorAccuracyPercent, 0, 100);
        _sliderColorAccuracy.Value = (int)_numColorAccuracy.Value;
        _numGaugeThresh.Value = (decimal)Math.Clamp(TriggerData.GaugeThresholdPercent, 0, 100);
        _sliderGaugeThresh.Value = (int)_numGaugeThresh.Value;
        _lblGaugeRoi.Text = TriggerData.GaugeRoi != null
            ? $"ROI: [{TriggerData.GaugeRoi.X}, {TriggerData.GaugeRoi.Y}, {TriggerData.GaugeRoi.Width}x{TriggerData.GaugeRoi.Height}]"
            : (Localization.IsKorean ? "ROI: 지정 필요" : "ROI: Required");

        // Image Settings
        _txtImagePath.Text = TriggerData.ImagePath ?? "";
        _numImageAccuracy.Value = (decimal)Math.Clamp(TriggerData.ImageAccuracyPercent, 0, 100);
        _sliderImageAccuracy.Value = (int)_numImageAccuracy.Value;
        _chkAllowFlipHorizontal.Checked = TriggerData.AllowFlipHorizontal;
        _lblImageRoi.Text = TriggerData.ImageRoi != null
            ? $"ROI: [{TriggerData.ImageRoi.X}, {TriggerData.ImageRoi.Y}, {TriggerData.ImageRoi.Width}x{TriggerData.ImageRoi.Height}]"
            : (Localization.IsKorean ? "ROI: 전체 화면" : "ROI: Full Screen");

        if (File.Exists(TriggerData.ImagePath))
        {
            try { _picPreview.Image = Image.FromFile(TriggerData.ImagePath); } catch { }
        }
        else if (!string.IsNullOrEmpty(TriggerData.ImageBase64))
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(TriggerData.ImageBase64);
                using var ms = new MemoryStream(bytes);
                _picPreview.Image = Image.FromStream(ms);
            }
            catch { _picPreview.Image = null; }
        }
        else _picPreview.Image = null;

        // Timer Settings
        _numIntervalMs.Value = Math.Clamp(TriggerData.IntervalMs, 50, 3600000);

        // Variable Settings
        _txtVarName.Text = TriggerData.VariableName ?? "변수1";
        _cboVarComp.SelectedIndex = (int)TriggerData.VarCompareOp;
        _numVarTarget.Value = (decimal)TriggerData.VarTargetValue;

        UpdatePanels();
        UpdateVisionSections();
    }

    private void SaveTriggerData()
    {
        if (_cboType.SelectedIndex == 1)
        {
            TriggerData.Type = TriggerType.VisionDetection;
            TriggerData.VisionTarget = _cboVisionTarget.SelectedIndex == 1 ? VisionTargetType.Image : VisionTargetType.Color;
            TriggerData.VisionMatchMode = _cboVisionMatchMode.SelectedIndex == 1 ? VisionMatchMode.Mismatch : VisionMatchMode.Match;

            if (TriggerData.VisionTarget == VisionTargetType.Color)
            {
                TriggerData.ComparisonOp = _cboGaugeOp.SelectedIndex switch
                {
                    1 => ComparisonOperator.LessThanOrEqual,
                    2 => ComparisonOperator.GreaterThan,
                    3 => ComparisonOperator.GreaterThanOrEqual,
                    _ => ComparisonOperator.LessThan
                };
                TriggerData.ColorAccuracyPercent = (double)_numColorAccuracy.Value;
                TriggerData.GaugeThresholdPercent = (double)_numGaugeThresh.Value;
            }
            else
            {
                TriggerData.ImagePath = _txtImagePath.Text;
                TriggerData.ImageAccuracyPercent = (double)_numImageAccuracy.Value;
                TriggerData.AllowFlipHorizontal = _chkAllowFlipHorizontal.Checked;
            }
        }
        else
        {
            TriggerData.Type = _cboType.SelectedIndex switch
            {
                0 => TriggerType.TimerInterval,
                2 => TriggerType.VariableCheck,
                3 => TriggerType.Always,
                _ => TriggerType.TimerInterval
            };

            switch (TriggerData.Type)
            {
                case TriggerType.TimerInterval:
                    TriggerData.IntervalMs = (int)_numIntervalMs.Value;
                    break;

                case TriggerType.VariableCheck:
                    TriggerData.VariableName = string.IsNullOrWhiteSpace(_txtVarName.Text) ? "변수1" : _txtVarName.Text.Trim();
                    TriggerData.VarCompareOp = (VariableComparison)_cboVarComp.SelectedIndex;
                    TriggerData.VarTargetValue = (double)_numVarTarget.Value;
                    break;
            }
        }
    }
}
