using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

public class MatchFlowTests
{
    private sealed class FakeClient : IMatchClient
    {
        public event Action Connected;
        public event Action<string> Disconnected;

        public int connectCalls;
        public int disconnectCalls;
        public RoomTicket lastTicket;
        public bool connectImmediately = true;

        public void Connect(RoomTicket ticket)
        {
            connectCalls++;
            lastTicket = ticket;
            if (connectImmediately)
                Connected?.Invoke();
        }

        public void Disconnect()
        {
            disconnectCalls++;
            Disconnected?.Invoke("left");
        }

        public void RaiseConnected() => Connected?.Invoke();
        public void RaiseDisconnected(string reason) => Disconnected?.Invoke(reason);
    }

    private sealed class ThrowingMatchmaker : IMatchmaker
    {
        public Task<MatchResult> CreateRoomAsync(string playerName, string roomName, int maxPlayers) => throw new InvalidOperationException("boom");
        public Task<MatchResult> JoinRoomAsync(string roomId, string playerName) => throw new InvalidOperationException("boom");
        public Task<RoomListResult> ListRoomsAsync() => throw new InvalidOperationException("boom");
    }

    private static MatchFlow NewFlow(IMatchmaker matchmaker, FakeClient client) => new MatchFlow(matchmaker, client);

    [Test]
    public async Task CreateRoom_ConnectsWithTheTicket()
    {
        var client = new FakeClient();
        var flow = NewFlow(new OfflineMatchmaker(), client);
        var states = new List<MatchState>();
        flow.StateChanged += states.Add;

        bool ok = await flow.CreateRoomAsync("Alice", null, 4);

        Assert.IsTrue(ok);
        Assert.AreEqual(MatchState.Connected, flow.State);
        Assert.AreEqual(1, client.connectCalls);
        Assert.AreEqual("Alice", client.lastTicket.token);
        Assert.AreEqual(4, flow.Room.MaxPlayers);
        CollectionAssert.AreEqual(new[] { MatchState.Requesting, MatchState.Connecting, MatchState.Connected }, states);
    }

    [Test]
    public async Task InvalidPlayerName_FailsWithoutCallingTheMatchmaker()
    {
        var client = new FakeClient();
        var flow = NewFlow(new ThrowingMatchmaker(), client);

        bool ok = await flow.CreateRoomAsync("bad/name", null, 4);

        Assert.IsFalse(ok);
        Assert.AreEqual(MatchState.Failed, flow.State);
        Assert.AreEqual(MatchErrors.InvalidRequest, flow.LastError);
        Assert.AreEqual(0, client.connectCalls);
    }

    [Test]
    public async Task JoinUnknownRoom_Fails()
    {
        var flow = NewFlow(new OfflineMatchmaker(), new FakeClient());

        bool ok = await flow.JoinRoomAsync("ZZZZZ", "Bob");

        Assert.IsFalse(ok);
        Assert.AreEqual(MatchErrors.NoSuchRoom, flow.LastError);
    }

    [Test]
    public async Task JoinBadRoomCode_FailsAsInvalid()
    {
        var flow = NewFlow(new OfflineMatchmaker(), new FakeClient());

        bool ok = await flow.JoinRoomAsync("abc", "Bob");

        Assert.IsFalse(ok);
        Assert.AreEqual(MatchErrors.InvalidRequest, flow.LastError);
    }

    [Test]
    public async Task SecondPlayer_CanJoinByCode_IgnoringCase()
    {
        var matchmaker = new OfflineMatchmaker();
        var host = NewFlow(matchmaker, new FakeClient());
        await host.CreateRoomAsync("Alice", null, 4);

        var guestClient = new FakeClient();
        var guest = NewFlow(matchmaker, guestClient);
        bool ok = await guest.JoinRoomAsync(host.Room.Id.ToLowerInvariant(), "Bob");

        Assert.IsTrue(ok);
        Assert.AreEqual(MatchState.Connected, guest.State);
        Assert.AreEqual(2, guest.Room.Players);
        Assert.AreEqual("Bob", guestClient.lastTicket.token);
    }

    [Test]
    public async Task FullRoom_RejectsAnotherPlayer()
    {
        var matchmaker = new OfflineMatchmaker();
        var host = NewFlow(matchmaker, new FakeClient());
        await host.CreateRoomAsync("Alice", null, 1);

        var guest = NewFlow(matchmaker, new FakeClient());
        bool ok = await guest.JoinRoomAsync(host.Room.Id, "Bob");

        Assert.IsFalse(ok);
        Assert.AreEqual(MatchErrors.RoomFull, guest.LastError);
    }

    [Test]
    public async Task MatchmakerException_BecomesANetworkFailure()
    {
        var flow = NewFlow(new ThrowingMatchmaker(), new FakeClient());

        bool ok = await flow.CreateRoomAsync("Alice", null, 4);

        Assert.IsFalse(ok);
        Assert.AreEqual(MatchState.Failed, flow.State);
        Assert.AreEqual(MatchErrors.Network, flow.LastError);
    }

