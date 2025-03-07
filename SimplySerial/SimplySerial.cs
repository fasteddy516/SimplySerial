using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SimplySerial
{
    class SimplySerial
    {
        const string version = "0.9.0";

        const string configFile = "ss.cfg";
        const string customBoardFile = "ss_board.json";
        const string filterFile = "ss_filters.json";

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        static string appFolder = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        static string workingFolder = Directory.GetCurrentDirectory().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        static string globalConfig = appFolder + configFile;
        static string localConfig = (appFolder != workingFolder) ? workingFolder + configFile : "noLocalConfig";
        static string userConfig = "noUserConfig";

        private static Dictionary<string, CommandLineArgument> CommandLineArguments = new Dictionary<string, CommandLineArgument>();

        static List<ComPort> availablePorts = new List<ComPort>();
        static SerialPort serialPort;

        // default comspec values and application settings set here will be overridden by values passed through command-line arguments
        static bool Quiet = false;
        static AutoConnect autoConnect = AutoConnect.ONE;
        static ComPort port = new ComPort();
        static int baud = -1;
        static Parity parity = Parity.None;
        static int dataBits = 8;
        static StopBits stopBits = StopBits.One;
        static bool logging = false;
        static FileMode logMode = FileMode.Create;
        static string logFile = string.Empty;
        static string logData = string.Empty;
        static int bufferSize = 102400;
        static DateTime lastFlush = DateTime.Now;
        static bool forceNewline = false;
        static Encoding encoding = Encoding.UTF8;
        static bool convertToPrintable = false;
        static bool clearScreen = true;
        static bool noStatus = false;
        static ConsoleKey exitKey = ConsoleKey.X;
        static bool localEcho = false;
        static bool bulkSend = false;

        // dictionary of "special" keys with the corresponding string to send out when they are pressed
        static Dictionary<ConsoleKey, String> specialKeys = new Dictionary<ConsoleKey, String>
        {
            { ConsoleKey.UpArrow, "\x1B[A" },
            { ConsoleKey.DownArrow, "\x1B[B" },
            { ConsoleKey.RightArrow, "\x1B[C" },
            { ConsoleKey.LeftArrow, "\x1B[D" },
            { ConsoleKey.Home, "\x1B[H" },
            { ConsoleKey.End, "\x1B[F" },
            { ConsoleKey.Insert, "\x1B[2~" },
            { ConsoleKey.Delete, "\x1B[3~" },
            { ConsoleKey.PageUp, "\x1B[5~" },
            { ConsoleKey.PageDown, "\x1B[6~" },
            { ConsoleKey.F1, "\x1B[11~" },
            { ConsoleKey.F2, "\x1B[12~" },
            { ConsoleKey.F3, "\x1B[13~" },
            { ConsoleKey.F4, "\x1B[14~" },
            { ConsoleKey.F5, "\x1B[15~" },
            { ConsoleKey.F6, "\x1B[17~" },
            { ConsoleKey.F7, "\x1B[18~" },
            { ConsoleKey.F8, "\x1B[19~" },
            { ConsoleKey.F9, "\x1B[20~" },
            { ConsoleKey.F10, "\x1B[21~" },
            { ConsoleKey.F11, "\x1B[23~" },
            { ConsoleKey.F12, "\x1B[24~" },
            { ConsoleKey.Enter, "\r" }
        };

        static void Main(string[] args)
        {
            // initialize port name
            port.name = String.Empty;

            // load and parse data in boards.json
            BoardManager.Load();

            // load and merge in custom board data
            BoardManager.Load(merge: appFolder + customBoardFile);
            if (appFolder != workingFolder)
            {
                BoardManager.Load(merge: workingFolder + customBoardFile);
            }

            // load device filters
            List<Filter> filters = Filter.AddFrom(appFolder + filterFile);
            if (appFolder != workingFolder)
            {
                filters = Filter.AddFrom(workingFolder + filterFile, existing: filters);
            }

            // process all command-line arguments
            ProcessArguments(args);

            if (clearScreen)
            {
                Console.Clear();
            }

            if (autoConnect == AutoConnect.ANY)
            {
                UpdateTitle("SimplySerial: Searching...");
                Output($"<<< Attemping to connect to any available COM port.  Use CTRL-{exitKey} to cancel >>>");
            }
            else if (autoConnect == AutoConnect.ONE)
            {
                if (clearScreen)
                {
                    Console.Clear();
                }
                if (port.name == String.Empty)
                {
                    UpdateTitle("SimplySerial: Searching...");
                    Output($"<<< Attempting to connect to first available COM port.  Use CTRL-{exitKey} to cancel >>>");
                }
                else
                {
                    UpdateTitle($"{port.name}: Searching...");
                    Output("<<< Attempting to connect to " + port.name + $".  Use CTRL-{exitKey} to cancel >>>");
                }
            }

            // attempt to enable virtual terminal escape sequence processing
            if (!convertToPrintable)
            {
                try
                {
                    var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
                    GetConsoleMode(iStdOut, out uint outConsoleMode);
                    outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                    SetConsoleMode(iStdOut, outConsoleMode);
                }
                catch
                {
                    // if the above fails, it doesn't really matter - it just means escape sequences won't process nicely
                }
            }

            Console.OutputEncoding = encoding;

            // verify log-related settings
            if (logging)
            {
                try
                {
                    FileStream stream = new FileStream(logFile, logMode, FileAccess.Write);
                    using (StreamWriter writer = new StreamWriter(stream, encoding))
                    {
                        writer.WriteLine($"\n----- LOGGING STARTED ({DateTime.Now}) ------------------------------------");
                    }
                }
                catch (Exception e)
                {
                    logging = false;
                    ExitProgram($"* Error accessing log file '{logFile}'\n  > {e.GetType()}: {e.Message}", exitCode: -1);
                }
            }

            // set up keyboard input for program control / relay to serial port
            ConsoleKeyInfo keyInfo = new ConsoleKeyInfo();
            Console.TreatControlCAsInput = true; // we need to use CTRL-C to activate the REPL in CircuitPython, so it can't be used to exit the application

            // this is where data read from the serial port will be temporarily stored
            string received = string.Empty;

            //main loop - keep this up until user presses CTRL-[exitKey] or an exception takes us down
            do
            {
                // first things first, check for (and respect) a request to exit the program via CTRL-[exitKey]
                if (Console.KeyAvailable)
                {
                    keyInfo = Console.ReadKey(intercept: true);
                    if ((keyInfo.Key == exitKey) && (keyInfo.Modifiers == ConsoleModifiers.Control))
                    {
                        Output($"\n<<< SimplySerial session terminated via CTRL-{exitKey} >>>");
                        ExitProgram(silent: true);
                    }
                }

                // get a list of available ports
                availablePorts = (SimplySerial.GetSerialPorts()).OrderBy(p => p.num).ToList();

                // if no port was specified/selected, pick one automatically
                if (port.name == String.Empty)
                {
                    // if there are com ports available, pick one
                    if (availablePorts.Count() >= 1)
                    {
                        // first, try to default to something that we assume is running CircuitPython unless this behaviour has been disabled by a filter
                        if (filters.Find(f => f.Type == FilterType.EXCLUDE && f.Match == FilterMatch.CIRCUITPYTHON) == null)
                        {
                            SimplySerial.port = availablePorts.Find(p => p.isCircuitPython == true);
                        }
                        else
                        {
                            SimplySerial.port = null;
                        }

                        // if that doesn't work out, just default to the first available COM port
                        if (SimplySerial.port == null)
                            SimplySerial.port = availablePorts[0];
                    }

                    // if there are no com ports available, exit or try again depending on autoconnect setting 
                    else
                    {
                        if (autoConnect == AutoConnect.NONE)
                            ExitProgram("No COM ports detected.", exitCode: -1);
                        else
                            continue;
                    }
                }

                // if a specific port has been selected, try to match it with one that actually exists
                else
                {
                    bool portMatched = false;

                    foreach (ComPort p in availablePorts)
                    {
                        if (p.name == port.name)
                        {
                            portMatched = true;
                            port = p;
                            break;
                        }
                    }

                    // if the specified port is not available, exit or try again depending on autoconnect setting
                    if (!portMatched)
                    {
                        if (autoConnect == AutoConnect.NONE)
                            ExitProgram(("Invalid port specified <" + port.name + ">"), exitCode: -1);
                        else
                            continue;
                    }
                }

                // if we get this far, it should be safe to set up the specified/selected serial port
                serialPort = new SerialPort(port.name)
                {
                    Handshake = Handshake.None, // we don't need to support any handshaking at this point 
                    ReadTimeout = 1, // minimal timeout - we don't want to wait forever for data that may not be coming!
                    WriteTimeout = 250, // small delay - if we go too small on this it causes System.IO semaphore timeout exceptions
                    DtrEnable = true, // without this we don't ever receive any data
                    RtsEnable = true, // without this we don't ever receive any data
                    Encoding = encoding
                };

                // attempt to set the baud rate, fail if the specified value is not supported by the hardware
                try
                {
                    if (baud < 0)
                    {
                        if (port.isCircuitPython)
                            baud = 115200;
                        else
                            baud = 9600;
                    }

                    serialPort.BaudRate = baud;
                }
                catch (ArgumentOutOfRangeException)
                {
                    ExitProgram(("The specified baud rate (" + baud + ") is not supported."), exitCode: -2);
                }

                // set other port parameters (which have already been validated)
                serialPort.Parity = parity;
                serialPort.DataBits = dataBits;
                serialPort.StopBits = stopBits;

                // attempt to open the serial port, deal with failures
                try
                {
                    serialPort.Open();
                }
                catch (Exception e)
                {
                    // if auto-connect is disabled than any exception should result in program termination
                    if (autoConnect == AutoConnect.NONE)
                    {
                        if (e is UnauthorizedAccessException)
                            ExitProgram((e.GetType() + " occurred while attempting to open " + port.name + ".  Is this port already in use in another application?"), exitCode: -1);
                        else
                            ExitProgram((e.GetType() + " occurred while attempting to open " + port.name + "."), exitCode: -1);
                    }

                    // if auto-connect is enabled, prepare to try again
                    serialPort.Dispose();
                    Thread.Sleep(1000); // putting a delay here to avoid gobbling tons of resources thruogh constant high-speed re-connect attempts
                    continue;
                }

                UpdateTitle($"{port.name}: {port.board.make} {port.board.model}");

                // if we get this far, clear the screen and send the connection message if not in 'quiet' mode
                if (clearScreen)
                {
                    Console.Clear();
                }
                else
                {
                    Output("");
                }
                Output(String.Format("<<< SimplySerial v{0} connected via {1} >>>\n" +
                    "Settings  : {2} baud, {3} parity, {4} data bits, {5} stop bit{6}, {7} encoding, auto-connect {8}, echo {9}{10}\n" +
                    "Device    : {11} {12}{13}\n{14}" +
                    "---\n\nUse CTRL-{15} to exit.\n",
                    version,
                    port.name,
                    baud,
                    (parity == Parity.None) ? "no" : (parity.ToString()).ToLower(),
                    dataBits,
                    (stopBits == StopBits.None) ? "0" : (stopBits == StopBits.One) ? "1" : (stopBits == StopBits.OnePointFive) ? "1.5" : "2", (stopBits == StopBits.One) ? "" : "s",
                    (encoding.ToString() == "System.Text.UTF8Encoding") ? "UTF-8" : (convertToPrintable) ? "RAW" : "ASCII",
                    (autoConnect == AutoConnect.ONE) ? "on" : (autoConnect == AutoConnect.ANY) ? "any" : "off",
                    (localEcho) ? "on" : "off",
                    (bulkSend) ? ", bulk send enabled" : "",
                    port.board.make,
                    port.board.model,
                    (port.isCircuitPython) ? " (CircuitPython-capable)" : "",
                    (logging == true) ? ($"Logfile   : {logFile} (Mode = " + ((logMode == FileMode.Create) ? "OVERWRITE" : "APPEND") + ")\n") : "",
                    exitKey
                ), flush: true); ;

                lastFlush = DateTime.Now;
                DateTime start = DateTime.Now;
                TimeSpan timeSinceRX = new TimeSpan();
                TimeSpan timeSinceFlush = new TimeSpan();

                // this is the core functionality - loop while the serial port is open
                while (serialPort.IsOpen)
                {
                    try
                    {
                        // process keypresses for transmission through the serial port
                        while (Console.KeyAvailable)
                        {
                            // determine what key is pressed (including modifiers)
                            keyInfo = Console.ReadKey(intercept: true);

                            // exit the program if CTRL-[exitKey] was pressed
                            if ((keyInfo.Key == exitKey) && (keyInfo.Modifiers == ConsoleModifiers.Control))
                            {
                                Output($"\n<<< SimplySerial session terminated via CTRL-{exitKey} >>>");
                                ExitProgram(silent: true);
                            }

                            // check for keys that require special processing (cursor keys, etc.)
                            else if (specialKeys.ContainsKey(keyInfo.Key))
                            {
                                serialPort.Write(specialKeys[keyInfo.Key]);
                                if (localEcho)
                                    Output(specialKeys[keyInfo.Key], force: true, newline: false);
                            }

                            // everything else just gets sent right on through
                            else
                            {
                                string outstring = keyInfo.KeyChar.ToString();
                                serialPort.Write(outstring);
                                if (localEcho)
                                    Output(outstring, force: true, newline: false);
                            }
                            if (!bulkSend)
                                break;
                        }

                        // process data coming in from the serial port
                        received = serialPort.ReadExisting();

                        // if anything was received, process it
                        if (received.Length > 0)
                        {
                            // if we're trying to filter out title/status updates in received data, try to ensure we've got the whole string
                            if (noStatus && received.Contains("\x1b"))
                            {
                                Thread.Sleep(100);
                                received += serialPort.ReadExisting();
                            }

                            if (forceNewline)
                                received = received.Replace("\r", "\n");

                            // write what was received to console
                            Output(received, force: true, newline: false);
                            start = DateTime.Now;
                        }
                        else
                            Thread.Sleep(1);

                        if (logging)
                        {
                            timeSinceRX = DateTime.Now - start;
                            timeSinceFlush = DateTime.Now - lastFlush;
                            if ((timeSinceRX.TotalSeconds >= 2) || (timeSinceFlush.TotalSeconds >= 10))
                            {
                                if (logData.Length > 0)
                                    Output("", force: true, newline: false, flush: true);
                                start = DateTime.Now;
                                lastFlush = DateTime.Now;
                            }
                        }

                        // if the serial port is unexpectedly closed, throw an exception
                        if (!serialPort.IsOpen)
                            throw new IOException();
                    }
                    catch (Exception e)
                    {
                        if (autoConnect == AutoConnect.NONE)
                            ExitProgram((e.GetType() + " occurred while attempting to read/write to/from " + port.name + "."), exitCode: -1);
                        else
                        {
                            UpdateTitle($"{port.name}: (disconnected)");
                            Output("\n<<< Communications Interrupted >>>\n");
                        }
                        try
                        {
                            serialPort.Dispose();
                        }
                        catch
                        {
                            //nothing to do here, other than prevent execution from stopping if dispose() throws an exception
                        }
                        Thread.Sleep(2000); // sort-of arbitrary delay - should be long enough to read the "interrupted" message
                        if (autoConnect == AutoConnect.ANY)
                        {
                            UpdateTitle("SimplySerial: Searching...");
                            port.name = String.Empty;
                            Output($"<<< Attemping to connect to any available COM port.  Use CTRL-{exitKey} to cancel >>>");
                        }
                        else if (autoConnect == AutoConnect.ONE)
                        {
                            UpdateTitle($"{port.name}: Searching...");
                            Output("<<< Attempting to re-connect to " + port.name + $".  Use CTRL-{exitKey} to cancel >>>");
                        }
                        break;
                    }
                }
            } while (autoConnect > AutoConnect.NONE);

            // if we get to this point, we should be exiting gracefully
            ExitProgram("<<< SimplySerial session terminated >>>", exitCode: 0);
        }

        static bool ArgProcessor_OnOff(string value)
        {
            value = value.ToLower();
            if (value == "" || value.StartsWith("on"))
                return true;
            else if (value.StartsWith("off"))
                return false;
            throw new ArgumentException();
        }

        static void ArgHandler_Help(string value)
        {
            ShowHelp();
            ExitProgram(silent: true);
        }

        static void ArgHandler_Version(string value)
        {
            ShowVersion();
            ExitProgram(silent: true);
        }

        static void ArgHandler_List(string value)
        {
            // get a list of all available ports
            availablePorts = (GetSerialPorts()).OrderBy(p => p.num).ToList();

            if (availablePorts.Count >= 1)
            {
                Console.WriteLine("\nPORT\tVID\tPID\tDESCRIPTION");
                Console.WriteLine("----------------------------------------------------------------------");
                foreach (ComPort p in availablePorts)
                {
                    Console.WriteLine("{0}\t{1}\t{2}\t{3} {4}",
                        p.name,
                        p.vid,
                        p.pid,
                        (p.isCircuitPython) ? (p.board.make + " " + p.board.model) : p.description,
                        ((p.busDescription.Length > 0) && !p.description.StartsWith(p.busDescription)) ? ("[" + p.busDescription + "]") : ""
                    );
                }
                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine("\nNo COM ports detected.\n");
            }

            ExitProgram(silent: true);
        }

        static void ArgHandler_Quiet(string value)
        {
            Quiet = ArgProcessor_OnOff(value);
        }

        static void ArgHandler_ForceNewLine(string value)
        {
            forceNewline = ArgProcessor_OnOff(value);
        }

        static void ArgHandler_ClearScreen(string value)
        {
            clearScreen = ArgProcessor_OnOff(value);
        }

        static void ArgHandler_Status(string value)
        {
            noStatus = ArgProcessor_OnOff(value);
        }

        static void ArgHandler_Com(string value)
        {
            // preliminary validate on com port, final validation occurs towards the end of ProcessArguments()
            string newPort = value.ToUpper();

            if (String.IsNullOrEmpty(value))
                throw new ArgumentException();
            if (!value.StartsWith("COM"))
                newPort = "COM" + value;
            port.name = newPort;
            autoConnect = AutoConnect.ONE;
        }

        static void ArgHandler_Baud(string value)
        {
            baud = Convert.ToInt32(value);
        }

        static void ArgHandler_Parity(string value)
        {
            value = value.ToLower();

            if (value.StartsWith("e"))
                parity = Parity.Even;
            else if (value.StartsWith("m"))
                parity = Parity.Mark;
            else if (value.StartsWith("n"))
                parity = Parity.None;
            else if (value.StartsWith("o"))
                parity = Parity.Odd;
            else if (value.StartsWith("s"))
                parity = Parity.Space;
            else
                throw new ArgumentException();
        }

        static void ArgHandler_DataBits(string value)
        {
            int newDataBits = Convert.ToInt32(value);

            if ((newDataBits > 3) && (newDataBits < 9))
                dataBits = newDataBits;
            else
                throw new ArgumentException();
        }

        static void ArgHandler_StopBits(string value)
        {
            if (value == "0")
                stopBits = StopBits.None;
            else if (value == "1")
                stopBits = StopBits.One;
            else if (value == "1.5")
                stopBits = StopBits.OnePointFive;
            else if (value == "2")
                stopBits = StopBits.Two;
            else
                ExitProgram(("Invalid stop bits specified <" + value + ">"), exitCode: -1);
        }

        static void ArgHandler_Encoding(string value)
        {
            value = value.ToLower();

            if (value.StartsWith("a"))
            {
                encoding = Encoding.ASCII;
                convertToPrintable = false;
            }
            else if (value.StartsWith("r"))
            {
                encoding = Encoding.GetEncoding(1252);
                convertToPrintable = true;
            }
            else if (value.StartsWith("u"))
            {
                encoding = Encoding.UTF8;
                convertToPrintable = false;
            }
            else
                throw new ArgumentException();
        }

        static void ArgHandler_Echo(string value)
        {
            localEcho = ArgProcessor_OnOff(value);
        }

        static void ArgHandler_AutoConnect(string value)
        {
            value = value.ToLower();
            if (value.StartsWith("n"))
                autoConnect = AutoConnect.NONE;
            else if (value.StartsWith("o"))
                autoConnect = AutoConnect.ONE;
            else if (value.StartsWith("a"))
                autoConnect = AutoConnect.ANY;
            else
                throw new ArgumentException();
        }

        static void ArgHandler_Log(string value)
        {
            if (String.IsNullOrEmpty(value))
                throw new ArgumentException();
            logging = true;
            logFile = value;
        }

        static void ArgHandler_LogMode(string value)
        {
            value = value.ToLower();
            if (value.StartsWith("o"))
                logMode = FileMode.Create;
            else if (value.StartsWith("a"))
                logMode = FileMode.Append;
            else
                throw new ArgumentException();
        }

        static void ArgHandler_Title(string value)
        {
            if (String.IsNullOrEmpty(value))
                throw new ArgumentException();
            noStatus = true;
            UpdateTitle(value, force: true);
        }

        static void ArgHandler_ExitKey(string value)
        {
            string keyArg = value.ToUpper();
            if (Enum.TryParse($"Oem{keyArg}", out ConsoleKey parsedKey) || Enum.TryParse(keyArg, out parsedKey))
            {
                exitKey = parsedKey;
            }
            else
            {
                throw new ArgumentException();    
            }
        }

        static void ArgHandler_BulkSend(string value)
        {
            bulkSend = ArgProcessor_OnOff(value);
        }

        static void ArgHandler_UpdateBoards(string value)
        {
            BoardManager.Update();
            ExitProgram(silent: true);
        }

        static void ArgHandler_TXOnEnter(string value)
        {
            value = value.ToLower();

            if (value.Equals("cr"))
            {
                specialKeys[ConsoleKey.Enter] = "\r";
            }
            else if (value.Equals("lf"))
            {
                specialKeys[ConsoleKey.Enter] = "\n";
            }
            else if (value.Equals("crlf"))
            {
                specialKeys[ConsoleKey.Enter] = "\r\n";
            }
            else if (value.StartsWith("custom=") && (value.Length > 7))
            {
                specialKeys[ConsoleKey.Enter] = value.Substring(7);
            }
            else if (value.StartsWith("bytes=") && (value.Length > 6) )
            {
                string temp = value.Substring(6).Replace(" ", "").Replace("\"", "").Replace("0x", "");

                if (temp.Length % 2 != 0)
                {
                    throw new ArgumentException();
                }
                else
                {
                    string plaintext = string.Empty;

                    for (int read_index = 0; read_index <= (temp.Length - 2); read_index += 2)
                    {
                        try
                        {
                            plaintext += Convert.ToChar(Convert.ToByte(temp.Substring(read_index, 2), 16));
                        }
                        catch
                        {
                            throw new ArgumentException();
                        }

                    }
                    specialKeys[ConsoleKey.Enter] = plaintext;
                }
            }
            else
            {
                throw new ArgumentException();
            }
        }

        static List<ArgumentData> ParseArguments(string[] args, bool noImmediate=false, string source="")
        {
            List<ArgumentData> receivedArguments = new List<ArgumentData>();
            string sourceType = "";

            if (source == globalConfig)
                sourceType = "Global";
            else if (source == localConfig)
                sourceType = "Local";
            else if (source == userConfig)
                sourceType = "User";

            // iterate through command-line arguments
            foreach (string arg in args)
            {
                // split argument into components based on 'key:value' formatting and switch argument name to lower case
                string[] argument = arg.Split(new[] { ':' }, 2);
                string matchedName = null;

                foreach (CommandLineArgument validArg in CommandLineArguments.Values)
                {
                    matchedName = validArg.Match(argument[0]);
                    if (!String.IsNullOrEmpty(matchedName))
                    {
                        argument[0] = matchedName;
                        if (!noImmediate || !validArg.Immediate)
                        {
                            receivedArguments.Add(new ArgumentData(argument, sourceType));
                        }
                        break;
                    }
                }

                if (String.IsNullOrEmpty(matchedName))
                {
                    if (source.Length > 0)
                        source = $" in [{source}]";
                    ExitProgram($"Invalid argument '{arg}'{source}\nTry 'ss.exe help' to see a list of valid arguments", exitCode: -1);
                }
            }
            return receivedArguments;
        }

        static string[] LoadConfig(string file, bool failOnError=true)
        {
            string[] args = new string[] { };
            try
            {
                args = File.ReadAllLines(file);
            }
            catch (Exception e)
            {
                if (failOnError)
                {
                    ExitProgram($"Error reading configuration file '{file}'\n> {e.GetType()}: {e.Message}", exitCode: -1);
                }
            }
            return args;
        }

        /// <summary>
        /// Validates and processes any command-line arguments that were passed in.  Invalid arguments will halt program execution.
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        static void ProcessArguments(string[] args)
        {
            // Add all command-line arguments to the dictionary.  Things to note:
            //
            // 1. Arguments are processed in ascending order based on priority, which is specified for commands
            //    that *need* to run before others, or in cases where multiple commands start with the same letter(s)
            //    and we want a short form to map to a specific command.  Default priority is 99.
            //
            //    In general, DON'T MESS WITH PRIORITY NUMBERS!  They are set up the way they are for a reason.
            //
            // 2. Arguments flagged as immediate are used to trigger actions that display to console and exit
            //    the program (i.e. help, version, list, etc.).  They are flagged so that they can be ignored
            //    if they are present in a configuration file.
            //
            // 3. Some arguments have multiple names that can be used to trigger them.  This is to allow for
            //    aliases (i.e. help and ?) as well as backwards compatability (i.e. clearscreen and noclear).
            //
            CommandLineArguments.Add("help", new CommandLineArgument(new[] { "help", "?" }, handler: ArgHandler_Help, priority: 0, immediate: true)); // always process help first
            CommandLineArguments.Add("version", new CommandLineArgument("version", handler: ArgHandler_Version, priority: 1, immediate: true)); // always process version second
            CommandLineArguments.Add("list", new CommandLineArgument("list", handler: ArgHandler_List, priority: 2, immediate: true)); // always process list third
            CommandLineArguments.Add("updateboards", new CommandLineArgument("updateboards", handler: ArgHandler_UpdateBoards, priority: 3, immediate: true)); // always process updateboards fourth
            CommandLineArguments.Add("quiet", new CommandLineArgument("quiet", handler: ArgHandler_Quiet, priority: 4)); // process quiet before anything else
            CommandLineArguments.Add("stopbits", new CommandLineArgument("stopbits", handler: ArgHandler_StopBits, priority: 5)); //process stop bits before any other 's' commands
            CommandLineArguments.Add("status", new CommandLineArgument(new[] { "status", "nostatus" }, handler: ArgHandler_Status, priority: 6)); // process status before ttle
            CommandLineArguments.Add("autoconnect", new CommandLineArgument("autoconnect", handler: ArgHandler_AutoConnect, priority: 7)); //process autoconnect before com
            CommandLineArguments.Add("com", new CommandLineArgument("com", handler: ArgHandler_Com, priority: 8)); // process com before any other 'c' commands
            CommandLineArguments.Add("log", new CommandLineArgument("log", handler: ArgHandler_Log, priority: 9)); // process log before any other 'l' commands
            CommandLineArguments.Add("baud", new CommandLineArgument("baud", handler: ArgHandler_Baud, priority: 10)); // process baud before any other 'b' commands
            CommandLineArguments.Add("encoding", new CommandLineArgument("encoding", handler: ArgHandler_Encoding, priority: 11)); // process encoding before any other 'e' commands
            CommandLineArguments.Add("title", new CommandLineArgument("title", handler: ArgHandler_Title, priority: 12));
            CommandLineArguments.Add("bulksend", new CommandLineArgument("bulksend", handler: ArgHandler_BulkSend));
            CommandLineArguments.Add("clearscreen", new CommandLineArgument(new[] { "clearscreen", "noclear" }, handler: ArgHandler_ClearScreen));
            CommandLineArguments.Add("config", new CommandLineArgument(new[] { "config", "input" }, handler: null));
            CommandLineArguments.Add("databits", new CommandLineArgument("databits", handler: ArgHandler_DataBits));
            CommandLineArguments.Add("echo", new CommandLineArgument("echo", handler: ArgHandler_Echo));
            CommandLineArguments.Add("exitkey", new CommandLineArgument("exitkey", handler: ArgHandler_ExitKey));
            CommandLineArguments.Add("forcenewline", new CommandLineArgument("forcenewline", handler: ArgHandler_ForceNewLine));
            CommandLineArguments.Add("logmode", new CommandLineArgument("logmode", handler: ArgHandler_LogMode));
            CommandLineArguments.Add("parity", new CommandLineArgument("parity", handler: ArgHandler_Parity));
            CommandLineArguments.Add("txonenter", new CommandLineArgument("txonenter", handler: ArgHandler_TXOnEnter));

            // Create a list of command-line arguments sorted by priority for processing
            List<CommandLineArgument> argumentsByPriority = CommandLineArguments.Values.OrderBy(a => a.Priority).ToList();

            // Parse command-line arguments and add them to the arguments list
            List<ArgumentData> arguments = ParseArguments(args);

            // Check for a user-specified configuration file and process it if specified
            ArgumentData userConfigFile = arguments.Find(item => item.Value != "" && item.Name == "config");
            if (userConfigFile != null)
            {
                userConfig = userConfigFile.Value;
                arguments.InsertRange(0, ParseArguments(LoadConfig(userConfig, failOnError: true), noImmediate: true, source: userConfig));
            }

            // Check for local and global configuration files and process them if they exist
            arguments.InsertRange(0, ParseArguments(LoadConfig($"{localConfig}", failOnError: false), noImmediate: true, source: localConfig));
            arguments.InsertRange(0, ParseArguments(LoadConfig($"{globalConfig}", failOnError: false), noImmediate: true, source: globalConfig));

            // Remove any 'config' arguments from the list of arguments to process (they've already been processed)
            arguments.RemoveAll(item => item.Name == "config");


            // Run through the list of received arguments.  Note that they were inserted into the arguments list
            // such that Global commands are processed first, followed by Local commands, then User commands, and
            // finally Command-Line arguments.  This ensures that the argument "closest" to the user is the one that
            // takes precedence.
            foreach (ArgumentData argument in arguments)
            {
                CommandLineArguments[argument.Name].RawValue = argument.Value;
                CommandLineArguments[argument.Name].SetBy = argument.Type;
                CommandLineArguments[argument.Name].Active = true;
            }

            // Process all arguments in order of priority
            foreach (CommandLineArgument argument in argumentsByPriority)
            {
                if (argument.Active)
                {
                    try
                    {
                        argument.Handle();
                    }
                    catch (Exception e)
                    {
                        ExitProgram($"{e.Message}", exitCode: -1);
                    }
                }
            } 
        }

        /// <summary>
        /// Updates the title of the console window
        /// </summary>
        /// <param name="title">New console window title</param>
        /// <param name="force">When true, forces the update even when a manual title has been set</param>
        static void UpdateTitle(string title, bool force = false)
        {
            if (force || !noStatus)
                Console.Title = title;
        }

        /// <summary>
        /// Writes messages using Console.WriteLine() as long as the 'Quiet' option hasn't been enabled
        /// </summary>
        /// <param name="message">Message to output (assuming 'Quiet' is false)</param>
        static void Output(string message, bool force = false, bool newline = true, bool flush = false)
        {
            if (!SimplySerial.Quiet || force)
            {
                if (newline)
                    message += "\n";

                if (message.Length > 0)
                {
                    if (noStatus)
                    {
                        Regex r = new Regex(@"\x1b\][02];.*\x1b\\");
                        message = r.Replace(message, string.Empty);
                    }

                    if (convertToPrintable)
                    {
                        string newMessage = "";
                        foreach (byte c in message)
                        {
                            if ((c > 31 && c < 128) || (c == 8) || (c == 9) || (c == 10) || (c == 13))
                                newMessage += (char)c;
                            else
                                newMessage += $"[{c:X2}]";
                        }
                        message = newMessage;
                    }
                    Console.Write(message);
                }

                if (logging)
                {
                    logData += message;
                    if ((logData.Length >= bufferSize) || flush)
                    {
                        try
                        {
                            FileStream stream = new FileStream(logFile, FileMode.Append, FileAccess.Write);
                            using (StreamWriter writer = new StreamWriter(stream, encoding))
                            {
                                writer.Write(logData);
                            }
                        }
                        catch
                        {
                            Console.WriteLine($"({DateTime.Now}) Error accessing log file '{logFile}'");
                        }
                        logData = string.Empty;
                    }
                }
            }
        }


        /// <summary>
        /// Displays help information about this application and its command-line arguments
        /// </summary>
        static void ShowHelp()
        {
            Console.WriteLine("Usage: ss.exe [-com:PORT] [-baud:RATE] [-parity:PARITY] [-databits:VAL]");
            Console.WriteLine("              [-stopbits:VAL] [-autoconnect:VAL] [-log:LOGFILE] [-logmode:MODE]");
            Console.WriteLine("              [-quiet]\n");
            Console.WriteLine("A basic serial terminal for IoT device programming in general, and working with");
            Console.WriteLine("CircuitPython devices specifically.  With no command-line arguments specified,");
            Console.WriteLine("SimplySerial will attempt to identify and connect to a CircuitPython-capable board");
            Console.WriteLine("at 115200 baud, no parity, 8 data bits and 1 stop bit.  If no known boards are");
            Console.WriteLine("detected, it will default to the first available serial (COM) port at 9600 baud.\n");
            Console.WriteLine("Optional arguments:");
            Console.WriteLine("  -help             Display this help message");
            Console.WriteLine("  -version          Display version and installation information");
            Console.WriteLine("  -list             Display a list of available serial (COM) ports");
            Console.WriteLine("  -updateboards     Update the list of known USB serial devices.");
            Console.WriteLine("  -com:PORT         COM port number (i.e. 1 for COM1, 22 for COM22, etc.)");
            Console.WriteLine("  -baud:RATE        1200 | 2400 | 4800 | 7200 | 9600 | 14400 | 19200 | 38400 |");
            Console.WriteLine("                    57600 | 115200 | (Any valid baud rate for the specified port.)");
            Console.WriteLine("  -parity:PARITY    NONE | EVEN | ODD | MARK | SPACE");
            Console.WriteLine("  -databits:VAL     4 | 5 | 6 | 7 | 8");
            Console.WriteLine("  -stopbits:VAL     0 | 1 | 1.5 | 2");
            Console.WriteLine("  -autoconnect:VAL  NONE| ONE | ANY, enable/disable auto-(re)connection when");
            Console.WriteLine("                    a device is disconnected / reconnected.");
            Console.WriteLine("  -echo:VAL         ON | OFF enable or disable printing typed characters locally");
            Console.WriteLine("  -log:LOGFILE      Logs all output to the specified file.");
            Console.WriteLine("  -logmode:MODE     APPEND | OVERWRITE, default is OVERWRITE");
            Console.WriteLine("  -quiet:VAL        ON | OFF when enabled, don't print any application messages/errors to console");
            Console.WriteLine("  -forcenewline:VAL ON | OFF enable/disable forcing of linefeeds (newline) in place of carriage returns in received data.");
            Console.WriteLine("  -encoding:ENC     UTF8 | ASCII | RAW");
            Console.WriteLine("  -clearscreen:VAL  ON | OFF enable/disable clearing of the terminal screen on connection.");
            Console.WriteLine("  -status:VAL       ON | OFF enable/disable status/title updates from virtual terminal sequences.");
            Console.WriteLine("  -exitkey:KEY      Specify a key to use along with CTRL for exiting the program (default is 'X').");
            Console.WriteLine("  -title:\"TITLE\"  Set the console window title.  Surround with quotation marks if your title has spaces.");
            Console.WriteLine("  -bulksend:VAL     ON | OFF enable or disable bulk send mode (send all characters typed/pasted at once).");
            Console.WriteLine("  -txonenter:VAL    CR | LF | CRLF | CUSTOM=\"CustomString\" | BYTES=\"custom sequence of bytes\", each byte must be expressed by 2 chars.");
            Console.WriteLine("                    Bytes sequence must be a hexadecimal value with or without leading 0x and separated or not by spaces.");
            Console.WriteLine("                    Determines what character(s) will be sent when the enter key is pressed.");
            Console.WriteLine("  -config:FILE      Load command-line arguments from the specified configuration file. (One command per line.)");
            Console.WriteLine($"\nPress CTRL-{exitKey} to exit a running instance of SimplySerial.\n");
        }

        /// <summary>
        /// Displays the contents of the specified configuration file (if it exists) to the console
        /// </summary>
        /// <param name="file">Full path to the configuration file</param>
        /// <param name="label">The label to apply to this set of configuration data</param>
        static void ShowArguments(string file, string label)
        {
            if (File.Exists(file))
            {
                Console.WriteLine($"{label} [{file}]:");
                foreach (string line in File.ReadLines(file))
                {
                    string lineOut = line.Trim();
                    if (lineOut.Length > 0)
                        Console.WriteLine($"  {lineOut}");
                }
                Console.WriteLine("");
            }
        }

        /// <summary>
        /// Displays version and installation information about this application
        /// </summary>
        static void ShowVersion()
        {
            string installType;

            // determine installation type (scoop/user/system/standalone)
            if (appFolder.ToLower().Contains("scoop"))
            {
                installType = "Scoop";
            }
            else if (appFolder.ToLower().Contains("appdata\\roaming"))
            {
                installType = "User";
            }
            else if (appFolder.ToLower().Contains("program files"))
            {
                installType = "System";
            }
            else
            {
                installType = "Standalone/Manual";
            }

            Console.WriteLine($"SimplySerial version {version}");
            Console.WriteLine($"  Installation Type : {installType}");
            Console.WriteLine($"  Installation Path : {appFolder}");
            Console.WriteLine($"  Board Data File   : {BoardManager.Version}\n");
            ShowArguments($"{globalConfig}", "Default Arguments");
            ShowArguments($"{localConfig}", "Local Argument Overrides");
        }


        /// <summary>
        /// Writes the specified exit message to the console, then waits for user to press a key before halting program execution.
        /// </summary>
        /// <param name="message">Message to display - should indicate the reason why the program is terminating.</param>
        /// <param name="exitCode">Code to return to parent process.  Should be &lt;0 if an error occurred, &gt;=0 if program is terminating normally.</param>
        /// <param name="silent">Exits without displaying a message or asking for a key press when set to 'true'</param>
        static void ExitProgram(string message = "", int exitCode = 0, bool silent = false)
        {
            // the serial port should be closed before exiting
            if (serialPort != null && serialPort.IsOpen)
                serialPort.Close();
            if (!silent)
                Output("\n" + message, flush: true);
            else if (logging)
                Output("", force: true, newline: false, flush: true);
#if DEBUG
            Console.WriteLine("\n>> Press any key to exit <<");
            Console.ReadKey();
#endif
            Environment.Exit(exitCode);
        }


        /// <summary>
        /// Returns a list of available serial ports with their associated PID, VID and descriptions 
        /// Modified from the example written by Kamil Górski (freakone) available at
        /// http://blog.gorski.pm/serial-port-details-in-c-sharp
        /// https://github.com/freakone/serial-reader
        /// Some modifications were based on this stackoverflow thread:
        /// https://stackoverflow.com/questions/11458835/finding-information-about-all-serial-devices-connected-through-usb-in-c-sharp
        /// Hardware Bus Description through WMI is based on Simon Mourier's answer on this stackoverflow thread:
        /// https://stackoverflow.com/questions/69362886/get-devpkey-device-busreporteddevicedesc-from-win32-pnpentity-in-c-sharp
        /// </summary>
        /// <returns>List of available serial ports</returns>
        private static List<ComPort> GetSerialPorts(bool excluded=false)
        {
            const string vidPattern = @"VID_([0-9A-F]{4})";
            const string pidPattern = @"PID_([0-9A-F]{4})";
            const string namePattern = @"(?<=\()COM[0-9]{1,3}(?=\)$)";
            const string query = "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"";

            // as per INTERFACE_PREFIXES in adafruit_board_toolkit
            // (see https://github.com/adafruit/Adafruit_Board_Toolkit/blob/main/adafruit_board_toolkit)
            string[] cpb_descriptions = new string[] { "CircuitPython CDC ", "Sol CDC ", "StringCarM0Ex CDC " };

            List<ComPort> detectedPorts = new List<ComPort>();
            List<ComPort> excludedPorts = new List<ComPort>();

            foreach (var p in new ManagementObjectSearcher("root\\CIMV2", query).Get().OfType<ManagementObject>())
            {
                ComPort c = new ComPort();

                // extract and clean up port name and number
                c.name = p.GetPropertyValue("Name").ToString();
                Match mName = Regex.Match(c.name, namePattern);
                if (mName.Success)
                {
                    c.name = mName.Value;
                    c.num = int.Parse(c.name.Substring(3));
                }

                // if the port name or number cannot be determined, skip this port and move on
                if (c.num < 1)
                    continue;

                // get the device's VID and PID
                string pidvid = p.GetPropertyValue("PNPDeviceID").ToString();

                // extract and clean up device's VID
                Match mVID = Regex.Match(pidvid, vidPattern, RegexOptions.IgnoreCase);
                if (mVID.Success)
                    c.vid = mVID.Groups[1].Value.Substring(0, Math.Min(4, c.vid.Length));

                // extract and clean up device's PID
                Match mPID = Regex.Match(pidvid, pidPattern, RegexOptions.IgnoreCase);
                if (mPID.Success)
                    c.pid = mPID.Groups[1].Value.Substring(0, Math.Min(4, c.pid.Length));

                // extract the device's friendly description (caption)
                c.description = p.GetPropertyValue("Caption").ToString();

                // attempt to match this device with a known board
                c.board = BoardManager.Match(c.vid, c.pid);

                // extract the device's hardware bus description
                c.busDescription = "";
                var inParams = new object[] { new string[] { "DEVPKEY_Device_BusReportedDeviceDesc" }, null };
                p.InvokeMethod("GetDeviceProperties", inParams);
                var outParams = (ManagementBaseObject[])inParams[1];
                if (outParams.Length > 0)
                {
                    var data = outParams[0].Properties.OfType<PropertyData>().FirstOrDefault(d => d.Name == "Data");
                    if (data != null)
                    {
                        c.busDescription = data.Value.ToString();
                    }
                }

                // we can determine if this is a CircuitPython board by its bus description
                foreach (string prefix in cpb_descriptions)
                {
                    if (c.busDescription.StartsWith(prefix))
                        c.isCircuitPython = true;
                }

                // apply filters to determine if this port should be included or excluded in autodetection

                // if there are *any* include filters than we can *only* include matches, and anything that doesn't match gets excluded

                // if there are *no* include filters, then we start out including everything

                // once we have our initial include list, we apply our exclude filters to remove any ports that match and add them to the exclude list

                // ORIGINAL CODE BELOW:

                // add this port to our list of detected ports
                detectedPorts.Add(c);
            }

            return (excluded == false) ? detectedPorts.Distinct().ToList() : excludedPorts.Distinct().ToList();
        }
    }
}


