using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Save;
using hp55games.Blockout.Achievements;

namespace hp55games.Blockout.Config
{
    // Lets a TryUnlockSkin caller branch on why it failed (a shop UI, eventually, needs to show
    // different messaging for "you can't afford this" vs "already own it") without parsing the
    // logged warning text.
    public enum UnlockSkinResult
    {
        Success,
        UnknownSkinId,
        AlreadyUnlocked,
        NotEnoughCoins,

        // Periodic Table GDD: the 118 imported element skins start with no unlock cost decided
        // (BlockoutSkin.HasCostSet == false) - the economy formula is an explicit open item, not
        // implemented yet. Distinct from NotEnoughCoins so a shop UI can tell "you can't afford
        // it" apart from "this isn't purchasable yet".
        CostNotSet
    }

    // Read-only snapshot for a shop UI: a BlockoutSkin paired with its current unlock/active
    // state, so listing the catalog doesn't require the UI to call IsUnlocked/ActiveSkin itself
    // per row (see BlockoutSkinService.GetElementShopEntries).
    public readonly struct BlockoutSkinShopEntry
    {
        public readonly BlockoutSkin Skin;
        public readonly bool IsUnlocked;
        public readonly bool IsActive;

        // Shop Technical Doc Phase 3: real group/period (and lanthanide/actinide flags) so the
        // shop grid can place this entry without recomputing PeriodicTableLayout itself.
        public readonly PeriodicTablePosition Position;

        public BlockoutSkinShopEntry(BlockoutSkin skin, bool isUnlocked, bool isActive, PeriodicTablePosition position)
        {
            Skin = skin;
            IsUnlocked = isUnlocked;
            IsActive = isActive;
            Position = position;
        }
    }

    // Technical Doc Phase 3 (active-skin tracking) + Phase 6 (real unlock/spend): tracks which
    // BlockoutSkin is active and which are unlocked, persisting both across sessions via
    // ISaveService/SaveData (activeSkinId, unlockedSkinIds, coins).
    public interface IBlockoutSkinService
    {
        // Never null as long as at least one BlockoutSkin exists in the catalog: falls back to
        // whichever skin has UnlockedByDefault set (the always-free default palette), or the
        // first catalog entry if even that's missing. Null only when the catalog has no
        // BlockoutSkin at all - callers already null-check catalog lookups the same way
        // (see BlockoutGameplayState.StartSpawning). Always unlocked (see IsUnlocked) - Initialize
        // never leaves the player unable to render pieces at all.
        BlockoutSkin ActiveSkin { get; }

        // True for the always-free default (BlockoutSkin.UnlockedByDefault) or any skin whose
        // SkinId is in SaveData.unlockedSkinIds (see TryUnlockSkin). False for a null skin.
        bool IsUnlocked(BlockoutSkin skin);

        // No-op (with a logged warning) if skinId doesn't match any catalog entry, or if it does
        // but IsUnlocked is false for it - callers should only activate a skin the player has
        // actually unlocked (TryUnlockSkin first, or it's the always-free default).
        void SetActiveSkin(string skinId);

        // Spends CostInCoins from SaveData.coins and adds skinId to SaveData.unlockedSkinIds,
        // then persists, returning Success. Otherwise returns (with a matching logged warning,
        // no coins spent) UnknownSkinId, AlreadyUnlocked, or NotEnoughCoins. Personal-best-score
        // bonus intentionally not implemented (GDD leaves it open) - this only ever spends coins,
        // never awards them; see BlockoutGameplayState.OnWellFull for the earning side of the
        // economy.
        UnlockSkinResult TryUnlockSkin(string skinId);

        // Shop Technical Doc: unconditionally marks skinId unlocked, no coin cost - the
        // achievement service's own unlock path (BlockoutSkin.UnlockMethod == Achievement),
        // called automatically the moment a trigger's condition is met, never on user request
        // (that's TryUnlockSkin's job, for Coins-method skins). No-op (with a logged warning) for
        // an unknown skinId; silently idempotent if already unlocked.
        void GrantUnlock(string skinId);

        // Periodic Table GDD Technical Doc Phase 4: every element skin (AtomicNumber > 0) in the
        // catalog, ordered by AtomicNumber ascending (lowest-to-highest, matching the periodic
        // table's left-to-right layout), each paired with its current unlock/active state - the
        // data a periodic-table shop UI consumes. Non-element skins (Default, Profondita, Juicy
        // Clear) are excluded; they belong to the older, separate skin shop.
        IReadOnlyList<BlockoutSkinShopEntry> GetElementShopEntries();
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

        public bool IsUnlocked(BlockoutSkin skin)
        {
            if (skin == null) return false;
            if (skin.UnlockedByDefault) return true;

            var save = ResolveSaveData();
            return save != null && save.unlockedSkinIds != null && save.unlockedSkinIds.Contains(skin.SkinId);
        }

