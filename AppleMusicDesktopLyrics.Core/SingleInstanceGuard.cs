using System;
using System.Threading;

namespace AppleMusicDesktopLyrics.Core
{
    public sealed class SingleInstanceGuard : IDisposable
    {
        private readonly Mutex mutex;
        private readonly EventWaitHandle activationSignal;

        private SingleInstanceGuard(Mutex mutex, EventWaitHandle activationSignal, bool hasHandle)
        {
            this.mutex = mutex;
            this.activationSignal = activationSignal;
            HasHandle = hasHandle;
        }

        public bool HasHandle { get; }

        public static SingleInstanceGuard TryAcquire(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Mutex name is required.", nameof(name));
            }

            bool createdNew;
            var mutex = new Mutex(true, name, out createdNew);
            var activationSignal = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
            return new SingleInstanceGuard(mutex, activationSignal, createdNew);
        }

        public void SignalExistingInstance()
        {
            activationSignal.Set();
        }

        public bool ConsumeActivationSignal(TimeSpan timeout)
        {
            return activationSignal.WaitOne(timeout);
        }

        public void Dispose()
        {
            if (HasHandle)
            {
                mutex.ReleaseMutex();
            }

            mutex.Dispose();
            activationSignal.Dispose();
        }
    }
}
