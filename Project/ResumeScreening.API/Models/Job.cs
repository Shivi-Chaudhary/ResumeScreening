using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumeScreening.API.Models
{
    public class Job
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        // URL of the JD file stored in Azure Blob Storage
        public string? JdFileUrl { get; set; }

        // Extracted plain text from the JD (used by scoring engine)
        public string? JdExtractedText { get; set; }

        public string Status { get; set; } = "Active"; // "Active" or "Closed"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Foreign key
        public int CreatedByUserId { get; set; }

        [ForeignKey("CreatedByUserId")]
        public User CreatedBy { get; set; } = null!;

        // Navigation
        public ICollection<Resume> Resumes { get; set; } = new List<Resume>();
    }
}
