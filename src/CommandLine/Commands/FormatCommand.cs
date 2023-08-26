﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Roslynator.Formatting;
using Roslynator.Host.Mef;
using static Roslynator.Logger;

namespace Roslynator.CommandLine;

internal class FormatCommand : MSBuildWorkspaceCommand<FormatCommandResult>
{
    public FormatCommand(FormatCommandLineOptions options, in ProjectFilter projectFilter, FileSystemFilter fileSystemFilter) : base(projectFilter, fileSystemFilter)
    {
        Options = options;
    }

    public FormatCommandLineOptions Options { get; }

    public override async Task<FormatCommandResult> ExecuteAsync(ProjectOrSolution projectOrSolution, CancellationToken cancellationToken = default)
    {
        ImmutableArray<DocumentId> formattedDocuments;

        var options = new CodeFormatterOptions(fileSystemFilter: FileSystemFilter, includeGeneratedCode: Options.IncludeGeneratedCode);

        if (projectOrSolution.IsProject)
        {
            Project project = projectOrSolution.AsProject();

            formattedDocuments = await FormatProjectAsync(project, options, cancellationToken);
        }
        else
        {
            Solution solution = projectOrSolution.AsSolution();

            formattedDocuments = await FormatSolutionAsync(solution, options, cancellationToken);
        }

        return new FormatCommandResult((formattedDocuments.Length > 0) ? CommandStatus.NotSuccess : CommandStatus.Success, formattedDocuments.Length);
    }

    private async Task<ImmutableArray<DocumentId>> FormatSolutionAsync(Solution solution, CodeFormatterOptions options, CancellationToken cancellationToken)
    {
        string solutionDirectory = Path.GetDirectoryName(solution.FilePath);

        WriteLine($"Analyze solution '{solution.FilePath}'", ConsoleColors.Cyan, Verbosity.Minimal);

        Stopwatch stopwatch = Stopwatch.StartNew();

        var changedDocuments = new ConcurrentBag<ImmutableArray<DocumentId>>();

#if NETFRAMEWORK
        await Task.CompletedTask;

        Parallel.ForEach(
            FilterProjects(solution),
            project =>
            {
                WriteLine($"  Analyze '{project.Name}'", Verbosity.Minimal);

                ISyntaxFactsService syntaxFacts = MefWorkspaceServices.Default.GetService<ISyntaxFactsService>(project.Language);

                Project newProject = CodeFormatter.FormatProjectAsync(project, syntaxFacts, options, cancellationToken).Result;

                ImmutableArray<DocumentId> formattedDocuments = CodeFormatter.GetFormattedDocumentsAsync(project, newProject, syntaxFacts).Result;

                if (formattedDocuments.Any())
                {
                    changedDocuments.Add(formattedDocuments);
                    LogHelpers.WriteFormattedDocuments(formattedDocuments, project, solutionDirectory);
                }

                WriteLine($"  Done analyzing '{project.Name}'", Verbosity.Normal);
            });
#else
        await Parallel.ForEachAsync(
            FilterProjects(solution),
            cancellationToken,
            async (project, cancellationToken) =>
            {
                WriteLine($"  Analyze '{project.Name}'", Verbosity.Minimal);

                ISyntaxFactsService syntaxFacts = MefWorkspaceServices.Default.GetService<ISyntaxFactsService>(project.Language);

                Project newProject = CodeFormatter.FormatProjectAsync(project, syntaxFacts, options, cancellationToken).Result;

                ImmutableArray<DocumentId> formattedDocuments = await CodeFormatter.GetFormattedDocumentsAsync(project, newProject, syntaxFacts);

                if (formattedDocuments.Any())
                {
                    changedDocuments.Add(formattedDocuments);
                    LogHelpers.WriteFormattedDocuments(formattedDocuments, project, solutionDirectory);
                }

                WriteLine($"  Done analyzing '{project.Name}'", Verbosity.Normal);
            });
#endif

        if (!changedDocuments.IsEmpty)
        {
            Solution newSolution = solution;

            foreach (DocumentId documentId in changedDocuments.SelectMany(f => f))
            {
                SourceText sourceText = await solution.GetDocument(documentId).GetTextAsync(cancellationToken);

                newSolution = newSolution.WithDocumentText(documentId, sourceText);
            }

            WriteLine($"Apply changes to solution '{solution.FilePath}'", Verbosity.Normal);

            if (!solution.Workspace.TryApplyChanges(newSolution))
            {
                Debug.Fail($"Cannot apply changes to solution '{solution.FilePath}'");
                WriteLine($"Cannot apply changes to solution '{solution.FilePath}'", ConsoleColors.Yellow, Verbosity.Diagnostic);
            }
        }

        int count = changedDocuments.Sum(f => f.Length);

        WriteLine(Verbosity.Minimal);
        WriteLine($"{count} {((count == 1) ? "document" : "documents")} formatted", ConsoleColors.Green, Verbosity.Minimal);

        WriteLine(Verbosity.Minimal);
        WriteLine($"Done formatting solution '{solution.FilePath}' in {stopwatch.Elapsed:mm\\:ss\\.ff}", Verbosity.Minimal);

        return changedDocuments.SelectMany(f => f).ToImmutableArray();
    }

    private static async Task<ImmutableArray<DocumentId>> FormatProjectAsync(Project project, CodeFormatterOptions options, CancellationToken cancellationToken)
    {
        Solution solution = project.Solution;

        string solutionDirectory = Path.GetDirectoryName(solution.FilePath);

        WriteLine($"  Analyze '{project.Name}'", Verbosity.Minimal);

        ISyntaxFactsService syntaxFacts = MefWorkspaceServices.Default.GetService<ISyntaxFactsService>(project.Language);

        Project newProject = await CodeFormatter.FormatProjectAsync(project, syntaxFacts, options, cancellationToken);

        ImmutableArray<DocumentId> formattedDocuments = await CodeFormatter.GetFormattedDocumentsAsync(project, newProject, syntaxFacts);

        LogHelpers.WriteFormattedDocuments(formattedDocuments, project, solutionDirectory);

        if (formattedDocuments.Length > 0)
        {
            Solution newSolution = newProject.Solution;

            WriteLine($"Apply changes to solution '{newSolution.FilePath}'", Verbosity.Normal);

            if (!solution.Workspace.TryApplyChanges(newSolution))
            {
                Debug.Fail($"Cannot apply changes to solution '{newSolution.FilePath}'");
                WriteLine($"Cannot apply changes to solution '{newSolution.FilePath}'", ConsoleColors.Yellow, Verbosity.Diagnostic);
            }
        }

        WriteSummary(formattedDocuments.Length);

        return formattedDocuments;
    }

    protected override void ProcessResults(IList<FormatCommandResult> results)
    {
        if (results.Count <= 1)
            return;

        WriteSummary(results.Sum(f => f.Count));
    }

    private static void WriteSummary(int count)
    {
        WriteLine(Verbosity.Minimal);
        WriteLine($"{count} {((count == 1) ? "document" : "documents")} formatted", ConsoleColors.Green, Verbosity.Minimal);
    }

    protected override void OperationCanceled(OperationCanceledException ex)
    {
        WriteLine("Formatting was canceled.", Verbosity.Minimal);
    }
}
