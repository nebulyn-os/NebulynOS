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
    public static class DriverList
    {
        private static List<IDriver> drivers = new List<IDriver>();
        public static SGenericStatus RegisterDriver(IDriver driver)
        {
            if (driver == null)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidArgument, "Driver cannot be null.");
            }
            if (!drivers.Any(d => d.Identify().DeviceID == driver.Identify().DeviceID))
            {
                drivers.Add(driver);
            }
            else
            {
                return SGenericStatus.Failure(EGenericResult.AlreadyExists, "Driver with the same DeviceID already exists.");
            }
            return SGenericStatus.Success("Driver registered successfully.");
        }
        public static SGenericStatus UnregisterDriver(IDriver driver)
        {
            if (driver == null)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidArgument, "Driver cannot be null.");
            }
            var existingDriver = drivers.FirstOrDefault(d => d.Identify().DeviceID == driver.Identify().DeviceID);
            if (existingDriver != null)
            {
                drivers.Remove(existingDriver);
                return SGenericStatus.Success("Driver unregistered successfully.");
            }
            return SGenericStatus.Failure(EGenericResult.NotFound, "Driver not found.");
        }
        public static IEnumerable<IDriver> GetDrivers()
        {
            return drivers.AsReadOnly();
        }
        public static void ListDrivers()
        {
            if (drivers.Count == 0)
            {
                Console.WriteLine("No drivers registered.");
                return;
            }

            int nameWidth = Math.Max(15, drivers.Max(d => d.Identify().Name.Length));
            int manufacturerWidth = Math.Max(20, drivers.Max(d => d.Identify().Manufacturer.Length));
            int purposeWidth = Math.Max(20, drivers.Max(d => SDriverInfo.GetDriverPurposeString(d.Identify().DriverPurpose).Length));

            string separator = "+"
                + new string('-', nameWidth + 2) + "+"
                + new string('-', manufacturerWidth + 2) + "+"
                + new string('-', purposeWidth + 2) + "+";

            // Header
            Console.WriteLine(separator);
            Console.WriteLine($"| {"Name".PadRight(nameWidth)} | {"Manufacturer".PadRight(manufacturerWidth)} | {"Purpose".PadRight(purposeWidth)} |");
            Console.WriteLine(separator);
            Console.ForegroundColor = ConsoleColor.Gray;
            // Rows
            foreach (var driver in drivers)
            {
                var info = driver.Identify();
                var purpose = SDriverInfo.GetDriverPurposeString(info.DriverPurpose);

                Console.WriteLine($"| {info.Name.PadRight(nameWidth)} | {info.Manufacturer.PadRight(manufacturerWidth)} | {purpose.PadRight(purposeWidth)} |");
                Console.WriteLine(separator);
            }
            Console.ResetColor();
        }

    }
}
