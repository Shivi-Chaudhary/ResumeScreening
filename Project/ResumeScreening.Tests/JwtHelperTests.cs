using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using ResumeScreening.API.Helpers;
using ResumeScreening.API.Models;

namespace ResumeScreening.Tests;

public class JwtHelperTests
{
    private static JwtHelper CreateHelper()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "ThisIsATestSecretKeyForUnitTests_MinLen32",
                ["JwtSettings:Issuer"] = "TestIssuer",
                ["JwtSettings:Audience"] = "TestAudience",
                ["JwtSettings:ExpiryInHours"] = "8"
            })
            .Build();

        return new JwtHelper(config);
    }

    private static User CreateTestUser(string role = "HRAdmin") => new()
    {
        Id = 1,
        FullName = "Test User",
        Email = "test@example.com",
        PasswordHash = "hash",
        Role = role
    };

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var helper = CreateHelper();
        var token = helper.GenerateToken(CreateTestUser());

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateToken_ContainsCorrectClaims()
    {
        var helper = CreateHelper();
        var user = CreateTestUser("HRAdmin");
        var tokenStr = helper.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenStr);

        Assert.Contains(token.Claims, c => c.Type == "uid" && c.Value == "1");
        Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == "HRAdmin");
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Name && c.Value == "Test User");
    }

    [Fact]
    public void GenerateToken_ViewerRole_ContainsViewerClaim()
    {
        var helper = CreateHelper();
        var user = CreateTestUser("Viewer");
        var tokenStr = helper.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenStr);

        Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == "Viewer");
    }

    [Fact]
    public void GenerateToken_HasCorrectExpiry()
    {
        var helper = CreateHelper();
        var beforeGeneration = DateTime.UtcNow;
        var tokenStr = helper.GenerateToken(CreateTestUser());

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenStr);

        // Token should expire approximately 8 hours from now
        var expectedExpiry = beforeGeneration.AddHours(8);
        Assert.True(token.ValidTo >= expectedExpiry.AddMinutes(-1));
        Assert.True(token.ValidTo <= expectedExpiry.AddMinutes(1));
    }

    [Fact]
    public void GenerateToken_HasCorrectIssuerAndAudience()
    {
        var helper = CreateHelper();
        var tokenStr = helper.GenerateToken(CreateTestUser());

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenStr);

        Assert.Equal("TestIssuer", token.Issuer);
        Assert.Contains("TestAudience", token.Audiences);
    }

    [Fact]
    public void GenerateToken_UniqueJtiPerCall()
    {
        var helper = CreateHelper();
        var user = CreateTestUser();

        var token1 = helper.GenerateToken(user);
        var token2 = helper.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jti1 = handler.ReadJwtToken(token1).Claims.First(c => c.Type == "jti").Value;
        var jti2 = handler.ReadJwtToken(token2).Claims.First(c => c.Type == "jti").Value;

        Assert.NotEqual(jti1, jti2);
    }
}
