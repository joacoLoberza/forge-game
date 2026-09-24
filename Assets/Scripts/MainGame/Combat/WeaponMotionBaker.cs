using System;
using UnityEngine;

namespace ForjaGame.Combat
{
    [Serializable]
    public struct BakedAttackFrame
    {
        public float time;
        public Vector3 handLocalPos;
        public Vector3 tipLocalPos;
        public Vector3 tipVelocity;
        public Quaternion weaponLocalRot;
        public float reach;
        public AttackPhase phase;
        public float edgeVelocityDot;
    }

    [Serializable]
    public class BakedAttackData
    {
        public BakedAttackFrame[] frames;
        public float duration;

        public int FrameCount => frames != null ? frames.Length : 0;

        public BakedAttackFrame Evaluate(float normalizedTime)
        {
            if (frames == null || frames.Length == 0)
                return default;

            if (frames.Length == 1) return frames[0];

            float t = Mathf.Clamp01(normalizedTime);
            float indexF = t * (frames.Length - 1);
            int idx = Mathf.FloorToInt(indexF);
            int nextIdx = Mathf.Min(idx + 1, frames.Length - 1);
            float blend = indexF - idx;

            BakedAttackFrame a = frames[idx];
            BakedAttackFrame b = frames[nextIdx];

            BakedAttackFrame result = a;
            result.time = t;
            result.handLocalPos = Vector3.Lerp(a.handLocalPos, b.handLocalPos, blend);
            result.tipLocalPos = Vector3.Lerp(a.tipLocalPos, b.tipLocalPos, blend);
            result.tipVelocity = Vector3.Lerp(a.tipVelocity, b.tipVelocity, blend);
            result.weaponLocalRot = Quaternion.Slerp(a.weaponLocalRot, b.weaponLocalRot, blend);
            result.reach = Mathf.Lerp(a.reach, b.reach, blend);
            result.phase = blend < 0.5f ? a.phase : b.phase;
            result.edgeVelocityDot = Mathf.Lerp(a.edgeVelocityDot, b.edgeVelocityDot, blend);

            return result;
        }
    }

    /// <summary>
    /// Generador offline / runtime de trayectorias horneadas a 60 Hz.
    /// Resuelve posición por Catmull-Rom y orientación con filo guiando la velocidad (LeadVelocity).
    /// </summary>
    public static class WeaponMotionBaker
    {
        public static BakedAttackData Bake(
            AttackKeyframe[] keyframes,
            float armLength = 0.72f,
            float weaponLength = 0.95f,
            float baseDuration = 0.65f,
            float anticipationEnd = 0.30f,
            float hitWindowStart = 0.45f,
            float hitWindowEnd = 0.55f,
            float recoveryStart = 0.75f)
        {
            if (keyframes == null || keyframes.Length < 2)
                return CreateDefaultData(armLength, weaponLength, baseDuration);

            int sampleCount = Mathf.Max(30, Mathf.RoundToInt(baseDuration * 60f));
            BakedAttackFrame[] samples = new BakedAttackFrame[sampleCount];
            float dt = baseDuration / Mathf.Max(1, sampleCount - 1);

            Vector3[] rawTipPos = new Vector3[sampleCount];
            Vector3[] rawHandPos = new Vector3[sampleCount];
            Vector3[] rawTipDir = new Vector3[sampleCount];
            float[] reaches = new float[sampleCount];
            EdgeMode[] modes = new EdgeMode[sampleCount];
            float[] rolls = new float[sampleCount];

            // 1. Muestreo de curvas espaciales
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                EvaluateKeyframes(keyframes, t, out float az, out float el, out float reach,
                                  out float tipPitch, out float tipYaw, out EdgeMode mode, out float roll);

                Vector3 handLocal = SphericalCoords.SphericalToVector(az, el) * (reach * armLength);
                Vector3 tipDir = SphericalCoords.CalculateTipDirection(tipPitch, tipYaw);
                Vector3 tipLocal = handLocal + tipDir * weaponLength;

                rawHandPos[i] = handLocal;
                rawTipPos[i] = tipLocal;
                rawTipDir[i] = tipDir;
                reaches[i] = reach;
                modes[i] = mode;
                rolls[i] = roll;
            }

            // 2. Velocidad de la punta y cálculo de orientación (Gram-Schmidt)
            Quaternion prevRot = Quaternion.identity;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);

                // Velocidad central diferencial
                Vector3 vTip;
                if (i == 0) vTip = (rawTipPos[1] - rawTipPos[0]) / dt;
                else if (i == sampleCount - 1) vTip = (rawTipPos[i] - rawTipPos[i - 1]) / dt;
                else vTip = (rawTipPos[i + 1] - rawTipPos[i - 1]) / (2f * dt);

                Vector3 zAxis = rawTipDir[i].sqrMagnitude > 1e-4f ? rawTipDir[i].normalized : Vector3.forward;
                Vector3 yAxis;

