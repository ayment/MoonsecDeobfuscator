using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;

namespace MoonsecDeobfuscator
{
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.AssemblyResolve += load;
        }

        private Assembly? load(object? sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name + ".dll";
            string binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dlls", assemblyName);

            if (File.Exists(binPath))
            {
                return Assembly.LoadFrom(binPath);
            }

            return null;
        }
    }
}