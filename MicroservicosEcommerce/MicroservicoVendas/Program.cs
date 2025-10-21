using Microsoft.EntityFrameworkCore;
using MicroservicoVendas.Infraestrutura.Db;
using MicroservicoVendas.Dominio.Entidades;
using MicroservicoVendas.Dominio.Servicos;
using MicroservicoVendas.Dominio.DTOs;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

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


#region Configurar validação JWT
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // em dev; true em prod
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

// ADICIONE ISSO
builder.Services.AddAuthorization();
#endregion


#region Configurar Swagger para testar tokens
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Use: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement{
        {
            new OpenApiSecurityScheme{ Reference = new OpenApiReference{ Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            new string[]{}
        }
    });
});
#endregion
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


// 💳 Rotas Minimal API
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
