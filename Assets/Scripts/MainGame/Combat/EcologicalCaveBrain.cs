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
        private bool _isPatrolMoving, _isHuntingActive;
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
            float dy = hasPlayer ? Mathf.Abs(player.position.y - pos.y) : 0f;
            float distPlayer = hasPlayer ? Vector3.Distance(pos, player.position) + (dy > 2.2f ? dy * 3.5f : 0f) : 999f;
            float playerDistAnchor = hasPlayer ? Vector3.Distance(_nestAnchor, player.position) + (dy > 2.2f ? dy * 3.5f : 0f) : 999f;

            return _role == CreatureEcologyRole.VeinGuardianGolem
                ? EvaluateGolem(pos, hpRatio, hasPlayer, distPlayer, playerDistAnchor)
                : EvaluateSpider(pos, hpRatio, hasPlayer, distPlayer, playerDistAnchor);
        }

        private EcologicalIntent EvaluateGolem(Vector3 pos, float hpRatio, bool hasPlayer, float distPlayer, float playerDistAnchor)
        {
            if (hasPlayer && hpRatio <= 0.28f)
                return EcologicalIntent.CriticalFlee;

            bool struckByPlayer = Time.time < _lastDamagedTime + 32f || Time.time < _forcedHostileUntil;
            bool veinDisturbed = Time.time < _veinAlertUntil;
            if (hasPlayer && struckByPlayer && distPlayer <= _leashRadius + 24f)
                return EcologicalIntent.SelfDefense;

            float homeDist = _homeVein != null ? Vector3.Distance(_homeVein.transform.position, pos) : 999f;
            float triggerDist = 6.2f;
            bool closeToGolemOrSpawnVein = distPlayer <= triggerDist || playerDistAnchor <= triggerDist || (homeDist < 900f && playerDistAnchor <= 6.8f);
            bool withinTerritorialChase = distPlayer <= 12.0f && playerDistAnchor <= 13.5f;
            if (hasPlayer && (closeToGolemOrSpawnVein || (veinDisturbed && withinTerritorialChase)) && withinTerritorialChase)
                return veinDisturbed ? EcologicalIntent.TerritorialDefense : EcologicalIntent.SelfDefense;

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
            if (hasPlayer && hpRatio <= 0.32f) { _isHuntingActive = false; return EcologicalIntent.CriticalFlee; }

            bool broodThreat = Time.time < _broodAlertUntil || (hasPlayer && playerDistAnchor <= 8.5f);
            if (hasPlayer && broodThreat) { _isHuntingActive = true; return EcologicalIntent.DefendBrood; }

            float huntRadius = _senseRadius + (_hunger >= 58f ? 8.5f : 0f);
            bool provoked = Time.time < _lastDamagedTime + 22f || Time.time < _forcedHostileUntil;
            if (hasPlayer && (provoked || distPlayer <= huntRadius || _isHuntingActive))
            {
                _isHuntingActive = true;
                return EcologicalIntent.HuntLivingPrey;
            }
            _isHuntingActive = false;

            if (_isResting) return EcologicalIntent.Resting;
            if (_hunger >= 62f)
            {
                UpdatePatrolState(pos, 7.5f, 16.5f, 0.9f, 2.0f);
                if (!_isPatrolMoving) _hunger = 32f;
                return EcologicalIntent.ForagingProwl;
            }

            UpdatePatrolState(pos, 0.7f, 2.8f, 2.5f, 4.8f);
            return EcologicalIntent.CalmPatrol;
        }

        public bool TryPerformFeedBite(Vector3 creaturePos, out bool completedMeal)
        {
            completedMeal = false;
            var vein = TargetVein;
            if (vein == null || !vein.IsAlive) { _hunger = 25f; return false; }
            Vector2 diff = new Vector2(vein.transform.position.x - creaturePos.x, vein.transform.position.z - creaturePos.z);
            if (diff.sqrMagnitude > 16.0f) return false;

            if (!IsActivelyFeeding)
            {
                _nextFeedFinishTime = Time.time + 1.45f;
                vein.ConsumeEcologicalBite();
                _hunger = 0f;
                completedMeal = true;
            }
            return true;
        }

        public static Vector3 ComputeSafeFleeDirection(
            Vector3 creaturePos, Vector3 playerPos, Vector3 homeAnchor,
            float zoneMinZ = 0f, float zoneMaxZ = 600f)
        {
            Vector3 away = creaturePos - playerPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = Vector3.forward;
            away.Normalize();

            float dirZ = Mathf.Sign(away.z);
            if (Mathf.Abs(away.z) < 0.15f)
                dirZ = (creaturePos.z < (zoneMinZ + zoneMaxZ) * 0.5f) ? 1f : -1f;

            float targetZ = creaturePos.z + dirZ * 12.0f;
            if (targetZ > zoneMaxZ - 2.5f) targetZ = creaturePos.z - 12.0f;
            else if (targetZ < zoneMinZ + 2.5f) targetZ = creaturePos.z + 12.0f;
            targetZ = Mathf.Clamp(targetZ, zoneMinZ + 1.5f, zoneMaxZ - 1.5f);

            float sx = CaveDungeonBuilder.GetSpineX(targetZ);
            CaveDungeonBuilder.GetWallWidthsAtZ(targetZ, out float lw, out float rw);
            float awayX = Mathf.Sign(away.x == 0f ? 1f : away.x);
            float targetX = Mathf.Clamp(creaturePos.x + awayX * 4.5f, sx - Mathf.Max(1.8f, lw - 2.0f), sx + Mathf.Max(1.8f, rw - 2.0f));

            return CaveCreatureBase.ComputeCaveGuidedDirection(creaturePos, new Vector3(targetX, creaturePos.y, targetZ));
        }

        private void UpdatePatrolState(Vector3 pos, float minR, float maxR, float waitMin, float waitMax)
        {
            if (_isPatrolMoving)
            {
                Vector2 flat = new Vector2(_patrolTarget.x - pos.x, _patrolTarget.z - pos.z);
                if (flat.sqrMagnitude <= 2.25f || Time.time >= _patrolWaitUntil)
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
                _isPatrolMoving = true; _patrolWaitUntil = Time.time + 5.5f;
            }
        }
    }
}
