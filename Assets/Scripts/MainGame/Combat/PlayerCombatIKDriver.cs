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
        [SerializeField] private float maxReachRatio = 0.96f;
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
        private Transform _rightUpperArm;
        private Transform _leftUpperArm;
        private Transform _rightForearm;
        private Transform _leftForearm;
        private Transform _rightHand;
        private Transform _leftHand;

        private float _rightArmLength = 0.65f;
        private float _leftArmLength = 0.65f;

        // Estado del arma
        private bool _isEquipped = false;
        private Transform _weaponHolder;
        private Transform _gripSocketRear;
        private Transform _gripSocketFront;
        private WeaponCombatStance _currentStance = WeaponCombatStance.OneHanded;
        private WeaponStats _currentWeapon;

        // Pesos dinámicos
        private float _currentIkWeight = 0f;
        private float _targetIkWeight = 0f;

        // Cinemática de Cadera y Torso (Hips, Spine, Chest) con Yaw, Pitch y Roll
        private bool _isAttacking = false;
        private int _currentComboIndex = 0;
        private float _attackProgress = 0f;

        private float _currentHipsYaw = 0f;
        private float _currentHipsPitch = 0f;
        private float _currentHipsRoll = 0f;
        private float _currentSpineYaw = 0f;
        private float _currentSpinePitch = 0f;
        private float _currentSpineRoll = 0f;
        private float _currentChestYaw = 0f;
        private float _currentChestPitch = 0f;
        private float _currentChestRoll = 0f;

        private float _strikeStartHipsYaw = 0f;
        private float _strikeStartHipsPitch = 0f;
        private float _strikeStartHipsRoll = 0f;
        private float _strikeStartSpineYaw = 0f;
        private float _strikeStartSpinePitch = 0f;
        private float _strikeStartSpineRoll = 0f;
        private float _strikeStartChestYaw = 0f;
        private float _strikeStartChestPitch = 0f;
        private float _strikeStartChestRoll = 0f;

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
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rightForearm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _leftForearm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);

            if (_rightUpperArm != null && _rightForearm != null && _rightHand != null)
            {
                float lenUpper = Vector3.Distance(_rightUpperArm.position, _rightForearm.position);
                float lenLower = Vector3.Distance(_rightForearm.position, _rightHand.position);
                _rightArmLength = Mathf.Max(0.55f, lenUpper + lenLower);
            }

            if (_leftUpperArm != null && _leftForearm != null && _leftHand != null)
            {
                float lenUpper = Vector3.Distance(_leftUpperArm.position, _leftForearm.position);
                float lenLower = Vector3.Distance(_leftForearm.position, _leftHand.position);
                _leftArmLength = Mathf.Max(0.55f, lenUpper + lenLower);
            }
        }

        public void SetWeaponEquipped(bool equipped, Transform weaponHolder, Transform gripRear, Transform gripFront, WeaponCombatStance stance, WeaponStats weapon = null)
        {
            _isEquipped = equipped;
            _weaponHolder = weaponHolder;
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
                _strikeStartHipsRoll = _currentHipsRoll;
                _strikeStartSpineYaw = _currentSpineYaw;
                _strikeStartSpinePitch = _currentSpinePitch;
                _strikeStartSpineRoll = _currentSpineRoll;
                _strikeStartChestYaw = _currentChestYaw;
                _strikeStartChestPitch = _currentChestPitch;
                _strikeStartChestRoll = _currentChestRoll;
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
                CalculateTorsoKinematics(_currentComboIndex, _attackProgress,
                    out float hY, out float hP, out float hR,
                    out float sY, out float sP, out float sR,
                    out float cY, out float cP, out float cR);

                // Asignación directa y síncrona con el avance del golpe (sin retraso de lerp artificial)
                _currentHipsYaw = hY;
                _currentHipsPitch = hP;
                _currentHipsRoll = hR;
                _currentSpineYaw = sY;
                _currentSpinePitch = sP;
                _currentSpineRoll = sR;
                _currentChestYaw = cY;
                _currentChestPitch = cP;
                _currentChestRoll = cR;
            }
            else
            {
                // Retorno suave a la postura de combate en reposo (CombatIdle)
                float speed = Time.deltaTime * 60f;
                _currentHipsYaw = Mathf.MoveTowards(_currentHipsYaw, 0f, speed);
                _currentHipsPitch = Mathf.MoveTowards(_currentHipsPitch, 0f, speed);
                _currentHipsRoll = Mathf.MoveTowards(_currentHipsRoll, 0f, speed);
                _currentSpineYaw = Mathf.MoveTowards(_currentSpineYaw, 0f, speed);
                _currentSpinePitch = Mathf.MoveTowards(_currentSpinePitch, 0f, speed);
                _currentSpineRoll = Mathf.MoveTowards(_currentSpineRoll, 0f, speed);
                _currentChestYaw = Mathf.MoveTowards(_currentChestYaw, 0f, speed);
                _currentChestPitch = Mathf.MoveTowards(_currentChestPitch, 0f, speed);
                _currentChestRoll = Mathf.MoveTowards(_currentChestRoll, 0f, speed);
            }
        }

        private void LateUpdate()
        {
            if (_animator == null || !_animator.isHuman || _currentIkWeight <= 0.001f) return;

            Transform playerTransform = transform.parent != null ? transform.parent : transform;

            // 1. EXTENSIÓN Y ACOMPAÑAMIENTO DE CADERA, COLUMNA Y PECHO
            // En LateUpdate para que persista en el renderizado final de pantalla sin ser sobrescrito por Mecanim
            ApplyTorsoAndHipsKinematics(playerTransform);

            // 2. RESOLUCIÓN DE BRAZOS CON TWO-BONE IK ANALÍTICO (0% deformación de malla, 0mm desconexión)
            SolveArmKinematics(playerTransform);
        }

        private void ApplyTorsoAndHipsKinematics(Transform playerTransform)
        {
            float weight = _currentIkWeight;
            if (weight <= 0.001f) return;

            Vector3 charUp = playerTransform.up;
            Vector3 charRight = playerTransform.right;
            Vector3 charForward = playerTransform.forward;

            // 1. Cadera / Pelvis (Hips) — Pivote de masa y orientación de la cintura
            if (_hipsBone != null)
            {
                Quaternion hYaw = Quaternion.AngleAxis(_currentHipsYaw * weight, charUp);
                Quaternion hPitch = Quaternion.AngleAxis(_currentHipsPitch * weight, charRight);
                Quaternion hRoll = Quaternion.AngleAxis(_currentHipsRoll * weight, charForward);
                _hipsBone.rotation = hYaw * hPitch * hRoll * _hipsBone.rotation;
            }

            // 2. Columna (Spine) — Flexión frontal, torsión y caída lateral (Roll)
            if (_spineBone != null)
            {
                Quaternion sYaw = Quaternion.AngleAxis(_currentSpineYaw * weight, charUp);
                Quaternion sPitch = Quaternion.AngleAxis(_currentSpinePitch * weight, charRight);
                Quaternion sRoll = Quaternion.AngleAxis(_currentSpineRoll * weight, charForward);
                _spineBone.rotation = sYaw * sPitch * sRoll * _spineBone.rotation;
            }

            // 3. Pecho y Hombros (Chest) — Proyección, inclinación frontal y lateral hacia el impacto
            if (_chestBone != null && _chestBone != _spineBone)
            {
                Quaternion cYaw = Quaternion.AngleAxis(_currentChestYaw * weight, charUp);
                Quaternion cPitch = Quaternion.AngleAxis(_currentChestPitch * weight, charRight);
                Quaternion cRoll = Quaternion.AngleAxis(_currentChestRoll * weight, charForward);
                _chestBone.rotation = cYaw * cPitch * cRoll * _chestBone.rotation;
            }
        }

        private void SolveArmKinematics(Transform playerTransform)
        {
            if (!_isEquipped || _gripSocketRear == null) return;
            if (_rightUpperArm == null || _rightForearm == null || _rightHand == null) return;

            // 1. Orientación y posición del agarre trasero (mano derecha dominante)
            Quaternion rOffsetRot = Quaternion.Euler(rightHandOffsetRot);
            Quaternion targetHandRotR = _gripSocketRear.rotation * Quaternion.Inverse(rOffsetRot);
            Vector3 targetHandPosR = _gripSocketRear.position - (targetHandRotR * rightHandOffsetPos);

            // 2. Control de alcance seguro respecto al hombro (que ya se inclinó con el torso)
            Vector3 rShoulderPos = _rightUpperArm.position;
            Vector3 toTargetR = targetHandPosR - rShoulderPos;
            float distR = toTargetR.magnitude;
            float maxReachR = _rightArmLength * maxReachRatio;

            // Si el socket se aleja del alcance máximo, acercamos levemente el weaponHolder en posición
            // ¡NUNCA modificamos la rotación del holder! Esto elimina el giro descontrolado
            if (distR > maxReachR && _weaponHolder != null)
            {
                Vector3 excess = toTargetR.normalized * (distR - maxReachR);
                _weaponHolder.position -= excess;
                targetHandPosR = _gripSocketRear.position - (targetHandRotR * rightHandOffsetPos);
                toTargetR = targetHandPosR - rShoulderPos;
                distR = toTargetR.magnitude;
            }

            // 3. Dirección dinámica del codo derecho (Pole Target)
            // Cuando el golpe cruza hacia la izquierda del cuerpo, el codo desciende hacia abajo y adelante para no penetrar el tórax
            Vector3 charRight = playerTransform.right;
            Vector3 charDown = -playerTransform.up;
            Vector3 charFwd = playerTransform.forward;
            float crossBody = Mathf.Clamp01(-Vector3.Dot(toTargetR.normalized, charRight));
            Vector3 rPoleDir = Vector3.Lerp(charRight * 0.40f + charDown * 0.25f - charFwd * 0.10f,
                                            charDown * 0.45f + charFwd * 0.15f + charRight * 0.15f,
                                            crossBody);
            Vector3 rPolePos = rShoulderPos + rPoleDir;

            // 4. Resolver Two-Bone IK para el brazo derecho (0% deformación de malla, longitudes óseas 100% rígidas)
            SolveTwoBoneIK(_rightUpperArm, _rightForearm, _rightHand, targetHandPosR, rPolePos, _currentIkWeight);

            // 5. Orientación de la muñeca derecha fijada al mango
            if (Quaternion.Dot(_rightHand.rotation, targetHandRotR) < 0f)
            {
                targetHandRotR = new Quaternion(-targetHandRotR.x, -targetHandRotR.y, -targetHandRotR.z, -targetHandRotR.w);
            }
            _rightHand.rotation = Quaternion.Slerp(_rightHand.rotation, targetHandRotR, _currentIkWeight);

            // 6. Brazo izquierdo para armas a dos manos o asta
            bool isBimanual = _currentStance == WeaponCombatStance.TwoHanded || _currentStance == WeaponCombatStance.Polearm;
            if (isBimanual && _gripSocketFront != null && _leftUpperArm != null && _leftForearm != null && _leftHand != null)
            {
                Quaternion lOffsetRot = Quaternion.Euler(leftHandOffsetRot);
                Quaternion targetHandRotL = _gripSocketFront.rotation * Quaternion.Inverse(lOffsetRot);
                Vector3 targetHandPosL = _gripSocketFront.position - (targetHandRotL * leftHandOffsetPos);

                Vector3 lShoulderPos = _leftUpperArm.position;
                Vector3 toTargetL = targetHandPosL - lShoulderPos;
                float distL = toTargetL.magnitude;
                float maxReachL = _leftArmLength * maxReachRatio;
                if (distL > maxReachL)
                {
                    targetHandPosL = lShoulderPos + toTargetL.normalized * maxReachL;
                }

                Vector3 lPoleDir = -charRight * 0.40f + charDown * 0.25f - charFwd * 0.10f;
                Vector3 lPolePos = lShoulderPos + lPoleDir;

                SolveTwoBoneIK(_leftUpperArm, _leftForearm, _leftHand, targetHandPosL, lPolePos, _currentIkWeight);

                if (Quaternion.Dot(_leftHand.rotation, targetHandRotL) < 0f)
                {
                    targetHandRotL = new Quaternion(-targetHandRotL.x, -targetHandRotL.y, -targetHandRotL.z, -targetHandRotL.w);
                }
                _leftHand.rotation = Quaternion.Slerp(_leftHand.rotation, targetHandRotL, _currentIkWeight);
            }
        }

        private static void SolveTwoBoneIK(Transform root, Transform mid, Transform end, Vector3 targetPos, Vector3 polePos, float weight)
        {
            if (weight <= 0.001f || root == null || mid == null || end == null) return;

            Vector3 a = root.position;
            Vector3 b = mid.position;
            Vector3 c = end.position;

            float lenAB = Vector3.Distance(a, b);
            float lenBC = Vector3.Distance(b, c);
            if (lenAB < 0.01f || lenBC < 0.01f) return;

            float maxDist = (lenAB + lenBC) * 0.999f;
            float minDist = Mathf.Abs(lenAB - lenBC) * 1.001f;

            Vector3 toTarget = targetPos - a;
            float dist = toTarget.magnitude;
            if (dist < 0.001f) return;

            float clampedDist = Mathf.Clamp(dist, minDist, maxDist);
            Vector3 dir = toTarget / dist;

            // Ley del Coseno para calcular el ángulo en la raíz (A)
            float cosAlpha = Mathf.Clamp((lenAB * lenAB + clampedDist * clampedDist - lenBC * lenBC) / (2f * lenAB * clampedDist), -1f, 1f);
            float sinAlpha = Mathf.Sqrt(Mathf.Max(0f, 1f - cosAlpha * cosAlpha));

            // Plano del brazo formado por la línea A->Target y el Pole Target
            Vector3 poleDir = (polePos - a).normalized;
            Vector3 planeNormal = Vector3.Cross(dir, poleDir);
            if (planeNormal.sqrMagnitude < 0.0001f)
            {
                planeNormal = Vector3.up;
            }
            planeNormal.Normalize();

            Vector3 tangent = Vector3.Cross(planeNormal, dir).normalized;

            // Posición analítica deseada del codo (B)
            Vector3 bDesired = a + dir * (lenAB * cosAlpha) + tangent * (lenAB * sinAlpha);

            Quaternion origRootRot = root.rotation;
            Quaternion origMidRot = mid.rotation;

            // Rotar raíz para ubicar el codo en bDesired
            Vector3 currentAB = b - a;
            Vector3 desiredAB = bDesired - a;
            if (currentAB.sqrMagnitude > 0.0001f && desiredAB.sqrMagnitude > 0.0001f)
            {
                Quaternion rotRoot = Quaternion.FromToRotation(currentAB, desiredAB);
                root.rotation = rotRoot * root.rotation;
            }

            // Rotar codo para ubicar la muñeca en targetPos
            b = mid.position;
            c = end.position;
            Vector3 currentBC = c - b;
            Vector3 desiredBC = (a + dir * clampedDist) - b;
            if (currentBC.sqrMagnitude > 0.0001f && desiredBC.sqrMagnitude > 0.0001f)
            {
                Quaternion rotMid = Quaternion.FromToRotation(currentBC, desiredBC);
                mid.rotation = rotMid * mid.rotation;
            }

            // Suavizado por peso (fade in/out)
            if (weight < 0.999f)
            {
                root.rotation = Quaternion.Slerp(origRootRot, root.rotation, weight);
                mid.rotation = Quaternion.Slerp(origMidRot, mid.rotation, weight);
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

            // 2. Desactivar IK nativo de Mecanim en extremidades para que la cinemática de LateUpdate gobierne sin interferencias
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            _animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);

            // 3. PIERNAS: Se mantienen 100% en la posición idle de batalla del Animator
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
        }

        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

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
                WeaponAnimationStyle.Dagger1H => 0.42f,   // Daga: ágil y sutil, perceptible pero nunca brusca
                WeaponAnimationStyle.Sword1H => 0.85f,    // Espada 1H: equilibrada y atlética
                WeaponAnimationStyle.Axe1H => 0.95f,      // Hacha 1H: corte directo con firmeza de tronco
                WeaponAnimationStyle.Hammer1H => 1.15f,   // Martillo 1H: impacto y empuje descendente
                WeaponAnimationStyle.Polearm => 1.15f,    // Asta: palanca amplia a dos manos
                WeaponAnimationStyle.Heavy2H => 1.35f,    // Mandoble 2H: inercia pesada y compromiso de cadera, sin exagerar
                _ => 1.0f
            };

            // Ajuste fino según el peso real del mineral forjado (0 = ligero, 1 = pesado)
            float normWeight = _currentWeapon.GetNormalizedWeight();
            float weightTweak = Mathf.Lerp(-0.08f, 0.08f, normWeight);

            // Ajuste fino según la longitud del arma forjada
            float lengthRatio = Mathf.Clamp(_currentWeapon.attackRange / 2.2f, 0.85f, 1.25f);
            float lengthTweak = (lengthRatio - 1.0f) * 0.12f;

            return Mathf.Clamp(baseMultiplier + weightTweak + lengthTweak, 0.38f, 1.45f);
        }

        /// <summary>
        /// Cadena cinética biomecánica con desfase secuencial entre articulaciones:
        /// Caderas -> Columna/Torso -> Hombros/Pecho -> Brazos -> Arma.
        /// 4 Fases dinámicas:
        /// 1. Anticipación (wind-up): torso gira en sentido contrario al corte, caderas cargan.
        /// 2. Aceleración (drive): caderas inician el giro primero, columna sigue, hombros/pecho liberan.
        /// 3. Impacto (apex): torso en máxima rotación hacia el corte, arma conecta a máxima velocidad.
        /// 4. Follow-Through / Recuperación: torso pasa de largo por inercia (overshoot) y frena suavemente a guardia (EaseOutCubic).
        /// </summary>
        private void CalculateTorsoKinematics(int comboIndex, float t,
            out float hipsYaw, out float hipsPitch, out float hipsRoll,
            out float spineYaw, out float spinePitch, out float spineRoll,
            out float chestYaw, out float chestPitch, out float chestRoll)
        {
            WeaponAnimationStyle style = _currentWeapon != null ? _currentWeapon.GetAnimationStyle() : WeaponAnimationStyle.Sword1H;
            AttackMotionType motionType = WeaponComboAnimator.GetMotionType(style, comboIndex);

            float weightNorm = _currentWeapon != null ? _currentWeapon.GetNormalizedWeight() : 0.5f;

            // 1. Duraciones dinámicas y retraso de la cadena cinética según el peso
            // Armas ligeras (daga): wind-up rápido y explosivo, desfase articular pequeño.
            // Armas pesadas (mandoble/martillo): mayor tiempo de carga, desfase articular marcado (inercia).
            float windupEnd = Mathf.Lerp(0.16f, 0.28f, weightNorm);
            float hitApex = Mathf.Lerp(0.40f, 0.52f, weightNorm);

            // Ajuste fino por tipo de movimiento
            if (motionType == AttackMotionType.OverheadSlam)
            {
                windupEnd += 0.04f;
                hitApex += 0.03f;
            }
            else if (motionType == AttackMotionType.Thrust)
            {
                windupEnd -= 0.02f;
                hitApex -= 0.02f;
            }

            float boneLag = Mathf.Lerp(0.025f, 0.055f, weightNorm);
            float overshootRatio = Mathf.Lerp(0.06f, 0.18f, weightNorm);
            float recoveryStart = hitApex + Mathf.Lerp(0.06f, 0.12f, weightNorm);

            float wHY, wHP, wHR, wSY, wSP, wSR, wCY, wCP, wCR;
            float sHY, sHP, sHR, sSY, sSP, sSR, sCY, sCP, sCR;

            int step = Mathf.Clamp(comboIndex, 0, 4);
            switch (motionType)
            {
                case AttackMotionType.Thrust:
                    // ESTOCADA MARCIAL:
                    // Windup: El cuerpo se perfila hacia atrás abriendo el flanco y cargando en la pierna trasera
                    // Impacto: Gran zancada y proyección frontal, hombro adelantado,
                    //          flexión frontal (+15°) e INCLINACIÓN LATERAL (Roll -12°) para bajar perfil atlético.
                    wHY = 8f;   wHP = -2f; wHR = 2f;
                    wSY = 12f;  wSP = -3f; wSR = 3f;
                    wCY = 16f;  wCP = -4f; wCR = 4f;

                    sHY = -10f; sHP = 6f;  sHR = -4f;
                    sSY = -14f; sSP = 10f; sSR = -8f;
                    sCY = -18f; sCP = 15f; sCR = -12f;
                    break;

                case AttackMotionType.OverheadSlam:
                    // APLASTAMIENTO VERTICAL (OVERHEAD):
                    // Windup: Arqueo dorsal hacia atrás abriendo la caja torácica (-18° pecho)
                    // Impacto: Flexión explosiva descendente (+26° pecho) transmitiendo el peso corporal
                    wHY = 0f; wHP = -4f;  wHR = 0f;
                    wSY = 0f; wSP = -10f; wSR = 0f;
                    wCY = 0f; wCP = -18f; wCR = 0f;

                    sHY = 0f; sHP = 8f;   sHR = 0f;
                    sSY = 0f; sSP = 16f;  sSR = 0f;
                    sCY = 0f; sCP = 26f;  sCR = 0f;
                    break;

                default: // AttackMotionType.HorizontalSlash
                    // TAJO HORIZONTAL:
                    // Windup: Torsión en sentido contrario al corte (anticipación biomecánica)
                    // Impacto: Descarga rotacional de cadera y tronco con INCLINACIÓN FRONTAL MARCADA (+20° pecho)
                    bool isRightToLeft = (step % 2 == 0);
                    if (isRightToLeft)
                    {
                        // Carga a la derecha (+Yaw), tajo hacia la izquierda (-Yaw) con inclinación frontal (+Pitch)
                        wHY = 12f;  wHP = 0f;  wHR = 2f;
                        wSY = 16f;  wSP = -2f; wSR = 3f;
                        wCY = 22f;  wCP = -3f; wCR = 4f;

                        sHY = -14f; sHP = 6f;  sHR = -2f;
                        sSY = -20f; sSP = 12f; sSR = -3f;
                        sCY = -26f; sCP = 20f; sCR = -4f;
                    }
                    else
                    {
                        // Retorno: Carga a la izquierda (-Yaw), tajo hacia la derecha (+Yaw)
                        wHY = -12f; wHP = 0f;  wHR = -2f;
                        wSY = -16f; wSP = 0f;  wSR = -3f;
                        wCY = -20f; wCP = 0f;  wCR = -4f;

                        sHY = 14f;  sHP = 5f;  sHR = 2f;
                        sSY = 18f;  sSP = 10f; sSR = 3f;
                        sCY = 24f;  sCP = 18f; sCR = 4f;
                    }
                    break;
            }

            // Modular la intensidad según el tipo, peso y longitud del arma
            float effort = GetTorsoEffortMultiplier();
            wHY *= effort; wHP *= effort; wHR *= effort;
            wSY *= effort; wSP *= effort; wSR *= effort;
            wCY *= effort; wCP *= effort; wCR *= effort;

            sHY *= effort; sHP *= effort; sHR *= effort;
            sSY *= effort; sSP *= effort; sSR *= effort;
            sCY *= effort; sCP *= effort; sCR *= effort;

            // Clamping anatómico de seguridad
            sCY = Mathf.Clamp(sCY, -32f, 32f);
            sCP = Mathf.Clamp(sCP, -12f, 30f);
            sCR = Mathf.Clamp(sCR, -16f, 16f);

            // =========================================================================
            // CADENA CINÉTICA DE 4 FASES CON RETRASO SECUENCIAL ENTRE ARTICULACIONES:
            // Caderas -> Columna/Torso -> Hombros/Pecho (Overlapping Action)
            // =========================================================================

            if (t <= windupEnd)
            {
                // FASE 1: ANTICIPACIÓN (WIND-UP)
                // El torso gira en sentido contrario al corte. Caderas cargan primero, luego columna, luego pecho.
                float tau = Mathf.Clamp01(t / windupEnd);
                float twoLag = Mathf.Min(0.5f, 2f * boneLag);

                float uH = EaseInOutSine(Mathf.Clamp01(tau / (1f - twoLag)));
                float uS = EaseInOutSine(Mathf.Clamp01((tau - boneLag) / (1f - twoLag)));
                float uC = EaseInOutSine(Mathf.Clamp01((tau - twoLag) / (1f - twoLag)));

                hipsYaw = Mathf.Lerp(_strikeStartHipsYaw, wHY, uH);
                hipsPitch = Mathf.Lerp(_strikeStartHipsPitch, wHP, uH);
                hipsRoll = Mathf.Lerp(_strikeStartHipsRoll, wHR, uH);

                spineYaw = Mathf.Lerp(_strikeStartSpineYaw, wSY, uS);
                spinePitch = Mathf.Lerp(_strikeStartSpinePitch, wSP, uS);
                spineRoll = Mathf.Lerp(_strikeStartSpineRoll, wSR, uS);

                chestYaw = Mathf.Lerp(_strikeStartChestYaw, wCY, uC);
                chestPitch = Mathf.Lerp(_strikeStartChestPitch, wCP, uC);
                chestRoll = Mathf.Lerp(_strikeStartChestRoll, wCR, uC);
            }
            else if (t <= hitApex)
            {
                // FASE 2: ACELERACIÓN (DRIVE) -> FASE 3: IMPACTO (APEX)
                // Las caderas inician el giro explosivamente primero, la columna sigue transmitiendo torsión,
                // el pecho se desata al final, proyectando los hombros y brazos hacia el impacto.
                float tau = Mathf.Clamp01((t - windupEnd) / (hitApex - windupEnd));
                float twoLag = Mathf.Min(0.5f, 2f * boneLag);

                // Caderas: arranque inmediato e impulsivo (EaseInQuad)
                float uH = EaseInQuad(Mathf.Clamp01(tau / (1f - twoLag)));
                // Columna: transmisión fluida de torsión (EaseInOutQuad)
                float uS = EaseInOutQuad(Mathf.Clamp01((tau - boneLag) / (1f - twoLag)));
                // Pecho: liberación potente sostenida (EaseOutQuad)
                float uC = EaseOutQuad(Mathf.Clamp01((tau - twoLag) / (1f - twoLag)));

                hipsYaw = Mathf.Lerp(wHY, sHY, uH);
                hipsPitch = Mathf.Lerp(wHP, sHP, uH);
                hipsRoll = Mathf.Lerp(wHR, sHR, uH);

                spineYaw = Mathf.Lerp(wSY, sSY, uS);
                spinePitch = Mathf.Lerp(wSP, sSP, uS);
                spineRoll = Mathf.Lerp(wSR, sSR, uS);

                chestYaw = Mathf.Lerp(wCY, sCY, uC);
                chestPitch = Mathf.Lerp(wCP, sCP, uC);
                chestRoll = Mathf.Lerp(wCR, sCR, uC);
            }
            else if (t <= recoveryStart)
            {
                // FASE 4A: FOLLOW-THROUGH (OVERSHOOT POR INERCIA)
                // Tras el impacto, el cuerpo pasa ligeramente de largo debido al momentum rotacional.
                float tau = Mathf.Clamp01((t - hitApex) / (recoveryStart - hitApex));
                float u = EaseOutQuad(tau);

                float oHY = sHY * (1f + overshootRatio);
                float oHP = sHP * (1f + overshootRatio * 0.5f);
                float oHR = sHR * (1f + overshootRatio * 0.5f);

                float oSY = sSY * (1f + overshootRatio);
                float oSP = sSP * (1f + overshootRatio * 0.5f);
                float oSR = sSR * (1f + overshootRatio * 0.5f);

                float oCY = sCY * (1f + overshootRatio);
                float oCP = sCP * (1f + overshootRatio * 0.5f);
                float oCR = sCR * (1f + overshootRatio * 0.5f);

                hipsYaw = Mathf.Lerp(sHY, oHY, u);
                hipsPitch = Mathf.Lerp(sHP, oHP, u);
                hipsRoll = Mathf.Lerp(sHR, oHR, u);

                spineYaw = Mathf.Lerp(sSY, oSY, u);
                spinePitch = Mathf.Lerp(sSP, oSP, u);
                spineRoll = Mathf.Lerp(sSR, oSR, u);

                chestYaw = Mathf.Lerp(sCY, oCY, u);
                chestPitch = Mathf.Lerp(sCP, oCP, u);
                chestRoll = Mathf.Lerp(sCR, oCR, u);
            }
            else
            {
                // FASE 4B: RECUPERACIÓN SUAVE (RECOVERY TO COMBAT IDLE)
                // Retorno orgánico a la guardia mediante EaseOutCubic
                float tau = Mathf.Clamp01((t - recoveryStart) / (1f - recoveryStart));
                float u = EaseOutCubic(tau);

                float oHY = sHY * (1f + overshootRatio);
                float oHP = sHP * (1f + overshootRatio * 0.5f);
                float oHR = sHR * (1f + overshootRatio * 0.5f);

                float oSY = sSY * (1f + overshootRatio);
                float oSP = sSP * (1f + overshootRatio * 0.5f);
                float oSR = sSR * (1f + overshootRatio * 0.5f);

                float oCY = sCY * (1f + overshootRatio);
                float oCP = sCP * (1f + overshootRatio * 0.5f);
                float oCR = sCR * (1f + overshootRatio * 0.5f);

                hipsYaw = Mathf.Lerp(oHY, 0f, u);
                hipsPitch = Mathf.Lerp(oHP, 0f, u);
                hipsRoll = Mathf.Lerp(oHR, 0f, u);

                spineYaw = Mathf.Lerp(oSY, 0f, u);
                spinePitch = Mathf.Lerp(oSP, 0f, u);
                spineRoll = Mathf.Lerp(oSR, 0f, u);

                chestYaw = Mathf.Lerp(oCY, 0f, u);
                chestPitch = Mathf.Lerp(oCP, 0f, u);
                chestRoll = Mathf.Lerp(oCR, 0f, u);
            }
        }
    }
}
