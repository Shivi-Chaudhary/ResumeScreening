using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResumeScreening.API.Data;
using ResumeScreening.API.Models;

namespace ResumeScreening.API.Services
{
    public class AiScoringService
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _http;
        private readonly ILogger<AiScoringService> _logger;
        private readonly string _apiKey;
        private readonly string _model;

        public AiScoringService(
            AppDbContext db,
            HttpClient http,
            IConfiguration config,
            ILogger<AiScoringService> logger)
        {
            _db = db;
            _http = http;
            _logger = logger;
            _apiKey = config["GeminiAI:ApiKey"] ?? "";
            _model = config["GeminiAI:Model"] ?? "gemini-2.0-flash";
        }

        public async Task<int> ScoreAllResumesForJobAsync(int jobId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.StartsWith("REPLACE"))
                throw new InvalidOperationException("Gemini API key is not configured. Set GeminiAI:ApiKey in appsettings.json.");

            var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
            if (job == null)
                throw new InvalidOperationException("Job not found.");

            var jdText = job.JdExtractedText ?? job.Description;
            if (string.IsNullOrWhiteSpace(jdText))
                throw new InvalidOperationException("Job has no description or extracted JD text to screen against.");

            var resumes = await _db.Resumes
                .Where(r => r.JobId == jobId && r.ExtractedText != null && r.ExtractedText != "")
                .ToListAsync(ct);

            if (resumes.Count == 0)
                throw new InvalidOperationException("No resumes with extracted text found for this job.");

            var existingScores = await _db.ScoreResults
                .Where(s => s.JobId == jobId)
                .ToListAsync(ct);
            if (existingScores.Count > 0)
            {
                _db.ScoreResults.RemoveRange(existingScores);
                await _db.SaveChangesAsync(ct);
            }

            int scored = 0;
            string? lastError = null;

            for (int i = 0; i < resumes.Count; i++)
            {
                var resume = resumes[i];
                try
                {
                    var result = await ScoreResumeWithGemini(resume, jdText, jobId, ct);
                    _db.ScoreResults.Add(result);
                    resume.Status = "Scored";
                    scored++;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    _logger.LogWarning(ex, "Gemini scoring failed for resume {ResumeId}: {Error}", resume.Id, ex.Message);
                }

                // Throttle: wait 7 seconds between requests to respect Gemini free tier (10 RPM)
                if (i < resumes.Count - 1)
                    await Task.Delay(7000, ct);
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Job {JobId}: AI scored {Count}/{Total} resumes using Gemini.", jobId, scored, resumes.Count);

            if (scored == 0 && lastError != null)
                throw new InvalidOperationException($"Gemini AI failed to score any resumes. Last error: {lastError}");

            return scored;
        }

