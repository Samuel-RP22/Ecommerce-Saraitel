const API = "https://localhost:7021/api";

let usuarioId = null;
let usuarioNombre = "";
let esAdministrador = false; 

/* ==========================================
   NAVEGACIÓN Y CONTROL DE VISTAS POR ROL
========================================== */

function mostrarFormulario(tipo) {
    document.getElementById("formLogin").classList.add("hidden");
    document.getElementById("formRegistro").classList.add("hidden");
    document.getElementById("formRecuperar").classList.add("hidden");
    document.getElementById("formRestablecer").classList.add("hidden");

    if (tipo === 'login') {
        document.getElementById("formLogin").classList.remove("hidden");
    } else if (tipo === 'registro') {
        document.getElementById("formRegistro").classList.remove("hidden");
    } else if (tipo === 'recuperar') {
        document.getElementById("formRecuperar").classList.remove("hidden");
    } else if (tipo === 'restablecer') {
        document.getElementById("formRestablecer").classList.remove("hidden");
    }
}

function volverAInicio() {
    document.getElementById("formLogin").classList.add("hidden");
    document.getElementById("formRegistro").classList.add("hidden");
    document.getElementById("formRecuperar").classList.add("hidden");
    document.getElementById("formRestablecer").classList.add("hidden");
    if(document.getElementById("formCrearProducto")) {
        document.getElementById("formCrearProducto").classList.add("hidden");
    }
}

function configurarInterfazPorRol() {
    const componentsCliente = document.getElementById("componentesCliente");
    const panelAdministrador = document.getElementById("panelAdministrador");
    const btnCrearProductoAdmin = document.getElementById("btnCrearProductoAdmin");
    const btnCrearCategoriaAdmin = document.getElementById("btnCrearCategoriaAdmin");

    if (esAdministrador) {
        if (componentsCliente) componentsCliente.classList.add("hidden");
        if (panelAdministrador) panelAdministrador.classList.remove("hidden");
        if (btnCrearProductoAdmin) btnCrearProductoAdmin.classList.remove("hidden");
        if (btnCrearCategoriaAdmin) btnCrearCategoriaAdmin.classList.remove("hidden");
    } else {
        if (componentsCliente) componentsCliente.classList.remove("hidden");
        if (panelAdministrador) panelAdministrador.classList.add("hidden");
        // ¡Arreglado aquí! Se usó classList.add en vez de .add a secas
        if (btnCrearProductoAdmin) btnCrearProductoAdmin.classList.add("hidden");
        if (btnCrearCategoriaAdmin) btnCrearCategoriaAdmin.classList.add("hidden");
    }
}

function cargarTodo() {
    cargarProductos();
    cargarCategorias();
    cargarCarrito();
}

/* =========================
   USUARIO (AUTH)
========================= */

function login() {
    const correoInput =
        document.getElementById("correo");

    const contrasenaInput =
        document.getElementById("contrasena");

    if (!correoInput || !contrasenaInput) return;

    const correo = correoInput.value.trim();
    const contrasena = contrasenaInput.value;

    if (!correo || !contrasena) {
        return alert("Por favor, llena ambos campos.");
    }

    const queryParams = new URLSearchParams({
        correo: correo,
        contrasena: contrasena
    });

    fetch(`${API}/usuario/login?${queryParams.toString()}`, {
        method: "POST"
    })
    .then(async r => {

        const data = await r.json();

        if (!r.ok) {
            throw new Error(
                data.message ||
                data.title ||
                "Error al iniciar sesión"
            );
        }

        return data;
    })
    .then(data => {

        alert(data.mensaje);

        // 🔥 Ahora sí toma el ID real
        usuarioId = data.idUsuario;
        usuarioNombre = data.nombre;

        // Detectar si es admin
        esAdministrador =
            data.rol.toLowerCase() === "administrador";

        document.getElementById("userInfo").innerText =
            `👤 ${usuarioNombre}`;

        contrasenaInput.value = "";

        document.getElementById("vistaAuth")
            .classList.add("hidden");

        document.getElementById("vistaTienda")
            .classList.remove("hidden");

        configurarInterfazPorRol();
        cargarTodo();

        console.log("Usuario logueado:");
        console.log("ID:", usuarioId);
        console.log("Nombre:", usuarioNombre);
        console.log("Admin:", esAdministrador);
    })
    .catch(err => {
        alert("Login fallido: " + err.message);
        contrasenaInput.value = "";
    });
}

