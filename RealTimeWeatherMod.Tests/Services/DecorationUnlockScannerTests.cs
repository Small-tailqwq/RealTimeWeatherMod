using System;
using System.Collections.Generic;
using Xunit;
using ChillWithYou.EnvSync.Services;

namespace RealTimeWeatherMod.Tests.Services
{
    public class DecorationUnlockScannerTests
    {
        [Fact]
        public void Unlock_UnlocksKnownDecorationGroupAndSkipsLoaderFields()
        {
            var service = new FakeKnownService();
            var infoLogs = new List<string>();
            var errorLogs = new List<string>();

            var unlocked = DecorationUnlockScanner.Unlock(service, infoLogs.Add, errorLogs.Add, false);

            Assert.Equal(2, unlocked);
            Assert.False(service.Decoration.SkinDic[1]._isLocked.Value);
            Assert.False(service.Decoration.SkinDic[2]._isLocked.Value);
            Assert.True(service.LoaderGroup.LoaderDic[99]._isLocked.Value);
            Assert.Empty(errorLogs);
        }

        private sealed class FakeKnownService
        {
            private FakeUnlockEnvironment _environment = new FakeUnlockEnvironment();
            private FakeUnlockDecoration _decoration = new FakeUnlockDecoration();
            private FakeMasterDataLoaderGroup _masterDataLoader = new FakeMasterDataLoaderGroup();

            public FakeUnlockEnvironment Environment { get { return _environment; } }
            public FakeUnlockDecoration Decoration { get { return _decoration; } }
            public FakeMasterDataLoaderGroup LoaderGroup { get { return _masterDataLoader; } }
        }

        private sealed class FakeUnlockEnvironment
        {
            private Dictionary<int, FakeUnlockItem> _environmentDic = new Dictionary<int, FakeUnlockItem>
            {
                { 10, new FakeUnlockItem(true) }
            };

            public Dictionary<int, FakeUnlockItem> EnvironmentDic { get { return _environmentDic; } }
        }

        private sealed class FakeUnlockDecoration
        {
            private Dictionary<int, FakeUnlockItem> _skinDic = new Dictionary<int, FakeUnlockItem>
            {
                { 1, new FakeUnlockItem(true) },
                { 2, new FakeUnlockItem(true) }
            };

            public Dictionary<int, FakeUnlockItem> SkinDic { get { return _skinDic; } }
        }

        private sealed class FakeMasterDataLoaderGroup
        {
            private Dictionary<int, FakeUnlockItem> _loaderDic = new Dictionary<int, FakeUnlockItem>
            {
                { 99, new FakeUnlockItem(true) }
            };

            public Dictionary<int, FakeUnlockItem> LoaderDic { get { return _loaderDic; } }
        }

        private sealed class FakeUnlockItem
        {
            public readonly FakeReactiveBool _isLocked;

            public FakeUnlockItem(bool isLocked)
            {
                _isLocked = new FakeReactiveBool(isLocked);
            }
        }

        private sealed class FakeReactiveBool
        {
            public FakeReactiveBool(bool value)
            {
                Value = value;
            }

            public bool Value { get; set; }
        }
    }
}
