using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Core;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Actualización térmica del fuego interno, vista previa 3D de minerales en crudo,
    /// animación de caída por la boca superior y extracción 3D de la Lupa por la boca inferior.
    /// </summary>
    public partial class FurnaceVisualController
    {
        private readonly List<GameObject> _stagedPreviewProps = new();
        private readonly List<GameObject> _crucibleProps = new();
        private GameObject _activeBloomObj;
        private string _activeBloomId;
        private int _lastSyncedInsertedCount = -1;
        public event Action BottomBloomClicked;

        private void Update()
        {
            if (!_isBuilt) EnsureBuilt();
            if (furnaceController == null) return;

            UpdateInternalFireFromHeat();
            AnimateStagedPreviewHover();
            SyncCrucibleInsertedOres();
            SyncBottomBloomVisual();
            CheckDirectClickOnBottomBloom();
        }

        private void UpdateInternalFireFromHeat()
        {
            float maxT = furnaceController.MaxTemperature > 0f ? furnaceController.MaxTemperature : 1600f;
            float temp = furnaceController.CurrentTemperature;
            float ratio = Mathf.Clamp01(temp / maxT);
            float flicker = 0.88f + 0.12f * Mathf.PerlinNoise(Time.time * 9.5f, 1.7f);

            Color fireCol = ratio < 0.35f
                ? Color.Lerp(new Color(0.65f, 0.10f, 0.02f), new Color(1.0f, 0.36f, 0.05f), ratio / 0.35f)
                : Color.Lerp(new Color(1.0f, 0.36f, 0.05f), new Color(1.0f, 0.88f, 0.42f), (ratio - 0.35f) / 0.65f);

            if (_hearthLight != null)
            {
                _hearthLight.color = fireCol;
                _hearthLight.intensity = (temp > 5f ? Mathf.Lerp(0.45f, 6.2f, ratio) : 0f) * flicker;
            }
            if (_crucibleLight != null)
            {
                _crucibleLight.color = fireCol;
                _crucibleLight.intensity = (temp > 80f ? Mathf.Lerp(0.2f, 3.2f, ratio) : 0f) * flicker;
            }
            if (_hearthFlamePs != null)
            {
                var em = _hearthFlamePs.emission;
                em.rateOverTime = temp <= 8f ? 0f : Mathf.Lerp(6f, 58f, ratio);
                var main = _hearthFlamePs.main;
                main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.12f, 0.24f, ratio), Mathf.Lerp(0.22f, 0.44f, ratio));
                main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.28f, 0.65f, ratio), Mathf.Lerp(0.50f, 1.15f, ratio));
            }
            if (_chimneySparksPs != null)
            {
                var em = _chimneySparksPs.emission;
                em.rateOverTime = temp <= 120f ? 0f : Mathf.Lerp(2f, 22f, ratio);
            }

            Color emberEmission = fireCol * (Mathf.Pow(ratio, 0.85f) * 2.4f * flicker);
            for (int i = 0; i < _emberMaterials.Count; i++)
                if (_emberMaterials[i] != null && _emberMaterials[i].HasProperty("_EmissionColor"))
                    _emberMaterials[i].SetColor("_EmissionColor", emberEmission);

            if (_interiorFireboxMat != null && _interiorFireboxMat.HasProperty("_EmissionColor"))
                _interiorFireboxMat.SetColor("_EmissionColor", fireCol * (ratio * 1.15f * flicker));
            if (_interiorCrucibleMat != null && _interiorCrucibleMat.HasProperty("_EmissionColor"))
                _interiorCrucibleMat.SetColor("_EmissionColor", fireCol * (ratio * 0.95f));
        }

        public void UpdateStagedOresPreview(IReadOnlyList<MetalType> stagedOres)
        {
            EnsureBuilt();
            ClearPropList(_stagedPreviewProps);
            if (stagedOres == null || stagedOres.Count == 0) return;

            int count = stagedOres.Count;
            for (int i = 0; i < count; i++)
            {
                GameObject prop = BloomVisualBuilder.CreateRawOreProp(stagedOres[i], 300 + i * 29, _stagedPreviewRoot, 0.21f);
                prop.transform.localPosition = ComputeStagedLocalPos(i, count);
                _stagedPreviewProps.Add(prop);
            }
        }

        private static Vector3 ComputeStagedLocalPos(int index, int count)
        {
            float spread = count <= 1 ? 0f : (index / (float)(count - 1)) - 0.5f;
            float angle = spread * 1.15f;
            float radius = 0.86f;
            float y = 1.48f + (index % 2) * 0.13f;
            return new Vector3(Mathf.Sin(angle) * 0.72f, y, -Mathf.Cos(angle) * radius);
        }

        private void AnimateStagedPreviewHover()
        {
            for (int i = 0; i < _stagedPreviewProps.Count; i++)
            {
                var p = _stagedPreviewProps[i];
                if (p == null) continue;
                Vector3 basePos = ComputeStagedLocalPos(i, _stagedPreviewProps.Count);
                p.transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 3.1f + i * 0.8f) * 0.03f);
                p.transform.Rotate(Vector3.up, 38f * Time.deltaTime, Space.Self);
            }
        }

        public void PlayInsertOresTopAnimation(IReadOnlyList<MetalType> committedOres)
        {
            EnsureBuilt();
            ClearPropList(_stagedPreviewProps);
            if (committedOres == null || committedOres.Count == 0) return;
            StartCoroutine(AnimateOresIntoTopOpeningRoutine(new List<MetalType>(committedOres)));
        }

        private IEnumerator AnimateOresIntoTopOpeningRoutine(List<MetalType> ores)
        {
            IsAnimatingTopDrop = true;
            var tempDrops = new List<GameObject>(ores.Count);
            var startPositions = new List<Vector3>(ores.Count);
            var endPositions = new List<Vector3>(ores.Count);

            for (int i = 0; i < ores.Count; i++)
            {
                GameObject prop = BloomVisualBuilder.CreateRawOreProp(ores[i], 900 + i * 37, _fxRoot, 0.21f);
                Vector3 start = ComputeStagedLocalPos(i, ores.Count);
                float ang = i * 2.2f;
                Vector3 end = new Vector3(Mathf.Cos(ang) * 0.09f, 1.88f + (i % 3) * 0.04f, Mathf.Sin(ang) * 0.09f);
                prop.transform.localPosition = start;
                tempDrops.Add(prop);
                startPositions.Add(start);
                endPositions.Add(end);
            }

            float totalDuration = 0.78f + ores.Count * 0.07f;
            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < tempDrops.Count; i++)
                {
                    if (tempDrops[i] == null) continue;
                    float t = Mathf.Clamp01((elapsed - i * 0.07f) / 0.72f);
                    if (t <= 0f) continue;

                    // Arco parabólico sobre el borde superior (H = 2.68m) y caída vertical por la boca superior (H = 2.28m -> 1.88m)
                    Vector3 horiz = Vector3.Lerp(startPositions[i], endPositions[i], Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.25f)));
                    float y = t < 0.55f
                        ? Mathf.Lerp(startPositions[i].y, 2.66f, Mathf.Sin((t / 0.55f) * Mathf.PI * 0.5f))
                        : Mathf.Lerp(2.66f, endPositions[i].y, Mathf.Pow((t - 0.55f) / 0.45f, 1.6f));

                    tempDrops[i].transform.localPosition = new Vector3(horiz.x, y, horiz.z);
                    tempDrops[i].transform.Rotate(new Vector3(180f, 120f, 90f) * Time.deltaTime, Space.Self);
                }
                yield return null;
            }

            ClearPropList(tempDrops);
            IsAnimatingTopDrop = false;
            _lastSyncedInsertedCount = -1;
            SyncCrucibleInsertedOres();
        }

        private void SyncCrucibleInsertedOres()
        {
            if (IsAnimatingTopDrop || furnaceController == null) return;
            int total = furnaceController.GetTotalOreCount();
            if (total != _lastSyncedInsertedCount)
            {
                _lastSyncedInsertedCount = total;
                ClearPropList(_crucibleProps);
                int idx = 0;
                foreach (var kvp in furnaceController.InsertedOres)
                {
                    for (int c = 0; c < kvp.Value; c++)
                    {
                        var prop = BloomVisualBuilder.CreateRawOreProp(kvp.Key, 1200 + idx * 41, _crucibleOresRoot, 0.18f);
                        float ang = idx * 2.15f;
                        prop.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.10f, 1.90f + (idx % 3) * 0.045f, Mathf.Sin(ang) * 0.10f);
                        _crucibleProps.Add(prop);
                        idx++;
                    }
                }
            }

            float melt = furnaceController.SmeltProgress;
            for (int i = 0; i < _crucibleProps.Count; i++)
                if (_crucibleProps[i] != null) _crucibleProps[i].transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.42f, melt);
        }

        private void OnFurnaceSmeltCompleted(BloomData bloom) => SyncBottomBloomVisual();

        private void SyncBottomBloomVisual()
        {
            BloomData unclaimed = furnaceController != null ? furnaceController.UnclaimedBloom : null;
            if (unclaimed == null)
            {
                if (_activeBloomObj != null) UnitySafeUtility.DestroyGameObjectSafe(_activeBloomObj);
                _activeBloomObj = null; _activeBloomId = null;
                return;
            }
            if (_activeBloomObj != null && _activeBloomId == unclaimed.Id) return;
            if (_activeBloomObj != null) UnitySafeUtility.DestroyGameObjectSafe(_activeBloomObj);

            _activeBloomId = unclaimed.Id;
            _activeBloomObj = BloomVisualBuilder.CreateBloomVisual(unclaimed, _bottomBloomRoot);
            var col = _activeBloomObj.AddComponent<SphereCollider>();
            col.radius = 0.28f; col.center = new Vector3(0f, 0.10f, 0f);
            StartCoroutine(SlideBloomToBottomMouthRoutine(_activeBloomObj.transform));
        }

        private IEnumerator SlideBloomToBottomMouthRoutine(Transform bloomTr)
        {
            Vector3 from = new Vector3(0f, 0.06f, -0.22f), to = new Vector3(0f, 0.06f, -0.68f);
            for (float t = 0f; t < 1f && bloomTr != null; t += Time.deltaTime * 1.8f)
            {
                bloomTr.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        private void CheckDirectClickOnBottomBloom()
        {
            if (_activeBloomObj == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 8f) && hit.collider != null && hit.collider.transform.IsChildOf(_activeBloomObj.transform))
                BottomBloomClicked?.Invoke();
        }

        private static void ClearPropList(List<GameObject> list)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] != null) UnitySafeUtility.DestroyGameObjectSafe(list[i]);
            list.Clear();
        }
    }
}
