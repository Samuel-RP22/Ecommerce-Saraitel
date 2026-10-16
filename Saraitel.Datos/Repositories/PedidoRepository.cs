using System.Collections.Generic;
using System.Linq;
using Saraitel.Datos.Managers;
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
    }
}