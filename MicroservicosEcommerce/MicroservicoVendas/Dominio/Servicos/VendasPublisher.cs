using RabbitMQ.Client;
using System.Text;
namespace MicroservicoVendas.Dominio.Servicos;

public class VendasPublisher
{
    public void NotificarVenda(string mensagem)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };

       using var connection = factory.CreateConnection("microservico-vendas");

        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: "fila_vendas",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var body = Encoding.UTF8.GetBytes(mensagem);

        channel.BasicPublish(
            exchange: "",
            routingKey: "fila_vendas",
            basicProperties: null,
            body: body
        );

        Console.WriteLine($" Mensagem enviada: {mensagem}");
    }
}