function ejecutarRegistro() {
    const nombre = document.getElementById("regNombre").value;
    const correo = document.getElementById("regCorreo").value;
    const telefono = document.getElementById("regTelefono").value;
    const contrasena = document.getElementById("regPassword").value; 
    const direccion = document.getElementById("regDireccion").value;
    const url = `${API}/usuario/registro?nombre=${encodeURIComponent(nombre)}&correo=${encodeURIComponent(correo)}&telefono=${encodeURIComponent(telefono)}&contrasena=${encodeURIComponent(contrasena)}&direccion=${encodeURIComponent(direccion)}`;
    
    fetch(url, { method: "POST" })
    .then(res => {
        if (res.ok) { return res.text(); } 
        else { return res.text().then(text => { throw new Error(text) }); }
    })
    .then(msg => {
        alert(msg); 
        document.getElementById("regNombre").value = "";
        document.getElementById("regCorreo").value = "";
        document.getElementById("regTelefono").value = "";
        document.getElementById("regPassword").value = "";
        document.getElementById("regDireccion").value = "";
        mostrarFormulario('login');
    })
    .catch(err => {
        alert("No se pudo registrar: " + err.message);
    });
}

function logout() {
    usuarioId = null;
    usuarioNombre = "";
    esAdministrador = false;

    document.getElementById("userInfo").innerText = "No logueado";
    document.getElementById("correo").value = "";
    document.getElementById("contrasena").value = "";

    document.getElementById("vistaTienda").classList.add("hidden");
    document.getElementById("vistaPerfil").classList.add("hidden");
    document.getElementById("vistaAuth").classList.remove("hidden");
    
    // Devolvemos la interfaz al estado de cliente y refrescamos el catálogo
    configurarInterfazPorRol();
    cargarProductos();
    volverAInicio();
}

/* ==========================================
   PERFIL DE USUARIO (HU-04 y HU-05)
========================================== */

function abrirPerfil() {
    if (!usuarioId) return alert("Debes iniciar sesión.");

    fetch(`${API}/usuario/perfil/${usuarioId}`)
        .then(async r => {
            if (!r.ok) throw new Error(await r.text());
            return r.json();
        })
        .then(perfil => {
            llenarPerfil(perfil);
            setModoEdicionPerfil(false);

            document.getElementById("vistaTienda").classList.add("hidden");
            document.getElementById("vistaPerfil").classList.remove("hidden");
        })
        .catch(err => alert("No se pudo cargar el perfil: " + err.message));
}

function llenarPerfil(perfil) {
    document.getElementById("perfNombre").value = perfil.nombre || "";
    document.getElementById("perfCorreo").value = perfil.correo || "";
    document.getElementById("perfTelefono").value = perfil.telefono || "";
    document.getElementById("perfDireccion").value = perfil.direccion || "";
    document.getElementById("perfRol").value = perfil.rol || "";

    // La dirección solo existe para clientes
    const esCliente = (perfil.rol || "").toLowerCase() === "cliente";
    document.getElementById("perfDireccionBox").classList.toggle("hidden", !esCliente);
}

function setModoEdicionPerfil(editando) {
    ["perfNombre", "perfCorreo", "perfTelefono", "perfDireccion"].forEach(id => {
        document.getElementById(id).disabled = !editando;
    });
    // El rol nunca se edita
    document.getElementById("perfRol").disabled = true;

    document.getElementById("btnEditarPerfil").classList.toggle("hidden", editando);
    document.getElementById("btnVolverPerfil").classList.toggle("hidden", editando);
    document.getElementById("btnGuardarPerfil").classList.toggle("hidden", !editando);
    document.getElementById("btnCancelarPerfil").classList.toggle("hidden", !editando);

    document.getElementById("perfilSubtitulo").innerText = editando
        ? "Modifica tus datos y guarda los cambios"
        : "Estos son los datos de tu cuenta";
}

function editarPerfil() {
    setModoEdicionPerfil(true);
}

