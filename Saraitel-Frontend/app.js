// Si la tienda la sirve la propia API (https://localhost:7021), se usa esa
// misma dirección; si se abre con Live Server, se apunta a la API local.
const API = location.protocol.startsWith("http") && ["7021", "5000"].includes(location.port)
    ? `${location.origin}/api`
    : "https://localhost:7021/api";

let usuarioId = null;
let usuarioNombre = "";
let esAdministrador = false; 

// Cada inicio/cierre de sesión cambia este número. Si una petición vieja
// responde tarde (de la cuenta anterior), se ignora su resultado.
let sesionActual = 0;

/* ==========================================
   ÍCONOS BLANCOS (SVG, toman el color del texto)
========================================== */

const ICONOS = {
    user: '<circle cx="12" cy="8" r="4"/><path d="M4 21c0-4 3.6-7 8-7s8 3 8 7"/>',
    cart: '<circle cx="9" cy="20" r="1.4"/><circle cx="18" cy="20" r="1.4"/><path d="M2 3h3l2.6 12.2a2 2 0 0 0 2 1.6h8.2a2 2 0 0 0 2-1.5L22 8H6.2"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    x: '<path d="M18 6 6 18M6 6l12 12"/>',
    chart: '<path d="M3 3v18h18"/><path d="M8 17v-5M13 17V8M18 17v-9"/>',
    package: '<path d="M21 8 12 3 3 8v8l9 5 9-5z"/><path d="m3 8 9 5 9-5M12 13v8"/>',
    wrench: '<path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.8-3.8a6 6 0 0 1-7.9 7.9l-6.9 6.9a2.1 2.1 0 0 1-3-3l6.9-6.9a6 6 0 0 1 7.9-7.9z"/>',
    edit: '<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z"/>',
    trash: '<path d="M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6M10 11v6M14 11v6"/>',
    tag: '<path d="M20.6 13.4l-7.2 7.2a2 2 0 0 1-2.8 0L3 13V3h10l7.6 7.6a2 2 0 0 1 0 2.8z"/><circle cx="7.5" cy="7.5" r="1.5"/>',
    arrowLeft: '<path d="M19 12H5M12 19l-7-7 7-7"/>',
    arrowRight: '<path d="M5 12h14M12 5l7 7-7 7"/>',
    logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9"/>',
    chevron: '<path d="m6 9 6 6 6-6"/>',
    check: '<circle cx="12" cy="12" r="10"/><path d="m8 12.5 2.8 2.8L16.5 9"/>',
    info: '<circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/>',
    clock: '<circle cx="12" cy="12" r="10"/><path d="M12 7v5l3 2"/>',
    xCircle: '<circle cx="12" cy="12" r="10"/><path d="m15 9-6 6M9 9l6 6"/>',
    lock: '<rect x="4" y="11" width="16" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
    download: '<path d="M12 3v12M7 10l5 5 5-5"/><path d="M5 21h14"/>',
    mail: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/>',
    chevronRight: '<path d="m9 6 6 6-6 6"/>'
};

/* ==========================================
   FORMATO DE PRECIOS:  $ 164.779
========================================== */

const FORMATO_COP = new Intl.NumberFormat("es-CO", {
    style: "currency",
    currency: "COP",
    minimumFractionDigits: 0,
    maximumFractionDigits: 0
});

function formatearPrecio(valor) {
    return FORMATO_COP.format(Number(valor) || 0);
}

