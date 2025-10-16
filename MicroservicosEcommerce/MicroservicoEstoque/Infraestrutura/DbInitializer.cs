using MicroservicoEstoque.Dominio.Entidades;
using MicroservicoEstoque.Infraestrutura.Db;

namespace MicroservicoEstoque.Infraestrutura;

    public static class DbInitializer
    {
        public static void Seed(EstoqueContext context)
        {
            if (!context.Produtos.Any())
            {
                context.Produtos.AddRange(
                    new Produto { Nome = "Mouse Gamer", Descricao = "Mouse RGB", Preco = 150, Quantidade = 30 },
                    new Produto { Nome = "Teclado Mecânico", Descricao = "Switch azul", Preco = 250, Quantidade = 15 }
                );
                context.SaveChanges();
            }
        }
    }
