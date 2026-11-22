using System.Reflection;
using CareerMatch.API.DTOs;
using CareerMatch.API.Services;
using Xunit;

namespace CareerMatch.API.Tests;

/// <summary>
/// Isolated pure-method tests. No database or network calls.
/// Private static helpers are accessed by reflection rather than
/// changing the production API solely for tests.
/// </summary>
public class JobSearchPureLogicTests
{
    private static object? Call(string name, params object?[] args)
    {
        var method = typeof(JobSearchService).GetMethod(name,
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method!.Invoke(null, args);
    }

    private static JobSearchRequest Request(string role = "Backend Developer",
        string country = "Lebanon", string? city = null,
        string work = "remote", string employment = "full-time") => new()
        {
            Role = role, Country = country, City = city,
            WorkType = work, EmploymentType = employment
        };

    [Fact]
    public void SameSearchDifferingOnlyInCaseAndWhitespace_HasSameCacheKey()
    {
        var a = (string)Call("CreateSearchCacheKey", Request())!;
        var b = (string)Call("CreateSearchCacheKey", Request(
            " backend  developer ", " LEBANON ", null, " REMOTE ", "FULL-TIME"))!;
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void ChangingWorkMode_ChangesCacheKey()
    {
        Assert.NotEqual(Call("CreateSearchCacheKey", Request()),
            Call("CreateSearchCacheKey", Request(work: "hybrid")));
    }

    [Fact]
    public void ChangingCity_ChangesCacheKey()
    {
        Assert.NotEqual(Call("CreateSearchCacheKey", Request(city: null)),
            Call("CreateSearchCacheKey", Request(city: "Beirut")));
    }

    [Fact]
    public void DedupHash_IgnoresWhitespaceAndCase()
    {
        Assert.Equal(Call("CreateJobDeduplicationHash", " .NET Engineer ", "Works with SQL  Server"),
            Call("CreateJobDeduplicationHash", ".net engineer", "works with sql server"));
    }

    [Fact]
    public void DedupHash_ChangesWhenDescriptionChanges()
    {
        Assert.NotEqual(Call("CreateJobDeduplicationHash", "Engineer", "SQL"),
            Call("CreateJobDeduplicationHash", "Engineer", "C#"));
    }

    [Fact]
    public void DescriptionHash_InsensitiveToWhitespaceAndCase()
    {
        Assert.Equal(Call("CreateDescriptionHash", " C#  SQL "),
            Call("CreateDescriptionHash", "c# sql"));
    }

    [Theory]
    [InlineData("", "Lebanon", "remote", "full-time")]
    [InlineData("Developer", "", "remote", "full-time")]
    [InlineData("Developer", "Lebanon", "", "full-time")]
    [InlineData("Developer", "Lebanon", "remote", "")]
    [InlineData("Developer", "Lebanon", "teleport", "full-time")]
    [InlineData("Developer", "Lebanon", "remote", "seasonal")]
    public void InvalidSearchInput_Rejected(string role, string country, string work, string employment)
    {
        var thrown = Assert.Throws<TargetInvocationException>(() =>
            Call("ValidateSearchRequest", Request(role, country, null, work, employment)));
        Assert.IsType<ArgumentException>(thrown.InnerException);
    }
}
