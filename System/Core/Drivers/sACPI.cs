using Nebulyn.System.Declarations.Drivers;
using Nebulyn.System.Declarations.Generic;
using Nebulyn.System.Derivatives.Drivers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Core.Drivers
{
    public class sACPI : DriverBase
    {
        protected override bool IsActive { get; set; }

        private GenericLogger Logger;

        private const string RSDP_SIGNATURE = "RSD PTR ";
        private const uint RSDP_SEARCH_START = 0x000E0000;
        private const uint RSDP_SEARCH_END = 0x00100000;
        private const byte RSDP_INCREMENT = 16;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct RSDPDescriptor
        {
            public fixed byte Signature[8];
            public byte Checksum;
            public fixed byte OemId[6];
            public byte Revision;
            public uint RsdtAddress;
        }

        public unsafe void* FindRSDT()
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("sACPI is not active. Please start the driver before attempting to find RSDT.");
            }
            for (uint address = RSDP_SEARCH_START; address < RSDP_SEARCH_END; address += RSDP_INCREMENT)
            {
                var ptr = (byte*)address;
                if (IsRSDP(ptr))
                {
                    return ptr;
                }
            }
            return null;
        }

        public unsafe RSDPDescriptor GetRSDPDescriptor(void* rsdpPtr)
        {
            if (!IsActive)
            {
                throw new InvalidOperationException("sACPI is not active. Please start the driver before attempting to get RSDP Descriptor.");
            }
            if (rsdpPtr == null)
            {
                throw new ArgumentNullException(nameof(rsdpPtr), "RSDP pointer cannot be null.");
            }
            RSDPDescriptor* rsdpDescriptor = (RSDPDescriptor*)rsdpPtr;
            return *rsdpDescriptor;
        }

        private unsafe bool IsRSDP(byte* ptr)
        {
            if (ptr == null)
            {
                throw new ArgumentNullException(nameof(ptr), "Pointer cannot be null.");
            }
            for (int i = 0; i < RSDP_SIGNATURE.Length; i++)
            {
                if (ptr[i] != RSDP_SIGNATURE[i])
                {
                    return false;
                }
            }
            return true;
        }

        public override SDriverInfo Identify()
        {
            return new SDriverInfo(
                    name: "sACPI",
                    version: "1.0.0",
                    description: "ACPI Handler",
                    manufacturer: "Nebulyn Systems",
                    deviceId: "04b0d9e2-7333-4704-8ee6-c0da0d38db41",
                    driverInstallType: EDriverInstallType.BuiltIn,
                    driverPurpose: EDriverPurpose.PowerManagement,
                    installationDate: DateTime.UtcNow,
                    isActive: IsActive,
                    filePath: ""
                );
        }

        public override SGenericStatus Install()
        {
            if (IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "sACPI is already installed and active.");
            
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
            Logger.DriverLog(this, "sACPI driver installed successfully.");
            return SGenericStatus.Success("sACPI installed successfully.");
        }

        public override SGenericStatus Restart()
        {
            if (!IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "sACPI is not active and cannot be restarted.");
            IsActive = false;
            IsActive = true;

            return SGenericStatus.Success("sACPI restarted successfully.");
        }

        public override SGenericStatus Start()
        {
            if (IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "sACPI is active and cannot be started.");

            IsActive = true;

            Logger.DriverLog(this, "sACPI driver started successfully.");
            return SGenericStatus.Success("sACPI started successfully.");
        }

        public override SGenericStatus Stop()
        {
            if (!IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "sACPI is not active and cannot be stopped.");
            IsActive = false;
            Logger.DriverLog(this, "sACPI driver stopped successfully.");
            return SGenericStatus.Success("sACPI stopped successfully.");
        }

        public override SGenericStatus Uninstall()
        {
            if (IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "sACPI is active and cannot be uninstalled.");
            IsActive = false;
            DriverList.UnregisterDriver(this);
            Logger.DriverLog(this, "sACPI driver uninstalled successfully.");
            return SGenericStatus.Success("sACPI uninstalled successfully.");
        }
    }
}
