using Quiz_Web.Models.Entities;

namespace Quiz_Web.Services.IServices;

public interface IRecommendationService
{
	List<Course> GetRelatedCourses(
		Course currentCourse,
		List<Course> allCourses,
		int limit = 5);

	List<Course> GetNextCoursesToLearn(
		int userId,
		Course? recentlyCompletedCourse,
		List<int> userEnrolledCourseIds,
		List<Course> allCourses,
		int limit = 10);
}
