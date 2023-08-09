﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CodeFixes;
using Roslynator.CSharp.Analysis;
using Roslynator.CSharp.Refactorings;

namespace Roslynator.CSharp.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveRedundantBooleanLiteralCodeFixProvider))]
[Shared]
public sealed class RemoveRedundantBooleanLiteralCodeFixProvider : BaseCodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds
    {
        get { return ImmutableArray.Create(DiagnosticIdentifiers.RemoveRedundantBooleanLiteral); }
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

        if (!TryFindFirstAncestorOrSelf(
            root,
            context.Span,
            out SyntaxNode node,
            predicate: f => f.IsKind(
                SyntaxKind.TrueLiteralExpression,
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression,
                SyntaxKind.LogicalAndExpression,
                SyntaxKind.LogicalOrExpression)))
        {
            return;
        }

        switch (node.Kind())
        {
            case SyntaxKind.TrueLiteralExpression:
                {
                    RegisterCodeFix(
                        context,
                        node.ToString(),
                        ct =>
                        {
                            return RemoveRedundantBooleanLiteralRefactoring.RefactorAsync(
                                context.Document,
                                (ForStatementSyntax)node.Parent,
                                ct);
                        });

                    break;
                }
            case SyntaxKind.EqualsExpression:
            case SyntaxKind.NotEqualsExpression:
            case SyntaxKind.LogicalAndExpression:
            case SyntaxKind.LogicalOrExpression:
                {
                    var binaryExpression = (BinaryExpressionSyntax)node;

                    TextSpan span = RemoveRedundantBooleanLiteralAnalysis.GetSpanToRemove(binaryExpression, binaryExpression.Left, binaryExpression.Right);

                    RegisterCodeFix(
                        context,
                        binaryExpression.ToString(span),
                        ct =>
                        {
                            return RemoveRedundantBooleanLiteralRefactoring.RefactorAsync(
                                context.Document,
                                binaryExpression,
                                ct);
                        });

                    break;
                }
        }
    }

    private static void RegisterCodeFix(CodeFixContext context, string textToRemove, Func<CancellationToken, Task<Document>> createChangedDocument)
    {
        CodeAction codeAction = CodeAction.Create(
            $"Remove redundant '{textToRemove}'",
            createChangedDocument,
            GetEquivalenceKey(DiagnosticIdentifiers.RemoveRedundantBooleanLiteral));

        context.RegisterCodeFix(codeAction, context.Diagnostics);
    }
}
