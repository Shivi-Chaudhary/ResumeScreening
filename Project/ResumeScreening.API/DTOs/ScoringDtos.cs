namespace ResumeScreening.API.DTOs
{
    /// <summary>Response after triggering screening for a job.</summary>
    public class ScreeningResponseDto
    {
        public int JobId { get; set; }
        public int ResumesScored { get; set; }
        public string Method { get; set; } = "TF-IDF";
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Ranked candidate row returned by the rankings endpoint.</summary>
    public class RankedCandidateDto
    {
        public int Rank { get; set; }
        public int ResumeId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidateEmail { get; set; }
        public double Score { get; set; }
        public string ScoreCategory { get; set; } = string.Empty;  // "green", "amber", "red"
        public string? MatchedKeywords { get; set; }
        public string? FileUrl { get; set; }
        public DateTime ScoredAt { get; set; }
        public string? HRStatus { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Request body for updating HR application status on a resume.</summary>
    public class UpdateApplicationStatusDto
    {
        public string HRStatus { get; set; } = "Pending";  // Pending | Shortlisted | UnderReview | Rejected
        public string? Notes { get; set; }
    }

    /// <summary>Detailed resume view with full score breakdown.</summary>
    public class ResumeDetailDto
    {
        public int Id { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidateEmail { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? ExtractedText { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }

        // Score info (null if not yet scored)
        public double? Score { get; set; }
        public string? ScoreCategory { get; set; }
        public string? MatchedKeywords { get; set; }
        public string? ScoreBreakdownJson { get; set; }
        public DateTime? ScoredAt { get; set; }

        // Application status
        public string? HRStatus { get; set; }
        public string? Notes { get; set; }
    }
}
