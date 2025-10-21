using MicroservicoEstoque.Infraestrutura.Db;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
namespace MicroservicoEstoque.Dominio.Servicos;
public class EstoqueConsumer
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EstoqueConsumer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void Start()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        var connection = factory.CreateConnection("microservico-estoque");
        var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: "fila_vendas",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var mensagem = Encoding.UTF8.GetString(body);
            Console.WriteLine($"[✔] Mensagem recebida: {mensagem}");

            var partes = mensagem.Split(',');
            if (!int.TryParse(partes[0].Split(':')[1], out int produtoId)) return;
            if (!int.TryParse(partes[1].Split(':')[1], out int quantidadeVendida)) return;

            // ✅ Cria escopo válido aqui
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EstoqueContext>();

            var produto = await db.Produtos.FindAsync(produtoId);
            if (produto != null)
            {
                produto.Quantidade -= quantidadeVendida;
                await db.SaveChangesAsync();
                Console.WriteLine($"[✔] Estoque atualizado: Produto {produtoId}, Nova quantidade: {produto.Quantidade}");
            }
        };

        channel.BasicConsume(queue: "fila_vendas", autoAck: true, consumer: consumer);
    }
}