                if (modes[i] == EdgeMode.LeadVelocity)
                {
                    Vector3 vPerp = vTip - Vector3.Dot(vTip, zAxis) * zAxis;
                    if (vPerp.sqrMagnitude > 1e-4f)
                    {
                        yAxis = vPerp.normalized;
                    }
                    else
                    {
                        Vector3 fallbackUp = Vector3.up;
                        yAxis = Vector3.ProjectOnPlane(fallbackUp, zAxis).normalized;
                        if (yAxis.sqrMagnitude < 1e-4f) yAxis = Vector3.ProjectOnPlane(Vector3.forward, zAxis).normalized;
                    }
                }
                else if (modes[i] == EdgeMode.Fixed)
                {
                    Vector3 refUp = Vector3.ProjectOnPlane(Vector3.up, zAxis).normalized;
                    if (refUp.sqrMagnitude < 1e-4f) refUp = Vector3.right;
                    yAxis = Quaternion.AngleAxis(rolls[i], zAxis) * refUp;
                }
                else // FaceForward
                {
                    yAxis = Vector3.ProjectOnPlane(Vector3.forward, zAxis).normalized;
                    if (yAxis.sqrMagnitude < 1e-4f) yAxis = Vector3.up;
                }

                Vector3 xAxis = Vector3.Cross(yAxis, zAxis).normalized;
                yAxis = Vector3.Cross(zAxis, xAxis).normalized;

                Quaternion rot = Quaternion.LookRotation(zAxis, yAxis);

                // Corrección de hemisferio continuo
                if (i > 0 && Quaternion.Dot(rot, prevRot) < 0f)
                {
                    rot = new Quaternion(-rot.x, -rot.y, -rot.z, -rot.w);
                }
                prevRot = rot;

                float dot = vTip.sqrMagnitude > 0.05f ? Vector3.Dot(yAxis, vTip.normalized) : 1f;

                AttackPhase phase = AttackPhase.Anticipation;
                if (t < anticipationEnd) phase = AttackPhase.Anticipation;
                else if (t < hitWindowStart) phase = AttackPhase.Strike;
                else if (t <= hitWindowEnd) phase = AttackPhase.HitWindow;
                else if (t < recoveryStart) phase = AttackPhase.FollowThrough;
                else phase = AttackPhase.Recovery;

                samples[i] = new BakedAttackFrame
                {
                    time = t,
                    handLocalPos = rawHandPos[i],
                    tipLocalPos = rawTipPos[i],
                    tipVelocity = vTip,
                    weaponLocalRot = rot,
                    reach = reaches[i],
                    phase = phase,
                    edgeVelocityDot = dot
                };
            }

            return new BakedAttackData { frames = samples, duration = baseDuration };
        }

        private static void EvaluateKeyframes(
            AttackKeyframe[] kfs, float t,
            out float az, out float el, out float reach,
            out float pitch, out float yaw, out EdgeMode mode, out float roll)
        {
            int n = kfs.Length;
            if (t <= kfs[0].time)
            {
                var k = kfs[0];
                az = k.azimuth; el = k.elevation; reach = k.reach;
                pitch = k.tipPitch; yaw = k.tipYaw; mode = k.edgeMode; roll = k.fixedRoll;
                return;
            }
            if (t >= kfs[n - 1].time)
            {
                var k = kfs[n - 1];
                az = k.azimuth; el = k.elevation; reach = k.reach;
                pitch = k.tipPitch; yaw = k.tipYaw; mode = k.edgeMode; roll = k.fixedRoll;
                return;
            }

            int idx = 0;
            while (idx < n - 1 && kfs[idx + 1].time < t) idx++;

            int i0 = Mathf.Max(0, idx - 1);
            int i1 = idx;
            int i2 = Mathf.Min(n - 1, idx + 1);
            int i3 = Mathf.Min(n - 1, idx + 2);

            float segmentDuration = Mathf.Max(1e-4f, kfs[i2].time - kfs[i1].time);
            float u = Mathf.Clamp01((t - kfs[i1].time) / segmentDuration);

            az = CatmullRom(kfs[i0].azimuth, kfs[i1].azimuth, kfs[i2].azimuth, kfs[i3].azimuth, u);
            el = CatmullRom(kfs[i0].elevation, kfs[i1].elevation, kfs[i2].elevation, kfs[i3].elevation, u);
            reach = CatmullRom(kfs[i0].reach, kfs[i1].reach, kfs[i2].reach, kfs[i3].reach, u);
            pitch = CatmullRom(kfs[i0].tipPitch, kfs[i1].tipPitch, kfs[i2].tipPitch, kfs[i3].tipPitch, u);
            yaw = CatmullRom(kfs[i0].tipYaw, kfs[i1].tipYaw, kfs[i2].tipYaw, kfs[i3].tipYaw, u);

            mode = kfs[i1].edgeMode;
            roll = Mathf.Lerp(kfs[i1].fixedRoll, kfs[i2].fixedRoll, u);
        }

        private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static BakedAttackData CreateDefaultData(float armLen, float weaponLen, float duration)
        {
            BakedAttackFrame[] f = new BakedAttackFrame[2];
            Vector3 hand = new Vector3(0.2f, -0.1f, 0.4f);
            f[0] = new BakedAttackFrame { time = 0f, handLocalPos = hand, tipLocalPos = hand + Vector3.forward * weaponLen, weaponLocalRot = Quaternion.identity, phase = AttackPhase.Anticipation, edgeVelocityDot = 1f };
            f[1] = new BakedAttackFrame { time = 1f, handLocalPos = hand, tipLocalPos = hand + Vector3.forward * weaponLen, weaponLocalRot = Quaternion.identity, phase = AttackPhase.Recovery, edgeVelocityDot = 1f };
            return new BakedAttackData { frames = f, duration = duration };
        }
    }
}
