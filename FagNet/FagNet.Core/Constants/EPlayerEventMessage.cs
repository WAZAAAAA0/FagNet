namespace FagNet.Core.Constants
{
    public enum EPlayerEventMessage
    {
        ChangedTeamTo = 1,
        EnteredRoom = 2,
        LeftRoom = 3,
        Kicked = 4,
        MasterAFK = 5,
        AFK = 6,
        KickedByModerator = 7,
        BallReset = 8,
        StartGame = 9,
        TouchdownAlpha = 10,
        TouchdownBeta = 11,
        ChatMessage = 13,
        TeamMessage = 14,
        ResetRound = 15,
        HalfTimeIn = 18,
        RespawnIn = 21,
        GodmodeForXSeconds = 22,
        CantStartGame = 24,
    }
}