    [Test]
    public async Task CannotStartASecondRequest_WhileConnected()
    {
        var client = new FakeClient();
        var flow = NewFlow(new OfflineMatchmaker(), client);
        await flow.CreateRoomAsync("Alice", null, 4);

        bool again = await flow.CreateRoomAsync("Alice", null, 4);

        Assert.IsFalse(again);
        Assert.AreEqual(1, client.connectCalls);
        Assert.AreEqual(MatchState.Connected, flow.State);
    }

    [Test]
    public async Task ServerRefusal_WhileConnecting_FailsWithTheReason()
    {
        var client = new FakeClient { connectImmediately = false };
        var flow = NewFlow(new OfflineMatchmaker(), client);
        await flow.CreateRoomAsync("Alice", null, 4);
        Assert.AreEqual(MatchState.Connecting, flow.State);

        client.RaiseDisconnected("bad token");

        Assert.AreEqual(MatchState.Failed, flow.State);
        Assert.AreEqual("bad token", flow.LastError);
    }

    [Test]
    public async Task Leave_GoesIdle_WithoutReportingAFailure()
    {
        var client = new FakeClient();
        var flow = NewFlow(new OfflineMatchmaker(), client);
        await flow.CreateRoomAsync("Alice", null, 4);
        var states = new List<MatchState>();
        flow.StateChanged += states.Add;

        flow.Leave();

        Assert.AreEqual(MatchState.Idle, flow.State);
        Assert.AreEqual(1, client.disconnectCalls);
        Assert.IsNull(flow.LastError);
        CollectionAssert.AreEqual(new[] { MatchState.Idle }, states);
    }

    [Test]
    public async Task CanTryAgain_AfterAFailure()
    {
        var flow = NewFlow(new OfflineMatchmaker(), new FakeClient());
        await flow.JoinRoomAsync("ZZZZZ", "Bob");
        Assert.AreEqual(MatchState.Failed, flow.State);

        bool ok = await flow.CreateRoomAsync("Bob", null, 4);

        Assert.IsTrue(ok);
        Assert.AreEqual(MatchState.Connected, flow.State);
        Assert.IsNull(flow.LastError);
    }

    [Test]
    public async Task OfflineFlow_PutsTheLocalPlayerIntoTheGameSession()
    {
        var session = new GameSession(GameRules.Default, 1);
        var local = new LocalMatch();
        using (new SessionPlayerBridge(session, local))
        using (var flow = new MatchFlow(new OfflineMatchmaker(), local))
        {
            await flow.CreateRoomAsync("Alice", null, 4);

            Assert.AreEqual(1, session.Data.Players.Count);
            Assert.AreEqual("Alice", session.Data.GetPlayer(LocalMatch.LocalPlayerId).Name);

            flow.Leave();
            Assert.AreEqual(0, session.Data.Players.Count);
        }
    }

    [Test]
    public void Bridge_ReportsPlayersTheSessionRefuses()
    {
        var session = new GameSession(new GameRules { maxPlayers = 1, minPlayers = 1 }, 1);
        var source = new TestPlayerSource();
        var rejected = new List<ulong>();
        using (var bridge = new SessionPlayerBridge(session, source))
        {
            bridge.PlayerRejected += rejected.Add;

            source.Connect(1, "A");
            source.Connect(2, "B");
        }

        CollectionAssert.AreEqual(new ulong[] { 2 }, rejected);
        Assert.AreEqual(1, session.Data.Players.Count);
    }

    [TestCase("Alice", true)]
    [TestCase("a b-c.d_9", true)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("bad/name", false)]
    [TestCase("it's", false)]
    [TestCase("123456789012345678901234", true)]
    [TestCase("1234567890123456789012345", false)]
    public void PlayerNames_FollowTheLobbyRules(string name, bool valid)
    {
        Assert.AreEqual(valid, MatchValidation.IsValidName(name));
    }

    [TestCase("abcde", "ABCDE")]
    [TestCase(" a1b2c ", "A1B2C")]
    [TestCase("abcd", null)]
    [TestCase("abcdef", null)]
    [TestCase("ab-de", null)]
    [TestCase(null, null)]
    public void RoomCodes_AreNormalized(string input, string expected)
    {
        Assert.AreEqual(expected, MatchValidation.NormalizeRoomId(input));
    }

    private sealed class TestPlayerSource : IPlayerSource
    {
        public event Action<ulong, string> PlayerConnected;
        public event Action<ulong> PlayerDisconnected;

        public void Connect(ulong id, string name) => PlayerConnected?.Invoke(id, name);
        public void Disconnect(ulong id) => PlayerDisconnected?.Invoke(id);
    }
}
