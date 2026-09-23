using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador anatómico de Cinemática Inversa (IK) y Cadena Cinética (Hips, Spine, Chest) para el Paladín.
    /// - Cadena Cinética y Torsión de Cadera: La cadera (Hips), columna (Spine) y pecho (Chest) rotan de forma
    ///   coordinada para acompañar y proyectar los ataques con potencia marcial visible, manteniendo las piernas
    ///   firmes en la postura de combate (CombatIdle).
    /// - Restricciones de Brazos: Limita el alcance máximo al 88% del largo físico del brazo para asegurar
    ///   que los codos mantengan flexión natural y nunca se quiebren, bloqueen o giren en ángulos extraños.
    /// - Alineación de Muñecas: Garantiza que las manos se adapten firmemente al mango del arma sin quiebres ni saltos.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerCombatIKDriver : MonoBehaviour
    {
        public static PlayerCombatIKDriver Instance { get; private set; }

        [Header("Calibración Anatómica del Torso y Brazos")]
        [SerializeField] private float maxReachRatio = 0.88f;
        [SerializeField] private float minReachRatio = 0.20f;
        [SerializeField] private float ikFadeSpeed = 8.0f;

        [Header("Offsets de Agarre")]
        [SerializeField] private Vector3 rightHandOffsetPos = new Vector3(0.035f, 0.075f, -0.015f);
        [SerializeField] private Vector3 rightHandOffsetRot = new Vector3(-90f, 0f, 90f);
        [SerializeField] private Vector3 leftHandOffsetPos = new Vector3(-0.035f, 0.075f, -0.015f);
        [SerializeField] private Vector3 leftHandOffsetRot = new Vector3(-90f, 0f, -90f);

        private Animator _animator;
        private Transform _hipsBone;
        private Transform _spineBone;
        private Transform _chestBone;
        private Transform _rightShoulder;
        private Transform _leftShoulder;
        private Transform _rightForearm;
        private Transform _leftForearm;

        private float _rightArmLength = 0.65f;
        private float _leftArmLength = 0.65f;

        // Estado del arma
        private bool _isEquipped = false;
        private Transform _gripSocketRear;
        private Transform _gripSocketFront;
        private WeaponCombatStance _currentStance = WeaponCombatStance.OneHanded;
        private WeaponStats _currentWeapon;

        // Pesos dinámicos
        private float _currentIkWeight = 0f;
        private float _targetIkWeight = 0f;

        // Cinemática de Cadera y Torso (Hips, Spine, Chest)
        private bool _isAttacking = false;
        private int _currentComboIndex = 0;
        private float _attackProgress = 0f;

        private float _currentHipsYaw = 0f;
        private float _currentHipsPitch = 0f;
        private float _currentSpineYaw = 0f;
        private float _currentSpinePitch = 0f;
        private float _currentChestYaw = 0f;
        private float _currentChestPitch = 0f;

        private float _strikeStartHipsYaw = 0f;
        private float _strikeStartHipsPitch = 0f;
        private float _strikeStartSpineYaw = 0f;
        private float _strikeStartSpinePitch = 0f;
        private float _strikeStartChestYaw = 0f;
        private float _strikeStartChestPitch = 0f;

        public float CurrentWeight => _currentIkWeight;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _animator = GetComponent<Animator>();
            CacheBones();
        }

        private void Start()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            CacheBones();
        }

        private void CacheBones()
        {
            if (_animator == null || !_animator.isHuman) return;

            _hipsBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spineBone = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chestBone = _animator.GetBoneTransform(HumanBodyBones.Chest) ?? _spineBone;
            _rightShoulder = _animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            _leftShoulder = _animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            _rightForearm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _leftForearm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);

            Transform rUpper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform rHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rUpper != null && _rightForearm != null && rHand != null)
            {
                float lenUpper = Vector3.Distance(rUpper.position, _rightForearm.position);
                float lenLower = Vector3.Distance(_rightForearm.position, rHand.position);
                _rightArmLength = Mathf.Max(0.55f, lenUpper + lenLower);
            }

            Transform lUpper = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (lUpper != null && _leftForearm != null && lHand != null)
            {
                float lenUpper = Vector3.Distance(lUpper.position, _leftForearm.position);
                float lenLower = Vector3.Distance(_leftForearm.position, lHand.position);
                _leftArmLength = Mathf.Max(0.55f, lenUpper + lenLower);
            }
        }

        public void SetWeaponEquipped(bool equipped, Transform gripRear, Transform gripFront, WeaponCombatStance stance, WeaponStats weapon = null)
        {
            _isEquipped = equipped;
            _gripSocketRear = gripRear;
            _gripSocketFront = gripFront;
            _currentStance = stance;
            _currentWeapon = weapon;
            _targetIkWeight = equipped ? 1.0f : 0.0f;
        }

        public void SetAttackState(bool isAttacking, int comboIndex, float progress)
        {
            if (isAttacking && (!_isAttacking || comboIndex != _currentComboIndex))
            {
                // Al iniciar un nuevo golpe o encadenar el combo, capturar la postura angular actual
                // para que el windup fluya de forma continua desde la inercia del golpe previo
                _strikeStartHipsYaw = _currentHipsYaw;
                _strikeStartHipsPitch = _currentHipsPitch;
                _strikeStartSpineYaw = _currentSpineYaw;
                _strikeStartSpinePitch = _currentSpinePitch;
                _strikeStartChestYaw = _currentChestYaw;
                _strikeStartChestPitch = _currentChestPitch;
            }

            _isAttacking = isAttacking;
            _currentComboIndex = comboIndex;
            _attackProgress = Mathf.Clamp01(progress);
        }

        private void Update()
        {
            // Transición suave de activación/desactivación del IK
            _currentIkWeight = Mathf.MoveTowards(_currentIkWeight, _targetIkWeight, Time.deltaTime * ikFadeSpeed);

            // Acompañamiento cinemático de Cadera y Torso durante el golpe
            if (_isAttacking && _isEquipped)
            {
                CalculateTorsoAndHipsKinematics(_currentComboIndex, _attackProgress,
                    out float hY, out float hP,
                    out float sY, out float sP,
                    out float cY, out float cP);

                // Asignación directa y síncrona con el avance del golpe (sin retraso de lerp artificial)
                _currentHipsYaw = hY;
                _currentHipsPitch = hP;
                _currentSpineYaw = sY;
                _currentSpinePitch = sP;
                _currentChestYaw = cY;
                _currentChestPitch = cP;
            }
            else
            {
                // Retorno suave a la postura de combate en reposo (CombatIdle)
                float speed = Time.deltaTime * 60f;
                _currentHipsYaw = Mathf.MoveTowards(_currentHipsYaw, 0f, speed);
                _currentHipsPitch = Mathf.MoveTowards(_currentHipsPitch, 0f, speed);
                _currentSpineYaw = Mathf.MoveTowards(_currentSpineYaw, 0f, speed);
                _currentSpinePitch = Mathf.MoveTowards(_currentSpinePitch, 0f, speed);
                _currentChestYaw = Mathf.MoveTowards(_currentChestYaw, 0f, speed);
                _currentChestPitch = Mathf.MoveTowards(_currentChestPitch, 0f, speed);
            }
        }

        private void LateUpdate()
        {
            if (_animator == null || !_animator.isHuman || _currentIkWeight <= 0.001f) return;

            Transform playerTransform = transform.parent != null ? transform.parent : transform;

            // 1. EXTENSIÓN Y ACOMPAÑAMIENTO DE CADERA, COLUMNA Y PECHO
            ApplyTorsoAndHipsKinematics(playerTransform);

            // 2. BLOQUEO EFECTOR ABSOLUTO DE MANOS AL AGARRE DEL ARMA (IK Helper Effector Lock)
            LockHandsToGripSockets(playerTransform);
        }

        private void ApplyTorsoAndHipsKinematics(Transform playerTransform)
        {
            float weight = _currentIkWeight;
            if (weight <= 0.001f) return;

            Vector3 charUp = playerTransform.up;
            Vector3 charRight = playerTransform.right;

            // 1. Cadera / Pelvis (Hips) — Pivote de masa y orientación de la cintura
            if (_hipsBone != null)
            {
                Quaternion hYaw = Quaternion.AngleAxis(_currentHipsYaw * weight, charUp);
                Quaternion hPitch = Quaternion.AngleAxis(_currentHipsPitch * weight, charRight);
                _hipsBone.rotation = hYaw * hPitch * _hipsBone.rotation;
            }

            // 2. Columna (Spine) — Flexión y torsión intermedia
            if (_spineBone != null)
            {
                Quaternion sYaw = Quaternion.AngleAxis(_currentSpineYaw * weight, charUp);
                Quaternion sPitch = Quaternion.AngleAxis(_currentSpinePitch * weight, charRight);
                _spineBone.rotation = sYaw * sPitch * _spineBone.rotation;
            }

            // 3. Pecho y Hombros (Chest) — Proyección y rotación final hacia el impacto
            if (_chestBone != null && _chestBone != _spineBone)
            {
                Quaternion cYaw = Quaternion.AngleAxis(_currentChestYaw * weight, charUp);
                Quaternion cPitch = Quaternion.AngleAxis(_currentChestPitch * weight, charRight);
                _chestBone.rotation = cYaw * cPitch * _chestBone.rotation;
            }
        }

        private void LockHandsToGripSockets(Transform playerTransform)
        {
            // En LateUpdate únicamente orientamos la muñeca (rotación).
            // NUNCA modificamos rHand.position directamente aquí, porque en esqueletos humanoides
            // trasladar un hueso hijo sin mover el antebrazo estira la malla como goma y la deforma.
            // Mecanim se encarga de la posición anatómica estricta con AvatarIKGoal en OnAnimatorIK.
            Transform rHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rHand != null && _gripSocketRear != null)
            {
                Quaternion rOffsetRot = Quaternion.Euler(rightHandOffsetRot);
                Quaternion targetHandRotR = _gripSocketRear.rotation * Quaternion.Inverse(rOffsetRot);

                // Evitar saltos de signo en la rotación (camino angular más corto)
                if (Quaternion.Dot(rHand.rotation, targetHandRotR) < 0f)
                {
                    targetHandRotR = new Quaternion(-targetHandRotR.x, -targetHandRotR.y, -targetHandRotR.z, -targetHandRotR.w);
                }

                rHand.rotation = Quaternion.Slerp(rHand.rotation, targetHandRotR, _currentIkWeight);
            }

            bool isBimanual = _currentStance == WeaponCombatStance.TwoHanded || _currentStance == WeaponCombatStance.Polearm;
            Transform lHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (isBimanual && lHand != null && _gripSocketFront != null)
            {
                Quaternion lOffsetRot = Quaternion.Euler(leftHandOffsetRot);
                Quaternion targetHandRotL = _gripSocketFront.rotation * Quaternion.Inverse(lOffsetRot);

                if (Quaternion.Dot(lHand.rotation, targetHandRotL) < 0f)
                {
                    targetHandRotL = new Quaternion(-targetHandRotL.x, -targetHandRotL.y, -targetHandRotL.z, -targetHandRotL.w);
                }

                lHand.rotation = Quaternion.Slerp(lHand.rotation, targetHandRotL, _currentIkWeight);
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null) return;

            Transform playerTransform = transform.parent != null ? transform.parent : transform;

            // 1. Mirada (LookAt) al frente
            if (_currentIkWeight > 0.001f)
            {
                Vector3 lookTarget = playerTransform.position + playerTransform.forward * 6.0f + Vector3.up * 1.50f;
                _animator.SetLookAtPosition(lookTarget);
                _animator.SetLookAtWeight(_currentIkWeight * 0.60f, 0.20f, 0.80f, 0.30f, 0.50f);
            }

            // 2. APLICAR CADENA CINÉTICA DE TORSO ANTES DE RESOLVER BRAZOS
            // Para que Mecanim resuelva la cinemática de los brazos con la clavícula y hombros ya orientados hacia el golpe
            if (_currentIkWeight > 0.001f)
            {
                ApplyTorsoAndHipsKinematics(playerTransform);
                ApplyHandIK(playerTransform);
            }
            else
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            }

            // 3. PIERNAS: Se mantienen 100% en la posición idle de batalla del Animator
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
        }

        private void ApplyHandIK(Transform playerTransform)
        {
            // MANO DERECHA (Pomo / Mango del Arma)
            if (_gripSocketRear != null)
            {
                Vector3 rSocketPos = _gripSocketRear.position;
                Quaternion rSocketRot = _gripSocketRear.rotation;

                Quaternion rOffsetRot = Quaternion.Euler(rightHandOffsetRot);
                Quaternion targetHandRotR = rSocketRot * Quaternion.Inverse(rOffsetRot);
                Vector3 targetHandPosR = rSocketPos - (targetHandRotR * rightHandOffsetPos);

                // Restricción anatómica de alcance: nunca sobrepasar el 88% del largo físico del brazo
                if (_rightShoulder != null)
                {
                    targetHandPosR = ClampArmReach(_rightShoulder.position, targetHandPosR, _rightArmLength, minReachRatio, maxReachRatio);
                }

                _animator.SetIKPosition(AvatarIKGoal.RightHand, targetHandPosR);
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _currentIkWeight);

                _animator.SetIKRotation(AvatarIKGoal.RightHand, targetHandRotR);
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _currentIkWeight);

                // Pole target del codo derecho (Kevin Iglesias IK Pole Target)
                // Apunta hacia afuera y abajo para garantizar flexión biomecánica natural
                if (_rightShoulder != null)
                {
                    Vector3 rElbowPole = _rightShoulder.position + playerTransform.right * 0.35f - playerTransform.up * 0.25f - playerTransform.forward * 0.15f;
                    _animator.SetIKHintPosition(AvatarIKHint.RightElbow, rElbowPole);
                    _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _currentIkWeight * 0.65f);
                }
            }

            // MANO IZQUIERDA (Armas a dos manos o asta)
            bool isBimanual = _currentStance == WeaponCombatStance.TwoHanded || _currentStance == WeaponCombatStance.Polearm;
            if (isBimanual && _gripSocketFront != null)
            {
                Vector3 lSocketPos = _gripSocketFront.position;
                Quaternion lSocketRot = _gripSocketFront.rotation;

                Quaternion lOffsetRot = Quaternion.Euler(leftHandOffsetRot);
                Quaternion targetHandRotL = lSocketRot * Quaternion.Inverse(lOffsetRot);
                Vector3 targetHandPosL = lSocketPos - (targetHandRotL * leftHandOffsetPos);

                if (_leftShoulder != null)
                {
                    targetHandPosL = ClampArmReach(_leftShoulder.position, targetHandPosL, _leftArmLength, minReachRatio, maxReachRatio);
                }

                _animator.SetIKPosition(AvatarIKGoal.LeftHand, targetHandPosL);
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _currentIkWeight);

                _animator.SetIKRotation(AvatarIKGoal.LeftHand, targetHandRotL);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _currentIkWeight);

                if (_leftShoulder != null)
                {
                    Vector3 lElbowPole = _leftShoulder.position - playerTransform.right * 0.35f - playerTransform.up * 0.25f - playerTransform.forward * 0.15f;
                    _animator.SetIKHintPosition(AvatarIKHint.LeftElbow, lElbowPole);
                    _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, _currentIkWeight * 0.65f);
                }
            }
            else
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
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

        /// <summary>
        /// Modula la intensidad de torsión de cadera y pecho según el tipo, peso y longitud del arma equipada.
        /// Armas ligeras (Dagas) tienen acompañamiento sutil y ágil.
        /// Armas pesadas (Mandoble 2H) involucran masa e inercia rotacional marcada, sin exagerar.
        /// </summary>
        private float GetTorsoEffortMultiplier()
        {
            if (!_isEquipped || _currentWeapon == null) return 1.0f;

            var style = _currentWeapon.GetAnimationStyle();
            float baseMultiplier = style switch
            {
                WeaponAnimationStyle.Dagger1H => 0.52f,   // Daga: ágil y sutil, perceptible pero nunca brusca
                WeaponAnimationStyle.Sword1H => 0.90f,    // Espada 1H: equilibrada y atlética
                WeaponAnimationStyle.Axe1H => 1.00f,      // Hacha 1H: corte directo con firmeza de tronco
                WeaponAnimationStyle.Hammer1H => 1.10f,   // Martillo 1H: impacto y empuje descendente
                WeaponAnimationStyle.Polearm => 1.18f,    // Asta: palanca amplia a dos manos
                WeaponAnimationStyle.Heavy2H => 1.35f,    // Mandoble 2H: inercia pesada y compromiso de cadera, sin exagerar
                _ => 1.0f
            };

            // Ajuste fino según el peso real del mineral forjado (0 = ligero, 1 = pesado)
            float normWeight = _currentWeapon.GetNormalizedWeight();
            float weightTweak = Mathf.Lerp(-0.06f, 0.06f, normWeight);

            // Ajuste fino según la longitud del arma forjada
            float lengthRatio = Mathf.Clamp(_currentWeapon.attackRange / 2.2f, 0.85f, 1.25f);
            float lengthTweak = (lengthRatio - 1.0f) * 0.12f;

            return Mathf.Clamp(baseMultiplier + weightTweak + lengthTweak, 0.42f, 1.45f);
        }

        /// <summary>
        /// Calcula la cadena cinética de Cadera (Hips), Columna (Spine) y Pecho (Chest)
        /// para que el cuerpo completo acompañe cada ataque con potencia visible y realista.
        /// </summary>
        private void CalculateTorsoAndHipsKinematics(int comboIndex, float t,
            out float hipsYaw, out float hipsPitch,
            out float spineYaw, out float spinePitch,
            out float chestYaw, out float chestPitch)
        {
            float windupEnd;
            float hitApex;

            float wHipsYaw, wHipsPitch, wSpineYaw, wSpinePitch, wChestYaw, wChestPitch;
            float sHipsYaw, sHipsPitch, sSpineYaw, sSpinePitch, sChestYaw, sChestPitch;

            int step = Mathf.Clamp(comboIndex, 0, 4);
            switch (step)
            {
                case 0:
                    // GOLPE 1: CORTE DIAGONAL DESCENDENTE (Der -> Izq)
                    windupEnd = 0.20f; hitApex = 0.44f;
                    // Carga a la derecha
                    wHipsYaw = 10f;  wHipsPitch = 0f;
                    wSpineYaw = 12f; wSpinePitch = -2f;
                    wChestYaw = 16f; wChestPitch = -5f;
                    // Impacto / Descarga diagonal a la izquierda con flexión frontal
                    sHipsYaw = -12f; sHipsPitch = 4f;
                    sSpineYaw = -15f; sSpinePitch = 8f;
                    sChestYaw = -20f; sChestPitch = 14f;
                    break;

                case 1:
                    // GOLPE 2: TAJO ASCENDENTE DE RETORNO (Izq -> Der)
                    windupEnd = 0.18f; hitApex = 0.42f;
                    // Carga a la izquierda
                    wHipsYaw = -10f; wHipsPitch = 2f;
                    wSpineYaw = -12f; wSpinePitch = 4f;
                    wChestYaw = -16f; wChestPitch = 6f;
                    // Corte hacia arriba y derecha
                    sHipsYaw = 10f;  sHipsPitch = -2f;
                    sSpineYaw = 14f; sSpinePitch = -4f;
                    sChestYaw = 18f; sChestPitch = -8f;
                    break;

                case 2:
                    // GOLPE 3: BARRIDO HORIZONTAL A LA CINTURA (Der -> Izq)
                    windupEnd = 0.20f; hitApex = 0.44f;
                    // Giro de torsión completo hacia la derecha
                    wHipsYaw = 12f;  wHipsPitch = 0f;
                    wSpineYaw = 15f; wSpinePitch = 0f;
                    wChestYaw = 20f; wChestPitch = 0f;
                    // Barrido horizontal pleno hacia la izquierda
                    sHipsYaw = -14f; sHipsPitch = 3f;
                    sSpineYaw = -18f; sSpinePitch = 6f;
                    sChestYaw = -24f; sChestPitch = 8f;
                    break;

                case 3:
                    // GOLPE 4: TAJO VERTICAL OVERHEAD (Slam)
                    windupEnd = 0.24f; hitApex = 0.46f;
                    // Elevación hacia atrás abriendo el pecho
                    wHipsYaw = 0f; wHipsPitch = -4f;
                    wSpineYaw = 0f; wSpinePitch = -8f;
                    wChestYaw = 0f; wChestPitch = -16f;
                    // Aplastamiento frontal contra el objetivo
                    sHipsYaw = 0f; sHipsPitch = 6f;
                    sSpineYaw = 0f; sSpinePitch = 12f;
                    sChestYaw = 0f; sChestPitch = 22f;
                    break;

                default: // case 4
                    // GOLPE 5: ESTOCADA FRONTAL PROFUNDA (Lunge Thrust)
                    windupEnd = 0.26f; hitApex = 0.48f;
                    // Retracción del brazo y hombro derecho
                    wHipsYaw = 4f;  wHipsPitch = 0f;
                    wSpineYaw = 6f;  wSpinePitch = -2f;
                    wChestYaw = 8f;  wChestPitch = -4f;
                    // Gran proyección del hombro y pecho al frente
                    sHipsYaw = -8f;  sHipsPitch = 6f;
                    sSpineYaw = -12f; sSpinePitch = 12f;
                    sChestYaw = -16f; sChestPitch = 20f;
                    break;
            }

            // Modular la intensidad del acompañamiento de torso según el peso y longitud del arma
            float effort = GetTorsoEffortMultiplier();
            wHipsYaw *= effort; wHipsPitch *= effort;
            wSpineYaw *= effort; wSpinePitch *= effort;
            wChestYaw *= effort; wChestPitch *= effort;

            sHipsYaw *= effort; sHipsPitch *= effort;
            sSpineYaw *= effort; sSpinePitch *= effort;
            sChestYaw *= effort; sChestPitch *= effort;

            // Interpolación fluida en 3 fases: Windup continuo -> Impacto -> Follow-through marcial
            if (t <= windupEnd)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / windupEnd);
                hipsYaw = Mathf.Lerp(_strikeStartHipsYaw, wHipsYaw, u);
                hipsPitch = Mathf.Lerp(_strikeStartHipsPitch, wHipsPitch, u);
                spineYaw = Mathf.Lerp(_strikeStartSpineYaw, wSpineYaw, u);
                spinePitch = Mathf.Lerp(_strikeStartSpinePitch, wSpinePitch, u);
                chestYaw = Mathf.Lerp(_strikeStartChestYaw, wChestYaw, u);
                chestPitch = Mathf.Lerp(_strikeStartChestPitch, wChestPitch, u);
            }
            else if (t <= hitApex)
            {
                float u = Mathf.SmoothStep(0f, 1f, (t - windupEnd) / (hitApex - windupEnd));
                hipsYaw = Mathf.Lerp(wHipsYaw, sHipsYaw, u);
                hipsPitch = Mathf.Lerp(wHipsPitch, sHipsPitch, u);
                spineYaw = Mathf.Lerp(wSpineYaw, sSpineYaw, u);
                spinePitch = Mathf.Lerp(wSpinePitch, sSpinePitch, u);
                chestYaw = Mathf.Lerp(wChestYaw, sChestYaw, u);
                chestPitch = Mathf.Lerp(wChestPitch, sChestPitch, u);
            }
            else
            {
                // Follow-through y espaciado marcial:
                // Tras el impacto, el cuerpo sostiene la torsión y proyección atlética
                // durante la ventana de encadenamiento de combo.
                float recoveryStart = Mathf.Lerp(hitApex, 1.0f, 0.45f);
                if (t <= recoveryStart)
                {
                    float u = Mathf.SmoothStep(0f, 1f, (t - hitApex) / (recoveryStart - hitApex));
                    hipsYaw = Mathf.Lerp(sHipsYaw, sHipsYaw * 0.92f, u);
                    hipsPitch = Mathf.Lerp(sHipsPitch, sHipsPitch * 0.92f, u);
                    spineYaw = Mathf.Lerp(sSpineYaw, sSpineYaw * 0.92f, u);
                    spinePitch = Mathf.Lerp(sSpinePitch, sSpinePitch * 0.92f, u);
                    chestYaw = Mathf.Lerp(sChestYaw, sChestYaw * 0.92f, u);
                    chestPitch = Mathf.Lerp(sChestPitch, sChestPitch * 0.92f, u);
                }
                else
                {
                    float u = Mathf.SmoothStep(0f, 1f, (t - recoveryStart) / (1.0f - recoveryStart));
                    hipsYaw = Mathf.Lerp(sHipsYaw * 0.92f, 0f, u);
                    hipsPitch = Mathf.Lerp(sHipsPitch * 0.92f, 0f, u);
                    spineYaw = Mathf.Lerp(sSpineYaw * 0.92f, 0f, u);
                    spinePitch = Mathf.Lerp(sSpinePitch * 0.92f, 0f, u);
                    chestYaw = Mathf.Lerp(sChestYaw * 0.92f, 0f, u);
                    chestPitch = Mathf.Lerp(sChestPitch * 0.92f, 0f, u);
                }
            }
        }
    }
}
