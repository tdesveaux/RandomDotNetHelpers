using System.Text.Json;
using Xunit;

namespace RandomHelpers.UnitTests
{
    public class P4FuckedUpToNormalTests
    {
        internal class Fake
        {
            public string key { get; set; } = string.Empty;
            public string otherkey { get; set; } = string.Empty;
        }

        [Fact]
        public void P4FuckedUpToNormal_OneKeyOneValue()
        {
            var jsonOutput = """
            {
                "key0": "value0"
            }
            """;
            var result = RandomHelpers.P4.P4FuckedUpToNormal<Fake>(jsonOutput);
            Assert.Equal(
                JsonSerializer.Serialize(new Fake[] { new Fake { key = "value0" } }),
                JsonSerializer.Serialize(result));
        }

        [Fact]
        public void P4FuckedUpToNormal_OneKeyMultipleValues()
        {
            var jsonOutput = """
            {
                "key0": "value0",
                "key1": "value1",
                "key2": "value2"
            }
            """;
            var expected = new Fake[]
            {
                new Fake { key= "value0" },
                new Fake { key= "value1" },
                new Fake { key= "value2" },
            };
            var result = RandomHelpers.P4.P4FuckedUpToNormal<Fake>(jsonOutput);
            Assert.Equal(
                JsonSerializer.Serialize(expected),
                JsonSerializer.Serialize(result));
        }

        [Fact]
        public void P4FuckedUpToNormal_MultipleKeysOneValue()
        {
            var jsonOutput = """
            {
                "key0": "value0",
                "otherkey0": "othervalue0"
            }
            """;
            var expected = new Fake[]
            {
                new Fake { key= "value0", otherkey= "othervalue0" },
            };
            var result = RandomHelpers.P4.P4FuckedUpToNormal<Fake>(jsonOutput);
            Assert.Equal(
                JsonSerializer.Serialize(expected),
                JsonSerializer.Serialize(result));
        }

        [Fact]
        public void P4FuckedUpToNormal_MultipleKeysMultipleValues()
        {
            var jsonOutput = """
            {
                "key0": "value0",
                "key1": "value1",
                "key2": "value2",
                "otherkey0": "othervalue0",
                "otherkey1": "othervalue1",
                "otherkey2": "othervalue2"
            }
            """;
            var expected = new Fake[]
            {
                new Fake { key= "value0", otherkey= "othervalue0" },
                new Fake { key= "value1", otherkey= "othervalue1" },
                new Fake { key= "value2", otherkey= "othervalue2" },
            };
            var result = RandomHelpers.P4.P4FuckedUpToNormal<Fake>(jsonOutput);
            Assert.Equal(
                JsonSerializer.Serialize(expected),
                JsonSerializer.Serialize(result)
            );
        }
    }


    public class ParsePrintOutputTests
    {
        [Fact]
        public void ParsePrintOutput_EmptyInput()
        {
            List<string> testData = new();
            var result = RandomHelpers.P4.ParsePrintOutput(testData);
            // Assert no issues
            Assert.Empty(result);
        }

        [Fact]
        public void ParsePrintOutput_NoRevisionAfterDate()
        {
            List<string> testData = new()
            {
                "{\"data\":\"//PG0/Main/DNE/Build/Build.version//{PATH}@{DATETIME},{DATETIME} - no revision(s) after that date.\\n\",\"generic\":17,\"severity\":2}"
            };
            var result = RandomHelpers.P4.ParsePrintOutput(testData);
            // Assert no issues
            Assert.Empty(result);
        }

        [Fact]
        public void ParsePrintOutput_()
        {
            List<string> testData = new()
            {
                "{\"action\":\"edit\",\"change\":\"1267119\",\"depotFile\":\"{DEPOT_PATH}\",\"fileSize\":\"81\",\"rev\":\"2922\",\"time\":\"1680772908\",\"type\":\"text\"}",
                "{\"data\":\"{FILE_CONTENT}\"}",
                "{\"action\":\"edit\",\"change\":\"1266686\",\"depotFile\":\"{DEPOT_PATH}\",\"fileSize\":\"81\",\"rev\":\"2921\",\"time\":\"1680703211\",\"type\":\"text\"}",
                "{\"data\":\"{FILE_CONTENT}\"}",
                // P4 throw some output with empty data for some reason
                "{\"data\":\"\"}"
            };
            List<RandomHelpers.P4.P4Print> expected = new()
            {
                new() { action = "edit", change = "1267119", time = "1680772908", rev = "2922", data = "{FILE_CONTENT}" },
                new() { action = "edit", change = "1266686", time = "1680703211", rev = "2921", data = "{FILE_CONTENT}" },
            };
            var result = RandomHelpers.P4.ParsePrintOutput(testData);
            // Assert no issues
            Assert.Equal(
                JsonSerializer.Serialize(expected),
                JsonSerializer.Serialize(result)
            );
        }
    }
}
