using CloudMVCApplication.Data;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Services;

/// <summary>
/// Rewrites legacy local /uploads paths (and localhost upload URLs) to /storage/...
/// so private S3 objects are served through StorageController.
/// </summary>
public static class LocalUploadPathMigration
{
    public static async Task MigrateAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var imageUpdates = 0;
        var propertyUpdates = 0;
        var avatarUpdates = 0;
        var documentUpdates = 0;

        var requestImages = await context.RequestImages
            .Where(i => i.ImageUrl.Contains("/uploads/") || i.ImageUrl.Contains("localhost"))
            .ToListAsync();

        foreach (var image in requestImages)
        {
            var rewritten = RewriteToStoragePath(image.ImageUrl);
            if (rewritten is null || rewritten == image.ImageUrl)
            {
                continue;
            }

            image.ImageUrl = rewritten;
            imageUpdates++;
        }

        var properties = await context.Properties
            .Where(p => p.ImageUrl != null &&
                        (p.ImageUrl.Contains("/uploads/") || p.ImageUrl.Contains("localhost")))
            .ToListAsync();

        foreach (var property in properties)
        {
            var rewritten = RewriteToStoragePath(property.ImageUrl!);
            if (rewritten is null || rewritten == property.ImageUrl)
            {
                continue;
            }

            property.ImageUrl = rewritten;
            propertyUpdates++;
        }

        var users = await context.Users
            .Where(u => u.AvatarUrl != null &&
                        (u.AvatarUrl.Contains("/uploads/") || u.AvatarUrl.Contains("localhost")))
            .ToListAsync();

        foreach (var user in users)
        {
            var rewritten = RewriteToStoragePath(user.AvatarUrl!);
            if (rewritten is null || rewritten == user.AvatarUrl)
            {
                continue;
            }

            user.AvatarUrl = rewritten;
            avatarUpdates++;
        }

        var documents = await context.TechnicianDocuments
            .Where(d => d.FilePath.Contains("/uploads/") || d.FilePath.Contains("localhost"))
            .ToListAsync();

        foreach (var document in documents)
        {
            var rewritten = RewriteToStoragePath(document.FilePath);
            if (rewritten is null || rewritten == document.FilePath)
            {
                continue;
            }

            document.FilePath = rewritten;
            documentUpdates++;
        }

        var total = imageUpdates + propertyUpdates + avatarUpdates + documentUpdates;
        if (total == 0)
        {
            logger.LogInformation("Local upload path migration: no /uploads paths left to rewrite.");
            return;
        }

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Local upload path migration updated RequestImages={ImageUpdates}, Properties={PropertyUpdates}, Avatars={AvatarUpdates}, Documents={DocumentUpdates}.",
            imageUpdates,
            propertyUpdates,
            avatarUpdates,
            documentUpdates);
    }

    /// <summary>
    /// /uploads/issues/a.jpg -> /storage/issues/a.jpg
    /// http://localhost:5xxx/uploads/proofs/a.jpg -> /storage/proofs/a.jpg
    /// </summary>
    public static string? RewriteToStoragePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var value = path.Trim();

        // Absolute localhost (or any host) ending with /uploads/...
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            absolute.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            value = absolute.AbsolutePath;
        }

        const string uploadsPrefix = "/uploads/";
        if (!value.StartsWith(uploadsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relative = value[uploadsPrefix.Length..].TrimStart('/');
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        return $"/storage/{relative}";
    }
}
