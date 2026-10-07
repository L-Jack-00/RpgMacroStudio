using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.UI;

/// <summary>
/// 이벤트 행동(커맨드) 설정 대화창입니다.
/// (키보드, 대기, 마우스 클릭, 변수 조작, 상태 전환)
/// </summary>
public class ActionEditDialog : Form
{
    public MacroAction ActionData { get; private set; }
    private readonly List<string> _availableStates;

    private CustomComboBox _cboActionType = null!;
    private Panel _pnlKey = null!;
    private Panel _pnlWait = null!;
    private Panel _pnlMouse = null!;
    private Panel _pnlVariable = null!;
    private Panel _pnlState = null!;

    // KeyPress 컨트롤
    private CustomComboBox _cboKey = null!;
    private NumericUpDown _numHold = null!;
    private NumericUpDown _numPost = null!;

    // Wait 컨트롤
    private NumericUpDown _numWait = null!;

    // Mouse 공통 컨트롤
    private CustomComboBox _cboMouseButton = null!;
    private CustomComboBox _cboMouseMode = null!;

    // Mouse: 좌표 모드 컨트롤
    private Panel _pnlCoordMode = null!;
    private NumericUpDown _numX = null!;
    private NumericUpDown _numY = null!;
    private Button _btnPickCoords = null!;

    // Mouse: 이미지 탐색 모드 컨트롤
    private Panel _pnlImgMode = null!;
    private TextBox _txtMouseImagePath = null!;
    private Button _btnBrowseMouseImg = null!;
    private PictureBox _picMousePreview = null!;
    private NumericUpDown _numMouseAccuracy = null!;
    private TrackBar _sliderMouseAccuracy = null!;
    private Button _btnMouseRoi = null!;
    private Label _lblMouseRoi = null!;

    // Mouse: 조건 감지 모드 컨트롤
    private Panel _pnlTriggerMode = null!;

    // Variable 컨트롤
    private TextBox _txtVarName = null!;
    private CustomComboBox _cboVarOp = null!;
    private NumericUpDown _numVarValue = null!;

    // State 전환 컨트롤
    private CustomComboBox _cboTargetState = null!;

