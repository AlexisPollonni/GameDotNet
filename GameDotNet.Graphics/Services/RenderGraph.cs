namespace GameDotNet.Graphics.Services;

public class RenderGraph
{
    public RenderGraph AddRenderPass(Action<RenderPassBuilder> builder)
    {
        return this;
    }

    public void Compile()
    { }

    public void Execute()
    { }
}

public class RenderPassBuilder
{ }