using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nelir.Models;

namespace Nelir.Services
{
    public class AiSuggestionService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        
        private List<string> _cachedFreeModels = new();
        private DateTime _cacheExpiration = DateTime.MinValue;
        private readonly object _cacheLock = new object();

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cachedFreeModels.Clear();
                _cacheExpiration = DateTime.MinValue;
            }
        }

        public async Task<List<string>> GetFreeModelsExternalAsync(string apiKey)
        {
            return await GetFreeModelsAsync(apiKey);
        }

        public async Task<AiSuggestionResult> GetSuggestionAsync(
            string apiKey,
            string currentRawText,
            string currentSpeaker,
            List<(string raw, string mtl)> precedingContext,
            IProgress<string> progress,
            List<string> preferredModels,
            CancellationToken externalCt)
        {
            var freeModels = await GetFreeModelsAsync(apiKey);
            if (freeModels.Count == 0)
                throw new AiSuggestionException("Không tìm thấy model free nào khả dụng trên OpenRouter.");

            // Build list of models to try, starting with whitelisted preferences
            var modelsToTry = new List<string>();
            foreach (var pref in preferredModels)
            {
                if (!string.IsNullOrWhiteSpace(pref) && !modelsToTry.Contains(pref))
                {
                    modelsToTry.Add(pref);
                }
            }

            // Fallback: Add all other free models not in the preferences
            foreach (var model in freeModels)
            {
                if (!modelsToTry.Contains(model))
                {
                    modelsToTry.Add(model);
                }
            }

            if (modelsToTry.Count == 0)
                throw new AiSuggestionException("Không có model free nào khả dụng để thực hiện gợi ý dịch.");

            var prompt = BuildPrompt(currentRawText, currentSpeaker, precedingContext);
            Exception? lastError = null;

            int modelsTried = 0;
            foreach (var model in modelsToTry)
            {
                if (modelsTried >= 5) break;
                modelsTried++;

                progress.Report($"Đang gọi model: {model}...");

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt, timeoutCts.Token);
                try
                {
                    var raw = await CallChatCompletionAsync(apiKey, model, prompt, linkedCts.Token);
                    var parsed = ParseStructuredResponse(raw);
                    parsed.ModelUsed = model;
                    return parsed;
                }
                catch (OperationCanceledException) when (!externalCt.IsCancellationRequested)
                {
                    lastError = new TimeoutException($"Model {model} không phản hồi trong 15s.");
                    progress.Report($"Model {model} hết thời gian chờ (15s).");
                }
                catch (HttpRequestException ex) when (IsRetryableStatus(ex))
                {
                    lastError = ex;
                    progress.Report($"Model {model} lỗi mạng/quá tải ({(ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : "HTTP Err")}).");
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && externalCt.IsCancellationRequested))
                {
                    lastError = ex;
                    progress.Report($"Model {model} lỗi: {ex.Message}");
                }
            }

            throw new AiSuggestionException("Tất cả các mô hình đề xuất đều thất bại hoặc quá thời gian chờ.", lastError);
        }

        private async Task<List<string>> GetFreeModelsAsync(string apiKey)
        {
            lock (_cacheLock)
            {
                if (_cachedFreeModels.Count > 0 && DateTime.UtcNow < _cacheExpiration)
                {
                    return _cachedFreeModels;
                }
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("HTTP-Referer", "https://github.com/anhluong447/Nelir-RPGM-Translator");
                request.Headers.Add("X-Title", "Nelir RPGM Translator");

                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var modelsList = new List<(string id, bool isWhitelisted, int whitelistOrder)>();

                var whitelist = new List<string>
                {
                    "meta-llama/llama-3-8b-instruct:free",
                    "google/gemini-flash-1.5:free",
                    "qwen/qwen-2-7b-instruct:free",
                    "mistralai/mistral-7b-instruct:free",
                    "microsoft/phi-3-medium-128k-instruct:free"
                };

                if (doc.RootElement.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataProp.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                        {
                            string id = idProp.GetString() ?? string.Empty;
                            bool isFree = id.EndsWith(":free");

                            if (!isFree && item.TryGetProperty("pricing", out var pricingProp))
                            {
                                isFree = IsFreePricing(pricingProp);
                            }

                            if (isFree)
                            {
                                int index = whitelist.FindIndex(w => id.Contains(w) || w.Contains(id));
                                if (index >= 0)
                                {
                                    modelsList.Add((id, true, index));
                                }
                                else
                                {
                                    modelsList.Add((id, false, int.MaxValue));
                                }
                            }
                        }
                    }
                }

                var sortedModels = modelsList
                    .OrderBy(m => !m.isWhitelisted)
                    .ThenBy(m => m.whitelistOrder)
                    .Select(m => m.id)
                    .ToList();

                if (sortedModels.Count > 0)
                {
                    lock (_cacheLock)
                    {
                        _cachedFreeModels = sortedModels;
                        _cacheExpiration = DateTime.UtcNow.AddHours(1);
                    }
                }

                return sortedModels;
            }
            catch (Exception ex)
            {
                lock (_cacheLock)
                {
                    if (_cachedFreeModels.Count > 0)
                    {
                        return _cachedFreeModels;
                    }
                }
                throw new AiSuggestionException($"Lấy danh sách model từ OpenRouter thất bại: {ex.Message}", ex);
            }
        }

        private bool IsFreePricing(JsonElement pricing)
        {
            try
            {
                double promptPrice = 0;
                if (pricing.TryGetProperty("prompt", out var promptProp))
                {
                    if (promptProp.ValueKind == JsonValueKind.Number)
                        promptPrice = promptProp.GetDouble();
                    else if (promptProp.ValueKind == JsonValueKind.String)
                        double.TryParse(promptProp.GetString(), out promptPrice);
                }

                double completionPrice = 0;
                if (pricing.TryGetProperty("completion", out var completionProp))
                {
                    if (completionProp.ValueKind == JsonValueKind.Number)
                        completionPrice = completionProp.GetDouble();
                    else if (completionProp.ValueKind == JsonValueKind.String)
                        double.TryParse(completionProp.GetString(), out completionPrice);
                }

                return promptPrice == 0 && completionPrice == 0;
            }
            catch
            {
                return false;
            }
        }

        private string BuildPrompt(string currentRawText, string currentSpeaker, List<(string raw, string mtl)> precedingContext)
        {
            var sb = new System.Text.StringBuilder();

            if (precedingContext.Count > 0)
            {
                sb.AppendLine("### Ngữ cảnh 5 dòng trước:");
                int idx = 1;
                foreach (var ctx in precedingContext)
                {
                    sb.Append($"{idx}. ");
                    if (!string.IsNullOrEmpty(ctx.raw))
                    {
                        sb.Append($"RAW: \"{ctx.raw.Replace("\"", "\\\"")}\"");
                    }
                    if (!string.IsNullOrEmpty(ctx.mtl))
                    {
                        sb.Append($" | MTL tham khảo: \"{ctx.mtl.Replace("\"", "\\\"")}\"");
                    }
                    sb.AppendLine();
                    idx++;
                }
                sb.AppendLine();
            }

            sb.AppendLine("### Dòng cần dịch:");
            sb.AppendLine($"Speaker: {(string.IsNullOrEmpty(currentSpeaker) ? "(không có)" : currentSpeaker)}");
            sb.AppendLine($"RAW: \"{currentRawText.Replace("\"", "\\\"")}\"");

            return sb.ToString();
        }

        private async Task<string> CallChatCompletionAsync(string apiKey, string model, string prompt, CancellationToken ct)
        {
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "Bạn là trợ lý dịch thuật game RPG Maker từ tiếng Anh sang tiếng Việt.\nDựa trên ngữ cảnh 5 dòng thoại trước đó (RAW + bản dịch máy tham khảo nếu có) và dòng cần dịch hiện tại, hãy đề xuất 2 phương án dịch tiếng Việt khác nhau (khác nhau về văn phong/cách diễn đạt, không phải chỉ đổi từ đồng nghĩa), kèm phân tích ngắn gọn (1-2 câu) về các thuật ngữ/tên riêng/sắc thái cần lưu ý.\n\nQUAN TRỌNG VỀ ĐỊNH DẠNG: Phải giữ nguyên chính xác các thẻ định dạng đặc biệt của RPG Maker (như <BR>, <br>, hoặc các ký tự điều khiển bắt đầu bằng gạch chéo ngược như \\\\N[n], \\\\C[n], \\\\V[n], \\\\., \\\\!, \\\\|...) ở các vị trí tương thích trong bản dịch. Tuyệt đối không xóa bỏ, thay đổi hoặc dịch các thẻ này. Nếu văn bản gốc chứa ký tự xuống dòng (newlines), bản dịch cũng phải giữ nguyên các vị trí xuống dòng tương đương.\n\nTrả lời CHỈ bằng JSON theo đúng cấu trúc sau, tuyệt đối không thêm bất kỳ văn bản/markdown nào khác:\n{\n  \"options\": [\n    { \"translated_text\": \"Bản dịch 1\", \"rationale\": \"Giải thích lý do dịch 1\" },\n    { \"translated_text\": \"Bản dịch 2\", \"rationale\": \"Giải thích lý do dịch 2\" }\n  ],\n  \"terminology_notes\": \"Lưu ý thuật ngữ\"\n}" },
                    new { role = "user", content = prompt }
                },
                temperature = 0.4
            };

            string requestJson = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("HTTP-Referer", "https://github.com/anhluong447/Nelir-RPGM-Translator");
            request.Headers.Add("X-Title", "Nelir RPGM Translator");
            request.Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("choices", out var choicesProp) &&
                choicesProp.ValueKind == JsonValueKind.Array &&
                choicesProp.GetArrayLength() > 0)
            {
                var firstChoice = choicesProp[0];
                if (firstChoice.TryGetProperty("message", out var msgProp) &&
                    msgProp.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString() ?? string.Empty;
                }
            }

            throw new Exception("Cấu trúc phản hồi từ OpenRouter không hợp lệ.");
        }

        private bool IsRetryableStatus(HttpRequestException ex)
        {
            if (!ex.StatusCode.HasValue) return true;
            int code = (int)ex.StatusCode.Value;
            return code == 429 || (code >= 500 && code <= 599);
        }

        public string CleanJsonContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            content = content.Trim();

            if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                content = content.Substring(7);
                if (content.EndsWith("```"))
                {
                    content = content.Substring(0, content.Length - 3);
                }
            }
            else if (content.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                content = content.Substring(3);
                if (content.EndsWith("```"))
                {
                    content = content.Substring(0, content.Length - 3);
                }
            }
            return content.Trim();
        }

        public AiSuggestionResult ParseStructuredResponse(string rawContent)
        {
            string cleaned = CleanJsonContent(rawContent);
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var rawResult = JsonSerializer.Deserialize<OpenRouterSuggestionResponse>(cleaned, options);
                if (rawResult != null)
                {
                    var result = new AiSuggestionResult
                    {
                        TerminologyNotes = rawResult.Terminology_Notes ?? string.Empty
                    };
                    if (rawResult.Options != null)
                    {
                        foreach (var opt in rawResult.Options)
                        {
                            if (!string.IsNullOrWhiteSpace(opt.Translated_Text))
                            {
                                result.Options.Add(new AiSuggestionOption
                                {
                                    TranslatedText = opt.Translated_Text.Trim(),
                                    Rationale = opt.Rationale ?? string.Empty
                                });
                            }
                        }
                    }

                    if (result.Options.Count > 0)
                    {
                        return result;
                    }
                }
            }
            catch
            {
                // Fallback to regex manual parsing
            }

            return ParseFallbackManual(cleaned);
        }

        private AiSuggestionResult ParseFallbackManual(string text)
        {
            var result = new AiSuggestionResult();

            var matches = System.Text.RegularExpressions.Regex.Matches(text,
                @"""translated_text""\s*:\s*""([^""]+)""(?:\s*,\s*""rationale""\s*:\s*""([^""]+)"")?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string translated = match.Groups[1].Value;
                string rationale = match.Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(translated))
                {
                    result.Options.Add(new AiSuggestionOption
                    {
                        TranslatedText = translated.Replace("\\\"", "\"").Trim(),
                        Rationale = rationale.Replace("\\\"", "\"").Trim()
                    });
                }
            }

            var notesMatch = System.Text.RegularExpressions.Regex.Match(text,
                @"""terminology_notes""\s*:\s*""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (notesMatch.Success)
            {
                result.TerminologyNotes = notesMatch.Groups[1].Value.Replace("\\\"", "\"").Trim();
            }

            if (result.Options.Count > 0)
            {
                return result;
            }

            throw new Exception("Không thể phân tích kết quả gợi ý từ model AI.");
        }

        private class OpenRouterSuggestionResponse
        {
            public List<OpenRouterSuggestionOption>? Options { get; set; }
            public string? Terminology_Notes { get; set; }
        }

        private class OpenRouterSuggestionOption
        {
            public string? Translated_Text { get; set; }
            public string? Rationale { get; set; }
        }
    }
}
