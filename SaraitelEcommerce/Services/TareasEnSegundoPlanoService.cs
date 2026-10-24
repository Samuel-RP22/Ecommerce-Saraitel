namespace Saraitel.API.Services
{
    // Ejecuta, una por una, las tareas de la cola (correos y facturas)
    public class TareasEnSegundoPlanoService : BackgroundService
    {
        private readonly ColaTareas _cola;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TareasEnSegundoPlanoService> _logger;

        public TareasEnSegundoPlanoService(
            ColaTareas cola,
            IServiceScopeFactory scopeFactory,
            ILogger<TareasEnSegundoPlanoService> logger)
        {
            _cola = cola;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var (descripcion, tarea) in _cola.LeerTodas(stoppingToken))
            {
                try
                {
                    // Cada tarea tiene su propio alcance (como si fuera una petición)
                    using var alcance = _scopeFactory.CreateScope();

                    // El envío por SMTP es bloqueante: se corre fuera del hilo principal
                    await Task.Run(() => tarea(alcance.ServiceProvider), stoppingToken);
                }
                catch (Exception ex)
                {
                    // Si un correo falla, la operación ya quedó guardada: solo se registra
                    _logger.LogError(ex, "Falló la tarea en segundo plano: {Descripcion}", descripcion);
                }
            }
        }
    }
}
