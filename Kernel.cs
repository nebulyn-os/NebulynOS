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
        RuntimeExecution runtimeExecution;
        protected override void BeforeRun()
        {
            logger = new GenericLogger();
            var status = logger.Install();
            if (status.IsSuccess)
            {
                logger.Start();
            }
            else
            {
                Console.WriteLine($"Logger installation failed: {status.Message}");
            }

            runtimeExecution = new RuntimeExecution();
            status = runtimeExecution.Install();
            if (status.IsSuccess)
            {
                runtimeExecution.Start();
            }
            else
            {
                Console.WriteLine($"Runtime Execution installation failed: {status.Message}");
            }

            logger.Log("Nebulyn Kernel is starting...");

            Console.Clear();
            DriverList.ListDrivers();

            runtimeExecution.SetInstruction(RuntimeExecution.SingleInstruction.HLT);
            runtimeExecution.Execute();

            logger.PrintLogs();


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
