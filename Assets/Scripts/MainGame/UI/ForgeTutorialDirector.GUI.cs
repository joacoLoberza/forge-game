using UnityEngine;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Environment;
using ForjaGame.Forge;

namespace ForjaGame.UI
{
    /// <summary>
    /// Renderizado diegético de HUD y GUI del Tutorial (QTE, Forjado, Dummies de Combate y Primer Encuentro en Mina).
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
                    DrawUISpotlightOverlay(GetWeaponButtonsCombinedRect(), _activeSpotlightLabel, "<b>▼ CHOOSE 1 WEAPON ▼</b>");
                else
                    DrawUISpotlightOverlay(_activeSpotlightTarget, _activeSpotlightLabel);
                return;
            }
            if (!string.IsNullOrEmpty(_activeSpotlightLabel))
            {
                DrawCenterAttentionCard(_activeSpotlightLabel);
                return;
            }

            var asm = _anvilCtrl != null ? _anvilCtrl.GetComponent<AnvilAssemblyController>() : null;
            bool isAsm = asm != null && asm.IsAssembling;
            if (_anvilCtrl != null && (_anvilCtrl.IsForging || _anvilCtrl.IsCarvingHandle || isAsm))
            {
                DrawAnvilStepByStepGuideCard(isAsm);
                return;
            }

            if (Time.time < _mineBlockedTipUntil)
            {
                DrawCenterAttentionCard("Complete your Forging & Dummy Combat/Parry Training before entering the Mine!");
                return;
            }

            if (_stage == TutorialStage.ControlsQTE) { DrawControlsQTEScreen(); return; }
            if (_stage == TutorialStage.FurnaceSmelt)
                DrawTopContextualBanner("STEP 1/5: SMELTING FURNACE", "Approach the illuminated FURNACE and press [F] to smelt 3 ores into a Bloom (error-free tutorial).");
            else if (_stage == TutorialStage.PressCompact)
                DrawTopContextualBanner("STEP 2/5: MECHANICAL PRESS", "Bloom obtained! Go to the illuminated PRESS next to the furnace and press [F] to compact it into an Ingot.");
            else if (_stage == TutorialStage.AnvilForge)
                DrawTopContextualBanner("STEP 3/5: FORGE YOUR FIRST WEAPON", "Ingot ready! Approach the illuminated ANVIL and press [F] to forge a Sword, Axe or Hammer.");
            else if (_stage == TutorialStage.CombatTraining)
                DrawCombatTrainingOverlay();
            else if (_stage == TutorialStage.MineFirstEncounter)
                DrawMineEncounterOverlay();
            else
            {
                Transform playerTr = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.transform : null;
                bool titanAlive = ForjaGame.Combat.TitanBossController.Instance != null && ForjaGame.Combat.TitanBossController.Instance.IsAlive;
                if (playerTr != null && playerTr.position.z >= CaveDungeonBuilder.VictoryAltarZ - 42f && !titanAlive)
                    DrawTopContextualBanner("TITAN DEFEATED! VICTORY PILLAR UNLOCKED", "The Victory Pillar has emerged in the Sanctum. Press [F] to consecrate victory.");
                else if (Time.time < _freePlayTipUntil)
                    DrawTopContextualBanner(
                        "TUTORIAL COMPLETE — YOU ARE FREE TO EXPLORE!",
                        "<b>[E]</b> Bag & Gear  |  <b>[Q]</b> Dodge Roll (1s Invuln)  |  <b>[LMB]</b> Attack  |  <b>[Hold/Tap RMB]</b> Block/Parry"
                    );
            }
        }

        private void DrawCenterAttentionCard(string message)
        {
            float w = 460f, h = 92f;
            float x = (Screen.width - w) * 0.5f, y = 104f;
            Rect box = new Rect(x, y, w, h);
            GUI.Box(box, GUIContent.none, _tipBoxStyle);
            DrawGoldFrame(box, 3f);
            GUI.Label(new Rect(box.x + 10f, box.y + 8f, w - 20f, 22f), "⚠ GUIDE ⚠", _tipTitleStyle);
            GUI.Label(new Rect(box.x + 12f, box.y + 32f, w - 24f, h - 38f), message, _tipBodyStyle);
        }

        private void DrawAnvilStepByStepGuideCard(bool isAssembling)
        {
            float w = 376f, h = 218f;
            Rect box = new Rect(Screen.width - w - 18f, 140f, w, h);
            GUI.Box(box, GUIContent.none, _tipBoxStyle); DrawGoldFrame(box, 3f);

            float temp = _anvilCtrl.ThermalController != null ? _anvilCtrl.ThermalController.CurrentTemperature : 750f;
            bool isReheating = _anvilCtrl.ThermalController != null && _anvilCtrl.ThermalController.IsReheating;
            bool isQuenching = _anvilCtrl.QuenchDifferential != null && _anvilCtrl.QuenchDifferential.IsQuenching;

            string stepTitle, stepInstructions;
            if (isAssembling)
            {
                stepTitle = "STEP 5/5: ASSEMBLE SWORD";
                stepInstructions = "• Use [WASD] to fit blade tang over handle, [Q/R] to rotate.\n• [LEFT CLICK] to lock & press [SPACE] to claim!";
            }
            else if (_anvilCtrl.IsCarvingHandle)
            {
                stepTitle = "STEP 4/5: WOOD HANDLE";
                stepInstructions = "• Optional: shape wood edges with [2] Chisel + [R].\n• Tutorial protects your handle: press [SPACE] anytime to mount blade!";
            }
            else if (isQuenching || _anvilCtrl.IsEdgeComplete)
            {
                stepTitle = isQuenching ? "STEP 3/5: WATER QUENCHING" : "STEP 3/5: EDGE COMPLETE! QUENCH";
                stepInstructions = isQuenching ? "• Submerging steel blade in water...\n• Almost ready for the handle!" : "• Blade beveling complete (100%)!\n• Press [SPACE] now to quench in water.";
            }
            else if (isReheating || temp < 500f)
            {
                stepTitle = isReheating ? $"STEP 1/5: HEATING ({temp:F0}°C)" : $"STEP 1/5: HEAT METAL ({temp:F0}°C)";
                stepInstructions = isReheating
                    ? (temp > 1220f ? "<color=#FF5533>• OVERHEAT BURNING!</color> Press [H] immediately to return to anvil!"
                        : (temp >= 850f ? "• OPTIMAL HEAT! Press [H] to return to anvil." : "• Heating in hearth. Reach >850°C then press [H]."))
                    : "• Metal is cool. Press [H] to heat it in the forge.";
            }
            else
            {
                stepTitle = $"STEP 2/5: FORGE EDGE ({Mathf.RoundToInt(_anvilCtrl.EdgeProgress * 100f)}%)";
                stepInstructions = "• Keep [1] Hammer & [LEFT CLICK] along the glowing edges.\n• If heat drops <500°C, press [H] to reheat safely.";
            }
            GUI.Label(new Rect(box.x + 14f, box.y + 10f, w - 28f, 24f), stepTitle, _tipTitleStyle);
            GUI.Label(new Rect(box.x + 16f, box.y + 38f, w - 32f, h - 48f), stepInstructions, _stepLeftStyle);
        }

        private void DrawUISpotlightOverlay(RectTransform target, string text, string badge = "<b>▼ CLICK HERE ▼</b>")
        {
            Vector3[] corners = new Vector3[4]; target.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            DrawUISpotlightOverlay(new Rect(min.x - 8f, Screen.height - max.y - 8f, Mathf.Max(64f, max.x - min.x + 16f), Mathf.Max(36f, max.y - min.y + 16f)), text, badge);
        }

        private void DrawUISpotlightOverlay(Rect frame, string text, string badge)
        {
            Color prev = GUI.color;
            GUI.color = new Color(1.0f, 0.85f, 0.20f, 0.24f + 0.16f * Mathf.Sin(Time.unscaledTime * 6f));
            GUI.DrawTexture(frame, _goldBorderTex);
            GUI.color = prev;
            DrawGoldFrame(frame, 5f);
            float bounce = Mathf.Sin(Time.unscaledTime * 7.5f) * 5f, bw = 185f, bh = 24f, bx = frame.x + (frame.width - bw) * 0.5f;
            float by = (frame.y - bh - 6f + bounce > 10f) ? (frame.y - bh - 6f + bounce) : (frame.yMax + 6f + bounce);
            GUI.Box(new Rect(bx, by, bw, bh), badge, _spotlightBadgeStyle);
            float tipW = 340f, tipH = 86f, tipX = (frame.xMax + 18f + tipW < Screen.width - 12f) ? (frame.xMax + 16f) : Mathf.Max(12f, frame.x - tipW - 16f), tipY = Mathf.Clamp(frame.y + (frame.height - tipH) * 0.5f, 16f, Screen.height - tipH - 16f);
            Rect box = new Rect(tipX, tipY, tipW, tipH);
            GUI.Box(box, GUIContent.none, _tipBoxStyle); DrawGoldFrame(box, 2.5f);
            GUI.Label(new Rect(box.x + 8f, box.y + 6f, tipW - 16f, 20f), "★ ACTION REQUIRED ★", _tipTitleStyle);
            GUI.Label(new Rect(box.x + 10f, box.y + 26f, tipW - 20f, tipH - 30f), text, _tipBodyStyle);
        }

        private float ComputeBannerTransitionOffset(string key, out float flashGlow)
        {
            if (_lastBannerAnimKey != key) { _lastBannerAnimKey = key; _bannerAnimStartTime = Time.unscaledTime; }
            float dt = Time.unscaledTime - _bannerAnimStartTime;
            flashGlow = dt < 0.70f ? Mathf.Sin((dt / 0.70f) * Mathf.PI) : 0f;
            if (dt >= 0.42f) return 0f;
            float u = dt / 0.42f;
            float back = 1f - Mathf.Pow(1f - u, 3f) + Mathf.Sin(u * Mathf.PI) * 0.14f;
            return (1f - back) * -36f;
        }

        private void DrawGoldFrame(Rect r, float t, float glowBoost = 0f)
        {
            Color prev = GUI.color;
            if (glowBoost > 0.01f)
            {
                GUI.color = new Color(1.0f, 0.85f, 0.25f, glowBoost * 0.24f);
                GUI.DrawTexture(r, _goldBorderTex);
            }
            GUI.color = new Color(1.0f, 0.84f, 0.26f, Mathf.Clamp01(0.68f + 0.32f * Mathf.Sin(Time.unscaledTime * 5.2f) + glowBoost * 0.35f));
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _goldBorderTex);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _goldBorderTex);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _goldBorderTex);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _goldBorderTex);
            GUI.color = prev;
        }

        private void DrawTopContextualBanner(string title, string body)
        {
            float yOff = ComputeBannerTransitionOffset(title, out float flashGlow);
            float scaleW = 680f + flashGlow * 22f;
            Rect box = new Rect((Screen.width - scaleW) * 0.5f, 18f + yOff, scaleW, 74f);
            GUI.Box(box, GUIContent.none, _tipBoxStyle);
            DrawGoldFrame(box, 2.2f + flashGlow * 2.5f, flashGlow);
            GUI.Label(new Rect(box.x + 16f, box.y + 7f, box.width - 32f, 22f), title, _tipTitleStyle);
            GUI.Label(new Rect(box.x + 16f, box.y + 29f, box.width - 32f, 40f), body, _tipBodyStyle);
        }

        private void EnsureGuiStyles()
        {
            if (_tipBoxStyle != null) return;
            _goldBorderTex = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave }; _goldBorderTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); _goldBorderTex.Apply();
            Color bg = new Color(0.10f, 0.07f, 0.05f, 0.96f);
            _darkBoxTex = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave }; _darkBoxTex.SetPixels(new[] { bg, bg, bg, bg }); _darkBoxTex.Apply();
            _tipBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _darkBoxTex } };
            _tipTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true, normal = { textColor = new Color(1f, 0.84f, 0.32f) } };
            _tipBodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true, normal = { textColor = new Color(0.95f, 0.90f, 0.80f) } };
            _stepLeftStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true, normal = { textColor = new Color(0.96f, 0.92f, 0.84f) } };
            _spotlightBadgeStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 12, richText = true, normal = { background = _goldBorderTex, textColor = new Color(0.15f, 0.10f, 0.02f) } };
            MedievalTypographyProvider.ApplyToGUIStyle(_tipTitleStyle, 14, FontStyle.Bold); MedievalTypographyProvider.ApplyToGUIStyle(_tipBodyStyle, 13, FontStyle.Normal);
            MedievalTypographyProvider.ApplyToGUIStyle(_stepLeftStyle, 12, FontStyle.Normal); MedievalTypographyProvider.ApplyToGUIStyle(_spotlightBadgeStyle, 12, FontStyle.Bold);
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
