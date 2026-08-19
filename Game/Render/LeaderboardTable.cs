/// <summary>
/// Shared leaderboard table, so the death screen and the main menu cannot
/// drift into showing the same data two different ways.
/// </summary>
public static class LeaderboardTable
{
    private const int RowWidth = 460;
    private const int RowHeight = 30;

    public static void Draw(Leaderboard board, int centreX, int y, int highlightIndex = -1)
    {
        if (board == null || board.Entries.Count == 0)
        {
            MenuUi.CentredText("No scores yet", centreX, y + 6, 18, MenuUi.TextDim);
            return;
        }

        int left = centreX - RowWidth / 2;

        for (int i = 0; i < board.Entries.Count; i++)
        {
            LeaderboardEntry entry = board.Entries[i];
            int rowY = y + i * RowHeight;

            bool highlight = i == highlightIndex;

            if (highlight)
            {
                Raylib.DrawRectangleRec(
                    new Rectangle(left - 10, rowY - 4, RowWidth + 20, RowHeight - 2),
                    new Color(255, 240, 210, 255));
            }

            Color ink = highlight ? MenuUi.Accent : MenuUi.Text;

            Raylib.DrawText($"{i + 1}.", left, rowY, 19, MenuUi.TextDim);
            Raylib.DrawText(entry.Name, left + 34, rowY, 19, ink);

            string wave = $"wave {entry.Wave}";
            Raylib.DrawText(wave, left + 210, rowY, 17, MenuUi.TextDim);

            string score = entry.Score.ToString();
            int sw = Raylib.MeasureText(score, 19);
            Raylib.DrawText(score, left + RowWidth - sw, rowY, 19, ink);
        }
    }

    public static int HeightFor(Leaderboard board) =>
        board == null || board.Entries.Count == 0 ? 30 : board.Entries.Count * RowHeight;
}
