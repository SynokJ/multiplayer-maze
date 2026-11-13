using NUnit.Framework;

// Checks that the client reads the Node lobby's real JSON shapes (see the WebGL implementation guide).
public class LobbyHttpMatchmakerTests
{
    private const string CONNECT_JSON =
        "{\"room\":{\"id\":\"ABCDE\",\"name\":\"Alice room\",\"players\":0,\"maxPlayers\":4,\"state\":\"ready\"}," +
        "\"url\":\"wss://maze.duckdns.org:443/r/7003\",\"host\":\"maze.duckdns.org\",\"port\":443," +
        "\"secure\":true,\"path\":\"/r/7003\",\"token\":\"payload.mac\"}";

    [Test]
    public void CreateResponse_BecomesATicket()
    {
        MatchResult result = LobbyHttpMatchmaker.ParseConnect(201, CONNECT_JSON, false);

        Assert.IsTrue(result.Ok);
        Assert.AreEqual("ABCDE", result.Room.Id);
        Assert.AreEqual(4, result.Room.MaxPlayers);
        Assert.AreEqual("maze.duckdns.org", result.Ticket.host);
        Assert.AreEqual(443, result.Ticket.port);
        Assert.AreEqual("/r/7003", result.Ticket.path);
        Assert.IsTrue(result.Ticket.secure);
        Assert.AreEqual("payload.mac", result.Ticket.token);
    }

    [Test]
    public void JoinResponse_WithStatus200_IsAccepted()
    {
        Assert.IsTrue(LobbyHttpMatchmaker.ParseConnect(200, CONNECT_JSON, false).Ok);
    }

    [TestCase(404L, "{\"error\":\"no_such_room\"}", MatchErrors.NoSuchRoom)]
    [TestCase(409L, "{\"error\":\"room_full\"}", MatchErrors.RoomFull)]
    [TestCase(500L, "{\"error\":\"room_failed_to_start\"}", MatchErrors.RoomFailedToStart)]
    [TestCase(503L, "{\"error\":\"no_capacity\"}", MatchErrors.NoCapacity)]
    [TestCase(429L, "{\"statusCode\":429,\"error\":\"Too Many Requests\"}", MatchErrors.RateLimited)]
    [TestCase(400L, "{\"statusCode\":400,\"error\":\"Bad Request\",\"message\":\"x\"}", MatchErrors.InvalidRequest)]
    [TestCase(502L, "<html>Bad Gateway</html>", MatchErrors.Network)]
    public void LobbyErrors_AreMappedToCodes(long status, string body, string expected)
    {
        MatchResult result = LobbyHttpMatchmaker.ParseConnect(status, body, false);

        Assert.IsFalse(result.Ok);
        Assert.AreEqual(expected, result.Error);
    }

    [Test]
    public void RequestThatNeverReachedTheServer_IsANetworkError()
    {
        MatchResult result = LobbyHttpMatchmaker.ParseConnect(0, "", true);

        Assert.IsFalse(result.Ok);
        Assert.AreEqual(MatchErrors.Network, result.Error);
    }

    [Test]
    public void ResponseWithoutAToken_IsRejected()
    {
        string json = CONNECT_JSON.Replace("\"token\":\"payload.mac\"", "\"token\":\"\"");

        Assert.IsFalse(LobbyHttpMatchmaker.ParseConnect(201, json, false).Ok);
    }

    [Test]
    public void RoomList_ReadsEveryRoom()
    {
        const string json = "{\"rooms\":[" +
            "{\"id\":\"AAAAA\",\"name\":\"one\",\"players\":1,\"maxPlayers\":4,\"state\":\"ready\"}," +
            "{\"id\":\"BBBBB\",\"name\":\"two\",\"players\":3,\"maxPlayers\":8,\"state\":\"ready\"}]}";

        RoomListResult result = LobbyHttpMatchmaker.ParseList(200, json, false);

        Assert.IsTrue(result.Ok);
        Assert.AreEqual(2, result.Rooms.Count);
        Assert.AreEqual("BBBBB", result.Rooms[1].Id);
        Assert.AreEqual(3, result.Rooms[1].Players);
    }

    [Test]
    public void EmptyRoomList_IsStillOk()
    {
        RoomListResult result = LobbyHttpMatchmaker.ParseList(200, "{\"rooms\":[]}", false);

        Assert.IsTrue(result.Ok);
        Assert.AreEqual(0, result.Rooms.Count);
    }
}
