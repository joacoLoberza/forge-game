using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Speedrun
{
    public enum LeaderboardFilterMode { All = 0, Solo = 1, Lan = 2 }

    [Serializable]
    public class SpeedrunRecord
    {
        public string id = Guid.NewGuid().ToString("N");
        public string teamName = "Iron Vanguard";
        public string playerNames = "Blacksmith";
        public float elapsedSeconds = 0f;
        public string formattedTime = "00:00.0";
        public string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        public bool isMultiplayer = false;
        public int playerCount = 1;
    }

    [Serializable]
    public class SpeedrunContainer
    {
        public List<SpeedrunRecord> records = new();
    }

    /// <summary>
    /// ScriptableObject y gestor persistente de la tabla de récords speedrun entre PCs de la LAN.
    /// </summary>
    [CreateAssetMenu(fileName = "DelveLeaderboardData", menuName = "Forja/Delve Leaderboard Data")]
    public class DelveLeaderboardDataSO : ScriptableObject
    {
        private static DelveLeaderboardDataSO _instance;
        public static DelveLeaderboardDataSO Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Resources.Load<DelveLeaderboardDataSO>("DelveLeaderboardData");
                    if (_instance == null) _instance = CreateInstance<DelveLeaderboardDataSO>();
                    _instance.LoadFromDisk();
                }
                return _instance;
            }
        }

        [SerializeField] private List<SpeedrunRecord> records = new();
        public IReadOnlyList<SpeedrunRecord> Records => records;
        public event Action OnLeaderboardChanged;

        public List<SpeedrunRecord> GetFilteredRecords(LeaderboardFilterMode filter)
        {
            if (filter == LeaderboardFilterMode.Solo) return records.FindAll(r => !r.isMultiplayer);
            if (filter == LeaderboardFilterMode.Lan) return records.FindAll(r => r.isMultiplayer);
            return new List<SpeedrunRecord>(records);
        }

        public static string BuildTableText(List<SpeedrunRecord> list, int maxRows = 8, bool compact = false)
        {
            if (list == null || list.Count == 0) return "<b>No speedrun records recorded in this category yet.</b>\n\nConsecrate the Deep Sanctum Altar to engrave your legacy in glory!";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(compact
                ? "<color=#382010><b><pos=4>#<pos=40>TEAM<pos=136>SMITH<pos=236>TIME</b></color>\n<color=#A08A75>--------------------------------------</color>"
                : "<color=#382010><b><pos=15>RANK<pos=80>TEAM<pos=270>BLACKSMITHS<pos=485>TIME<pos=585>MODE</b></color>\n<color=#A08A75>-------------------------------------------------------------------</color>");
            int count = Mathf.Min(maxRows, list.Count);
            for (int i = 0; i < count; i++)
            {
                var r = list[i];
                string medal = i switch { 0 => "<color=#B8860B><b>#1</b></color>", 1 => "<color=#666666><b>#2</b></color>", 2 => "<color=#9E5820><b>#3</b></color>", _ => $"<b>#{i + 1}</b>" };
                int maxT = compact ? 11 : 17, maxP = compact ? 11 : 21;
                string tName = r.teamName.Length <= maxT ? r.teamName : r.teamName.Substring(0, maxT - 1) + "..";
                string pNames = r.playerNames.Length <= maxP ? r.playerNames : r.playerNames.Substring(0, maxP - 1) + "..";
                string time = $"<color=#8B0000><b>{r.formattedTime}</b></color>";
                string mode = r.isMultiplayer ? "<color=#1B6634>LAN</color>" : "<color=#554030>Solo</color>";
                sb.AppendLine(compact
                    ? $"<pos=4>{medal}<pos=40><color=#1A0F08><b>{tName}</b></color><pos=136><color=#2C1A0C>{pNames}</color><pos=236>{time}"
                    : $"<pos=15>{medal}<pos=80><color=#1A0F08><b>{tName}</b></color><pos=270><color=#2C1A0C>{pNames}</color><pos=485>{time}<pos=585>{mode}");
            }
            return sb.ToString();
        }

        private static string SavePath => Path.Combine(Application.persistentDataPath, "delve_leaderboard.json");

        public void RegisterRun(string team, string players, float elapsed, bool isMulti, int pCount)
        {
            var rec = new SpeedrunRecord
            {
                id = Guid.NewGuid().ToString("N"),
                teamName = string.IsNullOrWhiteSpace(team) ? "Iron Vanguard" : team.Trim(),
                playerNames = string.IsNullOrWhiteSpace(players) ? "Blacksmith" : players.Trim(),
                elapsedSeconds = Mathf.Max(0.1f, elapsed),
                formattedTime = FormatSeconds(elapsed),
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                isMultiplayer = isMulti,
                playerCount = Mathf.Max(1, pCount)
            };
            AddOrUpdateRecord(rec);
            SaveToDisk();
        }

        public bool AddOrUpdateRecord(SpeedrunRecord rec)
        {
            if (rec == null || rec.elapsedSeconds <= 0.05f) return false;

            // Diferenciar estrictamente por modalidad: Solo (por nombre de jugador) vs LAN (por nombre de equipo)
            int idx = records.FindIndex(r =>
            {
                if (r.id == rec.id) return true;
                if (r.isMultiplayer != rec.isMultiplayer) return false;
                if (rec.isMultiplayer)
                {
                    return string.Equals(r.teamName?.Trim(), rec.teamName?.Trim(), StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    string p1 = string.IsNullOrWhiteSpace(r.playerNames) ? r.teamName : r.playerNames;
                    string p2 = string.IsNullOrWhiteSpace(rec.playerNames) ? rec.teamName : rec.playerNames;
                    return string.Equals(p1?.Trim(), p2?.Trim(), StringComparison.OrdinalIgnoreCase);
                }
            });

            if (idx >= 0)
            {
                // Únicamente actualizar si el nuevo tiempo es menor (más rápido) que el récord actual
                if (rec.elapsedSeconds < records[idx].elapsedSeconds)
                {
                    records[idx].elapsedSeconds = rec.elapsedSeconds;
                    records[idx].formattedTime = rec.formattedTime;
                    records[idx].timestamp = rec.timestamp;
                    records[idx].playerNames = rec.playerNames; // Actualizar lista de miembros del equipo si cambió
                    records[idx].playerCount = rec.playerCount;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                records.Add(rec);
            }

            SortRecords();
            OnLeaderboardChanged?.Invoke();
            return true;
        }

        public int MergeRecords(List<SpeedrunRecord> incoming)
        {
            if (incoming == null || incoming.Count == 0) return 0;
            int added = 0;
            foreach (var rec in incoming)
            {
                if (AddOrUpdateRecord(rec)) added++;
            }
            if (added > 0)
            {
                SaveToDisk();
                OnLeaderboardChanged?.Invoke();
            }
            return added;
        }

        public void SortRecords()
        {
            // Poda de duplicados históricos preservando únicamente el mejor tiempo por jugador (Solo) o escuadrón (LAN)
            var deduplicated = new Dictionary<string, SpeedrunRecord>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r == null || r.elapsedSeconds <= 0.05f) continue;
                string key = r.isMultiplayer
                    ? $"LAN_{r.teamName?.Trim().ToLowerInvariant()}"
                    : $"SOLO_{(string.IsNullOrWhiteSpace(r.playerNames) ? r.teamName : r.playerNames)?.Trim().ToLowerInvariant()}";

                if (!deduplicated.TryGetValue(key, out var existing) || r.elapsedSeconds < existing.elapsedSeconds)
                {
                    deduplicated[key] = r;
                }
            }

            records.Clear();
            records.AddRange(deduplicated.Values);
            records.Sort((a, b) => a.elapsedSeconds.CompareTo(b.elapsedSeconds));
            if (records.Count > 50) records.RemoveRange(50, records.Count - 50);
        }

        public void SaveToDisk()
        {
            try
            {
                SortRecords();
                var c = new SpeedrunContainer { records = records };
                File.WriteAllText(SavePath, JsonUtility.ToJson(c, true));
            }
            catch (Exception ex) { Debug.LogWarning($"[Leaderboard] Error saving to disk: {ex.Message}"); }
        }

        public void LoadFromDisk()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    var c = JsonUtility.FromJson<SpeedrunContainer>(json);
                    if (c?.records != null && c.records.Count > 0)
                    {
                        records.Clear();
                        records.AddRange(c.records);
                        SortRecords();
                        OnLeaderboardChanged?.Invoke();
                        return;
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[Leaderboard] Error loading from disk: {ex.Message}"); }

            if (records.Count == 0) SeedDefaultRecords();
        }

        private void SeedDefaultRecords()
        {
            records.Add(new SpeedrunRecord { teamName = "Iron Vanguard", playerNames = "Gildor, Balin", elapsedSeconds = 542.4f, formattedTime = "09:02.4", timestamp = "2026-11-08 18:20", isMultiplayer = true, playerCount = 2 });
            records.Add(new SpeedrunRecord { teamName = "Shadow Striders", playerNames = "Ranger", elapsedSeconds = 718.1f, formattedTime = "11:58.1", timestamp = "2026-11-09 21:10", isMultiplayer = false, playerCount = 1 });
            SortRecords();
            SaveToDisk();
        }

        public string ExportToJson() => JsonUtility.ToJson(new SpeedrunContainer { records = records });

        public static string FormatSeconds(float sec)
        {
            if (sec < 0f) sec = 0f;
            int m = (int)(sec / 60f);
            float rem = sec % 60f;
            int s = (int)rem;
            int ms = (int)((rem - s) * 10f);
            return $"{m:00}:{s:00}.{ms:0}";
        }
    }
}