function cancelarEdicionPerfil() {
    // Recargamos desde el servidor para descartar lo escrito
    abrirPerfil();
}

function guardarPerfil() {
    const datos = {
        nombre: document.getElementById("perfNombre").value.trim(),
        correo: document.getElementById("perfCorreo").value.trim(),
        telefono: document.getElementById("perfTelefono").value.trim(),
        direccion: document.getElementById("perfDireccion").value.trim()
    };

    if (!datos.nombre) return alert("El nombre es obligatorio.");
    if (!datos.correo) return alert("El correo es obligatorio.");

    const queryParams = new URLSearchParams(datos);

    fetch(`${API}/usuario/perfil/${usuarioId}?${queryParams.toString()}`, {
        method: "PUT"
    })
    .then(async r => {
        const msg = await r.text();
        if (!r.ok) throw new Error(msg);
        return msg;
    })
    .then(msg => {
        alert(msg);

        // Refrescamos el nombre que se ve en el header
        usuarioNombre = datos.nombre;
        document.getElementById("userInfo").innerText = `👤 ${usuarioNombre}`;

        abrirPerfil();
    })
    .catch(err => alert("No se pudo actualizar el perfil: " + err.message));
}

function cerrarPerfil() {
    document.getElementById("vistaPerfil").classList.add("hidden");
    document.getElementById("vistaTienda").classList.remove("hidden");
}

/* ==========================================
   RECUPERAR ACCESO (HU-06 y HU-07)
========================================== */

function solicitarRecuperacion() {
    const correo = document.getElementById("recCorreo").value.trim();

    if (!correo) return alert("Escribe el correo de tu cuenta.");

    fetch(`${API}/usuario/recuperar?correo=${encodeURIComponent(correo)}`, {
        method: "POST"
    })
    .then(async r => {
        const txt = await r.text();
        if (!r.ok) throw new Error(txt);
        return JSON.parse(txt);
    })
    .then(data => {
        document.getElementById("resCorreo").value = correo;
        document.getElementById("resCodigo").value = "";

        const aviso = document.getElementById("avisoCodigo");
        aviso.classList.remove("hidden");

        // Todavía no hay envío de correos: el backend devuelve el código
        // y lo mostramos aquí (solo para desarrollo).
        if (data.codigo) {
            aviso.innerText = `${data.mensaje} Código (simula el correo): ${data.codigo}. Vence en 15 minutos.`;
        } else {
            aviso.innerText = data.mensaje;
        }

        mostrarFormulario('restablecer');
    })
    .catch(err => alert("No se pudo solicitar la recuperación: " + err.message));
}

function ejecutarRestablecer() {
    const correo = document.getElementById("resCorreo").value.trim();
    const codigo = document.getElementById("resCodigo").value.trim();
    const nueva = document.getElementById("resPassword").value;
    const confirmar = document.getElementById("resPasswordConfirmar").value;

    if (!correo || !codigo || !nueva || !confirmar) {
        return alert("Completa todos los campos.");
    }
    if (nueva.length < 6) {
        return alert("La nueva contraseña debe tener al menos 6 caracteres.");
    }
    if (nueva !== confirmar) {
        return alert("Las contraseñas no coinciden.");
    }

    const queryParams = new URLSearchParams({
        correo: correo,
        codigo: codigo,
        nuevaContrasena: nueva
    });

    fetch(`${API}/usuario/restablecer?${queryParams.toString()}`, {
        method: "POST"
    })
    .then(async r => {
        const msg = await r.text();
        if (!r.ok) throw new Error(msg);
        return msg;
    })
    .then(msg => {
        alert(msg);

        document.getElementById("recCorreo").value = "";
        document.getElementById("resCorreo").value = "";
        document.getElementById("resCodigo").value = "";
        document.getElementById("resPassword").value = "";
        document.getElementById("resPasswordConfirmar").value = "";
        document.getElementById("avisoCodigo").classList.add("hidden");

        mostrarFormulario('login');
    })
    .catch(err => alert("No se pudo restablecer la contraseña: " + err.message));
}

/* =======================================================
   PRODUCTOS (GESTIÓN DEL CATÁLOGO, CREACIÓN Y MODIFICACIÓN)
======================================================= */

