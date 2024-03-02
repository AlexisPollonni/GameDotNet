// See https://aka.ms/new-console-template for more information

using GameDotNet.Graphics.Assets.Assimp;
using GameDotNet.Hosting;
using GameDotNet.Management.ECS;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Hello, World!");

using var app = new StandaloneApplication("SampleGame");

using var _ = app.Engine.OnInitialized.Subscribe(host =>
{
    var s = host.Services;
    var assetManager = s.GetRequiredService<AssimpNetImporter>();

    assetManager.LoadSceneFromFile("Assets/MonkeyScene.dae", out var scene);

    s.GetRequiredService<Universe>().LoadScene(scene!);
});

return await app.Run();