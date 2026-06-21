using System.Globalization;
using System.Text;
using Quiz_Web.Models.Entities;
using Quiz_Web.Models.Recommendations;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services;

/// <summary>
/// Lightweight two-stage recommendation pipeline:
/// Retrieval reduces the catalogue; Ranking applies detailed weighted scoring.
/// </summary>
public sealed class RecommendationService : IRecommendationService
{
	private const int CandidatePoolSize = 30;
	private const double CategoryWeight = 100;
	private const double TitleSimilarityWeight = 35;
	private const double InstructorWeight = 4;
	private const double RatingWeight = 10;
	private const double PopularityWeight = 5;
	private const double FreshnessWeight = 6;

	private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
	{
		"khoa", "hoc", "co", "ban", "va", "cho", "tu", "den", "cung", "voi"
	};

	public List<Course> GetRelatedCourses(
		Course currentCourse,
		List<Course> allCourses,
		int limit = 5)
	{
		ArgumentNullException.ThrowIfNull(currentCourse);
		ArgumentNullException.ThrowIfNull(allCourses);

		return RunTwoStagePipeline(
			currentCourse,
			allCourses,
			new HashSet<int> { currentCourse.CourseId },
			limit);
	}

	public List<Course> GetNextCoursesToLearn(
		int userId,
		Course? recentlyCompletedCourse,
		List<int> userEnrolledCourseIds,
		List<Course> allCourses,
		int limit = 10)
	{
		if (userId <= 0)
			throw new ArgumentOutOfRangeException(nameof(userId));

		ArgumentNullException.ThrowIfNull(userEnrolledCourseIds);
		ArgumentNullException.ThrowIfNull(allCourses);

		if (limit <= 0 || allCourses.Count == 0)
			return new List<Course>();

		// Loại trừ ngay các khóa người dùng đã mua hoặc đã học.
		var excludedCourseIds = userEnrolledCourseIds.ToHashSet();

		// Tài khoản chưa hoàn thành khóa học nào nhận danh sách mặc định
		// được xếp theo điểm Popularity trên toàn hệ thống.
		if (recentlyCompletedCourse == null)
			return GetMostPopularCourses(allCourses, excludedCourseIds, limit);

		excludedCourseIds.Add(recentlyCompletedCourse.CourseId);

		// Dùng lại toàn bộ Retrieval và Ranking của tính năng liên quan.
		return RunTwoStagePipeline(
			recentlyCompletedCourse,
			allCourses,
			excludedCourseIds,
			limit);
	}

	private static List<Course> RunTwoStagePipeline(
		Course currentCourse,
		List<Course> allCourses,
		HashSet<int> excludedCourseIds,
		int limit)
	{
		if (limit <= 0 || allCourses.Count == 0)
			return new List<Course>();

		var currentFeatures = MapFeatures(currentCourse);
		var publishedCatalogue = BuildPublishedCatalogue(allCourses);
		var catalogue = publishedCatalogue
			.Where(item => !excludedCourseIds.Contains(item.Features.Id))
			.ToList();

		if (catalogue.Count == 0)
			return new List<Course>();

		var popularityContext = CreatePopularityContext(publishedCatalogue);

		// Giai đoạn 1 - Retrieval: giữ tối đa 30 ứng viên liên quan.
		var candidates = RetrieveCandidates(currentFeatures, catalogue);
		var now = DateTime.UtcNow;

		// Giai đoạn 2 - Ranking: tính đầy đủ các thành phần trọng số.
		return candidates
			.Select(candidate => new RankedCourse(
				candidate.Course,
				CalculateScore(
					currentFeatures,
					candidate.Features,
					popularityContext,
					now)))
			.OrderByDescending(item => item.Score.Total)
			.ThenByDescending(item => item.Course.AverageRating)
			.ThenByDescending(item => item.Course.TotalReviews)
			.ThenByDescending(item => item.Course.CreatedAt)
			.Take(limit)
			.Select(item => item.Course)
			.ToList();
	}

	private static List<CourseCandidate> RetrieveCandidates(
		CourseRecommendationFeatures currentCourse,
		List<CourseCandidate> catalogue)
	{
		var candidates = catalogue
			.Select(candidate =>
			{
				var titleSimilarity = CalculateDiceCoefficient(
					currentCourse.Title,
					candidate.Features.Title);
				var isSameCategory = currentCourse.CategoryId.HasValue &&
					candidate.Features.CategoryId == currentCourse.CategoryId;

				return new RetrievalCandidate(candidate, isSameCategory, titleSimilarity);
			})
			// Retrieval: cùng danh mục hoặc Dice Similarity lớn hơn 0.
			.Where(item => item.IsSameCategory || item.TitleSimilarity > 0)
			.OrderByDescending(item => item.IsSameCategory)
			.ThenByDescending(item => item.TitleSimilarity)
			.ThenByDescending(item => item.Candidate.Features.ReviewCount)
			.ThenByDescending(item => item.Candidate.Features.StudentCount)
			.Take(CandidatePoolSize)
			.Select(item => item.Candidate)
			.ToList();

		return candidates;
	}

