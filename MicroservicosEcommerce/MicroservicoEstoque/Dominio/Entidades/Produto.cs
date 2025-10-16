using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MicroservicoEstoque.Dominio.Entidades
{
   public class Produto
{
   [Key]
        public int Id { get; set; }

       [Required]
[Column(TypeName = "varchar(255)")]
public string Nome { get; set; } = string.Empty;

[Required]
[Column(TypeName = "longtext")]
public string Descricao { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Preco { get; set; }

        [Required]
        public int Quantidade { get; set; }
}

}