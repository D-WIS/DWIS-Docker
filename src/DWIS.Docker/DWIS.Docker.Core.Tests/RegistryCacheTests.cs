using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using Xunit;

namespace DWIS.Docker.Core.Tests;

public class RegistryCacheTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static LocalImageInfo Local(string? digest, DateTimeOffset? created = null) =>
        new("sha256:local", ["digiwells/x:stable"], digest is null ? [] : [$"digiwells/x@{digest}"], created ?? T0);

    private static RemoteTagInfo Remote(DateTimeOffset? updated, params string[] digests) =>
        new("digiwells/x", "stable", new HashSet<string>(digests, StringComparer.OrdinalIgnoreCase), updated);

    [Fact]
    public void Missing_local_image_is_not_pulled() =>
        Assert.Equal(ImageStatus.NotPulled, RegistryCache.Compare(null, Remote(T0, "sha256:a")));

    [Fact]
    public void No_registry_answer_is_unknown() =>
        Assert.Equal(ImageStatus.Unknown, RegistryCache.Compare(Local("sha256:a"), null));

    [Fact]
    public void Matching_digest_is_up_to_date() =>
        Assert.Equal(ImageStatus.UpToDate, RegistryCache.Compare(Local("sha256:a"), Remote(T0.AddDays(5), "sha256:list", "sha256:a")));

    [Fact]
    public void Different_digest_is_update_available() =>
        Assert.Equal(ImageStatus.UpdateAvailable, RegistryCache.Compare(Local("sha256:a"), Remote(T0, "sha256:b")));

    [Fact]
    public void Locally_built_image_falls_back_to_dates()
    {
        Assert.Equal(ImageStatus.UpdateAvailable, RegistryCache.Compare(Local(null, T0), Remote(T0.AddHours(1), "sha256:b")));
        Assert.Equal(ImageStatus.UpToDate, RegistryCache.Compare(Local(null, T0.AddHours(2)), Remote(T0, "sha256:b")));
    }

    [Fact]
    public void Nothing_to_compare_is_unknown() =>
        Assert.Equal(ImageStatus.Unknown, RegistryCache.Compare(Local(null), Remote(null)));
}
