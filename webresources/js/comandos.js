/*
 * WR - CARP - JS - Comandos  (sanic_carp_wr_js_comandos)
 * diseno/07 5.1 a 5.4. Sin logica de negocio: las reglas viven en los plugins.
 *
 * Un namespace por tabla:
 *   SanicCarp.Comun         utilidades (bandera del guardia de OnSave, contexto del ribbon, errores, avisos, API, descarga)
 *   SanicCarp.Conversacion  OnLoad (aviso de otra conversacion abierta) + OnSave + gestos Tomar, Finalizar, Reabrir, Unir, Iniciar conversacion nueva
 *   SanicCarp.Arreglo       OnLoad/OnSave + gestos Generar formato, Desbloquear, Formalizar, Descartar, Anular, Reabrir arreglo
 *   SanicCarp.Cuota         OnSave (cancela el autoguardado)
 *   SanicCarp.Plantilla     OnSave del Mapeo + gestos Publicar version y Vista previa
 * El gesto 12 (Registrar pago) no tiene boton: es el Guardar nativo del formulario de Cuota.
 *
 * Reglas del archivo (diseno/07 5.3):
 *  - Un gesto = una llamada: o una Custom API, o UN Xrm.WebApi.updateRecord con SOLO las columnas del gesto.
 *  - Los errores CARP-* se muestran tal cual (desde el codigo hasta el final del mensaje, con todas las lineas).
 *  - Tras el gesto se refresca el formulario (data.refresh(false)) o la grilla (refresh()).
 *  - Las funciones de comando reciben (PrimaryControl) en un formulario y (SelectedControlSelectedItemIds, SelectedControl)
 *    en una grilla; las reglas de habilitacion (puedeX) reciben un solo parametro (PrimaryControl o SelectedControl) y
 *    devuelven true ante la duda: el servidor es la barrera.
 *
 * Campos "gesto": NO se usa setSubmitMode("never") (Learn: con "never" la columna no se puede editar).
 * En su lugar, el OnSave cancela el guardado, en cualquier modo, mientras un campo gesto este "dirty",
 * salvo que lo dispare el propio boton del gesto (SanicCarp.Comun.levantarBandera / bajarBandera). Todo gesto que
 * escribe corre dentro de SanicCarp.Comun.conBandera (try/finally): la bandera siempre baja, tambien ante un error.
 *
 * Todos los manejadores de evento se registran con "pasar el contexto de ejecucion como primer parametro".
 * Sintaxis: async/await (ES2017), soportada por los navegadores de la Unified Interface.
 */
var SanicCarp = SanicCarp || {};

