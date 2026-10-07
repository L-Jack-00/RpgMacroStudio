using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RpgMacroHarness.AI;

/// <summary>
/// AI 프로바이더 연결 설정입니다.
/// 사용자가 설정창에서 직접 입력한 값들을 보관하며, 개발자 서버를 거치지 않고 사용자 PC에서 직접 공식 API로 통신합니다.
/// </summary>
public class AiProviderConfig
{
    public AiProviderType ProviderType { get; set; } = AiProviderType.Gemini;

    /// <summary>
    /// 사용자 본인의 API Key (Bring Your Own Key)
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// 사용할 세부 모델 이름 (예: gemini-2.0-flash, gpt-4o-mini, claude-3-5-sonnet-20241022 등)
    /// </summary>
    public string ModelName { get; set; } = "";

    /// <summary>
    /// 사용자 정의 엔드포인트 URL (Ollama 또는 사설 프록시용, 기본값이 비어있으면 프로바이더 기본 URL 사용)
    /// </summary>
    public string CustomEndpoint { get; set; } = "";

    /// <summary>
    /// 요청 타임아웃 (초 단위, 기본 60초)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 프로바이더별 권장 기본 모델 이름을 반환합니다.
    /// </summary>
    public static string GetDefaultModel(AiProviderType provider) => provider switch
    {
        AiProviderType.Gemini => "gemini-2.0-flash",
        AiProviderType.OpenAI => "gpt-4o-mini",
        AiProviderType.Claude => "claude-3-5-sonnet-20241022",
        AiProviderType.DeepSeek => "deepseek-chat",
        AiProviderType.Ollama => "llama3.2-vision",
        AiProviderType.CustomOpenAiCompatible => "gpt-4o",
        AiProviderType.MockTest => "mock-rpg-engine-v5",
        _ => "gemini-2.0-flash"
    };

    /// <summary>
    /// 프로바이더별 초보자 추천 기본 프리셋 모델 목록을 반환합니다.
    /// </summary>
    public static List<string> GetPresetModels(AiProviderType provider) => provider switch
    {
        AiProviderType.Gemini => new List<string> { "gemini-2.0-flash", "gemini-1.5-flash", "gemini-1.5-pro" },
        AiProviderType.OpenAI => new List<string> { "gpt-4o-mini", "gpt-4o", "o1-mini", "gpt-3.5-turbo" },
        AiProviderType.Claude => new List<string> { "claude-3-5-sonnet-20241022", "claude-3-5-haiku-20241022", "claude-3-opus-20240229" },
        AiProviderType.DeepSeek => new List<string> { "deepseek-chat", "deepseek-reasoner" },
        AiProviderType.Ollama => new List<string> { "llama3.2-vision", "qwen2.5-coder", "llama3.1" },
        AiProviderType.CustomOpenAiCompatible => new List<string> { "gpt-4o", "gpt-4o-mini", "claude-3-5-sonnet", "deepseek-chat" },
        AiProviderType.MockTest => new List<string> { "mock-rpg-engine-v5", "mock-fsm-generator" },
        _ => new List<string> { "gemini-2.0-flash" }
    };

    /// <summary>
    /// 프로바이더별 기본 엔드포인트 URL을 반환합니다.
    /// </summary>
    public string GetEffectiveEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(CustomEndpoint))
        {
            return CustomEndpoint.TrimEnd('/');
        }

        return ProviderType switch
        {
            AiProviderType.Gemini => "https://generativelanguage.googleapis.com/v1beta",
            AiProviderType.OpenAI => "https://api.openai.com/v1",
            AiProviderType.Claude => "https://api.anthropic.com/v1",
            AiProviderType.DeepSeek => "https://api.deepseek.com",
            AiProviderType.Ollama => "http://localhost:11434",
            AiProviderType.CustomOpenAiCompatible => "https://api.openai.com/v1",
            _ => "http://localhost:11434"
        };
    }

    /// <summary>
    /// 실제 AI 서버에 질의하여 현재 서비스 중인 최신 모델 목록을 실시간으로 가져옵니다.
    /// (서버 통신 실패 시 기본 프리셋 목록 반환)
    /// </summary>
    public async Task<List<string>> FetchAvailableModelsAsync(CancellationToken ct = default)
    {
        var resultList = new List<string>();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        try
        {
            string endpoint = GetEffectiveEndpoint();

            if (ProviderType == AiProviderType.Gemini)
            {
                if (string.IsNullOrWhiteSpace(ApiKey)) return GetPresetModels(ProviderType);
                string url = $"{endpoint}/models?key={ApiKey.Trim()}";
                var res = await client.GetAsync(url, ct);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("models", out var modelsArr))
                    {
                        foreach (var m in modelsArr.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var nameProp))
                            {
                                string name = nameProp.GetString() ?? "";
                                if (name.StartsWith("models/")) name = name.Substring("models/".Length);
                                if (!string.IsNullOrEmpty(name)) resultList.Add(name);
                            }
                        }
                    }
                }
            }
            else if (ProviderType == AiProviderType.OpenAI || ProviderType == AiProviderType.DeepSeek || ProviderType == AiProviderType.CustomOpenAiCompatible)
            {
                if (string.IsNullOrWhiteSpace(ApiKey) && ProviderType != AiProviderType.CustomOpenAiCompatible)
                    return GetPresetModels(ProviderType);

                string url = $"{endpoint}/models";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(ApiKey))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey.Trim());
                }
                var res = await client.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataArr))
                    {
                        foreach (var item in dataArr.EnumerateArray())
                        {
                            if (item.TryGetProperty("id", out var idProp))
                            {
                                string id = idProp.GetString() ?? "";
                                if (!string.IsNullOrEmpty(id)) resultList.Add(id);
                            }
                        }
                    }
                }
            }
            else if (ProviderType == AiProviderType.Ollama)
            {
                string url = $"{endpoint}/api/tags";
                var res = await client.GetAsync(url, ct);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("models", out var modelsArr))
                    {
                        foreach (var m in modelsArr.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var nameProp))
                            {
                                string name = nameProp.GetString() ?? "";
                                if (!string.IsNullOrEmpty(name)) resultList.Add(name);
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // 네트워크 오류 또는 API 키 무효 시 무시하고 프리셋 반환
        }

        if (resultList.Count == 0)
        {
            return GetPresetModels(ProviderType);
        }

        return resultList;
    }
}
