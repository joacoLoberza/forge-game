using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    public struct HandleIntegrityResult
    {
        public bool isValid;
        public bool hasBrokenSection;
        public float breakX;
        public float tangX0, tangX1, tangCenterZ;
        public float gripX0, gripX1, gripCenterZ;
        public float tangLength;
        public float effectiveGripLength;
        public float tangDiameter, gripDiameter;
        public string statusMessage;
    }

    /// <summary>
    /// Evaluador y visualizador de integridad para el modelado de mangos en el yunque:
    /// 1. Longitud para el ojo del hacha = alto del lingote en Z (L_eye).
    /// 2. Búsqueda adaptativa deslizable de zonas continuas y conectadas (Espiga + Agarre).
    /// 3. Detección de secciones inútiles (> 20 cm) y corte de quebradura automático.
    /// </summary>
    public static class AnvilHandleCarver
    {
        public const float MinGripDiameter = 0.020f; // 2.0 cm agarre esbelto y cómodo
        public const float MaxGripDiameter = 0.140f; // 14.0 cm máximo tolerante
        public const float DefaultTangDiameter = 0.020f; // 2.0 cm espiga ojo de hacha adaptada
        public const float MaxTangDiameter = 0.140f; // 14.0 cm máximo ojo del hacha
        public const float BreakWidthThreshold = 0.010f; // 1.0 cm - Solo tramo < 1 cm durante >= 20 cm quiebra
        public const float MaxUselessSpan = 0.20f; // Tramo débil continuo >= 20 cm -> rotura al centro

        public static bool GetPerimeterSliceZ(float x, List<Vector2> perimPoly, out float minZ, out float maxZ)
        {
            minZ = float.MaxValue; maxZ = float.MinValue;
            if (perimPoly == null || perimPoly.Count < 3) return false;
            for (int i = 0; i < perimPoly.Count; i++)
            {
                Vector2 p1 = perimPoly[i], p2 = perimPoly[(i + 1) % perimPoly.Count];
                if ((p1.x <= x && p2.x >= x) || (p2.x <= x && p1.x >= x))
                {
                    float dx = p2.x - p1.x;
                    float t = Mathf.Abs(dx) > 1e-5f ? Mathf.Clamp01((x - p1.x) / dx) : 0f;
                    float z = Mathf.Lerp(p1.y, p2.y, t);
                    if (z < minZ) minZ = z;
                    if (z > maxZ) maxZ = z;
                }
            }
            return maxZ > minZ + 1e-4f;
        }

        public static HandleIntegrityResult ValidateHandle(
            Vector3[] columns, int[] perimIndices, float boardLength = 1.60f, float ingotHeight = 0.24f)
        {
            HandleIntegrityResult res = new HandleIntegrityResult
            {
                isValid = true,
                tangLength = 0.18f,
                statusMessage = "Mango Válido"
            };

            if (columns == null || columns.Length < 6)
            {
                res.isValid = false;
                res.statusMessage = "Mango destruido o sin masa suficiente.";
                return res;
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            for (int k = 0; k < columns.Length; k++)
            {
                if (columns[k].x < minX) minX = columns[k].x;
                if (columns[k].x > maxX) maxX = columns[k].x;
            }

            float totalLength = maxX - minX;
            if (totalLength < 0.35f)
            {
                res.isValid = false;
                res.statusMessage = "<color=#FF4444>¡MANGO DEMASIADO CORTO! Requiere al menos 40 cm de longitud.</color>";
                return res;
            }

            List<Vector2> perimPoly = new List<Vector2>();
            if (perimIndices != null)
            {
                for (int i = 0; i < perimIndices.Length; i++)
                {
                    int k = perimIndices[i];
                    if (k >= 0 && k < columns.Length) perimPoly.Add(new Vector2(columns[k].x, columns[k].z));
                }
            }

            res.tangX1 = maxX - 0.02f;
            res.tangX0 = res.tangX1 - res.tangLength;
            res.gripX0 = minX + 0.02f;
            res.gripX1 = Mathf.Min(res.tangX0 - 0.03f, res.gripX0 + 0.28f);
            res.effectiveGripLength = res.gripX1 - res.gripX0;

            float step = 0.02f;
            float totalSpan = 0f;
            int samples = 0;
            float curWeakStart = float.NaN, maxWeak = 0f, wStart = 0f, wEnd = 0f;

            for (float sx = minX + 0.02f; sx <= maxX - 0.02f; sx += step)
            {
                if (GetPerimeterSliceZ(sx, perimPoly, out float z0, out float z1))
                {
                    float span = z1 - z0;
                    totalSpan += span;
                    samples++;

                    if (span < BreakWidthThreshold)
                    {
                        if (float.IsNaN(curWeakStart)) curWeakStart = sx;
                        float weakL = sx - curWeakStart;
                        if (weakL > maxWeak) { maxWeak = weakL; wStart = curWeakStart; wEnd = sx; }
                    }
                    else curWeakStart = float.NaN;
                }
            }

            float avgWidth = samples > 0 ? (totalSpan / samples) : 0.14f;
            res.gripDiameter = avgWidth;
            res.tangDiameter = avgWidth;

            if (GetPerimeterSliceZ((res.tangX0 + res.tangX1) * 0.5f, perimPoly, out float tz0, out float tz1))
                res.tangCenterZ = (tz0 + tz1) * 0.5f;
            if (GetPerimeterSliceZ((res.gripX0 + res.gripX1) * 0.5f, perimPoly, out float gz0, out float gz1))
                res.gripCenterZ = (gz0 + gz1) * 0.5f;

            if (maxWeak >= MaxUselessSpan)
            {
                res.hasBrokenSection = true;
                res.isValid = false;
                res.breakX = (wStart + wEnd) * 0.5f;
                res.statusMessage = "<color=#FF4444>¡MANGO QUEBRADO! Tramo excesivamente débil (< 1.0 cm) por más de 20 cm.</color>";
                return res;
            }

            float widthCm = avgWidth * 100f;
            if (avgWidth > 0.065f)
            {
                res.isValid = true; // Inicia como mango válido, pero se recalca afinarlo
                res.statusMessage = $"<color=#FFCC00><b>TABLA EN BRUTO ({widthCm:F1} cm):</b> Afina los bordes cortando o lijando (ideal: 3.5 a 5.5 cm) para que deje de ser una tabla y tenga forma de mango.</color>";
            }
            else if (avgWidth >= 0.028f)
            {
                res.isValid = true;
                res.statusMessage = $"<color=#33FFAA><b>¡MANGO REFINADO ({widthCm:F1} cm)!</b> Ancho ergonómico ideal. Presiona [ESPACIO] para ensamblar.</color>";
            }
            else
            {
                res.isValid = avgWidth >= 0.018f;
                res.statusMessage = $"<color=#FFAA33><b>MANGO MUY DELGADO ({widthCm:F1} cm):</b> Precaución: mango esbelto y frágil (mínimo recomendado: 3.0 cm).</color>";
            }

            return res;
        }

        public static List<Vector3> GenerateFractureCut(float breakX, float minZ, float maxZ)
        {
            float z0 = minZ - 0.05f, z1 = maxZ + 0.05f, midZ = (minZ + maxZ) * 0.5f;
            return new List<Vector3>
            {
                new Vector3(breakX - 0.008f, 0f, z0),
                new Vector3(breakX + 0.008f, 0f, midZ),
                new Vector3(breakX - 0.006f, 0f, z1)
            };
        }

        public static void DrawRestrictionGuides(
            Transform boardT, Camera cam, float yTop, float yBot,
            System.Action<Vector3, Vector3, Color, float> drawLine, HandleIntegrityResult res, float boardLength = 1.35f)
        {
            if (boardT == null || cam == null || drawLine == null) return;

            float tangX1 = (res.tangX1 != 0f || res.tangX0 != 0f) ? res.tangX1 : (boardLength * 0.5f - 0.02f);
            float tangX0 = (res.tangX1 != 0f || res.tangX0 != 0f) ? res.tangX0 : (tangX1 - res.tangLength);
            float tangZ = res.tangCenterZ;

            float gripX0 = (res.gripX1 != 0f || res.gripX0 != 0f) ? res.gripX0 : (-boardLength * 0.5f + 0.02f);
            float gripX1 = (res.gripX1 != 0f || res.gripX0 != 0f) ? res.gripX1 : (gripX0 + 0.25f);
            float gripZ = res.gripCenterZ;

            // Área Mínima del Lingote (Ojo): Color Cyan / Azul para indicar encastre del lingote
            Color tangCol = res.isValid ? new Color(0.20f, 0.85f, 1.0f, 0.90f) : new Color(1.0f, 0.35f, 0.20f, 0.85f);
            // Área Mínima del Agarre: Color Dorado / Amarillo para zona donde agarra la mano
            Color gripCol = new Color(1.0f, 0.82f, 0.20f, 0.85f);

            float halfTangW = res.tangDiameter * 0.5f;
            Draw3DBox(boardT, tangX0, tangX1, tangZ - halfTangW, tangZ + halfTangW, yTop, yBot, tangCol, drawLine);

            float halfGripW = res.gripDiameter * 0.5f;
            Draw3DBox(boardT, gripX0, gripX1, gripZ - halfGripW, gripZ + halfGripW, yTop, yBot, gripCol, drawLine);
        }

        private static void Draw3DBox(
            Transform t, float x0, float x1, float z0, float z1, float yTop, float yBot,
            Color col, System.Action<Vector3, Vector3, Color, float> drawLine)
        {
            Vector3 t0 = t.TransformPoint(new Vector3(x0, yTop, z0)), t1 = t.TransformPoint(new Vector3(x1, yTop, z0));
            Vector3 t2 = t.TransformPoint(new Vector3(x1, yTop, z1)), t3 = t.TransformPoint(new Vector3(x0, yTop, z1));
            Vector3 b0 = t.TransformPoint(new Vector3(x0, yBot, z0)), b1 = t.TransformPoint(new Vector3(x1, yBot, z0));
            Vector3 b2 = t.TransformPoint(new Vector3(x1, yBot, z1)), b3 = t.TransformPoint(new Vector3(x0, yBot, z1));

            drawLine(t0, t1, col, 2.2f); drawLine(t1, t2, col, 2.2f);
            drawLine(t2, t3, col, 2.2f); drawLine(t3, t0, col, 2.2f);

            Color botCol = new Color(col.r * 0.7f, col.g * 0.7f, col.b * 0.7f, col.a * 0.75f);
            drawLine(b0, b1, botCol, 1.8f); drawLine(b1, b2, botCol, 1.8f);
            drawLine(b2, b3, botCol, 1.8f); drawLine(b3, b0, botCol, 1.8f);

            Color pilCol = new Color(col.r, col.g, col.b, col.a * 0.85f);
            drawLine(t0, b0, pilCol, 2f); drawLine(t1, b1, pilCol, 2f);
            drawLine(t2, b2, pilCol, 2f); drawLine(t3, b3, pilCol, 2f);
        }
    }
}
