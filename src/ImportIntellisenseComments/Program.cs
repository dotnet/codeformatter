using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using System.IO;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace ImportIntellisenseComments
{
    class Program
    {
        static void Main(string[] args)
        {

            if (args.Length != 2)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("{0} <IntelliSenseDirectory> <SourceDirectory>", AppDomain.CurrentDomain.FriendlyName);
                Console.ReadLine();
                return;
            }

            ParseIntelliSense p = new ParseIntelliSense(args[0]);

            //Check parameters
            if (!Directory.Exists(p.IntelliSenseDirectory))
            {
                Console.WriteLine($"Directory not found: {p.IntelliSenseDirectory}");
                Console.ReadLine();
                return;
            }            
            if (!Directory.Exists(args[1]))
            {
                Console.WriteLine($"Directory not found: {args[1]}");
                Console.ReadLine();
                return;
            }
            p.ParseIntelliSenseFiles();

            // Adds all the references needed for CommentID to build correctly
            var metadataReferences = new MetadataReference[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };

            foreach (var file in EnumerateSourceFiles(args[1]))
            {

                // Reads the source code from the file
                SourceText text;
                using (var stream = File.OpenRead(file))
                {
                    text = SourceText.From(stream);
                }

                SyntaxTree tree = (SyntaxTree)CSharpSyntaxTree.ParseText(text);

                var compilation = CSharpCompilation.Create("test", syntaxTrees: new[] { tree }, 
                    references: metadataReferences);
                var rewriter = new Rewriter(compilation.GetSemanticModel(tree), p.MembersDictionary);
                var newTreeRootNode = rewriter.Visit(tree.GetRoot());
                var newTree = newTreeRootNode.SyntaxTree;

                //Checks to see if the source code was changed
                if (tree != newTree)
                {
                    Workspace workspace = MSBuildWorkspace.Create();
                    OptionSet options = workspace.Options;
                    options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAccessors, false);
                    options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousMethods, false);
                    options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousTypes, false);
                    options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInControlBlocks, false);
                    options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInLambdaExpressionBody, false);
                    options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, true);
                    options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInObjectCollectionArrayInitializers, false);
                    options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInProperties, true);
                    options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, true);

                    SyntaxNode formattedNode = Formatter.Format(newTree.GetRoot(), workspace, options);

                    Console.WriteLine($"Saving file: {file}");
                    SourceText newText = formattedNode.GetText();
                    using (var writer = new StreamWriter(file, append: false, encoding: text.Encoding))
                    {
                        newText.Write(writer);
                    }
                }

            }

            Console.WriteLine("Press ENTER to exit;");
            Console.ReadLine();
        }

        private static IEnumerable<string> EnumerateSourceFiles(string path) =>
            Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories);

    }

}