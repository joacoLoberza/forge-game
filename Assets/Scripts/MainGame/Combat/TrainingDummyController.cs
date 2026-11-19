using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.UI;

namespace ForjaGame.Combat
{
    public enum DummyRole { StrikeTarget = 0, SparringTrainer = 1 }

    /// <summary>
    /// Muñeco medieval de entrenamiento en combate (hereda de CaveCreatureBase para integrarse con
    /// AttackController, bloqueo pasivo, Parry con estrellas de stun y retroalimentación audiovisual).
    /// Nunca muere y oscila físicamente como un poste con resorte de madera.
    /// </summary>
    public class TrainingDummyController : CaveCreatureBase
    {
        [SerializeField] private DummyRole role = DummyRole.StrikeTarget;
        private Transform _visualRig, _armPivot;
        private Renderer _telegraphRingRenderer;
        private Material _telegraphMat;
        private float _wobbleAmp, _wobbleTimer, _sparPhaseTimer, _sparCooldown = 0.9f, _manualSparUntil;
        private int _sparPhase; // 0=Idle, 1=Windup, 2=SwingFollowThrough
        private bool _dodgedCurrentWindup;

        public DummyRole Role => role;
        public bool IsTelegraphingStrike => role == DummyRole.SparringTrainer && _sparPhase == 1 && !IsStunned;
        public bool IsInParrySweetSpot => IsTelegraphingStrike && _sparPhaseTimer >= 1.08f;
        public float WindupNormalized => IsTelegraphingStrike ? Mathf.Clamp01(_sparPhaseTimer / 1.50f) : 0f;
        protected override string DeathAnimKey => "idle";
        protected override void OnHurtAudio() { }

        private void Awake()
        {
            maxHealth = currentHealth = 9999;
            mapSpawnPoint = transform.position;
            BuildDummyVisuals();
        }

        public void ConfigureDummy(DummyRole dummyRole, Vector3 worldPos, Vector3 lookToward)
        {
            role = dummyRole;
            maxHealth = currentHealth = 9999;
            transform.position = worldPos;
            mapSpawnPoint = worldPos;
            Vector3 flat = lookToward - worldPos; flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat.normalized);
            BuildDummyVisuals();
        }

