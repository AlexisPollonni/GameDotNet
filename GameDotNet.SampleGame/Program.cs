// See https://aka.ms/new-console-template for more information

using GameDotNet.Core;
using GameDotNet.Core.Tools;

Console.WriteLine("Hello, World!");

using var app = new Application("SampleGame");

using var assetManager = new AssetImporter();

assetManager.LoadSceneFromFile("Assets/Monkey.dae", out var scene);

app.Universe.LoadScene(scene!);

return app.Run();