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

		if (limit <= 0 || allCourses.Count == 0)
			return new List<Course>();

		var currentFeatures = MapFeatures(currentCourse);
		var publishedCatalogue = allCourses
			.Where(course => course.IsPublished)
			.Select(course => new CourseCandidate(course, MapFeatures(course)))
			.ToList();
		var catalogue = publishedCatalogue
			.Where(item => item.Features.Id != currentCourse.CourseId)
			.ToList();

		if (catalogue.Count == 0)
			return new List<Course>();

		// Popularity must be normalized against the complete catalogue.
		var maxReviewCount = Math.Max(
			1,
			publishedCatalogue.Max(item => item.Features.ReviewCount));
		var maxStudentCount = Math.Max(
			1,
			publishedCatalogue.Max(item => item.Features.StudentCount));

		// Stage 1: cheap matching reduces the catalogue to at most 30 candidates.
		var candidates = RetrieveCandidates(currentFeatures, catalogue);
		var now = DateTime.UtcNow;

		// Stage 2: full weighted scoring is only applied to retrieved candidates.
		return candidates
			.Select(candidate => new RankedCourse(
				candidate.Course,
				CalculateScore(
					currentFeatures,
					candidate.Features,
					maxReviewCount,
					maxStudentCount,
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
		var currentTitleTokens = Tokenize(currentCourse.Title);

		var candidates = catalogue
			.Select(candidate =>
			{
				var candidateTokens = Tokenize(candidate.Features.Title);
				var sharedKeywordCount = currentTitleTokens.Intersect(candidateTokens).Count();
				var isSameCategory = currentCourse.CategoryId.HasValue &&
					candidate.Features.CategoryId == currentCourse.CategoryId;

				return new RetrievalCandidate(candidate, isSameCategory, sharedKeywordCount);
			})
			// Retrieval rule: same category OR a shared title keyword.
			.Where(item => item.IsSameCategory || item.SharedKeywordCount > 0)
			.OrderByDescending(item => item.IsSameCategory)
			.ThenByDescending(item => item.SharedKeywordCount)
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
		int maxReviewCount,
		int maxStudentCount,
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
		var reviewRatio = Math.Clamp((double)candidate.ReviewCount / maxReviewCount, 0, 1);
		var studentRatio = Math.Clamp((double)candidate.StudentCount / maxStudentCount, 0, 1);
		var popularityScore = ((reviewRatio + studentRatio) / 2) * PopularityWeight;

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
				output.Append(character == 'đ' ? 'd' : character == 'Đ' ? 'D' : character);
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
		int SharedKeywordCount);

	private sealed record RankedCourse(Course Course, RecommendationScore Score);

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
