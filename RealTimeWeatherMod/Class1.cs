using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using Bulbul;

namespace ChillWithYou.EnvSync
{
    [BepInPlugin("chillwithyou.envsync", "Chill Env Sync", "3.1.0")]
    public class ChillEnvPlugin : BaseUnityPlugin
    {
        internal static ChillEnvPlugin Instance;
        internal static ManualLogSource Log;
        internal static UnlockItemService UnlockItemServiceInstance;
        internal static bool Initialized;

        // 配置项
        internal static ConfigEntry<int> Cfg_WeatherRefreshMinutes;
        internal static ConfigEntry<string> Cfg_SunriseTime;
        internal static ConfigEntry<string> Cfg_SunsetTime;
        internal static ConfigEntry<string> Cfg_SeniverseKey;
        internal static ConfigEntry<string> Cfg_Location;
        internal static ConfigEntry<bool> Cfg_EnableWeatherSync;

        private static AutoEnvRunner _runner;
        private static GameObject _runnerGO;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo("【3.1.0】启动 - 支持心知天气同步");

            try
            {
                var harmony = new Harmony("ChillWithYou.EnvSync");
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Log.LogError($"Harmony 失败: {ex}");
            }

            InitConfig();

            try
            {
                _runnerGO = new GameObject("ChillEnvSyncRunner");
                _runnerGO.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(_runnerGO);
                _runnerGO.SetActive(true);
                _runner = _runnerGO.AddComponent<AutoEnvRunner>();
                _runner.enabled = true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Runner 创建失败: {ex}");
            }
        }

        private void InitConfig()
        {
            Cfg_WeatherRefreshMinutes = Config.Bind("WeatherSync", "RefreshMinutes", 30, "自动刷新间隔(分钟)");
            Cfg_SunriseTime = Config.Bind("TimeConfig", "Sunrise", "06:30", "日出时间");
            Cfg_SunsetTime = Config.Bind("TimeConfig", "Sunset", "18:30", "日落时间");

            Cfg_EnableWeatherSync = Config.Bind("WeatherAPI", "EnableWeatherSync", false, "是否启用天气API同步（需要填写API Key）");
            Cfg_SeniverseKey = Config.Bind("WeatherAPI", "SeniverseKey", "", "心知天气 API Key");
            Cfg_Location = Config.Bind("WeatherAPI", "Location", "beijing", "城市名称（拼音或中文，如 beijing、上海、ip 表示自动定位）");
        }

        internal static void TryInitializeOnce(UnlockItemService svc)
        {
            if (Initialized || svc == null) return;

            ForceUnlockAllEnvironments(svc);
            ForceUnlockAllDecorations(svc);

            Initialized = true;
            Log?.LogInfo("初始化完成，环境和装饰品已解锁");
        }

