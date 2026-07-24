using CareerMatch.API.Data;
using CareerMatch.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;
using Resend;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//
// Controllers
//
builder.Services.AddControllers();

//
// CORS
//
// Always allow the local React frontend and the deployed Vercel frontend.
// Additional origins can still be added through configuration.
//
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
            "https://career-match-iota.vercel.app"
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

//
// OpenAPI and Swagger
//
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

//
// CareerMatch services
//
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

//
// General HttpClient support
//
builder.Services.AddHttpClient();

//
// Resend email API
//
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

//
// JWT configuration
//
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

//
// QuestPDF
//
QuestPDF.Settings.License = LicenseType.Community;

//
// Build application
//
var app = builder.Build();

//
// OpenAPI and Swagger
//
app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI();

//
// CORS must run before authentication and authorization.
//
app.UseCors("FrontendPolicy");

//
// Render handles HTTPS externally.
// Keep local HTTPS redirection outside production.
//
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

//
// Authentication and authorization
//
app.UseAuthentication();
app.UseAuthorization();

//
// Controller endpoints
//
app.MapControllers();

//
// Render health-check endpoint
//
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "healthy",
        service = "CareerMatch.API",
        timestamp = DateTime.UtcNow
    });
});

//
// Start application
//
app.Run();