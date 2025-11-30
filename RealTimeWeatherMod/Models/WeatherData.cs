using System;

namespace ChillWithYou.EnvSync.Models
{
    public enum WeatherCondition { Clear, Cloudy, Rainy, Snowy, Foggy, Unknown }

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