using System;
using System.Threading.Tasks;

namespace Infiniminer
{
    /// <summary>
    /// Interface for platform-specific services to enable cross-platform compatibility
    /// </summary>
    public interface IPlatformServices
    {
        /// <summary>
        /// Display an error message to the user
        /// </summary>
        void ShowError(string message, Exception exception = null);
        
        /// <summary>
        /// Display an informational message to the user
        /// </summary>
        void ShowMessage(string title, string message);
        
        /// <summary>
        /// Get text from the system clipboard
        /// </summary>
        Task<string> GetClipboardTextAsync();
        
        /// <summary>
        /// Set text to the system clipboard
        /// </summary>
        Task SetClipboardTextAsync(string text);
        
        /// <summary>
        /// Configure console settings for the current platform
        /// </summary>
        void ConfigureConsole(int width = 80, int height = 30);
        
        /// <summary>
        /// Check if clipboard operations are supported on this platform
        /// </summary>
        bool IsClipboardSupported { get; }

        /// <summary>
        /// Restart the application in a platform-appropriate way
        /// </summary>
        void RestartApplication();
    }
} 