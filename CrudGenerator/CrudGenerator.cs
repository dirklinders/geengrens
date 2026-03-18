namespace GeenGrens.CrudGenerator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

[Generator]
public class CrudGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {

        //Debugger.Launch(); // prompts to attach debugger

        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "GeenGrens.ApiService.GenerateCrudAttribute",
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) =>
                {
                    var symbol = (INamedTypeSymbol)ctx.TargetSymbol;

                    if (symbol.ContainingNamespace.ToDisplayString() != "GeenGrens.ApiService.Models")
                        return null;

                    var attribute = ctx.Attributes[0];

                    bool admin = false;


                    if (attribute.ConstructorArguments.Length > 0)
                    {
                        admin = (bool)attribute.ConstructorArguments[0].Value!;
                    }

                    return new ModelInfo(symbol.Name, admin, symbol);
                })
            .Where(m => m != null);

        var collected = models.Collect();

        context.RegisterSourceOutput(collected, (spc, list) =>
        {
            var managers = new List<string>();

            foreach (var model in list!)
            {
                var manager = Generate(spc, model!);
                managers.Add(manager);
            }

            GenerateDI(spc, managers);
            GenerateDbSets(spc, list);
            GenerateAutomapper(spc, list);
        });
    }
   

    static ModelInfo? GetClass(GeneratorSyntaxContext context)
    {
        var classSyntax = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classSyntax);        

        if (symbol == null)
            return null;

        if (symbol.ContainingNamespace.ToDisplayString() != "GeenGrens.ApiService.Models")
            return null;

        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == "GenerateCrudAttribute")
            {
                bool admin = false;

                if (attr.NamedArguments.Length > 0)
                {
                    foreach (var arg in attr.NamedArguments)
                    {
                        if (arg.Key == "Admin")
                            admin = (bool)arg.Value.Value!;
                    }
                }

                return new ModelInfo(symbol.Name, admin, null);
            }
        }

        return null;
    }

    private static bool  IsSimpleType(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Boolean:
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Decimal:
            case SpecialType.System_Double:
            case SpecialType.System_Single:
            case SpecialType.System_DateTime:
                return true;
            default:
                return false;
        }
    }

    private static string GetTypeName(ITypeSymbol type)
    {
        return type.ToString();

        var typeName = type.Name;

        // Nullable handling
        if (type.NullableAnnotation == NullableAnnotation.Annotated && type.IsValueType)
            return typeName + "?";

        return typeName;
    }

    static string Generate(SourceProductionContext context, ModelInfo model)
    {
        var className = model.Name;
        var entityName = className.Replace("Model", "");
        var plural = entityName + "s";

        var managerName = $"{entityName}Manager";
        var controllerName = $"{entityName}Controller";

        var scalarProps = model.Symbol.GetMembers()
    .OfType<IPropertySymbol>()
    .Where(p => IsSimpleType(p.Type))
    .ToArray();

        var dtoName = model.Name.Replace("Model", "DTO");

        var sb = new StringBuilder();
        sb.AppendLine($"namespace GeenGrens.ApiService.Generated;");
        sb.AppendLine();
        sb.AppendLine($"public class {dtoName}");
        sb.AppendLine("{");

        foreach (var prop in scalarProps)
        {
            var typeName = GetTypeName(prop.Type);
            sb.AppendLine($"    public {typeName} {prop.Name} {{ get; set; }}");
        }

        sb.AppendLine("}");
        var dtoText =  sb.ToString();


        var authorize = model.Admin ? "[Authorize(Roles=\"admin\")]" : "";

        var source = dtoText + $$"""



public class {{managerName}}
{
    private readonly GeenGrensContext _context;
    private readonly IMapper _mapper;

    public {{managerName}}(GeenGrensContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<List<{{className}}>> GetAll()
    {
        return await _context.{{plural}}.ToListAsync();
    }

    public async Task<{{className}}?> Get(int id)
    {
        return await _context.{{plural}}.FindAsync(id);
    }

    public async Task<{{className}}> Create({{dtoName}} dto)
    {
        var model = _mapper.Map<{{className}}>(dto);
        _context.{{plural}}.Add(model);
        await _context.SaveChangesAsync();
        return model;
    }

    public async Task Update({{dtoName}} entity)
    {
        var model = _mapper.Map<{{className}}>(entity);
        _context.{{plural}}.Update(model);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id)
    {
        var entity = await _context.{{plural}}.FindAsync(id);
        if (entity != null)
        {
            _context.{{plural}}.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}

[ApiController]
[Route("api/[controller]")]
{{authorize}}
public class {{controllerName}} : ControllerBase
{
    private readonly {{managerName}} _manager;

    public {{controllerName}}({{managerName}} manager)
    {
        _manager = manager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _manager.GetAll());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await _manager.Get(id);
        if (item == null)
            return NotFound();

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create({{dtoName}} entity)
        => Ok(await _manager.Create(entity));

    [HttpPut]
    public async Task<IActionResult> Update({{dtoName}} entity)
    {
        await _manager.Update(entity);
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _manager.Delete(id);
        return Ok();
    }
}
""";

        context.AddSource($"{entityName}Crud.g.cs", source);
        return managerName;
    }

    static void GenerateDI(SourceProductionContext context, List<string> managers)
    {
        var registrations = string.Join("\n",
            managers.Select(m => $"services.AddScoped<{m}>();"));

        var source = $$"""


namespace GeenGrens.ApiService.Generated;

public static class GeneratedCrudServiceExtensions
{
    public static IServiceCollection AddGeneratedCrudServices(this IServiceCollection services)
    {
{{registrations}}

        return services;
    }
}
""";

        context.AddSource("CrudServiceRegistration.g.cs", source);
    }

    static void GenerateAutomapper(SourceProductionContext context, ImmutableArray<ModelInfo> models)
    {
        var registrations = string.Join("\n",
            models.Select(m => $"CreateMap<{m.Name.Replace("Model","DTO")},{m.Name}>();"));

        var source = $$"""

namespace GeenGrens.ApiService.Context;

public class GeenGrensProfile : Profile
{
    public GeenGrensProfile()
    {
        {{registrations}}
    }

    
}
""";



        context.AddSource("GeenGrensProfile.g.cs", source);
    }

    static void GenerateDbSets(SourceProductionContext context, ImmutableArray<ModelInfo> models)
    {
        if (models.IsDefaultOrEmpty)
            return;

        var dbsets = new StringBuilder();
        var relations = new StringBuilder();

        foreach (var model in models)
        {
            var entity = model.Name.Replace("Model", "");
            var plural = entity + "s";

            dbsets.AppendLine($"    public DbSet<{model.Name}> {plural} {{ get; set; }} = null!;");
            relations.AppendLine($"    modelBuilder.Entity<{model.Name}>().HasKey(m => m.Id);");
        }

        foreach (var model in models)
        {
            var props = model.Symbol.GetMembers()
                .OfType<IPropertySymbol>();

            foreach (var prop in props)
            {
                // FOREIGN KEY DETECTION
                if (prop.Name.EndsWith("Id"))
                {
                    var entity = prop.Name.Replace("Id",string.Empty); 
                    var target = entity + "Model";

                    if (models.Any(m => m.Name == target))
                    {
                        
                        relations.AppendLine($$"""
        modelBuilder.Entity<{{model.Name}}>()
            .HasOne(e => e.{{entity}})
            .WithMany(t => t.{{model.Name.Replace("Model","s")}})
            .HasForeignKey(e => e.{{prop.Name}});
""");
                    }
                }

                // COLLECTION DETECTION
                if (prop.Type is INamedTypeSymbol named &&
                    named.IsGenericType &&
                    named.Name == "List")
                {
                    var arg = named.TypeArguments[0];

                    if (arg.Name.EndsWith("Model"))
                    {
                        var target = arg.Name;

                        var targetModel = models.FirstOrDefault(m => m.Name == target);

                        if (targetModel.Symbol != null)
                        {
                            var inverse = targetModel.Symbol
                                .GetMembers()
                                .OfType<IPropertySymbol>()
                                .FirstOrDefault(p =>
                                    p.Type is INamedTypeSymbol n &&
                                    n.IsGenericType &&
                                    n.Name == "List" &&
                                    n.TypeArguments[0].Name == model.Name);

                            if (inverse != null)
                            {
                                var left = model.Name.Replace("Model", "");
                                var right = target.Replace("Model", "");

                                var pair = new[] { left, right }
                                    .OrderBy(x => x)
                                    .ToArray();

                                var table = pair[0] + pair[1];

                                relations.AppendLine($$"""
        modelBuilder.Entity<{{model.Name}}>()
            .HasMany(e => e.{{prop.Name}})
            .WithMany(e => e.{{inverse.Name}})
            .UsingEntity(j => j.ToTable("{{table}}"));
""");
                            }
                        }
                    }
                }
            }
        }

        var source = $$"""

namespace GeenGrens.ApiService.Context;

public partial class GeenGrensContext
{
{{dbsets}}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

{{relations}}
    }
}
""";

        context.AddSource("GeenGrensContext.DbSets.g.cs", source);
    }


    //    static void GenerateDbSets(SourceProductionContext context, ImmutableArray<ModelInfo> models)
    //    {
    //        if (models.IsDefaultOrEmpty)
    //            return;

    //        var sb = new StringBuilder();

    //        foreach (var model in models.OrderBy(m => m.Name))
    //        {
    //            var className = model.Name;
    //            var entityName = className.Replace("Model", "");
    //            var plural = entityName + "s";

    //            sb.AppendLine($"    public DbSet<{className}> {plural} {{ get; set; }} = null!;");
    //        }

    //        var source = $$"""
    //using Microsoft.EntityFrameworkCore;
    //using GeenGrens.ApiService.Models;
    //namespace GeenGrens.ApiService.Context;

    //public partial class GeenGrensContext
    //{
    //{{sb}}
    //}
    //""";

    //        context.AddSource("GeenGrensContext.DbSets.g.cs", source);
    //    }

    class ModelInfo
    {
        public string Name { get; }
        public bool Admin { get; }
        public INamedTypeSymbol Symbol { get; }

        public ModelInfo(string name, bool admin, INamedTypeSymbol symbol)
        {
            Name = name;
            Admin = admin;
            Symbol = symbol;
        }
    }
}