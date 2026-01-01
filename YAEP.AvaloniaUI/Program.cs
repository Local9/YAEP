using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using YAEP.Helpers;

namespace YAEP
{
    internal sealed class Program
    {
        [ModuleInitializer]
        internal static void InitializeResolvers()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            // Add native DLL directory to PATH
            string? baseDir = GetBaseDirectory();
            if (baseDir == null) return;

            string nativePath = Path.Combine(baseDir, "bin", "runtimes", RuntimeInformation.RuntimeIdentifier, "native");
            if (!Directory.Exists(nativePath))
            {
                nativePath = Path.Combine(baseDir, "bin", "runtimes", "win-x64", "native");
            }

            if (Directory.Exists(nativePath))
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!path.Contains(nativePath, StringComparison.OrdinalIgnoreCase))
                {
                    Environment.SetEnvironmentVariable("PATH", $"{nativePath};{path}");
                }
            }
        }

        private static string? GetBaseDirectory()
        {
            return AppContext.BaseDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static Assembly? ResolveAssembly(object? sender, ResolveEventArgs args)
        {
            string? baseDir = GetBaseDirectory();
            if (baseDir == null) return null;

            string? name = new AssemblyName(args.Name).Name;
            if (name == null) return null;

            if (!SecurityValidationHelper.IsValidAssemblyName(name))
            {
                System.Diagnostics.Debug.WriteLine($"Invalid assembly name detected: {name}");
                return null;
            }

            string expectedBinDir = Path.Combine(baseDir, "bin");
            string dllPath = Path.Combine(expectedBinDir, $"{name}.dll");

            try
            {
                string fullDllPath = Path.GetFullPath(dllPath);
                string fullExpectedDir = Path.GetFullPath(expectedBinDir);

                if (!fullDllPath.StartsWith(fullExpectedDir, StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"Assembly path outside expected directory: {dllPath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating assembly path: {ex.Message}");
                return null;
            }

            if (!File.Exists(dllPath))
                return null;

            try
            {
                return Assembly.LoadFrom(dllPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load assembly {name}: {ex.Message}");
                return null;
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            // Ensure only one instance of the application can run
            const string mutexName = "YAEP_SingleInstance_Mutex";
            using (Mutex mutex = new Mutex(true, mutexName, out bool createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is already running
                    return;
                }

                // This instance owns the mutex, proceed with application startup
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
