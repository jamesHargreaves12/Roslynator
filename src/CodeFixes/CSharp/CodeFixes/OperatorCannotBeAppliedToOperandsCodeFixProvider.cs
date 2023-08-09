﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslynator.CodeFixes;
using Roslynator.CSharp.Refactorings;

namespace Roslynator.CSharp.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OperatorCannotBeAppliedToOperandsCodeFixProvider))]
[Shared]
public sealed class OperatorCannotBeAppliedToOperandsCodeFixProvider : CompilerDiagnosticCodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds
    {
        get { return ImmutableArray.Create(CompilerDiagnosticIdentifiers.CS0019_OperatorCannotBeAppliedToOperands); }
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];

        SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

        if (!IsEnabled(diagnostic.Id, CodeFixIdentifiers.AddComparisonWithBooleanLiteral, context.Document, root.SyntaxTree))
            return;

        if (!TryFindNode(root, context.Span, out BinaryExpressionSyntax binaryExpression))
            return;

        SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

        bool success = RegisterCodeFix(context, binaryExpression.Left, diagnostic, semanticModel);

        if (!success)
            RegisterCodeFix(context, binaryExpression.Right, diagnostic, semanticModel);
    }

    private static bool RegisterCodeFix(
        CodeFixContext context,
        ExpressionSyntax expression,
        Diagnostic diagnostic,
        SemanticModel semanticModel)
    {
        if (expression?.IsMissing == false
            && semanticModel.GetTypeSymbol(expression, context.CancellationToken)?.IsNullableOf(SpecialType.System_Boolean) == true)
        {
            CodeAction codeAction = CodeAction.Create(
                AddComparisonWithBooleanLiteralRefactoring.GetTitle(expression),
                ct => AddComparisonWithBooleanLiteralRefactoring.RefactorAsync(context.Document, expression, ct),
                GetEquivalenceKey(diagnostic));

            context.RegisterCodeFix(codeAction, diagnostic);
            return true;
        }

        return false;
    }
}
