using Microsoft.Extensions.Logging;

namespace RandomHelpers
{
    public static class Git
    {
        private static readonly string GitBin = "git";

        public class GitTempRepository : IDisposable
        {
            public readonly string URL;
            public readonly string LocalPath;

            private readonly ILogger<GitTempRepository>? _logger;

            public IEnumerable<string> GitCommand(IEnumerable<string> args) => new string[] { GitBin, "-C", LocalPath }.Concat(args);

            public GitTempRepository(string URL, ILogger<GitTempRepository>? logger = null)
            {
                this.URL = URL;
                LocalPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(URL));
                _logger = logger;
                _logger?.LogInformation("Will use local repo {0} for {1}", LocalPath, URL);
            }

            public async Task Initialize(CancellationToken cancellation = default)
            {
                await Command.Run(new string[] {
                    GitBin,
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
                    _logger?.LogInformation("Cleanup temp Git bare repo at {0}", LocalPath);
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
