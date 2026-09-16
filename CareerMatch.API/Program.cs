using CareerMatch.API.Data;
using CareerMatch.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;
using Resend;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

string[] configuredOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? [];

string[] allowedOrigins =
    configuredOrigins
        .Concat(
        [
            "http://localhost:5173",
            "https://career-match-iota.vercel.app",
            "https://career-match-app.com",
            "https://www.career-match-app.com"
        ])
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    const string bearerScheme = "Bearer";

    options.AddSecurityDefinition(
        bearerScheme,
        new OpenApiSecurityScheme
        {
            Description =
                "Enter your JWT token. Paste only the token itself.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [
                new OpenApiSecuritySchemeReference(
                    bearerScheme,
                    document
                )
            ] = new List<string>()
        });
});

builder.Services.AddScoped<DbConnectionFactory>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<CVService>();
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<JobSearchService>();
builder.Services.AddScoped<MatchingService>();
builder.Services.AddScoped<JobApplicationService>();
builder.Services.AddScoped<SavedJobService>();
builder.Services.AddScoped<GeneratedCVService>();
builder.Services.AddScoped<GeneratedCoverLetterService>();
builder.Services.AddScoped<GeneratedInterviewQuestionsService>();
builder.Services.AddHttpClient();
string resendApiKey =
    builder.Configuration["Resend:ApiKey"]
    ?? throw new Exception(
        "Resend:ApiKey is missing from configuration."
    );

builder.Services.AddOptions();

builder.Services.AddHttpClient<ResendClient>();

builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken = resendApiKey;
});

builder.Services.AddTransient<IResend, ResendClient>();

string jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? throw new Exception(
        "Jwt:Key is missing from configuration."
    );

if (jwtKey.Length < 32)
{
    throw new Exception(
        "Jwt:Key must contain at least 32 characters."
    );
}

string jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? "CareerMatch";

string jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? "CareerMatchUsers";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

builder.Services.AddAuthorization();
QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.UseEnvironmentFonts = false;
QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;

string fontsPath =
    Path.Combine(
        builder.Environment.ContentRootPath,
        "Resources",
        "Fonts"
    );

string regularFontPath =
    Path.Combine(
        fontsPath,
        "NotoSansArabic-Regular.ttf"
    );

string boldFontPath =
    Path.Combine(
        fontsPath,
        "NotoSansArabic-Bold.ttf"
    );

if (!File.Exists(regularFontPath))
{
    throw new FileNotFoundException(
        "NotoSansArabic-Regular.ttf was not found.",
        regularFontPath
    );
}

if (!File.Exists(boldFontPath))
{
    throw new FileNotFoundException(
        "NotoSansArabic-Bold.ttf was not found.",
        boldFontPath
    );
}

using (FileStream regularFontStream =
       File.OpenRead(regularFontPath))
{
    FontManager.RegisterFont(
        regularFontStream
    );
}

using (FileStream boldFontStream =
       File.OpenRead(boldFontPath))
{
    FontManager.RegisterFont(
        boldFontStream
    );
}
var app = builder.Build();
app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("FrontendPolicy");
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        service = "CareerMatch.API",
        timestamp = DateTime.UtcNow
    });
});
app.Run();