namespace LockPicker;

internal static class Program
{
    /// <summary>
    /// Точка входа в приложение.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
