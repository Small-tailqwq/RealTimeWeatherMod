using System;
using System.Collections.Generic;
using UnityEngine;
using Bulbul;
using ChillWithYou.EnvSync.Services;
using ChillWithYou.EnvSync.Utils;
using System.Reflection;

namespace ChillWithYou.EnvSync.Core
{
  public class SceneryAutomationSystem : MonoBehaviour
  {
    internal static HashSet<EnvironmentType> _autoEnabledMods = new HashSet<EnvironmentType>();
    public static HashSet<EnvironmentType> UserInteractedMods = new HashSet<EnvironmentType>();

    // --- 核心修复：点击冷却 + 延迟验证 ---
    private Dictionary<EnvironmentType, float> _lastClickTime = new Dictionary<EnvironmentType, float>();
    private Dictionary<EnvironmentType, PendingAction> _pendingActions = new Dictionary<EnvironmentType, PendingAction>();

    private const float ClickCooldown = 2.0f;      // 点击冷却
    private const float VerifyDelay = 0.5f;        // 点击后延迟验证的时间

    private class PendingAction
    {
      public bool TargetState;      // true=开启, false=关闭
      public float VerifyTime;      // 验证时间点
      public string RuleName;       // 规则名（用于日志）
    }

    private class SceneryRule
    {
      public EnvironmentType EnvType;
      public Func<bool> Condition;
      public string Name;
    }

    private List<SceneryRule> _rules = new List<SceneryRule>();
    private float _checkTimer = 0f;
    private const float CheckInterval = 5f;

    // Mod内部枚举
    private const EnvironmentType Env_Fireworks = EnvironmentType.Fireworks;
    private const EnvironmentType Env_Cooking = EnvironmentType.CookSimmer;
    private const EnvironmentType Env_AC = EnvironmentType.RoomNoise;
    private const EnvironmentType Env_Sakura = EnvironmentType.Sakura;
    private const EnvironmentType Env_Cicada = EnvironmentType.Chicada;
    private const EnvironmentType Env_DeepSea = EnvironmentType.DeepSea;
    private const EnvironmentType Env_Space = EnvironmentType.Space;
    private const EnvironmentType Env_Locomotive = EnvironmentType.Locomotive;
    private const EnvironmentType Env_Balloon = EnvironmentType.Balloon;
    private const EnvironmentType Env_Books = EnvironmentType.Books;
    private const EnvironmentType Env_BlueButterfly = EnvironmentType.BlueButterfly;
    private const EnvironmentType Env_WindBell = EnvironmentType.WindBell;
    private const EnvironmentType Env_HotSpring = EnvironmentType.HotSpring;
    private const EnvironmentType Env_Whale = EnvironmentType.Whale;

    // 概率触发相关状态
    private System.Random _random = new System.Random();
    private DateTime _lastDailyCheck = DateTime.MinValue;
    private bool _windBellTriggeredToday = false;
    private bool _windBellRollDoneToday = false;
    private bool _hotSpringTriggeredToday = false;
    private bool _hotSpringRollDoneToday = false;
    private bool _whaleTriggeredToday = false;
    private bool _whaleRollDoneToday = false; // 标记今天是否已经抽过鲸鱼
    private bool _spaceTriggeredToday = false;
    private bool _spaceRollDoneToday = false;
    private bool _blueButterflyTriggeredToday = false;
    private DateTime _blueButterflyStartTime = DateTime.MinValue;
    
    // 鲸鱼专属标志：标记当前鲸鱼是否为系统抽中（而非用户手动开启）
    internal static bool IsWhaleSystemTriggered = false;

    private void Start()
    {
      InitializeRules();
    }

