// See https://aka.ms/new-console-template for more information

using GameDotNet.Graphics;
using GameDotNet.Hosting;

Console.WriteLine("Hello, World!");

using var app = new Application("SampleGame");

using var assetManager = new AssetImporter();

assetManager.LoadSceneFromFile("Assets/MonkeyScene.dae", out var scene);

app.Universe.LoadScene(scene!);

return app.Run();