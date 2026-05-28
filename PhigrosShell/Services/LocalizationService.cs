using System.Globalization;
using System.Text.Json;

namespace PhigrosShell.Services;

internal class LocalizationService
{
    private Dictionary<string, string> _strings = new();

    public string this[string key]
    {
        get
        {
            if (_strings.TryGetValue(key, out var value))
                return value;
            return $"[{key}]";
        }
    }

    /// <summary>带参数格式化的本地化字符串</summary>
    public string this[string key, params object[] args]
    {
        get
        {
            if (_strings.TryGetValue(key, out var format))
            {
                try { return string.Format(format, args); }
                catch { return format; }
            }
            return $"[{key}]";
        }
    }

    public void Load(CultureInfo culture)
    {
        var name = culture.Name.ToLowerInvariant();
        var assembly = typeof(LocalizationService).Assembly;

        // Try to load from embedded resource
        string resourceName = "PhigrosShell.Resources.lang." + name + ".json";
        if (!TryLoadEmbedded(resourceName))
        {
            // Fallback to Chinese
            TryLoadEmbedded("PhigrosShell.Resources.lang.zh-cn.json");
        }

        // If still empty, create defaults
        if (_strings.Count == 0)
            LoadDefaults();
    }

    private bool TryLoadEmbedded(string resourceName)
    {
        try
        {
            var assembly = typeof(LocalizationService).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    reader.ReadToEnd()) ?? new Dictionary<string, string>();
                return true;
            }
        }
        catch { }
        return false;
    }

    private void LoadDefaults()
    {
        _strings = new Dictionary<string, string>
        {
            ["AppTitle"] = "=== {0} v{1} ===",
            ["BetaVersionPrompt"] = "This is a beta version. Some features may not work as expected.",
            ["Done"] = "Done",
            ["Error"] = "Error",
            ["HelpAvailableCommands"] = "Available Commands:",
            ["PressAnyKeyToContinue"] = "Press any key to continue...",
            ["LoggingIn"] = "Logging in",
            ["LoginSuccessfully"] = "Login successful!",
            ["LoginPlayerGetFailed"] = "Failed to get player info.",
            ["LoginQRCodeScanPrompt"] = "Scan this QR code with your TapTap app:",
            ["LoginLinkPrompt"] = "Or open the link: {0}",
            ["LoginQRExpirePrompt"] = "The QR code will expire in {0} seconds.",
            ["LoginQRWaiting"] = "Waiting for QR code scan...",
            ["CancelWithPressing"] = "Press '{0}' to cancel.",
            ["OperationCancelledByUser"] = "Operation cancelled.",
            ["LoginQRErrorWhenPolling"] = "An error occurred while polling the QR code.",
            ["LoginQRAuthorizationWaiting"] = "QR code scanned, waiting for authorization...",
            ["LoginQRInvalidGrantCode"] = "QR code expired, please try again.",
            ["LoginQRUserProfileGetFailed"] = "Failed to get user profile.",
            ["UserInfoNameTag"] = "Nickname",
            ["UserInfoIDTag"] = "Short ID",
            ["UserInfoObjectIDTag"] = "Object ID",
            ["UserInfoSessionTokenTag"] = "Session Token",
            ["UserInfoCreateTimeTag"] = "Create Time",
            ["UserInfoUpdateTimeTag"] = "Update Time",
            ["LoginSummaryTitle"] = "--- Save Summary ---",
            ["UserSummaryRankingScoreTag"] = "Ranking Score",
            ["UserSummaryAvatarTag"] = "Avatar",
            ["UserSummaryChallengeRankTag"] = "Challenge",
            ["UnhandledException"] = "Unhandled exception: {0}\nStack trace: {1}",
            ["UnhandledExceptionNoObject"] = "Unhandled exception with no exception object.",
            ["FailedToReadScriptFile"] = "Failed to read script file: {0}",
            ["WarnDifficultyTSVNotInitialized"] = "Warning: difficulty.tsv not initialized.",
            ["WarnDifficultyTSVNotLoaded"] = "Warning: difficulty.tsv not loaded.",
            ["WarnDifficultyTSVNoErrorDetect"] = "Cannot detect errors without difficulty.tsv.",
            ["WarnInfoTSVNotInitialized"] = "Warning: info.tsv not initialized.",
            ["WarnInfoTSVNotLoaded"] = "Warning: info.tsv not loaded.",
            ["P3B27NoSuggestion"] = "No suggestion",
            ["UpdateLogHeader"] = "Update Log",
        };
    }
}
