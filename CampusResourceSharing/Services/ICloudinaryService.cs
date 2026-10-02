using Microsoft.AspNetCore.Http;

namespace CampusResourceSharing.Services
{
    public interface ICloudinaryService
    {
        Task<(string? Url, string? PublicId)> UploadImageAsync(IFormFile file);
        Task<bool> DeleteImageAsync(string? publicId, string? imagePath = null);
    }
}
