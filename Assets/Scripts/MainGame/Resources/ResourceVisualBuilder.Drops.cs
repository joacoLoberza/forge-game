using UnityEngine;

namespace ForjaGame.Resources
{
    public static partial class ResourceVisualBuilder
    {
        /// <summary>
        /// Aplica la apariencia física a los ítems caídos en el mundo:
        /// Un fragmento de roca matriz facetada con vetas e incrustaciones del mineral en crudo,
        /// reflejando fielmente la mena de origen para que el filtrado/fundición tenga sentido.
        /// </summary>
        public static void ApplyDroppedItemStyle(GameObject dropRoot, string itemId)
        {
            if (dropRoot == null) return;

            int seed = dropRoot.GetInstanceID();
            string idLower = itemId != null ? itemId.ToLowerInvariant() : "iron";

            // 1. Núcleo principal de roca base con silueta geológica formulada única por mineral
            var mf = dropRoot.GetComponent<MeshFilter>();
            if (mf == null) mf = dropRoot.AddComponent<MeshFilter>();
            var mr = dropRoot.GetComponent<MeshRenderer>();
            if (mr == null) mr = dropRoot.AddComponent<MeshRenderer>();

            mf.sharedMesh = OreMeshGenerator.CreateOreDropRockMesh(itemId, 0.35f, seed);
            mr.sharedMaterial = CreateRockMaterial();

            // 2. Limpieza de incrustaciones previas si se vuelve a estilizar
            for (int i = dropRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = dropRoot.transform.GetChild(i);
                if (child.name.StartsWith("OreIncrust_"))
                {
                    if (Application.isPlaying) Object.Destroy(child.gameObject);
                    else Object.DestroyImmediate(child.gameObject);
                }
            }

            // 3. Vetas e incrustaciones del mineral con geometría cristalina orgánica específica
            Material oreMat = CreateResourceMaterial(itemId);
            int incrustCount = idLower.Contains("gold") ? 2 : (idLower.Contains("coal") ? 2 : 3);
            for (int i = 0; i < incrustCount; i++)
            {
                GameObject incrust = new GameObject($"OreIncrust_{i}", typeof(MeshFilter), typeof(MeshRenderer));
                incrust.transform.SetParent(dropRoot.transform, false);

                float angle = i * Mathf.PI * 2f / incrustCount + 0.40f;
                float r = 0.09f + (i % 2) * 0.03f;
                float y = (i - 0.5f) * 0.05f;
                incrust.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                incrust.transform.localRotation = Quaternion.Euler(20f * (i + 1), angle * Mathf.Rad2Deg + 15f, 12f * i);

                Mesh oreMesh = OreMeshGenerator.CreateOreInclusionMesh(itemId, 0.16f, seed + i * 31);
                incrust.GetComponent<MeshFilter>().sharedMesh = oreMesh;
                incrust.GetComponent<MeshRenderer>().sharedMaterial = oreMat;
            }

            // 4. Collider físico de recolección ajustado
            var col = dropRoot.GetComponent<SphereCollider>();
            if (col == null) col = dropRoot.AddComponent<SphereCollider>();
            if (col != null) { col.isTrigger = true; col.radius = 0.45f; }

            // 5. Luz ambiental tenue del color del mineral
            var l = dropRoot.GetComponent<Light>();
            if (l == null) l = dropRoot.AddComponent<Light>();
            if (l != null)
            {
                l.type = LightType.Point;
                l.color = GetColorForResource(itemId);
                l.range = 2.4f;
                l.intensity = 0.85f;
            }
        }
    }
}
