using ApiGateway.Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios => Set<Usuario>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    var admin = new Usuario
    {
        Id = 1, // Fixo
        Nome = "Administrador",
        Email = "admin@admin.com",
        SenhaHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
        Role = "Admin"
    };

    modelBuilder.Entity<Usuario>().HasData(admin);
}

    }
}
