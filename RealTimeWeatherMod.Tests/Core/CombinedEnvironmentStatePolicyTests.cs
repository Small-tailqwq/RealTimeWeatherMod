using ChillWithYou.EnvSync.Core;
using Xunit;

namespace RealTimeWeatherMod.Tests.Core
{
    public class CombinedEnvironmentStatePolicyTests
    {
        [Fact]
        public void Closing_WhenOnlySoundRemains_ChangesOnlySound()
        {
            Assert.False(CombinedEnvironmentStatePolicy.NeedsWindowChange(
                windowActive: false,
                targetState: false));
            Assert.True(CombinedEnvironmentStatePolicy.NeedsSoundChange(
                hasSound: true,
                soundActive: true,
                liveSoundActive: true,
                targetState: false));
        }

        [Fact]
        public void Closing_WhenOnlyWindowRemains_DoesNotMuteAgain()
        {
            Assert.True(CombinedEnvironmentStatePolicy.NeedsWindowChange(
                windowActive: true,
                targetState: false));
            Assert.False(CombinedEnvironmentStatePolicy.NeedsSoundChange(
                hasSound: true,
                soundActive: false,
                liveSoundActive: false,
                targetState: false));
        }

        [Fact]
        public void Opening_PreservesIntentionallyZeroLiveVolume()
        {
            Assert.True(CombinedEnvironmentStatePolicy.IsInTargetState(
                windowActive: true,
                hasSound: true,
                soundActive: true,
                liveSoundActive: false,
                targetState: true));
            Assert.False(CombinedEnvironmentStatePolicy.NeedsSoundChange(
                hasSound: true,
                soundActive: true,
                liveSoundActive: false,
                targetState: true));
        }

        [Fact]
        public void ViewOnlyEnvironment_DoesNotRequireSound()
        {
            Assert.True(CombinedEnvironmentStatePolicy.IsInTargetState(
                windowActive: true,
                hasSound: false,
                soundActive: false,
                liveSoundActive: false,
                targetState: true));
            Assert.False(CombinedEnvironmentStatePolicy.NeedsSoundChange(
                hasSound: false,
                soundActive: false,
                liveSoundActive: false,
                targetState: true));
        }

        [Fact]
        public void Closing_WhenEverythingOff_IsInTargetState()
        {
            Assert.True(CombinedEnvironmentStatePolicy.IsInTargetState(
                windowActive: false,
                hasSound: false,
                soundActive: false,
                liveSoundActive: false,
                targetState: false));
        }

        [Fact]
        public void Opening_WhenWindowOff_NeedsWindowChange()
        {
            Assert.True(CombinedEnvironmentStatePolicy.NeedsWindowChange(
                windowActive: false,
                targetState: true));
            Assert.False(CombinedEnvironmentStatePolicy.NeedsWindowChange(
                windowActive: true,
                targetState: true));
        }

        [Fact]
        public void Opening_WhenSoundOff_NeedsSoundChange()
        {
            Assert.True(CombinedEnvironmentStatePolicy.NeedsSoundChange(
                hasSound: true,
                soundActive: false,
                liveSoundActive: false,
                targetState: true));
        }
    }
}
