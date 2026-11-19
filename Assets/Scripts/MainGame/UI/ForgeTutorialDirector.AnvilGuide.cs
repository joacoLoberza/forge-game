using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Forge;

namespace ForjaGame.UI
{
    public partial class ForgeTutorialDirector
    {
        private enum AnvilGuideStage { FocusTemperature = 0, HammerYellowZone = 1, CutLesson = 2, PolishLesson = 3, FreeForge = 4 }

        private AnvilGuideStage _anvilGuideStage = AnvilGuideStage.FocusTemperature;
        private bool _wasAnvilForging, _anvilCutFlipped, _anvilCutExecuted, _sawCutTool, _sawPolishTool;
        private int _anvilHammerHits, _anvilYellowHits;
        private float _anvilPolishSeconds, _lastAnvilCardFlashUntil = -1f;
        private string _lastAnvilCardKey = string.Empty;

        public void NotifyAnvilReheatToggled()
        {
            if (!IsTutorialActive || _anvilCtrl == null) return;
            float temp = _anvilCtrl.ThermalController != null ? _anvilCtrl.ThermalController.CurrentTemperature : 0f;
            bool isReheating = _anvilCtrl.ThermalController != null && _anvilCtrl.ThermalController.IsReheating;
            if (!isReheating && temp >= 500f && _anvilGuideStage == AnvilGuideStage.FocusTemperature)
                AdvanceAnvilStage(AnvilGuideStage.HammerYellowZone, 0.80f);
        }

        public void NotifyAnvilHammerStrike(float timingQuality)
        {
            if (!IsTutorialActive || _anvilCtrl == null || _anvilCtrl.IsCarvingHandle) return;
            _anvilHammerHits++;
            if (timingQuality >= 0.95f) _anvilYellowHits++;
            if (_anvilGuideStage == AnvilGuideStage.HammerYellowZone
                && ((_anvilYellowHits >= 1 && _anvilHammerHits >= 2) || _anvilHammerHits >= 4 || _anvilCtrl.EdgeProgress >= 0.25f))
                AdvanceAnvilStage(AnvilGuideStage.CutLesson, 0.86f);
        }

        public void NotifyAnvilCutFlipped() { _anvilCutFlipped = true; if (_anvilGuideStage == AnvilGuideStage.CutLesson && _anvilInput != null && _anvilInput.CutPointCount >= 2 && _anvilCutExecuted) AdvanceAnvilStage(AnvilGuideStage.PolishLesson, 0.88f); }
        public void NotifyAnvilCutExecuted() { _anvilCutExecuted = true; if (_anvilGuideStage == AnvilGuideStage.CutLesson) AdvanceAnvilStage(AnvilGuideStage.PolishLesson, 0.90f); }
        public void NotifyAnvilPolishing(float dt) { if (!IsTutorialActive || _anvilCtrl == null || _anvilCtrl.IsCarvingHandle) return; _anvilPolishSeconds += dt; if (_anvilGuideStage == AnvilGuideStage.PolishLesson && _anvilPolishSeconds >= 0.90f) AdvanceAnvilStage(AnvilGuideStage.FreeForge, 0.94f); }
        private void AdvanceAnvilStage(AnvilGuideStage next, float pitch) { _anvilGuideStage = next; _lastAnvilCardFlashUntil = Time.unscaledTime + 0.90f; ForgeAudioSystem.PlayProcessComplete(pitch); }
        private void ResetAnvilGuideProgress() { _anvilGuideStage = AnvilGuideStage.FocusTemperature; _anvilHammerHits = _anvilYellowHits = 0; _anvilCutFlipped = _anvilCutExecuted = _sawCutTool = _sawPolishTool = false; _anvilPolishSeconds = 0f; _lastAnvilCardKey = string.Empty; }