        private void BuildDummyVisuals()
        {
            _col = UnitySafeUtility.GetOrAdd<CapsuleCollider>(gameObject);
            _col.center = new Vector3(0f, 1.05f, 0f); _col.radius = 0.58f; _col.height = 2.15f;
            _rb = UnitySafeUtility.GetOrAdd<Rigidbody>(gameObject);
            _rb.isKinematic = true; _rb.useGravity = false;

            if (_visualRig != null) return;
            var rigGo = new GameObject("VisualRig");
            rigGo.transform.SetParent(transform, false);
            _visualRig = rigGo.transform;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material wood = MakeMat(lit, "Mat_DummyWood", new Color(0.32f, 0.21f, 0.12f), 0f, 0.15f);
            Material sack = MakeMat(lit, "Mat_DummySack", new Color(0.72f, 0.60f, 0.42f), 0f, 0.08f);
            Material leather = MakeMat(lit, "Mat_DummyLeather", new Color(0.24f, 0.14f, 0.08f), 0.1f, 0.28f);
            Material steel = MakeMat(lit, "Mat_DummyHelm", new Color(0.54f, 0.56f, 0.60f), 0.72f, 0.50f);

            // Base en cruz + poste central
            MakePart(PrimitiveType.Cube, _visualRig, "BaseA", new Vector3(0f, 0.05f, 0f), new Vector3(1.10f, 0.10f, 0.20f), Quaternion.identity, wood);
            MakePart(PrimitiveType.Cube, _visualRig, "BaseB", new Vector3(0f, 0.05f, 0f), new Vector3(0.20f, 0.10f, 1.10f), Quaternion.identity, wood);
            MakePart(PrimitiveType.Cylinder, _visualRig, "Post", new Vector3(0f, 0.85f, 0f), new Vector3(0.16f, 0.80f, 0.16f), Quaternion.identity, wood);

            // Torso acolchado, hombreras y cabeza con yelmo/diana
            MakePart(PrimitiveType.Cylinder, _visualRig, "Torso", new Vector3(0f, 1.18f, 0f), new Vector3(0.56f, 0.42f, 0.46f), Quaternion.identity, sack);
            MakePart(PrimitiveType.Cylinder, _visualRig, "Belt", new Vector3(0f, 1.12f, 0f), new Vector3(0.59f, 0.07f, 0.49f), Quaternion.identity, leather);
            MakePart(PrimitiveType.Cube, _visualRig, "Shoulders", new Vector3(0f, 1.50f, 0f), new Vector3(1.08f, 0.14f, 0.18f), Quaternion.identity, wood);
            MakePart(PrimitiveType.Sphere, _visualRig, "Head", new Vector3(0f, 1.84f, 0f), new Vector3(0.36f, 0.38f, 0.36f), Quaternion.identity, sack);
            MakePart(PrimitiveType.Cylinder, _visualRig, "HelmRim", new Vector3(0f, 1.92f, 0f), new Vector3(0.39f, 0.10f, 0.39f), Quaternion.identity, role == DummyRole.SparringTrainer ? steel : leather);

            if (role == DummyRole.SparringTrainer)
            {
                var pivot = new GameObject("SparringArmPivot").transform;
                pivot.SetParent(_visualRig, false);
                pivot.localPosition = new Vector3(0.54f, 1.48f, 0f);
                _armPivot = pivot;
                MakePart(PrimitiveType.Cube, pivot, "ArmWood", new Vector3(0f, -0.22f, 0.24f), new Vector3(0.11f, 0.11f, 0.62f), Quaternion.identity, wood);
                MakePart(PrimitiveType.Cube, pivot, "BatonHead", new Vector3(0f, -0.22f, 0.68f), new Vector3(0.16f, 0.16f, 0.42f), Quaternion.identity, leather);

                var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ringGo.name = "SparringTelegraphRing";
                ringGo.transform.SetParent(transform, false);
                ringGo.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                ringGo.transform.localScale = new Vector3(3.6f, 0.015f, 3.6f);
                if (ringGo.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
                _telegraphRingRenderer = ringGo.GetComponent<MeshRenderer>();
                _telegraphMat = MakeMat(lit, "Mat_DummyTelegraph", new Color(0.25f, 0.65f, 1f), 0f, 0.3f);
                _telegraphMat.EnableKeyword("_EMISSION");
                _telegraphRingRenderer.sharedMaterial = _telegraphMat;
                _telegraphRingRenderer.enabled = false;
            }
        }

        public override void TakeDamage(int amount)
        {
            currentHealth = maxHealth;
            _wobbleAmp = Mathf.Min(16f, _wobbleAmp + 10f);
            _wobbleTimer = 0.55f;
            CombatDamageNumberHUD.SpawnAbove(transform, amount, false, amount >= 25);
            if (role == DummyRole.SparringTrainer) _manualSparUntil = Time.time + 15f;
            ForgeAudioSystem.PlayWallHit(transform.position + Vector3.up * 1.1f, 1, true);
            ForgeTutorialDirector.Instance?.NotifyDummyHit(this);
        }

        protected override void OnParryStunned()
        {
            _sparPhase = 0; _sparPhaseTimer = 0f; _sparCooldown = 2.15f;
            if (_armPivot != null) _armPivot.localRotation = Quaternion.Euler(28f, 0f, 0f);
            if (_telegraphRingRenderer != null) _telegraphRingRenderer.enabled = false;
            _wobbleAmp = 14f; _wobbleTimer = 0.65f;
            ForgeTutorialDirector.Instance?.NotifyDummyParried();
        }

        private void Update()
        {
            if (_wobbleTimer > 0f && _visualRig != null)
            {
                _wobbleTimer = Mathf.Max(0f, _wobbleTimer - Time.deltaTime);
                float tilt = Mathf.Sin(_wobbleTimer * 26f) * _wobbleAmp * (_wobbleTimer / 0.55f);
                _visualRig.localRotation = Quaternion.Euler(-tilt, 0f, tilt * 0.3f);
            }
            else if (_visualRig != null && _sparPhase == 0)
            {
                _visualRig.localRotation = Quaternion.Lerp(_visualRig.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                _visualRig.localPosition = Vector3.Lerp(_visualRig.localPosition, Vector3.zero, Time.deltaTime * 10f);
                _visualRig.localScale = Vector3.Lerp(_visualRig.localScale, Vector3.one, Time.deltaTime * 10f);
            }

            if (role != DummyRole.SparringTrainer) return;
            if (IsStunned) { if (_telegraphRingRenderer != null) _telegraphRingRenderer.enabled = false; return; }
            UpdateSparringCycle();
        }

        private void UpdateSparringCycle()
        {
            Transform player = FindClosestLivingPlayer(transform.position);
            bool tutWantsSpar = ForgeTutorialDirector.Instance != null && ForgeTutorialDirector.Instance.IsDummySparringRequested;
            bool freeSpar = Time.time < _manualSparUntil;
            bool inRange = player != null && !ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen
                && Vector3.Distance(transform.position, player.position) <= 5.4f;

            if (!inRange || (!tutWantsSpar && !freeSpar))
            {
                _sparPhase = 0; _sparPhaseTimer = 0f;
                if (_armPivot != null) _armPivot.localRotation = Quaternion.Lerp(_armPivot.localRotation, Quaternion.identity, Time.deltaTime * 6f);
                if (_telegraphRingRenderer != null) _telegraphRingRenderer.enabled = false;
                return;
            }

            Vector3 look = player.position - transform.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f && _sparPhase != 2)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized), Time.deltaTime * 6f);

