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


        static void Main(string[] args)
        {


            ConsoleKeyInfo cki = new ConsoleKeyInfo();
            Console.TreatControlCAsInput = true;

            // default comspec values (will be overridden by command-line arguments)
            string[] availablePorts = SerialPort.GetPortNames();
            string port = string.Empty;
            int baud = 9600;
            Parity parity = Parity.None;
            int dataBits = 8;
            StopBits stopBits = StopBits.One;

            if (availablePorts.Count() < 1)
                ExitProgram("No COM ports detected.", -1);
            else
                port = availablePorts[0];

            for(int i = 0; i < args.Count(); i++)
            {
                if ((args[i].ToLower()).StartsWith("-c"))
                {
                    Console.WriteLine("PORT: {0}", args[i]);
                }
                if ((args[i].ToLower()).StartsWith("-b"))
                {
                    Console.WriteLine("BAUD: {0}", args[i]);
                }
                if ((args[i].ToLower()).StartsWith("-p"))
                {
                    Console.WriteLine("PARITY: {0}", args[i]);
                }
                if ((args[i].ToLower()).StartsWith("-d"))
                {
                    Console.WriteLine("DATABITS: {0}", args[i]);
                }
                if ((args[i].ToLower()).StartsWith("-s"))
                {
                    Console.WriteLine("STOPBITS: {0}", args[i]);
                }
            }



            ExitProgram("That's All Folks!", 1);

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

        static void ExitProgram(string message, int exitCode)
        {
            Console.WriteLine(message);
            Console.WriteLine("\nPress any key to exit...");
            while (!Console.KeyAvailable)
                Thread.Sleep(25);
            Environment.Exit(exitCode);
        }
    }
}
