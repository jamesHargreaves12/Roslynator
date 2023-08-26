﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp.Analysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Roslynator.CSharp.CSharpFactory;

namespace Roslynator.CSharp.Refactorings;

internal static class ConvertExpressionBodyToBlockBodyRefactoring
{
    public const string Title = "Use block body";

    public static async Task<Document> RefactorAsync(
        Document document,
        MemberDeclarationListSelection selectedMembers,
        CancellationToken cancellationToken)
    {
        AnalyzerConfigOptions configOptions = document.GetConfigOptions(selectedMembers.Parent.SyntaxTree);

        SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<MemberDeclarationSyntax> newMembers = selectedMembers
            .UnderlyingList
            .ModifyRange(
                selectedMembers.FirstIndex,
                selectedMembers.Count,
                member =>
                {
                    ArrowExpressionClauseSyntax expressionBody = CSharpUtility.GetExpressionBody(member);

                    if (expressionBody is not null
                        && ExpandExpressionBodyAnalysis.IsFixable(expressionBody))
                    {
                        return (MemberDeclarationSyntax)Refactor(expressionBody, configOptions, semanticModel, cancellationToken);
                    }

                    return member;
                });

        return await document.ReplaceMembersAsync(SyntaxInfo.MemberDeclarationListInfo(selectedMembers.Parent), newMembers, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<Document> RefactorAsync(
        Document document,
        ArrowExpressionClauseSyntax expressionBody,
        CancellationToken cancellationToken = default)
    {
        AnalyzerConfigOptions configOptions = document.GetConfigOptions(expressionBody.SyntaxTree);

        SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        SyntaxNode newNode = Refactor(expressionBody, configOptions, semanticModel, cancellationToken);

        SyntaxToken token = expressionBody.ArrowToken.GetPreviousToken();

        if (SyntaxTriviaAnalysis.IsOptionalWhitespaceThenEndOfLineTrivia(token.TrailingTrivia))
        {
            SyntaxToken newToken = token.WithTrailingTrivia(ElasticSpace);

            newNode = newNode.ReplaceToken(token, newToken);
        }

        return await document.ReplaceNodeAsync(expressionBody.Parent, newNode, cancellationToken).ConfigureAwait(false);
    }

    public static SyntaxNode Refactor(
        ArrowExpressionClauseSyntax expressionBody,
        AnalyzerConfigOptions configOptions,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        SyntaxNode node = expressionBody.Parent;

        ExpressionSyntax expression = expressionBody.Expression;

        switch (node.Kind())
        {
            case SyntaxKind.MethodDeclaration:
                {
                    var method = (MethodDeclarationSyntax)node;

                    return method
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default)
                        .WithBody(CreateBlock(method, expression, method.SemicolonToken, method.ReturnType, configOptions, semanticModel, cancellationToken));
                }
            case SyntaxKind.ConstructorDeclaration:
                {
                    var constructor = (ConstructorDeclarationSyntax)node;

                    return constructor
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default)
                        .WithBody(CreateBlockWithExpressionStatement(constructor, expression, constructor.SemicolonToken, configOptions));
                }
            case SyntaxKind.DestructorDeclaration:
                {
                    var destructor = (DestructorDeclarationSyntax)node;

                    return destructor
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default)
                        .WithBody(CreateBlockWithExpressionStatement(destructor, expression, destructor.SemicolonToken, configOptions));
                }
            case SyntaxKind.OperatorDeclaration:
                {
                    var operatorDeclaration = (OperatorDeclarationSyntax)node;

                    return operatorDeclaration
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default)
                        .WithBody(CreateBlock(operatorDeclaration, expression, operatorDeclaration.SemicolonToken, configOptions));
                }
            case SyntaxKind.ConversionOperatorDeclaration:
                {
                    var conversionOperatorDeclaration = (ConversionOperatorDeclarationSyntax)node;

                    return conversionOperatorDeclaration
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default)
                        .WithBody(CreateBlock(conversionOperatorDeclaration, expression, conversionOperatorDeclaration.SemicolonToken, configOptions));
                }
            case SyntaxKind.PropertyDeclaration:
                {
                    var propertyDeclaration = (PropertyDeclarationSyntax)node;

                    return propertyDeclaration
                        .WithAccessorList(CreateAccessorList(propertyDeclaration, expression, propertyDeclaration.SemicolonToken, configOptions))
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default);
                }
            case SyntaxKind.IndexerDeclaration:
                {
                    var indexerDeclaration = (IndexerDeclarationSyntax)node;

                    return indexerDeclaration
                        .WithAccessorList(CreateAccessorList(indexerDeclaration, expression, indexerDeclaration.SemicolonToken, configOptions))
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default);
                }
            case SyntaxKind.GetAccessorDeclaration:
                {
                    var accessor = (AccessorDeclarationSyntax)node;

                    return accessor
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default)
                        .WithBody(CreateBlock(accessor, expression, accessor.SemicolonToken, configOptions));
                }
            case SyntaxKind.SetAccessorDeclaration:
            case SyntaxKind.InitAccessorDeclaration:
            case SyntaxKind.AddAccessorDeclaration:
            case SyntaxKind.RemoveAccessorDeclaration:
                {
                    var accessor = (AccessorDeclarationSyntax)node;

                    return accessor
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default)
                        .WithBody(CreateBlockWithExpressionStatement(accessor, expression, accessor.SemicolonToken, configOptions));
                }
            case SyntaxKind.LocalFunctionStatement:
                {
                    var localFunction = (LocalFunctionStatementSyntax)node;

                    return localFunction
                        .WithExpressionBody(null)
                        .WithSemicolonToken(default)
                        .WithBody(CreateBlock(localFunction, expression, localFunction.SemicolonToken, localFunction.ReturnType, configOptions, semanticModel, cancellationToken));
                }
            default:
                {
                    SyntaxDebug.Fail(node);
                    return node;
                }
        }
    }

    private static BlockSyntax CreateBlock(
        SyntaxNode declaration,
        ExpressionSyntax expression,
        SyntaxToken semicolon,
        AnalyzerConfigOptions configOptions,
        int increaseCount = 1)
    {
        return (expression.IsKind(SyntaxKind.ThrowExpression))
            ? CreateBlockWithExpressionStatement(declaration, expression, semicolon, configOptions, increaseCount)
            : CreateBlockWithReturnStatement(declaration, expression, semicolon, configOptions, increaseCount);
    }

    private static BlockSyntax CreateBlock(
        SyntaxNode declaration,
        ExpressionSyntax expression,
        SyntaxToken semicolon,
        TypeSyntax returnType,
        AnalyzerConfigOptions configOptions,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return (ShouldCreateExpressionStatement(returnType, expression, semanticModel, cancellationToken))
            ? CreateBlockWithExpressionStatement(declaration, expression, semicolon, configOptions)
            : CreateBlockWithReturnStatement(declaration, expression, semicolon, configOptions);

        static bool ShouldCreateExpressionStatement(
            TypeSyntax returnType,
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (returnType is null)
                return true;

            if (returnType.IsVoid())
                return true;

            switch (expression.Kind())
            {
                case SyntaxKind.ThrowExpression:
                    {
                        return true;
                    }
                case SyntaxKind.AwaitExpression:
                    {
                        ITypeSymbol originalDefinition = semanticModel
                            .GetTypeSymbol(returnType, cancellationToken)
                            .OriginalDefinition;

                        if (!originalDefinition.HasMetadataName(MetadataNames.System_Threading_Tasks_ValueTask_T)
                            && !originalDefinition.EqualsOrInheritsFrom(MetadataNames.System_Threading_Tasks_Task_T))
                        {
                            return true;
                        }

                        break;
                    }
            }

            return false;
        }
    }

    private static AccessorListSyntax CreateAccessorList(
        SyntaxNode declaration,
        ExpressionSyntax expression,
        SyntaxToken semicolon,
        AnalyzerConfigOptions configOptions)
    {
        BlockSyntax block = CreateBlock(declaration, expression, semicolon, configOptions, increaseCount: 2);

        AccessorListSyntax accessorList = AccessorList(GetAccessorDeclaration(block));

        if (expression.IsSingleLine())
        {
            accessorList = accessorList
                .RemoveWhitespace()
                .WithCloseBraceToken(accessorList.CloseBraceToken.WithLeadingTrivia(NewLine()))
                .WithFormatterAnnotation();
        }

        return accessorList;
    }

    private static BlockSyntax CreateBlockWithExpressionStatement(
        SyntaxNode declaration,
        ExpressionSyntax expression,
        SyntaxToken semicolon,
        AnalyzerConfigOptions configOptions,
        int increaseCount = 1)
    {
        return CreateBlock(
            declaration,
            expression,
            semicolon,
            configOptions,
            (e, s) =>
            {
                if (e is ThrowExpressionSyntax throwExpression)
                {
                    return ThrowStatement(Token(SyntaxKind.ThrowKeyword), throwExpression.Expression, s);
                }
                else
                {
                    return ExpressionStatement(e, s);
                }
            },
            increaseCount: increaseCount);
    }

    private static BlockSyntax CreateBlockWithReturnStatement(
        SyntaxNode declaration,
        ExpressionSyntax expression,
        SyntaxToken semicolon,
        AnalyzerConfigOptions configOptions,
        int increaseCount = 1)
    {
        return CreateBlock(
            declaration,
            expression,
            semicolon,
            configOptions,
            (e, s) => ReturnStatement(Token(SyntaxKind.ReturnKeyword), e, s),
            increaseCount: increaseCount);
    }

    private static BlockSyntax CreateBlock(
        SyntaxNode declaration,
        ExpressionSyntax expression,
        SyntaxToken semicolon,
        AnalyzerConfigOptions configOptions,
        Func<ExpressionSyntax, SyntaxToken, StatementSyntax> createStatement,
        int increaseCount = 1)
    {
        expression = SyntaxTriviaAnalysis.SetIndentation(expression, declaration, configOptions, increaseCount: increaseCount);

        return Block(
            Token(SyntaxKind.OpenBraceToken).WithFormatterAnnotation(),
            createStatement(expression, semicolon),
            Token(SyntaxKind.CloseBraceToken).WithFormatterAnnotation());
    }
}
