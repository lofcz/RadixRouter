using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;

namespace RadixRouter.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public class AuthorizeExtGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Získáme všechny enum deklarace s [AuthRoleEnum] atributem
        IncrementalValuesProvider<INamedTypeSymbol?> enumDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName("RadixRouter.Shared.AuthRoleEnumAttribute", 
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx)
            ).Where(static m => m is not null);

        // Registrujeme výstup
        context.RegisterSourceOutput(enumDeclarations,
            static (spc, enumSymbol) => Execute(enumSymbol!, spc));
    }
    
    private static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is EnumDeclarationSyntax { AttributeLists.Count: > 0 };

    private static INamedTypeSymbol? GetSemanticTargetForGeneration(GeneratorAttributeSyntaxContext context)
    {
        EnumDeclarationSyntax enumDeclaration = (EnumDeclarationSyntax)context.TargetNode;
        SemanticModel model = context.SemanticModel;

        if (model.GetDeclaredSymbol(enumDeclaration) is not INamedTypeSymbol enumSymbol)
        {
            return null;
        }

        // Kontrola, zda má enum [AuthRoleEnum] atribut
        return HasAuthRoleEnumAttribute(enumSymbol) ? enumSymbol : null;
    }

    private static bool HasAuthRoleEnumAttribute(INamedTypeSymbol enumSymbol)
        => enumSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name is "AuthRoleEnumAttribute");

   private static void Execute(INamedTypeSymbol enumSymbol, SourceProductionContext context)
    {
        string source = $$"""
                          using System;
                          using System.Collections.Generic;
                          using System.Linq;
                          using RadixRouter.Shared;
                          using {{enumSymbol.ContainingNamespace}};

                          #nullable enable

                          namespace {{enumSymbol.ContainingNamespace}}
                          {
                              public sealed class {{enumSymbol.Name}}AuthRole : IRole
                              {
                                  public {{enumSymbol.Name}} Role { get; }
                                  public string Name => Role.ToString();
                                  public int Value => (int)Role;
                          
                                  public {{enumSymbol.Name}}AuthRole({{enumSymbol.Name}} role)
                                  {
                                      Role = role;
                                  }
                          
                                  public static implicit operator {{enumSymbol.Name}}AuthRole({{enumSymbol.Name}} role)
                                      => new(role);
                          
                                  public override bool Equals(object? obj)
                                      => obj is {{enumSymbol.Name}}AuthRole other && Role.Equals(other.Role);
                          
                                  public override int GetHashCode()
                                      => Role.GetHashCode();
                          
                                  public override string ToString()
                                      => Role.ToString();
                              }
                          
                              public static class {{enumSymbol.Name}}Extensions
                              {
                                    public static IReadOnlyList<IRole> ToAuthRoles(this IEnumerable<{{enumSymbol.Name}}> roles)
                                        => roles.Select(r => new {{enumSymbol.Name}}AuthRole(r)).ToList();
                           
                                    public static IReadOnlyList<{{enumSymbol.Name}}> FromAuthRoles(this IEnumerable<IRole> roles)
                                        => roles.OfType<{{enumSymbol.Name}}AuthRole>().Select(r => r.Role).ToList();
                           
                                    public static {{enumSymbol.Name}}? TryParseRole(this IRole role)
                                        => role is {{enumSymbol.Name}}AuthRole typedRole ? typedRole.Role : null;
                              }
                          
                              [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
                              public sealed class AuthorizeExt : AuthorizeExtAttributeBase
                              {
                                  private readonly List<{{enumSymbol.Name}}AuthRole> _roles;
                                  public override IReadOnlyList<IRole> Roles => _roles;
                          
                                  public AuthorizeExt({{enumSymbol.Name}} role)
                                  {
                                      _roles = new List<{{enumSymbol.Name}}AuthRole> { new(role) };
                                  }
                          
                                  public AuthorizeExt(params {{enumSymbol.Name}}[] roles)
                                  {
                                      _roles = roles.Select(r => new {{enumSymbol.Name}}AuthRole(r)).ToList();
                                  }
                                  
                                  public AuthorizeExt(IEnumerable<{{enumSymbol.Name}}> roles)
                                  {
                                      _roles = roles.Select(r => new {{enumSymbol.Name}}AuthRole(r)).ToList();
                                  }
                                  
                                  public AuthorizeExt(List<{{enumSymbol.Name}}> roles)
                                  {
                                      _roles = roles.Select(r => new {{enumSymbol.Name}}AuthRole(r)).ToList();
                                  }
                              }
                          }
                          """;

        context.AddSource($"{enumSymbol.Name}AuthorizeExt.g.cs", source);
        ExecuteExtension(enumSymbol, context);
    }

    private static void ExecuteExtension(INamedTypeSymbol enumSymbol, SourceProductionContext context)
    {
        string source = $$"""
                          using System;
                          using System.Reflection;
                          using Microsoft.Extensions.DependencyInjection;

                          #nullable enable
                          
                          namespace {{enumSymbol.ContainingNamespace}}
                          {
                              public static class {{enumSymbol.Name}}BlazingRouterExtensions
                              {
                                  public static IBlazingRouterBuilder<{{enumSymbol.Name}}> AddBlazingRouter(this IServiceCollection services, Assembly? assembly = null)
                                  {
                                      services.AddSingleton<RouteManager>();
                                      BlazingRouterBuilder<{{enumSymbol.Name}}> builder = new BlazingRouterBuilder<{{enumSymbol.Name}}>();
                                      RouteManager.InitRouteManager(assembly ?? Assembly.GetExecutingAssembly(), builder);
                                      return builder;
                                  }
                              }
                          }
                          """;

        context.AddSource($"{enumSymbol.Name}BlazingRouterExtensions.g.cs", source);
    }
}