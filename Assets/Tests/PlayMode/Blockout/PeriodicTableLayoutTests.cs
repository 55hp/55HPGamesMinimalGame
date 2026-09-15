using NUnit.Framework;
using hp55games.Blockout.Config;

namespace hp55games.Blockout.Tests
{
    public class PeriodicTableLayoutTests
    {
        [TestCase(1, 1, 1)]   // H
        [TestCase(2, 18, 1)]  // He
        [TestCase(3, 1, 2)]   // Li
        [TestCase(4, 2, 2)]   // Be
        [TestCase(5, 13, 2)]  // B - the real group 2 -> 13 jump
        [TestCase(10, 18, 2)] // Ne
        [TestCase(11, 1, 3)]  // Na
        [TestCase(18, 18, 3)] // Ar
        [TestCase(19, 1, 4)]  // K
        [TestCase(26, 8, 4)]  // Fe
        [TestCase(36, 18, 4)] // Kr
        [TestCase(37, 1, 5)]  // Rb
        [TestCase(54, 18, 5)] // Xe
        [TestCase(55, 1, 6)]  // Cs
        [TestCase(56, 2, 6)]  // Ba
        [TestCase(72, 4, 6)]  // Hf - right after the lanthanide gap
        [TestCase(79, 11, 6)] // Au
        [TestCase(86, 18, 6)] // Rn
        [TestCase(87, 1, 7)]  // Fr
        [TestCase(88, 2, 7)]  // Ra
        [TestCase(104, 4, 7)] // Rf - right after the actinide gap
        [TestCase(118, 18, 7)] // Og
        public void GetPosition_ReturnsTheRealGroupAndPeriod_ForMainGridElements(int atomicNumber, int expectedGroup, int expectedPeriod)
        {
            var position = PeriodicTableLayout.GetPosition(atomicNumber);

            Assert.AreEqual(expectedGroup, position.Group);
            Assert.AreEqual(expectedPeriod, position.Period);
            Assert.IsFalse(position.IsInExtractedSeries);
        }

        [TestCase(57)]  // La
        [TestCase(64)]  // Gd
        [TestCase(71)]  // Lu
        public void GetPosition_FlagsLanthanides_AsExtractedFromTheMainGrid(int atomicNumber)
        {
            var position = PeriodicTableLayout.GetPosition(atomicNumber);

            Assert.IsTrue(position.IsLanthanide);
            Assert.IsFalse(position.IsActinide);
            Assert.IsTrue(position.IsInExtractedSeries);
            Assert.AreEqual(6, position.Period);
        }

        [TestCase(89)]  // Ac
        [TestCase(96)]  // Cm
        [TestCase(103)] // Lr
        public void GetPosition_FlagsActinides_AsExtractedFromTheMainGrid(int atomicNumber)
        {
            var position = PeriodicTableLayout.GetPosition(atomicNumber);

            Assert.IsTrue(position.IsActinide);
            Assert.IsFalse(position.IsLanthanide);
            Assert.IsTrue(position.IsInExtractedSeries);
            Assert.AreEqual(7, position.Period);
        }

        [Test]
        public void GetPosition_NeverFlagsBothLanthanideAndActinide_ForAnyElement1To118()
        {
            for (int z = 1; z <= 118; z++)
            {
                var position = PeriodicTableLayout.GetPosition(z);
                Assert.IsFalse(position.IsLanthanide && position.IsActinide, $"Z={z} flagged as both.");
                Assert.GreaterOrEqual(position.Period, 1);
                Assert.LessOrEqual(position.Period, 7);
                Assert.GreaterOrEqual(position.Group, 1);
                Assert.LessOrEqual(position.Group, 18);
            }
        }

