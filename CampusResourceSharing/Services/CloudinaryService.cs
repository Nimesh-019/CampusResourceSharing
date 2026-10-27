using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CampusResourceSharing.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary? _cloudinary;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CloudinaryService> _logger;

        public CloudinaryService(
            IOptions<CloudinarySettings> config,
            IWebHostEnvironment environment,
            ILogger<CloudinaryService> logger)
        {
            _environment = environment;
            _logger = logger;

            var cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL");
            var settings = config.Value;

            if (!string.IsNullOrWhiteSpace(cloudinaryUrl))
            {
                _cloudinary = new Cloudinary(cloudinaryUrl);
            }
            else if (!string.IsNullOrWhiteSpace(settings.CloudName) &&
                !string.IsNullOrWhiteSpace(settings.ApiKey) &&
                !string.IsNullOrWhiteSpace(settings.ApiSecret) &&
                settings.CloudName != "YOUR_CLOUD_NAME")
            {
                var account = new Account(
                    settings.CloudName,
                    settings.ApiKey,
                    settings.ApiSecret
                );
                _cloudinary = new Cloudinary(account);
            }
            else
            {
                _logger.LogWarning("Cloudinary is not fully configured in user-secrets, appsettings, or environment variables.");
            }
        }

        public async Task<(string? Url, string? PublicId)> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return (null, null);
            }

            if (_cloudinary == null)
            {
                _logger.LogError("Cloudinary client is not configured.");
                throw new InvalidOperationException("Cloudinary configuration is missing. Please set CloudName, ApiKey, and ApiSecret.");
            }

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "campus_items"
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary upload failed: {Error}", uploadResult.Error.Message);
                throw new InvalidOperationException($"Cloudinary upload error: {uploadResult.Error.Message}");
            }

            var url = uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
            var publicId = uploadResult.PublicId;

            return (url, publicId);
        }

        public async Task<bool> DeleteImageAsync(string? publicId, string? imagePath = null)
        {
            // 1. Try deleting via Cloudinary if publicId is provided or can be extracted
            var targetPublicId = publicId;
            if (string.IsNullOrWhiteSpace(targetPublicId) && !string.IsNullOrWhiteSpace(imagePath))
            {
                if (imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    targetPublicId = ExtractPublicIdFromUrl(imagePath);
                }
            }

            if (!string.IsNullOrWhiteSpace(targetPublicId) && _cloudinary != null)
            {
                try
                {
                    var deleteParams = new DeletionParams(targetPublicId);
                    var deleteResult = await _cloudinary.DestroyAsync(deleteParams);
                    _logger.LogInformation("Cloudinary image deletion result for {PublicId}: {Result}", targetPublicId, deleteResult.Result);
                    return deleteResult.Result == "ok";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting image from Cloudinary for PublicId: {PublicId}", targetPublicId);
                }
            }

            // 2. Backward compatibility: if imagePath is a local path, delete from wwwroot if exists
            if (!string.IsNullOrWhiteSpace(imagePath) &&
                !imagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !imagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var cleanPath = imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var fullLocalPath = Path.Combine(_environment.WebRootPath, cleanPath);
                    if (File.Exists(fullLocalPath))
                    {
                        File.Delete(fullLocalPath);
                        _logger.LogInformation("Deleted legacy local image file: {Path}", fullLocalPath);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting local file: {Path}", imagePath);
                }
            }

            return false;
        }

        private string? ExtractPublicIdFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;
                var uploadIndex = path.IndexOf("/upload/", StringComparison.OrdinalIgnoreCase);
                if (uploadIndex == -1) return null;

                var afterUpload = path.Substring(uploadIndex + "/upload/".Length);

                if (afterUpload.StartsWith("v") && afterUpload.Contains("/"))
                {
                    var firstSlash = afterUpload.IndexOf('/');
                    var versionPart = afterUpload.Substring(1, firstSlash - 1);
                    if (long.TryParse(versionPart, out _))
                    {
                        afterUpload = afterUpload.Substring(firstSlash + 1);
                    }
                }

                var lastDot = afterUpload.LastIndexOf('.');
                if (lastDot > 0)
                {
                    afterUpload = afterUpload.Substring(0, lastDot);
                }

                return string.IsNullOrWhiteSpace(afterUpload) ? null : afterUpload;
            }
            catch
            {
                return null;
            }
        }
    }
}
