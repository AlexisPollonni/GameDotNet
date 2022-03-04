using Microsoft.CodeAnalysis;

namespace GameDotNet.Generators.Component
{
    public class DiagnosticReporter : IDiagnosticReporter
    {
        private readonly GeneratorExecutionContext _context;

        public DiagnosticReporter(GeneratorExecutionContext context)
        {
            _context = context;
        }

        public void ReportError(string title, string message)
        {
            _context.ReportDiagnostic(Diagnostic.Create(
                                                        new DiagnosticDescriptor("GDN200",
                                                            title,
                                                            message,
                                                            "GameDotNet",
                                                            DiagnosticSeverity.Error,
                                                            true),
                                                        Location.None));
        }

        public void ReportWarning(string title, string message)
        {
            _context.ReportDiagnostic(Diagnostic.Create(
                                                        new DiagnosticDescriptor("GDN100",
                                                            title,
                                                            message,
                                                            "GameDotNet",
                                                            DiagnosticSeverity.Warning,
                                                            true),
                                                        Location.None));
        }

        public void ReportInfo(string title, string message)
        {
            _context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("GDN000",
                                                            title,
                                                            message,
                                                            "GameDotNet",
                                                            DiagnosticSeverity.Info,
                                                            true),
                                                        Location.None));
        }
    }
}