            if (_sparPhase == 0)
            {
                _sparCooldown -= Time.deltaTime;
                if (_armPivot != null) _armPivot.localRotation = Quaternion.Lerp(_armPivot.localRotation, Quaternion.identity, Time.deltaTime * 8f);
                if (_telegraphRingRenderer != null) _telegraphRingRenderer.enabled = false;
                if (_sparCooldown <= 0f) { _sparPhase = 1; _sparPhaseTimer = 0f; _dodgedCurrentWindup = false; ForgeAudioSystem.PlayWeaponSwing(transform.position, false); }
            }
            else if (_sparPhase == 1)
            {
                _sparPhaseTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_sparPhaseTimer / 1.50f);
                float ease = t * t * (3f - 2f * t);
                if (_visualRig != null)
                {
                    _visualRig.localRotation = Quaternion.Euler(-24f * ease, 0f, 0f);
                    _visualRig.localPosition = new Vector3(0f, 0.04f * ease, -0.22f * ease);
                    _visualRig.localScale = new Vector3(1f - 0.06f * ease, 1f + 0.14f * ease, 1f - 0.04f * ease);
                }
                if (_armPivot != null) _armPivot.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -96f, ease), 0f, -18f * ease);
                UpdateTelegraphVisuals();
                if (_sparPhaseTimer >= 1.50f) { _sparPhase = 2; _sparPhaseTimer = 0f; DeliverDummySwingImpact(player); }
            }
            else
            {
                _sparPhaseTimer += Time.deltaTime;
                if (_visualRig != null)
                {
                    float lunge = _sparPhaseTimer < 0.16f ? (_sparPhaseTimer / 0.16f) : (1f - Mathf.Clamp01((_sparPhaseTimer - 0.16f) / 0.34f));
                    _visualRig.localRotation = Quaternion.Euler(Mathf.Lerp(-10f, 25f, lunge), 0f, 0f);
                    _visualRig.localPosition = new Vector3(0f, 0f, Mathf.Lerp(0f, 0.28f, lunge));
                    _visualRig.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.07f, 0.92f, 1.07f), lunge);
                }
                float t = Mathf.Clamp01(_sparPhaseTimer / 0.22f);
                if (_armPivot != null) _armPivot.localRotation = Quaternion.Euler(Mathf.Lerp(-96f, 48f, t), 0f, 0f);
                if (_telegraphRingRenderer != null) _telegraphRingRenderer.enabled = false;
                if (_sparPhaseTimer >= 0.50f) { _sparPhase = 0; _sparCooldown = 1.15f; }
            }
        }

        private void UpdateTelegraphVisuals()
        {
            if (_telegraphRingRenderer == null || _telegraphMat == null) return;
            _telegraphRingRenderer.enabled = true;
            bool inParrySpot = IsInParrySweetSpot;
            bool isParryLesson = ForgeTutorialDirector.Instance != null && ForgeTutorialDirector.Instance.IsDummyParryLessonActive;
            bool isRollLesson = ForgeTutorialDirector.Instance != null && ForgeTutorialDirector.Instance.IsDummyRollLessonActive;
            Color col = isRollLesson ? (WindupNormalized >= 0.30f ? new Color(0.28f, 0.95f, 1f) : new Color(0.18f, 0.55f, 0.95f)) : ((isParryLesson && inParrySpot) ? new Color(1f, 0.82f, 0.14f) : (isParryLesson ? new Color(0.95f, 0.30f, 0.16f) : new Color(0.24f, 0.68f, 1.0f)));
            _telegraphMat.color = col; if (_telegraphMat.HasProperty("_BaseColor")) _telegraphMat.SetColor("_BaseColor", col);
            if (_telegraphMat.HasProperty("_EmissionColor")) _telegraphMat.SetColor("_EmissionColor", col * ((inParrySpot || (isRollLesson && WindupNormalized >= 0.30f)) ? 3.2f : 1.5f));
        }

        public void NotifyPlayerRollAttempt(ForgeTutorialDirector tut)
        {
            if (tut == null || !tut.IsDummyRollLessonActive) return;
            if (IsTelegraphingStrike && WindupNormalized >= 0.30f) _dodgedCurrentWindup = true; else tut.NotifyDummyRollTooEarly();
        }

        private void DeliverDummySwingImpact(Transform player)
        {
            bool dodged = _dodgedCurrentWindup; _dodgedCurrentWindup = false;
            ForgeAudioSystem.PlayWeaponSwing(transform.position, true);
            if (player == null || Vector3.Distance(transform.position, player.position) > 3.35f) { if (dodged) ForgeTutorialDirector.Instance?.NotifyDummyRolled(); return; }
            var hp = player.GetComponentInParent<PlayerHealthController>();
            if (hp == null || !hp.IsAlive) return;
            hp.TakeDamageFrom(4, this, isRanged: false);
        }

        private static Material MakeMat(Shader s, string name, Color col, float met, float sm)
        {
            var m = new Material(s) { name = name, color = col };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", met);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", sm);
            return m;
        }

        private static void MakePart(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale; go.transform.localRotation = rot;
            if (go.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}

