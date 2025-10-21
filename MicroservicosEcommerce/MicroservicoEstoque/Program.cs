using Microsoft.EntityFrameworkCore;
using MicroservicoEstoque.Infraestrutura.Db;
using MicroservicoEstoque.Dominio.Entidades;
using MicroservicoEstoque.Infraestrutura;
using MicroservicoEstoque.Dominio.Servicos;

#region Builder
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

#region Configuração do banco
builder.Services.AddDbContext<EstoqueContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
#endregion

#region Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
#endregion

builder.Services.AddHttpClient();

var app = builder.Build();
#endregion

// Criar banco e seed inicial
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EstoqueContext>();
    db.Database.EnsureCreated();
    DbInitializer.Seed(db);

    // Iniciar consumer
    var consumer = new EstoqueConsumer(app.Services.GetRequiredService<IServiceScopeFactory>());
    consumer.Start();
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

#region Produtos

app.MapGet("/produtos", async (EstoqueContext db) =>
    await db.Produtos.ToListAsync());

app.MapGet("/produtos/{id:int}", async (int id, EstoqueContext db) =>
    await db.Produtos.FindAsync(id) is Produto p ? Results.Ok(p) : Results.NotFound());

app.MapPost("/produtos", async (Produto produto, EstoqueContext db) =>
{
    db.Produtos.Add(produto);
    await db.SaveChangesAsync();
    return Results.Created($"/api/produtos/{produto.Id}", produto);
});

app.MapPut("/produtos/{id:int}", async (int id, Produto input, EstoqueContext db) =>
{
    var produto = await db.Produtos.FindAsync(id);
    if (produto is null) return Results.NotFound();

    produto.Nome = input.Nome;
    produto.Descricao = input.Descricao;
    produto.Preco = input.Preco;
    produto.Quantidade = input.Quantidade;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/produtos/{id}", async (int id, EstoqueContext db) =>
{
    var produto = await db.Produtos.FindAsync(id);
    if (produto is null) return Results.NotFound();

    db.Produtos.Remove(produto);
    await db.SaveChangesAsync();
    return Results.NoContent();
});
#endregion

app.Run();
