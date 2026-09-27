using System;
using System.IO;
using UnityEngine;

namespace Chainbound.Combat
{
    public struct CombatBattleLogExportResult
    {
        public string directoryPath;
        public string jsonPath;
        public string transcriptPath;
    }

    public static class CombatBattleLogExporter
    {
        public static bool TryExport(CombatEventLog log, out CombatBattleLogExportResult result)
        {
            result = default;
            if (log == null)
                return false;

            string directory = Path.Combine(Application.persistentDataPath, "CombatLogs");
            Directory.CreateDirectory(directory);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeBattleId = string.IsNullOrEmpty(log.BattleId) ? "unknown" : log.BattleId;
            string baseName = $"BattleLog_{safeBattleId}_{stamp}";

            string jsonPath = Path.Combine(directory, baseName + ".json");
            string txtPath = Path.Combine(directory, baseName + ".txt");

            File.WriteAllText(jsonPath, log.ExportForensicJson());

            string transcript = CombatLogSettings.EnableTranscript
                ? CombatTextFeedBuilder.BuildTranscript(log.GetForensicEventsSnapshot())
                : string.Empty;
            File.WriteAllText(txtPath, transcript);

            result = new CombatBattleLogExportResult
            {
                directoryPath = directory,
                jsonPath = jsonPath,
                transcriptPath = txtPath,
            };

            Debug.Log($"[CombatLogExporter] Saved battle log JSON: {jsonPath}");
            Debug.Log($"[CombatLogExporter] Saved battle transcript: {txtPath}");
            return true;
        }
    }
}
