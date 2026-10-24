using System.Threading.Channels;
using Saraitel.API.Interfaces;

namespace Saraitel.API.Services
{
    // Rendimiento: enviar un correo tarda varios segundos (conectar con Gmail,
    // autenticarse y enviar). Para que el cliente no espere, la tarea se deja en
    // esta cola y la respuesta sale de inmediato. TareasEnSegundoPlanoService
    // la ejecuta justo después.
    public class ColaTareas : IColaTareas
    {
        private readonly Channel<(string Descripcion, Action<IServiceProvider> Tarea)> _canal =
            Channel.CreateUnbounded<(string, Action<IServiceProvider>)>();

        public void Encolar(string descripcion, Action<IServiceProvider> tarea)
        {
            _canal.Writer.TryWrite((descripcion, tarea));
        }

        public IAsyncEnumerable<(string Descripcion, Action<IServiceProvider> Tarea)> LeerTodas(CancellationToken cancelacion)
        {
            return _canal.Reader.ReadAllAsync(cancelacion);
        }
    }
}
