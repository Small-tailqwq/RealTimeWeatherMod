using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using BepInEx; // 依赖 BepInEx 环境

namespace ChillWithYou.EnvSync.Patches
{
    /// <summary>
    /// 【开发者集成示例】MOD 设置界面对接脚本
    /// 
    /// 功能：
    /// 本脚本负责将当前 MOD 的配置项注册到主框架 (iGPU Savior / PotatoOptimization) 的统一设置界面中。
    /// 
    /// 核心逻辑：
    /// 1. 异步等待：使用 Coroutine 等待主框架初始化完成。
    /// 2. 反射调用：使用 C# 反射机制访问 API，实现软依赖（即使主框架没安装，本 MOD 也能正常运行不报错）。
    /// 3. 安全包装：对 API 调用进行了 try-catch 封装，防止单个设置项错误导致整个界面崩溃。
    /// 
    /// 使用方法：
    /// 将此脚本挂载到你的 BepInEx Plugin GameObject 上，或者在 Plugin.Start() 中手动挂载。
    /// </summary>
    public class ModSettingsIntegration : MonoBehaviour
    {
        // 防止重复注册的标志位
        private static bool _settingsRegistered = false;

        // 主框架的程序集限定名 (Namespace.ClassName, AssemblyName)
        // 如果你的框架类名或程序集名不同，请修改此处
        private const string MANAGER_TYPE_NAME = "ModShared.ModSettingsManager, iGPU Savior";

        // 城市位置修改防抖相关
        private Coroutine _locationDebounceCoroutine;
        private Coroutine _latLonDebounceCoroutine;

        private void Start()
        {
            // 启动协程，开始尝试注册设置
            StartCoroutine(RegisterSettingsWhenReady());
        }

        /// <summary>
        /// 协程：等待主框架准备就绪并注册设置
        /// </summary>
        private IEnumerator RegisterSettingsWhenReady()
        {
            // 初始等待一帧
            yield return null;

            float timeout = 10f; // 最大等待时间 (秒)
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                // 尝试注册，如果成功则退出协程
                if (TryRegisterSettings())
                {
                    ChillEnvPlugin.Log?.LogInfo("[EnvSync] MOD 设置已成功注册到 iGPU Savior 界面");
                    yield break;
                }

                // 每 0.5 秒重试一次
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            // 超时未找到主框架，说明用户可能未安装 iGPU Savior
            // 这不是错误，只是说明设置界面功能不可用
            ChillEnvPlugin.Log?.LogInfo("[EnvSync] 未安装可选的 iGPU Savior，已跳过设置界面集成");
        }

