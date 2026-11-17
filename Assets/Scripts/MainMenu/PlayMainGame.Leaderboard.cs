using UnityEngine;
using TMPro;
using ForjaGame.UI;
using ForjaGame.Speedrun;

public partial class PlayMainGame
{
    private GameObject _screenLdLeft, _screenLdRight;
    private TMP_Text _leaderboardTableText;
    private bool _hasSubscribedLeaderboard;
    private LeaderboardFilterMode _currentFilter = LeaderboardFilterMode.All;

    private void BuildLeaderboardScreenSpread()
    {
        _screenLdLeft = CreateSpreadLayer(_bookLeftPage, "LeaderboardLeft");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenLdLeft.transform, "LdFilterBanner", "RECORDS ARCHIVE",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17f);
        CreateMenuLabel(_screenLdLeft.transform, "LdSubtitle",
            "<i>Chronicles of the fastest blacksmiths to conquer the deep sanctum</i>",
            new Vector2(0f, 126f), new Vector2(344f, 34f), 12.5f, ForgeCanvasThemeStyler.SubInk, FontStyles.Italic);

        CreateMenuButton(_screenLdLeft.transform, "BtnFilterAll", "ALL RECORDS", new Vector2(0f, 66f), new Vector2(260f, 38f), true, () => SetLeaderboardFilter(LeaderboardFilterMode.All));
        CreateMenuButton(_screenLdLeft.transform, "BtnFilterSolo", "SOLO ONLY", new Vector2(0f, 18f), new Vector2(260f, 38f), false, () => SetLeaderboardFilter(LeaderboardFilterMode.Solo));
        CreateMenuButton(_screenLdLeft.transform, "BtnFilterLan", "LAN CO-OP ONLY", new Vector2(0f, -30f), new Vector2(260f, 38f), false, () => SetLeaderboardFilter(LeaderboardFilterMode.Lan));

        CreateMenuButton(_screenLdLeft.transform, "BtnRefreshLanSync", "REFRESH (LAN SYNC)",
            new Vector2(0f, -94f), new Vector2(260f, 42f), true, () =>
            {
                LANLeaderboardBroadcaster.Instance?.BroadcastSyncQuery();
                LANLeaderboardBroadcaster.Instance?.BroadcastLeaderboard();
                DelveLeaderboardDataSO.Instance.LoadFromDisk();
                RefreshLeaderboardTable();
            });
        CreateMenuButton(_screenLdLeft.transform, "BtnBackLeaderboard", "« BACK TO TITLE",
            new Vector2(0f, -166f), new Vector2(220f, 40f), false, ReturnToTitleScreen);

        _screenLdRight = CreateSpreadLayer(_bookRightPage, "LeaderboardRight");
        ForgeCanvasThemeStyler.CreateOrGetTitleBanner(_screenLdRight.transform, "LdTitleBanner", "HALL OF HEROES",
            new Vector2(0f, 178f), new Vector2(344f, 44f), fontSize: 17f);

        RectTransform tableInset = ForgeCanvasThemeStyler.CreateOrGetParchmentCard(
            _screenLdRight.transform, "TableInsetBox", new Vector2(0f, -26f), new Vector2(356f, 336f));
        _leaderboardTableText = CreateMenuLabel(tableInset, "TableContentText", "Loading chronicles...",
            Vector2.zero, new Vector2(328f, 310f), 12f, ForgeCanvasThemeStyler.DarkInk, FontStyles.Normal);
        _leaderboardTableText.alignment = TextAlignmentOptions.TopLeft;

        EnsureLeaderboardSubscription();
    }

    public void SetLeaderboardFilter(LeaderboardFilterMode mode)
    {
        _currentFilter = mode;
        RefreshLeaderboardTable();
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
        if (_leaderboardTableText == null) return;
        var list = DelveLeaderboardDataSO.Instance.GetFilteredRecords(_currentFilter);
        _leaderboardTableText.text = MedievalTypographyProvider.SanitizeForTMP(
            DelveLeaderboardDataSO.BuildTableText(list, 8, compact: true));
    }

    private static string Truncate(string str, int max)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Length <= max ? str : str.Substring(0, max - 1) + "..";
    }
}
