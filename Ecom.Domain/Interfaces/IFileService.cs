using Microsoft.AspNetCore.Http;

namespace Ecom.Domain.Interfaces
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IFormFile file, string directory);
        Task<bool> DeleteFileAsync(string fileUrl);
    }
}
