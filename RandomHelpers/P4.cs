using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RandomHelpers
{
    public static class P4
    {
        public static IEnumerable<string> P4Command(IEnumerable<string> args) => new string[] { "p4", "-ztag", "-Mj" }.Concat(args);

        public static DateTime P4TimeToDateTime(UInt64 time) => DateTime.UnixEpoch.AddSeconds(time);

        public static string DateTimeToP4RevSpec(DateTime dt) => DateTimeToP4RevSpec(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        public static string DateTimeToP4RevSpec(int year, int? month, int? day, int? hour, int? minute, int? second)
        {
            StringBuilder builder = new();

            builder.Append($"{year:D4}");
            // Bit convoluted but all values are optionals in P4 Specs
            if (month != null)
            {
                builder.Append("/").Append($"{month:D2}");
                if (day != null)
                {
                    builder.Append("/").Append($"{day:D2}");
                    if (hour != null)
                    {
                        builder.Append(":").Append($"{hour:D2}");
                        if (minute != null)
                        {
                            builder.Append(":").Append($"{minute:D2}");
                            if (second != null)
                            {
                                builder.Append(":").Append($"{second:D2}");
                            }
                        }
                    }
                }
            }

            return builder.ToString();
        }

        #region P4Data
        // These have only members I needed at the moment
        public class P4Fstat
        {
            public string depotFile { get; set; } = string.Empty;
        }

        public class P4Filelog
        {
            public string change { get; set; } = string.Empty;
            public string time { get; set; } = string.Empty;

            public DateTime TimeAsDateTime() => P4TimeToDateTime(UInt64.Parse(time));
        }

        public class P4Print
        {
            public string action { get; set; } = string.Empty;
            public string change { get; set; } = string.Empty;
            public string time { get; set; } = string.Empty;
            public string rev { get; set; } = string.Empty;

            public string data { get; set; } = string.Empty;

            public DateTime TimeAsDateTime() => P4TimeToDateTime(UInt64.Parse(time));
        }

        public class P4Error
        {
            public string data { get; set; } = string.Empty;
            public int? generic { get; set; } = null;
            public int? severity { get; set; } = null;
        }
        #endregion

        #region P4Output Parsing
        public static List<P4Print> ParsePrintOutput(List<string> rawOutput)
        {
            List<P4Print> results = new();

            if (rawOutput.Count == 1)
            {
                P4Error? error = JsonSerializer.Deserialize<P4Error>(rawOutput[0]);
                // catch
                // {"data":"//{PATH}@{DATETIME},{DATETIME} - no revision(s) after that date.\n","generic":17,"severity":2}
                if (error != null &&
                    error.generic != null && error.generic == 17 &&
                    error.severity != null && error.severity == 2)
                {
                    return results;
                }
            }

            foreach (var outputElement in rawOutput)
            {
                var jsonObject = JsonSerializer.Deserialize<JsonObject>(outputElement) ?? throw new ArgumentNullException(nameof(outputElement));
                if (jsonObject.TryGetPropertyValue("data", out var value))
                {
                    results.Last().data += value.Deserialize<string>() ?? throw new ArgumentNullException(nameof(value));
                }
                else
                {
                    results.Add(jsonObject.Deserialize<P4Print>() ?? throw new ArgumentNullException(nameof(jsonObject)));
                }
            }

            return results;
        }

        /// <summary>
        /// P4 can output ONE json object with format in: key0, key1, ..., keyN
        /// Reformat it to a list of objects.
        /// Ex: p4 filelog
        /// </summary>
        public static List<T> P4FuckedUpToNormal<T>(string fuckedUpOutput) where T : class, new()
        {
            var jsonObject = JsonSerializer.Deserialize<JsonObject>(fuckedUpOutput);
            if (jsonObject == null)
            {
                throw new ArgumentNullException(nameof(fuckedUpOutput));
            }

            Type dataType = typeof(T);
            var dataTypeProperties = dataType.GetProperties().ToArray();
            if (dataTypeProperties == null || dataTypeProperties.Length <= 0)
            {
                throw new ArgumentNullException(dataType.Name);
            }

            List<T> values = new();
            for (int idx = 0; jsonObject.ContainsKey($"{dataTypeProperties[0].Name}{idx}"); ++idx)
            {
                var tmpObject = new T();
                foreach (var property in dataTypeProperties)
                {
                    var propertyValue = jsonObject[$"{property.Name}{idx}"]?.Deserialize(property.PropertyType);
                    if (propertyValue != null)
                    {
                        property.SetValue(tmpObject, propertyValue, null);
                    }
                }
                values.Add(tmpObject);
            }

            return values;
        }
        #endregion
    }
}
