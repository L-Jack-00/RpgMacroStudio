using System.IO;
using System.Media;
using System.Text.Json;

namespace RpgMacroHarness.Data;

/// <summary>
/// 프로그램 전역 설정 (음소거, 언어 설정 등)을 관리하고 영구 저장합니다.
/// </summary>
public class AppSettings
{
    private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_settings.json");
    private static AppSettings? _current;

    public static AppSettings Current
    {
        get
        {
            if (_current == null)
            {
                Load();
            }
            return _current!;
        }
    }

    /// <summary>
    /// 효과음 및 비프음 음소거 여부
    /// </summary>
    public bool MuteSound { get; set; } = false;

    /// <summary>
    /// UI 언어 ("ko" = 한국어, "en" = English)
    /// </summary>
    public string Language { get; set; } = "ko";

    /// <summary>
    /// UI 테마 ("Dark" = 다크 모드, "Light" = 라이트 모드)
    /// </summary>
    public string ThemeMode { get; set; } = "Dark";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDarkMode => !ThemeMode.Equals("Light", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 매크로 녹화 시작/중지 단축키 (기본: F9)
    /// </summary>
    public string RecordHotkey { get; set; } = "F9";

    /// <summary>
    /// 시작 / 일시정지 단축키 (기본: F10)
    /// </summary>
    public string StartPauseHotkey { get; set; } = "F10";

    /// <summary>
    /// 완전 정지 단축키 (기본: F11)
    /// </summary>
    public string StopHotkey { get; set; } = "F11";

    /// <summary>
    /// 긴급 비상정지 단축키 (기본: F12)
    /// </summary>
    public string EmergencyStopHotkey { get; set; } = "F12";

    /// <summary>
    /// 하단 로그창 접힘(축소) 여부 기억
    /// </summary>
    public bool IsLogCollapsed { get; set; } = false;

    /// <summary>
    /// 노드 뷰 / 클래식 뷰 전환 상태 기억 (false = 노드 캔버스 뷰, true = 클래식 리스트 뷰)
    /// </summary>
    public bool IsClassicView { get; set; } = false;

    /// <summary>
    /// 메인 윈도우 창 너비
    /// </summary>
    public int WindowWidth { get; set; } = 1280;

    /// <summary>
    /// 메인 윈도우 창 높이
    /// </summary>
    public int WindowHeight { get; set; } = 800;

    /// <summary>
    /// 메인 윈도우 X 위치 (-1: 기본 가운데 정렬)
    /// </summary>
    public int WindowX { get; set; } = -1;

    /// <summary>
    /// 메인 윈도우 Y 위치 (-1: 기본 가운데 정렬)
    /// </summary>
    public int WindowY { get; set; } = -1;

    /// <summary>
    /// 메인 윈도우 최대화 상태 여부
    /// </summary>
    public bool IsWindowMaximized { get; set; } = false;

    /// <summary>
    /// 스플리터 구분 위치
    /// </summary>
    public int SplitterDistance { get; set; } = -1;

    /// <summary>
    /// 최초 실행 온보딩 시작 가이드(Get Started) 완료 여부
    /// </summary>
    public bool HasCompletedGetStarted { get; set; } = false;

    /// <summary>
    /// 도우미 마법사를 1회 이상 열어보았는지 여부 (1회 열람 시 툴바 버튼 색상을 일반 버튼 색상으로 영구 변경)
    /// </summary>
    public bool HasOpenedTemplateWizard { get; set; } = false;

    /// <summary>
    /// AI 프로바이더 타입 ("Gemini", "OpenAI", "Claude", "DeepSeek", "Ollama", "MockTest")
    /// </summary>
    public string AiProvider { get; set; } = "Gemini";

    /// <summary>
    /// 사용자 본인의 AI API Key (Bring-Your-Own-Key, 로컬에만 영구 저장)
    /// </summary>
    public string AiApiKey { get; set; } = "";

    /// <summary>
    /// 사용자 지정 모델명 (비어있으면 기본 추천 모델 사용)
    /// </summary>
    public string AiModelName { get; set; } = "";

    /// <summary>
    /// 사용자 정의 엔드포인트 URL (Ollama 등 로컬 또는 커스텀 프록시 주소)
    /// </summary>
    public string AiCustomEndpoint { get; set; } = "";

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                _current = JsonSerializer.Deserialize<AppSettings>(json);
            }
        }
        catch
        {
            // 불러오기 실패 시 기본 설정 사용
        }

        _current ??= new AppSettings();
    }

    public static void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Current, options);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // 저장 실패 무시
        }
    }

    /// <summary>
    /// 음소거가 아닐 때만 소리를 재생합니다.
    /// </summary>
    public static void PlaySound(SystemSound sound)
    {
        if (Current.MuteSound) return;
        try
        {
            sound.Play();
        }
        catch { }
    }
}
