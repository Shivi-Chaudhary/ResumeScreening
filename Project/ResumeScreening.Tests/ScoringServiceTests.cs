using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ResumeScreening.API.Data;
using ResumeScreening.API.Models;
using ResumeScreening.API.Services;

namespace ResumeScreening.Tests;

public class ScoringServiceTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(AppDbContext db, int jobId)> SeedJobWithResumes(
        string jdText, params string[] resumeTexts)
    {
        var db = CreateInMemoryDb();

        var user = new User
        {
            FullName = "Test Admin",
            Email = "admin@test.com",
            PasswordHash = "hash",
            Role = "HRAdmin"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var job = new Job
        {
            Title = "Test Job",
            Description = jdText,
            CreatedByUserId = user.Id
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        foreach (var text in resumeTexts)
        {
            db.Resumes.Add(new Resume
            {
                CandidateName = "Candidate",
                FileUrl = "http://test.com/resume.pdf",
                ExtractedText = text,
                Status = "Extracted",
                JobId = job.Id
            });
        }
        await db.SaveChangesAsync();

        return (db, job.Id);
    }

    [Fact]
    public async Task ScoreAllResumes_ScoresAllResumesForJob()
    {
        var jd = "Looking for a Java Spring Boot developer with REST API and microservices experience";
        var (db, jobId) = await SeedJobWithResumes(jd,
            "Java Spring Boot REST API microservices Docker",
            "Python Django machine learning data science"
        );

        var service = new ScoringService(db, Mock.Of<ILogger<ScoringService>>());
        var scored = await service.ScoreAllResumesForJobAsync(jobId);

        Assert.Equal(2, scored);
    }

    [Fact]
    public async Task ScoreAllResumes_HigherScoreForBetterMatch()
    {
        var jd = "Need Java Spring Boot developer with REST API and microservices experience";
        var (db, jobId) = await SeedJobWithResumes(jd,
            "5 years Java Spring Boot REST API microservices experience. Bachelor degree in Computer Science.",
            "Python Django web developer with no Java experience"
        );

        var service = new ScoringService(db, Mock.Of<ILogger<ScoringService>>());
        await service.ScoreAllResumesForJobAsync(jobId);

        var scores = await db.ScoreResults
            .Where(s => s.JobId == jobId)
            .OrderByDescending(s => s.Score)
            .ToListAsync();

        Assert.Equal(2, scores.Count);
        Assert.True(scores[0].Score > scores[1].Score,
            $"Java resume ({scores[0].Score}) should score higher than Python resume ({scores[1].Score})");
    }

    [Fact]
    public async Task ScoreAllResumes_ScoreWithinValidRange()
    {
        var jd = "Software Engineer with cloud computing AWS experience";
        var (db, jobId) = await SeedJobWithResumes(jd,
            "AWS cloud computing DevOps engineer with 3 years experience"
        );

        var service = new ScoringService(db, Mock.Of<ILogger<ScoringService>>());
        await service.ScoreAllResumesForJobAsync(jobId);

        var score = await db.ScoreResults.FirstAsync(s => s.JobId == jobId);

        Assert.InRange(score.Score, 0, 100);
    }

    [Fact]
    public async Task ScoreAllResumes_GeneratesBreakdownJson()
    {
        var jd = "React TypeScript frontend developer needed";
        var (db, jobId) = await SeedJobWithResumes(jd,
            "React TypeScript frontend developer with 2 years experience"
        );

        var service = new ScoringService(db, Mock.Of<ILogger<ScoringService>>());
        await service.ScoreAllResumesForJobAsync(jobId);

        var score = await db.ScoreResults.FirstAsync(s => s.JobId == jobId);

        Assert.NotNull(score.ScoreBreakdownJson);
        Assert.Contains("KeywordMatch", score.ScoreBreakdownJson);
        Assert.Contains("ExperienceBonus", score.ScoreBreakdownJson);
        Assert.Contains("SkillsBonus", score.ScoreBreakdownJson);
        Assert.Contains("DegreeBonus", score.ScoreBreakdownJson);
    }

    [Fact]
    public async Task ScoreAllResumes_ThrowsForNonExistentJob()
    {
        var db = CreateInMemoryDb();
        var service = new ScoringService(db, Mock.Of<ILogger<ScoringService>>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ScoreAllResumesForJobAsync(999));
    }

    [Fact]
    public async Task ScoreAllResumes_ThrowsWhenNoResumes()
    {
        var db = CreateInMemoryDb();
        var user = new User { FullName = "Admin", Email = "a@test.com", PasswordHash = "h", Role = "HRAdmin" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var job = new Job { Title = "Empty Job", Description = "Some description", CreatedByUserId = user.Id };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var service = new ScoringService(db, Mock.Of<ILogger<ScoringService>>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ScoreAllResumesForJobAsync(job.Id));
    }

    [Fact]
    public async Task ScoreAllResumes_ClearsOldScoresOnRescreen()
    {
        var jd = "Java developer needed";
        var (db, jobId) = await SeedJobWithResumes(jd, "Java developer expert");

        var service = new ScoringService(db, Mock.Of<ILogger<ScoringService>>());

        // Score once
        await service.ScoreAllResumesForJobAsync(jobId);
        var firstScore = (await db.ScoreResults.FirstAsync(s => s.JobId == jobId)).Score;

        // Score again — old scores should be replaced
        await service.ScoreAllResumesForJobAsync(jobId);
        var count = await db.ScoreResults.CountAsync(s => s.JobId == jobId);

        Assert.Equal(1, count); // Only one score, not duplicated
    }

    [Fact]
    public async Task ScoreAllResumes_SetsMatchedKeywords()
    {
        var jd = "Python data science machine learning pandas numpy";
        var (db, jobId) = await SeedJobWithResumes(jd,
            "Python data science machine learning pandas numpy sklearn"
        );

        var service = new ScoringService(db, Mock.Of<ILogger<ScoringService>>());
        await service.ScoreAllResumesForJobAsync(jobId);

        var score = await db.ScoreResults.FirstAsync(s => s.JobId == jobId);

        Assert.NotNull(score.MatchedKeywords);
        Assert.True(score.MatchedKeywords.Length > 0);
    }
}
