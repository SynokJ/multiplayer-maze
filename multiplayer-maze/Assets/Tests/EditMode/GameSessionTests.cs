using System.Collections.Generic;
using NUnit.Framework;

public class GameSessionTests
{
    private const ulong P1 = 1;
    private const ulong P2 = 2;

    private static GameRules Rules(float time = 60.0f, int items = 3, int lives = 1, float countdown = 3.0f, int min = 1, int max = 4)
        => new GameRules
        {
            timeLimitSeconds = time,
            itemsRequired = items,
            startingLives = lives,
            countdownSeconds = countdown,
            minPlayers = min,
            maxPlayers = max
        };

    // One player, ready, countdown already finished: the round is running.
    private static GameSession StartedSession(GameRules rules, int players = 1)
    {
        var session = new GameSession(rules, 100);
        for (ulong id = 1; id <= (ulong)players; ++id)
            session.AddPlayer(id, "P" + id);
        for (ulong id = 1; id <= (ulong)players; ++id)
            session.SetReady(id, true);

        session.Tick(rules.countdownSeconds + 0.1f);
        Assert.AreEqual(GamePhase.Playing, session.Data.Phase);
        return session;
    }

    [Test]
    public void NewSession_StartsInLobby_WithClampedRules()
    {
        var session = new GameSession(new GameRules { maxPlayers = 1000, minPlayers = 0, timeLimitSeconds = -5 }, 7);

        Assert.AreEqual(GamePhase.Lobby, session.Data.Phase);
        Assert.AreEqual(7, session.Data.Seed);
        Assert.AreEqual(GameRules.MAX_PLAYERS_LIMIT, session.Data.Rules.maxPlayers);
        Assert.AreEqual(1, session.Data.Rules.minPlayers);
        Assert.Greater(session.Data.Rules.timeLimitSeconds, 0.0f);
    }

    [Test]
    public void AddPlayer_RejectsDuplicates_AndRoomOverMaxPlayers()
    {
        var session = new GameSession(Rules(max: 2), 1);

        Assert.IsTrue(session.AddPlayer(P1, "A"));
        Assert.IsFalse(session.AddPlayer(P1, "again"));
        Assert.IsTrue(session.AddPlayer(P2, "B"));
        Assert.IsFalse(session.AddPlayer(3, "C"));
        Assert.IsFalse(session.CanJoin);
        Assert.AreEqual(2, session.Data.Players.Count);
    }

    [Test]
    public void AddPlayer_UsesDefaultName_WhenNameIsEmpty()
    {
        var session = new GameSession(Rules(), 1);

        session.AddPlayer(P1, "   ");

        Assert.AreEqual("Player 1", session.Data.GetPlayer(P1).Name);
    }

    [Test]
    public void AllPlayersReady_StartsCountdown_ThenPlaying()
    {
        var session = new GameSession(Rules(countdown: 3.0f), 1);
        session.AddPlayer(P1, "A");
        session.AddPlayer(P2, "B");

        session.SetReady(P1, true);
        Assert.AreEqual(GamePhase.Lobby, session.Data.Phase);

        session.SetReady(P2, true);
        Assert.AreEqual(GamePhase.Countdown, session.Data.Phase);

        session.Tick(2.0f);
        Assert.AreEqual(GamePhase.Countdown, session.Data.Phase);

        session.Tick(1.5f);
        Assert.AreEqual(GamePhase.Playing, session.Data.Phase);
    }

    [Test]
    public void ZeroCountdown_StartsPlayingAtOnce()
    {
        var session = new GameSession(Rules(countdown: 0.0f), 1);
        session.AddPlayer(P1, "A");

        session.SetReady(P1, true);

        Assert.AreEqual(GamePhase.Playing, session.Data.Phase);
    }

    [Test]
    public void NotEnoughPlayers_DoesNotStart()
    {
        var session = new GameSession(Rules(min: 2), 1);
        session.AddPlayer(P1, "A");

        session.SetReady(P1, true);

        Assert.AreEqual(GamePhase.Lobby, session.Data.Phase);
    }

