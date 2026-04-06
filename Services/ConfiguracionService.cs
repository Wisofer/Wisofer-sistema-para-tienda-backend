using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Services.IServices;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BarRestPOS.Services;

public class ConfiguracionService : IConfiguracionService
{
    private readonly ApplicationDbContext _context;

    public ConfiguracionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public string? ObtenerValor(string clave)
    {
        var configuracion = _context.Configuraciones
            .FirstOrDefault(c => c.Clave == clave);
        return configuracion?.Valor;
    }

    public decimal ObtenerValorDecimal(string clave)
    {
        var valor = ObtenerValor(clave);
        if (string.IsNullOrWhiteSpace(valor))
            return clave == "TipoCambioDolar" ? SD.TipoCambioDolar : 0;
        
        if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var resultado))
            return resultado;
        
        return 0;
    }

    public List<Configuracion> ObtenerTodas()
    {
        return _context.Configuraciones
            .OrderBy(c => c.Clave)
            .ToList();
    }

    public Configuracion? ObtenerPorClave(string clave)
    {
        return _context.Configuraciones
            .FirstOrDefault(c => c.Clave == clave);
    }

    public void ActualizarValor(string clave, string valor, string? usuarioActualizacion = null)
    {
        var configuracion = _context.Configuraciones.FirstOrDefault(c => c.Clave == clave);
        
        if (configuracion == null)
        {
            configuracion = new Configuracion
            {
                Clave = clave,
                Valor = valor,
                FechaCreacion = DateTime.Now
            };
            _context.Configuraciones.Add(configuracion);
        }
        else
        {
            configuracion.Valor = valor;
            configuracion.FechaActualizacion = DateTime.Now;
            configuracion.UsuarioActualizacion = usuarioActualizacion;
        }
        
        _context.SaveChanges();
    }

    public void CrearSiNoExiste(string clave, string valor, string? descripcion = null)
    {
        var existe = _context.Configuraciones.Any(c => c.Clave == clave);
        if (!existe)
        {
            var configuracion = new Configuracion
            {
                Clave = clave,
                Valor = valor,
                Descripcion = descripcion,
                FechaCreacion = DateTime.Now
            };
            _context.Configuraciones.Add(configuracion);
            _context.SaveChanges();
        }
    }

    public void Upsert(string clave, string valor, string? descripcion, string? usuario = null)
    {
        var config = _context.Configuraciones.FirstOrDefault(c => c.Clave == clave);
        if (config == null)
        {
            config = new Configuracion
            {
                Clave = clave.Trim(),
                Valor = valor.Trim(),
                Descripcion = descripcion?.Trim(),
                FechaCreacion = DateTime.Now,
                FechaActualizacion = DateTime.Now,
                UsuarioActualizacion = usuario
            };
            _context.Configuraciones.Add(config);
        }
        else
        {
            config.Valor = valor.Trim();
            config.Descripcion = descripcion?.Trim();
            config.FechaActualizacion = DateTime.Now;
            config.UsuarioActualizacion = usuario;
        }

        _context.SaveChanges();
    }

    #region Plantillas WhatsApp

    public async Task<List<PlantillaMensajeWhatsApp>> ObtenerPlantillasWhatsAppAsync(bool? activas)
    {
        var query = _context.PlantillasMensajeWhatsApp.AsNoTracking().AsQueryable();
        if (activas.HasValue) query = query.Where(p => p.Activa == activas.Value);

        return await query
            .OrderByDescending(p => p.EsDefault)
            .ThenBy(p => p.Nombre)
            .ToListAsync();
    }

    public async Task<PlantillaMensajeWhatsApp?> ObtenerPlantillaWhatsAppPorIdAsync(int id)
    {
        return await _context.PlantillasMensajeWhatsApp.FindAsync(id);
    }

    public async Task<PlantillaMensajeWhatsApp> CrearPlantillaWhatsAppAsync(PlantillaMensajeWhatsApp plantilla)
    {
        if (plantilla.EsDefault)
        {
            await QuitarDefaultsExistentesAsync();
        }

        plantilla.FechaCreacion = DateTime.Now;
        _context.PlantillasMensajeWhatsApp.Add(plantilla);
        await _context.SaveChangesAsync();
        return plantilla;
    }

    public async Task<PlantillaMensajeWhatsApp> ActualizarPlantillaWhatsAppAsync(PlantillaMensajeWhatsApp plantilla)
    {
        var existente = await _context.PlantillasMensajeWhatsApp.FindAsync(plantilla.Id);
        if (existente == null) throw new Exception("Plantilla no encontrada.");

        if (plantilla.EsDefault && !existente.EsDefault)
        {
            await QuitarDefaultsExistentesAsync(plantilla.Id);
        }

        existente.Nombre = plantilla.Nombre;
        existente.Mensaje = plantilla.Mensaje;
        existente.Activa = plantilla.Activa;
        existente.EsDefault = plantilla.EsDefault;
        existente.FechaActualizacion = DateTime.Now;

        await _context.SaveChangesAsync();
        return existente;
    }

    public async Task<bool> EliminarPlantillaWhatsAppAsync(int id)
    {
        var plantilla = await _context.PlantillasMensajeWhatsApp.FindAsync(id);
        if (plantilla == null) return false;

        if (plantilla.EsDefault)
        {
            var existeOtraDefault = await _context.PlantillasMensajeWhatsApp.AnyAsync(p => p.EsDefault && p.Id != id);
            if (!existeOtraDefault)
                throw new Exception("No se puede eliminar la única plantilla por defecto.");
        }

        _context.PlantillasMensajeWhatsApp.Remove(plantilla);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task MarcarPlantillaWhatsAppDefaultAsync(int id)
    {
        var plantilla = await _context.PlantillasMensajeWhatsApp.FindAsync(id);
        if (plantilla == null) throw new Exception("Plantilla no encontrada.");

        await QuitarDefaultsExistentesAsync(id);

        plantilla.EsDefault = true;
        plantilla.FechaActualizacion = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    private async Task QuitarDefaultsExistentesAsync(int? excuirId = null)
    {
        var query = _context.PlantillasMensajeWhatsApp.Where(p => p.EsDefault);
        if (excuirId.HasValue) query = query.Where(p => p.Id != excuirId.Value);

        var defaults = await query.ToListAsync();
        foreach (var p in defaults) p.EsDefault = false;
    }

    #endregion
}