// Para mostrar texto escrito por el usuario sin que se interprete como HTML
function escaparHTML(texto) {
    return String(texto ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");
}

function svgIcono(nombre) {
    return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICONOS[nombre] || ""}</svg>`;
}

function icono(nombre) {
    return `<span class="ico">${svgIcono(nombre)}</span>`;
}

// Pinta los íconos escritos en el HTML como <span class="ico" data-icon="...">
function pintarIconos() {
    document.querySelectorAll(".ico[data-icon]").forEach(el => {
        el.innerHTML = svgIcono(el.dataset.icon);
    });
}

/* ==========================================
   CAMBIO DE VISTAS
========================================== */

const VISTAS = [
    "vistaAuth", "vistaTienda", "vistaPerfil", "vistaDetalleProducto",
    "vistaCrearProducto", "vistaModificarProducto", "vistaCarrito",
    "vistaCheckout", "vistaConfirmacion", "vistaMisPedidos", "vistaPedido",
    "vistaGestionPedidos"
];

// Muestra una sola vista y oculta todas las demás
function mostrarVista(id) {
    VISTAS.forEach(v => {
        const el = document.getElementById(v);
        if (el) el.classList.toggle("hidden", v !== id);
    });

    // El header con el usuario y el carrito solo existe con sesión iniciada
    document.getElementById("headerTienda").classList.toggle("hidden", id === "vistaAuth");

    cerrarMenus();
    cerrarPanelCarrito();
    cerrarModal();
    window.scrollTo(0, 0);
}

function irATienda() {
    productoDetalleId = null;
    mostrarVista("vistaTienda");
}

/* ==========================================
   MENÚS DESPLEGABLES DEL HEADER
========================================== */

function toggleMenu(idMenu, evento) {
    if (evento) evento.stopPropagation();

    const menu = document.getElementById(idMenu);
    const abrir = menu.classList.contains("hidden");

    cerrarMenus();

    if (abrir) {
        menu.classList.remove("hidden");
        const boton = menu.previousElementSibling;
        if (boton) boton.setAttribute("aria-expanded", "true");
    }
}

function cerrarMenus() {
    document.querySelectorAll(".dropdown").forEach(m => m.classList.add("hidden"));
    document.querySelectorAll("[aria-expanded]").forEach(b => b.setAttribute("aria-expanded", "false"));
}

// Clic por fuera de un menú, o tecla Escape: se cierran
document.addEventListener("click", e => {
    if (!e.target.closest(".menu-wrap")) cerrarMenus();
});
document.addEventListener("keydown", e => {
    if (e.key === "Escape") {
        cerrarMenus();
        cerrarPanelCarrito();
        cerrarModal();
    }
});

/* ==========================================
   REINICIO DE ESTADO AL CAMBIAR DE SESIÓN
   Todo vuelve a su posición inicial para que una cuenta
   nunca vea lo que dejó abierto la anterior.
========================================== */

function limpiarCampos(ids) {
    ids.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = "";
    });
}

function reiniciarEstadoSesion() {
    sesionActual++;

    // Catálogo, búsqueda y paginación
    productosCatalogo = [];
    paginaActual = 1;
    ultimaBusqueda = "";
    categoriaSeleccionada = null;
    document.getElementById("categoriasTienda").innerHTML = "";
    productoDetalleId = null;
    cantidadSeleccionada = {};
    document.getElementById("inputBusquedaProducto").value = "";
    document.getElementById("barraBusqueda").classList.add("hidden");
    document.getElementById("productos").innerHTML = "";
    document.getElementById("paginacion").innerHTML = "";
    document.getElementById("detalleProducto").innerHTML = "";

    // Carrito (también los cambios de cantidad que estaban por enviarse)
    Object.values(temporizadoresCantidad).forEach(t => clearTimeout(t));
    temporizadoresCantidad = {};
    unidadesCarrito = {};
    carritoAgrupado = [];
    carritoTotal = 0;
    renderizarCarrito();

    // Checkout y confirmación
    checkoutPrellenado = false;
    document.getElementById("camposEnvio").innerHTML = "";
    document.getElementById("camposFacturacion").innerHTML = "";
    document.querySelector('input[name="facturacion"][value="igual"]').checked = true;
    document.getElementById("checkoutCorreo").innerText = "";
    document.getElementById("checkoutAvatar").innerText = "";
    document.getElementById("checkoutError").classList.add("hidden");
    document.getElementById("confirmacionContenido").innerHTML = "";
    detenerCuentaRegresiva();
    pedidoEnPantalla = null;
    misPedidos = [];
    filtroPedidos = "todos";
    paginaPedidos = 1;
    document.getElementById("listaMisPedidos").innerHTML = "";
    document.getElementById("filtrosPedidos").innerHTML = "";
    document.getElementById("paginacionPedidos").innerHTML = "";
    document.getElementById("pedidoContenido").innerHTML = "";
    cerrarModal();

    // Panel de administrador
    pedidosAdmin = [];
    filtroPedidosAdmin = "porEnviar";
    paginaPedidosAdmin = 1;
    document.getElementById("listaPedidosAdmin").innerHTML = "";
    document.getElementById("filtrosPedidosAdmin").innerHTML = "";
    document.getElementById("paginacionPedidosAdmin").innerHTML = "";
    document.getElementById("avisoAdminPedidos").classList.add("hidden");
    document.getElementById("pedidosAdminBadge").classList.add("hidden");
    document.getElementById("categorias").innerHTML = "";
    limpiarCampos(["buscarPedidoAdmin"]);

    // Formularios de producto y perfil
    limpiarCampos([
        "prodNombre", "prodDescripcion", "prodPrecio", "prodImagen",
        "modProdId", "modProdNombre", "modProdDescripcion", "modProdPrecio", "modProdStock", "modProdImagen",
        "perfNombre", "perfCorreo", "perfTelefono", "perfDireccion", "perfRol"
    ]);
    setModoEdicionPerfil(false);

    // Formularios de acceso (login, registro, recuperar contraseña)
    limpiarCampos([
        "correo", "contrasena",
        "regNombre", "regCorreo", "regTelefono", "regPassword", "regDireccion",
        "recCorreo", "resCorreo", "resCodigo", "resPassword", "resPasswordConfirmar"
    ]);
    document.getElementById("avisoCodigo").classList.add("hidden");
    volverAInicio();

    cerrarMenus();
    cerrarPanelCarrito();
    window.scrollTo(0, 0);
}

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
}

function configurarInterfazPorRol() {
    const componentsCliente = document.getElementById("menuCarritoWrap");
    const panelAdministrador = document.getElementById("panelAdministrador");
    const btnCrearProductoAdmin = document.getElementById("btnCrearProductoAdmin");
    const btnCrearCategoriaAdmin = document.getElementById("btnCrearCategoriaAdmin");

    // HU-18: el administrador no compra, así que no tiene "Mis pedidos";
    // en lugar del carrito tiene la caja que lleva a la gestión de pedidos
    document.getElementById("opcionMisPedidos").classList.toggle("hidden", esAdministrador);
    document.getElementById("menuPedidosAdminWrap").classList.toggle("hidden", !esAdministrador);

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
    if (esAdministrador) cargarPedidosAdmin();
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

        // Se parte de cero: nada de la sesión anterior queda visible
        reiniciarEstadoSesion();

        usuarioId = data.idUsuario;
        usuarioNombre = data.nombre;

        // Detectar si es admin
        esAdministrador =
            data.rol.toLowerCase() === "administrador";

        document.getElementById("userInfo").innerText = usuarioNombre;

        mostrarVista("vistaTienda");

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

    reiniciarEstadoSesion();
    configurarInterfazPorRol();
    mostrarVista("vistaAuth");
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

            mostrarVista("vistaPerfil");
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
        document.getElementById("userInfo").innerText = usuarioNombre;

        abrirPerfil();
    })
    .catch(err => alert("No se pudo actualizar el perfil: " + err.message));
}

function cerrarPerfil() {
    irATienda();
}

/* ==========================================
   RECUPERAR ACCESO (HU-06 y HU-07)
========================================== */

function solicitarRecuperacion() {
    const correo = document.getElementById("recCorreo").value.trim();

    if (!correo) return alert("Escribe el correo de tu cuenta.");

    const boton = document.getElementById("btnEnviarCodigo");
    boton.disabled = true;
    boton.innerText = "Enviando...";

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
        aviso.innerText = `${data.mensaje} Revisa tu bandeja de entrada y la carpeta de spam. El código vence en 15 minutos.`;

        mostrarFormulario('restablecer');
    })
    .catch(err => alert("No se pudo solicitar la recuperación: " + err.message))
    .finally(() => {
        boton.disabled = false;
        boton.innerText = "Enviar código";
    });
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

let productosCatalogo = [];
let productoDetalleId = null;   // producto que se está viendo en la página de detalle
let unidadesCarrito = {};      // idProducto -> unidades que el cliente ya tiene en su carrito
let cantidadSeleccionada = {}; // idProducto -> cantidad elegida en el selector - 1 +

// Paginación de la grilla: máximo 50 productos por página
const PRODUCTOS_POR_PAGINA = 50;
let paginaActual = 1;
let ultimaBusqueda = "";

// Categoría elegida en las etiquetas de la tienda (null = todas)
let categoriaSeleccionada = null;

// La paginación de "Mis pedidos" reutiliza el cálculo de los números
window.paginaActualTienda = () => paginaActual;

function cargarProductos() {
    const sesion = sesionActual;

    fetch(`${API}/producto`)
        .then(r => r.json())
        .then(data => {
            if (sesion !== sesionActual) return; // respuesta de otra sesión
            productosCatalogo = data;
            renderizarCategoriasTienda();
            filtrarProductos(); // respeta lo que haya escrito en la barra de búsqueda
            renderizarCarrito(); // precios y stock actuales en el carrito
        })
        .catch(err => console.error("Error cargando catálogo de productos:", err));
}

/* ---------- Búsqueda de productos (HU-09) ---------- */

// Quita mayúsculas, tildes y espacios de los extremos para comparar
function normalizarTexto(texto) {
    return (texto || "")
        .toString()
        .toLowerCase()
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .trim();
}

function toggleBusquedaProductos() {
    const barra = document.getElementById("barraBusqueda");

    if (barra.classList.contains("hidden")) {
        barra.classList.remove("hidden");
        document.getElementById("inputBusquedaProducto").focus();
    } else {
        cerrarBusquedaProductos();
    }
}

function cerrarBusquedaProductos() {
    document.getElementById("inputBusquedaProducto").value = "";
    document.getElementById("barraBusqueda").classList.add("hidden");
    filtrarProductos();
}

function filtrarProductos() {
    const input = document.getElementById("inputBusquedaProducto");
    const texto = normalizarTexto(input ? input.value : "");

    // Se combinan la búsqueda por nombre y la categoría elegida
    const lista = productosCatalogo.filter(p =>
        (texto === "" || normalizarTexto(p.nombre || p.Nombre).includes(texto)) &&
        (categoriaSeleccionada === null || categoriaDe(p) === categoriaSeleccionada));

    // Si cambió la búsqueda o la categoría, se vuelve a la primera página
    const filtroActual = `${texto}|${categoriaSeleccionada ?? ""}`;
    if (filtroActual !== ultimaBusqueda) {
        paginaActual = 1;
        ultimaBusqueda = filtroActual;
    }

    // Se asegura de que la página actual exista (por ejemplo, si se eliminaron productos)
    const totalPaginas = Math.max(1, Math.ceil(lista.length / PRODUCTOS_POR_PAGINA));
    if (paginaActual > totalPaginas) paginaActual = totalPaginas;

    const inicio = (paginaActual - 1) * PRODUCTOS_POR_PAGINA;
    const pagina = lista.slice(inicio, inicio + PRODUCTOS_POR_PAGINA);

    renderizarProductos(pagina, texto !== "" || categoriaSeleccionada !== null);
    renderizarPaginacion(totalPaginas);

    // Si hay un detalle abierto, se actualiza con el stock más reciente
    if (productoDetalleId !== null) renderizarDetalleProducto();
}

/* ---------- Filtro por categoría (etiquetas deslizables) ---------- */

function categoriaDe(producto) {
    const categoria = producto.categoria || producto.Categoria;
    return categoria ? (categoria.nombreCategoria || categoria.NombreCategoria || "") : "";
}

// Etiquetas: "Todas" y cada categoría con productos, con su cantidad.
// Las categorías sin productos no se muestran.
function renderizarCategoriasTienda() {
    const carril = document.getElementById("categoriasTienda");
    const desplazamiento = carril.scrollLeft;

    const conteo = new Map();
    productosCatalogo.forEach(p => {
        const nombre = categoriaDe(p);
        if (nombre) conteo.set(nombre, (conteo.get(nombre) || 0) + 1);
    });

    // Si la categoría elegida se quedó sin productos, se vuelve a "Todas"
    if (categoriaSeleccionada !== null && !conteo.has(categoriaSeleccionada)) {
        categoriaSeleccionada = null;
    }

    const etiqueta = (nombre, cantidad, valor) => {
        const activa = valor === categoriaSeleccionada;
        return `<button class="etiqueta-categoria${activa ? " activa" : ""}" role="tab" aria-selected="${activa}"
                    data-categoria="${valor === null ? "" : escaparHTML(valor)}"
                    onclick="elegirCategoria(this)">${escaparHTML(nombre)} <span>${cantidad}</span></button>`;
    };

    carril.innerHTML = etiqueta("Todas", productosCatalogo.length, null) +
        [...conteo.entries()].map(([nombre, cantidad]) => etiqueta(nombre, cantidad, nombre)).join("");

    carril.scrollLeft = desplazamiento; // no se pierde la posición al recargar
    actualizarFlechasCategorias();
}

function elegirCategoria(boton) {
    categoriaSeleccionada = boton.dataset.categoria === "" ? null : boton.dataset.categoria;

    document.querySelectorAll(".etiqueta-categoria").forEach(b => {
        const activa = b === boton;
        b.classList.toggle("activa", activa);
        b.setAttribute("aria-selected", activa);
    });
    boton.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "nearest" });

    filtrarProductos();
}

// Las flechas mueven las etiquetas casi un "carril" completo
function deslizarCategorias(direccion) {
    const carril = document.getElementById("categoriasTienda");
    carril.scrollBy({ left: direccion * carril.clientWidth * 0.8, behavior: "smooth" });
}

// Cada flecha se apaga cuando ya no hay más etiquetas hacia ese lado
function actualizarFlechasCategorias() {
    const carril = document.getElementById("categoriasTienda");
    const izq = document.getElementById("flechaCategoriasIzq");
    const der = document.getElementById("flechaCategoriasDer");
    const sobra = carril.scrollWidth - carril.clientWidth;

    izq.disabled = carril.scrollLeft <= 2;
    der.disabled = carril.scrollLeft >= sobra - 2;

    carril.classList.toggle("mas-izq", !izq.disabled);
    carril.classList.toggle("mas-der", !der.disabled);

    // Si todas caben, no hacen falta flechas
    const caben = sobra <= 2;
    izq.classList.toggle("hidden", caben);
    der.classList.toggle("hidden", caben);
}

document.getElementById("categoriasTienda").addEventListener("scroll", actualizarFlechasCategorias, { passive: true });
window.addEventListener("resize", actualizarFlechasCategorias);

/* ---------- Dibujar la grilla ---------- */

// Stock real del producto menos lo que el cliente ya tiene en su carrito
function disponibleDe(idProducto) {
    const p = productosCatalogo.find(x =>
        (x.idProducto !== undefined ? x.idProducto : x.IdProducto) === idProducto);

    if (!p) return 0;

    const stock = p.stock !== undefined ? p.stock : (p.Stock !== undefined ? p.Stock : 0);

    return esAdministrador
        ? stock
        : Math.max(0, stock - (unidadesCarrito[idProducto] || 0));
}

function renderizarProductos(lista, hayBusqueda) {
    let html = "";

    lista.forEach(p => {
        const urlImagen = p.imagen || p.Imagen || "";
        const urlFinal = urlImagen.trim() !== ""
            ? urlImagen
            : "https://images.unsplash.com/photo-1546868871-7041f2a55e12?w=500&q=80";

        const idProductoActual = p.idProducto !== undefined ? p.idProducto : p.IdProducto;
        const nombreProd = p.nombre || p.Nombre || "Producto sin nombre";
        const precioProd = p.precio !== undefined ? p.precio : p.Precio;

        // La grilla solo muestra imagen, nombre y precio; el resto está en el detalle
        html += `
        <div class="card-item card-clickable" onclick="verDetalleProducto(${idProductoActual})">
            <div class="producto-img-container">
                <img src="${urlFinal}" alt="${nombreProd}" class="producto-img">
            </div>
            <h3>${nombreProd}</h3>
            <b class="precio">${formatearPrecio(precioProd)}</b>
        </div>`;
    });

    if (html === "" && hayBusqueda) {
        html = categoriaSeleccionada !== null
            ? `<p class="sin-resultados">No hay productos de "${escaparHTML(categoriaSeleccionada)}" con ese nombre.</p>`
            : `<p class="sin-resultados">No se encontraron productos con ese nombre.</p>`;
    }

    document.getElementById("productos").innerHTML = html;
}

/* ---------- Paginación:  1  2  3  4  …  26  27  28  → ---------- */

// Decide qué números mostrar: los primeros, los últimos y los cercanos a la página actual
function paginasVisibles(totalPaginas, paginaActual = window.paginaActualTienda()) {
    if (totalPaginas <= 7) {
        return Array.from({ length: totalPaginas }, (_, i) => i + 1);
    }

    const numeros = new Set([1, totalPaginas]);
    for (let i = paginaActual - 1; i <= paginaActual + 1; i++) numeros.add(i);
    if (paginaActual <= 4) [2, 3, 4, 5].forEach(n => numeros.add(n));
    if (paginaActual >= totalPaginas - 3) {
        for (let i = totalPaginas - 4; i < totalPaginas; i++) numeros.add(i);
    }

    const ordenados = [...numeros]
        .filter(n => n >= 1 && n <= totalPaginas)
        .sort((a, b) => a - b);

    // Se insertan los "…" donde hay saltos
    const resultado = [];
    ordenados.forEach((n, i) => {
        if (i > 0) {
            const anterior = ordenados[i - 1];
            if (n - anterior === 2) resultado.push(anterior + 1);
            else if (n - anterior > 2) resultado.push("…");
        }
        resultado.push(n);
    });
    return resultado;
}

function renderizarPaginacion(totalPaginas) {
    const contenedor = document.getElementById("paginacion");
    if (!contenedor) return;

    // Con una sola página no hace falta mostrar los números
    if (totalPaginas <= 1) {
        contenedor.innerHTML = "";
        return;
    }

    let html = "";

    if (paginaActual > 1) {
        html += `<button class="pagina-btn pagina-flecha" onclick="irAPagina(${paginaActual - 1})" aria-label="Página anterior">${icono("arrowLeft")}</button>`;
    }

    paginasVisibles(totalPaginas).forEach(n => {
        if (n === "…") {
            html += `<span class="pagina-btn pagina-puntos">…</span>`;
        } else {
            const activa = n === paginaActual;
            html += `<button class="pagina-btn${activa ? " activa" : ""}" onclick="irAPagina(${n})" ${activa ? 'aria-current="page"' : ""}>${n}</button>`;
        }
    });

    if (paginaActual < totalPaginas) {
        html += `<button class="pagina-btn pagina-flecha" onclick="irAPagina(${paginaActual + 1})" aria-label="Página siguiente">${icono("arrowRight")}</button>`;
    }

    contenedor.innerHTML = html;
}

function irAPagina(numero) {
    paginaActual = numero;
    filtrarProductos();

    // Sube al inicio de la grilla para ver la nueva página desde arriba
    const grilla = document.getElementById("productos");
    if (grilla) grilla.closest(".card").scrollIntoView({ behavior: "smooth", block: "start" });
}

/* ---------- Página de detalle del producto (HU-12) ---------- */

function verDetalleProducto(idProducto) {
    productoDetalleId = idProducto;
    renderizarDetalleProducto();
    mostrarVista("vistaDetalleProducto");
}

function cerrarDetalleProducto() {
    irATienda();
}

function renderizarDetalleProducto() {
    const p = productosCatalogo.find(x =>
        (x.idProducto !== undefined ? x.idProducto : x.IdProducto) === productoDetalleId);

    // Si el producto ya no existe (por ejemplo, lo eliminaron), se vuelve a la tienda
    if (!p) {
        cerrarDetalleProducto();
        return;
    }

    const id = productoDetalleId;

    const urlImagen = p.imagen || p.Imagen || "";
    const urlFinal = urlImagen.trim() !== ""
        ? urlImagen
        : "https://images.unsplash.com/photo-1546868871-7041f2a55e12?w=500&q=80";

    const nombreProd = p.nombre || p.Nombre || "Producto sin nombre";
    const descProd = p.descripcion || p.Descripcion || "Este producto no tiene descripción.";
    const precioProd = p.precio !== undefined ? p.precio : p.Precio;

    const categoria = p.categoria || p.Categoria;
    const nombreCategoria = categoria
        ? (categoria.nombreCategoria || categoria.NombreCategoria || "Sin categoría")
        : "Sin categoría";

    const stockReal = p.stock !== undefined ? p.stock : (p.Stock !== undefined ? p.Stock : 0);
    const disponible = disponibleDe(id);
    const claseStock = disponible <= 3 ? "detalle-stock stock-bajo" : "detalle-stock";

    let acciones;
    if (esAdministrador) {
        acciones = `
        <div class="detalle-acciones-admin">
            <button class="btn-dark-blue" style="flex: 1;" onclick="modificarProducto(${id})">${icono("edit")} Modificar</button>
            <button class="btn-danger" style="flex: 1;" onclick="eliminarProducto(${id})">${icono("trash")} Eliminar</button>
        </div>`;
    } else {
        let boton;
        if (disponible <= 0) {
            // Sin stock: "Agotado", o el botón normal en gris si todo el stock ya está en el carrito
            const texto = stockReal <= 0 ? "Agotado" : "Agregar al Carrito";
            boton = `<button class="btn-add" style="width:100%" disabled>${texto}</button>`;
        } else {
            boton = `<button class="btn-add" style="width:100%" onclick="agregarCarrito(${id})">Agregar al Carrito</button>`;
        }

        acciones = `
        <div class="detalle-acciones">
            ${generarSelectorCantidad(id, disponible)}
            ${boton}
        </div>`;
    }

    document.getElementById("detalleProducto").innerHTML = `
    <section class="card detalle-producto">
        <button class="btn-secondary" onclick="cerrarDetalleProducto()">${icono("arrowLeft")} Volver a la tienda</button>

        <div class="detalle-grid">
            <div class="detalle-img">
                <img src="${urlFinal}" alt="${nombreProd}">
            </div>

            <div class="detalle-info">
                <span class="detalle-categoria">${icono("tag")} ${nombreCategoria}</span>
                <h2>${nombreProd}</h2>
                <b class="detalle-precio">${formatearPrecio(precioProd)}</b>
                <p class="detalle-descripcion">${descProd}</p>
                <span class="${claseStock}">Stock: ${disponible}</span>
                ${acciones}
            </div>
        </div>
    </section>`;
}

/* ---------- Selector de cantidad:  -  1  +  ---------- */

function generarSelectorCantidad(idProducto, disponible) {
    let cantidad = cantidadSeleccionada[idProducto] || 1;

    if (disponible <= 0) cantidad = 0;
    else if (cantidad > disponible) cantidad = disponible;

    cantidadSeleccionada[idProducto] = cantidad;

    const sinStock = disponible <= 0;

    return `
    <div class="cantidad-selector">
        <span class="cantidad-label">Cantidad</span>
        <div class="stepper">
            <button class="stepper-btn" id="menos-${idProducto}" onclick="cambiarCantidad(${idProducto}, -1)" ${cantidad <= 1 ? "disabled" : ""}>−</button>
            <input class="stepper-input" id="qty-${idProducto}" type="text" inputmode="numeric" value="${cantidad}" ${sinStock ? "disabled" : ""}
                   oninput="this.value = this.value.replace(/[^0-9]/g, '')"
                   onchange="confirmarCantidad(${idProducto})"
                   onkeydown="if (event.key === 'Enter') { this.blur(); }">
            <button class="stepper-btn" id="mas-${idProducto}" onclick="cambiarCantidad(${idProducto}, 1)" ${cantidad >= disponible ? "disabled" : ""}>+</button>
        </div>
    </div>`;
}

function actualizarStepper(idProducto) {
    const disponible = disponibleDe(idProducto);
    const cantidad = disponible <= 0 ? 0 : (cantidadSeleccionada[idProducto] || 1);

    const input = document.getElementById(`qty-${idProducto}`);
    const menos = document.getElementById(`menos-${idProducto}`);
    const mas = document.getElementById(`mas-${idProducto}`);

    if (!input || !menos || !mas) return;

    input.value = cantidad;
    menos.disabled = cantidad <= 1;
    mas.disabled = cantidad >= disponible;
}

function cambiarCantidad(idProducto, delta) {
    const disponible = disponibleDe(idProducto);
    let cantidad = (cantidadSeleccionada[idProducto] || 1) + delta;

    cantidad = Math.max(1, Math.min(cantidad, disponible));

    cantidadSeleccionada[idProducto] = cantidad;
    actualizarStepper(idProducto);
}

// Se ejecuta cuando el cliente escribe la cantidad y da Enter (o sale del campo)
async function confirmarCantidad(idProducto) {
    const input = document.getElementById(`qty-${idProducto}`);
    const cantidad = parseInt(input.value, 10);

    if (isNaN(cantidad) || cantidad < 1) {
        cantidadSeleccionada[idProducto] = 1;
        actualizarStepper(idProducto);
        return;
    }

    // Se consulta el stock real al servidor para no depender de datos viejos
    try {
        const r = await fetch(`${API}/producto/${idProducto}`);
        if (r.ok) {
            const actual = await r.json();
            const idx = productosCatalogo.findIndex(x =>
                (x.idProducto !== undefined ? x.idProducto : x.IdProducto) === idProducto);
            if (idx >= 0) productosCatalogo[idx] = actual;
        }
    } catch (e) {
        console.error("No se pudo consultar el stock actual:", e);
    }

    const p = productosCatalogo.find(x =>
        (x.idProducto !== undefined ? x.idProducto : x.IdProducto) === idProducto);

    if (!p) return;

    const nombre = p.nombre || p.Nombre || "el producto";
    const stockReal = p.stock !== undefined ? p.stock : (p.Stock !== undefined ? p.Stock : 0);
    const enCarrito = unidadesCarrito[idProducto] || 0;
    const disponible = Math.max(0, stockReal - enCarrito);

    if (cantidad > disponible) {
        let mensaje = `No se puede: excediste el stock disponible de "${nombre}". Stock disponible: ${stockReal}.`;

        if (enCarrito > 0) {
            mensaje += ` Ya tienes ${enCarrito} en tu carrito, puedes agregar máximo ${disponible} más.`;
        }

        alert(mensaje);
        cantidadSeleccionada[idProducto] = disponible;
    } else {
        cantidadSeleccionada[idProducto] = cantidad;
    }

    filtrarProductos(); // vuelve a dibujar la grilla con el stock actualizado
}

function crearProducto() {
    // Por seguridad, el formulario siempre se muestra al abrir esta página
    document.getElementById("formCrearProducto").classList.remove("hidden");
    mostrarVista("vistaCrearProducto");

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
    irATienda();
}

function modificarProducto(idProducto) {
    productoDetalleId = null;
    mostrarVista("vistaModificarProducto");

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
    irATienda();
}

/* =======================================================
   CARRITO DE COMPRA (OPERACIONES PARCHADAS E INTEGRACIÓN)
======================================================= */

// Carrito agrupado: una línea por producto con su cantidad
let carritoAgrupado = [];   // [{ id, nombre, precio, imagen, cantidad }]
let carritoTotal = 0;

const IMAGEN_POR_DEFECTO = "https://images.unsplash.com/photo-1546868871-7041f2a55e12?w=500&q=80";

function cargarCarrito() {
    if (!usuarioId || esAdministrador) return;

    const sesion = sesionActual;

    fetch(`${API}/carrito/${usuarioId}`)
        .then(async r => {
            // El backend responde 404 cuando el cliente aún no tiene carrito
            if (r.status === 404) return null;
            if (!r.ok) throw new Error("Error obteniendo el carrito.");
            return r.json();
        })
        .then(data => {
            if (sesion !== sesionActual) return; // respuesta de otra sesión

            const lista = data ? (data.listaProductos || data.ListaProductos || []) : [];

            // El backend guarda una entrada por unidad; aquí se agrupan por producto
            const grupos = new Map();
            lista.forEach(item => {
                const id = item.idProducto !== undefined ? item.idProducto : item.IdProducto;
                if (!grupos.has(id)) {
                    grupos.set(id, {
                        id,
                        nombre: item.nombre || item.Nombre || "Producto",
                        precio: Number(item.precio !== undefined ? item.precio : item.Precio) || 0,
                        imagen: (item.imagen || item.Imagen || "").trim() || IMAGEN_POR_DEFECTO,
                        cantidad: 0
                    });
                }
                grupos.get(id).cantidad++;
            });

            carritoAgrupado = [...grupos.values()];
            recalcularCarritoLocal();

            renderizarCarrito();
            filtrarProductos(); // actualiza el stock disponible en la grilla y el detalle
        })
        .catch(err => {
            console.error(err);
            if (sesion !== sesionActual) return;
            document.getElementById("miniCarritoLista").innerHTML =
                `<p class="carrito-vacio">Error al cargar el carrito.</p>`;
        });
}

// Stock real del producto según el catálogo (el carrito guarda copias viejas)
function stockDelProducto(idProducto) {
    const p = productosCatalogo.find(x =>
        (x.idProducto !== undefined ? x.idProducto : x.IdProducto) === idProducto);
    if (!p) return null;
    return p.stock !== undefined ? p.stock : (p.Stock !== undefined ? p.Stock : 0);
}

// Selector  −  n  +  de una línea del carrito
function selectorCantidadCarrito(g) {
    const stock = stockDelProducto(g.id);
    const maximo = stock === null ? g.cantidad : stock; // sin dato de stock no se deja subir

    let aviso = "";
    if (g.cantidad > maximo) aviso = `Solo hay ${maximo} disponibles`;
    else if (g.cantidad === maximo) aviso = "Máximo disponible";

    return `
    <div class="cantidad-carrito">
        <div class="stepper stepper-carrito">
            <button class="stepper-btn" onclick="ajustarCantidadCarrito(${g.id}, -1)" ${g.cantidad <= 1 ? "disabled" : ""} aria-label="Quitar una unidad">−</button>
            <input class="stepper-input" type="text" inputmode="numeric" value="${g.cantidad}" aria-label="Cantidad de ${g.nombre}"
                   oninput="this.value = this.value.replace(/[^0-9]/g, '')"
                   onchange="escribirCantidadCarrito(${g.id}, this.value)"
                   onkeydown="if (event.key === 'Enter') { this.blur(); }">
            <button class="stepper-btn" onclick="ajustarCantidadCarrito(${g.id}, 1)" ${g.cantidad >= maximo ? "disabled" : ""} aria-label="Agregar una unidad">+</button>
        </div>
        ${aviso ? `<span class="aviso-stock">${aviso}</span>` : ""}
    </div>`;
}

// El carrito guarda copias de los productos de cuando se agregaron.
// Se usan el nombre, la imagen y el precio actuales del catálogo, que son
// los mismos con los que el backend crea el pedido.
function sincronizarCarritoConCatalogo() {
    carritoAgrupado.forEach(g => {
        const p = productosCatalogo.find(x =>
            (x.idProducto !== undefined ? x.idProducto : x.IdProducto) === g.id);
        if (!p) return;
        g.nombre = p.nombre || p.Nombre || g.nombre;
        g.precio = Number(p.precio !== undefined ? p.precio : p.Precio) || g.precio;
        g.imagen = (p.imagen || p.Imagen || "").trim() || g.imagen;
    });
    recalcularCarritoLocal();
}

// Dibuja el contador del ícono, el panel lateral y la página del carrito
function renderizarCarrito() {
    sincronizarCarritoConCatalogo();

    // Si se está en el checkout, el resumen también se actualiza
    if (!document.getElementById("vistaCheckout").classList.contains("hidden")) {
        if (carritoAgrupado.length === 0) mostrarVista("vistaCarrito");
        else renderizarResumenCheckout();
    }

    const unidades = carritoAgrupado.reduce((suma, g) => suma + g.cantidad, 0);

    // Contador sobre el ícono
    const badge = document.getElementById("carritoBadge");
    badge.innerText = unidades > 99 ? "99+" : unidades;
    badge.classList.toggle("hidden", unidades === 0);

    document.getElementById("panelCarritoConteo").innerText =
        unidades > 0 ? `(${unidades})` : "";

    // ---- Panel lateral ----
    const panelLista = document.getElementById("miniCarritoLista");
    const panelPie = document.getElementById("panelCarritoPie");

    if (carritoAgrupado.length === 0) {
        panelLista.innerHTML = `
            <div class="carrito-vacio">
                ${icono("cart")}
                <p>Tu carrito está vacío</p>
                <button class="btn-dark-blue" onclick="irATienda()">Ver productos</button>
            </div>`;
        panelPie.classList.add("hidden");
    } else {
        panelLista.innerHTML = carritoAgrupado.map(g => `
            <div class="panel-item">
                <div class="carrito-foto panel-foto" onclick="verDetalleProducto(${g.id})">
                    <img src="${g.imagen}" alt="${g.nombre}">
                </div>
                <div class="panel-item-info">
                    <span class="panel-item-nombre" onclick="verDetalleProducto(${g.id})">${g.nombre}</span>
                    ${selectorCantidadCarrito(g)}
                </div>
                <div class="panel-item-derecha">
                    <b>${formatearPrecio(g.precio * g.cantidad)}</b>
                    <button class="btn-quitar" onclick="quitarDelCarrito(${g.id})" aria-label="Quitar ${g.nombre} del carrito">${icono("trash")}</button>
                </div>
            </div>`).join("");

        document.getElementById("miniCarritoTotal").innerText = formatearPrecio(carritoTotal);
        panelPie.classList.remove("hidden");
    }

    // ---- Página del carrito ----
    const pagina = document.getElementById("carritoPaginaLista");
    const pie = document.getElementById("carritoPaginaPie");

    if (carritoAgrupado.length === 0) {
        pagina.innerHTML = `
            <div class="carrito-vacio">
                ${icono("cart")}
                <p>Tu carrito está vacío</p>
                <button class="btn-dark-blue" onclick="irATienda()">Ver productos</button>
            </div>`;
        pie.classList.add("hidden");
        return;
    }

    pagina.innerHTML = `
        <div class="carrito-fila carrito-fila-titulos">
            <span>Producto</span>
            <span>Precio</span>
            <span>Cantidad</span>
            <span>Total</span>
            <span></span>
        </div>
        ${carritoAgrupado.map(g => `
        <div class="carrito-fila">
            <div class="carrito-producto" onclick="verDetalleProducto(${g.id})">
                <div class="carrito-foto"><img src="${g.imagen}" alt="${g.nombre}"></div>
                <span class="carrito-producto-nombre">${g.nombre}</span>
            </div>
            <span class="carrito-dato" data-label="Precio">${formatearPrecio(g.precio)}</span>
            <div class="carrito-dato carrito-dato-cantidad">${selectorCantidadCarrito(g)}</div>
            <b class="carrito-dato" data-label="Total">${formatearPrecio(g.precio * g.cantidad)}</b>
            <button class="btn-quitar" onclick="quitarDelCarrito(${g.id})" aria-label="Quitar ${g.nombre} del carrito">${icono("trash")}</button>
        </div>`).join("")}`;

    document.getElementById("carritoPaginaTotal").innerText = formatearPrecio(carritoTotal);
    pie.classList.remove("hidden");
}

function irAlCarrito() {
    renderizarCarrito();
    mostrarVista("vistaCarrito");
}

/* ---------- Panel lateral del carrito ---------- */

function abrirPanelCarrito() {
    cerrarMenus();
    renderizarCarrito();
    document.getElementById("panelCarrito").classList.add("abierto");
    document.getElementById("panelCarrito").setAttribute("aria-hidden", "false");
    document.getElementById("panelCarritoFondo").classList.add("abierto");
    document.body.classList.add("sin-scroll"); // la página de atrás no se mueve
}

function cerrarPanelCarrito() {
    document.getElementById("panelCarrito").classList.remove("abierto");
    document.getElementById("panelCarrito").setAttribute("aria-hidden", "true");
    document.getElementById("panelCarritoFondo").classList.remove("abierto");
    document.body.classList.remove("sin-scroll");
}

/* ---------- Cambiar cantidades dentro del carrito ---------- */

// La pantalla se actualiza al instante y el cambio se envía al servidor
// un momento después; así varios clics seguidos en + o − son una sola petición.
let temporizadoresCantidad = {}; // idProducto -> temporizador pendiente
let enviosEnCurso = 0;

function recalcularCarritoLocal() {
    carritoTotal = carritoAgrupado.reduce((suma, g) => suma + g.precio * g.cantidad, 0);
    unidadesCarrito = {};
    carritoAgrupado.forEach(g => unidadesCarrito[g.id] = g.cantidad);
}

function ajustarCantidadCarrito(idProducto, delta) {
    const g = carritoAgrupado.find(x => x.id === idProducto);
    if (!g) return;
    fijarCantidadCarrito(idProducto, g.cantidad + delta);
}

function escribirCantidadCarrito(idProducto, valor) {
    const g = carritoAgrupado.find(x => x.id === idProducto);
    if (!g) return;

    const cantidad = parseInt(valor, 10);
    if (isNaN(cantidad) || cantidad < 1) {
        renderizarCarrito(); // vuelve a mostrar la cantidad que había
        return;
    }

    const stock = stockDelProducto(idProducto);
    if (stock !== null && cantidad > stock) {
        alert(`No se puede: excediste el stock disponible de "${g.nombre}". Stock disponible: ${stock}.`);
    }
    fijarCantidadCarrito(idProducto, cantidad);
}

function fijarCantidadCarrito(idProducto, cantidad) {
    const g = carritoAgrupado.find(x => x.id === idProducto);
    if (!g) return;

    const stock = stockDelProducto(idProducto);
    const maximo = stock === null ? g.cantidad : stock;
    cantidad = Math.max(1, Math.min(cantidad, Math.max(maximo, 1)));

    if (cantidad === g.cantidad) {
        renderizarCarrito();
        return;
    }

    g.cantidad = cantidad;
    recalcularCarritoLocal();
    renderizarCarrito();

    clearTimeout(temporizadoresCantidad[idProducto]);
    temporizadoresCantidad[idProducto] = setTimeout(() => {
        delete temporizadoresCantidad[idProducto];
        enviarCantidadCarrito(idProducto, cantidad);
    }, 350);
}

async function enviarCantidadCarrito(idProducto, cantidad) {
    if (!usuarioId) return;
    const sesion = sesionActual;
    enviosEnCurso++;

    try {
        const res = await fetch(`${API}/carrito/cantidad?idUsuario=${usuarioId}&idProducto=${idProducto}&cantidad=${cantidad}`, {
            method: "PUT"
        });
        if (!res.ok) throw new Error(await res.text());
    } catch (err) {
        if (sesion === sesionActual) alert("Error: " + err.message);
    } finally {
        enviosEnCurso--;
    }

    // Se sincroniza con el servidor solo cuando ya no queda ningún cambio pendiente
    const hayPendientes = enviosEnCurso > 0 || Object.keys(temporizadoresCantidad).length > 0;
    if (sesion === sesionActual && !hayPendientes) {
        cargarProductos();
        cargarCarrito();
    }
}

function agregarCarrito(idProducto) {
    if (!usuarioId) return alert("Por favor, inicia sesión para añadir productos.");

    // Cantidad elegida en el selector - 1 +
    const input = document.getElementById(`qty-${idProducto}`);
    let cantidad = input ? parseInt(input.value, 10) : (cantidadSeleccionada[idProducto] || 1);
    if (isNaN(cantidad) || cantidad < 1) cantidad = 1;

    // Pasamos parámetros por QueryString como los espera CarritoController en C#
    fetch(`${API}/carrito/agregar?idUsuario=${usuarioId}&idProducto=${idProducto}&cantidad=${cantidad}`, {
        method: "POST"
    })
    .then(async res => {
        const txt = await res.text();
        if (!res.ok) throw new Error(txt);
        alert(txt); // Imprime: "Producto agregado al carrito"
        cantidadSeleccionada[idProducto] = 1;
        cargarCarrito(); // actualiza el carrito y el stock disponible de la grilla
    })
    .catch(err => {
        alert("Error: " + err.message);
        // Se vuelve a sincronizar con el stock real del servidor
        cargarProductos();
        cargarCarrito();
    });
}

// Quita del carrito todas las unidades de un producto (cantidad 0)
function quitarDelCarrito(idProducto) {
    if (!usuarioId) return;

    clearTimeout(temporizadoresCantidad[idProducto]);
    delete temporizadoresCantidad[idProducto];

    carritoAgrupado = carritoAgrupado.filter(g => g.id !== idProducto);
    recalcularCarritoLocal();
    renderizarCarrito();

    enviarCantidadCarrito(idProducto, 0);
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
        cargarCarrito();
    })
    .catch(err => alert("Error: " + err.message));
}

/* ==========================================
   HU-15: CHECKOUT (CONFIRMACIÓN DE PRODUCTOS)
========================================== */

const DEPARTAMENTOS = [
    "Amazonas", "Antioquia", "Arauca", "Atlántico", "Bogotá D.C.", "Bolívar", "Boyacá",
    "Caldas", "Caquetá", "Casanare", "Cauca", "Cesar", "Chocó", "Córdoba", "Cundinamarca",
    "Guainía", "Guaviare", "Huila", "La Guajira", "Magdalena", "Meta", "Nariño",
    "Norte de Santander", "Putumayo", "Quindío", "Risaralda", "San Andrés y Providencia",
    "Santander", "Sucre", "Tolima", "Valle del Cauca", "Vaupés", "Vichada"
];

// El backend manda los valores oficiales en /pedido/tarifas; estos son solo
// los valores con los que se dibuja mientras llega la respuesta.
let costoEnvio = 15000;
let tasaIva = 0.19;
let tarifasCargadas = false;
let checkoutPrellenado = false;

function cargarTarifas() {
    if (tarifasCargadas) return;

    fetch(`${API}/pedido/tarifas`)
        .then(r => r.ok ? r.json() : null)
        .then(t => {
            if (!t) return;
            costoEnvio = Number(t.costoEnvio !== undefined ? t.costoEnvio : t.CostoEnvio);
            tasaIva = Number(t.tasaIva !== undefined ? t.tasaIva : t.TasaIva);
            tarifasCargadas = true;
            renderizarResumenCheckout();
        })
        .catch(err => console.error("No se pudieron cargar las tarifas:", err));
}

/* ---------- Formulario ---------- */

// Un campo de texto con la etiqueta que sube al escribir (como en el ejemplo)
function campoTexto(id, etiqueta, autocompletar, extra = "") {
    return `
    <div class="campo">
        <input id="${id}" placeholder=" " autocomplete="${autocompletar}" ${extra}>
        <label for="${id}">${etiqueta}</label>
        <span class="campo-mensaje"></span>
    </div>`;
}

// Bloque de dirección. Se usa para la entrega ("envio") y para la facturación ("fac")
function camposDireccion(prefijo) {
    const seccion = prefijo === "envio" ? "shipping" : "billing";
    // Sin departamento preseleccionado: el cliente debe elegirlo
    const opcionesDepto = `<option value="" selected>Selecciona tu departamento</option>` +
        DEPARTAMENTOS.map(d => `<option value="${d}">${d}</option>`).join("");

    return `
    <div class="campo campo-select">
        <select id="${prefijo}Pais" disabled>
            <option>Colombia</option>
        </select>
        <label for="${prefijo}Pais">País / Región</label>
    </div>

    <div class="fila-campos">
        ${campoTexto(prefijo + "Nombre", "Nombre", seccion + " given-name")}
        ${campoTexto(prefijo + "Apellidos", "Apellidos", seccion + " family-name")}
    </div>

    ${campoTexto(prefijo + "Cedula", "Cédula", "off", 'inputmode="numeric" maxlength="10" class="solo-numeros"')}

    ${campoTexto(prefijo + "Direccion", "Dirección", seccion + " address-line1")}
    <p id="${prefijo}AvisoNumero" class="campo-ayuda hidden">
        ${icono("info")} Agrega un número de domicilio si lo tienes
    </p>

    ${campoTexto(prefijo + "Complemento", "Casa, apartamento, etc. (opcional)", seccion + " address-line2")}

    <div class="fila-campos fila-3">
        ${campoTexto(prefijo + "Ciudad", "Ciudad", seccion + " address-level2")}
        <div class="campo campo-select">
            <select id="${prefijo}Departamento" autocomplete="${seccion} address-level1">
                ${opcionesDepto}
            </select>
            <label for="${prefijo}Departamento">Departamento</label>
            <span class="campo-mensaje"></span>
        </div>
        ${campoTexto(prefijo + "CodigoPostal", "Código postal (opcional)", seccion + " postal-code", 'inputmode="numeric" maxlength="6" class="solo-numeros"')}
    </div>

    ${campoTexto(prefijo + "Telefono", "Teléfono", seccion + " tel-national", 'inputmode="tel" maxlength="10" class="solo-numeros"')}`;
}

// Mismas reglas que valida el backend (PedidoService.ValidarDatos)
const REGLAS_DIRECCION = [
    ["Nombre", v => v.trim() !== "" || "Ingresa un nombre"],
    ["Apellidos", v => v.trim() !== "" || "Ingresa los apellidos"],
    ["Cedula", v => /^\d{6,10}$/.test(v) || "Ingresa una cédula válida (6 a 10 números)"],
    ["Direccion", v => v.trim() !== "" || "Ingresa una dirección"],
    ["Ciudad", v => v.trim() !== "" || "Ingresa una ciudad"],
    ["Departamento", v => v !== "" || "Selecciona un departamento"],
    ["Telefono", v => /^\d{10}$/.test(v) || "Ingresa un teléfono de 10 números"]
];

function marcarError(campo, mensaje) {
    const contenedor = campo.closest(".campo");
    contenedor.classList.add("campo-error");
    contenedor.querySelector(".campo-mensaje").innerText = mensaje;
}

function limpiarErrorCampo(campo) {
    const contenedor = campo.closest(".campo");
    if (!contenedor) return;
    contenedor.classList.remove("campo-error");
    const mensaje = contenedor.querySelector(".campo-mensaje");
    if (mensaje) mensaje.innerText = "";
}

// Marca los campos con error y devuelve el primero (o null si todo está bien)
function validarDireccion(prefijo) {
    let primero = null;

    REGLAS_DIRECCION.forEach(([nombre, regla]) => {
        const campo = document.getElementById(prefijo + nombre);
        const resultado = regla(campo.value);
        if (resultado === true) {
            limpiarErrorCampo(campo);
        } else {
            marcarError(campo, resultado);
            if (!primero) primero = campo;
        }
    });

    return primero;
}

function leerDireccion(prefijo) {
    const valor = nombre => document.getElementById(prefijo + nombre).value.trim();
    return {
        pais: "Colombia",
        nombre: valor("Nombre"),
        apellidos: valor("Apellidos"),
        cedula: valor("Cedula"),
        direccion: valor("Direccion"),
        complemento: valor("Complemento"),
        ciudad: valor("Ciudad"),
        departamento: valor("Departamento"),
        codigoPostal: valor("CodigoPostal"),
        telefono: valor("Telefono")
    };
}

function actualizarAvisoNumero(prefijo) {
    const direccion = document.getElementById(prefijo + "Direccion");
    const aviso = document.getElementById(prefijo + "AvisoNumero");
    if (!direccion || !aviso) return;
    // Se sugiere el número cuando hay dirección escrita pero no tiene ningún número
    aviso.classList.toggle("hidden", direccion.value.trim() === "" || /\d/.test(direccion.value));
}

function construirFormularioCheckout() {
    if (document.getElementById("envioNombre")) return; // ya está dibujado

    document.getElementById("camposEnvio").innerHTML = camposDireccion("envio");
    document.getElementById("camposFacturacion").innerHTML = camposDireccion("fac");
    document.getElementById("checkoutCostoEnvio").innerText = formatearPrecio(costoEnvio);
    cambiarFacturacion();
}

// Un solo "escuchador" para todo el formulario: limpia errores, deja solo
// números donde corresponde y muestra el aviso del número de domicilio
document.getElementById("formCheckout").addEventListener("input", e => {
    const campo = e.target;
    if (campo.classList.contains("solo-numeros")) {
        campo.value = campo.value.replace(/[^0-9]/g, "");
    }
    limpiarErrorCampo(campo);
    if (campo.id.endsWith("Direccion")) actualizarAvisoNumero(campo.id.replace("Direccion", ""));
});

function cambiarFacturacion() {
    const distinta = document.querySelector('input[name="facturacion"]:checked').value === "distinta";
    document.getElementById("camposFacturacion").classList.toggle("hidden", !distinta);
    document.getElementById("opcionFacturacionIgual").classList.toggle("opcion-activa", !distinta);
    document.getElementById("opcionFacturacionDistinta").classList.toggle("opcion-activa", distinta);
}

// Llena los datos con el perfil del cliente (solo los campos vacíos)
function prellenarCheckout() {
    if (checkoutPrellenado) return;
    const sesion = sesionActual;

    fetch(`${API}/usuario/perfil/${usuarioId}`)
        .then(r => r.ok ? r.json() : null)
        .then(perfil => {
            if (!perfil || sesion !== sesionActual) return;
            checkoutPrellenado = true;

            const correo = perfil.correo || "";
            document.getElementById("checkoutCorreo").innerText = correo;
            document.getElementById("checkoutAvatar").innerText = (correo || usuarioNombre || "?").charAt(0).toUpperCase();

            // "Samuel Riaño Patiño" -> Nombre "Samuel", Apellidos "Riaño Patiño"
            const partes = (perfil.nombre || "").trim().split(/\s+/).filter(Boolean);
            const corte = partes.length <= 2 ? 1 : Math.floor(partes.length / 2);

            const llenar = (id, valor) => {
                const campo = document.getElementById(id);
                if (campo && campo.value.trim() === "" && valor) campo.value = valor;
            };

            llenar("envioNombre", partes.slice(0, corte).join(" "));
            llenar("envioApellidos", partes.slice(corte).join(" "));
            llenar("envioDireccion", perfil.direccion || "");
            llenar("envioTelefono", (perfil.telefono || "").replace(/[^0-9]/g, "").slice(-10));
            actualizarAvisoNumero("envio");
        })
        .catch(err => console.error("No se pudo cargar el perfil para el checkout:", err));
}

function irAlCheckout() {
    if (!usuarioId) return;
    if (carritoAgrupado.length === 0) return alert("Tu carrito está vacío.");

    construirFormularioCheckout();
    ocultarErrorCheckout();
    prellenarCheckout();
    cargarTarifas();
    renderizarResumenCheckout();
    mostrarVista("vistaCheckout");
}

/* ---------- Resumen del pedido (checkout y confirmación) ---------- */

// lineas: [{ nombre, imagen, precio, cantidad }]
function resumenPedidoHTML(lineas, subtotal, envio, impuestos, total) {
    const articulos = lineas.reduce((suma, l) => suma + l.cantidad, 0);

    return `
    <div class="resumen-lista">
        ${lineas.map(l => `
        <div class="resumen-item">
            <div class="resumen-foto">
                <img src="${l.imagen || IMAGEN_POR_DEFECTO}" alt="${l.nombre}">
                <span class="resumen-cantidad">${l.cantidad}</span>
            </div>
            <div class="resumen-info">
                <span class="resumen-nombre">${l.nombre}</span>
                <span class="resumen-unitario">${formatearPrecio(l.precio)} c/u</span>
            </div>
            <span class="resumen-precio">${formatearPrecio(l.precio * l.cantidad)}</span>
        </div>`).join("")}
    </div>

    <div class="resumen-totales">
        <div class="resumen-fila">
            <span>Subtotal · ${articulos} ${articulos === 1 ? "artículo" : "artículos"}</span>
            <span>${formatearPrecio(subtotal)}</span>
        </div>
        <div class="resumen-fila">
            <span class="con-ayuda" title="Envío estándar a cualquier ciudad de Colombia">Envío ${icono("info")}</span>
            <span>${formatearPrecio(envio)}</span>
        </div>
        <div class="resumen-fila resumen-total">
            <span>Total</span>
            <span><small>COP</small> ${formatearPrecio(total)}</span>
        </div>
        <p class="resumen-impuestos">Incluye ${formatearPrecio(impuestos)} de impuestos</p>
    </div>`;
}

function renderizarResumenCheckout() {
    const contenedor = document.getElementById("checkoutResumenContenido");
    if (!contenedor) return;

    const subtotal = carritoAgrupado.reduce((suma, g) => suma + g.precio * g.cantidad, 0);
    const impuestos = subtotal - subtotal / (1 + tasaIva); // los precios ya incluyen IVA
    const total = subtotal + costoEnvio;

    contenedor.innerHTML = resumenPedidoHTML(carritoAgrupado, subtotal, costoEnvio, impuestos, total);
    document.getElementById("checkoutResumenToggleTotal").innerText = formatearPrecio(total);
    document.getElementById("checkoutCostoEnvio").innerText = formatearPrecio(costoEnvio);
}

// En celular el resumen se puede mostrar u ocultar
function toggleResumenCheckout() {
    const resumen = document.querySelector(".checkout-resumen");
    const abierto = resumen.classList.toggle("resumen-abierto");
    document.querySelector(".checkout-resumen-toggle").setAttribute("aria-expanded", abierto);
    document.getElementById("checkoutResumenToggleTexto").innerText =
        abierto ? "Ocultar resumen del pedido" : "Mostrar resumen del pedido";
}

/* ---------- Enviar el pedido ---------- */

function mostrarErrorCheckout(mensaje) {
    const caja = document.getElementById("checkoutError");
    caja.innerHTML = `${icono("info")} <span>${escaparHTML(mensaje)}</span>`;
    caja.classList.remove("hidden");
    window.scrollTo({ top: 0, behavior: "smooth" });
}

function ocultarErrorCheckout() {
    document.getElementById("checkoutError").classList.add("hidden");
}

function enviarCheckout(evento) {
    evento.preventDefault();
    ocultarErrorCheckout();

    const facturacionDistinta =
        document.querySelector('input[name="facturacion"]:checked').value === "distinta";

    // Se validan los dos bloques para marcar todos los errores de una vez
    const errorEnvio = validarDireccion("envio");
    const errorFacturacion = facturacionDistinta ? validarDireccion("fac") : null;
    const primerError = errorEnvio || errorFacturacion;

    if (primerError) {
        primerError.focus();
        primerError.scrollIntoView({ behavior: "smooth", block: "center" });
        return;
    }

    const solicitud = {
        envio: leerDireccion("envio"),
        facturacionIgualEnvio: !facturacionDistinta,
        facturacion: facturacionDistinta ? leerDireccion("fac") : null
    };

    const boton = document.getElementById("btnPagarAhora");
    boton.disabled = true;
    boton.innerText = "Procesando...";
    const sesion = sesionActual;

    fetch(`${API}/pedido/checkout/${usuarioId}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(solicitud)
    })
    .then(async r => {
        if (!r.ok) throw new Error(await r.text());
        return r.json();
    })
    .then(pedido => {
        if (sesion !== sesionActual) return;
        mostrarEstadoPedido(pedido);
        // El carrito quedó vacío y el stock cambió
        cargarProductos();
        cargarCarrito();
        // HU-16: se abre de una vez la ventana de pago de Wompi
        iniciarPago(campo(pedido, "idPedido"));
    })
    .catch(err => {
        if (sesion !== sesionActual) return;
        mostrarErrorCheckout(err.message || "No se pudo crear el pedido. Intenta de nuevo.");
        // Si fue un problema de stock, se refresca el carrito con los datos reales
        cargarProductos();
        cargarCarrito();
    })
    .finally(() => {
        boton.disabled = false;
        boton.innerText = "Pagar ahora";
    });
}