        private void DrawAnvilStepByStepGuideCard(bool isAssembling)
        {
            bool isForgingNow = _anvilCtrl != null && (_anvilCtrl.IsForging || _anvilCtrl.IsCarvingHandle || isAssembling);
            if (isForgingNow && !_wasAnvilForging) ResetAnvilGuideProgress();
            _wasAnvilForging = isForgingNow;

            float temp = _anvilCtrl.ThermalController != null ? _anvilCtrl.ThermalController.CurrentTemperature : 750f;
            bool isReheating = _anvilCtrl.ThermalController != null && _anvilCtrl.ThermalController.IsReheating;
            bool isQuenching = _anvilCtrl.QuenchDifferential != null && _anvilCtrl.QuenchDifferential.IsQuenching;
            AnvilToolMode tool = _anvilInput != null ? _anvilInput.CurrentToolMode : AnvilToolMode.Hammer;

            if (!isReheating && temp >= 500f && _anvilGuideStage == AnvilGuideStage.FocusTemperature && !_anvilCtrl.IsCarvingHandle)
                _anvilGuideStage = AnvilGuideStage.HammerYellowZone;
            if (_anvilGuideStage == AnvilGuideStage.CutLesson) { if (tool == AnvilToolMode.Cut) _sawCutTool = true; else if (tool == AnvilToolMode.Polish || (_sawCutTool && tool == AnvilToolMode.Hammer)) _anvilGuideStage = AnvilGuideStage.PolishLesson; }
            if (_anvilGuideStage == AnvilGuideStage.PolishLesson) { if (tool == AnvilToolMode.Polish) _sawPolishTool = true; else if (_sawPolishTool && tool != AnvilToolMode.Polish) _anvilGuideStage = AnvilGuideStage.FreeForge; }

            float hudX = (Screen.width - 840f) * 0.5f, hudY = Screen.height - 114f;
            float barStartX = (Screen.width - 466f) * 0.5f;
            string ribbon, title, body;
            Color? accent = null;
            bool calmMode = false;

            if (isAssembling)
            {
                ribbon = "ANVIL - ASSEMBLY"; title = "FIT HEAD ONTO HANDLE";
                body = "<b><color=#FFD966>[W / A / S / D]</color></b> Align head onto handle.\n" +
                       "<b><color=#FFD966>[E] / [R]</color></b> Rotate 90 deg.\n" +
                       "<b><color=#66F09A>[LEFT CLICK]</color></b> Lock & <b><color=#FFD966>[SPACE]</color></b> Claim weapon!";
            }
            else if (_anvilCtrl.IsCarvingHandle)
            {
                ribbon = "ANVIL - WOOD HANDLE"; title = "SHAPE & MOUNT HANDLE";
                accent = new Color(0.95f, 0.72f, 0.30f, 1f); calmMode = true;
                body = BuildHandleGuideBody(tool);
            }
            else if (isQuenching)
            {
                ribbon = "ANVIL - QUENCHING"; title = "COOLING IN WATER TANK...";
                accent = new Color(0.32f, 0.85f, 1f, 1f); calmMode = true;
                body = "Submerging hot weapon in water to temper steel.\nWait a moment to shape the wooden handle!";
            }
            else if (_anvilGuideStage == AnvilGuideStage.FocusTemperature)
            {
                ribbon = "ANVIL 1/4 - TEMPERATURE [H]"; title = isReheating ? $"HEATING IN HEARTH: {temp:F0}C" : $"PRESS [H] TO HEAT METAL ({temp:F0}C)";
                accent = temp > 1220f ? new Color(1f, 0.30f, 0.20f, 1f) : new Color(1f, 0.62f, 0.16f, 1f);
                body = BuildTemperatureGuideBody(temp, isReheating);
                if (tool == AnvilToolMode.Hammer)
                    DrawHudHighlightRect(new Rect(hudX + 266f, hudY + 6f, 268f, 84f), isReheating ? "^ HEAT RANGE: 850C-1220C ^" : "^ PRESS [H] - WATCH HEAT BAR ^", accent);
            }
            else if (_anvilGuideStage == AnvilGuideStage.HammerYellowZone)
            {
                ribbon = "ANVIL 2/4 - HAMMER [1]"; title = $"HIT THE YELLOW ZONE! ({Mathf.RoundToInt(_anvilCtrl.EdgeProgress * 100f)}%)";
                accent = new Color(1f, 0.90f, 0.22f, 1f);
                if (tool != AnvilToolMode.Hammer)
                {
                    body = "Return to <b><color=#FFE066>[1] Hammer</color></b> to practice hitting the yellow zone!";
                    DrawHudHighlightRect(new Rect(barStartX, 16f, 150f, 40f), "v PRESS [1] HAMMER v", accent);
                }
                else
                {
                    body = "<size=15><b><color=#FFE066>PRESS [LEFT CLICK] IN YELLOW ZONE!</color></b></size>\n" +
                           "- Click when the white cursor crosses <b><color=#FFE066>YELLOW</color></b> (Master Strike x1.32).\n" +
                           "- <b><color=#FFD966>[H]</color></b> Reheat if <500C  |  <b><color=#66EEFF>[SPACE]</color></b> Quench at 100%.\n" +
                           $"<color=#66F09A>Progress: Yellow {_anvilYellowHits}/1  |  Hits {_anvilHammerHits}/3</color>";
                    if (!isReheating && temp >= 500f)
                    {
                        float barX = hudX + 150f, barY = hudY - 48f;
                        DrawHudHighlightRect(new Rect(barX + 128f, barY + 5f, 228f, 30f), null, new Color(1f, 0.80f, 0.22f, 0.55f));
                        DrawHudHighlightRect(new Rect(barX + 132f + 220f * 0.68f - 4f, barY + 5f, 220f * 0.20f + 8f, 30f), "v CLICK IN YELLOW! v", accent);
                    }
                    else DrawHudHighlightRect(new Rect(hudX + 266f, hudY + 6f, 268f, 84f), "^ PRESS [H] TO REHEAT ^", new Color(1f, 0.60f, 0.16f, 1f));
                }
            }
            else if (_anvilGuideStage == AnvilGuideStage.CutLesson)
                BuildCutLesson(tool, barStartX, out ribbon, out title, out body, out accent);
            else if (_anvilGuideStage == AnvilGuideStage.PolishLesson)
                BuildPolishLesson(tool, barStartX, out ribbon, out title, out body, out accent);
            else
            {
                calmMode = true;
                BuildFreeForgeToolGuide(tool, temp, isReheating, out ribbon, out title, out body, out accent);
            }

            RenderAnvilGuidePanel(ribbon, title, body, accent, $"{ribbon}|{tool}|{_anvilCtrl.IsEdgeComplete}", calmMode);
        }

