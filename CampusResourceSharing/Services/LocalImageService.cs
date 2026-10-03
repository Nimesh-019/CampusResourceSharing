using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CampusResourceSharing.Services
{
    /// <summary>
    /// Local file system image service for local development environment.
    /// Saves uploaded images directly into wwwroot/uploads/items/.
    /// Implements ICloudinaryService so controllers and business logic remain completely decoupled and unchanged.
    /// </summary>
    public class LocalImageService : ICloudinaryService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<LocalImageService> _logger;

        public LocalImageService(
            IWebHostEnvironment environment,
            ILogger<LocalImageService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<(string? Url, string? PublicId)> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return (null, null);
            }

            try
            {
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "items");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _logger.LogInformation("Saved local image file: {FilePath}", filePath);
                return ($"/uploads/items/{fileName}", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading local image file {FileName}", file.FileName);
                throw;
            }
        }

        public Task<bool> DeleteImageAsync(string? publicId, string? imagePath = null)
        {
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                try
                {
                    var cleanPath = imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var fullLocalPath = Path.Combine(_environment.WebRootPath, cleanPath);
                    if (File.Exists(fullLocalPath))
                    {
                        File.Delete(fullLocalPath);
                        _logger.LogInformation("Deleted local image file: {Path}", fullLocalPath);
                        return Task.FromResult(true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting local image file {Path}", imagePath);
                }
            }

            return Task.FromResult(false);
        }
    }
}
