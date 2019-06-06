using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Ports;
using System.Threading;
using System.Management;
using System.Text.RegularExpressions;

namespace SimplySerial
{
    class SimplySerial
    {
        enum AutoConnect { NONE, ONE, ANY };

        static SerialPort serialPort;

        // default comspec values and application settings set here will be overridden by values passed through command-line arguments
        static readonly string version = "0.2.0";
        static bool Quiet = false;
        static bool NoWait = false;
        static AutoConnect autoConnect = AutoConnect.ONE;
        static ComPort port;
        static int baud = 9600;
        static Parity parity = Parity.None;
        static int dataBits = 8;
        static StopBits stopBits = StopBits.One;

        static void Main(string[] args)
        {
            // process all command-line arguments
            ProcessArguments(args);

            // set up keyboard input for program control and relay to serial port
            ConsoleKeyInfo keyInfo = new ConsoleKeyInfo();
            Console.TreatControlCAsInput = true; // we need to use CTRL-C to activate the REPL in CircuitPython, so it can't be used to exit the application

            // this is where data read from the serial port will be temporarily stored
            string received = string.Empty;

            //main loop - keep this up until instructed otherwise
            do
            {
                // exit the program if CTRL-X was pressed
                if (Console.KeyAvailable)
                {
                    keyInfo = Console.ReadKey(intercept: true);
                    if ((keyInfo.Key == ConsoleKey.X) && (keyInfo.Modifiers == ConsoleModifiers.Control))
                    {
                        Output("\n<<< SimplySerial session terminated via CTRL-X >>>");
                        ExitProgram(silent: true);
                    }
                }

                // set up the serial port
                serialPort = new SerialPort(port.name, baud, parity, dataBits, stopBits)
                {
                    Handshake = Handshake.None, // we don't need to support any handshaking at this point 
                    ReadTimeout = 1, // minimal timeout - we don't want to wait forever for data that may not be coming!
                    WriteTimeout = 250, // small delay - if we go too small on this it causes System.IO semaphore timeout exceptions
                    DtrEnable = true, // without this we don't ever receive any data
                    RtsEnable = true // without this we don't ever receive any data
                };

                // attempt to open the serial port, terminate on error
                try
                {
                    serialPort.Open();
                }
                catch (System.UnauthorizedAccessException uae)
                {
                    Output(uae.GetType() + " occurred while attempting to open " + port.name + ".  Is this port already in use in another application?");
                    serialPort.Dispose();
                    Thread.Sleep(1000);
                    continue;
                }
                catch (Exception e)
                {
                    Output(e.GetType() + " occurred while attempting to open " + port.name + ".");
                    serialPort.Dispose();
                    Thread.Sleep(1000);
                    continue;
                }

                // if we get this far, clear the screen and send the connection message if not in 'quiet' mode
                Console.Clear();
                if (!SimplySerial.Quiet)
                {
                    Console.WriteLine(("<<< SimplySerial v{0} connected via {1} >>>\n" +
                                      "Settings  : {2} baud, {3} parity, {4} data bits, {5} stop bit{6}, auto-connect {7}.\n" +
                                      "Device    : {8} ({9}) {10} ({11}){12}\n" +
                                      "---\n\nUse CTRL-X to exit.\n"),
                        version,
                        port.name,
                        baud,
                        (parity == Parity.None) ? "no" : (parity.ToString()).ToLower(),
                        dataBits,
                        (stopBits == StopBits.None) ? "0" : (stopBits == StopBits.One) ? "1" : (stopBits == StopBits.OnePointFive) ? "1.5" : "2", (stopBits == StopBits.One) ? "" : "s",
                        (autoConnect == AutoConnect.ONE) ? "on" : (autoConnect == AutoConnect.ANY) ? "any" : "off",
                        port.board.make,
                        port.vid,
                        port.board.model,
                        port.pid,
                        (port.board.isCircuitPython) ? " + CircuitPython" : ""
                    );
                }

                // this is the core functionality - loop while the serial port is open
                while (serialPort.IsOpen)
                {
                    try
                    {
                        // process keypresses for transmission through the serial port
                        if (Console.KeyAvailable)
                        {
                            // determine what key is pressed (including modifiers)
                            keyInfo = Console.ReadKey(intercept: true);

                            // exit the program if CTRL-X was pressed
                            if ((keyInfo.Key == ConsoleKey.X) && (keyInfo.Modifiers == ConsoleModifiers.Control))
                            {
                                Output("\n<<< SimplySerial session terminated via CTRL-X >>>");
                                ExitProgram(silent: true);
                            }

                            // properly process the backspace character
                            else if (keyInfo.Key == ConsoleKey.Backspace)
                            {
                                serialPort.Write("\b");
                                Thread.Sleep(150); // sort of cheating here - by adding this delay we ensure that when we process the receive buffer it will contain the correct backspace control sequence
                            }

                            // everything else just gets sent right on through
                            else
                                serialPort.Write(Convert.ToString(keyInfo.KeyChar));
                        }

                        // process data coming in from the serial port
                        received = serialPort.ReadExisting();

                        // if anything was received, process it
                        if (received.Length > 0)
                        {
                            // properly process backspace
                            if (received == ("\b\x1B[K"))
                                received = "\b \b";

                            // write what was received to console
                            Console.Write(received);
                        }
                    }
                    catch (Exception e)
                    {
                        Output(e.GetType() + " occurred while attempting to read/write to/from " + port.name + ".");
                        serialPort.Dispose();
                        Thread.Sleep(1000);
                        continue;
                    }
                }
            } while (autoConnect > AutoConnect.NONE);

            // the program should have ended gracefully before now - there is no good reason for us to be here!
            ExitProgram("<<< SimplySerial session terminated >>>", exitCode: -1);
        }


