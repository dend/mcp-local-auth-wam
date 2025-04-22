using Den.Dev.LocalMCP.WAM.Models;
using Den.Dev.LocalMCP.WAM.Win32;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Den.Dev.LocalMCP.WAM
{
    public class AuthenticationService
    {
        private readonly IPublicClientApplication _msalClient;
        private static ILogger<AuthenticationService> _logger;
        private const string _clientId = "b4a9dacb-4c8e-45e2-9650-9ebaf98ecc40";
        private static readonly string[] _scopes = ["User.Read"];

        private AuthenticationService(ILogger<AuthenticationService> logger, IPublicClientApplication msalClient)
        {
            _logger = logger;
            _msalClient = msalClient;
        }

        public static async Task<AuthenticationService> CreateAsync(ILogger<AuthenticationService> logger)
        {
            // Initialize the static logger first so it's available for all static methods
            _logger = logger;

            var storageProperties =
                new StorageCreationPropertiesBuilder("authcache.bin", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Den.Dev.LocalMCP.WAM"))
                .Build();

            logger.LogInformation("Initializing AuthenticationService");

            var msalClient = PublicClientApplicationBuilder
                .Create(_clientId)
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId("b811a652-39e6-4a0c-b563-4279a1dd5012")
                .WithParentActivityOrWindow(GetBindingParentWindow)
                .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(msalClient.UserTokenCache);

            return new AuthenticationService(logger, msalClient);
        }

        public async Task<string> AcquireTokenAsync()
        {
            try
            {
                // Try silent authentication first
                var accounts = await _msalClient.GetAccountsAsync();
                var account = accounts.FirstOrDefault();

                AuthenticationResult? result = null;

                try
                {
                    if (account != null)
                    {
                        result = await _msalClient.AcquireTokenSilent(_scopes, account).ExecuteAsync();
                    }
                    else
                    {
                        result = await _msalClient.AcquireTokenSilent(_scopes, PublicClientApplication.OperatingSystemAccount)
                                            .ExecuteAsync();
                    }
                }
                catch (MsalUiRequiredException ex)
                {
                    result = await _msalClient.AcquireTokenInteractive(_scopes).ExecuteAsync();
                }

                return result.AccessToken;
            }
            catch (Exception ex)
            {
                throw new Exception($"Authentication failed: {ex.Message}", ex);
            }
        }

        private static IntPtr GetBindingParentWindow()
        {
            _logger.LogInformation("Finding parent process window for authentication binding");

            var currentProcessId = Environment.ProcessId;
            return FindWindowInProcessHierarchy(currentProcessId);
        }

        private static IntPtr FindWindowInProcessHierarchy(int processId, int hierarchyLevel = 0, int maxLevels = 5)
        {
            if (hierarchyLevel >= maxLevels)
            {
                _logger.LogWarning($"Reached maximum process hierarchy level ({maxLevels}), falling back to desktop window");
                IntPtr desktopWindow = NativeBridge.GetDesktopWindow();
                _logger.LogInformation($"Using desktop window as fallback: {desktopWindow}");
                return desktopWindow;
            }

            _logger.LogInformation($"Looking for window in process {processId} (hierarchy level {hierarchyLevel})");

            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(processId);

                // First try: get the main window directly from this process
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    _logger.LogInformation($"Found window from process {process.ProcessName} (ID: {processId}): {process.MainWindowHandle}");
                    return process.MainWindowHandle;
                }

                // Try to find any visible window from this process
                IntPtr windowHandle = FindWindowForProcess(processId);
                if (windowHandle != IntPtr.Zero)
                {
                    return windowHandle;
                }

                // If we can't find a window, try to get the parent process
                _logger.LogInformation($"No suitable window found for process {process.ProcessName} (ID: {processId}), checking parent process");

                try
                {
                    int parentProcessId = NativeBridge.GetParentProcessId(processId);

                    // Skip if we hit system processes or invalid ones
                    if (parentProcessId <= 4 || parentProcessId == processId)
                    {
                        _logger.LogInformation($"Reached system process or invalid parent (ID: {parentProcessId}), stopping hierarchy search");
                        IntPtr desktopWindow = NativeBridge.GetDesktopWindow();
                        _logger.LogInformation($"Using desktop window as fallback: {desktopWindow}");
                        return desktopWindow;
                    }

                    // Recursively check the parent process
                    return FindWindowInProcessHierarchy(parentProcessId, hierarchyLevel + 1, maxLevels);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to get parent process for {processId}: {ex.Message}");
                    // Fall through to desktop window
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to access process {processId}: {ex.Message}");
                // Fall through to desktop window
            }

            // If we've exhausted all options, return desktop window
            IntPtr desktop = NativeBridge.GetDesktopWindow();
            _logger.LogInformation($"Using desktop window as final fallback: {desktop}");
            return desktop;
        }

        private static IntPtr FindWindowForProcess(int processId)
        {
            var windowCandidates = new List<WindowCandidate>();

            NativeBridge.EnumWindowsProc enumProc = (hWnd, lParam) =>
            {
                // We're really not interested in invisible windows.
                if (!NativeBridge.IsWindowVisible(hWnd))
                    return true;

                // No tiny windows need to be considered (bar is 50x50).
                NativeBridge.RECT rect;
                if (!NativeBridge.GetWindowRect(hWnd, out rect) || !rect.IsValidSize)
                    return true;

                NativeBridge.GetWindowThreadProcessId(hWnd, out uint windowProcessId);

                if (windowProcessId != processId)
                    return true;

                var titleBuilder = new System.Text.StringBuilder(256);
                NativeBridge.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                var title = titleBuilder.ToString();

                // Add window as a candidate for potential parent
                // windows that we will use to parent WAM to.
                // Windows with a title are more relevant, so
                // we prioritize them in our selection.
                windowCandidates.Add(new WindowCandidate
                {
                    Handle = hWnd,
                    ProcessId = (int)windowProcessId,
                    Title = title,
                    Size = rect.Width * rect.Height
                });

                return true;
            };

            NativeBridge.EnumWindows(enumProc, IntPtr.Zero);

            var bestWindow = windowCandidates
                .OrderByDescending(w => !string.IsNullOrEmpty(w.Title))
                .ThenByDescending(w => w.Size)
                .FirstOrDefault();

            if (bestWindow != null && bestWindow.Handle != IntPtr.Zero)
            {
                _logger.LogInformation($"Found window for process {processId}: '{bestWindow.Title}' with handle {bestWindow.Handle}");
                return bestWindow.Handle;
            }

            _logger.LogInformation($"No suitable windows found for process {processId}");
            return IntPtr.Zero;
        }
    }
}