        /// <summary>
        /// 尝试获取管理器实例并注册所有设置项
        /// </summary>
        /// <returns>是否成功注册</returns>
        private bool TryRegisterSettings()
        {
            if (_settingsRegistered) return true;

            try
            {
                // =========================================================
                // 1. 反射获取管理器实例 (Reflection Setup)
                // =========================================================
                Type managerType = Type.GetType(MANAGER_TYPE_NAME);
                if (managerType == null) return false;
                var instanceProp = managerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null) return false;
                object managerInstance = instanceProp.GetValue(null);
                if (managerInstance == null) return false;
                var isInitializedProp = managerType.GetProperty("IsInitialized");
                if (isInitializedProp != null)
                {
                    bool isInitialized = (bool)isInitializedProp.GetValue(managerInstance);
                    if (!isInitialized) return false;
                }

                // =========================================================
                // 2. 注册 MOD 信息 (Register Mod Info)
                // =========================================================
                var regMethod = managerType.GetMethod("RegisterMod", new Type[] { typeof(string), typeof(string) });
                if (regMethod != null)
                {
                    regMethod.Invoke(managerInstance, new object[] { "Chill Env Sync", ChillEnvPlugin.PluginVersion });
                }

                // =========================================================
                // 2.5 注册多语言翻译 (新增)
                // =========================================================
                var regTransMethod = managerType.GetMethod("RegisterTranslation", new Type[] { typeof(string), typeof(string), typeof(string), typeof(string) });
                bool hasTranslation = regTransMethod != null;
                if (hasTranslation)
                {
                    // 参数：Key, English, Japanese, Chinese
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_ENABLE", "Weather Sync", "天気同期", "天气同步" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_TIME", "Time Sync", "時間同期", "时间同步" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_UI", "Show Weather on UI", "UIに天気を表示", "日期栏天气" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_DETAIL", "Detailed Segments", "詳細セグメント", "详细时段" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_EGG", "Seasonal Scenery", "季節の景色", "季节性景色" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_AMBIENT", "Ambient Sounds", "環境音", "环境音效" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_CITY", "City (Seniverse)", "都市（Seniverse）", "城市 (心知天气)" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_PROVIDER", "Weather Provider", "天気提供元", "天气数据源" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_PROVIDER_SENIVERSE", "Seniverse", "Seniverse", "心知天气" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_PROVIDER_OPENMETEO", "OpenMeteo", "OpenMeteo", "OpenMeteo" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_LAT", "Latitude (OpenMeteo)", "緯度（OpenMeteo）", "纬度 (OpenMeteo)" });
                    regTransMethod.Invoke(managerInstance, new object[] { "ENV_SYNC_LON", "Longitude (OpenMeteo)", "経度（OpenMeteo）", "经度 (OpenMeteo)" });
                }

                // =========================================================
                // 3. 注册具体设置项 (Register Settings)
                // =========================================================
                bool allSuccess = true;

                // --- 开关示例：启用天气同步 ---
                string labelEnable = hasTranslation ? "ENV_SYNC_ENABLE" : "天气同步";
                if (!AddToggleSafe(managerInstance, managerType,
                    labelEnable,
                    ChillEnvPlugin.Cfg_EnableWeatherSync.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_EnableWeatherSync.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                        ChillEnvPlugin.Log?.LogInfo($"[设置] 天气API同步已设置为: {value}");
                    }))
                {
                    allSuccess = false;
                }

                string labelTime = hasTranslation ? "ENV_SYNC_TIME" : "时间同步";
                if (!AddToggleSafe(managerInstance, managerType,
                    labelTime,
                    ChillEnvPlugin.Cfg_EnableTimeSync.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_EnableTimeSync.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                        ChillEnvPlugin.Log?.LogInfo($"[设置] 时间同步已设置为: {value}");
                    }))
                {
                    allSuccess = false;
                }

