using ConsoleAppConcurrentMemoryProblem.Helpers;
using System;

namespace ConsoleAppConcurrentMemoryProblem
{
    class Program
    {
        static void Main(string[] args)
        {
            GlobalSettings globalSettings = new GlobalSettings();

            BiggestFolder.Execute();

            Console.WriteLine("... done");
        }
    }
}
