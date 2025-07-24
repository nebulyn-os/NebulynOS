using Nebulyn.System.Core.Drivers;
using System;
using System.Collections.Generic;
using System.Linq;
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

            Console.Clear();
            DriverList.ListDrivers();
            string[] logs = logger.GetLogs().ToArray();

            if (logs.Length > 0)
            {
                Console.WriteLine("Logs:");
                foreach (var log in logs)
                {
                    Console.WriteLine(log);
                }
            }
            else
            {
                Console.WriteLine("No logs available.");
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