    [Test]
    public void Unready_DuringCountdown_ReturnsToLobby()
    {
        var session = new GameSession(Rules(), 1);
        session.AddPlayer(P1, "A");
        session.SetReady(P1, true);
        Assert.AreEqual(GamePhase.Countdown, session.Data.Phase);

        session.SetReady(P1, false);

        Assert.AreEqual(GamePhase.Lobby, session.Data.Phase);
    }

    [Test]
    public void PlayerLeaving_DuringCountdown_CancelsOnlyWhenTooFewRemain()
    {
        var session = new GameSession(Rules(min: 2), 1);
        session.AddPlayer(P1, "A");
        session.AddPlayer(P2, "B");
        session.SetReady(P1, true);
        session.SetReady(P2, true);
        Assert.AreEqual(GamePhase.Countdown, session.Data.Phase);

        session.RemovePlayer(P2);

        Assert.AreEqual(GamePhase.Lobby, session.Data.Phase);
    }

    [Test]
    public void LastUnreadyPlayerLeaving_StartsTheCountdownForTheRest()
    {
        var session = new GameSession(Rules(), 1);
        session.AddPlayer(P1, "A");
        session.AddPlayer(P2, "B");
        session.SetReady(P1, true);

        session.RemovePlayer(P2);

        Assert.AreEqual(GamePhase.Countdown, session.Data.Phase);
    }

    [Test]
    public void Playing_BlocksNewPlayers_AndReadyChanges()
    {
        GameSession session = StartedSession(Rules());

        Assert.IsFalse(session.CanJoin);
        Assert.IsFalse(session.AddPlayer(P2, "late"));
        Assert.IsFalse(session.SetReady(P1, false));
    }

    [Test]
    public void TimeRunningOut_LosesTheRound()
    {
        GameSession session = StartedSession(Rules(time: 10.0f));

        session.Tick(9.0f);
        Assert.AreEqual(GamePhase.Playing, session.Data.Phase);

        session.Tick(2.0f);
        Assert.AreEqual(GamePhase.Lost, session.Data.Phase);
        Assert.AreEqual(FinishReason.TimeUp, session.Data.FinishReason);
        Assert.AreEqual(0.0f, session.Data.TimeRemaining);
    }

    [Test]
    public void Tick_IgnoresZeroNegativeAndNaN()
    {
        GameSession session = StartedSession(Rules(time: 10.0f));
        float before = session.Data.TimeRemaining;

        session.Tick(0.0f);
        session.Tick(-5.0f);
        session.Tick(float.NaN);

        Assert.AreEqual(before, session.Data.TimeRemaining);
    }

    [Test]
    public void CollectItem_CountsForPlayerAndTeam_OnlyWhileAlivePlaying()
    {
        var session = new GameSession(Rules(), 1);
        session.AddPlayer(P1, "A");
        Assert.IsFalse(session.CollectItem(P1), "not playing yet");

        session.SetReady(P1, true);
        session.Tick(5.0f);

        Assert.IsTrue(session.CollectItem(P1));
        Assert.IsFalse(session.CollectItem(99), "unknown player");
        Assert.AreEqual(1, session.Data.ItemsCollected);
        Assert.AreEqual(1, session.Data.GetPlayer(P1).ItemsCollected);
    }

    [Test]
    public void ReachHatch_WithoutEnoughItems_IsBlocked()
    {
        GameSession session = StartedSession(Rules(items: 3));
        var blocked = new List<ulong>();
        session.HatchBlocked += blocked.Add;
        session.CollectItem(P1);

        bool won = session.ReachHatch(P1);

        Assert.IsFalse(won);
        Assert.AreEqual(GamePhase.Playing, session.Data.Phase);
        CollectionAssert.AreEqual(new ulong[] { P1 }, blocked);
    }

    [Test]
    public void ReachHatch_WithEnoughItems_WinsTheRound()
    {
        GameSession session = StartedSession(Rules(items: 2), players: 2);
        session.CollectItem(P1);
        session.CollectItem(P2);

        bool won = session.ReachHatch(P2);

        Assert.IsTrue(won);
        Assert.AreEqual(GamePhase.Won, session.Data.Phase);
        Assert.AreEqual(FinishReason.HatchReached, session.Data.FinishReason);
    }

