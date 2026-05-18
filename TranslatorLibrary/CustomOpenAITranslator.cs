using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TranslatorLibrary
{
    public class CustomOpenAITranslator : ITranslator
    {
        public const string TextPlaceholder = "{text}";
        public const string DefaultPromptTemplate = "<|im_start|>system\n你是一个轻小说翻译模型...\n<|im_end|>\n<|im_start|>user\n将下面的日文文本翻译成中文：{text}\n<|im_end|>\n<|im_start|>assistant";

        private const string ChatCompletionsPath = "/chat/completions";

        private string apiKey = string.Empty;
        private string apiUrl = string.Empty;
        private string modelName = string.Empty;
        private string promptTemplate = DefaultPromptTemplate;
        private double temperature = 0.2;
        private string errorInfo = string.Empty;

        public void TranslatorInit(string param1, string param2)
        {
            apiKey = param1 ?? string.Empty;
            apiUrl = param2 ?? string.Empty;
        }

        public void Configure(string key, string url, string model, string prompt, double temp)
        {
            apiKey = key ?? string.Empty;
            apiUrl = url ?? string.Empty;
            modelName = model ?? string.Empty;
            promptTemplate = prompt ?? string.Empty;
            temperature = Math.Max(0, Math.Min(2, temp));
        }

        public async Task<string> TranslateAsync(string sourceText, string desLang, string srcLang)
        {
            errorInfo = string.Empty;

            if (string.IsNullOrEmpty(sourceText) || string.IsNullOrEmpty(desLang) || string.IsNullOrEmpty(srcLang))
            {
                errorInfo = "Param Missing";
                return null;
            }

            if (!TryBuildChatCompletionsUri(apiUrl, out var uri, out errorInfo))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(modelName))
            {
                errorInfo = "Model Name is empty.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(promptTemplate))
            {
                errorInfo = "Prompt Template is empty.";
                return null;
            }

            var prompt = BuildPrompt(promptTemplate, sourceText);
            var completionRequest = new ChatCompletionRequest
            {
                Model = modelName.Trim(),
                Temperature = temperature,
                Stream = false,
                Messages = new List<ChatMessageRequest>
                {
                    new ChatMessageRequest
                    {
                        Role = "system",
                        Content = "你是一个专业翻译引擎。"
                    },
                    new ChatMessageRequest
                    {
                        Role = "user",
                        Content = prompt
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            }

            request.Content = new StringContent(JsonSerializer.Serialize(completionRequest), Encoding.UTF8, "application/json");

            try
            {
                using (request)
                {
                    var httpClient = CommonFunction.GetHttpClient();
                    using var response = await httpClient.SendAsync(request);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        errorInfo = $"HTTP {(int)response.StatusCode} ({response.StatusCode})\n{responseBody}";
                        return null;
                    }

                    ChatCompletionResponse completionResponse;
                    try
                    {
                        completionResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseBody);
                    }
                    catch (JsonException ex)
                    {
                        errorInfo = $"JSON parse failed: {ex.Message}\n{responseBody}";
                        return null;
                    }

                    if (completionResponse?.Choices == null || completionResponse.Choices.Count == 0)
                    {
                        errorInfo = "choices[0].message.content not found.";
                        return null;
                    }

                    var content = completionResponse.Choices[0]?.Message?.Content;
                    if (content == null)
                    {
                        errorInfo = "choices[0].message.content not found.";
                        return null;
                    }

                    return content.Trim();
                }
            }
            catch (Exception ex)
            {
                errorInfo = ex.Message;
                return null;
            }
        }

        public string GetLastError()
        {
            return errorInfo;
        }

        public static async Task<ModelListResult> GetModelsAsync(string url, string key)
        {
            if (!TryBuildModelsUri(url, out var uri, out var error))
            {
                return ModelListResult.Fail(error);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrWhiteSpace(key))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
            }

            try
            {
                using (request)
                {
                    var httpClient = CommonFunction.GetHttpClient();
                    using var response = await httpClient.SendAsync(request);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        return ModelListResult.Fail($"HTTP {(int)response.StatusCode} ({response.StatusCode})\n{responseBody}");
                    }

                    ModelsResponse modelsResponse;
                    try
                    {
                        modelsResponse = JsonSerializer.Deserialize<ModelsResponse>(responseBody);
                    }
                    catch (JsonException ex)
                    {
                        return ModelListResult.Fail($"JSON parse failed: {ex.Message}\n{responseBody}");
                    }

                    var models = modelsResponse?.Data?
                        .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                        .Select(x => x.Id.Trim())
                        .Distinct()
                        .ToList() ?? new List<string>();

                    if (models.Count == 0)
                    {
                        return ModelListResult.Fail("Model list is empty.");
                    }

                    return ModelListResult.Ok(models);
                }
            }
            catch (Exception ex)
            {
                return ModelListResult.Fail(ex.Message);
            }
        }

        private static string BuildPrompt(string template, string sourceText)
        {
            if (template.Contains(TextPlaceholder))
            {
                return template.Replace(TextPlaceholder, sourceText);
            }

            return template.TrimEnd() + Environment.NewLine + sourceText;
        }

        private static bool TryBuildChatCompletionsUri(string url, out Uri uri, out string error)
        {
            return TryBuildApiUri(url, true, out uri, out error);
        }

        private static bool TryBuildModelsUri(string url, out Uri uri, out string error)
        {
            return TryBuildApiUri(url, false, out uri, out error);
        }

        private static bool TryBuildApiUri(string url, bool chatCompletions, out Uri uri, out string error)
        {
            uri = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(url))
            {
                error = "API Base URL is empty.";
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var inputUri))
            {
                error = "Invalid API Base URL.";
                return false;
            }

            var builder = new UriBuilder(inputUri);
            var path = builder.Path.TrimEnd('/');

            if (chatCompletions)
            {
                if (!path.EndsWith(ChatCompletionsPath, StringComparison.OrdinalIgnoreCase))
                {
                    path = string.IsNullOrEmpty(path) ? ChatCompletionsPath : path + ChatCompletionsPath;
                }
            }
            else
            {
                if (path.EndsWith(ChatCompletionsPath, StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(0, path.Length - ChatCompletionsPath.Length);
                }

                path = string.IsNullOrEmpty(path) ? "/models" : path + "/models";
                builder.Query = string.Empty;
            }

            builder.Path = path;
            uri = builder.Uri;
            return true;
        }

        public class ModelListResult
        {
            public bool Success { get; set; }
            public List<string> Models { get; set; } = new List<string>();
            public string Error { get; set; } = string.Empty;

            public static ModelListResult Ok(List<string> models)
            {
                return new ModelListResult
                {
                    Success = true,
                    Models = models
                };
            }

            public static ModelListResult Fail(string error)
            {
                return new ModelListResult
                {
                    Success = false,
                    Error = error
                };
            }
        }

        private class ChatCompletionRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("messages")]
            public List<ChatMessageRequest> Messages { get; set; } = new List<ChatMessageRequest>();

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; } = 0.2;

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
        }

        private class ChatMessageRequest
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = string.Empty;

            [JsonPropertyName("content")]
            public string Content { get; set; } = string.Empty;
        }

        private class ChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<ChatChoiceResponse> Choices { get; set; } = new List<ChatChoiceResponse>();
        }

        private class ChatChoiceResponse
        {
            [JsonPropertyName("message")]
            public ChatMessageResponse Message { get; set; }
        }

        private class ChatMessageResponse
        {
            [JsonPropertyName("content")]
            public string Content { get; set; }
        }

        private class ModelsResponse
        {
            [JsonPropertyName("data")]
            public List<ModelInfo> Data { get; set; } = new List<ModelInfo>();
        }

        private class ModelInfo
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
        }
    }
}
