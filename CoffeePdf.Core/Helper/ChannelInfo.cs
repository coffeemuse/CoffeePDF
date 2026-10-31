using System.Text.Json;

namespace CoffeePdf.Core.Helper;

/// <summary>
/// <c>channel.json</c>, which the Helper writes in its support folder while its channel is up (the Desktop Clients
/// page): the port on 127.0.0.1, the secret every request carries, and the Helper's process id.
/// </summary>
public sealed record ChannelInfo(int Port, string Secret, int Pid)
{
    /// <summary>
    /// The file's contents, or null when it's missing, damaged, or not the Helper's shape: a port, a secret of 48 hex
    /// digits, and a process id.
    /// </summary>
    public static ChannelInfo? Read(string file)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(file));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("port", out var port) || port.ValueKind != JsonValueKind.Number || !port.TryGetInt32(out var p) || p is < 1 or > 65535
                || !root.TryGetProperty("secret", out var secret) || secret.ValueKind != JsonValueKind.String
                || secret.GetString() is not { Length: 48 } s || !s.All(Uri.IsHexDigit)
                || !root.TryGetProperty("pid", out var pid) || pid.ValueKind != JsonValueKind.Number || !pid.TryGetInt32(out var id) || id < 1)
                return null;
            return new ChannelInfo(p, s, id);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Leaves the secret out, so nothing that prints this prints it (§6.9).</summary>
    public override string ToString() => $"ChannelInfo {{ Port = {Port}, Pid = {Pid} }}";
}
