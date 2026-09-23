using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Inventory;

namespace ForjaGame.Forge
{
    public enum AssemblyStage { Positioning, Sliding, Inspection }

    /// <summary>
    /// Controlador interactivo de ensamblaje en el yunque:
    /// Posicionamiento estricto por silueta del lingote cortado, deslizamiento e inspección.
    /// </summary>
    public class AnvilAssemblyController : MonoBehaviour
    {
        private AssemblyStage _stage = AssemblyStage.Positioning; private GameObject _bladeObj;
        private Vector3 _bladeTargetLocalPos, _bladeStartLocalPos, _handleBaseLocalPos;
        private Action _onFinishedCallback; private AnvilController _anvil; private AudioSource _audioSource; private AudioClip _strikeSound;
        private ParticleSystem _vfxPrefab; private GUIStyle _hudStyle, _cardStyle; private ForjaGame.Core.PlayerMovement _playerMovement;

        private int _rotationStep = 1;
        private float _offsetX = 0f, _offsetZ = 0f, _handleShiftX = 0f, _tangMidX, _tangCenterZ, _tangLength, _tangDiameter, _slideTimer = 0f;
        private const float SlideDuration = 0.65f;
        private WeaponStats _previewStats;

        public bool IsAssembling => _stage != AssemblyStage.Inspection || _bladeObj != null;
        public Quaternion CustomHeadRotation => Quaternion.Euler(0f, _rotationStep * 90f, 0f);
        public Vector3 CustomHeadOffset => new Vector3(_offsetX, 0f, _offsetZ); public bool IsSharpParallel => true;

        private bool GetSliceZBounds(float ox, out float sMinZ, out float sMaxZ, out float minX, out float maxX, out int vCount)
        {
            sMinZ = float.MaxValue; sMaxZ = float.MinValue; minX = float.MaxValue; maxX = float.MinValue; vCount = 0;
            if (_anvil?.ForgedBladeMesh == null) return false;
            Vector3[] verts = _anvil.ForgedBladeMesh.vertices;
            if (verts == null || verts.Length == 0) return false;
            Quaternion rot = CustomHeadRotation;
            for (int i = 0; i < verts.Length; i++) { Vector3 p = rot * verts[i]; if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x; }
            float halfX = _tangLength * 0.5f, hX0 = -halfX - ox, hX1 = halfX - ox, oX0 = Mathf.Max(hX0, minX), oX1 = Mathf.Min(hX1, maxX);
            if (oX1 <= oX0 + 0.025f) return false;
            for (int i = 0; i < verts.Length; i++) { Vector3 p = rot * verts[i]; if (p.x >= oX0 - 0.015f && p.x <= oX1 + 0.015f) { if (p.z < sMinZ) sMinZ = p.z; if (p.z > sMaxZ) sMaxZ = p.z; vCount++; } }
            return vCount >= 4 && sMaxZ > sMinZ + 0.01f;
        }

        private bool GetSwordTangBounds(out float tMinZ, out float tMaxZ, out int count)
        {
            tMinZ = float.MaxValue; tMaxZ = float.MinValue; count = 0;
            if (_anvil?.ForgedBladeMesh == null) return false;
            Vector3[] verts = _anvil.ForgedBladeMesh.vertices;
            if (verts == null || verts.Length == 0) return false;
            float minX = float.MaxValue;
            for (int i = 0; i < verts.Length; i++) if (verts[i].x < minX) minX = verts[i].x;
            float tangLimitX = minX + 0.14f;
            for (int i = 0; i < verts.Length; i++)
                if (verts[i].x <= tangLimitX) { if (verts[i].z < tMinZ) tMinZ = verts[i].z; if (verts[i].z > tMaxZ) tMaxZ = verts[i].z; count++; }
            return count >= 4 && tMaxZ > tMinZ + 0.01f;
        }

