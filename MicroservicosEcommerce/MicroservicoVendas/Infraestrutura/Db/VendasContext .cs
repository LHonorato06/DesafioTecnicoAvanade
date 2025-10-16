using MicroservicoVendas.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MicroservicoVendas.Infraestrutura.Db;

    public class VendasContext : DbContext
    {
        public VendasContext(DbContextOptions<VendasContext> options) : base(options) { }

        public DbSet<Pedido> Pedidos => Set<Pedido>();
        public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pedido>().ToTable("Pedidos");
            modelBuilder.Entity<ItemPedido>().ToTable("ItensPedido");
        }
    }
