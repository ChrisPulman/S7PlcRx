// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using S7PlcRx.SourceGenerators;

namespace S7PlcRx.Tests.SourceGenerators;

/// <summary>
/// Tests for S7 tag binding source generation.
/// </summary>
public sealed class S7TagBindingSourceGeneratorTests
{
    /// <summary>
    /// Ensures attributed partial properties generate PLC binding hooks and grouped byte-array metadata.
    /// </summary>
    [Test]
    public void Generate_WithDbTags_ShouldEmitBindingHooksAndGroupedByteArrayMetadata()
    {
        const string source = """
            using S7PlcRx.SourceGeneration;

            namespace Demo;

            [S7PlcBinding]
            public partial class MachineTags
            {
                [S7Tag("DB1.DBD0", PollIntervalMs = 100)]
                public partial float Temperature { get; set; }

                [S7Tag("DB1.DBX4.0", PollIntervalMs = 100)]
                public partial bool Running { get; set; }

                [S7Tag("DB2.DBW0", PollIntervalMs = 250, Direction = S7TagDirection.ReadOnly)]
                public partial short Counter { get; set; }
            }
            """;

        var result = RunGenerator(source);
        var generated = string.Join("\n---\n", result.GeneratedTrees.Select(static tree => tree.GetText().ToString()));

        Assert.Multiple(() =>
        {
            Assert.That(generated, Does.Contain("global::S7PlcRx.Binding.S7TagRuntimeBinding.Bind"));
            Assert.That(generated, Does.Contain("new global::S7PlcRx.Binding.S7TagDefinition"));
            Assert.That(generated, Does.Contain("nameof(Temperature)"));
            Assert.That(generated, Does.Contain("\"DB1.DBD0\""));
            Assert.That(generated, Does.Contain("S7TagDirection.ReadOnly"));
            Assert.That(generated, Does.Contain("__s7SuppressWrites"));
            Assert.That(result.Diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error), Is.Empty);
        });
    }

    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IDisposable).GetTypeInfo().Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([new S7TagBindingSourceGenerator().AsSourceGenerator()], parseOptions: parseOptions);
        driver = driver.RunGenerators(compilation);

        return driver.GetRunResult();
    }
}
