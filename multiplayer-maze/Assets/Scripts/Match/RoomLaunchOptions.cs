using System;

/// <summary>
/// Settings the lobby gives a room server process: command-line flags for public values
/// (-port, -roomId, -maxPlayers, -level) and environment variables for secrets
/// (ROOM_SECRET, LOBBY_URL, LOBBY_INTERNAL_KEY), because arguments are visible in `ps`.
/// </summary>
public sealed class RoomLaunchOptions
{
    public ushort Port { get; private set; } = 7777;
    public string RoomId { get; private set; } = "DEV00";
    public int MaxPlayers { get; private set; } = 8;
    public int LevelIndex { get; private set; }

    public string Secret { get; private set; } = "dev-secret-dev-secret-dev-secret!";
    public string LobbyUrl { get; private set; }
    public string LobbyKey { get; private set; }

    public static RoomLaunchOptions Parse(string[] args, Func<string, string> getEnvironment)
    {
        var options = new RoomLaunchOptions();

        string port = ArgumentValue(args, "-port");
        if (ushort.TryParse(port, out ushort parsedPort) && parsedPort > 0)
            options.Port = parsedPort;

        string roomId = ArgumentValue(args, "-roomId");
        if (!string.IsNullOrEmpty(roomId))
            options.RoomId = roomId;

        if (int.TryParse(ArgumentValue(args, "-maxPlayers"), out int maxPlayers) && maxPlayers > 0)
            options.MaxPlayers = Math.Min(maxPlayers, GameRules.MAX_PLAYERS_LIMIT);

        if (int.TryParse(ArgumentValue(args, "-level"), out int level) && level >= 0)
            options.LevelIndex = level;

        string secret = getEnvironment?.Invoke("ROOM_SECRET");
        if (!string.IsNullOrEmpty(secret))
            options.Secret = secret;

        options.LobbyUrl = Normalize(getEnvironment?.Invoke("LOBBY_URL"));
        options.LobbyKey = Normalize(getEnvironment?.Invoke("LOBBY_INTERNAL_KEY"));
        return options;
    }

    private static string ArgumentValue(string[] args, string name)
    {
        if (args == null)
            return null;

        for (int i = 0; i < args.Length - 1; ++i)
            if (args[i] == name)
                return args[i + 1];

        return null;
    }

    private static string Normalize(string value) => string.IsNullOrEmpty(value) ? null : value;
}
