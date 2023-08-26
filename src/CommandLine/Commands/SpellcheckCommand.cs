﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Roslynator.Spelling;
using static Roslynator.Logger;

namespace Roslynator.CommandLine;

internal class SpellcheckCommand : MSBuildWorkspaceCommand<SpellcheckCommandResult>
{
    public SpellcheckCommand(
        SpellcheckCommandLineOptions options,
        in ProjectFilter projectFilter,
        FileSystemFilter fileSystemFilter,
        SpellingData spellingData,
        Visibility visibility,
        SpellingScopeFilter scopeFilter) : base(projectFilter, fileSystemFilter)
    {
        Options = options;
        SpellingData = spellingData;
        Visibility = visibility;
        ScopeFilter = scopeFilter;
    }

    public SpellcheckCommandLineOptions Options { get; }

    public SpellingData SpellingData { get; private set; }

    public Visibility Visibility { get; }

    public SpellingScopeFilter ScopeFilter { get; }

    public string OutputPath { get; }

    public override async Task<SpellcheckCommandResult> ExecuteAsync(ProjectOrSolution projectOrSolution, CancellationToken cancellationToken = default)
    {
        AssemblyResolver.Register();

        VisibilityFilter visibilityFilter = Visibility switch
        {
            Visibility.Public => VisibilityFilter.All,
            Visibility.Internal => VisibilityFilter.Internal | VisibilityFilter.Private,
            Visibility.Private => VisibilityFilter.Private,
            _ => throw new InvalidOperationException()
        };

        var options = new SpellcheckOptions()
        {
            FileSystemFilter = FileSystemFilter,
            ScopeFilter = ScopeFilter,
            SymbolVisibility = visibilityFilter,
            MinWordLength = Options.MinWordLength,
            MaxWordLength = Options.MaxWordLength,
            IncludeGeneratedCode = Options.IncludeGeneratedCode,
#if DEBUG
            Autofix = !Options.NoAutofix,
#endif
            Interactive = Options.Interactive,
            DryRun = Options.DryRun,
        };

        CultureInfo culture = (Options.Culture is not null) ? CultureInfo.GetCultureInfo(Options.Culture) : null;

        return await FixAsync(projectOrSolution, options, culture, cancellationToken);
    }

    private async Task<SpellcheckCommandResult> FixAsync(
        ProjectOrSolution projectOrSolution,
        SpellcheckOptions options,
        IFormatProvider formatProvider = null,
        CancellationToken cancellationToken = default)
    {
        SpellcheckAnalyzer spellingFixer = null;
        ImmutableArray<SpellingFixResult> results = default;

        if (projectOrSolution.IsProject)
        {
            Project project = projectOrSolution.AsProject();

            Solution solution = project.Solution;

            spellingFixer = GetSpellingFixer(solution);

            WriteLine($"Fix '{project.Name}'", ConsoleColors.Cyan, Verbosity.Minimal);

            Stopwatch stopwatch = Stopwatch.StartNew();

            results = await spellingFixer.FixProjectAsync(project, cancellationToken);

            stopwatch.Stop();

            WriteLine($"Done fixing project '{project.FilePath}' in {stopwatch.Elapsed:mm\\:ss\\.ff}", Verbosity.Minimal);
        }
        else
        {
            Solution solution = projectOrSolution.AsSolution();

            spellingFixer = GetSpellingFixer(solution);

            results = await spellingFixer.FixSolutionAsync(f => IsMatch(f), cancellationToken);
        }

        SpellingData = spellingFixer.SpellingData;

        WriteSummary(results);

        return new SpellcheckCommandResult(
            (results.Length == 0 || (!options.DryRun && results.All(f => f.HasFix)))
                ? CommandStatus.Success
                : CommandStatus.NotSuccess,
            results);

        SpellcheckAnalyzer GetSpellingFixer(Solution solution)
        {
            return new SpellcheckAnalyzer(
                solution,
                spellingData: SpellingData,
                formatProvider: formatProvider,
                options: options);
        }
    }

    protected override void OperationCanceled(OperationCanceledException ex)
    {
        WriteLine("Spellchecking was canceled.", Verbosity.Minimal);
    }

