using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlashcardApiController : ControllerBase
    {
        private readonly IFlashcardService _flashcardService;
        private readonly ILogger<FlashcardApiController> _logger;

        public FlashcardApiController(IFlashcardService flashcardService, ILogger<FlashcardApiController> logger)
        {
            _flashcardService = flashcardService;
            _logger = logger;
        }

        // GET: api/FlashcardApi
        [HttpGet]
        public IActionResult GetFlashcardSets([FromQuery] string? search)
        {
            try
            {
                List<FlashcardSet> sets;
                if (!string.IsNullOrEmpty(search))
                {
                    sets = _flashcardService.SearchFlashcardSets(search);
                }
                else
                {
                    sets = _flashcardService.GetAllPublishedFlashcardSets();
                }

                var responseSets = sets.Select(s => new
                {
                    setId = s.SetId,
                    title = s.Title,
                    description = s.Description,
                    visibility = s.Visibility,
                    coverUrl = s.CoverUrl,
                    tagsText = s.TagsText,
                    language = s.Language,
                    createdAt = s.CreatedAt,
                    cardCount = s.Flashcards?.Count ?? 0,
                    ownerName = s.Owner?.FullName
                }).ToList();

                return Ok(new { success = true, sets = responseSets });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting flashcard sets via API");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải danh sách bộ thẻ ghi nhớ" });
            }
        }

        // GET: api/FlashcardApi/{setId}
        [HttpGet("{setId:int}")]
        public async Task<IActionResult> GetFlashcardSetDetail(int setId)
        {
            try
            {
                var set = await _flashcardService.GetFlashcardSetByIdAsync(setId);
                if (set == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy bộ thẻ ghi nhớ" });
                }

                var flashcards = await _flashcardService.GetFlashcardsBySetIdAsync(setId);

                var responseSet = new
                {
                    setId = set.SetId,
                    title = set.Title,
                    description = set.Description,
                    visibility = set.Visibility,
                    coverUrl = set.CoverUrl,
                    tagsText = set.TagsText,
                    language = set.Language,
                    createdAt = set.CreatedAt,
                    ownerName = set.Owner?.FullName,
                    flashcards = flashcards.Select(f => new
                    {
                        cardId = f.CardId,
                        frontText = f.FrontText,
                        backText = f.BackText,
                        hint = f.Hint,
                        orderIndex = f.OrderIndex
                    }).ToList()
                };

                return Ok(new { success = true, set = responseSet });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting flashcard set {SetId} via API", setId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải chi tiết bộ thẻ ghi nhớ" });
            }
        }
    }
}
