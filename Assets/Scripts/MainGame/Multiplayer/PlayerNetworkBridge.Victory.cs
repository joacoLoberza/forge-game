using UnityEngine;
using Unity.Netcode;
using ForjaGame.Core;

namespace ForjaGame.Multiplayer
{
    public partial class PlayerNetworkBridge
    {
        public static PlayerNetworkBridge LocalBridge { get; set; }

        public static void BroadcastPartyVictoryFromLocal()
        {
            if (LocalBridge != null && LocalBridge.IsSpawned)
                LocalBridge.NotifyPartyVictoryServerRpc();
            else
                GameLoopController.Instance?.ReceiveNetworkVictory();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void NotifyPartyVictoryServerRpc()
        {
            NotifyPartyVictoryClientRpc();
        }

        [ClientRpc]
        private void NotifyPartyVictoryClientRpc()
        {
            GameLoopController.Instance?.ReceiveNetworkVictory();
        }

        public void RequestSessionStateFromHost()
        {
            if (IsSpawned && !IsServer)
                RequestSessionStateServerRpc(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestSessionStateServerRpc(ulong requestingClientId)
        {
            SendSessionStateToClient(requestingClientId);
        }

        public static void ServerBroadcastTimerState(double startTime, bool isRunning, bool hasWon, float finalTime)
        {
            var bridge = LocalBridge;
            if (bridge == null || !bridge.IsServer || !bridge.IsSpawned)
            {
                foreach (var b in FindObjectsByType<PlayerNetworkBridge>(FindObjectsSortMode.None))
                    if (b.IsServer && b.IsSpawned) { bridge = b; break; }
            }
            if (bridge != null)
                bridge.SyncTimerClientRpc(startTime, isRunning, hasWon, finalTime);
        }

        [ClientRpc]
        private void SyncTimerClientRpc(double startTime, bool isRunning, bool hasWon, float finalTime)
        {
            ForjaGame.Speedrun.DelveRunTimerHUD.Instance?.ApplyServerTimerSync(startTime, isRunning, hasWon, finalTime);
        }

        public void SendSessionStateToClient(ulong targetClientId)
        {
            string team = GameSessionData.TeamName;
            double timerStart = 0;
            bool isRunning = false;
            bool hasWon = false;
            float finalTime = 0f;

            var timer = ForjaGame.Speedrun.DelveRunTimerHUD.Instance;
            if (timer != null)
            {
                timerStart = timer.NetworkStartTime;
                isRunning = timer.IsTimerRunning;
                hasWon = timer.HasWon;
                finalTime = timer.FinalTime;
            }

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };

            SyncSessionStateClientRpc(team, timerStart, isRunning, hasWon, finalTime, rpcParams);
        }

        [ClientRpc]
        private void SyncSessionStateClientRpc(string serverTeamName, double serverTimerStartTime, bool isTimerRunning, bool hasWon, float finalTime, ClientRpcParams rpcParams = default)
        {
            if (!string.IsNullOrWhiteSpace(serverTeamName))
            {
                GameSessionData.TeamName = serverTeamName;
                PlayerPrefs.SetString("Forge_TeamName", serverTeamName);
                PlayerPrefs.Save();
            }

            ForjaGame.Speedrun.DelveRunTimerHUD.Instance?.ApplyServerTimerSync(serverTimerStartTime, isTimerRunning, hasWon, finalTime);
        }
    }
}

