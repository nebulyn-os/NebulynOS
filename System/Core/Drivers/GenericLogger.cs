using Nebulyn.System.Declarations.Drivers;
using Nebulyn.System.Declarations.Generic;
using Nebulyn.System.Derivatives.Drivers;
using System;
using System.Collections.Generic;

namespace Nebulyn.System.Core.Drivers
{
    public class GenericLogger : DriverBase
    {
        private List<(DateTime Timestamp, string Message)> _logs;

        protected override bool IsActive
        {
            get => _logs != null;
            set
            {
                if (value && _logs == null)
                    _logs = new List<(DateTime, string)>();
                else if (!value)
                    _logs = null;
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
                isActive: IsActive,
                filePath: string.Empty
            );
        }

        public override SGenericStatus Install()
        {
            if (IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is already installed and active.");

            DriverList.RegisterDriver(this);
            return SGenericStatus.Success("Generic Logger installed successfully.");
        }

        public override SGenericStatus Restart()
        {
            if (!IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is not active and cannot be restarted.");

            _logs.Clear();
            return SGenericStatus.Success("Generic Logger restarted successfully.");
        }

        public override SGenericStatus Start()
        {
            if (IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is already active.");

            IsActive = true;
            return SGenericStatus.Success("Generic Logger started successfully.");
        }

        public override SGenericStatus Stop()
        {
            if (!IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is not active and cannot be stopped.");

            IsActive = false;
            return SGenericStatus.Success("Generic Logger stopped successfully.");
        }

        public override SGenericStatus Uninstall()
        {
            if (IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is active and cannot be uninstalled.");

            _logs = null;
            DriverList.UnregisterDriver(this);
            return SGenericStatus.Success("Generic Logger uninstalled successfully.");
        }

        public SGenericStatus Log(string message)
        {
            if (!IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is not active and cannot log messages.");

            if (string.IsNullOrWhiteSpace(message))
                return SGenericStatus.Failure(EGenericResult.InvalidArgument, "Log message cannot be null or empty.");

            _logs.Add((DateTime.UtcNow, message));
            return SGenericStatus.Success("Message logged successfully.");
        }

        public IReadOnlyList<string> GetLogs()
            => _logs == null ? Array.Empty<string>() : _logs.ConvertAll(l => l.Message);

        public IReadOnlyList<DateTime> GetLogTimestamps()
            => _logs == null ? Array.Empty<DateTime>() : _logs.ConvertAll(l => l.Timestamp);

        public SGenericStatus ClearLogs()
        {
            if (!IsActive)
                return SGenericStatus.Failure(EGenericResult.InvalidState, "Generic Logger is not active and cannot clear logs.");

            _logs.Clear();
            return SGenericStatus.Success("Logs cleared successfully.");
        }

        public IReadOnlyList<string> GetCompiledLogs()
        {
            if (_logs == null || _logs.Count == 0)
                return Array.Empty<string>();

            var compiled = new string[_logs.Count];
            for (int i = 0; i < _logs.Count; i++)
            {
                compiled[i] = $"{_logs[i].Timestamp:o}: {_logs[i].Message}";
            }
            return compiled;
        }
    }
}
