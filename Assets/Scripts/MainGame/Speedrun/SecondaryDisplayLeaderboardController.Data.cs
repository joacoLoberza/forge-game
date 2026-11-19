using System.Collections.Generic;
using UnityEngine;
using ForjaGame.UI;

namespace ForjaGame.Speedrun
{
    public partial class SecondaryDisplayLeaderboardController
    {
        private static readonly Color ColEmptyInk = new Color(0.57f, 0.49f, 0.40f, 0.78f);
        private static readonly Color ColModeLan = new Color(0.11f, 0.39f, 0.20f, 1f);
        private static readonly Color ColModeSolo = new Color(0.36f, 0.25f, 0.16f, 1f);

        public void RefreshLeaderboardDisplay()
        {
            if (_secondaryCanvasRoot == null || _podiumCards[0].teamText == null) return;

            var db = DelveLeaderboardDataSO.Instance;
            var allRecords = db.Records;
            List<SpeedrunRecord> filtered = db.GetFilteredRecords(_activeFilter);

            _cachedTopRecordSeconds = (filtered.Count > 0) ? filtered[0].elapsedSeconds : -1f;

            if (_filterLabelText != null)
            {
                string filterName = _activeFilter switch
                {
                    LeaderboardFilterMode.Solo => "<color=#FFD97D>SOLO ONLY</color>",
                    LeaderboardFilterMode.Lan => "<color=#7CE88C>LAN CO-OP ONLY</color>",
                    _ => "<color=#FFF0C2>ALL EXPEDITIONS</color>"
                };
                _filterLabelText.text = MedievalTypographyProvider.SanitizeForTMP($"VIEW:  {filterName}");
            }

            // Poblar las 3 tarjetas de Podio (#1, #2, #3)
            for (int i = 0; i < _podiumCards.Length; i++)
            {
                SpeedrunRecord rec = (i < filtered.Count) ? filtered[i] : null;
                ApplyPodiumCard(_podiumCards[i], rec, i + 1);
            }

            // Poblar las 7 filas del Ledger (#4 a #10)
            for (int i = 0; i < _ledgerRows.Length; i++)
            {
                int rankIdx = i + 3;
                SpeedrunRecord rec = (rankIdx < filtered.Count) ? filtered[rankIdx] : null;
                ApplyLedgerRow(_ledgerRows[i], rec, rankIdx + 1);
            }

            RefreshFooterStats(allRecords);
            UpdateLiveTelemetry();
        }

        private void ApplyPodiumCard(PodiumCardRefs card, SpeedrunRecord rec, int rank)
        {
            if (rec == null)
            {
                card.teamText.color = ColEmptyInk;
                card.teamText.text = rank == 1 ? "UNCLAIMED THRONE" : "OPEN PODIUM SLOT";
                card.playersText.color = ColEmptyInk;
                card.playersText.text = "Consecrate the Deep Sanctum Altar";
                card.timeText.color = ColEmptyInk;
                card.timeText.text = "--:--.-";
                card.metaText.color = ColEmptyInk;
                card.metaText.text = "AWAITING CHALLENGERS";
                return;
            }

            card.teamText.color = ColInkDark;
            card.teamText.text = MedievalTypographyProvider.SanitizeForTMP(Truncate(rec.teamName, 24));

            card.playersText.color = ColInkMuted;
            card.playersText.text = MedievalTypographyProvider.SanitizeForTMP(Truncate(rec.playerNames, 34));

            card.timeText.color = ColCrimson;
            card.timeText.text = rec.formattedTime;

            string mode = rec.isMultiplayer ? $"LAN ({rec.playerCount}P)" : "SOLO";
            string date = ShortDate(rec.timestamp);
            card.metaText.color = rec.isMultiplayer ? ColModeLan : ColInkMuted;
            card.metaText.text = MedievalTypographyProvider.SanitizeForTMP($"{mode}   |   {date}");
        }

        private void ApplyLedgerRow(LedgerRowRefs row, SpeedrunRecord rec, int rankNumber)
        {
            row.rankText.text = $"#{rankNumber}";

            if (rec == null)
            {
                row.rankText.color = ColEmptyInk;
                row.teamText.color = ColEmptyInk;
                row.teamText.text = "Open Chronicle Slot";
                row.playersText.color = ColEmptyInk;
                row.playersText.text = "---";
                row.modeText.color = ColEmptyInk;
                row.modeText.text = "---";
                row.dateText.color = ColEmptyInk;
                row.dateText.text = "---";
                row.timeText.color = ColEmptyInk;
                row.timeText.text = "--:--.-";
                return;
            }

            row.rankText.color = ColInkDark;
            row.teamText.color = ColInkDark;
            row.teamText.text = MedievalTypographyProvider.SanitizeForTMP(Truncate(rec.teamName, 24));

            row.playersText.color = ColInkMuted;
            row.playersText.text = MedievalTypographyProvider.SanitizeForTMP(Truncate(rec.playerNames, 36));

            row.modeText.color = rec.isMultiplayer ? ColModeLan : ColModeSolo;
            row.modeText.text = rec.isMultiplayer ? $"LAN ({rec.playerCount}P)" : "SOLO";

            row.dateText.color = ColInkMuted;
            row.dateText.text = ShortDate(rec.timestamp);

            row.timeText.color = ColCrimson;
            row.timeText.text = rec.formattedTime;
        }

        private void RefreshFooterStats(IReadOnlyList<SpeedrunRecord> allRecords)
        {
            int total = allRecords != null ? allRecords.Count : 0;
            int soloCount = 0, lanCount = 0;
            SpeedrunRecord bestSolo = null, bestLan = null;

            if (allRecords != null)
            {
                for (int i = 0; i < allRecords.Count; i++)
                {
                    var r = allRecords[i];
                    if (r.isMultiplayer)
                    {
                        lanCount++;
                        if (bestLan == null || r.elapsedSeconds < bestLan.elapsedSeconds) bestLan = r;
                    }
                    else
                    {
                        soloCount++;
                        if (bestSolo == null || r.elapsedSeconds < bestSolo.elapsedSeconds) bestSolo = r;
                    }
                }
            }

            if (_footerTotalsText != null)
                _footerTotalsText.text = $"TOTAL RUNS: <color=#FFF0C2>{total}</color>   |   SOLO: <color=#FFF0C2>{soloCount}</color>   |   LAN: <color=#7CE88C>{lanCount}</color>";

            if (_footerBestSoloText != null)
            {
                _footerBestSoloText.text = bestSolo != null
                    ? $"BEST SOLO: <color=#FFD97D>{bestSolo.formattedTime}</color> ({MedievalTypographyProvider.SanitizeForTMP(Truncate(bestSolo.teamName, 14))})"
                    : "BEST SOLO: <color=#9E8E76>--:--.-</color>";
            }

            if (_footerBestLanText != null)
            {
                _footerBestLanText.text = bestLan != null
                    ? $"BEST LAN: <color=#7CE88C>{bestLan.formattedTime}</color> ({MedievalTypographyProvider.SanitizeForTMP(Truncate(bestLan.teamName, 14))})"
                    : "BEST LAN: <color=#9E8E76>--:--.-</color>";
            }
        }

        private static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value)) return "---";
            string trimmed = value.Trim();
            return trimmed.Length <= maxLen ? trimmed : trimmed.Substring(0, maxLen - 2) + "..";
        }

        private static string ShortDate(string timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp)) return "---";
            return timestamp.Length >= 10 ? timestamp.Substring(0, 10) : timestamp;
        }
    }
}

