using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using AirBreather;
using AirBreather.IO;

namespace StepperUpper
{
    internal static class SevenZipExtractor
    {
        private static readonly Lazy<Task<string>> StandaloneExecutablePath = new Lazy<Task<string>>(UnpackExecutableAsync);

        internal static async Task ExtractArchiveAsync(string archivePath, DirectoryInfo outputDirectory)
        {
            string executablePath = await StandaloneExecutablePath.Value.ConfigureAwait(false);
            int exitCode = await ProcessRunner.RunProcessAsync(executablePath, "x", "-aoa", "-o" + outputDirectory.FullName, archivePath);
            if (exitCode != 0)
            {
                throw new Exception("Extraction failed with exit code " + exitCode.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static async Task<string> UnpackExecutableAsync()
        {
            string targetPath = Path.Combine(Path.GetTempPath(), "STEP_EXEs");
            Directory.CreateDirectory(targetPath);
            targetPath = Path.Combine(targetPath, "7z.dll");

            var arch = Environment.Is64BitProcess ? "7z-x64.dll" : "7z-x86.dll";

            using (var executableStream = ResourceUtility.OpenEmbeddedResourceFile(arch))
            using (var targetStream = AsyncFile.CreateSequential(targetPath))
            {
                await executableStream.CopyToAsync(targetStream).ConfigureAwait(false);
            }

            targetPath = Path.ChangeExtension(targetPath, "exe");
            arch = Environment.Is64BitProcess ? "7z-x64.exe" : "7z-x86.exe";

            using (var executableStream = ResourceUtility.OpenEmbeddedResourceFile(arch))
            using (var targetStream = AsyncFile.CreateSequential(targetPath))
            {
                await executableStream.CopyToAsync(targetStream).ConfigureAwait(false);
            }

            return targetPath;
        }
    }
}
