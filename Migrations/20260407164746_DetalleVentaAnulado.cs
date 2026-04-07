using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaDeTienda.Migrations
{
    /// <inheritdoc />
    public partial class DetalleVentaAnulado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Anulado",
                table: "DetalleVentas",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Anulado",
                table: "DetalleVentas");
        }
    }
}
