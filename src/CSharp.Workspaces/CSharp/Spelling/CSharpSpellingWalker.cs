﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.Spelling;

namespace Roslynator.CSharp.Spelling;

internal sealed class CSharpSpellingWalker : CSharpSyntaxWalker
{
    private readonly SpellingAnalysisContext _analysisContext;
    private readonly Stack<SyntaxNode> _stack = new();

    private SpellcheckOptions Options => _analysisContext.Options;

    private CSharpSpellingWalker(SpellingAnalysisContext analysisContext, SyntaxWalkerDepth depth)
        : base(depth)
    {
        _analysisContext = analysisContext;
    }

    public static CSharpSpellingWalker Create(SpellingAnalysisContext context)
    {
        return new CSharpSpellingWalker(context, GetWalkerDepth(context));

        static SyntaxWalkerDepth GetWalkerDepth(SpellingAnalysisContext context)
        {
            if ((context.Options.ScopeFilter & (SpellingScopeFilter.DocumentationComment | SpellingScopeFilter.Region)) != 0)
                return SyntaxWalkerDepth.StructuredTrivia;

            if ((context.Options.ScopeFilter & SpellingScopeFilter.NonDocumentationComment) != 0)
                return SyntaxWalkerDepth.Trivia;

            return SyntaxWalkerDepth.Token;
        }
    }

    private void AnalyzeText(string value, SyntaxTree syntaxTree, TextSpan textSpan)
    {
        _analysisContext.CancellationToken.ThrowIfCancellationRequested();

        _analysisContext.AnalyzeText(value, textSpan, syntaxTree);
    }

    private void AnalyzeIdentifier(SyntaxToken identifier, int prefixLength = 0)
    {
        _analysisContext.CancellationToken.ThrowIfCancellationRequested();

        _analysisContext.AnalyzeIdentifier(identifier, prefixLength);
    }

    public override void VisitTrivia(SyntaxTrivia trivia)
    {
        switch (trivia.Kind())
        {
            case SyntaxKind.SingleLineCommentTrivia:
            case SyntaxKind.MultiLineCommentTrivia:
                {
                    if (ShouldVisit(SpellingScopeFilter.NonDocumentationComment))
                        AnalyzeText(trivia.ToString(), trivia.SyntaxTree, trivia.Span);

                    break;
                }
            case SyntaxKind.SingleLineDocumentationCommentTrivia:
            case SyntaxKind.MultiLineDocumentationCommentTrivia:
                {
                    if (ShouldVisit(SpellingScopeFilter.DocumentationComment))
                        base.VisitTrivia(trivia);

                    break;
                }
            case SyntaxKind.RegionDirectiveTrivia:
            case SyntaxKind.EndRegionDirectiveTrivia:
                {
                    if (ShouldVisit(SpellingScopeFilter.Region))
                        base.VisitTrivia(trivia);

                    break;
                }
            case SyntaxKind.PreprocessingMessageTrivia:
                {
                    Debug.Assert(ShouldVisit(SpellingScopeFilter.Region));

                    AnalyzeText(trivia.ToString(), trivia.SyntaxTree, trivia.Span);
                    break;
                }
        }
    }

    public override void VisitTupleType(TupleTypeSyntax node)
    {
        if (node.Elements.All(f => f.Identifier.IsKind(SyntaxKind.None)))
            return;

        if (!ShouldVisit(SpellingScopeFilter.LocalVariable
            | SpellingScopeFilter.Field
            | SpellingScopeFilter.Constant
            | SpellingScopeFilter.ReturnType
            | SpellingScopeFilter.Class
            | SpellingScopeFilter.Struct
            | SpellingScopeFilter.Interface
            | SpellingScopeFilter.Record))
        {
            return;
        }

        if (_stack.Count == 0)
        {
            SyntaxDebug.Fail(node);
            return;
        }

        SyntaxNode containingNode = _stack.Peek();

        switch (containingNode.Kind())
        {
            case SyntaxKind.LocalDeclarationStatement:
            case SyntaxKind.UsingStatement:
                {
                    if (ShouldVisit(SpellingScopeFilter.LocalVariable))
                        base.VisitTupleType(node);

                    break;
                }
            case SyntaxKind.FieldDeclaration:
                {
                    if (ShouldVisitFieldDeclaration(containingNode))
                        base.VisitTupleType(node);

                    break;
                }
            case SyntaxKind.ConversionOperatorDeclaration:
            case SyntaxKind.DelegateDeclaration:
            case SyntaxKind.IndexerDeclaration:
            case SyntaxKind.LocalFunctionStatement:
            case SyntaxKind.MethodDeclaration:
            case SyntaxKind.PropertyDeclaration:
                {
                    if (ShouldVisit(SpellingScopeFilter.ReturnType))
                        base.VisitTupleType(node);

                    break;
                }
            case SyntaxKind.EventDeclaration:
            case SyntaxKind.EventFieldDeclaration:
                {
                    if (ShouldVisit(SpellingScopeFilter.Event))
                        base.VisitTupleType(node);

                    break;
                }
            case SyntaxKind.ClassDeclaration:
                {
                    if (ShouldVisit(SpellingScopeFilter.Class))
                        base.VisitTupleType(node);

                    break;
                }
            case SyntaxKind.StructDeclaration:
                {
                    if (ShouldVisit(SpellingScopeFilter.Struct))
                        base.VisitTupleType(node);

                    break;
                }
            case SyntaxKind.InterfaceDeclaration:
                {
                    if (ShouldVisit(SpellingScopeFilter.Interface))
                        base.VisitTupleType(node);

                    break;
                }
            case SyntaxKind.RecordDeclaration:
            case SyntaxKind.RecordStructDeclaration:
                {
                    if (ShouldVisit(SpellingScopeFilter.Record))
                        base.VisitTupleType(node);

                    break;
                }
            case SyntaxKind.Parameter:
                {
                    if (ShouldVisit(SpellingScopeFilter.Parameter))
                        base.VisitTupleType(node);

                    break;
                }
            default:
                {
                    SyntaxDebug.Fail(containingNode);
                    break;
                }
        }
    }

