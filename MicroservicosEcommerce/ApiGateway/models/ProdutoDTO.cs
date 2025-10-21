namespace ApiGateway.Models;
   public class ProdutoDTO
    {
        public int Id { get; set; }
        public string Nome { get; set; } = default!;
        public string Descricao { get; set; } = default!;
        public decimal Preco { get; set; }
        public int Quantidade { get; set; }
    }
