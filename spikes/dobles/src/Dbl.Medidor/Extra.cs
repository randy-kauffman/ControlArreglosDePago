using System.Text.Json.Nodes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dbl.Medidor;

public static partial class Program
{
    /// <summary>Complementos descubiertos al leer la primera pasada: (a) statuscode=1 (el motivo por defecto de Activo) enviado con statecode Inactivo en Conversacion y Arreglo
    /// (en el email la plataforma lo acepto y lo reemplazo por el motivo por defecto del estado); (b) otras clases de Create fallido y si consumen autonumerico.</summary>
    private static void Extra()
    {
        Paso("filas base", () =>
        {
            Crear("cli-A", new Entity(CLI) { ["sanic_nombre"] = "ZZF-José", ["sanic_nombrecompleto"] = "ZZF-José", ["sanic_cedula"] = "ZZF-ced-A", ["ownerid"] = Dueno() });
            Crear("ver-A", VerE("ZZF-vA"));
        });
        var cli = G("cli-A"); var ver = G("ver-A");

        Paso("(6b) statuscode=1 hacia Inactivo", () =>
        {
            var conv = new JsonArray(); var arr = new JsonArray();
            Crear("conv-T1", ConvE("T1"));
            conv.Add(UpdateEstado("T1 en 0/1: Update con statecode=1 + statuscode=1", CONV, "conv-T1", 1, 1));
            conv.Add(UpdateEstado("T1 en 1/2: Update con statecode=1 + statuscode=1", CONV, "conv-T1", 1, 1));
            conv.Add(UpdateEstado("T1 en 1/2: Update con statecode=0 + statuscode=1", CONV, "conv-T1", 0, 1));
            conv.Add(UpdateEstado("T1 en 0/1: Update con statecode=1 + statuscode=159460001", CONV, "conv-T1", 1, 159460001));
            conv.Add(UpdateEstado("T1 en 1/159460001: Update con statecode=1 + statuscode=1", CONV, "conv-T1", 1, 1));
            conv.Add(UpdateEstado("T1 en 1/2: Update con statecode=0 + statuscode=2 (par invalido, statuscode=2 no es el 1)", CONV, "conv-T1", 0, 2));
            Crear("arr-T1", ArrE("T1", cli, ver));
            arr.Add(UpdateEstado("T1 en 0/1: Update con statecode=1 + statuscode=1", ARR, "arr-T1", 1, 1));
            arr.Add(UpdateEstado("T1 en 1/2: Update con statecode=0 + statuscode=1", ARR, "arr-T1", 0, 1));
            arr.Add(UpdateEstado("T1 en 0/1: Update con statecode=0 + statuscode=159460001 (Vigente)", ARR, "arr-T1", 0, 159460001));
            arr.Add(UpdateEstado("T1 en 0/159460001: Update con statecode=1 + statuscode=1", ARR, "arr-T1", 1, 1));
            arr.Add(UpdateEstado("T1 en 1/2: Update con statecode=1 + statuscode=159460002 (Anulado)", ARR, "arr-T1", 1, 159460002));
            arr.Add(UpdateEstado("T1 en 1/159460002: Update con statecode=1 + statuscode=1", ARR, "arr-T1", 1, 1));
            Escribir("13b-estados-statuscode-1.json", new JsonObject
            {
                ["cabecera"] = Cabecera("13b-estados-statuscode-1", "SINTETICAS ZZF- (Conversacion T1 y Arreglo T1)",
                    "Complemento del punto (6): en el email la plataforma acepto statuscode=1 con statecode Completado/Cancelado y lo reemplazo por el motivo por defecto del estado. Se mide si en Conversacion y Arreglo pasa lo mismo."),
                ["contenido"] = new JsonObject { ["conversacion"] = conv, ["arreglo"] = arr },
            });
        });

        Paso("(11b) otros Create fallidos y autonumerico", () =>
        {
            var casos = new JsonArray(); var seq = new JsonArray();
            void Nota(string tabla, string op, string clave) { var n = Nombre(tabla, clave); seq.Add(new JsonObject { ["tabla"] = tabla, ["operacion"] = op, ["sanic_nombre"] = n, ["numero"] = Num(n) }); }
            Crear("conv-E1", ConvE("E1")); Nota(CONV, "alta E1", "conv-E1");
            casos.Add(Intento("conv: Create con columna inexistente", () => { CrearIntento("conv-f1", ConvE("f1", e => e["sanic_noexiste"] = "x")); return null; }));
            Crear("conv-E2", ConvE("E2")); Nota(CONV, "alta E2 (tras Create fallido por columna inexistente)", "conv-E2");
            casos.Add(Intento("conv: Create con huella de 65 caracteres (texto largo)", () => { CrearIntento("conv-f2", ConvE(Rep("h", 59))); return null; }));
            Crear("conv-E3", ConvE("E3")); Nota(CONV, "alta E3 (tras Create fallido por texto largo)", "conv-E3");
            casos.Add(Intento("conv: Create con choice inexistente (sanic_resultado = 999)", () => { CrearIntento("conv-f3", ConvE("f3", e => e["sanic_resultado"] = new OptionSetValue(999))); return null; }));
            Crear("conv-E4", ConvE("E4")); Nota(CONV, "alta E4 (tras Create fallido por choice inexistente)", "conv-E4");
            casos.Add(Intento("conv: Create con tipo equivocado (sanic_cantidadrespuestas = '5')", () => { CrearIntento("conv-f4", ConvE("f4", e => e["sanic_cantidadrespuestas"] = "5")); return null; }));
            Crear("conv-E5", ConvE("E5")); Nota(CONV, "alta E5 (tras Create fallido por tipo equivocado)", "conv-E5");
            casos.Add(Intento("conv: Create con tabla de lookup equivocada (sanic_clienteid -> version)", () => { CrearIntento("conv-f5", ConvE("f5", e => e["sanic_clienteid"] = new EntityReference(VER, ver))); return null; }));
            Crear("conv-E6", ConvE("E6")); Nota(CONV, "alta E6 (tras Create fallido por lookup a tabla equivocada)", "conv-E6");
            Crear("arr-E1", ArrE("E1", cli, ver)); Nota(ARR, "alta A-E1", "arr-E1");
            casos.Add(Intento("arr: Create con columna inexistente", () => { CrearIntento("arr-f1", ArrE("f1", cli, ver, e => e["sanic_noexiste"] = "x")); return null; }));
            Crear("arr-E2", ArrE("E2", cli, ver)); Nota(ARR, "alta A-E2 (tras Create fallido por columna inexistente)", "arr-E2");
            casos.Add(Intento("arr: Create con par statecode/statuscode invalido (0,2)", () => { CrearIntento("arr-f2", ArrE("f2", cli, ver, e => { e["statecode"] = O(0); e["statuscode"] = O(2); })); return null; }));
            Crear("arr-E3", ArrE("E3", cli, ver)); Nota(ARR, "alta A-E3 (tras Create fallido por par invalido)", "arr-E3");
            var deltas = new JsonArray();
            foreach (var tabla in new[] { CONV, ARR })
            {
                var buenas = seq.Where(n => n!["tabla"]!.GetValue<string>() == tabla).ToList();
                for (int i = 1; i < buenas.Count; i++)
                    deltas.Add(new JsonObject { ["tabla"] = tabla, ["de"] = buenas[i - 1]!["operacion"]!.GetValue<string>(), ["a"] = buenas[i]!["operacion"]!.GetValue<string>(), ["delta"] = buenas[i]!["numero"]!.GetValue<int>() - buenas[i - 1]!["numero"]!.GetValue<int>() });
            }
            Escribir("17b-autonumerico-otras-fallas.json", new JsonObject
            {
                ["cabecera"] = Cabecera("17b-autonumerico-otras-fallas", "SINTETICAS ZZF- (borradas al final)",
                    "Complemento del punto (11): otras clases de Create fallido (columna inexistente, texto largo, choice inexistente, tipo equivocado, lookup a la tabla equivocada, par de estado invalido en Arreglo) y si consumen numero.",
                    "NO se uso SetAutoNumberSeed. delta 1 = no consumio; delta 2 = el Create fallido consumio un numero."),
                ["contenido"] = new JsonObject { ["secuencia_cronologica"] = seq, ["deltas_entre_altas_exitosas_consecutivas"] = deltas, ["creates_fallidos"] = casos },
            });
        });
    }
}
