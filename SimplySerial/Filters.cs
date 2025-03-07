
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace SimplySerial
{
    /// <summary>
    /// The type of filter to apply.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FilterType
    {
        INCLUDE,
        EXCLUDE,
        BLOCK,
    }

    /// <summary>
    /// The type of match to apply.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum FilterMatch
    {
        STRICT,
        LOOSE,
        CIRCUITPYTHON,
    }

    public class FilterSet
    {
        public List<Filter> Include => All.Where(f => f.Type == FilterType.INCLUDE).ToList();
        public List<Filter> Exclude => All.Where(f => f.Type == FilterType.EXCLUDE).ToList();
        public List<Filter> Block => All.Where(f => f.Type == FilterType.BLOCK).ToList();
        public List<Filter> All;
    }

    /// <summary>
    /// A filter to apply to serial devices.
    /// </summary>
    public class Filter
    {
        public FilterType Type { get; set; } = FilterType.EXCLUDE;
        public FilterMatch Match { get; set; } = FilterMatch.STRICT;
        public string Port { get; set; } = "*";
        public string VID { get; set; } = "*";
        public string PID { get; set; } = "*";
        public string Description { get; set; } = "*";
        public string Device { get; set; } = "*";

        public override string ToString()
        {
            return $"[{Type}:{Match}] Port[{Port}] VID[{VID}] PID[{PID}] Description[{Description}] Device[{Device}]";
        }


        /// <summary>
        /// Load filters from a JSON file, adding to an existing list if supplied
        /// </summary>
        /// <param name="path">Path of the file to add.</param>
        /// <param name="existing">Existing list of filters.</param>
        /// <returns></returns>
        public static List<Filter> AddFrom(string path, List<Filter> existing = null)
        {
            List<Filter> filters = new List<Filter>();

            try
            {
                filters = JsonConvert.DeserializeObject<List<Filter>>(File.ReadAllText(path));
                foreach (Filter f in filters)
                {
                    if (f.Port == "") f.Port = "*";
                    if (f.VID == "" || f.VID == "----") f.VID = "*";
                    if (f.PID == "" || f.PID == "----") f.PID = "*";
                    if (f.Description == "") f.Description = "*";
                    if (f.Device == "") f.Device = "*";
                }
                filters.RemoveAll(f => f.Port == "*" && f.VID == "*" && f.PID == "*" && f.Description == "*" && f.Device == "*" && f.Match != FilterMatch.CIRCUITPYTHON);
            }
            catch
            {
                filters = new List<Filter>();
            }

            if (existing != null)
            {
                filters.AddRange(existing);
            }

            return filters.OrderBy(f => f.Type).ToList();
        }

        public static bool MatchFilter(Filter filter, ComPort port)
        {
            string description = (port.isCircuitPython) ? (port.board.make + " " + port.board.model) : port.description;

            if (filter.Match == FilterMatch.STRICT)
            {
                if (filter.Port != "*" && filter.Port.ToLower() != port.name.ToLower()) return false;
                if (filter.VID != "*" && filter.VID.ToLower() != port.vid.ToLower()) return false;
                if (filter.PID != "*" && filter.PID.ToLower() != port.pid.ToLower()) return false;
                if (filter.Description != "*" && filter.Description != description) return false;
                if (filter.Device != "*" && filter.Device != port.busDescription) return false;
                return true;
            }
            else if (filter.Match == FilterMatch.LOOSE)
            {
                if (filter.Port != "*" && !port.name.ToLower().Contains(filter.Port.ToLower())) return false;
                if (filter.VID != "*" && !port.vid.ToLower().Contains(filter.VID.ToLower())) return false;
                if (filter.PID != "*" && !port.pid.ToLower().Contains(filter.PID.ToLower())) return false;
                if (filter.Description != "*" && !description.ToLower().Contains(filter.Description.ToLower())) return false;
                if (filter.Device != "*" && !port.busDescription.ToLower().Contains(filter.Device.ToLower())) return false;
                return true;
            }
            return false;
        }
    }
}
