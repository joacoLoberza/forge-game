using System.Collections;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Spawner y controlador físico de recortes desprendidos tras un corte en el yunque.
    /// Elimina el efecto "láser que borra" haciendo que las piezas separadas caigan físicamente
    /// con gravedad y se desvanezcan suavemente tras unos segundos.
    /// </summary>
    public static class AnvilOffcutSpawner
    {
        public static GameObject SpawnOffcut(Mesh offcutMesh, Transform sourceTransform, Material material, Vector3 cutDirection)
        {
            if (offcutMesh == null || sourceTransform == null) return null;

            GameObject offcut = new GameObject("SeveredOffcut");
            offcut.transform.position = sourceTransform.position;
            offcut.transform.rotation = sourceTransform.rotation;
            offcut.transform.localScale = sourceTransform.lossyScale;

            MeshFilter mf = offcut.AddComponent<MeshFilter>();
            mf.sharedMesh = offcutMesh;

            MeshRenderer mr = offcut.AddComponent<MeshRenderer>();
            if (material != null) mr.material = new Material(material);

            Vector3 worldOutward;
            if (cutDirection.y > 0.5f || cutDirection == Vector3.up)
            {
                worldOutward = Vector3.up;
            }
            else
            {
                Vector3 localOutward = cutDirection.sqrMagnitude > 0.001f ? cutDirection.normalized : Vector3.right;
                worldOutward = sourceTransform.TransformDirection(localOutward);
                worldOutward.y = 0f;
                if (worldOutward.sqrMagnitude > 0.001f) worldOutward.Normalize();
                else worldOutward = sourceTransform.right;
            }

            OffcutFader fader = offcut.AddComponent<OffcutFader>();
            fader.StartFade(mr, worldOutward, 2.5f);

            return offcut;
        }

        private class OffcutFader : MonoBehaviour
        {
            public void StartFade(MeshRenderer mr, Vector3 dir, float duration) => StartCoroutine(FadeRoutine(mr, dir, duration));

            private IEnumerator FadeRoutine(MeshRenderer mr, Vector3 dir, float duration)
            {
                Material instMat = mr != null ? mr.material : null;
                if (instMat != null)
                {
                    instMat.SetFloat("_Surface", 1);
                    instMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    instMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    instMat.SetInt("_ZWrite", 0);
                    instMat.DisableKeyword("_ALPHATEST_ON");
                    instMat.EnableKeyword("_ALPHABLEND_ON");
                    instMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    instMat.renderQueue = 3000;
                }

                Vector3 startPos = transform.position;
                Vector3 targetPos = (dir == Vector3.up) ? (startPos + Vector3.up * 0.08f) : (startPos + dir * 0.035f);

                float moveTime = 0.40f;
                float elapsedMove = 0f;
                while (elapsedMove < moveTime)
                {
                    elapsedMove += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsedMove / moveTime);
                    transform.position = Vector3.Lerp(startPos, targetPos, t);
                    yield return null;
                }
                transform.position = targetPos;

                float waitBeforeFade = duration * 0.20f;
                yield return new WaitForSeconds(waitBeforeFade);

                float fadeTime = duration * 0.80f;
                float elapsedFade = 0f;
                Color initCol = instMat != null ? instMat.color : Color.white;

                while (elapsedFade < fadeTime)
                {
                    elapsedFade += Time.deltaTime;
                    float alpha = Mathf.Clamp01(1f - (elapsedFade / fadeTime));
                    if (instMat != null)
                    {
                        Color c = initCol;
                        c.a = alpha;
                        instMat.color = c;
                        if (instMat.HasProperty("_BaseColor")) instMat.SetColor("_BaseColor", c);
                    }
                    yield return null;
                }

                Destroy(gameObject);
            }
        }
    }
}
