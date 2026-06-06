namespace ChillWithYou.EnvSync.Core
{
    internal static class SceneryAutomationCleanupPolicy
    {
        internal static bool ShouldDisableManagedMod(bool isRuleEnabled, bool isConditionSatisfied)
        {
            return !isRuleEnabled || !isConditionSatisfied;
        }
    }
}
