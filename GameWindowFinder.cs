using System.Diagnostics;

namespace ArknightsPresence.Capture;

public static class GameWindowFinder
{
    public static Process? FindArknights()
    {
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                process.Refresh();

                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    process.Dispose();
                    continue;
                }

                bool nameMatches =
                    process.ProcessName.Contains(
                        "Arknights",
                        StringComparison.OrdinalIgnoreCase);

                bool titleMatches =
                    process.MainWindowTitle.Contains(
                        "Arknights",
                        StringComparison.OrdinalIgnoreCase);

                if (nameMatches || titleMatches)
                {
                    return process;
                }

                process.Dispose();
            }
            catch
            {
                process.Dispose();
            }
        }

        return null;
    }

    public static void PrintWindows()
    {
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                Console.WriteLine(
                    $"{process.ProcessName,-30} " +
                    $"0x{process.MainWindowHandle.ToInt64():X} " +
                    $"{process.MainWindowTitle}");
            }
            catch
            {
                // Ignore inaccessible Windows processes.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}