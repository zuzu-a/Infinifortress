using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Infiniminer
{
    /// <summary>
    /// Cross-platform implementation of platform services
    /// </summary>
    public class CrossPlatformServices : IPlatformServices
    {
        private static CrossPlatformServices _instance;
        public static CrossPlatformServices Instance => _instance ??= new CrossPlatformServices();
        
        public bool IsClipboardSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || 
                                          RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                                          RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public void ShowError(string message, Exception exception = null)
        {
            // Always log to console for cross-platform compatibility
            Console.WriteLine($"ERROR: {message}");
            if (exception != null)
            {
                Console.WriteLine($"Exception: {exception.Message}");
                Console.WriteLine($"Stack Trace: {exception.StackTrace}");
            }
            
            // Platform-specific error display
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var process = new Process();
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = $"/c msg * /time:0 ERROR: {message}";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                }
                catch
                {
                    // Fallback to console if msg command fails
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
            else
            {
                // Unix-like systems just use console
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
        }

        public void ShowMessage(string title, string message)
        {
            Console.WriteLine($"{title}: {message}");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var process = new Process();
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = $"/c msg * /time:0 {title}: {message}";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.Start();
                }
                catch
                {
                    // Fallback to console
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
            else
            {
                // Unix-like systems use console
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
        }

        public async Task<string> GetClipboardTextAsync()
        {
            if (!IsClipboardSupported)
                return string.Empty;

            try
            {
                return await TextCopy.ClipboardService.GetTextAsync() ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clipboard read failed: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task SetClipboardTextAsync(string text)
        {
            if (!IsClipboardSupported || string.IsNullOrEmpty(text))
                return;

            try
            {
                await TextCopy.ClipboardService.SetTextAsync(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clipboard write failed: {ex.Message}");
            }
        }

        public void ConfigureConsole(int width = 80, int height = 30)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows-specific console configuration
                    // Get current window position
                    var windowWidth = Console.WindowWidth;
                    var windowHeight = Console.WindowHeight;
                    var windowLeft = Console.WindowLeft;
                    var windowTop = Console.WindowTop;

                    // First set window size to minimum to ensure buffer can be resized
                    Console.SetWindowSize(1, 1);
                    
                    // Set buffer size
                    width = Math.Min(width, short.MaxValue - 1);
                    height = Math.Min(height, short.MaxValue - 1);
                    Console.SetBufferSize(width, height);
                    
                    // Restore window size
                    var targetWidth = Math.Min(windowWidth, Console.LargestWindowWidth);
                    var targetHeight = Math.Min(windowHeight, Console.LargestWindowHeight);
                    Console.SetWindowSize(targetWidth, targetHeight);
                    
                    // Restore window position if possible
                    try
                    {
                        Console.WindowLeft = windowLeft;
                        Console.WindowTop = windowTop;
                    }
                    catch
                    {
                        // Ignore position restore errors
                    }
                }
                else
                {
                    // For Unix-like systems, we can't reliably set console size
                    // but we can still set other properties
                    Console.CursorVisible = true;
                }
                
                Console.Title = "Infiniminer";
            }
            catch (Exception ex)
            {
                // Console configuration is not critical, just log if it fails
                Console.WriteLine($"Console configuration failed: {ex.Message}\nActual value was {width}.");
            }
        }

        public void RestartApplication()
        {
            string executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(executablePath))
            {
                ShowError("Could not determine application path for restart");
                return;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = executablePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Unix-like systems
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "mono",
                        Arguments = executablePath,
                        UseShellExecute = false
                    });
                }
                
                // Exit current process
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                ShowError("Failed to restart application", ex);
            }
        }
    }
} 