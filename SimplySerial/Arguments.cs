using System;

namespace SimplySerial
{
    public class ArgumentData
    {
        public string Name { get; }
        public string Value { get; }
        public string Type { get; }

        public ArgumentData(string[] argument, string type = "")
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

        public CommandLineArgument(string[] names, Action<string> handler, int priority = 99, bool immediate = false)
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
}