        public bool IsContactingEye
        {
            get
            {
                if (_anvil?.SelectedWeaponType == WeaponType.Sword)
                {
                    if (!GetSwordTangBounds(out float tMinZ, out float tMaxZ, out int count)) return false;
                    float halfZ = _tangDiameter * 0.5f, hZ0 = _tangCenterZ - halfZ, hZ1 = _tangCenterZ + halfZ;
                    float mZ0 = tMinZ + _offsetZ, mZ1 = tMaxZ + _offsetZ;
                    float overlapZ = Mathf.Min(mZ1, hZ1) - Mathf.Max(mZ0, hZ0);
                    return overlapZ >= 0.015f && Mathf.Abs(_offsetX) <= (_tangLength * 0.65f) + 0.08f && count >= 4;
                }
                if (!GetSliceZBounds(_offsetX, out float sMinZ, out float sMaxZ, out float minX, out float maxX, out int c))
                    return _offsetX >= -0.45f && _offsetX <= 0.25f && Mathf.Abs(_offsetZ) <= 0.12f;
                float halfZ_axe = _tangDiameter * 0.5f;
                return (sMaxZ >= (halfZ_axe - _offsetZ - 0.025f) && sMinZ <= (-halfZ_axe - _offsetZ + 0.025f)) || (c >= 4 && sMaxZ - sMinZ >= _tangDiameter * 0.50f);
            }
        }

        public void BeginAssembly(
            AnvilController anvil, HandleIntegrityResult val, Mesh handleMesh,
            AudioSource audioSource, AudioClip strikeSound, ParticleSystem vfxPrefab, Action onFinished)
        {
            _anvil = anvil; _audioSource = audioSource; _strikeSound = strikeSound; _vfxPrefab = vfxPrefab;
            _onFinishedCallback = onFinished; _stage = AssemblyStage.Positioning;
            _rotationStep = _anvil?.SelectedWeaponType == WeaponType.Sword ? 0 : 1;
            _offsetX = 0f; _offsetZ = 0f; _slideTimer = 0f;
            if (_anvil?.IngotTransform != null) _handleBaseLocalPos = _anvil.IngotTransform.localPosition;

            float lowestHandleX = float.MaxValue;
            if (handleMesh != null && handleMesh.vertices != null) foreach (var v in handleMesh.vertices) if (v.x < lowestHandleX) lowestHandleX = v.x;
            if (lowestHandleX == float.MaxValue) lowestHandleX = val.gripX0;
            Camera cam = Camera.main; Plane plane = new Plane(_anvil.transform.up, _anvil.transform.position);
            Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.12f, 0.5f, 0f)) : default;
            float targetShift = (cam != null && plane.Raycast(ray, out float ent)) ? _anvil.transform.InverseTransformPoint(ray.GetPoint(ent)).x - lowestHandleX - _handleBaseLocalPos.x : -0.35f;
            _handleShiftX = Mathf.Clamp(targetShift, -0.45f, -0.22f);

            var station = _anvil != null ? (_anvil.GetComponentInParent<AnvilStationController>() ?? FindFirstObjectByType<AnvilStationController>()) : null;
            GameObject interactor = station != null ? station.CurrentInteractor : null;
            _playerMovement = interactor != null ? interactor.GetComponentInParent<ForjaGame.Core.PlayerMovement>() : FindFirstObjectByType<ForjaGame.Core.PlayerMovement>();
            _playerMovement?.SetMovementEnabled(false);

            _tangMidX = (val.tangX0 + val.tangX1) * 0.5f; _tangCenterZ = val.tangCenterZ;
            _tangLength = val.tangLength > 0.05f ? val.tangLength : 0.18f; _tangDiameter = val.tangDiameter > 0.02f ? val.tangDiameter : 0.06f;
            if (handleMesh != null)
            {
                float mZ = float.MaxValue, xZ = float.MinValue;
                foreach (var v in handleMesh.vertices) if (v.x >= val.tangX0 - 0.01f && v.x <= val.tangX1 + 0.01f) { if (v.z < mZ) mZ = v.z; if (v.z > xZ) xZ = v.z; }
                if (xZ > mZ + 0.01f) { _tangDiameter = xZ - mZ; _tangCenterZ = (mZ + xZ) * 0.5f; }
            }
            UpdatePositions();