        private static void ForceUnlockAllEnvironments(UnlockItemService svc)
        {
            try
            {
                var envProp = svc.GetType().GetProperty("Environment");
                var unlockEnvObj = envProp.GetValue(svc);

                var dictField = unlockEnvObj.GetType().GetField("_environmentDic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var dict = dictField.GetValue(unlockEnvObj) as IDictionary;

                int count = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    var data = entry.Value;
                    var lockField = data.GetType().GetField("_isLocked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var reactive = lockField.GetValue(data);
                    var propValue = reactive.GetType().GetProperty("Value");
                    propValue.SetValue(reactive, false, null);
                    count++;
                }
                Log?.LogInfo($"✅ 已解锁 {count} 个环境");
            }
            catch (Exception ex)
            {
                Log?.LogError("环境解锁异常: " + ex.Message);
            }
        }

        private static void ForceUnlockAllDecorations(UnlockItemService svc)
        {
            try
            {
                var decoProp = svc.GetType().GetProperty("Decoration");
                if (decoProp == null) return;

                var unlockDecoObj = decoProp.GetValue(svc);
                if (unlockDecoObj == null) return;

                var dictField = unlockDecoObj.GetType().GetField("_decorationDic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (dictField == null) return;

                var dict = dictField.GetValue(unlockDecoObj) as IDictionary;
                if (dict == null) return;

                int count = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    var data = entry.Value;
                    var lockField = data.GetType().GetField("_isLocked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (lockField == null) continue;

                    var reactive = lockField.GetValue(data);
                    if (reactive == null) continue;

                    var propValue = reactive.GetType().GetProperty("Value");
                    if (propValue == null) continue;

                    propValue.SetValue(reactive, false, null);
                    count++;

                    Log?.LogInfo($"🔓 已解锁装饰品: {entry.Key}");
                }
                Log?.LogInfo($"✅ 已解锁 {count} 个装饰品");
            }
            catch (Exception ex)
            {
                Log?.LogError("装饰品解锁异常: " + ex.Message);
            }
        }
    }

    // 天气数据
    public enum WeatherCondition
    {
        Clear,      // 晴天
        Cloudy,     // 多云/阴天
        Rainy,      // 雨天
        Snowy,      // 雪天
        Foggy,      // 雾/霾
        Unknown
    }

    public class WeatherInfo
    {
        public WeatherCondition Condition;
        public int Temperature;
        public string Text;
        public int Code;
        public DateTime UpdateTime;

        public override string ToString()
        {
            return $"{Text}({Condition}), {Temperature}°C, Code={Code}";
        }
    }

    // 心知天气 JSON 解析
    [Serializable]
    public class WeatherApiResponse
    {
        public WeatherResult[] results;
    }

    [Serializable]
    public class WeatherResult
    {
        public WeatherLocation location;
        public WeatherNow now;
    }

    [Serializable]
    public class WeatherLocation
    {
        public string name;
    }

    [Serializable]
    public class WeatherNow
    {
        public string text;
        public string code;
        public string temperature;
    }

    public class WeatherService
    {
        private static WeatherInfo _cachedWeather;
        private static DateTime _lastFetchTime;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

        // 依据天气文本到环境的映射（大小写不敏感）
        private static readonly Dictionary<string, EnvironmentType> WeatherToEnvironment = new Dictionary<string, EnvironmentType>(StringComparer.OrdinalIgnoreCase)
        {
            // 晴天/少云 -> 根据时间自动选择（白天/日落/夜晚）
            {"Clear", EnvironmentType.Day},
            {"Clouds", EnvironmentType.Cloudy},

            // 雨天
            {"Drizzle", EnvironmentType.LightRain},      // 毛毛雨
            {"Rain", EnvironmentType.HeavyRain},          // 普通雨
            {"Thunderstorm", EnvironmentType.ThunderRain}, // 雷雨

            // 雪
            {"Snow", EnvironmentType.Snow},

            // 其他天气 -> 映射到最接近的
            {"Mist", EnvironmentType.Cloudy},
            {"Fog", EnvironmentType.Cloudy},
            {"Haze", EnvironmentType.Cloudy},
            {"Dust", EnvironmentType.Wind},
            {"Sand", EnvironmentType.Wind},
            {"Squall", EnvironmentType.ThunderRain},
            {"Tornado", EnvironmentType.ThunderRain},
        };

        internal static bool TryGetEnvironment(string weatherText, out EnvironmentType env)
        {
            env = default(EnvironmentType);
            if (string.IsNullOrEmpty(weatherText)) return false;
            return WeatherToEnvironment.TryGetValue(weatherText.Trim(), out env);
        }

        public static WeatherInfo CachedWeather => _cachedWeather;

        public static IEnumerator FetchWeather(string apiKey, string location, Action<WeatherInfo> onComplete)
        {
            // 检查缓存
            if (_cachedWeather != null && DateTime.Now - _lastFetchTime < CacheExpiry)
            {
                ChillEnvPlugin.Log?.LogInfo($"使用缓存天气: {_cachedWeather}");
                onComplete?.Invoke(_cachedWeather);
                yield break;
            }

            string url = $"https://api.seniverse.com/v3/weather/now.json?key={apiKey}&location={UnityWebRequest.EscapeURL(location)}&language=zh-Hans&unit=c";

            ChillEnvPlugin.Log?.LogInfo($"请求天气: {location}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    ChillEnvPlugin.Log?.LogWarning($"天气API请求失败: {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                string json = request.downloadHandler.text;
                ChillEnvPlugin.Log?.LogInfo($"天气API返回: {json}");

                try
                {
                    // 手动解析 JSON（因为 JsonUtility 对嵌套数组支持不好）
                    var weather = ParseWeatherJson(json);
                    
                    if (weather != null)
                    {
                        _cachedWeather = weather;
                        _lastFetchTime = DateTime.Now;
                        
                        ChillEnvPlugin.Log?.LogInfo($"🌤️ 天气解析成功: {weather}");
                        onComplete?.Invoke(weather);
                    }
                    else
                    {
                        ChillEnvPlugin.Log?.LogWarning("天气数据解析失败");
                        onComplete?.Invoke(null);
                    }
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogError($"解析天气数据异常: {ex.Message}");
                    onComplete?.Invoke(null);
                }
            }
        }

        private static WeatherInfo ParseWeatherJson(string json)
        {
            try
            {
                // 查找 "now": 部分
                int nowIndex = json.IndexOf("\"now\"");
                if (nowIndex < 0) return null;

                // 提取 code
                int code = ExtractIntValue(json, "\"code\":\"", "\"");
                
                // 提取 temperature  
                int temp = ExtractIntValue(json, "\"temperature\":\"", "\"");
                
                // 提取 text
                string text = ExtractStringValue(json, "\"text\":\"", "\"");

                if (string.IsNullOrEmpty(text)) return null;

                return new WeatherInfo
                {
                    Code = code,
                    Text = text,
                    Temperature = temp,
                    Condition = MapCodeToCondition(code),
                    UpdateTime = DateTime.Now
                };
            }
            catch
            {
                return null;
            }
        }

        private static int ExtractIntValue(string json, string prefix, string suffix)
        {
            int start = json.IndexOf(prefix);
            if (start < 0) return 0;
            start += prefix.Length;
            
            int end = json.IndexOf(suffix, start);
            if (end < 0) return 0;
            
            string value = json.Substring(start, end - start);
            int result;
            int.TryParse(value, out result);
            return result;
        }

        private static string ExtractStringValue(string json, string prefix, string suffix)
        {
            int start = json.IndexOf(prefix);
            if (start < 0) return null;
            start += prefix.Length;
            
            int end = json.IndexOf(suffix, start);
            if (end < 0) return null;
            
            return json.Substring(start, end - start);
        }

        private static WeatherCondition MapCodeToCondition(int code)
        {
            // 晴天: 0-3
            if (code >= 0 && code <= 3)
                return WeatherCondition.Clear;

            // 多云/阴天: 4-9
            if (code >= 4 && code <= 9)
                return WeatherCondition.Cloudy;

            // 雨天: 10-20 (阵雨、雷阵雨、各种雨、冻雨、雨夹雪)
            if (code >= 10 && code <= 20)
                return WeatherCondition.Rainy;

            // 雪天: 21-25
            if (code >= 21 && code <= 25)
                return WeatherCondition.Snowy;

            // 浮尘/扬沙/沙尘暴: 26-29 -> 当作阴天
            if (code >= 26 && code <= 29)
                return WeatherCondition.Cloudy;

            // 雾/霾: 30-31
            if (code >= 30 && code <= 31)
                return WeatherCondition.Foggy;

            // 风: 32-36 -> 当作阴天
            if (code >= 32 && code <= 36)
                return WeatherCondition.Cloudy;

            // 冷/热: 37-38 -> 晴天
            if (code >= 37 && code <= 38)
                return WeatherCondition.Clear;

            return WeatherCondition.Unknown;
        }
    }

    public class AutoEnvRunner : MonoBehaviour
    {
        private float _nextTickTime;
        private EnvironmentType? _lastAppliedEnv;
        private bool _isFetching;

        // 互斥组1 - 基础环境（必须有且只有一个）
        private static readonly EnvironmentType[] BaseEnvironments = new[]
        {
            EnvironmentType.Day,
            EnvironmentType.Sunset,
            EnvironmentType.Night,
            EnvironmentType.Cloudy
        };

        // 互斥组2 - 降水天气（最多一个，可以没有）
        private static readonly EnvironmentType[] PrecipitationWeathers = new[]
        {
            EnvironmentType.LightRain,
            EnvironmentType.HeavyRain,
            EnvironmentType.ThunderRain
        };

        private static readonly EnvironmentType[] MainEnvironments = new[]
        {
            EnvironmentType.Day,
            EnvironmentType.Sunset,
            EnvironmentType.Night,
            EnvironmentType.Cloudy,
            EnvironmentType.LightRain,
            EnvironmentType.HeavyRain,
            EnvironmentType.ThunderRain
        };

        private void Start()
        {
            _nextTickTime = Time.time + 15f;
            ChillEnvPlugin.Log?.LogInfo("Runner 启动，15秒后首次同步");

            // 显示配置状态
            bool weatherEnabled = ChillEnvPlugin.Cfg_EnableWeatherSync.Value;
            string apiKey = ChillEnvPlugin.Cfg_SeniverseKey.Value;
            string location = ChillEnvPlugin.Cfg_Location.Value;

            if (weatherEnabled && !string.IsNullOrEmpty(apiKey))
            {
                ChillEnvPlugin.Log?.LogInfo($"天气同步已启用，城市: {location}");
            }
            else
            {
                ChillEnvPlugin.Log?.LogInfo("天气同步未启用，仅按时间同步");
            }
        }

        private void Update()
        {
            if (!ChillEnvPlugin.Initialized || EnvRegistry.Count == 0)
                return;

            // F9: 手动同步
            if (Input.GetKeyDown(KeyCode.F9))
            {
                ChillEnvPlugin.Log?.LogInfo("F9: 手动触发同步");
                TriggerSync();
            }

            // F8: 显示状态
            if (Input.GetKeyDown(KeyCode.F8))
            {
                ShowStatus();
            }

            // F7: 手动刷新天气
            if (Input.GetKeyDown(KeyCode.F7))
            {
                ChillEnvPlugin.Log?.LogInfo("F7: 强制刷新天气");
                ForceRefreshWeather();
            }

            // 定时同步
            if (Time.time >= _nextTickTime)
            {
                int minutes = Mathf.Max(1, ChillEnvPlugin.Cfg_WeatherRefreshMinutes.Value);
                _nextTickTime = Time.time + (minutes * 60f);
                TriggerSync();
            }
        }

        private void ShowStatus()
        {
            var now = DateTime.Now;
            ChillEnvPlugin.Log?.LogInfo($"--- 状态 [{now:HH:mm:ss}] ---");
            ChillEnvPlugin.Log?.LogInfo($"插件记录: {_lastAppliedEnv}");

            var currentActive = GetCurrentActiveEnvironment();
            ChillEnvPlugin.Log?.LogInfo($"游戏实际: {currentActive}");

            var cached = WeatherService.CachedWeather;
            if (cached != null)
            {
                ChillEnvPlugin.Log?.LogInfo($"缓存天气: {cached}");
            }
            else
            {
                ChillEnvPlugin.Log?.LogInfo("缓存天气: 无");
            }

            bool weatherEnabled = ChillEnvPlugin.Cfg_EnableWeatherSync.Value;
            ChillEnvPlugin.Log?.LogInfo($"天气同步: {(weatherEnabled ? "已启用" : "未启用")}");
        }

        private void ForceRefreshWeather()
        {
            if (_isFetching) return;

            string apiKey = ChillEnvPlugin.Cfg_SeniverseKey.Value;
            string location = ChillEnvPlugin.Cfg_Location.Value;

            if (string.IsNullOrEmpty(apiKey))
            {
                ChillEnvPlugin.Log?.LogWarning("API Key 未配置");
                return;
            }

            _isFetching = true;
            StartCoroutine(WeatherService.FetchWeather(apiKey, location, (weather) =>
            {
                _isFetching = false;
                if (weather != null)
                {
                    ChillEnvPlugin.Log?.LogInfo($"天气刷新完成: {weather}");
                    ApplyEnvironment(weather);
                }
            }));
        }

        private void TriggerSync()
        {
            bool weatherEnabled = ChillEnvPlugin.Cfg_EnableWeatherSync.Value;
            string apiKey = ChillEnvPlugin.Cfg_SeniverseKey.Value;

            if (weatherEnabled && !string.IsNullOrEmpty(apiKey) && !_isFetching)
            {
                // 使用天气API同步
                string location = ChillEnvPlugin.Cfg_Location.Value;
                _isFetching = true;

                StartCoroutine(WeatherService.FetchWeather(apiKey, location, (weather) =>
                {
                    _isFetching = false;
                    if (weather != null)
                    {
                        ApplyEnvironment(weather);
                    }
                    else
                    {
                        // API失败，回退到时间同步
                        ChillEnvPlugin.Log?.LogWarning("天气API失败，回退到时间同步");
                        ApplyTimeBasedEnvironment();
                    }
                }));
            }
            else
            {
                // 仅按时间同步
                ApplyTimeBasedEnvironment();
            }
        }

        private EnvironmentType? GetCurrentActiveEnvironment()
        {
            try
            {
                var windowViewDic = SaveDataManager.Instance.WindowViewDic;

                foreach (var envType in MainEnvironments)
                {
                    WindowViewType windowType;
                    if (Enum.TryParse(envType.ToString(), out windowType))
                    {
                        if (windowViewDic.ContainsKey(windowType) && windowViewDic[windowType].IsActive)
                        {
                            return envType;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"获取当前环境失败: {ex.Message}");
            }

            return null;
        }

        private bool IsEnvironmentActive(EnvironmentType envType)
        {
            try
            {
                var windowViewDic = SaveDataManager.Instance.WindowViewDic;
                WindowViewType windowType;
                if (Enum.TryParse(envType.ToString(), out windowType))
                {
                    return windowViewDic.ContainsKey(windowType) && windowViewDic[windowType].IsActive;
                }
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"检查环境状态失败: {ex.Message}");
            }
            return false;
        }

        private void ActivateEnvironment(EnvironmentType envType)
        {
            if (EnvRegistry.TryGet(envType, out var ctrl))
            {
                try
                {
                    ctrl.ChangeWindowView(ChangeType.Activate);
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogError($"激活 [{envType}] 失败: {ex}");
                }
            }
        }

        private void DeactivateEnvironment(EnvironmentType envType)
        {
            if (EnvRegistry.TryGet(envType, out var ctrl))
            {
                try
                {
                    ctrl.ChangeWindowView(ChangeType.Deactivate);
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogWarning($"关闭 [{envType}] 失败: {ex}");
                }
            }
        }

        private void ActivateEnvironmentWithMutex(EnvironmentType target)
        {
            // 判断目标属于哪个互斥组
            bool isBaseEnv = System.Array.IndexOf(BaseEnvironments, target) >= 0;
            bool isPrecipitation = System.Array.IndexOf(PrecipitationWeathers, target) >= 0;
            
            if (isBaseEnv)
            {
                // 关闭其他基础环境
                foreach (var env in BaseEnvironments)
                {
                    if (env != target && IsEnvironmentActive(env))
                    {
                        DeactivateEnvironment(env);
                        ChillEnvPlugin.Log?.LogInfo($"[互斥] 关闭基础环境: {env}");
                    }
                }
            }
            
            if (isPrecipitation)
            {
                // 关闭其他降水天气
                foreach (var env in PrecipitationWeathers)
                {
                    if (env != target && IsEnvironmentActive(env))
                    {
                        DeactivateEnvironment(env);
                        ChillEnvPlugin.Log?.LogInfo($"[互斥] 关闭降水天气: {env}");
                    }
                }
            }
            
            // 激活目标
            if (!IsEnvironmentActive(target))
            {
                ActivateEnvironment(target);
                ChillEnvPlugin.Log?.LogInfo($"[激活] {target}");
            }
        }

        private void ClearAllWeatherEffects()
        {
            // 晴天 = 只保留基础环境中的时间类，关闭 Cloudy 和所有降水
            if (IsEnvironmentActive(EnvironmentType.Cloudy))
            {
                DeactivateEnvironment(EnvironmentType.Cloudy);
                ChillEnvPlugin.Log?.LogInfo("[晴天] 关闭多云");
            }
            
            foreach (var env in PrecipitationWeathers)
            {
                if (IsEnvironmentActive(env))
                {
                    DeactivateEnvironment(env);
                    ChillEnvPlugin.Log?.LogInfo($"[晴天] 关闭降水: {env}");
                }
            }
        }

        private EnvironmentType GetTimeBasedEnvironment()
        {
            DateTime now = DateTime.Now;
            TimeSpan currentTime = now.TimeOfDay;

            TimeSpan sunrise, sunset;
            if (!TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunriseTime.Value, out sunrise))
                sunrise = new TimeSpan(6, 30, 0);
            if (!TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunsetTime.Value, out sunset))
                sunset = new TimeSpan(18, 30, 0);

            TimeSpan sunsetStart = sunset.Subtract(TimeSpan.FromHours(1));
            TimeSpan sunsetEnd = sunset.Add(TimeSpan.FromMinutes(30));

            if (currentTime >= sunrise && currentTime < sunsetStart)
            {
                return EnvironmentType.Day;
            }
            else if (currentTime >= sunsetStart && currentTime < sunsetEnd)
            {
                return EnvironmentType.Sunset;
            }
            else
            {
                return EnvironmentType.Night;
            }
        }

        private void ApplyEnvironment(WeatherInfo weather)
        {
            DateTime now = DateTime.Now;
            ChillEnvPlugin.Log?.LogInfo($"[天气决策] {weather.Text}(Code:{weather.Code}) + {now:HH:mm}");
            
            // 1. 先确定基础时间环境
            EnvironmentType timeEnv = GetTimeBasedEnvironment();
            
            // 2. 根据天气代码决定天气效果
            int code = weather.Code;
            
            if (code >= 0 && code <= 3) 
            {
                // 晴/少云 - 只设置时间，清除天气效果
                ChillEnvPlugin.Log?.LogInfo("[天气决策] 晴天，清除所有天气效果");
                ClearAllWeatherEffects();
                ActivateEnvironmentWithMutex(timeEnv);
            }
            else if (code >= 4 && code <= 9)
            {
                // 多云/阴天 - Cloudy 会替代时间环境
                ChillEnvPlugin.Log?.LogInfo("[天气决策] 阴天/多云");
                ClearAllWeatherEffects(); // 先清降水
                ActivateEnvironmentWithMutex(EnvironmentType.Cloudy);
            }
            else if (code >= 10 && code <= 12)
            {
                // 小雨/阵雨
                ChillEnvPlugin.Log?.LogInfo("[天气决策] 小雨/阵雨");
                ActivateEnvironmentWithMutex(EnvironmentType.Cloudy);
                ActivateEnvironmentWithMutex(EnvironmentType.LightRain);
            }
            else if (code >= 13 && code <= 14)
            {
                // 大雨/暴雨
                ChillEnvPlugin.Log?.LogInfo("[天气决策] 大雨/暴雨");
                ActivateEnvironmentWithMutex(EnvironmentType.Cloudy);
                ActivateEnvironmentWithMutex(EnvironmentType.HeavyRain);
            }
            else if (code >= 15 && code <= 18)
            {
                // 雷阵雨
                ChillEnvPlugin.Log?.LogInfo("[天气决策] 雷阵雨");
                ActivateEnvironmentWithMutex(EnvironmentType.Cloudy);
                ActivateEnvironmentWithMutex(EnvironmentType.ThunderRain);
            }
            else if (code >= 21 && code <= 25)
            {
                // 雪天 - 保留雪天场景，不使用降水互斥组
                ChillEnvPlugin.Log?.LogInfo("[天气决策] 雪天（暂不支持，回退到时间环境）");
                ClearAllWeatherEffects();
                ActivateEnvironmentWithMutex(timeEnv);
            }
            else
            {
                // 其他未知天气，保守处理
                ChillEnvPlugin.Log?.LogInfo($"[天气决策] 未知天气代码 {code}，使用默认时间环境");
                ClearAllWeatherEffects();
                ActivateEnvironmentWithMutex(timeEnv);
            }
            
            ChillEnvPlugin.Log?.LogInfo("✅ 天气切换完成");
        }

        private void ApplyTimeBasedEnvironment()
        {
            DateTime now = DateTime.Now;
            ChillEnvPlugin.Log?.LogInfo($"[时间决策] {now:HH:mm}");

            EnvironmentType targetEnv = GetTimeBasedEnvironment();
            
            ChillEnvPlugin.Log?.LogInfo($"[时间决策] -> {targetEnv}");
            ClearAllWeatherEffects();
            ActivateEnvironmentWithMutex(targetEnv);
            ChillEnvPlugin.Log?.LogInfo("✅ 时间切换完成");
        }

        private void SwitchToEnvironment(EnvironmentType targetEnv)
        {
            var currentActive = GetCurrentActiveEnvironment();

            if (currentActive.HasValue && currentActive.Value == targetEnv)
            {
                ChillEnvPlugin.Log?.LogInfo("目标环境已激活，跳过");
                _lastAppliedEnv = targetEnv;
                return;
            }

            // 关闭当前环境
            if (currentActive.HasValue && EnvRegistry.TryGet(currentActive.Value, out var oldCtrl))
            {
                try
                {
                    ChillEnvPlugin.Log?.LogInfo($"关闭 [{currentActive.Value}]");
                    oldCtrl.ChangeWindowView(ChangeType.Deactivate);
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogWarning($"关闭失败: {ex.Message}");
                }
            }

            // 激活目标环境
            if (EnvRegistry.TryGet(targetEnv, out var ctrl))
            {
                try
                {
                    ChillEnvPlugin.Log?.LogInfo($"激活 [{targetEnv}]");
                    ctrl.ChangeWindowView(ChangeType.Activate);
                    _lastAppliedEnv = targetEnv;
                    ChillEnvPlugin.Log?.LogInfo("✅ 切换成功");
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogError($"激活失败: {ex}");
                }
            }
            else
            {
                ChillEnvPlugin.Log?.LogWarning($"找不到 [{targetEnv}] 控制器");
            }
        }
    }

    internal static class EnvRegistry
    {
        private static readonly Dictionary<EnvironmentType, EnviromentController> _map = new Dictionary<EnvironmentType, EnviromentController>();
        internal static int Count => _map.Count;

        internal static void Register(EnvironmentType type, EnviromentController ctrl)
        {
            if (ctrl != null && !_map.ContainsKey(type))
            {
                _map[type] = ctrl;
            }
        }

        internal static bool TryGet(EnvironmentType type, out EnviromentController ctrl)
        {
            return _map.TryGetValue(type, out ctrl);
        }
    }

    [HarmonyPatch(typeof(UnlockItemService), "Setup")]
    internal static class UnlockServicePatch
    {
        static void Postfix(UnlockItemService __instance)
        {
            ChillEnvPlugin.UnlockItemServiceInstance = __instance;
            ChillEnvPlugin.TryInitializeOnce(__instance);
        }
    }

    [HarmonyPatch(typeof(EnviromentController), "Setup")]
    internal static class EnvControllerPatch
    {
        static void Postfix(EnviromentController __instance)
        {
            EnvRegistry.Register(__instance.EnvironmentType, __instance);
        }
    }
}