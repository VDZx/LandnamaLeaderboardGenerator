using System;
using System.IO;
using System.Text;

namespace LandnamaLeaderboardGenerator
{
    public static class PageWriter
    {
        public const string FILE_TEMPLATE_DAILY = "template_daily.html";
        public const string FILE_SCRIPT_COUNTDOWN = "countdown.js";

        public static bool initialized = false;
        public static string template_daily = string.Empty;
        public static string script_countdown = string.Empty;

        public static void Init()
        {
            if (initialized) return;
            template_daily = File.ReadAllText(FILE_TEMPLATE_DAILY);
            script_countdown = File.ReadAllText(FILE_SCRIPT_COUNTDOWN);
            initialized = true;
        }

        public static string GenerateDaily(PlayerEntry[] entries, int year, int month, int day, DateTime dataTimestamp)
        {
            Init();
            //Start with the template...
            string toReturn = template_daily;
            //Check if it's the current daily
            DateTime now = DateTime.UtcNow;
            bool ongoing = (now.Year == year && now.Month == month && now.Day == day);
            //Add countdown timer on current daily (add first so strings get replaced in it)
            toReturn = toReturn.Replace("$COUNTDOWN$", ongoing ? "<script>" + script_countdown + "</script>" : "");
            toReturn = toReturn.Replace("$COUNTDOWNDIV$", ongoing ? "<div id=\"count\" >Loading...</div><br /><br />" : "");
            //Replace time-relevant parts
            toReturn = toReturn.Replace("$ONGOING$", ongoing ? "(Ongoing)" : "");
            toReturn = toReturn.Replace("$DATE$", Program.GetYYYYMMDD(year, month, day));
            toReturn = toReturn.Replace("$TODAY$", Program.GetYYYYMMDD(now.Year, now.Month, now.Day));
            DateTime tomorrow = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc).AddDays(1);
            toReturn = toReturn.Replace("$TOMORROW$", Program.GetYYYYMMDD(tomorrow.Year, tomorrow.Month, tomorrow.Day));
            toReturn = toReturn.Replace("$TIMESTAMP$", dataTimestamp.ToString());
            toReturn = toReturn.Replace("$DATENAV$", GetDateNavigation(year, month, day, ongoing));
            //JSON link
            toReturn = toReturn.Replace("$JSON$", Program.GetDailyFilename("", year, month, day));

            //Add entries
            int topTenEnd = 10; //Due to ties it may stretch beyond 10 entries
            for (int i = 10; i < entries.Length; i++)
            {
                if (entries[i - 1].score < entries[i].score) break;
                if (entries[i - 1].won > entries[i].won) break;
                topTenEnd++;
            }
            toReturn = toReturn.Replace("$ENTRIES$", GetDailyEntryList(entries, 1, topTenEnd, true));
            toReturn = toReturn.Replace("$EXTRAENTRIES$", GetDailyEntryList(entries, topTenEnd + 1, -1, false));

            return toReturn;
        }

        public static string GetDailyEntryList(PlayerEntry[] entries, int start = 1, int end = -1, bool showRank = true)
        {
            start -= 1; //#1 is internally index 0
            int entryCount = entries.Length;
            if (end == -1) end = entryCount;
            StringBuilder sb = new StringBuilder();
            if (end - start == 0)
            {
                sb.Append("<tr><td>No entries</td></tr>");
            }
            else
            {
                for (int i = start; i < end; i++)
                {
                    PlayerEntry entry = entries[i];
                    sb.Append("<tr>");
                    if (showRank)
                    {
                        sb.Append("<td>");
                        if (i > 0 && entry.score == entries[i - 1].score && entry.won == entries[i - 1].won)
                        {
                            //Tie, treat as same rank
                            sb.Append("-");
                        }
                        else sb.Append(i + 1); //Start listing at 1, not 0
                        sb.Append("</td>");
                    }
                    sb.Append("<td>");
                    sb.Append(SanitizeHTMLString(entry.name.Replace("%20", " ")));
                    sb.Append("</td><td>");
                    sb.Append(entry.score);
                    if (entry.won != 1) sb.Append(" (lost)");
                    sb.AppendLine("</td></tr>");
                }
            }
            return sb.ToString();
        }

        public static string GetDateNavigation(int year, int month, int day, bool ongoing)
        {
            StringBuilder sb = new StringBuilder();
            DateTime dt = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);
            if (!(year == Program.config.firstYear && month == Program.config.firstMonth && day == Program.config.firstDay))
            {
                sb.Append("<a href=\"");
                sb.Append(Program.GetYYYYMMDD(dt.Subtract(new TimeSpan(1, 0, 0, 0))));
                sb.Append(".html\">");
                sb.Append(Program.GetYYYYMMDD(dt.Subtract(new TimeSpan(1, 0, 0, 0))));
                sb.Append("</a>");
                sb.Append("&nbsp;&nbsp;&nbsp;&nbsp;<<&nbsp;&nbsp;&nbsp;&nbsp;");
            }
            sb.Append(Program.GetYYYYMMDD(dt));
            sb.Append("&nbsp;&nbsp;&nbsp;&nbsp;>>&nbsp;&nbsp;&nbsp;&nbsp;");
            if (!ongoing)
            {
                sb.Append("<a href=\"");
                DateTime tomorrow = dt.AddDays(1);
                sb.Append(Program.GetYYYYMMDD(tomorrow));
                sb.Append(".html\">");
            }
            sb.Append(Program.GetYYYYMMDD(dt.AddDays(1)));
            if (!ongoing) sb.Append("</a>");
            return sb.ToString();
        }

        public static string SanitizeHTMLString(string input)
        {
            return input.Replace("<", "").Replace(">", "");
        }
    }
}