function cargarProductos() {
    fetch(`${API}/producto`)
        .then(r => r.json())
        .then(data => {
            let html = "";
            data.forEach(p => {
                const urlImagen = p.imagen || p.Imagen || "";
                const urlFinal = urlImagen.trim() !== "" 
                    ? urlImagen 
                    : "https://images.unsplash.com/photo-1546868871-7041f2a55e12?w=500&q=80";

                const idProductoActual = p.idProducto !== undefined ? p.idProducto : p.IdProducto;

                const botonAccion = esAdministrador
                    ? `<div style="display: flex; gap: 8px; width: 100%; margin-top: 10px;">
                        <button class="btn-dark-blue" style="flex: 1;" onclick="modificarProducto(${idProductoActual})">📝 Modificar</button>
                        <button class="btn-danger" style="flex: 1;" onclick="eliminarProducto(${idProductoActual})">🗑 Eliminar</button>
                       </div>`
                    : `<button class="btn-add" style="width:100%" onclick="agregarCarrito(${idProductoActual})">Agregar al Carrito</button>`;

                const nombreProd = p.nombre || p.Nombre || "Producto sin nombre";
                const descProd = p.descripcion || p.Descripcion || "";
                const precioProd = p.precio !== undefined ? p.precio : p.Precio;

                html += `
                <div class="card-item">
                    <div class="producto-img-container">
                        <img src="${urlFinal}" alt="${nombreProd}" class="producto-img">
                    </div>
                    <h3>${nombreProd}</h3>
                    <p>${descProd}</p>
                    <b class="precio">$${precioProd}</b>
                    ${botonAccion}
                </div>`;
            });
            document.getElementById("productos").innerHTML = html;
        })
        .catch(err => console.error("Error cargando catálogo de productos:", err));
}

function crearProducto() {
    document.getElementById('vistaTienda').classList.add('hidden'); 
    document.getElementById('vistaCrearProducto').classList.remove('hidden'); 

    const selectCategoria = document.getElementById("prodCategoria");
    if (!selectCategoria) return;

    selectCategoria.innerHTML = "<option value=''>Cargando categorías...</option>";

    fetch(`${API}/categoria`)
        .then(r => r.json())
        .then(data => {
            if (Array.isArray(data) && data.length > 0) {
                let optionsHtml = "<option value=''>-- Selecciona una categoría --</option>";
                data.forEach(c => {
                    if (!c) return;
                    const nombreCat = c.nombre || c.Nombre || c.nombreCategoria || c.NombreCategoria || "Sin Nombre";
                    optionsHtml += `<option value="${nombreCat}">${nombreCat}</option>`;
                });
                selectCategoria.innerHTML = optionsHtml;
            } else {
                selectCategoria.innerHTML = "<option value=''>No hay categorías disponibles</option>";
            }
        })
        .catch(err => {
            console.error("Error al poblar select de categorías:", err);
            selectCategoria.innerHTML = "<option value=''>Error al cargar</option>";
        });
}

function ejecutarCrearProducto() {
    const nombre = document.getElementById("prodNombre").value.trim();
    const description = document.getElementById("prodDescripcion").value.trim();
    const precio = parseFloat(document.getElementById("prodPrecio").value) || 0;
    const imagen = document.getElementById("prodImagen").value.trim();
    const nombreCategoriaSeleccionada = document.getElementById("prodCategoria").value; 

    if (!nombre) return alert("El nombre del producto es obligatorio.");
    if (!nombreCategoriaSeleccionada) return alert("Por favor, selecciona una categoría.");

    const nuevoProducto = {
        nombre: nombre,
        descripcion: description,
        precio: precio,
        imagen: imagen || "",
        stock: 10, 
        categoria: {
            idCategoria: 0,
            nombreCategoria: nombreCategoriaSeleccionada
        }
    };

    fetch(`${API}/producto`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(nuevoProducto)
    })
    .then(async r => {
        if (!r.ok) {
            const textoError = await r.text();
            throw new Error(textoError);
        }
        return r.text();
    })
    .then(() => {
        alert("¡Excelente! Producto añadido al catálogo.");
        document.getElementById("prodNombre").value = "";
        document.getElementById("prodDescripcion").value = "";
        document.getElementById("prodPrecio").value = "";
        document.getElementById("prodImagen").value = "";
        cancelarCrearProducto();
        cargarProductos(); 
    })
    .catch(err => {
        alert("No se pudo crear el producto: " + err.message);
    });
}

