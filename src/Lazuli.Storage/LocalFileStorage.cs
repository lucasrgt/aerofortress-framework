namespace Lazuli.Storage;

/// <summary>
/// A dev/local <see cref="IFileStorage"/> that writes files under a base directory and returns a plain
/// (unsigned) URL — so upload flows run end-to-end with no cloud account. A real backend (S3 / R2 /
/// MinIO) replaces it in the composition root and is where genuine signed URLs come from.
/// </summary>
/// <param name="baseDirectory">The directory files are written under.</param>
/// <param name="baseUrl">The URL prefix the returned read URLs are built from.</param>
public sealed class LocalFileStorage(string baseDirectory, string baseUrl) : IFileStorage
{
    /// <inheritdoc />
    public async Task<string> SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = Path.Combine(baseDirectory, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
        return key;
    }

    /// <inheritdoc />
    public Task<Uri> GetUrlAsync(string key, TimeSpan ttl, CancellationToken ct = default) =>
        Task.FromResult(new Uri($"{baseUrl.TrimEnd('/')}/{key}"));

    /// <inheritdoc />
    public Task<UploadIntent> GetUploadUrlAsync(string key, string contentType, TimeSpan ttl, CancellationToken ct = default) =>
        Task.FromResult(new UploadIntent(key, $"{baseUrl.TrimEnd('/')}/{key}", "PUT", contentType, DateTimeOffset.UtcNow.Add(ttl)));

    /// <inheritdoc />
    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = Path.Combine(baseDirectory, key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }
}
