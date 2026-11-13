using NUnit.Framework;

public class MatchMessagesTests
{
    [TestCase(MatchErrors.RoomFull)]
    [TestCase(MatchErrors.NoSuchRoom)]
    [TestCase(MatchErrors.Network)]
    [TestCase("version_mismatch")]
    [TestCase("room_closed")]
    public void KnownCodes_GiveText(string code)
    {
        Assert.IsNotEmpty(MatchMessages.Describe(code));
    }

    [Test]
    public void LeavingIsNotAnError()
    {
        Assert.IsEmpty(MatchMessages.Describe("left"));
    }

    [Test]
    public void UnknownCode_IsShownAsIs()
    {
        Assert.AreEqual("weird", MatchMessages.Describe("weird"));
        Assert.IsEmpty(MatchMessages.Describe(null));
    }
}
