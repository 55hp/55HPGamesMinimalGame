using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using hp55games.Mobile.Core.Architecture;
using hp55games.Mobile.Core.Pooling;
using hp55games.Blockout.Config;
using hp55games.Blockout.Rendering;

namespace hp55games.Blockout.Tests
{
    public class WellCellRendererTests
    {
        [SetUp]
        public void SetUp()
        {
            // A fresh pool per test: WellCellRenderer.Awake() resolves IObjectPoolService, and
            // pools are keyed by prefab reference, so a stale pool from an earlier test's
            // (destroyed) fallback prefab would never match this test's fallback prefab anyway -
            // registering fresh just keeps things simple and isolated.
            ServiceRegistry.Register<IObjectPoolService>(new ObjectPoolService());
        }

        private static WellCellRenderer CreateRenderer()
        {
            var go = new GameObject(nameof(WellCellRendererTests));
            return go.AddComponent<WellCellRenderer>();
        }

        [Test]
        public void ShowCell_MakesCellShown_WithGivenColor()
        {
            var renderer = CreateRenderer();
            var pos = new Vector3Int(1, 2, 3);

            renderer.ShowCell(pos, Color.red);

            Assert.IsTrue(renderer.IsCellShown(pos));
            Assert.AreEqual(Color.red, renderer.GetCellColor(pos));

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void ShowCell_RecolorsInPlace_WhenCalledAgainAtSamePosition()
        {
            var renderer = CreateRenderer();
            var pos = new Vector3Int(0, 0, 0);

            renderer.ShowCell(pos, Color.red);
            renderer.ShowCell(pos, Color.blue);

            Assert.AreEqual(Color.blue, renderer.GetCellColor(pos));
            Assert.AreEqual(1, renderer.ShownCellCount); // still exactly one cell tracked here, not two

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void HideCell_MakesCellNoLongerShown()
        {
            var renderer = CreateRenderer();
            var pos = new Vector3Int(4, 5, 6);
            renderer.ShowCell(pos, Color.green);

            renderer.HideCell(pos);

            Assert.IsFalse(renderer.IsCellShown(pos));
            Assert.AreEqual(0, renderer.ShownCellCount);

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void ShowCell_WorksAtANewPosition_AfterAnEarlierCellWasHidden()
        {
            var renderer = CreateRenderer();
            var posA = new Vector3Int(0, 0, 0);
            var posB = new Vector3Int(1, 0, 0);

            renderer.ShowCell(posA, Color.red);
            renderer.HideCell(posA);
            renderer.ShowCell(posB, Color.blue);

            Assert.IsTrue(renderer.IsCellShown(posB));
            Assert.IsFalse(renderer.IsCellShown(posA));

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void HideAll_ClearsEveryShownCell()
        {
            var renderer = CreateRenderer();
            renderer.ShowCell(new Vector3Int(0, 0, 0), Color.red);
            renderer.ShowCell(new Vector3Int(1, 0, 0), Color.blue);
            renderer.ShowCell(new Vector3Int(2, 0, 0), Color.green);

            renderer.HideAll();

            Assert.AreEqual(0, renderer.ShownCellCount);
            Assert.IsFalse(renderer.IsCellShown(new Vector3Int(0, 0, 0)));
            Assert.IsFalse(renderer.IsCellShown(new Vector3Int(1, 0, 0)));
            Assert.IsFalse(renderer.IsCellShown(new Vector3Int(2, 0, 0)));

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void CollapseLayer_HidesTheClearedLayer_AndReportsItsPositionsAndColors()
        {
            var renderer = CreateRenderer();
            renderer.ShowCell(new Vector3Int(0, 0, 0), Color.red);
            renderer.ShowCell(new Vector3Int(1, 0, 0), Color.blue);

            var positions = new List<Vector3Int>();
            var colors = new List<Color>();
            renderer.CollapseLayer(0, width: 2, depth: 1, height: 1, positions, colors);

            Assert.IsFalse(renderer.IsCellShown(new Vector3Int(0, 0, 0)));
            Assert.IsFalse(renderer.IsCellShown(new Vector3Int(1, 0, 0)));
            Assert.AreEqual(2, positions.Count);
            CollectionAssert.Contains(positions, new Vector3Int(0, 0, 0));
            CollectionAssert.Contains(positions, new Vector3Int(1, 0, 0));
            CollectionAssert.Contains(colors, Color.red);
            CollectionAssert.Contains(colors, Color.blue);

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void CollapseLayer_ShiftsEveryCellAboveDownByOne()
        {
            var renderer = CreateRenderer();
            renderer.ShowCell(new Vector3Int(0, 0, 0), Color.red); // the layer about to clear
            renderer.ShowCell(new Vector3Int(0, 1, 0), Color.green); // should end up at y=0
            renderer.ShowCell(new Vector3Int(0, 2, 0), Color.blue); // should end up at y=1

            renderer.CollapseLayer(0, width: 1, depth: 1, height: 3, new List<Vector3Int>(), new List<Color>());

            Assert.AreEqual(Color.green, renderer.GetCellColor(new Vector3Int(0, 0, 0)));
            Assert.AreEqual(Color.blue, renderer.GetCellColor(new Vector3Int(0, 1, 0)));
            Assert.IsFalse(renderer.IsCellShown(new Vector3Int(0, 2, 0))); // top layer now empty, nothing left to shift into it

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void CollapseLayer_RecolorsShiftedCellsToTheirNewLevel_KeepingTheirSurface()
        {
            var renderer = CreateRenderer();
            var surface = new CellSurface(0.7f, 0.4f, new Color(0.2f, 0f, 0f));
            renderer.ShowCell(new Vector3Int(0, 0, 0), Color.red, surface); // cleared layer
            renderer.ShowCell(new Vector3Int(0, 1, 0), Color.green, surface); // ends up at y=0
            Color[] levelColors = { Color.cyan, Color.magenta };

            renderer.CollapseLayer(0, width: 1, depth: 1, height: 2, new List<Vector3Int>(), new List<Color>(), level => levelColors[level]);

            var pos = new Vector3Int(0, 0, 0);
            Assert.AreEqual(Color.cyan, renderer.GetCellColor(pos)); // level 0's colour now, not level 1's
            var block = RendererBlock(renderer, pos);
            Assert.AreEqual(surface.Metallic, block.GetFloat("_Metallic"), 1e-5f);
            Assert.AreEqual(surface.Smoothness, block.GetFloat("_Smoothness"), 1e-5f);
            Assert.AreEqual((Vector4)surface.Emission, block.GetVector("_EmissionColor"));

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void CollapseLayer_LeavesUntouchedCellsAtOtherXZColumnsInPlace()
        {
            var renderer = CreateRenderer();
            renderer.ShowCell(new Vector3Int(0, 0, 0), Color.red); // cleared layer
            renderer.ShowCell(new Vector3Int(1, 1, 0), Color.yellow); // different column, above the cleared layer

            renderer.CollapseLayer(0, width: 2, depth: 1, height: 2, new List<Vector3Int>(), new List<Color>());

            Assert.AreEqual(Color.yellow, renderer.GetCellColor(new Vector3Int(1, 0, 0))); // shifted down within its own column
            Assert.IsFalse(renderer.IsCellShown(new Vector3Int(1, 1, 0)));

            Object.DestroyImmediate(renderer.gameObject);
        }

        // WellCellRenderer's 3 material fields have no test-facing setter (same reasoning as
        // BlockoutSkin's private fields elsewhere in this suite) - Bezi assigns them in the
        // Inspector in practice.
        private static void SetMaterial(WellCellRenderer renderer, string fieldName, Material material)
        {
            typeof(WellCellRenderer).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(renderer, material);
        }

        private static Material RendererMaterial(WellCellRenderer renderer, Vector3Int pos)
        {
            var instance = (Dictionary<Vector3Int, PooledObject>)typeof(WellCellRenderer)
                .GetField("_shownCells", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(renderer);
            return instance[pos].GetComponent<Renderer>().sharedMaterial;
        }

        private static MaterialPropertyBlock RendererBlock(WellCellRenderer renderer, Vector3Int pos)
        {
            var instance = (Dictionary<Vector3Int, PooledObject>)typeof(WellCellRenderer)
                .GetField("_shownCells", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(renderer);
            var block = new MaterialPropertyBlock();
            instance[pos].GetComponent<Renderer>().GetPropertyBlock(block);
            return block;
        }

        private static readonly CellSurface NonEmissiveSurface = new CellSurface(0.9f, 0.8f, Color.black);
        private static readonly CellSurface EmissiveSurface = new CellSurface(0.05f, 0.3f, new Color(0.3f, 0.1f, 0f));

        [Test]
        public void ShowCell_UsesTheBaseMaterial_ForANonEmissiveSurface()
        {
            var renderer = CreateRenderer();
            var baseMaterial = new Material(Shader.Find("Sprites/Default"));
            var emissive = new Material(Shader.Find("Sprites/Default"));
            SetMaterial(renderer, "_baseMaterial", baseMaterial);
            SetMaterial(renderer, "_emissiveMaterial", emissive);
            var pos = new Vector3Int(0, 0, 0);

            renderer.ShowCell(pos, Color.white, NonEmissiveSurface);

            Assert.AreEqual(baseMaterial, RendererMaterial(renderer, pos));

            Object.DestroyImmediate(renderer.gameObject);
            Object.DestroyImmediate(baseMaterial);
            Object.DestroyImmediate(emissive);
        }

        [Test]
        public void ShowCell_UsesTheEmissiveMaterial_ForAnEmissiveSurface()
        {
            var renderer = CreateRenderer();
            var baseMaterial = new Material(Shader.Find("Sprites/Default"));
            var emissive = new Material(Shader.Find("Sprites/Default"));
            SetMaterial(renderer, "_baseMaterial", baseMaterial);
            SetMaterial(renderer, "_emissiveMaterial", emissive);
            var pos = new Vector3Int(0, 0, 0);

            renderer.ShowCell(pos, Color.white, EmissiveSurface);

            Assert.AreEqual(emissive, RendererMaterial(renderer, pos));

            Object.DestroyImmediate(renderer.gameObject);
            Object.DestroyImmediate(baseMaterial);
            Object.DestroyImmediate(emissive);
        }

        [Test]
        public void ShowCell_FallsBackToTheBaseMaterial_WhenNoSurfaceIsGiven()
        {
            // Every non-element skin (Default, Profondita, Juicy Clear) calls ShowCell with no
            // surface at all - see BlockoutSpawner._cellSurface.
            var renderer = CreateRenderer();
            var baseMaterial = new Material(Shader.Find("Sprites/Default"));
            SetMaterial(renderer, "_baseMaterial", baseMaterial);
            var pos = new Vector3Int(0, 0, 0);

            renderer.ShowCell(pos, Color.white);

            Assert.AreEqual(baseMaterial, RendererMaterial(renderer, pos));

            Object.DestroyImmediate(renderer.gameObject);
            Object.DestroyImmediate(baseMaterial);
        }

        [Test]
        public void ShowCell_WritesTheSurfaceValues_IntoThePropertyBlock()
        {
            var renderer = CreateRenderer();
            var pos = new Vector3Int(0, 0, 0);

            renderer.ShowCell(pos, Color.red, EmissiveSurface);

            var block = RendererBlock(renderer, pos);
            Assert.AreEqual(EmissiveSurface.Metallic, block.GetFloat("_Metallic"), 1e-5f);
            Assert.AreEqual(EmissiveSurface.Smoothness, block.GetFloat("_Smoothness"), 1e-5f);
            Assert.AreEqual((Vector4)EmissiveSurface.Emission, block.GetVector("_EmissionColor"));

            Object.DestroyImmediate(renderer.gameObject);
        }

        [Test]
        public void ShowCell_ResetsMaterialAndSurface_WhenAPooledInstanceIsReusedWithoutASurface()
        {
            // IObjectPoolService can hand back an instance last shown under a different skin
            // (e.g. an emissive element from a previous run), so a later ShowCell for a
            // non-element skin (no surface) must actively reset both the material and the
            // per-cell PBR overrides, not leave them stale.
            var renderer = CreateRenderer();
            var baseMaterial = new Material(Shader.Find("Sprites/Default"));
            var emissive = new Material(Shader.Find("Sprites/Default"));
            SetMaterial(renderer, "_baseMaterial", baseMaterial);
            SetMaterial(renderer, "_emissiveMaterial", emissive);
            var pos = new Vector3Int(0, 0, 0);

            renderer.ShowCell(pos, Color.white, EmissiveSurface);
            renderer.HideCell(pos);
            renderer.ShowCell(pos, Color.white); // reuses the pooled instance, no surface this time

            Assert.AreEqual(baseMaterial, RendererMaterial(renderer, pos));
            Assert.IsFalse(RendererBlock(renderer, pos).HasFloat("_Metallic"));

            Object.DestroyImmediate(renderer.gameObject);
            Object.DestroyImmediate(baseMaterial);
            Object.DestroyImmediate(emissive);
        }

        [Test]
        public void ShowCell_LeavesTheExistingMaterialUntouched_WhenTheBaseSlotIsUnassigned()
        {
            var renderer = CreateRenderer();
            var pos = new Vector3Int(0, 0, 0);
            renderer.ShowCell(pos, Color.white); // establishes whatever the fallback prefab's default material is
            var before = RendererMaterial(renderer, pos);

            renderer.ShowCell(pos, Color.white, EmissiveSurface); // neither slot assigned

            Assert.AreEqual(before, RendererMaterial(renderer, pos));

            Object.DestroyImmediate(renderer.gameObject);
        }
    }
}
