using System.Text;
using Docker.DotNet.Models;

namespace DWIS.Docker.Core.Services;

/// <summary>
/// Renders Docker create parameters as the equivalent <c>docker run</c> command line, so a user can
/// recreate by hand exactly what <see cref="ContainerSpecBuilder"/> would create. Pure and unit-testable.
/// </summary>
public static class DockerRunCommandFormatter
{
    /// <summary>One line: <c>docker run -d --name … -p … -e … -v … -l … image args…</c>.</summary>
    public static string Format(CreateContainerParameters p)
    {
        var args = new List<string> { "docker", "run", "-d" };

        if (!string.IsNullOrEmpty(p.Name))
            args.AddRange(["--name", p.Name]);
        if (!string.IsNullOrEmpty(p.Hostname))
            args.AddRange(["--hostname", p.Hostname]);
        if (p.Tty && p.OpenStdin) args.Add("-it");
        else if (p.Tty) args.Add("-t");
        else if (p.OpenStdin) args.Add("-i");

        var host = p.HostConfig;
        if (host?.RestartPolicy is { Name: not RestartPolicyKind.No and not RestartPolicyKind.Undefined } policy)
        {
            var name = policy.Name switch
            {
                RestartPolicyKind.Always => "always",
                RestartPolicyKind.UnlessStopped => "unless-stopped",
                RestartPolicyKind.OnFailure => policy.MaximumRetryCount > 0 ? $"on-failure:{policy.MaximumRetryCount}" : "on-failure",
                _ => null
            };
            if (name is not null) args.AddRange(["--restart", name]);
        }

        if (host?.PortBindings is { Count: > 0 } ports)
        {
            foreach (var (key, bindings) in ports.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var containerPort = key.EndsWith("/tcp", StringComparison.OrdinalIgnoreCase) ? key[..^4] : key;
                foreach (var b in bindings ?? [])
                {
                    var hostPort = string.IsNullOrEmpty(b.HostPort) ? "" : b.HostPort;
                    var ip = string.IsNullOrEmpty(b.HostIP) ? "" : b.HostIP + ":";
                    args.AddRange(["-p", $"{ip}{hostPort}:{containerPort}"]);
                }
            }
        }
        else if (p.ExposedPorts is { Count: > 0 } exposed)
        {
            foreach (var key in exposed.Keys.OrderBy(k => k, StringComparer.Ordinal))
                args.AddRange(["--expose", key.EndsWith("/tcp", StringComparison.OrdinalIgnoreCase) ? key[..^4] : key]);
        }

        foreach (var env in p.Env ?? [])
            args.AddRange(["-e", env]);

        foreach (var bind in host?.Binds ?? [])
            args.AddRange(["-v", bind]);

        foreach (var extra in host?.ExtraHosts ?? [])
            args.AddRange(["--add-host", extra]);

        if (!string.IsNullOrEmpty(host?.NetworkMode))
            args.AddRange(["--network", host.NetworkMode]);

        foreach (var (key, value) in (p.Labels ?? new Dictionary<string, string>()).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            args.AddRange(["-l", $"{key}={value}"]);

        if (!string.IsNullOrEmpty(p.WorkingDir))
            args.AddRange(["-w", p.WorkingDir]);
        if (!string.IsNullOrEmpty(p.User))
            args.AddRange(["--user", p.User]);

        if (p.Entrypoint is { Count: > 0 } entrypoint)
            args.AddRange(["--entrypoint", entrypoint[0]]);

        args.Add(p.Image ?? "");

        if (p.Entrypoint is { Count: > 1 } ep)
            args.AddRange(ep.Skip(1));
        foreach (var c in p.Cmd ?? [])
            args.Add(c);

        var sb = new StringBuilder();
        foreach (var a in args)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(Quote(a));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Wraps an argument in double quotes when it has characters a shell would interpret. Double quotes
    /// are the one form that bash, PowerShell and cmd all accept for plain values. Backslashes are kept
    /// as they are so Windows paths stay valid.
    /// </summary>
    public static string Quote(string arg)
    {
        if (arg.Length == 0) return "\"\"";
        var safe = arg.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or '/' or ':' or '=' or ',' or '@' or '+' or '%' or '\\');
        if (safe) return arg;
        return "\"" + arg.Replace("\"", "\\\"") + "\"";
    }
}
