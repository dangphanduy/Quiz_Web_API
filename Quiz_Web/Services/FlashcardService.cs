using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services
{
    public class FlashcardService : IFlashcardService
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<FlashcardService> _logger;

        public FlashcardService(LearningPlatformContext context, ILogger<FlashcardService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Existing async methods
        public async Task<FlashcardSet?> GetFlashcardSetByIdAsync(int setId)
        {
            try
            {
                var flashcardSet = await _context.FlashcardSets
                    .Include(fs => fs.Flashcards.OrderBy(f => f.OrderIndex))
                    .FirstOrDefaultAsync(fs => fs.SetId == setId);

                return flashcardSet;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting flashcard set {setId}: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Flashcard>> GetFlashcardsBySetIdAsync(int setId)
        {
            try
            {
                var flashcards = await _context.Flashcards
                    .Where(f => f.SetId == setId)
                    .OrderBy(f => f.OrderIndex)
                    .ToListAsync();

                return flashcards;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting flashcards for set {setId}: {ex.Message}");
                return new List<Flashcard>();
            }
        }

        public async Task<bool> FlashcardSetExistsAsync(int setId)
        {
            try
            {
                return await _context.FlashcardSets.AnyAsync(fs => fs.SetId == setId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking flashcard set existence {setId}: {ex.Message}");
                return false;
            }
        }

        // FlashcardSet CRUD Operations
        public List<FlashcardSet> GetAllPublishedFlashcardSets()
        {
            try
            {
                return _context.FlashcardSets
                    .Include(fs => fs.Owner)
                    .Include(fs => fs.Flashcards)
                    .Where(fs => !fs.IsDeleted && fs.Visibility == "Public")
                    .OrderByDescending(fs => fs.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving published flashcard sets");
                return new List<FlashcardSet>();
            }
        }

        public List<FlashcardSet> GetFlashcardSetsByOwner(int ownerId)
        {
            try
            {
                return _context.FlashcardSets
                    .Include(fs => fs.Owner)
                    .Include(fs => fs.Flashcards)
                    .Where(fs => fs.OwnerId == ownerId && !fs.IsDeleted)
                    .OrderByDescending(fs => fs.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving flashcard sets by owner");
                return new List<FlashcardSet>();
            }
        }

        public FlashcardSet? GetFlashcardSetById(int setId)
        {
            try
            {
                return _context.FlashcardSets
                    .Include(fs => fs.Owner)
                    .Include(fs => fs.Flashcards.OrderBy(f => f.OrderIndex))
                    .FirstOrDefault(fs => fs.SetId == setId && !fs.IsDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving flashcard set by ID: {setId}");
                return null;
            }
        }

        public FlashcardSet? GetOwnedFlashcardSet(int setId, int ownerId)
        {
            try
            {
                return _context.FlashcardSets
                    .Include(fs => fs.Owner)
                    .Include(fs => fs.Flashcards.OrderBy(f => f.OrderIndex))
                    .FirstOrDefault(fs => fs.SetId == setId && fs.OwnerId == ownerId && !fs.IsDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving owned flashcard set");
                return null;
            }
        }

        public List<FlashcardSet> SearchFlashcardSets(string keyword)
        {
            try
            {
                return _context.FlashcardSets
                    .Include(fs => fs.Owner)
                    .Include(fs => fs.Flashcards)
                    .Where(fs => !fs.IsDeleted && fs.Visibility == "Public" &&
                        (fs.Title.Contains(keyword) || 
                         (fs.Description != null && fs.Description.Contains(keyword)) ||
                         (fs.TagsText != null && fs.TagsText.Contains(keyword))))
                    .OrderByDescending(fs => fs.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"SearchFlashcardSets error: {ex.Message}");
                return new List<FlashcardSet>();
            }
        }

        public FlashcardSet? CreateFlashcardSet(CreateFlashcardSetViewModel model, int ownerId)
        {
            try
            {
                var flashcardSet = new FlashcardSet
                {
                    OwnerId = ownerId,
                    Title = model.Title,
                    Description = model.Description,
                    Visibility = model.Visibility,
                    CoverUrl = model.CoverUrl,
                    TagsText = model.TagsText,
                    Language = model.Language,
                    CreatedAt = DateTimeHelper.Now,
                    IsDeleted = false
                };

                _context.FlashcardSets.Add(flashcardSet);
                _context.SaveChanges();

                return flashcardSet;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating flashcard set");
                return null;
            }
        }

        public FlashcardSet? UpdateFlashcardSet(EditFlashcardSetViewModel model, int ownerId)
        {
            try
            {
                var flashcardSet = _context.FlashcardSets
                    .FirstOrDefault(fs => fs.SetId == model.SetId && fs.OwnerId == ownerId && !fs.IsDeleted);
                
                if (flashcardSet == null) return null;

                flashcardSet.Title = model.Title;
                flashcardSet.Description = model.Description;
                flashcardSet.Visibility = model.Visibility;
                flashcardSet.CoverUrl = model.CoverUrl ?? flashcardSet.CoverUrl;
                flashcardSet.TagsText = model.TagsText;
                flashcardSet.Language = model.Language;
                flashcardSet.UpdatedAt = DateTimeHelper.Now;

                _context.SaveChanges();
                return flashcardSet;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating flashcard set");
                return null;
            }
        }

        public bool DeleteFlashcardSet(int setId, int ownerId, string? webRootPath)
        {
            try
            {
                var flashcardSet = _context.FlashcardSets
                    .FirstOrDefault(fs => fs.SetId == setId && fs.OwnerId == ownerId && !fs.IsDeleted);
                
                if (flashcardSet == null) return false;

                // Soft delete
                flashcardSet.IsDeleted = true;
                flashcardSet.UpdatedAt = DateTimeHelper.Now;

                // Try delete physical cover file if stored locally
                if (!string.IsNullOrWhiteSpace(flashcardSet.CoverUrl) &&
                    webRootPath != null &&
                    flashcardSet.CoverUrl.StartsWith("/"))
                {
                    var relative = flashcardSet.CoverUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
                    var physicalPath = Path.Combine(webRootPath, relative);
                    if (System.IO.File.Exists(physicalPath))
                        System.IO.File.Delete(physicalPath);
                }

                _context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting flashcard set {SetId}", setId);
                return false;
            }
        }

        // Flashcard CRUD Operations
        public List<Flashcard> GetFlashcardsBySetId(int setId)
        {
            try
            {
                return _context.Flashcards
                    .Where(f => f.SetId == setId)
                    .OrderBy(f => f.OrderIndex)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving flashcards for set {setId}");
                return new List<Flashcard>();
            }
        }

        public Flashcard? GetFlashcardById(int cardId)
        {
            try
            {
                return _context.Flashcards
                    .Include(f => f.Set)
                    .FirstOrDefault(f => f.CardId == cardId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving flashcard by ID: {cardId}");
                return null;
            }
        }

        public Flashcard? CreateFlashcard(CreateFlashcardViewModel model, int ownerId)
        {
            try
            {
                // Verify ownership of the set
                var set = _context.FlashcardSets
                    .FirstOrDefault(fs => fs.SetId == model.SetId && fs.OwnerId == ownerId && !fs.IsDeleted);
                
                if (set == null) return null;

                var flashcard = new Flashcard
                {
                    SetId = model.SetId,
                    FrontText = model.FrontText,
                    BackText = model.BackText,
                    Hint = model.Hint,
                    OrderIndex = model.OrderIndex,
                    CreatedAt = DateTimeHelper.Now
                };

                _context.Flashcards.Add(flashcard);
                _context.SaveChanges();

                return flashcard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating flashcard");
                return null;
            }
        }

        public Flashcard? UpdateFlashcard(EditFlashcardViewModel model, int ownerId)
        {
            try
            {
                var flashcard = _context.Flashcards
                    .Include(f => f.Set)
                    .FirstOrDefault(f => f.CardId == model.CardId && f.Set.OwnerId == ownerId && !f.Set.IsDeleted);
                
                if (flashcard == null) return null;

                flashcard.FrontText = model.FrontText;
                flashcard.BackText = model.BackText;
                flashcard.Hint = model.Hint;
                flashcard.OrderIndex = model.OrderIndex;
                flashcard.UpdatedAt = DateTimeHelper.Now;

                _context.SaveChanges();
                return flashcard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating flashcard");
                return null;
            }
        }

        public bool DeleteFlashcard(int cardId, int ownerId)
        {
            try
            {
                var flashcard = _context.Flashcards
                    .Include(f => f.Set)
                    .FirstOrDefault(f => f.CardId == cardId && f.Set.OwnerId == ownerId && !f.Set.IsDeleted);
                
                if (flashcard == null) return false;

                _context.Flashcards.Remove(flashcard);
                _context.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting flashcard {CardId}", cardId);
                return false;
            }
        }

        public bool DeleteAllFlashcardsInSet(int setId, int ownerId)
        {
            try
            {
                // Verify ownership of the set
                var set = _context.FlashcardSets
                    .FirstOrDefault(fs => fs.SetId == setId && fs.OwnerId == ownerId && !fs.IsDeleted);
                
                if (set == null) return false;

                // Get all flashcards in this set
                var flashcards = _context.Flashcards
                    .Where(f => f.SetId == setId)
                    .ToList();

                // Remove all flashcards
                _context.Flashcards.RemoveRange(flashcards);
                _context.SaveChanges();

                _logger.LogInformation($"Deleted {flashcards.Count} flashcards from set {setId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all flashcards in set {SetId}", setId);
                return false;
            }
        }

        public async Task<IEnumerable<FlashcardSet>> GetPublicFlashcardSetsAsync()
        {
            try
            {
                var publicFlashcardSets = await _context.FlashcardSets
                    .Include(fs => fs.Owner)
                    .Include(fs => fs.Flashcards)
                    .Where(fs => fs.Visibility.ToLower() == "public" && !fs.IsDeleted)
                    .OrderByDescending(fs => fs.CreatedAt)
                    .ToListAsync();

                return publicFlashcardSets;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting public flashcard sets: {ex.Message}");
                return new List<FlashcardSet>();
            }
        }
    }
}
