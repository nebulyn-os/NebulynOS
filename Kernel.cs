using Nebulyn.System.Core.Drivers;
using System;
using System.Collections.Generic;
using System.Text;
using Sys = Cosmos.System;

namespace Nebulyn
{
    public class Kernel : Sys.Kernel
    {
        GenericLogger logger;
        protected override void BeforeRun()
        {
            logger = new GenericLogger();
            var status = logger.Install();
            if (status.IsSuccess)
            {
                logger.Start();
                logger.Log("Logger installed successfully and started.");
            }
            else
            {
                Console.WriteLine($"Logger installation failed: {status.Message}");
            }

            logger.Log("Nebulyn Kernel is starting...");
            string loggerInfo = logger.Identify().ToString();
            Console.Clear();
            Console.WriteLine(loggerInfo);
            string[] logs = logger.GetCompiledLogs();
            foreach (string log in logs)
            {
                Console.WriteLine(log);
            }
        }

        protected override void Run()
        {
            Console.Write("Input: ");
            var input = Console.ReadLine();
            Console.Write("Text typed: ");
            Console.WriteLine(input);
        }
    }
}
