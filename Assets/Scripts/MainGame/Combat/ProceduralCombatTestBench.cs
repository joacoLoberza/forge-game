using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Banco de pruebas interactivo en tiempo de ejecución (Play mode) para el sistema de combate procedural.
    /// Bloque B (Fase 3):
    /// - Genera y equipa armas instantáneamente mediante 9 presets o sliders libres.
    /// - Ejecución directa de ataques y combos (Golpe 1, 2, 3) con monitor de fases (Windup/Strike/Recovery).
    /// - Herramienta de captura de poses en vivo directamente en ScriptableObjects de ataque.
    /// - Trazado de trayectorias (arcos de swing de la hoja y empuñadura) en Gizmos.
    /// - Monitoreo en tiempo real de la cadena cinética (torsión de caderas y torso).
    /// </summary>
    public class ProceduralCombatTestBench : MonoBehaviour
    {
        [Header("Atajos de Teclado")]
        [SerializeField] private Key togglePanelKey = Key.F1;
        [SerializeField] private Key slowMotionKey = Key.F2;
        [SerializeField] private Key toggleGizmosKey = Key.F3;

        [Header("Referencias")]
        [SerializeField] private PlayerEquipmentController equipmentController;
        [SerializeField] private ProceduralCombatController proceduralCombat;

        // Estado del Panel y Gizmos
        private bool _isPanelVisible = true;
        private bool _isSlowMotionActive = false;
        private bool _drawGizmos = true;
        private Vector2 _scrollPos = Vector2.zero;

        // Sliders de armas
        private float _sliderHandleLength = 0.35f;
        private float _sliderHeadLength = 0.85f;
        private float _sliderHeadWidth = 0.06f;
        private float _sliderHeadThickness = 0.015f;
        private float _sliderSharpness = 0.90f;
        private float _sliderLateralExtent = 0.02f;
        private float _sliderMass = 1.6f;
        private float _sliderGuardDistanceOffset = 0f;

        // Ataques, Trayectoria y Captura de Poses (Fase 3)
        private AttackSubPhase _selectedCapturePhase = AttackSubPhase.Windup;
        private bool _showPoseCapture = false;
        private readonly List<Vector3> _tipTrail = new();
        private readonly List<Vector3> _handTrail = new();
        private float _timeSinceLastAttack = 0f;

        // Estilos GUI
        private GUIStyle _panelStyle;
        private GUIStyle _hudBoxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _presetBtnStyle;

        private void Awake()
        {
            if (equipmentController == null) equipmentController = GetComponent<PlayerEquipmentController>() ?? FindFirstObjectByType<PlayerEquipmentController>();
            if (proceduralCombat == null) proceduralCombat = GetComponent<ProceduralCombatController>() ?? FindFirstObjectByType<ProceduralCombatController>();
        }

        private void Start()
        {
            // Equipar por defecto una espada de prueba para validar inmediatamente la guardia
            ApplyPreset(1);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Toggle Panel con F1
            if (kb[togglePanelKey].wasPressedThisFrame)
            {
                _isPanelVisible = !_isPanelVisible;
            }

            // Toggle Cámara Lenta con F2 o T
            if (kb[slowMotionKey].wasPressedThisFrame || kb.tKey.wasPressedThisFrame)
            {
                _isSlowMotionActive = !_isSlowMotionActive;
                Time.timeScale = _isSlowMotionActive ? 0.20f : 1.0f;
            }

            // Toggle Gizmos con F3 o G
            if (kb[toggleGizmosKey].wasPressedThisFrame)
            {
                _drawGizmos = !_drawGizmos;
            }

            // Registrar trayectoria de ataque para trazado de arco
            if (proceduralCombat != null && proceduralCombat.IsEquipped)
            {
                if (proceduralCombat.CurrentSubPhase != AttackSubPhase.InGuard)
                {
                    _timeSinceLastAttack = 0f;
                    if (proceduralCombat.BladeTip != null)
                    {
                        _tipTrail.Add(proceduralCombat.BladeTip.position);
                        if (_tipTrail.Count > 80) _tipTrail.RemoveAt(0);
                    }
                    if (proceduralCombat.GripSocketR != null)
                    {
                        _handTrail.Add(proceduralCombat.GripSocketR.position);
                        if (_handTrail.Count > 80) _handTrail.RemoveAt(0);
                    }
                }
                else
                {
                    _timeSinceLastAttack += Time.deltaTime;
                    if (_timeSinceLastAttack > 1.2f)
                    {
                        _tipTrail.Clear();
                        _handTrail.Clear();
                    }
                }
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 1. HUD de WeaponProfile siempre visible en la esquina superior izquierda
            DrawWeaponProfileHUD();

            // 2. Banner de Cámara Lenta si está activa
            if (_isSlowMotionActive)
            {
                GUI.Box(new Rect((Screen.width - 260f) * 0.5f, 15f, 260f, 32f), "<color=#FFCC00><b>CAMARA LENTA (0.20x) [F2/T]</b></color>", _headerStyle);
            }

            // 3. Panel de control desplegable
            if (!_isPanelVisible)
            {
                if (GUI.Button(new Rect(10f, 10f, 170f, 30f), "Banco Pruebas [F1]"))
                {
                    _isPanelVisible = true;
                }
                return;
            }

            float panelWidth = 470f;
            float panelHeight = Screen.height - 40f;
            Rect panelRect = new Rect(Screen.width - panelWidth - 15f, 20f, panelWidth, panelHeight);

            GUI.Box(panelRect, "", _panelStyle);
            GUILayout.BeginArea(new Rect(panelRect.x + 12f, panelRect.y + 12f, panelWidth - 24f, panelHeight - 24f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("BANCO DE PRUEBAS PROCEDURAL", _headerStyle);
            if (GUILayout.Button("X", GUILayout.Width(28), GUILayout.Height(24)))
            {
                _isPanelVisible = false;
            }
            GUILayout.EndHorizontal();

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            GUILayout.Label("<color=#CCCCCC>Fase 3: Ataques base, cadena cinética, exclusión y captura.</color>", _labelStyle);
            GUILayout.Space(6);

            // ============================================
            // SECCIÓN 1: ATAQUES Y COMBOS PROCEDURALES
            // ============================================
            GUILayout.Label("<b>1. Ejecución de Ataques y Combo (Fase 3):</b>", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Golpe 1 (Combo 0)", GUILayout.Height(28)))
            {
                if (proceduralCombat != null && proceduralCombat.IsEquipped) proceduralCombat.ExecuteAttack(0);
            }
            if (GUILayout.Button("Golpe 2 (Combo 1)", GUILayout.Height(28)))
            {
                if (proceduralCombat != null && proceduralCombat.IsEquipped) proceduralCombat.ExecuteAttack(1);
            }
            if (GUILayout.Button("Golpe 3 (Combo 2)", GUILayout.Height(28)))
            {
                if (proceduralCombat != null && proceduralCombat.IsEquipped) proceduralCombat.ExecuteAttack(2);
            }
            GUILayout.EndHorizontal();

            // Monitor en tiempo real de la fase activa
            if (proceduralCombat != null && proceduralCombat.IsEquipped)
            {
                string phaseColor = proceduralCombat.CurrentSubPhase switch
                {
                    AttackSubPhase.InGuard => "#33FFAA",
                    AttackSubPhase.Windup => "#FFCC00",
                    AttackSubPhase.Strike => "#FF3344",
                    AttackSubPhase.Recovery => "#33AAFF",
                    _ => "#FFFFFF"
                };

                string atkName = proceduralCombat.CurrentAttackDef != null ? proceduralCombat.CurrentAttackDef.attackName : "En Guardia";
                GUILayout.Label($"Ataque: <b>{atkName}</b>", _labelStyle);
                GUILayout.Label($"Fase: <color={phaseColor}><b>{proceduralCombat.CurrentSubPhase}</b></color> ({proceduralCombat.PhaseNormalizedTime * 100f:F0}%) | Inercia k: <b>{proceduralCombat.InertiaScale:F2}x</b>", _labelStyle);

                // Barra de progreso de la fase
                Rect barRect = GUILayoutUtility.GetRect(panelWidth - 60f, 10f);
                GUI.Box(barRect, "");
                float fillW = barRect.width * proceduralCombat.PhaseNormalizedTime;
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, fillW, barRect.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0.2f, 0.8f, 1.0f, 0.75f), 0, 0);

                // Monitor Cadena Cinética
                Vector3 hipsE = proceduralCombat.CurrentHipsEuler;
                Vector3 chestE = proceduralCombat.CurrentChestEuler;
                GUILayout.Label($"Cadena Cinética: Caderas: <b>{hipsE.y:F1}°</b> | Torso: Yaw <b>{chestE.y:F1}°</b>, Pitch <b>{chestE.x:F1}°</b>", _labelStyle);
            }

            GUILayout.Space(8);

            // ============================================
            // SECCIÓN 2: HERRAMIENTA DE CAPTURA DE POSES
            // ============================================
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>2. Captura de Poses (Autoría en Vivo):</b>", _labelStyle);
            if (GUILayout.Button(_showPoseCapture ? "Ocultar" : "Mostrar Editor", GUILayout.Width(110), GUILayout.Height(22)))
            {
                _showPoseCapture = !_showPoseCapture;
            }
            GUILayout.EndHorizontal();

            if (_showPoseCapture && proceduralCombat != null && proceduralCombat.IsEquipped)
            {
                GUILayout.Label("<color=#AAAAAA>Selecciona fase y guarda la pose actual en el Attack SO:</color>", _labelStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(_selectedCapturePhase == AttackSubPhase.InGuard, "Guard", "Button")) _selectedCapturePhase = AttackSubPhase.InGuard;
                if (GUILayout.Toggle(_selectedCapturePhase == AttackSubPhase.Windup, "Windup", "Button")) _selectedCapturePhase = AttackSubPhase.Windup;
                if (GUILayout.Toggle(_selectedCapturePhase == AttackSubPhase.Strike, "Strike", "Button")) _selectedCapturePhase = AttackSubPhase.Strike;
                if (GUILayout.Toggle(_selectedCapturePhase == AttackSubPhase.Recovery, "Recovery", "Button")) _selectedCapturePhase = AttackSubPhase.Recovery;
                GUILayout.EndHorizontal();

                if (proceduralCombat.WeaponPivot != null)
                {
                    Vector3 curPos = proceduralCombat.WeaponPivot.localPosition;
                    Vector3 curRot = proceduralCombat.WeaponPivot.localEulerAngles;
                    GUILayout.Label($"Pivot Pos: ({curPos.x:F2}, {curPos.y:F2}, {curPos.z:F2})", _labelStyle);
                    GUILayout.Label($"Pivot Rot: ({curRot.x:F1}°, {curRot.y:F1}°, {curRot.z:F1}°)", _labelStyle);
                }

                if (GUILayout.Button("★ GUARDAR POSE EN EL ATTACK SO ★", GUILayout.Height(30)))
                {
                    if (proceduralCombat.CurrentAttackDef != null)
                    {
                        proceduralCombat.CaptureCurrentPoseTo(proceduralCombat.CurrentAttackDef, _selectedCapturePhase);
                    }
                }
            }

            GUILayout.Space(8);

            // ============================================
            // SECCIÓN 3: PRESETS DE ARMAS
            // ============================================
            GUILayout.Label("<b>3. Presets de Armas (1 Click):</b>", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Daga", _presetBtnStyle)) ApplyPreset(0);
            if (GUILayout.Button("Espada", _presetBtnStyle)) ApplyPreset(1);
            if (GUILayout.Button("Mandoble", _presetBtnStyle)) ApplyPreset(2);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Guja / Asta", _presetBtnStyle)) ApplyPreset(3);
            if (GUILayout.Button("Hacha Mano", _presetBtnStyle)) ApplyPreset(4);
            if (GUILayout.Button("Alabarda", _presetBtnStyle)) ApplyPreset(5);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Martillo Guerra", _presetBtnStyle)) ApplyPreset(6);
            if (GUILayout.Button("Gran Mazo", _presetBtnStyle)) ApplyPreset(7);
            if (GUILayout.Button("Martillo Asta", _presetBtnStyle)) ApplyPreset(8);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // ============================================
            // SECCIÓN 4: SLIDERS GEOMÉTRICOS Y FÍSICOS
            // ============================================
            GUILayout.Label("<b>4. Sliders Geométricos y Físicos:</b>", _labelStyle);

            _sliderHandleLength = DrawSlider("Largo Mango", _sliderHandleLength, 0.15f, 1.85f, "m");
            _sliderHeadLength = DrawSlider("Largo Cabeza", _sliderHeadLength, 0.10f, 1.35f, "m");
            _sliderHeadWidth = DrawSlider("Ancho Cabeza", _sliderHeadWidth, 0.02f, 0.35f, "m");
            _sliderHeadThickness = DrawSlider("Espesor", _sliderHeadThickness, 0.008f, 0.18f, "m");
            _sliderSharpness = DrawSlider("Filo", _sliderSharpness, 0.0f, 1.0f, "");
            _sliderLateralExtent = DrawSlider("Extensión Lat.", _sliderLateralExtent, 0.0f, 0.45f, "m");
            _sliderMass = DrawSlider("Masa Total", _sliderMass, 0.4f, 10.0f, "kg");

            GUILayout.Space(6);
            if (GUILayout.Button("★ GENERAR Y EQUIPAR ARMA ★", GUILayout.Height(32)))
            {
                GenerateAndEquipCustomWeapon(
                    _sliderHandleLength, _sliderHeadLength, _sliderHeadWidth,
                    _sliderHeadThickness, _sliderSharpness, _sliderLateralExtent, _sliderMass);
            }

            GUILayout.Space(10);

            // ============================================
            // SECCIÓN 5: AJUSTES Y UTILIDADES
            // ============================================
            GUILayout.Label("<b>5. Ajuste de Postura / Distancia al Cuerpo:</b>", _labelStyle);
            float prevOffset = _sliderGuardDistanceOffset;
            _sliderGuardDistanceOffset = DrawSlider("Distancia Frontal", _sliderGuardDistanceOffset, -0.15f, 0.35f, "m");
            if (Mathf.Abs(prevOffset - _sliderGuardDistanceOffset) > 0.001f && proceduralCombat != null)
            {
                proceduralCombat.SetDynamicGuardOffset(new Vector3(0f, 0f, _sliderGuardDistanceOffset));
            }

            GUILayout.Space(8);
            GUILayout.Label("<b>6. Utilidades de Depuración:</b>", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_isSlowMotionActive ? "Normal (1.0x)" : "Cámara Lenta [F2]", GUILayout.Height(26)))
            {
                _isSlowMotionActive = !_isSlowMotionActive;
                Time.timeScale = _isSlowMotionActive ? 0.20f : 1.0f;
            }
            if (GUILayout.Button(_drawGizmos ? "Ocultar Gizmos" : "Ver Gizmos [F3]", GUILayout.Height(26)))
            {
                _drawGizmos = !_drawGizmos;
            }
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private float DrawSlider(string label, float val, float min, float max, string unit)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: <b>{val:F2}{unit}</b>", _labelStyle, GUILayout.Width(170));
            float res = GUILayout.HorizontalSlider(val, min, max);
            GUILayout.EndHorizontal();
            return res;
        }

        private void DrawWeaponProfileHUD()
        {
            if (proceduralCombat == null || !proceduralCombat.IsEquipped) return;

            WeaponProfile prof = proceduralCombat.CurrentProfile;
            float w = 360f;
            float h = 175f;
            Rect rect = new Rect(15f, 15f, w, h);

            GUI.Box(rect, "", _hudBoxStyle);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 10f, w - 24f, h - 20f));

            GUILayout.Label($"<b>{prof.generatedName.ToUpper()}</b>", _headerStyle);
            GUILayout.Label($"Familia: <b>{prof.family}</b>  |  Agarre: <b>{prof.grip}</b>  |  IK: <b>{proceduralCombat.IKWeight * 100f:F0}%</b>", _labelStyle);
            GUILayout.Label($"Masa: <b>{prof.mass:F1} kg</b>  |  Inercia (I): <b>{prof.inertia:F2} kg·m²</b>", _labelStyle);
            GUILayout.Label($"Longitud Total: <b>{prof.length:F2} m</b> (Mango: <b>{prof.handleLength:F2} m</b>)", _labelStyle);
            GUILayout.Label($"Centro de Masa: <b>{prof.gripToCom:F2} m</b>  |  Radio Cabeza: <b>{prof.headRadius:F2} m</b>", _labelStyle);

            // Estado de alcance de brazos
            float reachR = proceduralCombat.RightArmLength > 0.01f ? (proceduralCombat.CurrentDistR / proceduralCombat.RightArmLength) : 0f;
            float reachL = proceduralCombat.LeftArmLength > 0.01f ? (proceduralCombat.CurrentDistL / proceduralCombat.LeftArmLength) : 0f;

            string colR = reachR > 0.90f ? "#FF3333" : (reachR > 0.75f ? "#FFCC00" : "#33FFAA");
            string colL = reachL > 0.90f ? "#FF3333" : (reachL > 0.75f ? "#FFCC00" : "#33FFAA");

            string armInfo = $"Brazo Der: <color={colR}>{reachR * 100f:F0}%</color>";
            if (prof.grip == GripMode.TwoHand || prof.grip == GripMode.Pole)
            {
                armInfo += $"  •  Brazo Izq: <color={colL}>{reachL * 100f:F0}%</color>";
            }
            GUILayout.Label(armInfo, _labelStyle);

            GUILayout.EndArea();
        }

        public void ApplyPreset(int index)
        {
            switch (index)
            {
                case 0: // Daga
                    _sliderHandleLength = 0.18f; _sliderHeadLength = 0.24f; _sliderHeadWidth = 0.04f;
                    _sliderHeadThickness = 0.010f; _sliderSharpness = 0.95f; _sliderLateralExtent = 0.01f; _sliderMass = 0.65f;
                    break;
                case 1: // Espada
                    _sliderHandleLength = 0.35f; _sliderHeadLength = 0.85f; _sliderHeadWidth = 0.06f;
                    _sliderHeadThickness = 0.015f; _sliderSharpness = 0.90f; _sliderLateralExtent = 0.02f; _sliderMass = 1.55f;
                    break;
                case 2: // Mandoble
                    _sliderHandleLength = 0.65f; _sliderHeadLength = 1.15f; _sliderHeadWidth = 0.08f;
                    _sliderHeadThickness = 0.020f; _sliderSharpness = 0.85f; _sliderLateralExtent = 0.02f; _sliderMass = 3.40f;
                    break;
                case 3: // Guja / Naginata
                    _sliderHandleLength = 1.55f; _sliderHeadLength = 0.65f; _sliderHeadWidth = 0.07f;
                    _sliderHeadThickness = 0.018f; _sliderSharpness = 0.85f; _sliderLateralExtent = 0.02f; _sliderMass = 2.75f;
                    break;
                case 4: // Hacha de Mano
                    _sliderHandleLength = 0.38f; _sliderHeadLength = 0.24f; _sliderHeadWidth = 0.20f;
                    _sliderHeadThickness = 0.035f; _sliderSharpness = 0.80f; _sliderLateralExtent = 0.35f; _sliderMass = 1.40f;
                    break;
                case 5: // Alabarda
                    _sliderHandleLength = 1.65f; _sliderHeadLength = 0.38f; _sliderHeadWidth = 0.28f;
                    _sliderHeadThickness = 0.040f; _sliderSharpness = 0.80f; _sliderLateralExtent = 0.38f; _sliderMass = 3.90f;
                    break;
                case 6: // Martillo de Guerra
                    _sliderHandleLength = 0.55f; _sliderHeadLength = 0.22f; _sliderHeadWidth = 0.18f;
                    _sliderHeadThickness = 0.150f; _sliderSharpness = 0.05f; _sliderLateralExtent = 0.05f; _sliderMass = 2.90f;
                    break;
                case 7: // Gran Mazo
                    _sliderHandleLength = 0.75f; _sliderHeadLength = 0.35f; _sliderHeadWidth = 0.24f;
                    _sliderHeadThickness = 0.220f; _sliderSharpness = 0.05f; _sliderLateralExtent = 0.05f; _sliderMass = 6.80f;
                    break;
                case 8: // Martillo de Asta
                    _sliderHandleLength = 1.70f; _sliderHeadLength = 0.26f; _sliderHeadWidth = 0.20f;
                    _sliderHeadThickness = 0.160f; _sliderSharpness = 0.10f; _sliderLateralExtent = 0.08f; _sliderMass = 4.60f;
                    break;
            }

            GenerateAndEquipCustomWeapon(
                _sliderHandleLength, _sliderHeadLength, _sliderHeadWidth,
                _sliderHeadThickness, _sliderSharpness, _sliderLateralExtent, _sliderMass);
        }

        private void GenerateAndEquipCustomWeapon(
            float handleL, float headL, float headW, float headT, float sharpness, float latExtent, float mass)
        {
            if (equipmentController == null) equipmentController = GetComponent<PlayerEquipmentController>() ?? FindFirstObjectByType<PlayerEquipmentController>();
            if (equipmentController == null) return;

            ProceduralCombatConfigSO cfg = proceduralCombat != null ? proceduralCombat.Config : null;
            WeaponProfile prof = WeaponProfile.Calculate(handleL, headL, headW, headT, sharpness, latExtent, mass, cfg);

            WeaponType wType = prof.family switch
            {
                WeaponFamily.Blade => WeaponType.Sword,
                WeaponFamily.Axe => WeaponType.Axe,
                _ => WeaponType.Hammer
            };

            HandleLength hEnum = prof.handleLength < 0.45f ? HandleLength.Short : (prof.handleLength < 1.10f ? HandleLength.Medium : HandleLength.Long);
            ItemSize iSize = prof.mass < 2.0f ? ItemSize.Small : (prof.mass < 4.5f ? ItemSize.Medium : ItemSize.Large);

            // Generar mallas procedurales para visualización exacta
            Mesh handleMesh = CreateCylinderMesh(0.024f, prof.handleLength);
            Mesh headMesh = CreateBoxMesh(prof.headLength, prof.headWidth, prof.headThickness, prof.lateralExtent);

            WeaponStats stats = new WeaponStats
            {
                weaponName = prof.generatedName,
                weaponType = wType,
                ingotSize = iSize,
                handleLength = hEnum,
                customHandleMesh = handleMesh,
                customMesh = headMesh,
                bladeMountPosition = new Vector3(prof.handleLength, 0f, 0f),
                gripCenterOffset = new Vector3(0.05f, 0f, 0f),
                baseDamage = Mathf.RoundToInt(prof.mass * 8.5f),
                attackSpeed = 1.0f,
                attackRange = prof.length,
                maxSharpness = prof.headSharpness * 100f,
                currentSharpness = prof.headSharpness * 100f,
                quality = 0.85f,
                qualityPrefix = "TestBench"
            };

            stats.SetProfile(prof);
            equipmentController.EquipWeapon(stats);
        }

        private Mesh CreateCylinderMesh(float radius, float length)
        {
            Mesh m = new Mesh { name = "ProceduralHandleMesh" };
            int segments = 8;
            Vector3[] verts = new Vector3[segments * 2 + 2];
            int[] tris = new int[segments * 6 + segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float y = Mathf.Sin(angle) * radius;
                float z = Mathf.Cos(angle) * radius;
                verts[i] = new Vector3(0f, y, z);
                verts[i + segments] = new Vector3(length, y, z);
            }
            verts[segments * 2] = Vector3.zero;
            verts[segments * 2 + 1] = new Vector3(length, 0f, 0f);

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris[t++] = i; tris[t++] = next; tris[t++] = i + segments;
                tris[t++] = next; tris[t++] = next + segments; tris[t++] = i + segments;
            }

            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private Mesh CreateBoxMesh(float l, float w, float h, float lateral)
        {
            Mesh m = new Mesh { name = "ProceduralHeadMesh" };
            Vector3 size = new Vector3(l, h, w);
            Vector3 center = new Vector3(l * 0.5f, 0f, lateral);

            Vector3 p0 = center + new Vector3(-size.x, -size.y, -size.z) * 0.5f;
            Vector3 p1 = center + new Vector3(size.x, -size.y, -size.z) * 0.5f;
            Vector3 p2 = center + new Vector3(size.x, -size.y, size.z) * 0.5f;
            Vector3 p3 = center + new Vector3(-size.x, -size.y, size.z) * 0.5f;

            Vector3 p4 = center + new Vector3(-size.x, size.y, -size.z) * 0.5f;
            Vector3 p5 = center + new Vector3(size.x, size.y, -size.z) * 0.5f;
            Vector3 p6 = center + new Vector3(size.x, size.y, size.z) * 0.5f;
            Vector3 p7 = center + new Vector3(-size.x, size.y, size.z) * 0.5f;

            Vector3[] verts = {
                p0, p1, p2, p3, // abajo
                p7, p6, p5, p4, // arriba
                p0, p4, p5, p1, // frente
                p2, p6, p7, p3, // atras
                p0, p3, p7, p4, // izq
                p1, p5, p6, p2  // der
            };

            int[] tris = new int[36];
            for (int f = 0; f < 6; f++)
            {
                int v = f * 4;
                tris[f * 6 + 0] = v + 0; tris[f * 6 + 1] = v + 1; tris[f * 6 + 2] = v + 2;
                tris[f * 6 + 3] = v + 0; tris[f * 6 + 4] = v + 2; tris[f * 6 + 5] = v + 3;
            }

            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos || proceduralCombat == null || !proceduralCombat.IsEquipped) return;

            // 1. Sockets de manos
            if (proceduralCombat.GripSocketR != null)
            {
                Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.9f);
                Gizmos.DrawWireSphere(proceduralCombat.GripSocketR.position, 0.055f);
            }
            if (proceduralCombat.GripSocketL != null)
            {
                Gizmos.color = new Color(0.9f, 0.5f, 1.0f, 0.9f);
                Gizmos.DrawWireSphere(proceduralCombat.GripSocketL.position, 0.055f);
            }

            // 2. Líneas Hombro -> Mano con color según cercanía al límite anatómico
            DrawArmReachGizmo(proceduralCombat.RightShoulder, proceduralCombat.GripSocketR, proceduralCombat.RightArmLength);
            if (proceduralCombat.CurrentProfile.grip == GripMode.TwoHand || proceduralCombat.CurrentProfile.grip == GripMode.Pole)
            {
                DrawArmReachGizmo(proceduralCombat.LeftShoulder, proceduralCombat.GripSocketL, proceduralCombat.LeftArmLength);
            }

            // 3. Cápsulas de exclusión corporal (torso, cabeza, piernas)
            DrawExclusionCapsules();

            // 4. Trazado del arco de swing de la punta del arma (Cyan)
            if (_tipTrail.Count > 1)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 1.0f, 0.90f);
                for (int i = 0; i < _tipTrail.Count - 1; i++)
                {
                    Gizmos.DrawLine(_tipTrail[i], _tipTrail[i + 1]);
                }
            }

            // 5. Trazado del arco de swing de la mano (Amarillo)
            if (_handTrail.Count > 1)
            {
                Gizmos.color = new Color(1.0f, 0.85f, 0.2f, 0.75f);
                for (int i = 0; i < _handTrail.Count - 1; i++)
                {
                    Gizmos.DrawLine(_handTrail[i], _handTrail[i + 1]);
                }
            }
        }

        private void DrawArmReachGizmo(Transform shoulder, Transform socket, float armLength)
        {
            if (shoulder == null || socket == null || armLength < 0.01f) return;

            float dist = Vector3.Distance(shoulder.position, socket.position);
            float ratio = dist / armLength;

            Color c = ratio > 0.90f ? Color.red : (ratio > 0.75f ? Color.yellow : Color.green);
            Gizmos.color = c;
            Gizmos.DrawLine(shoulder.position, socket.position);
            Gizmos.DrawWireSphere(socket.position, 0.025f);
        }

        private void DrawExclusionCapsules()
        {
            if (proceduralCombat.ChestBone == null) return;
            Transform chest = proceduralCombat.ChestBone;
            Transform hips = proceduralCombat.HipsBone != null ? proceduralCombat.HipsBone : chest;
            var cfg = proceduralCombat.Config;
            float torsoR = cfg != null ? cfg.torsoRadius : 0.24f;
            float headR = cfg != null ? cfg.headRadius : 0.16f;
            float legsR = cfg != null ? cfg.legsRadius : 0.22f;

            // Torso
            Gizmos.color = new Color(1.0f, 0.6f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(chest.position, torsoR);

            // Cabeza
            Gizmos.color = new Color(1.0f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(chest.position + chest.up * 0.40f, headR);

            // Piernas
            Gizmos.color = new Color(0.3f, 0.8f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(hips.position + Vector3.down * 0.35f, legsR);
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null) return;

            _panelStyle = new GUIStyle(GUI.skin.box);
            Texture2D darkBg = new Texture2D(1, 1);
            darkBg.SetPixel(0, 0, new Color(0.10f, 0.11f, 0.14f, 0.94f));
            darkBg.Apply();
            _panelStyle.normal.background = darkBg;

            _hudBoxStyle = new GUIStyle(GUI.skin.box);
            Texture2D hudBg = new Texture2D(1, 1);
            hudBg.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.12f, 0.90f));
            hudBg.Apply();
            _hudBoxStyle.normal.background = hudBg;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _headerStyle.normal.textColor = new Color(0.3f, 1.0f, 0.7f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = true
            };
            _labelStyle.normal.textColor = Color.white;

            _presetBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 26
            };
        }
    }
}