function cancelarCrearProducto() {
    document.getElementById('vistaCrearProducto').classList.add('hidden'); 
    document.getElementById('vistaTienda').classList.remove('hidden'); 
}

function modificarProducto(idProducto) {
    document.getElementById('vistaTienda').classList.add('hidden'); 
    document.getElementById('vistaModificarProducto').classList.remove('hidden'); 

    const selectCategoria = document.getElementById("modProdCategoria");
    if (!selectCategoria) return;

    selectCategoria.innerHTML = "<option value=''>Cargando categorías...</option>";

    fetch(`${API}/categoria`)
        .then(r => r.json())
        .then(categorias => {
            let optionsHtml = "<option value=''>-- Selecciona una categoría --</option>";
            categorias.forEach(c => {
                if (!c) return;
                const nombreCat = c.nombre || c.Nombre || c.nombreCategoria || c.NombreCategoria || "Sin Nombre";
                optionsHtml += `<option value="${nombreCat}">${nombreCat}</option>`;
            });
            selectCategoria.innerHTML = optionsHtml;

            return fetch(`${API}/producto/${idProducto}`);
        })
        .then(r => {
            if (!r.ok) throw new Error("No se pudo obtener el producto.");
            return r.json();
        })
        .then(producto => {
            document.getElementById("modProdId").value = producto.idProducto || producto.IdProducto;
            document.getElementById("modProdNombre").value = producto.nombre || producto.Nombre || "";
            document.getElementById("modProdDescripcion").value = producto.descripcion || producto.Descripcion || "";
            document.getElementById("modProdPrecio").value = producto.precio !== undefined ? producto.precio : producto.Precio;
            document.getElementById("modProdStock").value = producto.stock !== undefined ? producto.stock : producto.Stock;
            document.getElementById("modProdImagen").value = producto.imagen || producto.Imagen || "";
            
            const catNombre = producto.categoria ? (producto.categoria.nombreCategoria || producto.categoria.NombreCategoria || "") : "";
            selectCategoria.value = catNombre;
        })
        .catch(err => {
            console.error("Error cargando los datos del producto:", err);
            alert("No se pudo cargar la información: " + err.message);
            cancelarModificarProducto();
        });
}

function ejecutarModificarProducto() {
    const id = parseInt(document.getElementById("modProdId").value);
    const nombre = document.getElementById("modProdNombre").value.trim();
    const descripcion = document.getElementById("modProdDescripcion").value.trim();
    const precio = parseFloat(document.getElementById("modProdPrecio").value) || 0;
    const stock = parseInt(document.getElementById("modProdStock").value) || 0;
    const imagen = document.getElementById("modProdImagen").value.trim();
    const nombreCategoriaSeleccionada = document.getElementById("modProdCategoria").value;

    if (!nombre) return alert("El nombre del producto es obligatorio.");
    if (!nombreCategoriaSeleccionada) return alert("Por favor, selecciona una categoría.");

    const productoActualizado = {
        IdProducto: id,
        Nombre: nombre,
        Descripcion: descripcion,
        Precio: precio,
        Stock: stock,
        Imagen: imagen,
        Categoria: {
            IdCategoria: 0,
            NombreCategoria: nombreCategoriaSeleccionada
        }
    };

    fetch(`${API}/producto`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(productoActualizado)
    })
    .then(async r => {
        if (!r.ok) {
            const textoError = await r.text();
            throw new Error(textoError);
        }
        return r.text();
    })
    .then(() => {
        alert("¡Producto modificado correctamente!");
        cancelarModificarProducto();
        cargarProductos(); 
    })
    .catch(err => {
        alert("No se pudo actualizar el producto: " + err.message);
    });
}

function cancelarModificarProducto() {
    document.getElementById('vistaModificarProducto').classList.add('hidden'); 
    document.getElementById('vistaTienda').classList.remove('hidden'); 
}

/* =======================================================
   CARRITO DE COMPRA (OPERACIONES PARCHADAS E INTEGRACIÓN)
======================================================= */

