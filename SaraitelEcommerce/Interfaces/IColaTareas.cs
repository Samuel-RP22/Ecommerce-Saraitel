namespace Saraitel.API.Interfaces
{
    // Tareas lentas (correos, factura en PDF) que se hacen después de responder
    public interface IColaTareas
    {
        void Encolar(string descripcion, Action<IServiceProvider> tarea);
    }
}
