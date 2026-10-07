using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RpgMacroHarness.AI.Clients;

/// <summary>
/// OpenAI 및 DeepSeek(OpenAI 호환) 공식 REST API 클라이언트입니다.
/// 사용자의 개인 API 키를 직접 사용하여 공급자 서버와 HTTPS로 통신합니다. (개발자 서버 미경유)
/// </summary>
public class OpenAiClient : IAiProviderClient
{
    private readonly AiProviderConfig _config;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public AiProviderType ProviderType => _config.ProviderType;

    public OpenAiClient(AiProviderConfig config)
    {
        _config = config;
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_config.ApiKey)) return false;

        try
        {
            string endpoint = _config.GetEffectiveEndpoint();
            string url = $"{endpoint}/models";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey.Trim());

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
        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            throw new InvalidOperationException($"{ProviderType} API Key가 설정되지 않았습니다. 프로그램 설정에서 API 키를 입력해주세요.");
        }

        string model = string.IsNullOrWhiteSpace(_config.ModelName) ? AiProviderConfig.GetDefaultModel(ProviderType) : _config.ModelName;
        string endpoint = _config.GetEffectiveEndpoint();
        string url = $"{endpoint}/chat/completions";

        object userContent;
        if (imageBytes != null && imageBytes.Length > 0)
        {
            userContent = new object[]
            {
                new { type = "text", text = userPrompt },
                new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}"
                    }
                }
            };
        }
        else
        {
            userContent = userPrompt;
        }

        var requestBody = new
        {
            model = model,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = (object)systemPrompt },
                new { role = "user", content = userContent }
            }
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody);
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey.Trim());
        req.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(req, ct);
        string responseString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{ProviderType} API 호출 실패 (HTTP {(int)response.StatusCode}): {responseString}");
        }

        using var doc = JsonDocument.Parse(responseString);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var msgObj) &&
                msgObj.TryGetProperty("content", out var contentProp))
            {
                return contentProp.GetString() ?? "";
            }
        }

        throw new InvalidOperationException($"{ProviderType} 응답 구조 파싱 실패: {responseString}");
    }
}
