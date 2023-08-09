﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Roslynator.CSharp.CodeFixes;
using Roslynator.Testing.CSharp;
using Xunit;

namespace Roslynator.CSharp.Analysis.Tests;

public class RCS1009UseExplicitTypeInsteadOfVarInForEachTests : AbstractCSharpDiagnosticVerifier<UseExplicitTypeInsteadOfVarInForEachAnalyzer, UseExplicitTypeInsteadOfVarInForEachCodeFixProvider>
{
    public override DiagnosticDescriptor Descriptor { get; } = DiagnosticRules.UseExplicitTypeInsteadOfVarInForEach;

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.UseExplicitTypeInsteadOfVarInForEach)]
    public async Task TestDiagnostic()
    {
        await VerifyDiagnosticAndFixAsync(@"
using System;
using System.Collections.Generic;

class C
{
    public void M()
    {
        var items = new List<DateTime>();

        foreach ([|var|] item in items)
        {
        }
    }
}
", @"
using System;
using System.Collections.Generic;

class C
{
    public void M()
    {
        var items = new List<DateTime>();

        foreach (DateTime item in items)
        {
        }
    }
}
");
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.UseExplicitTypeInsteadOfVarInForEach)]
    public async Task TestDiagnostic_Tuple_DeclarationExpression()
    {
        await VerifyDiagnosticAndFixAsync(@"
using System.Collections.Generic;

class C
{
    IEnumerable<(object x, System.DateTime y)> M()
    {
        foreach ([|var|] (x, y) in M())
        {
        }

        return default;
    }
}
", @"
using System.Collections.Generic;

class C
{
    IEnumerable<(object x, System.DateTime y)> M()
    {
        foreach ((object x, System.DateTime y) in M())
        {
        }

        return default;
    }
}
");
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.UseExplicitTypeInsteadOfVarInForEach)]
    public async Task TestDiagnostic_TupleExpression()
    {
        await VerifyDiagnosticAndFixAsync(@"
using System.Collections.Generic;

class C
{
    IEnumerable<(object x, System.DateTime y)> M()
    {
        foreach ((object x, [|var|] y) in M())
        {
        }

        return default;
    }
}
", @"
using System.Collections.Generic;

class C
{
    IEnumerable<(object x, System.DateTime y)> M()
    {
        foreach ((object x, System.DateTime y) in M())
        {
        }

        return default;
    }
}
");
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.UseExplicitTypeInsteadOfVarInForEach)]
    public async Task TestDiagnostic_TupleExpression_AllVar()
    {
        await VerifyDiagnosticAndFixAsync(@"
using System.Collections.Generic;

class C
{
    IEnumerable<(object x, System.DateTime y)> M()
    {
        foreach (([|var|] x, [|var|] y) in M())
        {
        }

        return default;
    }
}
", @"
using System.Collections.Generic;

class C
{
    IEnumerable<(object x, System.DateTime y)> M()
    {
        foreach ((object x, System.DateTime y) in M())
        {
        }

        return default;
    }
}
");
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.UseExplicitTypeInsteadOfVarInForEach)]
    public async Task TestNoDiagnostic()
    {
        await VerifyNoDiagnosticAsync(@"
using System;
using System.Collections.Generic;

class C
{
    public void M()
    {
        var items = new List<DateTime>();

        foreach (DateTime item in items)
        {
        }
    }
}");
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.UseExplicitTypeInsteadOfVarWhenTypeIsObvious)]
    public async Task Test_TupleExpression_WithDiscardPredefinedType()
    {
        await VerifyDiagnosticAndFixAsync(@"
using System;
using System.Collections.Generic;

class C
{
    void M()
    {
        var items = new List<(object, System.DateTime)>();

        foreach ([|var|] (_, y) in items)
        {
        }
    }
}
", @"
using System;
using System.Collections.Generic;

class C
{
    void M()
    {
        var items = new List<(object, System.DateTime)>();

        foreach ((object _, DateTime y) in items)
        {
        }
    }
}
");
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.UseExplicitTypeInsteadOfVarWhenTypeIsObvious)]
    public async Task Test_TupleExpression_WithDiscardNonPredefinedType()
    {
        await VerifyDiagnosticAndFixAsync(@"
using System;
using System.Collections.Generic;

class C
{
    void M()
    {
        var items = new List<(object, System.DateTime)>();

        foreach ([|var|] (x, _) in items)
        {
        }
    }
}
", @"
using System;
using System.Collections.Generic;

class C
{
    void M()
    {
        var items = new List<(object, System.DateTime)>();

        foreach ((object x, DateTime _) in items)
        {
        }
    }
}
");
    }
}
