using Xunit;

namespace RandomHelpers.UnitTests
{
    public class FormatterFileSizeTests
    {
        [Theory]
        [InlineData("1.0kB", 1 * 1024)]
        [InlineData("1.1kB", 1.1 * 1024)]
        [InlineData("1.1kB", 1.12 * 1024)]
        public void FormatFileSize_KB(string expected, double value)
        {
            Assert.Equal(expected, Formatter.FormatFileSize(value));
        }
    }
}
