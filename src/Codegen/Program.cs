using System;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;

namespace Codegen
{
    // TODO: Lav denne med foelgende CLI API
    //
    // codegen --table SOME_TABLE --out mytype.codegen.cs  --template XXXXX.tt (mangler en del her...proev dig frem)
    //
    // See https://github.com/natemcmaster/CommandLineUtils
    class Program
    {
        public static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        // TODO: Paste fra github....Lav dit eget CLI

        [Option(Description = "The subject")]
        public string Subject { get; }

        [Option(ShortName = "n")]
        public int Count { get; }

        [UsedImplicitly]
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
