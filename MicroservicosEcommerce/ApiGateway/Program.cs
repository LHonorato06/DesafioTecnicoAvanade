using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

// Adiciona o YARP e carrega configuração do appsettings.json
builder.Services.AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Swagger ou outras middlewares opcionais
app.UseHttpsRedirection();

// Mapeia o proxy
app.MapReverseProxy();

app.Run();
