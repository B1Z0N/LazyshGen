using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lazysh
{
    [Generator]
    public class LazyshSourceGenerator : ISourceGenerator
    {
        #region Constants

        private const string ItsGeneratedMsg = "// This code is generated with " + nameof(LazyshSourceGenerator) + ", do not modify by hand!";
        private const string NamespaceName = "LazyshImpl";
        private const string ReadmeLink = "https://github.com/B1Z0N/LazyshGen/";
        
        #endregion

        #region ISourceGenerator implementation 

        public void Initialize(GeneratorInitializationContext context)
            => context.RegisterForSyntaxNotifications(() => new LazyshSyntaxReceiver());

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;
            var syntaxReceiver = (LazyshSyntaxReceiver) context.SyntaxReceiver;

            var lazyshSrc = @$"
{ItsGeneratedMsg}

using System;

namespace {NamespaceName} {{
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
            var allTargets = new HashSet<ITypeSymbol>();
            var factoryToTargets = new Dictionary<ITypeSymbol, List<ITypeSymbol>>();
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
                    allTargets.UnionWith(toBeLazyshed);
                    factoryToTargets[semanticModel.GetDeclaredSymbol(target)] = toBeLazyshed;
                }
            }

            foreach (var targetType in allTargets)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var proxySource = GenerateProxy(targetType, NamespaceName);
                context.AddSource($"{GetProxyName(targetType)}.cs", proxySource);
            }

            var factorySource = GenerateFactories(factoryToTargets, allTargets, NamespaceName);
            context.AddSource($"LazyshFactory.cs", factorySource);
        }

        #endregion
        
        #region Generating classes
        
        private static string GenerateFactories(
            Dictionary<ITypeSymbol, List<ITypeSymbol>> factoryToTargets,
            HashSet<ITypeSymbol> allTargets,
            string namespaceName)
        {
            var sb = new StringBuilder();

            sb.Append($@"
{ItsGeneratedMsg}

using System;
using System.Collections.Generic;

namespace {namespaceName} {{
    public static class LazyshFactory
    {{
        static Dictionary<Type, object> interfaceToLazy = new();

        static LazyshFactory()
        {{");

            foreach (var target in allTargets)
            {
                var lazyName = GetProxyName(target);
                var targetName = GetFullQualifiedName(target);
                sb.Append(@$"
           interfaceToLazy[typeof({targetName})] = (Func<Func<{targetName}>, {targetName}>) ({lazyName}.Create);");
            }

            sb.Append($@"
        }}

        public static T TryGet<T>(Func<T> getter) where T : class // interface to be exact
        {{
            interfaceToLazy.TryGetValue(typeof(T), out var getLazy);
            if (getLazy == null) return null;

            return ((Func<Func<T>, T>)getLazy)(getter);
        }}

        public static T Get<T>(Func<T> getter) where T : class // interface to be exact
        {{
            return TryGet(getter) ?? throw new LazyshArgumentException(
                $""'{{typeof(T)}}' is not allowed. {GetAllowedFactoryArgumentsMsg(allTargets)}. Please see README for howto: {ReadmeLink}"");
        }}
    }}

    public class LazyshArgumentException : ArgumentException 
    {{
        public LazyshArgumentException(string msg) :base(msg) {{ }}    
    }}
");

            foreach (var factory in factoryToTargets.Keys)
            {
                sb.Append($@"
    public static class {factory.Name}
    {{
        static HashSet<Type> ThisFactoryLazies = new HashSet<Type> {{ typeof({String.Join("), typeof(", factoryToTargets[factory].Select(GetFullQualifiedName))}) }};
         
        public static T TryGet<T>(Func<T> getter) where T : class // interface to be exact
            => ThisFactoryLazies.Contains(typeof(T)) ? LazyshFactory.TryGet<T>(getter) : null;

        public static T Get<T>(Func<T> getter) where T : class // interface to be exact
        {{
            return TryGet(getter) ?? throw new LazyshArgumentException(
                $""'{{typeof(T)}}' is not allowed. {GetAllowedFactoryArgumentsMsg(factoryToTargets[factory])}. Please see README for howto: {ReadmeLink}"");
        }}
    }}
}}");
            }
            
            return sb.ToString();
        }

        private static string GenerateProxy(ITypeSymbol targetType, string namespaceName)
        {
            var allInterfaceMethods = targetType.GetMembers()
                .OfType<IMethodSymbol>()
                .ToList();

            var fullQualifiedName = GetFullQualifiedName(targetType);

            var sb = new StringBuilder();
            var proxyName = GetProxyName(targetType);
            sb.Append($@"
{ItsGeneratedMsg}

using System;

namespace {namespaceName} {{
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
        
        #endregion

        #region Helpers 

        private static string GetFullQualifiedName(ISymbol symbol)
        {
            var containingNamespace = symbol.ContainingNamespace;
            if (!containingNamespace.IsGlobalNamespace)
                return containingNamespace.ToDisplayString() + "." + symbol.Name;

            return symbol.Name;
        }

        private static string GetProxyName(ITypeSymbol target) => $"Lazysh{target.Name.Substring(1)}";

        private static string GetAllowedFactoryArgumentsMsg(IEnumerable<ITypeSymbol> allowed)
            => $"Allowed list: [{String.Join(", ", allowed.Select(GetFullQualifiedName))}]";

        #endregion
    }

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
}