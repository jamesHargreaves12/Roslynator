﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Roslynator.Diagnostics;

internal class CodeAnalyzerOptions : CodeAnalysisOptions
{
    public static CodeAnalyzerOptions Default { get; } = new();

    public CodeAnalyzerOptions(
        FileSystemFilter fileSystemFilter = null,
        bool ignoreAnalyzerReferences = false,
        bool ignoreCompilerDiagnostics = false,
        bool reportNotConfigurable = false,
        bool reportSuppressedDiagnostics = false,
        bool logAnalyzerExecutionTime = false,
        bool concurrentAnalysis = true,
        DiagnosticSeverity severityLevel = DiagnosticSeverity.Info,
        IEnumerable<string> supportedDiagnosticIds = null,
        IEnumerable<string> ignoredDiagnosticIds = null) : base(
            fileSystemFilter: fileSystemFilter,
            severityLevel: severityLevel,
            ignoreAnalyzerReferences: ignoreAnalyzerReferences,
            concurrentAnalysis: concurrentAnalysis,
            supportedDiagnosticIds: supportedDiagnosticIds,
            ignoredDiagnosticIds: ignoredDiagnosticIds)
    {
        IgnoreCompilerDiagnostics = ignoreCompilerDiagnostics;
        ReportNotConfigurable = reportNotConfigurable;
        ReportSuppressedDiagnostics = reportSuppressedDiagnostics;
        LogAnalyzerExecutionTime = logAnalyzerExecutionTime;
    }

    public bool IgnoreCompilerDiagnostics { get; }

    public bool ReportNotConfigurable { get; }

    public bool ReportSuppressedDiagnostics { get; }

    public bool LogAnalyzerExecutionTime { get; }
}
