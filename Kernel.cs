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

namespace Nebulyn
{
    public unsafe class Kernel : Sys.Kernel
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

            logger.Log("Nebulyn Kernel is starting...");

            runtimeExecution = new RuntimeExecution();
            status = runtimeExecution.Install();
            if (status.IsSuccess)
            {
                runtimeExecution.Start();
                runtimeExecution.Clear();
            }
            else
            {
                logger.Log($"Runtime Execution installation failed: {status.Message}");
            }

            int ebx = 0;
            int edx = 0;
            int ecx = 0;

            SGenericStatus returnValue = runtimeExecution.CreateScript()

                .Xor(Operand.Reg(Register.EAX), Operand.Reg(Register.EAX)) // Set EAX = 0
                .Cpuid()
                .MovToVariable(&ebx, Register.EBX)  // EBX = "Genu" or "Auth"
                .MovToVariable(&edx, Register.EDX)  // EDX = "ineI" or "enti"
                .MovToVariable(&ecx, Register.ECX)  // ECX = "ntel" or "cAMD"
                .Ret()
                .Execute();


            char[] vendorString = new char[12];
            vendorString[0] = (char)(ebx & 0xFF);
            vendorString[1] = (char)((ebx >> 8) & 0xFF);
            vendorString[2] = (char)((ebx >> 16) & 0xFF);
            vendorString[3] = (char)((ebx >> 24) & 0xFF);
            vendorString[4] = (char)(edx & 0xFF);
            vendorString[5] = (char)((edx >> 8) & 0xFF);
            vendorString[6] = (char)((edx >> 16) & 0xFF);
            vendorString[7] = (char)((edx >> 24) & 0xFF);
            vendorString[8] = (char)(ecx & 0xFF);
            vendorString[9] = (char)((ecx >> 8) & 0xFF);
            vendorString[10] = (char)((ecx >> 16) & 0xFF);
            vendorString[11] = (char)((ecx >> 24) & 0xFF);
            string vendor = new string(vendorString);

            logger.Log($"CPU Vendor: {vendor}");

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
