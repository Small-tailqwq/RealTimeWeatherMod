using ChillWithYou.EnvSync.Core;
using ChillWithYou.EnvSync.Models;
using Xunit;

namespace RealTimeWeatherMod.Tests.Core
{
    public class WeatherUiStateTests
    {
        [Fact]
        public void NextWeatherText_ShowDisabled_ClearsText()
        {
            string next = WeatherUiState.NextWeatherText(showWeatherOnDate: false, currentText: "雷雨 19°C", latestWeather: null);
            Assert.Equal(string.Empty, next);
        }

        [Fact]
        public void NextWeatherText_NewWeather_FormatsText()
        {
            var weather = new WeatherInfo { Text = "多云", Temperature = 24 };

            string next = WeatherUiState.NextWeatherText(showWeatherOnDate: true, currentText: "", latestWeather: weather);

            Assert.Equal("多云 24°C", next);
        }

        [Fact]
        public void NextWeatherText_FetchFailed_KeepsPreviousText()
        {
            string next = WeatherUiState.NextWeatherText(showWeatherOnDate: true, currentText: "晴 30°C", latestWeather: null);
            Assert.Equal("晴 30°C", next);
        }
    }
}