    private void InitializeRules()
    {
      // 1. 烟花 (WindowView) - 农历除夕和春节期间（正月初一到初五）
      _rules.Add(new SceneryRule
      {
        Name = "Fireworks",
        EnvType = Env_Fireworks,
        Condition = () =>
        {
          DateTime now = DateTime.Now;
          bool isNight = IsNight();
          bool isGregorianNewYear = (now.Month == 1 && now.Day == 1); // 公历元旦
          bool isLunarNewYear = IsLunarNewYearPeriod(now); // 农历春节期间
          return isNight && (isGregorianNewYear || isLunarNewYear);
        }
      });

      // 2. 做饭 (AmbientSound)
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

      // 3. 空调 (AmbientSound)
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

      // 4. 樱花 (WindowView)
      _rules.Add(new SceneryRule
      {
        Name = "Sakura",
        EnvType = Env_Sakura,
        Condition = () =>
        {
          return GetSeason() == Season.Spring && IsDay() && IsGoodWeather();
        }
      });

      // 5. 蝉鸣 (AmbientSound)
      _rules.Add(new SceneryRule
      {
        Name = "Cicadas",
        EnvType = Env_Cicada,
        Condition = () =>
        {
          return GetSeason() == Season.Summer && IsDay() && IsGoodWeather();
        }
      });

      // 6. 宇宙 (WindowView) - 极低概率触发 且 晴朗夜晚
      _rules.Add(new SceneryRule
      {
        Name = "Space",
        EnvType = Env_Space,
        Condition = () =>
        {
          CheckDailyReset();
          if (!IsNight() || !IsGoodWeather()) return false;
          
          if (_spaceTriggeredToday)
            return true; // 今天已触发，保持开启
          
          // 今天还没抽过，进行一次抽奖
          if (!_spaceRollDoneToday)
          {
            _spaceRollDoneToday = true;
            
            if (_random.NextDouble() < 0.01) // 1% 概率（晴朗夜晚前提下，一年约2次）
            {
              _spaceTriggeredToday = true;
              return true;
            }
          }
          
          return false;
        }
      });

      // 7. 火车 (WindowView) - 12.24-12.25 仅限夜晚
      _rules.Add(new SceneryRule
      {
        Name = "Locomotive",
        EnvType = Env_Locomotive,
        Condition = () =>
        {
          DateTime now = DateTime.Now;
          bool isChristmas = (now.Month == 12 && (now.Day == 24 || now.Day == 25));
          return isChristmas && IsNight() && IsGoodWeather();
        }
      });

      // 8. 热气球 (WindowView) - 6.1 儿童节仅限白天
      _rules.Add(new SceneryRule
      {
        Name = "Balloon",
        EnvType = Env_Balloon,
        Condition = () =>
        {
          DateTime now = DateTime.Now;
          bool isChildrensDay = (now.Month == 6 && now.Day == 1);
          return isChildrensDay && IsDay() && IsGoodWeather();
        }
      });

      // 9. 魔法书 (WindowView) - 4.23 读书日 或 9.1 开学
      _rules.Add(new SceneryRule
      {
        Name = "Books",
        EnvType = Env_Books,
        Condition = () =>
        {
          DateTime now = DateTime.Now;
          bool isBookDay = (now.Month == 4 && now.Day == 23);
          bool isSchoolDay = (now.Month == 9 && now.Day == 1);
          return isBookDay || isSchoolDay;
        }
      });

      // 10. 蓝蝶 (WindowView) - 5-6月夜晚（发光蝴蝶），20分钟一段，一天只触发一次
      _rules.Add(new SceneryRule
      {
        Name = "BlueButterfly",
        EnvType = Env_BlueButterfly,
        Condition = () =>
        {
          CheckDailyReset();
          DateTime now = DateTime.Now;
          int month = now.Month;
          
          if (month < 5 || month > 6 || !IsNight() || !IsGoodWeather())
            return false;
          
          // 如果已经触发过，检查是否超过20分钟
          if (_blueButterflyTriggeredToday)
          {
            if (_autoEnabledMods.Contains(Env_BlueButterfly))
            {
              // 检查是否已经开启超过20分钟
              if ((DateTime.Now - _blueButterflyStartTime).TotalMinutes >= 20)
              {
                return false; // 超时，应该关闭
              }
              return true; // 继续保持开启
            }
            return false; // 今天已触发过且已关闭
          }
          
          // 每20分钟一段计算触发概率
          int currentSegment = (now.Hour * 60 + now.Minute) / 20;
          int seed = now.Year * 10000 + now.DayOfYear * 100 + currentSegment;
          var segmentRandom = new System.Random(seed);
          
          if (segmentRandom.NextDouble() < 0.15) // 15%概率
          {
            _blueButterflyTriggeredToday = true;
            _blueButterflyStartTime = DateTime.Now;
            return true;
          }
          
          return false;
        }
      });

      // 11. 风铃 (WindowView) - 7-8月，启动后5%概率，每日计算一次
      _rules.Add(new SceneryRule
      {
        Name = "WindBell",
        EnvType = Env_WindBell,
        Condition = () =>
        {
          CheckDailyReset();
          DateTime now = DateTime.Now;
          int month = now.Month;
          
          if (month < 7 || month > 8 || !IsGoodWeather())
            return false;
          
          if (_windBellTriggeredToday)
            return true; // 今天已触发，保持开启
          
          // 今天还没抽过，进行一次抽奖
          if (!_windBellRollDoneToday)
          {
            _windBellRollDoneToday = true;
            
            if (_random.NextDouble() < 0.05) // 5%概率
            {
              _windBellTriggeredToday = true;
              return true;
            }
          }
          
          return false;
        }
      });

      // 12. 温泉 (WindowView) - 11月-2月，启动后5%概率（下雪时大幅提升），每日计算一次
      _rules.Add(new SceneryRule
      {
        Name = "HotSpring",
        EnvType = Env_HotSpring,
        Condition = () =>
        {
          CheckDailyReset();
          DateTime now = DateTime.Now;
          int month = now.Month;
          
          if (!((month >= 11 && month <= 12) || (month >= 1 && month <= 2)) || !IsGoodWeather())
            return false;
          
          if (_hotSpringTriggeredToday)
            return true; // 今天已触发，保持开启
          
          // 今天还没抽过，进行一次抽奖
          if (!_hotSpringRollDoneToday)
          {
            _hotSpringRollDoneToday = true;
            
            // 下雪时概率大幅提升至30%
            var w = WeatherService.CachedWeather;
            bool isSnowing = (w != null && w.Code >= 13 && w.Code <= 17); // 雪的天气代码
            double probability = isSnowing ? 0.30 : 0.05;
            
            if (_random.NextDouble() < probability)
            {
              _hotSpringTriggeredToday = true;
              return true;
            }
          }
          
          return false;
        }
      });

      // 13. 鲸鱼 (WindowView) - 启动后0.05%概率，每日计算一次，触发时强制好天气
      _rules.Add(new SceneryRule
      {
        Name = "Whale",
        EnvType = Env_Whale,
        Condition = () =>
        {
          CheckDailyReset();
          
          if (_whaleTriggeredToday)
            return true; // 今天已触发，保持开启（无视天气）
          
          // 今天还没抽过，进行一次抽奖
          if (!_whaleRollDoneToday)
          {
            _whaleRollDoneToday = true; // 标记已抽过
            
            if (_random.NextDouble() < 0.0005) // 0.05%概率
            {
              _whaleTriggeredToday = true;
              IsWhaleSystemTriggered = true; // 标记为系统抽中
              
              // 不强制切换时段，黄昏和晚上的鲸鱼也很美
              ChillEnvPlugin.Log?.LogWarning("[鲸鱼彩蛋] 🐋 系统抽中鲸鱼！保持当前时段...");
              
              return true;
            }
          }
          
          return false;
        }
      });
    }

