using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebulyn.System.Declarations.Generic
{
    public struct SGenericStatus
    {
        public EGenericResult Result { get; set; }
        public string Message { get; set; }
        public SGenericStatus(EGenericResult result, string message = "")
        {
            Result = result;
            Message = message;
        }

        public static SGenericStatus Success(string message = "")
        {
            return new SGenericStatus(EGenericResult.Success, message);
        }

        public static SGenericStatus Failure(EGenericResult error = EGenericResult.UnknownError, string message = "")
        {
            return new SGenericStatus(error, "\x1b[31m"+message+ "\x1b[0m");
        }

        public bool IsSuccess
        {
            get { return Result == EGenericResult.Success; }
        }
    }
}
