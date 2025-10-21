namespace ApiGateway.Dominio.DTOs;

public class PedidoDTO
{
    public int Id { get; set; }
    public string Cliente { get; set; } = default!;
    public DateTime DataPedido { get; set; } = DateTime.UtcNow;
    public List<ItemPedidoDTO> Itens { get; set; } = new();
}