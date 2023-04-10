using NLog;

namespace RandomHelpers
{
    public static class Git
    {
        private static readonly NLog.Logger logger = LogManager.GetLogger(nameof(RandomHelpers.Git));

        private static readonly string GitExe = "git.exe";

        public class GitTempRepository : IDisposable
        {
            public readonly string URL;
            public readonly string LocalPath;

            public IEnumerable<string> GitCommand(IEnumerable<string> args) => new string[] { GitExe, "-C", LocalPath }.Concat(args);

            public GitTempRepository(string URL)
            {
                this.URL = URL;
                LocalPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(URL));
                logger.Info("Will use local repo {} for {}", LocalPath, URL);
            }

            public async Task Initialize(CancellationToken cancellation = default)
            {
                await Command.Run(new string[] {
                    GitExe,
                    "clone",
                    "--bare",
                    "--quiet",
                    "--no-tags",
                    URL, LocalPath
                }, cancellation);
            }

            public async Task Fetch(IEnumerable<string>? branches = null, CancellationToken cancellation = default)
            {
                IEnumerable<string> args = new string[] {
                    "fetch",
                    "--no-tags",
                    "--force",
                    "--auto-gc",
                    "--prune", "--prune-tags",
                    "--quiet",
                    URL,
                };
                if (branches?.Any() ?? false)
                {
                    args = args.Concat(branches.Select(b => $"+{b}:{b}"));
                }

                await Command.Run(GitCommand(args), cancellation);
            }

            public void Dispose()
            {
                if (Path.Exists(LocalPath))
                {
                    logger.Info("Cleanup temp Git bare repo at {}", LocalPath);
                    var dirInfo = new DirectoryInfo(LocalPath);
                    try
                    {
                        dirInfo.Delete(true);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Some files are read-only in .git
                        foreach (var info in dirInfo.GetFileSystemInfos("*", SearchOption.AllDirectories))
                        {
                            info.Attributes = FileAttributes.Normal;
                        }
                        dirInfo.Delete(true);
                    }
                }
            }
        }
    }
}
