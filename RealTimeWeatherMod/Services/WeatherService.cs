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
            if (code >= 0 && code <= 3) return WeatherCondition.Clear;
            if (code >= 4 && code <= 9) return WeatherCondition.Cloudy;
            if (code >= 10 && code <= 20) return WeatherCondition.Rainy;
            if (code >= 21 && code <= 25) return WeatherCondition.Snowy;
            return WeatherCondition.Unknown;
        }
    }
}