using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace SimplySerial
{
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


    public class ComPortList
    {
        public List<ComPort> Available = new List<ComPort>();
        public List<ComPort> Excluded = new List<ComPort>();
    }


    public static class ComPortManager
    {
        public static FilterSet Filters = new FilterSet();

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
        public static ComPortList GetPorts()
        {
            const string vidPattern = @"VID_([0-9A-F]{4})";
            const string pidPattern = @"PID_([0-9A-F]{4})";
            const string namePattern = @"(?<=\()COM[0-9]{1,3}(?=\)$)";
            const string query = "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"";

            // as per INTERFACE_PREFIXES in adafruit_board_toolkit
            // (see https://github.com/adafruit/Adafruit_Board_Toolkit/blob/main/adafruit_board_toolkit)
            string[] cpb_descriptions = new string[] { "CircuitPython CDC ", "Sol CDC ", "StringCarM0Ex CDC " };

            if (Filters.All == null)
            {
                Filters.All = Filter.AddFrom(SimplySerial.AppFolder + SimplySerial.FilterFile);
                if (SimplySerial.AppFolder != SimplySerial.WorkingFolder)
                {
                    Filters.All = Filter.AddFrom(SimplySerial.WorkingFolder + SimplySerial.FilterFile, existing: Filters.All);
                }
            }

            List<ComPort> detectedPorts = new List<ComPort>();

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

                detectedPorts.Add(c);
            }

            // apply filters to determine if this port should be included or excluded in autodetection
            ComPortList ports = new ComPortList();

            // if there are *any* include filters than we can *only* include matches, and anything that doesn't match gets excluded
            if (Filters.Include.Count > 0)
            {
                foreach (ComPort p in detectedPorts)
                {
                    bool matched = false;

                    foreach (Filter f in Filters.Include)
                    {
                        if (Filter.MatchFilter(f, p))
                        {
                            ports.Available.Add(p);
                            matched = true;
                            break;
                        }
                    }
                    if (!matched)
                    {
                        ports.Excluded.Add(p);
                    }
                }
            }
            else
            {
                // if there are *no* include filters, then we start out including everything
                ports.Available = detectedPorts;
            }

            // once we have our initial include list, we apply our exclude filters to remove any ports that match and add them to the exclude list
            foreach (ComPort p in ports.Available.ToList())
            {
                foreach (Filter f in Filters.Exclude.Concat(Filters.Block))
                {
                    if (Filter.MatchFilter(f, p))
                    {
                        ports.Available.Remove(p);
                        ports.Excluded.Add(p);
                    }
                }
            }

            ports.Available = ports.Available.Distinct().OrderBy(p => p.num).ToList();
            ports.Excluded = ports.Excluded.Distinct().OrderBy(p => p.num).ToList();

            if (ports.Available.Count == 0 && Filters.Block.Count > 0)
            {
                Filters.All.RemoveAll(f => f.Type == FilterType.BLOCK);
            }

            return ports;
        }
    }
}
