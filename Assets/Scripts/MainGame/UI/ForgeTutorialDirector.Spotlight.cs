using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.UI
{
    public partial class ForgeTutorialDirector
    {
        private readonly List<Button> _lockedButtons = new();
        private GUIStyle _spotlightBadgeStyle;

        public void RestoreAllLockedButtons()
        {
            for (int i = 0; i < _lockedButtons.Count; i++)
                if (_lockedButtons[i] != null) _lockedButtons[i].interactable = true;
            _lockedButtons.Clear();
            _activeSpotlightTarget = null;
            _activeSpotlightLabel = string.Empty;
        }

        private void UpdateStationUISpotlights()
        {
            RestoreAllLockedButtons();
            if (!IsTutorialActive) return;

            EnsureTutorialResourcesNeverDeplete();
            EnforceSingleAllowedInteraction();

            switch (_stage)
            {
                case TutorialStage.FurnaceSmelt:
                    ResolveFurnaceSpotlight();
                    break;
                case TutorialStage.PressCompact:
                    ResolvePressSpotlight();
                    break;
                case TutorialStage.AnvilForge:
                    ResolveAnvilSpotlight();
                    break;
            }
        }

        private void EnsureTutorialResourcesNeverDeplete()
        {
            var inv = InventoryController.LocalInstance ?? InventoryController.Instance;
            if (inv == null) return;

            int coal = inv.GetCount("Coal");
            if (coal < 5) inv.AddItem("Coal", 5 - coal);

            if (inv.Handles.Count < 2)
                inv.AddHandle(new HandleData { length = HandleLength.Medium, handleName = "Oak Handle", rangeModifier = 1f, speedModifier = 0f });

            if (_stage == TutorialStage.FurnaceSmelt && _furnaceCtrl != null && !_furnaceCtrl.IsSmeltInProgress && _furnaceCtrl.UnclaimedBloom == null && inv.Blooms.Count == 0)
            {
                int staged = _furnacePanel != null && _furnacePanel.StagedOres != null ? _furnacePanel.StagedOres.Count : 0;
                int totalOres = _furnaceCtrl.GetTotalOreCount() + staged + inv.GetCount("Iron") + inv.GetCount("Copper") + inv.GetCount("Gold");
                if (totalOres < 3) inv.AddItem("Iron", 3 - totalOres);
            }
        }

        private void LockBtn(Button b, bool allow)
        {
            if (b == null) return;
            if (!allow && b.interactable)
            {
                b.interactable = false;
                _lockedButtons.Add(b);
            }
            else if (allow && !b.interactable)
            {
                b.interactable = true;
            }
        }

        private void EnforceSingleAllowedInteraction()
        {
            if (_furnacePanel != null && _furnacePanel.IsOpen && _furnaceCtrl != null)
            {
                int staged = _furnacePanel.StagedOres != null ? _furnacePanel.StagedOres.Count : 0;
                int inCrucible = _furnaceCtrl.GetTotalOreCount();
                bool hasBloom = _furnaceCtrl.UnclaimedBloom != null;
                var inv = InventoryController.LocalInstance ?? InventoryController.Instance;
                bool hasBloomInInv = inv != null && inv.Blooms.Count > 0;

                bool stepPickOres = !hasBloom && !hasBloomInInv && inCrucible < 3 && (inCrucible + staged) < 3;
                bool stepInsertOres = !hasBloom && !hasBloomInInv && inCrucible < 3 && (inCrucible + staged) >= 3;
                bool stepRegulateTemp = !hasBloom && !hasBloomInInv && inCrucible >= 3;
                bool stepExtractBloom = hasBloom;
                bool stepCloseFurnace = !hasBloom && hasBloomInInv;

                if (_furnacePanel.OresContainer != null)
                {
                    foreach (var btn in _furnacePanel.OresContainer.GetComponentsInChildren<Button>(true))
                        LockBtn(btn, stepPickOres);
                }
                LockBtn(_furnacePanel.InsertOresButton, stepInsertOres);
                LockBtn(_furnacePanel.ExtractOreButton, false);

                if (stepRegulateTemp)
                {
                    bool tooHot = _furnaceCtrl.TargetMaxTemp > 0f && _furnaceCtrl.CurrentTemperature > _furnaceCtrl.TargetMaxTemp;
                    bool tooCold = _furnaceCtrl.TargetMinTemp > 0f && _furnaceCtrl.CurrentTemperature < _furnaceCtrl.TargetMinTemp;
                    LockBtn(_furnacePanel.AddCoalButton, !tooHot);
                    LockBtn(_furnacePanel.BellowsButton, !tooCold);
                }
                else
                {
                    LockBtn(_furnacePanel.AddCoalButton, false);
                    LockBtn(_furnacePanel.BellowsButton, false);
                }

                LockBtn(_furnacePanel.ExtractBloomButton, stepExtractBloom);
                LockBtn(_furnacePanel.CloseButton, stepCloseFurnace);
            }

            if (_pressPanel != null && _pressPanel.IsOpen && _pressCtrl != null)
            {
                var inv = InventoryController.LocalInstance ?? InventoryController.Instance;
                bool hasIngotInInv = inv != null && inv.Ingots.Count > 0;
                LockBtn(_pressPanel.RetrieveBloomButton, false);
                LockBtn(_pressPanel.MashButton, _pressCtrl.HasBloom && !_pressCtrl.IsCompleted);
                LockBtn(_pressPanel.ExtractIngotButton, _pressCtrl.IsCompleted);
                LockBtn(_pressPanel.CloseButton, hasIngotInInv && !_pressCtrl.HasBloom);
            }
            if (_anvilPanel != null && _anvilPanel.IsOpen && _anvilCtrl != null)
            {
                bool step1 = IsAnvilStep1Active(), step2 = _anvilPanel.Step2IngotSelection != null && _anvilPanel.Step2IngotSelection.activeInHierarchy, stepConf = _anvilPanel.StepIngotConformation != null && _anvilPanel.StepIngotConformation.activeInHierarchy;
                if (step1) { LockBtn(_anvilPanel.SwordButton, true); LockBtn(_anvilPanel.AxeButton, true); LockBtn(_anvilPanel.HammerButton, true); }
                else if (step2 || stepConf)
                {
                    Transform stepRoot = step2 ? _anvilPanel.Step2IngotSelection.transform : _anvilPanel.StepIngotConformation.transform;
                    foreach (var b in stepRoot.GetComponentsInChildren<Button>(true))
                    { string bn = b.name.ToLower(); if (bn.Contains("back") || bn.Contains("atras") || bn.Contains("regresar")) LockBtn(b, false); }
                }
                LockBtn(_anvilPanel.CloseButton, _anvilCtrl.HasIngotOnAnvil || stepConf);
            }
        }

        private void ResolveFurnaceSpotlight()
        {
            if (_furnacePanel == null || !_furnacePanel.IsOpen || _furnaceCtrl == null) return;
            var inv = InventoryController.LocalInstance ?? InventoryController.Instance;
            if (inv != null && inv.Blooms.Count > 0 && _furnaceCtrl.UnclaimedBloom == null)
            {
                _activeSpotlightTarget = GetRect(_furnacePanel.CloseButton);
                _activeSpotlightLabel = "CLICK TO EXIT TO PRESS";
                return;
            }
            if (_furnaceCtrl.UnclaimedBloom != null)
            {
                _activeSpotlightTarget = GetRect(_furnacePanel.ExtractBloomButton);
                _activeSpotlightLabel = "EXTRACT HOT BLOOM";
                return;
            }
            if (_furnaceCtrl.IsSmeltInProgress || _furnaceCtrl.GetTotalOreCount() >= 3)
            {
                bool tooHot = _furnaceCtrl.TargetMaxTemp > 0f && _furnaceCtrl.CurrentTemperature > _furnaceCtrl.TargetMaxTemp;
                bool tooCold = _furnaceCtrl.TargetMinTemp > 0f && _furnaceCtrl.CurrentTemperature < _furnaceCtrl.TargetMinTemp;
                bool coalStopped = _furnaceCtrl.TargetTemperature <= _furnaceCtrl.CurrentTemperature + 12f || _furnaceCtrl.TargetTemperature < _furnaceCtrl.TargetMinTemp;
                if (tooHot)
                {
                    _activeSpotlightTarget = GetRect(_furnacePanel.BellowsButton);
                    _activeSpotlightLabel = "TOO HOT! USE BELLOWS [SPACE] TO COOL";
                }
                else if (tooCold && coalStopped)
                {
                    _activeSpotlightTarget = GetRect(_furnacePanel.AddCoalButton);
                    _activeSpotlightLabel = "TOO COLD! ADD COAL [SHIFT] TO HEAT";
                }
                return;
            }
            int stagedCount = _furnacePanel.StagedOres != null ? _furnacePanel.StagedOres.Count : 0;
            if (stagedCount > 0 && (_furnaceCtrl.GetTotalOreCount() + stagedCount) >= 3)
            {
                _activeSpotlightTarget = GetRect(_furnacePanel.InsertOresButton);
                _activeSpotlightLabel = "INSERT 3 ORES INTO CRUCIBLE";
                return;
            }
            _activeSpotlightTarget = FindFirstChildButtonRect(_furnacePanel.OresContainer);
            _activeSpotlightLabel = "SELECT 3 ORES";
        }

        private void ResolvePressSpotlight()
        {
            if (_pressPanel == null || !_pressPanel.IsOpen || _pressCtrl == null) return;
            var inv = InventoryController.LocalInstance ?? InventoryController.Instance;
            if (inv != null && inv.Ingots.Count > 0 && !_pressCtrl.HasBloom)
            { _activeSpotlightTarget = GetRect(_pressPanel.CloseButton); _activeSpotlightLabel = "CLICK TO GO TO ANVIL"; return; }
            if (_pressCtrl.IsCompleted)
            { _activeSpotlightTarget = GetRect(_pressPanel.ExtractIngotButton); _activeSpotlightLabel = "COLLECT FORGED INGOT"; return; }
            if (_pressCtrl.HasBloom)
            { _activeSpotlightTarget = GetRect(_pressPanel.MashButton); _activeSpotlightLabel = "PRESS [SPACE] REPEATEDLY"; return; }
            _activeSpotlightTarget = FindFirstChildButtonRect(_pressPanel.BloomsContainer);
            _activeSpotlightLabel = "SELECT BLOOM";
        }

        private bool IsAnvilStep1Active() => _anvilPanel != null
            && ((_anvilPanel.Step1WeaponSelection != null && _anvilPanel.Step1WeaponSelection.activeInHierarchy)
             || (_anvilPanel.SwordButton != null && _anvilPanel.SwordButton.gameObject.activeInHierarchy));

        private void ResolveAnvilSpotlight()
        {
            if (_anvilPanel == null || !_anvilPanel.IsOpen) return;
            if (IsAnvilStep1Active())
            {
                _activeSpotlightTarget = GetRect(_anvilPanel.SwordButton) ?? GetRect(_anvilPanel.AxeButton) ?? GetRect(_anvilPanel.HammerButton) ?? (_anvilPanel.Step1WeaponSelection != null ? GetRect(_anvilPanel.Step1WeaponSelection.transform) : null);
                _activeSpotlightLabel = "CHOOSE 1 OF THE 3 WEAPONS:\nSWORD, AXE OR HAMMER";
                return;
            }
            if (_anvilPanel.Step2IngotSelection != null && _anvilPanel.Step2IngotSelection.activeInHierarchy)
            { _activeSpotlightTarget = FindFirstChildButtonRect(_anvilPanel.Step2IngotSelection.transform); _activeSpotlightLabel = "SELECT INGOT FOR ANVIL"; return; }
            if (_anvilPanel.StepIngotConformation != null && _anvilPanel.StepIngotConformation.activeInHierarchy)
            { _activeSpotlightTarget = FindFirstChildButtonRect(_anvilPanel.StepIngotConformation.transform); _activeSpotlightLabel = "START FORGING"; }
        }

        private Rect GetWeaponButtonsCombinedRect()
        {
            Rect r = default; bool any = false;
            foreach (var btn in new[] { _anvilPanel.SwordButton, _anvilPanel.AxeButton, _anvilPanel.HammerButton })
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.TryGetComponent<RectTransform>(out var rt)) continue;
                Vector3[] c = new Vector3[4]; rt.GetWorldCorners(c);
                Vector2 mn = RectTransformUtility.WorldToScreenPoint(null, c[0]), mx = RectTransformUtility.WorldToScreenPoint(null, c[2]);
                Rect b = new Rect(mn.x - 8f, Screen.height - mx.y - 8f, Mathf.Max(48f, mx.x - mn.x + 16f), Mathf.Max(32f, mx.y - mn.y + 16f));
                r = !any ? b : Rect.MinMaxRect(Mathf.Min(r.xMin, b.xMin), Mathf.Min(r.yMin, b.yMin), Mathf.Max(r.xMax, b.xMax), Mathf.Max(r.yMax, b.yMax));
                any = true;
            }
            return any ? r : new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.5f - 80f, 400f, 160f);
        }

        private static RectTransform GetRect(Component c) => c != null ? c.GetComponent<RectTransform>() : null;
        private static RectTransform FindFirstChildButtonRect(Transform root)
        {
            if (root == null) return null;
            foreach (var b in root.GetComponentsInChildren<Button>(false))
                if (b != null && b.interactable && b.gameObject.activeInHierarchy) return b.GetComponent<RectTransform>();
            return root as RectTransform;
        }
    }
}
