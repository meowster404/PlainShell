using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Spectre.Console;

namespace PlainShell.Core;

public class ReleaseAsset
{
    public string Name { get; set; } = string.Empty;
    public string BrowserDownloadUrl { get; set; } = string.Empty;
    public long Size { get; set; }
}

public class ReleaseInfo
{
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool Prerelease { get; set; }
    public DateTime PublishedAt { get; set; }
    public List<ReleaseAsset> Assets { get; set; } = new();

    public ReleaseAsset? GetPreferredAsset()
    {
        // 1. Prefer Windows setup installer if available
        var installer = Assets.FirstOrDefault(a => a.Name.EndsWith("-Setup-x64.exe", StringComparison.OrdinalIgnoreCase));
        if (installer != null) return installer;

        // 2. Fall back to standalone PlainShell.exe
        var exe = Assets.FirstOrDefault(a => a.Name.Equals("PlainShell.exe", StringComparison.OrdinalIgnoreCase));
        if (exe != null) return exe;

        // 3. Any .exe asset
        return Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }
}

public static class UpdateManager
{
    public const string CurrentVersion = "1.0.0";
    public const string CurrentTag = "v1.0.0-beta.1";
    public const string GitHubRepo = "meowster404/PlainShell";

    public static string? PendingNotification { get; set; }