        private async Task<ScoreResult> ScoreResumeWithGemini(Resume resume, string jdText, int jobId, CancellationToken ct)
        {
            var resumeText = resume.ExtractedText ?? "";
            if (resumeText.Length > 6000)
                resumeText = resumeText[..6000];
            if (jdText.Length > 3000)
                jdText = jdText[..3000];

            var prompt = BuildPrompt(jdText, resumeText);

            var requestBody = new Dictionary<string, object>
            {
                ["contents"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["parts"] = new[] { new { text = prompt } }
                    }
                },
                ["generationConfig"] = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 1024,
                    responseMimeType = "application/json"
                }
            };

            // Disable thinking for gemini-2.5 models to get clean JSON responses
            if (_model.Contains("2.5"))
            {
                requestBody["generationConfig"] = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 1024,
                    responseMimeType = "application/json",
                    thinkingConfig = new { thinkingBudget = 0 }
                };
            }

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, content, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API error {Status}: {Body}", response.StatusCode, responseText);

                // Retry once after 10 seconds for rate limit errors
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogInformation("Rate limited — waiting 10 seconds before retry…");
                    await Task.Delay(10_000, ct);
                    response = await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct);
                    responseText = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("Gemini retry also failed {Status}: {Body}", response.StatusCode, responseText);
                        throw new InvalidOperationException($"Gemini API rate limit. Wait a minute and try again. Details: {responseText[..Math.Min(200, responseText.Length)]}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Gemini API returned {response.StatusCode}. Details: {responseText[..Math.Min(200, responseText.Length)]}");
                }
            }

            _logger.LogDebug("Gemini raw response (first 500 chars): {Response}", responseText[..Math.Min(500, responseText.Length)]);
            var parsed = ParseGeminiResponse(responseText);

            return new ScoreResult
            {
                ResumeId = resume.Id,
                JobId = jobId,
                Score = Math.Clamp(parsed.Score, 0, 100),
                MatchedKeywords = parsed.MatchedKeywords,
                ScoreBreakdownJson = parsed.BreakdownJson,
                ScoredAt = DateTime.UtcNow
            };
        }

        private static string BuildPrompt(string jdText, string resumeText)
        {
            return $@"You are an expert HR recruiter AI. Score the following resume against the job description.

JOB DESCRIPTION:
{jdText}

RESUME:
{resumeText}

Evaluate the resume and return a JSON object with exactly this structure:
{{
  ""score"": <number 0-100>,
  ""KeywordMatch"": <number 0-60 based on how many job-relevant skills/keywords appear in the resume>,
  ""ExperienceBonus"": <number 0-10 based on relevant work experience>,
  ""SkillsBonus"": <number 0-15 based on technical/professional skill alignment>,
  ""DegreeBonus"": <number 0-15 based on educational qualification relevance>,
  ""matchedKeywords"": ""<comma-separated list of matched skills/keywords>"",
  ""reasoning"": ""<one sentence explaining the score>""
}}

Rules:
- score must equal KeywordMatch + ExperienceBonus + SkillsBonus + DegreeBonus (capped at 100)
- Be strict but fair. A perfect match is rare.
- Consider context: ""React Native"" matches ""mobile development"", ""AWS"" matches ""cloud computing""
- Only return the JSON object, nothing else.";
        }

        private GeminiScoreResult ParseGeminiResponse(string responseText)
        {
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            // Gemini 2.5 models include "thinking" parts — find the last part with text that looks like JSON
            var parts = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts");

            string textContent = "{}";
            for (int i = parts.GetArrayLength() - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (part.TryGetProperty("text", out var textProp))
                {
                    var candidate = textProp.GetString()?.Trim() ?? "";
                    // Skip empty or thinking-only content — look for JSON
                    if (candidate.Contains('{'))
                    {
                        textContent = candidate;
                        break;
                    }
                }
            }

            // Clean markdown fences if present
            textContent = textContent.Trim();
            if (textContent.StartsWith("```"))
            {
                var firstNewline = textContent.IndexOf('\n');
                if (firstNewline > 0)
                    textContent = textContent[(firstNewline + 1)..];
                if (textContent.EndsWith("```"))
                    textContent = textContent[..^3];
                textContent = textContent.Trim();
            }

            _logger.LogDebug("Gemini parsed text: {Text}", textContent[..Math.Min(300, textContent.Length)]);

            using var scoreDoc = JsonDocument.Parse(textContent);
            var s = scoreDoc.RootElement;

            double score = s.TryGetProperty("score", out var sp) ? sp.GetDouble() : 0;
            double kwMatch = s.TryGetProperty("KeywordMatch", out var kp) ? kp.GetDouble() : 0;
            double expBonus = s.TryGetProperty("ExperienceBonus", out var ep) ? ep.GetDouble() : 0;
            double skillsBonus = s.TryGetProperty("SkillsBonus", out var skp) ? skp.GetDouble() : 0;
            double degreeBonus = s.TryGetProperty("DegreeBonus", out var dp) ? dp.GetDouble() : 0;
            string keywords = s.TryGetProperty("matchedKeywords", out var mkp) ? mkp.GetString() ?? "" : "";

            var breakdown = new Dictionary<string, double>
            {
                ["KeywordMatch"] = Math.Round(kwMatch, 1),
                ["ExperienceBonus"] = Math.Round(expBonus, 1),
                ["SkillsBonus"] = Math.Round(skillsBonus, 1),
                ["DegreeBonus"] = Math.Round(degreeBonus, 1)
            };

            return new GeminiScoreResult
            {
                Score = Math.Round(score, 1),
                MatchedKeywords = keywords,
                BreakdownJson = JsonSerializer.Serialize(breakdown)
            };
        }

        private class GeminiScoreResult
        {
            public double Score { get; set; }
            public string MatchedKeywords { get; set; } = "";
            public string BreakdownJson { get; set; } = "{}";
        }
    }
}
