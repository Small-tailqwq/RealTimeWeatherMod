using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using Bulbul;
using TMPro;

namespace ChillWithYou.EnvSync
{
    [BepInPlugin("chillwithyou.envsync", "Chill Env Sync", "5.0.1")]
    public class ChillEnvPlugin : BaseUnityPlugin
    {
        internal static ChillEnvPlugin Instance;
        internal static ManualLogSource Log;
        internal static UnlockItemService UnlockItemServiceInstance;

        internal static object WindowViewServiceInstance;
        internal static MethodInfo ChangeWeatherMethod;

        internal static string UIWeatherString = "";

        internal static bool Initialized;

        // --- 配置项 ---
        internal static ConfigEntry<int> Cfg_WeatherRefreshMinutes;
        internal static ConfigEntry<string> Cfg_SunriseTime;
        internal static ConfigEntry<string> Cfg_SunsetTime;
        internal static ConfigEntry<string> Cfg_SeniverseKey;
        internal static ConfigEntry<string> Cfg_Location;
        internal static ConfigEntry<bool> Cfg_EnableWeatherSync;
        internal static ConfigEntry<bool> Cfg_UnlockEnvironments;
        internal static ConfigEntry<bool> Cfg_UnlockDecorations;

        // UI 配置
        internal static ConfigEntry<bool> Cfg_ShowWeatherOnUI;
        internal static ConfigEntry<bool> Cfg_EnableEasterEggs;

        // 调试配置
        internal static ConfigEntry<bool> Cfg_DebugMode;
        internal static ConfigEntry<int> Cfg_DebugCode;
        internal static ConfigEntry<int> Cfg_DebugTemp;
        internal static ConfigEntry<string> Cfg_DebugText;

        private static AutoEnvRunner _runner;
        private static GameObject _runnerGO;
        private static SceneryAutomationSystem _scenerySystem;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Log.LogInfo("【5.0.1】启动 - 枚举ID实装 (彩蛋系统就绪)");

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

