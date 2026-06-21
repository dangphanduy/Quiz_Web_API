using System.ComponentModel.DataAnnotations;

namespace Quiz_Web.Models.ViewModels
{
	/// <summary>
	/// ViewModel cho form tạo khóa học nhiều bước
	/// </summary>
	public class CourseBuilderViewModel
	{
		// Step 1: Course Info
		[Required(ErrorMessage = "Tiêu đề khóa học là bắt buộc")]
		[StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
		[Display(Name = "Tiêu đề khóa học")]
		public string Title { get; set; } = null!;

		[Required(ErrorMessage = "Slug là bắt buộc")]
		[RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug chỉ được chứa chữ thường, số và dấu gạch ngang")]
		[StringLength(200, ErrorMessage = "Slug không được vượt quá 200 ký tự")]
		[Display(Name = "URL Slug")]
		public string Slug { get; set; } = null!;

		[StringLength(5000, ErrorMessage = "Mô tả không được vượt quá 5000 ký tự")]
		[Display(Name = "Mô tả ngắn")]
		public string? Summary { get; set; }

		[Display(Name = "Danh mục")]
		public int? CategoryId { get; set; }

		[StringLength(2048, ErrorMessage = "Link ảnh quá dài")]
		[Display(Name = "Link ảnh bìa")]
		public string? CoverUrl { get; set; }

		[Range(0, 999999999, ErrorMessage = "Giá phải từ 0 đến 999,999,999")]
		[Display(Name = "Giá khóa học (VNĐ)")]
		public decimal? Price { get; set; }

		[Display(Name = "Xuất bản ngay")]
		public bool IsPublished { get; set; }

		// Step 2: Chapters and Lessons
		public List<ChapterBuilderViewModel> Chapters { get; set; } = new();
	}

	public class ChapterBuilderViewModel
	{
		public int? ChapterId { get; set; }

		[Required(ErrorMessage = "Tên chương là bắt buộc")]
		[StringLength(200, ErrorMessage = "Tên chương không được vượt quá 200 ký tự")]
		public string Title { get; set; } = null!;

		[StringLength(2000, ErrorMessage = "Mô tả không được vượt quá 2000 ký tự")]
		public string? Description { get; set; }

		public int OrderIndex { get; set; }

		public List<LessonBuilderViewModel> Lessons { get; set; } = new();
	}

	public class LessonBuilderViewModel
	{
		public int? LessonId { get; set; }

		[Required(ErrorMessage = "Tên bài học là bắt buộc")]
		[StringLength(200, ErrorMessage = "Tên bài học không được vượt quá 200 ký tự")]
		public string Title { get; set; } = null!;

		[StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự")]
		public string? Description { get; set; }

		public int OrderIndex { get; set; }

		[Required]
		public string Visibility { get; set; } = "Course"; // Public/Private/Course

		public List<LessonContentBuilderViewModel> Contents { get; set; } = new();
	}

	public class LessonContentBuilderViewModel
	{
		public int? ContentId { get; set; }

		[Required(ErrorMessage = "Loại nội dung là bắt buộc")]
		public string ContentType { get; set; } = null!; // Video/Theory/FlashcardSet/Test

		public int? RefId { get; set; }

		[StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
		public string? Title { get; set; }

		public string? Body { get; set; } // For Theory content type

		[StringLength(2048, ErrorMessage = "URL video quá dài")]
		public string? VideoUrl { get; set; } // For Video content type

		[StringLength(2048, ErrorMessage = "URL tài liệu quá dài")]
		public string? DocumentUrl { get; set; }

		public int OrderIndex { get; set; }
		
		// For FlashcardSet content type
		public string? FlashcardSetTitle { get; set; }
		public string? FlashcardSetDesc { get; set; }
		public List<FlashcardBuilderViewModel>? Flashcards { get; set; }
		
		// For Test content type
		public string? TestTitle { get; set; }
		public string? TestDesc { get; set; }
		public int? TimeLimitMinutes { get; set; }
		public int? MaxAttempts { get; set; }
		public List<TestQuestionBuilderViewModel>? Questions { get; set; }
	}

	/// <summary>
	/// ViewModel cho Flashcard trong Course Builder
	/// </summary>
	public class FlashcardBuilderViewModel
	{
		public string FrontText { get; set; } = null!;
		public string BackText { get; set; } = null!;
		public string? Hint { get; set; }
		public int OrderIndex { get; set; }
	}

	/// <summary>
	/// ViewModel cho Test Question trong Course Builder
	/// </summary>
	public class TestQuestionBuilderViewModel
	{
		public string Type { get; set; } = "MCQ_Single"; // MCQ_Single/MCQ_Multi/TrueFalse
		public string StemText { get; set; } = null!;
		public decimal Points { get; set; } = 1;
		public int OrderIndex { get; set; }
		public List<TestQuestionOptionBuilderViewModel>? Options { get; set; }
	}

	/// <summary>
	/// ViewModel cho Question Option trong Course Builder
	/// </summary>
	public class TestQuestionOptionBuilderViewModel
	{
		public string OptionText { get; set; } = null!;
		public bool IsCorrect { get; set; }
		public int OrderIndex { get; set; }
	}

	/// <summary>
	/// ViewModel cho việc lưu nháp (autosave)
	/// </summary>
	public class CourseAutosaveViewModel
	{
		public int? CourseId { get; set; }
		public string Title { get; set; } = null!;
		public string Slug { get; set; } = null!;
		public string? Summary { get; set; }
		public int? CategoryId { get; set; }
		public string? CoverUrl { get; set; }
		public decimal? Price { get; set; }
		public bool IsPublished { get; set; }
	}

	/// <summary>
	/// Response cho các API calls
	/// </summary>
	public class CourseBuilderResponse
	{
		public bool Success { get; set; }
		public string Message { get; set; } = null!;
		public int? CourseId { get; set; }
		public string? Slug { get; set; }
		public object? Data { get; set; }
	}
}
