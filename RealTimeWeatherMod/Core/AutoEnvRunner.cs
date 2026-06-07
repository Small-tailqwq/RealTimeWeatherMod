using System;
using Bulbul;
using HarmonyLib;
using UnityEngine;
using ChillWithYou.EnvSync.Models;
using ChillWithYou.EnvSync.Services;
using ChillWithYou.EnvSync.Utils;

namespace ChillWithYou.EnvSync.Core
{
    public partial class AutoEnvRunner : MonoBehaviour
    {
        private float _nextWeatherCheckTime;
        private float _nextTimeCheckTime;
        private EnvironmentType? _lastAppliedEnv;
        private bool _isFetching;
        private bool _pendingForceRefresh;

        private static AutoEnvRunner _instance;

        private static readonly EnvironmentType[] BaseEnvironments = new[] { EnvironmentType.Day, EnvironmentType.Sunset, EnvironmentType.Night, EnvironmentType.Cloudy };
        private static readonly EnvironmentType[] SceneryWeathers = new[] { EnvironmentType.ThunderRain, EnvironmentType.HeavyRain, EnvironmentType.LightRain, EnvironmentType.Snow };
        private static readonly EnvironmentType[] MainEnvironments = new[] { EnvironmentType.Day, EnvironmentType.Sunset, EnvironmentType.Night, EnvironmentType.Cloudy, EnvironmentType.LightRain, EnvironmentType.HeavyRain, EnvironmentType.ThunderRain, EnvironmentType.Snow };

        private void Start()
        {
            _instance = this;
            _nextWeatherCheckTime = Time.time + 10f;
            _nextTimeCheckTime = Time.time + 10f;
            ChillEnvPlugin.Log?.LogInfo("Runner 启动...");

            CheckAndSyncSunSchedule();
            StartCoroutine(EarlyStartupSync());
        }

        private SyncPolicySnapshot BuildPolicySnapshot()
        {
            return SyncPolicy.Build(
                ChillEnvPlugin.Cfg_EnableTimeSync.Value,
                ChillEnvPlugin.Cfg_EnableWeatherSync.Value,
                ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value);
        }

        private bool HasUsableApiKey()
        {
            string apiKey = ChillEnvPlugin.Cfg_SeniverseKey.Value;
            return !string.IsNullOrEmpty(apiKey) || WeatherService.HasDefaultKey;
        }

        private void UpdateUiWeatherString(WeatherInfo weather)
        {
            ChillEnvPlugin.UIWeatherString = WeatherUiState.NextWeatherText(
                ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value,
                ChillEnvPlugin.UIWeatherString,
                weather);
        }

        private EnvironmentType GetBaseTimeEnvironmentOnly()
        {
            return GetTimeBasedEnvironment();
        }

        private float GetConfiguredWeatherRefreshSeconds()
        {
            return Mathf.Max(1, ChillEnvPlugin.Cfg_WeatherRefreshMinutes.Value) * 60f;
        }

        private void ScheduleDefaultWeatherCheck()
        {
            _nextWeatherCheckTime = Time.time + GetConfiguredWeatherRefreshSeconds();
        }

        private void ScheduleNextWeatherCheckFromCache(string location)
        {
            float remainingSeconds;
            if (WeatherService.TryGetCacheRemainingSeconds(location, out remainingSeconds))
            {
                _nextWeatherCheckTime = Time.time + Mathf.Max(5f, remainingSeconds + 1f);
                return;
            }

            ScheduleDefaultWeatherCheck();
        }

        private void CheckAndSyncSunSchedule()
        {
            if (!ChillEnvPlugin.Cfg_EnableWeatherSync.Value && !ChillEnvPlugin.Cfg_EnableTimeSync.Value)
            {
                return;
            }

            if (!HasUsableApiKey())
            {
                return;
            }

            string lastSync = ChillEnvPlugin.Cfg_LastSunSyncDate.Value;
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            if (lastSync != today)
            {
                StartCoroutine(SyncSunScheduleRoutine(today));
            }
        }

