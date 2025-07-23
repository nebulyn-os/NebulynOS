using Nebulyn.System.Declarations.Drivers;
using Nebulyn.System.Declarations.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Derivatives.Drivers
{
    public interface IDriver
    {
        SGenericStatus Install();
        SGenericStatus Uninstall();
        SDriverInfo Identify();
        SGenericStatus Start();
        SGenericStatus Stop();
        SGenericStatus Restart();
    }
}
