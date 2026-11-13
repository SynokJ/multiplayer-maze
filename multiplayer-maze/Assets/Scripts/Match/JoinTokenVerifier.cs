using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

public sealed class JoinClaims
{
    public string RoomId { get; }
    public string Name { get; }
    public string TokenId { get; }
    public long ExpiresAtUnix { get; }

    public JoinClaims(string roomId, string name, string tokenId, long expiresAtUnix)
    {
        RoomId = roomId;
        Name = name;
        TokenId = tokenId;
        ExpiresAtUnix = expiresAtUnix;
    }
}

public enum TokenError : byte
{
    None = 0,
    Malformed = 1,
    BadSignature = 2,
    WrongRoom = 3,
    Expired = 4,
    Reused = 5
}

/// <summary>
/// Room server side: checks the join token that the lobby signed.
/// Format (same as the lobby): base64url(JSON {r, n, j, exp}) + "." + base64url(HMAC-SHA256(secret, payloadB64)).
/// Each token works once, so a leaked token cannot be replayed.
/// </summary>
public sealed class JoinTokenVerifier
{
    private readonly byte[] _secret;
    private readonly string _roomId;
    private readonly HashSet<string> _usedTokenIds = new HashSet<string>();

    public JoinTokenVerifier(byte[] secret, string roomId)
    {
        _secret = secret;
        _roomId = roomId;
    }

    public TokenError Verify(string token, long nowUnix, out JoinClaims claims)
    {
        claims = null;

        string[] parts = (token ?? string.Empty).Split('.');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return TokenError.Malformed;

        byte[] givenMac;
        string json;
        try
        {
            givenMac = Base64UrlDecode(parts[1]);
            json = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        }
        catch (FormatException)
        {
            return TokenError.Malformed;
        }

        using (var hmac = new HMACSHA256(_secret))
        {
            byte[] expectedMac = hmac.ComputeHash(Encoding.UTF8.GetBytes(parts[0]));
            if (!CryptographicOperations.FixedTimeEquals(expectedMac, givenMac))
                return TokenError.BadSignature;
        }

        if (!TryReadString(json, "r", out string roomId)
            || !TryReadString(json, "n", out string name)
            || !TryReadString(json, "j", out string tokenId)
            || !TryReadLong(json, "exp", out long expires))
            return TokenError.Malformed;

        if (roomId != _roomId)
            return TokenError.WrongRoom;

        if (expires < nowUnix)
            return TokenError.Expired;

        if (!_usedTokenIds.Add(tokenId))
            return TokenError.Reused;

        claims = new JoinClaims(roomId, name, tokenId, expires);
        return TokenError.None;
    }

    // Lets a player use the same token again, e.g. when the connection was refused for another reason.
    public void Forget(string tokenId) => _usedTokenIds.Remove(tokenId);

    private static byte[] Base64UrlDecode(string text)
    {
        string padded = text.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
            case 1: throw new FormatException("bad base64url length");
        }

        return Convert.FromBase64String(padded);
    }

    // The payload is flat JSON written by the lobby; the signature is checked first,
    // so only simple "key":"value" and "key":number pairs need reading.
    private static bool TryReadString(string json, string key, out string value)
    {
        value = null;
        string marker = "\"" + key + "\":\"";
        int start = json.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return false;

        start += marker.Length;
        int end = json.IndexOf('"', start);
        if (end < 0)
            return false;

        value = json.Substring(start, end - start);
        return true;
    }

    private static bool TryReadLong(string json, string key, out long value)
    {
        value = 0;
        string marker = "\"" + key + "\":";
        int start = json.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return false;

        start += marker.Length;
        int end = start;
        while (end < json.Length && json[end] >= '0' && json[end] <= '9')
            end++;

        return end > start && long.TryParse(json.Substring(start, end - start), out value);
    }
}
