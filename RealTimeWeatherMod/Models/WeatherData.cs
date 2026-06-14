using System;

namespace ChillWithYou.EnvSync.Models
{
    public enum WeatherCondition { Clear, Cloudy, Rainy, Snowy, Foggy, Unknown }

    public static class WeatherConditionMapper
    {
        public static WeatherCondition FromSeniverseCode(int code)
        {
            if (code >= 0 && code <= 3) return WeatherCondition.Clear;
            if (code >= 4 && code <= 9) return WeatherCondition.Cloudy;
            if (code >= 10 && code <= 20) return WeatherCondition.Rainy;
            if (code >= 21 && code <= 25) return WeatherCondition.Snowy;
            if (code >= 26 && code <= 36) return WeatherCondition.Foggy;
            return WeatherCondition.Unknown;
        }
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

    // 心知天气 API 响应结构
    /* 虽然你在代码中主要使用手动 JSON 解析，
       但保留这些类定义有助于后续扩展或使用 JsonUtility
    */
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
}