using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lazysh
{
    public class LazyshSyntaxReceiver : ISyntaxReceiver
    {
        public HashSet<TypeDeclarationSyntax> TypeDeclarationsWithAttributes { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is TypeDeclarationSyntax declaration
                && declaration.AttributeLists.Any())
            {
                TypeDeclarationsWithAttributes.Add(declaration);
            }
        }
    }

    [Generator]
    public class LazyshSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
            => context.RegisterForSyntaxNotifications(() => new LazyshSyntaxReceiver());

        public void Execute(GeneratorExecutionContext context)
        {
            var namespaceName = "LazyshImplDefault";

            var compilation = context.Compilation;

            var syntaxReceiver = (LazyshSyntaxReceiver) context.SyntaxReceiver;
            var targets = syntaxReceiver.TypeDeclarationsWithAttributes;

            var lazyshSrc = "class LazyshAttribute : System.Attribute { }";
            context.AddSource("Lazysh.cs", lazyshSrc);

            var options = (CSharpParseOptions) compilation.SyntaxTrees.First().Options;
            var lazyshSyntaxTree = CSharpSyntaxTree.ParseText(lazyshSrc, options);
            compilation = compilation.AddSyntaxTrees(lazyshSyntaxTree);

            var logAttribute = compilation.GetTypeByMetadataName("LazyshAttribute");

            var targetTypes = new HashSet<ITypeSymbol>();
            foreach (var targetTypeSyntax in targets)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var semanticModel = compilation.GetSemanticModel(targetTypeSyntax.SyntaxTree);
                var targetType = semanticModel.GetDeclaredSymbol(targetTypeSyntax);
                var hasLogAttribute = targetType.GetAttributes()
                    .Any(x => x.AttributeClass.Equals(logAttribute));
                if (!hasLogAttribute)
                    continue;

                if (targetTypeSyntax is not InterfaceDeclarationSyntax)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            "LG01",
                            "Lazysh generator",
                            "[Lazysh] must be applied to an interface",
                            defaultSeverity: DiagnosticSeverity.Error,
                            severity: DiagnosticSeverity.Error,
                            isEnabledByDefault: true,
                            warningLevel: 0,
                            location: targetTypeSyntax.GetLocation()));
                    continue;
                }

                targetTypes.Add(targetType);
            }

            foreach (var targetType in targetTypes)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var proxySource = GenerateProxy(targetType, namespaceName);
                context.AddSource($"Lazysh{targetType.Name}.cs", proxySource);
            }
        }

        private string GenerateProxy(ITypeSymbol targetType, string namespaceName)
        {
            var allInterfaceMethods = targetType.AllInterfaces
                .SelectMany(x => x.GetMembers())
                .Concat(targetType.GetMembers())
                .OfType<IMethodSymbol>()
                .ToList();

            var fullQualifiedName = GetFullQualifiedName(targetType);

            var sb = new StringBuilder();
            var proxyName = $"Lazysh{targetType.Name.Substring(1)}";
            sb.Append($@"
using System;
using System.Text;
using NLog;
using System.Diagnostics;
namespace {namespaceName}
{{
  public class {proxyName} : {fullQualifiedName}
  {{
    private readonly {fullQualifiedName} _target;
    public {proxyName}({fullQualifiedName} target)
      => _target = target;
");

            foreach (var interfaceMethod in allInterfaceMethods)
            {
                var containingType = interfaceMethod.ContainingType;
                var parametersList = string.Join(", ",
                    interfaceMethod.Parameters.Select(x => $"{GetFullQualifiedName(x.Type)} {x.Name}"));
                var argumentLog = string.Join(", ", interfaceMethod.Parameters.Select(x => $"{x.Name} = {{{x.Name}}}"));
                var argumentList = string.Join(", ", interfaceMethod.Parameters.Select(x => x.Name));
                var isVoid = interfaceMethod.ReturnsVoid;
                var interfaceFullyQualifiedName = GetFullQualifiedName(containingType);
                sb.Append($@"
    {interfaceMethod.ReturnType} {interfaceFullyQualifiedName}.{interfaceMethod.Name}({parametersList})
    {{
{Log("LogLevel.Info", $"\"{interfaceMethod.Name} started...\"")}
{Log("LogLevel.Info", $"$\"  Arguments: {argumentLog}\"")}
      var sw = new Stopwatch();
      sw.Start();
      try
      {{
");

                sb.Append("        ");
                if (!isVoid)
                {
                    sb.Append("var result = ");
                }

                sb.AppendLine($"_target.{interfaceMethod.Name}({argumentList});");
                sb.AppendLine("  " + Log("LogLevel.Info",
                    $@"$""{interfaceMethod.Name} finished in {{sw.ElapsedMilliseconds}} ms"""));
                if (!isVoid)
                {
                    sb.AppendLine("  " + Log("LogLevel.Info", "$\"Return value: {result}\""));
                    sb.AppendLine("        return result;");
                }

                sb.Append($@"
      }}
      catch (Exception e)
      {{
  {Log("LogLevel.Error", "e.ToString()")}
        throw;
      }}
    }}");
            }

            sb.Append(@"
  }
}");
            return sb.ToString();

            string Log(string logLevel, string message)
            {
                return $"      _logger.Log({logLevel}, {message});";
            }
        }

        private static string GetFullQualifiedName(ISymbol symbol)
        {
            var containingNamespace = symbol.ContainingNamespace;
            if (!containingNamespace.IsGlobalNamespace)
                return containingNamespace.ToDisplayString() + "." + symbol.Name;

            return symbol.Name;
        }
    }
}