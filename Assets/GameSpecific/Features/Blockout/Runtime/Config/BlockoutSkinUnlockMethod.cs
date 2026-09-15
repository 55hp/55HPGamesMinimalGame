namespace hp55games.Blockout.Config
{
    // Periodic Table Shop GDD "Sblocco: tre meccanismi distinti". Which of the three unlock
    // mechanisms a BlockoutSkin-element uses - drives both TryUnlockSkin's coin-cost check and
    // which UI affordance the shop card shows (buy button vs achievement condition text vs
    // nothing, for the always-free default).
    public enum BlockoutSkinUnlockMethod
    {
        // Always-unlocked, zero cost - Carbon only (BlockoutSkin.UnlockedByDefault).
        Default,

        // Player-initiated coin purchase via TryUnlockSkin - BlockoutSkin.CostInCoins holds the
        // price (for periodic-table elements, their real discovery year).
        Coins,

        // Automatically granted by IBlockoutAchievementService when
        // BlockoutSkin.UnlockAchievementId's condition is met - never purchasable with coins
        // (CostInCoins stays BlockoutSkin.CostNotSetValue for these).
        Achievement
    }
}
