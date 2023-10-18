// See https://aka.ms/new-console-template for more information

using GameDotNet.Graphics.Assets.Assimp;
using GameDotNet.Hosting;

Console.WriteLine("Hello, World!");

using var app = new Application("SampleGame");

using var assetManager = new AssimpNetImporter();

assetManager.LoadSceneFromFile("Assets/MonkeyScene.dae", out var scene);

app.Universe.LoadScene(scene!);

return await app.Run();