namespace hp55games.Blockout.Achievements
{
    // Shop GDD "Sblocco: tre meccanismi distinti", case 3 (11 elements known since antiquity).
    // Minimal, Blockout-scoped achievement tracking - no generic achievement service exists in
    // the 55HPGamesMobileTemplate (checked before adding this, per the Shop Technical Doc's own
    // instruction). Persists via ISaveService/SaveData (completedAchievementIds, runsCompleted,
    // loginStreakDays), the same pattern already used for coins.
    //
    // Each Record* call re-evaluates only its own trigger family and, if a threshold is newly
    // crossed, grants every BlockoutSkin-element whose UnlockAchievementId matches - looked up
    // from the catalog by id, not a hardcoded id-to-skin map, so the element<->achievement
    // association lives in one place (the imported JSON data), not duplicated in code.
    public interface IBlockoutAchievementService
    {
        // True once achievementId has ever been completed - completion is permanent.
        bool IsCompleted(string achievementId);

        // Human-readable (Italian) unlock condition text for a shop card - "Completa la prima
        // run", etc. per the Shop GDD's achievement table. Falls back to the raw id if
        // unrecognized (should only happen for stale/malformed data).
        string GetDescription(string achievementId);

        // Call once per completed run (BlockoutGameplayState.OnWellFull) - increments
        // SaveData.runsCompleted and checks first_run_completed / runs_completed_10/50/100.
        void RecordRunCompleted();

        // Call once per completed run with that run's final score - checks
        // single_run_score_5000/15000. Safe to call with any score; below-threshold scores are
        // simply not a completion, not an error.
        void RecordRunScore(int score);

        // Call per layer-clear event (BlockoutGameplayState.OnLayersCleared) with its LayerCount -
        // checks multi_clear_3plus (layerCount >= 3). Most clears never reach 3 and are ignored.
        void RecordMultiClear(int layerCount);

        // Call once per fresh session (BlockoutGameplayState.EnterAsync, non-resuming only) -
        // updates the consecutive-day login streak (day-granularity, via ITimeService) and checks
        // login_streak_2/5/14.
        void RecordLoginForToday();

        // Re-checks elements_unlocked_20 against the current count of unlocked element skins.
        // Called automatically by every other Record* method after any unlock it grants (an
        // achievement unlock can itself be the 20th element), and by
        // BlockoutSkinService.TryUnlockSkin after a coin purchase - the one unlock path this
        // service has no other way to observe. Safe to call redundantly; idempotent.
        void RecheckElementsUnlockedThreshold();
    }
}
