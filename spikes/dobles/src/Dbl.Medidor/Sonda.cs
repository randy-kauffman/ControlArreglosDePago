using System.Text.Json.Nodes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dbl.Medidor;

public static partial class Program
{
    /// <summary>Reserva un GUID para una fila que va a crear el plugin: queda en estado.json ANTES de que exista (borrado con 404 o ya_no_existia si nunca nace).</summary>
    private static Guid Reservar(string clave, string tabla)
    {
        var id = Guid.NewGuid();
        Estado.Registrar(clave, tabla, id);
        Claves[id] = clave; PorClave[clave] = id;
        return id;
    }

    private static JsonNode Existencia(params string[] claves)
    {
        var o = new JsonObject();
        foreach (var k in claves) o[k] = Existe(k);
        return o;
    }

    private static string? NombreAuto(string tabla, string clave)
    {
        try { return Leer(tabla, G(clave), "sanic_nombre").GetAttributeValue<string>("sanic_nombre"); }
        catch { return null; }
    }

    private static string ListaIds(params Guid[] ids) => "ZZF-SND-ids:" + string.Join(",", ids);

    /// <summary>Escenarios que disparan el plugin sonda (registrado por d30 sobre tablas propias). Cada uno: operacion de afuera, resultado y filas que quedaron.</summary>
    private static void Sonda()
    {
        var esc = new JsonArray();
        var ventanaIni = DateTime.UtcNow;
        Paso("filas base de la sonda", () =>
        {
            Crear("cli-S", new Entity(CLI) { ["sanic_nombre"] = "ZZF-sonda-base", ["sanic_nombrecompleto"] = "ZZF-sonda-base", ["sanic_cedula"] = "ZZF-ced-S", ["ownerid"] = Dueno() });
            Crear("ver-S", VerE("ZZF-vS"));
        });

        JsonObject Cliente(string clave, string marca, Guid[]? ids, bool conId)
        {
            return Intento($"Create de cliente '{marca}' ({(conId ? "con" : "sin")} id enviado)", () =>
            {
                var e = new Entity(CLI) { ["sanic_nombre"] = marca, ["sanic_nombrecompleto"] = ids == null ? "ZZF-sonda" : ListaIds(ids), ["sanic_cedula"] = "ZZF-ced-" + clave, ["ownerid"] = Dueno() };
                Guid id;
                if (conId) id = Crear(clave, e); else id = CrearIntento(clave, e);
                return new JsonObject { ["id_devuelto_igual_al_enviado"] = conId ? id == e.Id : null, ["existe_despues"] = true };
            });
        }

        // ---------------------------------------------------------------- (f) ids
        Paso("sonda: ids", () =>
        {
            var o = new JsonObject { ["escenario"] = "ids / idsenv", ["descripcion"] = "Create de Cliente SIN id enviado y CON id enviado: Target.Id y PrimaryEntityId en PreValidation, PreOperation y PostOperation (trazas)" };
            o["sin_id"] = Cliente("cli-ids-a", "ZZF-SND-ids-a", null, false);
            o["con_id"] = Cliente("cli-idsenv-b", "ZZF-SND-idsenv-b", null, true);
            esc.Add(o);
        });

        // ---------------------------------------------------------------- (f) SharedVariables y escritura anidada, mismo usuario y SYSTEM; (11) autonumerico lo hace el plugin
        foreach (var variante in new[] { "sv", "sys" })
            Paso("sonda: " + variante, () =>
            {
                var o = new JsonObject { ["escenario"] = variante, ["descripcion"] = "PreOperation del Create de Cliente pone una SharedVariable y crea una Conversacion con IOrganizationService (" + (variante == "sv" ? "mismo usuario" : "SYSTEM") + "); los steps de la Conversacion (PreValidation, PreOperation, PostOperation) y el PostOperation del Cliente leen sus SharedVariables" };
                Crear("conv-" + variante + "-x1", ConvE(variante + "x1"));
                var g0 = Reservar("conv-" + variante + "-anidada", CONV);
                o["create_cliente"] = Cliente("cli-" + variante, "ZZF-SND-" + variante + "-a", new[] { g0 }, true);
                Crear("conv-" + variante + "-x2", ConvE(variante + "x2"));
                o["existencia"] = Existencia("cli-" + variante, "conv-" + variante + "-anidada");
                o["autonumerico"] = new JsonObject
                {
                    ["alta_plana_antes"] = Num(NombreAuto(CONV, "conv-" + variante + "-x1")),
                    ["alta_hecha_por_el_plugin"] = Existe("conv-" + variante + "-anidada") is JsonValue ev && ev.TryGetValue<bool>(out var hay) && hay ? Num(NombreAuto(CONV, "conv-" + variante + "-anidada")) : null,
                    ["alta_plana_despues"] = Num(NombreAuto(CONV, "conv-" + variante + "-x2")),
                };
                esc.Add(o);
            });

        // ---------------------------------------------------------------- (g) transaccion: falla de plataforma ATRAPADA en PreOperation y se sigue
        var descTx = new Dictionary<string, string>
        {
            ["tx0"] = "control: dos escrituras anidadas que funcionan (sin falla atrapada)",
            ["tx1"] = "se atrapa una clave alterna duplicada (K-01) en una escritura anidada y se SIGUE con otras escrituras",
            ["tx2"] = "se atrapa un Retrieve de fila inexistente y se sigue con otras escrituras",
            ["tx3"] = "se atrapa un Update de fila inexistente y se sigue con otras escrituras",
            ["tx4"] = "se atrapa un Create con lookup a fila inexistente y se sigue con otras escrituras",
        };
        foreach (var tx in new[] { "tx0", "tx1", "tx2", "tx3", "tx4" })
            Paso("sonda: " + tx, () =>
            {
                var o = new JsonObject { ["escenario"] = tx, ["descripcion"] = descTx[tx] };
                var gs = new[] { Reservar(tx + "-g0", CONV), Reservar(tx + "-g1", CONV), Reservar(tx + "-g2", CONV), Reservar(tx + "-g3", CONV) };
                o["create_cliente"] = Cliente("cli-" + tx, "ZZF-SND-" + tx + "-a", gs, true);
                o["existencia"] = Existencia("cli-" + tx, tx + "-g0", tx + "-g1", tx + "-g2", tx + "-g3");
                esc.Add(o);
            });

        // ---------------------------------------------------------------- (11) un Create que se revierte consume numero
        Paso("sonda: rev", () =>
        {
            var o = new JsonObject { ["escenario"] = "rev", ["descripcion"] = "el PostOperation de un Create de Conversacion lanza InvalidPluginExecutionException DESPUES de que la plataforma asigno el autonumerico: se revierte" };
            Crear("conv-rev-0", ConvE("rev0"));
            o["create_que_se_revierte"] = Intento("Create de Conversacion 'ZZF-SND-rev-a' (el plugin lo revierte)", () => { CrearIntento("conv-rev-a", ConvE("x").Also2(e => e["sanic_huellamessageid"] = "ZZF-SND-rev-a")); return null; });
            Crear("conv-rev-1", ConvE("rev1"));
            o["autonumerico"] = new JsonObject { ["alta_plana_antes"] = Num(NombreAuto(CONV, "conv-rev-0")), ["alta_plana_despues"] = Num(NombreAuto(CONV, "conv-rev-1")) };
            o["la_fila_revertida_existe"] = PorClave.ContainsKey("conv-rev-a") ? Existe("conv-rev-a") : false;
            esc.Add(o);
        });

        // ---------------------------------------------------------------- (16) tipos y Kind dentro del sandbox
        Paso("sonda: tipos", () =>
        {
            var o = new JsonObject { ["escenario"] = "tipos", ["descripcion"] = "Create y Update de un Arreglo: tipos .NET y DateTime.Kind del Target, de la pre-imagen y de un Retrieve hecho desde el plugin" };
            var cli = G("cli-S"); var ver = G("ver-S");
            o["create"] = Intento("Create de Arreglo 'ZZF-SND-tipos-a' (UL Utc, DO Unspecified)", () =>
            {
                Crear("arr-tipos", ArrE("x", cli, ver, e => { e["sanic_numeroproducto"] = "ZZF-SND-tipos-a"; e["sanic_fechacongelamiento"] = new DateTime(2026, 3, 5, 13, 45, 30, DateTimeKind.Utc); }));
                return null;
            });
            JsonObject Upd(string caso, Action<Entity> mas) => Intento(caso, () => { var e = new Entity(ARR, G("arr-tipos")); mas(e); Svc.Update(e); return null; });
            o["update_UL_Utc"] = Upd("Update: UL Utc 13:45:31, DO Unspecified 2027-05-01, decimal", e => { e["sanic_fechacongelamiento"] = new DateTime(2026, 3, 5, 13, 45, 31, DateTimeKind.Utc); e["sanic_fechafin"] = new DateTime(2027, 5, 1); e["sanic_saldoinicial"] = 2000.5m; });
            o["update_UL_Local"] = Upd("Update: UL Local 13:45:32 (UTC-6)", e => e["sanic_fechacongelamiento"] = new DateTime(2026, 3, 5, 13, 45, 32, DateTimeKind.Local));
            o["update_UL_Unspecified"] = Upd("Update: UL Unspecified 13:45:33", e => e["sanic_fechacongelamiento"] = new DateTime(2026, 3, 5, 13, 45, 33, DateTimeKind.Unspecified));
            o["update_DO_Utc_tarde"] = Upd("Update: DO Utc 2027-05-02 23:30", e => e["sanic_fechafin"] = new DateTime(2027, 5, 2, 23, 30, 0, DateTimeKind.Utc));
            esc.Add(o);
        });

        Escribir("30-sonda-escenarios.json", new JsonObject
        {
            ["cabecera"] = Cabecera("30-sonda-escenarios", "SINTETICAS ZZF- (marca ZZF-SND-); las filas anidadas las crea el plugin con GUID reservado y registrado antes",
                "Lo que observo el plugin esta en 31-sonda-trazas.json (plugintracelogs). Aqui: la operacion desde afuera, su resultado y que filas quedaron.",
                $"Ventana de las trazas: desde {ventanaIni:O} (UTC)."),
            ["ventana_inicio_utc"] = ventanaIni.ToString("O"),
            ["ventana_fin_utc"] = DateTime.UtcNow.ToString("O"),
            ["contenido"] = new JsonObject { ["escenarios"] = esc },
        });
    }

    private static Entity Also2(this Entity e, Action<Entity> a) { a(e); return e; }
}
