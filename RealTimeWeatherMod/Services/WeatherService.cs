using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using ChillWithYou.EnvSync.Models;
using Bulbul;

namespace ChillWithYou.EnvSync.Services
{
    public class WeatherService
    {
        private static WeatherInfo _cachedWeather;
        private static DateTime _lastFetchTime;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(60);
        private static string _lastLocation;
        public static WeatherInfo CachedWeather => _cachedWeather;
        private static readonly string _encryptedDefaultKey = "7Mr4YSR87bFvE4zDgj6NbuBKgz4EiPYEnRTQ0RIaeSU=";
        public static bool HasDefaultKey => !string.IsNullOrEmpty(_encryptedDefaultKey);

        public static IEnumerator FetchWeather(string apiKey, string location, bool force, Action<WeatherInfo> onComplete)
        {
            string normalizedLocation = location?.Trim() ?? "";

            if (!force && 
                _cachedWeather != null && 
                DateTime.Now - _lastFetchTime < CacheExpiry &&
                string.Equals(_lastLocation, normalizedLocation, StringComparison.OrdinalIgnoreCase))
            {
                onComplete?.Invoke(_cachedWeather);
                yield break;
            }

            string finalKey = apiKey;
            if (string.IsNullOrEmpty(finalKey) && HasDefaultKey)
            {
                finalKey = KeySecurity.Decrypt(_encryptedDefaultKey);
            }

            if (string.IsNullOrEmpty(finalKey))
            {
                ChillEnvPlugin.Log?.LogWarning("[API] 未配置 API Key 且无内置 Key");
                onComplete?.Invoke(null);
                yield break;
            }

            string url = $"https://api.seniverse.com/v3/weather/now.json?key={finalKey}&location={UnityWebRequest.EscapeURL(location)}&language=zh-Hans&unit=c";
            ChillEnvPlugin.Log?.LogInfo($"[API] 发起请求: {location}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    ChillEnvPlugin.Log?.LogWarning($"[API] 请求失败: {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                try
                {
                    var weather = ParseWeatherJson(request.downloadHandler.text);
                    if (weather != null)
                    {
                        _cachedWeather = weather;
                        _lastFetchTime = DateTime.Now;
                        _lastLocation = normalizedLocation;
                        ChillEnvPlugin.Log?.LogInfo($"[API] 数据更新: {weather}");
                        onComplete?.Invoke(weather);
                    }
                    else
                    {
                        ChillEnvPlugin.Log?.LogWarning($"[API] 解析失败");
                        onComplete?.Invoke(null);
                    }
                }
                catch { onComplete?.Invoke(null); }
            }
        }

        /// <summary>
        /// 主动清理缓存（如切换城市时）
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedWeather = null;
            _lastLocation = null;
            _lastFetchTime = DateTime.MinValue;
        }

        private static WeatherInfo ParseWeatherJson(string json)
        {
            try
            {
                if (json.Contains("\"status\"") && !json.Contains("\"results\"")) return null;
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

        public static IEnumerator FetchSunSchedule(string apiKey, string location, Action<SunData> onComplete)
        {
            string finalKey = apiKey;
            if (string.IsNullOrEmpty(finalKey) && HasDefaultKey)
            {
                finalKey = KeySecurity.Decrypt(_encryptedDefaultKey);
            }

            if (string.IsNullOrEmpty(finalKey))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            // start=0&days=1 (Today)
            string url = $"https://api.seniverse.com/v3/geo/sun.json?key={finalKey}&location={UnityWebRequest.EscapeURL(location)}&language=zh-Hans&start=0&days=1";
            ChillEnvPlugin.Log?.LogInfo($"[API] 请求日出日落: {location}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    ChillEnvPlugin.Log?.LogWarning($"[API] 日出日落请求失败: {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                try
                {
                    var sunData = ParseSunJson(request.downloadHandler.text);
                    onComplete?.Invoke(sunData);
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogError($"[API] 日出日落解析失败: {ex}");
                    onComplete?.Invoke(null);
                }
            }
        }

        private static SunData ParseSunJson(string json)
        {
            // Simple manual parsing to avoid heavy JSON library dependency if possible, 
            // but since we have UnityEngine.JSONSerializeModule, we can try JsonUtility if the structure matches,
            // OR just use string manipulation for robustness against extra fields.
            // Given the structure is nested, manual parsing might be safer without a full JSON lib.
            
            // Structure: {"results":[{"sun":[{"date":"...","sunrise":"...","sunset":"..."}]}]}
            
            int sunIndex = json.IndexOf("\"sun\"");
            if (sunIndex < 0) return null;

            string sunrise = ExtractStringValue(json, "\"sunrise\":\"", "\"");
            string sunset = ExtractStringValue(json, "\"sunset\":\"", "\"");
            
            if (!string.IsNullOrEmpty(sunrise) && !string.IsNullOrEmpty(sunset))
            {
                return new SunData { sunrise = sunrise, sunset = sunset };
            }
            return null;
        }

        private static int ExtractIntValue(string json, string prefix, string suffix)
        {
            int start = json.IndexOf(prefix); if (start < 0) return 0; start += prefix.Length;
            int end = json.IndexOf(suffix, start); if (end < 0) return 0;
            string val = json.Substring(start, end - start); int.TryParse(val, out int res); return res;
        }

        private static string ExtractStringValue(string json, string prefix, string suffix)
        {
            int start = json.IndexOf(prefix); if (start < 0) return null; start += prefix.Length;
            int end = json.IndexOf(suffix, start); if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        public static WeatherCondition MapCodeToCondition(int code)
        {
            if (code >= 0 && code <= 3) return WeatherCondition.Clear;       // 晴
            if (code >= 4 && code <= 9) return WeatherCondition.Cloudy;      // 阴/多云
            if (code >= 10 && code <= 20) return WeatherCondition.Rainy;     // 雨
            if (code >= 21 && code <= 25) return WeatherCondition.Snowy;     // 雪
            if (code >= 26 && code <= 36) return WeatherCondition.Foggy;     // 雾/霾/沙尘暴
            return WeatherCondition.Unknown;
        }
    }
}
