namespace ChillWithYou.EnvSync.Core
{
    internal sealed class SyncPolicySnapshot
    {
        public bool CanControlTime { get; private set; }
        public bool CanControlWeather { get; private set; }
        public bool CanApplyCloudyOverride { get; private set; }
        public bool NeedWeatherDataForUI { get; private set; }
        public bool CanFetchWeatherForUI { get; private set; }
        public bool CanMutateEnvironment { get; private set; }

        public SyncPolicySnapshot(
            bool canControlTime,
            bool canControlWeather,
            bool canApplyCloudyOverride,
            bool needWeatherDataForUI,
            bool canFetchWeatherForUI,
            bool canMutateEnvironment)
        {
            CanControlTime = canControlTime;
            CanControlWeather = canControlWeather;
            CanApplyCloudyOverride = canApplyCloudyOverride;
            NeedWeatherDataForUI = needWeatherDataForUI;
            CanFetchWeatherForUI = canFetchWeatherForUI;
            CanMutateEnvironment = canMutateEnvironment;
        }
    }

    internal static class SyncPolicy
    {
        internal static SyncPolicySnapshot Build(bool enableTimeSync, bool enableWeatherSync, bool showWeatherOnDate)
        {
            bool canControlTime = enableTimeSync;
            bool canControlWeather = enableWeatherSync;
            bool canApplyCloudyOverride = enableTimeSync && enableWeatherSync;
            bool needWeatherDataForUI = showWeatherOnDate;
            bool canFetchWeatherForUI = showWeatherOnDate && !enableWeatherSync;
            bool canMutateEnvironment = enableTimeSync || enableWeatherSync;

            return new SyncPolicySnapshot(
                canControlTime,
                canControlWeather,
                canApplyCloudyOverride,
                needWeatherDataForUI,
                canFetchWeatherForUI,
                canMutateEnvironment);
        }
    }
}
