﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Roslynator.Testing;

/// <summary>
/// Represents test data for a compiler diagnostic fix.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class CompilerDiagnosticFixTestData
{
    /// <summary>
    /// Initializes a new instance of <see cref="CompilerDiagnosticFixTestData"/>
    /// </summary>
    /// <param name="diagnosticId"></param>
    /// <param name="source"></param>
    /// <param name="additionalFiles"></param>
    /// <param name="equivalenceKey"></param>
    public CompilerDiagnosticFixTestData(
        string diagnosticId,
        string source,
        IEnumerable<AdditionalFile> additionalFiles = null,
        string equivalenceKey = null)
    {
        DiagnosticId = diagnosticId ?? throw new ArgumentNullException(nameof(diagnosticId));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        AdditionalFiles = additionalFiles?.ToImmutableArray() ?? ImmutableArray<AdditionalFile>.Empty;
        EquivalenceKey = equivalenceKey;
    }

    internal CompilerDiagnosticFixTestData(CompilerDiagnosticFixTestData other)
        : this(
            diagnosticId: other.DiagnosticId,
            source: other.Source,
            additionalFiles: other.AdditionalFiles,
            equivalenceKey: other.EquivalenceKey)
    {
    }

    /// <summary>
    /// Gets compiler diagnostic ID to be fixed.
    /// </summary>
    public string DiagnosticId { get; }

    /// <summary>
    /// Gets a source code that will report specified compiler diagnostic.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets additional source files.
    /// </summary>
    public ImmutableArray<AdditionalFile> AdditionalFiles { get; }

    /// <summary>
    /// Gets code action's equivalence key.
    /// </summary>
    public string EquivalenceKey { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"{DiagnosticId}  {Source}";

    /// <summary>
    /// Creates and return new instance of <see cref="CompilerDiagnosticFixTestData"/> updated with specified values.
    /// </summary>
    [Obsolete("This method is obsolete and will be removed in future version.")]
    public CompilerDiagnosticFixTestData Update(
        string diagnosticId,
        string source,
        IEnumerable<AdditionalFile> additionalFiles,
        string equivalenceKey)
    {
        return new CompilerDiagnosticFixTestData(
            diagnosticId: diagnosticId,
            source: source,
            additionalFiles: additionalFiles,
            equivalenceKey: equivalenceKey);
    }
}
