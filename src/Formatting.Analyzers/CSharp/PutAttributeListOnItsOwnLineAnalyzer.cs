﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;

namespace Roslynator.Formatting.CSharp;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PutAttributeListOnItsOwnLineAnalyzer : BaseDiagnosticAnalyzer
{
    private static ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            if (_supportedDiagnostics.IsDefault)
                Immutable.InterlockedInitialize(ref _supportedDiagnostics, DiagnosticRules.PutAttributeListOnItsOwnLine);

            return _supportedDiagnostics;
        }
    }

    public override void Initialize(AnalysisContext context)
    {
        base.Initialize(context);

        context.RegisterSyntaxNodeAction(f => AnalyzeClassDeclaration(f), SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeEnumDeclaration(f), SyntaxKind.EnumDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeInterfaceDeclaration(f), SyntaxKind.InterfaceDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeStructDeclaration(f), SyntaxKind.StructDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeRecordDeclaration(f), SyntaxKind.RecordDeclaration, SyntaxKind.RecordStructDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeDelegateDeclaration(f), SyntaxKind.DelegateDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeEventFieldDeclaration(f), SyntaxKind.EventFieldDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeFieldDeclaration(f), SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeMethodDeclaration(f), SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeConstructorDeclaration(f), SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeConversionOperatorDeclaration(f), SyntaxKind.ConversionOperatorDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeDestructorDeclaration(f), SyntaxKind.DestructorDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeEventDeclaration(f), SyntaxKind.EventDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeOperatorDeclaration(f), SyntaxKind.OperatorDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzePropertyDeclaration(f), SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(f => AnalyzeIndexerDeclaration(f), SyntaxKind.IndexerDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = classDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : classDeclaration.Keyword;

        Analyze(context, classDeclaration.AttributeLists, token);
    }

    private static void AnalyzeEnumDeclaration(SyntaxNodeAnalysisContext context)
    {
        var enumDeclaration = (EnumDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = enumDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : enumDeclaration.EnumKeyword;

        Analyze(context, enumDeclaration.AttributeLists, token);

        if (!enumDeclaration.IsSingleLine())
        {
            foreach (EnumMemberDeclarationSyntax enumMember in enumDeclaration.Members)
            {
                Analyze(context, enumMember.AttributeLists, enumMember.Identifier);
            }
        }
    }

    private static void AnalyzeInterfaceDeclaration(SyntaxNodeAnalysisContext context)
    {
        var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = interfaceDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : interfaceDeclaration.Keyword;

        Analyze(context, interfaceDeclaration.AttributeLists, token);
    }

    private static void AnalyzeStructDeclaration(SyntaxNodeAnalysisContext context)
    {
        var structDeclaration = (StructDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = structDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : structDeclaration.Keyword;

        Analyze(context, structDeclaration.AttributeLists, token);
    }

    private static void AnalyzeRecordDeclaration(SyntaxNodeAnalysisContext context)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = recordDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : recordDeclaration.Keyword;

        Analyze(context, recordDeclaration.AttributeLists, token);
    }

    private static void AnalyzeDelegateDeclaration(SyntaxNodeAnalysisContext context)
    {
        var delegateDeclaration = (DelegateDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = delegateDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : delegateDeclaration.DelegateKeyword;

        Analyze(context, delegateDeclaration.AttributeLists, token);
    }

    private static void AnalyzeEventFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        var eventFieldDeclaration = (EventFieldDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = eventFieldDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : eventFieldDeclaration.EventKeyword;

        Analyze(context, eventFieldDeclaration.AttributeLists, token);
    }

    private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
    {
        var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = fieldDeclaration.Modifiers;

        SyntaxNodeOrToken nodeOrToken = (modifiers.Any())
            ? modifiers[0]
            : fieldDeclaration.Declaration.Type;

        Analyze(context, fieldDeclaration.AttributeLists, nodeOrToken);
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = methodDeclaration.Modifiers;

        SyntaxNodeOrToken nodeOrToken = (modifiers.Any())
            ? modifiers[0]
            : methodDeclaration.ReturnType;

        Analyze(context, methodDeclaration.AttributeLists, nodeOrToken);
    }

    private static void AnalyzeConstructorDeclaration(SyntaxNodeAnalysisContext context)
    {
        var constructorDeclaration = (ConstructorDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = constructorDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : constructorDeclaration.Identifier;

        Analyze(context, constructorDeclaration.AttributeLists, token);
    }

    private static void AnalyzeConversionOperatorDeclaration(SyntaxNodeAnalysisContext context)
    {
        var conversionOperatorDeclaration = (ConversionOperatorDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = conversionOperatorDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : conversionOperatorDeclaration.ImplicitOrExplicitKeyword;

        Analyze(context, conversionOperatorDeclaration.AttributeLists, token);
    }

    private static void AnalyzeDestructorDeclaration(SyntaxNodeAnalysisContext context)
    {
        var destructorDeclaration = (DestructorDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = destructorDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : destructorDeclaration.TildeToken;

        Analyze(context, destructorDeclaration.AttributeLists, token);
    }

    private static void AnalyzeEventDeclaration(SyntaxNodeAnalysisContext context)
    {
        var eventDeclaration = (EventDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = eventDeclaration.Modifiers;

        SyntaxToken token = (modifiers.Any())
            ? modifiers[0]
            : eventDeclaration.EventKeyword;

        Analyze(context, eventDeclaration.AttributeLists, token);

        if (!eventDeclaration.IsSingleLine())
            AnalyzeAccessorList(context, eventDeclaration.AccessorList);
    }

    private static void AnalyzeOperatorDeclaration(SyntaxNodeAnalysisContext context)
    {
        var operatorDeclaration = (OperatorDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = operatorDeclaration.Modifiers;

        SyntaxNodeOrToken token = (modifiers.Any())
            ? modifiers[0]
            : operatorDeclaration.ReturnType;

        Analyze(context, operatorDeclaration.AttributeLists, token);
    }

    private static void AnalyzePropertyDeclaration(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = propertyDeclaration.Modifiers;

        SyntaxNodeOrToken token = (modifiers.Any())
            ? modifiers[0]
            : propertyDeclaration.Type;

        Analyze(context, propertyDeclaration.AttributeLists, token);

        if (!propertyDeclaration.IsSingleLine())
            AnalyzeAccessorList(context, propertyDeclaration.AccessorList);
    }

    private static void AnalyzeIndexerDeclaration(SyntaxNodeAnalysisContext context)
    {
        var indexerDeclaration = (IndexerDeclarationSyntax)context.Node;

        SyntaxTokenList modifiers = indexerDeclaration.Modifiers;

        SyntaxNodeOrToken nodeOrToken = (modifiers.Any())
            ? modifiers[0]
            : indexerDeclaration.Type;

        Analyze(context, indexerDeclaration.AttributeLists, nodeOrToken);

        if (!indexerDeclaration.IsSingleLine())
            AnalyzeAccessorList(context, indexerDeclaration.AccessorList);
    }

    private static void AnalyzeAccessorList(SyntaxNodeAnalysisContext context, AccessorListSyntax accessorList)
    {
        if (accessorList is null)
            return;

        foreach (AccessorDeclarationSyntax accessor in accessorList.Accessors)
        {
            SyntaxTokenList modifiers = accessor.Modifiers;

            SyntaxToken token = (modifiers.Any())
                ? modifiers[0]
                : accessor.Keyword;

            Analyze(context, accessor.AttributeLists, token);
        }
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, SyntaxList<AttributeListSyntax> attributeLists, SyntaxNodeOrToken nodeOrToken)
    {
        AttributeListSyntax first = attributeLists.FirstOrDefault();

        if (first is null)
            return;

        for (int i = 1; i < attributeLists.Count; i++)
        {
            AttributeListSyntax second = attributeLists[i];
            Analyze(context, first, second);
            first = second;
        }

        Analyze(context, attributeLists.Last(), nodeOrToken);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, AttributeListSyntax attributeList, SyntaxNodeOrToken nodeOrToken)
    {
        if (attributeList.SyntaxTree.IsSingleLineSpan(TextSpan.FromBounds(attributeList.Span.End, nodeOrToken.SpanStart)))
        {
            DiagnosticHelpers.ReportDiagnostic(
                context,
                DiagnosticRules.PutAttributeListOnItsOwnLine,
                Location.Create(nodeOrToken.SyntaxTree, nodeOrToken.Span.WithLength(0)));
        }
    }
}
