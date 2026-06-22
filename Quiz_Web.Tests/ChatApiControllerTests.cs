using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Quiz_Web.Controllers.API;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;
using Xunit;

namespace Quiz_Web.Tests;

public class ChatApiControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatApiControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetConversations_NoAuth_ReturnsRedirect()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/api/chat/conversations");

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task GetOrCreateConversation_ValidAccess_ReturnsOk()
    {
        // Arrange
        var mockChatService = new Mock<IChatService>();
        var mockStorageService = new Mock<IStorageService>();

        var dummyConversation = new ChatConversation
        {
            ConversationId = 1,
            StudentId = 10,
            InstructorId = 20,
            CourseId = 5,
            Student = new User { UserId = 10, FullName = "Học Viên A" },
            Instructor = new User { UserId = 20, FullName = "Giảng Viên B" },
            Course = new Course { CourseId = 5, Title = "Khóa Học Lập Trình" }
        };

        mockChatService.Setup(s => s.CanUserChatInCourseAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        mockChatService.Setup(s => s.GetOrCreateConversationAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(dummyConversation);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => mockChatService.Object);
                services.AddScoped(_ => mockStorageService.Object);
                
                // Cấu hình Fake Authentication để mock một User đã đăng nhập
                services.AddAuthentication("TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("TestScheme");

        // Act
        var response = await client.PostAsync("/api/chat/get-or-create?courseId=5", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ChatCreateResponse>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(1, result.ConversationId);
        Assert.Equal("Học Viên A", result.StudentName);
        Assert.Equal("Giảng Viên B", result.InstructorName);
        Assert.Equal("Khóa Học Lập Trình", result.CourseTitle);
    }

    private class ChatCreateResponse
    {
        public bool Success { get; set; }
        public int ConversationId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int InstructorId { get; set; }
        public string InstructorName { get; set; } = string.Empty;
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
    }
}

// Handler giả lập authentication phục vụ integration tests
public class TestAuthHandler : Microsoft.AspNetCore.Authentication.SignInAuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Role, "Student")
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        return Task.CompletedTask;
    }

    protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
    {
        return Task.CompletedTask;
    }
}
