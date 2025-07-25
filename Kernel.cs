using Cosmos.Core.Memory;
using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using Nebulyn.System.Core.Drivers;
using Nebulyn.System.Core.Temps;
using Nebulyn.System.Terminal;
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
            Globals.Canvas = new CGSCanvas();
            Globals.Terminal = new(
                PCScreenFont.Default,Globals.Canvas,
                Globals.Canvas.Width, Globals.Canvas.Height);

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

            CommandsRegistryService.RegisterCommands();

            Globals.Terminal.Draw(0, 0);
            Globals.Canvas.Display();
        }

        protected override void Run()
        {
            Globals.Terminal.UpdateInput();
            Globals.Terminal.Draw(0,0);
            Globals.Canvas.Display();

            if (Globals.Ticks % 20 == 0) Heap.Collect();
            Globals.Ticks++;
        }
    }
}
