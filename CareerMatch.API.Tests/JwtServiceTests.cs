using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CareerMatch.API.Models;
using CareerMatch.API.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CareerMatch.API.Tests;

public class JwtServiceTests
{
    private static JwtService Service(string secret = "A-TEST-ONLY-KEY-THAT-IS-AT-LEAST-32-CHARS") =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = secret,
            ["Jwt:Issuer"] = "CareerMatchTests",
            ["Jwt:Audience"] = "TestCandidates",
            ["Jwt:ExpireMinutes"] = "5"
        }).Build());

    [Fact]
    public void GeneratedToken_ContainsExpectedUserAndIssuer()
    {
        var user = new User { UserId = 42, Email = "test@example.com", FullName = "Test Candidate" };
        var (token, expiry) = Service().GenerateToken(user);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("CareerMatchTests", parsed.Issuer);
        Assert.Contains("TestCandidates", parsed.Audiences);
        Assert.Contains(parsed.Claims, claim =>
            (claim.Type == ClaimTypes.NameIdentifier || claim.Type == "nameid" || claim.Type == "sub")
            && claim.Value == "42");
        Assert.InRange((expiry - DateTime.UtcNow).TotalMinutes, 4, 6);
    }

    [Fact]
    public void WeakJwtKey_IsRejected()
    {
        var user = new User { UserId = 1, Email = "a@b.com", FullName = "Candidate" };
        Assert.Throws<Exception>(() => Service("short").GenerateToken(user));
    }
}
