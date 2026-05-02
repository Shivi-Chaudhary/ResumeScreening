namespace ResumeScreening.API.Services
{
    public interface IBlobService
    {
        /// <summary>Uploads a stream to the configured container. Returns the blob's public URI.</summary>
        Task<string> UploadAsync(Stream stream, string blobPath, string? contentType = null, CancellationToken cancellationToken = default);

        /// <summary>Deletes a blob given its full URL (as returned from UploadAsync).</summary>
        Task DeleteByUrlAsync(string? blobUrl, CancellationToken cancellationToken = default);
    }
}
