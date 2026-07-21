using CareerMatch.API.Data;
using CareerMatch.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Adds support for API controllers.
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Generates the built-in OpenAPI document.
builder.Services.AddOpenApi();

// Allows Swagger to discover controller endpoints.
builder.Services.AddEndpointsApiExplorer();

// Configures Swagger and adds JWT authorization support.
builder.Services.AddSwaggerGen(options =>
{
    // The name used to identify the JWT security scheme.
    const string bearerScheme = "Bearer";

    // Defines how Swagger should accept the JWT.
    options.AddSecurityDefinition(
        bearerScheme,
        new OpenApiSecurityScheme
        {
            // Explanation shown inside Swagger's Authorize dialog.
            Description =
                "Enter your JWT token. You only need to paste the token itself.",

            // Name of the HTTP header that carries the token.
            Name = "Authorization",

            // Tells Swagger that the token is sent inside a request header.
            In = ParameterLocation.Header,

            // Uses standard HTTP authentication.
            Type = SecuritySchemeType.Http,

            // Uses the Bearer authentication scheme.
            Scheme = "bearer",

            // Indicates that the Bearer value contains a JWT.
            BearerFormat = "JWT"
        }
    );

    // Applies the Bearer authentication scheme to API operations.
    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [
                new OpenApiSecuritySchemeReference(
                    bearerScheme,
                    document
                )
            ] = new List<string>()
        }
    );
});

// Registers the Dapper database connection factory.
builder.Services.AddScoped<DbConnectionFactory>();

// Registers the authentication service.
builder.Services.AddScoped<AuthService>();

// Registers the service responsible for creating JWT tokens.
builder.Services.AddScoped<JwtService>();

// Registers the service responsible for sending reset-password emails.
builder.Services.AddScoped<EmailService>();

// Registers the CV upload and extraction service.
builder.Services.AddScoped<CVService>();

// Registers the OpenAI communication service.
builder.Services.AddScoped<AIService>();

// Registers the external job search service.
builder.Services.AddScoped<JobSearchService>();

// Registers the AI matching service.
builder.Services.AddScoped<MatchingService>();

// Registers the job-application service.
builder.Services.AddScoped<JobApplicationService>();

// Registers the saved-jobs service.
builder.Services.AddScoped<SavedJobService>();

// Registers the improved-CV generation service.
builder.Services.AddScoped<GeneratedCVService>();

// Registers the cover-letter generation service.
builder.Services.AddScoped<GeneratedCoverLetterService>();

// Registers the interview-question generation service.
builder.Services.AddScoped<GeneratedInterviewQuestionsService>();

// Registers HttpClient support for OpenAI and JSearch.
builder.Services.AddHttpClient();

// Reads the JWT signing key from configuration.
string jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? throw new Exception(
        "Jwt:Key is missing from configuration."
    );

// A key shorter than 32 characters should not be used with HMAC SHA-256.
if (jwtKey.Length < 32)
{
    throw new Exception(
        "Jwt:Key must contain at least 32 characters."
    );
}

// Reads the JWT issuer.
string jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? "CareerMatch";

// Reads the JWT audience.
string jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? "CareerMatchUsers";

// Configures JWT authentication.
builder.Services
    .AddAuthentication(options =>
    {
        // Uses JWT Bearer authentication when ASP.NET Core tries to authenticate.
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        // Uses JWT Bearer authentication when access is denied because no token exists.
        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Defines all rules used to validate incoming JWTs.
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                // Confirms that the token was created by CareerMatch.
                ValidateIssuer = true,

                // Confirms that the token was intended for CareerMatch users.
                ValidateAudience = true,

                // Rejects expired tokens.
                ValidateLifetime = true,

                // Confirms that the token signature is valid.
                ValidateIssuerSigningKey = true,

                // Sets the expected issuer.
                ValidIssuer = jwtIssuer,

                // Sets the expected audience.
                ValidAudience = jwtAudience,

                // Uses the configured secret key to validate signatures.
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)
                    ),

                // Allows only a small difference between server clocks.
                ClockSkew = TimeSpan.FromSeconds(30)
            };
    });

// Enables the use of [Authorize] attributes.
builder.Services.AddAuthorization();

// Configures the QuestPDF Community license.
QuestPDF.Settings.License =
    LicenseType.Community;

var app = builder.Build();

// Enables Swagger only during development.
if (app.Environment.IsDevelopment())
{
    // Exposes the built-in OpenAPI document.
    app.MapOpenApi();

    // Exposes the Swagger JSON document.
    app.UseSwagger();

    // Exposes Swagger UI.
    app.UseSwaggerUI();
}
app.UseCors("FrontendPolicy");
// Redirects HTTP requests to HTTPS.
app.UseHttpsRedirection();

// Reads and validates JWT tokens.
// This must appear before UseAuthorization.
app.UseAuthentication();

// Enforces [Authorize] attributes.
app.UseAuthorization();

// Maps controller routes.
app.MapControllers();

// Starts the API.
app.Run();
