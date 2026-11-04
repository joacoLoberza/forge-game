using UnityEngine;
using ForjaGame.Environment;
using ForjaGame.Resources;

namespace ForjaGame.Combat
{
    public enum CreatureEcologyRole { VeinGuardianGolem, BroodHunterSpider }

    public enum EcologicalIntent
    {
        CriticalFlee, SelfDefense, TerritorialDefense, HuntLivingPrey,
        DefendBrood, WatchfulVigil, FeedingOnVein, ForagingProwl, Resting, CalmPatrol
    }

    /// <summary>
    /// Árbol de reacciones ecológicas realista con necesidades internas (Hambre y Fatiga/Descanso):
    /// - Golems: Custodios pacíficos de su mena. Si pasas de largo solo vigilan; atacan únicamente si
    ///   picas su mineral o los golpeas. Canalizan energía lítica al tener hambre y descansan junto a la veta.
    /// - Arañas: Depredadoras de seres vivos cercanos y protectoras feroces de sus crías (SpiderBroodNest),
    ///   alternando caza, forrajeo hambriento, descanso en el nido y patrulla.
    /// </summary>
    public class EcologicalCaveBrain
    {
        private CreatureEcologyRole _role;
        private Vector3 _nestAnchor;
        private float _senseRadius;
        private float _leashRadius;
        private ResourceNodeController _homeVein;
        private ResourceNodeController _targetVein;

        private float _lastDamagedTime = -99f;
        private float _veinAlertUntil = -99f;
        private float _broodAlertUntil = -99f;
        private float _forcedHostileUntil = -99f;
        private float _hunger;
        private float _fatigue;
        private bool _isResting;
        private Vector3 _patrolTarget;
        private float _patrolWaitUntil;
        private bool _isPatrolMoving;
        private float _nextFeedFinishTime = -1f;

        public bool IsActivelyFeeding => Time.time < _nextFeedFinishTime;
        public ResourceNodeController TargetVein => _targetVein != null ? _targetVein : _homeVein;
        public Vector3 PatrolTarget => _patrolTarget;
        public Vector3 Anchor => _nestAnchor;
        public bool IsPatrolMoving => _isPatrolMoving;
        public float Hunger => _hunger;
        public float Fatigue => _fatigue;

        public void Initialize(
            Vector3 anchor, CreatureEcologyRole role, float senseRadius, float leashRadius,
            int seedOffset, ResourceNodeController homeVein = null)
        {
            _nestAnchor = anchor;
            _role = role;
            _senseRadius = senseRadius;
            _leashRadius = leashRadius;
            _homeVein = homeVein;
            _targetVein = homeVein;
            _lastDamagedTime = _veinAlertUntil = _broodAlertUntil = _forcedHostileUntil = -99f;

            int absSeed = Mathf.Abs(seedOffset);
            _hunger = 24f + (absSeed * 13 % 40);
            _fatigue = 18f + (absSeed * 29 % 44);
            _isResting = false;
            _patrolTarget = anchor;
            _patrolWaitUntil = Time.time + 1.0f + (absSeed % 3);
            _isPatrolMoving = false;
        }

        public void SetHomeVein(ResourceNodeController vein)
        {
            _homeVein = vein;
            if (vein != null) _nestAnchor = vein.transform.position;
        }

        public void NotifyDamaged() { _lastDamagedTime = Time.time; _isResting = false; }
        public void ForceHostileAlert(float duration) { _forcedHostileUntil = Time.time + duration; _isResting = false; }
        public void NotifyFedOnPrey(float amount) => _hunger = Mathf.Max(0f, _hunger - amount);

        public void NotifyNearbyVeinMined(Vector3 veinPos, ResourceNodeController minedVein)
        {
            if (_role != CreatureEcologyRole.VeinGuardianGolem) return;
            if ((_homeVein != null && minedVein == _homeVein) || Vector3.Distance(_nestAnchor, veinPos) <= _senseRadius + 4.5f)
            {
                _veinAlertUntil = Time.time + 14.0f;
                _isResting = false;
            }
        }

