﻿// Copyright (c) Josef Pihrt and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AddCodeFileHeader
{
    internal static class Program
    {
        private const StringComparison _comparison = StringComparison.OrdinalIgnoreCase;

        private const string Header = "// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.";

        private static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
#if DEBUG
                args = new string[] { @"..\..\..\.." };
#else
                args = new string[] { Environment.CurrentDirectory };
#endif
            }

            string[] filesWithoutHeader = args
                .SelectMany(FindFilesWithoutHeader)
                .ToArray();

            if (filesWithoutHeader.Length == 0)
            {
                Console.WriteLine("No file without header found.");
            }
            else
            {
                foreach (string filePath in filesWithoutHeader)
                {
                    Console.WriteLine($"Adding header to {filePath}");

                    string s = File.ReadAllText(filePath, Encoding.UTF8);

                    s = Header + "\r\n\r\n" + s.TrimStart();

                    File.WriteAllText(filePath, s, Encoding.UTF8);
                }
            }
        }

        private static IEnumerable<string> FindFilesWithoutHeader(string dirPath)
        {
            foreach (string filePath in Directory.EnumerateFiles(dirPath, "*.cs", SearchOption.AllDirectories))
            {
                if (!IsGeneratedCodeFile(filePath))
                {
                    using (IEnumerator<string> en = File.ReadLines(filePath, Encoding.UTF8).GetEnumerator())
                    {
                        if (en.MoveNext()
                            && !string.Equals(en.Current, Header, StringComparison.Ordinal))
                        {
                            if (en.Current.StartsWith("// <auto-generated>", StringComparison.Ordinal))
                                continue;

                            bool isAutogenerated = false;

                            while (en.MoveNext()
                                && en.Current.StartsWith("//", StringComparison.Ordinal))
                            {
                                if (en.Current.StartsWith("// <auto-generated>", StringComparison.Ordinal))
                                {
                                    isAutogenerated = true;
                                    break;
                                }
                            }

                            if (!isAutogenerated)
                            {
                                Console.WriteLine($"header not found in {filePath.Replace(dirPath, "")}");

                                yield return filePath;
                            }
                        }
                    }
                }
            }
        }

        public static bool IsGeneratedCodeFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            string fileName = Path.GetFileName(filePath);

            if (fileName.StartsWith("TemporaryGeneratedFile_", _comparison))
                return true;

            string extension = Path.GetExtension(fileName);

            if (string.IsNullOrEmpty(extension))
                return false;

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            if (string.Equals(fileNameWithoutExtension, "AssemblyInfo", _comparison))
                return true;

            if (fileNameWithoutExtension.EndsWith(".Designer", _comparison))
                return true;

            if (fileNameWithoutExtension.EndsWith(".Generated", _comparison))
                return true;

            if (fileNameWithoutExtension.EndsWith(".g", _comparison))
                return true;

            if (fileNameWithoutExtension.EndsWith(".g.i", _comparison))
                return true;

            if (fileNameWithoutExtension.EndsWith(".AssemblyAttributes", _comparison))
                return true;

            return false;
        }
    }
}
