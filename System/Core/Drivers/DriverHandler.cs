using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Core.Drivers
{

    public static class DriverHandler
    {
        public struct GenericDriverBundle
        {
            public GenericLogger gLogger { get; set; }
            public RuntimeExecution gRuntimeExecution { get; set; }
            public sACPI gsACPI { get; set; }
        }

        public static GenericDriverBundle InstallAll()
        {
            GenericDriverBundle bundle = new GenericDriverBundle
            {
                gLogger = new GenericLogger(),
                gRuntimeExecution = new RuntimeExecution(),
                gsACPI = new sACPI()
            };

            var status = bundle.gLogger.Install();
            if (!status.IsSuccess)
            {
                Console.WriteLine($"Logger installation failed: {status.Message}");
            }

            status = bundle.gRuntimeExecution.Install();
            if (!status.IsSuccess)
            {
                Console.WriteLine($"Runtime Execution installation failed: {status.Message}");
            }

            status = bundle.gsACPI.Install();
            if (!status.IsSuccess)
            {
                Console.WriteLine($"sACPI installation failed: {status.Message}");
            }

            return bundle;
        }

        public static void RestartAll(GenericDriverBundle bundle)
        {
            bundle.gLogger.Restart();
            bundle.gRuntimeExecution.Restart();
            bundle.gsACPI.Restart();
        }
        public static void StartAll(GenericDriverBundle bundle)
        {
            bundle.gLogger.Start();
            bundle.gRuntimeExecution.Start();
            bundle.gsACPI.Start();
        }
        public static void StopAll(GenericDriverBundle bundle)
        {
            bundle.gLogger.Stop();
            bundle.gRuntimeExecution.Stop();
            bundle.gsACPI.Stop();
        }
        public static void UninstallAll(GenericDriverBundle bundle)
        {
            bundle.gLogger.Uninstall();
            bundle.gRuntimeExecution.Uninstall();
            bundle.gsACPI.Uninstall();
        }
    }
}