/* ==========================================
   VENTANA PROPIA (MODAL)
========================================== */

let accionesModal = [];

// botones: [{ texto, clase, accion }]
// contenidoHTML: formulario opcional dentro de la ventana
// Si la acción de un botón devuelve false, la ventana no se cierra
// (por ejemplo, para mostrar errores del formulario)
function mostrarModal(titulo, mensaje, botones, contenidoHTML = "") {
    accionesModal = botones.map(b => b.accion);

    document.getElementById("modalTitulo").innerText = titulo;
    document.getElementById("modalMensaje").innerText = mensaje;
    document.getElementById("modalContenido").innerHTML = contenidoHTML;
    document.getElementById("modalBotones").innerHTML = botones
        .map((b, i) => `<button class="${b.clase}" onclick="ejecutarAccionModal(${i})">${b.texto}</button>`)
        .join("");

    document.getElementById("modal").classList.add("abierto");
    document.getElementById("modal").setAttribute("aria-hidden", "false");
    document.getElementById("modalFondo").classList.add("abierto");

    const primerCampo = document.querySelector("#modalContenido select, #modalContenido input");
    (primerCampo || document.querySelector("#modalBotones button")).focus();
}

function cerrarModal() {
    preguntaCierreAbierta = false;
    const modal = document.getElementById("modal");
    if (!modal) return;
    modal.classList.remove("abierto");
    modal.setAttribute("aria-hidden", "true");
    document.getElementById("modalFondo").classList.remove("abierto");
}

