using System.Text.Json;
using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;

namespace DWIS.Docker.Core.Services;

/// <summary>Reads and writes the <c>config.json</c> of modules that have one.</summary>
public interface IConfigFileService
{
    /// <summary>Path of the config file as seen by this process.</summary>
    string GetLocalPath(ModuleDefinition module);

    /// <summary>Folder on the host side that is bind-mounted into the module container.</summary>
    string GetHostFolder(ModuleDefinition module);

    bool Exists(ModuleDefinition module);

    string GetDefaultJson(ModuleDefinition module);

    /// <summary>Returns problems with the JSON; empty when it is valid for the module's config type.</summary>
    IReadOnlyList<string> Validate(ModuleDefinition module, string json);

    Task<string?> ReadAsync(ModuleDefinition module, CancellationToken ct = default);

    /// <summary>Validates then writes. Throws <see cref="ConfigValidationException"/> on invalid JSON.</summary>
    Task WriteAsync(ModuleDefinition module, string json, CancellationToken ct = default);

    /// <summary>Writes the default config only when no file exists yet.</summary>
    Task EnsureDefaultAsync(ModuleDefinition module, CancellationToken ct = default);

    Task ResetToDefaultAsync(ModuleDefinition module, CancellationToken ct = default);
}

public sealed class ConfigFileService(HostEnvironment env, ISettingsStore settings) : IConfigFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions StrictOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private string HostRoot => settings.Current.HostVolumesRoot is { Length: > 0 } r ? r : env.DefaultHostVolumesRoot;

    public string GetHostFolder(ModuleDefinition module)
    {
        Require(module);
        return JoinHostPath(HostRoot, module.HostFolderName!);
    }

    public string GetLocalPath(ModuleDefinition module)
    {
        Require(module);
        return Path.Combine(env.LocalVolumesRoot(HostRoot), module.HostFolderName!, ModuleCatalog.ConfigFileName);
    }

    public bool Exists(ModuleDefinition module) =>
        module.HostFolderName is not null && File.Exists(GetLocalPath(module));

    public string GetDefaultJson(ModuleDefinition module)
    {
        if (module.ConfigType is null)
            throw new InvalidOperationException($"{module.DisplayName} has no configuration type.");
        var instance = Activator.CreateInstance(module.ConfigType)!;
        return JsonSerializer.Serialize(instance, module.ConfigType, JsonOptions);
    }

    public IReadOnlyList<string> Validate(ModuleDefinition module, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ["Configuration is empty."];
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return ["Configuration must be a JSON object."];
            if (module.ConfigType is not null)
                _ = JsonSerializer.Deserialize(json, module.ConfigType, StrictOptions);
            return [];
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? $" (line {line + 1}, position {ex.BytePositionInLine + 1})" : "";
            var message = ex.Message;
            var tail = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
            if (tail > 0) message = message[..tail];
            return [$"Invalid JSON{where}: {message}"];
        }
    }

    public async Task<string?> ReadAsync(ModuleDefinition module, CancellationToken ct = default)
    {
        var path = GetLocalPath(module);
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
    }

    public async Task WriteAsync(ModuleDefinition module, string json, CancellationToken ct = default)
    {
        var errors = Validate(module, json);
        if (errors.Count > 0)
            throw new ConfigValidationException(errors[0], errors);

        var path = GetLocalPath(module);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
        File.Move(tmp, path, overwrite: true);
    }

    public Task EnsureDefaultAsync(ModuleDefinition module, CancellationToken ct = default)
    {
        if (!module.HasConfig || Exists(module)) return Task.CompletedTask;
        return WriteAsync(module, GetDefaultJson(module), ct);
    }

    public Task ResetToDefaultAsync(ModuleDefinition module, CancellationToken ct = default) =>
        WriteAsync(module, GetDefaultJson(module), ct);

    private static void Require(ModuleDefinition module)
    {
        if (module.HostFolderName is null)
            throw new InvalidOperationException($"{module.DisplayName} has no configuration folder.");
    }

    private static string JoinHostPath(string root, string folder)
    {
        var separator = root.Contains('\\') || (root.Length >= 2 && root[1] == ':') ? '\\' : '/';
        return root.TrimEnd('\\', '/') + separator + folder;
    }
}
