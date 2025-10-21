using Microsoft.EntityFrameworkCore;
using MicroservicoVendas.Infraestrutura.Db;
using MicroservicoVendas.Dominio.Entidades;
using MicroservicoVendas.Dominio.Servicos;
using MicroservicoVendas.Dominio.DTOs;


#region Builder
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

#region  Configuração do banco
builder.Services.AddDbContext<VendasContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
#endregion
#region Swagger

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
#endregion

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<JwtDelegatingHandler>();

builder.Services.AddHttpClient("estoque", c =>
{
    c.BaseAddress = new Uri("http://localhost:5001");
}).AddHttpMessageHandler<JwtDelegatingHandler>();

var app = builder.Build();
#endregion



// Criar banco se não existir
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VendasContext>();
    db.Database.EnsureCreated();
}

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


#region Home
app.MapGet("/", () => Results.Redirect("/swagger"))
   .AllowAnonymous()
   .WithTags("Home");
#endregion


#region Pedidos 

app.MapGet("/pedidos", async (VendasContext db) =>
    await db.Pedidos.Include(p => p.Itens).ToListAsync());

app.MapGet("/pedidos/{id:int}", async (int id, VendasContext db) =>
    await db.Pedidos.Include(p => p.Itens)
        .FirstOrDefaultAsync(p => p.Id == id) is Pedido pedido
        ? Results.Ok(pedido)
        : Results.NotFound());

app.MapPost("/pedidos", async (Pedido pedido, VendasContext db, IHttpClientFactory factory) =>
{
    var httpClient = factory.CreateClient("estoque");

    foreach (var item in pedido.Itens)
    {
        var response = await httpClient.GetAsync($"/produtos/{item.ProdutoId}");
        if (!response.IsSuccessStatusCode)
            return Results.BadRequest($"Produto {item.ProdutoId} não encontrado ou serviço indisponível.");

        var produto = await response.Content.ReadFromJsonAsync<ProdutoDTO>();
        if (produto!.Quantidade < item.Quantidade)
            return Results.BadRequest($"Produto {produto.Nome} sem estoque suficiente.");
    }

    db.Pedidos.Add(pedido);
    await db.SaveChangesAsync();

    // Notifica Estoque via RabbitMQ
    var publisher = new VendasPublisher();
    foreach(var item in pedido.Itens)
    {
        publisher.NotificarVenda($"ProdutoId:{item.ProdutoId},Quantidade:{item.Quantidade}");
    }

    return Results.Created($"/pedidos/{pedido.Id}", pedido);
});


#endregion

app.Run();
