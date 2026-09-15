namespace hp55games.Blockout.Config
{
    // Which of the three candy-shader material variants (Bezi's Editor work - Periodic Table
    // Technical Doc Phase 2) a BlockoutSkin-element's pieces render with. Reflects the real
    // physical nature of the chemical element: metals are metallic, gases/volatiles are
    // translucent, non-metallic solids (carbon, sulfur, phosphorus...) are opaque. Purely
    // descriptive data carried on BlockoutSkin itself - the actual per-category material swap is
    // Phase 2 (Bezi, Editor work), not implemented by this enum alone.
    public enum PieceMaterialCategory
    {
        Metallic,
        Opaque,
        Translucent
    }
}
