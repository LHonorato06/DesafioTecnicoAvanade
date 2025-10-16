using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MicroservicoVendas.Dominio.Entidades
{
    public class ItemPedido
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
}
}