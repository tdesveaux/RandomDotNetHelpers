using NLog;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using static RandomHelpers.SymStore.RefPointer;
using static RandomHelpers.SymStore.TransactionDetail;

namespace RandomHelpers
{
    public static class SymStore
    {
        private static readonly NLog.Logger logger = LogManager.GetLogger(nameof(SymStore));

        // CAB Compressiond does not handle files larger than this
        public static readonly double CABSizeLimit = 1.5 * 1000 * 1000 * 1000;

        private static readonly string MetadataFolderName = "000Admin";
        private static readonly string ServerFileName = "server.txt";
        private static readonly string HistoryFileName = "history.txt";
        private static readonly string RefFileName = "refs.ptr";

        public static string SymStorePath
        {
            get
            {
                string[] candidates = {
                    @"D:\autoSDK\HostWin64\Win64\Windows Kits\10\Debuggers\x64\symstore.exe",
                    @"C:\Git\autoSDK\HostWin64\Win64\Windows Kits\10\Debuggers\x64\symstore.exe",
                };
                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                throw new NotSupportedException();
            }
        }

        public enum CompressionType
        {
            CAB,
            ZIP
        }

        public static async Task<bool> AddSymbols(string symStorePath, string product, string path, string? version, string? comment, CompressionType? compressionType)
        {
            ProcessStartInfo StartInfo = new(SymStorePath)
            {
                ArgumentList =
                {
                    "add",
                    "/s", symStorePath,
                    "/o", "/3", "-:NOFORCECOPY",
                    "/t", product,
                    "/f", path
                }
            };

            if (Directory.Exists(path))
            {
                // Insert /r after the 'add' argument
                StartInfo.ArgumentList.Insert(1, "/r");
            }
            if (version != null)
            {
                StartInfo.ArgumentList.Add("/v");
                StartInfo.ArgumentList.Add(version);
            }
            if (comment != null)
            {
                StartInfo.ArgumentList.Add("/c");
                StartInfo.ArgumentList.Add(comment);
            }
            if (compressionType != null)
            {
                StartInfo.ArgumentList.Add("/compress");
                string? compressionTypeValue = compressionType.ToString();
                if (compressionTypeValue != null)
                {
                    StartInfo.ArgumentList.Add(compressionTypeValue);
                }
            }

            using (Process process = new()
            {
                StartInfo = StartInfo
            })
            {
                process.Start();
                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
        }

        public class Transaction
        {
            public enum Operation
            {
                add,
                del
            }

            public enum Type
            {
                file,
                ptr
            }

            private static readonly Regex lineRegex = new(@"^(?<id>\d+),(?<operation>(add|del)),(?<type>(file|ptr)),(?<creation_date>\d{2}/\d{2}/\d{4}),(?<creation_time>\d{2}:\d{2}:\d{2}),\""(?<product_name>.*)\"",\""(?<version>.*)\"",\""(?<comment>.*)\"",$", RegexOptions.Compiled);

            public uint id;
            public Operation operation;
            public Type type;
            public DateTime creationDateTime; // stored as date and time separately
            public string productName;
            public string version = string.Empty;
            public string comment = string.Empty;

            private Transaction(uint id, Operation operation, Type type, DateTime creationDateTime, string productName)
            {
                this.id = id;
                this.operation = operation;
                this.type = type;
                this.creationDateTime = creationDateTime;
                this.productName = productName;
            }

            public static Transaction? FromLine(string line)
            {
                Match match = lineRegex.Match(line);
                if (!match.Success)
                {
                    logger.Error($"Line does not match regex: {line}");
                    return null;
                }

                DateTime CreateDateTime = DateTime.ParseExact($"{match.Groups["creation_date"].ValueSpan} {match.Groups["creation_time"].ValueSpan}", "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

                return new Transaction(
                    uint.Parse(match.Groups["id"].ValueSpan),
                    Enum.Parse<Operation>(match.Groups["operation"].ValueSpan),
                    Enum.Parse<Type>(match.Groups["type"].ValueSpan),
                    CreateDateTime,
                    match.Groups["product_name"].Value
                )
                {
                    version = match.Groups["version"].Value,
                    comment = match.Groups["comment"].Value
                };
            }

            public override string ToString()
            {
                return $"{id:D10},{operation},{type},{creationDateTime.ToString("MM/dd/yyyy,HH:mm:ss")},\"{productName}\",\"{version}\",\"{comment}\",";
            }
        }

        public static async Task<List<Transaction>> LoadServer(string symSrvPath)
        {
            using var nlogScopeContext = ScopeContext.PushNestedState("LoadServer");
            return await LoadTransactions(Path.Combine(symSrvPath, MetadataFolderName, ServerFileName), Transaction.FromLine);
        }

        public static async Task<List<Transaction>> LoadHistory(string symSrvPath)
        {
            using var nlogScopeContext = ScopeContext.PushNestedState("LoadHistory");
            return await LoadTransactions(Path.Combine(symSrvPath, MetadataFolderName, HistoryFileName), Transaction.FromLine);
        }

        public class TransactionDetail
        {
            public class ArtifactDetails
            {
                public string StoredSymbolPath;
                public string OriginalSymbolPath;

                private static readonly Regex lineRegex = new(@"^\""(?<storedPath>.*)\"",\""(?<originalPath>.*)\""$", RegexOptions.Compiled);

                private ArtifactDetails(string storedSymbolPath, string originalSymbolPath)
                {
                    StoredSymbolPath = storedSymbolPath;
                    OriginalSymbolPath = originalSymbolPath;
                }

                public static ArtifactDetails? FromLine(string line)
                {
                    Match match = lineRegex.Match(line);
                    if (!match.Success)
                    {
                        logger.Error($"Line does not match regex: {line}");
                        return null;
                    }

                    return new ArtifactDetails(
                        match.Groups["storedPath"].Value,
                        match.Groups["originalPath"].Value
                    );
                }

                public override string ToString()
                {
                    return $"{StoredSymbolPath},{OriginalSymbolPath}";
                }
            }

            public readonly uint Id;
            public List<ArtifactDetails> artifactDetails = new();

            public TransactionDetail(uint id)
            {
                Id = id;
            }
        }

        public static async Task<List<TransactionDetail>> LoadTransactionDetails(string symSrvPath)
        {
            using var nlogScopeContext = ScopeContext.PushNestedState("LoadTransactionDetails");

            DirectoryInfo metadataDirectory = new(Path.Combine(symSrvPath, MetadataFolderName));
            Regex transactionFileNameRegex = new(@"^\d{10}$");

            List<TransactionDetail> transactions = new();

            foreach (var file in metadataDirectory.EnumerateFiles().Where(f => transactionFileNameRegex.IsMatch(f.Name)))
            {
                logger.Debug($"Found transaction file {file.FullName}");
                transactions.Add(new(uint.Parse(file.Name))
                {
                    artifactDetails = await Task.Run(() => LoadTransactions(file.FullName, ArtifactDetails.FromLine))
                });
            }

            return transactions;
        }

        public class RefPointer
        {
            public class RefPointerEntry
            {
                private static readonly Regex lineRegex = new(@"^(?<id>\d+),(?<type>(file|ptr)),\""(?<originalPath>.*)\"",(?<PEType>(pri|bin)),,Y,,$", RegexOptions.Compiled);

                public enum PEType
                {
                    pri,
                    bin
                }

                public uint Id;
                public Transaction.Type TransactionType;
                public string OriginalPath;
                public PEType Type;

                private RefPointerEntry(uint id, Transaction.Type transactionType, string originalPath, PEType type)
                {
                    Id = id;
                    TransactionType = transactionType;
                    OriginalPath = originalPath;
                    Type = type;
                }

                public static RefPointerEntry? FromLine(string line)
                {
                    Match match = lineRegex.Match(line);
                    if (!match.Success)
                    {
                        logger.Error($"Line does not match regex: {line}");
                        return null;
                    }

                    return new RefPointerEntry(
                        uint.Parse(match.Groups["id"].Value),
                        Enum.Parse<Transaction.Type>(match.Groups["type"].ValueSpan),
                        match.Groups["originalPath"].Value,
                        Enum.Parse<PEType>(match.Groups["PEType"].ValueSpan)
                    );
                }

                public override string ToString()
                {
                    return $"{Id:D10},{TransactionType},{OriginalPath},{Type},,Y,,";
                }
            }

            public List<RefPointerEntry> Entries = new();
            public readonly string SubPath;

            public readonly string FileName;
            public readonly string Hash;

            public RefPointer(string subPath)
            {
                if (Path.GetFileName(subPath) != RefFileName)
                {
                    throw new ArgumentException("Provided SubPath does not point to a refs.ptr file");
                }

                this.SubPath = subPath;

                string? parentDirectoryName = Path.GetDirectoryName(subPath);
                if (string.IsNullOrEmpty(parentDirectoryName))
                {
                    throw new ArgumentException("Provided SubPath does not have the hash component");
                }
                this.Hash = Path.GetFileName(parentDirectoryName);

                parentDirectoryName = Path.GetDirectoryName(subPath);
                if (string.IsNullOrEmpty(parentDirectoryName))
                {
                    throw new ArgumentException("Provided SubPath does not have the filename component");
                }
                this.FileName = Path.GetFileName(parentDirectoryName);
            }
        }

        public static async Task<List<RefPointer>> LoadRefPointers(string symSrvPath)
        {
            using var nlogScopeContext = ScopeContext.PushNestedState("LoadRefPointers");

            DirectoryInfo symSrvDirectory = new(symSrvPath);
            List<RefPointer> refPointers = new();

            logger.Info($"Search for refPtr files");
            var refPtrFiles = symSrvDirectory
                .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                .Where(d => d.Name != MetadataFolderName) // Skip Metadata folder
                .Select(d => d.EnumerateFiles(RefFileName, SearchOption.AllDirectories))
                .SelectMany(f => f)
                .ToList();
            logger.Info($"Found {refPtrFiles.Count} refPtr files");

            foreach (var refPtrFile in refPtrFiles)
            {
                logger.Debug($"Found refPtr {refPtrFile.FullName}");
                refPointers.Add(new(Path.GetRelativePath(symSrvDirectory.FullName, refPtrFile.FullName))
                {
                    Entries = await Task.Run(() => LoadTransactions(refPtrFile.FullName, RefPointerEntry.FromLine))
                });
            }

            return refPointers;
        }

        private static async Task<List<_Data>> LoadTransactions<_Data>(string filepath, Func<string, _Data?> parsingMethod)
        {
            if (!File.Exists(filepath))
            {
                logger.Error($"transaction holder {filepath} does no exists");
                throw new FileNotFoundException();
            }

            List<_Data> transactions = new();

            using (StreamReader fStreamReader = new(filepath))
            {
                while (fStreamReader.Peek() >= 0)
                {
                    string? line = await fStreamReader.ReadLineAsync();

                    _Data? transaction = (line != null ? parsingMethod(line) : default);
                    if (transaction != null)
                    {
                        transactions.Add(transaction);
                    }
                }
            }

            return transactions;
        }
    }
}