        private static string BuildTemperatureGuideBody(float temp, bool isReheating)
        {
            if (isReheating)
            {
                if (temp > 1220f) return "<size=15><color=#FF6644><b>OVERHEATING (>1220C)!</b></color></size>\nPress <b><color=#FFD966>[H] IMMEDIATELY</color></b> to pull metal back to the anvil!";
                if (temp >= 850f) return "<size=15><color=#66F09A><b>IDEAL HEAT REACHED!</b></color></size>\nPress <b><color=#FFD966>[H] NOW</color></b> to return to the anvil and start hammering!";
                return $"<size=14><b>HEATING... ({temp:F0}C / 850C+)</b></size>\nWatch the <b>HEAT BAR</b> below climb into <b>850C-1220C</b>, then press <b><color=#FFD966>[H]</color></b>!";
            }
            return "<size=15><b><color=#FFD966>PRESS [H] TO HEAT METAL!</color></b></size>\n" +
                   "- Cold metal (<b><500C</b>) barely bends when hammered.\n" +
                   "- Watch the <b>HEAT BAR</b> below: heat to <b><color=#FFA844>850C-1220C</color></b>, then press <b><color=#FFD966>[H]</color></b> to forge!";
        }

        private void BuildCutLesson(AnvilToolMode tool, float barStartX, out string ribbon, out string title, out string body, out Color? accent)
        {
            ribbon = "ANVIL 3/4 - CUT TOOL [2]"; accent = new Color(0.38f, 0.92f, 0.58f, 1f);
            if (tool != AnvilToolMode.Cut)
            {
                title = "NOW TRY [2] CUT TOOL!";
                body = "<b>Hammer mastered!</b> Next, see how to cut & shape your weapon.\n\n- Press <b><color=#66F09A>[2]</color></b> (or click <b>[2] Cut</b> above) to switch to the Chisel!";
                DrawHudHighlightRect(new Rect(barStartX + 158f, 16f, 150f, 40f), "v PRESS [2] CUT v", accent);
                return;
            }
            int pts = _anvilInput != null ? _anvilInput.CutPointCount : 0;
            string side = (_anvilInput != null && _anvilInput.IsCutSideFlipped) ? "INNER" : "OUTER";
            title = $"CUT TOOL INPUTS (Pts: {pts} | {side})";
            body = "<b>CUT [2] CONTROLS:</b>\n" +
                   $"- <b><color=#FFD966>[LEFT CLICK]</color></b> Mark cut points on metal ({pts} pts).\n" +
                   $"- <b><color=#66EEFF>[F]</color></b> Switch cut area (Outer/Inner){(_anvilCutFlipped ? " [OK]" : "")}.\n" +
                   "- <b><color=#66F09A>[R]</color></b> Execute cut  |  <b>[RMB]</b> Undo  |  <b>[3]</b> Next: Polish.";
            DrawHudHighlightRect(new Rect((Screen.width - 840f) * 0.5f, Screen.height - 130f, 840f, 105f), "^ CUT CONTROLS: [LMB] / [F] / [R] ^", accent);
        }

