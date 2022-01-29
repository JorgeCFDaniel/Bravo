﻿namespace Sqlbi.Bravo.Infrastructure
{
    using Sqlbi.Bravo.Infrastructure.Configuration.Settings;
    using Sqlbi.Bravo.Infrastructure.Helpers;
    using Sqlbi.Bravo.Infrastructure.Messages;
    using Sqlbi.Bravo.Infrastructure.Windows.Interop;
    using System;
    using System.Text;
    using System.Text.Json;
    using System.Threading;

    internal class AppInstance : IDisposable
    {
        private readonly Mutex _instanceMutex;
        private readonly bool _instanceOwned;

        private bool _disposed;

        public AppInstance()
        {
            var appName = AppEnvironment.IsPackagedAppInstance ? AppEnvironment.ApplicationStoreAliasName : AppEnvironment.ApplicationName;

            _instanceMutex = new Mutex(initiallyOwned: true, name: $"Local\\Mutex{ appName }4f9wB", out _instanceOwned);

            GC.KeepAlive(_instanceMutex);

            if (_instanceOwned)
            {
                NotificationHelper.RegisterNotificationHandler();
            }
        }

        /// <summary>
        ///  Determines if the current instance is the only running instance of the application or if another instance is already running
        /// </summary>
        /// <returns>true if the current instance is the only running instance of the application; otherwise, false</returns>
        public bool IsOwned => _instanceOwned;

        /// <summary>
        /// Sends a message to the primary instance owner notifying it of startup arguments for the current instance
        /// </summary>
        public void NotifyOwner()
        {
            var startupSettings = StartupSettings.CreateFromCommandLineArguments();
            var message = AppInstanceStartupMessage.CreateFrom(startupSettings);

            var hWnd = User32.FindWindow(lpClassName: null, lpWindowName: AppEnvironment.ApplicationMainWindowTitle);
            if (hWnd != IntPtr.Zero)
            {
                var json = JsonSerializer.Serialize(message);
                var bytes = Encoding.Unicode.GetBytes(json);

                User32.COPYDATASTRUCT copyData;
                copyData.dwData = (IntPtr)100;
                copyData.lpData = json;
                copyData.cbData = bytes.Length + 1;

                _ = User32.SendMessage(hWnd, WindowMessage.WM_COPYDATA, wParam: 0, ref copyData);
            }
        }

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_instanceOwned)
                        _instanceMutex.ReleaseMutex();

                    _instanceMutex.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
