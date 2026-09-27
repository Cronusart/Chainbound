/// <summary>
/// Stable IDs for Chainling PC terminals and the Mart leftover unlock flag.
/// </summary>
public static class ChainlingPcTerminalIds
{
    public const string PlayerRoomTerminalId = "player_room_pc";
    public const string UnlockFlagKey = "world.player.chainling_pc";
    public const string LegacyUnlockFlagKey = "world.loomville_mart.open";
    public const string PlayerRoomMonitorName = "Monitor";
    public const string PlayerRoomPcObjectName = "Monitor_PcTerminal";
    public const string IntroGateName = "Monitor_IntroGate";
    public const string PcGateName = "Monitor_PcGate";

    public static bool IsUnlocked(ProfileWorldFlagStateService flags)
    {
        if (flags == null || !flags.IsHydrated)
            return false;

        return flags.GetFlag(UnlockFlagKey) || flags.GetFlag(LegacyUnlockFlagKey);
    }
}
