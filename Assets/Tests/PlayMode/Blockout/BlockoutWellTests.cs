using NUnit.Framework;
using UnityEngine;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Tests
{
    public class BlockoutWellTests
    {
        [Test]
        public void Constructor_ReadsDimensionsFromConfig()
        {
            var config = ScriptableObject.CreateInstance<BlockoutWellConfig>();
            var well = new BlockoutWell(config);

            Assert.AreEqual(config.Width, well.Width);
            Assert.AreEqual(config.Height, well.Height);
            Assert.AreEqual(config.Depth, well.Depth);
            Assert.AreEqual(config.Width, well.Grid.Width);
            Assert.AreEqual(config.Height, well.Grid.Height);
            Assert.AreEqual(config.Depth, well.Grid.Depth);

            Object.DestroyImmediate(config);
        }
    }
}
