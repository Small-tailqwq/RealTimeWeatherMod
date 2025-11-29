using BepInEx;
using BepInEx.Logging;
using Bulbul;
using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace ChillEnvMod
{
    [BepInPlugin("com.chillenv.plugin", "ChillEnv", "1.0.0")]
    public class ChillEnvPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static FacilityEnviroment Facility;
        private static Harmony _harmony;

        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("[ChillEnv] 插件加载中...");

            _harmony = new Harmony("com.chillenv.plugin");
            _harmony.PatchAll();

            Log.LogInfo("[ChillEnv] Harmony 补丁已应用");
        }

        private void Start()
        {
            AutoEnvRunner.StartLoop();
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception ex) { Log?.LogError("[MainThread] 执行队列操作失败: " + ex.Message); }
            }
        }

        internal static void EnqueueOnMainThread(Action action)
        {
            if (action != null) _mainThreadQueue.Enqueue(action);
        }
    }

    // ============ 解锁所有窗景 ============
    [HarmonyPatch(typeof(WindowViewData), "IsUnlocked", MethodType.Getter)]
    internal static class WindowViewUnlockPatch
    {
        static void Postfix(ref bool __result)
        {
            __result = true;
        }
    }

    // ============ 捕获 FacilityEnviroment 实例 ============
    [HarmonyPatch(typeof(FacilityEnviroment), "Setup")]
    internal static class FacilityEnviromentSetupPatch
    {
        static void Postfix(FacilityEnviroment __instance)
        {
            ChillEnvPlugin.Facility = __instance;
            ChillEnvPlugin.Log?.LogInfo("[AutoEnv] 捕获 FacilityEnviroment 实例");
        }
    }

    // ============ 主运行器 ============
    internal static class AutoEnvRunner
    {
        private const string API_URL =
            "https://api.open-meteo.com/v1/forecast?latitude=31.23&longitude=121.47&current=weather_code,is_day&timezone=auto";

        private const int CHECK_INTERVAL_SECONDS = 300;

        private static readonly WindowViewType[] PrecipitationViews =
        {
            WindowViewType.LightRain,
            WindowViewType.HeavyRain,
            WindowViewType.ThunderRain
        };

        public static void StartLoop()
        {
            ChillEnvPlugin.Log?.LogInfo("[AutoEnv] 等待游戏初始化...");
            Task.Run(async () =>
            {
                while (ChillEnvPlugin.Facility == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                ChillEnvPlugin.Log?.LogInfo("[AutoEnv] 开始天气同步循环");

                while (true)
                {
                    try
                    {
                        await FetchAndApplyWeather();
                    }
                    catch (Exception ex)
                    {
                        ChillEnvPlugin.Log?.LogError($"[AutoEnv] 天气同步出错: {ex.Message}");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS));
                }
            });
        }

        private static async Task FetchAndApplyWeather()
        {
            ChillEnvPlugin.Log?.LogInfo("[AutoEnv] 正在获取天气数据...");

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetStringAsync(API_URL);

                ChillEnvPlugin.Log?.LogInfo($"[AutoEnv] API 返回: {response.Substring(0, Math.Min(200, response.Length))}...");

                // 修正正则：JSON 中的引号不需要转义
                int weatherCode = TryExtractInt(response, "\"weather_code\"\\s*:\\s*(?<v>-?\\d+)");
                int isDay = TryExtractInt(response, "\"is_day\"\\s*:\\s*(?<v>-?\\d+)");

                ChillEnvPlugin.Log?.LogInfo($"[AutoEnv] 解析结果: 天气代码={weatherCode}, 白天={isDay}");

                ChillEnvPlugin.EnqueueOnMainThread(() =>
                {
                    ApplyWeather(weatherCode, isDay == 1);
                });
            }
        }

        private static int TryExtractInt(string text, string pattern)
        {
            try
            {
                var m = Regex.Match(text, pattern);
                if (m.Success)
                {
                    int v;
                    if (int.TryParse(m.Groups["v"].Value, out v))
                    {
                        return v;
                    }
                }
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogError($"[AutoEnv] 正则解析失败: {ex.Message}");
            }
            return -1; // 返回 -1 表示解析失败，而不是 0（0 是有效的天气代码）
        }

        private static bool IsWindowActive(WindowViewType type)
        {
            var dic = SaveDataManager.Instance?.WindowViewDic;
            if (dic == null) return false;

            WindowViewData data;
            return dic.TryGetValue(type, out data) && data.IsActive;
        }

        private static void LogCurrentState()
        {
            var activePrecip = new List<string>();
            foreach (var t in PrecipitationViews)
            {
                if (IsWindowActive(t))
                    activePrecip.Add(t.ToString());
            }

            var timeState = "未知";
            if (IsWindowActive(WindowViewType.Day)) timeState = "Day";
            else if (IsWindowActive(WindowViewType.Sunset)) timeState = "Sunset";
            else if (IsWindowActive(WindowViewType.Night)) timeState = "Night";
            else if (IsWindowActive(WindowViewType.Cloudy)) timeState = "Cloudy";

            bool snowActive = IsWindowActive(WindowViewType.Snow);

            ChillEnvPlugin.Log?.LogInfo($"[状态] 时间={timeState}, 降水={(activePrecip.Count == 0 ? "无" : string.Join(", ", activePrecip))}, 雪景={snowActive}");
        }

        private static void ClearPrecipitation(FacilityEnviroment fac)
        {
            foreach (var w in PrecipitationViews)
            {
                if (IsWindowActive(w))
                {
                    ChillEnvPlugin.Log?.LogInfo($"[操作] 关闭降水: {w}");
                    fac.ChangeWindowView(ChangeType.Deactivate, w);
                }
            }
        }

        private static void SetPrecipitation(FacilityEnviroment fac, WindowViewType target)
        {
            foreach (var w in PrecipitationViews)
            {
                if (w != target && IsWindowActive(w))
                {
                    ChillEnvPlugin.Log?.LogInfo($"[操作] 关闭其他降水: {w}");
                    fac.ChangeWindowView(ChangeType.Deactivate, w);
                }
            }

            if (!IsWindowActive(target))
            {
                ChillEnvPlugin.Log?.LogInfo($"[操作] 激活降水: {target}");
                fac.ChangeWindowView(ChangeType.Activate, target);
            }
        }

        private static void SetBaseTime(FacilityEnviroment fac, WindowViewType target)
        {
            if (IsWindowActive(target))
            {
                ChillEnvPlugin.Log?.LogInfo($"[跳过] 时间已是: {target}");
                return;
            }

            ChillEnvPlugin.Log?.LogInfo($"[操作] 切换时间: {target}");

            switch (target)
            {
                case WindowViewType.Day:
                    fac.OnClickButtonChangeTimeDay();
                    break;
                case WindowViewType.Sunset:
                    fac.OnClickButtonChangeTimeSunset();
                    break;
                case WindowViewType.Night:
                    fac.OnClickButtonChangeTimeNight();
                    break;
                case WindowViewType.Cloudy:
                    fac.OnClickButtonChangeTimeCloudy();
                    break;
            }
        }

        private static void ApplyWeather(int weatherCode, bool isDay)
        {
            var fac = ChillEnvPlugin.Facility;
            if (fac == null)
            {
                ChillEnvPlugin.Log?.LogWarning("[AutoEnv] FacilityEnviroment 为空，跳过");
                return;
            }

            if (weatherCode < 0)
            {
                ChillEnvPlugin.Log?.LogWarning("[AutoEnv] 天气代码解析失败，跳过本次同步");
                return;
            }

            LogCurrentState();

            ChillEnvPlugin.Log?.LogInfo($"[AutoEnv] 应用天气: Code={weatherCode}, IsDay={isDay}");

            switch (weatherCode)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    if (weatherCode <= 1)
                    {
                        SetBaseTime(fac, isDay ? WindowViewType.Day : WindowViewType.Night);
                    }
                    else
                    {
                        SetBaseTime(fac, WindowViewType.Cloudy);
                    }
                    ClearPrecipitation(fac);
                    ClearSnowIfNotSnowing(fac, weatherCode);
                    break;

                case 45:
                case 48:
                    SetBaseTime(fac, WindowViewType.Cloudy);
                    ClearPrecipitation(fac);
                    break;

                case 51:
                case 53:
                case 55:
                case 56:
                case 57:
                    SetBaseTime(fac, WindowViewType.Cloudy);
                    SetPrecipitation(fac, WindowViewType.LightRain);
                    break;

                case 61:
                    SetBaseTime(fac, WindowViewType.Cloudy);
                    SetPrecipitation(fac, WindowViewType.LightRain);
                    break;
                case 63:
                case 65:
                case 66:
                case 67:
                    SetBaseTime(fac, WindowViewType.Cloudy);
                    SetPrecipitation(fac, WindowViewType.HeavyRain);
                    break;
                case 80:
                    SetBaseTime(fac, WindowViewType.Cloudy);
                    SetPrecipitation(fac, WindowViewType.LightRain);
                    break;
                case 81:
                case 82:
                    SetBaseTime(fac, WindowViewType.Cloudy);
                    SetPrecipitation(fac, WindowViewType.HeavyRain);
                    break;

                case 71:
                case 73:
                case 75:
                case 77:
                case 85:
                case 86:
                    SetBaseTime(fac, WindowViewType.Cloudy);
                    ClearPrecipitation(fac);
                    if (!IsWindowActive(WindowViewType.Snow))
                    {
                        ChillEnvPlugin.Log?.LogInfo("[操作] 激活雪景");
                        fac.ChangeWindowView(ChangeType.Activate, WindowViewType.Snow);
                    }
                    break;

                case 95:
                case 96:
                case 99:
                    SetBaseTime(fac, WindowViewType.Cloudy);
                    SetPrecipitation(fac, WindowViewType.ThunderRain);
                    break;

                default:
                    ChillEnvPlugin.Log?.LogWarning($"[AutoEnv] 未知天气代码: {weatherCode}，使用默认");
                    SetBaseTime(fac, isDay ? WindowViewType.Day : WindowViewType.Night);
                    ClearPrecipitation(fac);
                    break;
            }

            LogCurrentState();
        }

        private static void ClearSnowIfNotSnowing(FacilityEnviroment fac, int weatherCode)
        {
            bool isSnowWeather = weatherCode == 71 || weatherCode == 73 || weatherCode == 75
                              || weatherCode == 77 || weatherCode == 85 || weatherCode == 86;

            if (!isSnowWeather && IsWindowActive(WindowViewType.Snow))
            {
                ChillEnvPlugin.Log?.LogInfo("[操作] 关闭雪景（非雪天）");
                fac.ChangeWindowView(ChangeType.Deactivate, WindowViewType.Snow);
            }
        }
    }
}