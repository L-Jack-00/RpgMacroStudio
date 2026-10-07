namespace RpgMacroHarness.AI;

/// <summary>
/// 지원하는 AI 모델 프로바이더 열거형입니다.
/// 모든 프로바이더는 사용자가 직접 발급한 API 키를 사용하는 Bring-Your-Own-Key(BYOK) 방식으로 동작합니다.
/// </summary>
public enum AiProviderType
{
    /// <summary>
    /// Google Gemini (무료 티어 제공, 멀티모달 스크린샷 비전 인식에 최적)
    /// </summary>
    Gemini,

    /// <summary>
    /// OpenAI (GPT-4o, GPT-4o-mini 등 정밀한 JSON 구조 출력에 최적)
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic Claude (Claude 3.5 Sonnet 등 복잡한 FSM 및 추론 능력 우수)
    /// </summary>
    Claude,

    /// <summary>
    /// DeepSeek (DeepSeek-V3 / R1 - OpenAI 호환 저비용 고성능 모델)
    /// </summary>
    DeepSeek,

    /// <summary>
    /// Ollama (사용자 로컬 PC에서 완전 무료/오프라인으로 구동되는 오픈소스 LLM)
    /// </summary>
    Ollama,

    /// <summary>
    /// 사용자 정의 / OpenAI 호환 (Custom URL & Model - 미래 신규 AI 및 사설 프록시 100% 호환)
    /// </summary>
    CustomOpenAiCompatible,

    /// <summary>
    /// 자체 테스트 및 오프라인 하네스 검증용 Mock 프로바이더 (API 키 불필요)
    /// </summary>
    MockTest
}