        public void NotifyBroodDisturbed(Vector3 nestPos)
        {
            if (_role == CreatureEcologyRole.BroodHunterSpider && Vector3.Distance(_nestAnchor, nestPos) <= _senseRadius + 8.0f)
            {
                _broodAlertUntil = Time.time + 13.5f;
                _isResting = false;
            }
        }

        public EcologicalIntent Evaluate(Vector3 pos, float hpRatio, Transform player, float dt)
        {
            _hunger = Mathf.Min(100f, _hunger + dt * (_role == CreatureEcologyRole.BroodHunterSpider ? 2.4f : 1.7f));
            if (_isResting)
            {
                _fatigue = Mathf.Max(0f, _fatigue - dt * 11.5f);
                if (_fatigue <= 12f) _isResting = false;
            }
            else
            {
                _fatigue = Mathf.Min(100f, _fatigue + dt * (_isPatrolMoving ? 2.1f : 0.9f));
                if (_fatigue >= 82f) _isResting = true;
            }

            bool hasPlayer = player != null;
            float distPlayer = hasPlayer ? Vector3.Distance(pos, player.position) : 999f;
            float playerDistAnchor = hasPlayer ? Vector3.Distance(_nestAnchor, player.position) : 999f;

            return _role == CreatureEcologyRole.VeinGuardianGolem
                ? EvaluateGolem(pos, hasPlayer, distPlayer, playerDistAnchor)
                : EvaluateSpider(pos, hpRatio, hasPlayer, distPlayer, playerDistAnchor);
        }

        private EcologicalIntent EvaluateGolem(Vector3 pos, bool hasPlayer, float distPlayer, float playerDistAnchor)
        {
            if (hasPlayer && (Time.time < _lastDamagedTime + 11f || Time.time < _forcedHostileUntil) && distPlayer <= _leashRadius + 6f)
                return EcologicalIntent.SelfDefense;

            if (hasPlayer && Time.time < _veinAlertUntil && (distPlayer <= _leashRadius || playerDistAnchor <= _leashRadius))
                return EcologicalIntent.TerritorialDefense;

            if (hasPlayer && (distPlayer <= 5.6f || playerDistAnchor <= 5.2f) && !_isResting)
                return EcologicalIntent.WatchfulVigil;

            if (IsActivelyFeeding) return EcologicalIntent.FeedingOnVein;
            if (_hunger >= 72f)
            {
                if (_homeVein != null && _homeVein.IsAlive) { _targetVein = _homeVein; return EcologicalIntent.FeedingOnVein; }
                _hunger = 25f;
            }

            if (_isResting) return EcologicalIntent.Resting;
            UpdatePatrolState(pos, 2.2f, 5.8f, 3.2f, 5.5f);
            return EcologicalIntent.CalmPatrol;
        }

        private EcologicalIntent EvaluateSpider(Vector3 pos, float hpRatio, bool hasPlayer, float distPlayer, float playerDistAnchor)
        {
            bool broodThreat = Time.time < _broodAlertUntil || (hasPlayer && playerDistAnchor <= 6.2f);
            if (hasPlayer && broodThreat && distPlayer <= _leashRadius + 5f)
                return EcologicalIntent.DefendBrood;

            float huntRadius = _senseRadius + (_hunger >= 60f ? 3.2f : 0f);
            bool provoked = Time.time < _lastDamagedTime + 10f || Time.time < _forcedHostileUntil;
            if (hasPlayer && (provoked || distPlayer <= huntRadius) && distPlayer <= _leashRadius + 4f)
            {
                if (hpRatio <= 0.22f && playerDistAnchor > 6.5f && Time.time >= _forcedHostileUntil)
                    return EcologicalIntent.CriticalFlee;
                return EcologicalIntent.HuntLivingPrey;
            }

            if (_isResting) return EcologicalIntent.Resting;
            if (_hunger >= 66f)
            {
                UpdatePatrolState(pos, 5.5f, 12.5f, 1.0f, 2.2f);
                if (!_isPatrolMoving) _hunger = 34f;
                return EcologicalIntent.ForagingProwl;
            }

            UpdatePatrolState(pos, 1.8f, 5.6f, 2.2f, 4.4f);
            return EcologicalIntent.CalmPatrol;
        }

