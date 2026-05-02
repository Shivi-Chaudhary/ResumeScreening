using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResumeScreening.API.Data;
using ResumeScreening.API.DTOs;
using ResumeScreening.API.Helpers;
using ResumeScreening.API.Models;

namespace ResumeScreening.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly JwtHelper    _jwt;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext db, IConfiguration config)
        {
            _db     = db;
            _jwt    = new JwtHelper(config);
            _config = config;
        }

        // POST api/auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
        {
            // Validate role
            var allowedRoles = new[] { "HRAdmin", "Viewer" };
            if (!allowedRoles.Contains(dto.Role))
                return BadRequest(new { message = "Invalid role. Use 'HRAdmin' or 'Viewer'." });

            // Check duplicate email
            if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
                return Conflict(new { message = "A user with this email already exists." });

            var user = new User
            {
                FullName     = dto.FullName,
                Email        = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role         = dto.Role
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Reload so Id (identity) is guaranteed for JWT claims — avoids sub=0 / FK failures on upload.
            var persisted = await _db.Users.AsNoTracking()
                .FirstAsync(u => u.Email == dto.Email);

            var token = _jwt.GenerateToken(persisted);

            return Ok(new AuthResponseDto
            {
                Token    = token,
                FullName = persisted.FullName,
                Email    = persisted.Email,
                Role     = persisted.Role,
                Expiry   = DateTime.UtcNow.AddHours(8)
            });
        }

        // GET api/auth/me  (verify JWT + claims after login)
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new
            {
                userId = User.GetUserId(),
                email  = User.FindFirstValue(ClaimTypes.Email),
                role   = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role"),
                name   = User.Identity?.Name,
            });
        }

        // POST api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            var user = await _db.Users
                                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid email or password." });

            var token = _jwt.GenerateToken(user);

            return Ok(new AuthResponseDto
            {
                Token    = token,
                FullName = user.FullName,
                Email    = user.Email,
                Role     = user.Role,
                Expiry   = DateTime.UtcNow.AddHours(8)
            });
        }
    }
}