SanicCarp.Comun = (function () {
    "use strict";

    var ROL_JEFE = "SR - CARP - Jefe";
    var MODO_AUTOGUARDADO = 70;   // Learn, getSaveMode: 70 = Auto Save
    var ID_AVISO = "sanic_carp_gesto_pendiente";
    var MIME_XLSX = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    var PATRON_CARP = /CARP-[A-Z]{3,4}-\d{3}:[\s\S]*/;
    var TEXTO_GENERICO = "No se pudo completar la acción. Intente de nuevo; si persiste, avise al equipo de soporte.";

    // Bandera de modulo: la levanta el boton de un gesto mientras guarda sus propias columnas.
    var aplicandoGesto = false;

    function levantarBandera() {
        aplicandoGesto = true;
    }

    function bajarBandera() {
        aplicandoGesto = false;
    }

    function banderaLevantada() {
        return aplicandoGesto;
    }

    /** Corre la accion con la bandera levantada y la baja siempre (exito o error). */
    async function conBandera(accion) {
        levantarBandera();
        try {
            return await accion();
        } finally {
            bajarBandera();
        }
    }

    /** El formContext a partir del contexto de ejecucion que entrega el formulario. */
    function formContext(executionContext) {
        return executionContext.getFormContext();
    }

    /** true si alguno de los campos gesto tiene un valor sin aplicar. */
    function hayGestoPendiente(fc, columnas) {
        for (var i = 0; i < columnas.length; i++) {
            var atributo = fc.getAttribute(columnas[i]);
            if (atributo && atributo.getIsDirty()) {
                return true;
            }
        }
        return false;
    }

    /**
     * OnSave de Conversacion y Arreglo: con un campo gesto dirty, cancela el guardado en TODOS los modos
     * (70, 2, 1, 59...) y avisa; si lo dispara el boton del gesto (bandera) o no hay gesto pendiente, no toca nada.
     */
    function guardarSinGestoSuelto(executionContext, columnas, aviso) {
        var fc = formContext(executionContext);
        if (aplicandoGesto) {
            return;
        }
        if (hayGestoPendiente(fc, columnas)) {
            executionContext.getEventArgs().preventDefault();
            fc.ui.setFormNotification(aviso, "INFO", ID_AVISO);
        } else {
            fc.ui.clearFormNotification(ID_AVISO);
        }
    }

    /** Cancela solo el guardado automatico (30 s); el guardado manual (icono del pie, Ctrl+S) sigue. */
    function cancelarAutoguardado(executionContext) {
        var args = executionContext.getEventArgs();
        if (args.getSaveMode() === MODO_AUTOGUARDADO) {
            args.preventDefault();
        }
    }

    /**
     * Solo UX: el servidor decide (EsJefatura). userSettings.roles incluye los roles que se reciben por team
     * (Learn: "assigned to the user and any security roles assigned to the team"); solo Unified Interface.
     */
    function esJefe() {
        var jefe = false;
        var roles = Xrm.Utility.getGlobalContext().userSettings.roles;
        roles.forEach(function (rol) {
            if (rol.name === ROL_JEFE) {
                jefe = true;
            }
        });
        return jefe;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Lote B: utilidades de los gestos
    // ---------------------------------------------------------------------------------------------------------

    /** Un Guid sin llaves y en minuscula, como lo espera la Web API. */
    function idLimpio(id) {
        return String(id).replace(/[{}]/g, "").toLowerCase();
    }

    function esFormulario(objeto) {
        return !!objeto && typeof objeto.getAttribute === "function" && !!objeto.data;
    }

    function esGrilla(objeto) {
        return !!objeto && typeof objeto.refresh === "function" && typeof objeto.getAttribute !== "function";
    }

    /**
     * Unifica lo que el ribbon entrega (G-I1b). En un formulario llega (PrimaryControl); en una grilla,
     * (SelectedControlSelectedItemIds, SelectedControl). Una regla recibe un solo parametro, de cualquiera de los dos.
     * Devuelve {tipo: "formulario"|"grilla", fc, grilla, ids} con ids ya limpios.
     */
    function contexto(primero, segundo) {
        if (esFormulario(primero)) {
            var id = primero.data.entity.getId();
            return { tipo: "formulario", fc: primero, grilla: null, ids: id ? [idLimpio(id)] : [] };
        }
        var ids = Array.isArray(primero) ? primero.map(idLimpio) : [];
        var grilla = esGrilla(segundo) ? segundo : (esGrilla(primero) ? primero : null);
        return { tipo: "grilla", fc: null, grilla: grilla, ids: ids };
    }

    /** El valor de una columna del formulario; undefined si la columna no esta en el formulario (ante la duda, se muestra). */
    function valor(fc, columna) {
        var atributo = fc.getAttribute(columna);
        return atributo ? atributo.getValue() : undefined;
    }

    /** Vacio para un lookup o un texto: null, "" o lista sin elementos. */
    function vacio(v) {
        return v === null || v === undefined || v === "" || (Array.isArray(v) && v.length === 0);
    }

    /** Refresca lo que el gesto cambio: el formulario (data.refresh(false)) o la grilla. */
    async function refrescar(ctx) {
        if (ctx.tipo === "formulario") {
            await ctx.fc.data.refresh(false);
            if (ctx.fc.ui && typeof ctx.fc.ui.refreshRibbon === "function") {
                ctx.fc.ui.refreshRibbon();
            }
        } else if (ctx.grilla) {
            ctx.grilla.refresh();
        }
    }

    /**
     * El texto para la persona. Si el mensaje trae un CARP-xxx-nnn, es desde el codigo hasta el final, con todas las lineas
     * (los errores de publicacion vienen unidos con salto de linea). Si no, un texto generico y el tecnico va a details.
     */
    function textoDeError(err) {
        var crudo = err && err.message ? String(err.message) : "";
        var encontrado = PATRON_CARP.exec(crudo);
        if (encontrado) {
            return { message: encontrado[0] };
        }
        return crudo ? { message: TEXTO_GENERICO, details: crudo } : { message: TEXTO_GENERICO };
    }

    function mostrarError(err) {
        return Xrm.Navigation.openErrorDialog(textoDeError(err));
    }

    /** Aviso de exito (toast global); si la plataforma no lo admite, no rompe el gesto. */
    function avisar(texto) {
        try {
            return Xrm.App.addGlobalNotification({ type: 2, level: 1, message: texto, showCloseButton: true });
        } catch (e) {
            return undefined;
        }
    }

    function alerta(texto) {
        return Xrm.Navigation.openAlertDialog({ text: texto, confirmButtonLabel: "Aceptar" });
    }

    async function confirmar(texto) {
        var respuesta = await Xrm.Navigation.openConfirmDialog(
            { text: texto, title: "Confirmar", confirmButtonLabel: "Aceptar", cancelButtonLabel: "Cancelar" },
            { height: 240, width: 460 });
        return !!(respuesta && respuesta.confirmed);
    }

    async function conProgreso(texto, accion) {
        Xrm.Utility.showProgressIndicator(texto);
        try {
            return await accion();
        } finally {
            Xrm.Utility.closeProgressIndicator();
        }
    }

    /**
     * Llama a una Custom API global (unbound) con parametros Guid. `parametros` = {nombre: guid}.
     * Devuelve el cuerpo JSON con las salidas. Un HTTP de error se vuelve una excepcion con su mensaje.
     */
    async function ejecutarApi(nombre, parametros) {
        var tipos = {};
        var peticion = {};
        Object.keys(parametros).forEach(function (clave) {
            tipos[clave] = { typeName: "Edm.Guid", structuralProperty: 1 };
            peticion[clave] = idLimpio(parametros[clave]);
        });
        peticion.getMetadata = function () {
            return { boundParameter: null, operationType: 0, operationName: nombre, parameterTypes: tipos };
        };
        var respuesta = await Xrm.WebApi.online.execute(peticion);
        if (respuesta && respuesta.ok === false) {
            var cuerpo = await respuesta.json();
            throw { message: (cuerpo && cuerpo.error && cuerpo.error.message) || "" };
        }
        return respuesta.json();
    }

    /**
     * Un gesto que escribe: confirma, hace UN updateRecord con `datos` (solo las columnas del gesto), refresca y avisa.
     * `exito` es un texto o una funcion(ctx) que se evalua DESPUES del refresco. Todo dentro de conBandera.
     * Devuelve true si se aplico.
     */
    async function escribirGesto(ctx, tabla, confirmacion, datos, exito) {
        if (ctx.ids.length !== 1) {
            await alerta("Guarde el registro antes de aplicar esta acción.");
            return false;
        }
        return conBandera(async function () {
            try {
                if (!(await confirmar(confirmacion))) {
                    return false;
                }
                await Xrm.WebApi.updateRecord(tabla, ctx.ids[0], datos);
                await refrescar(ctx);
                avisar(typeof exito === "function" ? exito(ctx) : exito);
                return true;
            } catch (err) {
                await mostrarError(err);
                return false;
            }
        });
    }

    /** Bytes de un Base64 sin decodificarlo: 3 por cada 4 caracteres, menos el relleno. */
    function bytesDeBase64(base64) {
        var relleno = base64.slice(-2) === "==" ? 2 : (base64.slice(-1) === "=" ? 1 : 0);
        return Math.floor(base64.length * 3 / 4) - relleno;
    }

    /** Descarga el .xlsx (Learn, openFile: fileSize en KB; openMode 2 = guardar). */
    function descargarXlsx(base64, nombre) {
        return Xrm.Navigation.openFile(
            { fileContent: base64, fileName: nombre, fileSize: Math.ceil(bytesDeBase64(base64) / 1024), mimeType: MIME_XLSX },
            { openMode: 2 });
    }

    /** Gestos 6 y 14: llama a generarformato (con o sin version previa), muestra "Generando…", descarga el .xlsx y muestra las advertencias. */
    async function generarYDescargar(parametros) {
        var salida = await conProgreso("Generando…", function () {
            return ejecutarApi("sanic_carp_capi_generarformato", parametros);
        });
        if (!salida.archivo) {
            throw { message: "" };   // sin archivo no hay nada que descargar: mensaje generico
        }
        await descargarXlsx(salida.archivo, salida.nombrearchivo);
        if (salida.advertencias) {
            await alerta(salida.advertencias);
        }
        return salida;
    }

    return {
        levantarBandera: levantarBandera,
        bajarBandera: bajarBandera,
        banderaLevantada: banderaLevantada,
        conBandera: conBandera,
        formContext: formContext,
        hayGestoPendiente: hayGestoPendiente,
        guardarSinGestoSuelto: guardarSinGestoSuelto,
        cancelarAutoguardado: cancelarAutoguardado,
        esJefe: esJefe,
        idLimpio: idLimpio,
        contexto: contexto,
        valor: valor,
        vacio: vacio,
        refrescar: refrescar,
        textoDeError: textoDeError,
        mostrarError: mostrarError,
        avisar: avisar,
        alerta: alerta,
        confirmar: confirmar,
        conProgreso: conProgreso,
        ejecutarApi: ejecutarApi,
        escribirGesto: escribirGesto,
        bytesDeBase64: bytesDeBase64,
        descargarXlsx: descargarXlsx,
        generarYDescargar: generarYDescargar
    };
})();

SanicCarp.Conversacion = (function () {
    "use strict";

    var C = SanicCarp.Comun;
    var TABLA = "sanic_carp_tbl_conversacion";
    var CAMPOS_GESTO = ["sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta"];
    var AVISO = "Use el botón Finalizar para aplicar el resultado y el motivo.";
    var ESTADO_ABIERTA = 1;              // statuscode de la Abierta (statecode 0)
    var ESTADO_FINALIZADA = 2;           // statuscode de la Finalizada (statecode 1)
    var VISTA_PARA_UNIR = "Conversaciones para unir";
    var TEXTO_TOM_900 = "La conversación cambió en este instante. Actualice la pantalla y vuelva a intentar.";

    function alGuardar(executionContext) {
        C.guardarSinGestoSuelto(executionContext, CAMPOS_GESTO, AVISO);
    }

    // --- Lote C: aviso de otra conversacion abierta (RF-14, DA-06) ---------------------------------------------

    var ID_AVISO_OTRA = "sanic_carp_otra_conversacion_abierta";
    var MAXIMO_OTRAS = 3;
    var LARGO_ASUNTO = 60;

    function comillasOData(texto) {
        return String(texto).replace(/'/g, "''");
    }

    function recortar(texto) {
        var t = String(texto === null || texto === undefined ? "" : texto);
        return t.length > LARGO_ASUNTO ? t.substring(0, LARGO_ASUNTO - 1) + "…" : t;
    }

    /** Texto del aviso: "Este cliente|remitente tiene otra(s) conversacion(es) abierta(s): CONV-1 «asunto»...". */
    function textoDeAviso(filas, clienteId) {
        var porCliente = !!clienteId && filas.some(function (f) {
            return !!f["_sanic_clienteid_value"] && C.idLimpio(f["_sanic_clienteid_value"]) === C.idLimpio(clienteId);
        });
        var detalle = filas.map(function (f) {
            return f["sanic_nombre"] + " «" + recortar(f["sanic_asunto"]) + "»";
        }).join("; ");
        var sujeto = porCliente ? "Este cliente" : "Este remitente";
        var cuerpo = filas.length === 1
            ? sujeto + " tiene otra conversación abierta: " + detalle + "."
            : sujeto + " tiene otras conversaciones abiertas: " + detalle + ".";
        return cuerpo + " Si es el mismo tema, use Unir a otra conversación.";
    }

    /**
     * OnLoad (RF-14, DA-06). Si la conversacion esta Abierta, consulta hasta 3 conversaciones Abiertas, con otro id, del mismo
     * cliente o del mismo remitente, y muestra un aviso INFO. Solo lectura; solo UX (el servidor decide al Unir). Ante cualquier
     * falla de la consulta no avisa y no rompe el formulario.
     */
    async function alCargar(executionContext) {
        var fc = C.formContext(executionContext);
        fc.ui.clearFormNotification(ID_AVISO_OTRA);
        var id = fc.data.entity.getId();
        if (!id || C.valor(fc, "statuscode") !== ESTADO_ABIERTA) {
            return;
        }
        var cliente = C.valor(fc, "sanic_clienteid");
        var clienteId = (Array.isArray(cliente) && cliente.length > 0) ? C.idLimpio(cliente[0].id) : null;
        var remitente = C.valor(fc, "sanic_remitente");
        var criterios = [];
        if (clienteId) {
            criterios.push("_sanic_clienteid_value eq " + clienteId);
        }
        if (!C.vacio(remitente)) {
            criterios.push("sanic_remitente eq '" + comillasOData(remitente) + "'");
        }
        if (criterios.length === 0) {
            return;
        }
        var consulta = "?$select=sanic_nombre,sanic_asunto,_sanic_clienteid_value&$top=" + MAXIMO_OTRAS +
            "&$orderby=sanic_fechaprimercorreo desc" +
            "&$filter=statecode eq 0 and sanic_carp_tbl_conversacionid ne " + C.idLimpio(id) +
            " and (" + criterios.join(" or ") + ")";
        try {
            var resultado = await Xrm.WebApi.retrieveMultipleRecords(TABLA, consulta);
            var filas = (resultado && resultado.entities) || [];
            if (filas.length > 0) {
                fc.ui.setFormNotification(textoDeAviso(filas, clienteId), "INFO", ID_AVISO_OTRA);
            }
        } catch (err) {
            // El aviso es una ayuda: si la consulta falla, la conversacion se abre igual.
        }
    }

    // --- Reglas de habilitacion (solo UX; true ante la duda) ---------------------------------------------------

    function enEstado(arg, estado) {
        var ctx = C.contexto(arg, arg);
        if (ctx.tipo !== "formulario") {
            return true;
        }
        var actual = C.valor(ctx.fc, "statuscode");
        return actual === undefined || actual === estado;
    }

    function puedeTomar(arg) {
        var ctx = C.contexto(arg, arg);
        if (ctx.tipo !== "formulario") {
            return true;   // en la grilla la vista no siempre trae el estado ni el oficial: el servidor decide
        }
        var oficial = C.valor(ctx.fc, "sanic_oficialasignadoid");
        return enEstado(arg, ESTADO_ABIERTA) && (oficial === undefined || C.vacio(oficial));
    }

    function puedeFinalizar(arg) {
        return enEstado(arg, ESTADO_ABIERTA);
    }

    function puedeUnir(arg) {
        return enEstado(arg, ESTADO_ABIERTA);
    }

    function puedeReabrir(arg) {
        return enEstado(arg, ESTADO_FINALIZADA);
    }

    function puedeIniciarNueva(arg) {
        var ctx = C.contexto(arg, arg);
        if (ctx.tipo !== "formulario") {
            return true;
        }
        var nuevo = C.valor(ctx.fc, "sanic_correonuevoenfinalizada");
        return enEstado(arg, ESTADO_FINALIZADA) && (nuevo === undefined || nuevo === true);
    }

    // --- Gestos -------------------------------------------------------------------------------------------------

    /** Gesto 1. Formulario o grilla (una sola fila). Custom API tomarconversacion; TOM-900 muestra el texto neutro y refresca. */
    async function tomar(primero, segundo) {
        var ctx = C.contexto(primero, segundo);
        if (ctx.ids.length !== 1) {
            await C.alerta("Seleccione una sola conversación.");
            return;
        }
        await C.conBandera(async function () {
            try {
                var salida = await C.ejecutarApi("sanic_carp_capi_tomarconversacion", { conversacionid: ctx.ids[0] });
                await C.refrescar(ctx);
                if (salida.yaasignada === true) {
                    C.avisar("Ya era suya.");
                } else {
                    C.avisar(salida.oficialnombre ? "Conversación tomada por " + salida.oficialnombre + "." : "Conversación tomada.");
                }
            } catch (err) {
                if (err && err.message && String(err.message).indexOf("CARP-TOM-900") !== -1) {
                    // Perdio la carrera: texto neutro del catalogo + refresco, sin reintento (DA-07).
                    await Xrm.Navigation.openErrorDialog({ message: TEXTO_TOM_900 });
                    await C.refrescar(ctx);
                } else {
                    await C.mostrarError(err);
                }
            }
        });
    }

    /** Gesto 2. Lee los campos gesto del formulario; los vacios (motivo y detalle) no se mandan. */
    async function finalizar(primero) {
        var ctx = C.contexto(primero);
        var datos = { statecode: 1, statuscode: 2, sanic_resultado: null };
        var resultado = C.valor(ctx.fc, "sanic_resultado");
        datos.sanic_resultado = C.vacio(resultado) ? null : resultado;
        ["sanic_motivosinrespuesta", "sanic_detallesinrespuesta"].forEach(function (columna) {
            var v = C.valor(ctx.fc, columna);
            if (!C.vacio(v)) {
                datos[columna] = v;
            }
        });
        return C.escribirGesto(ctx, TABLA, "Se registra el resultado, quién finaliza y cuándo.", datos, "Conversación finalizada.");
    }

    /** Gesto 3. SOLO el estado: cualquier otra columna da CARP-CNV-010. */
    async function reabrir(primero) {
        var ctx = C.contexto(primero);
        return C.escribirGesto(ctx, TABLA, "Vuelve a Abierta; se limpian el resultado y la finalización.",
            { statecode: 0, statuscode: ESTADO_ABIERTA }, "Conversación reabierta.");
    }

    /** Resuelve por nombre la vista del selector; si no la encuentra, el selector usa la vista de busqueda por defecto. */
    async function idDeVistaParaUnir() {
        try {
            var r = await Xrm.WebApi.retrieveMultipleRecords("savedquery",
                "?$select=savedqueryid&$filter=name eq '" + VISTA_PARA_UNIR + "' and returnedtypecode eq '" + TABLA + "'&$top=1");
            return r.entities.length ? r.entities[0].savedqueryid : null;
        } catch (e) {
            return null;
        }
    }

    /** Gesto 4. Selector nativo (una sola conversacion abierta, distinta de la actual) + confirmacion + Custom API unirconversaciones. */
    async function unir(primero) {
        var ctx = C.contexto(primero);
        if (ctx.ids.length !== 1) {
            await C.alerta("Guarde la conversación antes de unirla.");
            return;
        }
        var origenId = ctx.ids[0];
        var origenNombre = C.valor(ctx.fc, "sanic_nombre") || "esta conversación";
        await C.conBandera(async function () {
            try {
                var opciones = {
                    allowMultiSelect: false,
                    defaultEntityType: TABLA,
                    entityTypes: [TABLA],
                    disableMru: true,
                    filters: [{
                        entityLogicalName: TABLA,
                        filterXml: '<filter type="and"><condition attribute="statecode" operator="eq" value="0" />' +
                            '<condition attribute="' + TABLA + 'id" operator="ne" value="' + origenId + '" /></filter>'
                    }]
                };
                var vista = await idDeVistaParaUnir();
                if (vista) {
                    opciones.defaultViewId = C.idLimpio(vista);
                    opciones.viewIds = [C.idLimpio(vista)];
                }
                var elegidas = await Xrm.Utility.lookupObjects(opciones);
                if (!elegidas || elegidas.length === 0) {
                    return;
                }
                var destino = elegidas[0];
                var texto = "Los correos de " + origenNombre + " pasan a " + (destino.name || "la conversación elegida") +
                    ". No se puede deshacer.";
                if (!(await C.confirmar(texto))) {
                    return;
                }
                var salida = await C.conProgreso("Uniendo conversaciones…", function () {
                    return C.ejecutarApi("sanic_carp_capi_unirconversaciones", { origenid: origenId, destinoid: destino.id });
                });
                await C.refrescar(ctx);   // limpia lo "dirty" antes de salir: asi el guardado al navegar no se cancela
                C.avisar("Conversaciones unidas. Correos movidos: " + salida.correosmovidos +
                    ". Arreglos repunteados: " + salida.arreglosrepunteados + ".");
                await Xrm.Navigation.openForm({ entityName: TABLA, entityId: C.idLimpio(destino.id) });
            } catch (err) {
                await C.mostrarError(err);
            }
        });
    }

    /** Gesto 5. Custom API iniciarconversacionnueva; navega a la conversacion nueva. */
    async function iniciarNueva(primero) {
        var ctx = C.contexto(primero);
        if (ctx.ids.length !== 1) {
            await C.alerta("Guarde la conversación antes de iniciar una nueva.");
            return;
        }
        await C.conBandera(async function () {
            try {
                var texto = "El correo llegado después de la finalización se mueve a una conversación nueva; esta sigue finalizada.";
                if (!(await C.confirmar(texto))) {
                    return;
                }
                var salida = await C.ejecutarApi("sanic_carp_capi_iniciarconversacionnueva", { conversacionid: ctx.ids[0] });
                await C.refrescar(ctx);
                C.avisar("Conversación nueva iniciada. Correos movidos: " + salida.correosmovidos + ".");
                await Xrm.Navigation.openForm({ entityName: TABLA, entityId: C.idLimpio(salida.nuevaconversacionid) });
            } catch (err) {
                await C.mostrarError(err);
            }
        });
    }

    return {
        alCargar: alCargar,
        alGuardar: alGuardar,
        puedeTomar: puedeTomar,
        puedeFinalizar: puedeFinalizar,
        puedeReabrir: puedeReabrir,
        puedeUnir: puedeUnir,
        puedeIniciarNueva: puedeIniciarNueva,
        tomar: tomar,
        finalizar: finalizar,
        reabrir: reabrir,
        unir: unir,
        iniciarNueva: iniciarNueva
    };
})();

SanicCarp.Arreglo = (function () {
    "use strict";

    var C = SanicCarp.Comun;
    var TABLA = "sanic_carp_tbl_arreglo";
    var CAMPOS_GESTO = ["sanic_motivodescarte", "sanic_motivoanulacion", "sanic_motivoreapertura"];
    var AVISO = "Use el botón Descartar, Anular o Reabrir arreglo para aplicar el motivo.";
    var ESTADO_BORRADOR = 1;             // statuscode del Borrador (statecode 0)
    var ESTADO_TERMINADO = 2;            // statuscode del Terminado (statecode 1)
    var ESTADO_VIGENTE = 159460001;      // statuscode del Vigente (statecode 0)
    var ESTADO_ANULADO = 159460002;      // statuscode del Anulado (statecode 1)
    var ESTADO_DESCARTADO = 159460003;   // statuscode del Descartado (statecode 1)

    /**
     * El formulario de un registro inactivo deshabilita todos los controles. Para que el jefe pueda escribir el motivo
     * de reapertura en un Terminado (o en un Vigente congelado, por si la plataforma tambien lo deshabilita), se habilita
     * solo ese control. [verificar en Dev]: que el control habilitado por script se pueda escribir y que el boton lo envie.
     */
    function habilitarMotivoDeReapertura(fc) {
        var estado = fc.getAttribute("statuscode");
        var congelamiento = fc.getAttribute("sanic_fechacongelamiento");
        var control = fc.getControl("sanic_motivoreapertura");
        if (!estado || !control) {
            return;
        }
        var valor = estado.getValue();
        var vigenteCongelado = valor === ESTADO_VIGENTE && congelamiento !== null && congelamiento.getValue() !== null;
        if ((valor === ESTADO_TERMINADO || vigenteCongelado) && SanicCarp.Comun.esJefe()) {
            control.setDisabled(false);
        }
    }

    function alCargar(executionContext) {
        habilitarMotivoDeReapertura(SanicCarp.Comun.formContext(executionContext));
    }

    function alGuardar(executionContext) {
        SanicCarp.Comun.guardarSinGestoSuelto(executionContext, CAMPOS_GESTO, AVISO);
    }

    // --- Reglas de habilitacion (solo UX; true ante la duda) ---------------------------------------------------

    /** El formulario del arreglo, o null en una grilla (donde estas reglas no aplican: los botones son solo de formulario). */
    function formularioDe(arg) {
        var ctx = C.contexto(arg, arg);
        return ctx.tipo === "formulario" ? ctx.fc : null;
    }

    /** Congelado = fechacongelamiento con valor; ante la duda (la columna no esta en el formulario) se asume que si. */
    function estaCongelado(fc) {
        var v = C.valor(fc, "sanic_fechacongelamiento");
        return v === undefined || v !== null;
    }

    function estadoEs(fc, estado) {
        var actual = C.valor(fc, "statuscode");
        return actual === undefined || actual === estado;
    }

    /** Generar formato: siempre (tambien Terminado, Anulado y Descartado), salvo en el alta, donde todavia no hay arreglo. */
    function puedeGenerar(arg) {
        var fc = formularioDe(arg);
        return !fc || (fc.ui && typeof fc.ui.getFormType === "function" ? fc.ui.getFormType() !== 1 : true);
    }

    function puedeDesbloquear(arg) {
        var fc = formularioDe(arg);
        return !fc || (estadoEs(fc, ESTADO_BORRADOR) && estaCongelado(fc));
    }

    function puedeFormalizar(arg) {
        return puedeDesbloquear(arg);
    }

    function puedeDescartar(arg) {
        var fc = formularioDe(arg);
        return !fc || estadoEs(fc, ESTADO_BORRADOR);
    }

    function puedeAnular(arg) {
        var fc = formularioDe(arg);
        return !fc || estadoEs(fc, ESTADO_VIGENTE);
    }

    /** Reabrir arreglo: Terminado, o Vigente congelado, y solo el jefe. */
    function puedeReabrir(arg) {
        var fc = formularioDe(arg);
        if (!fc) {
            return C.esJefe();
        }
        var estado = C.valor(fc, "statuscode");
        var aplica = estado === undefined || estado === ESTADO_TERMINADO || (estado === ESTADO_VIGENTE && estaCongelado(fc));
        return aplica && C.esJefe();
    }

    // --- Gestos -------------------------------------------------------------------------------------------------

    /** Texto de la confirmacion de Generar formato: depende del estado y del congelamiento (diseno/07 5.4, gesto 6). */
    function confirmacionDeGenerar(fc) {
        var estado = C.valor(fc, "statuscode");
        var congelado = C.valor(fc, "sanic_fechacongelamiento");
        var sinCongelar = congelado === null || congelado === undefined;
        if (estado === ESTADO_BORRADOR && sinCongelar) {
            return "Al generar se bloquean los datos del acuerdo; podrá desbloquear antes de la firma.";
        }
        if (estado === ESTADO_VIGENTE && sinCongelar) {
            return "Se vuelve a congelar con los datos actuales del cliente.";
        }
        return "Se genera el formato; el arreglo no cambia.";
    }

    /** Gesto 6. Custom API generarformato; descarga el .xlsx; refresca (puede haber congelado). */
    async function generar(primero) {
        var ctx = C.contexto(primero);
        if (ctx.ids.length !== 1 || ctx.fc.data.getIsDirty()) {
            await C.alerta("Guarde los cambios antes de generar el formato.");
            return;
        }
        await C.conBandera(async function () {
            try {
                if (!(await C.confirmar(confirmacionDeGenerar(ctx.fc)))) {
                    return;
                }
                await C.generarYDescargar({ arregloid: ctx.ids[0] });
                await C.refrescar(ctx);
            } catch (err) {
                await C.mostrarError(err);
            }
        });
    }

    /** Gesto 7. SOLO sanic_fechacongelamiento = null (sobre un borrador no congelado daria CARP-ARR-017). */
    function desbloquear(primero) {
        return C.escribirGesto(C.contexto(primero), TABLA, "El formato ya impreso deja de valer; hay que reimprimirlo.",
            { sanic_fechacongelamiento: null }, "Borrador desbloqueado.");
    }

    /** Gesto 8. SOLO el estado Vigente; el numero de arreglo se relee del formulario ya refrescado. */
    function formalizar(primero) {
        return C.escribirGesto(C.contexto(primero), TABLA, "Confirme que el cliente firmó el formato impreso.",
            { statecode: 0, statuscode: ESTADO_VIGENTE },
            function (ctx) {
                var numero = C.valor(ctx.fc, "sanic_numeroarreglo");
                return C.vacio(numero) ? "Arreglo formalizado." : "Arreglo formalizado con el N.º " + numero;
            });
    }

    /** Gesto 9. El motivo se lee del campo gesto; si esta vacio lo reclama el servidor (CARP-ARR-013). */
    function descartar(primero) {
        var ctx = C.contexto(primero);
        var motivo = C.valor(ctx.fc, "sanic_motivodescarte");
        return C.escribirGesto(ctx, TABLA, "Queda como descartado sin consumir número. No se puede recuperar.",
            { statecode: 1, statuscode: ESTADO_DESCARTADO, sanic_motivodescarte: C.vacio(motivo) ? null : motivo },
            "Borrador descartado.");
    }

    /** Gesto 10. Motivo vacio: CARP-ARR-014. */
    function anular(primero) {
        var ctx = C.contexto(primero);
        var motivo = C.valor(ctx.fc, "sanic_motivoanulacion");
        return C.escribirGesto(ctx, TABLA,
            "La anulación no se puede revertir. Si el cliente vuelve a negociar, se registra un arreglo nuevo.",
            { statecode: 1, statuscode: ESTADO_ANULADO, sanic_motivoanulacion: C.vacio(motivo) ? null : motivo },
            "Arreglo anulado.");
    }

    /** Gesto 11. De un Terminado: estado Vigente + motivo. De un Vigente congelado: SOLO el motivo. */
    function reabrir(primero) {
        var ctx = C.contexto(primero);
        var motivo = C.valor(ctx.fc, "sanic_motivoreapertura");
        var datos = { sanic_motivoreapertura: C.vacio(motivo) ? null : motivo };
        if (C.valor(ctx.fc, "statuscode") === ESTADO_TERMINADO) {
            datos.statecode = 0;
            datos.statuscode = ESTADO_VIGENTE;
        }
        return C.escribirGesto(ctx, TABLA,
            "Descongela los datos para corregirlos. El próximo formato congela de nuevo con los datos actuales.",
            datos, "Arreglo reabierto.");
    }

    return {
        alCargar: alCargar,
        alGuardar: alGuardar,
        puedeGenerar: puedeGenerar,
        puedeDesbloquear: puedeDesbloquear,
        puedeFormalizar: puedeFormalizar,
        puedeDescartar: puedeDescartar,
        puedeAnular: puedeAnular,
        puedeReabrir: puedeReabrir,
        generar: generar,
        desbloquear: desbloquear,
        formalizar: formalizar,
        descartar: descartar,
        anular: anular,
        reabrir: reabrir
    };
})();

SanicCarp.Cuota = (function () {
    "use strict";

    // Sin esto, escribir la fecha de pago y esperar 30 s daria CARP-CUO-003 (fecha sin monto).
    function alGuardar(executionContext) {
        SanicCarp.Comun.cancelarAutoguardado(executionContext);
    }

    return { alGuardar: alGuardar };
})();

SanicCarp.Plantilla = (function () {
    "use strict";

    var C = SanicCarp.Comun;
    var TABLA = "sanic_carp_tbl_versionplantilla";
    var TABLA_ARREGLO = "sanic_carp_tbl_arreglo";
    var ESTADO_BORRADOR = 159460001;     // sanic_estado: Borrador
    var ESTADO_VIGENTE = 159460002;      // sanic_estado: Vigente

    // Mapeo de celda: sin esto, CARP-MAP-003 / -004 rebotan con el campo y el tipo a medio elegir.
    function alGuardarMapeo(executionContext) {
        SanicCarp.Comun.cancelarAutoguardado(executionContext);
    }

    // --- Reglas de habilitacion (solo UX; true ante la duda) ---------------------------------------------------

    /** Borrador y jefe. Publicar y Vista previa comparten la regla. */
    function borradorYJefe(arg) {
        var ctx = C.contexto(arg, arg);
        if (!C.esJefe()) {
            return false;
        }
        if (ctx.tipo !== "formulario") {
            return true;
        }
        var estado = C.valor(ctx.fc, "sanic_estado");
        return estado === undefined || estado === ESTADO_BORRADOR;
    }

    function puedePublicar(arg) {
        return borradorYJefe(arg);
    }

    function puedeVistaPrevia(arg) {
        return borradorYJefe(arg);
    }

    // --- Gestos -------------------------------------------------------------------------------------------------

    /** Gesto 13. SOLO sanic_estado = Vigente (el servidor rechaza cualquier otra columna, CARP-VER-013). */
    async function publicar(primero) {
        var ctx = C.contexto(primero);
        if (ctx.ids.length !== 1 || ctx.fc.data.getIsDirty()) {
            await C.alerta("Guarde los cambios antes de publicar.");
            return;
        }
        return C.escribirGesto(ctx, TABLA,
            "La versión pasa a Vigente, la actual pasa a Histórica y no se podrá modificar.",
            { sanic_estado: ESTADO_VIGENTE }, "Versión publicada.");
    }

    /** Gesto 14. Selector nativo de un arreglo (cualquier estado) + generarformato con versionpreviaid. No escribe nada. */
    async function vistaPrevia(primero) {
        var ctx = C.contexto(primero);
        if (ctx.ids.length !== 1) {
            await C.alerta("Guarde la versión antes de la vista previa.");
            return;
        }
        await C.conBandera(async function () {
            try {
                if (!(await C.confirmar("Se genera con un arreglo existente; no congela ni guarda nada."))) {
                    return;
                }
                var elegidos = await Xrm.Utility.lookupObjects({
                    allowMultiSelect: false,
                    defaultEntityType: TABLA_ARREGLO,
                    entityTypes: [TABLA_ARREGLO],
                    disableMru: true
                });
                if (!elegidos || elegidos.length === 0) {
                    return;
                }
                await C.generarYDescargar({ arregloid: elegidos[0].id, versionpreviaid: ctx.ids[0] });
            } catch (err) {
                await C.mostrarError(err);
            }
        });
    }

    return {
        alGuardarMapeo: alGuardarMapeo,
        puedePublicar: puedePublicar,
        puedeVistaPrevia: puedeVistaPrevia,
        publicar: publicar,
        vistaPrevia: vistaPrevia
    };
})();
