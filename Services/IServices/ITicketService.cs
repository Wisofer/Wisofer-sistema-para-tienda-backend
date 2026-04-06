using System.Threading.Tasks;

namespace SistemaDeTienda.Services.IServices;

public interface ITicketService
{
    Task<byte[]> GenerarTicketPdfAsync(int ventaId);
}
