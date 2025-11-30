using System;
using System.Collections.Generic;
using UnityEngine;
using Bulbul;
using ChillWithYou.EnvSync.Services;
using ChillWithYou.EnvSync.Utils;

namespace ChillWithYou.EnvSync.Core
{
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

        // 实装枚举
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
            // 1. 烟花
            _rules.Add(new SceneryRule
            {
                Name = "Fireworks",
                EnvType = Env_Fireworks,
                Condition = () => {
                    DateTime now = DateTime.Now;
                    bool isNight = IsNight();
                    bool isNewYear = (now.Month == 1 && now.Day == 1);
                    bool isSpringFestival = (now.Month == 1 || now.Month == 2);
                    return isNight && (isNewYear || isSpringFestival);
                }
            });

            // 2. 做饭
            _rules.Add(new SceneryRule
            {
                Name = "CookingAudio",
                EnvType = Env_Cooking,
                Condition = () => {
                    int h = DateTime.Now.Hour;
                    int m = DateTime.Now.Minute;
                    double time = h + m / 60.0;
                    return (time >= 11.5 && time <= 12.5) || (time >= 17.5 && time <= 18.5);
                }
            });

            // 3. 空调
            _rules.Add(new SceneryRule
            {
                Name = "AC_Audio",
                EnvType = Env_AC,
                Condition = () => {
                    var w = WeatherService.CachedWeather;
                    if (w == null) return false;
                    return w.Temperature > 30 || w.Temperature < 5;
                }
            });

            // 4. 樱花
            _rules.Add(new SceneryRule
            {
                Name = "Sakura",
                EnvType = Env_Sakura,
                Condition = () => {
                    return GetSeason() == Season.Spring && IsDay() && IsGoodWeather();
                }
            });

            // 5. 蝉鸣
            _rules.Add(new SceneryRule
            {
                Name = "Cicadas",
                EnvType = Env_Cicada,
                Condition = () => {
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
}