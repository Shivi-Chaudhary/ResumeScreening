using System.ComponentModel.DataAnnotations;

namespace ResumeScreening.API.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Viewer"; // "HRAdmin" or "Viewer"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Job> Jobs { get; set; } = new List<Job>();

        public ICollection<Resume> UploadedResumes { get; set; } = new List<Resume>();
    }
}
