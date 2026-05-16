using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ResumeScreening.API.Data;
using ResumeScreening.API.Helpers;
using ResumeScreening.API.Models;

namespace ResumeScreening.API.Services
{
    /// <summary>
    /// AI scoring engine: extracts JD keywords via TF-IDF, scores each resume,
    /// adds bonus points for experience / skills / degree, and persists results.
    /// </summary>
    public class ScoringService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ScoringService> _logger;

        public ScoringService(AppDbContext db, ILogger<ScoringService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Run AI screening for all resumes with extracted text under the given job.
        /// Overwrites any prior ScoreResult rows for those resumes.
        /// </summary>
        public async Task<int> ScoreAllResumesForJobAsync(int jobId, CancellationToken ct = default)
        {
            var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
            if (job == null)
                throw new InvalidOperationException("Job not found.");

            // Build the JD text to extract keywords from
            var jdText = job.JdExtractedText ?? job.Description;
            if (string.IsNullOrWhiteSpace(jdText))
                throw new InvalidOperationException("Job has no description or extracted JD text to screen against.");

            // Get all resumes that have extracted text
            var resumes = await _db.Resumes
                .Where(r => r.JobId == jobId && r.ExtractedText != null && r.ExtractedText != "")
                .ToListAsync(ct);

            if (resumes.Count == 0)
                throw new InvalidOperationException("No resumes with extracted text found for this job.");

            // Extract keywords from JD using TF-IDF
            // We build a small corpus: JD + all resume texts, then take JD keywords
            var corpus = new List<string> { jdText };
            corpus.AddRange(resumes.Select(r => r.ExtractedText!));
            var tfidfResults = TfIdfHelper.ComputeTfIdf(corpus, topN: 40);
            var jdKeywords = tfidfResults[0].Keys.ToList();

            _logger.LogInformation("Job {JobId}: extracted {Count} TF-IDF keywords from JD: {Keywords}",
                jobId, jdKeywords.Count, string.Join(", ", jdKeywords.Take(15)));

            // Remove existing scores for this job (re-screening)
            var existingScores = await _db.ScoreResults
                .Where(s => s.JobId == jobId)
                .ToListAsync(ct);
            if (existingScores.Count > 0)
            {
                _db.ScoreResults.RemoveRange(existingScores);
                await _db.SaveChangesAsync(ct);
            }

            int scored = 0;

            foreach (var resume in resumes)
            {
                var result = ScoreResume(resume, jdKeywords, jdText, jobId);
                _db.ScoreResults.Add(result);

                resume.Status = "Scored";
                scored++;
            }

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Job {JobId}: scored {Count} resumes.", jobId, scored);
            return scored;
        }

        /// <summary>
        /// Score a single resume against JD keywords and return a ScoreResult entity (not yet saved).
        /// </summary>
        private ScoreResult ScoreResume(Resume resume, List<string> jdKeywords, string jdText, int jobId)
        {
            var resumeText = resume.ExtractedText ?? string.Empty;
            var resumeTokens = new HashSet<string>(
                TfIdfHelper.Tokenise(resumeText),
                StringComparer.OrdinalIgnoreCase);

            // ── 1) Keyword overlap score (0–60 base) ─────────────────────────
            var matchedKeywords = jdKeywords
                .Where(kw => resumeTokens.Contains(kw))
                .ToList();

            double keywordScore = jdKeywords.Count > 0
                ? (double)matchedKeywords.Count / jdKeywords.Count * 60.0
                : 0;

            // ── 2) Experience bonus (+0–10) ──────────────────────────────────
            double experienceBonus = DetectExperienceBonus(resumeText);

            // ── 3) Skills section match bonus (+0–15) ────────────────────────
            double skillsBonus = DetectSkillsBonus(resumeText, jdKeywords);

            // ── 4) Degree level bonus (+0–15) ────────────────────────────────
            double degreeBonus = DetectDegreeBonus(resumeText);

            double totalScore = Math.Min(100, Math.Round(keywordScore + experienceBonus + skillsBonus + degreeBonus, 1));

            var breakdown = new Dictionary<string, double>
            {
                ["KeywordMatch"] = Math.Round(keywordScore, 1),
                ["ExperienceBonus"] = experienceBonus,
                ["SkillsBonus"] = skillsBonus,
                ["DegreeBonus"] = degreeBonus
            };

            return new ScoreResult
            {
                ResumeId = resume.Id,
                JobId = jobId,
                Score = totalScore,
                MatchedKeywords = string.Join(", ", matchedKeywords),
                ScoreBreakdownJson = JsonSerializer.Serialize(breakdown),
                ScoredAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Detect years of experience mentions and award bonus points.
        /// Looks for patterns like "5 years", "5+ years", "5-7 years".
        /// </summary>
        private static double DetectExperienceBonus(string text)
        {
            // Match patterns like "3 years", "5+ years of experience", "3-5 years"
            var matches = Regex.Matches(text, @"(\d{1,2})\s*[+\-]?\s*(?:to\s+\d{1,2}\s+)?years?\b", RegexOptions.IgnoreCase);
            if (matches.Count == 0)
                return 0;

            // Find the maximum years mentioned
            int maxYears = 0;
            foreach (Match m in matches)
            {
                if (int.TryParse(m.Groups[1].Value, out int years))
                    maxYears = Math.Max(maxYears, years);
            }

            // Scale: 1-2 years = +3, 3-5 = +6, 6+ = +10
            if (maxYears >= 6) return 10;
            if (maxYears >= 3) return 6;
            if (maxYears >= 1) return 3;
            return 0;
        }

        /// <summary>
        /// Detect whether the resume has a dedicated "Skills" section and how many JD keywords appear in it.
        /// </summary>
        private static double DetectSkillsBonus(string text, List<string> jdKeywords)
        {
            // Try to find a skills section
            var skillsSectionMatch = Regex.Match(text,
                @"(?:skills|technical\s+skills|core\s+competencies|technologies|proficiencies)\s*[:—\-\n](.{50,1500}?)(?:\n\s*(?:experience|education|projects|work|employment|certifications|awards|references)\b|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!skillsSectionMatch.Success)
            {
                // Fallback: check if common tech skills from JD appear frequently in resume
                var techMatches = jdKeywords.Count(kw =>
                    Regex.IsMatch(text, @"\b" + Regex.Escape(kw) + @"\b", RegexOptions.IgnoreCase));
                double ratio = jdKeywords.Count > 0 ? (double)techMatches / jdKeywords.Count : 0;
                return Math.Min(8, Math.Round(ratio * 15, 1));
            }

            var skillsText = skillsSectionMatch.Groups[1].Value;
            var skillTokens = new HashSet<string>(
                TfIdfHelper.Tokenise(skillsText),
                StringComparer.OrdinalIgnoreCase);

            var skillMatchCount = jdKeywords.Count(kw => skillTokens.Contains(kw));
            double skillRatio = jdKeywords.Count > 0 ? (double)skillMatchCount / jdKeywords.Count : 0;

            return Math.Min(15, Math.Round(skillRatio * 15, 1));
        }

        /// <summary>
        /// Detect educational qualifications and award bonus points based on degree level.
        /// </summary>
        private static double DetectDegreeBonus(string text)
        {
            var lower = text.ToLowerInvariant();

            // PhD / Doctorate
            if (Regex.IsMatch(lower, @"\b(?:ph\.?d|doctorate|doctor\s+of\s+philosophy)\b"))
                return 15;

            // Master's / M.Tech / MCA / MBA / M.Sc / MS
            if (Regex.IsMatch(lower, @"\b(?:master'?s?|m\.?tech|m\.?s\.?c|m\.?c\.?a|m\.?b\.?a|m\.?s\.?|m\.?e\.?)\b"))
                return 12;

            // Bachelor's / B.Tech / BCA / B.Sc / BE / BBA
            if (Regex.IsMatch(lower, @"\b(?:bachelor'?s?|b\.?tech|b\.?s\.?c|b\.?c\.?a|b\.?b\.?a|b\.?e\.?|b\.?s\.?)\b"))
                return 8;

            // Diploma
            if (Regex.IsMatch(lower, @"\b(?:diploma|associate\s+degree)\b"))
                return 4;

            return 0;
        }
    }
}
