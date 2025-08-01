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

            int testin = 5;
            try
            {
                SGenericStatus returnValue = runtimeExecution.CreateScript()

                    .Xor(Operand.Reg(Register.EAX), Operand.Reg(Register.EAX)) // Set EAX = 0
                    .Literal($"mov eax, {testin}")
                    .Literal($"mov ebx, 5")
                    .Literal($"add eax, ebx")
                    .Literal($"mov [{(uint)&testin}], eax") // Store result (testin + 5) back to testin
                    .Literal("ret")
                    .Execute();

                logger.Log($"Assembly script executed successfully. Result: {testin}");
            }
            catch (Exception ex)
            {
                logger.Log($"Error executing assembly script: {ex.Message}");
            }

            
            

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
