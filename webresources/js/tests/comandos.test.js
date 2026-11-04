/*
 * Pruebas offline de comandos.js (node --test webresources/js/tests/).
 * Xrm simulado: el serializador de Xrm.WebApi.online.execute lee un parametro Edm.Guid (structuralProperty 1)
 * desde `valor.guid` (Inogic, "Work with all available parameter types in Custom API", 2021; y el error real del
 * 2026-10-20 en Dev: "Cannot convert the literal 'undefined' to the expected type 'Edm.Guid'" cuando se paso un texto).
 */
"use strict";
const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const FUENTE = fs.readFileSync(path.join(__dirname, "..", "comandos.js"), "utf8");
const GUID = "0d68be71-aecc-f111-a05b-00224808ffcd";

/** Serializa el cuerpo como lo hace Xrm para los primitivos: Edm.Guid desde `.guid`, el resto tal cual. */
function serializar(peticion) {
    const meta = peticion.getMetadata();
    const cuerpo = {};
    Object.keys(meta.parameterTypes).forEach(function (nombre) {
        const tipo = meta.parameterTypes[nombre];
        const v = peticion[nombre];
        cuerpo[nombre] = tipo.typeName === "Edm.Guid" ? String(v && v.guid) : v;
    });
    return { nombre: meta.operationName, cuerpo: cuerpo };
}

function cargar() {
    const enviadas = [];
    const Xrm = {
        WebApi: {
            online: {
                execute: async function (peticion) {
                    const s = serializar(peticion);
                    enviadas.push(s);
                    const malo = Object.keys(s.cuerpo).find(function (k) {
                        return !/^[0-9a-f-]{36}$/.test(s.cuerpo[k]);
                    });
                    if (malo) {
                        return { ok: false, json: async function () {
                            return { error: { message: "Cannot convert the literal '" + s.cuerpo[malo] + "' to the expected type 'Edm.Guid'." } };
                        } };
                    }
                    return { ok: true, json: async function () { return { yaasignada: false, oficialnombre: "User 8" }; } };
                }
            }
        }
    };
    const caja = { Xrm: Xrm, console: console };
    vm.createContext(caja);
    vm.runInContext(FUENTE + "\nthis.SanicCarp = SanicCarp;", caja);
    return { C: caja.SanicCarp.Comun, enviadas: enviadas };
}

test("ejecutarApi manda el Guid en el formato que serializa Xrm ({guid})", async function () {
    const { C, enviadas } = cargar();
    const salida = await C.ejecutarApi("sanic_carp_capi_tomarconversacion", { conversacionid: "{" + GUID.toUpperCase() + "}" });
    assert.strictEqual(enviadas[0].cuerpo.conversacionid, GUID);
    assert.strictEqual(salida.oficialnombre, "User 8");
});

test("ejecutarApi omite un parametro opcional sin valor", async function () {
    const { C, enviadas } = cargar();
    await C.ejecutarApi("sanic_carp_capi_generarformato", { arregloid: GUID, versionpreviaid: undefined });
    assert.deepStrictEqual(Object.keys(enviadas[0].cuerpo), ["arregloid"]);
});

test("ejecutarApi rechaza un Guid invalido sin llamar al servidor", async function () {
    const { C, enviadas } = cargar();
    await assert.rejects(C.ejecutarApi("sanic_carp_capi_tomarconversacion", { conversacionid: "undefined" }));
    await assert.rejects(C.ejecutarApi("sanic_carp_capi_tomarconversacion", { conversacionid: "" }));
    assert.strictEqual(enviadas.length, 0);
});

test("contexto toma el id del formulario", function () {
    const { C } = cargar();
    const fc = { getAttribute: function () { return null; }, data: { entity: { getId: function () { return "{" + GUID.toUpperCase() + "}"; } } } };
    assert.deepStrictEqual(Array.from(C.contexto(fc).ids), [GUID]);
});

test("textoDeError muestra solo el texto de una Custom API, sin el codigo CARP", function () {
    const { C } = cargar();
    assert.strictEqual(C.textoDeError({ message: "CARP-TOM-001: La conversación ya está asignada." }).message, "La conversación ya está asignada.");
});

test("textoDeError quita el codigo de cada linea (errores de publicacion unidos con salto de linea)", function () {
    const { C } = cargar();
    const texto = C.textoDeError({ message: "CARP-VER-007: Primer error.\nCARP-VER-008: Segundo error.\nTercero sin codigo." }).message;
    assert.strictEqual(texto, "Primer error.\nSegundo error.\nTercero sin codigo.");
});

test("textoDeError muestra tal cual el rechazo de un plugin (0x80040265) sin codigo", function () {
    const { C } = cargar();
    const r = C.textoDeError({ errorCode: -2147220891, message: "El arreglo no admite ese cambio." });
    assert.strictEqual(r.message, "El arreglo no admite ese cambio.");
    assert.strictEqual(r.details, undefined);
});

