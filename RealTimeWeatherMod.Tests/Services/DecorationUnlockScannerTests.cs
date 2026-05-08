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

        [Fact]
        public void Unlock_UsesFallbackForUnknownDecorationGroup()
        {
            var service = new FakeFallbackService();
            var infoLogs = new List<string>();

            var unlocked = DecorationUnlockScanner.Unlock(service, infoLogs.Add, _ => { }, false);

            Assert.Equal(3, unlocked);
            Assert.False(service.Decoration.SkinDic[1]._isLocked.Value);
            Assert.False(service.Decoration.SkinDic[2]._isLocked.Value);
            Assert.False(service.BonusGroup.BonusDic[200]._isLocked.Value);
            Assert.True(infoLogs.Exists(x => x.Contains("_bonusGroup -> _bonusDic")));
        }

        [Fact]
        public void Unlock_IgnoresNonMatchingEntriesInsideMixedDictionary()
        {
            var service = new FakeMixedService();

            var unlocked = DecorationUnlockScanner.Unlock(service, _ => { }, _ => { }, false);

            Assert.Equal(3, unlocked);
            Assert.False(service.Decoration.SkinDic[1]._isLocked.Value);
            Assert.False(service.Decoration.SkinDic[2]._isLocked.Value);
            Assert.False(((FakeUnlockItem)service.MixedGroup.MixedDic[300])._isLocked.Value);
        }

        private sealed class FakeFallbackService
        {
            private FakeUnlockDecoration _decoration = new FakeUnlockDecoration();
            private FakeBonusGroup _bonusGroup = new FakeBonusGroup();

            public FakeUnlockDecoration Decoration { get { return _decoration; } }
            public FakeBonusGroup BonusGroup { get { return _bonusGroup; } }

            public void RelockDecoration(int key)
            {
                _decoration.SkinDic[key]._isLocked.Value = true;
            }

            public void RelockBonus(int key)
            {
                _bonusGroup.BonusDic[key]._isLocked.Value = true;
            }

            public void DropBonusGroup()
            {
                _bonusGroup = null;
            }
        }

        private sealed class FakeBonusGroup
        {
            private Dictionary<int, FakeUnlockItem> _bonusDic = new Dictionary<int, FakeUnlockItem>
            {
                { 200, new FakeUnlockItem(true) }
            };

            public Dictionary<int, FakeUnlockItem> BonusDic { get { return _bonusDic; } }
        }

        private sealed class FakeMixedService
        {
            private FakeUnlockDecoration _decoration = new FakeUnlockDecoration();
            private FakeMixedGroup _mixedGroup = new FakeMixedGroup();

            public FakeUnlockDecoration Decoration { get { return _decoration; } }
            public FakeMixedGroup MixedGroup { get { return _mixedGroup; } }
        }

        private sealed class FakeMixedGroup
        {
            private Dictionary<int, object> _mixedDic = new Dictionary<int, object>
            {
                { 300, new FakeUnlockItem(true) },
                { 301, "noise" }
            };

            public Dictionary<int, object> MixedDic { get { return _mixedDic; } }
        }

        [Fact]
        public void Unlock_DoesNotTouchEnvironmentDictionary()
        {
            var service = new FakeKnownService();

            DecorationUnlockScanner.Unlock(service, _ => { }, _ => { }, false);

            Assert.True(service.Environment.EnvironmentDic[10]._isLocked.Value);
            Assert.False(service.Decoration.SkinDic[1]._isLocked.Value);
            Assert.False(service.Decoration.SkinDic[2]._isLocked.Value);
        }

        [Fact]
        public void Unlock_ReusesLearnedAccessorWithinTheSameSession()
        {
            var service = new FakeFallbackService();
            DecorationUnlockScanner.Unlock(service, _ => { }, _ => { }, false);

            service.RelockBonus(200);
            var infoLogs = new List<string>();

            var unlocked = DecorationUnlockScanner.Unlock(service, infoLogs.Add, _ => { }, false);

            Assert.Equal(1, unlocked);
            Assert.False(service.BonusGroup.BonusDic[200]._isLocked.Value);
            Assert.False(infoLogs.Exists(x => x.Contains("[发现] ") && x.Contains("_bonusGroup -> _bonusDic")));
        }

        [Fact]
        public void Unlock_SkipsStaleCachedAccessorAndStillRunsKnownGroups()
        {
            var service = new FakeFallbackService();
            DecorationUnlockScanner.Unlock(service, _ => { }, _ => { }, false);

            service.DropBonusGroup();
            service.RelockDecoration(1);

            var unlocked = DecorationUnlockScanner.Unlock(service, _ => { }, _ => { }, false);

            Assert.Equal(1, unlocked);
            Assert.False(service.Decoration.SkinDic[1]._isLocked.Value);
        }
    }
}
