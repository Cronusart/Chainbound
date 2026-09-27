using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatChemistryMatrixPassTests
    {
        [Test]
        public void V1DirectionsHaveNoMutualAdvantagesAndExcludeMeme()
        {
            HashSet<string> edges = LoadDirectionEdges();

            Assert.AreEqual(57, edges.Count);
            foreach (string edge in edges)
            {
                string[] parts = edge.Split('>');
                Assert.AreEqual(2, parts.Length);
                Assert.AreNotEqual("Meme", parts[0]);
                Assert.AreNotEqual("Meme", parts[1]);
                Assert.IsFalse(edges.Contains($"{parts[1]}>{parts[0]}"), $"Mutual ChemAdv detected for {edge}");
            }
        }

        [Test]
        public void V1DirectionsKeepReadableCryptoTriangles()
        {
            HashSet<string> edges = LoadDirectionEdges();

            AssertTriangle(edges, "Node", "Frozen", "Bot");
            AssertTriangle(edges, "Gas", "Block", "Burned");
            AssertTriangle(edges, "Mined", "Block", "Forked");
            AssertTriangle(edges, "Encrypted", "Bot", "Glitched");
            AssertTriangle(edges, "Mined", "Frozen", "Shard");
            AssertTriangle(edges, "Staked", "Shard", "Mined");
            AssertTriangle(edges, "Compressed", "Encrypted", "Burned");
        }

        [Test]
        public void V1DirectionsMatchKeyRoleCounts()
        {
            HashSet<string> edges = LoadDirectionEdges();
            var rows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var cols = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string edge in edges)
            {
                string[] parts = edge.Split('>');
                rows.TryGetValue(parts[0], out int row);
                cols.TryGetValue(parts[1], out int col);
                rows[parts[0]] = row + 1;
                cols[parts[1]] = col + 1;
            }

            Assert.AreEqual(7, rows["Burned"]);
            Assert.AreEqual(6, rows["Shard"]);
            Assert.AreEqual(4, cols["Burned"]);
            Assert.AreEqual(4, cols["Shard"]);
            Assert.AreEqual(4, rows["Forked"]);
            Assert.AreEqual(4, cols["Forked"]);
            Assert.AreEqual(4, rows["Node"]);
            Assert.AreEqual(4, cols["Node"]);
            Assert.AreEqual(5, rows["Frozen"]);
            Assert.AreEqual(5, cols["Frozen"]);
            Assert.AreEqual(5, rows["Bot"]);
            Assert.AreEqual(5, cols["Bot"]);
            Assert.AreEqual(3, rows["Staked"]);
            Assert.AreEqual(5, cols["Staked"]);
            Assert.AreEqual(3, rows["Block"]);
            Assert.AreEqual(5, cols["Block"]);
            Assert.AreEqual(3, rows["Compressed"]);
            Assert.AreEqual(4, cols["Compressed"]);
        }

        [Test]
        public void RedesignMatrixUsesNewChemistryKitAndRejectsDeprecatedAuthors()
        {
            string matrixPath = Path.Combine(
                Directory.GetCurrentDirectory(), "Docs", "Specs", "Combat", "COMBAT_Interaction_Matrix_v1.csv");
            string statusesPath = Path.Combine(
                Directory.GetCurrentDirectory(), "Docs", "Specs", "Combat", "Data", "Statuses.csv");
            string matrix = File.ReadAllText(matrixPath);
            string statuses = File.ReadAllText(statusesPath);

            foreach (string status in new[] { "Firewall", "Slowed", "Shattered", "Drip", "Splinter" })
                StringAssert.Contains(status, statuses);

            StringAssert.Contains("Firewall @Self", matrix);
            StringAssert.Contains("Slowed @Foe", matrix);
            StringAssert.Contains("Shattered @Foe", matrix);
            StringAssert.Contains("Anchored @Foe", matrix);
            StringAssert.Contains("Cascade Failure @Foe", matrix);
            Assert.IsFalse(Regex.IsMatch(matrix, @"\s+x\d+", RegexOptions.IgnoreCase));

            string[] lines = File.ReadAllLines(matrixPath);
            for (int row = 2; row < lines.Length; row++)
            {
                string[] fields = lines[row].Split(',');
                for (int col = 1; col < fields.Length; col++)
                {
                    string cell = fields[col].Trim();
                    if (string.IsNullOrEmpty(cell) || cell == "-")
                        continue;
                    Assert.IsTrue(cell.EndsWith("@Self", StringComparison.Ordinal) ||
                                  cell.EndsWith("@Foe", StringComparison.Ordinal),
                        $"Missing explicit recipient at row {row + 1}, column {col + 1}: {cell}");
                    StringAssert.DoesNotContain(" + ", cell);
                }
            }

            foreach (string deprecated in new[] { "Firewalled", "Halted", "Crashed", "Liquidity Pump", "Evasive", "Bridged", "Narrowcast", "Overclocked" })
                StringAssert.DoesNotContain(deprecated, matrix);
        }

        private static void AssertTriangle(HashSet<string> edges, string first, string second, string third)
        {
            Assert.IsTrue(edges.Contains($"{first}>{second}"), $"{first} should beat {second}");
            Assert.IsTrue(edges.Contains($"{second}>{third}"), $"{second} should beat {third}");
            Assert.IsTrue(edges.Contains($"{third}>{first}"), $"{third} should beat {first}");
        }

        private static HashSet<string> LoadDirectionEdges()
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Docs",
                "Specs",
                "Combat",
                "COMBAT_Interaction_Matrix_v1_directions.csv");

            Assert.IsTrue(File.Exists(path), $"Missing chemistry direction matrix at {path}");

            string[] lines = File.ReadAllLines(path);
            Assert.GreaterOrEqual(lines.Length, 3);
            string[] headers = lines[1].Split(',');
            var edges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int row = 2; row < lines.Length; row++)
            {
                if (string.IsNullOrWhiteSpace(lines[row]))
                    continue;

                string[] fields = lines[row].Split(',');
                string attacker = fields[0].Trim();
                for (int col = 1; col < fields.Length && col < headers.Length; col++)
                {
                    if (!string.Equals(fields[col].Trim(), "ADV", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string defender = headers[col].Trim();
                    edges.Add($"{attacker}>{defender}");
                }
            }

            return edges;
        }
    }
}
