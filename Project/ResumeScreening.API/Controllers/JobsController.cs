using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ResumeScreening.API.Data;
using ResumeScreening.API.DTOs;
using ResumeScreening.API.Helpers;
using ResumeScreening.API.Models;
using ResumeScreening.API.Services;

namespace ResumeScreening.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class JobsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IBlobService _blobs;
        private readonly ILogger<JobsController> _logger;

        private static readonly HashSet<string> AllowedJdExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".txt"
        };
        private static readonly HashSet<string> AllowedResumeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        public JobsController(AppDbContext db, IBlobService blobs, ILogger<JobsController> logger)
        {
            _db = db;
            _blobs = blobs;
            _logger = logger;
        }

        // GET api/jobs?status=Active|Closed|All
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<JobListItemDto>>> GetJobs([FromQuery] string status = "All")
        {
            var q = _db.Jobs.AsNoTracking().Include(j => j.CreatedBy).AsQueryable();
            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                q = q.Where(j => j.Status == "Active");
            else if (status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                q = q.Where(j => j.Status == "Closed");

            var list = await q
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new JobListItemDto
                {
                    Id = j.Id,
                    Title = j.Title,
                    Status = j.Status,
                    CreatedAt = j.CreatedAt,
                    CreatedByUserId = j.CreatedByUserId,
                    CreatedByFullName = j.CreatedBy.FullName,
                    HasJdFile = j.JdFileUrl != null
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET api/jobs/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<JobResponseDto>> GetJob(int id)
        {
            var job = await _db.Jobs
                .AsNoTracking()
                .Include(j => j.CreatedBy)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return NotFound(new { message = "Job not found!!" });

            return Ok(MapToResponse(job));
        }

        // POST api/jobs  (JSON — avoids multipart + JWT header issues from SPA; use POST .../jd for file)
        [HttpPost]
        [Authorize(Roles = "HRAdmin")]
        [Consumes("application/json")]
        public async Task<ActionResult<JobResponseDto>> CreateJob(
            [FromBody] CreateJobDto dto,
            CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            if (userId is null)
                return Unauthorized(new { message = "Invalid token: missing user id claim. Sign out and sign in again." });

            var job = new Job
            {
                Title = dto.Title.Trim(),
                Description = dto.Description.Trim(),
                CreatedByUserId = userId.Value
            };

            _db.Jobs.Add(job);
            await _db.SaveChangesAsync(cancellationToken);

            await _db.Entry(job).Reference(j => j.CreatedBy).LoadAsync(cancellationToken);
            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, MapToResponse(job));
        }

        // PUT api/jobs/5
        [HttpPut("{id:int}")]
        [Authorize(Roles = "HRAdmin")]
        public async Task<ActionResult<JobResponseDto>> UpdateJob(int id, [FromBody] UpdateJobDto dto, CancellationToken cancellationToken)
        {
            var job = await _db.Jobs.Include(j => j.CreatedBy).FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
            if (job == null)
                return NotFound(new { message = "Job not found." });

            if (dto.Title != null)
                job.Title = dto.Title.Trim();
            if (dto.Description != null)
                job.Description = dto.Description.Trim();
            if (dto.Status != null)
                job.Status = dto.Status;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(MapToResponse(job));
        }

        // POST api/jobs/5/jd  — upload or replace job description file
        [HttpPost("{id:int}/jd")]
        [Authorize(Roles = "HRAdmin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<ActionResult<JobResponseDto>> UploadJobDescription(
            int id,
            CancellationToken cancellationToken)
        {
            var job = await _db.Jobs.Include(j => j.CreatedBy).FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
            if (job == null)
                return NotFound(new { message = "Job not found." });

            var form = await Request.ReadFormAsync(cancellationToken);
            var file = form.Files["file"];
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "A file is required (form field name: file)." });

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedJdExtensions.Contains(ext))
                return BadRequest(new { message = "File must be .pdf, .doc, .docx, or .txt." });

            await _blobs.DeleteByUrlAsync(job.JdFileUrl, cancellationToken);

            await using var readStream = file.OpenReadStream();
            await using var ms = new MemoryStream();
            await readStream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            var safeName = SanitizeFileName(file.FileName);
            var blobPath = $"job-descriptions/{Guid.NewGuid():N}_{safeName}";
            try
            {
                job.JdFileUrl = await _blobs.UploadAsync(ms, blobPath, file.ContentType, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                ms.Position = 0;
                job.JdExtractedText = PdfTextExtractor.TryExtractText(ms);
            }
            else
                job.JdExtractedText = null;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(MapToResponse(job));
        }

        // DELETE api/jobs/5
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "HRAdmin")]
        public async Task<IActionResult> DeleteJob(int id, CancellationToken cancellationToken)
        {
            var job = await _db.Jobs.Include(j => j.Resumes).FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
            if (job == null)
                return NotFound(new { message = "Job not found." });

            if (job.Resumes.Count > 0)
                return Conflict(new { message = "Cannot delete a job that has resume submissions. Close it instead (set status to Closed)." });

            await _blobs.DeleteByUrlAsync(job.JdFileUrl, cancellationToken);
            _db.Jobs.Remove(job);
            await _db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        // POST api/jobs/{id}/resumes  — bulk upload up to 20 PDF resumes
        [HttpPost("{id:int}/resumes")]
        [Authorize(Roles = "HRAdmin")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<ActionResult<BulkResumeUploadResponseDto>> UploadResumes(
            int id,
            CancellationToken cancellationToken)
        {
            var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
            if (job == null)
                return NotFound(new { message = "Job not found." });

            var form = await Request.ReadFormAsync(cancellationToken);
            var files = form.Files.GetFiles("files");
            if (files.Count == 0)
                return BadRequest(new { message = "At least one PDF file is required (form field name: files)." });
            if (files.Count > 20)
                return BadRequest(new { message = "You can upload at most 20 files per request." });

            var response = new BulkResumeUploadResponseDto
            {
                JobId = id,
                TotalFiles = files.Count
            };

            foreach (var file in files)
            {
                var result = new ResumeUploadResultDto { FileName = file.FileName };
                try
                {
                    if (file.Length <= 0)
                        throw new InvalidOperationException("File is empty.");

                    var ext = Path.GetExtension(file.FileName);
                    if (!AllowedResumeExtensions.Contains(ext))
                        throw new InvalidOperationException("Only .pdf resumes are supported.");

                    await using var readStream = file.OpenReadStream();
                    await using var ms = new MemoryStream();
                    await readStream.CopyToAsync(ms, cancellationToken);
                    ms.Position = 0;

                    var safeName = SanitizeFileName(file.FileName);
                    var blobPath = $"resumes/{id}/{Guid.NewGuid():N}_{safeName}";
                    var fileUrl = await _blobs.UploadAsync(ms, blobPath, file.ContentType, cancellationToken);

                    ms.Position = 0;
                    var extracted = PdfTextExtractor.TryExtractText(ms);
                    var candidateName = GuessCandidateName(file.FileName, extracted);
                    var candidateEmail = GuessCandidateEmail(extracted);

                    var resume = new Resume
                    {
                        JobId = id,
                        UploadedByUserId = null,
                        CandidateName = candidateName,
                        CandidateEmail = candidateEmail,
                        FileUrl = fileUrl,
                        ExtractedText = extracted,
                        Status = string.IsNullOrWhiteSpace(extracted) ? "Pending" : "Extracted",
                    };

                    _db.Resumes.Add(resume);
                    await _db.SaveChangesAsync(cancellationToken);

                    result.ResumeId = resume.Id;
                    result.CandidateName = resume.CandidateName;
                    result.CandidateEmail = resume.CandidateEmail;
                    result.Status = resume.Status;
                    result.Message = "Uploaded";
                    response.UploadedCount++;
                }
                catch (Exception ex)
                {
                    result.Status = "Failed";
                    result.Message = ex.Message;
                    response.FailedCount++;
                }

                response.Results.Add(result);
            }

            return Ok(response);
        }

        // GET api/jobs/{id}/resumes — HRAdmin: all resumes; Viewer: only their own submission for this job
        [HttpGet("{id:int}/resumes")]
        public async Task<ActionResult<IReadOnlyList<ResumeListItemDto>>> GetResumesByJob(int id, CancellationToken cancellationToken)
        {
            var exists = await _db.Jobs.AnyAsync(j => j.Id == id, cancellationToken);
            if (!exists)
                return NotFound(new { message = "Job not found." });

            var q = _db.Resumes.AsNoTracking().Where(r => r.JobId == id);

            if (!User.IsInRole("HRAdmin"))
            {
                var uid = User.GetUserId();
                if (uid is null)
                    return Unauthorized(new { message = "Invalid token: missing user id claim." });
                q = q.Where(r => r.UploadedByUserId == uid);
            }

            var list = await q
                .OrderByDescending(r => r.UploadedAt)
                .Select(r => new ResumeListItemDto
                {
                    Id = r.Id,
                    CandidateName = r.CandidateName,
                    CandidateEmail = r.CandidateEmail,
                    FileUrl = r.FileUrl,
                    Status = r.Status,
                    UploadedAt = r.UploadedAt,
                    HasExtractedText = r.ExtractedText != null && r.ExtractedText != ""
                })
                .ToListAsync(cancellationToken);

            return Ok(list);
        }

        // DELETE api/jobs/{jobId}/resumes/{resumeId} — HRAdmin: any resume on the job; Viewer: only own (UploadedByUserId)
        [HttpDelete("{jobId:int}/resumes/{resumeId:int}")]
        public async Task<IActionResult> DeleteResume(int jobId, int resumeId, CancellationToken cancellationToken)
        {
            var jobExists = await _db.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken);
            if (!jobExists)
                return NotFound(new { message = "Job not found." });

            var resume = await _db.Resumes.FirstOrDefaultAsync(
                r => r.Id == resumeId && r.JobId == jobId,
                cancellationToken);

            if (resume == null)
                return NotFound(new { message = "Resume not found." });

            if (User.IsInRole("HRAdmin"))
            {
                // allow
            }
            else
            {
                var uid = User.GetUserId();
                if (uid is null)
                    return Unauthorized(new { message = "Invalid token: missing user id claim." });
                if (resume.UploadedByUserId != uid)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only delete your own resume." });
            }

            await _blobs.DeleteByUrlAsync(resume.FileUrl, cancellationToken);
            _db.Resumes.Remove(resume);
            await _db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        // POST api/jobs/{id}/my-resume — Viewer submits/replaces their PDF for this job (one per user per job)
        [HttpPost("{id:int}/my-resume")]
        [Authorize(Roles = "Viewer")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<ActionResult<ResumeListItemDto>> UploadMyResume(
            int id,
            CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            if (userId is null)
                return Unauthorized(new { message = "Invalid token: missing user id claim." });

            var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
            if (job == null)
                return NotFound(new { message = "Job not found." });

            var form = await Request.ReadFormAsync(cancellationToken);
            var file = form.Files["file"];
            if (file == null || file.Length <= 0)
                return BadRequest(new { message = "A PDF file is required (form field name: file)." });

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedResumeExtensions.Contains(ext))
                return BadRequest(new { message = "Only .pdf resumes are supported." });

            await using var readStream = file.OpenReadStream();
            await using var ms = new MemoryStream();
            await readStream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            var safeName = SanitizeFileName(file.FileName);
            var blobPath = $"resumes/{id}/viewer-{userId}/{Guid.NewGuid():N}_{safeName}";
            string fileUrl;
            try
            {
                fileUrl = await _blobs.UploadAsync(ms, blobPath, file.ContentType, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            ms.Position = 0;
            var extracted = PdfTextExtractor.TryExtractText(ms);
            var candidateName = GuessCandidateName(file.FileName, extracted);
            var candidateEmail = GuessCandidateEmail(extracted);

            var existing = await _db.Resumes.FirstOrDefaultAsync(
                r => r.JobId == id && r.UploadedByUserId == userId,
                cancellationToken);

            if (existing != null)
            {
                await _blobs.DeleteByUrlAsync(existing.FileUrl, cancellationToken);
                existing.FileUrl = fileUrl;
                existing.CandidateName = candidateName;
                existing.CandidateEmail = candidateEmail;
                existing.ExtractedText = extracted;
                existing.Status = string.IsNullOrWhiteSpace(extracted) ? "Pending" : "Extracted";
                existing.UploadedAt = DateTime.UtcNow;
            }
            else
            {
                _db.Resumes.Add(new Resume
                {
                    JobId = id,
                    UploadedByUserId = userId,
                    CandidateName = candidateName,
                    CandidateEmail = candidateEmail,
                    FileUrl = fileUrl,
                    ExtractedText = extracted,
                    Status = string.IsNullOrWhiteSpace(extracted) ? "Pending" : "Extracted",
                });
            }

            await _db.SaveChangesAsync(cancellationToken);

            var saved = await _db.Resumes.AsNoTracking()
                .Where(r => r.JobId == id && r.UploadedByUserId == userId)
                .OrderByDescending(r => r.UploadedAt)
                .Select(r => new ResumeListItemDto
                {
                    Id = r.Id,
                    CandidateName = r.CandidateName,
                    CandidateEmail = r.CandidateEmail,
                    FileUrl = r.FileUrl,
                    Status = r.Status,
                    UploadedAt = r.UploadedAt,
                    HasExtractedText = r.ExtractedText != null && r.ExtractedText != ""
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (saved == null)
            {
                _logger.LogError("Resume save succeeded but row not found for job {JobId} user {UserId}", id, userId);
                return Problem(
                    detail: "Resume was saved but could not be reloaded.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            return Ok(saved);
        }

        private static JobResponseDto MapToResponse(Job job) => new()
        {
            Id = job.Id,
            Title = job.Title,
            Description = job.Description,
            JdFileUrl = job.JdFileUrl,
            JdExtractedText = job.JdExtractedText,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            CreatedByUserId = job.CreatedByUserId,
            CreatedByFullName = job.CreatedBy?.FullName ?? string.Empty
        };

        private static string SanitizeFileName(string fileName)
        {
            var name = Path.GetFileName(fileName);
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrEmpty(name) ? "file" : name;
        }

        private static string GuessCandidateName(string fileName, string? extractedText)
        {
            var fromFile = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Replace('-', ' ').Trim();
            if (!string.IsNullOrWhiteSpace(fromFile))
                return fromFile.Length <= 150 ? fromFile : fromFile[..150];

            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                var line = extractedText.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var candidate = line.Trim();
                    return candidate.Length <= 150 ? candidate : candidate[..150];
                }
            }

            return "Unknown Candidate";
        }

        private static string? GuessCandidateEmail(string? extractedText)
        {
            if (string.IsNullOrWhiteSpace(extractedText))
                return null;

            var match = Regex.Match(extractedText, @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}");
            return match.Success ? match.Value : null;
        }
    }
}
