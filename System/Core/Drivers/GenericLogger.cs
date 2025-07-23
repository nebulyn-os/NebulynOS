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
    public class GenericLogger : DriverBase
    {
        private string[] LoggedData = null;
        private DateTime[] DateTimes = null;

        protected override bool IsActive
        {
            get => LoggedData != null;
            set
            {
                if (value)
                {
                    LoggedData = new string[0];
                }
                else
                {
                    LoggedData = null;
                }
            }
        }

        public override SDriverInfo Identify()
        {
            return new SDriverInfo(
                    name: "Generic Logger",
                    version: "1.0.0",
                    description: "A generic logging driver for Nebulyn System.",
                    manufacturer: "Nebulyn Systems",
                    deviceId: "2d6aa0a6-b8f1-4321-b0b5-0a71520edae9",
                    driverInstallType: EDriverInstallType.BuiltIn,
                    driverPurpose: EDriverPurpose.Output,
                    installationDate: DateTime.UtcNow,
                    isActive: this.IsActive,
                    filePath: ""
                );
        }

        public override SGenericStatus Install()
        {
            LoggedData = new string[0];
            DateTimes = new DateTime[0];

            DriverList.RegisterDriver(this);

            return SGenericStatus.Success("Generic Logger installed successfully.");
        }

        public override SGenericStatus Restart()
        {
            if (!this.IsActive)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is not active and cannot be restarted.");
            }

            LoggedData = new string[0];
            DateTimes = new DateTime[0];

            return SGenericStatus.Success("Generic Logger restarted successfully.");
        }

        public override SGenericStatus Start()
        {
            if (this.IsActive)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is already active.");
            }

            this.IsActive = true;

            return SGenericStatus.Success("Generic Logger started successfully.");
        }

        public override SGenericStatus Stop()
        {
            if (!this.IsActive)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is not active and cannot be stopped.");
            }

            this.IsActive = false;

            return SGenericStatus.Success("Generic Logger stopped successfully.");
        }

        public override SGenericStatus Uninstall()
        {
            if (this.IsActive)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is active and cannot be uninstalled.");
            }

            LoggedData = null;
            DateTimes = null;

            DriverList.UnregisterDriver(this);

            return SGenericStatus.Success("Generic Logger uninstalled successfully.");
        }

        public SGenericStatus Log(string message)
        {
            if (!this.IsActive)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is not active and cannot log messages.");
            }
            if (string.IsNullOrEmpty(message))
            {
                return SGenericStatus.Failure(EGenericResult.InvalidArgument, "Log message cannot be null or empty.");
            }
            Array.Resize(ref LoggedData, LoggedData.Length + 1);
            LoggedData[LoggedData.Length - 1] = message;

            Array.Resize(ref DateTimes, DateTimes.Length + 1);
            DateTimes[DateTimes.Length - 1] = DateTime.UtcNow;

            return SGenericStatus.Success("Message logged successfully.");
        }

        public string[] GetLogs()
        {
            return LoggedData;
        }

        public DateTime[] GetLogTimestamps()
        {
            return DateTimes;
        }

        public SGenericStatus ClearLogs()
        {
            if (!this.IsActive)
            {
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is not active and cannot clear logs.");
            }
            LoggedData = new string[0];
            DateTimes = new DateTime[0];
            return SGenericStatus.Success("Logs cleared successfully.");
        }

        public string[] GetCompiledLogs()
        {
            // Format as TIME: MESSAGE
            if (LoggedData == null || LoggedData.Length == 0)
            {
                return new string[0];
            }
            string[] compiledLogs = new string[LoggedData.Length];
            for (int i = 0; i < LoggedData.Length; i++)
            {
                compiledLogs[i] = $"{DateTimes[i].ToString("o")}: {LoggedData[i]}";
            }
            return compiledLogs;
        }
    }
}