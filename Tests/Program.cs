using Core;
using Core.ECS;
using Core.ECS.Generated;
using Tests;

Console.WriteLine("Hello, World!");

//var app = new Application();

//app.Run();

var t = TypeId.Get<Application>();
var tt = TypeId<Application>.Get;

Console.WriteLine($"TypeId : {t.Id} - {t.Name}");

var store = new ComponentStore();

for (var i = 0; i < 1000; i++)
{
    var c = new TestComponent { Index = i };
    store.Add(in c);
}

var c2 = store.Get<TestComponent>(69);
if (c2.Index != 69)
    Console.WriteLine("ERROR: A component changed after inserting it into the store, this shouldn't happen !");

Console.WriteLine("End");