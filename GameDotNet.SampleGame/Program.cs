// See https://aka.ms/new-console-template for more information

using GameDotNet.Graphics.Assets.Assimp;
using GameDotNet.Hosting;
using GameDotNet.Management.ECS;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Hello, World!");

using var app = new Application("SampleGame");

using var assetManager = new AssimpNetImporter();

assetManager.LoadSceneFromFile("Assets/MonkeyScene.dae", out var scene);

app.GlobalHost.Services.GetRequiredService<Universe>().LoadScene(scene!);

await app.Initialize();
return await app.Run();