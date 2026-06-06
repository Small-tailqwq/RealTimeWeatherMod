using ChillWithYou.EnvSync.Core;
using Xunit;

namespace RealTimeWeatherMod.Tests.Core
{
    public class StartupWeatherSyncPolicyTests
    {
        [Fact]
        public void Determine_NoEnvironmentControl_SkipsStartupApply()
        {
            var action = StartupWeatherSyncPolicy.Determine(
                canMutateEnvironment: false,
                runtimeReady: true,
                needsWeatherData: true,
                weatherDecisionReady: true,
                needsSceneryControllers: true,
                sceneryControllersReady: true);

            Assert.Equal(StartupWeatherSyncAction.Skip, action);
        }

        [Fact]
        public void Determine_RuntimeNotReady_WaitsForRuntime()
        {
            var action = StartupWeatherSyncPolicy.Determine(
                canMutateEnvironment: true,
                runtimeReady: false,
                needsWeatherData: true,
                weatherDecisionReady: true,
                needsSceneryControllers: true,
                sceneryControllersReady: false);

            Assert.Equal(StartupWeatherSyncAction.WaitForRuntime, action);
        }

        [Fact]
        public void Determine_WeatherDataNotReady_WaitsForWeather()
        {
            var action = StartupWeatherSyncPolicy.Determine(
                canMutateEnvironment: true,
                runtimeReady: true,
                needsWeatherData: true,
                weatherDecisionReady: false,
                needsSceneryControllers: true,
                sceneryControllersReady: false);

            Assert.Equal(StartupWeatherSyncAction.WaitForWeather, action);
        }

        [Fact]
        public void Determine_WeatherControlBeforeControllersSetup_AppliesAndKeepsSettling()
        {
            var action = StartupWeatherSyncPolicy.Determine(
                canMutateEnvironment: true,
                runtimeReady: true,
                needsWeatherData: true,
                weatherDecisionReady: true,
                needsSceneryControllers: true,
                sceneryControllersReady: false);

            Assert.Equal(StartupWeatherSyncAction.ApplyAndKeepSettling, action);
        }

        [Fact]
        public void Determine_WeatherControlAfterControllersSetup_AppliesAndFinishes()
        {
            var action = StartupWeatherSyncPolicy.Determine(
                canMutateEnvironment: true,
                runtimeReady: true,
                needsWeatherData: true,
                weatherDecisionReady: true,
                needsSceneryControllers: true,
                sceneryControllersReady: true);

            Assert.Equal(StartupWeatherSyncAction.ApplyAndFinish, action);
        }

        [Fact]
        public void Determine_TimeOnly_DoesNotWaitForSceneryControllers()
        {
            var action = StartupWeatherSyncPolicy.Determine(
                canMutateEnvironment: true,
                runtimeReady: true,
                needsWeatherData: false,
                weatherDecisionReady: false,
                needsSceneryControllers: false,
                sceneryControllersReady: false);

            Assert.Equal(StartupWeatherSyncAction.ApplyAndFinish, action);
        }

        [Fact]
        public void Determine_RuntimeStillMissingWithoutWeather_NeedsRuntimeFirst()
        {
            var action = StartupWeatherSyncPolicy.Determine(
                canMutateEnvironment: true,
                runtimeReady: false,
                needsWeatherData: false,
                weatherDecisionReady: false,
                needsSceneryControllers: false,
                sceneryControllersReady: false);

            Assert.Equal(StartupWeatherSyncAction.WaitForRuntime, action);
        }

        [Fact]
        public void Determine_WeatherDecisionReadyWithoutSceneryDependency_FinishesImmediately()
        {
            var action = StartupWeatherSyncPolicy.Determine(
                canMutateEnvironment: true,
                runtimeReady: true,
                needsWeatherData: true,
                weatherDecisionReady: true,
                needsSceneryControllers: false,
                sceneryControllersReady: false);

            Assert.Equal(StartupWeatherSyncAction.ApplyAndFinish, action);
        }
    }
}
