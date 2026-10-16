using Saraitel.Datos.Managers;
using Saraitel.Datos.Models;
using Saraitel.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Saraitel.Datos.Repositories
{
    public class UsuarioRepository
    {
        private readonly JsonManager _jsonManager;

        public UsuarioRepository()
        {
            _jsonManager = JsonManager.Instance;
        }
        public List<Usuario> ObtenerUsuarios()
        {
            var data = _jsonManager.LeerDatos();
            return data.Usuarios;
        }

        public void AgregarUsuario(Usuario usuario)
        {
            var data = _jsonManager.LeerDatos();
            data.Usuarios.Add(usuario);
            _jsonManager.GuardarDatos(data);
        }

        public Usuario? ObtenerUsuarioPorCorreo(string correo)
        {
            if (string.IsNullOrEmpty(correo))
                return null;

            return ObtenerUsuarios().FirstOrDefault(u =>
                u.Correo.Trim().Equals(
                    correo.Trim(),
                    StringComparison.OrdinalIgnoreCase
                ));
        }

        public Usuario? ObtenerUsuarioPorId(int id)
        {
            return ObtenerUsuarios()
                .FirstOrDefault(u => u.IdUsuario == id);
        }

        public void ActualizarUsuario(Usuario usuarioActualizado)
        {
            var data = _jsonManager.LeerDatos();

            int indice = data.Usuarios.FindIndex(u =>
                u.IdUsuario == usuarioActualizado.IdUsuario);

            if (indice < 0)
                return;

            data.Usuarios[indice] = usuarioActualizado;

            _jsonManager.GuardarDatos(data);
        }

        public void GuardarCambios(List<Usuario> usuariosActualizados)
        {
            var data = _jsonManager.LeerDatos();

            data.Usuarios = usuariosActualizados;

            _jsonManager.GuardarDatos(data);
        }
    }
}