        public bool TryPerformFeedBite(Vector3 creaturePos, out bool completedMeal)
        {
            completedMeal = false;
            var vein = TargetVein;
            if (vein == null || !vein.IsAlive) { _hunger = 25f; return false; }
            Vector2 diff = new Vector2(vein.transform.position.x - creaturePos.x, vein.transform.position.z - creaturePos.z);
            if (diff.sqrMagnitude > 6.0f) return false;

            if (!IsActivelyFeeding)
            {
                _nextFeedFinishTime = Time.time + 1.45f;
                vein.ConsumeEcologicalBite();
                _hunger = 0f;
                completedMeal = true;
            }
            return true;
        }

        public static Vector3 ComputeSafeFleeDirection(Vector3 creaturePos, Vector3 playerPos, Vector3 homeAnchor)
        {
            Vector2 toHome = new Vector2(homeAnchor.x - creaturePos.x, homeAnchor.z - creaturePos.z);
            if (toHome.sqrMagnitude > 9f) return CaveCreatureBase.ComputeCaveGuidedDirection(creaturePos, homeAnchor);

            Vector3 away = new Vector3(creaturePos.x - playerPos.x, 0f, creaturePos.z - playerPos.z);
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            away.Normalize();
            float targetZ = Mathf.Clamp(creaturePos.z + Mathf.Sign(away.z == 0f ? 1f : away.z) * 5.5f,
                CaveDungeonBuilder.CaveEntranceZ + 2f, CaveDungeonBuilder.CaveEndZ - 2.5f);
            float sx = CaveDungeonBuilder.GetSpineX(targetZ);
            float maxOff = Mathf.Max(2.0f, CaveDungeonBuilder.GetHalfWidthAtZ(targetZ) - 2.2f);
            float safeX = Mathf.Clamp(creaturePos.x + away.x * 3.8f, sx - maxOff, sx + maxOff);
            return CaveCreatureBase.ComputeCaveGuidedDirection(creaturePos, new Vector3(safeX, creaturePos.y, targetZ));
        }

        private void UpdatePatrolState(Vector3 pos, float minR, float maxR, float waitMin, float waitMax)
        {
            if (_isPatrolMoving)
            {
                Vector2 flat = new Vector2(_patrolTarget.x - pos.x, _patrolTarget.z - pos.z);
                if (flat.sqrMagnitude <= 0.9f || Time.time >= _patrolWaitUntil)
                {
                    _isPatrolMoving = false;
                    _patrolWaitUntil = Time.time + Random.Range(waitMin, waitMax);
                }
            }
            else if (Time.time >= _patrolWaitUntil)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f), rad = Random.Range(minR, maxR);
                float z = Mathf.Clamp(_nestAnchor.z + Mathf.Sin(ang) * rad,
                    CaveDungeonBuilder.CaveEntranceZ + 2.2f, CaveDungeonBuilder.CaveEndZ - 2.5f);
                float sx = CaveDungeonBuilder.GetSpineX(z);
                CaveDungeonBuilder.GetWallWidthsAtZ(z, out float lw, out float rw);
                float x = Mathf.Clamp(_nestAnchor.x + Mathf.Cos(ang) * rad, sx - Mathf.Max(1.8f, lw - 2.0f), sx + Mathf.Max(1.8f, rw - 2.0f));
                _patrolTarget = new Vector3(x, CaveDungeonBuilder.GetFloorY(x, z), z);
                _isPatrolMoving = true;
                _patrolWaitUntil = Time.time + 5.5f;
            }
        }
    }
}
