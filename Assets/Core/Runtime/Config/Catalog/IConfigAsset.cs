namespace hp55games.Mobile.Core.Config
{
    /// <summary>
    /// Marker for any gameplay config ScriptableObject that should be collectible and
    /// resolvable through the ConfigCatalog. Implementing it on a config makes it
    /// discoverable by the editor scan (see ConfigCatalogEditor) and resolvable at runtime
    /// via IConfigCatalogService.Get&lt;T&gt;().
    ///
    /// No members: it's purely a type constraint. Distinct system from IConfigService
    /// (hp55games.Mobile.Core.Architecture), which loads the single app-level GameConfig via
    /// Addressables — this instead aggregates multiple gameplay configs for typed lookup.
    /// </summary>
    public interface IConfigAsset
    {
    }
}
