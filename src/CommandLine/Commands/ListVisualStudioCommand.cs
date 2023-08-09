﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Locator;
using static Roslynator.Logger;

namespace Roslynator.CommandLine;

internal class ListVisualStudioCommand
{
    public ListVisualStudioCommand(ListVisualStudioCommandLineOptions options)
    {
        Options = options;
    }

    public ListVisualStudioCommandLineOptions Options { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
    public CommandStatus Execute()
    {
        int count = 0;
        foreach (VisualStudioInstance instance in MSBuildLocator.QueryVisualStudioInstances())
        {
            WriteLine($"{instance.Name} {instance.Version}", ConsoleColors.Cyan, Verbosity.Normal);
            WriteLine($"  Visual Studio Path: {instance.VisualStudioRootPath}", Verbosity.Detailed);
            WriteLine($"  MSBuild Path:       {instance.MSBuildPath}", Verbosity.Detailed);

            count++;
        }

        WriteLine(Verbosity.Minimal);
        WriteLine($"{count} Visual Studio {((count == 1) ? "installation" : "installations")} found", ConsoleColors.Green, Verbosity.Minimal);

        return CommandStatus.Success;
    }
}
