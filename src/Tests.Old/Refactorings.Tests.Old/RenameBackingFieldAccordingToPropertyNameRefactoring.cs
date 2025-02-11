﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Roslynator.CSharp.Refactorings.Tests
{
    internal class RenameBackingFieldAccordingToPropertyNameRefactoring
    {
        private class Entity
        {
            private string _name;
            private string _value;

            public string Name
            {
                get { return _value; }
            }

            public string Name2
            {
                get { return _name; }
            }
        }
    }
}
