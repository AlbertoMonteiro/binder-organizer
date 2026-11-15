using BinderPokedex.Components;
using BinderPokedex.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(_ => new PokedexService());
builder.Services.AddSingleton<BrowserStorage>();
builder.Services.AddSingleton<GistSettings>();
builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri("https://api.github.com/") });
builder.Services.AddSingleton<ICollectionRemote, GistCollectionRemote>();
builder.Services.AddSingleton<ICollectionCache, BrowserCollectionCache>();
builder.Services.AddSingleton<CollectionService>();
builder.Services.AddSingleton<ReadingPosition>();

var host = builder.Build();

// Mostra na hora a cópia guardada no aparelho; a leitura do gist acontece em segundo plano.
await host.Services.GetRequiredService<CollectionService>().InitializeAsync();

await host.RunAsync();
