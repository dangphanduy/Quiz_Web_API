using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;
using Xunit;

namespace Quiz_Web.Tests
{
    public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public IntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetCourses_ReturnsSuccessAndPaginatedData()
        {
            // Arrange
            var mockCourseService = new Mock<ICourseService>();
            var mockContext = new Mock<LearningPlatformContext>();

            // Stub course list
            var dummyCourses = new List<Course>
            {
                new Course
                {
                    CourseId = 1,
                    Title = "Test Course 1",
                    Slug = "test-course-1",
                    Summary = "Test Summary 1",
                    Price = 99.99m,
                    AverageRating = 4.5m,
                    TotalReviews = 10,
                    Category = new CourseCategory { Name = "IT", Slug = "it" }
                }
            };

            mockCourseService.Setup(s => s.GetFilteredAndSortedCourses(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(),
                It.IsAny<decimal?>(), It.IsAny<bool?>(), It.IsAny<string>()
            )).Returns(dummyCourses);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Replace registration with mock instances
                    services.AddScoped(_ => mockCourseService.Object);
                    services.AddScoped(_ => mockContext.Object);
                });
            }).CreateClient();

            // Act
            var response = await client.GetAsync("/api/CourseApi");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var jsonResult = await response.Content.ReadFromJsonAsync<ApiResponse>();
            Assert.NotNull(jsonResult);
            Assert.True(jsonResult.Success);
            Assert.Equal(1, jsonResult.TotalCount);
            Assert.Single(jsonResult.Courses);
            Assert.Equal("Test Course 1", jsonResult.Courses[0].Title);
            Assert.Equal("test-course-1", jsonResult.Courses[0].Slug);
        }

        [Fact]
        public async Task GetCourseDetail_NotFound_ReturnsNotFoundResponse()
        {
            // Arrange
            var mockCourseService = new Mock<ICourseService>();
            var mockContext = new Mock<LearningPlatformContext>();

            mockCourseService.Setup(s => s.GetCourseBySlugWithFullDetails(It.IsAny<string>()))
                .Returns((Course)null!);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddScoped(_ => mockCourseService.Object);
                    services.AddScoped(_ => mockContext.Object);
                });
            }).CreateClient();

            // Act
            var response = await client.GetAsync("/api/CourseApi/non-existent-slug");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // Helper classes to deserialize API response
        private class ApiResponse
        {
            public bool Success { get; set; }
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public List<CourseDto> Courses { get; set; } = new();
        }

        private class CourseDto
        {
            public int CourseId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public decimal AverageRating { get; set; }
            public int TotalReviews { get; set; }
            public string CategoryName { get; set; } = string.Empty;
            public string CategorySlug { get; set; } = string.Empty;
        }
    }
}
