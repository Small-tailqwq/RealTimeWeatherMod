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
        private float _lastInitTryTime = 0f;
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

            _log.LogInfo("=== Chill Env Sync 插件已加载 ===");
            _log.LogInfo($"配置: 城市={_cityName.Value}, 间隔={_syncIntervalMinutes.Value}分钟, 启用={_enableWeatherSync.Value}");
        }

        private void Update()
        {
            if (!_enableWeatherSync.Value) return;

            // 尝试初始化（每2秒尝试一次，避免刷屏）
            if (!_isInitialized)
            {
                if (Time.time - _lastInitTryTime >= 2f)
                {
                    _lastInitTryTime = Time.time;
                    TryInitialize();
                }
                return;
            }

            // 定时同步
            float interval = _syncIntervalMinutes.Value * 60f;
            if (Time.time - _lastSyncTime >= interval)
            {
                _lastSyncTime = Time.time;
                _log.LogInfo($"[定时触发] 开始天气同步，间隔={interval}秒");
                StartCoroutine(FetchAndApplyWeather());
            }
        }

        private void TryInitialize()
        {
            try
            {
                _log.LogInfo("[初始化] 正在查找 WindowViewService...");

                // 方法1：直接查找
                _windowViewService = FindObjectOfType<WindowViewService>();

                if (_windowViewService != null)
                {
                    _isInitialized = true;
                    _log.LogInfo($"[初始化] ✓ WindowViewService 已找到: {_windowViewService.name}");
                    _log.LogInfo($"[初始化] GameObject路径: {GetGameObjectPath(_windowViewService.gameObject)}");

                    // 立即执行一次同步
                    _lastSyncTime = Time.time;
                    StartCoroutine(FetchAndApplyWeather());
                }
                else
                {
                    _log.LogWarning("[初始化] ✗ WindowViewService 未找到，2秒后重试...");

                    // 打印场景中的所有根对象，帮助调试
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    _log.LogInfo($"[初始化] 当前场景: {scene.name}");

                    var rootObjects = scene.GetRootGameObjects();
                    _log.LogInfo($"[初始化] 场景根对象数量: {rootObjects.Length}");
                    foreach (var root in rootObjects)
                    {
                        _log.LogInfo($"  - {root.name}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[初始化] 异常: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        // ========== 天气获取 ==========
        private IEnumerator FetchAndApplyWeather()
        {
            string city = _cityName.Value;
            string apiKey = _apiKey.Value;

            _log.LogInfo($"[天气请求] 城市: {city}, API密钥: {apiKey.Substring(0, Math.Min(8, apiKey.Length))}...");

            // 检查API密钥是否是默认值
            if (apiKey == "S-xxxxxxxx" || string.IsNullOrEmpty(apiKey))
            {
                _log.LogError("[天气请求] API密钥未配置！请在 BepInEx/config/com.yourname.chillroomsync.cfg 中设置");
                yield break;
            }

            string url = $"https://api.seniverse.com/v3/weather/now.json?key={apiKey}&location={UnityWebRequest.EscapeURL(city)}&language=zh-Hans&unit=c";
            _log.LogInfo($"[天气请求] URL: {url}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                _log.LogInfo("[天气请求] 发送请求...");
                yield return request.SendWebRequest();

                _log.LogInfo($"[天气请求] 响应状态: {request.result}");

                if (request.result != UnityWebRequest.Result.Success)
                {
                    _log.LogError($"[天气请求] 失败: {request.error}");
                    _log.LogError($"[天气请求] HTTP状态码: {request.responseCode}");
                    yield break;
                }

                string json = request.downloadHandler.text;
                _log.LogInfo($"[天气请求] 返回JSON: {json}");

                var weatherData = ParseWeatherJson(json);
                if (weatherData.HasValue)
                {
                    var data = weatherData.Value;
                    _log.LogInfo($"[天气解析] 成功: {data.Text}, {data.Temperature}°C, Code={data.Code}");

                    bool isDay = IsDaytime();
                    _log.LogInfo($"[天气决策] 当前时间: {DateTime.Now:HH:mm}, 是否白天: {isDay}");

                    ApplyWeather(data.Code, isDay);
                }
                else
                {
                    _log.LogError("[天气解析] 失败，无法解析JSON");
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

                _log.LogInfo($"[JSON解析] text匹配: {textMatch.Success}, code匹配: {codeMatch.Success}, temp匹配: {tempMatch.Success}");

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
                _log.LogError($"[JSON解析] 异常: {ex.Message}");
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

                case "11":
                case "12":
                    return new WeatherMapping
                    {
                        BaseEnvironment = WindowViewType.Cloudy,
                        PrecipitationType = WindowViewType.ThunderRain,
                        SpecialEffect = null,
                        ClearAllPrecipitation = false
                    };

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
                    _log.LogWarning($"[天气映射] 未知代码: {weatherCode}, 使用默认");
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
                _log.LogError("[应用天气] WindowViewService 为 null");
                return;
            }

            var mapping = GetWeatherMapping(weatherCode, isDay);

            _log.LogInfo($"[应用天气] 基础={mapping.BaseEnvironment}, " +
                        $"降水={mapping.PrecipitationType?.ToString() ?? "无"}, " +
                        $"特效={mapping.SpecialEffect?.ToString() ?? "无"}");

            try
            {
                // 先打印当前状态
                _log.LogInfo("[应用天气] 当前环境状态:");
                foreach (var env in BaseTimeWeather)
                {
                    bool active = _windowViewService.IsActiveWindow(env);
                    _log.LogInfo($"  - {env}: {(active ? "激活" : "关闭")}");
                }

                ApplyBaseEnvironment(mapping.BaseEnvironment);
                ApplyPrecipitation(mapping.PrecipitationType, mapping.ClearAllPrecipitation);

                if (mapping.SpecialEffect.HasValue)
                {
                    ApplySpecialEffect(mapping.SpecialEffect.Value);
                }

                SaveEnvironmentState(mapping);

                _log.LogInfo("[应用天气] ✓ 完成");
            }
            catch (Exception ex)
            {
                _log.LogError($"[应用天气] 异常: {ex.Message}\n{ex.StackTrace}");
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
                _log.LogInfo("[存档] 尝试保存环境状态...");

                var saveData = SaveDataManager.Instance;
                if (saveData == null)
                {
                    _log.LogWarning("[存档] SaveDataManager.Instance 为 null");
                    return;
                }

                if (saveData.WindowViewDic == null)
                {
                    _log.LogWarning("[存档] WindowViewDic 为 null");
                    return;
                }

                _log.LogInfo($"[存档] WindowViewDic 数量: {saveData.WindowViewDic.Count}");

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
                _log.LogInfo("[存档] ✓ 保存成功");
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[存档] 异常: {ex.Message}");
            }
        }
    }
}