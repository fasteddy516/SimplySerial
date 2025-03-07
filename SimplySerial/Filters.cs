
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

    /// <summary>
    /// A filter to apply to serial devices.
    /// </summary>
    public class Filter
    {
        public FilterType Type { get; set; } = FilterType.EXCLUDE;
        public FilterMatch Match { get; set; } = FilterMatch.STRICT;
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
                    if (f.Board.make == "") f.Board.make = "*";
                    if (f.Board.model == "") f.Board.model = "*";
                }
                filters.RemoveAll(f => f.Board.vid == "*" && f.Board.pid == "*" && f.Board.make == "*" && f.Board.model == "*" && f.Match != FilterMatch.CIRCUITPYTHON);
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
    }
}
