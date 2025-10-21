using ApiGateway.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

#region builder
var builder = WebApplication.CreateBuilder(args);

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

#region  HttpClients (deve ser antes do app.Build)
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

#region  Endpoint de login - gera o JWT
app.MapPost("/login", (LoginRequest req, IConfiguration cfg) =>
{
    if (req.Username != "admin" || req.Password != "admin123")
        return Results.Unauthorized();

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var expires = DateTime.UtcNow.AddMinutes(double.Parse(cfg["Jwt:ExpiryMinutes"] ?? "60"));

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, req.Username),
        new Claim(ClaimTypes.Role, "Admin"),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var token = new JwtSecurityToken(
        issuer: cfg["Jwt:Issuer"],
        audience: cfg["Jwt:Audience"],
        claims: claims,
        expires: expires,
        signingCredentials: creds
    );

    var jwt = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new LoginResponse(jwt, expires));
})
.AllowAnonymous()
.WithTags("Auth");
#endregion


#region Vendas (via Gateway)

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
.WithTags("Gateway - Vendas");


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
.WithTags("Gateway - Vendas");


app.MapPost("/gateway/vendas/pedidos", async (PedidoDTO pedido, IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("vendas");
    var response = await httpClient.PostAsJsonAsync("/pedidos", pedido);

    return Results.StatusCode((int)response.StatusCode);
}).RequireAuthorization()
.WithTags("Gateway - Vendas");

#endregion

#region Rotas - Gateway Estoque

// ✅ Listar todos os produtos
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
.WithTags("Gateway - Estoque");

// ✅ Buscar produto por ID
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
.WithTags("Gateway - Estoque");

// ✅ Criar novo produto
app.MapPost("/gateway/estoque/produtos", async (HttpContext ctx, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("estoque");
    var token = ctx.Request.Headers["Authorization"].ToString();

    // Lê o corpo da requisição original
    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var request = new HttpRequestMessage(HttpMethod.Post, "/produtos")
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Authorization", token);

    var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    return Results.Content(content, "application/json");
})
.RequireAuthorization()
.WithTags("Gateway - Estoque");

// ✅ Atualizar produto
app.MapPut("/gateway/estoque/produtos/{id:int}", async (int id, HttpContext ctx, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("estoque");
    var token = ctx.Request.Headers["Authorization"].ToString();

    var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var request = new HttpRequestMessage(HttpMethod.Put, $"/produtos/{id}")
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Authorization", token);

    var response = await client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    return Results.Content(content, "application/json");
})
.RequireAuthorization()
.WithTags("Gateway - Estoque");

// ✅ Excluir produto
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
.WithTags("Gateway - Estoque");

#endregion



app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

app.Run();

#region DTOs locais
record LoginRequest(string Username, string Password);
record LoginResponse(string Token, DateTime Expires);
#endregion
