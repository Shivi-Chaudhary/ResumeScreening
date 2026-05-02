using System.ComponentModel.DataAnnotations;

namespace ResumeScreening.API.DTOs
{
    // ── Register ──────────────────────────────────────────────────────────────
    public class RegisterRequestDto
    {
        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;

        // Only "HRAdmin" or "Viewer" — validated in the controller
        public string Role { get; set; } = "Viewer";
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    public class LoginRequestDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    // ── Response ──────────────────────────────────────────────────────────────
    public class AuthResponseDto
    {
        public string Token     { get; set; } = string.Empty;
        public string FullName  { get; set; } = string.Empty;
        public string Email     { get; set; } = string.Empty;
        public string Role      { get; set; } = string.Empty;
        public DateTime Expiry  { get; set; }
    }
}