            _bladeObj = new GameObject("AssemblingBladeHead");
            _bladeObj.transform.SetParent(_anvil.IngotTransform, false);
            _bladeObj.transform.localPosition = _bladeTargetLocalPos; _bladeObj.transform.localRotation = CustomHeadRotation;
            _bladeObj.AddComponent<MeshFilter>().sharedMesh = _anvil.ForgedBladeMesh;
            var mr = _bladeObj.AddComponent<MeshRenderer>();
            mr.material = new Material(_anvil.ForgedBladeMaterial != null ? _anvil.ForgedBladeMaterial : _anvil.IngotRenderer.material);
            Color bladeCol = _anvil.CurrentMetalBaseColor;
            if (bladeCol != default && bladeCol.a > 0.1f)
            {
                Color qCol = Color.Lerp(bladeCol, new Color(0.20f, 0.18f, 0.18f), 0.18f); mr.material.color = qCol;
                if (mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", qCol);
                if (mr.material.HasProperty("_Metallic")) mr.material.SetFloat("_Metallic", 0.88f);
                if (mr.material.HasProperty("_Smoothness")) mr.material.SetFloat("_Smoothness", 0.68f);
            }
            CalculatePreviewStats(val, handleMesh);
        }

        private void CalculatePreviewStats(HandleIntegrityResult val, Mesh handleMesh)
        {
            var assembly = _anvil != null ? (_anvil.GetComponentInParent<AnvilStationController>()?.Assembly ?? FindFirstObjectByType<WeaponAssemblyController>()) : null;
            if (assembly == null || _anvil?.CurrentIngot == null) return;
            float lowestHandleX = float.MaxValue;
            if (handleMesh != null && handleMesh.vertices != null) foreach (var v in handleMesh.vertices) if (v.x < lowestHandleX) lowestHandleX = v.x;
            if (lowestHandleX == float.MaxValue) lowestHandleX = val.gripX0;
            float maxMetalX = val.tangX1;
            if (_anvil.ForgedBladeMesh != null)
                foreach (var v in _anvil.ForgedBladeMesh.vertices) { Vector3 p = CustomHeadRotation * v + _bladeTargetLocalPos; if (p.x > maxMetalX) maxMetalX = p.x; }
            float totalLen = Mathf.Max(0.35f, maxMetalX - lowestHandleX), totalCm = Mathf.Round(totalLen * 100f);
            HandleLength hLen = totalLen < 0.90f ? HandleLength.Short : (totalLen <= 1.40f ? HandleLength.Medium : HandleLength.Long);
            float baseRng = _anvil.SelectedWeaponType switch { WeaponType.Sword => 1.25f, WeaponType.Axe => 1.10f, WeaponType.Hammer => 1.35f, _ => 1.10f };
            float rangeMod = Mathf.Max(0.40f, totalLen / baseRng), speedMod = Mathf.Clamp((1.10f - totalLen) * 0.35f, -0.30f, +0.30f);
            bool isRefined = val.gripDiameter >= 0.025f && val.gripDiameter <= 0.065f;
            if (isRefined) speedMod += 0.08f; else if (val.gripDiameter > 0.075f) speedMod -= 0.08f;
            string shapeTag = isRefined ? "Refinado" : (val.gripDiameter > 0.075f ? "en Bruto" : "Esbelto");
            string partName = _anvil.SelectedWeaponType == WeaponType.Sword ? "Espada" : (_anvil.SelectedWeaponType == WeaponType.Hammer ? "Martillo" : "Hacha");
            var handle = new HandleData { length = hLen, handleName = $"{partName} Artesanal {shapeTag} ({totalCm:F0} cm)", rangeModifier = rangeMod, speedModifier = speedMod };
            _previewStats = assembly.AssembleWeapon(_anvil.CurrentIngot, _anvil.SelectedWeaponType, handle, _anvil.ForgedBladeMesh, _anvil.RemainingMassRatio);
        }

        private Vector3 CurrentHandleLocalPos => _handleBaseLocalPos + new Vector3(_handleShiftX, 0f, 0f);

        private void UpdatePositions()
        {
            Vector3 target = new Vector3(_tangMidX + _offsetX, 0f, _tangCenterZ + _offsetZ);
            if (_anvil?.SelectedWeaponType == WeaponType.Sword && _anvil?.ForgedBladeMesh != null)
                target -= (CustomHeadRotation * new Vector3(_anvil.ForgedBladeMesh.bounds.min.x + 0.08f, 0f, 0f));
            _bladeTargetLocalPos = target;
        }

