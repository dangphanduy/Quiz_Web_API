using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Services;
using Quiz_Web.Services.IServices;
using Ganss.Xss;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Elasticsearch;
using Quiz_Web.Extensions;
using Quiz_Web.Models.PayOSPayment;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var logRoot = Path.Combine(builder.Environment.ContentRootPath, "logs");
var logFolders = new[] { "requests", "payment", "chat", "learning", "user", "system", "errors" };
const string logTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

foreach (var folder in logFolders)
{
    Directory.CreateDirectory(Path.Combine(logRoot, folder));
}

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Kafka(
        formatter: new ElasticsearchJsonFormatter(),
        bootstrapServers: builder.Configuration["Serilog:WriteTo:1:Args:bootstrapServers"] ?? "localhost:9094",
        topic: "quiz-logs")
    .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(IsHttpRequestLog)
            .WriteTo.File(
                Path.Combine(logRoot, "requests", "requests-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: logTemplate))
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(IsPaymentLog)
            .WriteTo.File(
                Path.Combine(logRoot, "payment", "payment-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: logTemplate))
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(IsChatLog)
            .WriteTo.File(
                Path.Combine(logRoot, "chat", "chat-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: logTemplate))
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(IsLearningLog)
            .WriteTo.File(
                Path.Combine(logRoot, "learning", "learning-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: logTemplate))
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(IsUserLog)
            .WriteTo.File(
                Path.Combine(logRoot, "user", "user-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: logTemplate))
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(IsSystemLog)
            .WriteTo.File(
                Path.Combine(logRoot, "system", "system-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: logTemplate))
        .WriteTo.File(
            Path.Combine(logRoot, "errors", "errors-.txt"),
            restrictedToMinimumLevel: LogEventLevel.Error,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            buffered: false,
            shared: true,
            outputTemplate: logTemplate)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllersWithViews();

// ? Add API Controllers support
builder.Services.AddControllers();

//session
builder.Services.AddSession(options=>
{
    options.IdleTimeout = TimeSpan.FromHours(3);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = "Quiz";
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "JWT_OR_COOKIE";
    options.DefaultChallengeScheme = "JWT_OR_COOKIE";
})
.AddPolicyScheme("JWT_OR_COOKIE", "JWT_OR_COOKIE", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        string authorization = context.Request.Headers.Authorization;
        if (!string.IsNullOrEmpty(authorization) && authorization.ToString().StartsWith("Bearer "))
        {
            return Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        }
        return Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddCookie(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.ExpireTimeSpan = TimeSpan.FromHours(3);
    options.LoginPath = "/login";
})
.AddJwtBearer(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, options =>
{
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var key = System.Text.Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "SuperSecretKeyForQuizWebAPI2026SecureEncryptionWithAtLeast256Bits");

    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => true;
    options.MinimumSameSitePolicy = SameSiteMode.None;
});

builder.Services.AddDbContext<LearningPlatformContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")),
    ServiceLifetime.Scoped);

// Configure PayOS settings
builder.Services.Configure<PayOSSettings>(builder.Configuration.GetSection("PayOSSettings"));

// Register PayOS Client as Singleton
builder.Services.AddSingleton(sp =>
{
    var settings = builder.Configuration.GetSection("PayOSSettings").Get<PayOSSettings>()
                   ?? throw new InvalidOperationException("PayOSSettings is missing from configuration.");
    return new PayOS.PayOSClient(new PayOS.PayOSOptions
    {
        ClientId = settings.ClientId,
        ApiKey = settings.ApiKey,
        ChecksumKey = settings.ChecksumKey
    });
});

// Register IPayOSService
builder.Services.AddScoped<IPayOSService, PayOSService>();

builder.Services.AddHttpClient<Quiz_Web.Services.IServices.ITokenService, Quiz_Web.Services.TokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<ITestService, TestService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFlashcardService, FlashcardService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICreateTestService, CreateTestService>();
builder.Services.AddScoped<ILessonService, LessonService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddSingleton<IStorageService, GoogleCloudStorageService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<ICourseAccessService, CourseAccessService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSignalR();
builder.Services.AddSingleton(TimeProvider.System);

// Register background service for course recommendations
builder.Services.AddHostedService<CourseRecommendationService>();

// Html sanitizer for CKEditor content
builder.Services.AddSingleton(sp =>
{
    var s = new HtmlSanitizer();
    s.AllowedSchemes.Add("data"); // allow data URLs if you paste images
    return s;
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.DocInclusionPredicate((docName, apiDesc) =>
    {
        return apiDesc.RelativePath != null && apiDesc.RelativePath.StartsWith("api/", StringComparison.OrdinalIgnoreCase);
    });
});

// Register exception handler
builder.Services.AddExceptionHandler<Quiz_Web.Extensions.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler("/Error");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Quiz Web API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// ? Map API Controllers FIRST (before MVC routes)
app.MapControllers();
app.MapHub<Quiz_Web.Hubs.ChatHub>("/chatHub");

// Add explicit route for Onboarding
app.MapControllerRoute(
    name: "onboarding",
    pattern: "Onboarding/{action=Index}/{id?}",
    defaults: new { controller = "Onboarding" });

// Add route for checkout
app.MapControllerRoute(
    name: "checkout",
    pattern: "checkout",
    defaults: new { controller = "Checkout", action = "Index" });

// Add route for Checkout controller
app.MapControllerRoute(
    name: "checkoutController",
    pattern: "Checkout/{action=Index}/{id?}",
    defaults: new { controller = "Checkout" });

// Route m?c ??nh tr? ??n Welcome action ?? x? l� logic
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Welcome}/{id?}")
    .WithStaticAssets();

app.Run();

static bool IsHttpRequestLog(LogEvent logEvent)
{
    return SourceContext(logEvent) == "Serilog.AspNetCore.RequestLoggingMiddleware";
}

static bool IsPaymentLog(LogEvent logEvent)
{
    var source = SourceContext(logEvent);

    return ContainsAny(
        source,
        "PaymentController",
        "PaymentApiController",
        "CheckoutController",
        "CartApiController",
        "PayOSService",
        "PurchaseService",
        "SubscriptionService",
        "CartService",
        "CourseAccessService");
}

static bool IsChatLog(LogEvent logEvent)
{
    var source = SourceContext(logEvent);

    return ContainsAny(
        source,
        "ChatController",
        "ChatApiController",
        "ChatHub",
        "ChatService");
}

static bool IsLearningLog(LogEvent logEvent)
{
    var source = SourceContext(logEvent);

    return ContainsAny(
        source,
        "CourseController",
        "CourseApiController",
        "CourseBuilderApiController",
        "CourseProgressController",
        "CourseService",
        "CourseRecommendationService",
        "LessonController",
        "LessonApiController",
        "LessonService",
        "CreateLessonController",
        "CreateLessonService",
        "CreateTestController",
        "CreateTestService",
        "CreateTextController",
        "CreateTextService",
        "TestController",
        "TestApiController",
        "TestService",
        "FlashcardController",
        "FlashcardApiController",
        "FlashcardService",
        "ReviewController",
        "ReviewApiController",
        "ReviewService",
        "CertificateController",
        "CertificateService",
        "LibraryController",
        "LibraryApiController",
        "LibraryService");
}

static bool IsUserLog(LogEvent logEvent)
{
    var source = SourceContext(logEvent);

    return ContainsAny(
        source,
        "AccountController",
        "AccountApiController",
        "AuthController",
        "OnboardingController",
        "OnboardingApiController",
        "UserService",
        "EmailService",
        "TokenService");
}

static bool IsSystemLog(LogEvent logEvent)
{
    return !IsHttpRequestLog(logEvent)
        && !IsPaymentLog(logEvent)
        && !IsChatLog(logEvent)
        && !IsLearningLog(logEvent)
        && !IsUserLog(logEvent);
}

static string SourceContext(LogEvent logEvent)
{
    return logEvent.Properties.TryGetValue("SourceContext", out var value)
        && value is ScalarValue { Value: string sourceContext }
            ? sourceContext
            : string.Empty;
}

static bool ContainsAny(string value, params string[] keywords)
{
    return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}

public partial class Program { }
