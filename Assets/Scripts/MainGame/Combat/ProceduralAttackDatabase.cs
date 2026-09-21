using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Fábrica y registro de los 9 ataques base iniciales del sistema procedural (3 por cada GripMode).
    /// Provee secuencias ordenadas de 3 golpes por combo según el GripMode y el sesgo de la familia del arma.
    /// </summary>
    public static class ProceduralAttackDatabase
    {
        private static readonly Dictionary<string, AttackDefinitionSO> _attackCache = new();

        public static AttackDefinitionSO GetAttack(string id)
        {
            if (_attackCache.TryGetValue(id, out var atk) && atk != null) return atk;
            atk = CreateAttackById(id);
            _attackCache[id] = atk;
            return atk;
        }

        /// <summary>
        /// Obtiene el combo ordenado de 3 ataques distintos según el GripMode y familia del arma.
        /// Nunca repite el mismo ataque 3 veces consecutivas.
        /// </summary>
        public static AttackDefinitionSO[] GetComboSequence(WeaponProfile profile)
        {
            AttackDefinitionSO[] combo = new AttackDefinitionSO[3];

            switch (profile.grip)
            {
                case GripMode.OneShort:
                case GripMode.OneHand:
                    if (profile.family == WeaponFamily.Blade)
                    {
                        combo[0] = GetAttack("1H_HorizontalSlash");
                        combo[1] = GetAttack("1H_DiagonalChop");
                        combo[2] = GetAttack("1H_Thrust");
                    }
                    else if (profile.family == WeaponFamily.Axe)
                    {
                        combo[0] = GetAttack("1H_DiagonalChop");
                        combo[1] = GetAttack("1H_HorizontalSlash");
                        combo[2] = GetAttack("1H_OverheadChop");
                    }
                    else // Blunt
                    {
                        combo[0] = GetAttack("1H_OverheadChop");
                        combo[1] = GetAttack("1H_DiagonalChop");
                        combo[2] = GetAttack("1H_HorizontalSlash");
                    }
                    break;

                case GripMode.TwoHand:
                    if (profile.family == WeaponFamily.Blade)
                    {
                        combo[0] = GetAttack("2H_WideDiagonalSlash");
                        combo[1] = GetAttack("2H_SpinBackhand");
                        combo[2] = GetAttack("2H_OverheadSmash");
                    }
                    else if (profile.family == WeaponFamily.Axe)
                    {
                        combo[0] = GetAttack("2H_WideDiagonalSlash");
                        combo[1] = GetAttack("2H_OverheadSmash");
                        combo[2] = GetAttack("2H_SpinBackhand");
                    }
                    else // Blunt
                    {
                        combo[0] = GetAttack("2H_OverheadSmash");
                        combo[1] = GetAttack("2H_WideDiagonalSlash");
                        combo[2] = GetAttack("2H_SpinBackhand");
                    }
                    break;

                case GripMode.Pole:
                default:
                    if (profile.family == WeaponFamily.Blade)
                    {
                        combo[0] = GetAttack("Pole_HorizontalSweep");
                        combo[1] = GetAttack("Pole_DiagonalCut");
                        combo[2] = GetAttack("Pole_PiercingThrust");
                    }
                    else if (profile.family == WeaponFamily.Axe)
                    {
                        combo[0] = GetAttack("Pole_DiagonalCut");
                        combo[1] = GetAttack("Pole_HorizontalSweep");
                        combo[2] = GetAttack("Pole_PiercingThrust");
                    }
                    else // Blunt
                    {
                        combo[0] = GetAttack("Pole_DiagonalCut");
                        combo[1] = GetAttack("Pole_HorizontalSweep");
                        combo[2] = GetAttack("Pole_PiercingThrust");
                    }
                    break;
            }

            return combo;
        }

        private static AttackDefinitionSO CreateAttackById(string id)
        {
            var so = ScriptableObject.CreateInstance<AttackDefinitionSO>();
            so.name = id;

            switch (id)
            {
                // ==================== 1 HAND SET ====================
                case "1H_HorizontalSlash":
                    Configure1H_HorizontalSlash(so);
                    break;
                case "1H_DiagonalChop":
                    Configure1H_DiagonalChop(so);
                    break;
                case "1H_Thrust":
                    Configure1H_Thrust(so);
                    break;
                case "1H_OverheadChop":
                    Configure1H_OverheadChop(so);
                    break;

                // ==================== 2 HANDS SET ====================
                case "2H_WideDiagonalSlash":
                    Configure2H_WideDiagonalSlash(so);
                    break;
                case "2H_OverheadSmash":
                    Configure2H_OverheadSmash(so);
                    break;
                case "2H_SpinBackhand":
                    Configure2H_SpinBackhand(so);
                    break;

                // ==================== POLEARM SET ====================
                case "Pole_HorizontalSweep":
                    ConfigurePole_HorizontalSweep(so);
                    break;
                case "Pole_DiagonalCut":
                    ConfigurePole_DiagonalCut(so);
                    break;
                case "Pole_PiercingThrust":
                    ConfigurePole_PiercingThrust(so);
                    break;

                default:
                    Configure1H_HorizontalSlash(so);
                    break;
            }

            return so;
        }

        // ==========================================
        // 1 HAND ATTACKS
        // ==========================================
        private static void Configure1H_HorizontalSlash(AttackDefinitionSO so)
        {
            so.attackName = "Tajo Horizontal (1 Mano)";
            so.gripMode = GripMode.OneHand;
            so.preferredFamily = WeaponFamily.Blade;
            so.swingArcAngle = 80f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.22f, -0.04f, 0.52f), Quaternion.Euler(-20f, 20f, -12f)), 0.10f);

            // Windup: mano cargada atrás a la derecha, cadera y pecho giran atrás
            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.38f, 0.04f, 0.36f), Quaternion.Euler(12f, 42f, 15f)),
                hipYaw = 15f, chestYaw = 24f, chestPitch = -4f, chestRoll = 2f,
                weightShift = new Vector3(0.08f, 0f, -0.08f), lunge = 0.05f, duration = 0.22f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            // Strike: barrido amplio horizontal que cruza al frente a la izquierda
            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.24f, -0.04f, 0.50f), Quaternion.Euler(-18f, -42f, -22f)),
                hipYaw = -18f, chestYaw = -28f, chestPitch = 4f, chestRoll = -3f,
                weightShift = new Vector3(-0.06f, 0f, 0.12f), lunge = 0.45f, duration = 0.16f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
                hasIntermediatePose = true,
                intermediateHandPose = new Pose(new Vector3(0.12f, 0.02f, 0.58f), Quaternion.Euler(-8f, 0f, -5f))
            };

            // Recovery: desaceleración al lado izquierdo y peso regresando al centro
            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.30f, -0.08f, 0.44f), Quaternion.Euler(-12f, -48f, -18f)),
                hipYaw = -12f, chestYaw = -18f, chestPitch = 2f, chestRoll = 0f,
                weightShift = Vector3.zero, lunge = 0.05f, duration = 0.26f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }

        private static void Configure1H_DiagonalChop(AttackDefinitionSO so)
        {
            so.attackName = "Tajo Diagonal Descendente (1 Mano)";
            so.gripMode = GripMode.OneHand;
            so.preferredFamily = WeaponFamily.Axe;
            so.swingArcAngle = 85f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.22f, -0.04f, 0.52f), Quaternion.Euler(-20f, 20f, -12f)), 0.10f);

            // Windup: alzada arriba sobre el hombro derecho
            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.32f, 0.20f, 0.35f), Quaternion.Euler(32f, 28f, 18f)),
                hipYaw = 12f, chestYaw = 20f, chestPitch = -6f, chestRoll = 4f,
                weightShift = new Vector3(0.06f, 0f, -0.06f), lunge = 0.05f, duration = 0.24f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            // Strike: tajo diagonal hacia abajo a la izquierda
            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.20f, -0.18f, 0.48f), Quaternion.Euler(-28f, -32f, -28f)),
                hipYaw = -16f, chestYaw = -24f, chestPitch = 12f, chestRoll = -6f,
                weightShift = new Vector3(-0.04f, 0f, 0.14f), lunge = 0.50f, duration = 0.17f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
                hasIntermediatePose = true,
                intermediateHandPose = new Pose(new Vector3(0.14f, 0.06f, 0.56f), Quaternion.Euler(2f, 0f, -10f))
            };

            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.25f, -0.22f, 0.42f), Quaternion.Euler(-22f, -36f, -22f)),
                hipYaw = -10f, chestYaw = -15f, chestPitch = 6f, chestRoll = -2f,
                weightShift = Vector3.zero, lunge = 0.05f, duration = 0.28f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }

        private static void Configure1H_Thrust(AttackDefinitionSO so)
        {
            so.attackName = "Estocada Frontal (1 Mano)";
            so.gripMode = GripMode.OneHand;
            so.preferredFamily = WeaponFamily.Blade;
            so.swingArcAngle = 25f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.22f, -0.04f, 0.52f), Quaternion.Euler(-20f, 20f, -12f)), 0.10f);

            // Windup: retraída cerca de la costilla derecha alineada al frente
            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.20f, -0.08f, 0.35f), Quaternion.Euler(-6f, 8f, 0f)),
                hipYaw = 8f, chestYaw = 12f, chestPitch = -2f, chestRoll = 0f,
                weightShift = new Vector3(0.04f, 0f, -0.06f), lunge = 0.05f, duration = 0.20f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            // Strike: extensión recta profunda al frente
            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.14f, 0.02f, 0.65f), Quaternion.Euler(-2f, 0f, 0f)),
                hipYaw = -6f, chestYaw = -8f, chestPitch = 4f, chestRoll = 0f,
                weightShift = new Vector3(0f, 0f, 0.18f), lunge = 0.70f, duration = 0.14f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.18f, -0.04f, 0.50f), Quaternion.Euler(-12f, 10f, -6f)),
                hipYaw = 0f, chestYaw = 0f, chestPitch = 0f, chestRoll = 0f,
                weightShift = Vector3.zero, lunge = 0.05f, duration = 0.24f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }

        private static void Configure1H_OverheadChop(AttackDefinitionSO so)
        {
            so.attackName = "Golpe Vertical (1 Mano)";
            so.gripMode = GripMode.OneHand;
            so.preferredFamily = WeaponFamily.Blunt;
            so.swingArcAngle = 90f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.22f, -0.04f, 0.52f), Quaternion.Euler(-20f, 20f, -12f)), 0.10f);

            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.20f, 0.26f, 0.34f), Quaternion.Euler(40f, 10f, 10f)),
                hipYaw = 6f, chestYaw = 10f, chestPitch = -8f, chestRoll = 0f,
                weightShift = new Vector3(0f, 0f, -0.08f), lunge = 0.05f, duration = 0.24f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.16f, -0.22f, 0.52f), Quaternion.Euler(-38f, 0f, 0f)),
                hipYaw = -4f, chestYaw = -6f, chestPitch = 16f, chestRoll = 0f,
                weightShift = new Vector3(0f, 0f, 0.16f), lunge = 0.55f, duration = 0.17f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.18f, -0.16f, 0.46f), Quaternion.Euler(-25f, 5f, 0f)),
                hipYaw = 0f, chestYaw = 0f, chestPitch = 6f, chestRoll = 0f,
                weightShift = Vector3.zero, lunge = 0.05f, duration = 0.28f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }

        // ==========================================
        // 2 HANDS ATTACKS
        // ==========================================
        private static void Configure2H_WideDiagonalSlash(AttackDefinitionSO so)
        {
            so.attackName = "Tajo Diagonal Amplio (2 Manos)";
            so.gripMode = GripMode.TwoHand;
            so.preferredFamily = WeaponFamily.Blade;
            so.swingArcAngle = 95f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.14f, -0.10f, 0.48f), Quaternion.Euler(-24f, 15f, -8f)), 0.12f);

            // Windup: torsión amplia del torso con arma cargada sobre el hombro
            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.28f, 0.18f, 0.36f), Quaternion.Euler(28f, 32f, 14f)),
                hipYaw = 18f, chestYaw = 28f, chestPitch = -6f, chestRoll = 4f,
                weightShift = new Vector3(0.10f, 0f, -0.10f), lunge = 0.08f, duration = 0.28f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            // Strike: barrido masivo cruzando al frente con paso delantero
            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.20f, -0.14f, 0.54f), Quaternion.Euler(-26f, -34f, -24f)),
                hipYaw = -22f, chestYaw = -32f, chestPitch = 8f, chestRoll = -6f,
                weightShift = new Vector3(-0.08f, 0f, 0.18f), lunge = 0.68f, duration = 0.20f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
                hasIntermediatePose = true,
                intermediateHandPose = new Pose(new Vector3(0.10f, 0.04f, 0.60f), Quaternion.Euler(0f, 0f, -8f))
            };

            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.26f, -0.18f, 0.46f), Quaternion.Euler(-20f, -40f, -18f)),
                hipYaw = -14f, chestYaw = -18f, chestPitch = 4f, chestRoll = -2f,
                weightShift = Vector3.zero, lunge = 0.06f, duration = 0.34f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }

        private static void Configure2H_OverheadSmash(AttackDefinitionSO so)
        {
            so.attackName = "Golpe Descendente Vertical (2 Manos)";
            so.gripMode = GripMode.TwoHand;
            so.preferredFamily = WeaponFamily.Blunt;
            so.swingArcAngle = 100f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.14f, -0.10f, 0.48f), Quaternion.Euler(-24f, 15f, -8f)), 0.12f);

            // Windup: alzada alta centrada sobre la cabeza
            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.06f, 0.32f, 0.34f), Quaternion.Euler(42f, 4f, 0f)),
                hipYaw = 0f, chestYaw = 0f, chestPitch = -12f, chestRoll = 0f,
                weightShift = new Vector3(0f, 0f, -0.12f), lunge = 0.06f, duration = 0.30f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            // Strike: impacto potente clavando hacia el suelo
            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.04f, -0.24f, 0.52f), Quaternion.Euler(-42f, 0f, 0f)),
                hipYaw = 0f, chestYaw = 0f, chestPitch = 22f, chestRoll = 0f,
                weightShift = new Vector3(0f, 0f, 0.20f), lunge = 0.60f, duration = 0.22f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.06f, -0.18f, 0.46f), Quaternion.Euler(-30f, 0f, 0f)),
                hipYaw = 0f, chestYaw = 0f, chestPitch = 10f, chestRoll = 0f,
                weightShift = Vector3.zero, lunge = 0.06f, duration = 0.36f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }

        private static void Configure2H_SpinBackhand(AttackDefinitionSO so)
        {
            so.attackName = "Revés Horizontal con Giro (2 Manos)";
            so.gripMode = GripMode.TwoHand;
            so.preferredFamily = WeaponFamily.Blade;
            so.swingArcAngle = 105f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.14f, -0.10f, 0.48f), Quaternion.Euler(-24f, 15f, -8f)), 0.12f);

            // Windup: enrollado bajo al flanco izquierdo
            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.18f, -0.12f, 0.38f), Quaternion.Euler(-14f, -38f, -14f)),
                hipYaw = -20f, chestYaw = -28f, chestPitch = 4f, chestRoll = -4f,
                weightShift = new Vector3(-0.08f, 0f, -0.08f), lunge = 0.08f, duration = 0.26f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            // Strike: estallido de revés de izquierda a derecha
            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.28f, 0.04f, 0.52f), Quaternion.Euler(-10f, 42f, 18f)),
                hipYaw = 24f, chestYaw = 34f, chestPitch = -2f, chestRoll = 6f,
                weightShift = new Vector3(0.10f, 0f, 0.16f), lunge = 0.75f, duration = 0.20f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
                hasIntermediatePose = true,
                intermediateHandPose = new Pose(new Vector3(0.04f, -0.02f, 0.58f), Quaternion.Euler(-8f, 0f, 0f))
            };

            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.32f, 0.02f, 0.45f), Quaternion.Euler(-6f, 48f, 14f)),
                hipYaw = 15f, chestYaw = 20f, chestPitch = 0f, chestRoll = 2f,
                weightShift = Vector3.zero, lunge = 0.06f, duration = 0.32f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }

        // ==========================================
        // POLEARM ATTACKS
        // ==========================================
        private static void ConfigurePole_HorizontalSweep(AttackDefinitionSO so)
        {
            so.attackName = "Barrido Horizontal de Asta (Polearm)";
            so.gripMode = GripMode.Pole;
            so.preferredFamily = WeaponFamily.Blade;
            so.swingArcAngle = 110f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.18f, -0.14f, 0.46f), Quaternion.Euler(-32f, 18f, 12f)), 0.14f);

            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.32f, -0.06f, 0.36f), Quaternion.Euler(-12f, 42f, 10f)),
                hipYaw = 20f, chestYaw = 30f, chestPitch = -4f, chestRoll = 4f,
                weightShift = new Vector3(0.10f, 0f, -0.10f), lunge = 0.08f, duration = 0.30f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.22f, -0.04f, 0.56f), Quaternion.Euler(-16f, -44f, -14f)),
                hipYaw = -24f, chestYaw = -34f, chestPitch = 6f, chestRoll = -6f,
                weightShift = new Vector3(-0.08f, 0f, 0.20f), lunge = 0.80f, duration = 0.22f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
                hasIntermediatePose = true,
                intermediateHandPose = new Pose(new Vector3(0.12f, 0.02f, 0.64f), Quaternion.Euler(-6f, 0f, 0f))
            };

            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.28f, -0.10f, 0.46f), Quaternion.Euler(-18f, -48f, -10f)),
                hipYaw = -14f, chestYaw = -18f, chestPitch = 2f, chestRoll = -2f,
                weightShift = Vector3.zero, lunge = 0.06f, duration = 0.36f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }

        private static void ConfigurePole_DiagonalCut(AttackDefinitionSO so)
        {
            so.attackName = "Tajo Diagonal Largo de Asta (Polearm)";
            so.gripMode = GripMode.Pole;
            so.preferredFamily = WeaponFamily.Axe;
            so.swingArcAngle = 100f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.18f, -0.14f, 0.46f), Quaternion.Euler(-32f, 18f, 12f)), 0.14f);

            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.24f, 0.26f, 0.35f), Quaternion.Euler(34f, 24f, 8f)),
                hipYaw = 16f, chestYaw = 24f, chestPitch = -8f, chestRoll = 2f,
                weightShift = new Vector3(0.08f, 0f, -0.08f), lunge = 0.08f, duration = 0.32f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.16f, -0.20f, 0.52f), Quaternion.Euler(-32f, -26f, -18f)),
                hipYaw = -18f, chestYaw = -26f, chestPitch = 14f, chestRoll = -6f,
                weightShift = new Vector3(-0.06f, 0f, 0.18f), lunge = 0.70f, duration = 0.22f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(-0.20f, -0.24f, 0.44f), Quaternion.Euler(-26f, -30f, -12f)),
                hipYaw = -10f, chestYaw = -14f, chestPitch = 8f, chestRoll = -2f,
                weightShift = Vector3.zero, lunge = 0.06f, duration = 0.38f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }

        private static void ConfigurePole_PiercingThrust(AttackDefinitionSO so)
        {
            so.attackName = "Estocada de Asta (Polearm)";
            so.gripMode = GripMode.Pole;
            so.preferredFamily = WeaponFamily.Blade;
            so.swingArcAngle = 20f;

            so.guardPhase = AttackPhase.CreateDefault(new Pose(new Vector3(0.18f, -0.14f, 0.46f), Quaternion.Euler(-32f, 18f, 12f)), 0.14f);

            so.windupPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.18f, -0.16f, 0.32f), Quaternion.Euler(-6f, 4f, 0f)),
                hipYaw = 12f, chestYaw = 18f, chestPitch = -2f, chestRoll = 0f,
                weightShift = new Vector3(0.06f, 0f, -0.08f), lunge = 0.06f, duration = 0.22f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            so.strikePhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.10f, -0.04f, 0.74f), Quaternion.Euler(-2f, 0f, 0f)),
                hipYaw = -8f, chestYaw = -10f, chestPitch = 6f, chestRoll = 0f,
                weightShift = new Vector3(0f, 0f, 0.22f), lunge = 0.95f, duration = 0.16f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            so.recoveryPhase = new AttackPhase
            {
                handPose = new Pose(new Vector3(0.14f, -0.10f, 0.50f), Quaternion.Euler(-18f, 10f, 4f)),
                hipYaw = 0f, chestYaw = 0f, chestPitch = 2f, chestRoll = 0f,
                weightShift = Vector3.zero, lunge = 0.06f, duration = 0.30f,
                ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };
        }
    }
}
