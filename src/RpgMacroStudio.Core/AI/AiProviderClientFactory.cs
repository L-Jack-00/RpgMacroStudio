using System;
using RpgMacroHarness.AI.Clients;

namespace RpgMacroHarness.AI;

/// <summary>
/// 선택된 AI 프로바이더 타입에 대응하는 IAiProviderClient 인스턴스를 동적으로 생성하는 팩토리 클래스입니다.
/// </summary>
public static class AiProviderClientFactory
{
    public static IAiProviderClient CreateClient(AiProviderConfig config)
    {
        return config.ProviderType switch
        {
            AiProviderType.Gemini => new GeminiAiClient(config),
            AiProviderType.OpenAI => new OpenAiClient(config),
            AiProviderType.Claude => new ClaudeAiClient(config),
            AiProviderType.DeepSeek => new OpenAiClient(config),
            AiProviderType.Ollama => new OllamaAiClient(config),
            AiProviderType.CustomOpenAiCompatible => new OpenAiClient(config),
            AiProviderType.MockTest => new MockAiProviderClient(),
            _ => throw new NotSupportedException($"지원되지 않는 AI 프로바이더입니다: {config.ProviderType}")
        };
    }
}