function cargarCarrito() {
    if (!usuarioId || esAdministrador) return;

    fetch(`${API}/carrito/${usuarioId}`)
        .then(async r => {
            // Manejamos el texto plano del error 404 seguro para evitar que rompa r.json()
            if (r.status === 404) {
                const mensajeError = await r.text();
                console.log("Backend responde:", mensajeError);
                document.getElementById("carrito").innerHTML = "<p class='text-muted'>El carrito está vacío</p>";
                return null;
            }
            if (!r.ok) throw new Error("Error obteniendo el carrito.");
            return r.json();
        })
        .then(data => {
            if (!data) return;

            const lista = data.listaProductos || data.ListaProductos || [];
            
            if (lista.length === 0) {
                document.getElementById("carrito").innerHTML = "<p class='text-muted'>El carrito está vacío</p>";
                return;
            }

            let html = "";
            lista.forEach((p) => {
                const nombreP = p.nombre || p.Nombre || "Accesorio";
                const precioP = p.precio !== undefined ? p.precio : p.Precio;
                const idP = p.idProducto !== undefined ? p.idProducto : p.IdProducto;

                html += `
                <div class="carrito-item" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; background: #f8fafc; padding: 8px; border-radius: 6px; border: 1px solid #e2e8f0;">
                    <span style="font-size: 0.9rem; font-weight: 500;">📌 ${nombreP}</span>
                    <div style="display: flex; align-items: center; gap: 10px;">
                        <b style="color: #1e293b; font-size: 0.9rem;">$${precioP}</b>
                        <button onclick="eliminarDelCarrito(${idP})" style="background: none; border: none; color: #ef4444; cursor: pointer; font-weight: bold; font-size: 16px; padding: 0 4px;">×</button>
                    </div>
                </div>`;
            });
            
            const totalC = data.totalCompra !== undefined ? data.totalCompra : data.TotalCompra;
            html += `<h3 class="total" style="margin-top: 15px; text-align: right; color: #0f172a; font-size: 1.1rem; font-weight: 700;">Total: $${totalC}</h3>`;
            document.getElementById("carrito").innerHTML = html;
        })
        .catch(err => {
            console.error(err);
            document.getElementById("carrito").innerHTML = "<p class='text-muted'>Error al cargar el carrito.</p>";
        });
}

function agregarCarrito(idProducto) {
    if (!usuarioId) return alert("Por favor, inicia sesión para añadir productos.");
    
    // Pasamos parámetros por QueryString como los espera CarritoController en C#
    fetch(`${API}/carrito/agregar?idUsuario=${usuarioId}&idProducto=${idProducto}&cantidad=1`, { 
        method: "POST" 
    })
    .then(async res => {
        const txt = await res.text(); 
        if (!res.ok) throw new Error(txt);
        alert(txt); // Imprime: "Producto agregado al carrito"
        cargarCarrito();
    })
    .catch(err => alert("Error: " + err.message));
}

function eliminarDelCarrito(idProducto) {
    if (!usuarioId) return;

    fetch(`${API}/carrito/eliminar?idUsuario=${usuarioId}&idProducto=${idProducto}`, {
        method: "DELETE"
    })
    .then(async res => {
        const txt = await res.text();
        if (!res.ok) throw new Error(txt);
        alert(txt); // Imprime: "Producto eliminado del carrito"
        cargarCarrito();
    })
    .catch(err => alert("Error: " + err.message));
}

function vaciarCarrito() {
    if (!usuarioId) return;
    if (!confirm("¿Deseas vaciar por completo tu carrito de compras?")) return;

    fetch(`${API}/carrito/vaciar/${usuarioId}`, { 
        method: "DELETE" 
    })
    .then(async res => {
        const txt = await res.text();
        if (!res.ok) throw new Error(txt);
        alert(txt); // Imprime: "Carrito vaciado"
        cargarCarrito();
    })
    .catch(err => alert("Error: " + err.message));
}

function comprar() {
    if (!usuarioId) return;

    fetch(`${API}/pedido/comprar/${usuarioId}`, { 
        method: "POST" 
    })
    .then(async r => {
        const msg = await r.text();
        if (!r.ok) throw new Error(msg);
        return msg;
    })
    .then(msg => {
        alert(msg); // Imprime: "Compra realizada correctamente. Pedido #X"
        cargarCarrito(); 
    })
    .catch(err => alert("Error al procesar compra: " + err.message));
}