        /// <summary>
        /// Validates and processes any command-line arguments that were passed in.  Invalid arguments will halt program execution.
        /// </summary>
        /// <param name="args">Command-line parameters</param>
        static void ProcessArguments(string[] args)
        {
            // get a list of all available ports
            List<ComPort> availablePorts = (SimplySerial.GetSerialPorts()).OrderBy(p => p.num).ToList();
            //List<ComPort> availablePorts = SimplySerial.GetSerialPorts();
            //availablePorts = (availablePorts.OrderBy(p => p.board.pid)).ToList();

            // set default port information
            port.name = String.Empty;

            // switch to lower case and remove '/', '--' and '-' from beginning of arguments - we can process correctly without them
            for (int i = 0; i < args.Count(); i++)
                args[i] = (args[i].TrimStart('/', '-')).ToLower();

            // sort the parameters so that they get processed in order of priority (i.e. 'quiet' option gets processed before something that would normally result in console output, etc.)
            Array.Sort(args, new ArgumentSorter());

            // iterate through command-line arguments
            foreach (string arg in args)
            {
                // split argument into components based on 'key:value' formatting             
                string[] argument = arg.Split(':');

                // help
                if (argument[0].StartsWith("h") || argument[0].StartsWith("?"))
                {
                    ShowHelp();
                    ExitProgram(silent: true);
                }

                // list available ports
                else if (argument[0].StartsWith("l"))
                {
                    if (availablePorts.Count >= 1)
                    {
                        Console.WriteLine("\nPORT\tVID\tPID\tDESCRIPTION");
                        Console.WriteLine("------------------------------------------------------------");
                        foreach (ComPort p in availablePorts)
                        {
                            Console.WriteLine("{0}\t{1}\t{2}\t{3}",
                                p.name,
                                p.vid,
                                p.pid,
                                (p.board.isCircuitPython) ? (p.board.make + " " + p.board.model) : p.description
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

                // quiet (no output to console other than comes in via serial)
                else if (argument[0].StartsWith("q"))
                {
                    SimplySerial.Quiet = true;
                }

                // nowait (disables the "press any key to exit" function)
                else if (argument[0].StartsWith("n"))
                {
                    SimplySerial.NoWait = true;
                }

                // the remainder of possible command-line arguments require two parameters, so let's enforce that now
                else if (argument.Count() < 2)
                {
                    ExitProgram(("Invalid or incomplete argument <" + arg + ">\nTry 'ss.exe help' to see a list of valid arguments"), exitCode: -1);
                }

                // preliminary validate on com port, final validation occurs towards the end of this method 
                else if (argument[0].StartsWith("c"))
                {
                    string newPort = argument[1].ToUpper();

                    if (!argument[1].StartsWith("COM"))
                        newPort = "COM" + argument[1];
                    port.name = newPort;
                }

                // validate baud rate, terminate on error
                else if (argument[0].StartsWith("b"))
                {
                    // these are the baud rates we're supporting for now
                    string[] availableBaudRates = new string[] { "1200", "2400", "4800", "7200", "9600", "14400", "19200", "38400", "57600", "115200" };

                    if (availableBaudRates.Contains(argument[1]))
                        baud = Convert.ToInt32(argument[1]);
                    else
                        ExitProgram(("Invalid baud rate specified <" + argument[1] + ">"), exitCode: -1);
                }

                // validate parity, terminate on error
                else if (argument[0].StartsWith("p"))
                {
                    if (argument[1].StartsWith("e"))
                        parity = Parity.Even;
                    else if (argument[1].StartsWith("m"))
                        parity = Parity.Mark;
                    else if (argument[1].StartsWith("n"))
                        parity = Parity.None;
                    else if (argument[1].StartsWith("o"))
                        parity = Parity.Odd;
                    else if (argument[1].StartsWith("s"))
                        parity = Parity.Space;
                    else
                        ExitProgram(("Invalid parity specified <" + argument[1] + ">"), exitCode: -1);
                }

                // validate databits, terminate on error
                else if (argument[0].StartsWith("d"))
                {
                    int newDataBits = Convert.ToInt32(argument[1]);

                    if ((newDataBits > 3) && (newDataBits < 9))
                        dataBits = newDataBits;
                    else
                        ExitProgram(("Invalid data bits specified <" + argument[1] + ">"), exitCode: -1);
                }

                // validate stopbits, terminate on error
                else if (argument[0].StartsWith("s"))
                {
                    if (argument[1] == "0")
                        stopBits = StopBits.None;
                    else if (argument[1] == "1")
                        stopBits = StopBits.One;
                    else if (argument[1] == "1.5")
                        stopBits = StopBits.OnePointFive;
                    else if (argument[1] == "2")
                        stopBits = StopBits.Two;
                    else
                        ExitProgram(("Invalid stop bits specified <" + argument[1] + ">"), exitCode: -1);
                }

                // validate auto connect, terminate on error
                else if (argument[0].StartsWith("a"))
                {
                    if (argument[1].StartsWith("n"))
                        autoConnect = AutoConnect.NONE;
                    else if (argument[1].StartsWith("o"))
                        autoConnect = AutoConnect.ONE;
                    else if (argument[1].StartsWith("a"))
                        autoConnect = AutoConnect.ANY;
                    else
                        ExitProgram(("Invalid auto connect setting specified <" + argument[1] + ">"), exitCode: -1);
                }

                // an invalid/incomplete argument was passed
                else
                {
                    ExitProgram(("Invalid or incomplete argument <" + arg + ">\nTry 'ss.exe -help' to see a list of valid arguments"), exitCode: -1);
                }
            }

            // if no port was specified, default to the first/only available com port, exit with error if no ports are available
            if (port.name == String.Empty)
            {
                if (availablePorts.Count() >= 1)
                {
                    // first, try to default to something that we assume is running CircuitPython
                    SimplySerial.port = availablePorts.Find(p => p.board.isCircuitPython == true);

                    // if that doesn't work out, just default to the first available COM port
                    if (SimplySerial.port.name == null)
                        SimplySerial.port = availablePorts[0];
                }
                else
                    ExitProgram("No COM ports detected.", exitCode: -1);
            }

            // if a port name was specified, try to match it with one that actually exists and fail if it doesn't
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
                if (!portMatched)
                    ExitProgram(("Invalid port specified <" + port.name + ">"), exitCode: -1);
            }

            // if we made it this far, everything has been processed and we're ready to proceed!
        }


        /// <summary>
        /// Writes messages using Console.WriteLine() as long as the 'Quiet' option hasn't been enabled
        /// </summary>
        /// <param name="message">Message to output (assuming 'Quiet' is false)</param>
        static void Output(string message)
        {
            if (!SimplySerial.Quiet)
                Console.WriteLine(message);
        }


        /// <summary>
        /// Displays help information about this application and its command-line arguments
        /// </summary>
        static void ShowHelp()
        {
            Console.WriteLine("\nUsage: ss.exe [-help] [-com:PORT] [-baud:RATE] [-parity:PARITY] [-databits:VALUE]");
            Console.WriteLine("              [-stopbits:VALUE][-quiet][-nowait]\n");
            Console.WriteLine("Barebones serial terminal for IoT device programming in general, and working with");
            Console.WriteLine("CircuitPython devices specifically!  With no command-line arguments specified,");
            Console.WriteLine("SimplySerial will attempt to identify and connect to a CircuitPython-capable board");
            Console.WriteLine("at 9600 baud, no parity, 8 data bits and 1 stop bit.  If no known boards are");
            Console.WriteLine("detected, it will default to the first available serial (COM) port.\n");
            Console.WriteLine("Optional arguments:");
            Console.WriteLine("  -help             Display this help message");
            Console.WriteLine("  -list             Display a list of available serial (COM) ports");
            Console.WriteLine("  -com:PORT         COM port number (i.e. 1 for COM1, 22 for COM22, etc.)");
            Console.WriteLine("  -baud:RATE        1200 | 2400 | 4800 | 7200 | 9600 | 14400 | 19200 | 38400 |");
            Console.WriteLine("                    57600 | 115200");
            Console.WriteLine("  -parity:PARITY    NONE | EVEN | ODD | MARK | SPACE");
            Console.WriteLine("  -databits:VAL     4 | 5 |  | 7 | 8");
            Console.WriteLine("  -stopbits:VAL     0 | 1 | 1.5 | 2");
            Console.WriteLine("  -autoconnect:VAL  NONE| ONE | ANY, enable/disable auto-(re)connection when");
            Console.WriteLine("                    a device is disconnected / reconnected.");
            Console.WriteLine("  -quiet            don't print any application messages/errors to console");
            Console.WriteLine("  -nowait           don't wait for user input (i.e. 'press any key to exit')\n");
            Console.WriteLine(" Press CTRL-X to exit a running instance of SimplySerial.");
        }


        /// <summary>
        /// Writes the specified exit message to the console, then waits for user to press a key before halting program execution.
        /// </summary>
        /// <param name="message">Message to display - should indicate the reason why the program is terminating.</param>
        /// <param name="exitCode">Code to return to parent process.  Should be &lt;0 if an error occurred, &gt;=0 if program is terminating normally.</param>
        /// <param name="silent">Exits without displaying a message or asking for a key press when set to 'true'</param>
        static void ExitProgram(string message="", int exitCode=0, bool silent=false)
        {
            // the serial port should be closed before exiting
            if (serialPort != null && serialPort.IsOpen)
                serialPort.Close();

            if (!silent)
                Output("\n" + message);

            if (!(SimplySerial.NoWait || silent))
            {
                // we output this line regardless of the 'quiet' option to make it clear that we're waiting for user input
                Console.WriteLine("\nPress any key to exit...");
                while (!Console.KeyAvailable)
                    Thread.Sleep(25);
            }
            Environment.Exit(exitCode);
        }


        /// <summary>
        /// Custom structure containing the name, VID, PID and description of a serial (COM) port
        /// Modified from the example written by Kamil Górski (freakone) available at
        /// http://blog.gorski.pm/serial-port-details-in-c-sharp
        /// https://github.com/freakone/serial-reader
        /// </summary>
        struct ComPort // custom struct with our desired values
        {
            public string name;
            public int num;
            public string vid;
            public string pid;
            public string description;
            public Board board;
        }


        /// <summary>
        /// Returns a list of available serial ports with their associated PID, VID and descriptions 
        /// Modified from the example written by Kamil Górski (freakone) available at
        /// http://blog.gorski.pm/serial-port-details-in-c-sharp
        /// https://github.com/freakone/serial-reader
        /// Some modifications were based on this stackoverflow thread:
        /// https://stackoverflow.com/questions/11458835/finding-information-about-all-serial-devices-connected-through-usb-in-c-sharp
        /// </summary>
        /// <returns>List of available serial ports</returns>
        private static List<ComPort> GetSerialPorts()
        {
            const string vidPattern = @"VID_([0-9A-F]{4})";
            const string pidPattern = @"PID_([0-9A-F]{4})";
            const string namePattern = @"(?<=\()COM[0-9]{1,3}(?=\)$)";

            using (var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\""))
            {
                var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
                return ports.Select(p =>
                {
                    ComPort c = new ComPort();
                    c.name = p.GetPropertyValue("Name").ToString();
                    c.vid = p.GetPropertyValue("PNPDeviceID").ToString();
                    c.description = p.GetPropertyValue("Caption").ToString();

                    Match mVID = Regex.Match(c.vid, vidPattern, RegexOptions.IgnoreCase);
                    Match mPID = Regex.Match(c.vid, pidPattern, RegexOptions.IgnoreCase);

                    if (mVID.Success)
                        c.vid = mVID.Groups[1].Value;
                    if (mPID.Success)
                        c.pid = mPID.Groups[1].Value;

                    c.board = MatchBoard(c.vid, c.pid);

                    Match mName = Regex.Match(c.name, namePattern);
                    if (mName.Success)
                    {
                        c.name = mName.Value;
                        c.num = int.Parse(c.name.Substring(3));
                    }
                    else
                    {
                        c.name = "COM??";
                        c.num = 0;
                    }

                    return c;

                }).ToList();
            }
        }


        /// <summary>
        /// Custom structure containing information about supported CircuitPython boards
        /// </summary>
        struct Board
        {
            public string pid;
            public string make;
            public string model;
            public bool isCircuitPython;
        }


        /// <summary>
        /// Custom structure containing information about CircuitPython board vendors
        /// </summary>
        struct Vendor
        {
            public string vid;
            public string vendor;
            public bool isCircuitPython;
            public List<Board> boards;
        }

        /// <summary>
        /// Master list of all of the CircuitPython boards we know about
        /// </summary>
        static readonly List<Vendor> vendors = new List<Vendor>()
        {
            new Vendor()
            {
                vid = "04D8",
                vendor = "Various",
                isCircuitPython = true,
                boards = new List<Board>()
                {
                    new Board() { pid = "ED5F" , make = "Itaca Innovation", model = "uChip M0", isCircuitPython = true },
                    new Board() { pid = "ED94" , make = "Max Holliday", model = "KickSat Sprite", isCircuitPython = true },
                    new Board() { pid = "EDB3" , make = "Capable Robot Components", model = "Programable USB Hub", isCircuitPython = true },
                    new Board() { pid = "EDBE" , make = "Max Holliday", model = "SAM32", isCircuitPython = true }
                }
            },
            new Vendor()
            {
                vid = "1209",
                vendor = "Various",
                isCircuitPython = true,
                boards = new List<Board>()
                {
                    new Board() { pid = "2017" , make = "Benjamin Shockley", model = "Mini SAM M4", isCircuitPython = true },
                    new Board() { pid = "4D43" , make = "Robotics Masters", model = "Robo HAT MM1", isCircuitPython = true },
                    new Board() { pid = "BAB1" , make = "Electronic Cats", model = "Meow Meow", isCircuitPython = true },
                    new Board() { pid = "BAB2" , make = "Electronic Cats", model = "CatWAN USB Stick", isCircuitPython = true },
                    new Board() { pid = "BAB3" , make = "Electronic Cats", model = "Bast Pro Mini M0", isCircuitPython = true },
                    new Board() { pid = "BAB6" , make = "Electronic Cats", model = "Escornabot Makech", isCircuitPython = true }
                }
            },
            new Vendor()
            {
                vid = "1B4F",
                vendor = "Sparkfun",
                isCircuitPython = true,
                boards = new List<Board>()
                {
                    new Board() { pid = "0015" , make = "", model = "RedBoard Turbo", isCircuitPython = true },
                    new Board() { pid = "0017" , make = "", model = "LumiDrive", isCircuitPython = true },
                    new Board() { pid = "5289" , make = "", model = "NRF52840 Mini", isCircuitPython = true },
                    new Board() { pid = "8D22" , make = "", model = "SAMD21 Mini", isCircuitPython = true },
                    new Board() { pid = "8D23" , make = "", model = "SAMD21 Dev", isCircuitPython = true },
                }
            },
            new Vendor()
            {
                vid = "2341",
                vendor = "Arduino",
                isCircuitPython = true,
                boards = new List<Board>()
                {
                    new Board() { pid = "8053" , make = "", model = "MKR WAN 1300", isCircuitPython = true },
                    new Board() { pid = "824D" , make = "", model = "Zero", isCircuitPython = true },
                }
            },
            new Vendor()
            {
                vid = "239A",
                vendor = "Adafruit",
                isCircuitPython = true,
                boards = new List<Board>()
                {
                    new Board() { pid = "8012" , make = "", model = "ItsyBitsy M0 Express", isCircuitPython = true },
                    new Board() { pid = "8014" , make = "", model = "Metro M0 Express", isCircuitPython = true },
                    new Board() { pid = "8015" , make = "", model = "Feather M0 Family", isCircuitPython = true },
                    new Board() { pid = "8019" , make = "", model = "Circuit Playground Express", isCircuitPython = true },
                    new Board() { pid = "801D" , make = "", model = "Gemma M0", isCircuitPython = true },
                    new Board() { pid = "801F" , make = "", model = "Trinket M0", isCircuitPython = true },
                    new Board() { pid = "8021" , make = "", model = "Metro M4 Express", isCircuitPython = true },
                    new Board() { pid = "8023" , make = "", model = "Feather M0 Express Family", isCircuitPython = true },
                    new Board() { pid = "8026" , make = "", model = "Feather M4 Express", isCircuitPython = true },
                    new Board() { pid = "8028" , make = "", model = "pIRkey", isCircuitPython = true },
                    new Board() { pid = "802A" , make = "Nordic Semiconductor", model = "NRF52840 Family", isCircuitPython = true },
                    new Board() { pid = "802C" , make = "", model = "ItsyBitsy M4 Express", isCircuitPython = true },
                    new Board() { pid = "8030" , make = "", model = "NeoTrellis M4", isCircuitPython = true },
                    new Board() { pid = "8032" , make = "", model = "Grand Central M4 Express", isCircuitPython = true },
                    new Board() { pid = "8034" , make = "", model = "PyBadge", isCircuitPython = true },
                    new Board() { pid = "8036" , make = "", model = "PyPortal", isCircuitPython = true },
                    new Board() { pid = "8038" , make = "", model = "Metro M4 AirLift Lite", isCircuitPython = true },
                    new Board() { pid = "803C" , make = "Electronut Labs", model = "Papyr", isCircuitPython = true },
                    new Board() { pid = "803E" , make = "", model = "PyGamer", isCircuitPython = true },
                    new Board() { pid = "8050" , make = "Arduino", model = "MKR Zero", isCircuitPython = true },
                    new Board() { pid = "D1ED" , make = "", model = "HalloWing M0 Express", isCircuitPython = true },
                }
            },
            new Vendor()
            {
                vid = "2B04",
                vendor = "Particle",
                isCircuitPython = true,
                boards = new List<Board>()
                {
                    new Board() { pid = "c00c" , make = "", model = "Argon", isCircuitPython = true },
                    new Board() { pid = "c00d" , make = "", model = "Boron", isCircuitPython = true },
                    new Board() { pid = "c00e" , make = "", model = "Xenon", isCircuitPython = true },
                }
            },
            new Vendor()
            {
                vid = "4097",
                vendor = "Datalore",
                isCircuitPython = true,
                boards = new List<Board>()
                {
                    new Board() { pid = "0001" , make = "", model = "IP M4", isCircuitPython = true },
                }
            }
        };


        /// <summary>
        /// Searches for a CircuitPython Vendor/Board match based on VID and PID
        /// </summary>
        /// <param name="vid">VID of board</param>
        /// <param name="pid">PID of board</param>
        /// <returns>Board structure containing information about the matched board, if any</returns>
        static Board MatchBoard(string vid, string pid)
        {
            Board mBoard = new Board();

            // search for matching vendor
            Vendor mVendor = vendors.Find(v => v.vid == vid);

            // if a matching vendor is found, look for a matching board
            if (mVendor.vid != null)
            {
                mBoard = mVendor.boards.Find(b => b.pid == pid);

                // if the board's 'make' field is blank, use the vendor name instead
                if (mBoard.make == "")
                    mBoard.make = mVendor.vendor;
            }

            // if no matching vendor is found we will return generic information and assume CircuitPython is not running
            else
            {
                mBoard.pid = pid;
                mBoard.make = "VID";
                mBoard.model = "PID";
                mBoard.isCircuitPython = false;
            }

            // if a vendor was matched but not the board, fill in gaps with generic/assumed information based on vendor
            if (mBoard.pid == null)
            {
                mBoard.pid = pid;
                mBoard.make = mVendor.vendor;
                mBoard.model = "Unknown Model";
                mBoard.isCircuitPython = mVendor.isCircuitPython;
            }

            return mBoard;
        }
    }

 
    /// <summary>
    /// Custom string array sorting logic for SimplySerial command-line arguments
    /// </summary>
    public class ArgumentSorter : IComparer<string>
    {
        /// <summary>
        /// Checks the first letter/character of incoming strings for a high-priority letter/character and sorts accordingly
        /// </summary>
        /// <param name="x">string to compare</param>
        /// <param name="y">string to compare</param>
        /// <returns>-1 if a priority character is found in string 'x', 1 if a priority character is found in 'y', 0 if neither string has a priority character</returns>
        public int Compare(string x, string y)
        {
            // '?' or 'h' trigger the 'help' text output and supersede all other command-line arguments
            // 'l' triggers the 'list available ports' output and supersedes all other command-line arguments aside from 'help'
            // 'q' enables the 'quiet' option, which needs to be enabled before something that would normally generate console output
            // 'n' enables the 'nowait' option, which needs to be enabled before anything that would trigger an artificial delay 
            foreach (char c in "?hlqn")
            {
                if (x[0] == c)
                    return (-1);
                else if (y[0] == c)
                    return (1);
            }

            // treat everything else equally, as processing order doesn't matter
            return (0);
        }
    }
}
