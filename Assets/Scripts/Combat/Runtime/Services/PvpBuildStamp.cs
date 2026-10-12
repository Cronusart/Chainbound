namespace Chainbound.Combat
{
    /// <summary>
    /// Pairing stamp shared by the Editor and device builds.
    /// Both read this committed constant. Application.buildGUID is a new value on every player build.
    /// </summary>
    public static class PvpBuildStamp
    {
        public const string Value = "9a0fa3f5c1246f460338afe07540a214047428b05e61a09629252b98b8bf2e8f";

        public static string Current => Value;
    }
}