    public ActionEditDialog(MacroAction? existing = null, List<string>? availableStates = null)
    {
        ActionData = existing ?? new MacroAction();
        _availableStates = availableStates ?? new List<string> { "전역 (Global)", "기본 상태", "전투 모드", "마을/정비" };
        InitializeComponent();
        LoadActionData();
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

        Text = ko ? "이벤트 행동(커맨드) 설정" : "Action Command Settings";
        Size = new Size(540, 500);
        MinimumSize = new Size(540, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = AppFontHelper.GetEmojiFont(9.5f);

        var lblType = new Label { Text = ko ? "행동 종류:" : "Action Type:", Left = 20, Top = 20, Width = 85 };
        _cboActionType = new CustomComboBox { Left = 110, Top = 17, Width = 385, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboActionType.Items.AddRange(new object[]
        {
            ko ? "⌨️ 키보드 키 누름 (KeyPress)" : "⌨️ Keyboard Key Press (KeyPress)",
            ko ? "⏳ 시간 대기 (Wait)" : "⏳ Wait Duration (Wait)",
            ko ? "🖱️ 마우스 클릭 (MouseClick)" : "🖱️ Mouse Click (MouseClick)",
            ko ? "🔢 변수 조작 (VariableControl)" : "🔢 Variable Control (VariableControl)",
            ko ? "🔄 상태 전환 (ChangeState)" : "🔄 Change State (ChangeState)"
        });
        _cboActionType.SelectedIndexChanged += (s, e) => UpdatePanels();

        // 1. Key Panel
        _pnlKey = new Panel { Left = 20, Top = 60, Width = 485, Height = 340, Visible = false };
        var lblKey = new Label { Text = ko ? "입력할 키:" : "Key to send:", Left = 10, Top = 15, Width = 110 };
        _cboKey = new CustomComboBox { Left = 130, Top = 12, Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var k in Enum.GetNames(typeof(Keys))) _cboKey.Items.Add(k);

        var lblHold = new Label { Text = ko ? "누름 시간(ms):" : "Hold duration (ms):", Left = 10, Top = 60, Width = 120 };
        _numHold = new NumericUpDown { Left = 130, Top = 58, Width = 90, Minimum = 10, Maximum = 5000, Value = 35 };

        var lblPost = new Label { Text = ko ? "입력 후 지연(ms):" : "Post delay (ms):", Left = 10, Top = 105, Width = 120 };
        _numPost = new NumericUpDown { Left = 130, Top = 103, Width = 90, Minimum = 0, Maximum = 10000, Value = 80 };

        _pnlKey.Controls.AddRange(new Control[] { lblKey, _cboKey, lblHold, _numHold, lblPost, _numPost });

        // 2. Wait Panel
        _pnlWait = new Panel { Left = 20, Top = 60, Width = 485, Height = 340, Visible = false };
        var lblWait = new Label { Text = ko ? "대기 시간 (밀리초):" : "Wait duration (ms):", Left = 10, Top = 20, Width = 140 };
        _numWait = new NumericUpDown { Left = 160, Top = 18, Width = 120, Minimum = 10, Maximum = 3600000, Value = 500 };
        var lblWaitHint = new Label { Text = ko ? "※ 1000밀리초 = 1초" : "※ 1000ms = 1 second", Left = 160, Top = 55, Width = 250, ForeColor = Color.DarkGray };
        _pnlWait.Controls.AddRange(new Control[] { lblWait, _numWait, lblWaitHint });

        // 3. Mouse Panel
        _pnlMouse = new Panel { Left = 20, Top = 60, Width = 485, Height = 340, Visible = false };

        var lblMouseBtn = new Label { Text = ko ? "마우스 버튼:" : "Mouse Button:", Left = 10, Top = 11, Width = 85 };
        _cboMouseButton = new CustomComboBox { Left = 100, Top = 8, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboMouseButton.Items.AddRange(new object[]
        {
            ko ? "Left (좌클릭)" : "Left Click",
            ko ? "Right (우클릭)" : "Right Click",
            ko ? "Middle (휠클릭)" : "Middle (Wheel Click)",
            ko ? "XButton1 (측면 1)" : "XButton 1 (Side 1)",
            ko ? "XButton2 (측면 2)" : "XButton 2 (Side 2)"
        });

        var lblMode = new Label { Text = ko ? "클릭 위치 방식:" : "Target Mode:", Left = 10, Top = 48, Width = 100 };
        _cboMouseMode = new CustomComboBox { Left = 115, Top = 45, Width = 355, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboMouseMode.Items.AddRange(new object[]
        {
            ko ? "🎯 특정 좌표 (X, Y) 클릭" : "🎯 Click Specific Coordinates (X, Y)",
            ko ? "🖼️ 화면에서 이미지 찾아 중심 클릭" : "🖼️ Search Image on Screen & Click Center",
            ko ? "⚡ 발동 조건에서 감지된 위치 클릭" : "⚡ Click Trigger-detected Position"
        });
        _cboMouseMode.SelectedIndexChanged += (s, e) => UpdateMouseSubPanels();

        // 3-A. 좌표 모드
        _pnlCoordMode = new Panel { Left = 10, Top = 85, Width = 465, Height = 240, Visible = false };
        var lblX = new Label { Text = ko ? "X 좌표:" : "X Coord:", Left = 10, Top = 15, Width = 60 };
        _numX = new NumericUpDown { Left = 75, Top = 12, Width = 90, Maximum = 10000 };
        var lblY = new Label { Text = ko ? "Y 좌표:" : "Y Coord:", Left = 180, Top = 15, Width = 60 };
        _numY = new NumericUpDown { Left = 245, Top = 12, Width = 90, Maximum = 10000 };

        _btnPickCoords = new Button
        {
            Text = ko ? "🎯 화면 클릭하여 좌표 지정 (스포이드)" : "🎯 Click on Screen to Pick Coordinates",
            Left = 10,
            Top = 55,
            Width = 325,
            Height = 35,
            BackColor = Color.FromArgb(46, 204, 113),
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold)
        };
        _btnPickCoords.Click += (s, e) => PickCoordinates();

        var lblCoordHint = new Label
        {
            Text = ko ? "💡 '화면 클릭하여 좌표 지정'을 누르고 원하는 위치를 클릭하면 X, Y가 자동 입력됩니다." : "💡 Click button and left-click anywhere on screen to auto-fill (X, Y).",
            Left = 10,
            Top = 100,
            Width = 445,
            Height = 40,
            ForeColor = Color.DarkSlateGray
        };
        _pnlCoordMode.Controls.AddRange(new Control[] { lblX, _numX, lblY, _numY, _btnPickCoords, lblCoordHint });

        // 3-B. 이미지 탐색 모드
        _pnlImgMode = new Panel { Left = 10, Top = 85, Width = 465, Height = 240, Visible = false };
        _btnBrowseMouseImg = new Button { Text = ko ? "이미지 선택..." : "Browse...", Left = 0, Top = 5, Width = 105, Height = 30 };
        _btnBrowseMouseImg.Click += (s, e) => SelectImageForAction();

        _picMousePreview = new PictureBox
        {
            Left = _pnlImgMode.Width - 95,
            Top = 5,
            Width = 90,
            Height = 60,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        _txtMouseImagePath = new TextBox { Left = 115, Top = 9, Width = _picMousePreview.Left - 125, ReadOnly = true };

        var lblAccTitle = new Label { Text = ko ? "정확도:" : "Accuracy:", Left = 0, Top = 50, AutoSize = true };
        _numMouseAccuracy = new NumericUpDown { Left = 75, Top = 47, Width = 60, Minimum = 0, Maximum = 100, DecimalPlaces = 1, Value = 85 };
        var lblPct = new Label { Text = "%", Left = 140, Top = 50, Width = 20, Font = new Font(Font, FontStyle.Bold) };
        _sliderMouseAccuracy = new TrackBar { Left = 165, Top = 43, Width = 180, Minimum = 0, Maximum = 100, Value = 85, TickFrequency = 10 };
        _sliderMouseAccuracy.Scroll += (s, e) => { _numMouseAccuracy.Value = _sliderMouseAccuracy.Value; };
        _numMouseAccuracy.ValueChanged += (s, e) => { _sliderMouseAccuracy.Value = Math.Clamp((int)_numMouseAccuracy.Value, 0, 100); };

        _btnMouseRoi = new Button { Text = ko ? "🎯 탐색 영역(ROI) 지정" : "🎯 Select Region (ROI)", Left = 0, Top = 95, Width = 200, Height = 30 };
        _btnMouseRoi.Click += (s, e) => SelectRoiForAction();
        _lblMouseRoi = new Label { Text = ko ? "ROI: 전체 화면" : "ROI: Full Screen", Left = 210, Top = 102, Width = 240, AutoEllipsis = true, ForeColor = Color.DarkSlateGray };

        _pnlImgMode.Controls.AddRange(new Control[] { _btnBrowseMouseImg, _txtMouseImagePath, _picMousePreview, lblAccTitle, _numMouseAccuracy, lblPct, _sliderMouseAccuracy, _btnMouseRoi, _lblMouseRoi });

        // 3-C. 조건 감지 모드
        _pnlTriggerMode = new Panel { Left = 10, Top = 85, Width = 465, Height = 240, Visible = false };
        var lblTrigHint = new Label
        {
            Text = ko 
                ? "💡 이 이벤트의 '발동 조건'에서 이미지가 감지되었을 때 해당 감지된 이미지의 중심 좌표를 자동으로 클릭합니다." 
                : "💡 Clicks the center location of the image detected by this event's trigger condition.",
            Left = 10,
            Top = 20,
            Width = 445,
            Height = 50,
            ForeColor = Color.DarkSlateGray
        };
        _pnlTriggerMode.Controls.Add(lblTrigHint);
        _pnlMouse.Controls.AddRange(new Control[] { lblMouseBtn, _cboMouseButton, lblMode, _cboMouseMode, _pnlCoordMode, _pnlImgMode, _pnlTriggerMode });

        // 4. Variable Panel
        _pnlVariable = new Panel { Left = 20, Top = 60, Width = 485, Height = 340, Visible = false };
        var lblVarName = new Label { Text = ko ? "변수 이름:" : "Variable:", Left = 10, Top = 15, Width = 90 };
        _txtVarName = new TextBox { Text = "변수1", Left = 110, Top = 12, Width = 160 };

        var lblVarOp = new Label { Text = ko ? "조작 연산:" : "Operation:", Left = 10, Top = 55, Width = 90 };
        _cboVarOp = new CustomComboBox { Left = 110, Top = 52, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboVarOp.Items.AddRange(new object[]
        {
            ko ? "대입 (=)" : "Set (=)",
            ko ? "더하기 (+=)" : "Add (+=)",
            ko ? "빼기 (-=)" : "Subtract (-=)"
        });
        _cboVarOp.SelectedIndex = 0;

        var lblVarVal = new Label { Text = ko ? "연산 값:" : "Value:", Left = 10, Top = 95, Width = 90 };
        _numVarValue = new NumericUpDown { Left = 110, Top = 92, Width = 120, Minimum = -999999, Maximum = 999999, DecimalPlaces = 2, Value = 1 };

        var lblVarHint = new Label
        {
            Text = ko 
                ? "💡 변수에 숫자를 대입하거나 가감할 수 있습니다. (ON/OFF 스위치 용도로 1 또는 0 대입 가능)"
                : "💡 Assign or calculate values on variables. (Use 1 or 0 for switch toggles)",
            Left = 10,
            Top = 135,
            Width = 450,
            Height = 45,
            ForeColor = Color.DarkSlateGray
        };
        _pnlVariable.Controls.AddRange(new Control[] { lblVarName, _txtVarName, lblVarOp, _cboVarOp, lblVarVal, _numVarValue, lblVarHint });

        // 5. State Panel
        _pnlState = new Panel { Left = 20, Top = 60, Width = 485, Height = 340, Visible = false };
        var lblTState = new Label { Text = ko ? "전환할 상태:" : "Target State:", Left = 10, Top = 20, Width = 100 };
        _cboTargetState = new CustomComboBox { Left = 120, Top = 17, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var st in _availableStates)
        {
            if (!MacroEventProfile.IsGlobalState(st)) _cboTargetState.Items.Add(st);
        }
        if (_cboTargetState.Items.Count > 0) _cboTargetState.SelectedIndex = 0;

        var lblStateHint = new Label
        {
            Text = ko 
                ? "💡 이 행동이 실행되면 매크로의 현재 활성 상태(State)가 즉시 전환됩니다."
                : "💡 Transitions current active state immediately when executed.",
            Left = 10,
            Top = 60,
            Width = 450,
            Height = 45,
            ForeColor = Color.DarkSlateGray
        };
        _pnlState.Controls.AddRange(new Control[] { lblTState, _cboTargetState, lblStateHint });

        // 하단 확인 / 취소 버튼
        var btnOk = new Button
        {
            Text = ko ? "확인" : "OK",
            DialogResult = DialogResult.OK,
            Left = 300,
            Top = 415,
            Width = 95,
            Height = 35,
            BackColor = Color.FromArgb(52, 152, 219),
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold)
        };
        btnOk.Click += (s, e) => SaveActionData();

        var btnCancel = new Button
        {
            Text = ko ? "취소" : "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 405,
            Top = 415,
            Width = 90,
            Height = 35
        };

        Controls.AddRange(new Control[]
        {
            lblType, _cboActionType,
            _pnlKey, _pnlWait, _pnlMouse, _pnlVariable, _pnlState,
            btnOk, btnCancel
        });

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        AppThemeHelper.ApplyTheme(this, AppSettings.Current.IsDarkMode);
    }

    private void UpdatePanels()
    {
        _pnlKey.Visible = _cboActionType.SelectedIndex == 0;
        _pnlWait.Visible = _cboActionType.SelectedIndex == 1;
        _pnlMouse.Visible = _cboActionType.SelectedIndex == 2;
        _pnlVariable.Visible = _cboActionType.SelectedIndex == 3;
        _pnlState.Visible = _cboActionType.SelectedIndex == 4;
    }

    private void UpdateMouseSubPanels()
    {
        _pnlCoordMode.Visible = _cboMouseMode.SelectedIndex == 0;
        _pnlImgMode.Visible = _cboMouseMode.SelectedIndex == 1;
        _pnlTriggerMode.Visible = _cboMouseMode.SelectedIndex == 2;
    }

    private void PickCoordinates()
    {
        WindowState = FormWindowState.Minimized;
        Thread.Sleep(300);

        using var picker = new CoordinatePickerForm();
        if (picker.ShowDialog() == DialogResult.OK)
        {
            _numX.Value = picker.SelectedPoint.X;
            _numY.Value = picker.SelectedPoint.Y;
        }

        WindowState = FormWindowState.Normal;
    }

    private void SelectImageForAction()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = Localization.IsKorean 
                ? "이미지 파일 (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|모든 파일 (*.*)|*.*"
                : "Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
            Title = Localization.IsKorean ? "클릭할 대상 이미지 선택" : "Select Target Image to Click"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _txtMouseImagePath.Text = dlg.FileName;
            ActionData.TargetImagePath = dlg.FileName;

            try
            {
                byte[] rawBytes = File.ReadAllBytes(dlg.FileName);
                ActionData.TargetImageBase64 = Convert.ToBase64String(rawBytes);

                using var ms = new MemoryStream(rawBytes);
                _picMousePreview.Image = Image.FromStream(ms);
            }
            catch { }
        }
    }

    private void SelectRoiForAction()
    {
        WindowState = FormWindowState.Minimized;
        Thread.Sleep(300);

        using var roiForm = new RoiSelectorForm();
        if (roiForm.ShowDialog() == DialogResult.OK)
        {
            var r = roiForm.SelectedRoi;
            ActionData.TargetImageRoi = new RoiConfig { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height };
            _lblMouseRoi.Text = $"ROI: [{r.X}, {r.Y}, {r.Width}x{r.Height}]";
        }

        WindowState = FormWindowState.Normal;
    }

    private void LoadActionData()
    {
        _cboActionType.SelectedIndex = ActionData.Type switch
        {
            ActionType.Wait => 1,
            ActionType.MouseClick => 2,
            ActionType.VariableControl => 3,
            ActionType.ChangeState => 4,
            _ => 0
        };

        // Key
        _cboKey.SelectedItem = ActionData.Key;
        if (_cboKey.SelectedIndex < 0) _cboKey.SelectedItem = "D1";
        _numHold.Value = Math.Clamp(ActionData.HoldTimeMs, 10, 5000);
        _numPost.Value = Math.Clamp(ActionData.PostDelayMs, 0, 10000);

        // Wait
        _numWait.Value = Math.Clamp(ActionData.WaitDurationMs, 10, 3600000);

        // Mouse
        _cboMouseButton.SelectedIndex = ActionData.MouseButton switch
        {
            MouseButtonType.Right => 1,
            MouseButtonType.Middle => 2,
            MouseButtonType.XButton1 => 3,
            MouseButtonType.XButton2 => 4,
            _ => 0
        };
        if (ActionData.TargetMode == MouseTargetMode.TargetImage || !string.IsNullOrEmpty(ActionData.TargetImagePath) || !string.IsNullOrEmpty(ActionData.TargetImageBase64))
            _cboMouseMode.SelectedIndex = 1;
        else if (ActionData.TargetMode == MouseTargetMode.TriggerDetected || ActionData.ClickDetectedLocation)
            _cboMouseMode.SelectedIndex = 2;
        else
            _cboMouseMode.SelectedIndex = 0;

        _numX.Value = Math.Clamp(ActionData.CustomX, 0, 10000);
        _numY.Value = Math.Clamp(ActionData.CustomY, 0, 10000);
        _txtMouseImagePath.Text = ActionData.TargetImagePath ?? "";
        decimal acc = (decimal)Math.Clamp(ActionData.TargetImageAccuracyPercent, 0.0, 100.0);
        _numMouseAccuracy.Value = acc;
        _sliderMouseAccuracy.Value = (int)acc;

        // Variable
        _txtVarName.Text = ActionData.VariableName ?? "변수1";
        _cboVarOp.SelectedIndex = (int)ActionData.VariableOp;
        _numVarValue.Value = (decimal)ActionData.VariableValue;

        // State
        if (_cboTargetState.Items.Contains(ActionData.TargetState))
            _cboTargetState.SelectedItem = ActionData.TargetState;

        UpdatePanels();
        UpdateMouseSubPanels();
    }

    private void SaveActionData()
    {
        bool ko = Localization.IsKorean;

        ActionData.Type = _cboActionType.SelectedIndex switch
        {
            1 => ActionType.Wait,
            2 => ActionType.MouseClick,
            3 => ActionType.VariableControl,
            4 => ActionType.ChangeState,
            _ => ActionType.KeyPress
        };

        switch (ActionData.Type)
        {
            case ActionType.KeyPress:
                ActionData.Key = _cboKey.SelectedItem?.ToString() ?? "D1";
                ActionData.HoldTimeMs = (int)_numHold.Value;
                ActionData.PostDelayMs = (int)_numPost.Value;
                ActionData.Description = ko
                    ? $"⌨️ 키 입력: [{ActionData.Key}] (누름 {ActionData.HoldTimeMs}ms, 딜레이 {ActionData.PostDelayMs}ms)"
                    : $"⌨️ Key Press: [{ActionData.Key}] (Hold {ActionData.HoldTimeMs}ms, Delay {ActionData.PostDelayMs}ms)";
                break;

            case ActionType.Wait:
                ActionData.WaitDurationMs = (int)_numWait.Value;
                ActionData.Description = ko
                    ? $"⏳ 대기: {ActionData.WaitDurationMs}ms ({(double)ActionData.WaitDurationMs / 1000:0.##}초)"
                    : $"⏳ Wait: {ActionData.WaitDurationMs}ms ({(double)ActionData.WaitDurationMs / 1000:0.##}s)";
                break;
            case ActionType.MouseClick:
                ActionData.MouseButton = _cboMouseButton.SelectedIndex switch
                {
                    1 => MouseButtonType.Right,
                    2 => MouseButtonType.Middle,
                    3 => MouseButtonType.XButton1,
                    4 => MouseButtonType.XButton2,
                    _ => MouseButtonType.Left
                };

                if (_cboMouseMode.SelectedIndex == 0)
                {
                    ActionData.TargetMode = MouseTargetMode.Coordinates;
                    ActionData.ClickDetectedLocation = false;
                    ActionData.CustomX = (int)_numX.Value;
                    ActionData.CustomY = (int)_numY.Value;
                    ActionData.Description = ko
                        ? $"🖱️ 마우스 클릭 (좌표): [{ActionData.CustomX}, {ActionData.CustomY}] ({ActionData.MouseButton})"
                        : $"🖱️ Mouse Click (Coords): [{ActionData.CustomX}, {ActionData.CustomY}] ({ActionData.MouseButton})";
                }
                else if (_cboMouseMode.SelectedIndex == 1)
                {
                    ActionData.TargetMode = MouseTargetMode.TargetImage;
                    ActionData.ClickDetectedLocation = false;
                    ActionData.TargetImagePath = _txtMouseImagePath.Text;
                    ActionData.TargetImageAccuracyPercent = (double)_numMouseAccuracy.Value;
                    string imgName = Path.GetFileName(ActionData.TargetImagePath);
                    if (string.IsNullOrEmpty(imgName)) imgName = ko ? "내장 이미지" : "Embedded Image";

                    ActionData.Description = ko
                        ? $"🖱️ 마우스 클릭 (이미지 탐색): [{imgName}] ({ActionData.MouseButton}, 정확도 {ActionData.TargetImageAccuracyPercent}%)"
                        : $"🖱️ Mouse Click (Image Match): [{imgName}] ({ActionData.MouseButton}, Acc {ActionData.TargetImageAccuracyPercent}%)";
                }
                else
                {
                    ActionData.TargetMode = MouseTargetMode.TriggerDetected;
                    ActionData.ClickDetectedLocation = true;
                    ActionData.Description = ko
                        ? $"🖱️ 마우스 클릭 (조건 감지 위치): [감지 위치 중심] ({ActionData.MouseButton})"
                        : $"🖱️ Mouse Click (Detected): [Trigger Location] ({ActionData.MouseButton})";
                }
                break;

            case ActionType.VariableControl:
                ActionData.VariableName = string.IsNullOrWhiteSpace(_txtVarName.Text) ? "변수1" : _txtVarName.Text.Trim();
                ActionData.VariableOp = (VariableOperator)_cboVarOp.SelectedIndex;
                ActionData.VariableValue = (double)_numVarValue.Value;
                string opSymbol = ActionData.VariableOp switch
                {
                    VariableOperator.Add => "+=",
                    VariableOperator.Subtract => "-=",
                    _ => "="
                };
                ActionData.Description = $"🔢 변수 조작: [{ActionData.VariableName}] {opSymbol} {ActionData.VariableValue}";
                break;

            case ActionType.ChangeState:
                ActionData.TargetState = _cboTargetState.SelectedItem?.ToString() ?? "기본 상태";
                ActionData.Description = $"🔄 상태 전환 ➔ [{ActionData.TargetState}]";
                break;
        }
    }
}