        public void SetActiveSkin(string skinId)
        {
            var skin = FindSkin(skinId);
            if (skin == null)
            {
                Debug.LogWarning($"[BlockoutSkinService] SetActiveSkin: no BlockoutSkin with SkinId \"{skinId}\" in the catalog. Ignored.");
                return;
            }

            if (!IsUnlocked(skin))
            {
                Debug.LogWarning($"[BlockoutSkinService] SetActiveSkin: \"{skinId}\" is not unlocked yet. Ignored.");
                return;
            }

            var save = ResolveSaveData();
            if (save == null) return;

            save.activeSkinId = skinId;
            ResolveSaveService()?.Save();
        }

        public UnlockSkinResult TryUnlockSkin(string skinId)
        {
            var skin = FindSkin(skinId);
            if (skin == null)
            {
                Debug.LogWarning($"[BlockoutSkinService] TryUnlockSkin: no BlockoutSkin with SkinId \"{skinId}\" in the catalog.");
                return UnlockSkinResult.UnknownSkinId;
            }

            if (IsUnlocked(skin))
            {
                Debug.LogWarning($"[BlockoutSkinService] TryUnlockSkin: \"{skinId}\" is already unlocked.");
                return UnlockSkinResult.AlreadyUnlocked;
            }

            if (!skin.HasCostSet)
            {
                Debug.LogWarning($"[BlockoutSkinService] TryUnlockSkin: \"{skinId}\" has no unlock cost set yet.");
                return UnlockSkinResult.CostNotSet;
            }

            var save = ResolveSaveData();
            if (save == null) return UnlockSkinResult.UnknownSkinId; // ISaveService missing - already logged by ResolveSaveData

            if (save.coins < skin.CostInCoins)
            {
                Debug.LogWarning($"[BlockoutSkinService] TryUnlockSkin: not enough coins for \"{skinId}\" (has {save.coins}, needs {skin.CostInCoins}).");
                return UnlockSkinResult.NotEnoughCoins;
            }

            save.coins -= skin.CostInCoins;
            save.unlockedSkinIds.Add(skinId);
            ResolveSaveService()?.Save();

            // The one unlock path the achievement service has no other way to observe (GrantUnlock
            // is achievements' own doing, so they already know) - a coin purchase can still be the
            // Nth element that crosses the "unlocked >= 20" achievement threshold.
            if (ServiceRegistry.TryResolve<IBlockoutAchievementService>(out var achievements))
            {
                achievements.RecheckElementsUnlockedThreshold();
            }

            return UnlockSkinResult.Success;
        }

        public void GrantUnlock(string skinId)
        {
            var skin = FindSkin(skinId);
            if (skin == null)
            {
                Debug.LogWarning($"[BlockoutSkinService] GrantUnlock: no BlockoutSkin with SkinId \"{skinId}\" in the catalog.");
                return;
            }

            if (IsUnlocked(skin)) return; // already unlocked (default, or a previous grant/purchase) - idempotent no-op

            var save = ResolveSaveData();
            if (save == null) return;

            save.unlockedSkinIds.Add(skinId);
            ResolveSaveService()?.Save();
        }

        public IReadOnlyList<BlockoutSkinShopEntry> GetElementShopEntries()
        {
            var skins = ResolveCatalogService()?.GetAll<BlockoutSkin>();
            if (skins == null) return new List<BlockoutSkinShopEntry>();

            var elements = new List<BlockoutSkin>();
            foreach (var skin in skins)
            {
                if (skin.AtomicNumber > 0) elements.Add(skin);
            }
            elements.Sort((a, b) => a.AtomicNumber.CompareTo(b.AtomicNumber));

            var active = ActiveSkin;
            var result = new List<BlockoutSkinShopEntry>(elements.Count);
            foreach (var skin in elements)
            {
                var position = PeriodicTableLayout.GetPosition(skin.AtomicNumber);
                result.Add(new BlockoutSkinShopEntry(skin, IsUnlocked(skin), ReferenceEquals(skin, active), position));
            }
            return result;
        }

        private static BlockoutSkin FindSkin(string skinId)
        {
            var skins = ResolveCatalogService()?.GetAll<BlockoutSkin>();
            if (skins == null) return null;

            foreach (var skin in skins)
            {
                if (skin.SkinId == skinId) return skin;
            }
            return null;
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

        private static SaveData ResolveSaveData() => ResolveSaveService()?.Data;

        private static ISaveService ResolveSaveService()
        {
            if (ServiceRegistry.TryResolve<ISaveService>(out var service)) return service;
            Debug.LogError("[BlockoutSkinService] ISaveService is not registered.");
            return null;
        }
    }
}
