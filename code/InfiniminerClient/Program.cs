using System;

namespace Infiniminer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                using (InfiniminerGame game = new InfiniminerGame(args))
                {
                    game.Run();
                }
                }
                catch (Exception e)
                {
                CrossPlatformServices.Instance.ShowError("Application Error", e);
            }
        }
    }
}

