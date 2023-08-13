/*
Landnama Leaderboard Generator - Tool to generate HTML pages based on Landnama leaderboard data
Written in 2023 by VDZ
To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.
*/
//TL;DR for above notice: You can do whatever you want with this including commercial use without any restrictions or requirements.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization; //Add reference to System.Web.Extensions for this
using System.Diagnostics;

namespace LandnamaLeaderboardGenerator
{
    public class Program
    {
        public const string FILE_CONFIG = "config.json";

        public static Config config = new Config();

        static bool silent = false;
        static JavaScriptSerializer jss = null;

        public static void Main(string[] args)
        {
            //Init
            if (File.Exists(FILE_CONFIG)) config = DeserializeFromFile<Config>(FILE_CONFIG, "config");
            int year = DateTime.UtcNow.Year;
            int month = DateTime.UtcNow.Month;
            int day = DateTime.UtcNow.Day;
            string path = string.Empty;

            //Parse arguments
            bool argumentsValid = false;
            bool doPerformUpdate = false;
            bool doGenerateDailyPage = false;
            bool doCreateSymlinks = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--daily":
                        if (args.Length < i + 4)
                        {
                            Log("Insufficient arguments for --daily. Ignoring.");
                            continue;
                        }
                        year = Convert.ToInt32(args[i + 1]);
                        month = Convert.ToInt32(args[i + 2]);
                        day = Convert.ToInt32(args[i + 3]);
                        doGenerateDailyPage = true;
                        argumentsValid = true;
                        Log("Work: Daily for " + GetYYYYMMDD(year, month, day));
                        i += 3;
                        break;
                    case "--out":
                        if (args.Length < i + 2) continue;
                        i++;
                        path = args[i];
                        Log("Data folder: " + path);
                        break;
                    case "--silent":
                        silent = true;
                        Log("Running in silent mode.");
                        break;
                    case "--symlink":
                        doCreateSymlinks = true;
                        Log("Will symlink generated data to 'current'.");
                        break;
                    case "--update":
                        doPerformUpdate = true;
                        doGenerateDailyPage = true;
                        argumentsValid = true;
                        Log("Work: Current daily");
                        break;
                    case "--yesterday":
                        DateTime date = DateTime.UtcNow.Subtract(new TimeSpan(1, 0, 0, 0));
                        year = date.Year;
                        month = date.Month;
                        day = date.Day;
                        doGenerateDailyPage = true;
                        argumentsValid = true;
                        Log("Work: Daily for " + GetYYYYMMDD(date) + " (yesterday)");
                        break;
                    default:
                        Log("Unrecognized parameter: " + args[i]);
                        break;
                }
            }

            //Verify
            if (!argumentsValid) throw new Exception("No valid work type specified!");

            //Work
            //--Perform update--
            if (doPerformUpdate)
            {
                bool res = PerformUpdate(path, year, month, day);
                if (res == false && DateTime.UtcNow.Hour > 6) throw new Exception("No entries downloaded! Is something broken?");
                else Program.Log("Update successful.");
                if (doCreateSymlinks) Process.Start("ln", "-s \"" + GetDailyFilename(path, year, month, day) + "\" "
                    + EnsureTrailingSlash(path) + "current.json");

            }
            //--Generate daily page--
            if (doGenerateDailyPage)
            {
                string outputFile = EnsureTrailingSlash(path) + GetYYYYMMDD(year, month, day) + ".html";
                GenerateDailyPage(GetDailyFilename(path, year, month, day), outputFile, year, month, day);
                if (doCreateSymlinks) Process.Start("ln", "-s \"" + outputFile + "\" \"" + EnsureTrailingSlash(path) + "current.html\"");
            }

