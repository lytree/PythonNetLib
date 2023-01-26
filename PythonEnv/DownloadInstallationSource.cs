using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PythonEnv
{
    public static partial class Installer
    {

        /// <summary>
        /// Installs Python from an embedded resource of a .NET assembly
        /// </summary>
        internal class DownloadInstallationSource : InstallationSource
        {
            /// <summary>
            /// The location on the web where to download the python distribution, for instance https://www.python.org/ftp/python/3.7.3/python-3.7.3-embed-amd64.zip
            /// </summary>
            public string DownloadUrl { get; set; }

            public override async Task<string> RetrievePythonZip(string destinationDirectory)
            {
                var zipFile = Path.Combine(destinationDirectory, GetPythonZipFileName());
                if (!Force && File.Exists(zipFile))
                    return zipFile;
                if (DownloadUrl == string.Empty)
                {
                    Log("Download url is empty");
                    return string.Empty;
                }

                try
                {
                    Log("Downloading source...");
                    await Downloader.Download(DownloadUrl, zipFile, progress => Log($"{progress:F2}%"));
                    Log("Done!");
                    return zipFile;
                }
                catch (Exception ex)
                {
                    Log($"There was a problem downloading the source: {ex.Message}");
                    return string.Empty;
                }

            }

            public override string GetPythonZipFileName()
            {
                Uri uri = new(DownloadUrl);
                return System.IO.Path.GetFileName(uri.LocalPath);
            }

            public static async Task RunCommand(string command, CancellationToken token)
            {
                Process process = new();
                try
                {
                    string args = null;
                    string filename = null;
                    ProcessStartInfo startInfo = new();
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        // Unix/Linux/macOS specific command execution
                        filename = "/bin/bash";
                        args = $"-c {command}";
                    }
                    else
                    {
                        // Windows specific command execution
                        filename = "cmd.exe";
                        args = $"/C {command}";
                    }
                    Log($"> {filename} {args}");
                    startInfo = new ProcessStartInfo
                    {
                        FileName = filename,
                        WorkingDirectory = Directory.GetCurrentDirectory(),
                        Arguments = args,

                        // If the UseShellExecute property is true, the CreateNoWindow property value is ignored and a new window is created.
                        // .NET Core does not support creating windows directly on Unix/Linux/macOS and the property is ignored.

                        CreateNoWindow = true,
                        UseShellExecute = false, // necessary for stdout redirection
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    };
                    process.StartInfo = startInfo;
                    process.OutputDataReceived += (x, y) => Log(y.Data);
                    process.ErrorDataReceived += (x, y) => Log(y.Data);
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    token.Register(() =>
                    {
                        try
                        {
                            if (!process.HasExited)
                                process.Kill();
                        }
                        catch (Exception) { /* ignore */ }
                    });
                    await Task.Run(() => { process.WaitForExit(); }, token);
                    if (process.ExitCode != 0)
                        Log(" => exit code " + process.ExitCode);
                }
                catch (Exception e)
                {
                    Log($"RunCommand: Error with command: '{command}'\r\n{e.Message}");
                }
                finally
                {
                    process?.Dispose();
                }
            }

        }


    }
}
