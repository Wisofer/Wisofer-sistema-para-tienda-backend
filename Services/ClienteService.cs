using SistemaDeTienda.Data;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace SistemaDeTienda.Services;

public class ClienteService : IClienteService
{
    private readonly ApplicationDbContext _context;

    public ClienteService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Cliente> ObtenerTodos()
    {
        return _context.Clientes
            .OrderByDescending(c => c.FechaRegistro)
            .ToList();
    }

    public Cliente? ObtenerPorId(int id)
    {
        return _context.Clientes
            .Include(c => c.Ventas)
            .FirstOrDefault(c => c.Id == id);
    }

    public Cliente? ObtenerPorCodigo(string codigo)
    {
        return _context.Clientes.FirstOrDefault(c => c.Codigo == codigo);
    }

    public List<Cliente> Buscar(string termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
            return ObtenerTodos();

        termino = termino.ToLower();
        return _context.Clientes
            .Where(c => c.Nombre.ToLower().Contains(termino) ||
                       c.Codigo.ToLower().Contains(termino) ||
                       (c.Cedula != null && c.Cedula.ToLower().Contains(termino)) ||
                       (c.Telefono != null && c.Telefono.Contains(termino)))
            .OrderByDescending(c => c.FechaRegistro)
            .ToList();
    }

    public Cliente Crear(Cliente cliente)
    {
        cliente.FechaRegistro = DateTime.Now;
        cliente.Activo = true;
        
        if (string.IsNullOrWhiteSpace(cliente.Codigo))
        {
            // Generar código simple CLI-XXXX
            var count = _context.Clientes.Count() + 1;
            cliente.Codigo = $"CLI-{count:D4}";
        }
        
        _context.Clientes.Add(cliente);
        _context.SaveChanges();
        return cliente;
    }

    public Cliente Actualizar(Cliente cliente)
    {
        var existente = _context.Clientes.Find(cliente.Id);
        if (existente == null)
            throw new Exception("Cliente no encontrado");

        existente.Nombre = cliente.Nombre;
        existente.Telefono = cliente.Telefono;
        existente.Direccion = cliente.Direccion;
        existente.Email = cliente.Email;
        existente.Cedula = cliente.Cedula;
        existente.Activo = cliente.Activo;

        _context.SaveChanges();
        return existente;
    }

    public bool Eliminar(int id)
    {
        var cliente = _context.Clientes.Find(id);
        if (cliente == null)
            return false;

        // No eliminar si tiene ventas
        var tieneVentas = _context.Ventas.Any(v => v.ClienteId == id);
        if (tieneVentas)
            throw new Exception("No se puede eliminar un cliente con historial de ventas.");

        _context.Clientes.Remove(cliente);
        _context.SaveChanges();
        return true;
    }
}
