using Microsoft.CodeAnalysis;

namespace GameDotNet.Generators.Component;

public class ComponentMaskGenerator
{
    private readonly GeneratorExecutionContext _context;
    private readonly DiagnosticReporter _diagnosticReporter;

    public ComponentMaskGenerator(GeneratorExecutionContext context, DiagnosticReporter diagnosticReporter)
    {
        _context = context;
        _diagnosticReporter = diagnosticReporter;
    }

    public void Execute()
    {
        // TODO : Generate a bitmask type to accelerate entity component look-up known at compile time
    }
}