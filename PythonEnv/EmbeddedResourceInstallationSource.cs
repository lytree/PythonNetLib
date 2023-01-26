
using System.Reflection;

namespace PythonEnv
{
    public static partial class Installer
    {

        /// <summary>
        /// Installs Python from an embedded resource of a .NET assembly
        /// </summary>
        internal class EmbeddedResourceInstallationSource : InstallationSource
        {
            /// <summary>
            /// The .NET assembly that includes a python zip as embedded resource.
            /// Note: you can get that by using <code>typeof(AnyTypeInYourAssembly).Assembly</code>
            /// </summary>
            public Assembly Assembly { get; set; }

            /// <summary>
            /// The name of the zip file that has been included in the given assembly as embedded resource, i.e. "python-3.7.3-embed-amd64.zip". 
            /// </summary>
            public string? ResourceName { get; set; }

            public override Task<string> RetrievePythonZip(string destinationDirectory)
            {
                var filePath = Path.Combine(destinationDirectory, ResourceName ?? string.Empty);
                if (!Force && File.Exists(filePath))
                    return Task.FromResult(filePath);
                CopyEmbeddedResourceToFile(Assembly, GetPythonDistributionName() ?? string.Empty, filePath);
                return Task.FromResult(filePath);
            }

            public override string? GetPythonZipFileName()
            {
                return Path.GetFileName(ResourceName);
            }

        }


    }
}
