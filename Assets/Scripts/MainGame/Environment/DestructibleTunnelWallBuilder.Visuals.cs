using UnityEngine;
using ForjaGame.Core;

namespace ForjaGame.Environment
{
    public static partial class DestructibleTunnelWallBuilder
    {
        private static void BuildWebVisual(Transform root, float span, float height, int seed)
        {
            Material mat = GetWebMaterial();
            float halfSpan = span * 0.52f;

            // 1. Cables y filamentos diagonales cruzados de extremo a extremo de la roca
            int strands = 16;
            for (int i = 0; i < strands; i++)
            {
                float t = i / (float)(strands - 1);
                float y1 = Mathf.Lerp(0.2f, height * 0.96f, t);
                float y2 = Mathf.Lerp(height * 0.96f, 0.2f, t);
                float x1 = (i % 2 == 0) ? -halfSpan : halfSpan;
                float x2 = (i % 2 == 0) ? halfSpan : -halfSpan;

                Vector3 p1 = new Vector3(x1, y1, (i % 3 - 1) * 0.14f);
                Vector3 p2 = new Vector3(x2, y2, (i % 3 - 1) * 0.14f);
                CreateStrand(root, $"SilkStrand_{i}", p1, p2, 0.10f, mat);
            }

            // 2. Láminas densas de seda continua sellando herméticamente todo el ancho del túnel
            int sheets = Mathf.Max(8, Mathf.CeilToInt(span / 1.7f));
            float stepX = span / sheets;
            for (int s = 0; s < sheets; s++)
            {
                float x = -span * 0.5f + (s + 0.5f) * stepX;
                var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sheet.name = $"WebSheet_{s}";
                sheet.transform.SetParent(root, false);
                UnitySafeUtility.DestroySafe(sheet.GetComponent<Collider>());
                sheet.GetComponent<MeshRenderer>().sharedMaterial = mat;

                sheet.transform.localPosition = new Vector3(x, height * 0.50f, ((s % 3) - 1) * 0.10f);
                sheet.transform.localRotation = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-5f, 5f), Random.Range(-8f, 8f));
                sheet.transform.localScale = new Vector3(stepX * 1.40f, height * 1.05f, 0.08f);
            }

            // 3. Capullos de presas colgando de la telaraña
            for (int c = 0; c < 4; c++)
            {
                var cocoon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cocoon.name = $"WebCocoon_{c}";
                cocoon.transform.SetParent(root, false);
                UnitySafeUtility.DestroySafe(cocoon.GetComponent<Collider>());
                cocoon.GetComponent<MeshRenderer>().sharedMaterial = mat;

                float cx = (c - 1.5f) * (span * 0.22f);
                float cy = height * 0.62f - (c % 2) * 0.45f;
                cocoon.transform.localPosition = new Vector3(cx, cy, 0.12f);
                cocoon.transform.localScale = new Vector3(0.55f, 1.10f, 0.55f);
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

