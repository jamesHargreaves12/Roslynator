﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Roslynator;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal readonly struct OverriddenSymbolInfo : IEquatable<OverriddenSymbolInfo>
{
    public OverriddenSymbolInfo(ISymbol symbol, ISymbol overriddenSymbol)
    {
        Symbol = symbol;
        OverriddenSymbol = overriddenSymbol;
    }

    public ISymbol Symbol { get; }

    public ISymbol OverriddenSymbol { get; }

    public bool Success
    {
        get { return Symbol is not null; }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get { return $"{Symbol.ToDisplayString(SymbolDisplayFormats.Test)} {OverriddenSymbol.ToDisplayString(SymbolDisplayFormats.Test)}"; }
    }

    public override bool Equals(object obj)
    {
        return obj is OverriddenSymbolInfo other && Equals(other);
    }

    public bool Equals(OverriddenSymbolInfo other)
    {
        return SymbolEqualityComparer.Default.Equals(Symbol, other.Symbol);
    }

    public override int GetHashCode()
    {
        return SymbolEqualityComparer.Default.GetHashCode(Symbol);
    }

    public static bool operator ==(in OverriddenSymbolInfo info1, in OverriddenSymbolInfo info2)
    {
        return info1.Equals(info2);
    }

    public static bool operator !=(in OverriddenSymbolInfo info1, in OverriddenSymbolInfo info2)
    {
        return !(info1 == info2);
    }
}
