using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DWIS.Docker.Core.Models;
using Microsoft.Extensions.Logging;

namespace DWIS.Docker.Core.Services;

public sealed record RemoteTagInfo(string Repo, string Tag, IReadOnlySet<string> Digests, DateTimeOffset? LastUpdated);

/// <summary>Looks up what the registry currently holds for an image tag. Never throws.</summary>
public interface IImageRegistryService
{
    /// <summary>Null when the registry cannot be reached or the tag does not exist.</summary>
    Task<RemoteTagInfo?> GetTagAsync(string repo, string tag, CancellationToken ct = default);
}

/// <summary>Anonymous lookup against Docker Hub's v2 API (public repositories only).</summary>
public sealed class DockerHubRegistryService(HttpClient http, ILogger<DockerHubRegistryService> logger) : IImageRegistryService
{
    public const string BaseUrl = "https://hub.docker.com/v2/repositories/";

    public async Task<RemoteTagInfo?> GetTagAsync(string repo, string tag, CancellationToken ct = default)
    {
        var (ns, name) = Split(repo);
        var url = $"{BaseUrl}{ns}/{name}/tags/{tag}";
        try
        {
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Registry returned {Status} for {Repo}:{Tag}", (int)response.StatusCode, repo, tag);
                return null;
            }
            var dto = await response.Content.ReadFromJsonAsync<TagDto>(cancellationToken: ct).ConfigureAwait(false);
            if (dto is null) return null;

            var digests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(dto.Digest)) digests.Add(dto.Digest);
            foreach (var img in dto.Images ?? [])
                if (!string.IsNullOrEmpty(img.Digest)) digests.Add(img.Digest);

            return new RemoteTagInfo(repo, tag, digests, dto.LastUpdated);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Registry lookup failed for {Repo}:{Tag}: {Message}", repo, tag, ex.Message);
            return null;
        }
    }

    private static (string ns, string name) Split(string repo)
    {
        var slash = repo.IndexOf('/');
        return slash < 0 ? ("library", repo) : (repo[..slash], repo[(slash + 1)..]);
    }

    private sealed class TagDto
    {
        [JsonPropertyName("digest")] public string? Digest { get; set; }
        [JsonPropertyName("last_updated")] public DateTimeOffset? LastUpdated { get; set; }
        [JsonPropertyName("images")] public List<ImageDto>? Images { get; set; }
    }

    private sealed class ImageDto
    {
        [JsonPropertyName("digest")] public string? Digest { get; set; }
        [JsonPropertyName("architecture")] public string? Architecture { get; set; }
    }
}

/// <summary>Caches registry answers per image with a time-to-live, so polling never hits the registry.</summary>
public sealed class RegistryCache(IImageRegistryService registry, ISettingsStore settings)
{
    private readonly Dictionary<string, (DateTimeOffset at, RemoteTagInfo? info)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public RemoteTagInfo? Peek(string imageRef)
    {
        lock (_gate) return _cache.TryGetValue(imageRef, out var e) ? e.info : null;
    }

    public bool IsFresh(string imageRef)
    {
        lock (_gate)
            return _cache.TryGetValue(imageRef, out var e) && DateTimeOffset.UtcNow - e.at < settings.Current.RegistryCheckInterval;
    }

    public async Task<RemoteTagInfo?> GetAsync(string repo, string tag, bool force, CancellationToken ct)
    {
        var imageRef = $"{repo}:{tag}";
        if (!force && IsFresh(imageRef)) return Peek(imageRef);
        var info = await registry.GetTagAsync(repo, tag, ct).ConfigureAwait(false);
        lock (_gate) _cache[imageRef] = (DateTimeOffset.UtcNow, info);
        return info;
    }

    /// <summary>Decides the image status from what is local and what the registry says.</summary>
    public static ImageStatus Compare(LocalImageInfo? local, RemoteTagInfo? remote)
    {
        if (local is null) return ImageStatus.NotPulled;
        if (remote is null) return ImageStatus.Unknown;

        if (remote.Digests.Count > 0)
        {
            foreach (var rd in local.RepoDigests)
            {
                var at = rd.IndexOf('@');
                var digest = at >= 0 ? rd[(at + 1)..] : rd;
                if (remote.Digests.Contains(digest)) return ImageStatus.UpToDate;
            }
            if (local.RepoDigests.Count > 0) return ImageStatus.UpdateAvailable;
        }

        if (remote.LastUpdated is { } updated)
            return updated > local.Created + TimeSpan.FromMinutes(1) ? ImageStatus.UpdateAvailable : ImageStatus.UpToDate;

        return ImageStatus.Unknown;
    }
}
