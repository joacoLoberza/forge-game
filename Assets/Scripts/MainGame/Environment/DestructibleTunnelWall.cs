using System.Collections;
using UnityEngine;
using ForjaGame.Combat;
using ForjaGame.Forge;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.UI;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Pared destructible instalada en los arcos de túnel entre cámaras.
    /// Bloquea el paso hasta ser derribada. Recibe daño x5 con su arma vulnerable correspondiente:
    /// - Tela de araña -> Espadas (x5 daño)
    /// - Maderas -> Hachas (x5 daño)
    /// - Ladrillos rotos -> Martillos (x5 daño)
    /// </summary>
    public class DestructibleTunnelWall : MonoBehaviour, IDestructibleTunnelWall
    {
        [SerializeField] private TunnelWallType wallType;
        [SerializeField] private int maxHealth = 180;
        [SerializeField] private BoxCollider wallCollider;

        private int _currentHealth;
        private bool _isDestroyed;
        private float _lastHitTime, _feedbackTime;
        private string _hitFeedbackText = string.Empty;
        private bool _wasLastHitEffective;
        private Vector3 _originalLocalPos;
        private Coroutine _wobbleRoutine;

        public static readonly System.Collections.Generic.List<DestructibleTunnelWall> ActiveWalls = new();
        public TunnelWallType WallType => wallType;
        public WeaponType EffectiveWeapon => wallType switch { TunnelWallType.Web => WeaponType.Sword, TunnelWallType.WoodPlanks => WeaponType.Axe, _ => WeaponType.Hammer };
        public bool IsAlive => !_isDestroyed && _currentHealth > 0;
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => maxHealth;
        private void OnEnable() { if (!ActiveWalls.Contains(this)) ActiveWalls.Add(this); }
        private void OnDisable() => ActiveWalls.Remove(this);

        private void Awake()
        {
            if (maxHealth <= 0)
                maxHealth = wallType switch { TunnelWallType.Web => 160, TunnelWallType.WoodPlanks => 240, _ => 300 };
            _currentHealth = maxHealth;
            _originalLocalPos = transform.localPosition;
            if (wallCollider == null) wallCollider = GetComponent<BoxCollider>();
        }

        public void Initialize(TunnelWallType type, int hp, BoxCollider col)
        {
            wallType = type;
            maxHealth = _currentHealth = hp;
            wallCollider = col;
            _originalLocalPos = transform.localPosition;
        }

        public void TakeDamage(int amount) => TakeWeaponDamage(amount, null, null);

        public void TakeWeaponDamage(int baseDamage, WeaponType? weaponType, GameObject attacker)
        {
            if (_isDestroyed || _currentHealth <= 0) return;

            bool isEffective = weaponType.HasValue && weaponType.Value == EffectiveWeapon;
            int finalDamage = isEffective ? Mathf.Max(1, baseDamage * 5) : Mathf.Max(1, baseDamage);

            _currentHealth = Mathf.Max(0, _currentHealth - finalDamage);
            ForjaGame.Hardware.ControladorLucesUDP.Instancia?.LuzGolpeForja();
            _lastHitTime = Time.time;
            _wasLastHitEffective = isEffective;
            _feedbackTime = Time.time + 1.85f;

            if (isEffective)
            {
                _hitFeedbackText = $"EFFECTIVE HIT x5! (-{finalDamage} HP)";
                PlayerCameraFollow.Instance?.TriggerImpulse(0.18f);
                PlayImpactSound(isHeavy: true, isEffective: true);
            }
            else
            {
                string weakName = EffectiveWeapon switch { WeaponType.Sword => "SWORD", WeaponType.Axe => "AXE", _ => "HAMMER" };
                _hitFeedbackText = $"-{finalDamage} HP • [Weak to: {weakName} (x5)]";
                PlayerCameraFollow.Instance?.TriggerImpulse(0.06f);
                PlayImpactSound(isHeavy: false, isEffective: false);
            }

            if (_wobbleRoutine != null) StopCoroutine(_wobbleRoutine);
            _wobbleRoutine = StartCoroutine(HitWobbleRoutine(isEffective ? 0.22f : 0.08f));
            if (wallType == TunnelWallType.Web) SpawnWebSilkShreds(transform.position + Vector3.up * 1.5f, isEffective ? 8 : 4);

            if (wallType == TunnelWallType.BrokenBricks && attacker != null)
            {
                var golems = GolemEnemyController.ActiveGolems;
                GolemEnemyController nearest = null; float minD = 35f * 35f;
                for (int i = 0; i < golems.Count; i++)
                {
                    if (golems[i] == null || !golems[i].IsAlive) continue;
                    if (CaveTargetVisibility.HasAliveWallBetweenZ(golems[i].transform.position.z, attacker.transform.position.z)) continue;
                    float d = (golems[i].transform.position - transform.position).sqrMagnitude;
                    if (d < minD) { minD = d; nearest = golems[i]; }
                }
                nearest?.TriggerWallDefenseRage(attacker.transform);
            }

            if (_currentHealth <= 0) BreakWall();
        }

        private void SpawnWebSilkShreds(Vector3 origin, int count)
        {
            var mat = DestructibleTunnelWallBuilder.GetWebMaterial();
            for (int i = 0; i < count; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (g.TryGetComponent<Collider>(out var c)) UnitySafeUtility.DestroySafe(c);
                g.GetComponent<MeshRenderer>().sharedMaterial = mat;
                g.transform.SetPositionAndRotation(origin + new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(-0.8f, 0.9f), Random.Range(-0.4f, 0.4f)), Random.rotation);
                g.transform.localScale = new Vector3(0.035f, Random.Range(0.28f, 0.55f), 0.035f);
                var rb = g.AddComponent<Rigidbody>(); rb.mass = 0.2f;
                rb.AddForce(new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(1.2f, 2.8f), Random.Range(-2.2f, 2.2f)), ForceMode.Impulse);
                Destroy(g, 0.85f);
            }
        }

        private void PlayImpactSound(bool isHeavy, bool isEffective) =>
            ForgeAudioSystem.PlayWallHit(transform.position + Vector3.up * 1.5f, (int)wallType, isEffective);

        private void BreakWall()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;
            _currentHealth = 0;
            if (wallCollider != null) wallCollider.enabled = false;
            PlayerCameraFollow.Instance?.TriggerImpulse(0.24f);

            Vector3 pos = transform.position + Vector3.up * 1.5f;
            if (wallType == TunnelWallType.BrokenBricks) ForgeAudioSystem.PlayGolemSound(pos, CreatureSoundType.Attack);
            else if (wallType == TunnelWallType.WoodPlanks) ForgeAudioSystem.PlayPressCrush(pos);
            else { ForgeAudioSystem.PlayChiselCut(pos); ForgeAudioSystem.PlaySpiderSound(pos, CreatureSoundType.Hurt); }

            string wallLabel = wallType switch { TunnelWallType.Web => "de Telaraña", TunnelWallType.WoodPlanks => "de Maderas", _ => "de Ladrillos Rotos" };
            Debug.Log($"<color=#33FFAA>[Pared Destruida]</color> ¡La barrera {wallLabel} ha sido derribada! Camino despejado.");
            if (wallType == TunnelWallType.Web) { ForgeTutorialDirector.Instance?.NotifyFirstWebWallBroken(this); StartCoroutine(TearWebRoutine()); }
            else StartCoroutine(CollapseDebrisRoutine());
        }

        private IEnumerator TearWebRoutine()
        {
            var children = new Transform[transform.childCount];
            var startPos = new Vector3[children.Length];
            var startScales = new Vector3[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                children[i] = transform.GetChild(i);
                startPos[i] = children[i].localPosition;
                startScales[i] = children[i].localScale;
            }
            float dur = 0.55f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float norm = t / dur, flutter = Mathf.Sin(t * 50f) * 0.05f * (1f - norm);
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i] == null) continue;
                    float sign = Mathf.Sign(startPos[i].x);
                    if (Mathf.Abs(sign) < 0.01f) sign = (i % 2 == 0) ? 1f : -1f;
                    children[i].localPosition = startPos[i] + new Vector3(sign * norm * 1.6f, flutter, 0f);
                    children[i].localScale = Vector3.Lerp(startScales[i], Vector3.zero, norm);
                }
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private IEnumerator HitWobbleRoutine(float strength)
        {
            Vector3 forward = transform.forward;
            for (float t = 0; t < 0.20f; t += Time.deltaTime)
            {
                float offset = Mathf.Sin(t * 38f) * strength * (1f - t / 0.20f);
                transform.localPosition = _originalLocalPos + forward * offset;
                yield return null;
            }
            transform.localPosition = _originalLocalPos;
        }

        private IEnumerator CollapseDebrisRoutine()
        {
            var rbs = GetComponentsInChildren<Rigidbody>(true);
            bool isWood = wallType == TunnelWallType.WoodPlanks;
            foreach (var rb in rbs)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearDamping = isWood ? 1.8f : 0.8f;
                rb.angularDamping = isWood ? 2.4f : 1.2f;
                Vector3 impulse = isWood
                    ? new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.15f, 0.65f), Random.Range(-0.4f, 0.4f)) + transform.forward * 0.45f
                    : new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.6f, 1.8f), Random.Range(-1.5f, 1.5f)) + transform.forward * 1.8f;
                rb.AddForce(impulse, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * (isWood ? 1.4f : 4.0f), ForceMode.Impulse);
            }

            yield return new WaitForSeconds(1.8f);
            Vector3 basePos = transform.localPosition;
            for (float t = 0; t < 0.8f; t += Time.deltaTime)
            {
                transform.localPosition = basePos + Vector3.down * (t * 2.8f);
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t / 0.8f);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private void OnGUI()
        {
            if (_isDestroyed || _currentHealth <= 0) return;
            Transform player = PlayerMovement.LocalInstance != null ? PlayerMovement.LocalInstance.transform : null;
            if (player == null) return;

            float dist = Vector3.Distance(player.position, transform.position);
            if (dist > 9.5f) return;
            float alpha = Mathf.Clamp01(1f - (dist - 4.5f) / 5.0f);
            if (alpha <= 0.05f) return;

            string title = wallType switch { TunnelWallType.Web => "COBWEB BARRIER", TunnelWallType.WoodPlanks => "WOODEN BARRICADE", _ => "CRUMBLING BRICK WALL" };
            string weakness = EffectiveWeapon switch { WeaponType.Sword => "Weak to: SWORD (x5)", WeaponType.Axe => "Weak to: AXE (x5)", _ => "Weak to: HAMMER (x5)" };
            bool bossActive = Combat.TitanBossController.Instance != null && Combat.TitanBossController.Instance.IsAlive && player.position.z >= 480f;
            bool tutBanner = UI.ForgeTutorialDirector.Instance != null && UI.ForgeTutorialDirector.Instance.HasTopBannerVisible;
            float boxW = 440f, boxH = 62f, boxX = (Screen.width - boxW) * 0.5f, boxY = (tutBanner || bossActive) ? 106f : 22f;
            string footerText = (Time.time < _feedbackTime && !string.IsNullOrEmpty(_hitFeedbackText))
                ? (_wasLastHitEffective ? $"<color=#8A5200><b>{_hitFeedbackText}</b></color>" : $"<color=#8F1D14>{_hitFeedbackText}</color>")
                : $"<color=#4A2B12>{weakness}</color>";
            EnemyHealthBarHUD.DrawPlayerStyledHealthPlaque(new Rect(boxX, boxY, boxW, boxH), title, _currentHealth, maxHealth, footerText, alpha);
        }
    }
}
