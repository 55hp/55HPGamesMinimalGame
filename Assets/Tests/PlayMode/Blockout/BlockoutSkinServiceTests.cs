using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Config;
using hp55games.Mobile.Core.Save;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Tests
{
    public class BlockoutSkinServiceTests
    {
        // BlockoutSkin's fields are private with no test-facing setter (same as every other
        // IConfigAsset ScriptableObject in this repo - see BlockoutFallCurveTests, which just
        // uses CreateInstance's default field values instead). Two distinguishable skins are
        // needed here, so reflection is the least-invasive way to configure them without adding
        // a test-only public surface to BlockoutSkin itself.
        private static BlockoutSkin CreateSkin(string skinId, bool unlockedByDefault)
        {
            var skin = ScriptableObject.CreateInstance<BlockoutSkin>();
            typeof(BlockoutSkin).GetField("_skinId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(skin, skinId);
            typeof(BlockoutSkin).GetField("_unlockedByDefault", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(skin, unlockedByDefault);
            return skin;
        }

        private sealed class FakeConfigCatalogService : IConfigCatalogService
        {
            private readonly List<BlockoutSkin> _skins;
            public FakeConfigCatalogService(params BlockoutSkin[] skins) => _skins = new List<BlockoutSkin>(skins);

            public T Get<T>() where T : ScriptableObject, IConfigAsset => throw new System.NotSupportedException();

            public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject, IConfigAsset
            {
                if (typeof(T) == typeof(BlockoutSkin)) return (IReadOnlyList<T>)(object)_skins;
                return new List<T>();
            }
        }

        private sealed class FakeSaveService : ISaveService
        {
            public SaveData Data { get; } = new SaveData();
            public int SaveCallCount { get; private set; }
            public void Load() { }
            public void Save() => SaveCallCount++;
        }

        private BlockoutSkin _defaultSkin;
        private BlockoutSkin _otherSkin;
        private FakeSaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            _defaultSkin = CreateSkin("default", unlockedByDefault: true);
            _otherSkin = CreateSkin("juicy-clear", unlockedByDefault: false);
            _saveService = new FakeSaveService();
            ServiceRegistry.Register<ISaveService>(_saveService);
            ServiceRegistry.Register<IConfigCatalogService>(new FakeConfigCatalogService(_defaultSkin, _otherSkin));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_defaultSkin);
            Object.DestroyImmediate(_otherSkin);
        }

        [Test]
        public void ActiveSkin_ReturnsTheDefaultSkin_WhenNoChoiceIsPersisted()
        {
            var service = new BlockoutSkinService();

            Assert.AreSame(_defaultSkin, service.ActiveSkin);
        }

        [Test]
        public void ActiveSkin_ReturnsThePersistedSkin_WhenItMatchesACatalogEntry()
        {
            _saveService.Data.activeSkinId = "juicy-clear";
            var service = new BlockoutSkinService();

            Assert.AreSame(_otherSkin, service.ActiveSkin);
        }

        [Test]
        public void ActiveSkin_FallsBackToDefault_WhenThePersistedIdMatchesNoCatalogEntry()
        {
            // Save data outliving a renamed/removed skin asset shouldn't crash or return null -
            // the always-free default is always a safe fallback.
            _saveService.Data.activeSkinId = "a-skin-that-no-longer-exists";
            var service = new BlockoutSkinService();

            Assert.AreSame(_defaultSkin, service.ActiveSkin);
        }

        [Test]
        public void ActiveSkin_ReturnsNull_WhenTheCatalogHasNoSkins()
        {
            ServiceRegistry.Register<IConfigCatalogService>(new FakeConfigCatalogService());
            var service = new BlockoutSkinService();

            Assert.IsNull(service.ActiveSkin);
        }

        [Test]
        public void SetActiveSkin_PersistsTheChoice_AndSavesImmediately()
        {
            var service = new BlockoutSkinService();

            service.SetActiveSkin("juicy-clear");

            Assert.AreEqual("juicy-clear", _saveService.Data.activeSkinId);
            Assert.AreEqual(1, _saveService.SaveCallCount);
            Assert.AreSame(_otherSkin, service.ActiveSkin); // takes effect immediately, not just on next read
        }

        [Test]
        public void SetActiveSkin_IgnoresAnUnknownSkinId()
        {
            var service = new BlockoutSkinService();

            LogAssert.Expect(LogType.Warning, new Regex("(?i)no BlockoutSkin"));
            service.SetActiveSkin("not-a-real-skin-id");

            Assert.IsNull(_saveService.Data.activeSkinId); // untouched
            Assert.AreEqual(0, _saveService.SaveCallCount);
        }
    }
}
