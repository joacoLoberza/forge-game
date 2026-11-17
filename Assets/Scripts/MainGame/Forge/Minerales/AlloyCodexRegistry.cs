using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    public struct DefinedAlloyEntry
    {
        public string Id;
        public string DisplayName;
        public string FormulaText;
        public string MysteryHint;
        public string EffectSummary;
        public Color SwatchColor;
    }

    /// <summary>
    /// Registro de las aleaciones definidas y su estado de descubrimiento ("???" vs desbloqueado).
    /// </summary>
    public static class AlloyCodexRegistry
    {
        private const string PrefPrefix = "FORGE_ALLOY_UNLOCKED_";

        public static readonly DefinedAlloyEntry[] Entries =
        {
            new DefinedAlloyEntry
            {
                Id = "FerricBronze", DisplayName = "Ferric Bronze",
                FormulaText = "Iron + Copper", MysteryHint = "???  (2 Metals)",
                EffectSummary = "Tough + Malleable", SwatchColor = new Color(0.84f, 0.58f, 0.36f)
            },
            new DefinedAlloyEntry
            {
                Id = "RoseGold", DisplayName = "Rose Gold",
                FormulaText = "Copper + Gold", MysteryHint = "???  (2 Metals)",
                EffectSummary = "Swift + Keen Edge", SwatchColor = new Color(0.98f, 0.64f, 0.44f)
            },
            new DefinedAlloyEntry
            {
                Id = "BlueGold", DisplayName = "Blue Gold",
                FormulaText = "Iron + Gold", MysteryHint = "???  (2 Metals)",
                EffectSummary = "Heavy Noble Strike", SwatchColor = new Color(0.72f, 0.80f, 0.96f)
            },
            new DefinedAlloyEntry
            {
                Id = "Orichalcum", DisplayName = "Orichalcum",
                FormulaText = "Iron + Copper + Gold", MysteryHint = "???  (3 Metals)",
                EffectSummary = "Mythic Tri-Metal", SwatchColor = new Color(1.00f, 0.78f, 0.24f)
            }
        };

        private static readonly HashSet<string> _sessionUnlocked = new();
        private static bool _loaded;
        public static string RecentUnlockName { get; private set; } = string.Empty;
        public static float RecentUnlockUntil { get; private set; } = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _sessionUnlocked.Clear();
            _loaded = false;
            RecentUnlockName = string.Empty;
            RecentUnlockUntil = -10f;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            for (int i = 0; i < Entries.Length; i++)
                if (PlayerPrefs.GetInt(PrefPrefix + Entries[i].Id, 0) == 1)
                    _sessionUnlocked.Add(Entries[i].Id);
        }

        public static bool IsUnlocked(string id)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(id) && _sessionUnlocked.Contains(id);
        }

        public static int UnlockedCount
        {
            get
            {
                EnsureLoaded();
                int count = 0;
                for (int i = 0; i < Entries.Length; i++)
                    if (_sessionUnlocked.Contains(Entries[i].Id)) count++;
                return count;
            }
        }

        public static bool RegisterFromComposition(AlloyComposition comp, bool notifyToast = true)
        {
            if (comp?.ingredients == null || comp.ingredients.Count < 2) return false;
            EnsureLoaded();
            string resolved = AlloyNameRegistry.ResolveAlloyMaterialName(comp.ingredients);
            for (int i = 0; i < Entries.Length; i++)
            {
                if (!string.Equals(Entries[i].DisplayName, resolved, StringComparison.OrdinalIgnoreCase)) continue;
                bool isNew = _sessionUnlocked.Add(Entries[i].Id);
                if (isNew)
                {
                    PlayerPrefs.SetInt(PrefPrefix + Entries[i].Id, 1);
                    PlayerPrefs.Save();
                    if (notifyToast)
                    {
                        RecentUnlockName = Entries[i].DisplayName;
                        RecentUnlockUntil = Time.unscaledTime + 4.5f;
                    }
                }
                return isNew;
            }
            return false;
        }
    }
}

