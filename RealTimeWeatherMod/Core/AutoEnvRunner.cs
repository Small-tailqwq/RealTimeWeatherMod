using System;
using UnityEngine;
using Bulbul;
using ChillWithYou.EnvSync.Models;
using ChillWithYou.EnvSync.Services;
using ChillWithYou.EnvSync.Utils;

namespace ChillWithYou.EnvSync.Core
{
    public class AutoEnvRunner : MonoBehaviour
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
        }

        private void CheckAndSyncSunSchedule()
        {
            if (!ChillEnvPlugin.Cfg_EnableWeatherSync.Value) return;

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
            const int MaxRetries = 10; // Max delay ~1024s (17 mins)

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
                        
                        // Update Config
                        ChillEnvPlugin.Cfg_SunriseTime.Value = data.sunrise;
                        ChillEnvPlugin.Cfg_SunsetTime.Value = data.sunset;
                        ChillEnvPlugin.Cfg_LastSunSyncDate.Value = targetDate;
                        
                        ChillEnvPlugin.Instance.Config.Save();
                        success = true;
                    }
                });

                if (success) yield break;

                ChillEnvPlugin.Log?.LogWarning($"[SunSync] 同步失败，{delay}秒后重试 ({retryCount + 1}/{MaxRetries})");
                yield return new WaitForSeconds(delay);
                
                delay *= 2f;
                retryCount++;
            }
            ChillEnvPlugin.Log?.LogError("[SunSync] 达到最大重试次数，今日放弃同步");
        }

        private void Update()
        {
            if (!ChillEnvPlugin.Initialized || EnvRegistry.Count == 0) return;
            if (Input.GetKeyDown(KeyCode.F9)) { ChillEnvPlugin.Log?.LogInfo("F9: 强制同步"); TriggerSync(false, true); }
            if (Input.GetKeyDown(KeyCode.F8)) ShowStatus();
            if (Input.GetKeyDown(KeyCode.F7)) { ChillEnvPlugin.Log?.LogInfo("F7: 强制刷新"); ChillEnvPlugin.Instance.Config.Reload(); ForceRefreshWeather(); }
            if (Time.time >= _nextTimeCheckTime) { _nextTimeCheckTime = Time.time + 30f; TriggerSync(false, false); }
            if (Time.time >= _nextWeatherCheckTime) { _nextWeatherCheckTime = Time.time + (Mathf.Max(1, ChillEnvPlugin.Cfg_WeatherRefreshMinutes.Value) * 60f); TriggerSync(true, false); }
        }

        private void ShowStatus()
        {
            var now = DateTime.Now;
            ChillEnvPlugin.Log?.LogInfo($"--- 状态 [{now:HH:mm:ss}] ---");
            ChillEnvPlugin.Log?.LogInfo($"插件记录: {_lastAppliedEnv}");
            var currentActive = GetCurrentActiveEnvironment();
            ChillEnvPlugin.Log?.LogInfo($"游戏实际: {currentActive}");
            ChillEnvPlugin.Log?.LogInfo($"UI文本: {ChillEnvPlugin.UIWeatherString}");
            if (ChillEnvPlugin.Cfg_DebugMode.Value) ChillEnvPlugin.Log?.LogWarning("【警告】调试模式已开启！");
        }

        private void ForceRefreshWeather()
        {
            // 正在请求中则排队一次刷新，避免丢失强制刷新需求
            if (_isFetching)
            {
                _pendingForceRefresh = true;
                ChillEnvPlugin.Log?.LogInfo("ForceRefresh requested while fetching; queued until current fetch completes");
                return;
            }

            _nextWeatherCheckTime = Time.time + (ChillEnvPlugin.Cfg_WeatherRefreshMinutes.Value * 60f);
            TriggerSync(true, false);
        }

        /// <summary>
        /// 公开方法：立即强制刷新天气（供外部调用）
        /// </summary>
        public static void TriggerWeatherRefresh()
        {
            if (_instance != null)
            {
                ChillEnvPlugin.Log?.LogInfo("🔄 外部触发天气刷新");
                _instance.ForceRefreshWeather();
            }
        }

        /// <summary>
        /// 公开方法：立即刷新日出日落数据（供外部调用，如城市修改时）
        /// </summary>
        public static void TriggerSunScheduleRefresh()
        {
            if (_instance != null)
            {
                ChillEnvPlugin.Log?.LogInfo("🌅 外部触发日出日落刷新");
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                _instance.StartCoroutine(_instance.SyncSunScheduleRoutine(today));
            }
        }

        private void TriggerSync(bool forceApi, bool forceApply)
        {
            ChillEnvPlugin.Log?.LogInfo($"TriggerSync called (forceApi={forceApi}, forceApply={forceApply})");

            if (ChillEnvPlugin.Cfg_DebugMode.Value)
            {
                ChillEnvPlugin.Log?.LogWarning("[调试模式] 使用模拟数据...");
                int mockCode = ChillEnvPlugin.Cfg_DebugCode.Value;
                var mockWeather = new WeatherInfo { Code = mockCode, Temperature = ChillEnvPlugin.Cfg_DebugTemp.Value, Text = ChillEnvPlugin.Cfg_DebugText.Value, Condition = WeatherService.MapCodeToCondition(mockCode), UpdateTime = DateTime.Now };
                ApplyEnvironment(mockWeather, forceApply);
                return;
            }

            bool weatherEnabled = ChillEnvPlugin.Cfg_EnableWeatherSync.Value;
            string apiKey = ChillEnvPlugin.Cfg_SeniverseKey.Value;
            ChillEnvPlugin.Log?.LogInfo($"TriggerSync: EnableWeatherSync={weatherEnabled}, ApiKeyPresent={!string.IsNullOrEmpty(apiKey)}, HasDefaultKey={WeatherService.HasDefaultKey}");

            if (!(weatherEnabled && (!string.IsNullOrEmpty(apiKey) || WeatherService.HasDefaultKey)))
            {
                if (!weatherEnabled)
                    ChillEnvPlugin.Log?.LogInfo("TriggerSync aborted: Weather sync disabled (Cfg_EnableWeatherSync=false)");
                else if (string.IsNullOrEmpty(apiKey) && !WeatherService.HasDefaultKey)
                    ChillEnvPlugin.Log?.LogInfo("TriggerSync aborted: No API key available and no default key");

                ApplyTimeBasedEnvironment(forceApply);
                return;
            }

            string location = ChillEnvPlugin.Cfg_Location.Value;
            ChillEnvPlugin.Log?.LogInfo($"TriggerSync: target location='{location}', forceApi={forceApi}");

            if (forceApi || WeatherService.CachedWeather == null)
            {
                if (_isFetching)
                {
                    ChillEnvPlugin.Log?.LogWarning("TriggerSync aborted: fetch already in progress (_isFetching=true)");
                    return;
                }

                _isFetching = true;
                ChillEnvPlugin.Log?.LogInfo("TriggerSync: starting WeatherService.FetchWeather coroutine");
                StartCoroutine(WeatherService.FetchWeather(apiKey, location, forceApi, (weather) => {
                    _isFetching = false;
                    if (weather != null) ApplyEnvironment(weather, forceApply);
                    else { ChillEnvPlugin.Log?.LogWarning("[API异常] 启用时间兜底"); ApplyTimeBasedEnvironment(forceApply); }

                    // 如果期间有排队的强制刷新（如改城市/F7），在当前请求结束后立即补一次
                    if (_pendingForceRefresh)
                    {
                        _pendingForceRefresh = false;
                        ChillEnvPlugin.Log?.LogInfo("TriggerSync: pending force refresh detected, running now");
                        ForceRefreshWeather();
                    }
                }));
            }
            else
            {
                ChillEnvPlugin.Log?.LogInfo("TriggerSync: using cached weather");
                ApplyEnvironment(WeatherService.CachedWeather, forceApply);
            }
        }

        private EnvironmentType? GetCurrentActiveEnvironment()
        {
            try { var dict = SaveDataManager.Instance.WindowViewDic; foreach (var env in MainEnvironments) { var winType = (WindowViewType)Enum.Parse(typeof(WindowViewType), env.ToString()); if (dict.ContainsKey(winType) && dict[winType].IsActive) return env; } } catch { }
            return null;
        }

        private bool IsEnvironmentActive(EnvironmentType env)
        {
            try { var dict = SaveDataManager.Instance.WindowViewDic; var winType = (WindowViewType)Enum.Parse(typeof(WindowViewType), env.ToString()); if (dict.ContainsKey(winType)) return dict[winType].IsActive; } catch { }
            return false;
        }

        private void SimulateClick(EnvironmentType env) { if (EnvRegistry.TryGet(env, out var ctrl)) ChillEnvPlugin.SimulateClickMainIcon(ctrl); }

        private bool IsBadWeather(int code)
        {
            // v4.4.1 逻辑：排除太阳雨/雪
            if (code == 10 || code == 13 || code == 21 || code == 22) return false;
            if (code == 4) return true;
            if (code >= 7 && code <= 31) return true;
            if (code >= 34 && code <= 36) return true;
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

        private EnvironmentType GetTimeBasedEnvironment()
        {
            DateTime now = DateTime.Now; TimeSpan cur = now.TimeOfDay;
            TimeSpan sunrise, sunset; TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunriseTime.Value, out sunrise); TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunsetTime.Value, out sunset);
            if (cur >= sunrise && cur < sunset.Subtract(TimeSpan.FromMinutes(30))) return EnvironmentType.Day;
            else if (cur >= sunset.Subtract(TimeSpan.FromMinutes(30)) && cur < sunset.Add(TimeSpan.FromMinutes(30))) return EnvironmentType.Sunset;
            else return EnvironmentType.Night;
        }

        private void ApplyBaseEnvironment(EnvironmentType target, bool force)
        {
            if (!force && IsEnvironmentActive(target)) return;
            foreach (var env in BaseEnvironments) if (env != target && IsEnvironmentActive(env)) SimulateClick(env);
            if (!IsEnvironmentActive(target)) SimulateClick(target);
            ChillEnvPlugin.CallServiceChangeWeather(target);
            ChillEnvPlugin.Log?.LogInfo($"[环境] 切换至: {target}");
        }

        private void ApplyScenery(EnvironmentType? target, bool force)
        {
            foreach (var env in SceneryWeathers)
            {
                bool shouldBeActive = (target.HasValue && target.Value == env);
                bool isActive = IsEnvironmentActive(env);
                if (shouldBeActive && !isActive) { SimulateClick(env); ChillEnvPlugin.Log?.LogInfo($"[景色] 开启: {env}"); }
                else if (!shouldBeActive && isActive) { SimulateClick(env); }
            }
        }

        private void ApplyEnvironment(WeatherInfo weather, bool force)
        {
            // 【鲸鱼保护】如果系统抽中的鲸鱼正在开启，跳过所有天气切换
            if (Core.SceneryAutomationSystem.IsWhaleSystemTriggered)
            {
                ChillEnvPlugin.Log?.LogInfo("[鲸鱼彩蛋] 🐋 系统抽中的鲸鱼生效中，跳过天气切换");
                return;
            }
            
            if (force || _lastAppliedEnv == null) ChillEnvPlugin.Log?.LogInfo($"[决策] 天气:{weather.Text}(Code:{weather.Code})");
            ChillEnvPlugin.UIWeatherString = $"{weather.Text} {weather.Temperature}°C";
            EnvironmentType baseEnv = GetTimeBasedEnvironment();
            EnvironmentType finalEnv = baseEnv;
            if (IsBadWeather(weather.Code)) { if (baseEnv != EnvironmentType.Night) finalEnv = EnvironmentType.Cloudy; }
            ApplyBaseEnvironment(finalEnv, force);
            ApplyScenery(GetSceneryType(weather.Code), force);
            _lastAppliedEnv = finalEnv;
        }

        private void ApplyTimeBasedEnvironment(bool force)
        {
            // 【鲸鱼保护】如果系统抽中的鲸鱼正在开启，跳过所有天气切换
            if (Core.SceneryAutomationSystem.IsWhaleSystemTriggered)
            {
                ChillEnvPlugin.Log?.LogInfo("[鲸鱼彩蛋] 🐋 系统抽中的鲸鱼生效中，跳过天气切换");
                return;
            }
            
            ChillEnvPlugin.UIWeatherString = "";
            EnvironmentType targetEnv = GetTimeBasedEnvironment();
            ApplyBaseEnvironment(targetEnv, force);
            ApplyScenery(null, force);
        }
    }
}
