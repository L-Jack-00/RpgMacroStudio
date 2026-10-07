using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RpgMacroHarness.Data;

namespace RpgMacroHarness.AI.Clients;

/// <summary>
/// 단위 테스트 및 오프라인 하네스 검증을 위한 모의 AI 클라이언트입니다.
/// 외부 네트워크 통신 및 사용자 토큰 소모 없이 정밀한 FSM 매크로 프로필 JSON을 생성하여 엔진의 무결성을 검증합니다.
/// </summary>
public class MockAiProviderClient : IAiProviderClient
{
    public AiProviderType ProviderType => AiProviderType.MockTest;

    public bool ShouldFailValidation { get; set; } = false;
    public bool ReturnMalformedJson { get; set; } = false;

    public Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        return Task.FromResult(!ShouldFailValidation);
    }

    public Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, byte[]? imageBytes = null, CancellationToken ct = default)
    {
        if (ReturnMalformedJson)
        {
            return Task.FromResult("이것은 유효하지 않은 JSON 응답입니다: { error: broken }");
        }

        // 사용자의 의도에 부합하는 정밀한 FSM 프로필 생성
        var mockProfile = new MacroEventProfile
        {
            ProfileName = "AI 자동 생성 RPG 매크로 프로필 (v5.0.0)",
            EmergencyStopKey = "F12",
            CheckIntervalMs = 100,
            States = new System.Collections.Generic.List<string> { "Global", "기본 상태", "사냥 상태", "마을 상점 상태" },
            InitialState = "기본 상태",
            Variables = new System.Collections.Generic.Dictionary<string, double>
            {
                { "체력물약수량", 10.0 },
                { "부활횟수", 0.0 }
            }
        };

        // 1. 전역 비상 감지 이벤트
        var globalEvt = new MacroEvent
        {
            Name = "전역 체력 위험 긴급 회피",
            StateTag = "Global",
            IsEnabled = true,
            IsParallel = true,
            HumanizerJitterMs = 25,
            Triggers = new System.Collections.Generic.List<MacroTrigger>
            {
                new MacroTrigger
                {
                    Type = TriggerType.GaugeBar,
                    GaugeThresholdPercent = 25.0,
                    ComparisonOp = ComparisonOperator.LessThanOrEqual,
                    CustomColorHex = "#FF0000",
                    GaugeRoi = new RoiConfig { X = 50, Y = 50, Width = 200, Height = 20 }
                }
            },
            Actions = new System.Collections.Generic.List<MacroAction>
            {
                new MacroAction { Type = ActionType.KeyPress, Key = "D1", HoldTimeMs = 40, PostDelayMs = 100, Description = "긴급 물약 (1번 키)" },
                new MacroAction { Type = ActionType.Wait, WaitDurationMs = 300, Description = "쿨타임 대기" }
            }
        };

        // 2. 기본 상태 -> 사냥 상태 전이 이벤트
        var startHuntEvt = new MacroEvent
        {
            Name = "사냥 시작 루프 진입",
            StateTag = "기본 상태",
            IsEnabled = true,
            Triggers = new System.Collections.Generic.List<MacroTrigger>
            {
                new MacroTrigger { Type = TriggerType.TimerInterval, IntervalMs = 500 }
            },
            Actions = new System.Collections.Generic.List<MacroAction>
            {
                new MacroAction { Type = ActionType.KeyPress, Key = "Space", Description = "출발" },
                new MacroAction { Type = ActionType.ChangeState, TargetState = "사냥 상태", Description = "사냥 상태로 전환" }
            }
        };

        // 3. 사냥 상태 지속 공격 이벤트
        var huntAttackEvt = new MacroEvent
        {
            Name = "주력 스킬 자동 시전",
            StateTag = "사냥 상태",
            IsEnabled = true,
            Triggers = new System.Collections.Generic.List<MacroTrigger>
            {
                new MacroTrigger { Type = TriggerType.TimerInterval, IntervalMs = 1200 }
            },
            Actions = new System.Collections.Generic.List<MacroAction>
            {
                new MacroAction { Type = ActionType.KeyPress, Key = "A", HoldTimeMs = 50, PostDelayMs = 80, Description = "스킬 A" },
                new MacroAction { Type = ActionType.KeyPress, Key = "S", HoldTimeMs = 50, PostDelayMs = 80, Description = "스킬 S" }
            }
        };

        // 4. 물약 소진 시 마을 상점으로 이동하는 FSM 전이 이벤트
        var returnTownEvt = new MacroEvent
        {
            Name = "물약 소진 시 마을 복귀",
            StateTag = "사냥 상태",
            IsEnabled = true,
            Triggers = new System.Collections.Generic.List<MacroTrigger>
            {
                new MacroTrigger
                {
                    Type = TriggerType.VariableCheck,
                    VariableName = "체력물약수량",
                    VarCompareOp = VariableComparison.LessThanOrEqual,
                    VarTargetValue = 0.0
                }
            },
            Actions = new System.Collections.Generic.List<MacroAction>
            {
                new MacroAction { Type = ActionType.KeyPress, Key = "B", HoldTimeMs = 60, PostDelayMs = 500, Description = "귀환석 사용 (B)" },
                new MacroAction { Type = ActionType.ChangeState, TargetState = "마을 상점 상태", Description = "마을 상점 상태로 전환" }
            }
        };

        mockProfile.Events.Add(globalEvt);
        mockProfile.Events.Add(startHuntEvt);
        mockProfile.Events.Add(huntAttackEvt);
        mockProfile.Events.Add(returnTownEvt);

        string json = JsonSerializer.Serialize(mockProfile, new JsonSerializerOptions { WriteIndented = true });
        string markdownWrapped = $"```json\n{json}\n```";

        return Task.FromResult(markdownWrapped);
    }
}
