using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Saraitel.Entidades
{
    public class Administrador : Usuario
    {
        public Administrador()
        {
            Rol = "Administrador";
        }
    }
}