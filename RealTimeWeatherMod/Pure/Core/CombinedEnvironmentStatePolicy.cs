namespace ChillWithYou.EnvSync.Core
{
    internal static class CombinedEnvironmentStatePolicy
    {
        internal static bool IsInTargetState(
            bool windowActive,
            bool hasSound,
            bool soundActive,
            bool liveSoundActive,
            bool targetState,
            bool? targetSoundState = null)
        {
            // Omitted sound targets retain the original coupled view/sound behavior.
            return !NeedsWindowChange(windowActive, targetState) &&
                !NeedsSoundChange(hasSound, soundActive, liveSoundActive, targetSoundState ?? targetState);
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
