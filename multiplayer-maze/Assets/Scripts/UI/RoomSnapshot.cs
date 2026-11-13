using System.Collections.Generic;

/// <summary>
/// Model (view data): what the room screen shows right now. The controller fills it from the
/// replicated game state; the View only reads it.
/// </summary>
public sealed class RoomSnapshot
{
    public GamePhase Phase;
    public FinishReason Reason;
    public int Round;
    public int Countdown;
    public int TimeLeft;
    public int Items;
    public int ItemsRequired;
    public bool IsOwner;
    public bool MyReady;
    public readonly List<PlayerLine> Players = new List<PlayerLine>();

    public struct PlayerLine
    {
        public ulong Id;
        public string Name;
        public bool IsOwner;
        public bool IsMe;
        public bool Ready;
        public int Items;
    }
}