    public override void VisitTupleElement(TupleElementSyntax node)
    {
        if (node.Identifier.Parent is not null)
            AnalyzeIdentifier(node.Identifier);

        base.VisitTupleElement(node);
    }

    public override void VisitArgument(ArgumentSyntax node)
    {
        if (node.Expression is DeclarationExpressionSyntax declarationExpression)
            VisitDeclarationExpression(declarationExpression);
    }

    public override void VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
    {
        if (node.NameEquals is not null
            && ShouldVisit(SpellingScopeFilter.LocalVariable))
        {
            AnalyzeIdentifier(node.NameEquals.Name.Identifier);
        }

        base.VisitAnonymousObjectMemberDeclarator(node);
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.LocalFunction))
            AnalyzeIdentifier(node.Identifier);

        _stack.Push(node);
        base.VisitLocalFunctionStatement(node);
        _stack.Pop();
    }

    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        SyntaxDebug.Assert(node.IsParentKind(SyntaxKind.VariableDeclaration), node.Parent);

        SyntaxNode containingNode = _stack.Peek();

        switch (containingNode.Kind())
        {
            case SyntaxKind.LocalDeclarationStatement:
            case SyntaxKind.UsingStatement:
            case SyntaxKind.ForStatement:
            case SyntaxKind.FixedStatement:
                {
                    if (ShouldVisit(SpellingScopeFilter.LocalVariable))
                        AnalyzeIdentifier(node.Identifier);

                    break;
                }
            case SyntaxKind.FieldDeclaration:
                {
                    if (ShouldVisitFieldDeclaration(containingNode))
                        AnalyzeIdentifier(node.Identifier);

                    break;
                }
            case SyntaxKind.EventFieldDeclaration:
                {
                    if (ShouldVisit(SpellingScopeFilter.Event))
                        AnalyzeIdentifier(node.Identifier);

                    break;
                }
            default:
                {
                    SyntaxDebug.Fail(containingNode);
                    break;
                }
        }

        base.VisitVariableDeclarator(node);
    }

    public override void VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.LocalVariable))
            AnalyzeIdentifier(node.Identifier);

        base.VisitSingleVariableDesignation(node);
    }

    public override void VisitCatchDeclaration(CatchDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.LocalVariable))
            AnalyzeIdentifier(node.Identifier);

        base.VisitCatchDeclaration(node);
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        if (node.Alias is not null
            && ShouldVisit(SpellingScopeFilter.UsingAlias))
        {
            AnalyzeIdentifier(node.Alias.Name.Identifier);
        }

        base.VisitUsingDirective(node);
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Namespace))
            VisitName(node.Name);

        base.VisitNamespaceDeclaration(node);
    }

    private void VisitName(NameSyntax node)
    {
        switch (node)
        {
            case IdentifierNameSyntax identifierName:
                {
                    AnalyzeIdentifier(identifierName.Identifier);
                    break;
                }
            case QualifiedNameSyntax qualifiedName:
                {
                    VisitName(qualifiedName.Left);

                    if (qualifiedName.Right is IdentifierNameSyntax identifierName)
                        AnalyzeIdentifier(identifierName.Identifier);

                    break;
                }
        }
    }

    public override void VisitTypeParameter(TypeParameterSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.TypeParameter))
        {
            SyntaxToken identifier = node.Identifier;
            string value = identifier.ValueText;

            int prefixLength = 0;
            if (value.Length > 1
                && value[0] == 'T'
                && char.IsUpper(value[1]))
            {
                prefixLength = 1;
            }

            AnalyzeIdentifier(identifier, prefixLength: prefixLength);
        }

        base.VisitTypeParameter(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Class))
            AnalyzeIdentifier(node.Identifier);

        _stack.Push(node);
        base.VisitClassDeclaration(node);
        _stack.Pop();
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Struct))
            AnalyzeIdentifier(node.Identifier);

        _stack.Push(node);
        base.VisitStructDeclaration(node);
        _stack.Pop();
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Interface))
        {
            SyntaxToken identifier = node.Identifier;
            string value = identifier.ValueText;

            int prefixLength = 0;
            if (value.Length > 1
                && value[0] == 'I'
                && char.IsUpper(value[1]))
            {
                prefixLength = 1;
            }

            AnalyzeIdentifier(identifier, prefixLength: prefixLength);
        }

        _stack.Push(node);
        base.VisitInterfaceDeclaration(node);
        _stack.Pop();
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Record))
            AnalyzeIdentifier(node.Identifier);

        _stack.Push(node);
        base.VisitRecordDeclaration(node);
        _stack.Pop();
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Enum))
            AnalyzeIdentifier(node.Identifier);

        base.VisitEnumDeclaration(node);
    }

    public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Delegate))
            AnalyzeIdentifier(node.Identifier);

        _stack.Push(node);
        base.VisitDelegateDeclaration(node);
        _stack.Pop();
    }

    public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Field))
            AnalyzeIdentifier(node.Identifier);

        base.VisitEnumMemberDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Method))
            AnalyzeIdentifier(node.Identifier);

        _stack.Push(node);
        base.VisitMethodDeclaration(node);
        _stack.Pop();
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Property))
            AnalyzeIdentifier(node.Identifier);

        _stack.Push(node);
        base.VisitPropertyDeclaration(node);
        _stack.Pop();
    }

    public override void VisitEventDeclaration(EventDeclarationSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Event))
            AnalyzeIdentifier(node.Identifier);

        base.VisitEventDeclaration(node);
    }

    public override void VisitParameter(ParameterSyntax node)
    {
        if (node.Identifier.IsKind(SyntaxKind.ArgListKeyword))
            return;

        if (ShouldVisit(SpellingScopeFilter.Parameter))
            AnalyzeIdentifier(node.Identifier);

        _stack.Push(node);
        base.VisitParameter(node);
        _stack.Pop();
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.LocalVariable))
            AnalyzeIdentifier(node.Identifier);

        base.VisitForEachStatement(node);
    }

    public override void VisitXmlElement(XmlElementSyntax node)
    {
        switch (node.StartTag.Name.LocalName.ValueText)
        {
            case "c":
            case "code":
                return;
        }

        base.VisitXmlElement(node);
    }

    public override void VisitXmlText(XmlTextSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.DocumentationComment))
        {
            foreach (SyntaxToken token in node.TextTokens)
            {
                if (token.IsKind(SyntaxKind.XmlTextLiteralToken))
                    AnalyzeText(token.ValueText, node.SyntaxTree, token.Span);
            }
        }

        base.VisitXmlText(node);
    }

    public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        _stack.Push(node);
        base.VisitLocalDeclarationStatement(node);
        _stack.Pop();
    }

    public override void VisitUsingStatement(UsingStatementSyntax node)
    {
        _stack.Push(node);
        base.VisitUsingStatement(node);
        _stack.Pop();
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        _stack.Push(node);
        base.VisitForStatement(node);
        _stack.Pop();
    }

    public override void VisitFixedStatement(FixedStatementSyntax node)
    {
        _stack.Push(node);
        base.VisitFixedStatement(node);
        _stack.Pop();
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        _stack.Push(node);
        base.VisitFieldDeclaration(node);
        _stack.Pop();
    }

    public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
    {
        _stack.Push(node);
        base.VisitEventFieldDeclaration(node);
        _stack.Pop();
    }

    public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
    {
        _stack.Push(node);
        base.VisitConversionOperatorDeclaration(node);
        _stack.Pop();
    }

    public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        _stack.Push(node);
        base.VisitIndexerDeclaration(node);
        _stack.Pop();
    }

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.StringLiteralExpression)
            && ShouldVisit(SpellingScopeFilter.Literal))
        {
            SyntaxToken token = node.Token;

            AnalyzeText(token.Text, node.SyntaxTree, token.Span);
        }

        base.VisitLiteralExpression(node);
    }

    public override void VisitInterpolatedStringText(InterpolatedStringTextSyntax node)
    {
        if (ShouldVisit(SpellingScopeFilter.Literal))
        {
            SyntaxToken token = node.TextToken;

            AnalyzeText(token.Text, node.SyntaxTree, token.Span);
        }

        base.VisitInterpolatedStringText(node);
    }

    private bool ShouldVisit(SpellingScopeFilter filter)
    {
        return (Options.ScopeFilter & filter) != 0;
    }

    private bool ShouldVisitFieldDeclaration(SyntaxNode fieldDeclaration)
    {
        return ShouldVisit((((FieldDeclarationSyntax)fieldDeclaration).Modifiers.Contains(SyntaxKind.ConstKeyword))
            ? SpellingScopeFilter.Constant
            : SpellingScopeFilter.Field);
    }
}
