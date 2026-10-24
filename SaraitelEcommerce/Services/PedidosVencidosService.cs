using Saraitel.API.Interfaces;

namespace Saraitel.API.Services
{
    // HU-16: cada minuto cancela los pedidos que no se pagaron a tiempo,
    // para que su stock quede disponible para otros clientes
    public class PedidosVencidosService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PedidosVencidosService> _logger;

        public PedidosVencidosService(
            IServiceScopeFactory scopeFactory,
            ILogger<PedidosVencidosService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(1));

            // Se revisa al arrancar la API y luego una vez por minuto
            do
            {
                try
                {
                    // IPedidoService está registrado por petición (Scoped),
                    // así que se crea un alcance propio para cada revisión
                    using var alcance = _scopeFactory.CreateScope();

                    var pedidos = alcance.ServiceProvider.GetRequiredService<IPedidoService>();

                    int cancelados = pedidos.CancelarPedidosVencidos();

                    if (cancelados > 0)
                        _logger.LogInformation("Se cancelaron {Cantidad} pedidos sin pagar.", cancelados);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al revisar los pedidos vencidos.");
                }
            }
            while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
    }
}
