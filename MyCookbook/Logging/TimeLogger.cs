using Serilog;
using System.Reflection;

namespace MyCookbook.Logging
{
    public class TimeLogger : IDisposable
    {
        private bool disposedValue;

        public TimeSpan WarningTime { get; } = TimeSpan.FromMilliseconds(200);
        public TimeSpan ErrorTime { get; } = TimeSpan.FromMilliseconds(500);
        private MethodBase? Method { get; }
        private DateTime StartTime { get; }

        public TimeLogger(MethodBase? method)
        {
            Method = method;
            StartTime = DateTime.Now;
        }

        public TimeLogger(MethodBase? method, TimeSpan warningTime, TimeSpan errorTime) : this(method)
        {
            WarningTime = warningTime;
            ErrorTime = errorTime;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    LogIfTooLong();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void LogIfTooLong()
        {
            var disposedTime = DateTime.Now;
            var duration = disposedTime - StartTime;

            if (Method == null)
            {
                return;
            }

            if (duration > ErrorTime)
            {
                Log.Error($"Method execution of {Method.Name} of declaring type {Method.DeclaringType} took too long. Allowed time: {ErrorTime}. Actual time: {duration}.");
            }
            else if (duration > WarningTime)
            {
                Log.Warning($"Method execution of {Method.Name} of declaring type {Method.DeclaringType} took too long. Allowed time: {WarningTime}. Actual time: {duration}.");
            }
        }
    }
}