function ejecutarAccionModal(indice) {
    const accion = accionesModal[indice];
    const resultado = accion ? accion() : undefined;
    if (resultado !== false) cerrarModal();
}

/* ==========================================
   HU-16: SELECCIÓN DE PAGO CON WOMPI
========================================== */

let pedidoEnPantalla = null;        // pedido que se está mostrando
let intervaloCuentaRegresiva = null;

// Lee una propiedad sin importar si viene como "nombre" o "Nombre"
function campo(obj, nombre) {
    if (!obj) return undefined;
    return obj[nombre] !== undefined
        ? obj[nombre]
        : obj[nombre.charAt(0).toUpperCase() + nombre.slice(1)];
}

// Mensajes de estado del pago dentro de la página del pedido
function mostrarAvisoPago(tipo, mensaje) {
    const caja = document.getElementById("avisoPago");
    if (!caja) return;

    const iconos = { error: "xCircle", info: "info", cargando: "clock", exito: "check" };

    caja.className = `aviso-pago aviso-${tipo}`;
    caja.innerHTML = `${icono(iconos[tipo] || "info")} <span>${escaparHTML(mensaje)}</span>`;
}

function ocultarAvisoPago() {
    const caja = document.getElementById("avisoPago");
    if (caja) caja.className = "aviso-pago hidden";
}