    public static async Task<ReleaseInfo?> CheckForUpdateAsync(bool silent = true)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlainShell-App", CurrentVersion));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            string url = $"https://api.github.com/repos/{GitHubRepo}/releases";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                return null;
            }

            foreach (var releaseElement in root.EnumerateArray())
            {
                string tag = releaseElement.GetProperty("tag_name").GetString() ?? string.Empty;
                string name = releaseElement.TryGetProperty("name", out var np) ? (np.GetString() ?? tag) : tag;
                string body = releaseElement.TryGetProperty("body", out var bp) ? (bp.GetString() ?? string.Empty) : string.Empty;
                bool prerelease = releaseElement.TryGetProperty("prerelease", out var pr) && pr.GetBoolean();

                var assets = new List<ReleaseAsset>();
                if (releaseElement.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in assetsElem.EnumerateArray())
                    {
                        assets.Add(new ReleaseAsset
                        {
                            Name = a.GetProperty("name").GetString() ?? string.Empty,
                            BrowserDownloadUrl = a.GetProperty("browser_download_url").GetString() ?? string.Empty,
                            Size = a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0
                        });
                    }
                }

                if (IsNewerVersion(tag, CurrentTag))
                {
                    return new ReleaseInfo
                    {
                        TagName = tag,
                        Name = name,
                        Body = body,
                        Prerelease = prerelease,
                        Assets = assets
                    };
                }
            }
        }
        catch
        {
            // Fail silently on network errors
        }

        return null;
    }

    public static void CheckAndPromptUpdate(bool interactive = true)
    {
        ConsoleHelper.SafeClear();
        AnsiConsole.Write(new Rule("[bold cyan]PlainShell In-App Auto-Updater[/]").LeftJustified().RuleStyle("steelBlue1"));

        AnsiConsole.MarkupLine($"[grey]Current Installed Version:[/] [bold green]{CurrentTag}[/]");
        AnsiConsole.MarkupLine($"[grey]Checking GitHub repository:[/] [cyan]{GitHubRepo}[/]\n");

        ReleaseInfo? latestRelease = null;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("[cyan]Checking for new releases...[/]", ctx =>
            {
                latestRelease = CheckForUpdateAsync(silent: false).GetAwaiter().GetResult();
            });

        if (latestRelease == null)
        {
            AnsiConsole.MarkupLine("[bold green]✓ You are running the latest version of PlainShell![/]");
            AnsiConsole.MarkupLine("[grey]No new updates available at this time.[/]\n");
            AnsiConsole.MarkupLine("[grey]Press any key to return to shell...[/]");
            Console.ReadKey(intercept: true);
            ConsoleHelper.SafeClear();
            return;
        }

        var asset = latestRelease.GetPreferredAsset();
        if (asset == null)
        {
            AnsiConsole.MarkupLine($"[yellow]A new release ({latestRelease.TagName}) is available, but no compatible Windows executable was found.[/]");
            AnsiConsole.MarkupLine("[grey]Press any key to return to shell...[/]");
            Console.ReadKey(intercept: true);
            ConsoleHelper.SafeClear();
            return;
        }

        // Display update information
        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap().PadRight(2));
        grid.AddColumn(new GridColumn());

        grid.AddRow("[bold cyan]New Version:[reset]", $"[bold green]{Markup.Escape(latestRelease.TagName)}[/] {(latestRelease.Prerelease ? "[yellow](Pre-release)[/]" : "")}");
        grid.AddRow("[bold cyan]Release Title:[reset]", $"[bold white]{Markup.Escape(latestRelease.Name)}[/]");
        grid.AddRow("[bold cyan]Package Asset:[reset]", $"[yellow]{Markup.Escape(asset.Name)}[/] ({FormatBytes(asset.Size)})");

        if (!string.IsNullOrWhiteSpace(latestRelease.Body))
        {
            string notes = latestRelease.Body.Length > 300 ? latestRelease.Body.Substring(0, 300) + "..." : latestRelease.Body;
            grid.AddRow("[bold cyan]Changelog:[reset]", $"[grey]{Markup.Escape(notes)}[/]");
        }

        var panel = new Panel(grid)
        {
            Header = new PanelHeader("[bold green] Update Available! [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.SpringGreen3),
            Padding = new Padding(1, 1, 1, 1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        bool proceed = AnsiConsole.Confirm("Would you like to download and install this update inside the app right now?", defaultValue: true);
        if (!proceed)
        {
            ConsoleHelper.SafeClear();
            return;
        }

        DownloadAndApplyUpdate(latestRelease, asset);
    }

    private static void DownloadAndApplyUpdate(ReleaseInfo release, ReleaseAsset asset)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "PlainShellUpdate");
        Directory.CreateDirectory(tempDir);
        string targetFilePath = Path.Combine(tempDir, asset.Name);

        try
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Progress()
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new DownloadedColumn()
                })
                .Start(ctx =>
                {
                    var task = ctx.AddTask($"[cyan]Downloading {asset.Name}[/]", maxValue: asset.Size > 0 ? asset.Size : 100);

                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PlainShell-App", CurrentVersion));

                    using var response = client.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();

                    using var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                    using var fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

                    byte[] buffer = new byte[81920];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, bytesRead);
                        task.Increment(bytesRead);
                    }
                });

            AnsiConsole.MarkupLine("\n[bold green]✓ Download completed successfully![/]");

            // 1. If asset is installer Setup, launch installer
            if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[bold cyan]Launching Windows Installer setup wizard...[/]");
                var startInfo = new ProcessStartInfo
                {
                    FileName = targetFilePath,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                AnsiConsole.MarkupLine("[grey]Exiting PlainShell to allow installation to finish.[/]");
                Thread.Sleep(1500);
                Environment.Exit(0);
                return;
            }

            // 2. If asset is standalone PlainShell.exe, apply in-place replacement
            if (asset.Name.Equals("PlainShell.exe", StringComparison.OrdinalIgnoreCase))
            {
                string currentExe = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlainShell.exe");
                string backupExe = currentExe + ".old";

                AnsiConsole.MarkupLine("[bold cyan]Applying in-place binary update...[/]");

                if (File.Exists(backupExe))
                {
                    try { File.Delete(backupExe); } catch { }
                }

                // On Windows, running executables CAN be renamed via File.Move!
                File.Move(currentExe, backupExe);
                File.Copy(targetFilePath, currentExe, overwrite: true);

                AnsiConsole.MarkupLine($"[bold green]✓ PlainShell has been updated to {release.TagName}![/]");
                AnsiConsole.MarkupLine("[yellow]Restarting application...[/]");
                Thread.Sleep(1200);

                // Spawn new version
                Process.Start(new ProcessStartInfo
                {
                    FileName = currentExe,
                    UseShellExecute = true
                });

                Environment.Exit(0);
                return;
            }

            AnsiConsole.MarkupLine($"[bold green]Update downloaded to:[/] {targetFilePath}");
            AnsiConsole.MarkupLine("[grey]Press any key to return to shell...[/]");
            Console.ReadKey(intercept: true);
            ConsoleHelper.SafeClear();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[bold red]Update failed:[/] {Markup.Escape(ex.Message)}");
            AnsiConsole.MarkupLine("[grey]Press any key to return to shell...[/]");
            Console.ReadKey(intercept: true);
            ConsoleHelper.SafeClear();
        }
    }

    private static bool IsNewerVersion(string remoteTag, string localTag)
    {
        string Clean(string tag) => tag.TrimStart('v', 'V').Split('-')[0];

        try
        {
            if (Version.TryParse(Clean(remoteTag), out var rVer) && Version.TryParse(Clean(localTag), out var lVer))
            {
                if (rVer > lVer) return true;
                if (rVer < lVer) return false;

                // Same version number, but remote is release while local is beta/prerelease
                if (localTag.Contains("beta", StringComparison.OrdinalIgnoreCase) && !remoteTag.Contains("beta", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