/* ==========================================
   CATEGORÍAS
========================================== */

function cargarCategorias() {
    fetch(`${API}/categoria`)
        .then(r => r.json())
        .then(data => {
            const contenedor = document.getElementById("categorias");
            if (!contenedor) return;

            let html = "";
            data.forEach(c => {
                if (!c) return;
                
                const idCat = c.idCategoria !== undefined ? c.idCategoria : c.IdCategoria;
                const nombreCat = c.nombreCategoria || c.NombreCategoria || c.nombre || c.Nombre || "Sin Nombre";

                const botonX = esAdministrador 
                    ? `<button onclick="eliminarCategoriaSegura(${idCat})" style="background: none; border: none; margin-left: 6px; color: #dc3545; cursor: pointer; font-weight: bold; padding: 0; font-size: 14px; display: inline-block; line-height: 1;">×</button>`
                    : "";

                html += `
                <span class="categoria-tag">
                    ${nombreCat}
                    ${botonX}
                </span>`;
            });

            contenedor.innerHTML = html;
        })
        .catch(err => console.error("Error cargando categorías:", err));
}

async function eliminarCategoriaSegura(idCategoria) {
    const confirmar = confirm("¿Eliminar esta categoría?");
    if (!confirmar) return;

    try {
        const res = await fetch(`${API}/categoria/${idCategoria}`, { method: "DELETE" });
        if (!res.ok) throw new Error("No se pudo eliminar la categoría.");

        alert("Categoría eliminada correctamente");
        cargarCategorias();
        cargarProductos();
    } catch (error) {
        alert(error.message);
    }
}

function agregarCategoriaPrompt() {
    const nuevaCat = prompt("Nombre de la nueva categoría:");
    if(!nuevaCat) return;
    
    const nuevaCategoriaPayload = { NombreCategoria: nuevaCat };

    fetch(`${API}/categoria`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(nuevaCategoriaPayload) 
    })
    .then(async res => {
        if (!res.ok) {
            const errText = await res.text();
            throw new Error(errText);
        }
        return res.text();
    })
    .then(() => {
        alert("¡Categoría añadida con éxito!");
        cargarCategorias(); 
    })
    .catch(err => {
        alert("No se pudo guardar la categoría: " + err.message);
    });
}

function eliminarProducto(idProducto) {
    if(!confirm("¿Deseas eliminar este producto?")) return;
    fetch(`${API}/producto/${idProducto}`, { method: "DELETE" })
    .then(() => {
        alert("Producto eliminado del catálogo");
        cargarProductos();
    });
}

/* ==========================================
   SOPORTE Y PEDIDOS ADMIN
========================================== */

function crearSoporte() {
    const inputMsg = document.getElementById("msgSoporte");
    const mensajeTexto = inputMsg.value.trim();

    if (!mensajeTexto) return alert("Por favor escribe un mensaje antes de enviar.");
    if (!usuarioId) return alert("Debes iniciar sesión para enviar soporte.");

    const nuevoSoporte = {
        IdUsuario: usuarioId,
        Mensaje: mensajeTexto
    };

    fetch(`${API}/soporte`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(nuevoSoporte)
    })
    .then(async r => {
        if (!r.ok) {
            const txt = await r.text();
            throw new Error(txt);
        }
        return r.text();
    })
    .then(() => {
        alert("¡Mensaje enviado con éxito!");
        inputMsg.value = ""; 
    })
    .catch(err => {
        alert("No se pudo enviar el soporte: " + err.message);
    });
}

