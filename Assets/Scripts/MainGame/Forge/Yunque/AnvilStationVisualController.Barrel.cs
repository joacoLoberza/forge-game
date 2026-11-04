using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Construcción y renderizado procedural del Barril de Templado en agua para el yunque.
    /// Posicionado a la derecha de la estación donde el filo del arma se sumerge para templar.
    /// Emite vapor denso y siseo durante la secuencia de templado diferencial.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    public partial class AnvilStationVisualController
    {
        private void BuildBarrelVisual()
        {
            float groundY = ForgeStationModelSetup.GetGroundHeightAt(transform.position.x, transform.position.z) + 0.02f;
            float localGroundY = transform.InverseTransformPoint(new Vector3(transform.position.x, groundY, transform.position.z)).y;

            Vector3 waterBasinOffset = (_quench != null) ? _quench.WaterBasinOffset : new Vector3(1.85f, -0.12f, 0.15f);
            float waterTargetLocalY = waterBasinOffset.y;
            float barrelTopY = waterTargetLocalY + 0.045f;
            float barrelHeight = Mathf.Max(0.72f, barrelTopY - localGroundY);

            _barrelRoot = new GameObject("Barrel_QuenchBasin").transform;
            _barrelRoot.SetParent(_toolsRoot, false);
            _barrelRoot.localPosition = new Vector3(waterBasinOffset.x, localGroundY, waterBasinOffset.z);
            _barrelRoot.localRotation = Quaternion.identity;

            // 1. Cuerpo de madera de roble con duelas abombadas e interior completo
            var bodyGo = new GameObject("Barrel_WoodStaves", typeof(MeshFilter), typeof(MeshRenderer));
            bodyGo.transform.SetParent(_barrelRoot, false);
            bodyGo.transform.localPosition = Vector3.zero;
            bodyGo.GetComponent<MeshFilter>().sharedMesh = AnvilStationMeshBuilder.CreateBarrelMesh(0.28f, 0.35f, 0.29f, barrelHeight, 18);
            bodyGo.GetComponent<MeshRenderer>().sharedMaterial = AnvilStationMeshBuilder.CreateMaterial(
                "Mat_QuenchBarrelWood", new Color(0.28f, 0.18f, 0.11f), metallic: 0.05f, smoothness: 0.25f);

            // 2. Aros de hierro forjado
            BuildBarrelHoops(_barrelRoot, barrelHeight);

            // 3. Superficie del agua elevada cerca del borde superior
            float waterRelY = barrelHeight - 0.045f;
            var waterGo = new GameObject("Barrel_WaterSurface", typeof(MeshFilter), typeof(MeshRenderer));
            waterGo.transform.SetParent(_barrelRoot, false);
            waterGo.transform.localPosition = new Vector3(0f, waterRelY, 0f);
            waterGo.GetComponent<MeshFilter>().sharedMesh = AnvilStationMeshBuilder.CreateWaterDiscMesh(0.252f, 18);
            waterGo.GetComponent<MeshRenderer>().sharedMaterial = AnvilStationMeshBuilder.CreateQuenchWaterMaterial();

            if (_quench != null)
            {
                _quench.SetWaterBasinOffset(new Vector3(waterBasinOffset.x, localGroundY + waterRelY, waterBasinOffset.z));
            }

            // 4. Emisor de vapor de templado suave y atmosférico
            _quenchSteamPs = CreateQuenchSteamParticles("QuenchSteam_VFX", _barrelRoot, new Vector3(0f, waterRelY + 0.02f, 0f));
        }

        private void BuildBarrelHoops(Transform parent, float barrelHeight)
        {
            var ironMat = AnvilStationMeshBuilder.CreateMaterial(
                "Mat_QuenchHoopIron", new Color(0.16f, 0.16f, 0.18f), metallic: 0.88f, smoothness: 0.40f);

            float[] hoopHeights = { 0.04f, barrelHeight * 0.35f, barrelHeight * 0.65f, barrelHeight - 0.045f };
            float[] hoopRadii = { 0.282f, 0.342f, 0.342f, 0.292f };

            for (int i = 0; i < hoopHeights.Length; i++)
            {
                var hoopGo = new GameObject($"Hoop_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                hoopGo.transform.SetParent(parent, false);
                hoopGo.transform.localPosition = new Vector3(0f, hoopHeights[i], 0f);
                hoopGo.GetComponent<MeshFilter>().sharedMesh = AnvilStationMeshBuilder.CreateHoopMesh(hoopRadii[i], 0.032f, 0.012f, 18);
                hoopGo.GetComponent<MeshRenderer>().sharedMaterial = ironMat;
            }
        }

        private ParticleSystem CreateQuenchSteamParticles(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();

            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sharedMaterial = AnvilStationMeshBuilder.CreateParticleMaterial($"Mat_{name}", isAdditive: false);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.16f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var em = ps.emission;
            em.rateOverTime = 0f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.35f);
            curve.AddKey(1f, 1.30f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var rotOverLife = ps.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var colLife = ps.colorOverLifetime;
            colLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.95f, 0.96f, 0.98f), 0f), new GradientColorKey(new Color(0.85f, 0.88f, 0.92f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.26f, 0.18f), new GradientAlphaKey(0f, 1f) });
            colLife.color = grad;

            return ps;
        }
    }
}

