using Python.Runtime;
using PythonEnv;

namespace PythonNetLib.Test
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            PythonEnvInit.LogMessage += Console.WriteLine;
            PythonEnvInit.InstallPath = Path.GetFullPath(".");
            await PythonEnvInit.DownloadPython();
           
            //await PythonEnvInit.SetupPython();
            // install pip3 for package installation
          await  PythonEnvInit.TryInstallPip();
            // ok, now use pythonnet from that installation
            // download and install Spacy from the internet
          await  Installer.PipInstallModule("numpy");
            PythonEngine.Initialize();

            // call Python's sys.version to prove we are executing the right version
            dynamic sys = Py.Import("sys");
            Console.WriteLine("### Python version:\n\t" + sys.version);

            // call os.getcwd() to prove we are executing the locally installed embedded python distribution
            dynamic os = Py.Import("os");
            Console.WriteLine("### Current working directory:\n\t" + os.getcwd());
            Console.WriteLine("### PythonPath:\n\t" + PythonEngine.PythonPath);


        }

    }
}