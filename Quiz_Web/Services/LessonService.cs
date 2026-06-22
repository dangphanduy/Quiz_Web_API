using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services
{
    public class LessonService : ILessonService
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<LessonService> _logger;

        public LessonService(LearningPlatformContext context, ILogger<LessonService> logger)
        {
            _context = context;
            _logger = logger;
        }

        //public async Task<int> CreateLessonAsync(LessonCreateViewModel viewModel, int ownerId)
        //{
        //    IDbContextTransaction? transaction = null;
            
        //    try
        //    {
        //        // Bắt đầu transaction
        //        transaction = await _context.Database.BeginTransactionAsync();
                
        //        _logger.LogInformation("Creating new lesson for owner {OwnerId}", ownerId);

        //        // B1: Tạo và INSERT vào dbo.Lessons
        //        var newLesson = new Lesson
        //        {
        //            OwnerId = ownerId,
        //            Title = viewModel.Title,
        //            Description = viewModel.Description,
        //            Visibility = viewModel.Visibility,
        //            CoverUrl = viewModel.CoverUrl,
        //            CreatedAt = DateTimeHelper.Now,
        //            IsDeleted = false
        //        };

        //        _context.Lessons.Add(newLesson);
        //        await _context.SaveChangesAsync();
                
        //        int newLessonId = newLesson.LessonId;
        //        _logger.LogInformation("Created lesson with ID {LessonId}", newLessonId);

        //        // B2-B4: Lặp qua các slides
        //        foreach (var slideVM in viewModel.Slides)
        //        {
        //            _logger.LogDebug("Processing slide {OrderIndex} of type {SlideType}", 
        //                slideVM.OrderIndex, slideVM.SlideType);

        //            // B3: INSERT vào dbo.LessonSlides
        //            var newSlide = new LessonSlide
        //            {
        //                LessonId = newLessonId,
        //                OrderIndex = slideVM.OrderIndex,
        //                SlideType = slideVM.SlideType,
        //                StemText = slideVM.StemText,
        //                Points = slideVM.Points,
        //                CreatedAt = DateTimeHelper.Now
        //            };

        //            _context.LessonSlides.Add(newSlide);
        //            await _context.SaveChangesAsync();
                    
        //            int newSlideId = newSlide.SlideId;
        //            _logger.LogDebug("Created slide with ID {SlideId}", newSlideId);

        //            // B4: Xử lý các loại slide khác nhau
        //            switch (slideVM.SlideType)
        //            {
        //                case "Flashcard":
        //                    await CreateFlashcardSlideAsync(newSlideId, slideVM);
        //                    break;

        //                case "MCQ_Single":
        //                case "MCQ_Multi":
        //                case "TrueFalse":
        //                    await CreateMCQSlideAsync(newSlideId, slideVM);
        //                    break;

        //                case "ShortText":
        //                    await CreateShortTextSlideAsync(newSlideId, slideVM);
        //                    break;

        //                default:
        //                    _logger.LogWarning("Unknown slide type: {SlideType}", slideVM.SlideType);
        //                    break;
        //            }
        //        }

        //        // B5: Commit transaction
        //        await transaction.CommitAsync();
        //        _logger.LogInformation("Successfully created lesson {LessonId} with {SlideCount} slides", 
        //            newLessonId, viewModel.Slides.Count);

        //        return newLessonId;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error creating lesson for owner {OwnerId}", ownerId);
                
        //        if (transaction != null)
        //        {
        //            await transaction.RollbackAsync();
        //            _logger.LogInformation("Transaction rolled back");
        //        }
                
        //        throw new Exception("Không thể tạo bài học. Vui lòng thử lại.", ex);
        //    }
        //    finally
        //    {
        //        transaction?.Dispose();
        //    }
        }

    //private async Task CreateFlashcardSlideAsync(int slideId, LessonSlideViewModel slideVM)
    //{
    //    _logger.LogDebug("Creating flashcard for slide {SlideId}", slideId);

    //    var flashcard = new LessonSlideFlashcard
    //    {
    //        SlideId = slideId,
    //        BackText = slideVM.BackText
    //    };

    //    _context.LessonSlideFlashcards.Add(flashcard);
    //    await _context.SaveChangesAsync();
    //}

    //private async Task CreateMCQSlideAsync(int slideId, LessonSlideViewModel slideVM)
    //{
    //    _logger.LogDebug("Creating MCQ options for slide {SlideId}, count: {OptionCount}", 
    //        slideId, slideVM.Options.Count);

    //    foreach (var optionVM in slideVM.Options)
    //    {
    //        var option = new LessonSlideOption
    //        {
    //            SlideId = slideId,
    //            OptionText = optionVM.OptionText,
    //            IsCorrect = optionVM.IsCorrect,
    //            OrderIndex = optionVM.OrderIndex
    //        };

    //        _context.LessonSlideOptions.Add(option);
    //    }

    //    await _context.SaveChangesAsync();
    //}

    //private async Task CreateShortTextSlideAsync(int slideId, LessonSlideViewModel slideVM)
    //{
    //    _logger.LogDebug("Creating short text answer for slide {SlideId}", slideId);

    //    var shortText = new LessonSlideShortText
    //    {
    //        SlideId = slideId,
    //        CorrectText = slideVM.CorrectText,
    //        CaseSensitive = false // Changed from IsCaseSensitive to CaseSensitive
    //    };

    //    _context.LessonSlideShortTexts.Add(shortText);
    //    await _context.SaveChangesAsync();
//}
    //}
}