            Program.Log("Done!");
        }

        static bool PerformUpdate(string path, int year, int month, int day)
        {
            Program.Log("Performing update.");
            bool toReturn = true;

            for (int iTypes = 0; iTypes < 2; iTypes++)
            {
                //Init for daily/monthly
                string url = "";
                switch (iTypes)
                {
                    case 0:
                        Program.Log("Updating daily entries.");
                        url = Program.config.dataurlDaily;
                        break;
                    case 1:
                        Program.Log("Updating monthly entries.");
                        url = Program.config.dataurlMonthly;
                        break;
                }
                //Get file paths
                string outfile = GetDailyFilename(path, year, month, day);
                if (iTypes == 1) outfile = GetMonthlyFilename(path, year, month);
                //Load existing data
                List<PlayerEntry> playerEntries = new List<PlayerEntry>();
                if (File.Exists(outfile))
                {
                    Program.Log("Entries file already found at '" + outfile + "', loading...");
                    playerEntries = DeserializeFromFile<List<PlayerEntry>>(outfile, "player entries");
                }
                //Download and add new data
                playerEntries.AddRange(DownloadPlayerEntries(url));
                if (playerEntries.Count == 0)
                {
                    Program.Log("No entries found. Aborting update step.");
                    toReturn = false;
                    continue;
                }
                //Sort
                playerEntries.Sort((x, y) =>
                {
                    int res = y.won.CompareTo(x.won);
                    if (res == 0) res = y.hagalaz.CompareTo(x.hagalaz);
                    if (res == 0) res = x.score.CompareTo(y.score);
                    return res;
                }
                );
                //Remove duplicates
                for (int i = 0; i < playerEntries.Count; i++)
                {
                    string playerName = playerEntries[i].name;
                    for (int j = i + 1; j < playerEntries.Count; j++)
                    {
                        if (playerEntries[j].name == playerName)
                        {
                            playerEntries.RemoveAt(j);
                            j--;
                            continue;
                        }
                    }
                }
                //Write JSON
                string tempOutfile = outfile + ".tmp";
                if (File.Exists(tempOutfile)) File.Delete(tempOutfile);
                SerializeToFile(tempOutfile, playerEntries, "player entries");
                if (File.Exists(outfile)) File.Delete(outfile);
                File.Move(tempOutfile, outfile);
                Program.Log("Update step successful.");
            }
            return toReturn;
        }

        static PlayerEntry[] DownloadPlayerEntries(string url)
        {
            Program.Log("Downloading data from '" + url + "'...");
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = Program.config.useragent;
            string json = wc.DownloadString(url);
            Program.Log("Data downloaded.");
            try
            {
                return Deserialize<PlayerEntry[]>(json, "player entries");
            }
            catch (Exception ex)
            {
                Program.Log("Exception occurred during JSON deserialization! Exception: " + ex.ToString() + "\n\nJSON:\n" + json);
                throw ex;
            }
        }

        static void GenerateDailyPage(string entriesFile, string outputFile, int year, int month, int day)
        {
            Program.Log("Generating daily page for " + GetYYYYMMDD(year, month, day)
                + " to file '" + outputFile + "', loading entries from '" + entriesFile + "'.");
            PlayerEntry[] entries = new PlayerEntry[0];
            DateTime timestamp = DateTime.UtcNow;
            if (!File.Exists(entriesFile))
            {
                Program.Log("Could not find entries file! Assuming no entries have been submitted so far!");
            }
            else
            {
                FileInfo fi = new FileInfo(entriesFile);
                timestamp = fi.LastWriteTimeUtc;
                entries = DeserializeFromFile<PlayerEntry[]>(entriesFile, "player entries");
            }
            string html = PageWriter.GenerateDaily(entries, year, month, day, timestamp);

            Program.Log("Writing page to disk...");
            string tempOutputfile = outputFile + ".tmp";
            if (File.Exists(tempOutputfile)) File.Delete(tempOutputfile);
            WriteFile(tempOutputfile, html, true);
            if (File.Exists(outputFile)) File.Delete(outputFile);
            File.Move(tempOutputfile, outputFile);
            Program.Log(outputFile + " written successfully!");
        }

        public static void WriteFile(string filename, string content, bool silent = false)
        {
            if (!silent) Log("Writing " + filename);
            StreamWriter sw = new StreamWriter(filename);
            sw.Write(content);
            sw.Close();
        }

        private static void InitSerializer()
        {
            if (jss != null) return;
            jss = new JavaScriptSerializer();
            jss.MaxJsonLength = 2000 * 1024 * 1024;
        }

        public static string Serialize(object obj, string description)
        {
            if (description != null) Program.Log("Serializing " + description + "...");
            InitSerializer();
            string json = jss.Serialize(obj);
            if (description != null) Program.Log("Serialization of " + description + " successful.");
            return json;
        }

        public static void SerializeToFile(string filename, object obj, string description)
        {
            string json = Serialize(obj, description);
            if (description != null) Program.Log("Writing JSON for " + description + " to " + filename + "...");
            Program.WriteFile(filename, json);
            if (description != null) Program.Log("Finished writing JSON for " + description + ".");
        }

        public static T Deserialize<T>(string json, string description)
        {
            InitSerializer();
            Program.Log("Deserializing " + description + "...");
            T toReturn = jss.Deserialize<T>(json);
            if (toReturn == null) throw new Exception("Could not deserialize JSON for " + description + "!");
            Program.Log("Deserialization of " + description + " successful.");
            return toReturn;
        }

        public static T DeserializeFromFile<T>(string filename, string description)
        {
            Program.Log("Reading " + filename + " for " + description + " deserialization...");
            StreamReader sr = new StreamReader(filename);
            string json = sr.ReadToEnd();
            sr.Close();
            return Deserialize<T>(json, description);
        }

        public static string GetDailyFilename(string path, int year, int month, int day)
        {
            //path/daily_2023-08-06.json
            return EnsureTrailingSlash(path) + "daily_"
                + GetYYYYMMDD(year, month, day) + ".json";
        }

        public static string GetMonthlyFilename(string path, int year, int month)
        {
            //path/monthly_2023-08.json
            return EnsureTrailingSlash(path) + "monthly_"
                + Convert.ToString(year) + "-" + Convert.ToString(month).PadLeft(2, '0') + ".json";
        }

        public static string EnsureTrailingSlash(string input)
        {
            if (input.Length == 0) return input;
            if (input.EndsWith("/") || input.EndsWith("\\")) return input;
            return input + "/";
        }

        public static string GetYYYYMMDD(DateTime dt)
        {
            return GetYYYYMMDD(dt.Year, dt.Month, dt.Day);
        }

        public static string GetYYYYMMDD(int year, int month, int day)
        {
            return Convert.ToString(year) + "-" + Convert.ToString(month).PadLeft(2, '0') + "-" + Convert.ToString(day).PadLeft(2, '0');
        }

        public static void Log(string msg)
        {
            msg = "[" + DateTime.UtcNow.ToString() + "] " + msg;
            if (!silent) Console.WriteLine(msg);
        }
    }
}