        private System.Collections.IEnumerator SyncSunScheduleRoutine(string targetDate)
        {
            int retryCount = 0;
            float delay = 1f;
            const int MaxRetries = 10;

            while (retryCount < MaxRetries)
            {
                bool success = false;
                string apiKey = ChillEnvPlugin.Cfg_SeniverseKey.Value;
                string location = ChillEnvPlugin.Cfg_Location.Value;

                yield return WeatherService.FetchSunSchedule(apiKey, location, (data) =>
                {
                    if (data != null)
                    {
                        ChillEnvPlugin.Log?.LogInfo($"[SunSync] 同步成功: 日出{data.sunrise} 日落{data.sunset}");

                        ChillEnvPlugin.Cfg_SunriseTime.Value = data.sunrise;
                        ChillEnvPlugin.Cfg_SunsetTime.Value = data.sunset;
                        ChillEnvPlugin.Cfg_LastSunSyncDate.Value = targetDate;

                        ChillEnvPlugin.Instance.Config.Save();
                        success = true;
                    }
                });

                if (success)
                {
                    yield break;
                }

                ChillEnvPlugin.Log?.LogWarning($"[SunSync] 同步失败，{delay}秒后重试 ({retryCount + 1}/{MaxRetries})");
                yield return new WaitForSeconds(delay);

                delay *= 2f;
                retryCount++;
            }

            ChillEnvPlugin.Log?.LogError("[SunSync] 达到最大重试次数，今日放弃同步");
        }

        private void Update()
        {
            if (!ChillEnvPlugin.Initialized || EnvRegistry.Count == 0)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                ChillEnvPlugin.Log?.LogInfo("F9: 强制同步");
                TriggerSync(false, true);
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                ShowStatus();
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                ChillEnvPlugin.Log?.LogInfo("F7: 强制刷新");
                ChillEnvPlugin.Instance.Config.Reload();
                ForceRefreshWeather();
            }

            if (Time.time >= _nextTimeCheckTime)
            {
                _nextTimeCheckTime = Time.time + 30f;
                TriggerSync(false, false);
            }

            if (Time.time >= _nextWeatherCheckTime)
            {
                TriggerSync(false, false);
            }
        }

        private void ShowStatus()
        {
            var now = DateTime.Now;
            ChillEnvPlugin.Log?.LogInfo($"--- 状态 [{now:HH:mm:ss}] ---");
            ChillEnvPlugin.Log?.LogInfo($"插件记录: {_lastAppliedEnv}");
            var currentActive = GetCurrentActiveEnvironment();
            ChillEnvPlugin.Log?.LogInfo($"游戏实际: {currentActive}");
            ChillEnvPlugin.Log?.LogInfo($"UI文本: {ChillEnvPlugin.UIWeatherString}");
            if (ChillEnvPlugin.Cfg_DebugMode.Value)
            {
                ChillEnvPlugin.Log?.LogWarning("【警告】调试模式已开启！");
            }
        }

        private void ForceRefreshWeather()
        {
            if (_isFetching)
            {
                _pendingForceRefresh = true;
                ChillEnvPlugin.Log?.LogInfo("ForceRefresh requested while fetching; queued until current fetch completes");
                return;
            }

            ScheduleDefaultWeatherCheck();
            TriggerSync(true, false);
        }

        /// <summary>
        /// 公开方法：立即强制刷新天气（供外部调用）
        /// </summary>
        public static void TriggerWeatherRefresh()
        {
            if (_instance != null)
            {
                ChillEnvPlugin.Log?.LogInfo("[外部触发] 请求天气刷新");
                _instance.ForceRefreshWeather();
            }
        }

        /// <summary>
        /// 公开方法：立即刷新 UI 天气文本（优先复用有效缓存）
        /// </summary>
        public static void TriggerUiWeatherRefresh()
        {
            if (_instance != null)
            {
                ChillEnvPlugin.Log?.LogInfo("[外部触发] 请求 UI 天气刷新");
                _instance.TriggerSync(false, false);
            }
        }

        /// <summary>
        /// 公开方法：立即刷新日出日落数据（供外部调用，如城市修改时）
        /// </summary>
        public static void TriggerSunScheduleRefresh()
        {
            if (_instance != null)
            {
                ChillEnvPlugin.Log?.LogInfo("[外部触发] 请求日出日落刷新");
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                _instance.StartCoroutine(_instance.SyncSunScheduleRoutine(today));
            }
        }

