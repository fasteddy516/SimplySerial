using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Ports;
using System.Threading;

namespace SimplySerial
{
    class SimplySerial
    {
        static SerialPort serialPort;

        // default comspec values and application settings set here will be overridden by values passed through command-line arguments
        static readonly string version = "0.1.0";
        static bool Quiet = false;
        static bool NoWait = false;
        static string port = string.Empty;
        static int baud = 9600;
        static Parity parity = Parity.None;
        static int dataBits = 8;
        static StopBits stopBits = StopBits.One;

        static void Main(string[] args)
        {
            // process all command-line arguments
            ProcessArguments(args);

            // set up the serial port
            serialPort = new SerialPort(port, baud, parity, dataBits, stopBits)
            {
                Handshake = Handshake.None, // we don't need to support any handshaking at this point 
                ReadTimeout = 1, // minimal timeout - we don't want to wait forever for data that may not be coming!
                WriteTimeout = 250, // small delay - if we go too small on this it causes System.IO semaphore timeout exceptions
                DtrEnable = true, // without this we don't ever receive any data
                RtsEnable = true // without this we don't ever receive any data
            };
            string received = string.Empty; // this is where data read from the serial port will be temporarily stored

            // attempt to open the serial port, terminate on error
            try
            {
                serialPort.Open();
            }
            catch (System.UnauthorizedAccessException uae)
            {
                ExitProgram((uae.GetType() + " occurred while attempting to open the serial port.  Is this serial port already in use in another application?"), exitCode: -1);
            }
            catch (Exception e)
            {
                ExitProgram((e.GetType() + " occurred while attempting to open the serial port."), exitCode: -1);
            }

            // set up keyboard input for relay to serial port
            ConsoleKeyInfo keyInfo = new ConsoleKeyInfo();
            Console.TreatControlCAsInput = true; // we need to use CTRL-C to activate the REPL in CircuitPython, so it can't be used to exit the application

            // if we get this far, clear the screen and send the connection message if not in 'quiet' mode
            Console.Clear();
            if (!SimplySerial.Quiet)
            {
                Console.WriteLine("SimplySerial v{0} connected at {1} baud, {2} parity, {3} data bits, {4} stop bit{5}.  Use CTRL-X to exit.\n",
                    version,
                    baud,
                    (parity == Parity.None) ? "no" : (parity.ToString()).ToLower(),
                    dataBits,
                    (stopBits == StopBits.None) ? "0" : (stopBits == StopBits.One) ? "1" : (stopBits == StopBits.OnePointFive) ? "1.5" : "2",
                    (stopBits == StopBits.One) ? "" : "s"
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
                           serialPort.Close();
                           ExitProgram("\nSession terminated by user via CTRL-X.", exitCode: 0);
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
                    ExitProgram((e.GetType() + " occurred while attempting to read/write to/from the serial port."), exitCode: -1);
                }
            }

            // the serial port should be closed by now, but we'll make sure it is anyway
            if (serialPort.IsOpen)
                serialPort.Close();

            // the program should have ended gracefully before now - there is no good reason for us to be here!
            ExitProgram("\nSession terminated unexpectedly.", exitCode: -1);
        }


        /// <summary>
        /// Validates and processes any command-line arguments that were passed in.  Invalid arguments will halt program execution.
        /// </summary>
        /// <param name="args">Command-line parameters</param>
        static void ProcessArguments(string[] args)
        {
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
                    ExitProgram(("Invalid or incomplete argument <" + arg + ">\nTry 'SimplySerial help' to see a list of valid arguments"), exitCode: -1);
                }

                // preliminary validate on com port, final validation occurs towards the end of this method 
                else if (argument[0].StartsWith("c"))
                {
                    string newPort = argument[1].ToUpper();

                    if (!argument[1].StartsWith("COM"))
                        newPort = "COM" + argument[1];
                    port = newPort;
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
                else
                {
                    ExitProgram(("Invalid or incomplete argument <" + arg + ">\nTry 'SimplySerial help' to see a list of valid arguments"), exitCode: -1);
                }
            }

            // get a list of all available com ports
            string[] availablePorts = SerialPort.GetPortNames();

            // if no port was specified, default to the first/only available com port, exit with error if no ports are available
            if (port == String.Empty)
            {
                if (availablePorts.Count() >= 1)
                    SimplySerial.port = availablePorts[0];
                else
                    ExitProgram("No COM ports detected.", exitCode: -1);
            }
            else if (!availablePorts.Contains(port))
                ExitProgram(("Invalid port specified <" + port + ">"), exitCode: -1);

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
            Console.WriteLine("SimplySerial will attempt to connect to the first available serial (COM) port at");
            Console.WriteLine("9600 baud, no parity, 8 data bits and 1 stop bit.\n");
            Console.WriteLine("Optional arguments:");
            Console.WriteLine("  -help             Display this help message");
            Console.WriteLine("  -com:PORT         COM port number (i.e. 1 for COM1, 22 for COM22, etc.)");
            Console.WriteLine("  -baud:RATE        1200 | 2400 | 4800 | 7200 | 9600 | 14400 | 19200 | 38400 |");
            Console.WriteLine("                    57600 | 115200");
            Console.WriteLine("  -parity:PARITY    NONE | EVEN | ODD | MARK | SPACE");
            Console.WriteLine("  -databits:VALUE   4 | 5 |  | 7 | 8");
            Console.WriteLine("  -stopbits:VALUE   0 | 1 | 1.5 | 2");
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
            // 'q' enables the 'quiet' option, which needs to be enabled before something that would normally generate console output
            // 'n' enables the 'nowait' option, which needs to be enabled before anything that would trigger an artificial delay 
            foreach (char c in "?hqn")
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
