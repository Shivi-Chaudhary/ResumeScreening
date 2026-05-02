using System.ComponentModel.DataAnnotations;

namespace ResumeScreening.API.DTOs
{
    public class ResumeUploadResultDto
    {
        public string FileName { get; set; } = string.Empty;
        public int ResumeId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidateEmail { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    public class BulkResumeUploadResponseDto
    {
        public int JobId { get; set; }
        public int TotalFiles { get; set; }
        public int UploadedCount { get; set; }
        public int FailedCount { get; set; }
        public List<ResumeUploadResultDto> Results { get; set; } = new();
    }

    public class ResumeListItemDto
    {
        public int Id { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string? CandidateEmail { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public bool HasExtractedText { get; set; }
    }
}
