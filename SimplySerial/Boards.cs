using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

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
    }


    /// <summary>
    /// Manages a collection of vendors and boards, and provides methods to load and match boards based on USB PID and VID.
    /// </summary>
    public class BoardManager
    {
        /// <summary>
        /// Gets the version of the board file.
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; private set; } = "";

        /// <summary>
        /// Gets the list of vendors.
        /// </summary>
        [JsonProperty("vendors")]
        public List<Vendor> Vendors { get; private set; }

        /// <summary>
        /// Gets the list of boards.
        /// </summary>
        [JsonProperty("boards")]
        public List<Board> Boards { get; private set; }

        [JsonIgnore]
        private string _boardFile = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + "boards.json";

        /// <summary>
        /// Loads the board data from a JSON file.
        /// </summary>
        /// <param name="file">Optional file path to load the board data from.</param>
        /// <returns>True if the data was loaded successfully, otherwise false.</returns>
        public bool Load(string file = "")
        {
            if (!String.IsNullOrEmpty(file))
            {
                _boardFile = file;
            }
            try
            {
                using (StreamReader r = new StreamReader(_boardFile))
                {
                    string json = r.ReadToEnd();
                    JsonConvert.PopulateObject(json, this);
                }
                return true;
            }
            catch (Exception)
            {
                Vendors = new List<Vendor>();
                Boards = new List<Board>();
                Version = "(board file is missing or invalid)";
            }
            return false;
        }

        /// <summary>
        /// Matches to a known development board based on VID and PID.
        /// </summary>
        /// <param name="vid">VID of the board.</param>
        /// <param name="pid">PID of the board.</param>
        /// <returns>A <see cref="Board"/> structure containing information about the matched board, or generic values otherwise.</returns>
        public Board Match(string vid, string pid)
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
        /// Updates the board data from the official GitHub repository.
        /// </summary>
        /// <returns></returns>
        public bool Update()
        {
            // we want to update boards.json from https://github.com/fasteddy516/SimplySerial-Boards/releases/latest/download/boards.json here!

            return false;
        }
    }
}
