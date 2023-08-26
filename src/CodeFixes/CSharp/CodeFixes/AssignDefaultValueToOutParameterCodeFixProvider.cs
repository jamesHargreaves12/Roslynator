﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CodeFixes;
using Roslynator.CSharp.Refactorings;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Roslynator.CSharp.CSharpFactory;

namespace Roslynator.CSharp.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AssignDefaultValueToOutParameterCodeFixProvider))]
[Shared]
public sealed class AssignDefaultValueToOutParameterCodeFixProvider : CompilerDiagnosticCodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds
    {
        get { return ImmutableArray.Create(CompilerDiagnosticIdentifiers.CS0177_OutParameterMustBeAssignedToBeforeControlLeavesCurrentMethod); }
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];

        SyntaxNode root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

        if (!IsEnabled(diagnostic.Id, CodeFixIdentifiers.AssignDefaultValueToOutParameter, context.Document, root.SyntaxTree))
            return;

        if (!TryFindFirstAncestorOrSelf(
            root,
            context.Span,
            out SyntaxNode node,
            predicate: f => f.IsKind(SyntaxKind.MethodDeclaration) || f is StatementSyntax))
        {
            return;
        }

        StatementSyntax statement = null;

        if (!node.IsKind(SyntaxKind.MethodDeclaration, SyntaxKind.LocalFunctionStatement))
        {
            statement = (StatementSyntax)node;

            node = node.FirstAncestor(f => f.IsKind(SyntaxKind.MethodDeclaration, SyntaxKind.LocalFunctionStatement));

            Debug.Assert(node is not null, "Containing method or local function not found.");

            if (node is null)
                return;
        }

        SyntaxNode bodyOrExpressionBody = GetBodyOrExpressionBody(node);

        if (bodyOrExpressionBody is null)
            return;

        if (bodyOrExpressionBody is BlockSyntax body
            && body.ContainsYield())
        {
            return;
        }

        SemanticModel semanticModel = await context.Document.GetSemanticModelAsync().ConfigureAwait(false);

        DataFlowAnalysis dataFlowAnalysis = AnalyzeDataFlow(bodyOrExpressionBody, semanticModel);

        // Flow analysis APIs do not work with local functions: https://github.com/dotnet/roslyn/issues/14214
        if (!dataFlowAnalysis.Succeeded)
            return;

        var methodSymbol = (IMethodSymbol)semanticModel.GetDeclaredSymbol(node);

        ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;

        ImmutableArray<ISymbol> alwaysAssigned = dataFlowAnalysis.AlwaysAssigned;

        IParameterSymbol singleParameter = null;
        var isAny = false;
        foreach (IParameterSymbol parameter in parameters)
        {
            if (parameter.RefKind == RefKind.Out
                && !alwaysAssigned.Contains(parameter))
            {
                if (singleParameter is null)
                {
                    singleParameter = parameter;
                    isAny = true;
                }
                else
                {
                    singleParameter = null;
                    break;
                }
            }
        }

        Debug.Assert(isAny, "No unassigned 'out' parameter found.");

        if (!isAny)
            return;

        CodeAction codeAction = CodeAction.Create(
            (singleParameter is not null)
                ? $"Assign default value to parameter '{singleParameter.Name}'"
                : "Assign default value to parameters",
            ct => RefactorAsync(context.Document, node, statement, bodyOrExpressionBody, parameters, alwaysAssigned, semanticModel, ct),
            GetEquivalenceKey(diagnostic));

        context.RegisterCodeFix(codeAction, diagnostic);
    }

    private static DataFlowAnalysis AnalyzeDataFlow(
        SyntaxNode bodyOrExpressionBody,
        SemanticModel semanticModel)
    {
        if (bodyOrExpressionBody is BlockSyntax body)
            return semanticModel.AnalyzeDataFlow(body);

        return semanticModel.AnalyzeDataFlow(((ArrowExpressionClauseSyntax)bodyOrExpressionBody).Expression);
    }

    private static Task<Document> RefactorAsync(
        Document document,
        SyntaxNode node,
        StatementSyntax statement,
        SyntaxNode bodyOrExpressionBody,
        ImmutableArray<IParameterSymbol> parameterSymbols,
        ImmutableArray<ISymbol> alwaysAssigned,
        SemanticModel semanticModel,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<ExpressionStatementSyntax> expressionStatements = parameterSymbols
            .Where(f => f.RefKind == RefKind.Out && !alwaysAssigned.Contains(f))
            .Select(f =>
            {
                ExpressionStatementSyntax expressionStatement = SimpleAssignmentStatement(
                    IdentifierName(f.Name),
                    f.Type.GetDefaultValueSyntax(document.GetDefaultSyntaxOptions()));

                return expressionStatement.WithFormatterAnnotation();
            });

        SyntaxNode newNode = null;

        if (bodyOrExpressionBody is ArrowExpressionClauseSyntax expressionBody)
        {
            AnalyzerConfigOptions configOptions = document.GetConfigOptions(node.SyntaxTree);

            newNode = ConvertExpressionBodyToBlockBodyRefactoring.Refactor(expressionBody, configOptions, semanticModel, cancellationToken);

            newNode = InsertStatements(newNode, expressionStatements);
        }
        else if (statement is not null)
        {
            if (statement.IsEmbedded())
            {
                newNode = node.ReplaceNode(statement, Block(expressionStatements.Concat(new StatementSyntax[] { statement })));
            }
            else
            {
                newNode = node.InsertNodesBefore(statement, expressionStatements);
            }
        }
        else
        {
            newNode = InsertStatements(node, expressionStatements);
        }

        return document.ReplaceNodeAsync(node, newNode, cancellationToken);
    }

    private static SyntaxNode InsertStatements(
        SyntaxNode node,
        IEnumerable<StatementSyntax> newStatements)
    {
        var body = (BlockSyntax)GetBodyOrExpressionBody(node);

        SyntaxList<StatementSyntax> statements = body.Statements;

        StatementSyntax lastStatement = statements.LastOrDefault(f => !f.IsKind(SyntaxKind.LocalFunctionStatement, SyntaxKind.ReturnStatement));

        int index = (lastStatement is not null)
            ? statements.IndexOf(lastStatement) + 1
            : 0;

        BlockSyntax newBody = body.WithStatements(statements.InsertRange(index, newStatements));

        if (node is MethodDeclarationSyntax methodDeclaration)
            return methodDeclaration.WithBody(newBody);

        return ((LocalFunctionStatementSyntax)node).WithBody(newBody);
    }

    private static SyntaxNode GetBodyOrExpressionBody(SyntaxNode node)
    {
        if (node is MethodDeclarationSyntax methodDeclaration)
            return methodDeclaration.BodyOrExpressionBody();

        return ((LocalFunctionStatementSyntax)node).BodyOrExpressionBody();
    }
}
