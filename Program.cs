using SistemaDeTienda.Data;
using SistemaDeTienda.Models.Api;
using SistemaDeTienda.Models.Entities;
using SistemaDeTienda.Services;
using SistemaDeTienda.Services.IServices;
using SistemaDeTienda.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

// Configurar Npgsql para manejar DateTime correctamente con PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor (API solo)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Clothing Store POS API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Bearer. Obtener con POST /api/v1/auth/login y usar: Authorization: Bearer {token}"
    });
});

// Configurar URLs en minúsculas
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// Configurar Entity Framework con PostgreSQL (DATABASE_URL / POSTGRES_URL / DefaultConnection)
var connectionString = PostgresConnectionResolver.Resolve(builder.Configuration);
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Base de datos no configurada. Defina DATABASE_URL o POSTGRES_URL, o ConnectionStrings:DefaultConnection en configuración.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// Configurar CORS
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Configurar JWT (sin clave por defecto en código)
var jwtSecret = builder.Configuration["JwtSettings:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException("JwtSettings:SecretKey no configurado en appsettings.json.");
}

if (builder.Environment.IsProduction() && jwtSecret.Length < 32)
{
    throw new InvalidOperationException("JwtSettings:SecretKey debe tener al menos 32 caracteres en producción.");
}

var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "ClothingStorePOS";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "ClothingStorePOSUsers";
var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtKey,
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireClaim("Rol", "Administrador"));
    options.AddPolicy("Cajero", policy => policy.RequireClaim("Rol", "Cajero", "Administrador"));
});

// Registrar servicios
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<ICategoriaProductoService, CategoriaProductoService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IInventarioService, InventarioService>();
builder.Services.AddScoped<IConfiguracionService, ConfiguracionService>();
builder.Services.AddScoped<IProveedorService, ProveedorService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<ICajaService, CajaService>();
builder.Services.AddScoped<IStorageService, R2StorageService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ExcelExportService>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Aplicar migraciones e inicializar datos
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        InicializarUsuarioAdmin.CrearAdminSiNoExiste(dbContext, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al inicializar la base de datos");
    }
}

// Configurar el pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Clothing Store POS API v1");
    });
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>
            {
                Success = false,
                Message = "Error interno del servidor.",
                Data = null
            });
        });
    });
    app.UseHsts();
}

app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });
}

app.UseRouting();

// CORS debe ir entre UseRouting y UseAuthentication
app.UseCors("FrontendCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
