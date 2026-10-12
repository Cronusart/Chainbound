#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SignalJam.Editor
{
    /// <summary>
    /// Development-machine-only access to the private Devnet RAM reward rollout list.
    /// This intentionally shells out to the developer's linked Supabase CLI rather than
    /// giving a Unity player build a service credential or a privileged Edge endpoint.
    /// </summary>
    public static class DevnetBattleRewardAllowlistTool
    {
        private const string MenuRoot = "Tools/Chainbound/Devnet/RAM Rewards/";
        private const string AllowlistMenuPath = MenuRoot + "Allowlist Active Play Profile...";
        private const string VerifyMenuPath = MenuRoot + "Verify Active Play Profile Allowlist";
        private const string SupabaseCliEnvironmentVariable = "CHAINBOUND_SUPABASE_CLI";
        private const int CommandTimeoutMilliseconds = 30000;

        [MenuItem(AllowlistMenuPath, priority = 100)]
        private static async void AllowlistActivePlayProfile()
        {
            if (!TryGetActiveDevnetProfile(out Guid profileId, out string problem))
            {
                ShowProblem(problem);
                return;
            }

            string normalizedProfileId = profileId.ToString("D");
            bool confirmed = EditorUtility.DisplayDialog(
                "Allowlist active Devnet test profile?",
                "This adds the currently active Play Mode profile to the private Devnet RAM reward allowlist.\n\n" +
                "Profile: " + normalizedProfileId + "\n\n" +
                "It does not grant RAM, change a battle result, or modify Mainnet. " +
                "The action uses this machine's authenticated Supabase CLI session.",
                "Allowlist profile",
                "Cancel");
            if (!confirmed)
                return;

            await RunAndReportAsync(
                profileId,
                BuildAllowlistSql(profileId),
                "Allowlist active Devnet test profile");
        }

        [MenuItem(VerifyMenuPath, priority = 101)]
        private static async void VerifyActivePlayProfile()
        {
            if (!TryGetActiveDevnetProfile(out Guid profileId, out string problem))
            {
                ShowProblem(problem);
                return;
            }

            await RunAndReportAsync(
                profileId,
                BuildVerifySql(profileId),
                "Verify active Devnet test profile allowlist");
        }

        [MenuItem(AllowlistMenuPath, true)]
        [MenuItem(VerifyMenuPath, true)]
        private static bool ValidateMenuItems()
        {
            return !EditorApplication.isCompiling &&
                   TryGetActiveDevnetProfile(out _, out _);
        }

        internal static string BuildAllowlistSql(Guid profileId)
        {
            string id = profileId.ToString("D");
            return $@"
WITH target AS (
    SELECT '{id}'::uuid AS profile_id
),
updated AS (
    UPDATE public.battle_reward_settings AS settings
       SET profile_allowlist = CASE
               WHEN target.profile_id = ANY(settings.profile_allowlist) THEN settings.profile_allowlist
               ELSE array_append(settings.profile_allowlist, target.profile_id)
           END,
           updated_at = now()
      FROM target
     WHERE settings.singleton
       AND settings.mode = 'grant'
       AND settings.cluster = 'devnet'
 RETURNING settings.mode, settings.cluster, settings.profile_allowlist
)
SELECT updated.mode,
       updated.cluster,
       target.profile_id = ANY(updated.profile_allowlist) AS profile_enabled,
       cardinality(updated.profile_allowlist) AS allowlisted_profiles
  FROM updated
 CROSS JOIN target;";
        }

        internal static string BuildVerifySql(Guid profileId)
        {
            string id = profileId.ToString("D");
            return $@"
SELECT settings.mode,
       settings.cluster,
       '{id}'::uuid = ANY(settings.profile_allowlist) AS profile_enabled,
       cardinality(settings.profile_allowlist) AS allowlisted_profiles
  FROM public.battle_reward_settings AS settings
 WHERE settings.singleton;";
        }

        private static async Task RunAndReportAsync(Guid profileId, string sql, string action)
        {
            EditorUtility.DisplayProgressBar("Chainbound Devnet", action + "...", 0.5f);
            try
            {
                CliResult command = await Task.Run(() => RunSupabaseQuery(sql));
                if (!command.Succeeded)
                {
                    ShowProblem(
                        "Supabase CLI could not complete the request.\n\n" +
                        command.UserFacingError +
                        "\n\nConfirm that the CLI is logged in and this project remains linked to Devnet.");
                    return;
                }

                if (!TryReadSingleRow(command.StandardOutput, out RewardAllowlistRow row, out string parseProblem))
                {
                    ShowProblem(parseProblem);
                    return;
                }

                if (!string.Equals(row.mode, "grant", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(row.cluster, "devnet", StringComparison.OrdinalIgnoreCase))
                {
                    ShowProblem(
                        "Devnet RAM rewards are not in grant mode. No allowlist change was accepted.\n\n" +
                        "Database mode: " + (row.mode ?? "(missing)") +
                        "\nCluster: " + (row.cluster ?? "(missing)"));
                    return;
                }

                string id = profileId.ToString("D");
                if (row.profile_enabled)
                {
                    Debug.Log(
                        "[Devnet RAM Allowlist] Active profile is enabled for live Devnet rewards. " +
                        "profile=" + id + ", allowlistedProfiles=" + row.allowlisted_profiles + ".");
                    EditorUtility.DisplayDialog(
                        "Devnet RAM profile enabled",
                        "The active test profile is allowlisted for live Devnet RAM rewards.\n\n" +
                        "Profile: " + id + "\nAllowlisted profiles: " + row.allowlisted_profiles,
                        "OK");
                    return;
                }

                ShowProblem(
                    "The database completed the command but did not enable the active profile. " +
                    "No battle reward settings were assumed or changed further.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowProblem("The Devnet allowlist tool failed before verification. See the Unity Console for details.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool TryGetActiveDevnetProfile(out Guid profileId, out string problem)
        {
            profileId = Guid.Empty;
            if (!Application.isPlaying)
            {
                problem = "Enter Play Mode and wait for the account session to initialize before using this tool.";
                return false;
            }

            if (PlatformContext.ResolveOwnershipEnvironment() != OwnershipDeploymentEnvironment.DevnetOwnership ||
                !PlatformContext.ResolveAllowDevelopmentTools())
            {
                problem = "This command is available only for the Devnet deployment profile with development tools enabled.";
                return false;
            }

            string rawProfileId = AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession?.PlayerProfileId
                : null;
            if (!Guid.TryParse(rawProfileId, out profileId))
            {
                problem = "The active Play Mode account has no valid backend profile ID yet. Finish sign-in/onboarding, then try again.";
                return false;
            }

            problem = null;
            return true;
        }

        private static CliResult RunSupabaseQuery(string sql)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return CliResult.Failed("Unable to resolve the Unity project root.");

            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveSupabaseCliExecutable(),
                Arguments = "--workdir " + QuoteArgument(projectRoot) +
                            " db query --linked --output-format json " + QuoteArgument(sql),
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(CommandTimeoutMilliseconds))
                    {
                        try { process.Kill(); }
                        catch (InvalidOperationException) { }
                        return CliResult.Failed("The Supabase CLI timed out after 30 seconds.");
                    }

                    Task.WaitAll(standardOutputTask, standardErrorTask);
                    string standardOutput = standardOutputTask.Result;
                    string standardError = standardErrorTask.Result;

                    return process.ExitCode == 0
                        ? CliResult.Success(standardOutput)
                        : CliResult.Failed(FirstUsefulLine(standardError, standardOutput));
                }
            }
            catch (Exception exception)
            {
                return CliResult.Failed(
                    "Unable to start the Supabase CLI ('" + startInfo.FileName + "'): " + exception.Message);
            }
        }

        private static string ResolveSupabaseCliExecutable()
        {
            string configuredPath = Environment.GetEnvironmentVariable(SupabaseCliEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configuredPath) ? "supabase" : configuredPath.Trim();
        }

        private static bool TryReadSingleRow(string output, out RewardAllowlistRow row, out string problem)
        {
            row = null;
            string json = ExtractJsonDocument(output);
            if (string.IsNullOrWhiteSpace(json))
            {
                problem = "Supabase CLI returned no JSON response to verify the allowlist state.";
                return false;
            }

            try
            {
                DbQueryResponse response = JsonUtility.FromJson<DbQueryResponse>(json);
                if (response?.rows == null || response.rows.Length != 1)
                {
                    problem = "The database did not return exactly one reward rollout settings row. " +
                              "No success was assumed.";
                    return false;
                }

                row = response.rows[0];
                problem = null;
                return true;
            }
            catch (Exception exception)
            {
                problem = "Supabase CLI returned an unreadable verification response: " + exception.Message;
                return false;
            }
        }

        private static string ExtractJsonDocument(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return string.Empty;

            int start = output.IndexOf('{');
            int end = output.LastIndexOf('}');
            return start >= 0 && end >= start ? output.Substring(start, end - start + 1) : string.Empty;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string FirstUsefulLine(string first, string fallback)
        {
            string source = string.IsNullOrWhiteSpace(first) ? fallback : first;
            if (string.IsNullOrWhiteSpace(source))
                return "The Supabase CLI exited without an error message.";

            string[] lines = source.Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrWhiteSpace(line))
                    return line.Length <= 600 ? line : line.Substring(0, 600);
            }

            return "The Supabase CLI exited without an error message.";
        }

        private static void ShowProblem(string message)
        {
            Debug.LogWarning("[Devnet RAM Allowlist] " + message);
            EditorUtility.DisplayDialog("Devnet RAM allowlist", message, "OK");
        }

        [Serializable]
        private sealed class DbQueryResponse
        {
            public RewardAllowlistRow[] rows;
        }

        [Serializable]
        private sealed class RewardAllowlistRow
        {
            public string mode;
            public string cluster;
            public bool profile_enabled;
            public int allowlisted_profiles;
        }

        private sealed class CliResult
        {
            public bool Succeeded { get; private set; }
            public string StandardOutput { get; private set; }
            public string UserFacingError { get; private set; }

            public static CliResult Success(string standardOutput)
            {
                return new CliResult { Succeeded = true, StandardOutput = standardOutput ?? string.Empty };
            }

            public static CliResult Failed(string error)
            {
                return new CliResult { Succeeded = false, UserFacingError = error ?? "Unknown Supabase CLI failure." };
            }
        }
    }
}
#endif
