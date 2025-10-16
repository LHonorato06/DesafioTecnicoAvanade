using Microsoft.EntityFrameworkCore;
using MicroservicoEstoque.Dominio.Entidades;

namespace MicroservicoEstoque.Infraestrutura.Db;

    public class EstoqueContext : DbContext
    {
        public EstoqueContext(DbContextOptions<EstoqueContext> options) : base(options) { }

        public DbSet<Produto> Produtos => Set<Produto>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Produto>().ToTable("Produtos");
        }
    }

