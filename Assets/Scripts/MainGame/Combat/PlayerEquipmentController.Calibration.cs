using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    public partial class PlayerEquipmentController
    {
        [Header("Prefabs de Armas para Testing (Assets/Prefabs/Weapons/)")]
        [SerializeField] private GameObject swordPrefab;
        [SerializeField] private GameObject greatswordPrefab;
        [SerializeField] private GameObject hammerPrefab;
        [SerializeField] private GameObject polearmPrefab;
        [SerializeField] private GameObject daggerPrefab;

        [Header("Posición de Guardia Frontal (Paralela al Pecho)")]
        [SerializeField] private Vector3 holderPosition_1H = new Vector3(0.32f, 0.05f, 0.38f);
        [SerializeField] private Vector3 holderPosition_2H = new Vector3(0.25f, 0.02f, 0.45f);
        [SerializeField] private Vector3 holderPosition_Polearm = new Vector3(0.28f, -0.10f, 0.42f);

        [Header("Separación de Manos en el Mango")]
        [SerializeField] private float handSeparation_1H = 0.10f;
        [SerializeField] private float handSeparation_2H = 0.24f;
        [SerializeField] private float handSeparation_Polearm = 0.50f;

        [Header("Alineación del Filo hacia el Frente")]
        [SerializeField] private float edgeAngleOffset = 0f;
        [SerializeField] private float hammerImpactAngleOffset = 0f;
        [SerializeField] private float axeBladeAngleOffset = 0f;

        [Header("Ajustes Finos Globales")]
        [SerializeField] private Vector3 globalPositionOffset = Vector3.zero;
        [SerializeField] private Vector3 globalRotationOffset = Vector3.zero;

        public (Vector3 pos, Vector3 rot, float separation) GetStanceCalibration(WeaponStats weapon, WeaponCombatStance stance)
        {
            Vector3 basePos = stance switch
            {
                WeaponCombatStance.Polearm => holderPosition_Polearm,
                WeaponCombatStance.TwoHanded => holderPosition_2H,
                _ => holderPosition_1H
            };

            float separation = stance switch
            {
                WeaponCombatStance.Polearm => handSeparation_Polearm,
                WeaponCombatStance.TwoHanded => handSeparation_2H,
                _ => handSeparation_1H
            };

            Vector3 pos = basePos + globalPositionOffset;
            Vector3 rot = CalculateEdgeAlignmentRotation(weapon, pos);

            return (pos, rot, separation);
        }

        public Vector3 CalculateEdgeAlignmentRotation(WeaponStats weapon, Vector3 holderPos)
        {
            // Para alinear el filo directamente con la parte frontal del modelo (0° respecto al frente +Z):
            // En los modelos de armas (FBX), el filo o cara frontal de impacto se ubica en el eje local +X.
            // Rotando -90° en Y, el filo local (+X) se alinea exactamente hacia el frente (+Z) del personaje.
            float baseYaw = -90f;

            // Detección de la cara frontal según el forjado en el yunque (isFrontFaceDominant):
            // Si el jugador forjó el filo dominante en la cara trasera, invertimos 180° para que el filo afilado mire hacia adelante.
            bool isFrontDominant = weapon == null || weapon.isFrontFaceDominant;
            if (!isFrontDominant)
            {
                baseYaw += 180f;
            }

            // Para martillos (WeaponType.Hammer), compensar la orientación de la cabeza de impacto hacia el frente
            if (weapon != null && weapon.weaponType == WeaponType.Hammer)
            {
                baseYaw += hammerImpactAngleOffset;
            }

            // Para hachas (WeaponType.Axe), compensar la orientación del filo hacia el frente
            if (weapon != null && weapon.weaponType == WeaponType.Axe)
            {
                baseYaw += axeBladeAngleOffset;
            }

            float finalYaw = baseYaw + edgeAngleOffset;

            // El arma permanece estrictamente en paralelo a la cápsula del personaje (Pitch X = 0, Roll Z = 0)
            return new Vector3(0f, finalYaw, 0f) + globalRotationOffset;
        }

        private GameObject GetPrefabForWeapon(WeaponStats weapon)
        {
            if (weapon == null) return swordPrefab;

            var stance = weapon.GetCombatStance();
            if (stance == WeaponCombatStance.Polearm)
            {
                return polearmPrefab != null ? polearmPrefab : swordPrefab;
            }

            if (stance == WeaponCombatStance.TwoHanded || weapon.ingotSize == ItemSize.Large)
            {
                return greatswordPrefab != null ? greatswordPrefab : (swordPrefab != null ? swordPrefab : hammerPrefab);
            }

            if (weapon.weaponType == WeaponType.Hammer)
            {
                return hammerPrefab != null ? hammerPrefab : swordPrefab;
            }

            if (weapon.handleLength == HandleLength.Short)
            {
                return daggerPrefab != null ? daggerPrefab : swordPrefab;
            }

            return swordPrefab;
        }

        private void LoadDefaultAssetsIfMissing()
        {
#if UNITY_EDITOR
            if (swordPrefab == null)
                swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Human_Sword.prefab");
            if (greatswordPrefab == null)
                greatswordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Human_Greatsword.prefab");
            if (hammerPrefab == null)
                hammerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Human_Warhammer.prefab");
            if (polearmPrefab == null)
                polearmPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Human_Polearm.prefab");
            if (daggerPrefab == null)
                daggerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Human_Dagger.prefab");

#endif
        }
    }
}
