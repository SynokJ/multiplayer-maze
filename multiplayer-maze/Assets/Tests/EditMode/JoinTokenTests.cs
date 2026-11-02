using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

public class JoinTokenTests
{
    private const string SECRET = "test-secret-test-secret-test-secret!";

    // Made by the lobby's own algorithm (Node: createHmac sha256 over the base64url payload),
    // payload {"r":"ABCDE","n":"Alice","j":"a1b2c3d4e5f60718","exp":4102444800}.
    private const string NODE_TOKEN =
        "eyJyIjoiQUJDREUiLCJuIjoiQWxpY2UiLCJqIjoiYTFiMmMzZDRlNWY2MDcxOCIsImV4cCI6NDEwMjQ0NDgwMH0." +
        "ioD25BXxu5W3LcpX8CoeRVzWVw-zgjBfYwiwAcGkL1Q";

    private const long NOW = 1_700_000_000;

    private static JoinTokenVerifier NewVerifier(string roomId = "ABCDE")
        => new JoinTokenVerifier(Encoding.UTF8.GetBytes(SECRET), roomId);

    private static string MakeToken(string secret, string roomId, string name, string tokenId, long expires)
    {
        string payload = Base64Url(Encoding.UTF8.GetBytes(
            "{\"r\":\"" + roomId + "\",\"n\":\"" + name + "\",\"j\":\"" + tokenId + "\",\"exp\":" + expires + "}"));

        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            return payload + "." + Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Test]
    public void TokenFromTheNodeLobby_IsAccepted()
    {
        TokenError error = NewVerifier().Verify(NODE_TOKEN, NOW, out JoinClaims claims);

        Assert.AreEqual(TokenError.None, error);
        Assert.AreEqual("ABCDE", claims.RoomId);
        Assert.AreEqual("Alice", claims.Name);
        Assert.AreEqual("a1b2c3d4e5f60718", claims.TokenId);
        Assert.AreEqual(4102444800L, claims.ExpiresAtUnix);
    }

    [Test]
    public void TokenMadeInCSharp_IsAccepted()
    {
        string token = MakeToken(SECRET, "ROOM1", "Bob", "id-1", NOW + 60);

        Assert.AreEqual(TokenError.None, NewVerifier("ROOM1").Verify(token, NOW, out JoinClaims claims));
        Assert.AreEqual("Bob", claims.Name);
    }

    [Test]
    public void TokenCannotBeUsedTwice_UntilForgotten()
    {
        JoinTokenVerifier verifier = NewVerifier();

        Assert.AreEqual(TokenError.None, verifier.Verify(NODE_TOKEN, NOW, out JoinClaims first));
        Assert.AreEqual(TokenError.Reused, verifier.Verify(NODE_TOKEN, NOW, out _));

        verifier.Forget(first.TokenId);
        Assert.AreEqual(TokenError.None, verifier.Verify(NODE_TOKEN, NOW, out _));
    }

    [Test]
    public void TokenForAnotherRoom_IsRejected()
    {
        Assert.AreEqual(TokenError.WrongRoom, NewVerifier("ZZZZZ").Verify(NODE_TOKEN, NOW, out _));
    }

    [Test]
    public void ExpiredToken_IsRejected()
    {
        string token = MakeToken(SECRET, "ABCDE", "Alice", "id-2", NOW - 1);

        Assert.AreEqual(TokenError.Expired, NewVerifier().Verify(token, NOW, out _));
    }

    [Test]
    public void TokenSignedWithAnotherSecret_IsRejected()
    {
        string token = MakeToken("another-secret-another-secret-12345", "ABCDE", "Alice", "id-3", NOW + 60);

        Assert.AreEqual(TokenError.BadSignature, NewVerifier().Verify(token, NOW, out _));
    }

