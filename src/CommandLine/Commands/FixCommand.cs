﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Roslynator.CodeFixes;
using static Roslynator.Logger;

namespace Roslynator.CommandLine;

internal class FixCommand : MSBuildWorkspaceCommand<FixCommandResult>
{
    public FixCommand(
        FixCommandLineOptions options,
        DiagnosticSeverity severityLevel,
        IEnumerable<KeyValuePair<string, string>> diagnosticFixMap,
        IEnumerable<KeyValuePair<string, string>> diagnosticFixerMap,
        FixAllScope fixAllScope,
        in ProjectFilter projectFilter,
        FileSystemFilter fileSystemFilter) : base(projectFilter, fileSystemFilter)
    {
        Options = options;
        SeverityLevel = severityLevel;
        DiagnosticFixMap = diagnosticFixMap;
        DiagnosticFixerMap = diagnosticFixerMap;
        FixAllScope = fixAllScope;
    }

    public FixCommandLineOptions Options { get; }

    public DiagnosticSeverity SeverityLevel { get; }

    public IEnumerable<KeyValuePair<string, string>> DiagnosticFixMap { get; }

    public IEnumerable<KeyValuePair<string, string>> DiagnosticFixerMap { get; }

    public FixAllScope FixAllScope { get; }

    public override async Task<FixCommandResult> ExecuteAsync(ProjectOrSolution projectOrSolution, CancellationToken cancellationToken = default)
    {
        AssemblyResolver.Register();

        var codeFixerOptions = new CodeFixerOptions(
            fileSystemFilter: FileSystemFilter,
            severityLevel: SeverityLevel,
            ignoreCompilerErrors: Options.IgnoreCompilerErrors,
            ignoreAnalyzerReferences: Options.IgnoreAnalyzerReferences,
            supportedDiagnosticIds: Options.SupportedDiagnostics,
            ignoredDiagnosticIds: Options.IgnoredDiagnostics,
            ignoredCompilerDiagnosticIds: Options.IgnoredCompilerDiagnostics,
            diagnosticIdsFixableOneByOne: Options.DiagnosticsFixableOneByOne,
            diagnosticFixMap: DiagnosticFixMap,
            diagnosticFixerMap: DiagnosticFixerMap,
            fixAllScope: FixAllScope,
            fileBanner: Options.FileBanner,
            maxIterations: Options.MaxIterations,
            batchSize: Options.BatchSize,
            format: Options.Format);

        IEnumerable<AnalyzerAssembly> analyzerAssemblies = Options.AnalyzerAssemblies
            .SelectMany(path => AnalyzerAssemblyLoader.LoadFrom(path).Select(info => info.AnalyzerAssembly));

        CultureInfo culture = (Options.Culture is not null) ? CultureInfo.GetCultureInfo(Options.Culture) : null;

        return await FixAsync(projectOrSolution, analyzerAssemblies, codeFixerOptions, culture, cancellationToken);
    }

