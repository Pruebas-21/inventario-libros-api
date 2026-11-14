"use strict";

const el = id => document.getElementById(id);
const [login, consulta, editor, eliminar] = ["login", "form-id", "form-libro", "form-borrar"].map(el);
const rutaLibros = "/api/libros";
const numero = new Intl.NumberFormat("es-CR");
const columnas = [["id", "ID"], ["titulo", "Título"], ["autor", "Autor"],
    ["anioPublicacion", "Año"], ["existencias", "Existencias"]];
let libros = [];
let cargado = false;
let libroConsultado;
let idEdicion;
let idEliminar;
let sesion;
let vencimiento;
let carga;

function nodo(etiqueta, texto, clase) {
    const elemento = document.createElement(etiqueta);
    if (texto !== undefined) elemento.textContent = texto;
    if (clase) elemento.className = clase;
    return elemento;
}

function aviso(id, mensaje = "", tipo = "err") {
    const elemento = el(id);
    elemento.textContent = mensaje;
    elemento.className = "aviso " + tipo;
    elemento.hidden = !mensaje;
}

function estado(conectado) {
    const indicador = el("estado");
    indicador.className = "estado " + (conectado ? "on" : "off");
    indicador.lastElementChild.textContent = conectado ? "API en línea" : "API no disponible";
}

function mostrarRespuesta(metodo, ruta, respuesta, cuerpo) {
    const linea = nodo("div", undefined, "linea");
    linea.append(nodo("span", metodo, "metodo m-" + metodo), nodo("code", ruta, "respuesta-ruta"),
        nodo("span", respuesta, "codigo"));
    el("consola").replaceChildren(linea, nodo("pre", cuerpo));
}

async function peticion(metodo, ruta, datos, requiereSesion = false, mostrar = true) {
    if (requiereSesion && !sesion) throw new Error("Inicia sesión para editar el inventario.");
    const headers = { Accept: "application/json" };
    if (datos) headers["Content-Type"] = "application/json";
    if (requiereSesion) headers.Authorization = "Bearer " + sesion.token;
    let respuesta;
    let cuerpo;
    try {
        respuesta = await fetch(ruta, { method: metodo, headers, body: datos && JSON.stringify(datos) });
        if (respuesta.headers.get("content-type")?.includes("json")) cuerpo = await respuesta.json();
        else if (respuesta.status !== 204) await respuesta.text();
    } catch {
        estado(false);
        if (mostrar) mostrarRespuesta(metodo, ruta, "Sin conexión", "No se pudo leer una respuesta de la API.");
        throw new Error("No se pudo conectar con la API. Intenta de nuevo.");
    }

    estado(respuesta.status < 500 && (cuerpo !== undefined || respuesta.status === 204 || !respuesta.ok));
    if (mostrar) {
        const visible = ruta === "/api/auth/login" && respuesta.ok
            ? { tokenType: cuerpo?.tokenType, expiresIn: cuerpo?.expiresIn, accessToken: "[oculto]", refreshToken: "[oculto]" }
            : cuerpo;
        mostrarRespuesta(metodo, ruta, "HTTP " + respuesta.status,
            visible === undefined ? "Sin cuerpo JSON" : JSON.stringify(visible, null, 2));
    }
    if (respuesta.ok) {
        if (respuesta.status === 204 || cuerpo !== undefined) return cuerpo;
        throw new Error("La API devolvió una respuesta inesperada.");
    }
    if (respuesta.status === 401 && requiereSesion) {
        cerrarSesion("La sesión venció. Inicia sesión de nuevo.");
        throw new Error("La sesión venció. Inicia sesión de nuevo.");
    }
    const mensajes = {
        400: "Revisa los campos de la solicitud.",
        401: "El correo o la contraseña son incorrectos.",
        404: "No se encontró el libro.",
        429: "Demasiados intentos. Espera un minuto y vuelve a intentarlo."
    };
    throw new Error(mensajes[respuesta.status] || "La API devolvió un error. Intenta de nuevo.");
}

function boton(texto, clase, accion) {
    const control = nodo("button", texto, "btn " + clase);
    control.type = "button";
    control.addEventListener("click", accion);
    return control;
}

