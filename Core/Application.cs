using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Core;

public class Application
{
    public IWindow Window { get; }

    public Application()
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Test";

        Window = Silk.NET.Windowing.Window.Create(options);
        Window.Load += OnWindowLoad;
    }

    public int Run()
    {
        Window.Run();

        return 0;
    }

    private void OnWindowLoad()
    {
        var input = Window.CreateInput();
        foreach (var kb in input.Keyboards)
        {
            kb.KeyUp += (keyboard, key, arg3) =>
            {
                if (key == Key.Escape)
                {
                    Window.Close();
                }
            };
        }

        if (Window.VkSurface is not null)
        {
            //var surface = Window.VkSurface.Create();
            
            //surface.
        }
    }
}