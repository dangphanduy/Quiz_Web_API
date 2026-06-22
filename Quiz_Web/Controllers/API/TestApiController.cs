using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using System.Security.Claims;

namespace Quiz_Web.Controllers.API
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TestApiController : ControllerBase
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<TestApiController> _logger;

        public TestApiController(LearningPlatformContext context, ILogger<TestApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/TestApi/completed
        [HttpGet("completed")]
        public async Task<IActionResult> GetCompletedTests()
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { success = false, message = "Người dùng chưa xác thực" });
                }

                var completedTests = await _context.TestAttempts
                    .Include(ta => ta.Test)
                        .ThenInclude(t => t.Owner)
                    .Where(ta => ta.UserId == userId && (ta.Status == "completed" || ta.Status == "Graded"))
                    .OrderByDescending(ta => ta.SubmittedAt)
                    .Select(ta => new
                    {
                        attemptId = ta.AttemptId,
                        testId = ta.TestId,
                        testTitle = ta.Test.Title,
                        startedAt = ta.StartedAt,
                        submittedAt = ta.SubmittedAt,
                        score = ta.Score,
                        maxScore = ta.MaxScore,
                        timeSpentSec = ta.TimeSpentSec,
                        status = ta.Status,
                        teacherName = ta.Test.Owner != null ? ta.Test.Owner.FullName : "Giảng viên"
                    })
                    .ToListAsync();

                return Ok(new { success = true, attempts = completedTests });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting completed tests");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải lịch sử bài kiểm tra" });
            }
        }

        // GET: api/TestApi/{testId}
        [HttpGet("{testId:int}")]
        public async Task<IActionResult> GetTestQuestions(int testId)
        {
            try
            {
                var test = await _context.Tests
                    .Include(t => t.Questions.OrderBy(q => q.OrderIndex))
                        .ThenInclude(q => q.QuestionOptions.OrderBy(o => o.OrderIndex))
                    .FirstOrDefaultAsync(t => t.TestId == testId && !t.IsDeleted);

                if (test == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy bài kiểm tra" });
                }

                // Check if test is open (if OpenAt and CloseAt are set)
                if (test.OpenAt.HasValue && DateTimeHelper.Now < test.OpenAt.Value)
                {
                    return BadRequest(new { success = false, message = "Bài kiểm tra chưa mở" });
                }

                if (test.CloseAt.HasValue && DateTimeHelper.Now > test.CloseAt.Value)
                {
                    return BadRequest(new { success = false, message = "Bài kiểm tra đã đóng" });
                }

                // Check max attempts
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var attemptCount = await _context.TestAttempts
                    .CountAsync(ta => ta.TestId == testId && ta.UserId == userId);

                if (test.MaxAttempts.HasValue && attemptCount >= test.MaxAttempts.Value)
                {
                    return BadRequest(new { success = false, message = "Bạn đã hết lượt làm bài" });
                }

                var response = new
                {
                    success = true,
                    test = new
                    {
                        testId = test.TestId,
                        title = test.Title,
                        description = test.Description,
                        timeLimitSec = test.TimeLimitSec,
                        maxScore = test.MaxScore,
                        questionCount = test.Questions.Count
                    },
                    questions = test.Questions.Select(q => new
                    {
                        questionId = q.QuestionId,
                        type = q.Type,
                        stemText = q.StemText,
                        points = q.Points,
                        orderIndex = q.OrderIndex,
                        options = q.QuestionOptions.Select(o => new
                        {
                            optionId = o.OptionId,
                            optionText = o.OptionText,
                            orderIndex = o.OrderIndex
                        }).ToList()
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading test {TestId}", testId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải bài kiểm tra" });
            }
        }

        // POST: api/TestApi/submit
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitTest([FromBody] SubmitTestRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                
                var test = await _context.Tests
                    .Include(t => t.Questions)
                        .ThenInclude(q => q.QuestionOptions)
                    .FirstOrDefaultAsync(t => t.TestId == request.TestId && !t.IsDeleted);

                if (test == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy bài kiểm tra" });
                }

                // Create test attempt
                var attempt = new Models.Entities.TestAttempt
                {
                    TestId = request.TestId,
                    UserId = userId,
                    StartedAt = DateTimeHelper.Now.AddSeconds(-request.TimeSpentSec),
                    SubmittedAt = DateTimeHelper.Now,
                    Status = "Graded",
                    TimeSpentSec = request.TimeSpentSec,
                    // ✅ FIX: Luôn tính MaxScore từ tổng điểm Questions, không dùng test.MaxScore
                    MaxScore = test.Questions.Sum(q => q.Points)
                };

                _context.TestAttempts.Add(attempt);
                await _context.SaveChangesAsync();

                // Grade answers
                decimal totalScore = 0;
                int correctAnswersCount = 0;
                int totalQuestions = test.Questions.Count;

                foreach (var answer in request.Answers)
                {
                    var question = test.Questions.FirstOrDefault(q => q.QuestionId == answer.QuestionId);
                    if (question == null) continue;

                    bool isCorrect = false;
                    decimal questionScore = 0;

                    // Normalize question type
                    string questionType = NormalizeQuestionType(question.Type);

                    _logger.LogInformation("Grading QuestionId={0}, Type={1}, NormalizedType={2}", 
                        question.QuestionId, question.Type, questionType);

                    if (questionType == "MCQ_Single" || questionType == "TrueFalse")
                    {
                        var correctOption = question.QuestionOptions.FirstOrDefault(o => o.IsCorrect);
                        if (correctOption != null)
                        {
                            _logger.LogInformation("CorrectOptionId={0}, SelectedOptionId={1}", 
                                correctOption.OptionId, answer.SelectedOptionId);
                            
                            if (answer.SelectedOptionId == correctOption.OptionId)
                            {
                                isCorrect = true;
                                questionScore = question.Points;
                                correctAnswersCount++;
                            }
                        }
                    }
                    else if (questionType == "MCQ_Multi")
                    {
                        var correctOptionIds = question.QuestionOptions
                            .Where(o => o.IsCorrect)
                            .Select(o => o.OptionId)
                            .OrderBy(id => id)
                            .ToList();

                        var selectedIds = answer.SelectedOptionIds?
                            .OrderBy(id => id)
                            .ToList() ?? new List<int>();

                        _logger.LogInformation("CorrectOptions=[{0}], SelectedOptions=[{1}]", 
                            string.Join(",", correctOptionIds), string.Join(",", selectedIds));

                        if (correctOptionIds.SequenceEqual(selectedIds))
                        {
                            isCorrect = true;
                            questionScore = question.Points;
                            correctAnswersCount++;
                        }
                    }

                    totalScore += questionScore;

                    // Save attempt answer
                    var attemptAnswer = new Models.Entities.AttemptAnswer
                    {
                        AttemptId = attempt.AttemptId,
                        QuestionId = question.QuestionId,
                        AnswerPayload = System.Text.Json.JsonSerializer.Serialize(answer),
                        IsCorrect = isCorrect,
                        Score = questionScore,
                        AutoGraded = true,
                        GradedAt = DateTimeHelper.Now
                    };

                    _context.AttemptAnswers.Add(attemptAnswer);
                }

                attempt.Score = totalScore;
                await _context.SaveChangesAsync();

                // Calculate percentage based on correct answers
                decimal percentage = totalQuestions > 0
                    ? ((decimal)correctAnswersCount / totalQuestions) * 100
                    : 0;

                _logger.LogInformation("Test {0} submitted: CorrectAnswers={1}/{2}, Score={3}/{4}, Percentage={5}%", 
                    test.TestId, correctAnswersCount, totalQuestions, totalScore, attempt.MaxScore, percentage);

                return Ok(new
                {
                    success = true,
                    attemptId = attempt.AttemptId,
                    score = totalScore,
                    maxScore = attempt.MaxScore,
                    correctAnswers = correctAnswersCount,
                    totalQuestions = totalQuestions,
                    percentage = Math.Round(percentage, 1),
                    message = percentage >= 60 ? "Chúc mừng! Bạn đã đạt!" : "Bạn chưa đạt. Hãy cố gắng lần sau!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting test");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi nộp bài" });
            }
        }

        /// <summary>
        /// Normalize question type to standard format
        /// </summary>
        private string NormalizeQuestionType(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return "MCQ_Single";

            // Convert to lowercase for comparison
            string lowerType = type.Trim().ToLower();

            return lowerType switch
            {
                "mcq_single" or "multiple_choice" or "single_choice" or "trắc nghiệm (1 đáp án)" => "MCQ_Single",
                "mcq_multi" or "multiple_answer" or "multi_choice" or "trắc nghiệm (nhiều đáp án)" => "MCQ_Multi",
                "truefalse" or "true_false" or "đúng/sai" or "đúng sai" => "TrueFalse",
                "shorttext" or "short_text" or "short_answer" or "câu hỏi ngắn" => "ShortText",
                "cloze" or "fill_in_blank" or "điền khuyết" => "Cloze",
                "range" or "number_range" => "Range",
                _ => type // Return original if no match
            };
        }
    }

    public class SubmitTestRequest
    {
        public int TestId { get; set; }
        public int TimeSpentSec { get; set; }
        public List<TestAnswerDto> Answers { get; set; } = new();
    }

    public class TestAnswerDto
    {
        public int QuestionId { get; set; }
        public int? SelectedOptionId { get; set; }
        public List<int>? SelectedOptionIds { get; set; }
    }
}
