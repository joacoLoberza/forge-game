using UnityEngine;
using ForjaGame.Resources;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Construcción y renderizado procedural del Fogón / Fragua de recalentado en el yunque (tecla [H]).
    /// Posicionado detrás del yunque donde reposa el lingote al calentarse al rojo vivo.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    public partial class AnvilStationVisualController
    {
        private void BuildHearthVisual()
        {
            float groundY = ForgeStationModelSetup.GetGroundHeightAt(transform.position.x, transform.position.z) + 0.02f;
            float localGroundY = transform.InverseTransformPoint(new Vector3(transform.position.x, groundY, transform.position.z)).y;

            Vector3 ingotBasePos = (_anvil != null && _anvil.IngotTransform != null) ? _anvil.IngotTransform.localPosition : new Vector3(0f, 1.10f, 0f);
            Vector3 reheatOffset = new Vector3(0f, 0.18f, 0.95f);
            Vector3 targetIngotLocal = ingotBasePos + reheatOffset;

            float hearthTopY = targetIngotLocal.y - 0.045f;
            float totalHearthHeight = Mathf.Max(0.85f, hearthTopY - localGroundY);
            float pedestalHeight = totalHearthHeight - 0.22f;

            _hearthRoot = new GameObject("Hearth_ForgeFire").transform;
            _hearthRoot.SetParent(_toolsRoot, false);
            _hearthRoot.localPosition = new Vector3(targetIngotLocal.x, localGroundY, targetIngotLocal.z);
            _hearthRoot.localRotation = Quaternion.identity;

            // 1. Pedestal de mampostería / piedra forjada
            var pedestalGo = new GameObject("Hearth_Pedestal", typeof(MeshFilter), typeof(MeshRenderer));
            pedestalGo.transform.SetParent(_hearthRoot, false);
            pedestalGo.transform.localPosition = Vector3.zero;
            pedestalGo.GetComponent<MeshFilter>().sharedMesh = AnvilStationMeshBuilder.CreatePedestalMesh(0.24f, 0.22f, pedestalHeight, 12);
            pedestalGo.GetComponent<MeshRenderer>().sharedMaterial = AnvilStationMeshBuilder.CreateMaterial(
                "Mat_AnvilHearthStone", new Color(0.24f, 0.23f, 0.22f), metallic: 0.10f, smoothness: 0.22f);

            // 2. Brasero de hierro fundido
            var bowlGo = new GameObject("Hearth_BrazierBowl", typeof(MeshFilter), typeof(MeshRenderer));
            bowlGo.transform.SetParent(_hearthRoot, false);
            bowlGo.transform.localPosition = new Vector3(0f, pedestalHeight, 0f);
            bowlGo.GetComponent<MeshFilter>().sharedMesh = AnvilStationMeshBuilder.CreateBrazierBowlMesh(0.24f, 0.44f, 0.22f, 16);
            bowlGo.GetComponent<MeshRenderer>().sharedMaterial = AnvilStationMeshBuilder.CreateMaterial(
                "Mat_AnvilHearthIron", new Color(0.18f, 0.18f, 0.20f), metallic: 0.82f, smoothness: 0.38f);

            // 3. Rejilla de hierro forjado
            BuildGrateBars(_hearthRoot, totalHearthHeight);

            // 4. Carbones y núcleo de brasas incandescentes
            BuildCoalsBed(_hearthRoot, pedestalHeight + 0.10f);

            // 5. Partículas de fuego y chispas discretas que no obstruyen la visión del lingote
            _hearthFlamePs = CreateHearthParticles("HearthFlames", _hearthRoot, new Vector3(0f, totalHearthHeight - 0.05f, 0f), isSparks: false);
            _hearthSparksPs = CreateHearthParticles("HearthSparks", _hearthRoot, new Vector3(0f, totalHearthHeight + 0.02f, 0f), isSparks: true);

            // 6. Luz cálida de fragua
            var lObj = new GameObject("Hearth_FireLight", typeof(Light));
            lObj.transform.SetParent(_hearthRoot, false);
            lObj.transform.localPosition = new Vector3(0f, totalHearthHeight + 0.14f, 0f);
            _hearthLight = lObj.GetComponent<Light>();
            _hearthLight.type = LightType.Point;
            _hearthLight.color = new Color(1.0f, 0.52f, 0.12f);
            _hearthLight.range = 3.6f;
            _hearthLight.intensity = 1.2f;
        }

        private void BuildGrateBars(Transform parent, float topY)
        {
            var grateRoot = new GameObject("Hearth_IronGrate");
            grateRoot.transform.SetParent(parent, false);
            grateRoot.transform.localPosition = new Vector3(0f, topY, 0f);
            var ironMat = AnvilStationMeshBuilder.CreateMaterial("Mat_AnvilGrateIron", new Color(0.14f, 0.14f, 0.15f), 0.85f, 0.42f);

            float[] offsets = { -0.16f, -0.05f, 0.05f, 0.16f };
            for (int i = 0; i < offsets.Length; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"GrateBar_{i}";
                bar.transform.SetParent(grateRoot.transform, false);
                var col = bar.GetComponent<Collider>();
                if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
                bar.transform.localPosition = new Vector3(offsets[i], 0f, 0f);
                bar.transform.localScale = new Vector3(0.028f, 0.022f, 0.72f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = ironMat;
            }
        }

        private void BuildCoalsBed(Transform parent, float coalBaseY)
        {
            var coalsRoot = new GameObject("Hearth_CoalsBed");
            coalsRoot.transform.SetParent(parent, false);
            coalsRoot.transform.localPosition = new Vector3(0f, coalBaseY, 0f);

            var coalMat = AnvilStationMeshBuilder.CreateMaterial("Mat_HearthCoalCharcoal", new Color(0.06f, 0.06f, 0.07f), 0.10f, 0.12f);
            _hearthEmbersMat = AnvilStationMeshBuilder.CreateMaterial(
                "Mat_HearthGlowingEmbers", new Color(0.12f, 0.04f, 0.02f), metallic: 0.1f, smoothness: 0.15f,
                emission: new Color(1.0f, 0.42f, 0.08f) * 1.8f);

            // Núcleo incandescente
            var coreGo = new GameObject("EmbersCore", typeof(MeshFilter), typeof(MeshRenderer));
            coreGo.transform.SetParent(coalsRoot.transform, false);
            coreGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            coreGo.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreatePolyhedralRockMesh(
                0.24f, 9, new Vector3(1.15f, 0.45f, 1.15f), 0.20f, 777, "HearthEmbersCore");
            coreGo.GetComponent<MeshRenderer>().sharedMaterial = _hearthEmbersMat;

            // Trozos de carbón
            for (int i = 0; i < 9; i++)
            {
                var chunk = new GameObject($"Coal_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                chunk.transform.SetParent(coalsRoot.transform, false);
                float ang = i * Mathf.PI * 2f / 9f;
                float r = (i % 2 == 0) ? 0.18f : 0.28f;
                chunk.transform.localPosition = new Vector3(Mathf.Cos(ang) * r, (i % 3) * 0.025f + 0.02f, Mathf.Sin(ang) * r);
                chunk.transform.localRotation = Quaternion.Euler(15f * i, ang * Mathf.Rad2Deg, 25f * i);
                chunk.GetComponent<MeshFilter>().sharedMesh = OreMeshGenerator.CreateFacetedChunkMesh(0.085f, 500 + i * 23);
                chunk.GetComponent<MeshRenderer>().sharedMaterial = (i % 3 == 0) ? _hearthEmbersMat : coalMat;
            }
        }

        private ParticleSystem CreateHearthParticles(string name, Transform parent, Vector3 localPos, bool isSparks)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial($"Mat_{name}", isAdditive: true);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = isSparks ? new ParticleSystem.MinMaxCurve(0.45f, 0.90f) : new ParticleSystem.MinMaxCurve(0.20f, 0.38f);
            main.startSpeed = isSparks ? new ParticleSystem.MinMaxCurve(0.4f, 0.9f) : new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
            main.startSize = isSparks ? new ParticleSystem.MinMaxCurve(0.015f, 0.035f) : new ParticleSystem.MinMaxCurve(0.05f, 0.10f);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = isSparks ? 0.20f : 0.26f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var em = ps.emission;
            em.rateOverTime = isSparks ? 1.5f : 3f;

            var colLife = ps.colorOverLifetime;
            colLife.enabled = true;
            Gradient grad = new Gradient();
            if (isSparks)
            {
                grad.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.88f, 0.38f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.05f), 0.7f), new GradientColorKey(new Color(0.4f, 0.1f, 0.02f), 1f) },
                    new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.65f, 0.6f), new GradientAlphaKey(0f, 1f) });
            }
            else
            {
                grad.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.80f, 0.28f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.06f), 0.5f), new GradientColorKey(new Color(0.4f, 0.06f, 0.02f), 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.2f), new GradientAlphaKey(0f, 1f) });
            }
            colLife.color = grad;
            return ps;
        }
    }
}