        private void Update()
        {
            if (_bladeObj == null) return;
            _playerMovement?.SetMovementEnabled(false);

            if (_stage == AssemblyStage.Positioning)
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame) _rotationStep = (_rotationStep + 1) % 4;
                    if (Keyboard.current.qKey.wasPressedThisFrame) _rotationStep = (_rotationStep + 3) % 4;
                    float speed = 0.22f * Time.deltaTime;
                    if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) _offsetX -= speed;
                    if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) _offsetX += speed;
                    if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) _offsetZ += speed;
                    if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) _offsetZ -= speed;
                    float maxTangOffset = Mathf.Max(0.08f, _tangLength * 0.65f);
                    _offsetX = Mathf.Clamp(_offsetX, -maxTangOffset, maxTangOffset); _offsetZ = Mathf.Clamp(_offsetZ, -0.15f, 0.15f);
                }

                UpdatePositions();
                _bladeObj.transform.localRotation = CustomHeadRotation;
                _bladeObj.transform.localPosition = _bladeTargetLocalPos;
                if (_anvil?.IngotTransform != null) _anvil.IngotTransform.localPosition = CurrentHandleLocalPos;

                bool canConfirm = IsContactingEye && IsSharpParallel;
                bool trg = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame);
                if (trg && canConfirm) { _stage = AssemblyStage.Sliding; _slideTimer = 0f; _bladeStartLocalPos = new Vector3(_bladeTargetLocalPos.x + 1.25f, 0f, _bladeTargetLocalPos.z); _bladeObj.transform.localPosition = _bladeStartLocalPos; }
            }
            else if (_stage == AssemblyStage.Sliding)
            {
                _slideTimer += Time.deltaTime; float t = Mathf.Clamp01(_slideTimer / SlideDuration);
                _bladeObj.transform.localPosition = Vector3.Lerp(_bladeStartLocalPos, _bladeTargetLocalPos, Mathf.SmoothStep(0f, 1f, t));
                if (_anvil?.IngotTransform != null) _anvil.IngotTransform.localPosition = CurrentHandleLocalPos;
                if (t >= 1f) {
                    _stage = AssemblyStage.Inspection; _bladeObj.transform.localPosition = _bladeTargetLocalPos;
                    if (_strikeSound != null && _audioSource != null) _audioSource.PlayOneShot(_strikeSound);
                    if (_vfxPrefab != null) { var fx = Instantiate(_vfxPrefab, _bladeObj.transform.position, Quaternion.identity); Destroy(fx.gameObject, 1.5f); }
                }
            }
            else if (_stage == AssemblyStage.Inspection)
            {
                _bladeObj.transform.localPosition = _bladeTargetLocalPos;
                if (_anvil?.IngotTransform != null) _anvil.IngotTransform.localPosition = CurrentHandleLocalPos;
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) FinishAssembly();
            }
        }

        private void FinishAssembly() { if (_anvil?.IngotTransform != null) _anvil.IngotTransform.localPosition = _handleBaseLocalPos; if (_bladeObj != null) Destroy(_bladeObj); _bladeObj = null; _playerMovement?.SetMovementEnabled(true); _onFinishedCallback?.Invoke(); Destroy(this); }

        private void OnGUI()
        {
            if (_bladeObj == null) return;
            EnsureStyles();

            if (_stage == AssemblyStage.Positioning)
            {
                float w = 720f, h = 90f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 25f;
                string wTitle = _anvil?.SelectedWeaponType == WeaponType.Hammer ? "MARTILLO" : (_anvil?.SelectedWeaponType == WeaponType.Sword ? "ESPADA" : "HACHA");
                string status = !IsContactingEye
                    ? (_anvil?.SelectedWeaponType == WeaponType.Sword ? "<color=#FF5533><b>ESPIGA FUERA DE RANGO:</b> La espiga debe entrar en el mango sin sobresalir al aire.</color>" : "<color=#FF5533><b>ALINEACIÓN FUERA DE RANGO:</b> El mango debe estar contenido dentro del lingote en todos sus bordes.</color>")
                    : "<color=#33FFAA><b>¡ENCASTRE ALINEADO!</b> Encastre correcto sin holguras. Haz [Click Izquierdo] para deslizar la pieza.</color>";
                GUI.Box(new Rect(x, y, w, h), $"<b>ENSAMBLAJE DE {wTitle}:</b> [Q]/[E]/[R]: Rotar ({_rotationStep * 90}°) • [A]/[D]/[W]/[S]: Ajustar en Área del Lingote ({_tangLength * 100f:F0}cm)\n{status}", _hudStyle);
            }
            else if (_stage == AssemblyStage.Sliding)
            {
                string part = _anvil?.SelectedWeaponType == WeaponType.Sword ? "HOJA DE LA ESPADA" : $"CABEZA DEL {(_anvil?.SelectedWeaponType == WeaponType.Hammer ? "MARTILLO" : "HACHA")}";
                GUI.Box(new Rect((Screen.width - 540f) * 0.5f, Screen.height - 78f, 540f, 48f), $"<b>DESLIZANDO {part} POR EL MANGO...</b>", _hudStyle);
            }
            else if (_stage == AssemblyStage.Inspection)
            {
                float w = 780f, h = 155f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 25f;
                string def = _anvil?.SelectedWeaponType == WeaponType.Hammer ? "Martillo Artesanal" : (_anvil?.SelectedWeaponType == WeaponType.Sword ? "Espada Artesanal" : "Hacha Artesanal");
                string nm = _previewStats?.weaponName ?? def, q = _previewStats?.qualityPrefix ?? "Standard";
                float d = _previewStats?.baseDamage ?? 30f, s = _previewStats?.attackSpeed ?? 1.0f, sh = _previewStats?.maxSharpness ?? 80f, r = _previewStats?.attackRange ?? 1.2f;

                var style = _previewStats != null ? _previewStats.GetAnimationStyle() : WeaponAnimationStyle.Axe1H;
                string comboDesc = style switch
                {
                    WeaponAnimationStyle.Axe1H => "Hacha 1H (Combo 5 Golpes: Tajos Laterales + Hachazos Verticales)",
                    WeaponAnimationStyle.Hammer1H => "Martillo 1H (Combo 5 Golpes: Aplastamientos + Impactos Horizontales)",
                    WeaponAnimationStyle.Sword1H => "Espada 1H (Combo 5 Golpes: Tajos Cruzados + Estocadas)",
                    WeaponAnimationStyle.Dagger1H => "Daga 1H (Combo 5 Golpes: Estocadas Rápidas y Cortes)",
                    WeaponAnimationStyle.Heavy2H => "Arma Pesada 2 Manos (Combo 5 Golpes Pesados de Gran Inercia)",
                    WeaponAnimationStyle.Polearm => "Arma de Asta (Combo 5 Golpes: Barridos Largos + Estocadas)",
                    _ => "Combo 5 Golpes"
                };

                string card = $"<b>★ ARMA ARTESANAL ENSAMBLADA CON ÉXITO ★</b>\n<color=#FFD700><b>{nm}</b></color> [{q}]\n" +
                              $"Daño: <b>{d:F1} DMG</b>  •  Velocidad: <b>{s:F2}x</b>  •  Filo: <b>{sh:F0}</b>  •  Alcance: <b>{r:F2}m</b>\n" +
                              $"Estilo de Combate: <color=#FFAA33><b>{comboDesc}</b></color>\n" +
                              $"<color=#33FFAA><b>Presiona [ESPACIO] para guardarla en tu inventario</b></color>";
                GUI.Box(new Rect(x, y, w, h), card, _cardStyle);
                if (GUI.Button(new Rect((Screen.width - 240f) * 0.5f, y + h - 30f, 240f, 26f), "Tomar Arma [Espacio]")) FinishAssembly();
            }
        }

        private void EnsureStyles() {
            if (_hudStyle != null) return;
            _hudStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _cardStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperCenter, normal = { textColor = Color.white } };
        }
        private void OnDestroy() { if (_anvil?.IngotTransform != null) _anvil.IngotTransform.localPosition = _handleBaseLocalPos; _playerMovement?.SetMovementEnabled(true); if (_bladeObj != null) Destroy(_bladeObj); }
    }
}
