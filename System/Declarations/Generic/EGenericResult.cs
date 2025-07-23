using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Declarations.Generic
{
    public enum EGenericResult
    {
        Success,
        UnknownError,
        NotFound,
        InvalidArgument,
        PermissionDenied,
        Timeout,
        AlreadyExists,
        NotSupported,
        OutOfMemory,
        OperationCanceled,
        NetworkError,
        DiskError,
        AuthenticationFailed,
        ResourceUnavailable,
        InvalidState,
        CorruptedData,
        UnsupportedOperation,
        InternalError,
        ConfigurationError,
        DependencyError,
        FeatureNotImplemented,
        ObjectStopped // Usually for drivers that are stopped or not running
    }
}
