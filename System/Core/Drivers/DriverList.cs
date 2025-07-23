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
    }
}
