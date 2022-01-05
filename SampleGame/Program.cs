// See https://aka.ms/new-console-template for more information

using Core;
using Silk.NET.Windowing;

Console.WriteLine("Hello, World!");

var p = Window.Platforms;

var app = new Application("SampleGame");

return app.Run();