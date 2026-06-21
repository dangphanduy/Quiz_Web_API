using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services
{
	public class CourseService : ICourseService
	{
		private readonly LearningPlatformContext _context;
		private readonly ILogger<CourseService> _logger;
		private readonly IRecommendationService _recommendationService;

		public CourseService(
			LearningPlatformContext context,
			ILogger<CourseService> logger)
		{
			_context = context;
			_logger = logger;
			_recommendationService = new RecommendationService();
		}

		public List<Course> GetAllPublishedCourses()
		{
			try
			{
				return _context.Courses
					.Include(c => c.Owner)
					.Where(c => c.IsPublished)
					.OrderByDescending(c => c.CreatedAt)
					.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving published courses");
				return new List<Course>();
			}
		}

		public Course? GetCourseById(int id)
		{
			try
			{
				return _context.Courses
					.Include(c => c.Owner)
					//.Include(c => c.CourseSections)
					//.Include(c => c.CourseContents)
					.FirstOrDefault(c => c.CourseId == id && c.IsPublished);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error retrieving course by ID: {id}");
				return null;
			}
		}

		public Course? GetCourseBySlug(string slug)
		{
			try
			{
				return _context.Courses
					.Include(c => c.Owner)
					.FirstOrDefault(c => c.Slug == slug && c.IsPublished);
			}
			catch (Exception ex)
			{
				_logger.LogError($"GetCoursesByCategory error: {ex.Message}");
				return null;
			}
		}

		public Course? GetCourseBySlugWithFullDetails(string slug)
		{
			try
			{
				return _context.Courses
					.Include(c => c.Owner)
					.Include(c => c.Category)
					.Include(c => c.CourseChapters.OrderBy(ch => ch.OrderIndex))
						.ThenInclude(ch => ch.Lessons.OrderBy(l => l.OrderIndex))
							.ThenInclude(l => l.LessonContents.OrderBy(lc => lc.OrderIndex))
					.Include(c => c.CoursePurchases)
					.Include(c => c.CourseReviews.OrderByDescending(r => r.CreatedAt))
						.ThenInclude(r => r.User)
					.FirstOrDefault(c => c.Slug == slug);
					//.FirstOrDefault(c => c.Slug == slug && c.IsPublished);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"GetCourseBySlugWithFullDetails error for slug: {slug}");
				return null;
			}
		}

		public List<Course> GetCoursesByCategory(string category)
		{
			try
			{
				return _context.Courses
					.Include(c => c.Owner)
					.Include(c => c.Category)
					.Where(c => c.IsPublished && c.Category != null && c.Category.Slug == category)
					.OrderByDescending(c => c.CreatedAt)
					.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError($"GetCourseByCategory error: {ex.Message}");
				return new List<Course>();
			}
		}

		public List<Course> SearchCourses(string keyword)
		{
			try
			{
				return _context.Courses
					.Include(c => c.Owner)
					.Where(c => c.IsPublished &&
						(c.Title.Contains(keyword) || (c.Summary != null && c.Summary.Contains(keyword))))
					.OrderByDescending(c => c.CreatedAt)
					.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError($"SearchCourses error: {ex.Message}");
				return new List<Course>();
			}
		}

		public Course? CreateCourse(CreateCourseViewModel model, int ownerId)
		{
			try
			{
				var course = new Course
				{
					OwnerId = ownerId,
					Title = model.Title,
					Slug = model.Slug,
					Summary = model.Description,
					CoverUrl = model.CoverUrl,
					Price = model.Price ?? 0,
					//Currency = model.Currency,
					IsPublished = model.IsPublished,
					CreatedAt = DateTime.Now
				};

				_context.Courses.Add(course);
				_context.SaveChanges();

				return course;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating course");
				return null;
			}
		}

		public bool IsSlugUnique(string slug)
		{
			try
			{
				return !_context.Courses.Any(c => c.Slug == slug);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error checking slug uniqueness: {slug}");
				return false;
			}
		}

		// New overload
		public bool IsSlugUnique(string slug, int? excludeCourseId)
		{
			try
			{
				return !_context.Courses.Any(c => c.Slug == slug && (!excludeCourseId.HasValue || c.CourseId != excludeCourseId.Value));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"Error checking slug uniqueness (exclude {excludeCourseId}): {slug}");
				return false;
			}
		}

		// Added
		public List<Course> GetCoursesByOwner(int ownerId)
		{
			try
			{
				return _context.Courses
					.Include(c => c.Owner)
					.Where(c => c.OwnerId == ownerId)
					.OrderByDescending(c => c.CreatedAt)
					.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving owner courses");
				return new List<Course>();
			}
		}

		public Course? GetOwnedCourse(int id, int ownerId)
		{
			try
			{
				return _context.Courses
					.Include(c => c.Owner)
					.FirstOrDefault(c => c.CourseId == id && c.OwnerId == ownerId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving owned course");
				return null;
			}
		}

		public Course? UpdateCourse(EditCourseViewModel model, int ownerId, string? sanitizedDescription)
		{
			try
			{
				var course = _context.Courses.FirstOrDefault(c => c.CourseId == model.CourseId && c.OwnerId == ownerId);
				if (course == null) return null;

				course.Title = model.Title;
				course.Slug = model.Slug;
				course.Summary = sanitizedDescription ?? model.Description;
				course.Price = model.Price ?? 0;
				//course.Currency = model.Currency;
				course.IsPublished = model.IsPublished;
				course.CoverUrl = model.CoverUrl;
				course.UpdatedAt = DateTime.UtcNow;

				_context.SaveChanges();
				return course;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating course");
				return null;
			}
		}

		public bool DeleteCourse(int id, int ownerId, string? webRootPath)
		{
			try
			{
				var course = _context.Courses.FirstOrDefault(c => c.CourseId == id && c.OwnerId == ownerId);
				if (course == null) return false;

				// Try delete physical cover file if stored locally
				if (!string.IsNullOrWhiteSpace(course.CoverUrl) &&
					webRootPath != null &&
					course.CoverUrl.StartsWith("/"))
				{
					var relative = course.CoverUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
					var physicalPath = Path.Combine(webRootPath, relative);
					if (System.IO.File.Exists(physicalPath))
						System.IO.File.Delete(physicalPath);
				}

				_context.Courses.Remove(course);
				_context.SaveChanges();
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting course {CourseId}", id);
				return false;
			}
		}

		// ============= NEW METHODS FOR COURSE BUILDER =============

		public List<CourseCategory> GetAllCategories()
		{
			try
			{
				return _context.CourseCategories
					.OrderBy(c => c.DisplayOrder)
					.ThenBy(c => c.Name)
					.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving categories");
				return new List<CourseCategory>();
			}
		}

		public Course? CreateCourseWithStructure(CourseBuilderViewModel model, int ownerId)
		{
			using var transaction = _context.Database.BeginTransaction();
			try
			{
				// Step 1: Create Course
				var course = new Course
				{
					OwnerId = ownerId,
					CategoryId = model.CategoryId,
					Title = model.Title,
					Slug = model.Slug,
					Summary = model.Summary,
					CoverUrl = model.CoverUrl,
					Price = model.Price ?? 0,
					IsPublished = model.IsPublished,
					CreatedAt = DateTime.UtcNow
				};

				_context.Courses.Add(course);
				_context.SaveChanges();

				// Step 2: Create Chapters, Lessons, and LessonContents
				foreach (var chapterVM in model.Chapters)
				{
					var chapter = new CourseChapter
					{
						CourseId = course.CourseId,
						Title = chapterVM.Title,
						Description = chapterVM.Description,
						OrderIndex = chapterVM.OrderIndex
					};

					_context.CourseChapters.Add(chapter);
					_context.SaveChanges();

					foreach (var lessonVM in chapterVM.Lessons)
					{
						var lesson = new Lesson
						{
							ChapterId = chapter.ChapterId,
							Title = lessonVM.Title,
							Description = lessonVM.Description,
							OrderIndex = lessonVM.OrderIndex,
							Visibility = lessonVM.Visibility,
							CreatedAt = DateTime.UtcNow
						};

						_context.Lessons.Add(lesson);
						_context.SaveChanges();

						foreach (var contentVM in lessonVM.Contents)
						{
							int? refId = null;

							// Step 3: Create FlashcardSet or Test if needed
							if (contentVM.ContentType == "FlashcardSet" && contentVM.Flashcards != null && contentVM.Flashcards.Any())
							{
								var flashcardSet = new FlashcardSet
								{
									OwnerId = ownerId,
									Title = contentVM.FlashcardSetTitle ?? contentVM.Title ?? "Untitled Flashcard Set",
									Description = contentVM.FlashcardSetDesc,
									Visibility = "Course", // Course-only visibility
									CreatedAt = DateTime.UtcNow,
									IsDeleted = false
								};

								_context.FlashcardSets.Add(flashcardSet);
								_context.SaveChanges();

								// Add flashcards
								foreach (var flashcardVM in contentVM.Flashcards)
								{
									var flashcard = new Flashcard
									{
										SetId = flashcardSet.SetId,
										FrontText = flashcardVM.FrontText,
										BackText = flashcardVM.BackText,
										Hint = flashcardVM.Hint,
										OrderIndex = flashcardVM.OrderIndex,
										CreatedAt = DateTime.UtcNow
									};

									_context.Flashcards.Add(flashcard);
								}
								_context.SaveChanges();

								refId = flashcardSet.SetId;
							}
							else if (contentVM.ContentType == "Test" && contentVM.Questions != null && contentVM.Questions.Any())
							{
								// ✅ VALIDATE: Kiểm tra TimeLimitMinutes và MaxAttempts không được âm
								var timeLimitMinutes = contentVM.TimeLimitMinutes ?? 30;
								var maxAttempts = contentVM.MaxAttempts ?? 3;

								if (timeLimitMinutes < 1)
								{
									_logger.LogWarning("Invalid TimeLimitMinutes: {TimeLimitMinutes}. Using default value 30.", timeLimitMinutes);
									timeLimitMinutes = 30;
								}

								if (maxAttempts < 1)
								{
									_logger.LogWarning("Invalid MaxAttempts: {MaxAttempts}. Using default value 3.", maxAttempts);
									maxAttempts = 3;
								}

								var test = new Test
								{
									OwnerId = ownerId,
									Title = contentVM.TestTitle ?? contentVM.Title ?? "Untitled Test",
									Description = contentVM.TestDesc,
									Visibility = "Course", // Course-only visibility
									TimeLimitSec = timeLimitMinutes * 60,
									MaxAttempts = maxAttempts,
									ShuffleQuestions = false,
									ShuffleOptions = false,
									GradingMode = "Auto",
									CreatedAt = DateTime.UtcNow,
									IsDeleted = false
								};

								_context.Tests.Add(test);
								_context.SaveChanges();

								// Add questions
								foreach (var questionVM in contentVM.Questions)
								{
									var question = new Question
									{
										TestId = test.TestId,
										Type = questionVM.Type,
										StemText = questionVM.StemText,
										Points = questionVM.Points,
										OrderIndex = questionVM.OrderIndex
									};

									_context.Questions.Add(question);
									_context.SaveChanges();

									// Add options
									if (questionVM.Options != null)
									{
										foreach (var optionVM in questionVM.Options)
										{
											var option = new QuestionOption
											{
												QuestionId = question.QuestionId,
												OptionText = optionVM.OptionText,
												IsCorrect = optionVM.IsCorrect,
												OrderIndex = optionVM.OrderIndex
											};

											_context.QuestionOptions.Add(option);
										}
										_context.SaveChanges();
									}
								}

								refId = test.TestId;
							}

							var content = new LessonContent
							{
								LessonId = lesson.LessonId,
								ContentType = contentVM.ContentType,
								RefId = refId,
								Title = contentVM.Title,
								Body = contentVM.Body,
								VideoUrl = contentVM.VideoUrl,
								DocumentUrl = contentVM.DocumentUrl,
								OrderIndex = contentVM.OrderIndex,
								CreatedAt = DateTime.UtcNow
							};

							_context.LessonContents.Add(content);
						}
						_context.SaveChanges();
					}
				}

				transaction.Commit();
				return course;
			}
			catch (Exception ex)
			{
				transaction.Rollback();
				_logger.LogError(ex, "Error creating course with structure");
				return null;
			}
		}

		public Course? UpdateCourseStructure(int courseId, CourseBuilderViewModel model, int ownerId)
		{
			using var transaction = _context.Database.BeginTransaction();
			try
			{
				var course = _context.Courses
					.Include(c => c.CourseChapters)
						.ThenInclude(ch => ch.Lessons)
							.ThenInclude(l => l.LessonContents)
					.FirstOrDefault(c => c.CourseId == courseId && c.OwnerId == ownerId);

				if (course == null) return null;

				// Update course info
				course.CategoryId = model.CategoryId;
				course.Title = model.Title;
				course.Slug = model.Slug;
				course.Summary = model.Summary;
				course.CoverUrl = model.CoverUrl;
				course.Price = model.Price ?? 0;
				course.IsPublished = model.IsPublished;
				course.UpdatedAt = DateTime.UtcNow;

				// Collect RefIds of FlashcardSets and Tests to delete
				var flashcardSetIdsToDelete = new List<int>();
				var testIdsToDelete = new List<int>();

				foreach (var chapter in course.CourseChapters.ToList())
				{
					foreach (var lesson in chapter.Lessons.ToList())
					{
						foreach (var content in lesson.LessonContents.ToList())
						{
							if (content.ContentType == "FlashcardSet" && content.RefId.HasValue)
							{
								flashcardSetIdsToDelete.Add(content.RefId.Value);
							}
							else if (content.ContentType == "Test" && content.RefId.HasValue)
							{
								testIdsToDelete.Add(content.RefId.Value);
							}
						}
						_context.LessonContents.RemoveRange(lesson.LessonContents);
						_context.Lessons.Remove(lesson);
					}
					_context.CourseChapters.Remove(chapter);
				}
				_context.SaveChanges();

				// Delete old FlashcardSets and Tests
				if (flashcardSetIdsToDelete.Any())
				{
					var flashcardSets = _context.FlashcardSets
						.Where(fs => flashcardSetIdsToDelete.Contains(fs.SetId) && fs.Visibility == "Course")
						.ToList();
					foreach (var set in flashcardSets)
					{
						set.IsDeleted = true; // Soft delete
					}
				}

				if (testIdsToDelete.Any())
				{
					var tests = _context.Tests
						.Where(t => testIdsToDelete.Contains(t.TestId) && t.Visibility == "Course")
						.ToList();
					foreach (var test in tests)
					{
						test.IsDeleted = true; // Soft delete
					}
				}
				_context.SaveChanges();

				// Add new structure
				foreach (var chapterVM in model.Chapters)
				{
					var chapter = new CourseChapter
					{
						CourseId = course.CourseId,
						Title = chapterVM.Title,
						Description = chapterVM.Description,
						OrderIndex = chapterVM.OrderIndex
					};

					_context.CourseChapters.Add(chapter);
					_context.SaveChanges();

					foreach (var lessonVM in chapterVM.Lessons)
					{
						var lesson = new Lesson
						{
							ChapterId = chapter.ChapterId,
							Title = lessonVM.Title,
							Description = lessonVM.Description,
							OrderIndex = lessonVM.OrderIndex,
							Visibility = lessonVM.Visibility,
							CreatedAt = DateTime.UtcNow
						};

						_context.Lessons.Add(lesson);
						_context.SaveChanges();

						foreach (var contentVM in lessonVM.Contents)
						{
							int? refId = null;

							// Create FlashcardSet or Test if needed
							if (contentVM.ContentType == "FlashcardSet" && contentVM.Flashcards != null && contentVM.Flashcards.Any())
							{
								var flashcardSet = new FlashcardSet
								{
									OwnerId = ownerId,
									Title = contentVM.FlashcardSetTitle ?? contentVM.Title ?? "Untitled Flashcard Set",
									Description = contentVM.FlashcardSetDesc,
									Visibility = "Course",
									CreatedAt = DateTime.UtcNow,
									IsDeleted = false
								};

								_context.FlashcardSets.Add(flashcardSet);
								_context.SaveChanges();

								// Add flashcards
								foreach (var flashcardVM in contentVM.Flashcards)
								{
									var flashcard = new Flashcard
									{
										SetId = flashcardSet.SetId,
										FrontText = flashcardVM.FrontText,
										BackText = flashcardVM.BackText,
										Hint = flashcardVM.Hint,
										OrderIndex = flashcardVM.OrderIndex,
										CreatedAt = DateTime.UtcNow
									};

									_context.Flashcards.Add(flashcard);
								}
								_context.SaveChanges();

								refId = flashcardSet.SetId;
							}
							else if (contentVM.ContentType == "Test" && contentVM.Questions != null && contentVM.Questions.Any())
							{
								// ✅ VALIDATE: Kiểm tra TimeLimitMinutes và MaxAttempts không được âm
								var timeLimitMinutes = contentVM.TimeLimitMinutes ?? 30;
								var maxAttempts = contentVM.MaxAttempts ?? 3;

								if (timeLimitMinutes < 1)
								{
									_logger.LogWarning("Invalid TimeLimitMinutes: {TimeLimitMinutes}. Using default value 30.", timeLimitMinutes);
									timeLimitMinutes = 30;
								}

								if (maxAttempts < 1)
								{
									_logger.LogWarning("Invalid MaxAttempts: {MaxAttempts}. Using default value 3.", maxAttempts);
									maxAttempts = 3;
								}

								var test = new Test
								{
									OwnerId = ownerId,
									Title = contentVM.TestTitle ?? contentVM.Title ?? "Untitled Test",
									Description = contentVM.TestDesc,
									Visibility = "Course",
									TimeLimitSec = timeLimitMinutes * 60,
									MaxAttempts = maxAttempts,
									ShuffleQuestions = false,
									ShuffleOptions = false,
									GradingMode = "Auto",
									CreatedAt = DateTime.UtcNow,
									IsDeleted = false
								};

								_context.Tests.Add(test);
								_context.SaveChanges();

								// Add questions
								foreach (var questionVM in contentVM.Questions)
								{
									var question = new Question
									{
										TestId = test.TestId,
										Type = questionVM.Type,
										StemText = questionVM.StemText,
										Points = questionVM.Points,
										OrderIndex = questionVM.OrderIndex
									};

									_context.Questions.Add(question);
									_context.SaveChanges();

									// Add options
									if (questionVM.Options != null)
									{
										foreach (var optionVM in questionVM.Options)
										{
											var option = new QuestionOption
											{
												QuestionId = question.QuestionId,
												OptionText = optionVM.OptionText,
												IsCorrect = optionVM.IsCorrect,
												OrderIndex = optionVM.OrderIndex
											};

											_context.QuestionOptions.Add(option);
										}
										_context.SaveChanges();
									}
								}

								refId = test.TestId;
							}

							var content = new LessonContent
							{
								LessonId = lesson.LessonId,
								ContentType = contentVM.ContentType,
								RefId = refId,
								Title = contentVM.Title,
								Body = contentVM.Body,
								VideoUrl = contentVM.VideoUrl, // ADD THIS LINE
								DocumentUrl = contentVM.DocumentUrl,
								OrderIndex = contentVM.OrderIndex,
								CreatedAt = DateTime.UtcNow
							};

							_context.LessonContents.Add(content);
						}
						_context.SaveChanges();
					}
				}

				transaction.Commit();
				return course;
			}
			catch (Exception ex)
			{
				transaction.Rollback();
				_logger.LogError(ex, "Error updating course structure");
				return null;
			}
		}

		public Course? GetCourseWithFullStructure(int courseId, int ownerId)
		{
			try
			{
				return _context.Courses
					.Include(c => c.Category)
					.Include(c => c.CourseChapters.OrderBy(ch => ch.OrderIndex))
						.ThenInclude(ch => ch.Lessons.OrderBy(l => l.OrderIndex))
							.ThenInclude(l => l.LessonContents.OrderBy(lc => lc.OrderIndex))
					.FirstOrDefault(c => c.CourseId == courseId && c.OwnerId == ownerId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving course with full structure");
				return null;
			}
		}

		public CourseBuilderViewModel? GetCourseBuilderData(int courseId, int ownerId)
		{
			try
			{
				var course = GetCourseWithFullStructure(courseId, ownerId);
				if (course == null) return null;

				var viewModel = new CourseBuilderViewModel
				{
					Title = course.Title,
					Slug = course.Slug,
					Summary = course.Summary,
					CategoryId = course.CategoryId,
					CoverUrl = course.CoverUrl,
					Price = course.Price,
					IsPublished = course.IsPublished,
					Chapters = course.CourseChapters.Select(ch => new ChapterBuilderViewModel
					{
						ChapterId = ch.ChapterId,
						Title = ch.Title,
						Description = ch.Description,
						OrderIndex = ch.OrderIndex,
						Lessons = ch.Lessons.Select(l => new LessonBuilderViewModel
						{
							LessonId = l.LessonId,
							Title = l.Title,
							Description = l.Description,
							OrderIndex = l.OrderIndex,
							Visibility = l.Visibility,
							Contents = l.LessonContents.Select(lc =>
							{
								var content = new LessonContentBuilderViewModel
								{
									ContentId = lc.ContentId,
									ContentType = lc.ContentType,
									RefId = lc.RefId,
									Title = lc.Title,
									Body = lc.Body,
									VideoUrl = lc.VideoUrl,
									DocumentUrl = lc.DocumentUrl,
									OrderIndex = lc.OrderIndex
								};

								// ✅ LOAD FLASHCARD DATA
								if (lc.ContentType == "FlashcardSet" && lc.RefId.HasValue)
								{
									var flashcardSet = _context.FlashcardSets
										.Include(fs => fs.Flashcards.OrderBy(f => f.OrderIndex))
										.FirstOrDefault(fs => fs.SetId == lc.RefId.Value);

									if (flashcardSet != null)
									{
										content.FlashcardSetTitle = flashcardSet.Title;
										content.FlashcardSetDesc = flashcardSet.Description;
										content.Flashcards = flashcardSet.Flashcards.Select(f => new FlashcardBuilderViewModel
										{
											FrontText = f.FrontText,
											BackText = f.BackText,
											Hint = f.Hint,
											OrderIndex = f.OrderIndex
										}).ToList();
									}
								}
								// ✅ LOAD TEST DATA
								else if (lc.ContentType == "Test" && lc.RefId.HasValue)
								{
									var test = _context.Tests
										.Include(t => t.Questions.OrderBy(q => q.OrderIndex))
											.ThenInclude(q => q.QuestionOptions.OrderBy(o => o.OrderIndex))
										.FirstOrDefault(t => t.TestId == lc.RefId.Value);

									if (test != null)
									{
										content.TestTitle = test.Title;
										content.TestDesc = test.Description;
										content.TimeLimitMinutes = test.TimeLimitSec.HasValue ? test.TimeLimitSec.Value / 60 : 30;
										content.MaxAttempts = test.MaxAttempts ?? 3;
										content.Questions = test.Questions.Select(q => new TestQuestionBuilderViewModel
										{
											Type = q.Type,
											StemText = q.StemText,
											Points = q.Points,
											OrderIndex = q.OrderIndex,
											Options = q.QuestionOptions.Select(o => new TestQuestionOptionBuilderViewModel
											{
												OptionText = o.OptionText,
												IsCorrect = o.IsCorrect,
												OrderIndex = o.OrderIndex
											}).ToList()
										}).ToList();
									}
								}

								return content;
							}).ToList()
						}).ToList()
					}).ToList()
				};

				return viewModel;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting course builder data");
				return null;
			}
		}

		public bool AutosaveCourse(int? courseId, CourseAutosaveViewModel model, int ownerId)
		{
			try
			{
				if (courseId.HasValue)
				{
					var course = _context.Courses.FirstOrDefault(c => c.CourseId == courseId && c.OwnerId == ownerId);
					if (course != null)
					{
						// Prevent unique constraint violation early
						var duplicate = _context.Courses.Any(c => c.Slug == model.Slug && c.CourseId != course.CourseId);
						if (duplicate)
						{
							return false;
						}

						course.Title = model.Title;
						course.Slug = model.Slug;
						course.Summary = model.Summary;
						course.CategoryId = model.CategoryId;
						course.CoverUrl = model.CoverUrl;
						course.Price = model.Price ?? 0;
						course.UpdatedAt = DateTime.UtcNow;
						_context.SaveChanges();
						return true;
					}
				}
				else
				{
					// Avoid insert when slug duplicates
					var duplicate = _context.Courses.Any(c => c.Slug == model.Slug);
					if (duplicate)
					{
						return false;
					}

					// Create new draft course
					var course = new Course
					{
						OwnerId = ownerId,
						CategoryId = model.CategoryId,
						Title = model.Title,
						Slug = model.Slug,
						Summary = model.Summary,
						CoverUrl = model.CoverUrl,
						Price = model.Price ?? 0,
						IsPublished = false,
						CreatedAt = DateTime.UtcNow
					};
					_context.Courses.Add(course);
					_context.SaveChanges();
					return true;
				}

				return false;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error autosaving course");
				return false;
			}
		}

		public List<Course> GetRecommendedCourses(int userId, int count = 6)
		{
			try
			{
				// Get user's interests (categories they're interested in)
				var userInterests = _context.UserInterests
					.Where(ui => ui.UserId == userId)
					.Select(ui => ui.CategoryId)
					.ToList();

				// If user has interests, prioritize courses in those categories
				if (userInterests.Any())
				{
					var recommendedCourses = _context.Courses
						.Include(c => c.Owner)
						.Include(c => c.Category)
						.Where(c => c.IsPublished &&
								   c.CategoryId.HasValue &&
								   userInterests.Contains(c.CategoryId.Value))
						.OrderByDescending(c => c.AverageRating)
						.ThenByDescending(c => c.TotalReviews)
						.ThenByDescending(c => c.CreatedAt)
						.Take(count)
						.ToList();

					// If not enough courses from user interests, add popular courses
					if (recommendedCourses.Count < count)
					{
						var remaining = count - recommendedCourses.Count;
						var popularCourses = _context.Courses
							.Include(c => c.Owner)
							.Include(c => c.Category)
							.Where(c => c.IsPublished &&
									   !recommendedCourses.Select(rc => rc.CourseId).Contains(c.CourseId))
							.OrderByDescending(c => c.AverageRating)
							.ThenByDescending(c => c.TotalReviews)
							.Take(remaining)
							.ToList();

						recommendedCourses.AddRange(popularCourses);
					}

					return recommendedCourses;
				}

				// If no user interests, return popular courses
				return _context.Courses
					.Include(c => c.Owner)
					.Include(c => c.Category)
					.Where(c => c.IsPublished)
					.OrderByDescending(c => c.AverageRating)
					.ThenByDescending(c => c.TotalReviews)
					.ThenByDescending(c => c.CreatedAt)
					.Take(count)
					.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting recommended courses for user {UserId}", userId);
				// Fallback: return popular courses
				return _context.Courses
					.Include(c => c.Owner)
					.Include(c => c.Category)
					.Where(c => c.IsPublished)
					.OrderByDescending(c => c.CreatedAt)
					.Take(count)
					.ToList();
			}
		}

		public List<Course> GetRelatedCourses(int courseId, int count = 6)
		{
			try
			{
				var currentCourse = _context.Courses
					.AsNoTracking()
					.FirstOrDefault(c => c.CourseId == courseId);

				if (currentCourse == null || count <= 0)
					return new List<Course>();

				var allPublishedCourses = _context.Courses
					.AsNoTracking()
					.Include(c => c.Owner)
					.Include(c => c.Category)
					.Include(c => c.CoursePurchases)
					.AsSplitQuery()
					.Where(c => c.IsPublished)
					.ToList();

				return _recommendationService.GetRelatedCourses(
					currentCourse,
					allPublishedCourses,
					count);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting related courses for course {CourseId}", courseId);
				return new List<Course>();
			}
		}

		public List<Course> GetNextCoursesToLearn(int userId, int count = 10)
		{
			try
			{
				if (userId <= 0 || count <= 0)
					return new List<Course>();

				// Bộ lọc loại trừ gồm khóa đã thanh toán và mọi khóa đã học.
				var purchasedCourseIds = _context.CoursePurchases
					.AsNoTracking()
					.Where(purchase =>
						purchase.BuyerId == userId &&
						purchase.Status == "Paid")
					.Select(purchase => purchase.CourseId)
					.ToList();

				var studiedCourseIds = _context.CourseProgresses
					.AsNoTracking()
					.Where(progress => progress.UserId == userId)
					.Select(progress => progress.CourseId)
					.Distinct()
					.ToList();

				var userEnrolledCourseIds = purchasedCourseIds
					.Concat(studiedCourseIds)
					.Distinct()
					.ToList();

				// CourseProgress được lưu theo từng nội dung. Một khóa hoàn thành
				// khi tất cả LessonContent đều có bản ghi IsCompleted = true.
				var completedProgress = _context.CourseProgresses
					.AsNoTracking()
					.Where(progress =>
						progress.UserId == userId &&
						progress.IsCompleted)
					.Select(progress => new
					{
						progress.CourseId,
						progress.ContentId,
						progress.CompletionAt
					})
					.ToList();

				Course? recentlyCompletedCourse = null;

				if (completedProgress.Count > 0)
				{
					var progressByCourse = completedProgress
						.GroupBy(progress => progress.CourseId)
						.ToDictionary(
							group => group.Key,
							group => new
							{
								CompletedContentCount = group
									.Select(progress => progress.ContentId)
									.Distinct()
									.Count(),
								LastCompletionAt = group.Max(progress => progress.CompletionAt)
							});

					var candidateCourseIds = progressByCourse.Keys.ToList();
					var contentCounts = _context.Courses
						.AsNoTracking()
						.Where(course => candidateCourseIds.Contains(course.CourseId))
						.Select(course => new
						{
							course.CourseId,
							TotalContentCount = course.CourseChapters
								.SelectMany(chapter => chapter.Lessons)
								.SelectMany(lesson => lesson.LessonContents)
								.Count()
						})
						.ToList();

					var recentlyCompletedCourseId = contentCounts
						.Where(item =>
							item.TotalContentCount > 0 &&
							progressByCourse[item.CourseId].CompletedContentCount >=
								item.TotalContentCount)
						.OrderByDescending(item =>
							progressByCourse[item.CourseId].LastCompletionAt ??
							DateTime.MinValue)
						.Select(item => (int?)item.CourseId)
						.FirstOrDefault();

					if (recentlyCompletedCourseId.HasValue)
					{
						recentlyCompletedCourse = _context.Courses
							.AsNoTracking()
							.FirstOrDefault(course =>
								course.CourseId == recentlyCompletedCourseId.Value);
					}
				}

				var allPublishedCourses = _context.Courses
					.AsNoTracking()
					.Include(course => course.Owner)
					.Include(course => course.Category)
					.Include(course => course.CoursePurchases)
					.AsSplitQuery()
					.Where(course => course.IsPublished)
					.ToList();

				return _recommendationService.GetNextCoursesToLearn(
					userId,
					recentlyCompletedCourse,
					userEnrolledCourseIds,
					allPublishedCourses,
					count);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Error getting next courses to learn for user {UserId}",
					userId);
				return new List<Course>();
			}
		}

		public List<Course> GetFilteredAndSortedCourses(
			string? searchKeyword = null,
			string? categorySlug = null,
			decimal? minRating = null,
			decimal? maxRating = null,
			bool? isFree = null,
			string? sortBy = null)
		{
			try
			{
				var query = _context.Courses
					.Include(c => c.Owner)
					.Include(c => c.Category)
					.Where(c => c.IsPublished)
					.AsQueryable();

				// Apply search filter
				if (!string.IsNullOrWhiteSpace(searchKeyword))
				{
					query = query.Where(c => 
						c.Title.Contains(searchKeyword) || 
						(c.Summary != null && c.Summary.Contains(searchKeyword)));
				}

				// Apply category filter
				if (!string.IsNullOrWhiteSpace(categorySlug))
				{
					query = query.Where(c => c.Category != null && c.Category.Slug == categorySlug);
				}

				// Apply rating filter
				if (minRating.HasValue)
				{
					query = query.Where(c => c.AverageRating >= minRating.Value);
				}
				if (maxRating.HasValue)
				{
					query = query.Where(c => c.AverageRating < maxRating.Value);
				}

				// Apply price filter
				if (isFree.HasValue)
				{
					if (isFree.Value)
					{
						query = query.Where(c => c.Price == 0);
					}
					else
					{
						query = query.Where(c => c.Price > 0);
					}
				}

				// Apply sorting
				query = sortBy switch
				{
					"rating" => query.OrderByDescending(c => c.AverageRating)
									 .ThenByDescending(c => c.TotalReviews),
					"price_asc" => query.OrderBy(c => c.Price),
					"price_desc" => query.OrderByDescending(c => c.Price),
					"newest" => query.OrderByDescending(c => c.CreatedAt),
					_ => query.OrderByDescending(c => c.CreatedAt) // Default sort
				};

				return query.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in GetFilteredAndSortedCourses");
				return new List<Course>();
			}
		}

		public List<Course> GetTopRatedCourses(int count = 6)
		{
			try
			{
				return _context.Courses
					.Include(c => c.Owner)
					.Include(c => c.Category)
					.Where(c => c.IsPublished && c.TotalReviews > 0) // Only courses with reviews
					.OrderByDescending(c => c.AverageRating)
					.ThenByDescending(c => c.TotalReviews)
					.ThenByDescending(c => c.CreatedAt)
					.Take(count)
					.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting top rated courses");
				// Fallback: return recent published courses
				return _context.Courses
					.Include(c => c.Owner)
					.Include(c => c.Category)
					.Where(c => c.IsPublished)
					.OrderByDescending(c => c.CreatedAt)
					.Take(count)
					.ToList();
			}
		}
	}
}