    private void WriteSummary(ImmutableArray<SpellingFixResult> results)
    {
        if (!ShouldWrite(Verbosity.Normal))
            return;

        var isFirst = true;
        bool isDetailed = ShouldWrite(Verbosity.Detailed);
        StringComparer comparer = StringComparer.InvariantCulture;

        if (ShouldWrite(Verbosity.Normal))
        {
            foreach (IGrouping<string, SpellingFixResult> grouping in results
                .Where(f => !f.HasFix && f.ContainingValue is not null)
                .GroupBy(f => f.ContainingValue!, comparer)
                .OrderBy(f => f.Key, comparer))
            {
                if (isFirst)
                {
                    WriteLine(Verbosity.Normal);
                    WriteLine("Words containing unknown words:", Verbosity.Normal);
                    isFirst = false;
                }

                WriteLine(grouping.Key, Verbosity.Normal);

                if (isDetailed)
                    WriteMatchingLines(grouping, comparer, ConsoleColors.Green, displayContainingValue: true);
            }
        }

        isFirst = true;

        IEnumerable<SpellingFixResult> filteredResults = results;

        if (!Options.DryRun)
            filteredResults = filteredResults.Where(f => !f.HasFix);

        foreach (IGrouping<string, SpellingFixResult> grouping in filteredResults
            .GroupBy(f => f.Value, comparer)
            .OrderBy(f => f.Key, comparer))
        {
            if (isFirst)
            {
                WriteLine(Verbosity.Normal);
                WriteLine("Unknown words:", Verbosity.Normal);
                isFirst = false;
            }

            Write(grouping.Key, Verbosity.Normal);

            var isFix = false;

            if (SpellingData.Fixes.TryGetValue(grouping.Key, out ImmutableHashSet<SpellingFix> possibleFixes))
            {
                ImmutableArray<SpellingFix> fixes = possibleFixes
                    .Where(
                        f => TextUtility.GetTextCasing(f.Value) != TextCasing.Undefined
                            || string.Equals(grouping.Key, f.Value, StringComparison.OrdinalIgnoreCase))
                    .ToImmutableArray();

                if (fixes.Any())
                {
                    Write(": ", ConsoleColors.Gray, Verbosity.Normal);

                    WriteLine(
                        string.Join(", ", fixes.Select(f => TextUtility.SetTextCasing(f.Value, TextUtility.GetTextCasing(grouping.Key)))),
                        ConsoleColors.Cyan,
                        Verbosity.Normal);

                    isFix = true;
                }
            }

            if (!isFix)
                WriteLine(Verbosity.Normal);

            if (isDetailed)
                WriteMatchingLines(grouping, comparer, ConsoleColors.Green);
        }

        var any1 = false;
        var any2 = false;

        if (!Options.DryRun)
        {
            any1 = WriteResults(results, SpellingFixKind.Predefined, "Auto fixes:", comparer, isDetailed);
            any2 = WriteResults(results, SpellingFixKind.User, "User-applied fixes:", comparer, isDetailed);
        }

        if (!isFirst
            && !any1
            && !any2)
        {
            WriteLine(Verbosity.Normal);
        }
    }

    private static bool WriteResults(
        ImmutableArray<SpellingFixResult> results,
        SpellingFixKind kind,
        string heading,
        StringComparer comparer,
        bool isDetailed)
    {
        var isFirst = true;

        foreach (IGrouping<string, SpellingFixResult> grouping in results
            .Where(f => f.Kind == kind)
            .OrderBy(f => f.Value)
            .ThenBy(f => f.Replacement)
            .GroupBy(f => $"{f.Value}: {f.Replacement}"))
        {
            if (isFirst)
            {
                WriteLine(Verbosity.Normal);
                WriteLine(heading, Verbosity.Normal);
                isFirst = false;
            }

            WriteLine(grouping.Key, Verbosity.Normal);

            if (isDetailed)
                WriteMatchingLines(grouping, comparer, ConsoleColors.Cyan);
        }

        return !isFirst;
    }

    private static void WriteMatchingLines(
        IGrouping<string, SpellingFixResult> grouping,
        StringComparer comparer,
        ConsoleColors colors,
        bool displayContainingValue = false)
    {
        foreach (IGrouping<string, SpellingFixResult> grouping2 in grouping
            .GroupBy(f => f.FilePath)
            .OrderBy(f => f.Key, comparer))
        {
            Write("  ", Verbosity.Detailed);
            WriteLine(grouping2.Key, ConsoleColors.Cyan, Verbosity.Detailed);

            foreach (SpellingFixResult result in grouping2
                .Where(f => f.SourceText is not null)
                .Distinct(SpellingFixResultEqualityComparer.ValueAndLineSpan)
                .OrderBy(f => f.LineNumber)
                .ThenBy(f => f.LineSpan.StartLinePosition.Character))
            {
                Write("    ", Verbosity.Detailed);
                Write(result.LineNumber.ToString(), ConsoleColors.Cyan, Verbosity.Detailed);
                Write(" ", Verbosity.Detailed);

                int lineStartIndex = result.LineStartIndex;
                int lineEndIndex = result.LineEndIndex;

                string value;
                int index;
                int endIndex;

                if (displayContainingValue)
                {
                    value = result.ContainingValue!;
                    index = result.ContainingValueIndex;
                    endIndex = result.ContainingValueIndex + value.Length;
                }
                else
                {
                    value = result.Replacement ?? result.Value;
                    index = result.Index;
                    endIndex = result.Index + result.Length;
                }

                Write(result.SourceText.Substring(lineStartIndex, index - lineStartIndex), Verbosity.Detailed);
                Out?.Write(">>>", Verbosity.Detailed);
                Write(value, colors, Verbosity.Detailed);
                Out?.Write("<<<", Verbosity.Detailed);
                WriteLine(result.SourceText.Substring(endIndex, lineEndIndex - endIndex), Verbosity.Detailed);
            }
        }
    }

