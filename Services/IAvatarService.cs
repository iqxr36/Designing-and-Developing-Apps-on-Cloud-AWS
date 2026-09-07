using CloudMVCApplication.Models;
using Microsoft.AspNetCore.Http;

namespace CloudMVCApplication.Services;

public interface IAvatarService
{
    Task<(bool Succeeded, string? ErrorMessage)> UpdateAsync(
        ApplicationUser user,
        IFormFile? avatarFile,
        string? avatarChoice,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default);
}
