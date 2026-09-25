using UnityEngine;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Rendering
{
    // URP Lit surface params for the falling piece's cubes, applied by WellCellRenderer via
    // MaterialPropertyBlock. Read directly from the active skin's authored values
    // (BlockoutSkin.Metallic/Smoothness/EmissionIntensity) once per run.
    public readonly struct CellSurface
    {
        public readonly float Metallic;
        public readonly float Smoothness;
        // Linear-space HDR emission. Black = not emissive.
        public readonly Color Emission;

        public bool IsEmissive => Emission.maxColorComponent > 0f;

        public CellSurface(float metallic, float smoothness, Color emission)
        {
            Metallic = Mathf.Clamp01(metallic);
            Smoothness = Mathf.Clamp01(smoothness);
            Emission = new Color(Mathf.Max(0f, emission.r), Mathf.Max(0f, emission.g), Mathf.Max(0f, emission.b), 1f);
        }

        // Emission = base colour (sRGB, as authored) converted to linear, times the skin's
        // intensity - black when the intensity is 0.
        public static CellSurface FromSkin(BlockoutSkin skin) =>
            new CellSurface(skin.Metallic, skin.Smoothness, skin.BaseColor.linear * Mathf.Max(0f, skin.EmissionIntensity));
    }
}
