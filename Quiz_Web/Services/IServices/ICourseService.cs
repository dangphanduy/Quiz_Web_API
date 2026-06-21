using Quiz_Web.Models.Entities;
using Quiz_Web.Models.ViewModels;

namespace Quiz_Web.Services.IServices
{
	public interface ICourseService
	{
		List<Course> GetAllPublishedCourses();
		Course? GetCourseById(int id);
		Course? GetCourseBySlug(string slug);
		Course? GetCourseBySlugWithFullDetails(string slug);
		List<Course> GetCoursesByCategory(string category);
		List<Course> SearchCourses(string keyword);
		
		// New method for filtering and sorting
		List<Course> GetFilteredAndSortedCourses(
			string? searchKeyword = null,
			string? categorySlug = null,
			decimal? minRating = null,
			decimal? maxRating = null,
			bool? isFree = null,
			string? sortBy = null);
		
		Course? CreateCourse(CreateCourseViewModel model, int ownerId);
		bool IsSlugUnique(string slug);
        // New overload: allow excluding a course when checking uniqueness (for edit/builder)
        bool IsSlugUnique(string slug, int? excludeCourseId);
		List<Course> GetCoursesByOwner(int ownerId);
		Course? GetOwnedCourse(int id, int ownerId);
		Course? UpdateCourse(EditCourseViewModel model, int ownerId, string? sanitizedDescription);
        bool DeleteCourse(int id, int ownerId, string? webRootPath);

		// New methods for Course Builder
		List<CourseCategory> GetAllCategories();
		Course? CreateCourseWithStructure(CourseBuilderViewModel model, int ownerId);
		Course? UpdateCourseStructure(int courseId, CourseBuilderViewModel model, int ownerId);
		Course? GetCourseWithFullStructure(int courseId, int ownerId);
		CourseBuilderViewModel? GetCourseBuilderData(int courseId, int ownerId);
		bool AutosaveCourse(int? courseId, CourseAutosaveViewModel model, int ownerId);
		
		// Home page recommendations
		List<Course> GetRecommendedCourses(int userId, int count = 6);
		List<Course> GetTopRatedCourses(int count = 6);

		// Contextual recommendations for the course detail page
		List<Course> GetRelatedCourses(int courseId, int count = 6);
	}
}