    [Test]
    public void ChangingThePayload_BreaksTheSignature()
    {
        string[] parts = NODE_TOKEN.Split('.');
        string forged = Base64Url(Encoding.UTF8.GetBytes(
            "{\"r\":\"ABCDE\",\"n\":\"Mallory\",\"j\":\"a1b2c3d4e5f60718\",\"exp\":4102444800}")) + "." + parts[1];

        Assert.AreEqual(TokenError.BadSignature, NewVerifier().Verify(forged, NOW, out _));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("nodots")]
    [TestCase("a.b.c")]
    [TestCase(".")]
    [TestCase("not base64!.also not")]
    [TestCase("a.")]
    public void GarbageTokens_AreMalformed(string token)
    {
        Assert.AreEqual(TokenError.Malformed, NewVerifier().Verify(token, NOW, out _));
    }

    [Test]
    public void ValidSignatureButMissingFields_IsMalformed()
    {
        string token = MakeToken(SECRET, "ABCDE", "Alice", "id-4", NOW + 60).Split('.')[0];
        string payload = Base64Url(Encoding.UTF8.GetBytes("{\"r\":\"ABCDE\"}"));
        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SECRET)))
            token = payload + "." + Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        Assert.AreEqual(TokenError.Malformed, NewVerifier().Verify(token, NOW, out _));
    }

    [Test]
    public void ConnectionPayload_RoundTrips()
    {
        byte[] bytes = ConnectionPayload.Encode("0.1.0", NODE_TOKEN);

        Assert.IsTrue(ConnectionPayload.TryDecode(bytes, out string version, out string token));
        Assert.AreEqual("0.1.0", version);
        Assert.AreEqual(NODE_TOKEN, token);
    }

    [Test]
    public void ConnectionPayload_RejectsEmptyAndOversizedData()
    {
        Assert.IsFalse(ConnectionPayload.TryDecode(null, out _, out _));
        Assert.IsFalse(ConnectionPayload.TryDecode(new byte[0], out _, out _));
        Assert.IsFalse(ConnectionPayload.TryDecode(Encoding.UTF8.GetBytes("no separator"), out _, out _));
        Assert.IsFalse(ConnectionPayload.TryDecode(Encoding.UTF8.GetBytes("|token"), out _, out _));
        Assert.IsFalse(ConnectionPayload.TryDecode(Encoding.UTF8.GetBytes("version|"), out _, out _));
        Assert.IsFalse(ConnectionPayload.TryDecode(new byte[3000], out _, out _));
    }

    [Test]
    public void LaunchOptions_ReadArgumentsAndEnvironment()
    {
        var args = new[] { "game.x86_64", "-batchmode", "-port", "7003", "-roomId", "ABCDE", "-maxPlayers", "4", "-level", "2" };
        var environment = new System.Collections.Generic.Dictionary<string, string>
        {
            { "ROOM_SECRET", "s3cret" }, { "LOBBY_URL", "http://127.0.0.1:3000" }, { "LOBBY_INTERNAL_KEY", "key" }
        };

        RoomLaunchOptions options = RoomLaunchOptions.Parse(args, name => environment.TryGetValue(name, out string v) ? v : null);

        Assert.AreEqual(7003, options.Port);
        Assert.AreEqual("ABCDE", options.RoomId);
        Assert.AreEqual(4, options.MaxPlayers);
        Assert.AreEqual(2, options.LevelIndex);
        Assert.AreEqual("s3cret", options.Secret);
        Assert.AreEqual("http://127.0.0.1:3000", options.LobbyUrl);
        Assert.AreEqual("key", options.LobbyKey);
    }

    [Test]
    public void LaunchOptions_FallBackToDefaults_OnBadOrMissingValues()
    {
        var args = new[] { "-port", "99999", "-maxPlayers", "-3", "-level", "x", "-roomId" };

        RoomLaunchOptions options = RoomLaunchOptions.Parse(args, _ => null);

        Assert.AreEqual(7777, options.Port);
        Assert.AreEqual("DEV00", options.RoomId);
        Assert.AreEqual(8, options.MaxPlayers);
        Assert.AreEqual(0, options.LevelIndex);
        Assert.IsNull(options.LobbyUrl);
    }

    [Test]
    public void LaunchOptions_CapMaxPlayers()
    {
        RoomLaunchOptions options = RoomLaunchOptions.Parse(new[] { "-maxPlayers", "500" }, _ => null);

        Assert.AreEqual(GameRules.MAX_PLAYERS_LIMIT, options.MaxPlayers);
    }
}
