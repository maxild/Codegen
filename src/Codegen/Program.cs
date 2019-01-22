using System;
using McMaster.Extensions.CommandLineUtils;

namespace Codegen
{
    // See https://github.com/natemcmaster/CommandLineUtils
    class Program
    {
        public static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        [Option(Description = "The subject")]
        public string Subject { get; }

        [Option(ShortName = "n")]
        public int Count { get; }

        private void OnExecute()
        {
            var subject = Subject ?? "world";
            for (var i = 0; i < Count; i++)
            {
                Console.WriteLine($"Hello {subject}!");
            }
        }
    }
}
