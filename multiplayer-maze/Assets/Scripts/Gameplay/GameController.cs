using System;
using UnityEngine;

/// <summary>
/// Controller (thin): owns the GameSession, ticks it, and builds or clears the maze on phase changes.
/// Views (HUD, menus) listen to Session events. The round's rules live in GameSession.
/// Turn off "Generate On Start" on the MazeController: this component builds the maze now.
/// </summary>
public class GameController : MonoBehaviour
{
    [SerializeField] private MazeController _maze;
    [SerializeField] private LevelConfigSO _level;
    [Tooltip("Single player and Editor testing: a local player creates a room and is ready at once. " +
             "Turn off when a room server drives the game.")]
    [SerializeField] private bool _startOffline = true;

    private GameSession _session;
    private LocalMatch _localMatch;
    private SessionPlayerBridge _bridge;
    private MatchFlow _flow;

    // Only the authority (single player, or the room server) advances time.
    public bool HasAuthority { get; set; } = true;

    // Created on first use, so Views may subscribe in OnEnable in any order.
    public GameSession Session
    {
        get
        {
            if (_session == null)
                CreateSession();

            return _session;
        }
    }

    private async void Start()
    {
        if (!_startOffline)
            return;

        try
        {
            _localMatch = new LocalMatch();
            _bridge = new SessionPlayerBridge(Session, _localMatch);
            _flow = new MatchFlow(new OfflineMatchmaker(), _localMatch);

            bool created = await _flow.CreateRoomAsync("Player", null, Session.Data.Rules.maxPlayers);

            // The scene may have been left while we waited.
            if (this != null && created)
                Session.SetReady(LocalMatch.LocalPlayerId, true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void Update()
    {
        if (HasAuthority)
            Session.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        _flow?.Dispose();
        _bridge?.Dispose();

        if (_session != null)
            _session.PhaseChanged -= OnPhaseChanged;
    }

    public bool RequestRestart()
        => HasAuthority && Session.Restart(NewSeed());

    private void CreateSession()
    {
        GameRules rules = _level != null ? _level.Rules : GameRules.Default;

        _session = new GameSession(rules, NewSeed());
        _session.PhaseChanged += OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase from, GamePhase to)
    {
        if (_maze == null)
            return;

        if (to == GamePhase.Countdown && from == GamePhase.Lobby)
            BuildMaze();
        else if (to == GamePhase.Lobby)
            _maze.Clear();
    }

    private void BuildMaze()
    {
        int seed = _session.Data.Seed;

        if (_level != null)
            _maze.Generate(_level, seed);
        else
            _maze.Generate(new MazeSettings(MazeSettings.Default.width, MazeSettings.Default.height, seed));
    }

    private static int NewSeed() => UnityEngine.Random.Range(int.MinValue, int.MaxValue);
}
