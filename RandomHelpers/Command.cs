using System.Diagnostics;
using System.Text;
using NLog;

namespace RandomHelpers
{
    public static class Command
    {
        private static readonly NLog.Logger logger = LogManager.GetLogger(nameof(RandomHelpers.Command));

        /// <returns>
        /// A tuple of process return code and process StdOut lines
        /// </returns>
        public static async Task<(int returnCode, List<string> output)> GetOutput(IEnumerable<string> command, CancellationToken cancellation = default)
        {
            using (var process = InternalRun(command))
            {
                if (process == null)
                {
                    throw new NullReferenceException(nameof(process));
                }

                List<string> outputs = new();
                while (!cancellation.IsCancellationRequested)
                {
                    string? output = await process.StandardOutput.ReadLineAsync(cancellation);
                    if (output == null)
                    {
                        break;
                    }
                    outputs.Add(output);
                }
                await process.WaitForExitAsync(cancellation);

                return (process.ExitCode, outputs);
            }
        }

        public static async Task<int> Run(IEnumerable<string> command, CancellationToken cancellation = default)
        {
            using (var process = InternalRun(command))
            {
                await process.WaitForExitAsync(cancellation);
                return process.ExitCode;
            }
        }

        private static Process InternalRun(IEnumerable<string> command)
        {
            var startInfo = new ProcessStartInfo(command.First())
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            // This is dumb
            foreach (var arg in command.Skip(1))
            {
                startInfo.ArgumentList.Add(arg);
            }

            logger.Info("Command {} {}", startInfo.FileName, startInfo.ArgumentList);

            return Process.Start(startInfo) ?? throw new NullReferenceException(nameof(Process));
        }
    }
}
