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

using static Nebulyn.System.Core.Drivers.RuntimeExecution;
using Nebulyn.System.Declarations.Generic;
using Cosmos.Core;

namespace Nebulyn
{
    public unsafe class Kernel : Sys.Kernel
    {
        GenericLogger logger;
        RuntimeExecution runtimeExecution;
        DriverHandler.GenericDriverBundle driverBundle;
        protected override void BeforeRun()
        {
            Globals.Canvas = new CGSCanvas();
            Globals.Terminal = new(
                PCScreenFont.Default,Globals.Canvas,
                Globals.Canvas.Width, Globals.Canvas.Height);

            driverBundle = DriverHandler.InstallAll();
            DriverHandler.StartAll(driverBundle);
            logger = driverBundle.gLogger;
            runtimeExecution = driverBundle.gRuntimeExecution;

            logger.Log("Nebulyn Kernel is starting...");

            

            Console.Clear();
            DriverList.ListDrivers();

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
