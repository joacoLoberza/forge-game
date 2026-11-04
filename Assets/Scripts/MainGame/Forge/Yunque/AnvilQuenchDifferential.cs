using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Secuencia de templado diferencial selectivo en agua:
    /// Solo sumerge la parte del lingote que tiene capa mínima (el filo).
    /// Si hay filos repartidos en distintos lados, sumerge y retira la pieza
    /// automáticamente por etapas orientando cada filo hacia el agua.
    /// Emite vapor denso y sonido de siseo térmico.
    /// </summary>
    public class AnvilQuenchDifferential : MonoBehaviour
    {
        [Header("Quench Basin Configuration")]
        [SerializeField] private Vector3 waterBasinOffset = new Vector3(1.85f, -0.12f, 0.15f);
        [SerializeField] private ParticleSystem steamVFXPrefab;
        [SerializeField] private AudioClip sizzleAudioClip;

        private AnvilController _anvil;
        private bool _isQuenching;

        public bool IsQuenching => _isQuenching;
        public Vector3 WaterBasinOffset => waterBasinOffset;
        public Vector3 CurrentWaterTargetWorldPos => transform.TransformPoint(waterBasinOffset);
        public void SetWaterBasinOffset(Vector3 offset) => waterBasinOffset = offset;

        public void Initialize(AnvilController anvil) => _anvil = anvil;

        public void ExecuteSelectiveQuench(
            AudioSource audioSource, AudioClip quenchSound, Action onFinished)
        {
            if (_isQuenching || _anvil == null || _anvil.IngotTransform == null) return;
            StartCoroutine(QuenchSequenceRoutine(audioSource, quenchSound ?? sizzleAudioClip, onFinished));
        }

        private IEnumerator QuenchSequenceRoutine(
            AudioSource audioSource, AudioClip sound, Action onFinished)
        {
            _isQuenching = true;
            Transform ingotT = _anvil.IngotTransform;
            Vector3 originLocalPos = ingotT.localPosition;
            Quaternion originLocalRot = ingotT.localRotation;

            Vector3 waterLocalPos = waterBasinOffset;
            List<Vector3> edgeVectors = GetSharpEdgeDirections();
            if (edgeVectors.Count == 0) edgeVectors.Add(Vector3.forward);

            // 1. Elevar ligeramente lingote del yunque
            yield return LerpTransform(ingotT, originLocalPos + Vector3.up * 0.18f, originLocalRot, 0.35f);

            // 2. Sumergir el filo detectado en el agua a la derecha de la estación
            for (int i = 0; i < edgeVectors.Count; i++)
            {
                Vector3 edgeDir = edgeVectors[i];
                Quaternion dipRot = Quaternion.FromToRotation(edgeDir, Vector3.down) * originLocalRot;
                Vector3 aboveWater = waterLocalPos + Vector3.up * 0.10f;

                yield return LerpTransform(ingotT, aboveWater, dipRot, 0.45f);

                Vector3 inWater = waterLocalPos + Vector3.down * 0.04f;
                yield return LerpTransform(ingotT, inWater, dipRot, 0.25f);

                Vector3 worldWaterPoint = transform.TransformPoint(inWater);
                if (steamVFXPrefab != null)
                {
                    var vfx = Instantiate(steamVFXPrefab, worldWaterPoint, Quaternion.identity);
                    Destroy(vfx.gameObject, 4.5f);
                }
                if (audioSource != null && sound != null)
                    audioSource.PlayOneShot(sound);
                else
                    ForjaGame.Audio.ForgeAudioSystem.PlayQuenchSizzle(worldWaterPoint, 1.0f);

                // Enfriamiento dinámico inmerso en agua: mínimo 4.5 segundos garantizados con vapor y siseo
                float minImmersionTime = 4.5f;
                float elapsed = 0f;
                float startTemp = _anvil.ThermalController != null ? _anvil.ThermalController.CurrentTemperature : 350f;
                float targetTemp = _anvil.ThermalController != null ? _anvil.ThermalController.AmbientTemperature : 25f;
                float steamTimer = 0f;

                while (elapsed < minImmersionTime)
                {
                    float dt = Time.deltaTime;
                    elapsed += dt;
                    if (_anvil.ThermalController != null)
                    {
                        float tRatio = Mathf.Clamp01(elapsed / minImmersionTime);
                        _anvil.ThermalController.SetTemperature(Mathf.Lerp(startTemp, targetTemp, tRatio));
                    }
                    steamTimer += dt;
                    if (steamTimer >= 0.75f && steamVFXPrefab != null)
                    {
                        steamTimer = 0f;
                        var puff = Instantiate(steamVFXPrefab, worldWaterPoint, Quaternion.identity);
                        Destroy(puff.gameObject, 3.0f);
                    }
                    yield return null;
                }
                if (_anvil.ThermalController != null) _anvil.ThermalController.ResetThermal(targetTemp);

                yield return LerpTransform(ingotT, aboveWater, dipRot, 0.40f);
                yield return new WaitForSeconds(0.25f);
            }

            // 3. Regresar a la mesa/yunque con material templado endurecido
            yield return LerpTransform(ingotT, originLocalPos, originLocalRot, 0.45f);

            ApplyQuenchedDarkSteelMaterial();
            _isQuenching = false;
            onFinished?.Invoke();
        }

        private List<Vector3> GetSharpEdgeDirections()
        {
            List<Vector3> list = new List<Vector3>();
            if (_anvil != null) list.Add(_anvil.GetDominantSharpDirection());
            else list.Add(Vector3.forward);
            return list;
        }

        private void ApplyQuenchedDarkSteelMaterial()
        {
            if (_anvil.IngotRenderer == null) return;
            Material mat = _anvil.IngotRenderer.material;
            mat.mainTexture = null;
            if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", null); mat.SetTextureScale("_BaseMap", Vector2.one); }
            if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", null); mat.SetTextureScale("_MainTex", Vector2.one); }
            Color baseCol = _anvil.CurrentMetalBaseColor != default ? _anvil.CurrentMetalBaseColor : new Color(0.85f, 0.88f, 0.92f);
            Color quenchedColor = Color.Lerp(baseCol, new Color(0.20f, 0.18f, 0.18f), 0.18f);
            mat.color = quenchedColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", quenchedColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.90f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.70f);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            _anvil.IngotRenderer.SetPropertyBlock(null);
        }

        private IEnumerator LerpTransform(Transform t, Vector3 targetPos, Quaternion targetRot, float duration)
        {
            Vector3 startPos = t.localPosition;
            Quaternion startRot = t.localRotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float f = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                t.localPosition = Vector3.Lerp(startPos, targetPos, f);
                t.localRotation = Quaternion.Slerp(startRot, targetRot, f);
                yield return null;
            }
            t.localPosition = targetPos;
            t.localRotation = targetRot;
        }
    }
}