    private void Update()
    {
      if (!ChillEnvPlugin.Cfg_EnableEasterEggs.Value) return;
      if (!ChillEnvPlugin.Initialized) return;

      // 处理延迟验证
      ProcessPendingActions();

      _checkTimer += Time.deltaTime;
      if (_checkTimer >= CheckInterval)
      {
        _checkTimer = 0f;
        RunAutomationLogic();
      }
    }

    /// <summary>
    /// 处理延迟验证：检查点击后的状态是否符合预期
    /// </summary>
    private void ProcessPendingActions()
    {
      List<EnvironmentType> completed = new List<EnvironmentType>();

      foreach (var kvp in _pendingActions)
      {
        if (Time.time >= kvp.Value.VerifyTime)
        {
          var env = kvp.Key;
          var action = kvp.Value;
          bool currentState = IsEnvActive(env);

          if (currentState == action.TargetState)
          {
            // 状态符合预期
            if (action.TargetState)
            {
              _autoEnabledMods.Add(env);
              ChillEnvPlugin.Log?.LogInfo($"[自动托管] ✓ 已开启: {action.RuleName}");
            }
            else
            {
              _autoEnabledMods.Remove(env);
              ChillEnvPlugin.Log?.LogInfo($"[自动托管] ✓ 已关闭: {action.RuleName}");
            }
          }
          else
          {
            // 状态不符合预期（可能点击失败或用户已手动操作）
            ChillEnvPlugin.Log?.LogInfo(
              $"[自动托管] ✗ 状态验证失败: {action.RuleName} (期望={action.TargetState}, 实际={currentState})"
            );
          }

          completed.Add(env);
        }
      }

      foreach (var env in completed)
      {
        _pendingActions.Remove(env);
      }
    }