test("textoDeError usa el texto generico con details cuando no es un rechazo conocido", function () {
    const { C } = cargar();
    const r = C.textoDeError({ errorCode: 500, message: "Boom tecnico" });
    assert.ok(r.message.indexOf("No se pudo completar la acción") === 0);
    assert.strictEqual(r.details, "Boom tecnico");
    assert.strictEqual(C.textoDeError({}).details, undefined);
});

// ---------------------------------------------------------------------------------------------------------
// Arreglo: documento firmado (diseno/03 2.6, "Documento firmado") y conversacion de origen bloqueada fuera del Borrador
// (pedido del usuario en la prueba guiada, 2026-11-03)
// ---------------------------------------------------------------------------------------------------------

const BORRADOR = 1;
const VIGENTE = 159460001;
const TERMINADO = 2;

/** Carga comandos.js con un Xrm que registra lo que pasa (alertas, confirmaciones, consultas y escrituras). */
function cargarConRegistro(opciones) {
    const reg = { alertas: [], confirmaciones: [], consultas: [], escrituras: [], avisos: [] };
    const Xrm = {
        Navigation: {
            openAlertDialog: async function (o) { reg.alertas.push(o.text); },
            openConfirmDialog: async function (o) { reg.confirmaciones.push(o.text); return { confirmed: opciones.confirma !== false }; },
            openErrorDialog: async function (o) { reg.errores = (reg.errores || []).concat(o.message); }
        },
        WebApi: {
            retrieveRecord: async function (tabla, id, consulta) {
                reg.consultas.push({ tabla: tabla, id: id, consulta: consulta });
                if (opciones.consultaFalla) {
                    throw new Error("sin red");
                }
                return { sanic_documentofirmado_name: opciones.nombreDelDocumento };
            },
            updateRecord: async function (tabla, id, datos) { reg.escrituras.push({ tabla: tabla, id: id, datos: datos }); }
        },
        App: { addGlobalNotification: async function (n) { reg.avisos.push(n.message); } },
        Utility: { getGlobalContext: function () { return { userSettings: { roles: [] } }; } }
    };
    const caja = { Xrm: Xrm, console: console };
    vm.createContext(caja);
    vm.runInContext(FUENTE + "\nthis.SanicCarp = SanicCarp;", caja);
    return { A: caja.SanicCarp.Arreglo, reg: reg };
}

/** Un formulario de arreglo simulado: atributos con valor y controles que anotan setDisabled. */
function formulario(valores, controles) {
    const llamadas = {};
    const fc = {
        getAttribute: function (nombre) {
            return Object.prototype.hasOwnProperty.call(valores, nombre) ? { getValue: function () { return valores[nombre]; } } : null;
        },
        getControl: function (nombre) {
            if (controles.indexOf(nombre) < 0) { return null; }
            return { setDisabled: function (v) { llamadas[nombre] = v; } };
        },
        data: {
            entity: { getId: function () { return "{" + GUID.toUpperCase() + "}"; } },
            getIsDirty: function () { return false; },
            refresh: async function () { }
        },
        ui: { refreshRibbon: function () { } }
    };
    return { fc: fc, llamadas: llamadas, ctx: { getFormContext: function () { return fc; } } };
}

test("alCargar en un Vigente deshabilita la conversacion de origen y el documento firmado", function () {
    const { A } = cargarConRegistro({});
    const f = formulario({ statuscode: VIGENTE, sanic_fechacongelamiento: new Date() }, ["sanic_conversacionid", "sanic_documentofirmado"]);
    A.alCargar(f.ctx);
    assert.strictEqual(f.llamadas.sanic_conversacionid, true);
    assert.strictEqual(f.llamadas.sanic_documentofirmado, true);
});

test("alCargar en un Terminado deshabilita la conversacion de origen y el documento firmado", function () {
    const { A } = cargarConRegistro({});
    const f = formulario({ statuscode: TERMINADO, sanic_fechacongelamiento: new Date() }, ["sanic_conversacionid", "sanic_documentofirmado"]);
    A.alCargar(f.ctx);
    assert.strictEqual(f.llamadas.sanic_conversacionid, true);
    assert.strictEqual(f.llamadas.sanic_documentofirmado, true);
});

test("alCargar en un Borrador congelado deja la conversacion como esta y habilita el documento firmado", function () {
    const { A } = cargarConRegistro({});
    const f = formulario({ statuscode: BORRADOR, sanic_fechacongelamiento: new Date() }, ["sanic_conversacionid", "sanic_documentofirmado"]);
    A.alCargar(f.ctx);
    assert.strictEqual(Object.prototype.hasOwnProperty.call(f.llamadas, "sanic_conversacionid"), false);
    assert.strictEqual(f.llamadas.sanic_documentofirmado, false);
});