/* ---------- 1. Preparar el pago en el backend ---------- */

function iniciarPago(idPedido) {
    if (!usuarioId) return;
    const sesion = sesionActual;

    mostrarAvisoPago("cargando", "Abriendo la ventana de pago segura de Wompi...");

    fetch(`${API}/pedido/${idPedido}/iniciar-pago?idUsuario=${usuarioId}`, { method: "POST" })
        .then(async r => {
            if (!r.ok) throw new Error((await r.text()) || `Error ${r.status} del servidor`);
            return r.json();
        })
        .then(datos => {
            if (sesion !== sesionActual) return;
            abrirWidgetWompi(datos);
        })
        .catch(err => {
            if (sesion !== sesionActual) return;
            mostrarAvisoPago("error", err.message || "No se pudo iniciar el pago.");
            // Puede que el pedido se haya cancelado por tiempo: se vuelve a consultar
            refrescarPedido(idPedido);
        });
}

/* ---------- 2. Abrir el widget encima de la página ---------- */

function abrirWidgetWompi(datos) {
    if (typeof WidgetCheckout === "undefined") {
        mostrarAvisoPago("error",
            "No se pudo cargar Wompi. Revisa tu conexión a internet y vuelve a intentarlo.");
        return;
    }

    const idPedido = campo(datos, "idPedido");
    const envio = campo(pedidoEnPantalla, "envio") || {};
    const correo = document.getElementById("checkoutCorreo").innerText.trim();

    const configuracion = {
        currency: campo(datos, "moneda"),
        amountInCents: Number(campo(datos, "montoEnCentavos")),
        reference: campo(datos, "referencia"),
        publicKey: campo(datos, "llavePublica"),
        signature: { integrity: campo(datos, "firma") },
        expirationTime: campo(datos, "fechaLimite"),
        // Los datos del checkout ya van llenos en Wompi
        shippingAddress: {
            addressLine1: campo(envio, "direccion"),
            city: campo(envio, "ciudad"),
            region: campo(envio, "departamento"),
            country: "CO",
            phoneNumber: campo(envio, "telefono")
        }
    };

    if (correo) {
        configuracion.customerData = {
            email: correo,
            fullName: `${campo(envio, "nombre") || ""} ${campo(envio, "apellidos") || ""}`.trim(),
            phoneNumber: campo(envio, "telefono"),
            phoneNumberPrefix: "+57",
            legalId: campo(envio, "cedula"),
            legalIdType: "CC"
        };
    }

    let terminoTransaccion = false;
    const sesion = sesionActual;
    const intento = ++intentoPago; // cada vez que se abre Wompi es un intento nuevo

    ocultarAvisoPago();

    const checkout = new WidgetCheckout(configuracion);

    // Wompi llama esta función cuando el cliente termina una transacción
    // (aprobada, rechazada o en proceso). Puede llegar varios segundos
    // después de que la ventana se cierra.
    checkout.open(resultado => {
        terminoTransaccion = true;
        if (sesion !== sesionActual) return;

        // Si alcanzó a salir la pregunta de "¿no quieres pagar?", sobra: se quita
        if (preguntaCierreAbierta) cerrarModal();

        const transaccion = resultado && resultado.transaction;
        if (transaccion && transaccion.id) {
            confirmarPago(idPedido, transaccion.id);
        }
    });

    vigilarCierreWidget(() => terminoTransaccion, idPedido, sesion, intento);
}

/* ---------- 3. Si el cliente cierra la ventana sin pagar, se le pregunta ---------- */

let intentoPago = 0;              // número del intento de pago actual
let preguntaCierreAbierta = false; // si está a la vista la pregunta "¿no quieres pagar?"

// La ventana de Wompi no avisa cuando se cierra, así que se revisa si queda
// alguno de sus marcos (iframe) en pantalla. Wompi cambia de marco mientras
// procesa el pago, por eso se exige que NO quede ninguno visible durante un
// rato antes de considerar que el cliente la cerró.
function vigilarCierreWidget(huboTransaccion, idPedido, sesion, intento) {
    const estaVisible = el => el.isConnected && el.getClientRects().length > 0 &&
        getComputedStyle(el).visibility !== "hidden";
    const hayVentanaWompi = () =>
        [...document.querySelectorAll('iframe[src*="wompi"]')].some(estaVisible);

    const sigueVigente = () =>
        sesion === sesionActual && intento === intentoPago && !huboTransaccion();

    let seAbrio = false;
    let esperas = 0;
    let cerradaDesde = null;

    const revision = setInterval(() => {
        if (!sigueVigente()) return clearInterval(revision);

        const visible = hayVentanaWompi();

        // Primero se espera a que Wompi termine de abrir su ventana (máx. 15 s)
        if (!seAbrio) {
            if (visible) seAbrio = true;
            else if (++esperas > 50) clearInterval(revision);
            return;
        }

        if (visible) {
            cerradaDesde = null;
            return;
        }

        // La ventana debe seguir cerrada al menos 1,5 s seguidos
        if (cerradaDesde === null) cerradaDesde = Date.now();
        if (Date.now() - cerradaDesde < 1500) return;

        clearInterval(revision);

        // Margen extra para que Wompi entregue el resultado del pago
        setTimeout(() => verificarAntesDePreguntar(idPedido, sigueVigente), 2500);
    }, 300);
}

// Antes de preguntar, se confirma con el servidor que el pedido de verdad
// sigue sin pagar (por si el resultado de Wompi llegó por otro lado)
function verificarAntesDePreguntar(idPedido, sigueVigente) {
    if (!sigueVigente()) return;

    fetch(`${API}/pedido/${idPedido}`)
        .then(r => r.ok ? r.json() : null)
        .then(pedido => {
            if (!sigueVigente()) return;

            if (pedido && campo(pedido, "estadoPedido") !== "Pendiente de pago") {
                // Ya no está pendiente (pagado o cancelado): se muestra su estado real
                mostrarEstadoPedido(pedido);
                return;
            }
            preguntarAlCerrarPago(idPedido);
        })
        .catch(() => {
            if (sigueVigente()) preguntarAlCerrarPago(idPedido);
        });
}

function preguntarAlCerrarPago(idPedido) {
    mostrarModal(
        "¿Seguro que no quieres pagar?",
        "Cerraste la ventana de pago sin completar la compra. Si la cancelas, el pedido " +
        "se anulará y los productos volverán a tu carrito.",
        [
            { texto: "Cancelar compra", clase: "btn-danger", accion: () => cancelarPedidoCliente(idPedido) },
            { texto: "Volver al pago", clase: "btn-success", accion: () => iniciarPago(idPedido) }
        ]
    );
    preguntaCierreAbierta = true;
}

/* ---------- 4. Confirmar el resultado con el backend (que le pregunta a Wompi) ---------- */

function confirmarPago(idPedido, idTransaccion, intento = 1) {
    const sesion = sesionActual;

    mostrarAvisoPago("cargando", "Confirmando tu pago con Wompi...");

    fetch(`${API}/pedido/${idPedido}/confirmar-pago?idUsuario=${usuarioId}&idTransaccion=${encodeURIComponent(idTransaccion)}`, {
        method: "POST"
    })
    .then(async r => {
        if (!r.ok) throw new Error(await r.text());
        return r.json();
    })
    .then(resultado => {
        if (sesion !== sesionActual) return;

        const estado = campo(resultado, "estado");
        const mensaje = campo(resultado, "mensaje");
        const pedido = campo(resultado, "pedido");

        if (estado === "APROBADO") {
            mostrarEstadoPedido(pedido, false, campo(resultado, "correoFactura") || "");
            return;
        }

        if (estado === "PENDIENTE") {
            // PSE o Nequi pueden tardar: se vuelve a consultar cada 4 segundos (máx. ~1 min)
            if (intento < 15) {
                mostrarAvisoPago("cargando", mensaje);
                setTimeout(() => confirmarPago(idPedido, idTransaccion, intento + 1), 4000);
            } else {
                mostrarAvisoPago("info",
                    "Tu pago sigue en proceso. Vuelve a consultar en un momento.");
                document.getElementById("avisoPago").insertAdjacentHTML("beforeend",
                    `<button class="btn-dark-blue btn-aviso" onclick="confirmarPago(${idPedido}, '${escaparHTML(idTransaccion)}')">Consultar de nuevo</button>`);
            }
            return;
        }

        // Rechazado: el pedido sigue pendiente y se puede reintentar
        if (pedido) mostrarEstadoPedido(pedido);
        mostrarAvisoPago("error", `${mensaje} Puedes intentarlo de nuevo con el mismo u otro medio de pago.`);
    })
    .catch(err => {
        if (sesion !== sesionActual) return;
        mostrarAvisoPago("error", err.message || "No se pudo confirmar el pago.");
    });
}

/* ---------- HU-17: descargar la factura en PDF ---------- */

function descargarFactura(idPedido) {
    const boton = document.getElementById("btnDescargarFactura");
    const textoOriginal = boton ? boton.innerHTML : "";
    if (boton) {
        boton.disabled = true;
        boton.innerText = "Generando factura...";
    }

    fetch(`${API}/pedido/${idPedido}/factura?idUsuario=${usuarioId}`)
        .then(async r => {
            if (!r.ok) throw new Error((await r.text()) || `Error ${r.status}`);
            return r.blob();
        })
        .then(pdf => {
            // Se descarga con el número de factura como nombre del archivo
            const numero = campo(pedidoEnPantalla, "numeroFactura") || `pedido-${idPedido}`;
            const enlace = document.createElement("a");
            enlace.href = URL.createObjectURL(pdf);
            enlace.download = `Factura-${numero}.pdf`;
            document.body.appendChild(enlace);
            enlace.click();
            enlace.remove();
            setTimeout(() => URL.revokeObjectURL(enlace.href), 10000);
        })
        .catch(err => mostrarAvisoPago("error", "No se pudo descargar la factura: " + err.message))
        .finally(() => {
            if (boton) {
                boton.disabled = false;
                boton.innerHTML = textoOriginal;
            }
        });
}

/* ---------- Cancelar ---------- */

function confirmarCancelacion(idPedido) {
    mostrarModal(
        "¿Cancelar la compra?",
        "El pedido se anulará y los productos volverán a tu carrito.",
        [
            { texto: "No, volver", clase: "btn-secondary", accion: null },
            { texto: "Sí, cancelar compra", clase: "btn-danger", accion: () => cancelarPedidoCliente(idPedido) }
        ]
    );
}

