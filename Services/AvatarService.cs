using CloudMVCApplication.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace CloudMVCApplication.Services;

public sealed class AvatarService : IAvatarService
{
    public const long MaxAvatarFileSize = 5 * 1024 * 1024;

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp"
    };

    public static readonly HashSet<string> AllowedPresetAvatars = new(StringComparer.OrdinalIgnoreCase)
    {
        "/images/avatars/avatar-1.svg",
        "/images/avatars/avatar-2.svg",
        "/images/avatars/avatar-3.svg",
        "/images/avatars/avatar-4.svg"
    };

    private readonly IFileStorageService _fileStorage;
    private readonly UserManager<ApplicationUser> _userManager;

    public AvatarService(
        IFileStorageService fileStorage,
        UserManager<ApplicationUser> userManager)
    {
        _fileStorage = fileStorage;
        _userManager = userManager;
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> UpdateAsync(
        ApplicationUser user,
        IFormFile? avatarFile,
        string? avatarChoice,
        CancellationToken cancellationToken = default)
    {
        if (avatarFile != null && avatarFile.Length > 0)
        {
            var validationError = ValidateUpload(avatarFile);
            if (validationError != null)
            {
                return (false, validationError);
            }

            var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            var previousAvatar = user.AvatarUrl;
            var fileName = $"{user.Id}-{Guid.NewGuid():N}{extension}";
            user.AvatarUrl = await _fileStorage.UploadAsync(
                avatarFile,
                "avatars",
                fileName,
                cancellationToken);

            await _userManager.UpdateAsync(user);
            await DeleteStoredAvatarIfNeededAsync(previousAvatar, cancellationToken);
            return (true, null);
        }

        if (!string.IsNullOrWhiteSpace(avatarChoice))
        {
            var choice = avatarChoice.Trim();
            if (!AllowedPresetAvatars.Contains(choice))
            {
                return (false, "Choose a valid default avatar or upload an image.");
            }

            var previousAvatar = user.AvatarUrl;
            user.AvatarUrl = choice;
            await _userManager.UpdateAsync(user);
            await DeleteStoredAvatarIfNeededAsync(previousAvatar, cancellationToken);
            return (true, null);
        }

        return (false, "Choose an avatar or upload an image first.");
    }

    public async Task RemoveAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var previousAvatar = user.AvatarUrl;
        user.AvatarUrl = null;
        await _userManager.UpdateAsync(user);
        await DeleteStoredAvatarIfNeededAsync(previousAvatar, cancellationToken);
    }

    private static string? ValidateUpload(IFormFile avatarFile)
    {
        if (avatarFile.Length <= 0)
        {
            return "Avatar image cannot be empty.";
        }

        if (avatarFile.Length > MaxAvatarFileSize)
        {
            return "Avatar image must be 5MB or smaller.";
        }

        var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return "Please upload a JPG, PNG, GIF, or WebP image.";
        }

        return null;
    }

    private async Task DeleteStoredAvatarIfNeededAsync(
        string? previousAvatar,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(previousAvatar) &&
            previousAvatar.StartsWith(S3FileStorageService.StorageRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _fileStorage.DeleteAsync(previousAvatar, cancellationToken);
        }
    }
}
