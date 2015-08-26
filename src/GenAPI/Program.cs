﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Writers;
using Microsoft.Cci.Writers.Syntax;
using Microsoft.Fx.CommandLine;

namespace GenAPI
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            ParseCommandLine(args);
            HostEnvironment host = new HostEnvironment();
            host.UnableToResolve += host_UnableToResolve;

            host.UnifyToLibPath = true;
            if (!string.IsNullOrWhiteSpace(s_libPath))
                host.AddLibPaths(HostEnvironment.SplitPaths(s_libPath));

            IEnumerable<IAssembly> assemblies = host.LoadAssemblies(HostEnvironment.SplitPaths(s_assembly));

            if (!assemblies.Any())
            {
                Console.WriteLine("ERROR: Failed to load any assemblies from '{0}'", s_assembly);
                return 1;
            }

            using (TextWriter output = GetOutput())
            using (IStyleSyntaxWriter syntaxWriter = GetSyntaxWriter(output))
            {
                ICciWriter writer = GetWriter(output, syntaxWriter);
                writer.WriteAssemblies(assemblies);
            }

            return 0;
        }

        private static void host_UnableToResolve(object sender, UnresolvedReference<IUnit, AssemblyIdentity> e)
        {
            Console.WriteLine("Unable to resolve assembly '{0}' referenced by '{1}'.", e.Unresolved.ToString(), e.Referrer.ToString());
        }

        private static TextWriter GetOutput()
        {
            // If this is a null, empty, whitespace, or a directory use console
            if (string.IsNullOrWhiteSpace(s_out) || Directory.Exists(s_out))
                return Console.Out;

            return new StreamWriter(File.OpenWrite(s_out));
        }

        private static ICciWriter GetWriter(TextWriter output, IStyleSyntaxWriter syntaxWriter)
        {
            ICciFilter filter = GetFilter();

            switch (s_writer)
            {
                case WriterType.DocIds:
                    return new DocumentIdWriter(output, filter);
                case WriterType.TypeForwards:
                    return new TypeForwardWriter(output, filter);
                case WriterType.TypeList:
                    return new TypeListWriter(syntaxWriter, filter);
                default:
                case WriterType.CSDecl:
                    {
                        CSharpWriter writer = new CSharpWriter(syntaxWriter, filter, s_apiOnly);
                        writer.IncludeSpaceBetweenMemberGroups = writer.IncludeMemberGroupHeadings = s_memberHeadings;
                        writer.HighlightBaseMembers = s_hightlightBaseMembers;
                        writer.HighlightInterfaceMembers = s_hightlightInterfaceMembers;
                        return writer;
                    }
            }
        }

        private static ICciFilter GetFilter()
        {
            if (string.IsNullOrWhiteSpace(s_apiList))
            {
                if (s_all)
                    return new IncludeAllFilter();

                return new PublicOnlyCciFilter(excludeAttributes: s_apiOnly);
            }

            return new DocIdWhitelistFilter(s_apiList);
        }

        private static IStyleSyntaxWriter GetSyntaxWriter(TextWriter output)
        {
            if (s_writer != WriterType.CSDecl && s_writer != WriterType.TypeList)
                return null;

            switch (s_syntaxWriter)
            {
                case SyntaxWriterType.Xml:
                    return new OpenXmlSyntaxWriter(output);
                case SyntaxWriterType.Html:
                    return new HtmlSyntaxWriter(output);
                case SyntaxWriterType.Text:
                default:
                    return new TextSyntaxWriter(output);
            }
        }

        private enum WriterType
        {
            CSDecl,
            DocIds,
            TypeForwards,
            TypeList,
        }

        private enum SyntaxWriterType
        {
            Text,
            Html,
            Xml
        }

        private static string s_assembly;
        private static WriterType s_writer = WriterType.CSDecl;
        private static SyntaxWriterType s_syntaxWriter = SyntaxWriterType.Text;
        private static string s_apiList;
        private static string s_out;
        private static string s_libPath;
        private static bool s_apiOnly;
        private static bool s_memberHeadings;
        private static bool s_hightlightBaseMembers;
        private static bool s_hightlightInterfaceMembers;
        private static bool s_all;

        private static void ParseCommandLine(string[] args)
        {
            CommandLineParser.ParseForConsoleApplication((parser) =>
            {
                parser.DefineOptionalQualifier("libPath", ref s_libPath, "Delimited (',' or ';') set of paths to use for resolving assembly references");
                parser.DefineAliases("apiList", "al");
                parser.DefineOptionalQualifier("apiList", ref s_apiList, "(-al) Specify a api list in the DocId format of which APIs to include.");
                parser.DefineAliases("writer", "w");
                parser.DefineOptionalQualifier<WriterType>("writer", ref s_writer, "(-w) Specify the writer type to use.");
                parser.DefineAliases("syntax", "s");
                parser.DefineOptionalQualifier<SyntaxWriterType>("syntax", ref s_syntaxWriter, "(-s) Specific the syntax writer type. Only used if the writer is CSDecl");
                parser.DefineOptionalQualifier("out", ref s_out, "Output path. Default is the console.");
                parser.DefineAliases("apiOnly", "api");
                parser.DefineOptionalQualifier("apiOnly", ref s_apiOnly, "(-api) [CSDecl] Include only API's not CS code that compiles.");
                parser.DefineOptionalQualifier("all", ref s_all, "Include all API's not just public APIs. Default is public only.");
                parser.DefineAliases("memberHeadings", "mh");
                parser.DefineOptionalQualifier("memberHeadings", ref s_memberHeadings, "(-mh) [CSDecl] Include member headings for each type of member.");
                parser.DefineAliases("hightlightBaseMembers", "hbm");
                parser.DefineOptionalQualifier("hightlightBaseMembers", ref s_hightlightBaseMembers, "(-hbm) [CSDecl] Highlight overridden base members.");
                parser.DefineAliases("hightlightInterfaceMembers", "him");
                parser.DefineOptionalQualifier("hightlightInterfaceMembers", ref s_hightlightInterfaceMembers, "(-him) [CSDecl] Highlight interface implementation members.");
                parser.DefineParameter<string>("", ref s_assembly, "Path for an specific assembly or a directory to get all assemblies.");
            }, args);
        }
    }
}
