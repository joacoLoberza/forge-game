using UnityEngine;

namespace ForjaGame.Multiplayer
{
    /// <summary>
    /// Almacena y persiste los datos de sesión del jugador entre escenas (MainMenu -> Furnace/MainGame).
    /// Guarda nombre de jugador y nombre de equipo (Team) en PlayerPrefs.
    /// </summary>
    public static class GameSessionData
    {
        private const string KeyPlayerName = "Forge_PlayerName";
        private const string KeyTeamName = "Forge_TeamName";

        private static string _playerName;
        private static string _teamName;

        public static string PlayerName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_playerName))
                    _playerName = PlayerPrefs.GetString(KeyPlayerName, "Blacksmith");
                return _playerName;
            }
            set
            {
                _playerName = string.IsNullOrWhiteSpace(value) ? "Blacksmith" : value.Trim();
                PlayerPrefs.SetString(KeyPlayerName, _playerName);
                PlayerPrefs.Save();
            }
        }

        public static string TeamName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_teamName))
                    _teamName = PlayerPrefs.GetString(KeyTeamName, "Iron Vanguard");
                return _teamName;
            }
            set
            {
                _teamName = string.IsNullOrWhiteSpace(value) ? "Iron Vanguard" : value.Trim();
                PlayerPrefs.SetString(KeyTeamName, _teamName);
                PlayerPrefs.Save();
            }
        }

        public static bool IsMultiplayerSession { get; set; }

        public static void SaveSession(string player, string team, bool isMulti)
        {
            PlayerName = player;
            TeamName = team;
            IsMultiplayerSession = isMulti;
        }
    }
}

