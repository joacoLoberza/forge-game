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

            if (anvilController.IsCarvingHandle)
            {
                if (_isDiscardingHandle) return true;

                if (anvilController.CompleteHandleCarving(out Mesh handleMesh, out HandleIntegrityResult validation))
                {
                    if (hammerAudioSource != null && strikeSound != null) hammerAudioSource.PlayOneShot(strikeSound);
                    if (!anvilController.TryGetComponent<AnvilAssemblyController>(out var asm))
                        asm = anvilController.gameObject.AddComponent<AnvilAssemblyController>();
                    asm.BeginAssembly(anvilController, validation, handleMesh, hammerAudioSource, strikeSound, sparkVFXPrefab, () => AssembleAndDeliverWeapon(validation, handleMesh));
                    return true;
                }
                else
                {
                    var inv = GetStationInteractorInventory();
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
                    if (hammerAudioSource != null && quenchSound != null) hammerAudioSource.PlayOneShot(quenchSound);
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
            bool isShaftWeapon = anvilController != null && (anvilController.SelectedWeaponType == WeaponType.Axe || anvilController.SelectedWeaponType == WeaponType.Hammer);
            HandleLength hLen = isShaftWeapon
                ? (shaftLen < 0.56f ? HandleLength.Short : (shaftLen <= 0.88f ? HandleLength.Medium : HandleLength.Long))
                : (totalLen < 0.88f ? HandleLength.Short : (totalLen <= 1.38f ? HandleLength.Medium : HandleLength.Long));
            float baseRng = anvilController != null ? (anvilController.SelectedWeaponType switch { WeaponType.Sword => 1.25f, WeaponType.Axe => 1.10f, WeaponType.Hammer => 1.35f, _ => 1.10f }) : 1.10f;
            float rangeMod = Mathf.Max(0.40f, totalLen / baseRng), speedMod = Mathf.Clamp((1.10f - totalLen) * 0.35f, -0.30f, +0.30f);

            bool isRefined = val.gripDiameter >= 0.025f && val.gripDiameter <= 0.065f;
            if (isRefined) speedMod += 0.08f;
            else if (val.gripDiameter > 0.075f) speedMod -= 0.08f;
            string shapeTag = isRefined ? "Refinado" : (val.gripDiameter > 0.075f ? "en Bruto" : "Esbelto");
            string partName = anvilController != null ? (anvilController.SelectedWeaponType == WeaponType.Sword ? "Espada" : (anvilController.SelectedWeaponType == WeaponType.Hammer ? "Martillo" : "Hacha")) : "Arma";
            var customHandle = new HandleData
            {
                length = hLen,
                handleName = $"{partName} Artesanal {shapeTag}",
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

                inv?.AddWeapon(weapon);
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

            float bw = 740f, bh = _isDiscardingHandle ? 56f : 108f;
            float bx = (Screen.width - bw) * 0.5f;
            float by = 68f;

            if (_isDiscardingHandle)
            {
                GUI.Box(new Rect(bx, by, bw, bh), "<b><color=#33FFAA>DESCARTANDO MANGO INÚTIL...</color> Deslizando nueva tabla al yunque</b>", _hudStyle);
                return;
            }

            var val = anvilController.ValidateCurrentHandle();
            var inv = GetStationInteractorInventory();
            int remHandles = inv != null ? inv.Handles.Count : 0;
            string handleCounter = $"• <color={(remHandles > 0 ? "#33FFAA" : "#FF5555")}>Mangos en bolsa: {remHandles}</color>";

            string wName = anvilController.SelectedWeaponType == WeaponType.Hammer ? "el martillo" : "el hacha";
            string fractureWarn = val.hasBrokenSection ? " <color=#FFAA22>(¡Sección débil se quebrará!)</color>" : "";
            string actionHint = val.isValid
                ? $"<color=#33FFAA>Presiona [ESPACIO] para ensamblar {wName}.</color>"
                : (remHandles > 0
                    ? "<color=#FFD700><b>Presiona [ESPACIO] para descartar el mango inútil y recibir una nueva tabla.</b></color>"
                    : "<color=#FF5555><b>¡Sin mangos de repuesto en inventario!</b></color>");

            string refinePrompt = val.gripDiameter > 0.065f
                ? "<color=#FFDD66><b>[Recomendación]</b> Desbasta los bordes para darle forma de mango ergonómico.</color>"
                : "<color=#33FFAA><b>[Ergonomía]</b> Grosor adecuado para un empuñe firme y seguro.</color>";

            string header = $"<b>TALLADO DE MANGO ARTESANAL</b> ({handleCounter})\n" +
                            $"{val.statusMessage}{fractureWarn}\n" +
                            $"{refinePrompt} • {actionHint}";

            GUI.Box(new Rect(bx, by, bw, bh), header, _hudStyle);
        }
    }
}
