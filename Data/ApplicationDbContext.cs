using Microsoft.EntityFrameworkCore;
using SistemaDeTienda.Models.Entities;

namespace SistemaDeTienda.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Entidades del POS de Tienda de Ropa
    public DbSet<CategoriaProducto> CategoriasProducto { get; set; }
    public DbSet<Producto> Productos { get; set; }
    public DbSet<ProductoVariante> ProductoVariantes { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Venta> Ventas { get; set; }
    public DbSet<VentaDetalle> DetalleVentas { get; set; }
    public DbSet<Pago> Pagos { get; set; }
    public DbSet<PagoVenta> PagoVentas { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Configuracion> Configuraciones { get; set; }
    public DbSet<CierreCaja> CierresCaja { get; set; }
    public DbSet<MovimientoInventario> MovimientosInventario { get; set; }
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<PlantillaMensajeWhatsApp> PlantillasMensajeWhatsApp { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de CategoriaProducto
        modelBuilder.Entity<CategoriaProducto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        // Configuración de Producto
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Codigo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Precio).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PrecioCompra).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.Codigo).IsUnique();
            
            entity.HasOne(e => e.CategoriaProducto)
                .WithMany(c => c.Productos)
                .HasForeignKey(e => e.CategoriaProductoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Proveedor)
                .WithMany()
                .HasForeignKey(e => e.ProveedorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de ProductoVariante
        modelBuilder.Entity<ProductoVariante>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Talla).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(50); // Removido IsRequired()
            entity.Property(e => e.SKU).HasMaxLength(100);
            entity.Property(e => e.PrecioAdicional).HasColumnType("decimal(18,2)");
            
            entity.HasOne(e => e.Producto)
                .WithMany(p => p.Variantes)
                .HasForeignKey(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de Venta
        modelBuilder.Entity<Venta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Numero).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Monto).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Descuento).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Impuesto).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.Numero).IsUnique();
            
            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Ventas) 
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Usuario)
                .WithMany()
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de VentaDetalle
        modelBuilder.Entity<VentaDetalle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Total).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Descuento).HasColumnType("decimal(18,2)");
            
            entity.HasOne(e => e.Venta)
                .WithMany(v => v.DetalleVentas)
                .HasForeignKey(e => e.VentaId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Producto)
                .WithMany()
                .HasForeignKey(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ProductoVariante)
                .WithMany()
                .HasForeignKey(e => e.ProductoVarianteId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuración de Pago
        modelBuilder.Entity<Pago>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Monto).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoRecibido).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Vuelto).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DescuentoMonto).HasColumnType("decimal(18,2)");
        });

        // Configuración de Usuario
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NombreUsuario).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.NombreUsuario).IsUnique();
        });

        // Configuración de MovimientoInventario
        modelBuilder.Entity<MovimientoInventario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CostoUnitario).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CostoTotal).HasColumnType("decimal(18,2)");
            
            entity.HasOne(e => e.Producto)
                .WithMany()
                .HasForeignKey(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
