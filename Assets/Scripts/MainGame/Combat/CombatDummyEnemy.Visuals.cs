using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class CombatDummyEnemy
    {
        private void SetupMaterial()
        {
            if (_renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _bodyMaterial = new Material(shader)
                {
                    name = "EnemyBody_Mat",
                    color = GetDynamicNormalColor()
                };
                if (_bodyMaterial.HasProperty("_BaseColor")) _bodyMaterial.SetColor("_BaseColor", GetDynamicNormalColor());
                _renderer.sharedMaterial = _bodyMaterial;
            }
        }

        private void SetupHitboxes()
        {
            // 1. Hurtbox corporal (Cápsula centrada exactamente en el modelo)
            GameObject bodyObj = new GameObject("EnemyBodyHurtbox");
            bodyObj.transform.SetParent(transform, false);
            _bodyHurtbox = bodyObj.AddComponent<HitboxVisualizer>();
            _bodyHurtbox.SetShape(HitboxShape.Capsule);
            _bodyHurtbox.UpdateCapsule(Vector3.zero, 0.5f, 2.0f);

            // 2. Hitbox de ataque frontal (Alineada al torso frontal del enemigo)
            GameObject atkObj = new GameObject("EnemyAttackHitbox");
            atkObj.transform.SetParent(transform, false);
            _attackHitbox = atkObj.AddComponent<HitboxVisualizer>();
            _attackHitbox.SetShape(HitboxShape.Box);
            _attackHitbox.UpdateBox(new Vector3(0f, 0f, 1.0f), new Vector3(1.2f, 1.2f, 1.5f));
            _attackHitbox.IsVolumeActive = false;
        }

        /// <summary>
        /// Calcula el color normal (rojo) oscurecido proporcionalmente a la salud restante (sin barra de vida).
        /// </summary>
        public Color GetDynamicNormalColor()
        {
            float hpRatio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
            // De rojo brillante a 100% hasta rojo tizón/negruzco casi apagado al borde de morir
            Color deadDarkRed = new Color(0.12f, 0.03f, 0.03f, 1.0f);
            return Color.Lerp(deadDarkRed, _baseNormalColor, Mathf.Lerp(0.15f, 1.0f, hpRatio));
        }

        /// <summary>
        /// Calcula el color de aviso previo al golpe (azul) oscurecido proporcionalmente a la salud restante.
        /// </summary>
        public Color GetDynamicTelegraphColor()
        {
            float hpRatio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
            // De azul eléctrico intenso a 100% hasta azul marino muy oscuro y apagado a poca vida
            Color deadDarkBlue = new Color(0.02f, 0.06f, 0.18f, 1.0f);
            return Color.Lerp(deadDarkBlue, _baseTelegraphColor, Mathf.Lerp(0.15f, 1.0f, hpRatio));
        }

        public void RefreshCurrentColor()
        {
            if (_isTelegraphing) SetBodyColor(GetDynamicTelegraphColor());
            else SetBodyColor(GetDynamicNormalColor());
        }

        public void SetBodyColor(Color color)
        {
            if (_bodyMaterial != null)
            {
                _bodyMaterial.color = color;
                if (_bodyMaterial.HasProperty("_BaseColor")) _bodyMaterial.SetColor("_BaseColor", color);
            }
        }

        /// <summary>
        /// Genera el área 2D plana circular sobre el suelo en el punto de spawn definido:
        /// 1. Zona central de spawn (disco terracota).
        /// 2. Zona de detección / aggro circundante (anillo y disco translúcido dorado delimitador).
        /// </summary>
        private void Create2DFloorArea()
        {
            if (_floorZoneObj != null) return;

            _floorZoneObj = new GameObject("EnemySpawnFloorArea_2D");
            _floorZoneObj.transform.position = new Vector3(mapSpawnPoint.x, 0.03f, mapSpawnPoint.z);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");

            // 1. Disco de spawn central
            GameObject spawnPad = new GameObject("SpawnPad");
            spawnPad.transform.SetParent(_floorZoneObj.transform, false);
            var mfPad = spawnPad.AddComponent<MeshFilter>();
            var mrPad = spawnPad.AddComponent<MeshRenderer>();
            mrPad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrPad.receiveShadows = false;
            mfPad.sharedMesh = GenerateCircleMesh(spawnZoneRadius, 36);
            mrPad.sharedMaterial = CreateTranslucentMaterial(shader, new Color(0.85f, 0.25f, 0.15f, 0.38f), "EnemySpawnPad_Mat");

            // 2. Anillo delimitador del radio de detección (Aggro Zone Perimeter)
            GameObject detectRing = new GameObject("DetectionPerimeterRing");
            detectRing.transform.SetParent(_floorZoneObj.transform, false);
            var mfRing = detectRing.AddComponent<MeshFilter>();
            var mrRing = detectRing.AddComponent<MeshRenderer>();
            mrRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrRing.receiveShadows = false;
            mfRing.sharedMesh = GenerateRingMesh(detectionRadius - 0.18f, detectionRadius + 0.18f, 54);
            mrRing.sharedMaterial = CreateTranslucentMaterial(shader, new Color(0.95f, 0.68f, 0.10f, 0.45f), "EnemyDetectRing_Mat");

            // 3. Disco sutil del área de detección completa
            GameObject detectFill = new GameObject("DetectionAreaFill");
            detectFill.transform.SetParent(_floorZoneObj.transform, false);
            detectFill.transform.localPosition = new Vector3(0f, -0.005f, 0f);
            var mfFill = detectFill.AddComponent<MeshFilter>();
            var mrFill = detectFill.AddComponent<MeshRenderer>();
            mrFill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrFill.receiveShadows = false;
            mfFill.sharedMesh = GenerateCircleMesh(detectionRadius, 48);
            mrFill.sharedMaterial = CreateTranslucentMaterial(shader, new Color(0.95f, 0.68f, 0.10f, 0.07f), "EnemyDetectFill_Mat");
        }

        private static Material CreateTranslucentMaterial(Shader shader, Color col, string matName)
        {
            Material mat = new Material(shader) { name = matName };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 2999;
            return mat;
        }

        private static Mesh GenerateCircleMesh(float radius, int segments)
        {
            Mesh mesh = new Mesh { name = "CircleMesh" };
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            float step = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float rad = Mathf.Deg2Rad * (i * step);
                vertices[i + 1] = new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);

                int next = (i + 1) % segments;
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = next + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh GenerateRingMesh(float innerRadius, float outerRadius, int segments)
        {
            Mesh mesh = new Mesh { name = "RingMesh" };
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];

            float step = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float rad = Mathf.Deg2Rad * (i * step);
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                int idxInner = i * 2;
                int idxOuter = i * 2 + 1;

                vertices[idxInner] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                vertices[idxOuter] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);

                int nextI = (i + 1) % segments;
                int nextInner = nextI * 2;
                int nextOuter = nextI * 2 + 1;

                int triBase = i * 6;
                triangles[triBase + 0] = idxInner;
                triangles[triBase + 1] = nextInner;
                triangles[triBase + 2] = idxOuter;

                triangles[triBase + 3] = idxOuter;
                triangles[triBase + 4] = nextInner;
                triangles[triBase + 5] = nextOuter;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