function cancelarPedidoCliente(idPedido) {
    const sesion = sesionActual;

    fetch(`${API}/pedido/${idPedido}/cancelar?idUsuario=${usuarioId}`, { method: "POST" })
        .then(async r => {
            if (!r.ok) throw new Error(await r.text());
        })
        .catch(err => {
            if (sesion === sesionActual) mostrarAvisoPago("error", err.message);
        })
        .finally(() => {
            if (sesion !== sesionActual) return;
            refrescarPedido(idPedido);
            // El stock volvió al catálogo y los productos al carrito
            cargarProductos();
            cargarCarrito();
        });
}

function refrescarPedido(idPedido) {
    const sesion = sesionActual;

    fetch(`${API}/pedido/${idPedido}`)
        .then(r => r.ok ? r.json() : null)
        .then(pedido => {
            if (!pedido || sesion !== sesionActual) return;

            // Solo se actualiza la página del pedido que se está viendo;
            // si el cliente está en otra parte, no se le cambia de página
            if (!document.getElementById("vistaPedido").classList.contains("hidden")) {
                mostrarDetallePedido(pedido);
            } else if (!document.getElementById("vistaConfirmacion").classList.contains("hidden")) {
                mostrarEstadoPedido(pedido, true);
            } else if (!document.getElementById("vistaMisPedidos").classList.contains("hidden")) {
                abrirMisPedidos(false);
            }
        })
        .catch(err => console.error("No se pudo consultar el pedido:", err));
}

/* ---------- Cuenta regresiva para pagar ---------- */

function detenerCuentaRegresiva() {
    clearInterval(intervaloCuentaRegresiva);
    intervaloCuentaRegresiva = null;
}

function iniciarCuentaRegresiva(idPedido, fechaLimite) {
    detenerCuentaRegresiva();
    const limite = new Date(fechaLimite).getTime();
    if (isNaN(limite)) return;

    const actualizar = () => {
        const restante = limite - Date.now();
        const reloj = document.getElementById("cuentaRegresiva");

        if (restante <= 0) {
            detenerCuentaRegresiva();
            if (reloj) reloj.innerText = "00:00";
            // El backend también lo cancela solo; así se ve de inmediato
            cancelarPedidoCliente(idPedido);
            return;
        }

        const minutos = Math.floor(restante / 60000);
        const segundos = Math.floor((restante % 60000) / 1000);
        if (reloj) {
            reloj.innerText = `${String(minutos).padStart(2, "0")}:${String(segundos).padStart(2, "0")}`;
        }
    };

    actualizar();
    intervaloCuentaRegresiva = setInterval(actualizar, 1000);
}

/* ---------- Página del pedido: pendiente, pagado o cancelado ---------- */

// conservarAviso: deja visible el mensaje de pago que hubiera
// correoFactura: correo al que se acaba de enviar la factura (HU-17)
function mostrarEstadoPedido(pedido, conservarAviso = false, correoFactura = "") {
    pedidoEnPantalla = pedido;

    const avisoAnterior = conservarAviso ? document.getElementById("avisoPago") : null;
    const avisoHTML = avisoAnterior && !avisoAnterior.classList.contains("hidden")
        ? avisoAnterior.outerHTML
        : `<div id="avisoPago" class="aviso-pago hidden" role="status"></div>`;

    // El detalle informativo usa los mismos elementos (aviso, cuenta regresiva): se limpia
    document.getElementById("pedidoContenido").innerHTML = "";

    const v = campo;
    const t = (obj, nombre) => escaparHTML(v(obj, nombre));

    const id = v(pedido, "idPedido");
    const estado = v(pedido, "estadoPedido");
    const envio = v(pedido, "envio") || {};
    const facturacion = v(pedido, "facturacion");
    const nombre = escaparHTML(v(envio, "nombre") || usuarioNombre);

    const lineas = (v(pedido, "detalles") || []).map(d => ({
        nombre: v(d, "nombre"),
        imagen: v(d, "imagen"),
        precio: v(d, "precioUnitario"),
        cantidad: v(d, "cantidad")
    }));

    const fechaTexto = fecha => {
        const f = new Date(fecha);
        return isNaN(f) ? "" : f.toLocaleString("es-CO", { dateStyle: "long", timeStyle: "short" });
    };

    const direccionHTML = d => `
        ${t(d, "nombre")} ${t(d, "apellidos")}<br>
        C.C. ${t(d, "cedula")}<br>
        ${t(d, "direccion")}${v(d, "complemento") ? ", " + t(d, "complemento") : ""}<br>
        ${t(d, "ciudad")}, ${t(d, "departamento")}${v(d, "codigoPostal") ? " · " + t(d, "codigoPostal") : ""}<br>
        Tel. ${t(d, "telefono")}`;

    const mismaFacturacion = !facturacion || JSON.stringify(facturacion) === JSON.stringify(envio);

    // Lo que cambia según el estado del pedido
    let iconoEstado, titulo, aviso, pagoHTML, acciones;

    if (estaPagado(estado)) {
        iconoEstado = "check";
        titulo = `¡Gracias por tu compra, ${nombre}!`;
        aviso = `Recibimos tu pago con ${t(pedido, "medioPago")}. Te avisaremos cuando tu pedido vaya en camino.`;
        pagoHTML = `Pagado con ${t(pedido, "medioPago")}<br>
                    Transacción ${t(pedido, "idTransaccion")}<br>
                    ${fechaTexto(v(pedido, "fechaPago"))}
                    ${v(pedido, "numeroFactura") ? `<br>Factura N° ${t(pedido, "numeroFactura")}` : ""}`;
        acciones = `
            <button class="btn-dark-blue" id="btnDescargarFactura" onclick="descargarFactura(${id})">${icono("download")} Descargar factura</button>
            <button class="btn-success" onclick="irATienda()">Seguir comprando</button>`;
    } else if (estado === "Cancelado") {
        iconoEstado = "xCircle";
        titulo = "Pedido cancelado";
        aviso = `${t(pedido, "motivoCancelacion") || "El pedido fue cancelado"}. Los productos volvieron a tu carrito.`;
        pagoHTML = "Sin pago";
        acciones = `
            <button class="btn-dark-blue" onclick="irAlCarrito()">Ver mi carrito</button>
            <button class="btn-success" onclick="irATienda()">Seguir comprando</button>`;
    } else {
        iconoEstado = "clock";
        titulo = `${nombre}, tu pedido está listo para pagar`;
        aviso = `Tienes <b id="cuentaRegresiva">--:--</b> para completar el pago. Después, el pedido se cancelará y los productos volverán al catálogo.`;
        pagoHTML = `${t(pedido, "metodoPago")} · pendiente`;
        acciones = `
            <button class="btn-danger" onclick="confirmarCancelacion(${id})">Cancelar compra</button>
            <button class="btn-success" onclick="iniciarPago(${id})">${icono("lock")} Pagar ahora</button>`;
    }

    document.getElementById("confirmacionContenido").innerHTML = `
    <div class="confirmacion-cabecera">
        <span class="confirmacion-icono">${icono(iconoEstado)}</span>
        <div>
            <span class="confirmacion-numero">Pedido #${id}</span>
            <h2>${titulo}</h2>
        </div>
    </div>

    <div class="confirmacion-aviso">
        <span class="estado-chip estado-${escaparHTML((estado || "").toLowerCase().replace(/\s+/g, "-"))}">${escaparHTML(estado)}</span>
        <p>${aviso}</p>
    </div>

    ${avisoHTML}

    ${estaPagado(estado) && correoFactura ? `
    <div class="aviso-pago aviso-exito">
        ${icono("mail")} <span>Te enviamos la factura ${t(pedido, "numeroFactura")} a <b>${escaparHTML(correoFactura)}</b>.</span>
    </div>` : ""}

    <div class="confirmacion-grid">
        <div class="confirmacion-datos">
            <div class="confirmacion-bloque">
                <h3>Entrega</h3>
                <p>${direccionHTML(envio)}</p>
            </div>
            <div class="confirmacion-bloque">
                <h3>Facturación</h3>
                <p>${mismaFacturacion ? "La misma dirección de envío" : direccionHTML(facturacion)}</p>
            </div>
            <div class="confirmacion-bloque">
                <h3>Envío</h3>
                <p>${t(pedido, "metodoEnvio")} · ${formatearPrecio(v(pedido, "costoEnvio"))}<br>
                   Pedido hecho el ${fechaTexto(v(pedido, "fecha"))}</p>
            </div>
            <div class="confirmacion-bloque">
                <h3>Pago</h3>
                <p>${pagoHTML}</p>
            </div>
        </div>

        <div class="confirmacion-resumen">
            ${resumenPedidoHTML(lineas, v(pedido, "subtotal"), v(pedido, "costoEnvio"), v(pedido, "impuestos"), v(pedido, "totalPedido"))}
        </div>
    </div>

    <div class="confirmacion-acciones">${acciones}</div>`;

    if (estado === "Pendiente de pago") {
        iniciarCuentaRegresiva(id, v(pedido, "fechaLimitePago"));
    } else {
        detenerCuentaRegresiva();
    }

    // Si la ventana de Wompi está abierta encima, la vista ya es esta: no se reinicia el scroll
    if (document.getElementById("vistaConfirmacion").classList.contains("hidden")) {
        mostrarVista("vistaConfirmacion");
    }
}

/* ==========================================
   HU-18: CONSULTA DE COMPRAS (MIS PEDIDOS)
========================================== */

const PEDIDOS_POR_PAGINA = 10;

const FILTROS_PEDIDOS = [
    { clave: "todos", texto: "Todos", estados: null },
    { clave: "pendientes", texto: "Pendientes", estados: ["Pendiente de pago"] },
    { clave: "pagados", texto: "Pagados", estados: ["Pagado"] },
    { clave: "enviados", texto: "Enviados", estados: ["Enviado"] },
    { clave: "entregados", texto: "Entregados", estados: ["Entregado"] },
    { clave: "cancelados", texto: "Cancelados", estados: ["Cancelado"] }
];

// Un pedido enviado o entregado también está pagado
function estaPagado(estado) {
    return estado === "Pagado" || estado === "Enviado" || estado === "Entregado";
}

let misPedidos = [];
let filtroPedidos = "todos";
let paginaPedidos = 1;

// reiniciar: true al entrar desde el menú (vuelve a "Todos" y a la página 1)
function abrirMisPedidos(reiniciar = true) {
    if (!usuarioId || esAdministrador) return;
    const sesion = sesionActual;

    if (reiniciar) {
        filtroPedidos = "todos";
        paginaPedidos = 1;
    }

    document.getElementById("listaMisPedidos").innerHTML =
        `<p class="pedidos-mensaje">Cargando tus pedidos...</p>`;
    mostrarVista("vistaMisPedidos");

    fetch(`${API}/pedido/usuario/${usuarioId}`)
        .then(async r => {
            if (!r.ok) throw new Error((await r.text()) || `Error ${r.status}`);
            return r.json();
        })
        .then(lista => {
            if (sesion !== sesionActual) return;
            misPedidos = lista;
            renderizarMisPedidos();
        })
        .catch(err => {
            if (sesion !== sesionActual) return;
            document.getElementById("listaMisPedidos").innerHTML =
                `<p class="pedidos-mensaje">No se pudieron cargar tus pedidos: ${escaparHTML(err.message)}</p>`;
        });
}

function cambiarFiltroPedidos(clave) {
    filtroPedidos = clave;
    paginaPedidos = 1;
    renderizarMisPedidos();
}

function irAPaginaPedidos(numero) {
    paginaPedidos = numero;
    renderizarMisPedidos();
    document.querySelector(".mis-pedidos").scrollIntoView({ behavior: "smooth", block: "start" });
}

function renderizarMisPedidos() {
    const estadoDe = p => campo(p, "estadoPedido");

    // Pestañas con la cantidad de pedidos de cada estado
    document.getElementById("filtrosPedidos").innerHTML = FILTROS_PEDIDOS.map(f => {
        const cantidad = f.estados ? misPedidos.filter(p => f.estados.includes(estadoDe(p))).length : misPedidos.length;
        const activo = f.clave === filtroPedidos;
        return `<button class="filtro-pedido${activo ? " activo" : ""}" role="tab" aria-selected="${activo}"
                    onclick="cambiarFiltroPedidos('${f.clave}')">${f.texto} <span>${cantidad}</span></button>`;
    }).join("");

    const filtro = FILTROS_PEDIDOS.find(f => f.clave === filtroPedidos);
    const lista = filtro.estados ? misPedidos.filter(p => filtro.estados.includes(estadoDe(p))) : misPedidos;

    const contenedor = document.getElementById("listaMisPedidos");
    const paginacion = document.getElementById("paginacionPedidos");

    if (lista.length === 0) {
        contenedor.innerHTML = misPedidos.length === 0
            ? `<div class="carrito-vacio">
                   ${icono("package")}
                   <p>Todavía no has hecho ningún pedido</p>
                   <button class="btn-dark-blue" onclick="irATienda()">Ver productos</button>
               </div>`
            : `<p class="pedidos-mensaje">No tienes pedidos ${filtro.texto.toLowerCase()}.</p>`;
        paginacion.innerHTML = "";
        return;
    }

    const totalPaginas = Math.ceil(lista.length / PEDIDOS_POR_PAGINA);
    if (paginaPedidos > totalPaginas) paginaPedidos = totalPaginas;
    const inicio = (paginaPedidos - 1) * PEDIDOS_POR_PAGINA;

    contenedor.innerHTML = lista.slice(inicio, inicio + PEDIDOS_POR_PAGINA).map(tarjetaPedidoHTML).join("");
    paginacion.innerHTML = paginacionHTML(totalPaginas, paginaPedidos, "irAPaginaPedidos");
}

// Círculos de paginación (mismo estilo de la tienda)
function paginacionHTML(totalPaginas, actual, funcion) {
    if (totalPaginas <= 1) return "";

    let html = "";
    if (actual > 1) {
        html += `<button class="pagina-btn pagina-flecha" onclick="${funcion}(${actual - 1})" aria-label="Página anterior">${icono("arrowLeft")}</button>`;
    }
    paginasVisibles(totalPaginas, actual).forEach(n => {
        if (n === "…") {
            html += `<span class="pagina-btn pagina-puntos">…</span>`;
        } else {
            const activa = n === actual;
            html += `<button class="pagina-btn${activa ? " activa" : ""}" onclick="${funcion}(${n})" ${activa ? 'aria-current="page"' : ""}>${n}</button>`;
        }
    });
    if (actual < totalPaginas) {
        html += `<button class="pagina-btn pagina-flecha" onclick="${funcion}(${actual + 1})" aria-label="Página siguiente">${icono("arrowRight")}</button>`;
    }
    return html;
}

function claseEstado(estado) {
    return "estado-" + (estado || "").toLowerCase().replace(/\s+/g, "-");
}

