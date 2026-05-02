using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace ResumeScreening.API.Services
{
    public class BlobService : IBlobService
    {
        private readonly string? _connectionString;
        private readonly string _containerName;
        private readonly ILogger<BlobService> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly bool _useLocalDisk;
        private readonly string _uploadsRoot;
        private readonly string _publicBaseUrl;
        private BlobContainerClient? _container;

        public BlobService(
            IConfiguration config,
            IWebHostEnvironment env,
            ILogger<BlobService> logger)
        {
            _env = env;
            _logger = logger;
            var section = config.GetSection("AzureBlobStorage");
            _connectionString = section["ConnectionString"];
            _containerName = section["ContainerName"] ?? "resume-files";

            var localSection = config.GetSection("LocalFileStorage");
            var cs = _connectionString?.Trim() ?? "";
            var azureMissingOrTemplate = string.IsNullOrWhiteSpace(cs) ||
                cs.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase);
            // Development: use disk when Azure is not configured, or when LocalFileStorage:Force is true
            // (invalid real-looking connection strings otherwise skip local fallback and fail Azure calls).
            var forceLocalDev = env.IsDevelopment() && localSection.GetValue("Force", false);

            _useLocalDisk = forceLocalDev || (azureMissingOrTemplate && env.IsDevelopment());
            _uploadsRoot = Path.Combine(
                env.ContentRootPath,
                localSection["RootFolder"] ?? "App_Data",
                "uploads");
            _publicBaseUrl = (localSection["PublicBaseUrl"] ?? "http://localhost:5109").TrimEnd('/');

            if (_useLocalDisk)
            {
                Directory.CreateDirectory(_uploadsRoot);
                _logger.LogWarning(
                    "Azure Blob is not configured; using local disk under {Root}. Files are served at {Base}/uploads/",
                    _uploadsRoot,
                    _publicBaseUrl);
            }
        }

        private BlobContainerClient GetContainer()
        {
            if (_useLocalDisk)
                throw new InvalidOperationException("Internal: local disk mode does not use Azure container.");

            if (string.IsNullOrWhiteSpace(_connectionString) ||
                _connectionString.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Configure AzureBlobStorage:ConnectionString in appsettings.json, or run in Development without Azure to use local disk (see LocalFileStorage:PublicBaseUrl).");
            }

            return _container ??= new BlobContainerClient(_connectionString, _containerName);
        }

        public async Task<string> UploadAsync(Stream stream, string blobPath, string? contentType = null, CancellationToken cancellationToken = default)
        {
            if (_useLocalDisk)
            {
                blobPath = blobPath.Replace('\\', '/');
                var fullPath = Path.GetFullPath(Path.Combine(_uploadsRoot, blobPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!fullPath.StartsWith(Path.GetFullPath(_uploadsRoot), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Invalid blob path.");

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                stream.Position = 0;
                await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    await stream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);

                var url = $"{_publicBaseUrl}/uploads/{blobPath}";
                _logger.LogInformation("Saved file locally {Path}", fullPath);
                return url;
            }

            var container = GetContainer();
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var blob = container.GetBlobClient(blobPath);
            stream.Position = 0;
            await blob.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = contentType ?? "application/octet-stream"
            }, cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Uploaded blob {BlobPath}", blobPath);
            return blob.Uri.ToString();
        }

        public async Task DeleteByUrlAsync(string? blobUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blobUrl))
                return;

            if (_useLocalDisk)
            {
                TryDeleteLocal(blobUrl);
                return;
            }

            if (!TryParseBlobPath(blobUrl, out var blobPath))
            {
                _logger.LogWarning("Could not parse blob URL for delete: {Url}", blobUrl);
                return;
            }

            try
            {
                var container = GetContainer();
                var blob = container.GetBlobClient(blobPath);
                var response = await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                if (response.Value)
                    _logger.LogInformation("Deleted blob {BlobPath}", blobPath);
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("Skipped blob delete (storage not configured): {Url}", blobUrl);
            }
        }

        private void TryDeleteLocal(string blobUrl)
        {
            var prefix = _publicBaseUrl + "/uploads/";
            if (!blobUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Local delete skipped (URL not under uploads): {Url}", blobUrl);
                return;
            }

            var rel = blobUrl[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(_uploadsRoot, rel));
            if (!fullPath.StartsWith(Path.GetFullPath(_uploadsRoot), StringComparison.OrdinalIgnoreCase))
                return;
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted local file {Path}", fullPath);
            }
        }

        private bool TryParseBlobPath(string blobUrl, out string blobPath)
        {
            blobPath = string.Empty;
            if (!Uri.TryCreate(blobUrl, UriKind.Absolute, out var uri))
                return false;

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
                return false;

            blobPath = string.Join("/", segments.Skip(1));
            return !string.IsNullOrEmpty(blobPath);
        }
    }
}
