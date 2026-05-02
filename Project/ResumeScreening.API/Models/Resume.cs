using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumeScreening.API.Models
{
    public class Resume
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string CandidateName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? CandidateEmail { get; set; }

        // URL of the PDF stored in Azure Blob Storage
        [Required]
        public string FileUrl { get; set; } = string.Empty;

        // Plain text extracted from the PDF by PdfPig
        public string? ExtractedText { get; set; }

        // "Pending" → "Extracted" → "Scored"
        public string Status { get; set; } = "Pending";

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When set, this resume was submitted by that user (Viewer). HR bulk uploads leave this null.</summary>
        public int? UploadedByUserId { get; set; }

        [ForeignKey("UploadedByUserId")]
        public User? UploadedBy { get; set; }

        // Foreign key
        public int JobId { get; set; }

        [ForeignKey("JobId")]
        public Job Job { get; set; } = null!;

        // Navigation
        public ScoreResult? ScoreResult { get; set; }
        public Application? Application { get; set; }
    }
}
