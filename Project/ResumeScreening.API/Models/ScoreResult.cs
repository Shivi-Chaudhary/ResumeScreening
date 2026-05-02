using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumeScreening.API.Models
{
    public class ScoreResult
    {
        public int Id { get; set; }

        // 0 to 100
        public double Score { get; set; }

        // Comma-separated list of matched keywords
        public string? MatchedKeywords { get; set; }

        // Breakdown stored as JSON string: { "KeywordMatch": 60, "ExperienceBonus": 10, "SkillsBonus": 15 }
        public string? ScoreBreakdownJson { get; set; }

        public DateTime ScoredAt { get; set; } = DateTime.UtcNow;

        // Foreign keys
        public int ResumeId { get; set; }

        [ForeignKey("ResumeId")]
        public Resume Resume { get; set; } = null!;

        public int JobId { get; set; }

        [ForeignKey("JobId")]
        public Job Job { get; set; } = null!;
    }
}
