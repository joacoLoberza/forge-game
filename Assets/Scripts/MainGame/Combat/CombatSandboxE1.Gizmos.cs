using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class CombatSandboxE1
    {
        public void SetupDefaultHierarchy()
        {
            if (_bodySpace == null)
            {
                Transform found = transform.Find("BodySpace");
                if (found != null) _bodySpace = found;
                else
                {
                    GameObject bs = new GameObject("BodySpace");
                    bs.transform.SetParent(transform, false);
                    _bodySpace = bs.transform;
                }
            }

            if (_shoulderRight == null)
            {
                Transform found = _bodySpace.Find("Shoulder_R");
                if (found != null) _shoulderRight = found;
                else
                {
                    GameObject sh = new GameObject("Shoulder_R");
                    sh.transform.SetParent(_bodySpace, false);
                    sh.transform.localPosition = new Vector3(0.20f, 1.40f, 0.0f);
                    _shoulderRight = sh.transform;
                }
            }

            if (_handTargetR == null)
            {
                Transform found = transform.Find("HandTarget_R");
                if (found != null) _handTargetR = found;
                else
                {
                    GameObject ht = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ht.name = "HandTarget_R";
                    ht.transform.SetParent(transform, false);
                    ht.transform.localScale = Vector3.one * 0.08f;
                    Collider c = ht.GetComponent<Collider>();
                    if (c != null) { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
                    _handTargetR = ht.transform;
                }
            }

            if (_weaponRig == null)
            {
                _weaponRig = _handTargetR.GetComponentInChildren<WeaponRig>();
                if (_weaponRig == null)
                {
                    GameObject wr = new GameObject("WeaponRig");
                    wr.transform.SetParent(_handTargetR, false);
                    _weaponRig = wr.AddComponent<WeaponRig>();
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_shoulderRight == null) return;

            Vector3 shoulderPos = _shoulderRight.position;
            Quaternion bodyRot = _bodySpace != null ? _bodySpace.rotation : Quaternion.identity;

            // 1. Esferas de alcance del hombro
            if (_showReachSpheres)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.25f);
                Gizmos.DrawWireSphere(shoulderPos, _armLength); // 100%
                Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.4f);
                Gizmos.DrawWireSphere(shoulderPos, _armLength * SphericalCoords.SafeReachLimit); // 92% seguro
            }

            // 2. Trayectoria horneada y normales de filo (+Y)
            if (_showTrajectory && _bakedData != null && _bakedData.FrameCount > 1)
            {
                Vector3 prevTip = Vector3.zero;
                for (int i = 0; i < _bakedData.FrameCount; i++)
                {
                    BakedAttackFrame f = _bakedData.frames[i];
                    Vector3 tipWorld = shoulderPos + bodyRot * f.tipLocalPos;

                    if (i > 0)
                    {
                        Gizmos.color = (f.phase == AttackPhase.Strike || f.phase == AttackPhase.HitWindow)
                            ? Color.red : new Color(0.2f, 0.8f, 1f);
                        Gizmos.DrawLine(prevTip, tipWorld);
                    }
                    prevTip = tipWorld;

                    // Mostrar dirección del filo (+Y) a intervalos
                    if (_showEdgeNormals && i % 4 == 0)
                    {
                        Vector3 edgeDirWorld = bodyRot * (f.weaponLocalRot * Vector3.up);
                        Gizmos.color = Color.green;
                        Gizmos.DrawRay(tipWorld, edgeDirWorld * 0.15f);
                    }
                }
            }

            // 3. Vector de velocidad instantánea de la punta
            if (_bakedData != null && _weaponRig != null)
            {
                BakedAttackFrame current = _bakedData.Evaluate(_normalizedTime);
                Vector3 vTipWorld = bodyRot * current.tipVelocity;
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(_weaponRig.TipPointWorld, vTipWorld * 0.1f);
            }
        }

        private void OnGUI()
        {
            BakedAttackFrame frame = _bakedData != null ? _bakedData.Evaluate(_normalizedTime) : default;
            float reachPct = frame.reach * 100f;
            float alignment = frame.edgeVelocityDot;
            bool aligned = alignment >= 0.85f;

            GUILayout.BeginArea(new Rect(20, 20, 360, 290), GUI.skin.box);
            GUILayout.Label("<b>SISTEMA DE ATAQUE PROCEDURAL - ETAPA E1</b>", GUILayout.Height(22));
            GUILayout.Label("Espada 1H: Golpe 1 (Tajo Diagonal Descendente)");
            GUILayout.Space(4);

            string phaseColor = frame.phase switch
            {
                AttackPhase.Anticipation => "#FFA500",
                AttackPhase.Strike => "#FF4500",
                AttackPhase.HitWindow => "#FF0000",
                AttackPhase.FollowThrough => "#9370DB",
                _ => "#32CD32"
            };
            GUILayout.Label($"Fase: <color={phaseColor}><b>{frame.phase}</b></color>");
            GUILayout.Label($"Tiempo: <b>{_normalizedTime * _baseDuration:F2}s</b> / {_baseDuration:F2}s ({_normalizedTime * 100f:F0}%)");

            string reachColor = reachPct <= 92f ? "#32CD32" : "#FF4500";
            GUILayout.Label($"Alcance de Brazo: <color={reachColor}><b>{reachPct:F1}%</b></color> (Tope seguro: 92%)");

            string alignColor = aligned ? "#32CD32" : "#FF0000";
            GUILayout.Label($"Alineación Filo / Velocidad: <color={alignColor}><b>{alignment:F3}</b></color> (Mín: 0.85)");
            GUILayout.Label(aligned ? "<color=#32CD32>✔ FILO LIDERA EL MOVIMIENTO</color>" : "<color=#FF0000>⚠ REVISAR ALINEACIÓN</color>");

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_isPlaying ? "Pausar" : "Reproducir", GUILayout.Width(100)))
            {
                _isPlaying = !_isPlaying;
            }
            if (GUILayout.Button("Reiniciar", GUILayout.Width(80)))
            {
                _normalizedTime = 0f;
                _isPlaying = true;
            }
            _loop = GUILayout.Toggle(_loop, " En Bucle");
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label($"Velocidad ({_playbackSpeed:F1}x):");
            _playbackSpeed = GUILayout.HorizontalSlider(_playbackSpeed, 0.1f, 2.0f);

            GUILayout.Label("Scrubber de Tiempo (0..1):");
            float newT = GUILayout.HorizontalSlider(_normalizedTime, 0f, 1f);
            if (Mathf.Abs(newT - _normalizedTime) > 0.001f)
            {
                _normalizedTime = newT;
                _isPlaying = false;
            }

            GUILayout.EndArea();
        }
    }
}
