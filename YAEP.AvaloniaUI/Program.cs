using Avalonia;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

            string dllPath = Path.Combine(baseDir, "bin", $"{name}.dll");
            return File.Exists(dllPath) ? Assembly.LoadFrom(dllPath) : null;
        }

        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
