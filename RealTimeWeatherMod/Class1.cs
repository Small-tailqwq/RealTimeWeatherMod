using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Networking;
using Bulbul;

namespace ChillEnvSync
{
    [BepInPlugin("com.yourname.chillroomsync", "Chill Room Weather Sync", "1.0.0")]
    public class ChillEnvSync : BaseUnityPlugin
    {
        // ========== 配置 ==========
        private ConfigEntry<bool> _enableWeatherSync;
        private ConfigEntry<string> _cityName;
        private ConfigEntry<string> _apiKey;
        private ConfigEntry<int> _syncIntervalMinutes;

        // ========== 状态 ==========
        private float _lastSyncTime = -9999f;
        private WindowViewService _windowViewService;
        private bool _isInitialized = false;
        private ManualLogSource _log;

        // ========== 互斥组定义 ==========
        private static readonly HashSet<WindowViewType> BaseTimeWeather = new HashSet<WindowViewType>
        {
            WindowViewType.Day,
            WindowViewType.Sunset,
            WindowViewType.Night,
            WindowViewType.Cloudy
        };

        private static readonly HashSet<WindowViewType> PrecipitationWeather = new HashSet<WindowViewType>
        {
            WindowViewType.LightRain,
            WindowViewType.HeavyRain,
            WindowViewType.ThunderRain
        };

        // ========== 初始化 ==========
        private void Awake()
        {
            _log = Logger;

            _enableWeatherSync = Config.Bind("General", "EnableWeatherSync", true, "启用天气同步");
            _cityName = Config.Bind("General", "CityName", "北京", "城市名称");
            _apiKey = Config.Bind("General", "ApiKey", "S-xxxxxxxx", "心知天气API密钥");
            _syncIntervalMinutes = Config.Bind("General", "SyncIntervalMinutes", 30, "同步间隔(分钟)");

            _log.LogInfo("Chill Env Sync 已加载");
        }

        private void Update()
        {
            if (!_enableWeatherSync.Value) return;

            // 尝试初始化
            if (!_isInitialized)
            {
                TryInitialize();
                return;
            }

            // 定时同步
            float interval = _syncIntervalMinutes.Value * 60f;
            if (Time.time - _lastSyncTime >= interval)
            {
                _lastSyncTime = Time.time;
                StartCoroutine(FetchAndApplyWeather());
            }
        }

        private void TryInitialize()
        {
            try
            {
                _windowViewService = FindObjectOfType<WindowViewService>();
                if (_windowViewService != null)
                {
                    _isInitialized = true;
                    _log.LogInfo("WindowViewService 已找到，初始化完成");

                    // 立即执行一次同步
                    _lastSyncTime = Time.time;
                    StartCoroutine(FetchAndApplyWeather());
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"初始化失败: {ex.Message}");
            }
        }

        // ========== 天气获取 ==========
        private IEnumerator FetchAndApplyWeather()
        {
            string city = _cityName.Value;
            string apiKey = _apiKey.Value;

            _log.LogInfo($"请求天气: {city}");

            string url = $"https://api.seniverse.com/v3/weather/now.json?key={apiKey}&location={UnityWebRequest.EscapeURL(city)}&language=zh-Hans&unit=c";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    _log.LogError($"天气请求失败: {request.error}");
                    yield break;
                }

                string json = request.downloadHandler.text;
                _log.LogInfo($"天气API返回: {json}");

