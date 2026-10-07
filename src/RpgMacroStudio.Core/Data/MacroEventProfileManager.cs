using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RpgMacroHarness.Data;

public static class MacroEventProfileManager
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void SaveToFile(string filePath, MacroEventProfile profile)
    {
        string json = JsonSerializer.Serialize(profile, _jsonOptions);
        string tempPath = filePath + ".tmp";
        string backupPath = filePath + ".bak";

        // 1. 임시 파일에 먼저 기록 (정전/강제 종료 시 0바이트 손상 방지)
        File.WriteAllText(tempPath, json);

        // 2. 기존 정상 프로필이 있으면 백업 파일(.bak)로 보존
        if (File.Exists(filePath))
        {
            try
            {
                File.Copy(filePath, backupPath, true);
            }
            catch { }
        }

        // 3. 원자적(Atomic) 파일 교체
        File.Move(tempPath, filePath, true);

        // 4. 최초 저장 시에도 비상 복구를 위해 백업본 생성 보장
        if (!File.Exists(backupPath))
        {
            try
            {
                File.Copy(filePath, backupPath, true);
            }
            catch { }
        }
    }

    public static MacroEventProfile LoadFromFile(string filePath)
    {
        string backupPath = filePath + ".bak";
        string? json = null;
        MacroEventProfile? profile = null;

        // 1. 기본 프로필 파일 읽기 시도
        if (File.Exists(filePath))
        {
            try
            {
                json = File.ReadAllText(filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    profile = JsonSerializer.Deserialize<MacroEventProfile>(json, _jsonOptions);
                }
            }
            catch { }
        }

        // 2. 기본 파일 손상 또는 읽기 실패 시 .bak 백업 파일로 자동 재난 복구 시도
        if (profile == null && File.Exists(backupPath))
        {
            try
            {
                string backupJson = File.ReadAllText(backupPath);
                if (!string.IsNullOrWhiteSpace(backupJson))
                {
                    profile = JsonSerializer.Deserialize<MacroEventProfile>(backupJson, _jsonOptions);
                }
            }
            catch { }
        }

        if (profile == null)
        {
            if (!File.Exists(filePath) && !File.Exists(backupPath))
                throw new FileNotFoundException($"프로필 파일을 찾을 수 없습니다: {filePath}");

            throw new InvalidOperationException($"프로필 파일이 손상되었으며 백업본에서 복구할 수 없습니다: {filePath}");
        }

        NormalizeProfile(profile);
        return profile;
    }

    /// <summary>
    /// JSON 문자열로부터 프로필을 역직렬화하고 무결성을 정규화합니다. (AI 생성기 및 클립보드 호환)
    /// </summary>
    public static MacroEventProfile LoadFromString(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON 문자열이 비어있습니다.", nameof(json));

        var profile = JsonSerializer.Deserialize<MacroEventProfile>(json, _jsonOptions);
        if (profile == null)
            throw new InvalidOperationException("프로필 역직렬화 결과가 null입니다.");

        NormalizeProfile(profile);
        return profile;
    }

    /// <summary>
    /// 프로필을 JSON 문자열로 직렬화합니다.
    /// </summary>
    public static string SaveToString(MacroEventProfile profile)
    {
        return JsonSerializer.Serialize(profile, _jsonOptions);
    }

    /// <summary>
    /// 하위 호환성 보장 및 무결성 정규화 (전역 상태 태그, 트리거 리스트, 노드 위치 정렬)
    /// </summary>
    public static void NormalizeProfile(MacroEventProfile profile)
    {
        foreach (var evt in profile.Events)
        {
            if (evt.Triggers == null || evt.Triggers.Count == 0)
            {
                evt.Triggers = new List<MacroTrigger> { new MacroTrigger() };
            }
            if (string.IsNullOrEmpty(evt.StateTag) || MacroEventProfile.IsGlobalState(evt.StateTag))
            {
                evt.StateTag = "Global";
            }
        }

        if (profile.States == null || profile.States.Count == 0)
        {
            profile.States = new List<string> { "Global", "기본 상태" };
        }
        else
        {
            for (int i = 0; i < profile.States.Count; i++)
            {
                if (MacroEventProfile.IsGlobalState(profile.States[i]))
                {
                    profile.States[i] = "Global";
                }
            }
            if (!profile.States.Any(s => MacroEventProfile.IsGlobalState(s)))
            {
                profile.States.Insert(0, "Global");
            }
        }

        if (profile.NodePositions != null)
        {
            foreach (var key in profile.NodePositions.Keys.ToList())
            {
                if (MacroEventProfile.IsGlobalState(key) && key != "Global")
                {
                    profile.NodePositions["Global"] = profile.NodePositions[key];
                    profile.NodePositions.Remove(key);
                }
            }
        }

        if (profile.Variables == null)
        {
            profile.Variables = new Dictionary<string, double>();
        }
    }

    public static MacroEventProfile CreateDefaultProfile()
    {
        return new MacroEventProfile
        {
            ProfileName = "새 이벤트 프로필",
            EmergencyStopKey = "F12",
            CheckIntervalMs = 100,
            States = new List<string> { "Global", "기본 상태" },
            InitialState = "기본 상태",
            Events = new List<MacroEvent>() // 완전히 비어있는 상태로 시작
        };
    }

    public static MacroEventProfile CreateSampleProfile()
    {
        return new MacroEventProfile
        {
            ProfileName = "RPG 기본 템플릿 프로필 (스마트 FSM)",
            EmergencyStopKey = "F12",
            CheckIntervalMs = 100,
            Events = new List<MacroEvent>
            {
                new MacroEvent
                {
                    Name = "체력 40% 미만 시 물약 복용",
                    IsEnabled = true,
                    IsParallel = true,
                    Trigger = new MacroTrigger
                    {
                        Type = TriggerType.GaugeBar,
                        GaugeColor = Vision.GaugeColorType.RedHp,
                        ComparisonOp = ComparisonOperator.LessThan,
                        GaugeThresholdPercent = 40.0,
                        GaugeRoi = new RoiConfig { X = 50, Y = 50, Width = 200, Height = 15 }
                    },
                    Actions = new List<MacroAction>
                    {
                        new MacroAction
                        {
                            Type = ActionType.KeyPress,
                            Key = "D1",
                            HoldTimeMs = 35,
                            PostDelayMs = 200,
                            Description = "⌨️ 키 입력: [D1] (누름 35ms, 딜레이 200ms)"
                        }
                    }
                }
            }
        };
    }
}
