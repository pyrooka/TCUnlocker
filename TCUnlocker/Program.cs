using System.Threading;

namespace TCUnlocker
{
    class Program
    {
        static void Main(string[] args)
        {
            Unlocker tcUnlocker = new Unlocker(
                "TOTALCMD",         // Partial name of the process.
                "TNASTYNAGSCREEN",  // Main window name of the process.
                10,                 // Attempts for trying to find the child windows.
                100                 // Delay between the attempts.
            );

            tcUnlocker.StartWatching();

            new ManualResetEvent(false).WaitOne();

            tcUnlocker.StopWatching();
        }
    }
}