function mostrarLibros() {
    const filtro = el("filtro").value.trim().toLocaleLowerCase("es");
    const visibles = libros.filter(libro => [libro.titulo, libro.autor]
        .some(valor => valor.toLocaleLowerCase("es").includes(filtro)));
    const tabla = el("tabla");
    if (!visibles.length) {
        tabla.replaceChildren(nodo("div", !cargado ? "El listado aún no está disponible."
            : filtro ? "No hay libros que coincidan con tu búsqueda." : "Todavía no hay libros registrados.", "mensaje-tabla"));
        return;
    }

    const contenido = nodo("table");
    contenido.setAttribute("aria-label", "Libros del inventario");
    const cabecera = contenido.createTHead().insertRow();
    for (const titulo of [...columnas.map(([, nombre]) => nombre), ...(sesion ? ["Acciones"] : [])]) {
        const th = nodo("th", titulo);
        th.scope = "col";
        cabecera.append(th);
    }
    const cuerpo = contenido.createTBody();
    for (const libro of visibles) {
        const fila = cuerpo.insertRow();
        for (const [clave, nombre] of columnas) {
            const celda = fila.insertCell();
            celda.textContent = libro[clave];
            celda.dataset.k = nombre;
            if (clave === "titulo") celda.className = "nombre";
        }
        if (sesion) {
            const acciones = nodo("div", undefined, "acciones");
            acciones.append(boton("Editar", "btn-s", () => abrirEditor(libro)),
                boton("Eliminar", "btn-s", () => abrirBorrar(libro)));
            fila.insertCell().append(acciones);
        }
    }
    tabla.replaceChildren(contenido);
}

async function cargarLibros(mostrar = true) {
    const sesionActual = sesion;
    const solicitud = carga = peticion("GET", rutaLibros, undefined, false, mostrar);
    const registros = await solicitud;
    if (!sesionActual || sesion !== sesionActual || carga !== solicitud) return;
    libros = registros;
    cargado = true;
    el("total").textContent = numero.format(libros.length);
    el("existencias").textContent = numero.format(libros.reduce((total, libro) => total + libro.existencias, 0));
    mostrarLibros();
}

function mostrarFicha(libro) {
    libroConsultado = libro?.id;
    const ficha = el("ficha");
    ficha.hidden = !libro;
    ficha.replaceChildren();
    if (!libro) return;

    const titulo = nodo("div", undefined, "tit");
    titulo.append(nodo("h3", "Libro #" + libro.id));
    const cerrar = boton("Cerrar", "btn-link cerrar", () => mostrarFicha());
    ficha.append(titulo, cerrar);
    for (const [clave, nombre] of columnas.slice(1)) {
        const dato = nodo("dl");
        dato.append(nodo("dt", nombre), nodo("dd", libro[clave]));
        ficha.append(dato);
    }
}

function mostrarSesion() {
    login.hidden = Boolean(sesion);
    el("sesion").hidden = !sesion;
    for (const id of ["estadisticas", "panel-libros", "endpoints", "enlace-endpoints"]) {
        el(id).hidden = !sesion;
    }
    el("correo-activo").textContent = sesion?.correo ?? "";
    el("nuevo").disabled = !sesion;
    mostrarLibros();
}

function cerrarSesion(mensaje = "") {
    clearTimeout(vencimiento);
    sesion = undefined;
    libros = [];
    cargado = false;
    el("total").textContent = "—";
    el("existencias").textContent = "—";
    el("filtro").value = "";
    consulta.reset();
    mostrarFicha();
    aviso("aviso");
    el("editor").close();
    el("borrar").close();
    el("estado").className = "estado";
    el("estado").lastElementChild.textContent = "Sesión cerrada";
    login.reset();
    mostrarSesion();
    aviso("error-login", mensaje);
}

function abrirEditor(libro) {
    editor.reset();
    idEdicion = libro?.id;
    el("editor-titulo").textContent = idEdicion ? "Editar libro #" + idEdicion : "Agregar libro";
    if (libro) {
        for (const [clave] of columnas.slice(1)) editor.elements.namedItem(clave).value = libro[clave];
    } else {
        editor.elements.namedItem("existencias").value = 0;
    }
    aviso("error-editor");
    el("editor").showModal();
}

function abrirBorrar(libro) {
    idEliminar = libro.id;
    el("borrar-detalle").textContent = "¿Eliminar «" + libro.titulo + "» del inventario?";
    aviso("error-borrar");
    el("borrar").showModal();
}

