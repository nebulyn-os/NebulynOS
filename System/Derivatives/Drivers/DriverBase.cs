using Nebulyn.System.Declarations.Drivers;
using Nebulyn.System.Declarations.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Derivatives.Drivers
{
    public abstract class DriverBase : IDriver
    {
        protected abstract bool IsActive { get; set; }

        public abstract SGenericStatus Install();
        public abstract SGenericStatus Uninstall();
        public abstract SDriverInfo Identify();
        public abstract SGenericStatus Start();
        public abstract SGenericStatus Stop();
        public abstract SGenericStatus Restart();
        protected virtual void Log(string message)
        {
            // Default logging implementation
            Console.WriteLine(message);
        }
    }
}
