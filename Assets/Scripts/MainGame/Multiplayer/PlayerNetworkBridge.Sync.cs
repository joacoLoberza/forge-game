using UnityEngine;
using Unity.Netcode;
using ForjaGame.Combat;
using ForjaGame.Forge;

namespace ForjaGame.Multiplayer
{
    /// <summary>
    /// Sincronización de red para ataques, combos y armas forjadas (hoja y mango procedurales).
    /// </summary>
    public partial class PlayerNetworkBridge
    {
        private void HandleLocalAttackTriggered(int comboIndex, int weaponType, float speedMult)
        {
            if (IsOwner && IsSpawned) TriggerAttackServerRpc(comboIndex, weaponType, speedMult);
        }

        private void HandleLocalAttackEnded()
        {
            if (IsOwner && IsSpawned) EndAttackServerRpc();
        }

        private void HandleLocalWeaponEquipped(WeaponStats weapon)
        {
            if (!IsOwner || !IsSpawned || weapon == null) return;

            Color alloyCol = new Color(0.74f, 0.77f, 0.82f);
            if (weapon.customBladeMaterial != null) alloyCol = weapon.customBladeMaterial.color;
            else if (weapon.composition?.ingredients?.Count > 0)
                alloyCol = MetalProperties.GetMetalColor(weapon.composition.CalculateWeightedProperties().metalType);

            short[] qVerts = (weapon.weaponType != WeaponType.Pickaxe) ? QuantizeMeshVertices(weapon.customMesh) : System.Array.Empty<short>();
            ushort[] qTris = (weapon.weaponType != WeaponType.Pickaxe) ? QuantizeMeshTriangles(weapon.customMesh) : System.Array.Empty<ushort>();

            bool isCustomHandle = weapon.customHandleMesh != null && !weapon.customHandleMesh.name.StartsWith("Archetype");
            short[] qHVerts = isCustomHandle ? QuantizeMeshVertices(weapon.customHandleMesh) : System.Array.Empty<short>();
            ushort[] qHTris = isCustomHandle ? QuantizeMeshTriangles(weapon.customHandleMesh) : System.Array.Empty<ushort>();

            EquipWeaponServerRpc(
                (int)weapon.weaponType, (int)weapon.handleLength, (int)weapon.ingotSize,
                weapon.baseDamage, weapon.attackRange, weapon.attackSpeed, weapon.quality,
                alloyCol.r, alloyCol.g, alloyCol.b,
                weapon.bladeMountPosition, weapon.bladeMountRotation, weapon.gripCenterOffset, weapon.isFrontFaceDominant,
                qVerts, qTris, qHVerts, qHTris
            );
        }

        private void HandleLocalWeaponUnequipped()
        {
            if (IsOwner && IsSpawned) UnequipWeaponServerRpc();
        }

        [ServerRpc]
        private void TriggerAttackServerRpc(int comboIndex, int weaponType, float speedMult) =>
            TriggerAttackClientRpc(comboIndex, weaponType, speedMult);

        [ClientRpc]
        private void TriggerAttackClientRpc(int comboIndex, int weaponType, float speedMult)
        {
            if (IsOwner || equipmentController == null) return;
            var anim = equipmentController.PaladinAnimator;
            if (anim == null) return;

            anim.SetBool("MirrorAttack", false);
            anim.SetFloat("AttackSpeed", speedMult);
            anim.SetInteger("ComboIndex", comboIndex);
            anim.SetBool("IsAttacking", true);

            bool isPickaxe = (WeaponType)weaponType == WeaponType.Pickaxe;
            if (isPickaxe)
            {
                anim.CrossFadeInFixedTime("Attack1", 0.10f, 0);
            }
            else
            {
                int layer = anim.layerCount > 1 ? 1 : 0;
                if (layer == 1) anim.SetLayerWeight(1, 1f);
                anim.CrossFadeInFixedTime($"Attack{Mathf.Clamp(comboIndex, 0, 4) + 1}", 0.12f, layer);
            }
        }

        [ServerRpc]
        private void EndAttackServerRpc() => EndAttackClientRpc();