	private static RecommendationScore CalculateScore(
		CourseRecommendationFeatures currentCourse,
		CourseRecommendationFeatures candidate,
		PopularityContext popularityContext,
		DateTime now)
	{
		// Same category: +100 points.
		var categoryScore = currentCourse.CategoryId.HasValue &&
			candidate.CategoryId == currentCourse.CategoryId
				? CategoryWeight
				: 0;

		// Sørensen-Dice title similarity: up to 35 points.
		var titleSimilarityScore =
			CalculateDiceCoefficient(currentCourse.Title, candidate.Title) *
			TitleSimilarityWeight;

		// Same instructor: +4 points.
		var instructorScore = currentCourse.InstructorId == candidate.InstructorId
			? InstructorWeight
			: 0;

		// Rating: (rating / 5) * 10, clamped to the platform scale.
		var normalizedRating = Math.Clamp((double)candidate.Rating, 0, 5) / 5;
		var ratingScore = normalizedRating * RatingWeight;

		// Popularity: average of normalized reviews and students, up to 5 points.
		var popularityScore = CalculatePopularityScore(candidate, popularityContext);

		// Freshness: full 6 points for the first 30 days, then decreases
		// linearly until it reaches zero at 365 days.
		var ageInDays = Math.Max(
			0,
			(now - candidate.CreatedDate.ToUniversalTime()).TotalDays);
		var freshnessRatio = ageInDays <= 30
			? 1
			: Math.Clamp(1 - ((ageInDays - 30) / 335), 0, 1);
		var freshnessScore = freshnessRatio * FreshnessWeight;

		return new RecommendationScore(
			categoryScore,
			titleSimilarityScore,
			instructorScore,
			ratingScore,
			popularityScore,
			freshnessScore);
	}

	private static List<Course> GetMostPopularCourses(
		List<Course> allCourses,
		HashSet<int> excludedCourseIds,
		int limit)
	{
		var publishedCatalogue = BuildPublishedCatalogue(allCourses);

		if (publishedCatalogue.Count == 0)
			return new List<Course>();

		var popularityContext = CreatePopularityContext(publishedCatalogue);

		return publishedCatalogue
			.Where(item => !excludedCourseIds.Contains(item.Features.Id))
			.OrderByDescending(item =>
				CalculatePopularityScore(item.Features, popularityContext))
			.ThenByDescending(item => item.Features.Rating)
			.ThenByDescending(item => item.Features.CreatedDate)
			.Take(limit)
			.Select(item => item.Course)
			.ToList();
	}

	private static List<CourseCandidate> BuildPublishedCatalogue(List<Course> allCourses)
	{
		return allCourses
			.Where(course => course.IsPublished)
			.Select(course => new CourseCandidate(course, MapFeatures(course)))
			.ToList();
	}

	private static PopularityContext CreatePopularityContext(
		List<CourseCandidate> publishedCatalogue)
	{
		return new PopularityContext(
			MaxReviewCount: Math.Max(
				1,
				publishedCatalogue.Max(item => item.Features.ReviewCount)),
			MaxStudentCount: Math.Max(
				1,
				publishedCatalogue.Max(item => item.Features.StudentCount)));
	}

	private static double CalculatePopularityScore(
		CourseRecommendationFeatures course,
		PopularityContext context)
	{
		var reviewRatio = Math.Clamp(
			(double)course.ReviewCount / context.MaxReviewCount,
			0,
			1);
		var studentRatio = Math.Clamp(
			(double)course.StudentCount / context.MaxStudentCount,
			0,
			1);

		return ((reviewRatio + studentRatio) / 2) * PopularityWeight;
	}

	private static double CalculateDiceCoefficient(string firstTitle, string secondTitle)
	{
		var firstTokens = Tokenize(firstTitle);
		var secondTokens = Tokenize(secondTitle);

		if (firstTokens.Count == 0 || secondTokens.Count == 0)
			return 0;

		var sharedTokenCount = firstTokens.Intersect(secondTokens).Count();
		return (2d * sharedTokenCount) / (firstTokens.Count + secondTokens.Count);
	}

	private static HashSet<string> Tokenize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		var normalized = RemoveDiacritics(value).ToLowerInvariant();
		var words = new StringBuilder(normalized.Length);

		foreach (var character in normalized)
			words.Append(char.IsLetterOrDigit(character) ? character : ' ');

		return words
			.ToString()
			.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(token => token.Length > 1 && !StopWords.Contains(token))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static string RemoveDiacritics(string value)
	{
		var decomposed = value.Normalize(NormalizationForm.FormD);
		var output = new StringBuilder(decomposed.Length);

		foreach (var character in decomposed)
		{
			if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
				output.Append(
					character == '\u0111'
						? 'd'
						: character == '\u0110'
							? 'D'
							: character);
		}

		return output.ToString().Normalize(NormalizationForm.FormC);
	}

	private static CourseRecommendationFeatures MapFeatures(Course course)
	{
		return new CourseRecommendationFeatures(
			Id: course.CourseId,
			Title: course.Title,
			CategoryId: course.CategoryId,
			InstructorId: course.OwnerId,
			Rating: course.AverageRating,
			ReviewCount: course.TotalReviews,
			StudentCount: course.CoursePurchases.Count(purchase => purchase.Status == "Paid"),
			CreatedDate: course.CreatedAt);
	}

	private sealed record CourseCandidate(
		Course Course,
		CourseRecommendationFeatures Features);

	private sealed record RetrievalCandidate(
		CourseCandidate Candidate,
		bool IsSameCategory,
		double TitleSimilarity);

	private sealed record RankedCourse(Course Course, RecommendationScore Score);

	private sealed record PopularityContext(
		int MaxReviewCount,
		int MaxStudentCount);

	private sealed record RecommendationScore(
		double Category,
		double TitleSimilarity,
		double Instructor,
		double Rating,
		double Popularity,
		double Freshness)
	{
		public double Total =>
			Category +
			TitleSimilarity +
			Instructor +
			Rating +
			Popularity +
			Freshness;
	}
}
