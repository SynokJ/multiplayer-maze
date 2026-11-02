# Multiplayer Maze

Unity 6 (6000.4), URP 2D, C#. Project lives in `multiplayer-maze/`. The old single-player version is in `_old/` (gitignored, reference only).

## Architecture rules

Every system follows SOLID together with MVC. Keep KISS in mind: do not add a pattern or interface until there is a second use for it.

### Model
- Data only, no behaviour.
- Read-only config (levels, wall styles, balance numbers) is a ScriptableObject. Never write runtime state into one: the asset is shared, its changes persist in the Editor, and it cannot be synced over the network.
- Runtime state (health, score, positions, collected items) is a plain C# class built from the config.
- Networked state is a `NetworkVariable`, not a ScriptableObject.
- Anything that can be rebuilt from a seed or config (e.g. `MazeData`) is not an asset.
- The Model knows nothing about Views or Controllers.

### View
- Only presentation: rendering, effects, animation, audio, UI.
- Reacts to the Model and Controller through events (Observer). Unsubscribe in `OnDisable`/`OnDestroy`.
- Never writes to the Model. User input is raised as an event and the Controller decides.

### Controller
- Wires Models, Views and other components, and responds to input events.
- Keep it thin. Real logic goes into plain C# classes (no Unity types where possible) so it can be unit tested.

### Rules
- Dependencies go one way: Controller changes Model, View observes.
- Prefer composition and small interfaces over inheritance. Depend on abstractions, not concrete classes.
- No static managers or singletons for game state. The old project's statics are the thing to avoid.
- Do not force MVC onto objects that are inherently view plus physics (`Rigidbody2D`, `SpriteRenderer`). Single responsibility is enough there.

### Multiplayer
- The host or server Controller owns the game logic.
- Model state syncs to clients, and every client's View reacts.
- Only send small data: a level index and a seed, never an asset. The same `(width, height, seed)` must build the same maze on every machine.

## Layers (Assets/Scripts)
- `Maze/`: seeded maze. `MazeData` is pure C#; `MazeBuilder` is the View; `MazeController` wires them.
- `Gameplay/`: `GameData` (Model), `GameSession` (pure C# rules and flow: Lobby, Countdown, Playing, Won/Lost, Restart), `GameController` (thin Unity wrapper).
- `Match/`: `IMatchmaker` (lobby HTTP or offline), `IMatchClient` (player connection), `IPlayerSource` (room server connection events), `MatchFlow`, `SessionPlayerBridge`. The NGO transport implements the two interfaces later.
- Keep Unity types out of the pure C# files so they run under `dotnet test` (C# 9 only, Unity 6).
- Never pause the game with `Time.timeScale`; the server advances time through `GameSession.Tick`.
- The lobby API contract and room flow are described in the WebGL implementation guide in `Docs`.

## Code style
- Private serialized fields use `_camelCase`, constants use `_UPPER_CASE`, matching the existing scripts.
- Fix typos instead of copying them from `_old/` (`Scritps`, `Animaiton`, `Ynadex`).
- Keep comments short and only where the reason is not obvious.
