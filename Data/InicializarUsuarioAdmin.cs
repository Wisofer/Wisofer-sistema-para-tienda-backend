using BarRestPOS.Data;
using BarRestPOS.Models.Entities;
using BarRestPOS.Utils;
using Microsoft.EntityFrameworkCore;

namespace BarRestPOS.Data;

public static class InicializarUsuarioAdmin
{
    public static void CrearAdminSiNoExiste(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            // Verificar si ya existe un usuario admin
            if (!context.Usuarios.Any(u => u.NombreUsuario.ToLower() == "admin"))
            {
                var admin = new Usuario
                {
                    NombreUsuario = "admin",
                    Contrasena = PasswordHelper.HashPassword("admin"),
                    NombreCompleto = "Administrador del Sistema",
                    Rol = SD.RolAdministrador,
                    Activo = true,
                    Email = "admin@clothingstorepos.com",
                    FechaCreacion = DateTime.Now
                };
                context.Usuarios.Add(admin);
                logger.LogInformation("Usuario admin creado: admin/admin");
            }

            // Sembrar Configuración inicial: Tipo de Cambio
            if (!context.Configuraciones.Any(c => c.Clave == "TipoCambioDolar"))
            {
                context.Configuraciones.Add(new Configuracion
                {
                    Clave = "TipoCambioDolar",
                    Valor = "36.65",
                    Descripcion = "Tipo de cambio oficial (C$ por $1)",
                    FechaCreacion = DateTime.Now,
                    FechaActualizacion = DateTime.Now,
                    UsuarioActualizacion = "sistema"
                });
                logger.LogInformation("Configuración Inicial creada: TipoCambioDolar");
            }

            // Sembrar Plantilla de WhatsApp inicial
            if (!context.PlantillasMensajeWhatsApp.Any())
            {
                context.PlantillasMensajeWhatsApp.Add(new PlantillaMensajeWhatsApp
                {
                    Nombre = "Ticket de Venta Estándar",
                    Mensaje = "Hola *{nombre_cliente}*, gracias por tu compra en nuestra tienda. Tu ticket *#{numero_ticket}* por un total de *{total}* ha sido generado con éxito. ¡Vuelve pronto!",
                    Activa = true,
                    EsDefault = true,
                    FechaCreacion = DateTime.Now
                });
                logger.LogInformation("Plantilla de WhatsApp inicial creada.");
            }

            context.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al inicializar los datos maestros del sistema.");
        }
    }
}
