using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class ProceduralCombatController
    {
        // =========================================================================
        // VOLUMEN DE EXCLUSIÓN CORPORAL (Anti-Clipping Físico)
        // =========================================================================

        private void ApplyBodyExclusion()
        {
            if (_weaponPivot == null || config == null) return;

            Transform chest = _chestBone != null ? _chestBone : transform;
            Transform hips = _hipsBone != null ? _hipsBone : transform;
            Transform head = _headBone != null ? _headBone : chest;

            Vector3 pGripR = _gripSocketR != null ? _gripSocketR.position : _weaponPivot.position;
            Vector3 pGripL = _gripSocketL != null ? _gripSocketL.position : _weaponPivot.position;
            Vector3 pBladeBase = _bladeBase != null ? _bladeBase.position : _weaponPivot.position;
            Vector3 pBladeTip = _bladeTip != null ? _bladeTip.position : _weaponPivot.position;
            Vector3 pBladeMid = (pBladeBase + pBladeTip) * 0.5f;

            Vector3[] weaponPoints = { pGripR, pGripL, pBladeBase, pBladeMid, pBladeTip };

            // 1. Torso: segmento entre cadera y cuello
            Vector3 torsoStart = hips.position + Vector3.up * 0.10f;
            Vector3 torsoEnd = chest.position + Vector3.up * 0.15f;
            float torsoR = config.torsoRadius + config.exclusionMargin;

            // 2. Cabeza: esfera
            Vector3 headCenter = head.position;
            float headR = config.headRadius + config.exclusionMargin;

            // 3. Piernas: segmento desde cadera hacia el suelo
            Vector3 legsStart = hips.position;
            Vector3 legsEnd = transform.position + Vector3.up * 0.15f;
            float legsR = config.legsRadius + config.exclusionMargin;

            Vector3 totalRepulsion = Vector3.zero;

            foreach (var pt in weaponPoints)
            {
                // Chequear cápsula del Torso
                Vector3 cTorso = ClosestPointOnSegment(torsoStart, torsoEnd, pt);
                Vector3 toPtTorso = pt - cTorso;
                float distTorso = toPtTorso.magnitude;
                if (distTorso < torsoR)
                {
                    Vector3 pushDir = distTorso > 0.001f ? toPtTorso / distTorso : transform.forward;
                    float pen = torsoR - distTorso;
                    if (pen > totalRepulsion.magnitude) totalRepulsion = pushDir * pen;
                }

                // Chequear esfera de la Cabeza
                Vector3 toPtHead = pt - headCenter;
                float distHead = toPtHead.magnitude;
                if (distHead < headR)
                {
                    Vector3 pushDir = distHead > 0.001f ? toPtHead / distHead : (transform.forward + transform.up * 0.5f).normalized;
                    float pen = headR - distHead;
                    if (pen > totalRepulsion.magnitude) totalRepulsion = pushDir * pen;
                }

                // Chequear cápsula de Piernas
                Vector3 cLegs = ClosestPointOnSegment(legsStart, legsEnd, pt);
                Vector3 toPtLegs = pt - cLegs;
                float distLegs = toPtLegs.magnitude;
                if (distLegs < legsR)
                {
                    Vector3 pushDir = distLegs > 0.001f ? toPtLegs / distLegs : transform.forward;
                    float pen = legsR - distLegs;
                    if (pen > totalRepulsion.magnitude) totalRepulsion = pushDir * pen;
                }
            }

            if (totalRepulsion.sqrMagnitude > 0.00001f)
            {
                _weaponPivot.position += totalRepulsion;
            }
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
            return a + ab * t;
        }
    }
}
