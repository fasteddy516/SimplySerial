using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace SimplySerial
{
    public enum AutoConnect { NONE, ONE, ANY };

    public class ArgumentData
    {
        public string Name { get; }
        public string Value { get; }
        public string Type { get; }

        public ArgumentData(string[] argument, string type="")
        {
            Name = argument[0];
            Value = (argument.Length > 1) ? argument[1] : string.Empty;
            Type = type;
        }
    }

    public class CommandLineArgument : IComparable<CommandLineArgument>
    {
        public string Name { get { return Names[0]; } }

        public string[] Names { get; }

        public int Priority { get; }

        public Action<string> Handler { get; }

        public bool Immediate { get; }

        public string RawValue { get; set; }

        public string SetBy { get; set; }

        public bool Active { get; set; }

        public CommandLineArgument(string name, Action<string> handler, int priority = 99, bool immediate = false) : this(new[] { name }, handler, priority, immediate)
        {
        }

        public CommandLineArgument(string[] names, Action<string> handler, int priority=99, bool immediate=false)
        {
            Names = names;
            Handler = handler;
            Priority = Math.Min(priority, 99);
            Immediate = immediate;
            RawValue = string.Empty;
            SetBy = string.Empty;
            Active = false;
        }

        public int CompareTo(CommandLineArgument other)
        {
            if (other == null) return 1; // Treat null as having the lowest priority
            return Priority.CompareTo(other.Priority);
        }

        public string Match(string arg)
        {
            arg = arg.TrimStart('/', '-').ToLower();
            foreach (string name in Names)
            {
                if (name.StartsWith(arg))
                    return Name;
            }
            return null;
        }

        public void Handle()
        {
            try
            {
                Handler(RawValue);
            }
            catch (Exception)
            {
                string setby = SetBy.Length > 0 ? $" in {SetBy} Config" : "";
                string message;

                if (RawValue.Length > 0)
                    message = $"Invalid '{Name}' value <{RawValue}> specified";
                else
                    message = $"No value specified for '{Name}'";
                throw new ArgumentException($"{message}{setby}");
            }
        }
    }

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
