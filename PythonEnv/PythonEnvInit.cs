using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Python;
using Python.Runtime;

namespace PythonEnv
{
    public static class PythonEnvInit
    {
        public static string PythonVersion = "python310";

        /// <summary>
        /// Path to install python. If needed, set before calling SetupPython().
        /// <para>Default is: Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)</para>
        /// </summary>
        public static string InstallPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        /// <summary>
        /// Name of the python directory. If needed, set before calling SetupPython().
        /// Defaults to python-3.7.3-embed-amd64
        /// </summary>
        public static string InstallDirectory { get; set; } = "python-3.10.9-embed-amd64";

        /// <summary>
        /// The full path to the Python directory. Customize this by setting InstallPath and InstallDirectory
        /// </summary>
        public static string EmbeddedPythonHome => Path.Combine(InstallPath, InstallDirectory);

        public static string ResourceName = "python-3.10.9-embed-amd64.zip";

        public static string DownloadUrl = @"https://www.python.org/ftp/python/3.10.9/python-3.10.9-embed-amd64.zip";
        /// <summary>
        /// Subscribe to this event to get installation log messages 
        /// </summary>
        public static event Action<string> LogMessage;

        private static void Log(string message)
        {
            LogMessage?.Invoke(message);
        }

        public static async Task DownloadPython()
        {
            if (!PythonEnv.DeployEmbeddedPython)
                return;

            if (Runtime.PythonDLL == null)
                Runtime.PythonDLL = "python310.dll"; // <-- note: since pythonnet v3.0.1 this can not be set multiple times!

            try
            {
                Installer.LogMessage += Log;
                Installer.Source = GetDownloadInstallationSource();
                Installer.PythonDirectoryName = InstallDirectory;
                Installer.InstallPath = InstallPath;
                await Installer.SetupPython(false);
            }
            finally
            {
                Installer.LogMessage -= Log;
            }
        }

        public static async Task SetupPython(bool force = false)
        {
            if (!PythonEnv.DeployEmbeddedPython)
                return;

            if (Runtime.PythonDLL == null)
                Runtime.PythonDLL = "python310.dll"; // <-- note: since pythonnet v3.0.1 this can not be set multiple times!

            try
            {
                Installer.LogMessage += Log;
                Installer.Source = GetInstallationSource();
                Installer.PythonDirectoryName = InstallDirectory;
                Installer.InstallPath = InstallPath;
                await Installer.SetupPython(force);
            }
            finally
            {
                Installer.LogMessage -= Log;
            }
        }

        private static Installer.InstallationSource GetInstallationSource()
        {
            return new Installer.EmbeddedResourceInstallationSource()
            {
                Assembly = typeof(PythonEnv).Assembly,
                ResourceName = ResourceName,
            };
        }
        private static Installer.InstallationSource GetDownloadInstallationSource()
        {
            return new Installer.DownloadInstallationSource()
            {
                DownloadUrl = DownloadUrl
            };
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
            try
            {
                Installer.LogMessage += Log;
                Installer.Source = GetInstallationSource();
                Installer.PythonDirectoryName = InstallDirectory;
                Installer.InstallPath = InstallPath;
                await Installer.InstallWheel(assembly, resourceName, force);
            }
            finally
            {
                Installer.LogMessage -= Log;
            }
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
            try
            {
                Installer.LogMessage += Log;
                Installer.Source = GetInstallationSource();
                Installer.PythonDirectoryName = InstallDirectory;
                Installer.InstallPath = InstallPath;
                await Installer.PipInstallWheel(assembly, resourceName, force);
            }
            finally
            {
                Installer.LogMessage -= Log;
            }
        }

        /// <summary>
        /// Uses pip to find and install the specified package.
        /// </summary>
        /// <param name="moduleName">The module/package to install </param>
        /// <param name="version"></param>
        /// <param name="force">When true, reinstall the packages even if it is already up-to-date.</param>
        public static async Task PipInstallModule(string moduleName, string version = "", bool force = false)
        {
            try
            {
                Installer.LogMessage += Log;
                Installer.Source = GetInstallationSource();
                Installer.PythonDirectoryName = InstallDirectory;
                Installer.InstallPath = InstallPath;
                await Installer.PipInstallModule(moduleName, version, force);
            }
            finally
            {
                Installer.LogMessage -= Log;
            }
        }

        /// <summary>
        /// Download and install pip.
        /// </summary>
        /// <remarks>
        /// Creates the lib folder under <see cref="EmbeddedPythonHome"/> if it does not exist.
        /// </remarks>
        public static async Task InstallPip()
        {
            try
            {
                Installer.LogMessage += Log;
                Installer.Source = GetInstallationSource();
                Installer.PythonDirectoryName = InstallDirectory;
                Installer.InstallPath = InstallPath;
                await Installer.InstallPip();
            }
            finally
            {
                Installer.LogMessage -= Log;
            }
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
    }
}
