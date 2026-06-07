using ChillWithYou.EnvSync.Core;
using Xunit;

namespace RealTimeWeatherMod.Tests.Core
{
    public class AutoManagedStateCodecTests
    {
        [Fact]
        public void Parse_IgnoresEmptyAndDuplicateEntries()
        {
            var result = AutoManagedStateCodec.Parse(" CookSimmer,Whale,,CookSimmer ");

            Assert.Equal(2, result.Count);
            Assert.Contains("CookSimmer", result);
            Assert.Contains("Whale", result);
        }

        [Fact]
        public void Serialize_ProducesStableUniqueValue()
        {
            string result = AutoManagedStateCodec.Serialize(
                new[] { "Whale", "CookSimmer", "Whale", " " });

            Assert.Equal("CookSimmer,Whale", result);
        }

        [Fact]
        public void Parse_EmptyValue_ReturnsEmptySet()
        {
            Assert.Empty(AutoManagedStateCodec.Parse(null));
        }
    }
}
