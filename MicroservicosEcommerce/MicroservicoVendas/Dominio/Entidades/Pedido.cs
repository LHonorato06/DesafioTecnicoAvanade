namespace MicroservicoVendas.Dominio.Entidades
{
    public class Pedido
{
    public int Id { get; set; }
    public string Cliente { get; set; } = default!;
    public DateTime Data { get; set; } = DateTime.Now;
    public List<ItemPedido> Itens { get; set; } = new();
}
}