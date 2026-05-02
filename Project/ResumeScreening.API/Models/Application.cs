using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumeScreening.API.Models
{
    public class Application
    {
        public int Id { get; set; }

        // HR decision: "Pending", "Shortlisted", "UnderReview", "Rejected"
        public string HRStatus { get; set; } = "Pending";

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Foreign keys
        public int ResumeId { get; set; }

        [ForeignKey("ResumeId")]
        public Resume Resume { get; set; } = null!;

        public int JobId { get; set; }

        [ForeignKey("JobId")]
        public Job Job { get; set; } = null!;
    }
}
