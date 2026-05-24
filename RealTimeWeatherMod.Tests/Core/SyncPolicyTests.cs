using ChillWithYou.EnvSync.Core;
using Xunit;

namespace RealTimeWeatherMod.Tests.Core
{
    public class SyncPolicyTests
    {
        [Fact]
        public void Build_WeatherOff_TimeOn_OnlyControlsTime()
        {
            var policy = SyncPolicy.Build(enableTimeSync: true, enableWeatherSync: false, showWeatherOnDate: true);

            Assert.True(policy.CanControlTime);
            Assert.False(policy.CanControlWeather);
            Assert.False(policy.CanApplyCloudyOverride);
            Assert.True(policy.NeedWeatherDataForUI);
            Assert.True(policy.CanFetchWeatherForUI);
            Assert.True(policy.CanMutateEnvironment);
        }

        [Fact]
        public void Build_WeatherOff_TimeOff_DoesNotMutateEnvironment()
        {
            var policy = SyncPolicy.Build(enableTimeSync: false, enableWeatherSync: false, showWeatherOnDate: true);

            Assert.False(policy.CanControlTime);
            Assert.False(policy.CanControlWeather);
            Assert.False(policy.CanApplyCloudyOverride);
            Assert.True(policy.NeedWeatherDataForUI);
            Assert.True(policy.CanFetchWeatherForUI);
            Assert.False(policy.CanMutateEnvironment);
        }

        [Fact]
        public void Build_WeatherOn_TimeOff_DisablesCloudyOverride()
        {
            var policy = SyncPolicy.Build(enableTimeSync: false, enableWeatherSync: true, showWeatherOnDate: false);

            Assert.False(policy.CanControlTime);
            Assert.True(policy.CanControlWeather);
            Assert.False(policy.CanApplyCloudyOverride);
            Assert.False(policy.NeedWeatherDataForUI);
            Assert.False(policy.CanFetchWeatherForUI);
            Assert.True(policy.CanMutateEnvironment);
        }

        [Fact]
        public void Build_WeatherOn_TimeOn_EnablesCloudyOverride()
        {
            var policy = SyncPolicy.Build(enableTimeSync: true, enableWeatherSync: true, showWeatherOnDate: true);

            Assert.True(policy.CanControlTime);
            Assert.True(policy.CanControlWeather);
            Assert.True(policy.CanApplyCloudyOverride);
            Assert.True(policy.NeedWeatherDataForUI);
            Assert.False(policy.CanFetchWeatherForUI);
            Assert.True(policy.CanMutateEnvironment);
        }
    }
}
