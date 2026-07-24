using CareerMatch.API.Data;
using CareerMatch.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//
// Controllers
//
builder.Services.AddControllers();

//
// CORS
//
// Local React frontend is allowed now.
// Later, add your deployed Vercel URL as another origin.
//
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173"
                // Add your Vercel URL later:
                // "https://your-project.vercel.app"
            )
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
// HttpClient
//
builder.Services.AddHttpClient();

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
// Build the application
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
// Render terminates HTTPS before forwarding requests to the container.
// Keep HTTPS redirection for local development only.
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
// Start the application
//
app.Run();