        private void TriggerSync(bool forceApi, bool forceApply)
        {
            if (ChillEnvPlugin.Cfg_DebugMode.Value)
            {
                ChillEnvPlugin.Log?.LogInfo($"TriggerSync called (forceApi={forceApi}, forceApply={forceApply})");
            }

            if (ChillEnvPlugin.IsInCutscene())
            {
                _nextTimeCheckTime = Time.time + 30f;
                _nextWeatherCheckTime = Time.time + 30f;
                return;
            }

            var policy = BuildPolicySnapshot();
            bool hasApiKey = HasUsableApiKey();
            string location = ChillEnvPlugin.Cfg_Location.Value;
            bool hasValidWeatherCache = WeatherService.HasValidCache(location);

            if (!ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value)
            {
                ChillEnvPlugin.UIWeatherString = string.Empty;
            }

            if (ChillEnvPlugin.Cfg_DebugMode.Value)
            {
                int mockCode = ChillEnvPlugin.Cfg_DebugCode.Value;
                var mockWeather = new WeatherInfo
                {
                    Code = mockCode,
                    Temperature = ChillEnvPlugin.Cfg_DebugTemp.Value,
                    Text = ChillEnvPlugin.Cfg_DebugText.Value,
                    Condition = WeatherService.MapCodeToCondition(mockCode),
                    UpdateTime = DateTime.Now
                };

                UpdateUiWeatherString(mockWeather);
                if (policy.CanMutateEnvironment)
                {
                    ApplyByPolicy(policy, mockWeather, forceApply);
                }

                ScheduleDefaultWeatherCheck();
                return;
            }

            if (!policy.CanMutateEnvironment)
            {
                if (policy.CanFetchWeatherForUI && hasApiKey)
                {
                    if (!forceApi && hasValidWeatherCache)
                    {
                        UpdateUiWeatherString(WeatherService.CachedWeather);
                        ScheduleNextWeatherCheckFromCache(location);
                        return;
                    }

                    if (_isFetching)
                    {
                        if (forceApi)
                        {
                            _pendingForceRefresh = true;
                        }

                        ScheduleDefaultWeatherCheck();
                        return;
                    }

                    _isFetching = true;
                    StartCoroutine(WeatherService.FetchWeather(
                        ChillEnvPlugin.Cfg_SeniverseKey.Value,
                        location,
                        forceApi,
                        (weather) =>
                        {
                            _isFetching = false;
                            UpdateUiWeatherString(weather);
                            if (weather != null)
                            {
                                ScheduleNextWeatherCheckFromCache(location);
                            }
                            else
                            {
                                ScheduleDefaultWeatherCheck();
                            }

                            HandlePendingForceRefresh();
                        }));
                }
                else if (policy.NeedWeatherDataForUI && hasValidWeatherCache)
                {
                    UpdateUiWeatherString(WeatherService.CachedWeather);
                    ScheduleNextWeatherCheckFromCache(location);
                }
                else
                {
                    ScheduleDefaultWeatherCheck();
                }

                return;
            }

            bool shouldFetchWeather = (policy.CanControlWeather || policy.CanFetchWeatherForUI) && hasApiKey;
            bool needFetchWeather = shouldFetchWeather && (forceApi || !hasValidWeatherCache);

            if (needFetchWeather)
            {
                if (_isFetching)
                {
                    if (forceApi)
                    {
                        _pendingForceRefresh = true;
                    }

                    ChillEnvPlugin.Log?.LogWarning("TriggerSync aborted: fetch already in progress");
                    ScheduleDefaultWeatherCheck();
                    return;
                }

                _isFetching = true;
                StartCoroutine(WeatherService.FetchWeather(
                    ChillEnvPlugin.Cfg_SeniverseKey.Value,
                    location,
                    forceApi,
                    (weather) =>
                    {
                        _isFetching = false;
                        UpdateUiWeatherString(weather);
                        ApplyByPolicy(policy, weather, forceApply);
                        if (weather != null)
                        {
                            ScheduleNextWeatherCheckFromCache(location);
                        }
                        else
                        {
                            ScheduleDefaultWeatherCheck();
                        }

                        HandlePendingForceRefresh();
                    }));
                return;
            }

            if (policy.NeedWeatherDataForUI && hasValidWeatherCache)
            {
                UpdateUiWeatherString(WeatherService.CachedWeather);
            }

            WeatherInfo weatherForApply = null;
            if (policy.CanControlWeather && hasValidWeatherCache)
            {
                weatherForApply = WeatherService.CachedWeather;
            }

            ApplyByPolicy(policy, weatherForApply, forceApply);

            if (shouldFetchWeather && hasValidWeatherCache)
            {
                ScheduleNextWeatherCheckFromCache(location);
            }
            else
            {
                ScheduleDefaultWeatherCheck();
            }
        }

        private void HandlePendingForceRefresh()
        {
            if (_pendingForceRefresh)
            {
                _pendingForceRefresh = false;
                ForceRefreshWeather();
            }
        }

        private EnvironmentType? GetCurrentBaseEnvironment()
        {
            foreach (var env in BaseEnvironments)
            {
                if (IsEnvironmentActive(env))
                {
                    return env;
                }
            }

            return null;
        }

        private EnvironmentType? GetCurrentActiveEnvironment()
        {
            foreach (var env in MainEnvironments)
            {
                var winType = (WindowViewType)Enum.Parse(typeof(WindowViewType), env.ToString());
                if (WindowViewStateAccessor.TryIsWindowViewActive(winType, out var isActive) && isActive)
                {
                    return env;
                }
            }

            return null;
        }

