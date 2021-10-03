using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Bloomn.Cli
{
    internal static class Constants
    {
        public const string Add = "add";
        public const string Check = "check";
        public const string Show = "show";
        public const string Create = "create";
        public const string StateFlag = "--state";
        public const string StateShortFlag = "-s";
        public const string StateEnv = "BLOOMN_STATE";
    }
    
    class Program
    {
        [DoesNotReturn]
        public static void Help(string? command, string? error)
        {
            if (error != null)
            {
                Console.WriteLine(error);
                Console.WriteLine();
            }
            
            Console.WriteLine($"For all commands, the path of the state/config file can be provided as {Constants.StateShortFlag}|{Constants.StateFlag} or as an environment variable {Constants.StateEnv}.");
            
            switch (command)
            {
                case Constants.Add:
                    Console.WriteLine("Provide a newline delimited list of keys to add, or the command will read from stdin. ");
                    break;
                case Constants.Check:
                    Console.WriteLine("Provide a newline delimited list of keys to check, or the command will read from stdin. The command will print `{key} : {0|1}` for each key provided.");
                    break;
                case Constants.Show:
                    Console.WriteLine("Prints the configuration of the filter from the state file.");
                    break;
                case Constants.Create:
                    Console.WriteLine("Creates a default, empty state file.");
                    break;
                default:
                    Console.WriteLine(@"Commands:
  add       Add keys to filter
  check     Check if keys are present in filter
  create    Create a new filter by saving a default config
  show      Print off configuration of filter");
                    break;
                    
            }

            if (error != null)
            {
                Environment.Exit(1);
            }
            else
            {
                Environment.Exit(0);
            }
        }

        private static string ExtractFlagValue(int index, string flag, string[] args)
        {
            if (flag.Contains("="))
            {
                return flag.Split("=").Last();
            }

            if (flag.StartsWith("--"))
            {
                if (args.Length >= index + 2)
                {
                    return args[index + 1];
                } 
                Console.WriteLine($"Flag {flag} requires a parameter");
            }

            return flag.Substring(2);
        }
        
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Help(null, null);
            }

            var cmd = args[0];

            var stateFilePath = Environment.GetEnvironmentVariable(Constants.StateEnv);

            for (int i = 0; i < args.Length; i++)
            {
                var flag = args[i];
                if (flag.StartsWith(Constants.StateShortFlag) || flag.StartsWith(Constants.StateFlag))
                {
                    stateFilePath = ExtractFlagValue(i, flag, args);
                }
            }

            var app = new App()
            {
                StateFilePath = stateFilePath,
            };

            switch (cmd)
            {
                case Constants.Add:
                    app.Add();
                    break;
                case Constants.Check:
                    app.Check();
                    break;
                case Constants.Show:
                    app.Show();
                    break;
                case Constants.Create:
                    app.Create();
                    break;
                default:
                    Help(cmd, null);
                    break;
            }
            
        }
    }

    public class App
    {
        public string? StateFilePath { get; set; }

        public void Add()
        {
            throw new NotImplementedException();
        }

        public void Check()
        {
            throw new NotImplementedException();
        }

        public void Show()
        {
            throw new NotImplementedException();
        }

        public void Create()
        {
            throw new NotImplementedException();
        }
    }
}