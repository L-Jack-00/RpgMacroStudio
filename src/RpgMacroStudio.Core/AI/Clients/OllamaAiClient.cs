using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RpgMacroHarness.AI.Clients;

/// <summary>
/// 사용자 로컬 PC에서 완전 무료/오프라인으로 구동되는 Ollama REST API 클라이언트입니다.
/// 외부 API 통신이나 토큰 비용이 0원이며, 로컬 Qwen2.5-VL 또는 Llama3 등을 지원합니다.
/// </summary>
public class OllamaAiClient : IAiProviderClient
{
    private readonly AiProviderConfig _config;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };

    public AiProviderType ProviderType => AiProviderType.Ollama;

    public OllamaAiClient(AiProviderConfig config)
    {
        _config = config;
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            // Ollama 서버 생존 여부 확인
            string endpoint = _config.GetEffectiveEndpoint();
            string url = $"{endpoint}/api/tags";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var res = await _httpClient.SendAsync(req, ct);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, byte[]? imageBytes = null, CancellationToken ct = default)
    {
        string model = string.IsNullOrWhiteSpace(_config.ModelName) ? AiProviderConfig.GetDefaultModel(AiProviderType.Ollama) : _config.ModelName;
        string endpoint = _config.GetEffectiveEndpoint();
        string url = $"{endpoint}/api/chat";

        var userMessage = new System.Collections.Generic.Dictionary<string, object>
        {
            { "role", "user" },
            { "content", userPrompt }
        };

        if (imageBytes != null && imageBytes.Length > 0)
        {
            userMessage["images"] = new[] { Convert.ToBase64String(imageBytes) };
        }

        var requestBody = new
        {
            model = model,
            stream = false,
            format = "json",
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                userMessage
            }
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(url, content, ct);
        string responseString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Ollama 로컬 API 호출 실패 (HTTP {(int)response.StatusCode}): {responseString}");
        }

        using var doc = JsonDocument.Parse(responseString);
        if (doc.RootElement.TryGetProperty("message", out var msgObj) &&
            msgObj.TryGetProperty("content", out var contentProp))
        {
            return contentProp.GetString() ?? "";
        }

        throw new InvalidOperationException($"Ollama 응답 구조 파싱 실패: {responseString}");
    }
}
