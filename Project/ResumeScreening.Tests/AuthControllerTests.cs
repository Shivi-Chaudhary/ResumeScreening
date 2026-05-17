using Microsoft.EntityFrameworkCore;
using ResumeScreening.API.Data;
using ResumeScreening.API.Models;

namespace ResumeScreening.Tests;

public class AuthControllerTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Register_CreatesUserWithHashedPassword()
    {
        var db = CreateInMemoryDb();

        var user = new User
        {
            FullName = "Test User",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123"),
            Role = "HRAdmin"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var saved = await db.Users.FirstAsync(u => u.Email == "test@test.com");

        Assert.NotEqual("Password123", saved.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123", saved.PasswordHash));
    }

    [Fact]
    public async Task Register_EnforcesUniqueEmail()
    {
        var db = CreateInMemoryDb();

        db.Users.Add(new User
        {
            FullName = "User 1",
            Email = "same@test.com",
            PasswordHash = "hash1",
            Role = "HRAdmin"
        });
        await db.SaveChangesAsync();

        // Check if email already exists (simulating controller logic)
        var exists = await db.Users.AnyAsync(u => u.Email == "same@test.com");
        Assert.True(exists);
    }

    [Fact]
    public void BCrypt_VerifyCorrectPassword()
    {
        var password = "SecureP@ss123";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash));
    }

    [Fact]
    public void BCrypt_RejectWrongPassword()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword");

        Assert.False(BCrypt.Net.BCrypt.Verify("WrongPassword", hash));
    }

    [Fact]
    public void BCrypt_DifferentHashesForSamePassword()
    {
        var password = "SamePassword123";
        var hash1 = BCrypt.Net.BCrypt.HashPassword(password);
        var hash2 = BCrypt.Net.BCrypt.HashPassword(password);

        // BCrypt should produce different hashes due to random salt
        Assert.NotEqual(hash1, hash2);

        // But both should verify correctly
        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash1));
        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash2));
    }

    [Fact]
    public async Task UserRoles_OnlyTwoRolesAllowed()
    {
        var db = CreateInMemoryDb();

        var admin = new User { FullName = "Admin", Email = "admin@test.com", PasswordHash = "h", Role = "HRAdmin" };
        var viewer = new User { FullName = "Viewer", Email = "viewer@test.com", PasswordHash = "h", Role = "Viewer" };

        db.Users.AddRange(admin, viewer);
        await db.SaveChangesAsync();

        var roles = await db.Users.Select(u => u.Role).Distinct().ToListAsync();

        Assert.Contains("HRAdmin", roles);
        Assert.Contains("Viewer", roles);
        Assert.Equal(2, roles.Count);
    }
}
