using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Environment;
using ForjaGame.Forge;

namespace ForjaGame.UI
{
    /// <summary>
    /// Renderizado diegetico de HUD y GUI del Tutorial (QTE, Forjado, Dummies de Combate y Primer Encuentro en Mina).
    /// </summary>
    public partial class ForgeTutorialDirector
    {
        private string _lastBannerAnimKey = string.Empty;
        private float _bannerAnimStartTime = -10f;

        public bool HasTopBannerVisible => ForjaGame.Core.TutorialSettings.IsTutorialEnabled
            && !GameLoopController.IsEndScreenOpen && !ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen
            && !(_anvilCtrl != null && (_anvilCtrl.IsForging || _anvilCtrl.IsCarvingHandle))
            && (_stage != TutorialStage.FreePlayDelve || Time.time < _freePlayTipUntil);

        private void OnGUI()
        {
            if (GameLoopController.IsEndScreenOpen || !ForjaGame.Core.TutorialSettings.IsTutorialEnabled) return;
            GUI.depth = -150;
            EnsureGuiStyles();

            if (_activeSpotlightTarget != null && !string.IsNullOrEmpty(_activeSpotlightLabel))
            {
                if (_anvilPanel != null && _anvilPanel.IsOpen && IsAnvilStep1Active())
                    DrawUISpotlightOverlay(GetWeaponButtonsCombinedRect(), _activeSpotlightLabel, "<b>v CHOOSE 1 WEAPON v</b>");
                else
                    DrawUISpotlightOverlay(_activeSpotlightTarget, _activeSpotlightLabel);
                return;
            }
            if (!string.IsNullOrEmpty(_activeSpotlightLabel)) { DrawCenterAttentionCard(_activeSpotlightLabel); return; }

            var asm = _anvilCtrl != null ? _anvilCtrl.GetComponent<AnvilAssemblyController>() : null;
            bool isAsm = asm != null && asm.IsAssembling;
            if (_anvilCtrl != null && (_anvilCtrl.IsForging || _anvilCtrl.IsCarvingHandle || isAsm)) { DrawAnvilStepByStepGuideCard(isAsm); return; }
            if (Time.time < _mineBlockedTipUntil) { DrawCenterAttentionCard("Complete Forging & ALL Dummy Training (Strike, Block, Parry & Dodge Roll) before entering the Mine!"); return; }

            if (_stage == TutorialStage.ControlsQTE) { DrawControlsQTEScreen(); return; }
            if (_stage == TutorialStage.FurnaceSmelt)
                DrawTopContextualBanner("STEP 1/5: SMELTING FURNACE", "Approach the illuminated FURNACE and press <b><color=#FFD966>[F]</color></b> to smelt 3 ores into a Bloom.");
            else if (_stage == TutorialStage.PressCompact)
                DrawTopContextualBanner("STEP 2/5: MECHANICAL PRESS", "Bloom obtained! Go to the illuminated PRESS next to the furnace and press <b><color=#FFD966>[F]</color></b> to compact an Ingot.");
            else if (_stage == TutorialStage.AnvilForge)
                DrawTopContextualBanner("STEP 3/5: FORGE YOUR FIRST WEAPON", "Ingot ready! Approach the illuminated ANVIL and press <b><color=#FFD966>[F]</color></b> to forge a Sword, Axe or Hammer.");
            else if (_stage == TutorialStage.CombatTraining)
                DrawCombatTrainingOverlay();
            else if (_stage == TutorialStage.MineFirstEncounter)
                DrawMineEncounterOverlay();
            else
            {
                Transform playerTr = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.transform : null;
                bool titanAlive = TitanBossController.Instance != null && TitanBossController.Instance.IsAlive;
                if (playerTr != null && playerTr.position.z >= CaveDungeonBuilder.VictoryAltarZ - 42f && !titanAlive)
                    DrawTopContextualBanner("TITAN DEFEATED! VICTORY PILLAR UNLOCKED", "The Victory Pillar has emerged in the Sanctum. Press <b><color=#FFD966>[F]</color></b> to consecrate victory.");
                else if (Time.time < _freePlayTipUntil)
                    DrawTopContextualBanner("CONGRATULATIONS! TUTORIAL COMPLETE!",
                        "You are ready to explore the cavern freely, forge new alloys, and defeat the Titan!");
            }
        }

