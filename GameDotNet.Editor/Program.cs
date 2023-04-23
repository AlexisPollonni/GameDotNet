using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Vulkan;

namespace GameDotNet.Editor
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                         .With(new X11PlatformOptions { UseVulkan = true })
                         .With(new Win32PlatformOptions { UseVulkan = true })
                         .With(new VulkanOptions
                         {
                             VulkanInstanceCreationOptions =
                             {
                                 UseDebug = true
                             },
                             VulkanDeviceCreationOptions =
                             {
                                 PreferDiscreteGpu = true
                             }
                         })
                         .UsePlatformDetect()
                         .LogToTrace()
                         .UseReactiveUI();
    }
}