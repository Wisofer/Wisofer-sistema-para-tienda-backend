using SistemaDeTienda.Models.Entities;

namespace SistemaDeTienda.Services.IServices;

public interface IConfiguracionService
{
    string? ObtenerValor(string clave);
    decimal ObtenerValorDecimal(string clave);
    List<Configuracion> ObtenerTodas();
    Configuracion? ObtenerPorClave(string clave);
    void ActualizarValor(string clave, string valor, string? usuarioActualizacion = null);
    void CrearSiNoExiste(string clave, string valor, string? descripcion = null);
    void Upsert(string clave, string valor, string? descripcion, string? usuario = null);

    // Plantillas WhatsApp
    Task<List<PlantillaMensajeWhatsApp>> ObtenerPlantillasWhatsAppAsync(bool? activas);
    Task<PlantillaMensajeWhatsApp?> ObtenerPlantillaWhatsAppPorIdAsync(int id);
    Task<PlantillaMensajeWhatsApp> CrearPlantillaWhatsAppAsync(PlantillaMensajeWhatsApp plantilla);
    Task<PlantillaMensajeWhatsApp> ActualizarPlantillaWhatsAppAsync(PlantillaMensajeWhatsApp plantilla);
    Task<bool> EliminarPlantillaWhatsAppAsync(int id);
    Task MarcarPlantillaWhatsAppDefaultAsync(int id);
}