function alEnviar(formulario, zonaAviso, accion) {
    formulario.addEventListener("submit", async evento => {
        evento.preventDefault();
        const control = formulario.querySelector('[type="submit"]');
        if (control.disabled) return;
        control.disabled = true;
        aviso(zonaAviso);
        try {
            await accion(Object.fromEntries(new FormData(formulario)));
        } catch (error) {
            aviso(zonaAviso, error.message);
        } finally {
            control.disabled = false;
        }
    });
}

alEnviar(login, "error-login", async datos => {
    const respuesta = await peticion("POST", "/api/auth/login", datos);
    if (!respuesta.accessToken) throw new Error("La API no devolvió un token de acceso.");
    sesion = { token: respuesta.accessToken, correo: datos.correo };
    login.reset();
    mostrarSesion();
    vencimiento = setTimeout(() => cerrarSesion("La sesión venció. Inicia sesión de nuevo."), respuesta.expiresIn * 1000);
    await cargarLibros().catch(error => aviso("aviso", error.message));
});

alEnviar(consulta, "aviso", async datos => {
    mostrarFicha();
    mostrarFicha(await peticion("GET", rutaLibros + "/" + datos.id));
});

async function refrescarTrasCambio(mensaje) {
    aviso("aviso", mensaje, "ok");
    try {
        await cargarLibros(false);
    } catch {
        aviso("aviso", mensaje + " No se pudo refrescar el listado.");
    }
}

alEnviar(editor, "error-editor", async datos => {
    datos.titulo = datos.titulo.trim();
    datos.autor = datos.autor.trim();
    datos.anioPublicacion = Number(datos.anioPublicacion);
    datos.existencias = Number(datos.existencias);
    if (!datos.titulo || !datos.autor) throw new Error("Escribe un título y un autor.");
    const actualizando = Boolean(idEdicion);
    const resultado = await peticion(actualizando ? "PUT" : "POST",
        actualizando ? rutaLibros + "/" + idEdicion : rutaLibros, datos, true);
    el("editor").close();
    if (libroConsultado === resultado.id) mostrarFicha(resultado);
    await refrescarTrasCambio(actualizando ? "Libro actualizado." : "Libro agregado.");
});

alEnviar(eliminar, "error-borrar", async () => {
    const id = idEliminar;
    await peticion("DELETE", rutaLibros + "/" + id, undefined, true);
    el("borrar").close();
    if (libroConsultado === id) mostrarFicha();
    await refrescarTrasCambio("Libro eliminado.");
});

const ejemploLibro = { titulo: "El principito", autor: "Antoine de Saint-Exupéry", anioPublicacion: 1943, existencias: 3 };
const endpoints = [
    ["GET", rutaLibros, "Consultar todos", "200 OK"],
    ["GET", rutaLibros + "/{id}", "Consultar por ID", "200 OK"],
    ["POST", rutaLibros, "Agregar un libro", "201 Created", ejemploLibro, true],
    ["PUT", rutaLibros + "/{id}", "Actualizar un libro", "200 OK", { ...ejemploLibro, existencias: 8 }, true],
    ["DELETE", rutaLibros + "/{id}", "Eliminar un libro", "204 No Content", undefined, true],
    ["POST", "/api/auth/login", "Iniciar sesión", "200 OK", { correo: "evaluacion@example.test", contrasena: "CLAVE_DE_PRUEBA" }]
];
for (const [metodo, ruta, descripcion, codigo, cuerpo, privado] of endpoints) {
    const detalle = nodo("details", undefined, "ep");
    const resumen = nodo("summary");
    resumen.append(nodo("span", metodo, "metodo m-" + metodo), nodo("code", ruta, "ruta"),
        nodo("span", descripcion, "que"));
    const contenido = nodo("div", undefined, "cuerpo");
    contenido.append(nodo("p", codigo + (privado ? " · Requiere token Bearer" : ""), "sin"),
        cuerpo ? nodo("pre", JSON.stringify(cuerpo, null, 2)) : nodo("p", "Sin cuerpo de solicitud.", "sin"));
    detalle.append(resumen, contenido);
    el("lista-endpoints").append(detalle);
}

el("filtro").addEventListener("input", mostrarLibros);
el("nuevo").addEventListener("click", () => abrirEditor());
el("salir").addEventListener("click", () => cerrarSesion());
document.querySelectorAll("[data-cerrar]").forEach(control => {
    control.addEventListener("click", () => control.closest("dialog").close());
});
mostrarSesion();
