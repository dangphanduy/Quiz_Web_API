using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Services;
using Quiz_Web.Services.IServices;
using Ganss.Xss;
using Quiz_Web.Models.MoMoPayment;

var builder = WebApplication.CreateBuilder(args);

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

// Configure MoMo settings
builder.Services.Configure<MoMoSettings>(builder.Configuration.GetSection("MoMoSettings"));

// Register HttpClient for MoMoPaymentService
builder.Services.AddHttpClient<IMoMoPaymentService, MoMoPaymentService>();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
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
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// ? Map API Controllers FIRST (before MVC routes)
app.MapControllers();

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
