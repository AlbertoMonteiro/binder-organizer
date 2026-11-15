using BinderPokedex.Components;
using BinderPokedex.Persistence;
using BinderPokedex.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;

// Como serviço do Windows o diretório atual é System32, então a raiz passa a ser a pasta do executável.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});

builder.Services.AddWindowsService(o => o.ServiceName = "BinderPokedex");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration["Binder:DatabasePath"] is { Length: > 0 } configured
    ? configured
    : Path.Combine(builder.Environment.ContentRootPath, "binder.db");
builder.Services.AddDbContextFactory<BinderDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton(_ => new PokedexService());
builder.Services.AddSingleton<CollectionService>();
builder.Services.AddScoped<ReadingPosition>();

var app = builder.Build();

await app.Services.GetRequiredService<CollectionService>().InitializeAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
