using Microsoft.EntityFrameworkCore;
using MicroservicoVendas.Infraestrutura.Db;
using MicroservicoVendas.Dominio.Entidades;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 🗃️ Configuração do banco
builder.Services.AddDbContext<VendasContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

// 💳 Rotas Minimal API
app.MapGet("/pedidos", async (VendasContext db) =>
    await db.Pedidos.Include(p => p.Itens).ToListAsync());

app.MapGet("/pedidos/{id:int}", async (int id, VendasContext db) =>
    await db.Pedidos.Include(p => p.Itens)
        .FirstOrDefaultAsync(p => p.Id == id) is Pedido pedido
        ? Results.Ok(pedido)
        : Results.NotFound());

app.MapPost("/pedidos", async (Pedido pedido, VendasContext db) =>
{
    db.Pedidos.Add(pedido);
    await db.SaveChangesAsync();
    // 🔜 futuramente enviar mensagem via RabbitMQ
    return Results.Created($"/api/pedidos/{pedido.Id}", pedido);
});

app.Run();
