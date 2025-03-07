using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace SimplySerial
{
    /// <summary>
    /// Represents a vendor with a vendor ID and make.
    /// </summary>
    public class Vendor
    {
        /// <summary>
        /// Vendor ID.
        /// </summary>
        public string vid = "----";

        /// <summary>
        /// Vendor make.
        /// </summary>
        public string make = "VID";
    }


    /// <summary>
    /// Represents a development board with USB PID/VID, make and model.
    /// </summary>
    public class Board
    {
        /// <summary>
        /// Vendor ID.
        /// </summary>
        public string vid;

        /// <summary>
        /// Product ID.
        /// </summary>
        public string pid;

        /// <summary>
        /// Make of the board.
        /// </summary>
        public string make;

        /// <summary>
        /// Model of the board.
        /// </summary>
        public string model;

        /// <summary>
        /// Initializes a new instance of the <see cref="Board"/> class.
        /// </summary>
        /// <param name="vid">Vendor ID.</param>
        /// <param name="pid">Product ID.</param>
        /// <param name="make">Make of the board.</param>
        /// <param name="model">Model of the board.</param>
        public Board(string vid = "----", string pid = "----", string make = "", string model = "")
        {
            this.vid = vid.ToUpper();
            this.pid = pid.ToUpper();

            if (make != "")
                this.make = make;
            else
                this.make = $"VID:{this.vid}";

            if (model != "")
                this.model = model;
            else
                this.model = $"PID:{this.pid}";
        }

        public override string ToString()
        {
            return $"[{vid}:{pid}] {make} {model}";
        }
    }

    /// <summary>
    /// Represents the board list data structure.
    /// </summary>
    public class BoardData
    {
        [JsonProperty("version")]
        public string Version { get; set; } = "";

        [JsonProperty("vendors")]
        public List<Vendor> Vendors { get; set; } = new List<Vendor>();

        [JsonProperty("boards")]
        public List<Board> Boards { get; set; } = new List<Board>();

    }


    /// <summary>
    /// Manages a collection of vendors and boards, and provides methods to load and match boards based on USB PID and VID.
    /// </summary>
    public static class BoardManager
    {
        /// <summary>
        /// Gets the version of the board file.
        /// </summary>
        public static string Version => BoardManager._boardData.Version;

        /// <summary>
        /// Gets the list of vendors.
        /// </summary>
        public static List<Vendor> Vendors => BoardManager._boardData.Vendors;

        /// <summary>
        /// Gets the list of boards.
        /// </summary>
        public static List<Board> Boards => BoardManager._boardData.Boards;

        private static BoardData _boardData = new BoardData();
        private static string _boardFile = SimplySerial.AppFolder + "boards.json";

        /// <summary>
        /// Loads the board data from a JSON file.
        /// </summary>
        /// <param name="file">Optional file path to load the board data from.</param>
        /// <param name="merge">Optional file path to merge with previously loaded data.</param>
        /// <returns>True if the data was loaded successfully, otherwise false.</returns>
        public static void Load(string file = "", string merge = "")
        {
            string newFile;
            BoardData newData = new BoardData();

            if (!String.IsNullOrEmpty(file))
            {
                _boardFile = file;
                newFile = file;
                merge = "";
            }
            else if (!String.IsNullOrEmpty(merge))
            {
                newFile = merge;
            }
            else
            {
                newFile = _boardFile;
            }
            try
            {
                using (StreamReader r = new StreamReader(newFile))
                {
                    newData = JsonConvert.DeserializeObject<BoardData>(r.ReadToEnd());
                }
            }
            catch (Exception)
            {
                newData.Vendors = new List<Vendor>();
                newData.Boards = new List<Board>();
                newData.Version = "(board file is missing or invalid)";
            }

            if (!String.IsNullOrEmpty(merge))
            {
                foreach (Vendor vendor in newData.Vendors)
                {
                    if (vendor == null)
                        continue;
                    _boardData.Vendors.RemoveAll(v => v.vid == vendor.vid);
                    _boardData.Vendors.Add(vendor);
                }
                foreach (Board board in newData.Boards)
                {
                    if (board == null)
                        continue;
                    _boardData.Boards.RemoveAll(b => b.vid == board.vid && b.pid == board.pid);
                    _boardData.Boards.Add(board);
                }
            }
            else
            {
                _boardData = newData;
            }
        }


        /// <summary>
        /// Matches to a known development board based on VID and PID.
        /// </summary>
        /// <param name="vid">VID of the board.</param>
        /// <param name="pid">PID of the board.</param>
        /// <returns>A <see cref="Board"/> structure containing information about the matched board, or generic values otherwise.</returns>
        public static Board Match(string vid, string pid)
        {
            Board mBoard = null;
            if (Boards != null)
                mBoard = Boards.Find(b => (b.vid == vid) && (b.pid == pid));

            if (mBoard == null)
            {
                mBoard = new Board(vid: vid, pid: pid);

                Vendor mVendor = null;
                if (Vendors != null)
                    mVendor = Vendors.Find(v => v.vid == vid);
                if (mVendor != null)
                    mBoard.make = mVendor.make;
            }

            return mBoard;
        }

        /// <summary>
        /// Updates the board data file from the official GitHub repository.
        /// </summary>
        /// <returns></returns>
        public static bool Update()
        {
            const string RepoOwner = "fasteddy516";
            const string RepoName = "SimplySerial-Boards";

            Console.WriteLine("SimplySerial boards.json updater");
            Console.WriteLine($"  Installed: {BoardManager.Version}");

            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "SimplySerial-Boards-Updater");

                    // Get latest release info
                    string apiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                    string responseBody = client.DownloadString(apiUrl);

                    JObject releaseData = JObject.Parse(responseBody);
                    string availableVersion = releaseData["tag_name"]?.ToString();
                    string releaseNotes = releaseData["body"]?.ToString();
                    string boardsJsonUrl = null;

                    // Find the correct asset URL
                    foreach (JToken asset in releaseData["assets"] ?? new JArray())
                    {
                        string assetName = asset["name"]?.ToString();
                        if (string.Equals(assetName, "boards.json", StringComparison.OrdinalIgnoreCase))
                        {
                            boardsJsonUrl = asset["browser_download_url"]?.ToString();
                            break;
                        }
                    }

                    if (boardsJsonUrl == null)
                    {
                        Console.WriteLine("No boards.json found in the latest release.");
                        return false;
                    }

                    Console.WriteLine($"  Available: {availableVersion}\n");

                    if (Version == availableVersion)
                    {
                        Console.WriteLine("* boards.json is already up to date\n");
                        return false;
                    }

                    // Prompt user for action
                    while (true)
                    {
                        Console.Write("* An update is available.  Install it (Y/N) or view the release notes (R)?");
                        ConsoleKey key = Console.ReadKey(true).Key; // Reads key without displaying it
                        Console.WriteLine();

                        if (key == ConsoleKey.Y)
                        {
                            break; // Proceed with update
                        }
                        else if (key == ConsoleKey.R)
                        {
                            Console.WriteLine("\n--[ RELEASE NOTES ]-----------------------------------------\n");
                            Console.WriteLine(releaseNotes.TrimEnd());
                            Console.WriteLine("\n----------------------------------[ END OF RELEASE NOTES ]--\n");
                        }
                        else
                        {
                            Console.WriteLine("\nUpdate canceled.\n");
                            return false;
                        }
                    }

                    // Download and replace boards.json
                    Console.Write("\n+ Downloading new boards.json...");
                    try
                    {
                        client.DownloadFile(boardsJsonUrl, _boardFile);
                        Console.WriteLine("DONE");
                        Console.WriteLine("\nUpdate complete.\n");
                        return true; // Indicate update was applied
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException is UnauthorizedAccessException)
                        {
                            Console.WriteLine("ERROR: Permission denied. Try running the application as an administrator.");
                        }
                        else
                        {
                            Console.WriteLine($"ERROR: {ex.Message}");
                        }
                        Console.WriteLine("\nUpdate failed.\n");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n! Error checking for update: {ex.Message}\n");
            }

            return false;
        }
    }
}
