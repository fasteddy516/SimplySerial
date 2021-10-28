using System.Collections.Generic;

namespace SimplySerial
{
    // TODO: Comment and cleanup
    
    public enum AutoConnect { NONE, ONE, ANY };

    public class BoardData
    {
        public string version = "";
        public List<Vendor> vendors;
        public List<Board> boards;
    }
    
    public class Board
    {
        public string vid;
        public string pid;
        public string make;
        public string model;
    
        public Board(string vid="----", string pid="----", string make="", string model="")
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

    public class Vendor
    {
        public string vid = "----";
        public string make = "VID";
    }

    /// <summary>
    /// Custom structure containing the name, VID, PID and description of a serial (COM) port
    /// Modified from the example written by Kamil Górski (freakone) available at
    /// http://blog.gorski.pm/serial-port-details-in-c-sharp
    /// https://github.com/freakone/serial-reader
    /// </summary>
    public class ComPort // custom struct with our desired values
    {
        public string name;
        public int num = -1;
        public string vid = "----";
        public string pid = "----";
        public string description;
        public string busDescription;
        public Board board;
        public bool isCircuitPython = false;
    }
}
