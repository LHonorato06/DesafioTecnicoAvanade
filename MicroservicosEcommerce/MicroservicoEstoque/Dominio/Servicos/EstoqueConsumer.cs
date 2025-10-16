using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
namespace MicroservicoEstoque.Dominio.Servicos;
public class EstoqueConsumer
{
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
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var mensagem = Encoding.UTF8.GetString(body);
            Console.WriteLine($"[✔] Mensagem recebida: {mensagem}");

            // Aqui você pode chamar o serviço de atualização de estoque
        };

        channel.BasicConsume(queue: "fila_vendas", autoAck: true, consumer: consumer);

        Console.WriteLine("👂 Aguardando mensagens...");
    }
}