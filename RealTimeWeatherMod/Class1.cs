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
    [BepInPlugin("chillwithyou.envsync", "Chill Env Sync", "3.5.0")]
    public class ChillEnvPlugin : BaseUnityPlugin
    {
        internal static ChillEnvPlugin Instance;
        internal static ManualLogSource Log;
        internal static UnlockItemService UnlockItemServiceInstance;

        internal static object WindowViewServiceInstance;
        internal static MethodInfo ChangeWeatherMethod;

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

            Log.LogInfo("【3.5.0】启动 - 双按钮逻辑适配 (模拟点击MainIcon)");

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

        internal static void CallServiceChangeWeather(EnvironmentType envType)
        {
            if (WindowViewServiceInstance == null || ChangeWeatherMethod == null) return;

            try
            {
                var parameters = ChangeWeatherMethod.GetParameters();
                if (parameters.Length == 0) return;

                Type windowViewEnumType = parameters[0].ParameterType;
                object enumValue = null;

                try
                {
                    enumValue = Enum.Parse(windowViewEnumType, envType.ToString());
                }
                catch { return; }

                ChangeWeatherMethod.Invoke(WindowViewServiceInstance, new object[] { enumValue });
            }
            catch (Exception ex)
            {
                Log?.LogError($"调用 Service 失败: {ex.Message}");
            }
        }

        // 【新增】模拟点击 MainIcon (按钮A)
        internal static void SimulateClickMainIcon(EnviromentController ctrl)
        {
            if (ctrl == null) return;
            try
            {
                // 反射调用 OnClickButtonMainIcon
                MethodInfo clickMethod = ctrl.GetType().GetMethod("OnClickButtonMainIcon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (clickMethod != null)
                {
                    clickMethod.Invoke(ctrl, null);
                    // Log?.LogInfo($"[模拟点击] {ctrl.EnvironmentType} MainIcon");
                }
                else
                {
                    Log?.LogWarning($"[警告] 未找到 OnClickButtonMainIcon 方法: {ctrl.EnvironmentType}");
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"模拟点击失败: {ex.Message}");
            }
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
                }
                Log?.LogInfo($"✅ 已解锁 {count} 个装饰品");
            }
            catch (Exception ex)
            {
                Log?.LogError("装饰品解锁异常: " + ex.Message);
            }
        }
    }

    public enum WeatherCondition
    {
        Clear, Cloudy, Rainy, Snowy, Foggy, Unknown
    }

    public class WeatherInfo
    {
        public WeatherCondition Condition;
        public int Temperature;
        public string Text;
        public int Code;
        public DateTime UpdateTime;
        public override string ToString() => $"{Text}({Condition}), {Temperature}°C, Code={Code}";
    }

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

        private static readonly Dictionary<string, EnvironmentType> WeatherToEnvironment = new Dictionary<string, EnvironmentType>(StringComparer.OrdinalIgnoreCase)
        {
            {"Clear", EnvironmentType.Day},
            {"Clouds", EnvironmentType.Cloudy},
            {"Drizzle", EnvironmentType.LightRain},
            {"Rain", EnvironmentType.HeavyRain},
            {"Thunderstorm", EnvironmentType.ThunderRain},
            {"Snow", EnvironmentType.Snow},
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
                try
                {
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
                int nowIndex = json.IndexOf("\"now\"");
                if (nowIndex < 0) return null;

                int code = ExtractIntValue(json, "\"code\":\"", "\"");
                int temp = ExtractIntValue(json, "\"temperature\":\"", "\"");
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
            catch { return null; }
        }

        private static int ExtractIntValue(string json, string prefix, string suffix)
        {
            int start = json.IndexOf(prefix);
            if (start < 0) return 0;
            start += prefix.Length;
            int end = json.IndexOf(suffix, start);
            if (end < 0) return 0;
            string value = json.Substring(start, end - start);
            int.TryParse(value, out int result);
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
            if (code >= 0 && code <= 3) return WeatherCondition.Clear;
            if (code >= 4 && code <= 9) return WeatherCondition.Cloudy;
            if (code >= 10 && code <= 20) return WeatherCondition.Rainy;
            if (code >= 21 && code <= 25) return WeatherCondition.Snowy;
            if (code >= 26 && code <= 29) return WeatherCondition.Cloudy;
            if (code >= 30 && code <= 31) return WeatherCondition.Foggy;
            if (code >= 32 && code <= 36) return WeatherCondition.Cloudy;
            if (code >= 37 && code <= 38) return WeatherCondition.Clear;
            return WeatherCondition.Unknown;
        }
    }

    public class AutoEnvRunner : MonoBehaviour
    {
        private float _nextTickTime;
        private EnvironmentType? _lastAppliedEnv;
        private bool _isFetching;

        private static readonly EnvironmentType[] BaseEnvironments = new[]
        {
            EnvironmentType.Day, EnvironmentType.Sunset, EnvironmentType.Night, EnvironmentType.Cloudy
        };

        private static readonly EnvironmentType[] PrecipitationWeathers = new[]
        {
            EnvironmentType.ThunderRain, EnvironmentType.HeavyRain, EnvironmentType.LightRain, EnvironmentType.Snow
        };

        private static readonly EnvironmentType[] MainEnvironments = new[]
        {
            EnvironmentType.Day, EnvironmentType.Sunset, EnvironmentType.Night, EnvironmentType.Cloudy,
            EnvironmentType.LightRain, EnvironmentType.HeavyRain, EnvironmentType.ThunderRain, EnvironmentType.Snow
        };

        private void Start()
        {
            _nextTickTime = Time.time + 15f;
            ChillEnvPlugin.Log?.LogInfo("Runner 启动，15秒后首次同步");
        }

        private void Update()
        {
            if (!ChillEnvPlugin.Initialized || EnvRegistry.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.F9)) TriggerSync();
            if (Input.GetKeyDown(KeyCode.F8)) ShowStatus();
            if (Input.GetKeyDown(KeyCode.F7)) ForceRefreshWeather();

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
        }

        private void ForceRefreshWeather()
        {
            if (_isFetching) return;
            string apiKey = ChillEnvPlugin.Cfg_SeniverseKey.Value;
            string location = ChillEnvPlugin.Cfg_Location.Value;
            if (string.IsNullOrEmpty(apiKey)) return;

            _isFetching = true;
            StartCoroutine(WeatherService.FetchWeather(apiKey, location, (weather) =>
            {
                _isFetching = false;
                if (weather != null) ApplyEnvironment(weather);
            }));
        }

        private void TriggerSync()
        {
            bool weatherEnabled = ChillEnvPlugin.Cfg_EnableWeatherSync.Value;
            string apiKey = ChillEnvPlugin.Cfg_SeniverseKey.Value;

            if (weatherEnabled && !string.IsNullOrEmpty(apiKey) && !_isFetching)
            {
                string location = ChillEnvPlugin.Cfg_Location.Value;
                _isFetching = true;
                StartCoroutine(WeatherService.FetchWeather(apiKey, location, (weather) =>
                {
                    _isFetching = false;
                    if (weather != null) ApplyEnvironment(weather);
                    else ApplyTimeBasedEnvironment();
                }));
            }
            else
            {
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
                        if (windowViewDic.ContainsKey(windowType) && windowViewDic[windowType].IsActive) return envType;
                    }
                }
            }
            catch { }
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
            catch { }
            return false;
        }

        private void ActivateEnvironment(EnvironmentType envType)
        {
            if (EnvRegistry.TryGet(envType, out var ctrl))
            {
                // 如果当前没开，就点一下 MainIcon (它是Toggle)
                if (!IsEnvironmentActive(envType))
                {
                    ChillEnvPlugin.SimulateClickMainIcon(ctrl);
                    // 补刀：如果模拟点击失败（比如方法名不对），再尝试 Service
                    ChillEnvPlugin.CallServiceChangeWeather(envType);
                }
            }
        }

        private void DeactivateEnvironment(EnvironmentType envType)
        {
            if (EnvRegistry.TryGet(envType, out var ctrl))
            {
                // 如果当前开着，就点一下 MainIcon (Toggle 会把它关掉)
                if (IsEnvironmentActive(envType))
                {
                    ChillEnvPlugin.SimulateClickMainIcon(ctrl);

                    // 特殊处理：如果是 3.3.0 那样 UI 没关掉，可能需要手动 Service 修正
                    // 但通常 MainIcon 会处理好一切
                    // 如果模拟点击成功，SaveData 和 Service 应该会自动更新
                }
            }
        }

        private void ActivateEnvironmentWithMutex(EnvironmentType target)
        {
            // 策略：
            // 1. 先关闭互斥组里的其他环境 (通过模拟点击)
            // 2. 再开启目标环境 (通过模拟点击)
            // 3. 最后用 Service 兜底 (确保天气层同步)

            bool isBaseEnv = Array.IndexOf(BaseEnvironments, target) >= 0;
            bool isPrecipitation = Array.IndexOf(PrecipitationWeathers, target) >= 0;

            if (isBaseEnv)
            {
                foreach (var env in BaseEnvironments)
                    if (env != target) DeactivateEnvironment(env);
            }

            if (isPrecipitation)
            {
                foreach (var env in PrecipitationWeathers)
                    if (env != target) DeactivateEnvironment(env);
            }

            // 开启目标
            ActivateEnvironment(target);
            ChillEnvPlugin.Log?.LogInfo($"[决策] 切换至 {target}");

            // 兜底同步 Service (防止点击没触发天气系统)
            ChillEnvPlugin.CallServiceChangeWeather(target);
        }

        private void ClearAllWeatherEffects()
        {
            if (IsEnvironmentActive(EnvironmentType.Cloudy)) DeactivateEnvironment(EnvironmentType.Cloudy);

            foreach (var env in PrecipitationWeathers)
            {
                if (IsEnvironmentActive(env))
                {
                    DeactivateEnvironment(env);
                    ChillEnvPlugin.Log?.LogInfo($"[清除] {env}");
                }
            }
        }

        private EnvironmentType GetTimeBasedEnvironment()
        {
            DateTime now = DateTime.Now;
            TimeSpan currentTime = now.TimeOfDay;
            TimeSpan sunrise, sunset;
            if (!TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunriseTime.Value, out sunrise)) sunrise = new TimeSpan(6, 30, 0);
            if (!TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunsetTime.Value, out sunset)) sunset = new TimeSpan(18, 30, 0);

            TimeSpan sunsetStart = sunset.Subtract(TimeSpan.FromHours(1));
            TimeSpan sunsetEnd = sunset.Add(TimeSpan.FromMinutes(30));

            if (currentTime >= sunrise && currentTime < sunsetStart) return EnvironmentType.Day;
            else if (currentTime >= sunsetStart && currentTime < sunsetEnd) return EnvironmentType.Sunset;
            else return EnvironmentType.Night;
        }

        private void ApplyEnvironment(WeatherInfo weather)
        {
            DateTime now = DateTime.Now;
            ChillEnvPlugin.Log?.LogInfo($"[API] {weather.Text}(Code:{weather.Code})");

            EnvironmentType timeEnv = GetTimeBasedEnvironment();
            int code = weather.Code;

            if (code >= 0 && code <= 3) // 晴天
            {
                ClearAllWeatherEffects();
                ActivateEnvironmentWithMutex(timeEnv);
            }
            else if (code >= 4 && code <= 9) // 阴天
            {
                ClearAllWeatherEffects();
                ActivateEnvironmentWithMutex(EnvironmentType.Cloudy);
            }
            else if (code >= 10 && code <= 12) // 小雨
            {
                ActivateEnvironmentWithMutex(EnvironmentType.Cloudy);
                ActivateEnvironmentWithMutex(EnvironmentType.LightRain);
            }
            else if (code >= 13 && code <= 14) // 大雨
            {
                ActivateEnvironmentWithMutex(EnvironmentType.Cloudy);
                ActivateEnvironmentWithMutex(EnvironmentType.HeavyRain);
            }
            else if (code >= 15 && code <= 18) // 雷雨
            {
                ActivateEnvironmentWithMutex(EnvironmentType.Cloudy);
                ActivateEnvironmentWithMutex(EnvironmentType.ThunderRain);
            }
            else if (code >= 21 && code <= 25) // 雪
            {
                ActivateEnvironmentWithMutex(EnvironmentType.Cloudy);
                ActivateEnvironmentWithMutex(EnvironmentType.Snow);
            }
            else
            {
                ClearAllWeatherEffects();
                ActivateEnvironmentWithMutex(timeEnv);
            }
            ChillEnvPlugin.Log?.LogInfo("✅ 完成");
        }

        private void ApplyTimeBasedEnvironment()
        {
            EnvironmentType targetEnv = GetTimeBasedEnvironment();
            ClearAllWeatherEffects();
            ActivateEnvironmentWithMutex(targetEnv);
        }
    }

    internal static class EnvRegistry
    {
        private static readonly Dictionary<EnvironmentType, EnviromentController> _map = new Dictionary<EnvironmentType, EnviromentController>();
        internal static int Count => _map.Count;

        internal static void Register(EnvironmentType type, EnviromentController ctrl)
        {
            if (ctrl != null && !_map.ContainsKey(type)) _map[type] = ctrl;
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

    [HarmonyPatch(typeof(FacilityEnviroment), "Setup")]
    internal static class FacilityEnvPatch
    {
        static void Postfix(FacilityEnviroment __instance)
        {
            try
            {
                FieldInfo field = typeof(FacilityEnviroment).GetField("_windowViewService", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    object service = field.GetValue(__instance);
                    if (service != null)
                    {
                        ChillEnvPlugin.WindowViewServiceInstance = service;
                        ChillEnvPlugin.ChangeWeatherMethod = service.GetType().GetMethod("ChangeWeatherAndTime", BindingFlags.Instance | BindingFlags.Public);

                        if (ChillEnvPlugin.ChangeWeatherMethod != null)
                        {
                            ChillEnvPlugin.Log?.LogInfo("✅ 成功捕获 WindowViewService.ChangeWeatherAndTime");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogError($"捕获 Service 失败: {ex}");
            }
        }
    }
}