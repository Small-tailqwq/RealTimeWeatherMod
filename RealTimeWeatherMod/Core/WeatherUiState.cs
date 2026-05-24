using ChillWithYou.EnvSync.Models;

namespace ChillWithYou.EnvSync.Core
{
    internal static class WeatherUiState
    {
        internal static string NextWeatherText(bool showWeatherOnDate, string currentText, WeatherInfo latestWeather)
        {
            if (!showWeatherOnDate)
            {
                return string.Empty;
            }

            if (latestWeather == null)
            {
                return string.IsNullOrEmpty(currentText) ? string.Empty : currentText;
            }

            return latestWeather.Text + " " + latestWeather.Temperature + "°C";
        }
    }
}
