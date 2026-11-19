using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.UI;
using ForjaGame.Speedrun;

public partial class PlayMainGame
{
    private GameObject _screenLdLeft, _screenLdRight;
    private RectTransform _leaderboardTableRoot;
    private Button _btnLdFilterAll, _btnLdFilterSolo, _btnLdFilterLan;
    private bool _hasSubscribedLeaderboard;
    private LeaderboardFilterMode _currentFilter = LeaderboardFilterMode.All;

    private void BuildLeaderboardScreenSpread()
    {
        _screenLdLeft = CreateSpreadLayer(_bookLeftPage, "LeaderboardLeft");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenLdLeft.transform, "LdFilterBanner", "RECORDS ARCHIVE",
            new Vector2(0f, 178f), new Vector2(354f, 44f), fontSize: 17f);
        CreateMenuLabel(_screenLdLeft.transform, "LdSubtitle",
            "<i>Chronicles of the fastest blacksmiths to conquer the deep sanctum</i>",
            new Vector2(0f, 126f), new Vector2(344f, 34f), 12.5f, ForgeCanvasThemeStyler.SubInk, FontStyles.Italic);

        _btnLdFilterAll = CreateMenuButton(_screenLdLeft.transform, "BtnFilterAll", "ALL RECORDS",
            new Vector2(0f, 66f), new Vector2(276f, 40f), true, () => SetLeaderboardFilter(LeaderboardFilterMode.All)).GetComponent<Button>();
        _btnLdFilterSolo = CreateMenuButton(_screenLdLeft.transform, "BtnFilterSolo", "SOLO ONLY",
            new Vector2(0f, 16f), new Vector2(276f, 40f), false, () => SetLeaderboardFilter(LeaderboardFilterMode.Solo)).GetComponent<Button>();
        _btnLdFilterLan = CreateMenuButton(_screenLdLeft.transform, "BtnFilterLan", "LAN CO-OP ONLY",
            new Vector2(0f, -34f), new Vector2(276f, 40f), false, () => SetLeaderboardFilter(LeaderboardFilterMode.Lan)).GetComponent<Button>();

        CreateMenuButton(_screenLdLeft.transform, "BtnRefreshLanSync", "REFRESH (LAN SYNC)",
            new Vector2(0f, -98f), new Vector2(276f, 42f), false, () =>
            {
                LANLeaderboardBroadcaster.Instance?.BroadcastSyncQuery();
                LANLeaderboardBroadcaster.Instance?.BroadcastLeaderboard();
                DelveLeaderboardDataSO.Instance.LoadFromDisk();
                RefreshLeaderboardTable();
            });
        CreateMenuButton(_screenLdLeft.transform, "BtnBackLeaderboard", "BACK TO TITLE",
            new Vector2(0f, -168f), new Vector2(226f, 40f), false, ReturnToTitleScreen);

        _screenLdRight = CreateSpreadLayer(_bookRightPage, "LeaderboardRight");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenLdRight.transform, "LdTitleBanner", "HALL OF HEROES",
            new Vector2(0f, 178f), new Vector2(354f, 44f), fontSize: 17f);

        _leaderboardTableRoot = ForgeCanvasThemeStyler.CreateOrGetParchmentCard(
            _screenLdRight.transform, "TableInsetBox", new Vector2(0f, -26f), new Vector2(364f, 340f));

        EnsureLeaderboardSubscription();
        UpdateLeaderboardFilterHighlights();
    }

    public void SetLeaderboardFilter(LeaderboardFilterMode mode)
    {
        _currentFilter = mode;
        UpdateLeaderboardFilterHighlights();
        RefreshLeaderboardTable();
    }

    private void UpdateLeaderboardFilterHighlights()
    {
        if (_screenLdLeft == null) return;
        ForgeCanvasThemeStyler.StyleForgeButton(_btnLdFilterAll, _currentFilter == LeaderboardFilterMode.All, _screenLdLeft.transform, new Vector2(0f, 66f), new Vector2(276f, 40f));
        ForgeCanvasThemeStyler.StyleForgeButton(_btnLdFilterSolo, _currentFilter == LeaderboardFilterMode.Solo, _screenLdLeft.transform, new Vector2(0f, 16f), new Vector2(276f, 40f));
        ForgeCanvasThemeStyler.StyleForgeButton(_btnLdFilterLan, _currentFilter == LeaderboardFilterMode.Lan, _screenLdLeft.transform, new Vector2(0f, -34f), new Vector2(276f, 40f));
    }

    public void OpenLeaderboardScreen()
    {
        SwitchScreen(MainMenuScreen.Leaderboard);
        if (Display.displays.Length > 1) Display.displays[1].Activate();
        EnsureLeaderboardSubscription();
        LANLeaderboardBroadcaster.Instance?.BroadcastSyncQuery();
        RefreshLeaderboardTable();
    }

    private void EnsureLeaderboardSubscription()
    {
        if (_hasSubscribedLeaderboard) return;
        DelveLeaderboardDataSO.Instance.OnLeaderboardChanged += RefreshLeaderboardTable;
        _hasSubscribedLeaderboard = true;
    }

    public void RefreshLeaderboardTable()
    {
        if (_leaderboardTableRoot == null) return;
        var list = DelveLeaderboardDataSO.Instance.GetFilteredRecords(_currentFilter);
        CompactLeaderboardTableBuilder.Render(_leaderboardTableRoot, list, maxRows: 8);
    }

    private static string Truncate(string str, int max)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Length <= max ? str : str.Substring(0, max - 1) + "..";
    }
}
