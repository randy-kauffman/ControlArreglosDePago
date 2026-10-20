/*
 * WR - CARP - JS - Comandos  (sanic_carp_wr_js_comandos)
 * diseno/07 5.1 a 5.3. Sin logica de negocio: las reglas viven en los plugins.
 *
 * LOTE A (esta version): solo los eventos de formulario OnLoad y OnSave.
 *   Conversacion : SanicCarp.Conversacion.alGuardar
 *   Arreglo      : SanicCarp.Arreglo.alCargar / alGuardar
 *   Cuota        : SanicCarp.Cuota.alGuardar
 *   Mapeo        : SanicCarp.Plantilla.alGuardarMapeo
 * Los 14 gestos del boton (lote B) se agregan a este mismo archivo.
 *
 * Campos "gesto": NO se usa setSubmitMode("never") (Learn: con "never" la columna no se puede editar).
 * En su lugar, el OnSave cancela el guardado, en cualquier modo, mientras un campo gesto este "dirty",
 * salvo que lo dispare el propio boton del gesto (SanicCarp.Comun.levantarBandera / bajarBandera).
 *
 * Todos los manejadores se registran con "pasar el contexto de ejecucion como primer parametro".
 */
var SanicCarp = SanicCarp || {};

SanicCarp.Comun = (function () {
    "use strict";

    var ROL_JEFE = "SR - CARP - Jefe";
    var MODO_AUTOGUARDADO = 70;   // Learn, getSaveMode: 70 = Auto Save
    var ID_AVISO = "sanic_carp_gesto_pendiente";

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

    return {
        levantarBandera: levantarBandera,
        bajarBandera: bajarBandera,
        banderaLevantada: banderaLevantada,
        formContext: formContext,
        hayGestoPendiente: hayGestoPendiente,
        guardarSinGestoSuelto: guardarSinGestoSuelto,
        cancelarAutoguardado: cancelarAutoguardado,
        esJefe: esJefe
    };
})();

SanicCarp.Conversacion = (function () {
    "use strict";

    var CAMPOS_GESTO = ["sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta"];
    var AVISO = "Use el botón Finalizar para aplicar el resultado y el motivo.";

    function alGuardar(executionContext) {
        SanicCarp.Comun.guardarSinGestoSuelto(executionContext, CAMPOS_GESTO, AVISO);
    }

    return { alGuardar: alGuardar };
})();

SanicCarp.Arreglo = (function () {
    "use strict";

    var CAMPOS_GESTO = ["sanic_motivodescarte", "sanic_motivoanulacion", "sanic_motivoreapertura"];
    var AVISO = "Use el botón Descartar, Anular o Reabrir arreglo para aplicar el motivo.";
    var ESTADO_TERMINADO = 2;            // statuscode del Terminado (statecode 1)
    var ESTADO_VIGENTE = 159460001;      // statuscode del Vigente (statecode 0)

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

    return { alCargar: alCargar, alGuardar: alGuardar };
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

    // Mapeo de celda: sin esto, CARP-MAP-003 / -004 rebotan con el campo y el tipo a medio elegir.
    function alGuardarMapeo(executionContext) {
        SanicCarp.Comun.cancelarAutoguardado(executionContext);
    }

    return { alGuardarMapeo: alGuardarMapeo };
})();
