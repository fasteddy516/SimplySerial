using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;

namespace SimplySerial
{
    class SimplySerial
    {
        static bool _continue = true;
        static SerialPort _serialPort;

        static bool Quiet = false;
        static bool NoWait = false;
        
        // default comspec values set here will be overridden by values passed through command-line arguments
        static string port = string.Empty;
        static int baud = 9600;
        static Parity parity = Parity.None;
        static int dataBits = 8;
        static StopBits stopBits = StopBits.One;

        static void Main(string[] args)
        {
            ProcessArguments(args);

            ConsoleKeyInfo cki = new ConsoleKeyInfo();
            Console.TreatControlCAsInput = true;


            _serialPort = new SerialPort("COM15", 9600, Parity.None, 8, StopBits.One);
            _serialPort.Handshake = Handshake.None;
            _serialPort.ReadTimeout = 1; //Timeout.Infinite;
            _serialPort.WriteTimeout = 250; //Timeout.Infinite;
            _serialPort.DtrEnable = true;
            _serialPort.RtsEnable = true;

            byte[] buffer = new byte[_serialPort.ReadBufferSize];
            string rx = string.Empty;
            int bytesRead = 0;


            _serialPort.Open();
            //while (!_serialPort.IsOpen)
               //Thread.Sleep(25);

            while (_continue)
            {
                if (Console.KeyAvailable)
                {
                    cki = Console.ReadKey(true);
                    _serialPort.Write(Convert.ToString(cki.KeyChar));
                }

                try
                {
                    //bytesRead = _serialPort.Read(buffer, 0, buffer.Length);
                    rx = _serialPort.ReadExisting();

                }
                catch (TimeoutException)
                {

                }
                //if (bytesRead > 0)
                if (rx.Length > 0)
                {
                    //rx = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    Console.Write(rx);
                }
            }
            _serialPort.Close();

        }


        /// <summary>
        /// Validates and processes any command-line arguments that were passed in.  Invalid arguments will halt program execution.
        /// </summary>
        /// <param name="args">Command-line parameters</param>
        static void ProcessArguments(string[] args)
        {
            // get a list of all available com ports
            string[] availablePorts = SerialPort.GetPortNames();

            // switch to lower case and remove '/', '--' and '-' from beginning of arguments - we can process correctly without them
            for (int i = 0; i < args.Count(); i++)
                args[i] = (args[i].TrimStart('/', '-')).ToLower();

            // sort the parameters so that they get processed in order of priority (i.e. 'quiet' option gets processed before something that would normally result in console output)
            Array.Sort(args, new ArgumentSorter());

            // iterate through command-line arguments
            foreach (string arg in args)
            {
                // split argument into components based on 'key:value' formatting             
                string[] argument = arg.Split(':');

                // help
                if (argument[0].StartsWith("h") || argument[0].StartsWith("?"))
                {
                    Console.WriteLine("Sorry, you're on your own.");
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

                // com port 
                else if (argument[0].StartsWith("c"))
                {
                    string newPort = argument[1].ToUpper();

                    if (!argument[1].StartsWith("COM"))
                        newPort = "COM" + argument[1];
                    if (!availablePorts.Contains(newPort))
                        ExitProgram(("Cannot find specified port <" + newPort + ">"), exitCode: -1);
                    port = newPort;
                    Output("PORT: " + port);
                }

                // baud rate
                else if (argument[0].StartsWith("b"))
                {
                    //validate requested baud rate
                    Output("BAUD: " + argument[1]);
                }

                // parity
                else if (argument[0].StartsWith("p"))
                {
                    //validate requested COM port
                    Output("PARITY: " + argument[1]);
                }

                // databits
                else if (argument[0].StartsWith("d"))
                {
                    //validate requested data bits
                    Output("DATABITS: " + argument[1]);
                }

                // stopbits
                else if (argument[0].StartsWith("s"))
                {
                    //validate requested stop bits
                    Output("STOPBITS: " + argument[1]);
                }
                else
                {
                    ExitProgram(("Invalid or incomplete argument <" + arg + ">\nTry 'SimplySerial help' to see a list of valid arguments"), exitCode: -1);
                }
            }

            // default to the first/only available com port, exit with error if no ports are available
            if (availablePorts.Count() >= 1)
                SimplySerial.port = availablePorts[0];
            else
                ExitProgram("No COM ports detected.", exitCode: -1);

            ExitProgram("That's All Folks!", exitCode: 1);

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
        /// Writes the specified exit message to the console, then waits for user to press a key before halting program execution.
        /// </summary>
        /// <param name="message">Message to display - should indicate the reason why the program is terminating.</param>
        /// <param name="exitCode">Code to return to parent process.  Should be &lt;0 if an error occurred, &gt;=0 if program is terminating normally.</param>
        /// <param name="silent">Exits without displaying a message or asking for a key press when set to 'true'</param>
        static void ExitProgram(string message="", int exitCode=0, bool silent=false)
        {
            if (!silent)
                Output(message);

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
