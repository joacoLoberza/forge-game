using System;
using UnityEngine;
using ForjaGame.Forge;
using ForjaGame.Core;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Fases de ejecución de un ataque procedural.
    /// </summary>
    public enum AttackSubPhase
    {
        InGuard,
        Windup,
        Strike,
        Recovery
    }

    /// <summary>
    /// Controlador central de combate procedural conducido por el perfil físico del arma forjada.
    /// Bloque B (Fase 3):
    /// - Ejecución de ataques procedurales derivados de AttackDefinitionSO (9 ataques base por GripMode).
    /// - Interpolación por curvas (Ease) y arcos Bézier con poses intermedias.
    /// - Cadena cinética secuencial (efecto látigo: Hips -> Spine -> Chest -> Arms) con retardos temporales configurables.
    /// - Límites anatómicos de hombro, muñeca y alcance bimanual clamped sin pop ni hiperextensión.
    /// - Volumen de exclusión corporal (5 puntos del arma testeados contra torso, cabeza y piernas con repulsión física).
    /// - Sincronización del lunge hacia Rigidbody.
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
        private Transform _hipsBone;
        private Transform _spineBone;
        private Transform _chestBone;
        private Transform _headBone;
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

        // Pose de guardia base
        private Vector3 _currentGuardLocalPos;
        private Quaternion _currentGuardLocalRot;
        private Vector3 _dynamicGuardOffset = Vector3.zero;

        // Máquina de estados de ataque procedural
        private AttackSubPhase _currentSubPhase = AttackSubPhase.InGuard;
        private AttackDefinitionSO _currentAttackDef;
        private AttackDefinitionSO[] _currentComboSequence;
        private int _currentComboIndex = 0;
        private float _phaseTime = 0f;
        private float _phaseDuration = 0.15f;
        private float _inertiaScale = 1.0f;

        // Poses e interpolación
        private Pose _fromHandPose;
        private Pose _toHandPose;
        private AttackPhase _activePhaseData;

        // Cadena cinética (ángulos y desplazamiento acumulados)
        private float _hipsYaw = 0f;
        private float _spineYaw = 0f;
        private float _spinePitch = 0f;
        private float _spineRoll = 0f;
        private float _chestYaw = 0f;
        private float _chestPitch = 0f;
        private float _chestRoll = 0f;
        private Vector3 _weightShift = Vector3.zero;

        private float _fromHipYaw = 0f, _toHipYaw = 0f;
        private float _fromSpineYaw = 0f, _toSpineYaw = 0f;
        private float _fromSpinePitch = 0f, _toSpinePitch = 0f;
        private float _fromSpineRoll = 0f, _toSpineRoll = 0f;
        private float _fromChestYaw = 0f, _toChestYaw = 0f;
        private float _fromChestPitch = 0f, _toChestPitch = 0f;
        private float _fromChestRoll = 0f, _toChestRoll = 0f;
        private Vector3 _fromWeightShift = Vector3.zero, _toWeightShift = Vector3.zero;

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
        public Transform HipsBone => _hipsBone;
        public Transform RightShoulder => _rightShoulder;
        public Transform LeftShoulder => _leftShoulder;
        public float RightArmLength => _rightArmLength;
        public float LeftArmLength => _leftArmLength;
        public float CurrentDistR => _currentDistR;
        public float CurrentDistL => _currentDistL;
        public float IKWeight => _ikWeight;
        public bool IsEquipped => _isEquipped;

        public AttackSubPhase CurrentSubPhase => _currentSubPhase;
        public AttackDefinitionSO CurrentAttackDef => _currentAttackDef;
        public int CurrentComboIndex => _currentComboIndex;
        public float InertiaScale => _inertiaScale;
        public float PhaseDuration => _phaseDuration;
        public float PhaseTime => _phaseTime;
        public float PhaseNormalizedTime => _phaseDuration > 0.001f ? Mathf.Clamp01(_phaseTime / _phaseDuration) : 0f;
        public Vector3 CurrentHipsEuler => new Vector3(0f, _hipsYaw, 0f);
        public Vector3 CurrentChestEuler => new Vector3(_chestPitch, _chestYaw, _chestRoll);
        public Vector3 CurrentWeightShift => _weightShift;

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

            _hipsBone = characterAnimator.GetBoneTransform(HumanBodyBones.Hips);
            _spineBone = characterAnimator.GetBoneTransform(HumanBodyBones.Spine);
            _chestBone = characterAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? _spineBone;
            _headBone = characterAnimator.GetBoneTransform(HumanBodyBones.Head);
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

            // Asegurar puente OnAnimatorIK en el GameObject que contiene el Animator
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
        /// y estableciendo la pose de guardia según el GripMode.
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
                // El arma es hija del WeaponPivot (hijo de Chest)
                visualInstance.transform.SetParent(_weaponPivot, false);
                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = Quaternion.identity;
                visualInstance.transform.localScale = Vector3.one;
            }

            // Calibrar sockets dinámicos desde la geometría del mango
            UpdateSocketsFromProfile(_currentProfile);

            // Cargar la secuencia de combo de 3 golpes predilecta para este perfil
            _currentComboSequence = ProceduralAttackDatabase.GetComboSequence(_currentProfile);

            // Determinar pose de guardia inicial según GripMode
            ConfigureGuardPoseForGrip(_currentProfile.grip);

            ResetToGuard();
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

            // Mano delantera (Left Hand): separación que escala con h y agarre
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

            // Base y punta de la hoja/cabeza
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
            ResetToGuard();
        }

        // =========================================================================
        // EJECUCIÓN DE ATAQUES PROCEDURALES Y MÁQUINA DE ESTADOS
        // =========================================================================

        /// <summary>
        /// Inicia un ataque procedural correspondiente al índice de combo solicitado (0, 1 o 2).
        /// Escala la duración de las fases mediante la inercia del arma: k = sqrt(I / Iref).
        /// </summary>
        public void ExecuteAttack(int comboIndex)
        {
            if (!_isEquipped) return;

            if (_currentComboSequence == null || _currentComboSequence.Length < 3)
            {
                _currentComboSequence = ProceduralAttackDatabase.GetComboSequence(_currentProfile);
            }

            _currentComboIndex = Mathf.Clamp(comboIndex, 0, _currentComboSequence.Length - 1);
            _currentAttackDef = _currentComboSequence[_currentComboIndex];
            if (_currentAttackDef == null) return;

            // Escala de inercia física k = sqrt(I / Iref)
            float inertia = _currentProfile.inertia > 0.01f ? _currentProfile.inertia : 0.85f;
            float refInertia = config != null ? config.referenceInertia : 0.85f;
            _inertiaScale = Mathf.Clamp(Mathf.Sqrt(inertia / refInertia), 0.60f, 1.80f);

            // Iniciar fase de Windup (Anticipación)
            float windupDur = CalculatePhaseDuration(_currentAttackDef.windupPhase.duration, AttackSubPhase.Windup);
            SetPhase(AttackSubPhase.Windup, _currentAttackDef.windupPhase, windupDur);
        }

        /// <summary>
        /// Devuelve las duraciones físicas escaladas por inercia para sincronización con AttackController.
        /// </summary>
        public (float totalDuration, float windupDuration, float strikeDuration, float recoveryDuration) GetCurrentAttackTimings()
        {
            if (_currentAttackDef == null) return (0.80f, 0.22f, 0.18f, 0.40f);

            float w = CalculatePhaseDuration(_currentAttackDef.windupPhase.duration, AttackSubPhase.Windup);
            float s = CalculatePhaseDuration(_currentAttackDef.strikePhase.duration, AttackSubPhase.Strike);
            float r = CalculatePhaseDuration(_currentAttackDef.recoveryPhase.duration, AttackSubPhase.Recovery);
            return (w + s + r, w, s, r);
        }

        public void ResetToGuard()
        {
            _currentSubPhase = AttackSubPhase.InGuard;
            _phaseTime = 0f;
            _phaseDuration = 0.20f;
            if (_weaponPivot != null)
            {
                _fromHandPose = new Pose(_weaponPivot.localPosition, _weaponPivot.localRotation);
            }
            Vector3 guardPos = _currentGuardLocalPos + _dynamicGuardOffset;
            _toHandPose = new Pose(guardPos, _currentGuardLocalRot);
            _activePhaseData = AttackPhase.CreateDefault(_toHandPose, 0.20f);

            _fromHipYaw = _hipsYaw; _toHipYaw = 0f;
            _fromSpineYaw = _spineYaw; _toSpineYaw = 0f;
            _fromSpinePitch = _spinePitch; _toSpinePitch = 0f;
            _fromSpineRoll = _spineRoll; _toSpineRoll = 0f;
            _fromChestYaw = _chestYaw; _toChestYaw = 0f;
            _fromChestPitch = _chestPitch; _toChestPitch = 0f;
            _fromChestRoll = _chestRoll; _toChestRoll = 0f;
            _fromWeightShift = _weightShift; _toWeightShift = Vector3.zero;
        }

        private float CalculatePhaseDuration(float baseDuration, AttackSubPhase subPhase)
        {
            float dur = baseDuration * _inertiaScale;
            if (config != null)
            {
                switch (subPhase)
                {
                    case AttackSubPhase.Windup:
                        dur = Mathf.Clamp(dur, config.windupClampMin, config.windupClampMax);
                        break;
                    case AttackSubPhase.Strike:
                        dur = Mathf.Clamp(dur, config.strikeClampMin, config.strikeClampMax);
                        break;
                    case AttackSubPhase.Recovery:
                        dur = Mathf.Clamp(dur, config.recoveryClampMin, config.recoveryClampMax);
                        break;
                }
            }
            return dur;
        }

        private void SetPhase(AttackSubPhase newPhase, AttackPhase phaseData, float duration)
        {
            _currentSubPhase = newPhase;
            _phaseTime = 0f;
            _phaseDuration = Mathf.Max(0.04f, duration);
            _activePhaseData = phaseData;

            if (_weaponPivot != null)
            {
                _fromHandPose = new Pose(_weaponPivot.localPosition, _weaponPivot.localRotation);
            }
            _toHandPose = phaseData.handPose;

            // Guardar estado actual de la cadena cinética para interpolar suavemente
            _fromHipYaw = _hipsYaw;
            _toHipYaw = phaseData.hipYaw;

            float spineRatio = config != null ? config.spineContributionRatio : 0.35f;
            float chestRatio = config != null ? config.chestContributionRatio : 0.40f;
            float totalRatio = Mathf.Max(0.01f, spineRatio + chestRatio);

            _fromSpineYaw = _spineYaw;
            _toSpineYaw = phaseData.chestYaw * (spineRatio / totalRatio);
            _fromSpinePitch = _spinePitch;
            _toSpinePitch = phaseData.chestPitch * 0.45f;
            _fromSpineRoll = _spineRoll;
            _toSpineRoll = phaseData.chestRoll * 0.45f;

            _fromChestYaw = _chestYaw;
            _toChestYaw = phaseData.chestYaw * (chestRatio / totalRatio);
            _fromChestPitch = _chestPitch;
            _toChestPitch = phaseData.chestPitch * 0.55f;
            _fromChestRoll = _chestRoll;
            _toChestRoll = phaseData.chestRoll * 0.55f;

            _fromWeightShift = _weightShift;
            _toWeightShift = phaseData.weightShift;
        }

        private void Update()
        {
            // Suavizado del peso del IK general
            _ikWeight = Mathf.MoveTowards(_ikWeight, _targetIkWeight, Time.deltaTime * 6.0f);

            if (!_isEquipped || _weaponPivot == null) return;

            UpdateAttackStateMachine();

            // Repeler el arma si cualquier punto penetra cápsulas de exclusión corporal
            ApplyBodyExclusion();
        }

        private void UpdateAttackStateMachine()
        {
            if (_currentSubPhase == AttackSubPhase.InGuard)
            {
                // En guardia: mantener pose de reposo y relajar la columna
                Vector3 targetGuardPos = _currentGuardLocalPos + _dynamicGuardOffset;
                _weaponPivot.localPosition = Vector3.Lerp(_weaponPivot.localPosition, targetGuardPos, Time.deltaTime * 10f);
                _weaponPivot.localRotation = Quaternion.Slerp(_weaponPivot.localRotation, _currentGuardLocalRot, Time.deltaTime * 10f);

                _hipsYaw = Mathf.MoveTowards(_hipsYaw, 0f, Time.deltaTime * 180f);
                _spineYaw = Mathf.MoveTowards(_spineYaw, 0f, Time.deltaTime * 180f);
                _spinePitch = Mathf.MoveTowards(_spinePitch, 0f, Time.deltaTime * 180f);
                _spineRoll = Mathf.MoveTowards(_spineRoll, 0f, Time.deltaTime * 180f);
                _chestYaw = Mathf.MoveTowards(_chestYaw, 0f, Time.deltaTime * 180f);
                _chestPitch = Mathf.MoveTowards(_chestPitch, 0f, Time.deltaTime * 180f);
                _chestRoll = Mathf.MoveTowards(_chestRoll, 0f, Time.deltaTime * 180f);
                _weightShift = Vector3.MoveTowards(_weightShift, Vector3.zero, Time.deltaTime * 2.0f);
                return;
            }

            // En ataque activo: avanzar temporizador
            _phaseTime += Time.deltaTime;
            float lag = config != null ? config.kineticChainLagSeconds : 0.055f;

            // Evaluación de retardos secuenciales de la cadena cinética (Efecto Látigo):
            // 1. Cadera (reacciona de inmediato)
            float tHips = Mathf.Clamp01(_phaseTime / _phaseDuration);
            float uHips = EvaluateEase(tHips);
            _hipsYaw = Mathf.Lerp(_fromHipYaw, _toHipYaw, uHips);

            // 2. Columna (reacciona con retraso lag)
            float tSpine = Mathf.Clamp01((_phaseTime - lag) / _phaseDuration);
            float uSpine = EvaluateEase(tSpine);
            _spineYaw = Mathf.Lerp(_fromSpineYaw, _toSpineYaw, uSpine);
            _spinePitch = Mathf.Lerp(_fromSpinePitch, _toSpinePitch, uSpine);
            _spineRoll = Mathf.Lerp(_fromSpineRoll, _toSpineRoll, uSpine);

            // 3. Pecho (reacciona con retraso 2*lag)
            float tChest = Mathf.Clamp01((_phaseTime - (2f * lag)) / _phaseDuration);
            float uChest = EvaluateEase(tChest);
            _chestYaw = Mathf.Lerp(_fromChestYaw, _toChestYaw, uChest);
            _chestPitch = Mathf.Lerp(_fromChestPitch, _toChestPitch, uChest);
            _chestRoll = Mathf.Lerp(_fromChestRoll, _toChestRoll, uChest);
            _weightShift = Vector3.Lerp(_fromWeightShift, _toWeightShift, uChest);

            // 4. Brazos / Arma (reacciona con retraso 2.5*lag y sigue la trayectoria en arco Bézier)
            float tWeapon = Mathf.Clamp01((_phaseTime - (2.5f * lag)) / Mathf.Max(0.02f, _phaseDuration - (2.5f * lag)));
            float uWeapon = EvaluateEase(tWeapon);
            Pose targetPose = InterpolateHandPose(_fromHandPose, _toHandPose, _activePhaseData, uWeapon);
            _weaponPivot.localPosition = targetPose.position;
            _weaponPivot.localRotation = targetPose.rotation;

            // 5. Aplicar impulso de Lunge al Rigidbody (físico sin atravesar paredes)
            if (playerRigidbody != null && !playerRigidbody.isKinematic && _activePhaseData.lunge > 0.01f)
            {
                float remainingTime = Mathf.Max(0.01f, _phaseDuration - _phaseTime);
                float stepVelocity = (_activePhaseData.lunge / _phaseDuration) * (1.0f - tHips);
                Vector3 lungeDir = transform.forward;
                Vector3 currentVel = playerRigidbody.linearVelocity;
                playerRigidbody.linearVelocity = new Vector3(lungeDir.x * stepVelocity, currentVel.y, lungeDir.z * stepVelocity);
            }

            // Transición automática al finalizar la fase
            if (_phaseTime >= _phaseDuration)
            {
                AdvanceAttackPhase();
            }
        }

        private void AdvanceAttackPhase()
        {
            if (_currentAttackDef == null)
            {
                ResetToGuard();
                return;
            }

            if (_currentSubPhase == AttackSubPhase.Windup)
            {
                // Windup finalizado -> Pasar al golpe (Strike)
                float strikeDur = CalculatePhaseDuration(_currentAttackDef.strikePhase.duration, AttackSubPhase.Strike);
                SetPhase(AttackSubPhase.Strike, _currentAttackDef.strikePhase, strikeDur);

                // Disparar detección de impacto en el momento exacto en que comienza a barrer el golpe
                if (attackController != null)
                {
                    attackController.TriggerHitDetection();
                }
            }
            else if (_currentSubPhase == AttackSubPhase.Strike)
            {
                // Strike finalizado -> Pasar a la recuperación (Recovery)
                float recDur = CalculatePhaseDuration(_currentAttackDef.recoveryPhase.duration, AttackSubPhase.Recovery);
                SetPhase(AttackSubPhase.Recovery, _currentAttackDef.recoveryPhase, recDur);
            }
            else if (_currentSubPhase == AttackSubPhase.Recovery)
            {
                // Recuperación completada -> Volver a guardia
                ResetToGuard();
            }
        }

        private float EvaluateEase(float t)
        {
            if (_activePhaseData.ease != null && _activePhaseData.ease.length > 0)
            {
                return Mathf.Clamp01(_activePhaseData.ease.Evaluate(t));
            }
            return Mathf.SmoothStep(0f, 1f, t);
        }

        private static Pose InterpolateHandPose(Pose from, Pose to, AttackPhase phase, float u)
        {
            if (phase.hasIntermediatePose)
            {
                // Curva cuadrática Bézier para describir un arco tridimensional
                Vector3 p0 = from.position;
                Vector3 p1 = phase.intermediateHandPose.position;
                Vector3 p2 = to.position;
                float oneMinusU = 1f - u;
                Vector3 pos = (oneMinusU * oneMinusU * p0) + (2f * oneMinusU * u * p1) + (u * u * p2);

                Quaternion rot;
                if (u < 0.5f)
                {
                    rot = Quaternion.Slerp(from.rotation, phase.intermediateHandPose.rotation, u * 2f);
                }
                else
                {
                    rot = Quaternion.Slerp(phase.intermediateHandPose.rotation, to.rotation, (u - 0.5f) * 2f);
                }
                return new Pose(pos, rot);
            }
            else
            {
                Vector3 pos = Vector3.Lerp(from.position, to.position, u);
                Quaternion rot = Quaternion.Slerp(from.rotation, to.rotation, u);
                return new Pose(pos, rot);
            }
        }

        // =========================================================================
        // VOLUMEN DE EXCLUSIÓN CORPORAL (Anti-Clipping Físico)
        // =========================================================================

        private void ApplyBodyExclusion()
        {
            if (_weaponPivot == null || config == null) return;

            Transform chest = _chestBone != null ? _chestBone : transform;
            Transform hips = _hipsBone != null ? _hipsBone : transform;
            Transform head = _headBone != null ? _headBone : chest;

            Vector3 pGripR = _gripSocketR != null ? _gripSocketR.position : _weaponPivot.position;
            Vector3 pGripL = _gripSocketL != null ? _gripSocketL.position : _weaponPivot.position;
            Vector3 pBladeBase = _bladeBase != null ? _bladeBase.position : _weaponPivot.position;
            Vector3 pBladeTip = _bladeTip != null ? _bladeTip.position : _weaponPivot.position;
            Vector3 pBladeMid = (pBladeBase + pBladeTip) * 0.5f;

            Vector3[] weaponPoints = { pGripR, pGripL, pBladeBase, pBladeMid, pBladeTip };

            // 1. Torso: segmento entre cadera y cuello
            Vector3 torsoStart = hips.position + Vector3.up * 0.10f;
            Vector3 torsoEnd = chest.position + Vector3.up * 0.15f;
            float torsoR = config.torsoRadius + config.exclusionMargin;

            // 2. Cabeza: esfera
            Vector3 headCenter = head.position;
            float headR = config.headRadius + config.exclusionMargin;

            // 3. Piernas: segmento desde cadera hacia el suelo
            Vector3 legsStart = hips.position;
            Vector3 legsEnd = transform.position + Vector3.up * 0.15f;
            float legsR = config.legsRadius + config.exclusionMargin;

            Vector3 totalRepulsion = Vector3.zero;

            foreach (var pt in weaponPoints)
            {
                // Chequear cápsula del Torso
                Vector3 cTorso = ClosestPointOnSegment(torsoStart, torsoEnd, pt);
                Vector3 toPtTorso = pt - cTorso;
                float distTorso = toPtTorso.magnitude;
                if (distTorso < torsoR)
                {
                    Vector3 pushDir = distTorso > 0.001f ? toPtTorso / distTorso : transform.forward;
                    float pen = torsoR - distTorso;
                    if (pen > totalRepulsion.magnitude) totalRepulsion = pushDir * pen;
                }

                // Chequear esfera de la Cabeza
                Vector3 toPtHead = pt - headCenter;
                float distHead = toPtHead.magnitude;
                if (distHead < headR)
                {
                    Vector3 pushDir = distHead > 0.001f ? toPtHead / distHead : (transform.forward + transform.up * 0.5f).normalized;
                    float pen = headR - distHead;
                    if (pen > totalRepulsion.magnitude) totalRepulsion = pushDir * pen;
                }

                // Chequear cápsula de Piernas
                Vector3 cLegs = ClosestPointOnSegment(legsStart, legsEnd, pt);
                Vector3 toPtLegs = pt - cLegs;
                float distLegs = toPtLegs.magnitude;
                if (distLegs < legsR)
                {
                    Vector3 pushDir = distLegs > 0.001f ? toPtLegs / distLegs : transform.forward;
                    float pen = legsR - distLegs;
                    if (pen > totalRepulsion.magnitude) totalRepulsion = pushDir * pen;
                }
            }

            if (totalRepulsion.sqrMagnitude > 0.00001f)
            {
                _weaponPivot.position += totalRepulsion;
            }
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
            return a + ab * t;
        }

        // =========================================================================
        // ON ANIMATOR IK (Two-Bone IK, Torsión de Muñeca, LookAt y Cadena Cinética)
        // =========================================================================

        public void OnAnimatorIKCallback(int layerIndex)
        {
            // Ejecutar exclusivamente en el primer pase para evitar doble rotación
            if (layerIndex != 0 || characterAnimator == null || _ikWeight <= 0.001f || _gripSocketR == null || _gripSocketL == null) return;

            // 1. Aplicar la torsión de la cadena cinética sobre los huesos del torso
            ApplyKineticChainTorso();

            // 2. LookAt suave de la cabeza manteniendo el objetivo al frente
            Vector3 lookTarget = transform.position + Vector3.up * 1.55f + transform.forward * 12.0f;
            characterAnimator.SetLookAtPosition(lookTarget);
            characterAnimator.SetLookAtWeight(_ikWeight * 0.85f, 0.20f, 0.80f, 0.40f, 0.50f);

            Transform chest = _chestBone != null ? _chestBone : transform;
            float minReachRatio = config != null ? config.armReachMinRatio : 0.25f;
            float maxReachRatio = config != null ? config.armReachMaxRatio : 0.95f;

            // 3. MANO DERECHA (Mano trasera en el pomo)
            Vector3 rSocketPos = _gripSocketR.position;
            Quaternion rSocketRot = _gripSocketR.rotation;

            Vector3 rOffsetPos = config != null ? config.rightHandOffsetPos : new Vector3(0.035f, 0.075f, -0.015f);
            Vector3 rOffsetRotEuler = config != null ? config.rightHandOffsetRot : new Vector3(-90f, 0f, 90f);
            Quaternion rOffsetRot = Quaternion.Euler(rOffsetRotEuler);

            Quaternion targetHandRotR = rSocketRot * Quaternion.Inverse(rOffsetRot);
            Vector3 targetHandPosR = rSocketPos - (targetHandRotR * rOffsetPos);

            if (_rightShoulder != null)
            {
                _currentDistR = Vector3.Distance(_rightShoulder.position, targetHandPosR);
                targetHandPosR = ClampArmReach(_rightShoulder.position, targetHandPosR, _rightArmLength, minReachRatio, maxReachRatio);
            }

            // Clamp anatómico de torsión de muñeca derecha
            targetHandRotR = ClampWristTwist(HumanBodyBones.RightLowerArm, targetHandRotR);

            characterAnimator.SetIKPosition(AvatarIKGoal.RightHand, targetHandPosR);
            characterAnimator.SetIKRotation(AvatarIKGoal.RightHand, targetHandRotR);
            characterAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, _ikWeight);
            characterAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, _ikWeight);

            // Hint de codo derecho
            if (_rightShoulder != null)
            {
                Vector3 rElbowOffset = config != null ? config.rightElbowHintOffset : new Vector3(0.22f, -0.26f, -0.10f);
                Vector3 rHint = (_rightShoulder.position + targetHandPosR) * 0.5f + chest.TransformDirection(rElbowOffset);
                characterAnimator.SetIKHintPosition(AvatarIKHint.RightElbow, rHint);
                characterAnimator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _ikWeight);
            }

            // 4. MANO IZQUIERDA (Mano delantera para TwoHand y Pole)
            bool isBimanual = _currentProfile.grip == GripMode.TwoHand || _currentProfile.grip == GripMode.Pole;
            if (isBimanual)
            {
                // Resolución geométrica para que ambas manos alcancen el mango en posiciones extremas
                if (_leftShoulder != null)
                {
                    float maxReachL = _leftArmLength * maxReachRatio;
                    float distToLeftSocket = Vector3.Distance(_leftShoulder.position, _gripSocketL.position);
                    if (distToLeftSocket > maxReachL)
                    {
                        Vector3 pull = (_leftShoulder.position - _gripSocketL.position).normalized * (distToLeftSocket - maxReachL);
                        _weaponPivot.position += pull * 0.45f;
                    }
                }

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

                // Clamp anatómico de torsión de muñeca izquierda
                targetHandRotL = ClampWristTwist(HumanBodyBones.LeftLowerArm, targetHandRotL);

                characterAnimator.SetIKPosition(AvatarIKGoal.LeftHand, targetHandPosL);
                characterAnimator.SetIKRotation(AvatarIKGoal.LeftHand, targetHandRotL);
                characterAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _ikWeight);
                characterAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _ikWeight);

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
                // 1 Mano: liberar completamente la mano izquierda para que acompañe la animación libre
                _currentDistL = 0f;
                characterAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0.0f);
                characterAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0.0f);
                characterAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0.0f);
            }
        }

        private void ApplyKineticChainTorso()
        {
            // 1. Cadera (Hips) Yaw
            if (_hipsBone != null && Mathf.Abs(_hipsYaw) > 0.01f)
            {
                float clampedHipYaw = Mathf.Clamp(_hipsYaw, -config.hipYawMaxAngle, config.hipYawMaxAngle);
                _hipsBone.rotation = Quaternion.AngleAxis(clampedHipYaw, transform.up) * _hipsBone.rotation;
            }

            // Desplazamiento de centro de masa (Weight Shift) sobre las caderas
            if (_hipsBone != null && _weightShift.sqrMagnitude > 0.0001f)
            {
                _hipsBone.position += transform.TransformDirection(_weightShift);
            }

            // 2. Columna (Spine) Yaw, Pitch, Roll
            if (_spineBone != null)
            {
                Quaternion sYaw = Quaternion.AngleAxis(_spineYaw, transform.up);
                Quaternion sPitch = Quaternion.AngleAxis(_spinePitch, transform.right);
                Quaternion sRoll = Quaternion.AngleAxis(_spineRoll, transform.forward);
                _spineBone.rotation = sYaw * sPitch * sRoll * _spineBone.rotation;
            }

            // 3. Pecho (Chest) Yaw, Pitch, Roll
            if (_chestBone != null && _chestBone != _spineBone)
            {
                Quaternion cYaw = Quaternion.AngleAxis(_chestYaw, transform.up);
                Quaternion cPitch = Quaternion.AngleAxis(_chestPitch, transform.right);
                Quaternion cRoll = Quaternion.AngleAxis(_chestRoll, transform.forward);
                _chestBone.rotation = cYaw * cPitch * cRoll * _chestBone.rotation;
            }
        }

        private Quaternion ClampWristTwist(HumanBodyBones forearmBone, Quaternion targetHandRot)
        {
            if (config == null || characterAnimator == null) return targetHandRot;

            Transform forearm = characterAnimator.GetBoneTransform(forearmBone);
            if (forearm == null) return targetHandRot;

            Quaternion relRot = Quaternion.Inverse(forearm.rotation) * targetHandRot;
            relRot.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            if (Mathf.Abs(angle) > config.wristTwistMaxAngle)
            {
                float clampedAngle = Mathf.Sign(angle) * config.wristTwistMaxAngle;
                return forearm.rotation * Quaternion.AngleAxis(clampedAngle, axis);
            }

            return targetHandRot;
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

        // =========================================================================
        // HERRAMIENTA DE CAPTURA DE POSES (Editor / Play Mode)
        // =========================================================================

        /// <summary>
        /// Captura la pose actual del WeaponPivot y los ángulos del torso en una fase del AttackDefinitionSO.
        /// </summary>
        public void CaptureCurrentPoseTo(AttackDefinitionSO attack, AttackSubPhase subPhase)
        {
            if (attack == null || _weaponPivot == null) return;

            AttackPhase phase = new AttackPhase
            {
                handPose = new Pose(_weaponPivot.localPosition, _weaponPivot.localRotation),
                hipYaw = _hipsYaw,
                chestYaw = _chestYaw + _spineYaw,
                chestPitch = _chestPitch + _spinePitch,
                chestRoll = _chestRoll + _spineRoll,
                weightShift = _weightShift,
                lunge = _activePhaseData.lunge,
                duration = _activePhaseData.duration > 0.05f ? _activePhaseData.duration : 0.20f,
                ease = _activePhaseData.ease ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            switch (subPhase)
            {
                case AttackSubPhase.InGuard:
                    attack.guardPhase = phase;
                    break;
                case AttackSubPhase.Windup:
                    attack.windupPhase = phase;
                    break;
                case AttackSubPhase.Strike:
                    attack.strikePhase = phase;
                    break;
                case AttackSubPhase.Recovery:
                    attack.recoveryPhase = phase;
                    break;
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(attack);
#endif
            Debug.Log($"[ProceduralCombat] Pose capturada con éxito en {attack.name} -> {subPhase}!");
        }
    }
}
