using Timer = System.Timers.Timer;
using System.Timers;

namespace PhigrosShell.Utils;

internal static class LoadUtils
{
    private static Timer _loadTimer = new(100);
    private static bool _isLoading;
    private static bool _loadStopWriteLine = true;
    private static bool _isDone;
    private static bool _isError;
    private static string _loadingText = "-\\|/";
    private static int _loadingIndex;
    private static string _loadingSuffix = " ...";

    private static string _loadingDoneSuffix => Program.Localization["Done"];
    private static string _loadingErrorSuffix => Program.Localization["Error"];

    static LoadUtils()
    {
        _loadTimer.Elapsed += OnTimerElapsed;
        _loadTimer.AutoReset = true;
        _loadTimer.Enabled = true;
    }

    private static void ResetTimer()
    {
        _loadingIndex = 0;
        _isLoading = false;
        _isDone = false;
        _isError = false;
    }

    private static void UpdateLoadingContent(bool firstLoad = false)
    {
        if (!firstLoad)
        {
            for (int i = 0; i < _loadingSuffix.Length + 1; i++)
                Console.Write("\b");
        }
        Console.Write(_loadingText[_loadingIndex]);

        var color = Console.ForegroundColor;
        string suffix = _loadingSuffix;

        if (_isError)
        {
            color = ConsoleColor.Red;
            suffix = _loadingErrorSuffix;
            Console.Write("\b");
        }
        else if (_isDone)
        {
            color = ConsoleColor.Green;
            suffix = _loadingDoneSuffix;
            Console.Write("\b");
        }

        FluentConsole.Color(color).Text(suffix);
        _loadingIndex = (_loadingIndex + 1) % _loadingText.Length;
    }

    private static void OnTimerElapsed(object? sender = null, ElapsedEventArgs? e = null)
    {
        UpdateLoadingContent();
        if (_isLoading)
        {
            if (_isDone || _isError) ResetTimer();
            return;
        }
        _loadTimer.Stop();
        if (_loadStopWriteLine) Console.WriteLine();
    }

    public static void StartLoading()
    {
        ResetTimer();
        Console.Write(' ');
        UpdateLoadingContent(firstLoad: true);
        _isLoading = true;
        _loadTimer.Start();
    }

    public static void StopLoading(bool loadStopWriteLine = true)
    {
        _loadStopWriteLine = loadStopWriteLine;
        _isLoading = false;
        OnTimerElapsed();
    }

    public static void LoadingDone(bool loadStopWriteLine = true)
    {
        _loadStopWriteLine = loadStopWriteLine;
        _isDone = true;
        _isLoading = false;
        OnTimerElapsed();
    }

    public static void LoadingError(bool loadStopWriteLine = true)
    {
        _loadStopWriteLine = loadStopWriteLine;
        _isError = true;
        _isLoading = false;
        OnTimerElapsed();
    }
}
