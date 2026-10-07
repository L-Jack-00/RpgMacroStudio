using System;
using System.Threading;
using System.Threading.Tasks;

namespace RpgMacroHarness.AI;

/// <summary>
/// 각 AI 프로바이더(Gemini, OpenAI, Claude, DeepSeek, Ollama, Mock)를 단일하게 제어하는 공통 클라이언트 인터페이스입니다.
/// </summary>
public interface IAiProviderClient
{
    /// <summary>
    /// 프로바이더 타입
    /// </summary>
    AiProviderType ProviderType { get; }

    /// <summary>
    /// 시스템 프롬프트, 사용자 프롬프트 및 선택적 스크린샷 이미지를 전달하여 AI 텍스트 응답(JSON 등)을 생성합니다.
    /// </summary>
    Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, byte[]? imageBytes = null, CancellationToken ct = default);

    /// <summary>
    /// 등록된 API Key 및 연결 상태가 유효한지 가볍게 테스트합니다.
    /// </summary>
    Task<bool> ValidateApiKeyAsync(CancellationToken ct = default);
}
