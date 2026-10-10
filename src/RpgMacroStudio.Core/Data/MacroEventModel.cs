using System.Drawing;
using System.Text.Json.Serialization;
using OpenCvSharp;
using RpgMacroHarness.Vision;

namespace RpgMacroHarness.Data;

public enum ComparisonOperator
{
    LessThan,          // 미만 (<)
    GreaterThan,       // 초과 (>)
    LessThanOrEqual,   // 이하 (<=)
    GreaterThanOrEqual // 이상 (>=)
}

public enum VariableOperator
{
    Set,       // 대입 (=)
    Add,       // 더하기 (+=)
    Subtract   // 빼기 (-=)
}

public enum VariableComparison
{
    Equal,              // ==
    NotEqual,           // !=
    GreaterThan,        // >
    GreaterThanOrEqual, // >=
    LessThan,           // <
    LessThanOrEqual     // <=
}

public class RoiConfig
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public enum VisionTargetType
{
    Color, // 색상 감지 (스포이드, 면적 밀도/일치)
    Image  // 이미지 템플릿 매칭
}

public enum VisionMatchMode
{
    Match,    // 일치 (조건 만족 시 발동)
    Mismatch  // 불일치 (조건 불만족 시 발동)
}

public enum TriggerType
{
    Always,          // 상시 반복 실행 (조건 없음)
    VisionDetection, // 색깔/이미지 일치/불일치 비전 감지 시스템 (통합)
    GaugeBar,        // 하위 호환 (색상 감지)
    ImageDetection,  // 하위 호환 (이미지 감지)
    TimerInterval,   // 주기적 타이머 (X초마다)
    VariableCheck    // 변수/스위치 비교 조건 (변수 == 값 등)
}

public enum ActionType
{
    KeyPress,        // 키보드 키 누름
    Wait,            // 대기 (지연)
    MouseClick,      // 마우스 클릭 (좌표 or 이미지 탐색)
    VariableControl, // 변수/스위치 연산 (값 대입, 더하기, 빼기)
    ChangeState      // 상태 전환 (예: "전투" -> "마을")
}

public enum KeyStrokeMode
{
    KeyDownUp,  // 키다운업 (누르고 뗌 - 기본값)
    KeyDown,    // 키다운 (누름 유지 - 동시 입력 시작)
    KeyUp       // 키업 (손 뗌 - 동시 입력 종료)
}

public enum MouseStrokeMode
{
    MouseDownUp, // 마우스다운업 (누르고 뗌 - 기본 클릭)
    MouseDown,   // 마우스다운 (누름 유지 - 드래그/동시 입력 시작)
    MouseUp      // 마우스업 (손 뗌 - 드래그/동시 입력 해제)
}

public enum MouseButtonType
{
    Left,
    Right,
    Middle,
    XButton1,
    XButton2
}

public enum MouseTargetMode
{
    Coordinates,        // 지정된 좌표 (X, Y) 클릭
    TargetImage,        // 화면에서 지정된 이미지를 찾아 중심 클릭
    TriggerDetected     // 발동 조건에서 감지된 위치 클릭
}

/// <summary>
/// 이벤트 내에서 순차적으로 실행되는 개별 행동 커맨드 (RPG 쯔꾸르의 이벤트 커맨드)
/// </summary>
public class MacroAction
{
    public ActionType Type { get; set; } = ActionType.KeyPress;
    public string Description { get; set; } = "키 입력";

    // 1. KeyPress 설정
    public KeyStrokeMode KeyMode { get; set; } = KeyStrokeMode.KeyDownUp;
    public string Key { get; set; } = "D1";
    public int HoldTimeMs { get; set; } = 35;
    public int PostDelayMs { get; set; } = 80;

    // 2. Wait 설정
    public int WaitDurationMs { get; set; } = 500;

    // 3. MouseClick 설정
    public MouseButtonType MouseButton { get; set; } = MouseButtonType.Left;
    public MouseStrokeMode MouseMode { get; set; } = MouseStrokeMode.MouseDownUp;
    public MouseTargetMode TargetMode { get; set; } = MouseTargetMode.Coordinates;
    public bool ClickDetectedLocation { get; set; } = false; // 하위 호환성 유지용
    public int CustomX { get; set; }
    public int CustomY { get; set; }

    // 마우스 클릭 대상 이미지 설정 (화면에서 이미지를 찾아 클릭)
    public string? TargetImagePath { get; set; }
    public string? TargetImageBase64 { get; set; }
    public double TargetImageAccuracyPercent { get; set; } = 85.0;
    public RoiConfig? TargetImageRoi { get; set; }

