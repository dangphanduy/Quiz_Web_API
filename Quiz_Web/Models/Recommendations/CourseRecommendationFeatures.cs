namespace Quiz_Web.Models.Recommendations;

/// <summary>
/// Small, algorithm-focused course representation used by the recommendation
/// pipeline. This keeps ranking independent from Entity Framework Core.
/// </summary>
public sealed record CourseRecommendationFeatures(
	int Id,
	string Title,
	int? CategoryId,
	int InstructorId,
	decimal Rating,
	int ReviewCount,
	int StudentCount,
	DateTime CreatedDate);