        private void BuildPolishLesson(AnvilToolMode tool, float barStartX, out string ribbon, out string title, out string body, out Color? accent)
        {
            ribbon = "ANVIL 4/4 - POLISH TOOL [3]"; accent = new Color(0.35f, 0.88f, 1f, 1f);
            if (tool != AnvilToolMode.Polish)
            {
                title = "NOW TRY [3] POLISH TOOL!";
                body = "<b>Cut tool seen!</b> Last tool: polish & refine your weapon.\n\n- Press <b><color=#66EEFF>[3]</color></b> (or click <b>[3] Polish</b> above) to switch to the Polisher!";
                DrawHudHighlightRect(new Rect(barStartX + 316f, 16f, 150f, 40f), "v PRESS [3] POLISH v", accent);
                return;
            }
            int pct = Mathf.Clamp(Mathf.RoundToInt((_anvilPolishSeconds / 0.90f) * 100f), 0, 100);
            title = $"POLISH TOOL INPUTS ({pct}%)";
            body = "<b>POLISH [3] CONTROLS:</b>\n" +
                   "- <b><color=#66EEFF>[HOLD LEFT CLICK + DRAG]</color></b> Rub across metal edges to smooth notches and raise quality!\n" +
                   "- Rub briefly (or press <b>[1]</b>) to forge freely as you wish.";
            DrawHudHighlightRect(new Rect((Screen.width - 840f) * 0.5f, 68f, 840f, 74f), "v HOLD [LEFT CLICK] & RUB METAL v", accent);
        }

        private void BuildFreeForgeToolGuide(AnvilToolMode tool, float temp, bool isReheating, out string ribbon, out string title, out string body, out Color? accent)
        {
            bool readyToQuench = _anvilCtrl.IsEdgeComplete;
            ribbon = readyToQuench ? "READY TO QUENCH [SPACE]" : "FREE FORGING";
            accent = readyToQuench ? new Color(0.35f, 0.88f, 1f, 0.85f) : new Color(0.86f, 0.72f, 0.34f, 0.75f);
            title = readyToQuench ? "QUENCH AVAILABLE - KEEP SHAPING OR [SPACE]" : "FREEDOM TO SHAPE YOUR WEAPON";

            string toolLine = tool == AnvilToolMode.Cut
                ? "<b>[2] Cut:</b> [LMB] Points  |  [F] Flip Area  |  [R] Cut  |  [RMB] Undo"
                : (tool == AnvilToolMode.Polish
                    ? "<b>[3] Polish:</b> [Hold LMB + Drag] Smooth edges & boost quality"
                    : $"<b>[1] Hammer:</b> [LMB] Yellow Zone  |  [H] Heat ({temp:F0}C{(temp < 500f && !isReheating ? " - Cool" : "")})");

            string quenchLine = readyToQuench
                ? "<color=#66EEFF><b>Ready to temper!</b> Press <b>[SPACE]</b> whenever you finish shaping.</color>"
                : $"<color=#CBB592>Shape freely with [1] Hammer, [2] Cut & [3] Polish. Edge: {Mathf.RoundToInt(_anvilCtrl.EdgeProgress * 100f)}% (Quench unlocks at 100%).</color>";

            body = "You have full freedom to deform, trim & polish as you like.\n" + toolLine + "\n" + quenchLine;
        }

