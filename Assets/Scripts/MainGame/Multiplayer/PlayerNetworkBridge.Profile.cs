using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using ForjaGame.Combat;

namespace ForjaGame.Multiplayer
{
    /// <summary>
    /// Sincronización de red para Nombre de Jugador, Nombre de Equipo (Team) y Salud en Red.
    /// Gestiona la visualización del overhead plate sobre la cabeza de jugadores remotos.
    /// </summary>
    public partial class PlayerNetworkBridge
    {
        public NetworkVariable<FixedString64Bytes> NetworkPlayerName = new(
            new FixedString64Bytes("Blacksmith"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        public NetworkVariable<FixedString64Bytes> NetworkTeamName = new(
            new FixedString64Bytes("Iron Vanguard"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> NetworkCurrentHealth = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        public NetworkVariable<int> NetworkMaxHealth = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private PlayerOverheadPlate _overheadPlate;
        private PlayerHealthController _healthCtrl;

        private void InitializeProfileOnSpawn()
        {
            _healthCtrl = GetComponent<PlayerHealthController>();

            if (IsServer)
            {
                NetworkTeamName.Value = new FixedString64Bytes(GameSessionData.TeamName);
            }

            if (IsOwner)
            {
                NetworkPlayerName.Value = new FixedString64Bytes(GameSessionData.PlayerName);
                if (_healthCtrl != null)
                {
                    NetworkCurrentHealth.Value = _healthCtrl.CurrentHealth;
                    NetworkMaxHealth.Value = _healthCtrl.MaxHealth;
                }

                string currentTeam = NetworkTeamName.Value.ToString();
                if (!string.IsNullOrWhiteSpace(currentTeam))
                    GameSessionData.TeamName = currentTeam;

                NetworkTeamName.OnValueChanged += (prev, curr) =>
                {
                    if (!string.IsNullOrWhiteSpace(curr.ToString()))
                        GameSessionData.TeamName = curr.ToString();
                };
            }
            else
            {
                EnsureOverheadPlate();
                NetworkTeamName.OnValueChanged += (prev, curr) =>
                {
                    if (!string.IsNullOrWhiteSpace(curr.ToString()))
                        GameSessionData.TeamName = curr.ToString();
                };
            }
        }

        public static void ServerBroadcastTeamName(string newTeam)
        {
            if (string.IsNullOrWhiteSpace(newTeam)) return;
            GameSessionData.TeamName = newTeam.Trim();
            var bridges = FindObjectsByType<PlayerNetworkBridge>(FindObjectsSortMode.None);
            foreach (var b in bridges)
            {
                if (b.IsServer)
                    b.NetworkTeamName.Value = new FixedString64Bytes(GameSessionData.TeamName);
            }
        }

        private void EnsureOverheadPlate()
        {
            if (IsOwner) return; // Solo visible para los demás jugadores

            if (_overheadPlate == null)
            {
                _overheadPlate = GetComponentInChildren<PlayerOverheadPlate>(true);
                if (_overheadPlate == null)
                {
                    GameObject go = new GameObject("OverheadPlate");
                    go.transform.SetParent(transform, false);
                    _overheadPlate = go.AddComponent<PlayerOverheadPlate>();
                }
            }
            _overheadPlate.Initialize(this);
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                if (_healthCtrl == null) _healthCtrl = GetComponent<PlayerHealthController>();
                if (_healthCtrl != null)
                {
                    if (NetworkCurrentHealth.Value != _healthCtrl.CurrentHealth)
                        NetworkCurrentHealth.Value = _healthCtrl.CurrentHealth;
                    if (NetworkMaxHealth.Value != _healthCtrl.MaxHealth)
                        NetworkMaxHealth.Value = _healthCtrl.MaxHealth;
                }
            }
            else if (_overheadPlate == null)
            {
                EnsureOverheadPlate();
            }
        }
    }
}