        // Independently-authored answer key (real IUPAC group/period for every element,
        // transcribed by hand from the periodic table's known shape - not derived from
        // PeriodicTableLayout's own formula) so this test can actually catch a formula bug, not
        // just confirm the formula agrees with itself. This is the gap the earlier TestCase-based
        // spot checks above left open: a formula bug could shift 2-3 elements into a
        // still-in-range, still-plausible-looking (group, period) and no sampled subset would
        // reliably catch it. Kept in the same file as an ordinary array so a wrong entry is easy
        // to fix in place, one atomic number per line.
        private static readonly (int AtomicNumber, int Group, int Period, bool IsLanthanide, bool IsActinide)[] ExpectedPositions =
        {
            (1, 1, 1, false, false),    // H
            (2, 18, 1, false, false),   // He
            (3, 1, 2, false, false),    // Li
            (4, 2, 2, false, false),    // Be
            (5, 13, 2, false, false),   // B
            (6, 14, 2, false, false),   // C
            (7, 15, 2, false, false),   // N
            (8, 16, 2, false, false),   // O
            (9, 17, 2, false, false),   // F
            (10, 18, 2, false, false),  // Ne
            (11, 1, 3, false, false),   // Na
            (12, 2, 3, false, false),   // Mg
            (13, 13, 3, false, false),  // Al
            (14, 14, 3, false, false),  // Si
            (15, 15, 3, false, false),  // P
            (16, 16, 3, false, false),  // S
            (17, 17, 3, false, false),  // Cl
            (18, 18, 3, false, false),  // Ar
            (19, 1, 4, false, false),   // K
            (20, 2, 4, false, false),   // Ca
            (21, 3, 4, false, false),   // Sc
            (22, 4, 4, false, false),   // Ti
            (23, 5, 4, false, false),   // V
            (24, 6, 4, false, false),   // Cr
            (25, 7, 4, false, false),   // Mn
            (26, 8, 4, false, false),   // Fe
            (27, 9, 4, false, false),   // Co
            (28, 10, 4, false, false),  // Ni
            (29, 11, 4, false, false),  // Cu
            (30, 12, 4, false, false),  // Zn
            (31, 13, 4, false, false),  // Ga
            (32, 14, 4, false, false),  // Ge
            (33, 15, 4, false, false),  // As
            (34, 16, 4, false, false),  // Se
            (35, 17, 4, false, false),  // Br
            (36, 18, 4, false, false),  // Kr
            (37, 1, 5, false, false),   // Rb
            (38, 2, 5, false, false),   // Sr
            (39, 3, 5, false, false),   // Y
            (40, 4, 5, false, false),   // Zr
            (41, 5, 5, false, false),   // Nb
            (42, 6, 5, false, false),   // Mo
            (43, 7, 5, false, false),   // Tc
            (44, 8, 5, false, false),   // Ru
            (45, 9, 5, false, false),   // Rh
            (46, 10, 5, false, false),  // Pd
            (47, 11, 5, false, false),  // Ag
            (48, 12, 5, false, false),  // Cd
            (49, 13, 5, false, false),  // In
            (50, 14, 5, false, false),  // Sn
            (51, 15, 5, false, false),  // Sb
            (52, 16, 5, false, false),  // Te
            (53, 17, 5, false, false),  // I
            (54, 18, 5, false, false),  // Xe
            (55, 1, 6, false, false),   // Cs
            (56, 2, 6, false, false),   // Ba
            (57, 3, 6, true, false),    // La
            (58, 3, 6, true, false),    // Ce
            (59, 3, 6, true, false),    // Pr
            (60, 3, 6, true, false),    // Nd
            (61, 3, 6, true, false),    // Pm
            (62, 3, 6, true, false),    // Sm
            (63, 3, 6, true, false),    // Eu
            (64, 3, 6, true, false),    // Gd
            (65, 3, 6, true, false),    // Tb
            (66, 3, 6, true, false),    // Dy
            (67, 3, 6, true, false),    // Ho
            (68, 3, 6, true, false),    // Er
            (69, 3, 6, true, false),    // Tm
            (70, 3, 6, true, false),    // Yb
            (71, 3, 6, true, false),    // Lu
            (72, 4, 6, false, false),   // Hf
            (73, 5, 6, false, false),   // Ta
            (74, 6, 6, false, false),   // W
            (75, 7, 6, false, false),   // Re
            (76, 8, 6, false, false),   // Os
            (77, 9, 6, false, false),   // Ir
            (78, 10, 6, false, false),  // Pt
            (79, 11, 6, false, false),  // Au
            (80, 12, 6, false, false),  // Hg
            (81, 13, 6, false, false),  // Tl
            (82, 14, 6, false, false),  // Pb
            (83, 15, 6, false, false),  // Bi
            (84, 16, 6, false, false),  // Po
            (85, 17, 6, false, false),  // At
            (86, 18, 6, false, false),  // Rn
            (87, 1, 7, false, false),   // Fr
            (88, 2, 7, false, false),   // Ra
            (89, 3, 7, false, true),    // Ac
            (90, 3, 7, false, true),    // Th
            (91, 3, 7, false, true),    // Pa
            (92, 3, 7, false, true),    // U
            (93, 3, 7, false, true),    // Np
            (94, 3, 7, false, true),    // Pu
            (95, 3, 7, false, true),    // Am
            (96, 3, 7, false, true),    // Cm
            (97, 3, 7, false, true),    // Bk
            (98, 3, 7, false, true),    // Cf
            (99, 3, 7, false, true),    // Es
            (100, 3, 7, false, true),   // Fm
            (101, 3, 7, false, true),   // Md
            (102, 3, 7, false, true),   // No
            (103, 3, 7, false, true),   // Lr
            (104, 4, 7, false, false),  // Rf
            (105, 5, 7, false, false),  // Db
            (106, 6, 7, false, false),  // Sg
            (107, 7, 7, false, false),  // Bh
            (108, 8, 7, false, false),  // Hs
            (109, 9, 7, false, false),  // Mt
            (110, 10, 7, false, false), // Ds
            (111, 11, 7, false, false), // Rg
            (112, 12, 7, false, false), // Cn
            (113, 13, 7, false, false), // Nh
            (114, 14, 7, false, false), // Fl
            (115, 15, 7, false, false), // Mc
            (116, 16, 7, false, false), // Lv
            (117, 17, 7, false, false), // Ts
            (118, 18, 7, false, false), // Og
        };

        [Test]
        public void GetPosition_MatchesTheFullReferenceTable_ForAllOneHundredEighteenElements()
        {
            Assert.AreEqual(118, ExpectedPositions.Length, "The reference table itself is missing rows - fix the table, not this assertion.");

            foreach (var expected in ExpectedPositions)
            {
                var actual = PeriodicTableLayout.GetPosition(expected.AtomicNumber);

                Assert.AreEqual(expected.Group, actual.Group, $"Z={expected.AtomicNumber}: wrong group.");
                Assert.AreEqual(expected.Period, actual.Period, $"Z={expected.AtomicNumber}: wrong period.");
                Assert.AreEqual(expected.IsLanthanide, actual.IsLanthanide, $"Z={expected.AtomicNumber}: wrong IsLanthanide.");
                Assert.AreEqual(expected.IsActinide, actual.IsActinide, $"Z={expected.AtomicNumber}: wrong IsActinide.");
            }
        }
    }
}