function cargarSoportesAdmin() {
    const contenedorSoporte = document.getElementById("listaSoportesAdmin");
    if (!contenedorSoporte) return; 

    fetch(`${API}/soporte`)
        .then(r => {
            if (!r.ok) throw new Error("Error en servidor de soporte");
            return r.json();
        })
        .then(data => {
            if (!Array.isArray(data)) return;

            let html = "";
            const soportesActivos = data.filter(s => {
                if (!s) return false;
                const estadoPlano = s.EstadoSolicitud || s.estadoSolicitud || s.estado || s.Estado || "";
                return estadoPlano.toString().toLowerCase() !== "resuelto";
            });

            if (soportesActivos.length === 0) { 
                contenedorSoporte.innerHTML = "<p class='text-muted'>🎉 ¡Bandeja limpia! No hay dudas pendientes.</p>";
                return;
            }
            
            soportesActivos.forEach(soporte => {
                const idSoporte = soporte.idSoporte !== undefined ? soporte.idSoporte : soporte.IdSoporte;
                const mensaje = soporte.mensaje || soporte.Mensaje || "Sin mensaje";
                
                html += `
                <div style="display: flex; justify-content: space-between; align-items: center; padding: 12px; margin-bottom: 8px; border-bottom: 1px solid #e2e8f0; background: #ffffff; border-radius: 4px; box-shadow: 0 1px 3px rgba(0,0,0,0.05);">
                    <span style="color: #334155; font-weight: 500;">💬 ${mensaje}</span>
                    <button style="background-color: #dc2626; color: white; border: none; padding: 6px 12px; border-radius: 4px; cursor: pointer; font-size: 0.75rem; font-weight: bold;" onclick="archivarSoporte(${idSoporte})">❌ Archivar</button>
                </div>`;
            });
            
            contenedorSoporte.innerHTML = html;
        })
        .catch(err => {
            console.error(err);
            contenedorSoporte.innerHTML = "<p class='text-muted'>Error al cargar bandeja de soporte.</p>";
        });
}

function archivarSoporte(idSoporte) {
    if (idSoporte === undefined || idSoporte === null) {
        return alert("Error: El mensaje de soporte no tiene un ID asignado.");
    }
    if (!confirm(`¿Deseas marcar la solicitud #${idSoporte} como resuelta?`)) return;

    fetch(`${API}/soporte/estado/${idSoporte}?estado=Resuelto`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" }
    })
    .then(async r => {
        if (!r.ok) {
            const errorTexto = await r.text();
            throw new Error(errorTexto);
        }
        return r.text();
    })
    .then(() => {
        alert("¡Solicitud archivada con éxito!");
        cargarSoportesAdmin(); 
    })
    .catch(err => {
        alert("No se pudo cambiar el estado: " + err.message);
    });
}

function verPedidos() {
    fetch(`${API}/pedido`)
        .then(r => r.json())
        .then(data => {
            let html = "";
            if(data.length === 0) { html = "<p class='text-muted'>No hay pedidos registrados.</p>"; }
            data.forEach(pedido => {
                const idPed = pedido.idPedido || pedido.IdPedido;
                const totalPed = pedido.totalPedido !== undefined ? pedido.totalPedido : pedido.TotalPedido;
                const estPed = pedido.estadoPedido || pedido.EstadoPedido || "Pagado";
                html += `
                <div class="admin-list-item" style="padding: 8px; border-bottom: 1px solid #eee;">
                    <p><b>Pedido ID:</b> #${idPed} | <b>Total:</b> $${totalPed} | <b>Estado:</b> ${estPed}</p>
                </div>`;
            });
            document.getElementById("listaPedidosAdmin").innerHTML = html;
        });
}

function buscarPedidoPorCorreo() {
    const correo = document.getElementById("searchPedidoCorreo").value;
    if(!correo) return alert("Escribe un correo electrónico");

    fetch(`${API}/usuario/correo/${encodeURIComponent(correo)}`)
        .then(r => {
            if(!r.ok) throw new Error("Usuario no encontrado");
            return r.json();
        })
        .then(usuario => {
            let html = `<h4>Pedidos de: ${usuario.nombre || usuario.Nombre}</h4>`;
            const pedidos = usuario.historialCompras || usuario.HistorialCompras || [];
            if(pedidos.length === 0) { html += "<p class='text-muted'>Este cliente no tiene compras.</p>"; }
            pedidos.forEach(p => {
                const idP = p.idPedido || p.IdPedido;
                const totalP = p.totalPedido !== undefined ? p.totalPedido : p.TotalPedido;
                html += `<div class="admin-list-item"><p><b>Pedido #:</b> ${idP} | <b>Total:</b> $${totalP}</p></div>`;
            });
            document.getElementById("listaPedidosAdmin").innerHTML = html;
        })
        .catch(err => alert(err.message));
}