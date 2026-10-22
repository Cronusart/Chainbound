using System.Collections.Generic;
using System.IO;
using Chainbound.Combat.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class KnownPoolGenerationTests
    {
        [Test]
        public void GeneratedServerTablesMatchCurrentImportedAssetsByteForByte()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            IReadOnlyDictionary<string, string> expected = CombatServerTablesExporter.BuildCurrentTables();
            foreach (KeyValuePair<string, string> table in expected)
            {
                string path = Path.Combine(root, "Supabase/functions/_shared", table.Key);
                Assert.That(File.Exists(path), Is.True, $"Missing generated table {path}.");
                Assert.That(File.ReadAllText(path), Is.EqualTo(table.Value),
                    $"{table.Key} is stale; run Tools/Chainbound/Combat/Export Server Move Tables.");
            }
        }
    }
}
