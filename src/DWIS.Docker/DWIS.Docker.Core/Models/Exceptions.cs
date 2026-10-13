namespace DWIS.Docker.Core.Models;

/// <summary>Base for every error the UI should show to the user as-is.</summary>
public class DwisDockerException : Exception
{
    public DwisDockerException(string userMessage, Exception? inner = null) : base(userMessage, inner) { }

    public string UserMessage => Message;
}

public sealed class DockerUnavailableException(string message, Exception? inner = null)
    : DwisDockerException(message, inner);

public sealed class ContainerConflictException(string message, Exception? inner = null)
    : DwisDockerException(message, inner);

public sealed class PortInUseException(string message, Exception? inner = null)
    : DwisDockerException(message, inner);

public sealed class NotFoundException(string message, Exception? inner = null)
    : DwisDockerException(message, inner);

public sealed class ImagePullException(string message, Exception? inner = null)
    : DwisDockerException(message, inner);

public sealed class HubUnavailableException(string message, Exception? inner = null)
    : DwisDockerException(message, inner);

public sealed class ConfigValidationException(string message, IReadOnlyList<string> errors)
    : DwisDockerException(message)
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
