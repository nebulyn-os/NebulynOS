using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Declarations.Drivers
{
    /// <summary>
    /// Enumeration representing the type of driver in the system.
    /// </summary>
    public enum EDriverInstallType
    {
        /// <summary>
        /// Represents a built-in driver type, built into the system.
        /// </summary>
        BuiltIn = 0x0,
        /// <summary>
        /// Represents a driver that is loaded into memory, typically for immediate use.
        /// </summary>
        Memory = 0x1,
        /// <summary>
        /// Represents a driver that is stored on disk or in a persistent storage medium, typically for later use.
        /// </summary>
        Stored = 0x2
    }
}
