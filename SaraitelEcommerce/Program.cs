using Microsoft.Extensions.FileProviders;
using QuestPDF.Infrastructure;
using Saraitel.API.Interfaces;
using Saraitel.API.Services;
using Saraitel.Datos.Conexion;
using Saraitel.Datos.Repositories;

var builder = WebApplication.CreateBuilder(args);

// HU-17: QuestPDF es gratis para proyectos educativos y empresas pequeñas
// con la licencia Community, pero hay que declararla
QuestPDF.Settings.License = LicenseType.Community;

// Base de datos PostgreSQL en Neon. La cadena de conexión NO va en appsettings.json
// (se subiría a GitHub): va en los secretos de usuario como "ConnectionStrings:Saraitel"
ConexionBD.Configurar(builder.Configuration.GetConnectionString("Saraitel"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddScoped<ICarritoService, CarritoService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();

// HU-16: Wompi usa HttpClient para consultar las transacciones
builder.Services.AddHttpClient<IWompiService, WompiService>();

// HU-16: cancela los pedidos que pasan 20 minutos sin pagarse
builder.Services.AddHostedService<PedidosVencidosService>();

// HU-17: genera las facturas en PDF
builder.Services.AddScoped<IFacturaService, FacturaService>();

// Rendimiento: los correos y las facturas en PDF se hacen en segundo plano,
// así comprar, enviar, entregar o gestionar devoluciones responde de inmediato
builder.Services.AddSingleton<ColaTareas>();
builder.Services.AddSingleton<IColaTareas>(servicios => servicios.GetRequiredService<ColaTareas>());
builder.Services.AddHostedService<TareasEnSegundoPlanoService>();

// HU-19 a HU-23: devoluciones y garantías
builder.Services.AddScoped<IPosventaService, PosventaService>();

builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<ProductoRepository>();
builder.Services.AddScoped<CategoriaRepository>();
builder.Services.AddScoped<PedidoRepository>();
builder.Services.AddScoped<PosventaRepository>();

// Los textos no anulables (string) no se vuelven obligatorios automáticamente:
// así un campo opcional vacío ("") no rechaza la petición y cada servicio
// valida sus datos con mensajes claros en español.
builder.Services.AddControllers(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Se prueba la conexión al arrancar para avisar de una vez si algo está mal
try
{
    ConexionBD.Probar();
    app.Logger.LogInformation("Conectado a la base de datos de Neon.");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "No se pudo conectar a la base de datos. Revisa la cadena de conexión " +
        "(secretos de usuario: ConnectionStrings:Saraitel) y que el .sql se haya ejecutado en Neon.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// La tienda (carpeta Saraitel-Frontend) se sirve desde esta misma API.
// Así, con F5 queda todo funcionando en https://localhost:7021
// sin Live Server. Se usa la misma carpeta de siempre: no hay copias.
string carpetaTienda = Path.GetFullPath(
    Path.Combine(app.Environment.ContentRootPath, "..", "Saraitel-Frontend"));

if (Directory.Exists(carpetaTienda))
{
    var archivosTienda = new PhysicalFileProvider(carpetaTienda);

    // "/" abre index.html
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = archivosTienda });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = archivosTienda });
}
else
{
    Console.WriteLine($"AVISO: no se encontró la tienda en {carpetaTienda}");
}

app.UseCors("PermitirFrontend");

app.UseAuthorization();
app.MapControllers();
app.Run();