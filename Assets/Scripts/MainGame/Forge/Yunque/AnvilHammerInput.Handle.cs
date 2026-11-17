using UnityEngine;
using ForjaGame.Inventory;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo de flujo de templado, tallado de mango y ensamblaje de arma para AnvilHammerInput.
    /// </summary>
    public partial class AnvilHammerInput
    {
        private bool _isDiscardingHandle = false;

        private bool HandleSpacebarPress()
        {
            if (anvilController == null) return false;

            if (anvilController.IsIngotRuinedTooSmall)
            {
                DiscardRuinedIngotAndReopenSelection();
                return true;
            }

            if (anvilController.IsCarvingHandle)
            {
                if (_isDiscardingHandle) return true;

                if (anvilController.CompleteHandleCarving(out Mesh handleMesh, out HandleIntegrityResult validation))
                {
                    if (!anvilController.TryGetComponent<AnvilAssemblyController>(out var asm))
                        asm = anvilController.gameObject.AddComponent<AnvilAssemblyController>();
                    asm.BeginAssembly(anvilController, validation, handleMesh, hammerAudioSource, strikeSound, sparkVFXPrefab, () => AssembleAndDeliverWeapon(validation, handleMesh));
                    return true;
                }
                else
                {
                    var inv = GetStationInteractorInventory();
                    if (ForjaGame.UI.ForgeTutorialDirector.Instance != null && ForjaGame.UI.ForgeTutorialDirector.Instance.IsTutorialActive && inv != null && inv.Handles.Count == 0)
                        inv.AddHandle(new ForjaGame.Forge.HandleData { length = ForjaGame.Forge.HandleLength.Medium, handleName = "Oak Handle", rangeModifier = 1f, speedModifier = 0f });
                    if (inv == null || inv.Handles.Count == 0)
                    {
                        Debug.LogWarning("[Anvil] No quedan mangos de repuesto en el inventario.");
                        return true;
                    }
                    inv.ConsumeHandle();
                    _isDiscardingHandle = true;
                    ResetCutState(true);
                    anvilController.DiscardAndRespawnHandle(hammerAudioSource, strikeSound, sparkVFXPrefab, () =>
                    {
                        _isDiscardingHandle = false;
                        ResetCutState(true);
                        _toolMode = AnvilToolMode.Cut;
                    });
                    return true;
                }
            }
            else if (anvilController.IsEdgeComplete)
            {
                if (anvilController.QuenchDifferential != null && anvilController.QuenchDifferential.IsQuenching)
                    return true;

                if (anvilController.QuenchDifferential != null)
                {
                    ResetCutState(true);
                    _toolMode = AnvilToolMode.Hammer;
                    anvilController.QuenchDifferential.ExecuteSelectiveQuench(
                        hammerAudioSource, quenchSound, () =>
                        {
                            anvilController.Quench();
                            var inv = GetStationInteractorInventory();
                            if (inv != null && inv.ConsumeHandle())
                            {
                                anvilController.PlaceHandleBoard(true, hammerAudioSource, strikeSound);
                                _toolMode = AnvilToolMode.Cut;
                            }
                            ResetCutState(true);
                        });
                    return true;
                }
                else if (anvilController.Quench())
                {
                    ForjaGame.Audio.ForgeAudioSystem.PlayQuenchSizzle(transform.position, 1.0f);
                    var inv = GetStationInteractorInventory();
                    if (inv != null && inv.ConsumeHandle())
                    {
                        anvilController.PlaceHandleBoard(true, hammerAudioSource, strikeSound);
                        _toolMode = AnvilToolMode.Cut;
                    }
                    _cutPoints.Clear();
                    return true;
                }
            }

            return false;
        }

        private void AssembleAndDeliverWeapon(HandleIntegrityResult val, Mesh handleMesh)
        {
            var assembly = stationController != null ? stationController.Assembly : FindFirstObjectByType<WeaponAssemblyController>();
            WeaponStats weapon = null;

            float lowestHandleX = val.gripX0;
            if (handleMesh != null)
                foreach (var v in handleMesh.vertices) if (v.x >= val.gripX0 - 0.02f && v.x < lowestHandleX) lowestHandleX = v.x;

            float maxMetalX = val.tangX1;
            Quaternion rot = Quaternion.identity;
            Vector3 target = Vector3.zero;

            if (anvilController?.ForgedBladeMesh != null)
            {
                var asm = anvilController.GetComponent<AnvilAssemblyController>();
                rot = asm != null ? asm.CustomHeadRotation : (anvilController.SelectedWeaponType == WeaponType.Sword ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));
                Vector3 off = asm != null ? asm.CustomHeadOffset : Vector3.zero;
                float tangMidX = (val.tangX0 + val.tangX1) * 0.5f;
                target = new Vector3(tangMidX + off.x, 0f, val.tangCenterZ + off.z);
                if (anvilController.SelectedWeaponType == WeaponType.Sword)
                    target -= (rot * new Vector3(anvilController.ForgedBladeMesh.bounds.min.x + 0.08f, 0f, 0f));
                foreach (var v in anvilController.ForgedBladeMesh.vertices)
                {
                    Vector3 p = rot * v + target;
                    if (p.x > maxMetalX) maxMetalX = p.x;
                }
            }

            float totalLen = Mathf.Max(0.35f, maxMetalX - lowestHandleX);
            float shaftLen = Mathf.Max(0.25f, val.tangX1 - lowestHandleX);
            HandleLength hLen = AnvilController.ClassifyHandleLength(shaftLen);
            float baseRng = anvilController != null ? (anvilController.SelectedWeaponType switch { WeaponType.Sword => 1.25f, WeaponType.Axe => 1.10f, WeaponType.Hammer => 1.35f, _ => 1.10f }) : 1.10f;
            float rangeMod = Mathf.Max(0.40f, totalLen / baseRng), speedMod = Mathf.Clamp((1.10f - totalLen) * 0.35f, -0.30f, +0.30f);

            bool isRefined = val.gripDiameter >= 0.025f && val.gripDiameter <= 0.065f;
            if (isRefined) speedMod += 0.08f;
            else if (val.gripDiameter > 0.075f) speedMod -= 0.08f;
            string shapeTag = isRefined ? "Refined" : (val.gripDiameter > 0.075f ? "Raw" : "Slender");
            string partName = anvilController != null ? (anvilController.SelectedWeaponType == WeaponType.Sword ? "Sword" : (anvilController.SelectedWeaponType == WeaponType.Hammer ? "Hammer" : "Axe")) : "Weapon";
            var customHandle = new HandleData
            {
                length = hLen,
                handleName = $"Artisan {partName} {shapeTag}",
                rangeModifier = rangeMod,
                speedModifier = speedMod
            };

            if (assembly != null && anvilController.CurrentIngot != null)
            {
                weapon = assembly.AssembleWeapon(
                    anvilController.CurrentIngot,
                    anvilController.SelectedWeaponType,
                    customHandle,
                    anvilController.ForgedBladeMesh,
                    anvilController.RemainingMassRatio
                );
            }

            var inv = GetStationInteractorInventory();
            if (weapon != null)
            {
                // Clonar mallas para garantizar persistencia independiente de la mesa del yunque
                weapon.customMesh = anvilController.ForgedBladeMesh != null ? Object.Instantiate(anvilController.ForgedBladeMesh) : null;
                weapon.customHandleMesh = handleMesh != null ? Object.Instantiate(handleMesh) : null;
                weapon.customBladeMaterial = anvilController.ForgedBladeMaterial != null ? new Material(anvilController.ForgedBladeMaterial) : null;

                Material handleMat = null;
                if (anvilController.IngotTransform != null)
                {
                    var mr = anvilController.IngotTransform.GetComponent<MeshRenderer>();
                    if (mr != null && mr.material != null) handleMat = new Material(mr.material);
                }
                weapon.customHandleMaterial = handleMat;

                weapon.bladeMountPosition = target;
                weapon.bladeMountRotation = rot;
                weapon.gripCenterOffset = new Vector3((val.gripX0 + val.gripX1) * 0.5f, 0f, val.gripCenterZ);

                int sharpFront = anvilController != null ? anvilController.SharpFrontCount : 0;
                int sharpBack = anvilController != null ? anvilController.SharpBackCount : 0;
                weapon.isFrontFaceDominant = (sharpFront >= sharpBack);
                weapon.CalculateProfile();
                if (!string.IsNullOrEmpty(weapon.Profile.generatedName)) weapon.weaponName = $"{weapon.Profile.generatedName} ({weapon.composition.GetAlloyName(partName)})";
                inv?.AddWeapon(weapon);
                ForjaGame.Audio.ForgeAudioSystem.PlayWeaponClaimed(transform.position);
            }

            RestoreCamera();
            anvilController.ClearAnvilSurface();
            if (stationController != null) stationController.ReleaseStation();
            var gs = FindFirstObjectByType<ForjaGame.Core.GameStateController>();
            gs?.TryChangeState(ForjaGame.Core.GameState.Exploration);
        }

        private void DrawHandleOverlayGUI()
        {
            if (anvilController == null || !anvilController.IsCarvingHandle) return;

            float bw = 840f, bh = _isDiscardingHandle ? 60f : 125f;
            float bx = (Screen.width - bw) * 0.5f;
            float by = 68f;

            if (_isDiscardingHandle)
            {
                GUI.Box(new Rect(bx, by, bw, bh), "<size=14><b><color=#166534>DISCARDING UNUSABLE HANDLE...</color></b></size>\n<color=#1C1917>Sliding new wood board onto anvil...</color>", _hudStyle);
                return;
            }

            var val = anvilController.ValidateCurrentHandle();
            var inv = GetStationInteractorInventory();
            int remHandles = inv != null ? inv.Handles.Count : 0;
            string handleCounter = $"• <color={(remHandles > 0 ? "#166534" : "#991B1B")}><b>Handles in bag: {remHandles}</b></color>";

            string wName = anvilController.SelectedWeaponType == WeaponType.Hammer ? "the hammer" : (anvilController.SelectedWeaponType == WeaponType.Axe ? "the axe" : "the sword");
            string fractureWarn = val.hasBrokenSection ? " <color=#991B1B><b>(Weak section will break!)</b></color>" : "";
            string actionHint = val.isValid
                ? $"<color=#166534><b>Press [SPACE] to assemble {wName}.</b></color>"
                : (remHandles > 0
                    ? "<color=#9A3412><b>Press [SPACE] to discard unusable handle and receive a new board.</b></color>"
                    : "<color=#991B1B><b>No spare handles in inventory!</b></color>");

            string refinePrompt = val.gripDiameter > 0.065f
                ? "<color=#9A3412><b>[Tip]</b> Trim edges to sculpt an ergonomic handle.</color>"
                : "<color=#166534><b>[Ergonomics]</b> Proper thickness for a firm, secure grip.</color>";

            float lenCm = anvilController.CurrentHandleLength * 100f;
            var lenCat = AnvilController.ClassifyHandleLength(anvilController.CurrentHandleLength);
            string lenTag = lenCat == HandleLength.Short ? "Short <=70" : (lenCat == HandleLength.Medium ? "Medium 71-120" : "Long >120");
            string header = $"<size=14><b>HANDLE CARVING</b> ({handleCounter} • {lenCm:F0}/160 cm [{lenTag}])</size>\n" +
                            $"<color=#1C1917>{val.statusMessage}{fractureWarn}</color>\n" +
                            $"{refinePrompt}  •  {actionHint}";

            GUI.Box(new Rect(bx, by, bw, bh), header, _hudStyle);
        }

        private void DrawIngotTooSmallWarningGUI()
        {
            EnsureUIStyles();
            float w = 780f, h = 165f, x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
            string msg = "<size=16><b><color=#991B1B>⚠ WARNING: INGOT TOO SMALL TO CONTINUE (< 10% OF MIN. AREA) ⚠</color></b></size>\n\n" +
                         "<color=#1C1917>The ingot has been cut below <b>10%</b> of the smallest ingot's area and no longer has enough mass to forge a weapon.\n" +
                         "You must restart the ingot process from 0.</color>";
            GUI.Box(new Rect(x, y, w, h), msg, _parchmentCardStyle);
            if (GUI.Button(new Rect((Screen.width - 440f) * 0.5f, y + h - 48f, 440f, 38f), "<b>[SPACE] RETURN TO INGOT SELECTION (START FROM 0)</b>", _goldBannerStyle))
            {
                ForjaGame.Audio.ForgeAudioSystem.PlayUIClick();
                DiscardRuinedIngotAndReopenSelection();
            }
        }
    }
}
