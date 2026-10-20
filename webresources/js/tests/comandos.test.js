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