    private async Task<FixCommandResult> FixAsync(
        ProjectOrSolution projectOrSolution,
        IEnumerable<AnalyzerAssembly> analyzerAssemblies,
        CodeFixerOptions codeFixerOptions,
        IFormatProvider formatProvider = null,
        CancellationToken cancellationToken = default)
    {
        foreach (string id in codeFixerOptions.IgnoredCompilerDiagnosticIds.OrderBy(f => f))
            WriteLine($"Ignore compiler diagnostic '{id}'", Verbosity.Diagnostic);

        foreach (string id in codeFixerOptions.IgnoredDiagnosticIds.OrderBy(f => f))
            WriteLine($"Ignore diagnostic '{id}'", Verbosity.Diagnostic);

        ImmutableArray<ProjectFixResult> results;

        if (projectOrSolution.IsProject)
        {
            Project project = projectOrSolution.AsProject();

            Solution solution = project.Solution;

            CodeFixer codeFixer = GetCodeFixer(solution);

            WriteLine($"Fix '{project.Name}'", ConsoleColors.Cyan, Verbosity.Minimal);

            Stopwatch stopwatch = Stopwatch.StartNew();

            ProjectFixResult result = await codeFixer.FixProjectAsync(project, cancellationToken);

            stopwatch.Stop();

            WriteLine($"Done fixing project '{project.FilePath}' in {stopwatch.Elapsed:mm\\:ss\\.ff}", Verbosity.Minimal);

            results = ImmutableArray.Create(result);
        }
        else
        {
            Solution solution = projectOrSolution.AsSolution();

            CodeFixer codeFixer = GetCodeFixer(solution);

            results = await codeFixer.FixSolutionAsync(f => IsMatch(f), cancellationToken);
        }

        WriteProjectFixResults(results, codeFixerOptions, formatProvider);

        return new FixCommandResult(
            (results.Any(f => f.UnfixedDiagnostics.Length > 0)) ? CommandStatus.NotSuccess : CommandStatus.Success,
            results);

        CodeFixer GetCodeFixer(Solution solution)
        {
            var analyzerLoader = new AnalyzerLoader(analyzerAssemblies, codeFixerOptions);

            analyzerLoader.AnalyzerAssemblyAdded += (sender, args) =>
            {
                AnalyzerAssembly analyzerAssembly = args.AnalyzerAssembly;

                if (analyzerAssembly.Name.EndsWith(".Analyzers")
                    || analyzerAssembly.HasAnalyzers
                    || analyzerAssembly.HasFixers)
                {
                    WriteLine($"Add analyzer assembly '{analyzerAssembly.FullName}'", ConsoleColors.DarkGray, Verbosity.Detailed);
                }
            };

            return new CodeFixer(
                solution,
                analyzerLoader: analyzerLoader,
                formatProvider: formatProvider,
                options: codeFixerOptions);
        }
    }

    protected override void ProcessResults(IList<FixCommandResult> results)
    {
        if (results.Count <= 1)
            return;

        WriteFixResults(results.SelectMany(f => f.FixResults));
    }

    private static void WriteFixResults(
        IEnumerable<ProjectFixResult> results,
        IFormatProvider formatProvider = null)
    {
        if (results.Any(f => f.NumberOfAddedFileBanners >= 0))
        {
            int numberOfAddedFileBanners = results.Where(f => f.NumberOfAddedFileBanners >= 0).Sum(f => f.NumberOfAddedFileBanners);

            WriteLine(Verbosity.Normal);
            WriteLine($"{numberOfAddedFileBanners} file {((numberOfAddedFileBanners == 1) ? "banner" : "banners")} added", Verbosity.Normal);
        }

        if (results.Any(f => f.NumberOfFormattedDocuments >= 0))
        {
            int numberOfFormattedDocuments = results.Where(f => f.NumberOfFormattedDocuments > 0).Sum(f => f.NumberOfFormattedDocuments);

            WriteLine(Verbosity.Normal);
            WriteLine($"{numberOfFormattedDocuments} {((numberOfFormattedDocuments == 1) ? "document" : "documents")} formatted", Verbosity.Normal);
        }

        WriteDiagnostics(results.SelectMany(f => f.UnfixableDiagnostics), "Unfixable diagnostics:");
        WriteDiagnostics(results.SelectMany(f => f.UnfixedDiagnostics), "Unfixed diagnostics:");
        WriteDiagnostics(results.SelectMany(f => f.FixedDiagnostics), "Fixed diagnostics:");

        int fixedCount = results.Sum(f => f.FixedDiagnostics.Length);

        WriteLine(Verbosity.Minimal);
        WriteLine($"{fixedCount} {((fixedCount == 1) ? "diagnostic" : "diagnostics")} fixed", ConsoleColors.Green, Verbosity.Minimal);

        void WriteDiagnostics(
            IEnumerable<DiagnosticInfo> diagnostics,
            string title)
        {
            List<(DiagnosticDescriptor descriptor, int count)> diagnosticsById = diagnostics
                .GroupBy(f => f.Descriptor, DiagnosticDescriptorComparer.Id)
                .Select(f => (descriptor: f.Key, count: f.Count()))
                .OrderByDescending(f => f.count)
                .ThenBy(f => f.descriptor.Id)
                .ToList();

            if (diagnosticsById.Count > 0)
            {
                WriteLine(Verbosity.Normal);
                WriteLine(title, Verbosity.Normal);

                int maxIdLength = diagnosticsById.Max(f => f.descriptor.Id.Length);
                int maxCountLength = diagnosticsById.Max(f => f.count.ToString("n0").Length);

                foreach ((DiagnosticDescriptor descriptor, int count) in diagnosticsById)
                {
                    WriteLine($"  {count.ToString("n0").PadLeft(maxCountLength)} {descriptor.Id.PadRight(maxIdLength)} {descriptor.Title.ToString(formatProvider)}", Verbosity.Normal);
                }
            }
        }
    }

