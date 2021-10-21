using System.Collections.Generic;

namespace SimplySerial
{
    // TODO: Comment and cleanup
    
    public enum AutoConnect { NONE, ONE, ANY };

    public class Board
    {
        public string pid;
        public string make = "";
        public string model = "";
    }

    public class Vendor
    {
        public string vid;
        public string vendor;
        public List<Board> boards;
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
