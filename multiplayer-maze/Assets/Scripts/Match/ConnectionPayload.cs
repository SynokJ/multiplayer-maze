using System.Text;

/// <summary>
/// What a client sends with its connection request: the build version and the join token.
/// The room server rejects a mismatched version (a stale browser cache is the usual cause).
/// </summary>
public static class ConnectionPayload
{
    private const char _SEPARATOR = '|';

    public static byte[] Encode(string version, string token)
        => Encoding.UTF8.GetBytes((version ?? string.Empty) + _SEPARATOR + (token ?? string.Empty));

    public static bool TryDecode(byte[] payload, out string version, out string token)
    {
        version = null;
        token = null;

        if (payload == null || payload.Length == 0 || payload.Length > 2048)
            return false;

        string text = Encoding.UTF8.GetString(payload);
        int split = text.IndexOf(_SEPARATOR);
        if (split <= 0 || split == text.Length - 1)
            return false;

        version = text.Substring(0, split);
        token = text.Substring(split + 1);
        return true;
    }
}
