using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.AI;

/// <summary>
/// AI 프로필 생성 결과 객체입니다.
/// </summary>
public class AiGenerationResult
{
    public bool Success { get; set; }
    public MacroEventProfile? Profile { get; set; }
    public string RawJson { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string Summary { get; set; } = "";
}

/// <summary>
/// 사용자의 자연어 프롬프트와 (선택적) 게임 화면 스냅샷을 분석하여,
/// RpgMacroStudio v5.0.0 호환 FSM 매크로 프로필을 전자동으로 생성하고 무결성을 검증·배치하는 생성기 엔진입니다.
/// </summary>
public class AiProfileGenerator
{
    private readonly IAiProviderClient _client;

    public AiProfileGenerator(IAiProviderClient client)
    {
        _client = client;
    }

    /// <summary>
    /// RpgMacroStudio v5.0.0 시스템 프롬프트를 생성합니다.
    /// 엄격한 JSON 스키마 규약 및 FSM 상태 전이 패턴을 안내합니다.
    /// </summary>
    public static string BuildSystemPrompt()
    {
        return """
You are the Lead Macro Architect for RpgMacroStudio v5.0.0, an advanced C# .NET 9.0 WinForms automation tool.
Generate a valid, defect-free MacroEventProfile JSON object based on the user's instructions.

CRITICAL JSON SCHEMA RULES:
1. Root Object: "MacroEventProfile"
   - ProfileName: string
   - EmergencyStopKey: string (e.g. "F12")
   - CheckIntervalMs: integer (recommended: 100)
   - States: array of strings (e.g. ["Global", "기본 상태", "사냥 상태", "마을 상점 상태"])
   - InitialState: string (must be in States, cannot be "Global")
   - Variables: object with numeric values (e.g. {"HpPotion": 10.0, "DeathCount": 0.0})
   - Events: array of MacroEvent objects

2. MacroEvent Object:
   - Id: string (GUID format)
   - Name: string (descriptive name)
   - IsEnabled: boolean (true)
   - IsParallel: boolean (true)
   - HumanizerJitterMs: integer (20 to 45 for anti-ban humanized timing)
   - StateTag: string (must be "Global" or match one of the defined States)
   - Triggers: array of MacroTrigger objects (AND combined)
   - Actions: array of MacroAction objects (Executed sequentially)

3. MacroTrigger Object:
   - Type: One of ["Always", "TimerInterval", "GaugeBar", "VisionDetection", "VariableCheck"]
   - If Type == "TimerInterval": IntervalMs (e.g. 1000)
   - If Type == "GaugeBar": CustomColorHex (e.g. "#FF0000"), GaugeThresholdPercent (e.g. 30.0), ComparisonOp ("LessThanOrEqual", "LessThan", "GreaterThan", "GreaterThanOrEqual"), GaugeRoi ({ "X": 100, "Y": 50, "Width": 200, "Height": 15 })
   - If Type == "VariableCheck": VariableName, VarCompareOp ("Equal", "NotEqual", "GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual"), VarTargetValue (number)

4. MacroAction Object:
   - Type: One of ["KeyPress", "Wait", "MouseClick", "VariableControl", "ChangeState"]
   - If KeyPress: Key (e.g. "D1", "Space", "A"), HoldTimeMs (30~60), PostDelayMs (60~120), Description
   - If Wait: WaitDurationMs (e.g. 500), Description
   - If MouseClick: MouseButton ("Left", "Right"), TargetMode ("Coordinates", "TargetImage", "TriggerDetected"), CustomX, CustomY, Description
   - If VariableControl: VariableName, VariableOp ("Set", "Add", "Subtract"), VariableValue (number), Description
   - If ChangeState: TargetState (must be one of the defined States), Description

OUTPUT REQUIREMENT:
Return ONLY the raw JSON or a JSON code block (```json ... ```). Do not include conversational markdown before or after the JSON.
""";
    }

    /// <summary>
    /// 사용자 프롬프트와 선택적 스크린샷 이미지를 분석하여 FSM 프로필을 생성하고 자동 레이아웃을 부여합니다.
    /// </summary>
    public async Task<AiGenerationResult> GenerateProfileAsync(
        string userPrompt,
        byte[]? screenshotBytes = null,
        CancellationToken ct = default)
    {
        var result = new AiGenerationResult();

        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            result.Success = false;
            result.ErrorMessage = "사용자 요구사항 프롬프트가 비어 있습니다.";
            return result;
        }

        try
        {
            string systemPrompt = BuildSystemPrompt();
            string responseText = await _client.GenerateTextAsync(systemPrompt, userPrompt, screenshotBytes, ct);
            result.RawJson = responseText;

            // 마크다운 코드 블록(```json ... ```) 정제
            string cleanJson = ExtractJsonString(responseText);

            // RpgMacroStudio 정규 매니저를 통한 무결성 로드 검증
            var profile = MacroEventProfileManager.LoadFromString(cleanJson);
            if (profile == null)
            {
                result.Success = false;
                result.ErrorMessage = "JSON 역직렬화 결과가 null입니다.";
                return result;
            }

            // 필수 상태 점검 및 보정
            if (profile.States == null || profile.States.Count == 0)
            {
                profile.States = new List<string> { "Global", "기본 상태" };
            }
            if (!profile.States.Exists(MacroEventProfile.IsGlobalState))
            {
                profile.States.Insert(0, "Global");
            }
            if (string.IsNullOrWhiteSpace(profile.InitialState) || MacroEventProfile.IsGlobalState(profile.InitialState))
            {
                profile.InitialState = profile.States.Find(s => !MacroEventProfile.IsGlobalState(s)) ?? "기본 상태";
            }

            // 노드 캔버스 스마트 위상 정렬 및 충돌 방지 레이아웃 자동 계산 (MacroLayoutEngine 연동)
            MacroLayoutEngine.ComputeSmartLayout(profile);

            result.Success = true;
            result.Profile = profile;
            result.Summary = $"상태 {profile.States.Count}개, 이벤트 {profile.Events.Count}개, 변수 {profile.Variables.Count}개 자동 생성 및 위상 배치 완료";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"AI 프로필 생성 파이프라인 실패: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// 응답 텍스트에서 순수 JSON 문자열을 추출합니다.
    /// </summary>
    public static string ExtractJsonString(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";

        // ```json ... ``` 패턴 매칭
        var match = Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // 중괄호 { ... } 최외각 영역 탐색
        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return text.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
        }

        return text.Trim();
    }
}
