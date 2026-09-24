#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ForjaGame.Combat;

namespace ForjaGame.EditorTools
{
    /// <summary>
    /// Herramienta de autoría y validación para AttackMove en el Editor de Unity.
    /// Valida alcance máximo (tope 96%) y penetración de la cápsula del torso.
    /// Marca en verde waypoints válidos y en rojo los defectuosos.
    /// </summary>
    [CustomEditor(typeof(AttackMove))]
    public class AttackMoveValidator : Editor
    {
        private const float DefaultArmLength = 0.65f;
        private const float TorsoCapsuleRadius = 0.22f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            AttackMove move = (AttackMove)target;
            if (move == null) return;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Validación Biomecánica (Single Source of Truth)", EditorStyles.boldLabel);

            if (GUILayout.Button("Validar Waypoints y Alcance de Rig"))
            {
                ValidateMove(move);
            }
        }

        private void ValidateMove(AttackMove move)
        {
            if (move.waypoints == null || move.waypoints.Length == 0)
            {
                EditorUtility.DisplayDialog("Validador de Combate", "El movimiento no contiene waypoints.", "OK");
                return;
            }

            int errors = 0;
            int warnings = 0;
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < move.waypoints.Length; i++)
            {
                var wp = move.waypoints[i];
                float reachDist = wp.gripPosChestNorm.magnitude * DefaultArmLength;
                float reachRatio = reachDist / DefaultArmLength;

                // 1. Verificación de alcance
                if (reachRatio > ArmIK.MaxReachCap)
                {
                    errors++;
                    sb.AppendLine($"<color=red>• Waypoint [{i}] (t={wp.time:F2}):</color> Alcance excesivo ({reachRatio * 100:F0}% > 96%). Inalcanzable.");
                }
                else if (reachRatio > ArmIK.ReachAssistThreshold)
                {
                    warnings++;
                    sb.AppendLine($"<color=yellow>• Waypoint [{i}] (t={wp.time:F2}):</color> Requiere asistencia biomecánica ({reachRatio * 100:F0}% > 92%).");
                }

                // 2. Verificación de penetración de cápsula del torso
                Vector2 horizGrip = new Vector2(wp.gripPosChestNorm.x, wp.gripPosChestNorm.z) * DefaultArmLength;
                if (horizGrip.magnitude < TorsoCapsuleRadius && wp.gripPosChestNorm.y > -0.3f && wp.gripPosChestNorm.y < 0.3f)
                {
                    errors++;
                    sb.AppendLine($"<color=red>• Waypoint [{i}] (t={wp.time:F2}):</color> Penetra la cápsula del torso (distancia {horizGrip.magnitude:F2}m < {TorsoCapsuleRadius:F2}m).");
                }
            }

            if (errors == 0 && warnings == 0)
            {
                EditorUtility.DisplayDialog("Validador de Combate", $"¡Validación exitosa!\nLos {move.waypoints.Length} waypoints son anatómicamente correctos y alcanzables (en verde).", "OK");
            }
            else
            {
                Debug.LogWarning($"<b>[Validador AttackMove: {move.name}]</b>\n{sb}");
                EditorUtility.DisplayDialog("Resultado de Validación", $"Errores: {errors} (en rojo)\nAvisos: {warnings} (en amarillo)\nRevisa la consola para más detalles.", "OK");
            }
        }

        private void OnSceneGUI()
        {
            AttackMove move = (AttackMove)target;
            if (move == null || move.waypoints == null || move.waypoints.Length < 2) return;

            Transform chest = null;
            var ik = Object.FindFirstObjectByType<PlayerCombatIKDriver>();
            if (ik != null && ik.ChestBone != null) chest = ik.ChestBone;

            Vector3 origin = chest != null ? chest.position : Vector3.zero;
            Quaternion rot = chest != null ? chest.rotation : Quaternion.identity;

            for (int i = 0; i < move.waypoints.Length; i++)
            {
                var wp = move.waypoints[i];
                Vector3 worldPos = origin + (rot * (wp.gripPosChestNorm * DefaultArmLength));
                float reachRatio = wp.gripPosChestNorm.magnitude;

                bool isError = reachRatio > ArmIK.MaxReachCap;
                Handles.color = isError ? Color.red : Color.green;
                Handles.SphereHandleCap(0, worldPos, Quaternion.identity, 0.045f, EventType.Repaint);

                Handles.color = Color.cyan;
                Vector3 tipEnd = worldPos + (rot * (wp.tipDirChest * 0.25f));
                Handles.DrawLine(worldPos, tipEnd);
            }
        }
    }
}
#endif
