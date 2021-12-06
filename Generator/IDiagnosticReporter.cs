﻿namespace Analysers
{
    public interface IDiagnosticReporter
    {
        void ReportError(string title, string message);

        void ReportWarning(string title, string message);

        void ReportInfo(string title, string message);
    }
}