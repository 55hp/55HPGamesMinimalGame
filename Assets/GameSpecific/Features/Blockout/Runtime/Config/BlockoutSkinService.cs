using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Save;

namespace hp55games.Blockout.Config
{
    // Technical Doc Phase 3: tracks which BlockoutSkin is active and persists the choice across
    // sessions via ISaveService/SaveData.activeSkinId. Every skin is treated as unlocked here -
    // Phase 6 adds the real unlock check on SetActiveSkin.
    public interface IBlockoutSkinService
    {
        // Never null as long as at least one BlockoutSkin exists in the catalog: falls back to
        // whichever skin has UnlockedByDefault set (the always-free default palette), or the
        // first catalog entry if even that's missing. Null only when the catalog has no
        // BlockoutSkin at all - callers already null-check catalog lookups the same way
        // (see BlockoutGameplayState.StartSpawning).
        BlockoutSkin ActiveSkin { get; }

        // No-op (with a logged warning) if skinId doesn't match any catalog entry - callers
        // should only pass an id they got from a real BlockoutSkin.SkinId.
        void SetActiveSkin(string skinId);
    }

    public sealed class BlockoutSkinService : IBlockoutSkinService
    {
        public BlockoutSkin ActiveSkin
        {
            get
            {
                var skins = ResolveCatalogService()?.GetAll<BlockoutSkin>();
                if (skins == null || skins.Count == 0) return null;

                var activeSkinId = ResolveSaveData()?.activeSkinId;
                if (!string.IsNullOrEmpty(activeSkinId))
                {
                    foreach (var skin in skins)
                    {
                        if (skin.SkinId == activeSkinId) return skin;
                    }
                }

                // No persisted choice yet (fresh save), or it no longer matches any catalog
                // entry (skin renamed/removed since): fall back to the always-free default.
                foreach (var skin in skins)
                {
                    if (skin.UnlockedByDefault) return skin;
                }

                return skins[0];
            }
        }

        public void SetActiveSkin(string skinId)
        {
            var skins = ResolveCatalogService()?.GetAll<BlockoutSkin>();
            bool exists = false;
            if (skins != null)
            {
                foreach (var skin in skins)
                {
                    if (skin.SkinId == skinId) { exists = true; break; }
                }
            }

            if (!exists)
            {
                Debug.LogWarning($"[BlockoutSkinService] SetActiveSkin: no BlockoutSkin with SkinId \"{skinId}\" in the catalog. Ignored.");
                return;
            }

            var save = ResolveSaveData();
            if (save == null) return;

            save.activeSkinId = skinId;
            ServiceRegistry.Resolve<ISaveService>().Save();
        }

        // Resolved lazily (not cached at construction) rather than injected: this service is
        // registered from BlockoutGameplayStateInstaller.Awake() in 01_Menu.unity, which runs in
        // no guaranteed order relative to ConfigCatalogInstaller's own Awake() in the same scene
        // - by the time ActiveSkin/SetActiveSkin are actually called (gameplay start), both are
        // long since registered regardless of that ordering.
        private static IConfigCatalogService ResolveCatalogService()
        {
            if (ServiceRegistry.TryResolve<IConfigCatalogService>(out var service)) return service;
            Debug.LogError("[BlockoutSkinService] IConfigCatalogService is not registered.");
            return null;
        }

        private static SaveData ResolveSaveData()
        {
            if (ServiceRegistry.TryResolve<ISaveService>(out var service)) return service.Data;
            Debug.LogError("[BlockoutSkinService] ISaveService is not registered.");
            return null;
        }
    }
}
