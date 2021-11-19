using Core;
using Core.ECS;
using Core.ECS.Generated;

Console.WriteLine("Hello, World!");

//var app = new Application();

//app.Run();

var t = TypeId.Get<Application>();
var tt = TypeId<Application>.Get;

Console.WriteLine($"TypeId : {t.Id} - {t.Name}");

var store = new ComponentStore();

Console.WriteLine("End");