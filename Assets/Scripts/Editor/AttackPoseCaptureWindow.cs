using UnityEditor;
using UnityEngine;
using ForjaGame.Combat;

namespace ForjaGame.EditorTools
{
    /// <summary>
    /// Ventana de Editor para captura interactiva de poses de ataque en Play Mode.
    /// Accesible desde el menú Tools/Forja/Captura de Poses de Ataque.
    /// Requiere que el juego esté en Play Mode y que exista un ProceduralCombatController en la escena.
    /// </summary>
    public class AttackPoseCaptureWindow : EditorWindow
    {
        private ProceduralCombatController _controller;
        private AttackDefinitionSO _targetAttack;
        private AttackSubPhase _selectedPhase = AttackSubPhase.Windup;
        private bool _previewMode = false;
        private AttackPhase _previewPhase;
        private Vector2 _scrollPos;

        [MenuItem("Tools/Forja/Captura de Poses de Ataque (Play Mode)")]
        [MenuItem("Forja/Captura de Poses de Ataque (Play Mode)")]
        public static void ShowWindow()
        {
            var window = GetWindow<AttackPoseCaptureWindow>("Poses de Ataque");
            window.minSize = new Vector2(320f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            DrawControllerSection();

            if (_controller == null)
            {
                EditorGUILayout.HelpBox("No se encontró un ProceduralCombatController en la escena activa.\nIniciar Play Mode y presionar 'Buscar en Escena'.", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("La captura de poses requiere que el juego esté en Play Mode.", MessageType.Info);
            }

            DrawAttackSelection();
            DrawPhaseSelector();
            DrawCurrentPoseInfo();
            DrawCaptureButton();
            DrawPreviewSection();
            DrawHandleHint();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.Space(6);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            EditorGUILayout.LabelField("★ Captura de Poses de Ataque", titleStyle);
            EditorGUILayout.LabelField("Mueve el WeaponPivot en la Scene View y guarda la pose.", EditorStyles.miniLabel);
            GUILayout.Space(4);
            DrawSeparator();
        }

        private void DrawControllerSection()
        {
            EditorGUILayout.LabelField("Controlador de Combate", EditorStyles.boldLabel);
            _controller = (ProceduralCombatController)EditorGUILayout.ObjectField(
                "ProceduralCombatController", _controller, typeof(ProceduralCombatController), true);

            if (GUILayout.Button("Buscar en Escena Activa"))
            {
                _controller = FindFirstObjectByType<ProceduralCombatController>();
                if (_controller == null)
                    Debug.LogWarning("[PoseCapture] No se encontró ProceduralCombatController en la escena.");
            }
            DrawSeparator();
        }

        private void DrawAttackSelection()
        {
            EditorGUILayout.LabelField("Ataque de Destino", EditorStyles.boldLabel);
            _targetAttack = (AttackDefinitionSO)EditorGUILayout.ObjectField(
                "Attack Definition SO", _targetAttack, typeof(AttackDefinitionSO), false);

            if (_controller != null && _controller.CurrentAttackDef != null)
            {
                if (GUILayout.Button($"Usar ataque activo: [{_controller.CurrentAttackDef.name}]"))
                {
                    _targetAttack = _controller.CurrentAttackDef;
                }
            }
            DrawSeparator();
        }

        private void DrawPhaseSelector()
        {
            EditorGUILayout.LabelField("Fase a Capturar", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (AttackSubPhase phase in System.Enum.GetValues(typeof(AttackSubPhase)))
            {
                bool selected = _selectedPhase == phase;
                GUIStyle style = selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (GUILayout.Toggle(selected, phase.ToString(), style))
                    _selectedPhase = phase;
            }
            EditorGUILayout.EndHorizontal();
            DrawSeparator();
        }

        private void DrawCurrentPoseInfo()
        {
            if (_controller == null || _controller.WeaponPivot == null) return;

            EditorGUILayout.LabelField("Pose Actual del WeaponPivot", EditorStyles.boldLabel);
            Transform pivot = _controller.WeaponPivot;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector3Field("Posición Local", pivot.localPosition);
            EditorGUILayout.Vector3Field("Rotación Local", pivot.localEulerAngles);
            EditorGUILayout.Vector3Field("Caderas Euler", _controller.CurrentHipsEuler);
            EditorGUILayout.Vector3Field("Pecho Euler", _controller.CurrentChestEuler);
            EditorGUI.EndDisabledGroup();
            DrawSeparator();
        }

        private void DrawCaptureButton()
        {
            EditorGUI.BeginDisabledGroup(!Application.isPlaying || _targetAttack == null || _controller == null);

            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f);

            if (GUILayout.Button($"★ CAPTURAR POSE EN: [{_selectedPhase}]", GUILayout.Height(36)))
            {
                _controller.CaptureCurrentPoseTo(_targetAttack, _selectedPhase);
                EditorUtility.SetDirty(_targetAttack);
                AssetDatabase.SaveAssets();
            }

            GUI.backgroundColor = prevColor;
            EditorGUI.EndDisabledGroup();

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Requiere Play Mode activo.", MessageType.Info);
            if (_targetAttack == null)
                EditorGUILayout.HelpBox("Selecciona un AttackDefinitionSO de destino.", MessageType.Warning);
            DrawSeparator();
        }

        private void DrawPreviewSection()
        {
            _previewMode = EditorGUILayout.Foldout(_previewMode, "Previsualizar Fase Existente");
            if (!_previewMode || _targetAttack == null || !Application.isPlaying || _controller == null) return;

            EditorGUILayout.HelpBox("Aplica la pose guardada al personaje para revisarla en la Scene View.", MessageType.None);
            if (GUILayout.Button($"Preview: {_selectedPhase} actual"))
            {
                AttackPhase phase = GetPhaseFromSO(_targetAttack, _selectedPhase);
                _controller.PreviewPose(phase);
            }
        }

        private void DrawHandleHint()
        {
            DrawSeparator();
            EditorGUILayout.HelpBox(
                "En Play Mode:\n" +
                "• Selecciona el WeaponPivot en la Hierarchy.\n" +
                "• Usa el gizmo de posición/rotación en la Scene View para moverlo.\n" +
                "• Cuando esté en la posición deseada, pulsa CAPTURAR POSE.",
                MessageType.Info);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!Application.isPlaying || _controller == null || _controller.WeaponPivot == null) return;

            Transform pivot = _controller.WeaponPivot;

            // Dibujar etiqueta sobre el pivot en la Scene View
            Handles.color = new Color(0.3f, 1.0f, 0.5f, 0.9f);
            Handles.Label(pivot.position + Vector3.up * 0.12f, $"WeaponPivot\n[{_selectedPhase}]");
            Handles.DrawWireDisc(pivot.position, Vector3.up, 0.06f);
            Handles.DrawWireDisc(pivot.position, Vector3.forward, 0.06f);
        }

        private AttackPhase GetPhaseFromSO(AttackDefinitionSO so, AttackSubPhase phase)
        {
            return phase switch
            {
                AttackSubPhase.InGuard => so.guardPhase,
                AttackSubPhase.Windup => so.windupPhase,
                AttackSubPhase.Strike => so.strikePhase,
                AttackSubPhase.Recovery => so.recoveryPhase,
                _ => so.guardPhase
            };
        }

        private void DrawSeparator()
        {
            GUILayout.Space(4);
            Rect rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.4f, 0.4f, 0.4f, 0.5f));
            GUILayout.Space(4);
        }
    }
}
