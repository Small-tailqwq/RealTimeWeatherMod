using System;
using System.Collections;
using System.Globalization;
using UnityEngine.Networking;
using ChillWithYou.EnvSync.Models;
using Bulbul;

namespace ChillWithYou.EnvSync.Services
{
    public class WeatherService
    {
        private const string ProviderOpenMeteo = "OpenMeteo";
        private const string ProviderSeniverseDisplayName = "心知天气";

        private static WeatherInfo _cachedWeather;
        private static DateTime _lastFetchTime;
        private static string _lastLocation;
        public static WeatherInfo CachedWeather => _cachedWeather;
        public static bool LastFetchSucceeded { get; private set; }
        private static readonly string _encryptedDefaultKey = "7Mr4YSR87bFvE4zDgj6NbuBKgz4EiPYEnRTQ0RIaeSU=";
        public static bool HasDefaultKey => !string.IsNullOrEmpty(_encryptedDefaultKey);

        public static bool HasUsableProvider(string provider, string apiKey)
        {
            return IsOpenMeteoProvider(provider) || !string.IsNullOrEmpty(apiKey) || HasDefaultKey;
        }

        private static bool IsOpenMeteoProvider(string provider)
        {
            return string.Equals(provider?.Trim(), ProviderOpenMeteo, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetCurrentProviderTargetDescription(string seniverseLocation)
        {
            if (IsOpenMeteoProvider(ChillEnvPlugin.Cfg_WeatherProvider?.Value))
            {
                double latitude = ChillEnvPlugin.Cfg_OpenMeteoLatitude?.Value ?? 39.9042d;
                double longitude = ChillEnvPlugin.Cfg_OpenMeteoLongitude?.Value ?? 116.4074d;
                return $"{ProviderOpenMeteo} 坐标 {FormatCoordinates(latitude, longitude)}";
            }

            return $"{ProviderSeniverseDisplayName} 城市 {NormalizeLocation(seniverseLocation)}";
        }

        private static int GetCacheExpiryMinutes()
        {
            int minutes = ChillEnvPlugin.Cfg_CacheExpiryMinutes?.Value ?? 60;
            return Math.Max(1, minutes);
        }

        private static string NormalizeLocation(string location)
        {
            return location?.Trim() ?? string.Empty;
        }

        private static string FormatCoordinates(double latitude, double longitude)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.####}, {1:0.####}", latitude, longitude);
        }

        private static string GetCacheKey(string location)
        {
            if (IsOpenMeteoProvider(ChillEnvPlugin.Cfg_WeatherProvider?.Value))
            {
                double latitude = ChillEnvPlugin.Cfg_OpenMeteoLatitude?.Value ?? 39.9042d;
                double longitude = ChillEnvPlugin.Cfg_OpenMeteoLongitude?.Value ?? 116.4074d;
                return string.Format(CultureInfo.InvariantCulture, "OpenMeteo:{0:0.####},{1:0.####}", latitude, longitude);
            }

            return NormalizeLocation(location).ToLowerInvariant();
        }

        private static bool HasAnyCacheNormalized(string cacheKey)
        {
            return _cachedWeather != null &&
                string.Equals(_lastLocation, cacheKey, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasValidCacheNormalized(string cacheKey)
        {
            return HasAnyCacheNormalized(cacheKey) &&
                DateTime.Now - _lastFetchTime < TimeSpan.FromMinutes(GetCacheExpiryMinutes());
        }

        public static bool HasValidCache(string location)
        {
            return HasValidCacheNormalized(GetCacheKey(location));
        }

        public static bool TryGetCacheRemainingSeconds(string location, out float seconds)
        {
            seconds = 0f;
            string cacheKey = GetCacheKey(location);
            if (!HasValidCacheNormalized(cacheKey))
            {
                return false;
            }

            TimeSpan remaining = TimeSpan.FromMinutes(GetCacheExpiryMinutes()) - (DateTime.Now - _lastFetchTime);
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            seconds = (float)remaining.TotalSeconds;
            return true;
        }

        public static IEnumerator FetchWeather(string apiKey, string location, bool force, Action<WeatherInfo> onComplete)
        {
            LastFetchSucceeded = false;
            string cacheKey = GetCacheKey(location);

            if (!force && HasValidCacheNormalized(cacheKey))
            {
                LastFetchSucceeded = true;
                onComplete?.Invoke(_cachedWeather);
                yield break;
            }

            WeatherInfo fallback = HasAnyCacheNormalized(cacheKey) ? _cachedWeather : null;
            if (IsOpenMeteoProvider(ChillEnvPlugin.Cfg_WeatherProvider?.Value))
            {
                yield return FetchOpenMeteoWeather(cacheKey, fallback, onComplete);
                yield break;
            }

            yield return FetchSeniverseWeather(apiKey, location, cacheKey, fallback, onComplete);
        }

        private static IEnumerator FetchSeniverseWeather(
            string apiKey,
            string location,
            string cacheKey,
            WeatherInfo fallback,
            Action<WeatherInfo> onComplete)
        {
            string finalKey = apiKey;
            if (string.IsNullOrEmpty(finalKey) && HasDefaultKey)
            {
                finalKey = KeySecurity.Decrypt(_encryptedDefaultKey);
            }

            if (string.IsNullOrEmpty(finalKey))
            {
                ChillEnvPlugin.Log?.LogWarning("[心知天气] 未配置 API Key 且无内置 Key");
                onComplete?.Invoke(fallback);
                yield break;
            }

            string url = $"https://api.seniverse.com/v3/weather/now.json?key={finalKey}&location={UnityWebRequest.EscapeURL(location)}&language=zh-Hans&unit=c";
            ChillEnvPlugin.Log?.LogInfo($"[心知天气] 发起天气请求: {NormalizeLocation(location)}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    ChillEnvPlugin.Log?.LogWarning($"[心知天气] 天气请求失败: {request.error}");
                    onComplete?.Invoke(fallback);
                    yield break;
                }

                try
                {
                    var weather = ParseWeatherJson(request.downloadHandler.text);
                    if (weather != null)
                    {
                        StoreCache(cacheKey, weather);
                        ChillEnvPlugin.Log?.LogInfo($"[心知天气] 天气数据更新: {weather}");
                        onComplete?.Invoke(weather);
                    }
                    else
                    {
                        ChillEnvPlugin.Log?.LogWarning("[心知天气] 天气解析失败");
                        onComplete?.Invoke(fallback);
                    }
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogError($"[心知天气] 天气解析异常: {ex}");
                    onComplete?.Invoke(fallback);
                }
            }
        }

        private static IEnumerator FetchOpenMeteoWeather(string cacheKey, WeatherInfo fallback, Action<WeatherInfo> onComplete)
        {
            double latitude = ChillEnvPlugin.Cfg_OpenMeteoLatitude?.Value ?? 39.9042d;
            double longitude = ChillEnvPlugin.Cfg_OpenMeteoLongitude?.Value ?? 116.4074d;
            string url = string.Format(
                CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,relative_humidity_2m,precipitation,rain,showers,snowfall,weather_code,is_day,cloud_cover&timezone=auto&forecast_days=1",
                latitude,
                longitude);

            ChillEnvPlugin.Log?.LogInfo($"[OpenMeteo] 发起天气请求: {FormatCoordinates(latitude, longitude)}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    ChillEnvPlugin.Log?.LogWarning($"[OpenMeteo] 请求失败: {request.error}");
                    onComplete?.Invoke(fallback);
                    yield break;
                }

                try
                {
                    var weather = ParseOpenMeteoWeatherJson(request.downloadHandler.text);
                    if (weather != null)
                    {
                        StoreCache(cacheKey, weather);
                        ChillEnvPlugin.Log?.LogInfo($"[OpenMeteo] 天气数据更新: {weather}");
                        onComplete?.Invoke(weather);
                    }
                    else
                    {
                        ChillEnvPlugin.Log?.LogWarning("[OpenMeteo] 解析失败");
                        onComplete?.Invoke(fallback);
                    }
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogError($"[OpenMeteo] 解析异常: {ex}");
                    onComplete?.Invoke(fallback);
                }
            }
        }

        private static void StoreCache(string cacheKey, WeatherInfo weather)
        {
            _cachedWeather = weather;
            _lastFetchTime = DateTime.Now;
            _lastLocation = cacheKey;
            LastFetchSucceeded = true;
        }

        public static void InvalidateCache()
        {
            _cachedWeather = null;
            _lastLocation = null;
            _lastFetchTime = DateTime.MinValue;
            LastFetchSucceeded = false;
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

        private static WeatherInfo ParseOpenMeteoWeatherJson(string json)
        {
            string current = ExtractObjectProperty(json, "\"current\"");
            if (string.IsNullOrEmpty(current)) return null;

            int weatherCode = ExtractIntNumberValue(current, "\"weather_code\"");
            double temperature = ExtractDoubleValue(current, "\"temperature_2m\"");
            double precipitation = ExtractDoubleValue(current, "\"precipitation\"");
            double rain = ExtractDoubleValue(current, "\"rain\"");
            double showers = ExtractDoubleValue(current, "\"showers\"");
            double snowfall = ExtractDoubleValue(current, "\"snowfall\"");
            double cloudCover = ExtractDoubleValue(current, "\"cloud_cover\"");

            return OpenMeteoWeatherMapper.Map(
                weatherCode,
                temperature,
                precipitation,
                rain,
                showers,
                snowfall,
                cloudCover,
                DateTime.Now);
        }

        public static IEnumerator FetchSunSchedule(string apiKey, string location, Action<SunData> onComplete)
        {
            if (IsOpenMeteoProvider(ChillEnvPlugin.Cfg_WeatherProvider?.Value))
            {
                yield return FetchOpenMeteoSunSchedule(onComplete);
                yield break;
            }

            yield return FetchSeniverseSunSchedule(apiKey, location, onComplete);
        }

        private static IEnumerator FetchSeniverseSunSchedule(string apiKey, string location, Action<SunData> onComplete)
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

            string url = $"https://api.seniverse.com/v3/geo/sun.json?key={finalKey}&location={UnityWebRequest.EscapeURL(location)}&language=zh-Hans&start=0&days=1";
            ChillEnvPlugin.Log?.LogInfo($"[心知天气] 发起日出日落请求: {NormalizeLocation(location)}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    ChillEnvPlugin.Log?.LogWarning($"[心知天气] 日出日落请求失败: {request.error}");
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
                    ChillEnvPlugin.Log?.LogError($"[心知天气] 日出日落解析失败: {ex}");
                    onComplete?.Invoke(null);
                }
            }
        }