                var weatherData = ParseWeatherJson(json);
                if (weatherData.HasValue)
                {
                    var data = weatherData.Value;
                    _log.LogInfo($"天气解析成功: {data.Text}, {data.Temperature}°C, Code={data.Code}");

                    bool isDay = IsDaytime();
                    _log.LogInfo($"[天气决策] {data.Text} + 当前时间 -> {(isDay ? "Day" : "Night")}");

                    ApplyWeather(data.Code, isDay);
                }
                else
                {
                    _log.LogError("天气解析失败");
                }
            }
        }

        private struct WeatherData
        {
            public string Text;
            public string Code;
            public string Temperature;
        }

        private WeatherData? ParseWeatherJson(string json)
        {
            try
            {
                var textMatch = Regex.Match(json, "\"text\"\\s*:\\s*\"([^\"]+)\"");
                var codeMatch = Regex.Match(json, "\"code\"\\s*:\\s*\"([^\"]+)\"");
                var tempMatch = Regex.Match(json, "\"temperature\"\\s*:\\s*\"([^\"]+)\"");

                if (textMatch.Success && codeMatch.Success && tempMatch.Success)
                {
                    return new WeatherData
                    {
                        Text = textMatch.Groups[1].Value,
                        Code = codeMatch.Groups[1].Value,
                        Temperature = tempMatch.Groups[1].Value
                    };
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"JSON解析异常: {ex.Message}");
            }
            return null;
        }

        private bool IsDaytime()
        {
            int hour = DateTime.Now.Hour;
            return hour >= 6 && hour < 18;
        }

        // ========== 天气映射 ==========
        private struct WeatherMapping
        {
            public WindowViewType BaseEnvironment;
            public WindowViewType? PrecipitationType;
            public WindowViewType? SpecialEffect;
            public bool ClearAllPrecipitation;
        }

        private WeatherMapping GetWeatherMapping(string weatherCode, bool isDay)
        {
            WindowViewType baseEnv = isDay ? WindowViewType.Day : WindowViewType.Night;

            switch (weatherCode)
            {
                // 晴天
                case "0":
                case "1":
                case "2":
                case "3":
                    return new WeatherMapping
                    {
                        BaseEnvironment = baseEnv,
                        PrecipitationType = null,
                        SpecialEffect = null,
                        ClearAllPrecipitation = true
                    };

                // 多云/阴
                case "4":
                case "5":
                case "6":
                case "7":
                case "8":
                case "9":
                    return new WeatherMapping
                    {
                        BaseEnvironment = WindowViewType.Cloudy,
                        PrecipitationType = null,
                        SpecialEffect = null,
                        ClearAllPrecipitation = true
                    };

                // 小雨/阵雨
                case "10":
                case "13":
                case "14":
                case "19":
                    return new WeatherMapping
                    {
                        BaseEnvironment = WindowViewType.Cloudy,
                        PrecipitationType = WindowViewType.LightRain,
                        SpecialEffect = null,
                        ClearAllPrecipitation = false
                    };

                // 大雨/暴雨
                case "15":
                case "16":
                case "17":
                case "18":
                    return new WeatherMapping
                    {
                        BaseEnvironment = WindowViewType.Cloudy,
                        PrecipitationType = WindowViewType.HeavyRain,
                        SpecialEffect = null,
                        ClearAllPrecipitation = false
                    };

                // 雷雨
                case "11":
                case "12":
                    return new WeatherMapping
                    {
                        BaseEnvironment = WindowViewType.Cloudy,
                        PrecipitationType = WindowViewType.ThunderRain,
                        SpecialEffect = null,
                        ClearAllPrecipitation = false
                    };

                // 雪
                case "20":
                case "21":
                case "22":
                case "23":
                case "24":
                case "25":
                    return new WeatherMapping
                    {
                        BaseEnvironment = WindowViewType.Cloudy,
                        PrecipitationType = null,
                        SpecialEffect = WindowViewType.Snow,
                        ClearAllPrecipitation = true
                    };

                // 雾/霾/沙尘
                case "30":
                case "31":
                case "32":
                case "33":
                case "34":
                case "35":
                    return new WeatherMapping
                    {
                        BaseEnvironment = WindowViewType.Cloudy,
                        PrecipitationType = null,
                        SpecialEffect = null,
                        ClearAllPrecipitation = true
                    };

                default:
                    _log.LogWarning($"未知天气代码: {weatherCode}, 使用默认映射");
                    return new WeatherMapping
                    {
                        BaseEnvironment = baseEnv,
                        PrecipitationType = null,
                        SpecialEffect = null,
                        ClearAllPrecipitation = true
                    };
            }
        }

        // ========== 应用天气 ==========
        private void ApplyWeather(string weatherCode, bool isDay)
        {
            if (_windowViewService == null)
            {
                _log.LogError("WindowViewService 未初始化");
                return;
            }

            var mapping = GetWeatherMapping(weatherCode, isDay);

            _log.LogInfo($"[应用天气] 基础={mapping.BaseEnvironment}, " +
                        $"降水={mapping.PrecipitationType?.ToString() ?? "无"}, " +
                        $"特效={mapping.SpecialEffect?.ToString() ?? "无"}");

            try
            {
                ApplyBaseEnvironment(mapping.BaseEnvironment);
                ApplyPrecipitation(mapping.PrecipitationType, mapping.ClearAllPrecipitation);

                if (mapping.SpecialEffect.HasValue)
                {
                    ApplySpecialEffect(mapping.SpecialEffect.Value);
                }

                SaveEnvironmentState(mapping);

                _log.LogInfo("[应用天气] 完成");
            }
            catch (Exception ex)
            {
                _log.LogError($"应用天气失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ApplyBaseEnvironment(WindowViewType target)
        {
            WindowViewType? currentBase = null;
            foreach (var env in BaseTimeWeather)
            {
                if (_windowViewService.IsActiveWindow(env))
                {
                    currentBase = env;
                    break;
                }
            }

            if (currentBase == target)
            {
                _log.LogInfo($"[基础环境] {target} 已激活，跳过");
                return;
            }

            _log.LogInfo($"[基础环境] {currentBase?.ToString() ?? "无"} -> {target}");
            _windowViewService.ChangeWeatherAndTime(target);
        }

        private void ApplyPrecipitation(WindowViewType? target, bool clearAll)
        {
            if (clearAll)
            {
                foreach (var precip in PrecipitationWeather)
                {
                    if (_windowViewService.IsActiveWindow(precip))
                    {
                        _log.LogInfo($"[降水] 关闭: {precip}");
                        _windowViewService.DeactivateWindow(precip);
                    }
                }
            }
            else if (target.HasValue)
            {
                foreach (var precip in PrecipitationWeather)
                {
                    bool isActive = _windowViewService.IsActiveWindow(precip);
                    bool shouldBeActive = (precip == target.Value);

                    if (shouldBeActive && !isActive)
                    {
                        _log.LogInfo($"[降水] 激活: {precip}");
                        _windowViewService.ActivateWindow(precip);
                    }
                    else if (!shouldBeActive && isActive)
                    {
                        _log.LogInfo($"[降水] 关闭: {precip}");
                        _windowViewService.DeactivateWindow(precip);
                    }
                }
            }
        }

        private void ApplySpecialEffect(WindowViewType effect)
        {
            if (!_windowViewService.IsActiveWindow(effect))
            {
                _log.LogInfo($"[特效] 激活: {effect}");
                _windowViewService.ActivateWindow(effect);
            }
        }

        private void SaveEnvironmentState(WeatherMapping mapping)
        {
            try
            {
                var saveData = SaveDataManager.Instance;
                if (saveData?.WindowViewDic == null) return;

                foreach (var env in BaseTimeWeather)
                {
                    if (saveData.WindowViewDic.ContainsKey(env))
                    {
                        saveData.WindowViewDic[env].IsActive = (env == mapping.BaseEnvironment);
                    }
                }

                foreach (var precip in PrecipitationWeather)
                {
                    if (saveData.WindowViewDic.ContainsKey(precip))
                    {
                        bool shouldBeActive = mapping.PrecipitationType.HasValue &&
                                             mapping.PrecipitationType.Value == precip;
                        saveData.WindowViewDic[precip].IsActive = shouldBeActive;
                    }
                }

                if (mapping.SpecialEffect.HasValue &&
                    saveData.WindowViewDic.ContainsKey(mapping.SpecialEffect.Value))
                {
                    saveData.WindowViewDic[mapping.SpecialEffect.Value].IsActive = true;
                }

                saveData.SaveEnviroment();
                _log.LogInfo("[存档] 环境状态已保存");
            }
            catch (Exception ex)
            {
                _log.LogWarning($"保存存档失败: {ex.Message}");
            }
        }
    }
}