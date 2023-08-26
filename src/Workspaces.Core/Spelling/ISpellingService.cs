﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host;

namespace Roslynator.Spelling;

internal interface ISpellingService : ILanguageService
{
    ISyntaxFactsService SyntaxFacts { get; }

    DiagnosticAnalyzer CreateAnalyzer(SpellingData spellingData, SpellcheckOptions options);

    ImmutableArray<Diagnostic> AnalyzeSpelling(
        SyntaxNode node,
        SpellingData spellingData,
        SpellcheckOptions options,
        CancellationToken cancellationToken);

    SpellingDiagnostic CreateSpellingDiagnostic(Diagnostic diagnostic);
}
