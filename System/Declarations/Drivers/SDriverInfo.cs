using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Declarations.Drivers
{
    public struct SDriverInfo
    {
        /// <summary>
        /// The name of the driver.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The version of the driver.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// A brief description of the driver.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The manufacturer of the driver, which could be a company or an individual.
        /// </summary>
        public string Manufacturer { get; set; }

        /// <summary>
        /// The unique identifier for the device that this driver is associated with. (GUID or similar identifier)
        /// </summary>
        public string DeviceID { get; set; }

        /// <summary>
        /// The type of driver, indicating whether it is built-in, loaded into memory, or stored on disk.
        /// </summary>
        public EDriverInstallType DriverInstallType { get; set; }

        /// <summary>
        /// The purpose of the driver, indicating its primary function or role in the system.
        /// </summary>
        public EDriverPurpose DriverPurpose { get; set; }

        /// <summary>
        /// The date and time when the driver was installed on the system.
        /// </summary>
        public DateTime InstallationDate { get; set; }

        /// <summary>
        /// Indicates whether the driver is currently active or not.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// The path to the driver file, if applicable.
        /// </summary>
        public string FilePath { get; set; }

        public SDriverInfo(string name, string version, string description, string manufacturer, string deviceId, EDriverInstallType driverInstallType, EDriverPurpose driverPurpose, DateTime installationDate, bool isActive, string filePath)
        {
            Name = name;
            Version = version;
            Description = description;
            Manufacturer = manufacturer;
            DeviceID = deviceId;
            DriverInstallType = driverInstallType;
            DriverPurpose = driverPurpose;
            InstallationDate = installationDate;
            IsActive = isActive;
            FilePath = filePath;
        }

        public override string ToString()
        {
            return $"Driver Info:\n" +
                   $"Name: {Name}\n" +
                   $"Version: {Version}\n" +
                   $"Description: {Description}\n" +
                   $"Manufacturer: {Manufacturer}\n" +
                   $"Device ID: {DeviceID}\n" +
                   $"Install Type: {GetDriverInstallString(DriverInstallType)}\n" +
                   $"Purpose: {GetDriverPurposeString(DriverPurpose)}\n" +
                   $"Is Active: {IsActive}\n" +
                   $"File Path: {FilePath}";
        }

        public static string GetDriverInstallString(EDriverInstallType driverInstallType)
        {
            return driverInstallType switch
            {
                EDriverInstallType.BuiltIn => "Built-in Driver",
                EDriverInstallType.Memory => "Memory Driver",
                EDriverInstallType.Stored => "Stored Driver",
                _ => "Unknown Driver Install Type"
            };
        }

        public static string GetDriverPurposeString(EDriverPurpose driverPurpose)
        {
            return driverPurpose switch
            {
                EDriverPurpose.Other => "Other Driver",
                EDriverPurpose.Graphics => "Graphics Driver",
                EDriverPurpose.Audio => "Audio Driver",
                EDriverPurpose.Network => "Network Driver",
                EDriverPurpose.Storage => "Storage Driver",
                EDriverPurpose.Input => "Input Driver",
                EDriverPurpose.Output => "Output Driver",
                EDriverPurpose.System => "System Driver",
                EDriverPurpose.Communication => "Communication Driver",
                EDriverPurpose.Security => "Security Driver",
                EDriverPurpose.Printing => "Printing Driver",
                EDriverPurpose.Virtualization => "Virtualization Driver",
                EDriverPurpose.PowerManagement => "Power Management Driver",
                EDriverPurpose.Filesystem => "Filesystem Driver",
                EDriverPurpose.Bluetooth => "Bluetooth Driver",
                EDriverPurpose.USB => "USB Driver",
                EDriverPurpose.Serial => "Serial Driver",
                EDriverPurpose.Parallel => "Parallel Driver",
                EDriverPurpose.Sensor => "Sensor Driver",
                EDriverPurpose.Battery => "Battery Driver",
                EDriverPurpose.Clock => "Clock Driver",
                EDriverPurpose.Camera => "Camera Driver",
                EDriverPurpose.Display => "Display Driver",
                EDriverPurpose.Touch => "Touch Driver",
                EDriverPurpose.Firmware => "Firmware Driver",
                EDriverPurpose.Diagnostic => "Diagnostic Driver",
                EDriverPurpose.Debug => "Debug Driver",
                EDriverPurpose.Bus => "Bus Driver",
                EDriverPurpose.HID => "Human Interface Device Driver",
                EDriverPurpose.Wireless => "Wireless Driver",
                EDriverPurpose.RAID => "RAID Controller Driver",
                EDriverPurpose.TPM => "Trusted Platform Module Driver",
                EDriverPurpose.SmartCard => "Smart Card Driver",
                EDriverPurpose.Cooling => "Cooling System Driver",
                EDriverPurpose.Lighting => "Lighting Control Driver",
                EDriverPurpose.Accessibility => "Accessibility Driver",
                EDriverPurpose.Cryptography => "Cryptographic Driver",
                EDriverPurpose.Hypervisor => "Hypervisor Driver",
                EDriverPurpose.KernelExtension => "Kernel Extension",
                EDriverPurpose.Boot => "Boot Driver",
                EDriverPurpose.Hotplug => "Hotplug Controller Driver",
                EDriverPurpose.Update => "Driver Update Service",
                EDriverPurpose.Voice => "Voice Processing Driver",
                EDriverPurpose.Gesture => "Gesture Input Driver",
                EDriverPurpose.NFC => "NFC Driver",
                EDriverPurpose.Location => "Location Services Driver",
                EDriverPurpose.Thunderbolt => "Thunderbolt Driver",
                EDriverPurpose.FireWire => "FireWire Driver",

                _ => "Unknown Driver Type"
            };
        }

    }
}
