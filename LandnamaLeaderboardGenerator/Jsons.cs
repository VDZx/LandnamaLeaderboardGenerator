namespace LandnamaLeaderboardGenerator
{
    public class Config
    {
        public string dataurlDaily = "https://us-central1-landnama-2a2f1.cloudfunctions.net/getDailyLeaderboard";
        public string dataurlMonthly = "https://us-central1-landnama-2a2f1.cloudfunctions.net/getMonthlyLeaderboard";
        public string useragent = "LandnamaLeaderBoardGenerator/0.3";
        public int firstYear = 2023;
        public int firstMonth = 08;
        public int firstDay = 06;
    }

    public class PlayerEntry
    {
        public string name = null;
        public int score = int.MaxValue;
        public int won = -1; //Daily only
        public int hagalaz = -1; //Monthly only; not sure what value 'no hagalaz' is as it starts on 0
    }
}