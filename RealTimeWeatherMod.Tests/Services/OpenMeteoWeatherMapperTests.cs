using System;
using ChillWithYou.EnvSync.Models;
using ChillWithYou.EnvSync.Services;
using Xunit;

namespace RealTimeWeatherMod.Tests.Services
{
    public class OpenMeteoWeatherMapperTests
    {
        private static WeatherInfo Map(
            int weatherCode,
            double temperature = 20d,
            double precipitation = 0d,
            double rain = 0d,
            double showers = 0d,
            double snowfall = 0d,
            double cloudCover = 0d)
        {
            return OpenMeteoWeatherMapper.Map(
                weatherCode,
                temperature,
                precipitation,
                rain,
                showers,
                snowfall,
                cloudCover,
                DateTime.UtcNow);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        public void Map_WithHighCloudCover_ReturnsCloudy(int weatherCode)
        {
            var info = Map(weatherCode, cloudCover: 80d);

            Assert.Equal(4, info.Code);
            Assert.Equal("Cloudy", info.Text);
            Assert.Equal(WeatherCondition.Cloudy, info.Condition);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void Map_PartlyCloudyCodes_ReturnCloudy(int weatherCode)
        {
            var info = Map(weatherCode);

            Assert.Equal(4, info.Code);
            Assert.Equal("Cloudy", info.Text);
            Assert.Equal(WeatherCondition.Cloudy, info.Condition);
        }

        [Theory]
        [InlineData(45)]
        [InlineData(48)]
        public void Map_FogCodes_ReturnFog(int weatherCode)
        {
            var info = Map(weatherCode);

            Assert.Equal(26, info.Code);
            Assert.Equal("Fog", info.Text);
            Assert.Equal(WeatherCondition.Foggy, info.Condition);
        }

        [Theory]
        [InlineData(51)]
        [InlineData(53)]
        [InlineData(61)]
        [InlineData(80)]
        public void Map_LightRainCodes_ReturnLightRain(int weatherCode)
        {
            var info = Map(weatherCode);

            Assert.Equal(13, info.Code);
            Assert.Equal("LightRain", info.Text);
            Assert.Equal(WeatherCondition.Rainy, info.Condition);
        }

        [Theory]
        [InlineData(65)]
        [InlineData(67)]
        [InlineData(81)]
        [InlineData(82)]
        public void Map_HeavyRainCodes_ReturnHeavyRain(int weatherCode)
        {
            var info = Map(weatherCode);

            Assert.Equal(14, info.Code);
            Assert.Equal("HeavyRain", info.Text);
            Assert.Equal(WeatherCondition.Rainy, info.Condition);
        }

        [Theory]
        [InlineData(95)]
        [InlineData(96)]
        [InlineData(99)]
        public void Map_ThunderCodes_ReturnThunderRain(int weatherCode)
        {
            var info = Map(weatherCode);

            Assert.Equal(11, info.Code);
            Assert.Equal("ThunderRain", info.Text);
            Assert.Equal(WeatherCondition.Rainy, info.Condition);
        }

        [Theory]
        [InlineData(71)]
        [InlineData(73)]
        [InlineData(75)]
        [InlineData(77)]
        [InlineData(85)]
        [InlineData(86)]
        public void Map_SnowCodes_ReturnSnow(int weatherCode)
        {
            var info = Map(weatherCode);

            Assert.Equal(21, info.Code);
            Assert.Equal("Snow", info.Text);
            Assert.Equal(WeatherCondition.Snowy, info.Condition);
        }

        [Theory]
        [InlineData(0, 0.1d, 13)]
        [InlineData(0, 2.5d, 14)]
        [InlineData(0, 5d, 14)]
        public void Map_ClearCodeWithPrecipitation_ReturnsRain(int weatherCode, double precipitation, int expectedCode)
        {
            var info = Map(weatherCode, precipitation: precipitation);

            Assert.Equal(expectedCode, info.Code);
        }

        [Fact]
        public void Map_ClearDayWithLowCloudCover_ReturnsClear()
        {
            var info = Map(0, cloudCover: 30d);

            Assert.Equal(1, info.Code);
            Assert.Equal("Clear", info.Text);
            Assert.Equal(WeatherCondition.Clear, info.Condition);
        }

        [Fact]
        public void Map_SnowfallOverridesRainCode()
        {
            var info = Map(weatherCode: 65, precipitation: 10d, snowfall: 1d);

            Assert.Equal(21, info.Code);
            Assert.Equal("Snow", info.Text);
        }

        [Fact]
        public void Map_ThunderOverridesHeavyRain()
        {
            var info = Map(weatherCode: 95, precipitation: 10d);

            Assert.Equal(11, info.Code);
            Assert.Equal("ThunderRain", info.Text);
        }

        [Fact]
        public void Map_UnrecognizedCode_ReturnsClearFallback()
        {
            var info = Map(weatherCode: 999);

            Assert.Equal(1, info.Code);
            Assert.Equal("Unknown", info.Text);
            Assert.Equal(WeatherCondition.Clear, info.Condition);
        }

        [Fact]
        public void Map_ClearCodeWithExtremeCloudCover_ReturnsCloudy()
        {
            var info = Map(weatherCode: 0, cloudCover: 999d);

            Assert.Equal(4, info.Code);
            Assert.Equal("Cloudy", info.Text);
        }

        [Fact]
        public void Map_Temperature_RoundedToInteger()
        {
            var info = Map(0, temperature: 23.7d);

            Assert.Equal(24, info.Temperature);
        }

        [Fact]
        public void Map_SetsUpdateTime()
        {
            var now = DateTime.UtcNow;

            var info = OpenMeteoWeatherMapper.Map(0, 20d, 0d, 0d, 0d, 0d, 0d, now);

            Assert.Equal(now, info.UpdateTime);
        }
    }
}