        private bool IsEnvironmentActive(EnvironmentType env)
        {
            var winType = (WindowViewType)Enum.Parse(typeof(WindowViewType), env.ToString());
            if (WindowViewStateAccessor.TryIsWindowViewActive(winType, out var isActive))
            {
                return isActive;
            }

            return false;
        }

        private void SimulateClick(EnvironmentType env)
        {
            if (EnvRegistry.TryGet(env, out var ctrl))
            {
                ChillEnvPlugin.SimulateClickMainIcon(ctrl);
            }
        }

        public static bool IsBadWeather(int code)
        {
            if (code == 10 || code == 13 || code == 21 || code == 22)
            {
                return false;
            }

            if (code == 4)
            {
                return true;
            }

            if (code >= 7 && code <= 31)
            {
                return true;
            }

            if (code >= 34 && code <= 36)
            {
                return true;
            }

            return false;
        }

        private EnvironmentType? GetSceneryType(int code)
        {
            if (code >= 20 && code <= 25) return EnvironmentType.Snow;
            if (code == 11 || code == 12 || (code >= 16 && code <= 18)) return EnvironmentType.ThunderRain;
            if (code == 10 || code == 14 || code == 15) return EnvironmentType.HeavyRain;
            if (code == 13 || code == 19) return EnvironmentType.LightRain;
            return null;
        }

        public static EnvironmentType GetTimeBasedEnvironment()
        {
            DateTime now = DateTime.Now;
            TimeSpan currentTime = now.TimeOfDay;

            TimeSpan sunrise;
            TimeSpan sunset;
            TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunriseTime.Value, out sunrise);
            TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunsetTime.Value, out sunset);

            if (currentTime >= sunrise && currentTime < sunset.Subtract(TimeSpan.FromMinutes(30)))
            {
                return EnvironmentType.Day;
            }

            if (currentTime >= sunset.Subtract(TimeSpan.FromMinutes(30)) && currentTime < sunset.Add(TimeSpan.FromMinutes(30)))
            {
                return EnvironmentType.Sunset;
            }

            return EnvironmentType.Night;
        }

        private void ApplyBaseEnvironment(EnvironmentType target, bool force)
        {
            if (force || !IsEnvironmentActive(target))
            {
                ChillEnvPlugin.CallServiceChangeWeather(target);
                ChillEnvPlugin.Log?.LogInfo($"[环境] 切换至: {target}");
            }
        }

        private void ApplyScenery(EnvironmentType? target, bool force)
        {
            foreach (var env in SceneryWeathers)
            {
                bool shouldBeActive = target.HasValue && target.Value == env;
                if (EnvRegistry.TryGet(env, out var controller))
                {
                    bool isInTargetState;
                    if (!EnvironmentControllerStateSetter.TryIsInTargetState(
                        controller,
                        shouldBeActive,
                        out isInTargetState))
                    {
                        ChillEnvPlugin.Log?.LogWarning($"[景色] 状态判定失败，降级为点击方案: {env}");
                        SimulateClick(env);
                        continue;
                    }

                    if (!isInTargetState)
                    {
                        if (!EnvironmentControllerStateSetter.TrySetCombinedState(controller, shouldBeActive))
                        {
                            ChillEnvPlugin.Log?.LogWarning(
                                $"[景色] 窗景音频直连失败，降级为点击方案: {env}");
                            SimulateClick(env);
                            continue;
                        }

                        ChillEnvPlugin.Log?.LogInfo(
                            $"[景色] 已同步: {env}={shouldBeActive}");
                    }
                    continue;
                }

                bool isActive = IsEnvironmentActive(env);
                if (shouldBeActive != isActive)
                {
                    SimulateClick(env);
                }
            }
        }

        private void ApplyByPolicy(SyncPolicySnapshot policy, WeatherInfo weather, bool force)
        {
            if (SceneryAutomationSystem.IsWhaleSystemTriggered)
            {
                ChillEnvPlugin.Log?.LogInfo("[鲸鱼彩蛋] 系统抽中的鲸鱼生效中，跳过天气切换");
                return;
            }

            EnvironmentType timeBase = GetBaseTimeEnvironmentOnly();
            EnvironmentType finalEnv = timeBase;
            if (policy.CanApplyCloudyOverride && weather != null && IsBadWeather(weather.Code) && timeBase != EnvironmentType.Night)
            {
                finalEnv = EnvironmentType.Cloudy;
            }

            if (policy.CanControlTime)
            {
                ApplyBaseEnvironment(finalEnv, force);
                _lastAppliedEnv = finalEnv;
            }

            if (!policy.CanControlWeather || weather == null)
            {
                return;
            }

            ApplyScenery(GetSceneryType(weather.Code), force);
            if (!policy.CanControlTime)
            {
                _lastAppliedEnv = GetCurrentBaseEnvironment() ?? finalEnv;
            }
        }
    }
}
