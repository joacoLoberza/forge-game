using UnityEngine;

namespace ForjaGame.Combat
{
    public partial class CombatDummyEnemy
    {
        private void Update()
        {
            if (!IsAlive) return;

            // Failsafe de seguridad física: si traspasa el piso por glitch, reubicar flotando arriba
            if (transform.position.y < -5.0f)
            {
                transform.position = mapSpawnPoint + Vector3.up * 1.5f;
                if (_rb != null)
                {
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
            }

            Transform closest = GetClosestLivingPlayerTransform();
            if (closest != null) _playerTransform = closest;
            if (_playerTransform == null) return;

            Vector3 toPlayer = _playerTransform.position - transform.position;
            toPlayer.y = 0f;
            float distToPlayer = toPlayer.magnitude;

            var pHealth = _playerTransform.GetComponent<PlayerHealthController>() ?? PlayerHealthController.Instance;
            bool isPlayerAlive = pHealth != null && pHealth.IsAlive && !pHealth.IsDefeated;

            // 1. Gestión de estado de detección (Aggro circundante)
            if (!isPlayerAlive)
            {
                if (_isPlayerDetected)
                {
                    _isPlayerDetected = false;
                    _isTelegraphing = false;
                    _isAttacking = false;
                    RefreshCurrentColor();
                }
            }
            else
            {
                if (!_isPlayerDetected)
                {
                    // Solo detecta al entrar dentro del área circundante
                    if (distToPlayer <= detectionRadius)
                    {
                        _isPlayerDetected = true;
                        _nextAttackTime = Time.time + attackInterval;
                        Debug.Log("<color=yellow>[Enemigo Cápsula] ¡Jugador detectado en el área circundante! Iniciando combate.</color>");
                    }
                }
                else
                {
                    // Si el jugador se aleja más allá de la distancia de desenganche, pierde interés
                    if (distToPlayer > deaggroRadius)
                    {
                        _isPlayerDetected = false;
                        _isTelegraphing = false;
                        _isAttacking = false;
                        RefreshCurrentColor();
                        Debug.Log("<color=grey>[Enemigo Cápsula] Jugador fuera de rango. Retornando a zona de spawn.</color>");
                    }
                }
            }

            // 2. Comportamiento en combate cuando el jugador está detectado
            if (_isPlayerDetected && isPlayerAlive)
            {
                // Orientación continua hacia el jugador
                if (distToPlayer > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
                }

                // Lógica de ataque cada 2 segundos con TELEGRAPH AZUL (1.0s previo al golpe)
                if (distToPlayer <= attackRange)
                {
                    float timeToAttack = _nextAttackTime - Time.time;

                    // Exactamente 1.0s antes del ataque: Entrar en modo aviso y cambiar color a AZUL oscurecido
                    if (timeToAttack <= 1.0f && !_isTelegraphing && !_isAttacking)
                    {
                        _isTelegraphing = true;
                        SetBodyColor(GetDynamicTelegraphColor());
                    }

                    // Disparo del ataque al cumplirse los 2 segundos
                    if (Time.time >= _nextAttackTime)
                    {
                        _isTelegraphing = false;
                        RefreshCurrentColor();
                        ExecuteEnemyAttack();
                    }
                }
                else
                {
                    // Si el jugador se aleja del rango durante el telegraph, cancela el aviso azul
                    if (_isTelegraphing)
                    {
                        _isTelegraphing = false;
                        RefreshCurrentColor();
                    }
                }
            }
            else
            {
                // Fuera de alerta: asegurarse de restaurar color normal
                if (_isTelegraphing)
                {
                    _isTelegraphing = false;
                    RefreshCurrentColor();
                }
            }

            // Ocultamiento de la hitbox activa tras el golpe
            if (_attackHitbox != null && _isAttacking && Time.time > _attackVisualEndTime)
            {
                _isAttacking = false;
                _attackHitbox.IsVolumeActive = false;
                _attackHitbox.IsConnecting = false;
            }

            // Restauración del color de la hurtbox tras recibir daño del jugador
            if (_bodyHurtbox != null && Time.time > _bodyHurtBlackUntil)
            {
                _bodyHurtbox.IsConnecting = false;
            }
        }

        private void FixedUpdate()
        {
            if (!IsAlive || _rb == null) return;

            if (_isPlayerDetected && _playerTransform != null)
            {
                Vector3 toPlayer = _playerTransform.position - transform.position;
                toPlayer.y = 0f;
                float dist = toPlayer.magnitude;

                // Si está preparando el ataque (telegraph azul) o ejecutando ataque, frena los pies
                if (_isTelegraphing || _isAttacking)
                {
                    _rb.linearVelocity = new Vector3(_rb.linearVelocity.x * 0.2f, _rb.linearVelocity.y, _rb.linearVelocity.z * 0.2f);
                    return;
                }

                // Persecución terrestre hasta el alcance de ataque
                if (dist > attackRange * 0.90f)
                {
                    Vector3 moveDir = toPlayer.normalized;
                    Vector3 vel = moveDir * moveSpeed;
                    _rb.linearVelocity = new Vector3(vel.x, _rb.linearVelocity.y, vel.z);
                }
                else
                {
                    _rb.linearVelocity = new Vector3(_rb.linearVelocity.x * 0.2f, _rb.linearVelocity.y, _rb.linearVelocity.z * 0.2f);
                }
            }
            else
            {
                // Jugador no detectado o fuera de rango: Retorno tranquilo a su zona de spawn
                Vector3 toHome = mapSpawnPoint - transform.position;
                toHome.y = 0f;
                float distHome = toHome.magnitude;

                if (distHome > 0.8f)
                {
                    Vector3 returnDir = toHome.normalized;
                    Quaternion homeRot = Quaternion.LookRotation(returnDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, homeRot, 4f * Time.fixedDeltaTime);
                    Vector3 vel = returnDir * (moveSpeed * 0.65f);
                    _rb.linearVelocity = new Vector3(vel.x, _rb.linearVelocity.y, vel.z);
                }
                else
                {
                    _rb.linearVelocity = new Vector3(_rb.linearVelocity.x * 0.2f, _rb.linearVelocity.y, _rb.linearVelocity.z * 0.2f);
                }
            }
        }

        /// <summary>
        /// Ejecuta el ataque activo. NO hay daño por mero contacto físico; el daño solo ocurre
        /// cuando el enemigo lanza este golpe tras el aviso azul.
        /// </summary>
        private void ExecuteEnemyAttack()
        {
            _nextAttackTime = Time.time + attackInterval;
            _isAttacking = true;
            _attackVisualEndTime = Time.time + 0.35f;

            if (_attackHitbox != null)
            {
                _attackHitbox.IsVolumeActive = true;
                _attackHitbox.IsConnecting = false;
            }

            // Detección frontal con altura centrada exactamente en el torso
            Vector3 center = transform.position + transform.forward * 1.0f;
            Vector3 halfExtents = new Vector3(0.6f, 0.6f, 0.75f);
            Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Ignore);

            bool connectedToPlayer = false;
            foreach (Collider hit in hits)
            {
                if (hit.gameObject == gameObject || hit.transform.IsChildOf(transform)) continue;

                var playerHealth = hit.GetComponentInParent<PlayerHealthController>() ?? hit.GetComponentInChildren<PlayerHealthController>();
                if (playerHealth != null && playerHealth.IsAlive)
                {
                    connectedToPlayer = true;
                    playerHealth.TakeDamage(attackDamage);
                    break;
                }
            }

            if (_attackHitbox != null)
            {
                _attackHitbox.IsConnecting = connectedToPlayer;
            }
        }
    }
}