    // 4. 변수 조작 설정 (VariableControl)
    public string VariableName { get; set; } = "변수1";
    public VariableOperator VariableOp { get; set; } = VariableOperator.Set;
    public double VariableValue { get; set; } = 1.0;

    // 5. 상태 전환 설정 (ChangeState)
    public string TargetState { get; set; } = "기본 상태";
}

/// <summary>
/// 이벤트의 실행 조건 (FSM 노드 발동 조건)
/// </summary>
public class MacroTrigger
{
    public TriggerType Type { get; set; } = TriggerType.Always;

    // 통합 비전 감지 설정 (색상/이미지 일치/불일치)
    public VisionTargetType VisionTarget { get; set; } = VisionTargetType.Color;
    public VisionMatchMode VisionMatchMode { get; set; } = VisionMatchMode.Match;
    public bool AllowFlipHorizontal { get; set; } = false; // 이미지 감지 시 좌우 반전 포함 여부

    // 게이지바 / 색상 조건
    public GaugeColorType GaugeColor { get; set; } = GaugeColorType.CustomEyedropper;
    public string CustomColorHex { get; set; } = "#E74C3C"; // 스포이드 색상 HEX
    public double ColorAccuracyPercent { get; set; } = 85.0;
    public ComparisonOperator ComparisonOp { get; set; } = ComparisonOperator.LessThanOrEqual;
    public double GaugeThresholdPercent { get; set; } = 40.0;
    public RoiConfig? GaugeRoi { get; set; }

    // 이미지 감지 조건
    public string? ImagePath { get; set; }
    public string? ImageBase64 { get; set; }
    public double ImageAccuracyPercent { get; set; } = 85.0;
    public RoiConfig? ImageRoi { get; set; }

    // 주기적 타이머 조건
    public int IntervalMs { get; set; } = 1000;

    // 변수 비교 조건 (VariableCheck)
    public string VariableName { get; set; } = "변수1";
    public VariableComparison VarCompareOp { get; set; } = VariableComparison.Equal;
    public double VarTargetValue { get; set; } = 1.0;

    /// <summary>
    /// UI 요약 문자열 반환
    /// </summary>
    public string GetSummary()
    {
        bool ko = Localization.IsKorean;
        if (Type == TriggerType.VisionDetection || Type == TriggerType.GaugeBar || Type == TriggerType.ImageDetection)
        {
            bool isImage = (Type == TriggerType.ImageDetection) || (Type == TriggerType.VisionDetection && VisionTarget == VisionTargetType.Image);
            string modeStr = ko 
                ? (VisionMatchMode == VisionMatchMode.Match ? "일치" : "불일치")
                : (VisionMatchMode == VisionMatchMode.Match ? "Match" : "Mismatch");

            if (isImage)
            {
                string flip = AllowFlipHorizontal ? (ko ? " [반전포함]" : " [Flipped]") : "";
                return ko
                    ? $"🖼️ 이미지 {modeStr} ({ImageAccuracyPercent:F0}%{flip})"
                    : $"🖼️ Image {modeStr} ({ImageAccuracyPercent:F0}%{flip})";
            }
            else
            {
                string op = ComparisonOp switch
                {
                    ComparisonOperator.LessThan => "<",
                    ComparisonOperator.GreaterThan => ">",
                    ComparisonOperator.GreaterThanOrEqual => ">=",
                    _ => "<="
                };
                return ko
                    ? $"🎨 색상 {modeStr} ({GaugeThresholdPercent:F0}% {op})"
                    : $"🎨 Color {modeStr} ({GaugeThresholdPercent:F0}% {op})";
            }
        }

        return Type switch
        {
            TriggerType.Always => ko ? "🔄 무조건 상시" : "🔄 Always (Unconditional)",
            TriggerType.TimerInterval => ko ? $"⏱️ 타이머 ({IntervalMs}ms)" : $"⏱️ Timer ({IntervalMs}ms)",
            TriggerType.VariableCheck => ko ? $"🔢 변수 [{VariableName}] {VarCompareOp} {VarTargetValue}" : $"🔢 Var [{VariableName}] {VarCompareOp} {VarTargetValue}",
            _ => ko ? "기본 조건" : "Default Condition"
        };
    }
}

