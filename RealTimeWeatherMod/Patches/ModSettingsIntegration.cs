using System;
using System.Collections;
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
                    ChillEnvPlugin.Log?.LogInfo("✅ [EnvSync] MOD 设置已成功注册到 iGPU Savior 界面");
                    yield break;
                }

                // 每 0.5 秒重试一次
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            // 超时未找到主框架，说明用户可能未安装 iGPU Savior
            // 这不是错误，只是说明设置界面功能不可用
            ChillEnvPlugin.Log?.LogWarning("⚠️ [EnvSync] 未检测到 iGPU Savior，设置界面集成已跳过。");
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
                
                // 获取类型
                Type managerType = Type.GetType(MANAGER_TYPE_NAME);
                if (managerType == null) return false; // 类型未加载，说明主框架未运行

                // 获取单例属性 Instance
                var instanceProp = managerType.GetProperty("Instance", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null) return false;

                // 获取单例对象
                object managerInstance = instanceProp.GetValue(null);
                if (managerInstance == null) return false; // 单例尚未初始化

                // 检查初始化状态 (兼容性检查)
                var isInitializedProp = managerType.GetProperty("IsInitialized");
                if (isInitializedProp != null)
                {
                    bool isInitialized = (bool)isInitializedProp.GetValue(managerInstance);
                    if (!isInitialized) return false; // 主框架还在启动中
                }

                // =========================================================
                // 2. 注册 MOD 信息 (Register Mod Info)
                // =========================================================
                
                // 获取注册方法
                var regMethod = managerType.GetMethod("RegisterMod", new Type[] { typeof(string), typeof(string) });
                if (regMethod != null)
                {
                    // 参数：Mod名称, Mod版本
                    regMethod.Invoke(managerInstance, new object[] { "Chill Env Sync", "5.1.3" });
                }

                // =========================================================
                // 3. 注册具体设置项 (Register Settings)
                // =========================================================

                bool allSuccess = true;

                // --- 开关示例：启用天气同步 ---
                if (!AddToggleSafe(managerInstance, managerType,
                    "天气同步", // 显示的标签
                    ChillEnvPlugin.Cfg_EnableWeatherSync.Value, // 当前值
                    (value) => // 回调函数
                    {
                        ChillEnvPlugin.Cfg_EnableWeatherSync.Value = value;
                        ChillEnvPlugin.Instance.Config.Save(); // 别忘了保存配置！
                        ChillEnvPlugin.Log?.LogInfo($"[设置] 天气API同步已设置为: {value}");
                    }))
                {
                    allSuccess = false;
                }

                // --- 开关示例：UI显示 ---
                AddToggleSafe(managerInstance, managerType,
                    "日期栏天气",
                    ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                    });

                AddToggleSafe(managerInstance, managerType,
                    "详细时段",
                    ChillEnvPlugin.Cfg_DetailedTimeSegments.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_DetailedTimeSegments.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                    });

                AddToggleSafe(managerInstance, managerType,
                    "彩蛋",
                    ChillEnvPlugin.Cfg_EnableEasterEggs.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_EnableEasterEggs.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                    });

                // --- 输入框示例：城市位置 ---
                if (!AddInputFieldSafe(managerInstance, managerType,
                    "城市", // 标签
                    ChillEnvPlugin.Cfg_Location.Value, // 初始文本
                    (val) => // 回调
                    {
                        ChillEnvPlugin.Cfg_Location.Value = val;
                        ChillEnvPlugin.Instance.Config.Save();
                        Services.WeatherService.InvalidateCache(); // 切换城市时清空缓存，避免沿用旧城市天气
                        
                        // 防抖：取消之前的延迟任务
                        if (_locationDebounceCoroutine != null)
                        {
                            StopCoroutine(_locationDebounceCoroutine);
                        }
                        
                        // 启动新的延迟任务：3秒后刷新天气
                        _locationDebounceCoroutine = StartCoroutine(RefreshWeatherAfterDelay(val, 3f));
                    }))
                {
                    allSuccess = false;
                }

                // =========================================================
                // 4. 完成注册
                // =========================================================

                if (allSuccess)
                {
                    // 只有全部成功才标记为已注册，否则下次可能还会尝试重试（取决于你的重试策略）
                    // 这里我们为了简单，只要大体成功就标记 true
                    _settingsRegistered = true;
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                // 捕获反射过程中的任何意外，防止影响游戏主逻辑
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
            string label, string initialValue, Action<string> callback)
        {
            try
            {
                // 查找目标方法：AddInputField(string, string, Action<string>)
                var method = managerType.GetMethod("AddInputField", new Type[]
                {
                    typeof(string), typeof(string), typeof(Action<string>)
                });

                if (method == null) return false;

                Action<string> safeCallback = (val) =>
                {
                    try { callback?.Invoke(val); }
                    catch (Exception ex) { Debug.LogError($"[EnvSync] Input callback error ({label}): {ex}"); }
                };

                method.Invoke(managerInstance, new object[] { label, initialValue, safeCallback });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EnvSync] Failed to add input field '{label}': {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Debounce Helper

        /// <summary>
        /// 延迟刷新天气 - 防抖机制
        /// </summary>
        private IEnumerator RefreshWeatherAfterDelay(string location, float delay)
        {
            // 用真实时间的延迟,避免 Time.timeScale 影响导致等待过长
            yield return new WaitForSecondsRealtime(delay);
            
            ChillEnvPlugin.Log?.LogInfo($"🔄 [EnvSync] 城市已更新为 '{location}',正在刷新天气与日出日落数据...");
            
            // 立即触发天气刷新 (配置值已在回调中更新,无需 Reload)
            Core.AutoEnvRunner.TriggerWeatherRefresh();
            
            // 同时刷新日出日落数据 (地理位置变化会影响日出日落时间)
            Core.AutoEnvRunner.TriggerSunScheduleRefresh();
            
            _locationDebounceCoroutine = null;
        }

        #endregion
    }
}
