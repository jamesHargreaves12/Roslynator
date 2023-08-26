﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Roslynator.CodeMetrics;
using Roslynator.Host.Mef;
using static Roslynator.Logger;

namespace Roslynator.CommandLine;

internal class LogicalLinesOfCodeCommand : AbstractLinesOfCodeCommand<LinesOfCodeCommandResult>
{
    public LogicalLinesOfCodeCommand(LogicalLinesOfCodeCommandLineOptions options, in ProjectFilter projectFilter, FileSystemFilter fileSystemFilter) : base(projectFilter, fileSystemFilter)
    {
        Options = options;
    }

    public LogicalLinesOfCodeCommandLineOptions Options { get; }

    public override async Task<LinesOfCodeCommandResult> ExecuteAsync(ProjectOrSolution projectOrSolution, CancellationToken cancellationToken = default)
    {
        var codeMetricsOptions = new CodeMetricsOptions(includeGenerated: Options.IncludeGeneratedCode);

        CodeMetricsInfo codeMetrics;

        if (projectOrSolution.IsProject)
        {
            Project project = projectOrSolution.AsProject();

            ICodeMetricsService service = MefWorkspaceServices.Default.GetService<ICodeMetricsService>(project.Language);

            if (service is not null)
            {
                codeMetrics = await CountLinesAsync(project, service, codeMetricsOptions, cancellationToken);
            }
            else
            {
                WriteLine($"Cannot count logical lines for language '{project.Language}'", ConsoleColors.Yellow, Verbosity.Minimal);
                return new LinesOfCodeCommandResult(CommandStatus.Fail, default);
            }
        }
        else
        {
            ImmutableDictionary<ProjectId, CodeMetricsInfo> codeMetricsByProject = await CountLinesAsync(projectOrSolution.AsSolution(), codeMetricsOptions, cancellationToken);

            codeMetrics = CodeMetricsInfo.Create(codeMetricsByProject.Values);
        }

        return new LinesOfCodeCommandResult(CommandStatus.Success, codeMetrics);
    }

    private async Task<CodeMetricsInfo> CountLinesAsync(Project project, ICodeMetricsService service, CodeMetricsOptions options, CancellationToken cancellationToken)
    {
        WriteLine($"Count logical lines for '{project.Name}'", ConsoleColors.Cyan, Verbosity.Minimal);

        Stopwatch stopwatch = Stopwatch.StartNew();

        CodeMetricsInfo codeMetrics = await service.CountLinesAsync(
            project,
            LinesOfCodeKind.Logical,
            FileSystemFilter,
            options,
            cancellationToken);

        stopwatch.Stop();

        WriteMetrics(
            codeMetrics.CodeLineCount,
            codeMetrics.WhitespaceLineCount,
            codeMetrics.CommentLineCount,
            codeMetrics.PreprocessorDirectiveLineCount,
            codeMetrics.TotalLineCount);

        WriteLine(Verbosity.Minimal);
        WriteLine($"Done counting logical lines for '{project.FilePath}' in {stopwatch.Elapsed:mm\\:ss\\.ff}", Verbosity.Normal);

        return codeMetrics;
    }

    private async Task<ImmutableDictionary<ProjectId, CodeMetricsInfo>> CountLinesAsync(Solution solution, CodeMetricsOptions options, CancellationToken cancellationToken)
    {
        WriteLine($"Count logical lines for solution '{solution.FilePath}'", ConsoleColors.Cyan, Verbosity.Minimal);

        IEnumerable<Project> projects = FilterProjects(solution);

        Stopwatch stopwatch = Stopwatch.StartNew();

        ImmutableDictionary<ProjectId, CodeMetricsInfo> codeMetrics = await CountLinesAsync(projects, LinesOfCodeKind.Logical, options, cancellationToken);

        stopwatch.Stop();

        if (codeMetrics.Count > 0)
        {
            WriteLine(Verbosity.Normal);
            WriteLine("Logical lines of code by project:", Verbosity.Normal);

            WriteLinesOfCode(solution, codeMetrics);
        }

        WriteMetrics(
            codeMetrics.Sum(f => f.Value.CodeLineCount),
            codeMetrics.Sum(f => f.Value.WhitespaceLineCount),
            codeMetrics.Sum(f => f.Value.CommentLineCount),
            codeMetrics.Sum(f => f.Value.PreprocessorDirectiveLineCount),
            codeMetrics.Sum(f => f.Value.TotalLineCount));

        WriteLine(Verbosity.Minimal);
        WriteLine($"Done counting logical lines for solution '{solution.FilePath}' in {stopwatch.Elapsed:mm\\:ss\\.ff}", Verbosity.Normal);

        return codeMetrics;
    }

    private static void WriteMetrics(
        int totalCodeLineCount,
        int totalWhitespaceLineCount,
        int totalCommentLineCount,
        int totalPreprocessorDirectiveLineCount,
        int totalLineCount)
    {
        string totalCodeLines = totalCodeLineCount.ToString("n0");
        string totalWhitespaceLines = totalWhitespaceLineCount.ToString("n0");
        string totalCommentLines = totalCommentLineCount.ToString("n0");
        string totalPreprocessorDirectiveLines = totalPreprocessorDirectiveLineCount.ToString("n0");
        string totalLines = totalLineCount.ToString("n0");

        int maxDigits = Math.Max(
            totalCodeLines.Length,
            Math.Max(
                totalWhitespaceLines.Length,
                Math.Max(
                    totalCommentLines.Length,
                    Math.Max(totalPreprocessorDirectiveLines.Length, totalLines.Length))));

        WriteLine(Verbosity.Minimal);
        WriteLine($"{totalCodeLines.PadLeft(maxDigits)} {totalCodeLineCount / (double)totalLineCount,5:P0} logical lines of code", ConsoleColors.Green, Verbosity.Minimal);
        WriteLine($"{totalWhitespaceLines.PadLeft(maxDigits)} {totalWhitespaceLineCount / (double)totalLineCount,5:P0} white-space lines", Verbosity.Minimal);
        WriteLine($"{totalCommentLines.PadLeft(maxDigits)} {totalCommentLineCount / (double)totalLineCount,5:P0} comment lines", Verbosity.Minimal);
        WriteLine($"{totalPreprocessorDirectiveLines.PadLeft(maxDigits)} {totalPreprocessorDirectiveLineCount / (double)totalLineCount,5:P0} preprocessor directive lines", Verbosity.Minimal);
        WriteLine($"{totalLines.PadLeft(maxDigits)} {totalLineCount / (double)totalLineCount,5:P0} total lines", Verbosity.Minimal);
    }

    protected override void ProcessResults(IList<LinesOfCodeCommandResult> results)
    {
        if (results.Count <= 1)
            return;

        WriteMetrics(
            totalCodeLineCount: results.Sum(f => f.Metrics.CodeLineCount),
            totalWhitespaceLineCount: results.Sum(f => f.Metrics.WhitespaceLineCount),
            totalCommentLineCount: results.Sum(f => f.Metrics.CommentLineCount),
            totalPreprocessorDirectiveLineCount: results.Sum(f => f.Metrics.PreprocessorDirectiveLineCount),
            totalLineCount: results.Sum(f => f.Metrics.TotalLineCount));
    }

    protected override void OperationCanceled(OperationCanceledException ex)
    {
        WriteLine("Logical lines counting was canceled.", Verbosity.Quiet);
    }
}