        private void DrawAttentionPanel(Rect box, string topRibbon, float flashGlow, Color? accentOverride = null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.2f);
            Color accent = accentOverride ?? new Color(1.0f, 0.84f, 0.26f, 1f);
            Color prev = GUI.color;

            // Sombra exterior y aura brillante pulsante para que nunca pase desapercibido
            GUI.color = new Color(0f, 0f, 0f, 0.58f);
            GUI.DrawTexture(new Rect(box.x - 3f, box.y - 2f, box.width + 8f, box.height + 9f), _goldBorderTex);
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.16f + pulse * 0.14f + flashGlow * 0.24f);
            GUI.DrawTexture(new Rect(box.x - 8f, box.y - 8f, box.width + 16f, box.height + 16f), _goldBorderTex);
            GUI.color = Color.white;

            GUI.Box(box, GUIContent.none, _tipBoxStyle);
            DrawGoldFrame(box, 3.5f + flashGlow * 2.0f, flashGlow, accentOverride);

            // Filete interior sutil
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.30f);
            GUI.DrawTexture(new Rect(box.x + 6f, box.y + 6f, box.width - 12f, 1f), _goldBorderTex);
            GUI.DrawTexture(new Rect(box.x + 6f, box.yMax - 7f, box.width - 12f, 1f), _goldBorderTex);
            GUI.color = Color.white;

            if (!string.IsNullOrEmpty(topRibbon))
            {
                float rw = Mathf.Clamp(topRibbon.Length * 9.8f + 38f, 200f, box.width - 28f);
                GUI.Box(new Rect(box.x + (box.width - rw) * 0.5f, box.y - 13f, rw, 24f), $"<b>{topRibbon}</b>", _spotlightBadgeStyle);
            }
            GUI.color = prev;
        }

        private void DrawCenterAttentionCard(string message)
        {
            float w = 540f, h = 108f;
            Rect box = new Rect((Screen.width - w) * 0.5f, 108f, w, h);
            DrawAttentionPanel(box, "ATTENTION - TUTORIAL", 0.35f, new Color(1f, 0.45f, 0.18f, 1f));
            GUI.Label(new Rect(box.x + 14f, box.y + 16f, w - 28f, 24f), "[!] IMPORTANT GUIDE [!]", _tipTitleStyle);
            GUI.Label(new Rect(box.x + 16f, box.y + 42f, w - 32f, h - 50f), message, _tipBodyStyle);
        }

        private void DrawUISpotlightOverlay(RectTransform target, string text, string badge = "<b>v CLICK HERE v</b>")
        {
            Vector3[] corners = new Vector3[4]; target.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            DrawUISpotlightOverlay(new Rect(min.x - 8f, Screen.height - max.y - 8f, Mathf.Max(64f, max.x - min.x + 16f), Mathf.Max(36f, max.y - min.y + 16f)), text, badge);
        }

        private void DrawUISpotlightOverlay(Rect frame, string text, string badge)
        {
            Color prev = GUI.color;
            // Atenuacion perimetral sutil para dirigir la vista directo al boton destacado
            GUI.color = new Color(0.03f, 0.02f, 0.01f, 0.44f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Mathf.Max(0f, frame.y)), _goldBorderTex);
            GUI.DrawTexture(new Rect(0f, frame.yMax, Screen.width, Mathf.Max(0f, Screen.height - frame.yMax)), _goldBorderTex);
            GUI.DrawTexture(new Rect(0f, frame.y, Mathf.Max(0f, frame.x), frame.height), _goldBorderTex);
            GUI.DrawTexture(new Rect(frame.xMax, frame.y, Mathf.Max(0f, Screen.width - frame.xMax), frame.height), _goldBorderTex);

            GUI.color = new Color(1.0f, 0.85f, 0.20f, 0.22f + 0.18f * Mathf.Sin(Time.unscaledTime * 6.5f));
            GUI.DrawTexture(new Rect(frame.x - 6f, frame.y - 6f, frame.width + 12f, frame.height + 12f), _goldBorderTex);
            GUI.color = prev;
            DrawGoldFrame(frame, 5.5f, 0.35f);

            float bounce = Mathf.Sin(Time.unscaledTime * 7.5f) * 5f, bw = 210f, bh = 25f, bx = frame.x + (frame.width - bw) * 0.5f;
            float by = (frame.y - bh - 8f + bounce > 10f) ? (frame.y - bh - 8f + bounce) : (frame.yMax + 8f + bounce);
            GUI.Box(new Rect(bx, by, bw, bh), badge, _spotlightBadgeStyle);

            float tipW = 390f, tipH = 110f;
            float tipX = (frame.xMax + 20f + tipW < Screen.width - 12f) ? (frame.xMax + 18f) : Mathf.Max(12f, frame.x - tipW - 18f);
            float tipY = Mathf.Clamp(frame.y + (frame.height - tipH) * 0.5f, 20f, Screen.height - tipH - 16f);
            Rect box = new Rect(tipX, tipY, tipW, tipH);
            DrawAttentionPanel(box, "ACTION REQUIRED", 0.30f);
            GUI.Label(new Rect(box.x + 14f, box.y + 16f, tipW - 28f, 24f), "FOLLOW THIS STEP:", _tipTitleStyle);
            GUI.Label(new Rect(box.x + 16f, box.y + 42f, tipW - 32f, tipH - 50f), text, _tipBodyStyle);
        }

        private float ComputeBannerTransitionOffset(string key, out float flashGlow)
        {
            if (_lastBannerAnimKey != key)
            {
                if (!string.IsNullOrEmpty(_lastBannerAnimKey)) ForgeAudioSystem.PlayUITab();
                _lastBannerAnimKey = key; _bannerAnimStartTime = Time.unscaledTime;
            }
            float dt = Time.unscaledTime - _bannerAnimStartTime;
            flashGlow = dt < 0.85f ? Mathf.Sin((dt / 0.85f) * Mathf.PI) : 0f;
            if (dt >= 0.42f) return 0f;
            float u = dt / 0.42f;
            float back = 1f - Mathf.Pow(1f - u, 3f) + Mathf.Sin(u * Mathf.PI) * 0.14f;
            return (1f - back) * -36f;
        }

        private void DrawGoldFrame(Rect r, float t, float glowBoost = 0f, Color? accentOverride = null)
        {
            Color prev = GUI.color;
            Color accent = accentOverride ?? new Color(1.0f, 0.84f, 0.26f, 1f);
            if (glowBoost > 0.01f)
            {
                GUI.color = new Color(accent.r, accent.g, accent.b, glowBoost * 0.22f);
                GUI.DrawTexture(r, _goldBorderTex);
            }
            GUI.color = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01(0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5.2f) + glowBoost * 0.35f));
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _goldBorderTex);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _goldBorderTex);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _goldBorderTex);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _goldBorderTex);
            GUI.color = prev;
        }

        private void DrawTopContextualBanner(string title, string body)
        {
            float yOff = ComputeBannerTransitionOffset(title, out float flashGlow);
            float scaleW = 760f + flashGlow * 26f;
            Rect box = new Rect((Screen.width - scaleW) * 0.5f, 26f + yOff, scaleW, 92f);
            DrawAttentionPanel(box, "TUTORIAL OBJECTIVE", flashGlow);
            GUI.Label(new Rect(box.x + 18f, box.y + 15f, box.width - 36f, 24f), title, _tipTitleStyle);
            GUI.Label(new Rect(box.x + 18f, box.y + 40f, box.width - 36f, 44f), body, _tipBodyStyle);
        }

        private void EnsureGuiStyles()
        {
            if (_tipBoxStyle != null) return;
            _goldBorderTex = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave }; _goldBorderTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); _goldBorderTex.Apply();
            Color ribbon = new Color(1.0f, 0.84f, 0.29f, 1f);
            _goldRibbonTex = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave }; _goldRibbonTex.SetPixels(new[] { ribbon, ribbon, ribbon, ribbon }); _goldRibbonTex.Apply();
            Color bg = new Color(0.08f, 0.05f, 0.03f, 0.97f);
            _darkBoxTex = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave }; _darkBoxTex.SetPixels(new[] { bg, bg, bg, bg }); _darkBoxTex.Apply();
            _tipBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _darkBoxTex } };
            _tipTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true, normal = { textColor = new Color(1f, 0.91f, 0.49f) } };
            _tipBodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true, normal = { textColor = new Color(1f, 0.97f, 0.91f) } };
            _stepLeftStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true, normal = { textColor = new Color(1f, 0.97f, 0.91f) } };
            _spotlightBadgeStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true, normal = { background = _goldRibbonTex, textColor = new Color(0.11f, 0.07f, 0.02f) } };
            MedievalTypographyProvider.ApplyToGUIStyle(_tipTitleStyle, 16, FontStyle.Bold); MedievalTypographyProvider.ApplyToGUIStyle(_tipBodyStyle, 14, FontStyle.Normal);
            MedievalTypographyProvider.ApplyToGUIStyle(_stepLeftStyle, 13, FontStyle.Normal); MedievalTypographyProvider.ApplyToGUIStyle(_spotlightBadgeStyle, 13, FontStyle.Bold);
        }

        private void UpdateDiegeticStationBeacon()
        {
            if (_beaconSpotLight == null || _floorWarmPoolLight == null) return;
            bool isInteracting = BaseForgeStationController.IsAnyStationOccupied()
                || (_furnacePanel != null && _furnacePanel.IsOpen) || (_pressPanel != null && _pressPanel.IsOpen)
                || (_anvilPanel != null && _anvilPanel.IsOpen) || (_anvilCtrl != null && (_anvilCtrl.IsForging || _anvilCtrl.IsCarvingHandle));
            Transform targetTr = isInteracting ? null : ResolveCurrentObjectiveTransform();
            if (targetTr == null) { HideBeaconLights(); return; }

            GetStationVisualCenterAndBounds(targetTr, out Vector3 center, out _, out float maxY, out float radius);
            _beaconSpotLight.enabled = true; _floorWarmPoolLight.enabled = true;
            bool isCreature = targetTr.GetComponent<CaveCreatureBase>() != null || targetTr.GetComponent<TrainingDummyController>() != null;
            float bx = isCreature ? center.x : targetTr.position.x, bz = isCreature ? center.z : targetTr.position.z;
            bool onWorkshopStone = bx >= -14.2f && bx <= -4.8f && bz >= 3.0f && bz <= 17.0f;
            float floorY = isCreature
                ? UnitySafeUtility.ProbeGroundY(new Vector3(bx, targetTr.position.y + 0.55f, bz), targetTr, 0.8f, 3.5f)
                : (bz >= CaveDungeonBuilder.CaveEntranceZ - 0.5f ? CaveDungeonBuilder.GetFloorY(bx, bz) : (onWorkshopStone ? 0.08f : GreenPlateauTerrainController.GetSimpleHeight(bx, bz, 3.6f)));
            if (targetTr.GetComponent<TrainingDummyController>() != null) floorY = Mathf.Max(floorY, 0.032f);

            float pulse = 0.85f + 0.30f * Mathf.Sin(Time.time * 4.2f), topY = Mathf.Clamp(maxY, floorY + 1.2f, floorY + 3.2f);
            Vector3 spotPos = new Vector3(bx + 1.35f, topY + 3.1f, bz - 0.8f);
            _beaconSpotLight.transform.SetPositionAndRotation(spotPos, Quaternion.LookRotation(new Vector3(bx, floorY + 0.8f, bz) - spotPos));
            _beaconSpotLight.intensity = 28.0f * pulse; _floorWarmPoolLight.transform.position = new Vector3(bx + 0.8f, floorY + 0.9f, bz); _floorWarmPoolLight.intensity = 15.5f * pulse;

            if (_stationBeaconRing != null)
            {
                _stationBeaconRing.gameObject.SetActive(true);
                float baseDia = isCreature ? 2.45f : Mathf.Clamp(Mathf.Max(radius * 2.2f, 2.55f), 2.55f, 4.10f);
                float ringDia = baseDia * (0.96f + 0.06f * Mathf.Sin(Time.time * 4.2f));
                _stationBeaconRing.position = new Vector3(bx, floorY + 0.03f, bz);
                _stationBeaconRing.localScale = new Vector3(ringDia, 0.032f, ringDia);
                if (_beaconRingMat != null && _beaconRingMat.HasProperty("_EmissionColor")) _beaconRingMat.SetColor("_EmissionColor", new Color(1.0f, 0.70f, 0.16f) * (3.1f * pulse));
            }
        }
    }
}