        private static IEnumerator FetchOpenMeteoSunSchedule(Action<SunData> onComplete)
        {
            double latitude = ChillEnvPlugin.Cfg_OpenMeteoLatitude?.Value ?? 39.9042d;
            double longitude = ChillEnvPlugin.Cfg_OpenMeteoLongitude?.Value ?? 116.4074d;
            string url = string.Format(
                CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&daily=sunrise,sunset&timezone=auto&forecast_days=1",
                latitude,
                longitude);

            ChillEnvPlugin.Log?.LogInfo($"[OpenMeteo] 发起日出日落请求: {FormatCoordinates(latitude, longitude)}");

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success || string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    ChillEnvPlugin.Log?.LogWarning($"[OpenMeteo] 日出日落请求失败: {request.error}");
                    onComplete?.Invoke(null);
                    yield break;
                }

                try
                {
                    var sunData = ParseOpenMeteoSunJson(request.downloadHandler.text);
                    onComplete?.Invoke(sunData);
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogError($"[OpenMeteo] 日出日落解析失败: {ex}");
                    onComplete?.Invoke(null);
                }
            }
        }

        private static SunData ParseSunJson(string json)
        {
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

        private static SunData ParseOpenMeteoSunJson(string json)
        {
            string sunrise = NormalizeOpenMeteoTime(ExtractFirstArrayString(json, "\"sunrise\":["));
            string sunset = NormalizeOpenMeteoTime(ExtractFirstArrayString(json, "\"sunset\":["));
            
            if (!string.IsNullOrEmpty(sunrise) && !string.IsNullOrEmpty(sunset))
            {
                return new SunData { sunrise = sunrise, sunset = sunset };
            }
            return null;
        }

        private static string NormalizeOpenMeteoTime(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            int timeStart = value.IndexOf('T');
            string time = timeStart >= 0 ? value.Substring(timeStart + 1) : value;
            return time.Length >= 5 ? time.Substring(0, 5) : time;
        }

        private static int ExtractIntValue(string json, string prefix, string suffix)
        {
            int start = json.IndexOf(prefix); if (start < 0) return 0; start += prefix.Length;
            int end = json.IndexOf(suffix, start); if (end < 0) return 0;
            string val = json.Substring(start, end - start); int.TryParse(val, out int res); return res;
        }

        private static int ExtractIntNumberValue(string json, string propertyName)
        {
            return (int)Math.Round(ExtractDoubleValue(json, propertyName));
        }

        private static double ExtractDoubleValue(string json, string propertyName)
        {
            string quotedKey = propertyName.StartsWith("\"") && propertyName.EndsWith("\"")
                ? propertyName
                : "\"" + propertyName + "\"";

            int searchStart = 0;
            int start;
            while (true)
            {
                start = json.IndexOf(quotedKey, searchStart, StringComparison.Ordinal);
                if (start < 0) return 0d;

                int colon = start + quotedKey.Length;
                while (colon < json.Length && char.IsWhiteSpace(json[colon])) colon++;
                if (colon < json.Length && json[colon] == ':')
                {
                    start = colon + 1;
                    break;
                }

                searchStart = start + 1;
            }

            while (start < json.Length && char.IsWhiteSpace(json[start]))
            {
                start++;
            }

            int end = start;
            while (end < json.Length && "-+0123456789.eE".IndexOf(json[end]) >= 0)
            {
                end++;
            }

            if (end <= start) return 0d;

            string val = json.Substring(start, end - start);
            double res;
            return double.TryParse(val, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out res) ? res : 0d;
        }

        private static string ExtractStringValue(string json, string prefix, string suffix)
        {
            int start = json.IndexOf(prefix); if (start < 0) return null; start += prefix.Length;
            int end = json.IndexOf(suffix, start); if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static string ExtractFirstArrayString(string json, string prefix)
        {
            int start = json.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0) return null;
            start += prefix.Length;

            int quoteStart = json.IndexOf('"', start);
            if (quoteStart < 0) return null;
            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return null;

            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        private static string ExtractObjectProperty(string json, string propertyName)
        {
            int nameStart = json.IndexOf(propertyName, StringComparison.Ordinal);
            if (nameStart < 0) return null;

            int colon = json.IndexOf(':', nameStart + propertyName.Length);
            if (colon < 0) return null;

            int start = json.IndexOf('{', colon + 1);
            if (start < 0) return null;

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < json.Length; i++)
            {
                char ch = json[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(start, i - start + 1);
                    }
                }
            }

            return null;
        }

        public static WeatherCondition MapCodeToCondition(int code)
        {
            return WeatherConditionMapper.FromSeniverseCode(code);
        }
    }
}