    private void RunAutomationLogic()
    {
      if (IsEnvActive(Env_DeepSea))
      {
        CleanupAllAutoMods();
        return;
      }

      // Step 1: 检查已托管的环境，关闭不满足条件的
      List<EnvironmentType> toCheck = new List<EnvironmentType>(_autoEnabledMods);
      foreach (var envType in toCheck)
      {
        // 用户手动操作过的不管
        if (UserInteractedMods.Contains(envType))
        {
          _autoEnabledMods.Remove(envType);
          continue;
        }

        // 如果正在等待验证，跳过
        if (_pendingActions.ContainsKey(envType)) continue;

        var rule = _rules.Find(r => r.EnvType == envType);
        if (rule != null && !rule.Condition())
        {
          DisableMod(rule.Name, envType);
        }
      }

      // Step 2: 检查未托管的环境，开启满足条件的
      foreach (var rule in _rules)
      {
        // 跳过：用户操作过的、已托管的、等待验证的
        if (UserInteractedMods.Contains(rule.EnvType)) continue;
        if (_autoEnabledMods.Contains(rule.EnvType)) continue;
        if (_pendingActions.ContainsKey(rule.EnvType)) continue;

        // 已经开着了就不管
        if (IsEnvActive(rule.EnvType)) continue;

        if (rule.Condition())
        {
          EnableMod(rule.Name, rule.EnvType);
        }
      }
    }

    private void EnableMod(string ruleName, EnvironmentType env)
    {
      // 1. 冷却检查
      if (_lastClickTime.TryGetValue(env, out float lastTime))
      {
        if (Time.time - lastTime < ClickCooldown)
        {
          return; // 冷却中
        }
      }

      // 2. 再次确认当前状态
      if (IsEnvActive(env))
      {
        // 已经是开启状态，直接加入托管列表
        _autoEnabledMods.Add(env);
        ChillEnvPlugin.Log?.LogInfo($"[自动托管] ↻ 已是开启状态: {ruleName}");
        return;
      }

      // 3. 执行点击
      if (EnvRegistry.TryGet(env, out var ctrl))
      {
        ChillEnvPlugin.Log?.LogInfo($"[自动托管] → 点击开启: {ruleName}");
        ChillEnvPlugin.SimulateClickMainIcon(ctrl);
        _lastClickTime[env] = Time.time;

        // 4. 登记延迟验证任务
        _pendingActions[env] = new PendingAction
        {
          TargetState = true,
          VerifyTime = Time.time + VerifyDelay,
          RuleName = ruleName
        };
      }
    }

    private void DisableMod(string ruleName, EnvironmentType env)
    {
      // 1. 冷却检查
      if (_lastClickTime.TryGetValue(env, out float lastTime))
      {
        if (Time.time - lastTime < ClickCooldown)
        {
          return;
        }
      }

      // 2. 再次确认当前状态
      if (!IsEnvActive(env))
      {
        // 已经是关闭状态
        _autoEnabledMods.Remove(env);
        ChillEnvPlugin.Log?.LogInfo($"[自动托管] ↻ 已是关闭状态: {ruleName}");
        return;
      }

      // 3. 执行点击
      if (EnvRegistry.TryGet(env, out var ctrl))
      {
        ChillEnvPlugin.Log?.LogInfo($"[自动托管] → 点击关闭: {ruleName}");
        ChillEnvPlugin.SimulateClickMainIcon(ctrl);
        _lastClickTime[env] = Time.time;

        // 4. 登记延迟验证任务
        _pendingActions[env] = new PendingAction
        {
          TargetState = false,
          VerifyTime = Time.time + VerifyDelay,
          RuleName = ruleName
        };
      }
    }

    private void CleanupAllAutoMods()
    {
      List<EnvironmentType> toClean = new List<EnvironmentType>(_autoEnabledMods);
      foreach (var env in toClean)
      {
        var rule = _rules.Find(r => r.EnvType == env);
        if (rule != null)
        {
          DisableMod(rule.Name, env);
        }
      }
    }

    public bool IsAutoManaged(EnvironmentType type)
    {
      return _autoEnabledMods.Contains(type);
    }

