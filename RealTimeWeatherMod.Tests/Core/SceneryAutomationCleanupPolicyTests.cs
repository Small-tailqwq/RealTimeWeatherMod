using ChillWithYou.EnvSync.Core;
using Xunit;

namespace RealTimeWeatherMod.Tests.Core
{
    public class SceneryAutomationCleanupPolicyTests
    {
        [Fact]
        public void ShouldDisableManagedMod_WhenRuleDisabled_ReturnsTrue()
        {
            Assert.True(SceneryAutomationCleanupPolicy.ShouldDisableManagedMod(
                isRuleEnabled: false,
                isConditionSatisfied: true));
        }

        [Fact]
        public void ShouldDisableManagedMod_WhenConditionNoLongerSatisfied_ReturnsTrue()
        {
            Assert.True(SceneryAutomationCleanupPolicy.ShouldDisableManagedMod(
                isRuleEnabled: true,
                isConditionSatisfied: false));
        }

        [Fact]
        public void ShouldDisableManagedMod_WhenRuleStillValid_ReturnsFalse()
        {
            Assert.False(SceneryAutomationCleanupPolicy.ShouldDisableManagedMod(
                isRuleEnabled: true,
                isConditionSatisfied: true));
        }
    }
}
