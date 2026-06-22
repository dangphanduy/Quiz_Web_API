using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetResponseAsync(string prompt, string context, string? pdfUrl = null, string? systemInstruction = null)
    {
        try
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"] ?? "gemini-1.5-flash";

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_GEMINI_API_KEY")
            {
                return "Cấu hình API Key của Gemini chưa hợp lệ hoặc trống. Vui lòng cập nhật cấu hình API Key trong file settings.";
            }

            var url = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={apiKey}";

            var sysInstruction = !string.IsNullOrEmpty(systemInstruction) ? systemInstruction : 
                                 "Bạn là AI Course Assistant - Trợ lý tư vấn khóa học thông minh của hệ thống. " +
                                 "Nhiệm vụ của bạn chỉ là: gợi ý khóa học phù hợp từ thông tin được cung cấp, tóm tắt mô tả khóa học, giải thích sơ lược nội dung khóa học, " +
                                 "và hướng dẫn thao tác trong hệ thống cho học viên.\n" +
                                 "QUY TẮC NGHIÊM NGẶT:\n" +
                                 "1. Bạn tuyệt đối KHÔNG ĐƯỢC giải bài tập, không trả lời các câu hỏi quiz, không tiết lộ đáp án các câu hỏi thi hay kiểm tra dưới mọi hình thức.\n" +
                                 "2. Nếu người dùng hỏi câu hỏi thi, quiz hoặc đáp án, hãy lịch sự từ chối và hướng dẫn họ tự làm bài để ôn tập kiến thức.\n" +
                                 "3. Tuyệt đối KHÔNG hiển thị ID của khóa học (ví dụ: ID: 5, (ID: 5),...) trong câu trả lời gửi cho người dùng. Hãy ẩn thông tin ID này đi.\n" +
                                 "4. Trả lời bằng tiếng Việt, tự nhiên, ngắn gọn và tập trung vào khóa học.";

            var partsList = new List<object>
            {
                new { text = $"[Chỉ thị hệ thống / Quy tắc bắt buộc]:\n{sysInstruction}\n\n[Bối cảnh / Ngữ cảnh]:\n{context}\n\n[Câu hỏi từ người dùng]:\n{prompt}" }
            };

            if (!string.IsNullOrEmpty(pdfUrl))
            {
                try
                {
                    _logger.LogInformation("Downloading PDF file for Gemini inlineData from: {Url}", pdfUrl);
                    var pdfBytes = await _httpClient.GetByteArrayAsync(pdfUrl);
                    if (pdfBytes != null && pdfBytes.Length > 0)
                    {
                        var base64Data = Convert.ToBase64String(pdfBytes);
                        partsList.Add(new
                        {
                            inlineData = new
                            {
                                mimeType = "application/pdf",
                                data = base64Data
                            }
                        });
                        _logger.LogInformation("Successfully downloaded and attached PDF ({Size} bytes).", pdfBytes.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download and attach PDF from {Url}", pdfUrl);
                }
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = partsList.ToArray()
                    }
                }
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            _logger.LogInformation("Request body sent to Gemini: {JsonContent}", jsonContent);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation($"Calling Gemini API with model {model}...");
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Gemini API error for model {model}: {response.StatusCode} - {errorMsg}");

                // Fallback mechanism: try fallback models in sequence
                var fallbackModels = new List<string> { "gemini-2.5-flash", "gemini-3.1-flash-lite", "gemini-2.5-flash-lite" };
                foreach (var fallbackModel in fallbackModels)
                {
                    if (fallbackModel == model) continue;

                    _logger.LogWarning($"Primary model {model} failed. Attempting fallback to {fallbackModel}...");
                    try
                    {
                        var fallbackUrl = $"https://generativelanguage.googleapis.com/v1/models/{fallbackModel}:generateContent?key={apiKey}";
                        var fallbackContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                        var fallbackResponse = await _httpClient.PostAsync(fallbackUrl, fallbackContent);
                        if (fallbackResponse.IsSuccessStatusCode)
                        {
                            var fallbackResponseJson = await fallbackResponse.Content.ReadAsStringAsync();
                            dynamic? fallbackResult = JsonConvert.DeserializeObject(fallbackResponseJson);
                            string? fallbackResponseText = fallbackResult?.candidates?[0]?.content?.parts?[0]?.text;
                            if (!string.IsNullOrEmpty(fallbackResponseText))
                            {
                                _logger.LogInformation($"Successfully got response using fallback model {fallbackModel}.");
                                return fallbackResponseText;
                            }
                        }
                        else
                        {
                            var fallbackErrorMsg = await fallbackResponse.Content.ReadAsStringAsync();
                            _logger.LogError($"Gemini API fallback error ({fallbackModel}): {fallbackResponse.StatusCode} - {fallbackErrorMsg}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Exception occurred during fallback to {fallbackModel}");
                    }
                }

                return "Đã xảy ra lỗi khi kết nối tới dịch vụ AI. Vui lòng thử lại sau.";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            dynamic? result = JsonConvert.DeserializeObject(responseJson);

            string? responseText = result?.candidates?[0]?.content?.parts?[0]?.text;
            return responseText ?? "Không thể nhận diện câu trả lời từ AI.";
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Gemini API call timed out.");
            return "Rất tiếc, dịch vụ AI phản hồi quá lâu. Bạn có thể thử lại với câu hỏi cụ thể hơn.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in GeminiService.");
            return "Đã xảy ra lỗi hệ thống trong quá trình kết nối AI. Vui lòng kiểm tra lại.";
        }
    }
}
