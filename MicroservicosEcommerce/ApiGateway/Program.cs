using ApiGateway.Data;
using ApiGateway.Dominio.DTOs;
using ApiGateway.Dominio.Models;
using ApiGateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

#region builder
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 0))
    )
);

builder.Services.AddEndpointsApiExplorer();
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

    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});
#endregion

#region  HttpClients
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<JwtDelegatingHandler>();

builder.Services.AddHttpClient("estoque", c =>
{
    c.BaseAddress = new Uri("http://localhost:5001");
}).AddHttpMessageHandler<JwtDelegatingHandler>();

builder.Services.AddHttpClient("vendas", c =>
{
    c.BaseAddress = new Uri("http://localhost:5184");
}).AddHttpMessageHandler<JwtDelegatingHandler>();

#endregion

#region  JWT Configuration
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuerSigningKey = true
    };
});

builder.Services.AddAuthorization();
#endregion

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

#region Endpoint de login - gera o JWT
app.MapPost("/login", async (LoginRequest request, AppDbContext db) =>
{
    var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == request.Email);
    if (usuario == null)
        return Results.Unauthorized();

    // Verifica senha com BCrypt
    if (!BCrypt.Net.BCrypt.Verify(request.Senha, usuario.SenhaHash))
        return Results.Unauthorized();

    // Gera o token JWT
var token = JwtTokenService.GenerateToken(usuario, builder.Configuration);
    return Results.Ok(new { token });
})
.AllowAnonymous()
.WithTags("Auth")
.WithName("Login")
.WithOpenApi(op =>
{
    op.Summary = "Realizar login";
    op.Description = "Autentica o usuário e retorna um JWT válido para acessar os endpoints protegidos.";
    return op;
});
#endregion


#region Rotas - Gateway Vendas
app.MapGet("/gateway/vendas/pedidos", async (IHttpClientFactory factory, HttpContext ctx) =>
{
    var client = factory.CreateClient("vendas");
    var token = ctx.Request.Headers["Authorization"].ToString();

    var request = new HttpRequestMessage(HttpMethod.Get, "/pedidos");
    request.Headers.Add("Authorization", token);

    var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
})
.RequireAuthorization()
.WithTags("Gateway - Vendas")
.WithName("ListarPedidos")
.WithOpenApi(op =>
{
    op.Summary = "Listar todos os pedidos";
    op.Description = "Retorna a lista de todos os pedidos registrados no sistema de vendas.";
    return op;
});


app.MapGet("/gateway/vendas/pedidos/{id:int}", async (int id, IHttpClientFactory factory, HttpContext ctx) =>
{
    var client = factory.CreateClient("vendas");
    var token = ctx.Request.Headers["Authorization"].ToString();

    var request = new HttpRequestMessage(HttpMethod.Get, $"/pedidos/{id}");
    request.Headers.Add("Authorization", token);

    var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
})
.RequireAuthorization()
.WithTags("Gateway - Vendas")
.WithName("BuscarPedidoPorId")
.WithOpenApi(op =>
{
    op.Summary = "Buscar pedido por ID";
    op.Description = "Recupera os detalhes de um pedido específico com base no ID fornecido.";
    return op;
});


app.MapPost("/gateway/vendas/pedidos", async (PedidoDTO pedido, IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("vendas");
    var response = await httpClient.PostAsJsonAsync("/pedidos", pedido);

    return Results.StatusCode((int)response.StatusCode);
})
.RequireAuthorization()
.WithTags("Gateway - Vendas")
.WithName("CriarPedido")
.WithOpenApi(op =>
{
    op.Summary = "Fazer um novo pedido";
    op.Description = "Cria um novo pedido no sistema de vendas com os dados fornecidos.";
    return op;
});
#endregion

#region Rotas - Gateway Estoque
app.MapGet("/gateway/estoque/produtos", async (IHttpClientFactory factory, HttpContext ctx) =>
{
    var client = factory.CreateClient("estoque");
    var token = ctx.Request.Headers["Authorization"].ToString();

    var request = new HttpRequestMessage(HttpMethod.Get, "/produtos");
    request.Headers.Add("Authorization", token);

    var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    return Results.Content(content, "application/json");
})
.RequireAuthorization()
.WithTags("Gateway - Estoque")
.WithName("ListarProdutos")
.WithOpenApi(op =>
{
    op.Summary = "Listar todos os produtos";
    op.Description = "Retorna a lista completa de produtos disponíveis no estoque.";
    return op;
});


app.MapGet("/gateway/estoque/produtos/{id:int}", async (int id, IHttpClientFactory factory, HttpContext ctx) =>
{
    var client = factory.CreateClient("estoque");
    var token = ctx.Request.Headers["Authorization"].ToString();

    var request = new HttpRequestMessage(HttpMethod.Get, $"/produtos/{id}");
    request.Headers.Add("Authorization", token);

    var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    return Results.Content(content, "application/json");
})
.RequireAuthorization()
.WithTags("Gateway - Estoque")
.WithName("BuscarProdutoPorId")
.WithOpenApi(op =>
{
    op.Summary = "Buscar produto por ID";
    op.Description = "Recupera os dados de um produto específico com base no ID informado.";
    return op;
});


app.MapPost("/gateway/estoque/produtos", async (ProdutoDTO produto, IHttpClientFactory factory, HttpContext ctx) =>
{
    var client = factory.CreateClient("estoque");

    var request = new HttpRequestMessage(HttpMethod.Post, "/produtos")
    {
        Content = JsonContent.Create(produto)
    };

    // Passa o token recebido pelo Gateway
    var token = ctx.Request.Headers["Authorization"].ToString();
    request.Headers.Add("Authorization", token);

    var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    return Results.Json(content, statusCode: (int)response.StatusCode);
})
.RequireAuthorization()
.WithTags("Gateway - Estoque")
.WithName("CriarProduto")
.WithOpenApi(op =>
{
    op.Summary = "Criar novo produto";
    op.Description = "Adiciona um novo produto ao estoque com base nas informações fornecidas.";
    return op;
});


app.MapPut("/gateway/estoque/produtos/{id:int}", async (int id, ProdutoDTO produto, IHttpClientFactory factory, HttpContext ctx) =>
{
    var client = factory.CreateClient("estoque");

    var request = new HttpRequestMessage(HttpMethod.Put, $"/produtos/{id}")
    {
        Content = JsonContent.Create(produto)
    };

    var token = ctx.Request.Headers["Authorization"].ToString();
    request.Headers.Add("Authorization", token);

    var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    return Results.Json(content, statusCode: (int)response.StatusCode);
})
.RequireAuthorization()
.WithTags("Gateway - Estoque")
.WithName("AtualizarProduto")
.WithOpenApi(op =>
{
    op.Summary = "Atualizar produto";
    op.Description = "Atualiza os dados de um produto existente no estoque com base no ID.";
    return op;
});


app.MapDelete("/gateway/estoque/produtos/{id:int}", async (int id, IHttpClientFactory factory, HttpContext ctx) =>
{
    var client = factory.CreateClient("estoque");
    var token = ctx.Request.Headers["Authorization"].ToString();

    var request = new HttpRequestMessage(HttpMethod.Delete, $"/produtos/{id}");
    request.Headers.Add("Authorization", token);

    var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    return Results.Content(content, "application/json");
})
.RequireAuthorization()
.WithTags("Gateway - Estoque")
.WithName("ExcluirProduto")
.WithOpenApi(op =>
{
    op.Summary = "Excluir produto";
    op.Description = "Remove um produto do estoque com base no ID fornecido.";
    return op;
});
#endregion



app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();
app.Run();

