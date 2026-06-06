namespace ChillWithYou.EnvSync.Core
{
    internal enum StartupWeatherSyncAction
    {
        Skip,
        WaitForRuntime,
        WaitForWeather,
        ApplyAndKeepSettling,
        ApplyAndFinish
    }

    internal static class StartupWeatherSyncPolicy
    {
        internal static StartupWeatherSyncAction Determine(
            bool canMutateEnvironment,
            bool runtimeReady,
            bool needsWeatherData,
            bool weatherDecisionReady,
            bool needsSceneryControllers,
            bool sceneryControllersReady)
        {
            if (!canMutateEnvironment)
            {
                return StartupWeatherSyncAction.Skip;
            }

            if (!runtimeReady)
            {
                return StartupWeatherSyncAction.WaitForRuntime;
            }

            if (needsWeatherData && !weatherDecisionReady)
            {
                return StartupWeatherSyncAction.WaitForWeather;
            }

            if (needsSceneryControllers && !sceneryControllersReady)
            {
                return StartupWeatherSyncAction.ApplyAndKeepSettling;
            }

            return StartupWeatherSyncAction.ApplyAndFinish;
        }
    }
}