        [ClientRpc]
        private void EndAttackClientRpc()
        {
            if (IsOwner || equipmentController == null) return;
            var anim = equipmentController.PaladinAnimator;
            if (anim == null) return;

            anim.SetBool("IsAttacking", false);
            anim.ResetTrigger("Attack");

            int combatLayer = (anim.layerCount > 1) ? 1 : 0;
            if (combatLayer == 1)
            {
                var cur = equipmentController.CurrentWeapon;
                bool is2H = cur != null && cur.GetCombatStance() != WeaponCombatStance.OneHanded;
                float initWeight = cur != null && cur.weaponType == WeaponType.Pickaxe ? 0.18f : (is2H ? 0.42f : 0.22f);
                anim.CrossFadeInFixedTime("CombatStance", 0.20f, 1);
                anim.SetLayerWeight(1, initWeight);
            }
            else
            {
                anim.CrossFadeInFixedTime("Locomotion", 0.18f, 0);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestWeaponSyncServerRpc(ulong requesterClientId)
        {
            if (equipmentController?.CurrentWeapon != null)
                HandleLocalWeaponEquipped(equipmentController.CurrentWeapon);
            else
                RequestWeaponSyncClientRpc();
        }

        [ClientRpc]
        private void RequestWeaponSyncClientRpc()
        {
            if (IsOwner && equipmentController?.CurrentWeapon != null)
                HandleLocalWeaponEquipped(equipmentController.CurrentWeapon);
        }

        [ServerRpc]
        private void EquipWeaponServerRpc(
            int weaponType, int handleLength, int ingotSize,
            float dmg, float range, float speed, float quality,
            float colR, float colG, float colB,
            Vector3 mountPos, Quaternion mountRot, Vector3 gripOffset, bool frontDominant,
            short[] qVerts, ushort[] qTris, short[] qHVerts, ushort[] qHTris)
        {
            EquipWeaponClientRpc(
                weaponType, handleLength, ingotSize,
                dmg, range, speed, quality,
                colR, colG, colB,
                mountPos, mountRot, gripOffset, frontDominant,
                qVerts, qTris, qHVerts, qHTris);
        }

        [ClientRpc]
        private void EquipWeaponClientRpc(
            int weaponType, int handleLength, int ingotSize,
            float dmg, float range, float speed, float quality,
            float colR, float colG, float colB,
            Vector3 mountPos, Quaternion mountRot, Vector3 gripOffset, bool frontDominant,
            short[] qVerts, ushort[] qTris, short[] qHVerts, ushort[] qHTris)
        {
            if (IsOwner || equipmentController == null) return;
            var wType = (WeaponType)weaponType;
            if (wType == WeaponType.Pickaxe)
            {
                equipmentController.EquipWeapon(PickaxeModelGenerator.CreateDefaultIronPickaxe());
                return;
            }

            Color syncedColor = new Color(colR, colG, colB, 1f);
            WeaponStats remote = new WeaponStats
            {
                weaponName = $"Synced_{wType}",
                weaponType = wType,
                handleLength = (HandleLength)handleLength,
                ingotSize = (ItemSize)ingotSize,
                baseDamage = dmg, attackRange = range, attackSpeed = speed,
                quality = Mathf.Clamp01(quality > 0f ? quality : 0.85f),
                maxSharpness = 100f, currentSharpness = 100f,
                bladeMountPosition = mountPos, bladeMountRotation = mountRot,
                gripCenterOffset = gripOffset, isFrontFaceDominant = frontDominant
            };

            remote.customBladeMaterial = WeaponVisualBuilder.CreateAlloyBladeMaterial(remote, syncedColor);
            remote.customMesh = DequantizeMesh(qVerts, qTris, $"RemoteForgedBlade_{wType}");
            remote.customHandleMesh = DequantizeMesh(qHVerts, qHTris, $"RemoteForgedHandle_{wType}")
                ?? WeaponVisualBuilder.GenerateArchetypeHandle(remote.handleLength, remote.weaponType);

            equipmentController.EquipWeapon(remote);
        }

        [ServerRpc] private void UnequipWeaponServerRpc() => UnequipWeaponClientRpc();
        [ClientRpc] private void UnequipWeaponClientRpc() { if (!IsOwner && equipmentController != null) equipmentController.UnequipWeapon(); }

        private static short[] QuantizeMeshVertices(Mesh mesh)
        {
            if (mesh == null || mesh.vertexCount == 0 || mesh.vertexCount > 3000) return System.Array.Empty<short>();
            Vector3[] v = mesh.vertices;
            short[] q = new short[v.Length * 3];
            for (int i = 0; i < v.Length; i++)
            {
                q[i * 3]     = (short)Mathf.Clamp(Mathf.RoundToInt(v[i].x * 1000f), short.MinValue, short.MaxValue);
                q[i * 3 + 1] = (short)Mathf.Clamp(Mathf.RoundToInt(v[i].y * 1000f), short.MinValue, short.MaxValue);
                q[i * 3 + 2] = (short)Mathf.Clamp(Mathf.RoundToInt(v[i].z * 1000f), short.MinValue, short.MaxValue);
            }
            return q;
        }

        private static ushort[] QuantizeMeshTriangles(Mesh mesh)
        {
            if (mesh == null) return System.Array.Empty<ushort>();
            int[] t = mesh.triangles;
            if (t.Length > 15000) return System.Array.Empty<ushort>();
            ushort[] q = new ushort[t.Length];
            for (int i = 0; i < t.Length; i++) q[i] = (ushort)t[i];
            return q;
        }

        private static Mesh DequantizeMesh(short[] qVerts, ushort[] qTris, string name)
        {
            if (qVerts == null || qVerts.Length < 9 || qTris == null || qTris.Length < 3) return null;
            int vCount = qVerts.Length / 3;
            Vector3[] verts = new Vector3[vCount];
            for (int i = 0; i < vCount; i++)
                verts[i] = new Vector3(qVerts[i * 3] / 1000f, qVerts[i * 3 + 1] / 1000f, qVerts[i * 3 + 2] / 1000f);
            int[] tris = new int[qTris.Length];
            for (int i = 0; i < qTris.Length; i++) tris[i] = qTris[i];

            Mesh m = new Mesh { name = name };
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
