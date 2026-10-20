using ChillWithYou.EnvSync.Core;
using Xunit;

namespace RealTimeWeatherMod.Tests.Core
{
    public class CombinedEnvironmentStatePolicyTests
    {
        [Theory]
        [InlineData(false, false, false, true, false)]
        [InlineData(false, true, true, true, true)]
        [InlineData(true, true, true, false, true)]
        [InlineData(true, false, true, false, true)]
        [InlineData(true, true, false, false, true)]
        [InlineData(true, false, false, false, false)]
        public void SilentRain_OpensOrKeepsWindow_AndMutesStoredOrLiveSound(
            bool windowActive, bool soundActive, bool liveSoundActive,
            bool changeWindow, bool changeSound)
        {
            Assert.Equal(changeWindow, CombinedEnvironmentStatePolicy.NeedsWindowChange(windowActive, true));
            Assert.Equal(changeSound, CombinedEnvironmentStatePolicy.NeedsSoundChange(
                true, soundActive, liveSoundActive, false));
            Assert.Equal(!changeWindow && !changeSound, CombinedEnvironmentStatePolicy.IsInTargetState(
                windowActive, true, soundActive, liveSoundActive, true, targetSoundState: false));
        }

        [Fact]
        public void SilentRain_Stopping_ClosesWindowWithoutMutingAgain()
        {
            Assert.True(CombinedEnvironmentStatePolicy.NeedsWindowChange(true, false));
            Assert.False(CombinedEnvironmentStatePolicy.NeedsSoundChange(true, false, false, false));
            Assert.False(CombinedEnvironmentStatePolicy.IsInTargetState(
                true, true, false, false, false, targetSoundState: false));
            Assert.True(CombinedEnvironmentStatePolicy.IsInTargetState(
                false, true, false, false, false, targetSoundState: false));
        }

        [Fact]
        public void EnablingRainSounds_WhenWindowAlreadyOpen_ChangesOnlySound()
        {
            Assert.False(CombinedEnvironmentStatePolicy.NeedsWindowChange(true, true));
            Assert.True(CombinedEnvironmentStatePolicy.NeedsSoundChange(true, false, false, true));
            Assert.False(CombinedEnvironmentStatePolicy.IsInTargetState(
                true, true, false, false, true, targetSoundState: true));
            Assert.True(CombinedEnvironmentStatePolicy.IsInTargetState(
                true, true, true, true, true, targetSoundState: true));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ViewOnlyScenery_DoesNotRequireSound_WithEitherSoundTarget(bool soundTarget)
        {
            Assert.True(CombinedEnvironmentStatePolicy.IsInTargetState(
                true, false, false, false, true, targetSoundState: soundTarget));
            Assert.False(CombinedEnvironmentStatePolicy.NeedsSoundChange(false, false, false, soundTarget));
        }

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
