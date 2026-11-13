using System.Collections.Generic;

// Error codes. The first four are the lobby's own codes; the rest come from the client side.
public static class MatchErrors
{
    public const string NoSuchRoom = "no_such_room";
    public const string RoomFull = "room_full";
    public const string RoomFailedToStart = "room_failed_to_start";
    public const string NoCapacity = "no_capacity";
    public const string InvalidRequest = "invalid_request";
    public const string RateLimited = "rate_limited";
    public const string Network = "network_error";
}

public sealed class MatchResult
{
    public bool Ok { get; }
    public string Error { get; }
    public RoomInfo Room { get; }
    public RoomTicket Ticket { get; }

    private MatchResult(bool ok, string error, RoomInfo room, RoomTicket ticket)
    {
        Ok = ok;
        Error = error;
        Room = room;
        Ticket = ticket;
    }

    public static MatchResult Success(RoomInfo room, RoomTicket ticket)
        => new MatchResult(true, null, room, ticket);

    public static MatchResult Failure(string error)
        => new MatchResult(false, error, null, default);
}

public sealed class RoomListResult
{
    public bool Ok { get; }
    public string Error { get; }
    public IReadOnlyList<RoomInfo> Rooms { get; }

    private RoomListResult(bool ok, string error, IReadOnlyList<RoomInfo> rooms)
    {
        Ok = ok;
        Error = error;
        Rooms = rooms;
    }

    public static RoomListResult Success(IReadOnlyList<RoomInfo> rooms)
        => new RoomListResult(true, null, rooms);

    public static RoomListResult Failure(string error)
        => new RoomListResult(false, error, new RoomInfo[0]);
}
