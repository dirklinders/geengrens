


using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

var openAiKey = builder.Configuration["OpenAIKey"];
builder.Services.AddSingleton(new ChatClient(model: "gpt-4o-mini", openAiKey));
builder.Services.AddScoped<ChatFEManager>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddGeneratedCrudServices();

builder.Services.AddControllers();

builder.Services.AddDbContext<GeenGrensContext>(opts => opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<UserModel, IdentityRole>()
    .AddEntityFrameworkStores<GeenGrensContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAutoMapper(opts => opts.AddProfile(typeof(GeenGrensProfile)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GeenGrensContext>();
    SqlScriptGenerator.Generate(); // generate SQL scripts for all entities with [GenerateCrud]
    db.RunMigrations(); // call your migration method
}
// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(opts =>
    {
        opts.SwaggerEndpoint("/openapi/v1.json", "API V1");
        opts.EnableTryItOutByDefault();
    });
}


app.MapGet("/", () => "API service is running.");


app.MapControllers();

app.MapDefaultEndpoints();

app.Run();

