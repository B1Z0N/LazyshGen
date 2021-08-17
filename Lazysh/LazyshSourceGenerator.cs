using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lazysh {
    

    public class LazyshSyntaxReceiver : ISyntaxReceiver
    {
        public HashSet<TypeDeclarationSyntax> TypeDeclarationsWithTypeParamsAttributes { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is TypeDeclarationSyntax declaration && declaration.TypeParameterList != null)
            {
                foreach (var typeParam in declaration.TypeParameterList.Parameters)
                {
                    if (typeParam.AttributeLists.Any()) 
                        TypeDeclarationsWithTypeParamsAttributes.Add(declaration);
                }
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
            var namespaceName = "LazyshImpl";
            
            var compilation = context.Compilation;
            var syntaxReceiver = (LazyshSyntaxReceiver) context.SyntaxReceiver;

            var lazyshSrc = @$"
using System;

namespace {namespaceName} {{
    [AttributeUsage(validOn: AttributeTargets.GenericParameter)]
    class LazyshAttribute : Attribute
    {{
        public Type[] Lazies;
        public LazyshAttribute(params Type[] lazies) => Lazies = lazies;
    }}

    interface ILazysh {{ }}
}}
";
            context.AddSource("Lazysh.cs", lazyshSrc);
            
            var options = (CSharpParseOptions) compilation.SyntaxTrees.First().Options;
            var lazyshSyntaxTree = CSharpSyntaxTree.ParseText(lazyshSrc, options);
            compilation = compilation.AddSyntaxTrees(lazyshSyntaxTree);

            // SpinWait.SpinUntil(() => Debugger.IsAttached);
            var targets = new HashSet<ITypeSymbol>();
            foreach (var target in syntaxReceiver.TypeDeclarationsWithTypeParamsAttributes)
            {
                var semanticModel = compilation.GetSemanticModel(target.SyntaxTree);
                var toBeLazyshed = target.TypeParameterList.Parameters
                    .SelectMany(p => p.AttributeLists.SelectMany(atl => atl.Attributes))
                    .Where(at => at.Name.ToString().Equals("Lazysh"))
                    .SelectMany(at => at.ArgumentList.Arguments)
                    .Select(attrExpr => attrExpr.Expression)
                    .OfType<TypeOfExpressionSyntax>()
                    .Select(typeOfExpr => semanticModel.GetTypeInfo(typeOfExpr.Type).Type).ToList();

                if (toBeLazyshed.Any())
                {
                    targets.UnionWith(toBeLazyshed);
                    
                }
            }
            
            foreach (var targetType in targets)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var proxySource = GenerateProxy(targetType, namespaceName);
                context.AddSource($"Lazysh{targetType.Name.Substring(1)}.cs", proxySource);
            }
        }
        
        private static string GenerateFactory(ITypeSymbol)

        private static string GenerateProxy(ITypeSymbol targetType, string namespaceName)
        {
            var allInterfaceMethods = targetType.GetMembers()
                .OfType<IMethodSymbol>()
                .ToList();

            var fullQualifiedName = GetFullQualifiedName(targetType);
            
            var sb = new StringBuilder();
            var proxyName = $"Lazysh{targetType.Name.Substring(1)}";
            sb.Append($@"
using System;

namespace {namespaceName} {{
    // generated via LazyshSourceGenerator
    internal class {proxyName} : Lazy<{fullQualifiedName}>, {fullQualifiedName}, ILazysh
    {{
        public {proxyName}(Func<{fullQualifiedName}> val) :base(val)
        {{
        }}");
            
            foreach (var interfaceMethod in allInterfaceMethods)
            {
                var containingType = interfaceMethod.ContainingType;
                var parameters = string.Join(", ",
                    interfaceMethod.Parameters.Select(x => $"{GetFullQualifiedName(x.Type)} {x.Name}"));
                var parametersNames = string.Join(", ", interfaceMethod.Parameters.Select(x => x.Name));
                var interfaceFullyQualifiedName = GetFullQualifiedName(containingType);
                sb.Append($@"
        {interfaceMethod.ReturnType} {interfaceFullyQualifiedName}.{interfaceMethod.Name}({parameters}) 
            => Value.{interfaceMethod.Name}({parametersNames});");
            }

            return sb.Append($@"
        public static {fullQualifiedName} Create(Func<{fullQualifiedName}> getter) => new {proxyName}(getter);
  }}
}}").ToString();
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