using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Management;
using System.Runtime.InteropServices;

namespace TCUnlocker
{
    class Unlocker
    {
        // Imports from Win32 API.
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern void SendMessage(IntPtr hWnd, int msg, int wParam, System.Text.StringBuilder text);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr window, EnumWindowProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, UInt32 Msg, int wParam, int lParam);

        // Reference type for the EnumChildWIndows callback.
        private delegate bool EnumWindowProc(IntPtr hwnd, IntPtr lParam);

        // Get text from window message.
        private const UInt16 WM_GETTEXT = 0x000D;

        // Windows key pressed message.
        private const UInt16 WM_KEYDOWN = 0x0100;

        // Virtual key codes.
        private const UInt16 VK_1 = 0x31;  // 1
        private const UInt16 VK_2 = 0x32;  // 2
        private const UInt16 VK_3 = 0x33;  // 3

        private ManagementEventWatcher eventWatcher;
        private string processName, mainWindowName;
        private int attempts, sleepTime;

        public Unlocker(string processName, string mainWindowName, int attempts, int sleepTime)
        {
            // Assigns variables to this instance.
            this.processName = processName;
            this.mainWindowName = mainWindowName;
            this.attempts = attempts;
            this.sleepTime = sleepTime;

            // Create an event watcher which is triggered when a new process started.
            this.eventWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            // Add handler for new events.
            this.eventWatcher.EventArrived += new EventArrivedEventHandler(EventHandler);
        }

        public void StartWatching()
        {
            this.eventWatcher.Start();
        }

        public void StopWatching()
        {
            this.eventWatcher.Stop();
        }

        // Handle the event watcher event arrived event. So many event here O_o
        private void EventHandler(object sender, EventArrivedEventArgs e)
        {
            // Get the name of the process.
            string processName = e.NewEvent.Properties["ProcessName"].Value.ToString();

            // If it's a TCMD process, try to unlock it.
            if (processName.ToUpper().Contains(this.processName))
            {
                UnlockWindow();
            }
        }

        // Unlock the window.
        private void UnlockWindow()
        {
            // Get handle for the main (registration) window.
            IntPtr mainWindow = FindWindow(this.mainWindowName, null);

            // If not found, return.
            if (mainWindow == IntPtr.Zero)
            {
                return;
            }

            // StringBuilder for string operations.
            StringBuilder sb = new StringBuilder(255);

            // The number to unlock.
            ushort unlockNumber = 0;

            // Acceptable numbers.
            ushort[] unlockNumbers = new ushort[] { 1, 2, 3 };

            // If found, iterate over the child windows and try to find the one with a single number.
            // 10 attempts with delay. It's necessary because the speed of loading is not always the same.
            for (int i = 0; i < this.attempts; ++i)
            {
                // Child handlers.
                List<IntPtr> children = GetAllChildHandles(mainWindow);

                // We need at least 7 children.
                if (children.Count > 7)
                {
                    // Iterate over the children.
                    foreach (IntPtr child in children)
                    {
                        // Send a GETTEXT message to the child window.
                        SendMessage(child, WM_GETTEXT, sb.Capacity, sb);
                        // Try to parse the result to a 16-bit unsigned integer.
                        bool parseResult = ushort.TryParse(sb.ToString(), out unlockNumber);
                        // Clear the string builder for later use.
                        sb.Clear();

                        // If found a single number, break the loop.
                        if (parseResult && unlockNumbers.Contains(unlockNumber))
                        {
                            goto NUMBER_FOUND;
                        }
                    }
                }

                // Sleep the thread for X millisec, to let the system load missing windows.
                Thread.Sleep(this.sleepTime);
            }

            // If we reach this code it means we finished with all the iterations and didn't find anything.
            return;

            // With goto we can jump here and it means we found something.
            NUMBER_FOUND:
            // Unlock or return, based on the number we have.
            switch (unlockNumber)
            {
                case 1:
                    PostMessage(mainWindow, WM_KEYDOWN, VK_1, 0);
                    break;
                case 2:
                    PostMessage(mainWindow, WM_KEYDOWN, VK_2, 0);
                    break;
                case 3:
                    PostMessage(mainWindow, WM_KEYDOWN, VK_3, 0);
                    break;
            }
        }

        // Get all the child handles for a specific window.
        private List<IntPtr> GetAllChildHandles(IntPtr mainHandle)
        {
            List<IntPtr> childHandles = new List<IntPtr>();

            GCHandle gcChildhandlesList = GCHandle.Alloc(childHandles);
            IntPtr pointerChildHandlesList = GCHandle.ToIntPtr(gcChildhandlesList);

            try
            {
                EnumWindowProc childProc = new EnumWindowProc(EnumWindow);
                EnumChildWindows(mainHandle, childProc, pointerChildHandlesList);
            }
            finally
            {
                gcChildhandlesList.Free();
            }

            return childHandles;
        }

        private bool EnumWindow(IntPtr hWnd, IntPtr lParam)
        {
            GCHandle gcChildhandlesList = GCHandle.FromIntPtr(lParam);

            if (gcChildhandlesList == null || gcChildhandlesList.Target == null)
            {
                return false;
            }

            List<IntPtr> childHandles = gcChildhandlesList.Target as List<IntPtr>;
            childHandles.Add(hWnd);

            return true;
        }
    }
}
