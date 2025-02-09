using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logger
{
    public class Program
    {
        private static readonly string Endpoint = "cn-hangzhou.log.aliyuncs.com";
        private static readonly string EncryptionKey = "PLbYEJG(%6Xyn#iS4KDU$=9zmZkT2a8v";
        private static readonly EncryptionHelper EncryptionHelper = new EncryptionHelper(EncryptionKey);
        public static string Project = "lnvpe";
        public static string AccessKeyId = string.Empty;
        public static string AccessKeySecret = string.Empty;

        public static readonly Dictionary<string, string> PipeToLogStore = new Dictionary<string, string>
        {
            { "CpuInfoPipe", "cpuinfolog" },
            { "DispatcherLogPipe", "dispatcherlog" },
            { "DispatcherCSVPipe", "dispatchercsv" },
            { "mlscenariocsvPipe", "mlscenariocsv" }
        };

        public static void Main(string[] args)
        {
            LoadCredentials();

            if (args.Length == 0)
            {
                Console.WriteLine("Invalid arguments. Use: Logger.exe /stream OR Logger.exe /file /logstore <filepath>");
            }
            else if (args[0].Equals("/stream", StringComparison.OrdinalIgnoreCase) && args.Length == 1)
            {
                var streamMode = new StreamMode(Endpoint);
                Task.Run(() => streamMode.Start()).Wait();
            }
            else if (args[0].Equals("/file", StringComparison.OrdinalIgnoreCase) && args.Length == 3)
            {
                string logStore = args[1].TrimStart('/');
                string filePath = args[2];

                var validLogStores = PipeToLogStore.Values.Select(v => v.ToLower()).ToArray();
                if (validLogStores.Contains(logStore.ToLower()))
                {
                    var FileMode = new FileMode(Endpoint);
                    Task.Run(() => FileMode.Start(logStore, filePath)).Wait();
                }
                else
                {
                    Console.WriteLine("Invalid logstore. Valid options are: " + string.Join(", ", validLogStores) + ".");
                }
            }
            else if (args[0].Equals("/newkey", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write("Enter AccessKeyId: ");
                AccessKeyId = Console.ReadLine()?.Trim() ?? string.Empty;

                Console.Write("Enter AccessKeySecret: ");
                AccessKeySecret = Console.ReadLine()?.Trim() ?? string.Empty;

                string encrypted = EncryptionHelper.EncryptString($"{AccessKeyId}|{AccessKeySecret}");
                Console.WriteLine($"Encrypted string: {encrypted}");
            }
            else
            {
                Console.WriteLine("Invalid arguments. Use: Logger.exe /stream OR Logger.exe /file /logstore <filepath>");
            }
        }

        private static void LoadCredentials()
        {
            EncryptionHelper.LoadCredentials(out AccessKeyId, out AccessKeySecret);
        }

    }
}
