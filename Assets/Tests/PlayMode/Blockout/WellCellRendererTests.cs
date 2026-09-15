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

        [Test]
        public void ShowCell_SwapsToTheMetallicMaterial_WhenGivenTheMetallicCategory()
        {
            var renderer = CreateRenderer();
            var metallic = new Material(Shader.Find("Sprites/Default"));
            SetMaterial(renderer, "_metallicMaterial", metallic);
            var pos = new Vector3Int(0, 0, 0);

            renderer.ShowCell(pos, Color.white, PieceMaterialCategory.Metallic);

            Assert.AreEqual(metallic, RendererMaterial(renderer, pos));

            Object.DestroyImmediate(renderer.gameObject);
            Object.DestroyImmediate(metallic);
        }

        [Test]
        public void ShowCell_FallsBackToTheOpaqueMaterial_WhenNoCategoryIsGiven()
        {
            // Every non-element skin (Default, Profondita, Juicy Clear) calls ShowCell with no
            // category at all - see BlockoutSpawner._materialCategory.
            var renderer = CreateRenderer();
            var opaque = new Material(Shader.Find("Sprites/Default"));
            SetMaterial(renderer, "_opaqueMaterial", opaque);
            var pos = new Vector3Int(0, 0, 0);

            renderer.ShowCell(pos, Color.white);

            Assert.AreEqual(opaque, RendererMaterial(renderer, pos));

            Object.DestroyImmediate(renderer.gameObject);
            Object.DestroyImmediate(opaque);
        }

        [Test]
        public void ShowCell_SwapsMaterial_WhenAPooledInstanceIsReusedUnderADifferentCategory()
        {
            // Regression coverage for the exact bug the Opaque fallback (rather than a no-op)
            // avoids: IObjectPoolService can hand back an instance last shown under a different
            // skin's category (e.g. Translucent from a previous run), so a later ShowCell for a
            // non-element skin (no category) must actively reset it, not leave it stale.
            var renderer = CreateRenderer();
            var translucent = new Material(Shader.Find("Sprites/Default"));
            var opaque = new Material(Shader.Find("Sprites/Default"));
            SetMaterial(renderer, "_translucentMaterial", translucent);
            SetMaterial(renderer, "_opaqueMaterial", opaque);
            var pos = new Vector3Int(0, 0, 0);

            renderer.ShowCell(pos, Color.white, PieceMaterialCategory.Translucent);
            renderer.HideCell(pos);
            renderer.ShowCell(pos, Color.white); // reuses the pooled instance, no category this time

            Assert.AreEqual(opaque, RendererMaterial(renderer, pos));

            Object.DestroyImmediate(renderer.gameObject);
            Object.DestroyImmediate(translucent);
            Object.DestroyImmediate(opaque);
        }

        [Test]
        public void ShowCell_LeavesTheExistingMaterialUntouched_WhenTheResolvedCategorysSlotIsUnassigned()
        {
            var renderer = CreateRenderer();
            var pos = new Vector3Int(0, 0, 0);
            renderer.ShowCell(pos, Color.white); // establishes whatever the fallback prefab's default material is
            var before = RendererMaterial(renderer, pos);

            renderer.ShowCell(pos, Color.white, PieceMaterialCategory.Translucent); // _translucentMaterial never assigned

            Assert.AreEqual(before, RendererMaterial(renderer, pos));

            Object.DestroyImmediate(renderer.gameObject);
        }
    }
}
