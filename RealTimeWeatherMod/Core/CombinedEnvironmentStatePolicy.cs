namespace ChillWithYou.EnvSync.Core
{
    internal static class CombinedEnvironmentStatePolicy
    {
        internal static bool IsInTargetState(
            bool windowActive,
            bool hasSound,
            bool soundActive,
            bool liveSoundActive,
            bool targetState)
        {
            if (targetState)
            {
                return windowActive && (!hasSound || soundActive);
            }

            return !windowActive && (!hasSound || (!soundActive && !liveSoundActive));
        }

        internal static bool NeedsWindowChange(bool windowActive, bool targetState)
        {
            return windowActive != targetState;
        }

        internal static bool NeedsSoundChange(
            bool hasSound,
            bool soundActive,
            bool liveSoundActive,
            bool targetState)
        {
            return hasSound && (targetState
                ? !soundActive
                : soundActive || liveSoundActive);
        }
    }
}
