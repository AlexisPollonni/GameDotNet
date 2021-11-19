namespace Core;

public class TestClass
{
    private readonly Dictionary<string, double> _coords;

    public TestClass()
    {
        _coords = new Dictionary<string, double>();

        for (var i = 0; i < 100000000; i++)
        {
            _coords.Add($"coordinate_{i}", Random.Shared.NextDouble());
        }
    }

    public bool Execute()
    {
        foreach (var (key, value) in _coords)
        {
            var v = value * Random.Shared.NextDouble();

            _coords[key] = v;
        }

        return true;
    }
}