                _scenerySystem = _runnerGO.AddComponent<SceneryAutomationSystem>();
                _scenerySystem.enabled = true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Runner 创建失败: {ex}");
            }
        }

        private void InitConfig()
        {
            Cfg_WeatherRefreshMinutes = Config.Bind("WeatherSync", "RefreshMinutes", 30, "天气API刷新间隔(分钟)");
            Cfg_SunriseTime = Config.Bind("TimeConfig", "Sunrise", "06:30", "日出时间");
            Cfg_SunsetTime = Config.Bind("TimeConfig", "Sunset", "18:30", "日落时间");

            Cfg_EnableWeatherSync = Config.Bind("WeatherAPI", "EnableWeatherSync", false, "是否启用天气API同步（需要填写API Key）");
            Cfg_SeniverseKey = Config.Bind("WeatherAPI", "SeniverseKey", "", "心知天气 API Key");
            Cfg_Location = Config.Bind("WeatherAPI", "Location", "beijing", "城市名称（拼音或中文，如 beijing、上海、ip 表示自动定位）");

            Cfg_UnlockEnvironments = Config.Bind("Unlock", "UnlockAllEnvironments", true, "是否自动解锁所有环境场景");
            Cfg_UnlockDecorations = Config.Bind("Unlock", "UnlockAllDecorations", true, "是否自动解锁所有装饰品");

            Cfg_ShowWeatherOnUI = Config.Bind("UI", "ShowWeatherOnDate", true, "是否在游戏日期栏显示实时天气和温度");
            Cfg_EnableEasterEggs = Config.Bind("Automation", "EnableSeasonalEasterEggs", true, "启用季节性彩蛋与环境音效自动托管");

            Cfg_DebugMode = Config.Bind("Debug", "EnableDebugMode", false, "是否开启调试模式");
            Cfg_DebugCode = Config.Bind("Debug", "SimulatedCode", 1, "模拟天气代码");
            Cfg_DebugTemp = Config.Bind("Debug", "SimulatedTemp", 25, "模拟温度");
            Cfg_DebugText = Config.Bind("Debug", "SimulatedText", "DebugWeather", "模拟天气描述");
        }

        internal static void TryInitializeOnce(UnlockItemService svc)
        {
            if (Initialized || svc == null) return;

            if (Cfg_UnlockEnvironments.Value) ForceUnlockAllEnvironments(svc);
            if (Cfg_UnlockDecorations.Value) ForceUnlockAllDecorations(svc);

            Initialized = true;
            Log?.LogInfo("初始化完成");
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

        internal static void SimulateClickMainIcon(EnviromentController ctrl)
        {
            if (ctrl == null) return;
            try
            {
                MethodInfo clickMethod = ctrl.GetType().GetMethod("OnClickButtonMainIcon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (clickMethod != null)
                {
                    UserInteractionPatch.IsSimulatingClick = true;
                    clickMethod.Invoke(ctrl, null);
                    UserInteractionPatch.IsSimulatingClick = false;
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
            catch { }
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
            catch { }
        }
    }

    // ====================================================================================
    // 环境与彩蛋自动托管系统
    // ====================================================================================
    public class SceneryAutomationSystem : MonoBehaviour
    {
        private HashSet<EnvironmentType> _autoEnabledMods = new HashSet<EnvironmentType>();
        public static HashSet<EnvironmentType> UserInteractedMods = new HashSet<EnvironmentType>();

        private class SceneryRule
        {
            public EnvironmentType EnvType;
            public Func<bool> Condition;
            public string Name;
        }

        private List<SceneryRule> _rules = new List<SceneryRule>();
        private float _checkTimer = 0f;
        private const float CheckInterval = 5f;

        private const EnvironmentType Env_Fireworks = EnvironmentType.Fireworks;
        private const EnvironmentType Env_Cooking = EnvironmentType.CookSimmer;
        private const EnvironmentType Env_AC = EnvironmentType.RoomNoise;
        private const EnvironmentType Env_Sakura = EnvironmentType.Sakura;
        private const EnvironmentType Env_Cicada = EnvironmentType.Chicada;
        private const EnvironmentType Env_DeepSea = EnvironmentType.DeepSea;

        private void Start()
        {
            InitializeRules();
        }

        private void InitializeRules()
        {
            // 1. 烟花 (Fireworks): 新年/春节 + 夜晚
            _rules.Add(new SceneryRule
            {
                Name = "Fireworks",
                EnvType = Env_Fireworks,
                Condition = () =>
                {
                    DateTime now = DateTime.Now;
                    bool isNight = IsNight();
                    bool isNewYear = (now.Month == 1 && now.Day == 1);
                    bool isSpringFestival = (now.Month == 1 || now.Month == 2);
                    return isNight && (isNewYear || isSpringFestival);
                }
            });

            // 2. 做饭音效 (CookSimmer): 饭点
            _rules.Add(new SceneryRule
            {
                Name = "CookingAudio",
                EnvType = Env_Cooking,
                Condition = () =>
                {
                    int h = DateTime.Now.Hour;
                    int m = DateTime.Now.Minute;
                    double time = h + m / 60.0;
                    return (time >= 11.5 && time <= 12.5) || (time >= 17.5 && time <= 18.5);
                }
            });

            // 3. 空调音效 (RoomNoise): 太热或太冷
            _rules.Add(new SceneryRule
            {
                Name = "AC_Audio",
                EnvType = Env_AC,
                Condition = () =>
                {
                    var w = WeatherService.CachedWeather;
                    if (w == null) return false;
                    return w.Temperature > 30 || w.Temperature < 5;
                }
            });

            // 4. 樱花 (Sakura): 春天 + 白天 + 好天气
            _rules.Add(new SceneryRule
            {
                Name = "Sakura",
                EnvType = Env_Sakura,
                Condition = () =>
                {
                    return GetSeason() == Season.Spring && IsDay() && IsGoodWeather();
                }
            });

            // 5. 蝉鸣 (Chicada): 夏天 + 白天 + 好天气
            _rules.Add(new SceneryRule
            {
                Name = "Cicadas",
                EnvType = Env_Cicada,
                Condition = () =>
                {
                    return GetSeason() == Season.Summer && IsDay() && IsGoodWeather();
                }
            });
        }

        private void Update()
        {
            if (!ChillEnvPlugin.Cfg_EnableEasterEggs.Value) return;
            if (!ChillEnvPlugin.Initialized) return;

            _checkTimer += Time.deltaTime;
            if (_checkTimer >= CheckInterval)
            {
                _checkTimer = 0f;
                RunAutomationLogic();
            }
        }

        private void RunAutomationLogic()
        {
            // 深海优先
            if (IsEnvActive(Env_DeepSea))
            {
                CleanupAllAutoMods();
                return;
            }

            // Step 1: Cleanup
            List<EnvironmentType> toRemove = new List<EnvironmentType>();
            foreach (var envType in _autoEnabledMods)
            {
                if (UserInteractedMods.Contains(envType))
                {
                    toRemove.Add(envType);
                    continue;
                }

                var rule = _rules.Find(r => r.EnvType == envType);
                if (rule != null)
                {
                    if (!rule.Condition())
                    {
                        DisableMod(envType);
                        toRemove.Add(envType);
                        ChillEnvPlugin.Log?.LogInfo($"[自动托管] 条件失效，关闭: {rule.Name}");
                    }
                }
            }
            foreach (var rm in toRemove) _autoEnabledMods.Remove(rm);

            // Step 2: Trigger
            foreach (var rule in _rules)
            {
                if (UserInteractedMods.Contains(rule.EnvType)) continue;
                if (_autoEnabledMods.Contains(rule.EnvType)) continue;
                if (IsEnvActive(rule.EnvType)) continue;

                if (rule.Condition())
                {
                    EnableMod(rule.EnvType);
                    _autoEnabledMods.Add(rule.EnvType);
                    ChillEnvPlugin.Log?.LogInfo($"[自动托管] 条件满足，开启: {rule.Name}");
                }
            }
        }

        private void EnableMod(EnvironmentType env)
        {
            if (EnvRegistry.TryGet(env, out var ctrl))
            {
                if (!IsEnvActive(env)) ChillEnvPlugin.SimulateClickMainIcon(ctrl);
            }
        }

        private void DisableMod(EnvironmentType env)
        {
            if (EnvRegistry.TryGet(env, out var ctrl))
            {
                if (IsEnvActive(env)) ChillEnvPlugin.SimulateClickMainIcon(ctrl);
            }
        }

        private void CleanupAllAutoMods()
        {
            foreach (var env in _autoEnabledMods) DisableMod(env);
            _autoEnabledMods.Clear();
        }

        private bool IsEnvActive(EnvironmentType env)
        {
            try
            {
                var dict = SaveDataManager.Instance.WindowViewDic;
                var winType = (WindowViewType)Enum.Parse(typeof(WindowViewType), env.ToString());
                if (dict.ContainsKey(winType)) return dict[winType].IsActive;
            }
            catch { }
            return false;
        }

        private enum Season { Spring, Summer, Autumn, Winter }
        private Season GetSeason()
        {
            int month = DateTime.Now.Month;
            if (month >= 3 && month <= 5) return Season.Spring;
            if (month >= 6 && month <= 8) return Season.Summer;
            if (month >= 9 && month <= 11) return Season.Autumn;
            return Season.Winter;
        }

        private bool IsDay()
        {
            int h = DateTime.Now.Hour;
            return h >= 6 && h < 18;
        }

        private bool IsNight()
        {
            int h = DateTime.Now.Hour;
            return h >= 19 || h < 5;
        }

        private bool IsGoodWeather()
        {
            var w = WeatherService.CachedWeather;
            if (w == null) return true;
            return w.Code >= 0 && w.Code <= 9;
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

    public class WeatherService
    {
        private static WeatherInfo _cachedWeather;
        private static DateTime _lastFetchTime;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(60);

        public static WeatherInfo CachedWeather => _cachedWeather;

        public static IEnumerator FetchWeather(string apiKey, string location, bool force, Action<WeatherInfo> onComplete)
        {
            if (!force && _cachedWeather != null && DateTime.Now - _lastFetchTime < CacheExpiry)
            {
                onComplete?.Invoke(_cachedWeather);
                yield break;
            }

            string url = $"https://api.seniverse.com/v3/weather/now.json?key={apiKey}&location={UnityWebRequest.EscapeURL(location)}&language=zh-Hans&unit=c";

            ChillEnvPlugin.Log?.LogInfo($"[API] 发起请求: {location}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    ChillEnvPlugin.Log?.LogWarning($"[API] 网络错误: {request.result}, Code: {request.responseCode}, Error: {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                string json = request.downloadHandler.text;
                if (string.IsNullOrEmpty(json))
                {
                    ChillEnvPlugin.Log?.LogWarning("[API] 错误: 返回内容为空");
                    onComplete?.Invoke(null);
                    yield break;
                }

                try
                {
                    var weather = ParseWeatherJson(json);
                    if (weather != null)
                    {
                        _cachedWeather = weather;
                        _lastFetchTime = DateTime.Now;
                        ChillEnvPlugin.Log?.LogInfo($"[API] 数据更新: {weather}");
                        onComplete?.Invoke(weather);
                    }
                    else
                    {
                        ChillEnvPlugin.Log?.LogWarning($"[API] 解析失败! 原始返回: {json}");
                        onComplete?.Invoke(null);
                    }
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogError($"[API] 异常: {ex.Message}");
                    onComplete?.Invoke(null);
                }
            }
        }

        private static WeatherInfo ParseWeatherJson(string json)
        {
            try
            {
                if (json.Contains("\"status\"") && !json.Contains("\"results\""))
                {
                    return null;
                }

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

        public static WeatherCondition MapCodeToCondition(int code)
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
        private float _nextWeatherCheckTime;
        private float _nextTimeCheckTime;

        private EnvironmentType? _lastAppliedEnv;
        private bool _isFetching;

        private static readonly EnvironmentType[] BaseEnvironments = new[]
        {
            EnvironmentType.Day, EnvironmentType.Sunset, EnvironmentType.Night, EnvironmentType.Cloudy
        };

        private static readonly EnvironmentType[] SceneryWeathers = new[]
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
            _nextWeatherCheckTime = Time.time + 10f;
            _nextTimeCheckTime = Time.time + 10f;
            ChillEnvPlugin.Log?.LogInfo("Runner 启动，等待首次同步...");
        }

        private void Update()
        {
            if (!ChillEnvPlugin.Initialized || EnvRegistry.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.F9))
            {
                ChillEnvPlugin.Log?.LogInfo("F9: 手动强制同步状态");
                TriggerSync(forceApi: false, forceApply: true);
            }
            if (Input.GetKeyDown(KeyCode.F8)) ShowStatus();
            if (Input.GetKeyDown(KeyCode.F7))
            {
                ChillEnvPlugin.Log?.LogInfo("F7: 强制重载 (尝试读取最新配置/API)");
                ChillEnvPlugin.Instance.Config.Reload();
                ForceRefreshWeather();
            }

            if (Time.time >= _nextTimeCheckTime)
            {
                _nextTimeCheckTime = Time.time + 30f;
                TriggerSync(forceApi: false, forceApply: false);
            }

            if (Time.time >= _nextWeatherCheckTime)
            {
                int minutes = Mathf.Max(1, ChillEnvPlugin.Cfg_WeatherRefreshMinutes.Value);
                _nextWeatherCheckTime = Time.time + (minutes * 60f);
                TriggerSync(forceApi: true, forceApply: false);
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
                ChillEnvPlugin.Log?.LogWarning("【警告】调试模式已开启！API请求被屏蔽。");
            }
        }

        private void ForceRefreshWeather()
        {
            _nextWeatherCheckTime = Time.time + (ChillEnvPlugin.Cfg_WeatherRefreshMinutes.Value * 60f);
            TriggerSync(forceApi: true, forceApply: false);
        }

        private void TriggerSync(bool forceApi, bool forceApply)
        {
            if (ChillEnvPlugin.Cfg_DebugMode.Value)
            {
                ChillEnvPlugin.Log?.LogWarning("[调试模式] 使用模拟数据...");
                int mockCode = ChillEnvPlugin.Cfg_DebugCode.Value;
                var mockWeather = new WeatherInfo
                {
                    Code = mockCode,
                    Temperature = ChillEnvPlugin.Cfg_DebugTemp.Value,
                    Text = ChillEnvPlugin.Cfg_DebugText.Value,
                    Condition = WeatherService.MapCodeToCondition(mockCode),
                    UpdateTime = DateTime.Now
                };
                ApplyEnvironment(mockWeather, forceApply);
                return;
            }

            bool weatherEnabled = ChillEnvPlugin.Cfg_EnableWeatherSync.Value;
            string apiKey = ChillEnvPlugin.Cfg_SeniverseKey.Value;

            if (weatherEnabled && !string.IsNullOrEmpty(apiKey))
            {
                string location = ChillEnvPlugin.Cfg_Location.Value;

                if (forceApi || WeatherService.CachedWeather == null)
                {
                    if (_isFetching) return;
                    _isFetching = true;
                    StartCoroutine(WeatherService.FetchWeather(apiKey, location, forceApi, (weather) =>
                    {
                        _isFetching = false;
                        if (weather != null) ApplyEnvironment(weather, forceApply);
                        else
                        {
                            ChillEnvPlugin.Log?.LogWarning("[API异常] 启用时间兜底");
                            ApplyTimeBasedEnvironment(forceApply);
                        }
                    }));
                }
                else
                {
                    ApplyEnvironment(WeatherService.CachedWeather, forceApply);
                }
            }
            else
            {
                ApplyTimeBasedEnvironment(forceApply);
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

        private void SimulateClick(EnvironmentType envType)
        {
            if (EnvRegistry.TryGet(envType, out var ctrl))
            {
                ChillEnvPlugin.SimulateClickMainIcon(ctrl);
            }
        }

        private bool IsBadWeather(int code)
        {
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
            DateTime now = DateTime.Now;
            TimeSpan currentTime = now.TimeOfDay;
            TimeSpan sunrise, sunset;
            if (!TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunriseTime.Value, out sunrise)) sunrise = new TimeSpan(6, 30, 0);
            if (!TimeSpan.TryParse(ChillEnvPlugin.Cfg_SunsetTime.Value, out sunset)) sunset = new TimeSpan(18, 30, 0);

            TimeSpan sunsetStart = sunset.Subtract(TimeSpan.FromMinutes(30));
            TimeSpan sunsetEnd = sunset.Add(TimeSpan.FromMinutes(30));

            if (currentTime >= sunrise && currentTime < sunsetStart) return EnvironmentType.Day;
            else if (currentTime >= sunsetStart && currentTime < sunsetEnd) return EnvironmentType.Sunset;
            else return EnvironmentType.Night;
        }

        private void ApplyBaseEnvironment(EnvironmentType target, bool force)
        {
            if (!force && IsEnvironmentActive(target)) return;

            foreach (var env in BaseEnvironments)
            {
                if (env != target && IsEnvironmentActive(env))
                {
                    SimulateClick(env);
                }
            }

            if (!IsEnvironmentActive(target))
            {
                SimulateClick(target);
            }

            ChillEnvPlugin.CallServiceChangeWeather(target);
            ChillEnvPlugin.Log?.LogInfo($"[环境] 切换至: {target}");
        }

        private void ApplyScenery(EnvironmentType? target, bool force)
        {
            foreach (var env in SceneryWeathers)
            {
                bool shouldBeActive = (target.HasValue && target.Value == env);
                bool isActive = IsEnvironmentActive(env);

                if (shouldBeActive)
                {
                    if (!isActive)
                    {
                        SimulateClick(env);
                        ChillEnvPlugin.Log?.LogInfo($"[景色] 开启: {env}");
                    }
                }
                else
                {
                    if (isActive)
                    {
                        SimulateClick(env);
                    }
                }
            }
        }

        private void ApplyEnvironment(WeatherInfo weather, bool force)
        {
            if (force || _lastAppliedEnv == null)
            {
                ChillEnvPlugin.Log?.LogInfo($"[决策] 天气:{weather.Text}(Code:{weather.Code})");
            }

            ChillEnvPlugin.UIWeatherString = $"{weather.Text} {weather.Temperature}°C";

            EnvironmentType baseEnv = GetTimeBasedEnvironment();
            EnvironmentType finalEnv = baseEnv;

            if (IsBadWeather(weather.Code))
            {
                if (baseEnv != EnvironmentType.Night) finalEnv = EnvironmentType.Cloudy;
            }

            EnvironmentType? targetScenery = GetSceneryType(weather.Code);

            ApplyBaseEnvironment(finalEnv, force);
            ApplyScenery(targetScenery, force);

            _lastAppliedEnv = finalEnv;
        }

        private void ApplyTimeBasedEnvironment(bool force)
        {
            ChillEnvPlugin.UIWeatherString = "";

            EnvironmentType targetEnv = GetTimeBasedEnvironment();
            ApplyBaseEnvironment(targetEnv, force);
            ApplyScenery(null, force);
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

    [HarmonyPatch(typeof(CurrentDateAndTimeUI), "UpdateDateAndTime")]
    internal static class DateUIPatch
    {
        static void Postfix(CurrentDateAndTimeUI __instance)
        {
            if (!ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value || string.IsNullOrEmpty(ChillEnvPlugin.UIWeatherString)) return;

            try
            {
                var field = typeof(CurrentDateAndTimeUI).GetField("_dateText", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var textMesh = field.GetValue(__instance) as TextMeshProUGUI;
                    if (textMesh != null)
                    {
                        textMesh.text += $" | {ChillEnvPlugin.UIWeatherString}";
                    }
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(EnviromentController), "OnClickButtonMainIcon")]
    internal static class UserInteractionPatch
    {
        public static bool IsSimulatingClick = false;

        static void Prefix(EnviromentController __instance)
        {
            if (!IsSimulatingClick)
            {
                EnvironmentType type = __instance.EnvironmentType;
                if (!SceneryAutomationSystem.UserInteractedMods.Contains(type))
                {
                    SceneryAutomationSystem.UserInteractedMods.Add(type);
                    ChillEnvPlugin.Log?.LogInfo($"[用户交互] 用户接管了 {type}，停止自动托管。");
                }
            }
        }
    }
}