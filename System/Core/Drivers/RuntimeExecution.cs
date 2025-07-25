using Cosmos.Core.Memory;
using Nebulyn.System.Declarations.Drivers;
using Nebulyn.System.Declarations.Generic;
using Nebulyn.System.Derivatives.Drivers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Core.Drivers
{
    public class RuntimeExecution : DriverBase
    {
        private unsafe byte* RuntimeMemory = null;
        private GenericLogger Logger;

        protected unsafe override bool IsActive
        {
            get => RuntimeMemory != null;
            set
            {
                if (value)
                {
                    RuntimeMemory = Heap.Alloc(2);
                    // Set to RET instruction
                    *RuntimeMemory = (byte)SingleInstruction.NOP;
                    *(RuntimeMemory + 1) = (byte)SingleInstruction.RET;
                }
                else
                {
                    if (RuntimeMemory != null)
                    {
                        Heap.Free(RuntimeMemory);
                    }
                    RuntimeMemory = null;
                }
            }
        }

        public override SDriverInfo Identify()
        {
            return new SDriverInfo(
                    name: "Runtime Executor",
                    version: "1.0.0",
                    description: "A code execution driver for Nebulyn System.",
                    manufacturer: "Nebulyn Systems",
                    deviceId: "ba19bd0d-eab3-406d-9ef3-72ce5d7ec13d",
                    driverInstallType: EDriverInstallType.BuiltIn,
                    driverPurpose: EDriverPurpose.KernelExtension,
                    installationDate: DateTime.UtcNow,
                    isActive: this.IsActive,
                    filePath: ""
                );
        }

        public override SGenericStatus Install()
        {
            if (IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is already installed and active.");
                return SGenericStatus.Failure(EGenericResult.AlreadyExists, "Runtime Executor is already installed.");
            }

            // Try to aquire a GenericLogger instance
            var status = DriverList.GetDriverById("2d6aa0a6-b8f1-4321-b0b5-0a71520edae9", out IDriver aDriver);

            if (status.IsSuccess)
            {
                Logger = aDriver as GenericLogger;
                if (Logger == null)
                {
                    return SGenericStatus.Failure(EGenericResult.InvalidState, "Failed to acquire GenericLogger instance.");
                }
            }
            else
            {
                return SGenericStatus.Failure(EGenericResult.NotFound, "GenericLogger driver not found.");
            }

            DriverList.RegisterDriver(this);

            Logger.DriverLog(this, "Runtime Executor installed successfully.");
            return SGenericStatus.Success("Runtime Executor installed successfully.");
        }

        public override SGenericStatus Restart()
        {
            if (!IsActive)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active and cannot be restarted.");
            }

            IsActive = false; // Deactivate first

            IsActive = true; // Reactivate

            Logger.DriverLog(this, "Runtime Executor restarted successfully.");
            return SGenericStatus.Success("Runtime Executor restarted successfully.");
        }

        public override SGenericStatus Start()
        {
            if (IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is already active.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is already active.");
            }
            IsActive = true; // Activate the runtime executor

            Logger.DriverLog(this, "Runtime Executor started successfully.");
            return SGenericStatus.Success("Runtime Executor started successfully.");
        }

        public override SGenericStatus Stop()
        {
            if (!IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is not active and cannot be stopped.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active.");
            }
            IsActive = false; // Deactivate the runtime executor
            Logger.DriverLog(this, "Runtime Executor stopped successfully.");
            return SGenericStatus.Success("Runtime Executor stopped successfully.");
        }

        public override SGenericStatus Uninstall()
        {
            if (!IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is not active and cannot be uninstalled.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active and cannot be uninstalled.");
            }
            IsActive = false; // Deactivate before uninstalling
            Logger.DriverLog(this, "Uninstalling Runtime Executor...");
            Logger = null; // Clear the logger reference
            DriverList.UnregisterDriver(this);
            return SGenericStatus.Success("Runtime Executor uninstalled successfully.");
        }

        public unsafe SGenericStatus SetInstruction(SingleInstruction instruction)
        {
            if (!IsActive)
            {
                Logger.DriverLog(this, "Runtime Executor is not active. Please start the driver first.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active. Please start the driver first.");
            }
            if (RuntimeMemory == null)
            {
                Logger.DriverLog(this, "Runtime memory is not allocated. Please ensure the driver is active.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime memory is not allocated. Please ensure the driver is active.");
            }
            // Set the instruction in the allocated memory
            *RuntimeMemory = (byte)instruction;
            return SGenericStatus.Success($"Instruction {(byte)instruction} set successfully in runtime memory.");
        }

        public unsafe SGenericStatus Execute()
        {
            if (!IsActive) {
                Logger.DriverLog(this, "Runtime Executor is not active. Please start the driver first.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime Executor is not active. Please start the driver first.");
            }
            if (RuntimeMemory == null)
            {
                Logger.DriverLog(this, "Runtime memory is not allocated. Please ensure the driver is active.");
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Runtime memory is not allocated. Please ensure the driver is active.");
            }
            // Execute the instruction by jumping to the memory location
            // Using unmanaged delegate ptr
            unsafe
            {
                Logger.DriverLog(this, $"Executing instruction at memory address: {(ulong)RuntimeMemory:X}");
                var instructionPtr = (delegate*<void>)RuntimeMemory;
                instructionPtr(); // Call the instruction
            }
            Logger.DriverLog(this, "Instruction executed successfully.");
            return SGenericStatus.Success("Instruction executed successfully.");
        }

        public enum SingleInstruction
        {
            // Single byte instructions
            /// <summary>
            /// Return from subroutine
            /// </summary>
            RET = 0xC3,

            /// <summary>
            /// Halt the CPU until the next interrupt
            /// </summary>
            HLT = 0xF4,

            /// <summary>
            /// No operation (do nothing)
            /// </summary>
            NOP = 0x90,

            /// <summary>
            /// Clear the carry flag
            /// </summary>
            CLC = 0xF8,

            /// <summary>
            /// Set the carry flag
            /// </summary>
            CLI = 0xFA,

            /// <summary>
            /// Clear the direction flag
            /// </summary>
            CLD = 0xFC,

            /// <summary>
            /// Breakpoint interrupt
            /// </summary>
            INT3 = 0xCC,


        }
    }
}