                // --- 开关示例：UI显示 ---
                string labelUI = hasTranslation ? "ENV_SYNC_UI" : "日期栏天气";
                AddToggleSafe(managerInstance, managerType,
                    labelUI,
                    ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value = value;
                        if (!value)
                        {
                            ChillEnvPlugin.UIWeatherString = string.Empty;
                        }
                        else
                        {
                            Core.AutoEnvRunner.TriggerUiWeatherRefresh();
                        }
                        ChillEnvPlugin.Instance.Config.Save();
                    });

                string labelDetail = hasTranslation ? "ENV_SYNC_DETAIL" : "详细时段";
                AddToggleSafe(managerInstance, managerType,
                    labelDetail,
                    ChillEnvPlugin.Cfg_DetailedTimeSegments.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_DetailedTimeSegments.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                    });

                string labelEgg = hasTranslation ? "ENV_SYNC_EGG" : "彩蛋";
                AddToggleSafe(managerInstance, managerType,
                    labelEgg,
                    ChillEnvPlugin.Cfg_EnableEasterEggs.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_EnableEasterEggs.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                    });

                string labelAmbient = hasTranslation ? "ENV_SYNC_AMBIENT" : "环境音效";
                AddToggleSafe(managerInstance, managerType,
                    labelAmbient,
                    ChillEnvPlugin.Cfg_EnableAmbientSounds.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_EnableAmbientSounds.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                    });

                // --- 下拉/输入框：天气服务提供商 ---
                string labelProvider = hasTranslation ? "ENV_SYNC_PROVIDER" : "天气数据源";
                var providerValues = new string[] { "Seniverse", "OpenMeteo" };
                var providerOptions = hasTranslation
                    ? new string[] { "ENV_SYNC_PROVIDER_SENIVERSE", "ENV_SYNC_PROVIDER_OPENMETEO" }
                    : providerValues;
                string currentProvider = ChillEnvPlugin.Cfg_WeatherProvider.Value;
                if (string.IsNullOrWhiteSpace(currentProvider) ||
                    System.Array.IndexOf(providerValues, currentProvider) < 0)
                {
                    currentProvider = "Seniverse";
                }

                Action<string> onProviderChanged = (val) =>
                {
                    ChillEnvPlugin.Cfg_WeatherProvider.Value = val;
                    ChillEnvPlugin.Instance.Config.Save();
                    Services.WeatherService.InvalidateCache();
                    ChillEnvPlugin.Log?.LogInfo($"[设置] 天气数据源已设置为: {val}");
                    Core.AutoEnvRunner.TriggerWeatherRefresh();
                };

                AddDropdownSafe(managerInstance, managerType,
                    labelProvider,
                    currentProvider,
                    providerOptions,
                    providerValues,
                    onProviderChanged);

                object visibleForOpenMeteo = CreateDropdownOptionVisibleWhen(
                    managerType,
                    labelProvider,
                    providerOptions[1]);
                object visibleForSeniverse = CreateDropdownOptionVisibleWhen(
                    managerType,
                    labelProvider,
                    providerOptions[0]);

                // --- 输入框：心知天气 - 城市位置 ---
                string labelCity = hasTranslation ? "ENV_SYNC_CITY" : "城市 (心知天气)";
                if (!AddInputFieldSafe(managerInstance, managerType,
                    labelCity,
                    ChillEnvPlugin.Cfg_Location.Value,
                    (val) =>
                    {
                        ChillEnvPlugin.Cfg_Location.Value = val;
                        ChillEnvPlugin.Instance.Config.Save();
                        Services.WeatherService.InvalidateCache();
                        if (_locationDebounceCoroutine != null)
                        {
                            StopCoroutine(_locationDebounceCoroutine);
                        }
                        _locationDebounceCoroutine = StartCoroutine(
                            RefreshWeatherAfterDelay($"心知天气城市 '{val}'", 3f, false));
                    },
                    visibleForSeniverse))
                {
                    allSuccess = false;
                }

                // --- 输入框：OpenMeteo - 纬度 ---
                string labelLat = hasTranslation ? "ENV_SYNC_LAT" : "纬度 (OpenMeteo)";
                if (!AddInputFieldSafe(managerInstance, managerType,
                    labelLat,
                    ChillEnvPlugin.Cfg_OpenMeteoLatitude.Value.ToString("F4"),
                    (val) => TryParseAndSetCoordinate(val, isLatitude: true),
                    visibleForOpenMeteo))
                {
                    allSuccess = false;
                }

                // --- 输入框：OpenMeteo - 经度 ---
                string labelLon = hasTranslation ? "ENV_SYNC_LON" : "经度 (OpenMeteo)";
                if (!AddInputFieldSafe(managerInstance, managerType,
                    labelLon,
                    ChillEnvPlugin.Cfg_OpenMeteoLongitude.Value.ToString("F4"),
                    (val) => TryParseAndSetCoordinate(val, isLatitude: false),
                    visibleForOpenMeteo))
                {
                    allSuccess = false;
                }

                // =========================================================
                // 4. 完成注册
                // =========================================================
                if (allSuccess)
                {
                    _settingsRegistered = true;
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogError($"❌ [EnvSync] 注册设置时发生致命错误: {ex.Message}");
                return false;
            }
        }

        #region Helper Methods (Safe Wrappers)

        /// <summary>
        /// 安全添加开关 (Toggle) - 封装了反射逻辑和错误处理
        /// </summary>
        /// <param name="managerInstance">管理器实例</param>
        /// <param name="managerType">管理器类型</param>
        /// <param name="label">UI显示的文字</param>
        /// <param name="defaultValue">初始开关状态</param>
        /// <param name="callback">值变更时的回调</param>
        /// <returns>是否添加成功</returns>
        private bool AddToggleSafe(object managerInstance, Type managerType,
            string label, bool defaultValue, Action<bool> callback)
        {
            try
            {
                // 查找目标方法：AddToggle(string, bool, Action<bool>)
                var method = managerType.GetMethod("AddToggle", new Type[] {
                    typeof(string), typeof(bool), typeof(Action<bool>)
                });

                if (method == null)
                {
                    ChillEnvPlugin.Log?.LogError("❌ API Error: AddToggle method not found.");
                    return false;
                }

                // 包装回调：在回调内部捕获业务逻辑的异常，防止炸毁 UI 线程
                Action<bool> safeCallback = (value) =>
                {
                    try { callback?.Invoke(value); }
                    catch (Exception ex) { ChillEnvPlugin.Log?.LogError($"❌ 设置回调错误 ({label}): {ex.Message}"); }
                };

                // 调用方法
                method.Invoke(managerInstance, new object[] { label, defaultValue, safeCallback });
                return true;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogError($"❌ 添加开关失败 '{label}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 安全添加文本输入框 (InputField) - 封装了反射逻辑
        /// </summary>
        /// <param name="managerInstance">管理器实例</param>
        /// <param name="managerType">管理器类型</param>
        /// <param name="label">UI显示的标题</param>
        /// <param name="initialValue">初始文本内容</param>
        /// <param name="callback">结束编辑后的回调</param>
        /// <returns>是否添加成功</returns>
        private bool AddInputFieldSafe(object managerInstance, Type managerType,
            string label, string initialValue, Action<string> callback, object visibleWhen = null)
        {
            try
            {
                var method = FindInputFieldMethod(managerType, visibleWhen);

                if (method == null) return false;

                Action<string> safeCallback = (val) =>
                {
                    try { callback?.Invoke(val); }
                    catch (Exception ex) { Debug.LogError($"[EnvSync] Input callback error ({label}): {ex}"); }
                };

                object[] args = method.GetParameters().Length == 4
                    ? new object[] { label, initialValue, safeCallback, visibleWhen }
                    : new object[] { label, initialValue, safeCallback };

                method.Invoke(managerInstance, args);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EnvSync] Failed to add input field '{label}': {ex.Message}");
                return false;
            }
        }

        private MethodInfo FindInputFieldMethod(Type managerType, object visibleWhen)
        {
            if (visibleWhen != null)
            {
                var methods = managerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var candidate in methods)
                {
                    if (candidate.Name != "AddInputField")
                    {
                        continue;
                    }

                    var parameters = candidate.GetParameters();
                    if (parameters.Length != 4 ||
                        parameters[0].ParameterType != typeof(string) ||
                        parameters[1].ParameterType != typeof(string) ||
                        parameters[2].ParameterType != typeof(Action<string>) ||
                        !parameters[3].ParameterType.IsInstanceOfType(visibleWhen))
                    {
                        continue;
                    }

                    return candidate;
                }
            }

            // 旧版 iGPU Savior 没有条件可见性参数时，退回到无条件显示。
            return managerType.GetMethod("AddInputField", new Type[]
            {
                typeof(string), typeof(string), typeof(Action<string>)
            });
        }

        private object CreateDropdownOptionVisibleWhen(Type managerType, string targetKey, string expectedOption)
        {
            try
            {
                Type visibleWhenType = managerType.Assembly.GetType("ModShared.VisibleWhen") ??
                    Type.GetType("ModShared.VisibleWhen, iGPU Savior");
                if (visibleWhenType == null)
                {
                    return null;
                }

                var method = visibleWhenType.GetMethod(
                    "DropdownOption",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string), typeof(string) },
                    null);
                if (method == null)
                {
                    return null;
                }

                return method.Invoke(null, new object[] { targetKey, expectedOption });
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[EnvSync] 条件可见性不可用，设置项将保持显示: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 安全添加下拉框 (Dropdown) - 封装了反射逻辑；iGPU Savior 签名:
        /// AddDropdown(string labelOrKey, List&lt;string&gt; options, int defaultIndex, Action&lt;int&gt; onValueChanged)
        /// </summary>
        private bool AddDropdownSafe(object managerInstance, Type managerType,
            string label, string currentValue, string[] options, string[] optionValues, Action<string> callback)
        {
            try
            {
                var listStringType = typeof(System.Collections.Generic.List<string>);

                // iGPU Savior 实际签名：AddDropdown(string, List<string>, int, Action<int>)
                var method = managerType.GetMethod("AddDropdown", new Type[] {
                    typeof(string), listStringType, typeof(int), typeof(Action<int>) });

                if (method == null) return false;

                var optionsList = new System.Collections.Generic.List<string>(options ?? new string[0]);
                var valuesList = new System.Collections.Generic.List<string>(optionValues ?? options ?? new string[0]);
                int selectedIndex = 0;
                if (!string.IsNullOrEmpty(currentValue))
                {
                    int found = valuesList.IndexOf(currentValue);
                    if (found < 0)
                    {
                        found = optionsList.IndexOf(currentValue);
                    }
                    if (found >= 0) selectedIndex = found;
                }

                Action<string> safeStringCallback = (val) =>
                {
                    try { callback?.Invoke(val); }
                    catch (Exception ex) { ChillEnvPlugin.Log?.LogError($"❌ 下拉回调错误 ({label}): {ex.Message}"); }
                };

                var intCallback = new Action<int>((idx) =>
                {
                    if (idx >= 0 && idx < valuesList.Count)
                        safeStringCallback(valuesList[idx]);
                });

                method.Invoke(managerInstance, new object[] { label, optionsList, selectedIndex, intCallback });
                return true;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[EnvSync] 添加下拉框失败 '{label}': {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Debounce Helper

        /// <summary>
        /// 延迟刷新天气 - 防抖机制
        /// </summary>
        private IEnumerator RefreshWeatherAfterDelay(string targetDescription, float delay, bool isCoordinateRefresh)
        {
            // 用真实时间的延迟,避免 Time.timeScale 影响导致等待过长
            yield return new WaitForSecondsRealtime(delay);
            
            ChillEnvPlugin.Log?.LogInfo($"[EnvSync] {targetDescription} 已更新，正在刷新天气与日出日落数据...");
            
            // 立即触发天气刷新 (配置值已在回调中更新,无需 Reload)
            Core.AutoEnvRunner.TriggerWeatherRefresh();
            
            // 同时刷新日出日落数据 (地理位置变化会影响日出日落时间)
            Core.AutoEnvRunner.TriggerSunScheduleRefresh();
            
            if (isCoordinateRefresh)
                _latLonDebounceCoroutine = null;
            else
                _locationDebounceCoroutine = null;
        }

        private string GetOpenMeteoCoordinateDescription()
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "OpenMeteo 坐标 {0:0.####}, {1:0.####}",
                ChillEnvPlugin.Cfg_OpenMeteoLatitude.Value,
                ChillEnvPlugin.Cfg_OpenMeteoLongitude.Value);
        }

        /// <summary>
        /// 解析并保存经纬度输入,失败时记录警告
        /// </summary>
        private void TryParseAndSetCoordinate(string val, bool isLatitude)
        {
            if (double.TryParse(val,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double result))
            {
                double min = isLatitude ? -90d : -180d;
                double max = isLatitude ? 90d : 180d;
                string name = isLatitude ? "纬度" : "经度";
                if (result < min || result > max)
                {
                    ChillEnvPlugin.Log?.LogWarning($"[设置] OpenMeteo {name} 超出有效范围 [{min:F0}, {max:F0}]: {result:F4}");
                    return;
                }

                if (isLatitude)
                    ChillEnvPlugin.Cfg_OpenMeteoLatitude.Value = result;
                else
                    ChillEnvPlugin.Cfg_OpenMeteoLongitude.Value = result;

                ChillEnvPlugin.Instance.Config.Save();
                Services.WeatherService.InvalidateCache();

                ChillEnvPlugin.Log?.LogInfo($"[设置] OpenMeteo {name} 已设置为: {result:F4}");

                if (_latLonDebounceCoroutine != null)
                {
                    StopCoroutine(_latLonDebounceCoroutine);
                }
                _latLonDebounceCoroutine = StartCoroutine(
                    RefreshWeatherAfterDelay(GetOpenMeteoCoordinateDescription(), 3f, true));
            }
            else
            {
                string name = isLatitude ? "纬度" : "经度";
                ChillEnvPlugin.Log?.LogWarning($"[设置] 无法解析 OpenMeteo {name} 输入: {val}");
            }
        }

        #endregion
    }
}
