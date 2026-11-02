using System;

/// <summary>
/// Model: the numbers that define one round. Part of a level; read-only at runtime.
/// </summary>
[Serializable]
public struct GameRules
{
    public const int MAX_PLAYERS_LIMIT = 16;

    public float timeLimitSeconds;
    public int itemsRequired;
    public int startingLives;
    public float countdownSeconds;
    public int minPlayers;
    public int maxPlayers;

    public static GameRules Default => new GameRules
    {
        timeLimitSeconds = 120.0f,
        itemsRequired = 5,
        startingLives = 1,
        countdownSeconds = 3.0f,
        minPlayers = 1,
        maxPlayers = 8
    };

    // Rules can come from an asset or from the network: never trust them as they are.
    public GameRules Clamped()
    {
        GameRules rules = this;

        rules.timeLimitSeconds = Math.Max(1.0f, timeLimitSeconds);
        rules.itemsRequired = Math.Max(0, itemsRequired);
        rules.startingLives = Math.Max(1, startingLives);
        rules.countdownSeconds = Math.Max(0.0f, countdownSeconds);
        rules.maxPlayers = Math.Min(Math.Max(1, maxPlayers), MAX_PLAYERS_LIMIT);
        rules.minPlayers = Math.Min(Math.Max(1, minPlayers), rules.maxPlayers);

        return rules;
    }
}
