using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SalienBot
{
    class PlayerInfo
    {
        public int time_on_planet;
        public Planet active_planet;
        public int score;
        public int next_level_score;
        public int level;
    }

    class Zone
    {
        public int difficulty;
        public int planet_priority;

        public int planet_id;
        public int zone_position;

        public double capture_progress;
    }

    class Planet
    {
        public int id;
        public string name;
        public int difficulty;
        public double capture_progress;
        public int total_joins;
        public int current_players;
        public int planet_priority;

        public List<Zone> availableZones;
    }

    class Program
    {
        static int SLEEP_TIME = 120;
        static string ACCESS_TOKEN;

        public static List<int> Priorities = new List<int>() {
            2,   //Assasins Creed
            531, // Bioshock, Soma, Subnautica
            526, // Doom, Master of Orion, Prey
            1,   // 60 seconds and Lisa
            25,  // Stardew Valley
            20,  // Tomb Raider
            21,  // Slime Rancher
            36,  // Prince of Persia, SuperHot
            18,  // DBF Z, Mortal Kombat
            22,  // Megaman
            24,  // Super Meat Boy
            26,  // The Witcher and Amnesia
            35,  // Rocket League
            40,  // Goat Simulator
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14,
            15,
            16,
            17,
            19,
            27,
            28,
            29,
            30,
            31,
            32,
            33,
            34,
            38,
            39,
            41,
            42,
            508,
            520,
            524,
            525,
            527,
            528,
            529,
            530,
            532,
            533,
            534
        };

        static List<Planet> ActivePlanets = new List<Planet>();

        public static string BuildUrl(string method)
        {
            return "https://community.steam-api.com/" + method + "/v0001/";
        }

        static void Main(string[] args)
        {
            ACCESS_TOKEN = File.ReadAllText("token.txt");
            if (ACCESS_TOKEN.Length == 0)
            {
                Console.WriteLine("Token is empty!");
                return;
            }
            Console.WriteLine("Using Token: " + ACCESS_TOKEN);

            try
            {
                string[] lines = File.ReadAllLines("priorities.txt");
                if (lines.Length > 0)
                {
                    Priorities.Clear();
                    foreach (string l in lines)
                    {
                        Priorities.Add(int.Parse(l.Split('#')[0]));
                    }
                }
            }
            catch (Exception e) { }

            while (true)
            {
                try
                {
                  Iteration();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e.Message);
                    Console.WriteLine("Starting again...");
                }
            }
        }

        public static void Iteration()
        {
            RefreshData();

            Zone bestZone = DeterminateBestZoneAndPlanet();
            PlayerInfo playerInfo = GetPlayerInfo();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("------------------------------");
            if(playerInfo.active_planet != null)
                Console.WriteLine("Time on planet '" + playerInfo.active_planet.name + "': " + playerInfo.time_on_planet + "s");
            Console.WriteLine("Level: " + playerInfo.level);
            Console.WriteLine("XP: " + playerInfo.score + "/" + playerInfo.next_level_score + "  (" + (((double)playerInfo.score / (double)playerInfo.next_level_score)*100).ToString("#.##") + "%)");
            Console.WriteLine("------------------------------");
            Console.ResetColor();

            // Leave planet if necessary
            if (playerInfo.active_planet != null && playerInfo.active_planet.id != bestZone.planet_id)
            {
                Console.WriteLine("Leaving planet " + playerInfo.active_planet.name + "...");
                DoPostWithToken(BuildUrl("IMiniGameService/LeaveGame"), "gameid=" + playerInfo.active_planet.id);
                playerInfo.active_planet = null;
            }
            // Join planet if necessary
            if(playerInfo.active_planet == null || playerInfo.active_planet.id != bestZone.planet_id)
            {
                playerInfo.active_planet = ActivePlanets.Find(x => x.id == bestZone.planet_id);
                Console.WriteLine("Joining planet " + playerInfo.active_planet.name + "...");
                DoPostWithToken(BuildUrl("ITerritoryControlMinigameService/JoinPlanet"), "id=" + bestZone.planet_id);
            }

            JToken zone_join_resp = DoPostWithToken(BuildUrl("ITerritoryControlMinigameService/JoinZone"), "zone_position=" + bestZone.zone_position);
            if (!zone_join_resp.HasValues)
            {
                Console.WriteLine("Couldn't join zone " + bestZone.zone_position + "!");
                return;
            }
            Console.WriteLine("Joined zone " + bestZone.zone_position + " in planet " + playerInfo.active_planet.name);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("------------------------------");
            Console.WriteLine("Current zone captured: " + (bestZone.capture_progress * 100).ToString("#.##") + "%");
            Console.WriteLine("Current planet captured: " + (playerInfo.active_planet.capture_progress * 100).ToString("#.##") + "%");
            Console.WriteLine("Current planet players: " + playerInfo.active_planet.current_players);
            Console.ResetColor();

            Console.WriteLine("Sleeping for " + SLEEP_TIME + " seconds...");
            Thread.Sleep(1000 * SLEEP_TIME);

            ReportScore(GetScoreFromDifficulty(bestZone.difficulty));
        }

        private static void ReportScore(int score)
        {
            Console.WriteLine("Reporting score " + score + "...");

            JToken token = DoPostWithToken(BuildUrl("ITerritoryControlMinigameService/ReportScore"), "score=" + score + "&language=english");

            if (token.HasValues)
            {
                int new_score = (int)token["new_score"];
                int old_score = (int)token["old_score"];
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Earned " + (new_score - old_score));
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("Couldn't report score! :(");
            }
        }

        private static int GetScoreFromDifficulty(int difficulty)
        {
            if (difficulty == 1)
                return 600;
            else if (difficulty == 2)
                return 1200;
            else
                return 2400;
        }

        public static void RefreshData()
        {
            ActivePlanets.Clear();

            JToken response = DoGet(BuildUrl("ITerritoryControlMinigameService/GetPlanets") + "/?active_only=1&language=english");
            var planets = response.SelectToken("planets");

            string s = planets.ToString();

            foreach (JToken planet in planets)
            {
                Planet p = new Planet
                {
                    id = (int)planet["id"],
                    name = (string)planet["state"]["name"],
                    difficulty = (int)planet["state"]["difficulty"],
                    capture_progress = (double)planet["state"]["capture_progress"],
                    total_joins = (int)planet["state"]["total_joins"],
                    current_players = (int)planet["state"]["current_players"],
                    planet_priority = 100000 - Priorities.IndexOf((int)planet["id"]),
                    availableZones = new List<Zone>()
                };

                JToken planet_response = DoGet(BuildUrl("ITerritoryControlMinigameService/GetPlanet") + "/?id=" + p.id);
                var zones = planet_response["planets"].First["zones"];

                foreach (JToken zone in zones)
                {
                    if (!(bool)zone["captured"])
                    {
                        Zone z = new Zone
                        {
                            difficulty = (int)zone["difficulty"],
                            planet_priority = p.planet_priority,

                            planet_id = p.id,
                            zone_position = (int)zone["zone_position"],

                            capture_progress = (double)zone["capture_progress"],
                        };
                        p.availableZones.Add(z);
                    }
                }

                ActivePlanets.Add(p);
            }
        }

        public static Zone DeterminateBestZoneAndPlanet()
        {
            List<Zone> allZones = new List<Zone>();

            foreach (Planet p in ActivePlanets)
                allZones.AddRange(p.availableZones);

            var result = allZones.OrderBy(c => c.difficulty).ThenBy(c => c.planet_priority);

            return result.Last();
        }

        public static PlayerInfo GetPlayerInfo()
        {
            JToken response = DoPostWithToken(BuildUrl("ITerritoryControlMinigameService/GetPlayerInfo"));

            PlayerInfo pi = new PlayerInfo
            {
                score = (int)response["score"],
                next_level_score = (int)response["next_level_score"],
                level = (int)response["level"],
                time_on_planet = 0,
                active_planet = null
            };

            try
            {
                pi.time_on_planet = (int)response["time_on_planet"];
                pi.active_planet = ActivePlanets.Find(x => x.id == (int)response["active_planet"]);
            }
            catch (Exception e) { }

            return pi;
        }

        public static JToken ParseResponse(string response)
        {
            return JObject.Parse(response).SelectToken("response");
        }

        public static JToken DoPostWithToken(string url, string post_data = "")
        {
            return DoPost(url, post_data + (post_data.Length > 0 ? "&" : "") + "access_token=" + ACCESS_TOKEN);
        }

        public static JToken DoGet(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            var response = (HttpWebResponse)request.GetResponse();
            return ParseResponse(new StreamReader(response.GetResponseStream()).ReadToEnd());
        }

        public static JToken DoPost(string url, string post_data)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            var data = Encoding.ASCII.GetBytes(post_data);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();

            return ParseResponse(new StreamReader(response.GetResponseStream()).ReadToEnd());
        }
    }
}