    [Test]
    public void HitPlayer_LosesOnlyWhenEveryoneIsDead()
    {
        GameSession session = StartedSession(Rules(lives: 1), players: 2);

        session.HitPlayer(P1);
        Assert.IsFalse(session.Data.GetPlayer(P1).IsAlive);
        Assert.AreEqual(GamePhase.Playing, session.Data.Phase);

        session.HitPlayer(P2);
        Assert.AreEqual(GamePhase.Lost, session.Data.Phase);
        Assert.AreEqual(FinishReason.AllPlayersDead, session.Data.FinishReason);
    }

    [Test]
    public void ExtraLives_AreConsumedOneByOne()
    {
        GameSession session = StartedSession(Rules(lives: 3));

        session.HitPlayer(P1);
        session.HitPlayer(P1);

        Assert.AreEqual(1, session.Data.GetPlayer(P1).Lives);
        Assert.AreEqual(GamePhase.Playing, session.Data.Phase);
    }

    [Test]
    public void DeadPlayer_CannotCollectOrOpenTheHatch()
    {
        GameSession session = StartedSession(Rules(items: 0), players: 2);
        session.HitPlayer(P1);

        Assert.IsFalse(session.CollectItem(P1));
        Assert.IsFalse(session.ReachHatch(P1));
        Assert.AreEqual(GamePhase.Playing, session.Data.Phase);
    }

    [Test]
    public void LastAlivePlayerDisconnecting_LosesTheRound()
    {
        GameSession session = StartedSession(Rules(), players: 2);
        session.HitPlayer(P1);

        session.RemovePlayer(P2);

        Assert.AreEqual(GamePhase.Lost, session.Data.Phase);
        Assert.AreEqual(FinishReason.AllPlayersDead, session.Data.FinishReason);
    }

    [Test]
    public void Restart_OnlyAfterFinish_AndResetsTheRound()
    {
        GameSession session = StartedSession(Rules(items: 1, lives: 2));
        Assert.IsFalse(session.Restart(5), "still playing");

        session.CollectItem(P1);
        session.HitPlayer(P1);
        session.ReachHatch(P1);
        Assert.AreEqual(GamePhase.Won, session.Data.Phase);

        Assert.IsTrue(session.Restart(555));

        GameData data = session.Data;
        Assert.AreEqual(GamePhase.Lobby, data.Phase);
        Assert.AreEqual(555, data.Seed);
        Assert.AreEqual(1, data.Round);
        Assert.AreEqual(FinishReason.None, data.FinishReason);
        Assert.AreEqual(0, data.ItemsCollected);
        Assert.AreEqual(data.Rules.timeLimitSeconds, data.TimeRemaining);
        Assert.AreEqual(2, data.GetPlayer(P1).Lives);
        Assert.IsFalse(data.GetPlayer(P1).IsReady);
    }

    [Test]
    public void AfterRestart_APlayerCanJoin_AndTheNextRoundStarts()
    {
        GameSession session = StartedSession(Rules(items: 0));
        session.ReachHatch(P1);
        session.Restart(9);

        Assert.IsTrue(session.AddPlayer(P2, "new"));
        session.SetReady(P1, true);
        session.SetReady(P2, true);
        session.Tick(10.0f);

        Assert.AreEqual(GamePhase.Playing, session.Data.Phase);
    }

    [Test]
    public void PhaseChanged_ReportsTheWholeRoundInOrder()
    {
        var phases = new List<GamePhase>();
        var session = new GameSession(Rules(items: 0), 1);
        session.PhaseChanged += (from, to) => phases.Add(to);

        session.AddPlayer(P1, "A");
        session.SetReady(P1, true);
        session.Tick(5.0f);
        session.ReachHatch(P1);
        session.Restart(2);

        CollectionAssert.AreEqual(
            new[] { GamePhase.Countdown, GamePhase.Playing, GamePhase.Won, GamePhase.Lobby },
            phases);
    }
}
