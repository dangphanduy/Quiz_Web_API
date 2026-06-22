using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Quiz_Web.Models.EF;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Controllers.API;

[Route("api/[controller]")]
[ApiController]
public class ChatbotApiController : ControllerBase
{
    private readonly LearningPlatformContext _context;
    private readonly IGeminiService _geminiService;
    private readonly IMemoryCache _cache;

    public ChatbotApiController(LearningPlatformContext context, IGeminiService geminiService, IMemoryCache cache)
    {
        _context = context;
        _geminiService = geminiService;
        _cache = cache;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatbotRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Tin nhắn không được để trống." });
        }

        var userMessage = request.Message.Trim();

        // 1. Kiểm tra Cache
        var cacheKey = $"chatbot_{userMessage.ToLower().GetHashCode()}";
        if (_cache.TryGetValue(cacheKey, out string? cachedResponse))
        {
            return Ok(new { response = cachedResponse, fromCache = true });
        }

        // 2. Kiểm tra câu hỏi có quá chung chung hay không
        if (IsTooGeneral(userMessage))
        {
            var generalResponse = "Chào bạn! Bạn đang tìm kiếm khóa học thuộc chủ đề nào (ví dụ: frontend, backend, database, di động) hay cấp độ nào (cơ bản, nâng cao)? Hãy cung cấp thêm thông tin để mình hỗ trợ tốt nhất nhé.";
            return Ok(new { response = generalResponse, fromCache = false });
        }

        // 3. Trích xuất từ khóa từ câu hỏi người dùng
        var keywords = ExtractKeywords(userMessage);

        // 4. Query Database giới hạn số lượng kết quả (Top 5) có sử dụng Index
        var query = _context.Courses.AsNoTracking().Where(c => c.IsPublished);

        if (keywords.Any())
        {
            if (keywords.Count == 1)
            {
                var k1 = keywords[0];
                query = query.Where(c => c.Title.Contains(k1) || (c.Summary != null && c.Summary.Contains(k1)));
            }
            else if (keywords.Count == 2)
            {
                var k1 = keywords[0];
                var k2 = keywords[1];
                query = query.Where(c => c.Title.Contains(k1) || (c.Summary != null && c.Summary.Contains(k1)) ||
                                         c.Title.Contains(k2) || (c.Summary != null && c.Summary.Contains(k2)));
            }
            else
            {
                var k1 = keywords[0];
                var k2 = keywords[1];
                var k3 = keywords[2];
                query = query.Where(c => c.Title.Contains(k1) || (c.Summary != null && c.Summary.Contains(k1)) ||
                                         c.Title.Contains(k2) || (c.Summary != null && c.Summary.Contains(k2)) ||
                                         c.Title.Contains(k3) || (c.Summary != null && c.Summary.Contains(k3)));
            }
        }

        var courses = await query
            .OrderByDescending(c => c.AverageRating)
            .Take(5)
            .Select(c => new
            {
                c.CourseId,
                c.Title,
                c.Summary,
                c.Price,
                c.AverageRating,
                c.TotalReviews
            })
            .ToListAsync();

        // 5. Chuẩn bị context rút gọn để gửi qua Gemini API
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("Dưới đây là danh sách tối đa 5 khóa học phù hợp nhất từ hệ thống:");
        if (courses.Any())
        {
            foreach (var course in courses)
            {
                contextBuilder.AppendLine($"- ID: {course.CourseId}, Tên: '{course.Title}', Tóm tắt: '{course.Summary ?? "Không có"}', Giá: {course.Price:N0}đ, Đánh giá trung bình: {course.AverageRating}/5 ({course.TotalReviews} đánh giá)");
            }
        }
        else
        {
            contextBuilder.AppendLine("(Không tìm thấy khóa học nào khớp với từ khóa tìm kiếm trực tiếp trong database. Vui lòng hướng dẫn người dùng thử tìm kiếm bằng từ khóa khác hoặc tư vấn các chủ đề công nghệ chung).");
        }

        // 6. Gửi prompt và context sang Gemini API
        var responseText = await _geminiService.GetResponseAsync(userMessage, contextBuilder.ToString());

        // 7. Lưu kết quả vào Cache
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
            .SetSlidingExpiration(TimeSpan.FromMinutes(2));

        _cache.Set(cacheKey, responseText, cacheEntryOptions);

        return Ok(new { response = responseText, fromCache = false });
    }

    private bool IsTooGeneral(string message)
    {
        var generalQuestions = new[] { "tôi nên học gì", "học gì đây", "tư vấn giúp tôi", "hello", "xin chào", "hi" };
        var cleaned = message.Trim().ToLower();
        return generalQuestions.Any(q => cleaned == q || cleaned.StartsWith(q + " "));
    }

    private List<string> ExtractKeywords(string message)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tôi", "muốn", "học", "tìm", "khóa", "khoá", "nào", "sao", "cho", "người", "mới", "bắt", "đầu", "cơ", "bản", "nâng", "cao", "giúp", "tư", "vấn", "về", "có", "không", "gợi", "ý", "ở", "đâu", "chỉ", "tóm", "tắt", "thông", "tin"
        };

        var words = message.Split(new[] { ' ', ',', '.', '?', '!', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
        var keywords = new List<string>();
        foreach (var word in words)
        {
            var cleaned = word.Trim().ToLower();
            if (cleaned.Length > 1 && !stopWords.Contains(cleaned))
            {
                keywords.Add(cleaned);
            }
        }

        return keywords.Distinct().Take(3).ToList(); // Lấy tối đa 3 từ khóa chính để tối ưu query
    }
}

public class ChatbotRequest
{
    public string Message { get; set; } = string.Empty;
}
