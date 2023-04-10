using NLog;
using NLog.Common;
using NLog.Targets;
using System.Reflection;

namespace RandomHelpers
{
    public static class Logger
    {
        public static void InitializeLogger()
        {
            string LogLayout = "[${longdate}][${level}][${logger}]${when:when=length('${scopenested}')>0:Inner=[}${scopenested}${when:when=length('${scopenested}')>0:Inner=]} ${message}${onexception:${newline}}${exception:format=tostring}";
            InternalLogger.LogToConsole = false;

            ConsoleTarget ConsoleTarget = new("Console")
            {
                StdErr = false,
                Layout = LogLayout
            };

            FileTarget FileTarget = new("File")
            {
                Layout = LogLayout,
                FileName = Path.Combine(Path.GetTempPath(), $"{AppDomain.CurrentDomain.FriendlyName}.log"),
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles = 10,
                ArchiveOldFileOnStartup = true
            };

            NLog.Config.LoggingConfiguration nlogConfiguration = new();
            nlogConfiguration.AddRule(LogLevel.Info, LogLevel.Fatal, ConsoleTarget);
            nlogConfiguration.AddRule(LogLevel.Trace, LogLevel.Fatal, FileTarget);

            LogManager.Configuration = nlogConfiguration;
        }
    }
}
