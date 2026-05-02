using System.ComponentModel.DataAnnotations;

namespace ResumeScreening.API.DTOs
{
    public class CreateJobDto
    {
        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;
    }

    public class JobResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? JdFileUrl { get; set; }
        public string? JdExtractedText { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByFullName { get; set; } = string.Empty;
    }

    public class JobListItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByFullName { get; set; } = string.Empty;
        public bool HasJdFile { get; set; }
    }

    public class UpdateJobDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        public string? Description { get; set; }

        /// <summary>Active or Closed</summary>
        [RegularExpression("^(Active|Closed)$")]
        public string? Status { get; set; }
    }
}
