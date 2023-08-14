﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslynator.CodeFixes;
using Roslynator.CSharp.Analysis;
using Roslynator.CSharp.Syntax;
using static Roslynator.CSharp.CSharpFactory;

namespace Roslynator.CSharp.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SimplifyLogicalNegationCodeFixProvider))]
[Shared]
public sealed class SimplifyLogicalNegationCodeFixProvider : BaseCodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds
    {
        get { return ImmutableArray.Create(DiagnosticIdentifiers.SimplifyLogicalNegation); }
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

        if (!TryFindFirstAncestorOrSelf(root, context.Span, out PrefixUnaryExpressionSyntax prefixUnaryExpression))
            return;

        Diagnostic diagnostic = context.Diagnostics[0];

        Document document = context.Document;

        CodeAction codeAction = CodeAction.Create(
            "Simplify logical negation",
            ct => SimplifyLogicalNegationAsync(document, prefixUnaryExpression, ct),
            GetEquivalenceKey(diagnostic));

        context.RegisterCodeFix(codeAction, diagnostic);
    }

    private static Task<Document> SimplifyLogicalNegationAsync(
        Document document,
        PrefixUnaryExpressionSyntax logicalNot,
        CancellationToken cancellationToken = default)
    {
        ExpressionSyntax newNode = GetNewNode(logicalNot, document)
            .WithTriviaFrom(logicalNot)
            .WithSimplifierAnnotation();

        return document.ReplaceNodeAsync(logicalNot, newNode, cancellationToken);
    }

    private static ExpressionSyntax GetNewNode(PrefixUnaryExpressionSyntax logicalNot, Document document)
    {
        ExpressionSyntax operand = logicalNot.Operand;
        ExpressionSyntax expression = operand.WalkDownParentheses();

        switch (expression.Kind())
        {
            case SyntaxKind.TrueLiteralExpression:
            case SyntaxKind.FalseLiteralExpression:
                {
                    LiteralExpressionSyntax newNode = BooleanLiteralExpression(expression.Kind() == SyntaxKind.FalseLiteralExpression);

                    newNode = newNode.WithTriviaFrom(expression);

                    return operand.ReplaceNode(expression, newNode);
                }
            case SyntaxKind.LogicalNotExpression:
                {
                    return ((PrefixUnaryExpressionSyntax)expression).Operand;
                }
            case SyntaxKind.EqualsExpression:
            case SyntaxKind.NotEqualsExpression:
            case SyntaxKind.LessThanExpression:
            case SyntaxKind.LessThanOrEqualExpression:
            case SyntaxKind.GreaterThanExpression:
            case SyntaxKind.GreaterThanOrEqualExpression:
                {
                    BinaryExpressionSyntax newExpression = SyntaxLogicalInverter.GetInstance(document).InvertBinaryExpression((BinaryExpressionSyntax)expression);

                    return operand.ReplaceNode(expression, newExpression);
                }
            case SyntaxKind.InvocationExpression:
                {
                    var invocationExpression = (InvocationExpressionSyntax)expression;

                    var memberAccessExpression = (MemberAccessExpressionSyntax)invocationExpression.Expression;

                    ExpressionSyntax lambdaExpression = invocationExpression.ArgumentList.Arguments[0].Expression.WalkDownParentheses();

                    SingleParameterLambdaExpressionInfo lambdaInfo = SyntaxInfo.SingleParameterLambdaExpressionInfo(lambdaExpression);

                    ExpressionSyntax logicalNot2 = SimplifyLogicalNegationAnalyzer.GetReturnExpression(lambdaInfo.Body).WalkDownParentheses();

                    ExpressionSyntax invertedExperssion = SyntaxLogicalInverter.GetInstance(document).LogicallyInvert(logicalNot2);

                    InvocationExpressionSyntax newNode = invocationExpression.ReplaceNode(logicalNot2, invertedExperssion.WithTriviaFrom(logicalNot2));

                    return SyntaxRefactorings.ChangeInvokedMethodName(newNode, (memberAccessExpression.Name.Identifier.ValueText == "All") ? "Any" : "All");
                }
            case SyntaxKind.IsPatternExpression:
                {
                    var isPatternExpression = (IsPatternExpressionSyntax)expression;

                    var pattern = (ConstantPatternSyntax)isPatternExpression.Pattern;

                    UnaryPatternSyntax newPattern = NotPattern(pattern.WithoutTrivia()).WithTriviaFrom(pattern);

                    return isPatternExpression.WithPattern(newPattern)
                        .PrependToLeadingTrivia(logicalNot.GetLeadingTrivia())
                        .AppendToTrailingTrivia(logicalNot.GetTrailingTrivia());
                }
        }

        return null;
    }
}
