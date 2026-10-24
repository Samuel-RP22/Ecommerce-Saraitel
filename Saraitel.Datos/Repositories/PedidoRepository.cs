using System.Collections.Generic;
using System.Linq;
using Saraitel.Datos.Managers;
using Saraitel.Datos.Models;
using Saraitel.Entidades;

namespace Saraitel.Datos.Repositories
{
    public class PedidoRepository
    {
        private readonly JsonManager _jsonManager;

        public PedidoRepository()
        {
            _jsonManager = JsonManager.Instance;
        }

        public List<Pedido> ObtenerPedidos()
        {
            return _jsonManager.LeerDatos().Pedidos;
        }

        public void AgregarPedido(Pedido pedido)
        {
            var data = _jsonManager.LeerDatos();
            data.Pedidos.Add(pedido);
            _jsonManager.GuardarDatos(data);
        }

        public Pedido? ObtenerPedidoPorId(int id)
        {
            return ObtenerPedidos().FirstOrDefault(p => p.IdPedido == id);
        }

        // HU-16: guarda los cambios de un pedido. El cliente también tiene una
        // copia en su historial de compras, así que se actualizan las dos.
        public void ActualizarPedido(Pedido pedido)
        {
            var data = _jsonManager.LeerDatos();

            ReemplazarPedido(data, pedido);

            _jsonManager.GuardarDatos(data);
        }

        // HU-16: cancela un pedido pendiente en una sola escritura del archivo:
        // cambia el estado, devuelve el stock reservado y regresa los productos
        // al carrito del cliente para que pueda intentarlo de nuevo si quiere.
        public void CancelarPedido(Pedido pedido, string motivo)
        {
            var data = _jsonManager.LeerDatos();

            pedido.EstadoPedido = Pedido.EstadoCancelado;
            pedido.MotivoCancelacion = motivo;

            ReemplazarPedido(data, pedido);

            var cliente = data.Usuarios
                .OfType<Cliente>()
                .FirstOrDefault(c => c.IdUsuario == pedido.IdUsuario);

            foreach (var detalle in pedido.Detalles)
            {
                var producto = data.Productos
                    .FirstOrDefault(p => p.IdProducto == detalle.IdProducto);

                // El producto pudo haber sido eliminado del catálogo
                if (producto == null)
                    continue;

                producto.Stock += detalle.Cantidad;

                if (cliente != null)
                {
                    for (int i = 0; i < detalle.Cantidad; i++)
                    {
                        cliente.Carrito.ListaProductos.Add(producto);
                    }
                }
            }

            if (cliente != null)
            {
                cliente.Carrito.CantidadProductos = cliente.Carrito.ListaProductos.Count;
                cliente.Carrito.TotalCompra = cliente.Carrito.ListaProductos.Sum(p => p.Precio);
            }

            _jsonManager.GuardarDatos(data);
        }

        // HU-17: le asigna al pedido el siguiente número de factura y lo guarda.
        // El consecutivo y el pedido se guardan en la misma escritura del archivo,
        // así nunca quedan dos facturas con el mismo número ni huecos.
        public void AsignarFactura(Pedido pedido)
        {
            var data = _jsonManager.LeerDatos();

            if (string.IsNullOrEmpty(pedido.NumeroFactura))
            {
                data.ConsecutivoFactura++;
                pedido.NumeroFactura = $"FV-{data.ConsecutivoFactura:D5}";
                pedido.FechaFactura = DateTime.Now;
            }

            ReemplazarPedido(data, pedido);

            _jsonManager.GuardarDatos(data);
        }

        private static void ReemplazarPedido(DataStore data, Pedido pedido)
        {
            int indice = data.Pedidos.FindIndex(p => p.IdPedido == pedido.IdPedido);

            if (indice >= 0)
                data.Pedidos[indice] = pedido;

            foreach (var cliente in data.Usuarios.OfType<Cliente>())
            {
                int enHistorial = cliente.HistorialCompras
                    .FindIndex(p => p.IdPedido == pedido.IdPedido);

                if (enHistorial >= 0)
                    cliente.HistorialCompras[enHistorial] = pedido;
            }
        }
    }
}