test("alCargar en un Borrador sin congelar deshabilita el documento firmado (primero hay que generar el formato)", function () {
    const { A } = cargarConRegistro({});
    const f = formulario({ statuscode: BORRADOR, sanic_fechacongelamiento: null }, ["sanic_conversacionid", "sanic_documentofirmado"]);
    A.alCargar(f.ctx);
    assert.strictEqual(Object.prototype.hasOwnProperty.call(f.llamadas, "sanic_conversacionid"), false);
    assert.strictEqual(f.llamadas.sanic_documentofirmado, true);
});

test("alCargar sin los controles en el formulario no rompe (ante la duda no toca nada)", function () {
    const { A } = cargarConRegistro({});
    const f = formulario({ statuscode: VIGENTE }, []);
    assert.doesNotThrow(function () { A.alCargar(f.ctx); });
    const sinEstado = formulario({}, ["sanic_conversacionid", "sanic_documentofirmado"]);
    A.alCargar(sinEstado.ctx);
    assert.deepStrictEqual(sinEstado.llamadas, {});
});

test("formalizar sin documento firmado avisa en el campo del formulario y NO llama al servidor ni confirma", async function () {
    const { A, reg } = cargarConRegistro({ nombreDelDocumento: null });
    const f = formulario({ statuscode: BORRADOR, sanic_fechacongelamiento: new Date() }, ["sanic_documentofirmado"]);
    const aplico = await A.formalizar(f.fc);
    assert.strictEqual(aplico, false);
    assert.strictEqual(reg.escrituras.length, 0);
    assert.strictEqual(reg.confirmaciones.length, 0);
    assert.strictEqual(reg.alertas.length, 1);
    assert.ok(reg.alertas[0].indexOf("documento firmado") >= 0 && reg.alertas[0].indexOf("PDF, JPG o PNG") >= 0);
    assert.ok(reg.alertas[0].indexOf("CARP-") < 0);
    assert.strictEqual(reg.consultas[0].tabla, "sanic_carp_tbl_arreglo");
    assert.strictEqual(reg.consultas[0].consulta, "?$select=sanic_documentofirmado_name");
});

test("formalizar con el nombre del documento vacio tambien avisa", async function () {
    const { A, reg } = cargarConRegistro({ nombreDelDocumento: "" });
    const f = formulario({ statuscode: BORRADOR, sanic_fechacongelamiento: new Date() }, []);
    assert.strictEqual(await A.formalizar(f.fc), false);
    assert.strictEqual(reg.escrituras.length, 0);
});

test("formalizar con documento firmado sigue el flujo: confirma (menciona el documento) y escribe SOLO el estado Vigente", async function () {
    const { A, reg } = cargarConRegistro({ nombreDelDocumento: "firmado.pdf" });
    const f = formulario({ statuscode: BORRADOR, sanic_fechacongelamiento: new Date() }, []);
    const aplico = await A.formalizar(f.fc);
    assert.strictEqual(aplico, true);
    assert.strictEqual(reg.alertas.length, 0);
    assert.strictEqual(reg.confirmaciones.length, 1);
    assert.ok(reg.confirmaciones[0].indexOf("documento firmado") >= 0);
    // El objeto nace en el contexto de vm (otro prototipo): se compara por contenido.
    assert.deepStrictEqual(JSON.parse(JSON.stringify(reg.escrituras[0].datos)), { statecode: 0, statuscode: VIGENTE });
    assert.strictEqual(reg.escrituras[0].id, GUID);
});

test("formalizar con documento pero sin confirmar no escribe", async function () {
    const { A, reg } = cargarConRegistro({ nombreDelDocumento: "firmado.png", confirma: false });
    const f = formulario({ statuscode: BORRADOR, sanic_fechacongelamiento: new Date() }, []);
    assert.strictEqual(await A.formalizar(f.fc), false);
    assert.strictEqual(reg.escrituras.length, 0);
});

test("formalizar: si la consulta del documento falla, el servidor decide (se sigue al flujo normal)", async function () {
    const { A, reg } = cargarConRegistro({ consultaFalla: true });
    const f = formulario({ statuscode: BORRADOR, sanic_fechacongelamiento: new Date() }, []);
    assert.strictEqual(await A.formalizar(f.fc), true);
    assert.strictEqual(reg.alertas.length, 0);
    assert.strictEqual(reg.escrituras.length, 1);
});

test("el aviso de formalizar sin documento no lleva codigos internos", function () {
    assert.ok(FUENTE.indexOf("Antes de formalizar, adjunte el documento firmado (PDF, JPG o PNG)") >= 0);
});