        private string BuildHandleGuideBody(AnvilToolMode tool)
        {
            if (tool == AnvilToolMode.Polish)
                return "<b>[3] Polish:</b> [Hold LMB + Drag] Sand wood edges.\nPress <b><color=#66F09A>[SPACE]</color></b> whenever ready to mount blade.";
            int pts = _anvilInput != null ? _anvilInput.CutPointCount : 0;
            return $"<b>[2] Wood Cut ({pts} pts):</b> [LMB] Mark  |  [F] Flip  |  [R] Cut.\nPress <b><color=#66F09A>[SPACE]</color></b> whenever ready to mount blade.";
        }

        private void RenderAnvilGuidePanel(string ribbon, string title, string body, Color? accent, string cardKey, bool calmMode)
        {
            if (_lastAnvilCardKey != cardKey) { if (!string.IsNullOrEmpty(_lastAnvilCardKey) && !calmMode) _lastAnvilCardFlashUntil = Time.unscaledTime + 0.65f; _lastAnvilCardKey = cardKey; }
            float flash = !calmMode && Time.unscaledTime < _lastAnvilCardFlashUntil ? Mathf.Clamp01((_lastAnvilCardFlashUntil - Time.unscaledTime) / 0.65f) : 0f;
            float w = calmMode ? 404f : 428f, h = calmMode ? 172f : 254f;
            Rect box = new Rect(Screen.width - w - 20f, 118f, w, h);
            if (calmMode)
            {
                GUI.Box(box, GUIContent.none, _tipBoxStyle);
                DrawGoldFrame(box, 2.2f, 0f, accent);
                float rw = Mathf.Clamp(ribbon.Length * 9.0f + 30f, 170f, w - 28f);
                GUI.Box(new Rect(box.x + (w - rw) * 0.5f, box.y - 12f, rw, 22f), $"<b>{ribbon}</b>", _spotlightBadgeStyle);
            }
            else DrawAttentionPanel(box, ribbon, flash, accent);
            GUI.Label(new Rect(box.x + 14f, box.y + 14f, w - 28f, 24f), title, _tipTitleStyle);
            GUI.Label(new Rect(box.x + 16f, box.y + 40f, w - 32f, h - 48f), body, _stepLeftStyle);
        }

        private void DrawHudHighlightRect(Rect frame, string badge, Color? accentOverride = null)
        {
            Color prev = GUI.color; Color accent = accentOverride ?? new Color(1f, 0.86f, 0.22f, 1f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.14f + 0.12f * pulse);
            GUI.DrawTexture(new Rect(frame.x - 5f, frame.y - 5f, frame.width + 10f, frame.height + 10f), _goldBorderTex);
            DrawGoldFrame(frame, 3.5f, 0.25f, accentOverride);
            if (!string.IsNullOrEmpty(badge))
            {
                float bounce = Mathf.Sin(Time.unscaledTime * 7.5f) * 3.5f;
                float bw = Mathf.Clamp(badge.Length * 8.2f + 24f, 160f, 320f), bh = 22f;
                float bx = Mathf.Clamp(frame.x + (frame.width - bw) * 0.5f, 8f, Screen.width - bw - 8f);
                float by = (frame.y - bh - 5f > 8f) ? (frame.y - bh - 5f + bounce) : (frame.yMax + 5f - bounce);
                GUI.color = Color.white; GUI.Box(new Rect(bx, by, bw, bh), $"<b>{badge}</b>", _spotlightBadgeStyle);
            }
            GUI.color = prev;
        }
    }
}