/// <summary>
/// RPG 쯔꾸르의 '이벤트' 하나에 대응하는 독립 실행 단위 (상태 태그 및 다중 조건 지원)
/// </summary>
public class MacroEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "새 이벤트";
    public bool IsEnabled { get; set; } = true;
    public bool IsParallel { get; set; } = true; // 동시 실행(병렬) 여부
    public int HumanizerJitterMs { get; set; } = 27; // 휴머나이저 지연 편차 (0~54ms, 기본 27ms)
    public int HumanizerJitterPercent { get; set; } = 100; // 하위 호환용 (0~200%)

    /// <summary>
    /// 소속 상태 태그 ("Global": 모든 상태에서 동작, 또는 특정 상태 이름)
    /// </summary>
    public string StateTag { get; set; } = "Global";

    /// <summary>
    /// 다중 발동 조건 목록 (모든 조건이 만족할 때 실행 - AND 결합)
    /// </summary>
    public List<MacroTrigger> Triggers { get; set; } = new() { new MacroTrigger() };

    /// <summary>
    /// 구버전 JSON 프로필의 "Trigger" 단일 필드를 Triggers[0]으로 안전하게 역직렬화하기 위한 어댑터
    /// </summary>
    [JsonPropertyName("Trigger")]
    public MacroTrigger? LegacyTrigger
    {
        get => null;
        set
        {
            if (value != null)
            {
                if (Triggers.Count == 0) Triggers.Add(value);
                else Triggers[0] = value;
            }
        }
    }

    /// <summary>
    /// 단일 조건 호환용 프로퍼티 (첫 번째 조건과 연동)
    /// </summary>
    [JsonIgnore]
    public MacroTrigger Trigger
    {
        get
        {
            if (Triggers.Count == 0) Triggers.Add(new MacroTrigger());
            return Triggers[0];
        }
        set
        {
            if (Triggers.Count == 0) Triggers.Add(value);
            else Triggers[0] = value;
        }
    }

    /// <summary>
    /// 실행할 행동 목록 (순차 실행)
    /// </summary>
    public List<MacroAction> Actions { get; set; } = new();
}

/// <summary>
/// 전체 매크로 프로젝트 프로필 (상태 목록 및 전역 변수 관리)
/// </summary>
public class MacroEventProfile
{
    public string ProfileName { get; set; } = "RPG 매크로 이벤트 프로필";
    public string EmergencyStopKey { get; set; } = "F12";
    public int CheckIntervalMs { get; set; } = 100;

    /// <summary>
    /// 등록된 상태 목록 (기본: "Global", "기본 상태")
    /// </summary>
    public List<string> States { get; set; } = new() { "Global", "기본 상태" };

    /// <summary>
    /// 시작 시 기본 활성 상태
    /// </summary>
    public string InitialState { get; set; } = "기본 상태";

