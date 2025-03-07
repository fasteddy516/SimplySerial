
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        public List<Filter> All;
    }

    /// <summary>
    /// A filter to apply to serial devices.
    /// </summary>
    public class Filter
    {
        public FilterType Type { get; set; } = FilterType.EXCLUDE;
        public FilterMatch Match { get; set; } = FilterMatch.STRICT;
        public int Port { get; set; } = -1;
        public Board Board { get; set; } = new Board();

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
                    if (f.Board.vid == "" || f.Board.vid == "----") f.Board.vid = "*";
                    if (f.Board.pid == "" || f.Board.pid == "----") f.Board.pid = "*";
                    if (f.Board.make == "" || f.Board.make == "VID:----") f.Board.make = "*";
                    if (f.Board.model == "" || f.Board.model == "PID:----") f.Board.model = "*";
                }
                filters.RemoveAll(f => f.Board.vid == "*" && f.Board.pid == "*" && f.Board.make == "*" && f.Board.model == "*" && f.Match != FilterMatch.CIRCUITPYTHON && f.Port == -1);
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
            if (filter.Port != -1 && filter.Port != port.num) return false;
            if (filter.Match == FilterMatch.STRICT)
            {
                if (filter.Board.vid != "*" && filter.Board.vid != port.vid) return false;
                if (filter.Board.pid != "*" && filter.Board.pid != port.pid) return false;
                if (filter.Board.make != "*" && filter.Board.make != port.board.make && filter.Board.make != port.description) return false;
                if (filter.Board.model != "*" && filter.Board.model != port.board.model && filter.Board.model != port.busDescription) return false;
                return true;
            }
            else if (filter.Match == FilterMatch.LOOSE)
            {
                if (filter.Board.vid != "*" && !port.vid.ToLower().Contains(filter.Board.vid.ToLower())) return false;
                if (filter.Board.pid != "*" && !port.pid.ToLower().Contains(filter.Board.pid.ToLower())) return false;
                if (filter.Board.make != "*" && !port.board.make.ToLower().Contains(filter.Board.make.ToLower()) && !port.description.ToLower().Contains(filter.Board.make.ToLower())) return false;
                if (filter.Board.model != "*" && !port.board.model.ToLower().Contains(filter.Board.model.ToLower()) && !port.busDescription.ToLower().Contains(filter.Board.model.ToLower())) return false;
                return true;
            }
            return false;
        }
    }
}
