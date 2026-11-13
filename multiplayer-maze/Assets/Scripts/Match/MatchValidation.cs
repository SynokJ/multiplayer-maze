/// <summary>
/// Mirrors the lobby's input rules (ASCII only: the lobby uses the JavaScript \w).
/// Checked on the client for fast feedback; the lobby still validates everything itself.
/// </summary>
public static class MatchValidation
{
    public const int MAX_NAME_LENGTH = 24;
    public const int ROOM_ID_LENGTH = 5;

    public static bool IsValidName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MAX_NAME_LENGTH)
            return false;

        foreach (char c in name)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                      || c == '_' || c == ' ' || c == '.' || c == '-';
            if (!ok)
                return false;
        }

        return true;
    }

    // Room codes are 5 characters, A-Z and 0-9. Returns the upper-case code or null.
    public static string NormalizeRoomId(string roomId)
    {
        if (string.IsNullOrEmpty(roomId))
            return null;

        string code = roomId.Trim().ToUpperInvariant();
        if (code.Length != ROOM_ID_LENGTH)
            return null;

        foreach (char c in code)
            if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                return null;

        return code;
    }
}
