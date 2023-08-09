﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Roslynator.Testing;

/// <summary>
/// Gets test data for a code refactoring.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class RefactoringTestData
{
    /// <summary>
    /// Initializes a new instance of <see cref="RefactoringTestData"/>.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="spans"></param>
    /// <param name="additionalFiles"></param>
    /// <param name="equivalenceKey"></param>
    public RefactoringTestData(
        string source,
        IEnumerable<TextSpan> spans,
        IEnumerable<AdditionalFile> additionalFiles = null,
        string equivalenceKey = null)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Spans = spans?.ToImmutableArray() ?? ImmutableArray<TextSpan>.Empty;
        AdditionalFiles = additionalFiles?.ToImmutableArray() ?? ImmutableArray<AdditionalFile>.Empty;
        EquivalenceKey = equivalenceKey;
    }

    /// <summary>
    /// Gets a source code to be refactored.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets text spans on which a code refactoring will be applied.
    /// </summary>
    public ImmutableArray<TextSpan> Spans { get; }

    /// <summary>
    /// Gets additional source files.
    /// </summary>
    public ImmutableArray<AdditionalFile> AdditionalFiles { get; }

    /// <summary>
    /// Gets code action's equivalence key.
    /// </summary>
    public string EquivalenceKey { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => Source;

    internal RefactoringTestData(RefactoringTestData other)
        : this(
            source: other.Source,
            spans: other.Spans,
            additionalFiles: other.AdditionalFiles,
            equivalenceKey: other.EquivalenceKey)
    {
    }

    /// <summary>
    /// Creates and return new instance of <see cref="RefactoringTestData"/> updated with specified values.
    /// </summary>
    [Obsolete("This method is obsolete and will be removed in future version.")]
    public RefactoringTestData Update(
        string source,
        IEnumerable<TextSpan> spans,
        IEnumerable<AdditionalFile> additionalFiles,
        string equivalenceKey)
    {
        return new RefactoringTestData(
            source: source,
            spans: spans,
            additionalFiles: additionalFiles,
            equivalenceKey: equivalenceKey);
    }
}
