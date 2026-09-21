using System;
using UnityEngine;
using ForjaGame.Forge;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador central de combate procedural conducido por el perfil físico del arma forjada.
    /// Fase 2: WeaponPivot hijo de Chest, GripSockets generados por geometría del mango,
    /// pose de guardia procedural por GripMode, Two-Bone IK con hints y clamp de alcance anatómico.
    /// </summary>
    public class ProceduralCombatController : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private ProceduralCombatConfigSO config;

        [Header("Referencias Opcionales")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private AttackController attackController;

        // Jerarquía de huesos
        private Transform _chestBone;
        private Transform _rightShoulder;
        private Transform _leftShoulder;
        private Transform _weaponPivot;
        private Transform _gripSocketR;
        private Transform _gripSocketL;
        private Transform _bladeTip;
        private Transform _bladeBase;

        // Longitudes anatómicas de brazos
        private float _rightArmLength = 0.72f;
        private float _leftArmLength = 0.72f;
        private float _currentDistR = 0f;
        private float _currentDistL = 0f;

        // Estado del arma
        private WeaponStats _currentWeapon;
        private WeaponProfile _currentProfile;
        private GameObject _spawnedWeaponInstance;
        private bool _isEquipped = false;
        private float _ikWeight = 0.0f;
        private float _targetIkWeight = 0.0f;

        // Pose de guardia actual
        private Vector3 _currentGuardLocalPos;
        private Quaternion _currentGuardLocalRot;

        // Propiedades públicas para Gizmos, Debug y Banco de Pruebas
        public ProceduralCombatConfigSO Config => config;
        public WeaponProfile CurrentProfile => _currentProfile;
        public WeaponStats CurrentWeapon => _currentWeapon;
        public Transform WeaponPivot => _weaponPivot;
        public Transform GripSocketR => _gripSocketR;
        public Transform GripSocketL => _gripSocketL;
        public Transform BladeTip => _bladeTip;
        public Transform BladeBase => _bladeBase;
        public Transform ChestBone => _chestBone;
        public Transform RightShoulder => _rightShoulder;
        public Transform LeftShoulder => _leftShoulder;
        public float RightArmLength => _rightArmLength;
        public float LeftArmLength => _leftArmLength;
        public float CurrentDistR => _currentDistR;
        public float CurrentDistL => _currentDistL;
        public float IKWeight => _ikWeight;
        public bool IsEquipped => _isEquipped;

        private void Awake()
        {
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
            if (playerRigidbody == null) playerRigidbody = GetComponent<Rigidbody>();
            if (attackController == null) attackController = GetComponent<AttackController>();
            if (config == null)
            {
#if UNITY_EDITOR
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<ProceduralCombatConfigSO>(
                    "Assets/ScriptableObjects/Combat/Config/ProceduralCombatConfig.asset");
#endif
                if (config == null) config = ScriptableObject.CreateInstance<ProceduralCombatConfigSO>();
            }

            InitializeSkeletonAndPivot();
        }

        public void InitializeSkeletonAndPivot()
        {
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
            if (characterAnimator == null) return;

            _chestBone = characterAnimator.GetBoneTransform(HumanBodyBones.Chest) ??
                         characterAnimator.GetBoneTransform(HumanBodyBones.Spine);
            _rightShoulder = characterAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);
            _leftShoulder = characterAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);

            MeasureArmLengths();

            if (_chestBone != null)
            {
                if (_weaponPivot == null)
                {
                    Transform existing = _chestBone.Find("WeaponPivot_Chest");
                    if (existing != null) _weaponPivot = existing;
                    else
                    {
                        GameObject go = new GameObject("WeaponPivot_Chest");
                        go.transform.SetParent(_chestBone, false);
                        _weaponPivot = go.transform;
                    }
                }

                EnsureSockets();
            }

            // Asegurar puente OnAnimatorIK en CharacterVisual
            var bridge = characterAnimator.GetComponent<PlayerIKBridge>() ??
                         characterAnimator.gameObject.AddComponent<PlayerIKBridge>();
            bridge.SetProceduralController(this);
        }

        private void MeasureArmLengths()
        {
            if (characterAnimator == null) return;

            Transform rUpper = characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rLower = characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            Transform rHand = characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);

            if (rUpper != null && rLower != null && rHand != null)
            {
                _rightArmLength = Vector3.Distance(rUpper.position, rLower.position) +
                                  Vector3.Distance(rLower.position, rHand.position);
            }

            Transform lUpper = characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lLower = characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform lHand = characterAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

            if (lUpper != null && lLower != null && lHand != null)
            {
                _leftArmLength = Vector3.Distance(lUpper.position, lLower.position) +
                                 Vector3.Distance(lLower.position, lHand.position);
            }
        }

        private void EnsureSockets()
        {
            if (_weaponPivot == null) return;

            _gripSocketR = FindOrCreateChild(_weaponPivot, "GripSocket_R", Vector3.zero, Quaternion.identity);
            _gripSocketL = FindOrCreateChild(_weaponPivot, "GripSocket_L", new Vector3(0f, 0.20f, 0f), Quaternion.identity);
            _bladeBase = FindOrCreateChild(_weaponPivot, "BladeBasePoint", new Vector3(0f, 0.50f, 0f), Quaternion.identity);
            _bladeTip = FindOrCreateChild(_weaponPivot, "BladeTipPoint", new Vector3(0f, 1.20f, 0f), Quaternion.identity);
        }

        private Transform FindOrCreateChild(Transform parent, string childName, Vector3 localPos, Quaternion localRot)
        {
            Transform t = parent.Find(childName);
            if (t == null)
            {
                GameObject go = new GameObject(childName);
                go.transform.SetParent(parent, false);
                t = go.transform;
            }
            t.localPosition = localPos;
            t.localRotation = localRot;
            return t;
        }

        /// <summary>
        /// Equipa un arma forjada calculando su WeaponProfile, posicionando sockets dinámicos
        /// a lo largo del mango y aplicando la pose de guardia según el GripMode.
        /// </summary>
        public void EquipWeapon(WeaponStats weapon, GameObject visualInstance)
        {
            InitializeSkeletonAndPivot();

            _currentWeapon = weapon;
            _spawnedWeaponInstance = visualInstance;
            _currentProfile = weapon != null ? weapon.Profile : default;
            _isEquipped = weapon != null;

            if (_weaponPivot != null && visualInstance != null)
            {
                // El arma es hija del WeaponPivot (hijo del Chest), nunca del hueso de la mano
                visualInstance.transform.SetParent(_weaponPivot, false);
                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = Quaternion.identity;
                visualInstance.transform.localScale = Vector3.one;
            }

            // Calibrar sockets dinámicos desde la geometría del mango
            UpdateSocketsFromProfile(_currentProfile);

            // Determinar pose de guardia inicial según GripMode
            ConfigureGuardPoseForGrip(_currentProfile.grip);

            _targetIkWeight = 1.0f;
        }

        /// <summary>
        /// Posiciona GripSocket_R en el pomo y GripSocket_L a una fracción del mango
        /// que escala con h = InverseLerp(minHandle, maxHandle, handleLength).
        /// </summary>
        public void UpdateSocketsFromProfile(WeaponProfile profile)
        {
            if (_gripSocketR == null || _gripSocketL == null) EnsureSockets();

            float handleLen = profile.handleLength > 0.05f ? profile.handleLength : 0.50f;
            float minH = config != null ? config.minHandleLength : 0.15f;
            float maxH = config != null ? config.maxHandleLength : 1.85f;
            float h = Mathf.Clamp01(Mathf.InverseLerp(minH, maxH, handleLen));

            // Mano trasera (Right Hand): alineada en el centro de la empuñadura del pomo
            _gripSocketR.localPosition = Vector3.zero;
            _gripSocketR.localRotation = Quaternion.identity;

            // Mano delantera (Left Hand): separación que crece con h
            // Para TwoHand: separación compacta (0.16m - 0.26m).
            // Para Pole: separación amplia sobre el asta (0.35m - 0.70m).
            float leftY;
            if (profile.grip == GripMode.Pole)
            {
                leftY = Mathf.Clamp(Mathf.Lerp(0.35f, handleLen * 0.65f, h), 0.35f, handleLen * 0.75f);
            }
            else
            {
                leftY = Mathf.Clamp(0.15f + (handleLen * 0.12f), 0.15f, 0.28f);
            }

            _gripSocketL.localPosition = new Vector3(0f, leftY, 0f);
            _gripSocketL.localRotation = Quaternion.identity;

            // Posición de la base y punta de la cabeza metálica
            if (_bladeBase != null)
            {
                _bladeBase.localPosition = new Vector3(0f, handleLen, 0f);
            }
            if (_bladeTip != null)
            {
                float totalLength = profile.length > 0.1f ? profile.length : (handleLen + profile.headLength);
                _bladeTip.localPosition = new Vector3(0f, totalLength, 0f);
            }
        }

        private void ConfigureGuardPoseForGrip(GripMode grip)
        {
            if (config != null)
            {
                switch (grip)
                {
                    case GripMode.OneShort:
                        _currentGuardLocalPos = config.guardPosOneShort;
                        _currentGuardLocalRot = Quaternion.Euler(config.guardRotOneShort);
                        break;
                    case GripMode.OneHand:
                        _currentGuardLocalPos = config.guardPosOneHand;
                        _currentGuardLocalRot = Quaternion.Euler(config.guardRotOneHand);
                        break;
                    case GripMode.TwoHand:
                        _currentGuardLocalPos = config.guardPosTwoHand;
                        _currentGuardLocalRot = Quaternion.Euler(config.guardRotTwoHand);
                        break;
                    case GripMode.Pole:
                        _currentGuardLocalPos = config.guardPosPole;
                        _currentGuardLocalRot = Quaternion.Euler(config.guardRotPole);
                        break;
                }
            }
            else
            {
                switch (grip)
                {
                    case GripMode.OneShort:
                        _currentGuardLocalPos = new Vector3(0.22f, -0.05f, 0.48f);
                        _currentGuardLocalRot = Quaternion.Euler(-12f, 20f, -10f);
                        break;
                    case GripMode.OneHand:
                        _currentGuardLocalPos = new Vector3(0.22f, -0.04f, 0.52f);
                        _currentGuardLocalRot = Quaternion.Euler(-20f, 20f, -12f);
                        break;
                    case GripMode.TwoHand:
                        _currentGuardLocalPos = new Vector3(0.14f, -0.10f, 0.48f);
                        _currentGuardLocalRot = Quaternion.Euler(-24f, 15f, -8f);
                        break;
                    case GripMode.Pole:
                        _currentGuardLocalPos = new Vector3(0.18f, -0.14f, 0.46f);
                        _currentGuardLocalRot = Quaternion.Euler(-32f, 18f, 12f);
                        break;
                }
            }
        }

        private Vector3 _dynamicGuardOffset = Vector3.zero;

        public void SetDynamicGuardOffset(Vector3 offset)
        {
            _dynamicGuardOffset = offset;
        }

        public void UnequipWeapon()
        {
            _currentWeapon = null;
            _spawnedWeaponInstance = null;
            _isEquipped = false;
            _targetIkWeight = 0.0f;
        }

        private void Update()
        {
            // Suavizado del peso del IK (entrada/salida fluida)
            _ikWeight = Mathf.MoveTowards(_ikWeight, _targetIkWeight, Time.deltaTime * 5.0f);

            if (!_isEquipped || _weaponPivot == null) return;

            // Mantener suavemente la pose de guardia del WeaponPivot
            Vector3 targetGuardPos = _currentGuardLocalPos + _dynamicGuardOffset;
            _weaponPivot.localPosition = Vector3.Lerp(_weaponPivot.localPosition, targetGuardPos, Time.deltaTime * 10f);
            _weaponPivot.localRotation = Quaternion.Slerp(_weaponPivot.localRotation, _currentGuardLocalRot, Time.deltaTime * 10f);
        }

        /// <summary>
        /// Callback ejecutado desde PlayerIKBridge en OnAnimatorIK.
        /// Conecta las manos a los sockets del mango con Two-Bone IK, compensación de palma,
        /// hints de codo y clamp de alcance anatómico.
        /// </summary>
        public void OnAnimatorIKCallback(int layerIndex)
        {
            if (characterAnimator == null || _ikWeight <= 0.001f || _gripSocketR == null || _gripSocketL == null) return;

            Transform chest = _chestBone != null ? _chestBone : transform;
            float minReachRatio = config != null ? config.armReachMinRatio : 0.25f;
            float maxReachRatio = config != null ? config.armReachMaxRatio : 0.95f;

            // 1. MANO DERECHA (Siempre activa en guardia)
            Vector3 rSocketPos = _gripSocketR.position;
            Quaternion rSocketRot = _gripSocketR.rotation;

            Vector3 rOffsetPos = config != null ? config.rightHandOffsetPos : new Vector3(0.035f, 0.075f, -0.015f);
            Vector3 rOffsetRotEuler = config != null ? config.rightHandOffsetRot : new Vector3(-90f, 0f, 90f);
            Quaternion rOffsetRot = Quaternion.Euler(rOffsetRotEuler);

            // Calcular rotación y posición del hueso RightHand (muñeca) para que la palma coincida exactamente con el mango
            Quaternion targetHandRotR = rSocketRot * Quaternion.Inverse(rOffsetRot);
            Vector3 targetHandPosR = rSocketPos - (targetHandRotR * rOffsetPos);

            if (_rightShoulder != null)
            {
                _currentDistR = Vector3.Distance(_rightShoulder.position, targetHandPosR);
                targetHandPosR = ClampArmReach(_rightShoulder.position, targetHandPosR, _rightArmLength, minReachRatio, maxReachRatio);
            }

            characterAnimator.SetIKPosition(AvatarIKGoal.RightHand, targetHandPosR);
            characterAnimator.SetIKRotation(AvatarIKGoal.RightHand, targetHandRotR);
            characterAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, _ikWeight);
            characterAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, _ikWeight);

            // Hint de codo derecho: siempre hacia abajo y afuera
            if (_rightShoulder != null)
            {
                Vector3 rElbowOffset = config != null ? config.rightElbowHintOffset : new Vector3(0.22f, -0.26f, -0.10f);
                Vector3 rHint = (_rightShoulder.position + targetHandPosR) * 0.5f + chest.TransformDirection(rElbowOffset);
                characterAnimator.SetIKHintPosition(AvatarIKHint.RightElbow, rHint);
                characterAnimator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _ikWeight);
            }

            // 2. MANO IZQUIERDA (Activa ÚNICAMENTE en TwoHand y Pole)
            bool isBimanual = _currentProfile.grip == GripMode.TwoHand || _currentProfile.grip == GripMode.Pole;
            if (isBimanual)
            {
                Vector3 lSocketPos = _gripSocketL.position;
                Quaternion lSocketRot = _gripSocketL.rotation;

                Vector3 lOffsetPos = config != null ? config.leftHandOffsetPos : new Vector3(-0.035f, 0.075f, -0.015f);
                Vector3 lOffsetRotEuler = config != null ? config.leftHandOffsetRot : new Vector3(-90f, 0f, -90f);
                Quaternion lOffsetRot = Quaternion.Euler(lOffsetRotEuler);

                Quaternion targetHandRotL = lSocketRot * Quaternion.Inverse(lOffsetRot);
                Vector3 targetHandPosL = lSocketPos - (targetHandRotL * lOffsetPos);

                if (_leftShoulder != null)
                {
                    _currentDistL = Vector3.Distance(_leftShoulder.position, targetHandPosL);
                    targetHandPosL = ClampArmReach(_leftShoulder.position, targetHandPosL, _leftArmLength, minReachRatio, maxReachRatio);
                }

                characterAnimator.SetIKPosition(AvatarIKGoal.LeftHand, targetHandPosL);
                characterAnimator.SetIKRotation(AvatarIKGoal.LeftHand, targetHandRotL);
                characterAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _ikWeight);
                characterAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _ikWeight);

                // Hint de codo izquierdo: siempre hacia abajo y afuera a la izquierda
                if (_leftShoulder != null)
                {
                    Vector3 lElbowOffset = config != null ? config.leftElbowHintOffset : new Vector3(-0.22f, -0.26f, -0.10f);
                    Vector3 lHint = (_leftShoulder.position + targetHandPosL) * 0.5f + chest.TransformDirection(lElbowOffset);
                    characterAnimator.SetIKHintPosition(AvatarIKHint.LeftElbow, lHint);
                    characterAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, _ikWeight);
                }
            }
            else
            {
                // En 1 mano: liberar totalmente la mano izquierda
                _currentDistL = 0f;
                characterAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0.0f);
                characterAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0.0f);
                characterAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0.0f);
            }
        }

        private static Vector3 ClampArmReach(Vector3 shoulderPos, Vector3 targetPos, float armLength, float minRatio, float maxRatio)
        {
            Vector3 toTarget = targetPos - shoulderPos;
            float dist = toTarget.magnitude;
            if (dist < 0.001f) return shoulderPos + Vector3.forward * (armLength * minRatio);

            float minReach = armLength * minRatio;
            float maxReach = armLength * maxRatio;

            if (dist > maxReach)
            {
                return shoulderPos + toTarget.normalized * maxReach;
            }
            else if (dist < minReach)
            {
                return shoulderPos + toTarget.normalized * minReach;
            }

            return targetPos;
        }
    }
}