function fechaPedidoTexto(fecha, conHora = true) {
    const f = new Date(fecha);
    if (isNaN(f)) return "";
    return conHora
        ? f.toLocaleString("es-CO", { dateStyle: "long", timeStyle: "short" })
        : f.toLocaleDateString("es-CO", { dateStyle: "long" });
}

// Una tarjeta de la lista: número, estado, fecha, fotos, artículos y total
function tarjetaPedidoHTML(p) {
    const id = campo(p, "idPedido");
    const estado = campo(p, "estadoPedido");
    const detalles = campo(p, "detalles") || [];
    const articulos = detalles.reduce((suma, d) => suma + (campo(d, "cantidad") || 0), 0);
    const factura = campo(p, "numeroFactura");

    const MAX_FOTOS = 4;
    const fotos = detalles.slice(0, MAX_FOTOS).map(d => `
        <span class="pedido-foto" title="${escaparHTML(campo(d, "nombre"))}">
            <img src="${escaparHTML(campo(d, "imagen") || IMAGEN_POR_DEFECTO)}" alt="${escaparHTML(campo(d, "nombre"))}">
        </span>`).join("");
    const extra = detalles.length > MAX_FOTOS
        ? `<span class="pedido-foto pedido-foto-extra">+${detalles.length - MAX_FOTOS}</span>` : "";

    let nota = "";
    if (estado === "Pendiente de pago" && campo(p, "fechaLimitePago")) {
        const limite = new Date(campo(p, "fechaLimitePago"));
        nota = `Paga antes de las ${limite.toLocaleTimeString("es-CO", { hour: "numeric", minute: "2-digit" })}`;
    } else if (estado === "Cancelado") {
        nota = escaparHTML(campo(p, "motivoCancelacion") || "");
    } else if (estado === "Enviado") {
        nota = `${escaparHTML(campo(p, "transportadora"))} · Guía ${escaparHTML(campo(p, "numeroGuia"))}`;
    } else if (estado === "Entregado") {
        nota = `Entregado el ${fechaPedidoTexto(campo(p, "fechaEntrega"), false)}`;
    } else if (factura) {
        nota = `Factura ${escaparHTML(factura)}`;
    }

    return `
    <button class="pedido-item" onclick="abrirDetallePedido(${id})">
        <div class="pedido-item-info">
            <div class="pedido-item-titulo">
                <b>Pedido #${id}</b>
                <span class="estado-chip ${claseEstado(estado)}">${escaparHTML(estado)}</span>
            </div>
            <span class="pedido-item-fecha">${fechaPedidoTexto(campo(p, "fecha"))}</span>
            ${nota ? `<span class="pedido-item-nota">${nota}</span>` : ""}
        </div>
        <div class="pedido-fotos">${fotos}${extra}</div>
        <div class="pedido-item-total">
            <b>${formatearPrecio(campo(p, "totalPedido"))}</b>
            <span>${articulos} ${articulos === 1 ? "artículo" : "artículos"}</span>
        </div>
        <span class="pedido-item-flecha">${icono("chevronRight")}</span>
    </button>`;
}

/* ---------- Detalle informativo de un pedido ---------- */

function abrirDetallePedido(idPedido) {
    const sesion = sesionActual;

    fetch(`${API}/pedido/${idPedido}`)
        .then(async r => {
            if (!r.ok) throw new Error((await r.text()) || `Error ${r.status}`);
            return r.json();
        })
        .then(pedido => {
            if (sesion !== sesionActual) return;
            // Un cliente solo puede ver sus propios pedidos
            if (campo(pedido, "idUsuario") !== usuarioId) throw new Error("Pedido no encontrado");
            mostrarDetallePedido(pedido);
        })
        .catch(err => {
            if (sesion === sesionActual) alert("No se pudo abrir el pedido: " + err.message);
        });
}

// Línea de tiempo: Pedido hecho → Pagado → Enviado → Entregado
function lineaTiempoHTML(pedido) {
    const estado = campo(pedido, "estadoPedido");

    const pasos = [{ titulo: "Pedido hecho", detalle: fechaPedidoTexto(campo(pedido, "fecha")), estado: "hecho" }];

    if (estado === "Cancelado") {
        pasos.push({ titulo: "Cancelado", detalle: escaparHTML(campo(pedido, "motivoCancelacion") || ""), estado: "cancelado" });
    } else {
        const pagado = estaPagado(estado);
        const enviado = estado === "Enviado" || estado === "Entregado";
        const entregado = estado === "Entregado";

        pasos.push({
            titulo: "Pagado",
            detalle: pagado ? fechaPedidoTexto(campo(pedido, "fechaPago")) : "Esperando el pago",
            estado: pagado ? "hecho" : "actual"
        });
        pasos.push({
            titulo: "Enviado",
            detalle: enviado
                ? `${fechaPedidoTexto(campo(pedido, "fechaEnvio"))}<br>${escaparHTML(campo(pedido, "transportadora"))}`
                : (pagado ? "Preparando tu pedido" : ""),
            estado: enviado ? "hecho" : (pagado ? "actual" : "pendiente")
        });
        pasos.push({
            titulo: "Entregado",
            detalle: entregado ? fechaPedidoTexto(campo(pedido, "fechaEntrega")) : (enviado ? "En camino" : ""),
            // Cuando se entrega, este paso va en verde (como el rojo de "Cancelado")
            estado: entregado ? "entregado" : (enviado ? "actual" : "pendiente")
        });
    }

    return `
    <ol class="linea-tiempo">
        ${pasos.map(p => `
        <li class="paso paso-${p.estado}">
            <span class="paso-punto">${p.estado === "hecho" || p.estado === "entregado" ? icono("check") : p.estado === "cancelado" ? icono("xCircle") : ""}</span>
            <span class="paso-titulo">${p.titulo}</span>
            <span class="paso-detalle">${p.detalle}</span>
        </li>`).join("")}
    </ol>`;
}

function mostrarDetallePedido(pedido) {
    // Para que la descarga de factura y el pago usen este pedido
    pedidoEnPantalla = pedido;

    // La página de pago podría tener su propia cuenta regresiva: se limpia
    detenerCuentaRegresiva();
    document.getElementById("confirmacionContenido").innerHTML = "";

    const v = campo;
    const t = (obj, nombre) => escaparHTML(v(obj, nombre));

    const id = v(pedido, "idPedido");
    const estado = v(pedido, "estadoPedido");
    const envio = v(pedido, "envio") || {};
    const facturacion = v(pedido, "facturacion");
    const mismaFacturacion = !facturacion || JSON.stringify(facturacion) === JSON.stringify(envio);

    const direccionHTML = d => `
        ${t(d, "nombre")} ${t(d, "apellidos")}<br>
        C.C. ${t(d, "cedula")}<br>
        ${t(d, "direccion")}${v(d, "complemento") ? ", " + t(d, "complemento") : ""}<br>
        ${t(d, "ciudad")}, ${t(d, "departamento")}${v(d, "codigoPostal") ? " · " + t(d, "codigoPostal") : ""}<br>
        Tel. ${t(d, "telefono")}`;

    const lineas = (v(pedido, "detalles") || []).map(d => ({
        nombre: v(d, "nombre"),
        imagen: v(d, "imagen"),
        precio: v(d, "precioUnitario"),
        cantidad: v(d, "cantidad")
    }));

    // Datos del pago según el estado
    let pagoHTML;
    if (estaPagado(estado)) {
        pagoHTML = `
            <dl class="datos-lista">
                <dt>Medio</dt><dd>${t(pedido, "medioPago")} (Wompi)</dd>
                <dt>Transacción</dt><dd>${t(pedido, "idTransaccion")}</dd>
                <dt>Referencia</dt><dd>${t(pedido, "referenciaPago")}</dd>
                <dt>Pagado el</dt><dd>${fechaPedidoTexto(v(pedido, "fechaPago"))}</dd>
            </dl>`;
    } else if (estado === "Cancelado") {
        pagoHTML = `<p>Sin pago</p>`;
    } else {
        pagoHTML = `<p>Wompi · pendiente</p>`;
    }

    // Aviso y acciones según el estado
    let avisoEstado = "";
    let acciones = "";
    if (estado === "Pendiente de pago") {
        avisoEstado = `
        <div class="confirmacion-aviso">
            ${icono("clock")}
            <p>Quedan <b id="cuentaRegresiva">--:--</b> para pagar este pedido. Después se cancelará y los productos volverán al catálogo.</p>
        </div>`;
        acciones = `
            <button class="btn-danger" onclick="confirmarCancelacion(${id})">Cancelar compra</button>
            <button class="btn-success" onclick="pagarDesdeDetalle(${id})">${icono("lock")} Pagar ahora</button>`;
    } else if (estaPagado(estado) && (v(pedido, "detalles") || []).length > 0) {
        acciones = `
            <button class="btn-dark-blue" id="btnDescargarFactura" onclick="descargarFactura(${id})">${icono("download")} Descargar factura</button>`;
    }

    document.getElementById("pedidoContenido").innerHTML = `
    <div class="detalle-pedido-cabecera">
        <button class="btn-secondary" onclick="abrirMisPedidos(false)">${icono("arrowLeft")} Mis pedidos</button>
    </div>

    <div class="detalle-pedido-titulo">
        <div>
            <span class="confirmacion-numero">Hecho el ${fechaPedidoTexto(v(pedido, "fecha"), false)}</span>
            <h2>Pedido #${id}</h2>
        </div>
        <span class="estado-chip ${claseEstado(estado)}">${escaparHTML(estado)}</span>
    </div>

    ${lineaTiempoHTML(pedido)}

    ${avisoEstado}

    <div id="avisoPago" class="aviso-pago hidden" role="status"></div>

    <div class="confirmacion-grid">
        <div class="confirmacion-datos detalle-datos">
            <div class="confirmacion-bloque">
                <h3>Entrega</h3>
                <p>${direccionHTML(envio)}</p>
            </div>
            <div class="confirmacion-bloque">
                <h3>Facturación</h3>
                <p>${mismaFacturacion ? "La misma dirección de envío" : direccionHTML(facturacion)}</p>
            </div>
            <div class="confirmacion-bloque">
                <h3>Envío</h3>
                <p>${t(pedido, "metodoEnvio") || "Estándar"} · ${formatearPrecio(v(pedido, "costoEnvio"))}</p>
                ${v(pedido, "numeroGuia") ? `
                <div class="guia-envio">
                    <span class="guia-etiqueta">${t(pedido, "transportadora")} · Número de guía</span>
                    <div class="guia-numero">
                        <b>${t(pedido, "numeroGuia")}</b>
                        <button class="btn-secondary btn-copiar" onclick="copiarGuia('${t(pedido, "numeroGuia")}', this)">Copiar</button>
                    </div>
                    <span class="guia-ayuda">Rastréalo en la página de ${t(pedido, "transportadora")}.</span>
                </div>` : ""}
            </div>
            <div class="confirmacion-bloque">
                <h3>Pago</h3>
                ${pagoHTML}
            </div>
            ${v(pedido, "numeroFactura") ? `
            <div class="confirmacion-bloque">
                <h3>Factura</h3>
                <p>N° ${t(pedido, "numeroFactura")}<br>Emitida el ${fechaPedidoTexto(v(pedido, "fechaFactura"), false)}</p>
            </div>` : ""}
        </div>

        <div class="confirmacion-resumen">
            ${resumenPedidoHTML(lineas, v(pedido, "subtotal"), v(pedido, "costoEnvio"), v(pedido, "impuestos"), v(pedido, "totalPedido"))}
        </div>
    </div>

    ${acciones ? `<div class="confirmacion-acciones">${acciones}</div>` : ""}`;

    if (estado === "Pendiente de pago") {
        iniciarCuentaRegresiva(id, v(pedido, "fechaLimitePago"));
    }

    if (document.getElementById("vistaPedido").classList.contains("hidden")) {
        mostrarVista("vistaPedido");
    }
}

// Pagar un pedido pendiente: se pasa a la página de pago y se abre Wompi
function pagarDesdeDetalle(idPedido) {
    mostrarEstadoPedido(pedidoEnPantalla);
    iniciarPago(idPedido);
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
                    ? `<button class="btn-quitar btn-quitar-tag" onclick="eliminarCategoriaSegura(${idCat})" aria-label="Eliminar categoría">${icono("x")}</button>`
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
        cerrarDetalleProducto(); // si estabas en el detalle, vuelve a la tienda
        cargarProductos();
    });
}

/* ==========================================
   GESTIÓN DE PEDIDOS (ADMINISTRADOR)
   Envío manual: Pagado → Enviado (transportadora y guía) → Entregado
========================================== */

const FILTROS_ADMIN = [
    { clave: "porEnviar", texto: "Por enviar", estados: ["Pagado"] },
    { clave: "enviados", texto: "Enviados", estados: ["Enviado"] },
    { clave: "entregados", texto: "Entregados", estados: ["Entregado"] },
    { clave: "pendientes", texto: "Pendientes de pago", estados: ["Pendiente de pago"] },
    { clave: "cancelados", texto: "Cancelados", estados: ["Cancelado"] },
    { clave: "todos", texto: "Todos", estados: null }
];

let pedidosAdmin = [];
let filtroPedidosAdmin = "porEnviar";
let paginaPedidosAdmin = 1;
let transportadoras = [];

// La caja del header abre la página de gestión de pedidos
function abrirGestionPedidos() {
    if (!esAdministrador) return;
    filtroPedidosAdmin = "porEnviar";
    paginaPedidosAdmin = 1;
    elegirPestanaInicial = true;
    document.getElementById("avisoAdminPedidos").classList.add("hidden");
    mostrarVista("vistaGestionPedidos");
    cargarPedidosAdmin();
}

// Si no queda nada por enviar pero sí por entregar, se abre en "Enviados"
let elegirPestanaInicial = false;

// Contador sobre la caja:
// - Círculo blanco relleno: pedidos pagados que faltan por despachar.
// - Solo si ya se despacharon todos, círculo vacío con contorno blanco:
//   pedidos enviados que faltan por entregar.
function actualizarContadorPorEnviar() {
    const contar = estado => pedidosAdmin.filter(p => campo(p, "estadoPedido") === estado).length;
    const porEnviar = contar("Pagado");
    const porEntregar = contar("Enviado");

    const badge = document.getElementById("pedidosAdminBadge");
    const boton = document.getElementById("btnPedidosAdmin");
    const formato = n => (n > 99 ? "99+" : n);

    badge.classList.remove("hidden", "badge-contorno");

    if (porEnviar > 0) {
        badge.innerText = formato(porEnviar);
        boton.title = `${porEnviar} ${porEnviar === 1 ? "pedido" : "pedidos"} por enviar`;
    } else if (porEntregar > 0) {
        badge.innerText = formato(porEntregar);
        badge.classList.add("badge-contorno");
        boton.title = `${porEntregar} ${porEntregar === 1 ? "pedido" : "pedidos"} por entregar`;
    } else {
        badge.classList.add("hidden");
        boton.title = "Gestión de pedidos";
    }
    boton.setAttribute("aria-label", boton.title);
}