    /// <summary>
    /// 每日重置概率触发标志
    /// </summary>
    private void CheckDailyReset()
    {
      DateTime today = DateTime.Today;
      if (_lastDailyCheck.Date != today)
      {
        _lastDailyCheck = today;
        _windBellTriggeredToday = false;
        _windBellRollDoneToday = false;
        _hotSpringTriggeredToday = false;
        _hotSpringRollDoneToday = false;
        _whaleTriggeredToday = false;
        _whaleRollDoneToday = false;
        _spaceTriggeredToday = false;
        _spaceRollDoneToday = false;
        _blueButterflyTriggeredToday = false;
        IsWhaleSystemTriggered = false; // 重置鲸鱼系统触发标志
        ChillEnvPlugin.Log?.LogDebug("[每日重置] 概率触发标志已重置");
      }
    }

    /// <summary>
    /// 状态检测：读取 VolumeSlider 或 WindowView 的真实值
    /// </summary>
    private bool IsEnvActive(EnvironmentType env)
    {
      if (!EnvRegistry.TryGet(env, out var ctrl)) return false;

      try
      {
        var ctrlType = ctrl.GetType();

        // --- 环境音：通过 _ambientSoundBehavior 检查滑块值 ---
        var ambientBehaviorField = ctrlType.GetField("_ambientSoundBehavior",
          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (ambientBehaviorField != null)
        {
          var ambientBehavior = ambientBehaviorField.GetValue(ctrl);
          if (ambientBehavior != null)
          {
            var sliderField = ambientBehavior.GetType().GetField("_volumeSlider",
              BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            if (sliderField != null)
            {
              var sliderObj = sliderField.GetValue(ambientBehavior);
              if (sliderObj != null)
              {
                var valueProp = sliderObj.GetType().GetProperty("value");
                if (valueProp != null)
                {
                  float val = (float)valueProp.GetValue(sliderObj);
                  
                  // 调试日志
                  ChillEnvPlugin.Log?.LogDebug($"[状态检测] {env} Slider值={val:F3}");
                  
                  // 与游戏源码保持一致：ChangeMute() 判断 > 0f
                  return val > 0f;
                }
              }
            }
          }
        }

        // --- 窗景：检查 IsActive ---
        var windowField = ctrlType.GetField("_windowBehavior",
          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (windowField != null)
        {
          var behaviorObj = windowField.GetValue(ctrl);
          if (behaviorObj != null)
          {
            var typeProp = behaviorObj.GetType().GetProperty("WindowViewType");
            if (typeProp != null)
            {
              var winType = (WindowViewType)typeProp.GetValue(behaviorObj);
              if (SaveDataManager.Instance.WindowViewDic.TryGetValue(winType, out var data))
              {
                return data.IsActive;
              }
            }
          }
        }

        // --- Fallback ---
        var activeProp = ctrlType.GetProperty("IsActive",
          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (activeProp != null) return (bool)activeProp.GetValue(ctrl);

        var activeField = ctrlType.GetField("_isActive",
          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (activeField != null) return (bool)activeField.GetValue(ctrl);

      }
      catch (Exception ex)
      {
        ChillEnvPlugin.Log?.LogError($"[CheckState] Error: {ex.Message}");
      }

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

    /// <summary>
    /// 判断是否在农历春节期间（除夕到正月初五）
    /// </summary>
    private bool IsLunarNewYearPeriod(DateTime gregorianDate)
    {
      try
      {
        var chineseCalendar = new System.Globalization.ChineseLunisolarCalendar();
        
        // 获取农历日期
        int lunarMonth = chineseCalendar.GetMonth(gregorianDate);
        int lunarDay = chineseCalendar.GetDayOfMonth(gregorianDate);
        
        // 正月初一到初五
        if (lunarMonth == 1 && lunarDay >= 1 && lunarDay <= 5)
        {
          return true;
        }
        
        // 除夕（十二月最后一天）
        if (lunarMonth == 12)
        {
          int lunarYear = chineseCalendar.GetYear(gregorianDate);
          int daysInMonth = chineseCalendar.GetDaysInMonth(lunarYear, 12);
          if (lunarDay == daysInMonth) // 是最后一天（除夕）
          {
            return true;
          }
        }
        
        return false;
      }
      catch
      {
        // 如果农历计算失败，返回 false
        return false;
      }
    }
  }
}