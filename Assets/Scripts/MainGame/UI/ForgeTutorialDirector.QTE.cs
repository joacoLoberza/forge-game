using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Audio;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Environment;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    public partial class ForgeTutorialDirector
    {
        private int _qteStep; // 0=WASD, 1=Shift+Space+Q(Roll), 2=E Inventory, 3=F at Furnace
        private bool _qteW, _qteA, _qteS, _qteD, _qteShift, _qteSpace, _qteRoll, _qteInvOpened, _qteInvClosed;
        private float _qteAdvanceAt = -1f;
        private Transform _mineEntranceMarker;
        public void NotifyPlayerRolled() { if (_stage == TutorialStage.ControlsQTE && _qteStep == 1 && !_qteRoll) { _qteRoll = true; ForgeAudioSystem.PlayUITab(); } }

        private void CreateDiegeticBeaconLights()
        {
            var spotGo = new GameObject("DiegeticStationSpotCone", typeof(Light));
            spotGo.transform.SetParent(transform, false);
            _beaconSpotLight = spotGo.GetComponent<Light>();
            _beaconSpotLight.type = LightType.Spot; _beaconSpotLight.spotAngle = 72f; _beaconSpotLight.innerSpotAngle = 40f;
            _beaconSpotLight.range = 18.0f; _beaconSpotLight.color = new Color(1.0f, 0.70f, 0.22f);
            _beaconSpotLight.intensity = 28.0f; _beaconSpotLight.shadows = LightShadows.None;

            var poolGo = new GameObject("DiegeticFloorWarmPool", typeof(Light));
            poolGo.transform.SetParent(transform, false);
            _floorWarmPoolLight = poolGo.GetComponent<Light>();
            _floorWarmPoolLight.type = LightType.Point; _floorWarmPoolLight.range = 11.5f;
            _floorWarmPoolLight.color = new Color(1.0f, 0.74f, 0.24f); _floorWarmPoolLight.intensity = 15.0f;
            _floorWarmPoolLight.shadows = LightShadows.None;

            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "StationDiegeticBeaconHalo";
            ringGo.transform.SetParent(transform, false);
            if (ringGo.TryGetComponent<Collider>(out var col)) UnitySafeUtility.DestroySafe(col);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _beaconRingMat = new Material(lit) { name = "Mat_StationBeaconHalo", color = new Color(1.0f, 0.76f, 0.18f, 1f) };
            if (_beaconRingMat.HasProperty("_BaseColor")) _beaconRingMat.SetColor("_BaseColor", _beaconRingMat.color);
            if (_beaconRingMat.HasProperty("_EmissionColor")) { _beaconRingMat.EnableKeyword("_EMISSION"); _beaconRingMat.SetColor("_EmissionColor", new Color(1.0f, 0.68f, 0.14f) * 3.2f); }
            var ringMr = ringGo.GetComponent<MeshRenderer>();
            ringMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringMr.receiveShadows = false; ringMr.sharedMaterial = _beaconRingMat;
            _stationBeaconRing = ringGo.transform;
        }

        private Transform ResolveCurrentObjectiveTransform()
        {
            if (_stage == TutorialStage.ControlsQTE)
                return _qteStep == 3 ? (_furnaceStation != null ? _furnaceStation.transform : _furnaceCtrl?.transform) : null;
            if (_stage == TutorialStage.FurnaceSmelt) return _furnaceStation != null ? _furnaceStation.transform : _furnaceCtrl?.transform;
            if (_stage == TutorialStage.PressCompact) return _pressStation != null ? _pressStation.transform : _pressCtrl?.transform;
            if (_stage == TutorialStage.AnvilForge) return _anvilStation != null ? _anvilStation.transform : _anvilCtrl?.transform;
            if (_stage == TutorialStage.CombatTraining)
                return _dummyHits < 3 ? TrainingDummyArenaBuilder.StrikeDummy?.transform : TrainingDummyArenaBuilder.SparringDummy?.transform;
            if (_stage == TutorialStage.MineFirstEncounter)
            {
                var spider = ForjaGame.Resources.MineSpawnerController.FirstDoorSpider;
                var pl = PlayerMovement.LocalInstance;
                bool spiderEngaged = spider != null && spider.IsAlive && spider.HasTargetEngaged;
                if (!spiderEngaged && pl != null && pl.transform.position.z < CaveDungeonBuilder.CaveEntranceZ - 2.6f)
                {
                    if (_mineEntranceMarker == null)
                    {
                        _mineEntranceMarker = new GameObject("MineEntranceBeaconAnchor").transform;
                        _mineEntranceMarker.SetParent(transform, false);
                        _mineEntranceMarker.position = new Vector3(0f, 0.05f, CaveDungeonBuilder.CaveEntranceZ - 0.8f);
                    }
                    return _mineEntranceMarker;
                }
                if (spider != null && spider.IsAlive) return spider.transform;
                var coal = ForjaGame.Resources.MineSpawnerController.FirstCoalVein;
                if (!_hasMinedFirstCoal && coal != null && coal.IsAlive) return coal.transform;
                if (_firstTunnelWall != null && _firstTunnelWall.IsAlive) return _firstTunnelWall.transform;
            }
            return null;
        }

        private static void GetStationVisualCenterAndBounds(Transform root, out Vector3 center, out float bottomY, out float topY, out float horizRadius)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false; Bounds combined = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r.bounds.size.sqrMagnitude < 0.001f) continue;
                if (!hasBounds) { combined = r.bounds; hasBounds = true; } else combined.Encapsulate(r.bounds);
            }
            center = hasBounds ? combined.center : (root.position + Vector3.up * 0.9f);
            bottomY = hasBounds ? combined.min.y : root.position.y;
            topY = hasBounds ? combined.max.y : (root.position.y + 1.8f);
            horizRadius = hasBounds ? Mathf.Max(combined.extents.x, combined.extents.z) : 1.2f;
        }

        private void UpdateControlsQTE()
        {
            if (PauseMenuPanel.IsGamePaused) return;
            var kb = Keyboard.current;
            if (_qteAdvanceAt > 0f)
            {
                if (Time.time >= _qteAdvanceAt) { _qteAdvanceAt = -1f; _qteStep++; }
                return;
            }
            if (_qteStep == 0 && kb != null)
            {
                if (!_qteW && kb.wKey.isPressed) { _qteW = true; ForgeAudioSystem.PlayUITab(); }
                if (!_qteA && kb.aKey.isPressed) { _qteA = true; ForgeAudioSystem.PlayUITab(); }
                if (!_qteS && kb.sKey.isPressed) { _qteS = true; ForgeAudioSystem.PlayUITab(); }
                if (!_qteD && kb.dKey.isPressed) { _qteD = true; ForgeAudioSystem.PlayUITab(); }
                if (_qteW && _qteA && _qteS && _qteD) { ForgeAudioSystem.PlayItemSelect(); _qteAdvanceAt = Time.time + 0.45f; }
            }
            else if (_qteStep == 1 && kb != null)
            {
                if (!_qteShift && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) { _qteShift = true; ForgeAudioSystem.PlayUITab(); }
                if (!_qteSpace && kb.spaceKey.isPressed) { _qteSpace = true; ForgeAudioSystem.PlayUITab(); }
                if (!_qteRoll && kb.qKey.wasPressedThisFrame) { _qteRoll = true; ForgeAudioSystem.PlayUITab(); }
                if (_qteShift && _qteSpace && _qteRoll) { ForgeAudioSystem.PlayItemSelect(); _qteAdvanceAt = Time.time + 0.45f; }
            }
            else if (_qteStep == 2)
            {
                bool invOpen = InventoryPanel.Instance != null && InventoryPanel.Instance.IsOpen;
                if (!_qteInvOpened && invOpen) { _qteInvOpened = true; ForgeAudioSystem.PlayUITab(); }
                if (_qteInvOpened && !invOpen) { _qteInvClosed = true; ForgeAudioSystem.PlayItemSelect(); _qteAdvanceAt = Time.time + 0.35f; }
            }
            else if (_qteStep >= 3)
            {
                bool inFurnace = (_furnacePanel != null && _furnacePanel.IsOpen) || (_furnaceStation != null && _furnaceStation.IsOccupied);
                if (inFurnace) { _stage = TutorialStage.FurnaceSmelt; ForgeAudioSystem.PlayUITab(); }
            }
        }

        public bool IsAnyForgeStationLockedOpen => IsTutorialActive
            && ((_furnacePanel != null && _furnacePanel.IsOpen && !CanReleaseOccupiedStation(_furnaceStation))
             || (_pressPanel != null && _pressPanel.IsOpen && !CanReleaseOccupiedStation(_pressStation))
             || (_anvilPanel != null && _anvilPanel.IsOpen && !CanReleaseOccupiedStation(_anvilStation)));

        public bool CanReleaseOccupiedStation(BaseForgeStationController station)
        {
            if (!IsTutorialActive || station == null) return true;
            var inv = InventoryController.LocalInstance ?? InventoryController.Instance;
            if (station is FurnaceStationController) return _stage > TutorialStage.FurnaceSmelt || (inv != null && inv.Blooms.Count > 0 && _furnaceCtrl?.UnclaimedBloom == null);
            if (station is PressStationController) return _stage > TutorialStage.PressCompact || (inv != null && inv.Ingots.Count > 0 && (_pressCtrl == null || !_pressCtrl.HasBloom));
            if (station is AnvilStationController) return _stage > TutorialStage.AnvilForge;
            return true;
        }

        private void DrawControlsQTEScreen()
        {
            string qteKey = $"QTE_{_qteStep}_{_qteInvOpened}";
            float yOff = ComputeBannerTransitionOffset(qteKey, out float flashGlow);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.2f);
            float w = 600f, h = _qteStep == 0 ? 214f : 172f, x = (Screen.width - w) * 0.5f, y = 46f + yOff;
            Rect box = new Rect(x, y, w, h);
            GUI.color = new Color(1f, 0.72f, 0.15f, 0.14f + pulse * 0.10f);
            GUI.DrawTexture(new Rect(x - 10f, y - 10f, w + 20f, h + 20f), _goldBorderTex);
            GUI.color = Color.white;
            GUI.Box(box, GUIContent.none, _tipBoxStyle);
            DrawGoldFrame(box, 3.2f + flashGlow * 2.5f, flashGlow + pulse * 0.25f);

            GUI.Box(new Rect(x + (w - 270f) * 0.5f, y - 13f, 270f, 24f), $"<b>★ TUTORIAL — STEP {_qteStep + 1}/4 ★</b>", _spotlightBadgeStyle);
            float barProg = _qteStep == 0 ? (((_qteW ? 1 : 0) + (_qteA ? 1 : 0) + (_qteS ? 1 : 0) + (_qteD ? 1 : 0)) / 16f) : (_qteStep * 0.25f + 0.08f);
            GUI.color = new Color(0.18f, 0.14f, 0.09f, 0.95f); GUI.DrawTexture(new Rect(x + 28f, y + 18f, w - 56f, 5f), _goldBorderTex);
            GUI.color = new Color(1f, 0.80f, 0.22f, 0.95f); GUI.DrawTexture(new Rect(x + 28f, y + 18f, (w - 56f) * Mathf.Clamp01(barProg), 5f), _goldBorderTex);
            GUI.color = Color.white;
            if (_qteStep == 0)
            {
                int doneN = (_qteW ? 1 : 0) + (_qteA ? 1 : 0) + (_qteS ? 1 : 0) + (_qteD ? 1 : 0);
                GUI.Label(new Rect(x + 12f, y + 26f, w - 24f, 24f), $"<size=15><b>MOVEMENT TRAINING — PRESS ALL 4 KEYS ({doneN}/4)</b></size>", _tipTitleStyle);
                float cx = x + w * 0.5f;
                DrawKeyChip(new Rect(cx - 29f, y + 58f, 58f, 44f), "W", _qteW); DrawKeyChip(new Rect(cx - 95f, y + 110f, 58f, 44f), "A", _qteA);
                DrawKeyChip(new Rect(cx - 29f, y + 110f, 58f, 44f), "S", _qteS); DrawKeyChip(new Rect(cx + 37f, y + 110f, 58f, 44f), "D", _qteD);
                GUI.Label(new Rect(x + 14f, y + 166f, w - 28f, 36f), "Press <b><color=#FFD966>W, A, S, D</color></b> to move around the workshop and unlock the next tutorial step.", _tipBodyStyle);
                return;
            }
            string hdr = _qteStep == 1 ? "<b>SPRINT, JUMP & ROLL TRAINING</b>" : (_qteStep == 2 ? "<b>JOURNAL & INVENTORY TRAINING</b>" : "<b>ENTER THE SMELTING FURNACE</b>");
            GUI.Label(new Rect(x + 12f, y + 26f, w - 24f, 24f), hdr, _tipTitleStyle);
            if (_qteStep == 1) { DrawKeyChip(new Rect(x + 34f, y + 60f, 166f, 44f), "SHIFT (Sprint)", _qteShift); DrawKeyChip(new Rect(x + 217f, y + 60f, 166f, 44f), "SPACE (Jump)", _qteSpace); DrawKeyChip(new Rect(x + 400f, y + 60f, 166f, 44f), "Q (Roll)", _qteRoll); }
            else if (_qteStep == 2) { DrawKeyChip(new Rect(x + 95f, y + 60f, 195f, 44f), _qteInvOpened ? "E (Opened ✓)" : "E (Inventory)", _qteInvOpened && _qteInvClosed); DrawKeyChip(new Rect(x + 320f, y + 60f, 180f, 44f), "ESC (Pause)", true); }
            else DrawKeyChip(new Rect(x + (w - 210f) * 0.5f, y + 60f, 210f, 44f), "F (Use Station)", false);
            string warmHint = (ShaderWarmupCurtain.Instance != null && ShaderWarmupCurtain.Instance.IsWarming) ? "<color=#9EE8FF>[Loading World — Practice keys now!]</color> " : "";
            string msg = _qteStep == 1 ? $"{warmHint}Press <b><color=#FFD966>[SHIFT]</color></b> Sprint, <b><color=#FFD966>[SPACE]</color></b> Jump & <b><color=#FFD966>[Q + WASD]</color></b> Roll (1s Invuln)!"
                : (_qteStep == 2 ? (_qteInvOpened ? "Great! Press <b>[E]</b> (or <b>[ESC]</b>) to close Inventory." : "Press <b><color=#FFD966>[E]</color></b> to open your Journal & Pickaxe, then close it.")
                : "Walk toward the <b><color=#FFD966>illuminated FURNACE</color></b> ahead and press <b>[F]</b> to begin forging!");
            GUI.Label(new Rect(x + 14f, y + 118f, w - 28f, 38f), msg, _tipBodyStyle);
        }

        private void DrawKeyChip(Rect r, string label, bool done)
        {
            float bob = done ? 2f : Mathf.Sin(Time.unscaledTime * 6.5f) * 1.6f;
            Rect k = new Rect(r.x, r.y + bob, r.width, r.height);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(k.x + 2f, k.y + 4f, k.width, k.height), _goldBorderTex);
            GUI.color = done ? new Color(0.14f, 0.62f, 0.28f, 0.96f) : new Color(0.28f, 0.19f, 0.09f, 0.98f);
            GUI.DrawTexture(k, _goldBorderTex);
            float glow = done ? 1f : (0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 7f));
            GUI.color = done ? new Color(0.45f, 1.0f, 0.55f, 1f) : new Color(1.0f, 0.84f, 0.24f, glow);
            GUI.DrawTexture(new Rect(k.x, k.y, k.width, 2.5f), _goldBorderTex); GUI.DrawTexture(new Rect(k.x, k.yMax - 2.5f, k.width, 2.5f), _goldBorderTex);
            GUI.DrawTexture(new Rect(k.x, k.y, 2.5f, k.height), _goldBorderTex); GUI.DrawTexture(new Rect(k.xMax - 2.5f, k.y, 2.5f, k.height), _goldBorderTex);
            GUI.color = prev;
            GUI.Label(k, done ? $"<size=15><b>✓ {label}</b></size>" : $"<size=16><b>{label}</b></size>", _tipTitleStyle);
        }

        private void DrawCombatTrainingOverlay()
        {
            var spar = TrainingDummyArenaBuilder.SparringDummy;
            string head = "STEP 4/5: COMBAT TRAINING DUMMIES (OUTSIDE MINE)";
            string c1 = _dummyHits >= 3 ? "<color=#66F09A>✓ 1. Strike (3/3) [LMB]</color>" : $"<b>▶ 1. Strike ({_dummyHits}/3) [LMB]</b>";
            string c2 = _dummyBlocked ? "<color=#66F09A>✓ 2. Block [Hold RMB]</color>" : (_dummyHits >= 3 ? "<color=#FFD966><b>▶ 2. Block [Hold RMB]</b></color>" : "<color=#9E9585>○ 2. Block [Hold RMB]</color>");
            string c3 = _dummyParried ? "<color=#66F09A>✓ 3. Parry [Tap RMB]</color>" : (_dummyBlocked ? "<color=#FFD966><b>▶ 3. Parry [Tap RMB]</b></color>" : "<color=#9E9585>○ 3. Parry [Tap RMB]</color>");
            string c4 = _dummyRolled ? "<color=#66F09A>✓ 4. Roll [Q+WASD]</color>" : (_dummyParried ? "<color=#66EEFF><b>▶ 4. Dodge Roll [Q + WASD] (1s Invuln)</b></color>" : "<color=#9E9585>○ 4. Roll [Q]</color>");
            DrawTopContextualBanner(head, $"{c1}  |  {c2}\n{c3}  |  {c4}");

            if (spar != null && spar.IsTelegraphingStrike && _dummyHits >= 3 && (!_dummyBlocked || !_dummyParried || !_dummyRolled))
            {
                float bw = 440f, bh = 58f, bx = (Screen.width - bw) * 0.5f, by = 100f;
                Rect box = new Rect(bx, by, bw, bh);
                GUI.Box(box, GUIContent.none, _tipBoxStyle); DrawGoldFrame(box, 3f);
                bool parryNow = _dummyBlocked && !_dummyParried && spar.IsInParrySweetSpot;
                string alert = !_dummyBlocked ? "<color=#66CCFF><b>DUMMY ATTACKING — HOLD [RMB] TO BLOCK!</b></color>"
                    : (!_dummyParried ? (parryNow ? "<color=#FFE044><b>★ PARRY NOW! TAP [RMB]! ★</b></color>" : "<color=#FF8866>DUMMY WINDING UP — READY TO TAP [RMB]...</color>")
                    : "<color=#66EEFF><b>★ DODGE ROLL NOW! PRESS [Q + WASD] (1s INVULN)! ★</b></color>");
                GUI.Label(new Rect(bx + 10f, by + 6f, bw - 20f, 24f), alert, _tipBodyStyle);
                Rect bar = new Rect(bx + 24f, by + 35f, bw - 48f, 12f);
                GUI.color = new Color(0.15f, 0.12f, 0.10f, 0.9f); GUI.DrawTexture(bar, _goldBorderTex);
                GUI.color = _dummyParried ? new Color(0.24f, 0.88f, 1f) : (parryNow ? new Color(1f, 0.86f, 0.16f) : (_dummyBlocked ? new Color(0.92f, 0.35f, 0.22f) : new Color(0.28f, 0.72f, 1f)));
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * spar.WindupNormalized, bar.height), _goldBorderTex);
                GUI.color = Color.white;
            }
        }

        private void DrawMineEncounterOverlay()
        {
            var spider = ForjaGame.Resources.MineSpawnerController.FirstDoorSpider;
            bool spiderDead = spider != null && !spider.IsAlive;
            if (!spiderDead)
                DrawTopContextualBanner("STEP 5/5 — MINE ENTRANCE (1/3): DEFEAT THE FIRST SPIDER", "Slay the Cave Spider using <b>[LMB] Attack</b>, <b>[Q+WASD] Roll</b>, <b>[RMB] Block/Parry</b>!");
            else if (!_hasMinedFirstCoal) DrawTopContextualBanner("STEP 5/5 — MINE ENTRANCE (2/3): MINE YOUR FIRST COAL", _firstCoalVeinBroken ? "Coal vein broken! Walk over the <b>Coal on the ground</b> to collect your first mineral." : "Spider defeated! Open <b>[E] Inventory</b> (Weapons) to equip your <b>Pickaxe</b>, then break the glowing <b>Coal Vein [LMB]</b>!");
            else DrawTopContextualBanner("STEP 5/5 — MINE ENTRANCE (3/3): BREAK THE WEB BARRIER", "Coal gathered! Equip your <b>Weapon</b> from <b>[E] Inventory</b> (Sword deals x5 damage) and break the <b>Web Wall</b> ahead to unlock the Mine!");
        }
    }
}
