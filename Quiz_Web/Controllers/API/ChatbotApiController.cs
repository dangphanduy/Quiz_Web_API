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
        var cacheKey = request.LessonId.HasValue 
            ? $"chatbot_lesson_{request.LessonId.Value}_{userMessage.ToLower().GetHashCode()}"
            : $"chatbot_{userMessage.ToLower().GetHashCode()}";

        if (_cache.TryGetValue(cacheKey, out string? cachedResponse))
        {
            return Ok(new { response = cachedResponse, fromCache = true });
        }

        // 2. Kiểm tra câu hỏi có quá chung chung hay không
        if (IsTooGeneral(userMessage))
        {
            if (request.LessonId.HasValue)
            {
                var lessonGreeting = "Chào bạn! Mình là Trợ lý học tập AI. Mình có thể giúp bạn tóm tắt bài học, giải thích các khái niệm khó hoặc tài liệu PDF của bài học này. Bạn cần mình hỗ trợ gì nào?";
                return Ok(new { response = lessonGreeting, fromCache = false });
            }
            else
            {
                var generalResponse = "Chào bạn! Bạn đang tìm kiếm khóa học thuộc chủ đề nào (ví dụ: frontend, backend, database, di động) hay cấp độ nào (cơ bản, nâng cao)? Hãy cung cấp thêm thông tin để mình hỗ trợ tốt nhất nhé.";
                return Ok(new { response = generalResponse, fromCache = false });
            }
        }

        // 2.1 Kiểm tra câu hỏi có liên quan đến học tập/hệ thống hay không
        if (!IsRelevantQuery(userMessage))
        {
            var irrelevantResponse = "Chào bạn! Mình là Trợ lý học tập AI của hệ thống. Hiện tại mình chỉ hỗ trợ giải đáp các câu hỏi liên quan đến nội dung bài học, tài liệu lý thuyết, tư vấn khóa học và thông tin trên hệ thống. Bạn vui lòng đặt câu hỏi liên quan nhé! 😊";
            return Ok(new { response = irrelevantResponse, fromCache = false });
        }

        // 3. Nếu là bối cảnh bài học cụ thể
        if (request.LessonId.HasValue)
        {
            var lesson = await _context.Lessons
                .AsNoTracking()
                .Include(l => l.Chapter)
                    .ThenInclude(c => c.Course)
                .Include(l => l.LessonContents)
                .FirstOrDefaultAsync(l => l.LessonId == request.LessonId.Value);

            if (lesson != null)
            {
                var theoryContents = lesson.LessonContents
                    .Where(c => c.ContentType == "Theory")
                    .OrderBy(c => c.OrderIndex)
                    .ToList();

                if (!theoryContents.Any())
                {
                    var noTheoryResponse = "Bài học này hiện không có tài liệu lý thuyết để giải thích hoặc tóm tắt. Bạn có câu hỏi nào khác không?";
                    return Ok(new { response = noTheoryResponse, fromCache = false });
                }

                var contextBuilder = new StringBuilder();
                contextBuilder.AppendLine($"[Bối cảnh bài học đang học]:");
                contextBuilder.AppendLine($"- Khóa học: {lesson.Chapter.Course.Title}");
                contextBuilder.AppendLine($"- Chương: {lesson.Chapter.Title}");
                contextBuilder.AppendLine($"- Bài học: {lesson.Title}");
                if (!string.IsNullOrEmpty(lesson.Description))
                {
                    contextBuilder.AppendLine($"- Mô tả bài học: {lesson.Description}");
                }
                contextBuilder.AppendLine();
                contextBuilder.AppendLine("[Nội dung lý thuyết (Text)]:");

                foreach (var content in theoryContents)
                {
                    if (!string.IsNullOrEmpty(content.Body))
                    {
                        var cleanBody = System.Text.RegularExpressions.Regex.Replace(content.Body, "<.*?>", string.Empty).Trim();
                        contextBuilder.AppendLine($"-- Tiêu đề: {content.Title ?? "Nội dung"} --");
                        contextBuilder.AppendLine(cleanBody);
                        contextBuilder.AppendLine();
                    }
                }

                // Lấy file PDF đính kèm (nếu có)
                var pdfContent = theoryContents.FirstOrDefault(c => !string.IsNullOrEmpty(c.DocumentUrl) && c.DocumentUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
                var pdfUrl = pdfContent?.DocumentUrl;

                var systemInstruction = $"Bạn là AI Study Assistant - Trợ lý học tập thông minh trên hệ thống.\n" +
                                        $"Học viên đang học bài học '{lesson.Title}' thuộc chương '{lesson.Chapter.Title}', khóa học '{lesson.Chapter.Course.Title}'.\n" +
                                        $"Nhiệm vụ chính của bạn là giải thích chi tiết các kiến thức, khái niệm, thuật ngữ và tóm tắt nội dung lý thuyết hoặc tài liệu PDF đính kèm của bài học này dựa trên bối cảnh và tài liệu được cung cấp.\n" +
                                        $"QUY TẮC BẮT BUỘC:\n" +
                                        $"1. Tuyệt đối KHÔNG giải bài tập hộ, không trả lời các câu hỏi kiểm tra/trắc nghiệm/thi (Quiz/Test) của học viên, không tiết lộ đáp án trực tiếp. Nếu học viên hỏi đáp án bài tập hoặc quiz, hãy từ chối lịch sự và hướng dẫn họ cách tự làm, gợi ý các phần tài liệu liên quan để tự học.\n" +
                                        $"2. Trả lời bằng tiếng Việt, mạch lạc, dễ hiểu, định dạng markdown đẹp mắt (in đậm, danh sách dòng,...) để học viên dễ đọc.\n" +
                                        $"3. Tập trung sát vào nội dung bài học. Nếu học viên hỏi những câu hỏi hoàn toàn ngoài lề không liên quan tới học tập hoặc bài học này, hãy khéo léo từ chối và hướng họ quay lại bài học.";

                var responseText = await _geminiService.GetResponseAsync(userMessage, contextBuilder.ToString(), pdfUrl, systemInstruction);

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));

                _cache.Set(cacheKey, responseText, cacheEntryOptions);

                return Ok(new { response = responseText, fromCache = false });
            }
        }

        // 4. Trích xuất từ khóa từ câu hỏi người dùng (cho tìm kiếm khóa học thông thường)
        var keywords = ExtractKeywords(userMessage);

        // 5. Query Database giới hạn số lượng kết quả (Top 5) có sử dụng Index
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

        // 6. Chuẩn bị context rút gọn để gửi qua Gemini API
        var generalContextBuilder = new StringBuilder();
        generalContextBuilder.AppendLine("Dưới đây là danh sách tối đa 5 khóa học phù hợp nhất từ hệ thống:");
        if (courses.Any())
        {
            foreach (var course in courses)
            {
                generalContextBuilder.AppendLine($"- ID: {course.CourseId}, Tên: '{course.Title}', Tóm tắt: '{course.Summary ?? "Không có"}', Giá: {course.Price:N0}đ, Đánh giá trung bình: {course.AverageRating}/5 ({course.TotalReviews} đánh giá)");
            }
        }
        else
        {
            generalContextBuilder.AppendLine("(Không tìm thấy khóa học nào khớp với từ khóa tìm kiếm trực tiếp trong database. Vui lòng hướng dẫn người dùng thử tìm kiếm bằng từ khóa khác hoặc tư vấn các chủ đề công nghệ chung).");
        }

        // 7. Gửi prompt và context sang Gemini API
        var generalResponseText = await _geminiService.GetResponseAsync(userMessage, generalContextBuilder.ToString());

        // 8. Lưu kết quả vào Cache
        var generalCacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(10))
            .SetSlidingExpiration(TimeSpan.FromMinutes(2));

        _cache.Set(cacheKey, generalResponseText, generalCacheEntryOptions);

        return Ok(new { response = generalResponseText, fromCache = false });
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
    private bool IsRelevantQuery(string message)
    {
        var cleaned = message.Trim().ToLower();

        // Danh sách từ khóa liên quan đến Công nghệ / Lập trình / Bài học / Hệ thống / Xã giao bot
        var relevantKeywords = new[]
        {
            // Lập trình & Công nghệ
            "api", "sql", "c#", "csharp", "python", "java", "javascript", "js", "html", "css", "react", "vue", "angular", "database", "dữ liệu", "git", "docker", "kubernetes", "web", "frontend", "backend", "mobile", "android", "ios", "flutter", "swift", "kotlin", "lập trình", "code", "vòng lặp", "hàm", "class", "biến", "mảng", "chuỗi", "oop", "thuật toán", "cấu trúc dữ liệu", "net core", "asp.net",
            // Học tập & Bài học
            "học", "khóa học", "khoá học", "bài học", "bài tập", "lý thuyết", "tài liệu", "pdf", "slide", "video", "flashcard", "test", "quiz", "thi", "kiểm tra", "đáp án", "chương", "giáo trình", "ôn tập", "kiến thức", "tóm tắt", "giải thích", "tư vấn", "gợi ý", "lớp", "giảng viên", "thầy", "cô", "chủ đề",
            // Hệ thống & Tài khoản
            "giá", "tiền", "mua", "nạp", "đăng ký", "đăng kí", "thanh toán", "chuyển khoản", "tài khoản", "mật khẩu", "chứng chỉ", "chứng nhận", "certificate", "đánh giá", "rating", "review", "hỗ trợ", "help", "cách dùng", "thao tác", "lỗi",
            // Giao tiếp thông thường & Xã giao bot
            "chào", "hello", "hi", "cảm ơn", "cám ơn", "tạm biệt", "ok", "oke", "được không", "giúp", "trợ lý", "trợ lí", "assistant", "chatbot", "ai", "bạn là", "mày là", "là ai", "giới thiệu"
        };

        // Nếu câu hỏi chứa bất kỳ từ khóa liên quan nào, coi là hợp lệ
        return relevantKeywords.Any(keyword => cleaned.Contains(keyword));
    }
}

public class ChatbotRequest
{
    public string Message { get; set; } = string.Empty;
    public int? LessonId { get; set; }
}
