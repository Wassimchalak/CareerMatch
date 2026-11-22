using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CareerMatch.API.Models;
using CareerMatch.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CareerMatch.API.Tests;

/// <summary>
/// Executes ASP.NET Core controllers, middleware, JWT authentication, model binding
/// and environment-specific endpoint registration with a real in-memory HTTP server.
/// No production database, external API, SMTP/Resend, or live user is contacted.
/// </summary>
public sealed class HttpPipelineFactory : WebApplicationFactory<Program>
{
    internal const string JwtKey = "Capstone-Integration-Key-Long-Enough-2026-Safe-Only";
    internal const string JwtIssuer = "CareerMatchPipelineTests";
    internal const string JwtAudience = "CareerMatchTestCandidates";

    internal static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtKey,
            ["Jwt:Issuer"] = JwtIssuer,
            ["Jwt:Audience"] = JwtAudience,
            ["Jwt:ExpireMinutes"] = "10",
            ["Resend:ApiKey"] = "integration-test-only-placeholder",
            ["ConnectionStrings:DefaultConnection"] =
                "Server=127.0.0.1,1;Database=DO_NOT_CONNECT;User Id=none;Password=none;TrustServerCertificate=True;"
        }).Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseContentRoot(FindApiContentRoot());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                BuildConfiguration().AsEnumerable()
                    .ToDictionary(pair => pair.Key, pair => pair.Value));
        });
    }

    private static string FindApiContentRoot()
    {
        for (DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);
             folder != null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "CareerMatch.API");
            if (File.Exists(Path.Combine(candidate, "CareerMatch.API.csproj")) &&
                File.Exists(Path.Combine(candidate, "Resources", "Fonts",
                    "NotoSansArabic-Regular.ttf")))
                return candidate;
        }
        throw new InvalidOperationException("Unable to locate CareerMatch.API source and fonts.");
    }
}

public class HttpPipelineIntegrationTests : IClassFixture<HttpPipelineFactory>
{
    private readonly HttpClient _client;

    public HttpPipelineIntegrationTests(HttpPipelineFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task HealthEndpoint_WorksWithoutDatabaseOrAuthentication()
    {
        using var result = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("CareerMatch.API", json.RootElement.GetProperty("service").GetString());
    }

    [Theory]
    [InlineData("/api/SavedJob/mine")]
    [InlineData("/api/JobApplication/mine")]
    [InlineData("/api/SavedJob/123/calculate-score")]
    public async Task ProtectedRoutes_RejectUnauthenticatedRequests(string route)
    {
        using var result = route.Contains("calculate-score", StringComparison.Ordinal)
            ? await _client.PostAsync(route, null)
            : await _client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task ProductionDoesNotPublishInteractiveApiDocumentation(string route)
    {
        using var result = await _client.GetAsync(route);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    [Fact]
    public async Task InvalidLoginJson_IsRejectedBeforeAnyDatabaseCall()
    {
        using var payload = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
        using var result = await _client.PostAsync("/api/Auth/login", payload);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Theory]
    [InlineData("/api/SavedJob/save")]
    [InlineData("/api/JobApplication/apply")]
    public async Task InvalidJobId_RejectsAnAuthenticatedRequestWithoutDatabaseAccess(string route)
    {
        var user = new User { UserId = 987, Email = "test@example.invalid",
            FullName = "Test-Only Candidate" };
        var (token, _) = new JwtService(HttpPipelineFactory.BuildConfiguration()).GenerateToken(user);
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(new { JobId = 0 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var result = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
}
