using System.Globalization;

namespace RandomHelpers
{
    public static class Formatter
    {
        #region
        private static readonly List<string> prefixCollection = new List<string>() { "", "k", "M", "G", "T", "P", "E", "Z", "Y" };
        // Not from me, 'stole' this somewhere but can't remember where
        public static string FormatUnit(double value, string unit, double multiplier = 1000)
        {
            int prefixIndex = 0;
            while ((Math.Abs(value) > multiplier) && (prefixIndex < (prefixCollection.Count - 1)))
            {
                value /= multiplier;
                prefixIndex += 1;
            }

            return value.ToString("N1", CultureInfo.InvariantCulture) + prefixCollection[prefixIndex] + unit;
        }

        public static string FormatFileSize(double value) => FormatUnit(value, "B", 1024);
        #endregion
    }
}
