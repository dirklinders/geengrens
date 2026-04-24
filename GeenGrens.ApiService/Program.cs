

using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddProblemDetails();

var openAiKey = builder.Configuration["OpenAIKey"];
builder.Services.AddSingleton(new ChatClient(model: "gpt-5.4", openAiKey));
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

#if DEBUG

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins("https://localhost:3000","http://localhost:3000") // Blazor FE dev URL
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // if using cookies/auth
        });
});

#endif

builder.Services.AddAutoMapper(opts => opts.AddProfile(typeof(GeenGrensProfile)));

builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";

        options.Cookie.Name = "AuthCookie";
        options.Cookie.HttpOnly = true;
        options.SlidingExpiration = true;

#if DEBUG
        // Cross-site in dev (FE :3000 → BE :7424)
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
#else
    // Same-domain in production (via reverse proxy)
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
#endif
    }).AddGoogle(opts =>
    {
        opts.AccessDeniedPath = "/access-denied";
        opts.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        opts.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        opts.CallbackPath = "/api/signin-google";
    });

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
        opts.HeadContent += @"
        <script>
        function addGoogleLoginButton() {
            // Try to find the topbar
            const topbar = document.querySelector('.swagger-ui .topbar')
                        || document.querySelector('.swagger-container .swagger-ui .topbar');
            if (!topbar) return false;

            // Create the button
            const loginButton = document.createElement('button');
            loginButton.innerText = 'Login with Google';
            loginButton.style.margin = '10px';
            loginButton.style.padding = '6px 12px';
            loginButton.style.backgroundColor = '#4285F4';
            loginButton.style.color = '#fff';
            loginButton.style.border = 'none';
            loginButton.style.borderRadius = '4px';
            loginButton.style.cursor = 'pointer';

            loginButton.onclick = function () {
                window.location.href = '/api/externalauth/logingoogle?returnUrl=/swagger';
            };

            topbar.appendChild(loginButton);
            return true;
        }

        // Poll until Swagger UI has rendered
        const interval = setInterval(function () {
            if (addGoogleLoginButton()) clearInterval(interval);
        }, 100); // try every 100ms
        </script>
    ";
    });
}

#if DEBUG

app.UseCors();


#else
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto,
    KnownProxies = { IPAddress.Parse("127.0.0.1") }
});

#endif


app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "API service is running.");

app.MapControllers();

app.Run();

