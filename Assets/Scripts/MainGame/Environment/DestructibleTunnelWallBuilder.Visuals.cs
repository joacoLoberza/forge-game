using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    public static partial class DestructibleTunnelWallBuilder
    {
        private static void BuildWebVisual(Transform root, float span, float height, int seed)
        {
            Material mat = GetWebMaterial();
            float halfSpan = span * 0.56f;
            float rx = halfSpan * 0.98f, ry = height * 0.54f;

            // Dos telarañas orbitales completas superpuestas (frontal y posterior) con radios, espirales y filamentos de anclaje
            for (int layer = 0; layer < 2; layer++)
            {
                float zOff = layer == 0 ? -0.15f : 0.16f;
                Vector3 hub = new Vector3(layer == 0 ? -span * 0.05f : span * 0.06f, height * (layer == 0 ? 0.54f : 0.48f), zOff);
                int spokes = 18, rings = 7;
                var anchors = new Vector3[spokes];

                for (int i = 0; i < spokes; i++)
                {
                    float ang = (i / (float)spokes) * Mathf.PI * 2f + layer * 0.17f;
                    float jitter = 0.92f + Mathf.Abs(Mathf.Sin((i + seed) * 2.1f)) * 0.16f;
                    float ax = Mathf.Clamp(hub.x + Mathf.Cos(ang) * rx * jitter, -halfSpan, halfSpan);
                    float ay = Mathf.Clamp(hub.y + Mathf.Sin(ang) * ry * jitter, 0.05f, height * 1.04f);
                    anchors[i] = new Vector3(ax, ay, zOff + Mathf.Sin(i * 1.7f) * 0.08f);
                    CreateStrand(root, $"WebSpoke_{layer}_{i}", hub, anchors[i], layer == 0 ? 0.052f : 0.044f, mat);
                }

                for (int r = 1; r <= rings; r++)
                {
                    float ringT = Mathf.Pow(r / (float)rings, 0.88f);
                    for (int i = 0; i < spokes; i++)
                    {
                        int next = (i + 1) % spokes;
                        Vector3 pA = Vector3.Lerp(hub, anchors[i], ringT) + Vector3.down * (0.04f * r * Mathf.Abs(Mathf.Sin(i)));
                        Vector3 pB = Vector3.Lerp(hub, anchors[next], ringT) + Vector3.down * (0.04f * r * Mathf.Abs(Mathf.Sin(next)));
                        CreateStrand(root, $"WebRing_{layer}_{r}_{i}", pA, pB, r >= rings - 1 ? 0.042f : 0.030f, mat);
                    }
                }
            }

            // Filamentos cruzados gruesos entre arcos, suelo y laterales + hebras rasgadas de anclaje
            for (int d = 0; d < 24; d++)
            {
                float t = d / 23f;
                float x1 = (d % 2 == 0 ? -1f : 1f) * Mathf.Lerp(halfSpan * 0.55f, halfSpan, (d % 4) / 3f);
                float x2 = -x1 * Random.Range(0.55f, 0.98f);
                float y1 = Mathf.Lerp(0.12f, height * 0.98f, t);
                float y2 = Mathf.Clamp(y1 + ((d % 3) - 1) * height * 0.28f, 0.10f, height * 1.02f);
                CreateStrand(root, $"SilkCable_{d}", new Vector3(x1, y1, ((d % 3) - 1) * 0.18f), new Vector3(x2, y2, (((d + 1) % 3) - 1) * 0.18f), 0.036f, mat);
            }

            // Capullos de seda ahusados colgados entre los radios con ataduras en anillo
            for (int c = 0; c < 5; c++)
            {
                var cocoon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cocoon.name = $"WebCocoon_{c}";
                cocoon.transform.SetParent(root, false);
                UnitySafeUtility.DestroySafe(cocoon.GetComponent<Collider>());
                cocoon.GetComponent<MeshRenderer>().sharedMaterial = mat;
                float cx = (c - 2f) * (span * 0.19f), cy = height * (0.58f - (c % 2) * 0.14f);
                cocoon.transform.localPosition = new Vector3(cx, cy, (c % 2 == 0 ? -0.10f : 0.12f));
                cocoon.transform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), 0f, Random.Range(-14f, 14f));
                cocoon.transform.localScale = new Vector3(0.42f, 0.96f, 0.42f);
                CreateStrand(root, $"CocoonTether_{c}", new Vector3(cx, height, 0f), cocoon.transform.localPosition + Vector3.up * 0.42f, 0.042f, mat);
            }
        }

        private static void BuildWoodPlanksVisual(Transform root, float span, float height, int seed)
        {
            Material timberMat = CaveDungeonBuilder.GetMineTimberMaterial();
            float plankWidth = span * 1.10f; // Incrustado profundo en la roca lateral
            int plankCount = Mathf.Max(8, Mathf.CeilToInt(height / 0.60f));
            float stepY = height / (plankCount + 0.2f);

            // 1. Postes verticales de anclaje empotrados en roca y túnel
            for (int v = -2; v <= 2; v++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = $"TimberPost_{v + 2}";
                post.transform.SetParent(root, false);
                UnitySafeUtility.DestroySafe(post.GetComponent<Collider>());
                post.GetComponent<MeshRenderer>().sharedMaterial = timberMat;

                float vx = v * (span * 0.24f);
                post.transform.localPosition = new Vector3(vx, height * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.42f, height * 1.10f, 0.42f);
                post.AddComponent<Rigidbody>().isKinematic = true;
            }

            // 2. Tablones horizontales solapados sin fisuras
            for (int i = 0; i < plankCount; i++)
            {
                var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = $"TimberPlank_{i}";
                plank.transform.SetParent(root, false);
                UnitySafeUtility.DestroySafe(plank.GetComponent<Collider>());
                plank.GetComponent<MeshRenderer>().sharedMaterial = timberMat;

                float y = (i + 0.60f) * stepY;
                float zOff = (i % 2 == 0) ? -0.11f : 0.11f;
                float tiltZ = Mathf.Sin((i + seed) * 1.7f) * 2.2f;

                plank.transform.localPosition = new Vector3(0f, y, zOff);
                plank.transform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);
                plank.transform.localScale = new Vector3(plankWidth, stepY * 1.18f, 0.30f);
                plank.AddComponent<Rigidbody>().isKinematic = true;
            }

            // 3. Vigas diagonales de refuerzo en 'X'
            float diagLen = Mathf.Sqrt(plankWidth * plankWidth + height * height) * 0.99f;
            float angleDeg = Mathf.Atan2(height, plankWidth) * Mathf.Rad2Deg;

            for (int d = 0; d < 2; d++)
            {
                var brace = GameObject.CreatePrimitive(PrimitiveType.Cube);
                brace.name = $"TimberBrace_{d}";
                brace.transform.SetParent(root, false);
                UnitySafeUtility.DestroySafe(brace.GetComponent<Collider>());
                brace.GetComponent<MeshRenderer>().sharedMaterial = timberMat;

                float rotZ = (d == 0) ? angleDeg : -angleDeg;
                brace.transform.localPosition = new Vector3(0f, height * 0.5f, (d == 0) ? -0.25f : 0.25f);
                brace.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
                brace.transform.localScale = new Vector3(diagLen, 0.36f, 0.30f);
                brace.AddComponent<Rigidbody>().isKinematic = true;
            }
        }

        private static void BuildBrokenBricksVisual(Transform root, float span, float height, int seed)
        {
            Material rockMat = CaveDungeonBuilder.GetCaveRockMaterial();

            // Pared de piedras formada exclusivamente por bloques individuales de sillería rústica (sin panel base posterior)
            int rows = 8;
            int cols = Mathf.Max(9, Mathf.CeilToInt((span * 1.10f) / 1.45f));
            float blockW = (span * 1.10f) / cols;
            float blockH = height / rows;

            for (int r = 0; r < rows; r++)
            {
                float rowY = (r + 0.5f) * blockH;
                float rowOffset = (r % 2 == 0) ? 0f : (blockW * 0.5f);

                for (int c = 0; c < cols; c++)
                {
                    var brick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    brick.name = $"Brick_{r}_{c}";
                    brick.transform.SetParent(root, false);
                    UnitySafeUtility.DestroySafe(brick.GetComponent<Collider>());
                    brick.GetComponent<MeshRenderer>().sharedMaterial = rockMat;

                    float x = -span * 0.55f + (c + 0.5f) * blockW + rowOffset;
                    float z = ((r * 7 + c * 13 + seed) % 5 - 2) * 0.06f;
                    float tilt = ((r + c + seed) % 3 - 1) * 1.8f;

                    brick.transform.localPosition = new Vector3(x, rowY, z);
                    brick.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                    brick.transform.localScale = new Vector3(blockW * 0.98f, blockH * 0.96f, 0.72f);
                    brick.AddComponent<Rigidbody>().isKinematic = true;
                }
            }

            // Escombros y rocas caídas en la base de la pared
            for (int rub = 0; rub < 7; rub++)
            {
                var rubble = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rubble.name = $"BrickRubble_{rub}";
                rubble.transform.SetParent(root, false);
                UnitySafeUtility.DestroySafe(rubble.GetComponent<Collider>());
                rubble.GetComponent<MeshRenderer>().sharedMaterial = rockMat;

                float rx = (rub - 3f) * (span * 0.15f);
                rubble.transform.localPosition = new Vector3(rx, 0.24f, (rub % 2 == 0 ? -0.45f : 0.45f));
                rubble.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 180f), Random.Range(-15f, 15f));
                rubble.transform.localScale = new Vector3(blockW * 0.75f, blockH * 0.60f, 0.55f);
                rubble.AddComponent<Rigidbody>().isKinematic = true;
            }
        }
    }
}

