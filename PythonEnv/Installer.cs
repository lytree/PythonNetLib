

using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PythonEnv
{
    public static partial class Installer
    {
        /// <summary>
        /// Path to install python. If needed, set before calling SetupPython().
        /// <para>Default is: Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)</para>
        /// </summary>
        public static string InstallPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        /// <summary>
        /// Name of the python directory. If left null, this is directly derived from the python distribution that is installed. If needed, set before calling SetupPython().
        /// For instance, if you install https://www.python.org/ftp/python/3.7.3/python-3.7.3-embed-amd64.zip the directory defaults to python-3.7.3-embed-amd64
        /// </summary>
        public static string PythonDirectoryName { get; set; } = null;

        public static InstallationSource Source { get; set; } = new DownloadInstallationSource() { DownloadUrl = @"https://www.python.org/ftp/python/3.10.9/python-3.10.9-embed-amd64.zip" };

        /// <summary>
        /// The full path to the Python directory. Customize this by setting InstallPath and InstallDirectory
        /// </summary>
        public static string EmbeddedPythonHome => Path.Combine(InstallPath, (!string.IsNullOrWhiteSpace(PythonDirectoryName) ? PythonDirectoryName : Source.GetPythonDistributionName()) ?? string.Empty);

        /// <summary>
        /// Subscribe to this event to get installation log messages 
        /// </summary>
        public static event Action<string>? LogMessage;

        private static void Log(string message)
        {
            LogMessage?.Invoke(message);
        }

        public static async Task SetupPython(bool force = false)
        {
            Environment.SetEnvironmentVariable("PATH", $"{EmbeddedPythonHome};" + Environment.GetEnvironmentVariable("PATH"));
            if (!force && Directory.Exists(EmbeddedPythonHome) && File.Exists(Path.Combine(EmbeddedPythonHome, "python.exe"))) // python seems installed, so exit
                return;
            var zip = await Source.RetrievePythonZip(InstallPath);
            if (string.IsNullOrWhiteSpace(zip))
            {
                Log("SetupPython: Error obtaining zip file from installation source");
                return;
            }
            await Task.Run(() =>
            {
                try
                {
                    //ZipFile.ExtractToDirectory(zip, zip.Replace(".zip", ""));
                    ZipFile.ExtractToDirectory(zip, EmbeddedPythonHome);

                    // allow pip on embedded python installation
                    // see https://github.com/pypa/pip/issues/4207#issuecomment-281236055
                    var pth = Path.Combine(EmbeddedPythonHome, Source.GetPythonVersion() + "._pth");
                    File.Delete(pth);
                }
                catch (Exception e)
                {
                    Log("SetupPython: Error extracting zip file: " + zip);
                }
            });
        }

        /// <summary>
        /// Install a python library (.whl file) in the embedded python installation of Python.Included
        ///
        /// Note: Installing python packages using a custom wheel may result in an invalid python environment if the packages don't match the python version.
        /// To be safe, use pip by calling Installer.PipInstallModule.
        /// </summary>
        /// <param name="assembly">The assembly containing the embedded wheel</param>
        /// <param name="resourceName">Name of the embedded wheel file i.e. "numpy-1.16.3-cp37-cp37m-win_amd64.whl"</param>
        /// <param name="force"></param>
        /// <returns></returns>
        public static async Task InstallWheel(Assembly assembly, string resourceName, bool force = false)
        {
            var key = GetResourceKey(assembly, resourceName);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"The resource '{resourceName}' was not found in assembly '{assembly.FullName}'");

            var moduleName = resourceName.Split('-').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException($"The resource name '{resourceName}' did not contain a valid module name");

            var lib = GetLibDirectory();

            var modulePath = Path.Combine(lib, moduleName);
            if (!force && Directory.Exists(modulePath))
                return;

            var wheelPath = Path.Combine(lib, key);
            await Task.Run(() =>
            {
                CopyEmbeddedResourceToFile(assembly, key, wheelPath, force);
            }).ConfigureAwait(false);

            await InstallLocalWheel(wheelPath, lib).ConfigureAwait(false);

            File.Delete(wheelPath);
        }

        /// <summary>
        /// Install a python library (.whl file) in the embedded python installation of Python.Included
        /// Note: Installing python packages using a custom wheel may result in an invalid python environment if the packages don't match the python version.
        /// To be safe, use pip by calling Installer.PipInstallModule.
        /// </summary>
        /// <param name="wheelPath">The wheel file path.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        /// <exception cref="ArgumentException">
        /// The resource '{resource_name}' was not found in assembly '{assembly.FullName}'
        /// or
        /// The resource name '{resource_name}' did not contain a valid module name
        /// </exception>
        public static async Task InstallWheel(string wheelPath, bool force = false)
        {
            var moduleName = GetModuleNameFromWheelFile(wheelPath);
            var lib = GetLibDirectory();

            var modulePath = Path.Combine(lib, moduleName);
            if (!force && Directory.Exists(modulePath))
                return;

            await InstallLocalWheel(wheelPath, lib).ConfigureAwait(false);
        }

        private static string GetModuleNameFromWheelFile(string wheelPath)
        {
            var fileName = Path.GetFileName(wheelPath);
            var moduleName = fileName.Split('-').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(moduleName))
            {
                throw new ArgumentException($"The file name '{fileName}' did not contain a valid module name");
            }
            return moduleName;
        }

        private static string GetLibDirectory()
        {
            var lib = Path.Combine(EmbeddedPythonHome, "Lib");
            if (!Directory.Exists(lib))
            {
                Directory.CreateDirectory(lib);
            }
            return lib;
        }

        private static async Task InstallLocalWheel(string wheelPath, string lib)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var zip = ZipFile.OpenRead(wheelPath);
                    var allFilesAlreadyPresent = AreAllFilesAlreadyPresent(zip, lib);
                    if (!allFilesAlreadyPresent)
                    {
                        zip.ExtractToDirectory(lib);
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Error extracting zip file: " + wheelPath);
                }

                // modify _pth file
                var pth = Path.Combine(EmbeddedPythonHome, Source.GetPythonVersion() + "._pth");
                if (File.Exists(pth) && !File.ReadAllLines(pth).Contains("./Lib"))
                    File.AppendAllLines(pth, new[] { "./Lib" });
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Uses the local python-embedded pip module to install a python library (.whl file) in the embedded python installation of Python.Included
        ///
        /// Note: Installing python packages using a custom wheel may result in an invalid python environment if the packages don't match the python version.
        /// To be safe, use pip by calling Installer.PipInstallModule.
        /// </summary>
        /// <param name="assembly">The assembly containing the embedded wheel</param>
        /// <param name="resourceName">Name of the embedded wheel file i.e. "numpy-1.16.3-cp37-cp37m-win_amd64.whl"</param>
        /// <param name="force"></param>
        /// <returns></returns>
        public static async Task PipInstallWheel(Assembly assembly, string resourceName, bool force = false)
        {
            string key = GetResourceKey(assembly, resourceName);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"The resource '{resourceName}' was not found in assembly '{assembly.FullName}'");
            string? moduleName = resourceName.Split('-').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException($"The resource name '{resourceName}' did not contain a valid module name");
            string libDir = Path.Combine(EmbeddedPythonHome, "Lib");
            if (!Directory.Exists(libDir))
                Directory.CreateDirectory(libDir);
            string modulePath = Path.Combine(libDir, moduleName);
            if (!force && Directory.Exists(modulePath))
                return;

            string wheelPath = Path.Combine(libDir, key);
            string pipPath = Path.Combine(EmbeddedPythonHome, "Scripts", "pip3");

            CopyEmbeddedResourceToFile(assembly, key, wheelPath, force);

            await TryInstallPip();

            RunCommand($"{pipPath} install {wheelPath}");
        }

        private static void CopyEmbeddedResourceToFile(Assembly assembly, string resourceName, string filePath, bool force = false)
        {
            if (!force && File.Exists(filePath))
                return;
            var key = GetResourceKey(assembly, resourceName);
            if (key == null)
            {
                Log($"Error: Resource name '{resourceName}' not found in assembly {assembly.FullName}!");
            }

            try
            {
                using Stream stream = assembly.GetManifestResourceStream(key);
                using var file = new FileStream(filePath, FileMode.Create);
                if (stream == null)
                {
                    Log($"CopyEmbeddedResourceToFile: Resource name '{resourceName}' not found!");
                    throw new ArgumentException($"Resource name '{resourceName}' not found!");
                }
                stream.CopyTo(file);
            }
            catch (Exception e)
            {
                Log($"Error: unable to extract embedded resource '{resourceName}' from  {assembly.FullName}: " +
                    e.Message);
            }
        }

        public static string? GetResourceKey(Assembly assembly, string embeddedFile)
        {
            return assembly.GetManifestResourceNames().FirstOrDefault(x => x.Contains(embeddedFile));
        }

        /// <summary>
        /// Uses pip to find and install the specified package.
        /// </summary>
        /// <param name="moduleName">The module/package to install </param>
        /// <param name="force">When true, reinstall the packages even if it is already up-to-date.</param>
        /// <param name="runInBackground">
        /// Indicates that no command windows will be visible and the process will automatically
        /// terminate when complete. When true, the command window must be manually closed before
        /// processing will continue.
        /// </param>
        public static async Task PipInstallModule(string moduleName, string version = "", bool force = false)
        {
            await TryInstallPip();

            if (IsModuleInstalled(moduleName) && !force)
                return;

            string pipPath = Path.Combine(EmbeddedPythonHome, "Scripts", "pip");
            string pythonPath = Path.Combine(EmbeddedPythonHome, "", "python");
            string forceInstall = force ? " --force-reinstall" : "";
            if (version.Length > 0)
                version = $"=={version}";

            RunCommand($"{pythonPath} -m pip install -i https://pypi.tuna.tsinghua.edu.cn/simple {moduleName}{version} {forceInstall}");
        }

        /// <summary>
        /// Download and install pip.
        /// </summary>
        /// <remarks>
        /// Creates the lib folder under <see cref="EmbeddedPythonHome"/> if it does not exist.
        /// </remarks>
        /// <param name="runInBackground">
        /// Indicates that no command windows will be visible and the process will automatically
        /// terminate when complete. When true, the command window must be manually closed before
        /// processing will continue.
        /// </param>
        public static async Task InstallPip()
        {
            string libDir = Path.Combine(EmbeddedPythonHome, "Lib");

            if (!Directory.Exists(libDir))
                Directory.CreateDirectory(libDir);

            string getPipUrl = @"https://bootstrap.pypa.io/get-pip.py";
            string getPipFilePath = Path.Combine(libDir, "get-pip.py");

            try
            {
                Log("Downloading Pip...");
                await Downloader.Download(getPipUrl, getPipFilePath, progress => Log($"{progress:F2}%"));
                Log("Done!");
            }
            catch (Exception ex)
            {
                Log($"There was a problem downloading pip: {ex.Message}");
                return;
            }


            RunCommand($"cd {EmbeddedPythonHome} && python.exe Lib\\get-pip.py");
        }

        public static async Task<bool> TryInstallPip(bool force = false)
        {
            if (!IsPipInstalled() || force)
            {
                try
                {
                    await InstallPip();
                }
                catch
                {
                    throw new FileNotFoundException("pip is not installed");
                }
            }
            return IsPipInstalled();
        }

        public static bool IsPythonInstalled()
        {
            return File.Exists(Path.Combine(EmbeddedPythonHome, "python.exe"));

        }

        public static bool IsPipInstalled()
        {
            return File.Exists(Path.Combine(EmbeddedPythonHome, "Scripts", "pip.exe"));
        }

        public static bool IsModuleInstalled(string module)
        {
            if (!IsPythonInstalled())
                return false;

            string moduleDir = Path.Combine(EmbeddedPythonHome, "Lib", "site-packages", module);
            return Directory.Exists(moduleDir) && File.Exists(Path.Combine(moduleDir, "__init__.py"));
        }

        /// <summary>
        /// Runs the specified command as a local system cmd processes.
        /// </summary>
        /// <param name="command">The arguments passed to cmd.</param>
        /// <param name="runInBackground">
        /// Indicates that no command windows will be visible and the process will automatically
        /// terminate when complete. When true, the command window must be manually closed before
        /// processing will continue.
        /// </param>
        public static void RunCommand(string command) =>
            RunCommand(command, CancellationToken.None).Wait();

        public static async Task RunCommand(string command, CancellationToken token)
        {
            Process process = new();
            try
            {
                string? args = null;
                string filename = null;
                ProcessStartInfo startInfo = new ProcessStartInfo();
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
                    WorkingDirectory = EmbeddedPythonHome,
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

        private static bool AreAllFilesAlreadyPresent(ZipArchive zip, string lib)
        {
            return zip.Entries.Select(entry => Path.Combine(lib, entry.FullName)).All(File.Exists);
        }
    }
}