function cargarPedidosAdmin() {
    const sesion = sesionActual;

    fetch(`${API}/pedido`)
        .then(r => r.ok ? r.json() : Promise.reject(new Error(`Error ${r.status}`)))
        .then(lista => {
            if (sesion !== sesionActual) return;
            // Del más nuevo al más viejo
            pedidosAdmin = lista.slice().sort((a, b) => new Date(campo(b, "fecha")) - new Date(campo(a, "fecha")));
            actualizarContadorPorEnviar();

            if (elegirPestanaInicial) {
                elegirPestanaInicial = false;
                const hay = estado => pedidosAdmin.some(p => campo(p, "estadoPedido") === estado);
                if (!hay("Pagado") && hay("Enviado")) filtroPedidosAdmin = "enviados";
            }

            renderizarPedidosAdmin();
        })
        .catch(err => {
            if (sesion === sesionActual) mostrarAvisoAdmin("error", "No se pudieron cargar los pedidos: " + err.message);
        });

    // Lista de transportadoras para el formulario de envío (se pide una vez)
    if (transportadoras.length === 0) {
        fetch(`${API}/pedido/transportadoras`)
            .then(r => r.ok ? r.json() : [])
            .then(lista => { transportadoras = lista; })
            .catch(() => {});
    }
}

function mostrarAvisoAdmin(tipo, mensaje) {
    const caja = document.getElementById("avisoAdminPedidos");
    const iconos = { error: "xCircle", exito: "check", info: "info" };
    caja.className = `aviso-pago aviso-${tipo}`;
    caja.innerHTML = `${icono(iconos[tipo] || "info")} <span>${escaparHTML(mensaje)}</span>`;
}

function cambiarFiltroAdmin(clave) {
    filtroPedidosAdmin = clave;
    paginaPedidosAdmin = 1;
    renderizarPedidosAdmin();
}

function irAPaginaPedidosAdmin(numero) {
    paginaPedidosAdmin = numero;
    renderizarPedidosAdmin();
    document.querySelector(".admin-pedidos").scrollIntoView({ behavior: "smooth", block: "start" });
}

function buscarPedidosAdmin() {
    paginaPedidosAdmin = 1;
    renderizarPedidosAdmin();
}

function renderizarPedidosAdmin() {
    const estadoDe = p => campo(p, "estadoPedido");

    // Búsqueda por número de pedido, nombre del cliente o cédula
    const texto = normalizarTexto(document.getElementById("buscarPedidoAdmin").value).replace(/^#/, "");
    const coincide = p => {
        if (!texto) return true;
        const envio = campo(p, "envio") || {};
        return String(campo(p, "idPedido")) === texto
            || normalizarTexto(`${campo(envio, "nombre") || ""} ${campo(envio, "apellidos") || ""}`).includes(texto)
            || (campo(envio, "cedula") || "").includes(texto);
    };
    const encontrados = pedidosAdmin.filter(coincide);

    document.getElementById("filtrosPedidosAdmin").innerHTML = FILTROS_ADMIN.map(f => {
        const cantidad = f.estados ? encontrados.filter(p => f.estados.includes(estadoDe(p))).length : encontrados.length;
        const activo = f.clave === filtroPedidosAdmin;
        return `<button class="filtro-pedido${activo ? " activo" : ""}" role="tab" aria-selected="${activo}"
                    onclick="cambiarFiltroAdmin('${f.clave}')">${f.texto} <span>${cantidad}</span></button>`;
    }).join("");

    const filtro = FILTROS_ADMIN.find(f => f.clave === filtroPedidosAdmin);
    const lista = filtro.estados ? encontrados.filter(p => filtro.estados.includes(estadoDe(p))) : encontrados;

    const contenedor = document.getElementById("listaPedidosAdmin");
    const paginacion = document.getElementById("paginacionPedidosAdmin");

    if (lista.length === 0) {
        contenedor.innerHTML = `<p class="pedidos-mensaje">${texto
            ? "Ningún pedido coincide con la búsqueda."
            : `No hay pedidos ${filtro.texto.toLowerCase()}.`}</p>`;
        paginacion.innerHTML = "";
        return;
    }

    const totalPaginas = Math.ceil(lista.length / PEDIDOS_POR_PAGINA);
    if (paginaPedidosAdmin > totalPaginas) paginaPedidosAdmin = totalPaginas;
    const inicio = (paginaPedidosAdmin - 1) * PEDIDOS_POR_PAGINA;

    contenedor.innerHTML = lista.slice(inicio, inicio + PEDIDOS_POR_PAGINA).map(tarjetaPedidoAdminHTML).join("");
    paginacion.innerHTML = paginacionHTML(totalPaginas, paginaPedidosAdmin, "irAPaginaPedidosAdmin");
}

function tarjetaPedidoAdminHTML(p) {
    const t = (obj, nombre) => escaparHTML(campo(obj, nombre));
    const id = campo(p, "idPedido");
    const estado = campo(p, "estadoPedido");
    const envio = campo(p, "envio") || {};
    const detalles = campo(p, "detalles") || [];
    const articulos = detalles.reduce((suma, d) => suma + (campo(d, "cantidad") || 0), 0);

    const fotos = detalles.slice(0, 4).map(d => `
        <span class="pedido-foto" title="${escaparHTML(campo(d, "nombre"))}">
            <img src="${escaparHTML(campo(d, "imagen") || IMAGEN_POR_DEFECTO)}" alt="${escaparHTML(campo(d, "nombre"))}">
        </span>`).join("") +
        (detalles.length > 4 ? `<span class="pedido-foto pedido-foto-extra">+${detalles.length - 4}</span>` : "");

    // Lo que puede hacer el administrador según el estado
    let accion = "";
    if (estado === "Pagado") {
        accion = `<button class="btn-success" onclick="abrirFormularioEnvio(${id})">${icono("package")} Marcar como enviado</button>`;
    } else if (estado === "Enviado") {
        accion = `
            <span class="admin-guia">${t(p, "transportadora")} · Guía <b>${t(p, "numeroGuia")}</b></span>
            <button class="btn-dark-blue" onclick="confirmarEntrega(${id})">${icono("check")} Marcar como entregado</button>`;
    } else if (estado === "Entregado") {
        accion = `<span class="admin-guia">${t(p, "transportadora")} · Guía <b>${t(p, "numeroGuia")}</b> · Entregado el ${fechaPedidoTexto(campo(p, "fechaEntrega"), false)}</span>`;
    } else if (estado === "Cancelado") {
        accion = `<span class="admin-guia">${t(p, "motivoCancelacion")}</span>`;
    } else {
        accion = `
            <span class="admin-guia">Esperando el pago del cliente</span>
            <button class="btn-danger" onclick="confirmarCancelacionAdmin(${id})">${icono("xCircle")} Cancelar pedido</button>`;
    }

    return `
    <div class="pedido-admin">
        <div class="pedido-item-info">
            <div class="pedido-item-titulo">
                <b>Pedido #${id}</b>
                <span class="estado-chip ${claseEstado(estado)}">${escaparHTML(estado)}</span>
            </div>
            <span class="pedido-item-fecha">${fechaPedidoTexto(campo(p, "fecha"))}</span>
            <span class="pedido-admin-cliente">
                ${t(envio, "nombre")} ${t(envio, "apellidos")} · C.C. ${t(envio, "cedula")} · Tel. ${t(envio, "telefono")}
            </span>
            <span class="pedido-item-nota">
                ${t(envio, "direccion")}${campo(envio, "complemento") ? ", " + t(envio, "complemento") : ""} — ${t(envio, "ciudad")}, ${t(envio, "departamento")}
            </span>
        </div>
        <div class="pedido-fotos">${fotos}</div>
        <div class="pedido-item-total">
            <b>${formatearPrecio(campo(p, "totalPedido"))}</b>
            <span>${articulos} ${articulos === 1 ? "artículo" : "artículos"}</span>
        </div>
        <div class="pedido-admin-acciones">${accion}</div>
    </div>`;
}

/* ---------- Marcar como enviado: transportadora y número de guía ---------- */

function abrirFormularioEnvio(idPedido) {
    const opciones = transportadoras
        .map(nombre => `<option value="${escaparHTML(nombre)}">${escaparHTML(nombre)}</option>`)
        .join("");

    mostrarModal(
        `Enviar pedido #${idPedido}`,
        "Registra con qué transportadora salió y su número de guía. El cliente recibirá un correo para rastrearlo.",
        [
            { texto: "Cancelar", clase: "btn-secondary", accion: null },
            { texto: "Marcar como enviado", clase: "btn-success", accion: () => guardarEnvio(idPedido) }
        ],
        `
        <div class="campo campo-select">
            <select id="envioTransportadora">
                <option value="">Selecciona la transportadora</option>
                ${opciones}
            </select>
            <label for="envioTransportadora">Transportadora</label>
            <span class="campo-mensaje"></span>
        </div>
        <div class="campo">
            <input id="envioGuia" placeholder=" " maxlength="30" autocomplete="off">
            <label for="envioGuia">Número de guía</label>
            <span class="campo-mensaje"></span>
        </div>`
    );
}

// Devuelve false para que la ventana siga abierta mientras se guarda o si hay errores
function guardarEnvio(idPedido) {
    const selector = document.getElementById("envioTransportadora");
    const guia = document.getElementById("envioGuia");
    const numero = guia.value.trim().toUpperCase();

    limpiarErrorCampo(selector);
    limpiarErrorCampo(guia);

    let valido = true;
    if (!selector.value) {
        marcarError(selector, "Selecciona la transportadora");
        valido = false;
    }
    if (!/^[A-Za-z0-9-]{4,30}$/.test(numero)) {
        marcarError(guia, "Entre 4 y 30 letras, números o guiones");
        valido = false;
    }
    if (!valido) return false;

    const boton = document.querySelector("#modalBotones .btn-success");
    boton.disabled = true;
    boton.innerText = "Guardando...";

    const parametros = new URLSearchParams({
        idAdministrador: usuarioId,
        transportadora: selector.value,
        numeroGuia: numero
    });

    fetch(`${API}/pedido/${idPedido}/enviar?${parametros}`, { method: "POST" })
        .then(async r => {
            const texto = await r.text();
            if (!r.ok) throw new Error(texto || `Error ${r.status}`);
        })
        .then(() => {
            cerrarModal();
            mostrarAvisoAdmin("exito", `Pedido #${idPedido} marcado como enviado. Le avisamos al cliente por correo.`);
            cargarPedidosAdmin();
        })
        .catch(err => {
            marcarError(guia, err.message);
            boton.disabled = false;
            boton.innerText = "Marcar como enviado";
        });

    return false;
}

/* ---------- Marcar como entregado ---------- */

function confirmarEntrega(idPedido) {
    mostrarModal(
        `¿El pedido #${idPedido} ya fue entregado?`,
        "Confírmalo cuando la transportadora reporte la entrega. El cliente recibirá un correo.",
        [
            { texto: "Todavía no", clase: "btn-secondary", accion: null },
            { texto: "Sí, fue entregado", clase: "btn-success", accion: () => guardarEntrega(idPedido) }
        ]
    );
}

function guardarEntrega(idPedido) {
    fetch(`${API}/pedido/${idPedido}/entregar?idAdministrador=${usuarioId}`, { method: "POST" })
        .then(async r => {
            const texto = await r.text();
            if (!r.ok) throw new Error(texto || `Error ${r.status}`);
        })
        .then(() => {
            mostrarAvisoAdmin("exito", `Pedido #${idPedido} marcado como entregado. Le avisamos al cliente por correo.`);
            cargarPedidosAdmin();
        })
        .catch(err => mostrarAvisoAdmin("error", err.message));
}

/* ---------- Cancelar un pedido pendiente ---------- */

function confirmarCancelacionAdmin(idPedido) {
    mostrarModal(
        `¿Cancelar el pedido #${idPedido}?`,
        "El cliente no ha pagado. Al cancelarlo, el stock vuelve al catálogo, los productos " +
        "vuelven a su carrito y le avisamos por correo.",
        [
            { texto: "No, dejarlo", clase: "btn-secondary", accion: null },
            { texto: "Sí, cancelar pedido", clase: "btn-danger", accion: () => guardarCancelacionAdmin(idPedido) }
        ]
    );
}

function guardarCancelacionAdmin(idPedido) {
    fetch(`${API}/pedido/${idPedido}/cancelar-admin?idAdministrador=${usuarioId}`, { method: "POST" })
        .then(async r => {
            const texto = await r.text();
            if (!r.ok) throw new Error(texto || `Error ${r.status}`);
        })
        .then(() => {
            mostrarAvisoAdmin("exito", `Pedido #${idPedido} cancelado. El stock volvió al catálogo y le avisamos al cliente.`);
            cargarPedidosAdmin();
            cargarProductos(); // el stock cambió
        })
        .catch(err => mostrarAvisoAdmin("error", err.message));
}

/* ---------- Cliente: copiar el número de guía ---------- */

function copiarGuia(numero, boton) {
    const listo = () => {
        boton.innerText = "¡Copiado!";
        setTimeout(() => { boton.innerText = "Copiar"; }, 1600);
    };

    if (navigator.clipboard) {
        navigator.clipboard.writeText(numero).then(listo).catch(() => {});
    } else {
        // Navegadores sin portapapeles moderno (por ejemplo, archivo abierto con doble clic)
        const campoTemporal = document.createElement("textarea");
        campoTemporal.value = numero;
        document.body.appendChild(campoTemporal);
        campoTemporal.select();
        document.execCommand("copy");
        campoTemporal.remove();
        listo();
    }
}

/* ==========================================
   ENTER PARA ENVIAR
   Solo en el inicio de sesión y en "Recuperar acceso".
   Crear cuenta y cambiar la contraseña siguen pidiendo clic.
========================================== */

function enviarConEnter(idsCampos, accion) {
    idsCampos.forEach(id => {
        document.getElementById(id).addEventListener("keydown", evento => {
            if (evento.key === "Enter" && !evento.repeat) {
                evento.preventDefault();
                accion();
            }
        });
    });
}

enviarConEnter(["correo", "contrasena"], login);
enviarConEnter(["recCorreo"], () => {
    // Si ya se está enviando el código, no se manda otra vez
    if (!document.getElementById("btnEnviarCodigo").disabled) solicitarRecuperacion();
});

/* ==========================================
   ARRANQUE
========================================== */

pintarIconos();
renderizarCarrito();
