﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using CommandLine;

namespace Roslynator.CommandLine;

// Files, IgnoredFiles
public abstract class MSBuildCommandLineOptions : BaseCommandLineOptions
{
    [Option(
        longName: OptionNames.IgnoredProjects,
        HelpText = "Defines projects that should not be analyzed.",
        MetaValue = "<PROJECT_NAME>")]
    public IEnumerable<string> IgnoredProjects { get; set; }

    [Option(
        longName: "language",
        HelpText = "Defines project language. Allowed values are cs[harp] or v[isual-]b[asic].",
        MetaValue = "<LANGUAGE>")]
    public string Language { get; set; }

    [Option(
        shortName: OptionShortNames.MSBuildPath,
        longName: OptionNames.MSBuildPath,
        HelpText = "Defines a path to MSBuild directory.",
        MetaValue = "<DIRECTORY_PATH>")]
    public string MSBuildPath { get; set; }

    [Option(
        longName: OptionNames.Projects,
        HelpText = "Defines projects that should be analyzed.",
        MetaValue = "<PROJECT_NAME>")]
    public IEnumerable<string> Projects { get; set; }

    [Option(
        shortName: OptionShortNames.Properties,
        longName: "properties",
        HelpText = "Defines one or more MSBuild properties.",
        MetaValue = "<NAME=VALUE>")]
    public IEnumerable<string> Properties { get; set; }

    internal bool TryGetLanguage(out string value)
    {
        if (Language is not null)
            return ParseHelpers.TryParseLanguage(Language, out value);

        value = null;
        return true;
    }

    internal bool TryGetProjectFilter(out ProjectFilter projectFilter)
    {
        projectFilter = default;

        string language = null;

        if (Language is not null
            && !ParseHelpers.TryParseLanguage(Language, out language))
        {
            return false;
        }

        if (Projects?.Any() == true
            && IgnoredProjects?.Any() == true)
        {
            Logger.WriteLine($"Cannot specify both '{OptionNames.Projects}' and '{OptionNames.IgnoredProjects}'.", Roslynator.Verbosity.Quiet);
            return false;
        }

        projectFilter = new ProjectFilter(Projects, IgnoredProjects, language);
        return true;
    }
}