    /// <summary>
    /// 지정된 상태명이 '전역(Global)' 상태인지 여부를 판별합니다.
    /// 구버전 호환("전역", "Global", "전역 (Global)")을 모두 완벽하게 지원합니다.
    /// </summary>
    public static bool IsGlobalState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return false;
        string s = state.Trim();
        return s.Equals("전역", StringComparison.OrdinalIgnoreCase) ||
               s.Equals("Global", StringComparison.OrdinalIgnoreCase) ||
               s.StartsWith("전역 (Global)", StringComparison.OrdinalIgnoreCase) ||
               s.StartsWith("전역 (", StringComparison.OrdinalIgnoreCase) ||
               s.StartsWith("전역(", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 지정된 상태명이 '기본 상태(Default State)'인지 여부를 판별합니다.
    /// </summary>
    public static bool IsDefaultState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return false;
        string s = state.Trim();
        return s.Equals("기본 상태", StringComparison.OrdinalIgnoreCase) ||
               s.Equals("Default State", StringComparison.OrdinalIgnoreCase) ||
               s.StartsWith("기본 상태 (Default", StringComparison.OrdinalIgnoreCase) ||
               s.StartsWith("기본 상태(", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// UI 표시용 상태 명칭을 시스템 언어에 맞춰 동적으로 반환합니다.
    /// 프로필 파일의 고정 문자열에 구애받지 않고, 한국어 설정 시 "전역", 영문 설정 시 "Global"로 완벽하게 표시됩니다.
    /// </summary>
    public static string FormatStateDisplay(string? state, bool isKorean)
    {
        if (string.IsNullOrWhiteSpace(state)) return "";
        if (IsGlobalState(state)) return isKorean ? "전역" : "Global";
        if (IsDefaultState(state)) return isKorean ? "기본 상태" : "Default State";
        return state;
    }

    /// <summary>
    /// UI 표시용 이벤트 명칭을 시스템 언어에 맞춰 동적으로 반환합니다.
    /// 사용자가 직접 입력한 커스텀 명칭은 온전히 보존하며, 전역 기본 이벤트(예: "전역 이벤트 1", "Global Event 1")는 시스템 언어에 따라 실시간으로 번역 표시합니다.
    /// </summary>
    public static string FormatEventDisplay(MacroEvent? evt, bool isKorean)
    {
        if (evt == null) return "";
        string name = evt.Name ?? "";

        if (IsGlobalState(evt.StateTag))
        {
            var match = System.Text.RegularExpressions.Regex.Match(name, @"^(?:전역(?:\s*\(Global\))?\s*이벤트|Global\s*Event)\s*(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string num = match.Groups[1].Value;
                return isKorean ? $"전역 이벤트 {num}" : $"Global Event {num}";
            }

            if (name == "전역 이벤트" || name == "Global Event" || name == "전역 (Global) 이벤트")
            {
                return isKorean ? "전역 이벤트" : "Global Event";
            }
        }

        return name;
    }

    /// <summary>
    /// 전역 변수 저장소 (변수 이름 -> 값)
    /// </summary>
    public Dictionary<string, double> Variables { get; set; } = new();

    /// <summary>
    /// 등록된 이벤트 전체 목록
    /// </summary>
    public List<MacroEvent> Events { get; set; } = new();

    /// <summary>
    /// 노드 캔버스 상의 각 상태 노드 위치 좌표 (X, Y)
    /// </summary>
    public Dictionary<string, NodeCanvasPosition> NodePositions { get; set; } = new();

    /// <summary>
    /// 노드 캔버스 상의 각 상태 노드 커스텀 크기 (Width, Height)
    /// </summary>
    public Dictionary<string, NodeCanvasSize> NodeSizes { get; set; } = new();

    /// <summary>
    /// 핀 그래프 상의 각 노드별 위치 좌표 (X, Y)
    /// </summary>
    public Dictionary<string, NodeCanvasPosition> PinGraphNodePositions { get; set; } = new();

    /// <summary>
    /// 핀 그래프 상의 각 노드별 커스텀 크기 (Width, Height)
    /// </summary>
    public Dictionary<string, NodeCanvasSize> PinGraphNodeSizes { get; set; } = new();

    /// <summary>
    /// 핀 그래프 상의 연속 키 입력 묶음 등 노드별 접힘/펼침 상태 (nodeId -> isCollapsed)
    /// </summary>
    public Dictionary<string, bool> PinGraphCollapsedNodes { get; set; } = new();

    /// <summary>
    /// 새로 추가할 상태 노드의 추천 기본 명칭을 생성합니다. (예: "상태 1", "상태 2"...)
    /// 전역(Global)이나 기본 상태를 제외하고, 사용되지 않은 가장 작은 양의 정수(1부터 시작)를 찾아 번호를 순차 부여합니다.
    /// </summary>
    public string GenerateNextStateName(bool isKorean)
    {
        int index = 1;
        while (States != null && States.Any(s => s.Equals(isKorean ? $"상태 {index}" : $"State {index}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }
        return isKorean ? $"상태 {index}" : $"State {index}";
    }

    /// <summary>
    /// 입력받은 상태명이 기존 목록과 중복되는 경우, 안전하게 고유한 번호를 덧붙여 반환합니다.
    /// </summary>
    public string ResolveUniqueStateName(string desiredName, bool isKorean = true)
    {
        if (string.IsNullOrWhiteSpace(desiredName)) return GenerateNextStateName(isKorean);
        string clean = desiredName.Trim();
        if (States == null || !States.Any(s => s.Equals(clean, StringComparison.OrdinalIgnoreCase)))
        {
            return clean;
        }

        int counter = 1;
        string candidate = $"{clean} {counter}";
        while (States.Any(s => s.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
            candidate = $"{clean} {counter}";
        }
        return candidate;
    }
}

/// <summary>
/// 노드 캔버스 상의 2D 좌표
/// </summary>
public class NodeCanvasPosition
{
    public float X { get; set; }
    public float Y { get; set; }

    public NodeCanvasPosition() { }
    public NodeCanvasPosition(float x, float y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// 노드 캔버스 상의 2D 크기
/// </summary>
public class NodeCanvasSize
{
    public float Width { get; set; }
    public float Height { get; set; }

    public NodeCanvasSize() { }
    public NodeCanvasSize(float width, float height)
    {
        Width = width;
        Height = height;
    }
}

/// <summary>
/// 노드 및 이벤트 복사/붙여넣기용 클립보드 데이터 모델입니다.
/// </summary>
public class NodeClipboardEnvelope
{
    public const string MagicHeader = "RpgMacroStudio.NodePayload.v5";

    public string Header { get; set; } = MagicHeader;
    public string Kind { get; set; } = "State"; // "State" or "Event"
    public string? StateName { get; set; }
    public NodeCanvasPosition? Position { get; set; }
    public NodeCanvasSize? Size { get; set; }
    public List<MacroEvent> Events { get; set; } = new();
    public MacroEvent? SingleEvent { get; set; }
}