    protected override void ProcessResults(IList<SpellcheckCommandResult> results)
    {
        if (results.Count <= 1)
            return;

        WriteSummary(results.SelectMany(f => f.SpellingResults).ToImmutableArray());
    }

#if DEBUG
    public static void SaveNewValues(
        SpellingData spellingData,
        FixList originalFixList,
        List<SpellingFixResult> results,
        string newWordsPath = null,
        string newFixesPath = null,
        string outputPath = null,
        CancellationToken cancellationToken = default)
    {
        if (newFixesPath is not null)
        {
            Dictionary<string, List<SpellingFix>> fixes = spellingData.Fixes.Items.ToDictionary(
                f => f.Key,
                f => f.Value.ToList(),
                WordList.DefaultComparer);

            if (fixes.Count > 0)
            {
                if (File.Exists(newFixesPath))
                {
                    foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in FixList.LoadFile(newFixesPath!).Items)
                    {
                        if (fixes.TryGetValue(kvp.Key, out List<SpellingFix> list))
                        {
                            list.AddRange(kvp.Value);
                        }
                        else
                        {
                            fixes[kvp.Key] = kvp.Value.ToList();
                        }
                    }
                }

                foreach (KeyValuePair<string, ImmutableHashSet<SpellingFix>> kvp in originalFixList.Items)
                {
                    if (fixes.TryGetValue(kvp.Key, out List<SpellingFix> list))
                    {
                        list.RemoveAll(f => kvp.Value.Contains(f, SpellingFixComparer.InvariantCultureIgnoreCase));

                        if (list.Count == 0)
                            fixes.Remove(kvp.Key);
                    }
                }
            }

            ImmutableDictionary<string, ImmutableHashSet<SpellingFix>> newFixes = fixes.ToImmutableDictionary(
                f => f.Key.ToLowerInvariant(),
                f => f.Value
                    .Select(f => f.WithValue(f.Value.ToLowerInvariant()))
                    .Distinct(SpellingFixComparer.InvariantCultureIgnoreCase)
                    .ToImmutableHashSet(SpellingFixComparer.InvariantCultureIgnoreCase));

            if (newFixes.Count > 0)
                FixList.Save(newFixesPath, newFixes);
        }

        const StringComparison comparison = StringComparison.InvariantCulture;
        StringComparer comparer = StringComparerUtility.FromComparison(comparison);

        if (newWordsPath is not null)
        {
            HashSet<string> newValues = spellingData.IgnoredValues
                .Concat(results.Select(f => f.Value))
                .Except(spellingData.Fixes.Items.Select(f => f.Key), WordList.DefaultComparer)
                .ToHashSet(comparer);

            var possibleNewFixes = new List<string>();

            foreach (string value in newValues)
            {
                string valueLower = value.ToLowerInvariant();

                ImmutableArray<string> possibleFixes = SpellingFixProvider.SwapLetters(valueLower, spellingData);

                if (possibleFixes.Length == 0
                    && value.Length >= 8)
                {
                    possibleFixes = SpellingFixProvider.Fuzzy(valueLower, spellingData, cancellationToken);
                }

                possibleNewFixes.AddRange(possibleFixes.Select(f => FixList.GetItemText(value, f)));
            }

            IEnumerable<string> compoundWords = results
                .Select(f => f.ContainingValue)
                .Where(f => f is not null)
                .Select(f => f!);

            WordList.Save(newWordsPath, newValues.Concat(compoundWords).Concat(possibleNewFixes), comparer, merge: true);
        }

        if (outputPath is not null
            && results.Count > 0)
        {
            using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
            {
                foreach (IGrouping<string, SpellingFixResult> grouping in results
                    .GroupBy(f => f.Value, comparer)
                    .OrderBy(f => f.Key, comparer))
                {
                    writer.WriteLine(grouping.Key);

                    foreach (IGrouping<string, SpellingFixResult> grouping2 in grouping
                        .Where(f => f.SourceText is not null)
                        .GroupBy(f => f.FilePath)
                        .OrderBy(f => f.Key, comparer))
                    {
                        writer.Write("  ");
                        writer.WriteLine(grouping2.Key);

                        foreach (SpellingFixResult result in grouping2.OrderBy(f => f.LineSpan.StartLine()))
                        {
                            writer.Write("    ");
                            writer.Write(result.LineNumber);
                            writer.Write(" ");

                            int lineStartIndex = result.LineStartIndex;
                            int lineEndIndex = result.LineEndIndex;
                            string value = result.Replacement ?? result.Value;
                            int index = result.Index;
                            int endIndex = result.Index + result.Length;

                            Write(result.SourceText.Substring(lineStartIndex, index - lineStartIndex), Verbosity.Detailed);
                            Out?.Write(">>>", Verbosity.Detailed);
                            Write(value, ConsoleColors.Green, Verbosity.Detailed);
                            Out?.Write("<<<", Verbosity.Detailed);
                            WriteLine(result.SourceText.Substring(endIndex, lineEndIndex - endIndex), Verbosity.Detailed);
                        }
                    }
                }
            }
        }
    }
#endif
}
