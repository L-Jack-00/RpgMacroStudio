using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RpgMacroHarness.AI.Clients;

/// <summary>
/// Google Gemini (Google AI Studio) 공식 REST API 클라이언트입니다.
/// 사용자의 개인 API 키를 직접 사용하여 구글 서버와 HTTPS로 통신합니다. (개발자 서버 미경유)
/// </summary>
public class GeminiAiClient : IAiProviderClient
{
    private readonly AiProviderConfig _config;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public AiProviderType ProviderType => AiProviderType.Gemini;

    public GeminiAiClient(AiProviderConfig config)
    {
        _config = config;
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_config.ApiKey)) return false;

        try
        {
            string model = string.IsNullOrWhiteSpace(_config.ModelName) ? AiProviderConfig.GetDefaultModel(AiProviderType.Gemini) : _config.ModelName;
            string endpoint = _config.GetEffectiveEndpoint();
            string url = $"{endpoint}/models/{model}?key={_config.ApiKey.Trim()}";

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
        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            throw new InvalidOperationException("Gemini API Key가 설정되지 않았습니다. 프로그램 설정에서 API 키를 입력해주세요.");
        }

        string model = string.IsNullOrWhiteSpace(_config.ModelName) ? AiProviderConfig.GetDefaultModel(AiProviderType.Gemini) : _config.ModelName;
        string endpoint = _config.GetEffectiveEndpoint();
        string url = $"{endpoint}/models/{model}:generateContent?key={_config.ApiKey.Trim()}";

        var userParts = new System.Collections.Generic.List<object>
        {
            new { text = userPrompt }
        };

        if (imageBytes != null && imageBytes.Length > 0)
        {
            userParts.Add(new
            {
                inline_data = new
                {
                    mime_type = "image/png",
                    data = Convert.ToBase64String(imageBytes)
                }
            });
        }

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = userParts
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                response_mime_type = "application/json"
            }
        };

        string jsonPayload = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(url, content, ct);
        string responseString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Gemini API 호출 실패 (HTTP {(int)response.StatusCode}): {responseString}");
        }

        using var doc = JsonDocument.Parse(responseString);
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var firstCandidate = candidates[0];
            if (firstCandidate.TryGetProperty("content", out var cObj) &&
                cObj.TryGetProperty("parts", out var parts) &&
                parts.GetArrayLength() > 0)
            {
                var sb = new StringBuilder();
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textProp))
                    {
                        sb.Append(textProp.GetString());
                    }
                }

                string combinedText = sb.ToString();
                if (!string.IsNullOrWhiteSpace(combinedText))
                {
                    return combinedText;
                }
            }

            if (firstCandidate.TryGetProperty("finishReason", out var reasonProp))
            {
                string reason = reasonProp.GetString() ?? "";
                if (reason == "SAFETY")
                {
                    throw new InvalidOperationException("Gemini 안전성 정책 필터에 의해 생성이 차단되었습니다. 프롬프트 내용을 조정해주세요.");
                }
                if (reason == "MAX_TOKENS")
                {
                    throw new InvalidOperationException("응답 최대 토큰 한도를 초과하여 생성이 중간에 중단되었습니다.");
                }
                if (reason != "STOP" && !string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidOperationException($"Gemini 응답 비정상 종료 (사유: {reason}). 추천 모델인 'gemini-2.0-flash'를 사용해보세요.");
                }
            }
        }

        throw new InvalidOperationException($"Gemini 응답 구조 파싱 실패 (빈 텍스트 응답): {responseString}");
    }
}