    private static void WriteProjectFixResults(
        IList<ProjectFixResult> results,
        CodeFixerOptions options,
        IFormatProvider formatProvider = null)
    {
        if (options.FileBannerLines.Any())
        {
            int count = results.Where(f => f.NumberOfAddedFileBanners >= 0).Sum(f => f.NumberOfAddedFileBanners);
            WriteLine(Verbosity.Normal);
            WriteLine($"{count} file {((count == 1) ? "banner" : "banners")} added", Verbosity.Normal);
        }

        if (options.Format)
        {
            int count = results.Where(f => f.NumberOfFormattedDocuments >= 0).Sum(f => f.NumberOfFormattedDocuments);
            WriteLine(Verbosity.Normal);
            WriteLine($"{count} {((count == 1) ? "document" : "documents")} formatted", Verbosity.Normal);
        }

        WriteFixSummary(
            results.SelectMany(f => f.FixedDiagnostics),
            results.SelectMany(f => f.UnfixedDiagnostics),
            results.SelectMany(f => f.UnfixableDiagnostics),
            addEmptyLine: true,
            formatProvider: formatProvider,
            verbosity: Verbosity.Normal);

        int fixedCount = results.Sum(f => f.FixedDiagnostics.Length);

        WriteLine(Verbosity.Minimal);
        WriteLine($"{fixedCount} {((fixedCount == 1) ? "diagnostic" : "diagnostics")} fixed", ConsoleColors.Green, Verbosity.Minimal);
    }

    private static void WriteFixSummary(
        IEnumerable<DiagnosticInfo> fixedDiagnostics,
        IEnumerable<DiagnosticInfo> unfixedDiagnostics,
        IEnumerable<DiagnosticInfo> unfixableDiagnostics,
        string indentation = null,
        bool addEmptyLine = false,
        IFormatProvider formatProvider = null,
        Verbosity verbosity = Verbosity.None)
    {
        WriteDiagnosticRules(unfixableDiagnostics, "Unfixable diagnostics:");
        WriteDiagnosticRules(unfixedDiagnostics, "Unfixed diagnostics:");
        WriteDiagnosticRules(fixedDiagnostics, "Fixed diagnostics:");

        void WriteDiagnosticRules(
            IEnumerable<DiagnosticInfo> diagnostics,
            string title)
        {
            List<(DiagnosticDescriptor descriptor, ImmutableArray<DiagnosticInfo> diagnostics)> diagnosticsById = diagnostics
                .GroupBy(f => f.Descriptor, DiagnosticDescriptorComparer.Id)
                .Select(f => (descriptor: f.Key, diagnostics: f.ToImmutableArray()))
                .OrderByDescending(f => f.diagnostics.Length)
                .ThenBy(f => f.descriptor.Id)
                .ToList();

            if (diagnosticsById.Count > 0)
            {
                if (addEmptyLine)
                    WriteLine(verbosity);

                Write(indentation, verbosity);
                WriteLine(title, verbosity);

                int maxIdLength = diagnosticsById.Max(f => f.descriptor.Id.Length);
                int maxCountLength = diagnosticsById.Max(f => f.diagnostics.Length.ToString("n0").Length);

                foreach ((DiagnosticDescriptor descriptor, ImmutableArray<DiagnosticInfo> diagnostics2) in diagnosticsById)
                {
                    Write(indentation, verbosity);
                    WriteLine($"  {diagnostics2.Length.ToString("n0").PadLeft(maxCountLength)} {descriptor.Id.PadRight(maxIdLength)} {descriptor.Title.ToString(formatProvider)}", verbosity);
                }
            }
        }
    }

    protected override void OperationCanceled(OperationCanceledException ex)
    {
        WriteLine("Fixing was canceled.", Verbosity.Quiet);
    }
}
