using System.Text.Json.Nodes;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dbl.Medidor;

public static partial class Program
{
    // ------------------------------------------------------------------ (13) nativas sin muestra (SOLO LECTURA, anonimizadas en memoria)
    private static void NativasSinMuestra()
    {
        var cont = new JsonObject();
        var bu = Guid.Parse(Base["bu_id"]!.GetValue<string>());
        var orgId = Guid.Parse(Base["organizationid"]!.GetValue<string>());

        JsonObject Retr(string caso, string tabla, Guid id) => Intento(caso, () => S.Entidad(Svc.Retrieve(tabla, id, new ColumnSet(true))));
        JsonObject Multi(string caso, string tabla, Action<QueryExpression> arma) => Intento(caso, () =>
        {
            var q = new QueryExpression(tabla) { ColumnSet = new ColumnSet(true) };
            arma(q);
            return S.Coleccion(Svc.RetrieveMultiple(q));
        });

        cont["businessunit_retrieve"] = Retr("businessunit (la BU del proyecto): Retrieve ColumnSet(true)", "businessunit", bu);
        cont["businessunit_retrievemultiple"] = Multi("businessunit: RetrieveMultiple ColumnSet(true) de la BU", "businessunit", q => q.Criteria.AddCondition("businessunitid", ConditionOperator.Equal, bu));
        cont["organization_retrieve"] = Retr("organization: Retrieve ColumnSet(true)", "organization", orgId);
        cont["organization_retrievemultiple"] = Multi("organization: RetrieveMultiple ColumnSet(true)", "organization", q => q.TopCount = 1);

        // systemuserroles de los usuarios de aplicacion (el conectado y el no administrador)
        var usuarios = new List<object> { UserId };
        if (NoAdminId != Guid.Empty) usuarios.Add(NoAdminId);
        var qsur = new QueryExpression("systemuserroles") { ColumnSet = new ColumnSet(true), TopCount = 5 };
        qsur.Criteria.AddCondition("systemuserid", ConditionOperator.In, usuarios.ToArray());
        EntityCollection? sur = null;
        cont["systemuserroles_retrievemultiple"] = Intento("systemuserroles: RetrieveMultiple ColumnSet(true) de los usuarios de aplicacion", () => { sur = Svc.RetrieveMultiple(qsur); return S.Coleccion(sur); });
        Guid? surId = sur?.Entities.FirstOrDefault()?.Id;
        Guid? roleId = sur?.Entities.FirstOrDefault()?.GetAttributeValue<Guid>("roleid");
        cont["systemuserroles_retrieve"] = surId == null ? new JsonObject { ["caso"] = "systemuserroles: Retrieve", ["resultado"] = "NO MEDIDO", ["motivo"] = "el RetrieveMultiple no devolvio filas" }
            : Retr("systemuserroles: Retrieve ColumnSet(true) por id", "systemuserroles", surId.Value);
        cont["role_retrieve"] = roleId == null || roleId == Guid.Empty ? new JsonObject { ["caso"] = "role: Retrieve", ["resultado"] = "NO MEDIDO", ["motivo"] = "no hay roleid en systemuserroles" }
            : Retr("role: Retrieve ColumnSet(true) por id (el primer rol de un usuario de aplicacion)", "role", roleId.Value);
        cont["role_retrievemultiple"] = Multi("role: RetrieveMultiple ColumnSet(true) TopCount 2", "role", q => q.TopCount = 2);

        // mailbox: 2 filas
        EntityCollection? mb = null;
        cont["mailbox_retrievemultiple"] = Intento("mailbox: RetrieveMultiple ColumnSet(true) TopCount 2", () =>
        {
            var q = new QueryExpression("mailbox") { ColumnSet = new ColumnSet(true), TopCount = 2 };
            mb = Svc.RetrieveMultiple(q);
            return S.Coleccion(mb);
        });
        cont["mailbox_retrieve"] = mb == null || mb.Entities.Count == 0 ? new JsonObject { ["caso"] = "mailbox: Retrieve", ["resultado"] = "NO MEDIDO", ["motivo"] = "el RetrieveMultiple no devolvio filas" }
            : Retr("mailbox: Retrieve ColumnSet(true) por id (la primera fila)", "mailbox", mb.Entities[0].Id);

        Escribir("19-nativas-sin-muestra.json", new JsonObject
        {
            ["cabecera"] = Cabecera("19-nativas-sin-muestra", "REALES anonimizadas (solo lectura): businessunit, systemuserroles, role, organization, mailbox",
                "Punto (13). Cada atributo: {tipo .NET, valor}. TODO texto que no empiece con ZZF- se reemplaza por un sintetico (Anonimizador), todo GUID por uno sintetico, todo correo por zzf-buzonN@example.com.",
                "No se conecto ni se toco ningun buzon: solo se leyeron las filas de la tabla mailbox."),
            ["contenido"] = cont,
        });
    }

    // ------------------------------------------------------------------ (a) sin privilegio, con CallerId
    private static void Privilegios()
    {
        var c = new JsonArray();
        if (NoAdminId == Guid.Empty)
        {
            c.Add(new JsonObject { ["resultado"] = "NO MEDIDO", ["motivo"] = "no se encontro el usuario de aplicacion no administrador en Dev" });
        }
        else
        {
            try
            {
                Cli.CallerId = NoAdminId;
                c.Add(Intento("WhoAmI con CallerId = usuario de aplicacion no administrador", () => { var w = (WhoAmIResponse)Svc.Execute(new WhoAmIRequest()); return new JsonObject { ["UserId_es_el_suplantado"] = w.UserId == NoAdminId }; }));
                c.Add(Intento("Retrieve de una Conversacion ZZF- (lectura)", () => V(Svc.Retrieve(CONV, G("conv-Ca"), new ColumnSet("sanic_huellamessageid")).Id != Guid.Empty)));
                c.Add(Intento("RetrieveMultiple de Conversacion (lectura)", () => { var q = new QueryExpression(CONV) { ColumnSet = new ColumnSet(Pk(CONV)), TopCount = 1 }; return new JsonObject { ["n"] = Svc.RetrieveMultiple(q).Entities.Count }; }));
                c.Add(Intento("Update de una Conversacion ZZF- (escritura)", () => { Svc.Update(new Entity(CONV, G("conv-Ca")) { ["sanic_asunto"] = "ZZF-priv" }); return null; }));
                c.Add(Intento("Create de un Cliente ZZF- (creacion)", () => { CrearIntento("cli-priv", new Entity(CLI) { ["sanic_nombre"] = "ZZF-priv", ["sanic_nombrecompleto"] = "ZZF-priv", ["ownerid"] = Dueno() }); return null; }));
                c.Add(Intento("Retrieve de una tabla nativa sin lectura probable (role)", () => V(Svc.RetrieveMultiple(new QueryExpression("role") { ColumnSet = new ColumnSet("name"), TopCount = 1 }).Entities.Count)));
            }
            finally { Cli.CallerId = Guid.Empty; }
            c.Add(Intento("control: la misma lectura sin CallerId", () => V(Svc.Retrieve(CONV, G("conv-Ca"), new ColumnSet("sanic_huellamessageid")).Id != Guid.Empty)));
        }
        Escribir("21-sin-privilegio.json", new JsonObject
        {
            ["cabecera"] = Cabecera("21-sin-privilegio", "SINTETICAS ZZF-; usuario de aplicacion no administrador (el mismo de spikes/fixtures/salida/06-whoami-systemuser.json) por CallerId",
                "Punto (a). El usuario se busco en Dev por applicationid = PP_MAIL_CLIENT_ID (la consulta del exportador); su GUID no se escribe.",
                $"En la linea base: deshabilitado={Base["usuario_no_admin_isdisabled"]}, roles asignados (conteo)={Base["usuario_no_admin_roles_conteo"]}. No se le asigno nada ni se lo habilito: si la suplantacion falla por eso, se informa tal cual."),
            ["contenido"] = new JsonObject { ["casos"] = c },
        });
    }

    // ------------------------------------------------------------------ (15) borrado por relacion
    private static JsonNode Existe(string clave)
    {
        try { Svc.Retrieve(TablaDe(clave), G(clave), new ColumnSet(false)); return true; }
        catch (Exception ex) { return Hex(ex) == "0x80040217" ? false : "error " + Hex(ex); }
    }

    private static string TablaDe(string clave) => clave.StartsWith("cli") || clave.StartsWith("rel-cli") ? CLI : clave.StartsWith("tx") ? CONV : clave.StartsWith("conv") || clave.StartsWith("rel-conv") ? CONV : clave.StartsWith("arr") || clave.StartsWith("rel-arr") ? ARR
        : clave.StartsWith("ver") || clave.StartsWith("rel-ver") ? VER : clave.StartsWith("gen") ? GEN : clave.StartsWith("rel-cuota") ? CUO : clave.StartsWith("rel-mapeo") ? MAP : clave.StartsWith("email") || clave.StartsWith("rel-email") ? "email" : "";

    private static void BorradoPorRelacion()
    {
        var c = new JsonArray();
        JsonObject Existen(params string[] claves) { var o = new JsonObject(); foreach (var k in claves) o[k] = Existe(k); return o; }
        JsonObject Borrar(string caso, string tabla, string clave, params string[] vigilar) => Intento(caso, () =>
        {
            Svc.Delete(tabla, G(clave));
            return new JsonObject { ["existen_despues"] = Existen(vigilar) };
        }).Also(o => { if (o["resultado"]!.GetValue<string>() == "falla") o["existen_despues"] = Existen(vigilar); });

        // REL-01 Cliente -> Conversacion (restringido): cli-B tiene a conv-Nm1
        c.Add(Borrar("REL-01 (restringido): borrar el Cliente cli-B que tiene la Conversacion conv-Nm1", CLI, "cli-B", "cli-B", "conv-Nm1"));
        // REL-02 Cliente -> Arreglo y REL-03 Conversacion -> Arreglo: filas nuevas con un solo hijo
        Crear("rel-cliC", new Entity(CLI) { ["sanic_nombre"] = "ZZF-cli-C", ["sanic_nombrecompleto"] = "ZZF-cli-C", ["sanic_cedula"] = "ZZF-ced-C", ["ownerid"] = Dueno() });
        Crear("rel-convX", ConvE("RelX"));
        Crear("rel-arrC", ArrE("RelC", G("rel-cliC"), G("ver-A"), e => e["sanic_conversacionid"] = Ref(CONV, G("rel-convX"))));
        c.Add(Borrar("REL-02 (restringido): borrar el Cliente rel-cliC que tiene el Arreglo rel-arrC", CLI, "rel-cliC", "rel-cliC", "rel-arrC"));
        c.Add(Borrar("REL-03 (restringido): borrar la Conversacion rel-convX que es la conversacion de origen del Arreglo rel-arrC", CONV, "rel-convX", "rel-convX", "rel-arrC"));
        // REL-04 Version -> Arreglo (restringido): ver-A tiene a todos los arreglos
        c.Add(Borrar("REL-04 (restringido): borrar la Version ver-A que tienen los Arreglos", VER, "ver-A", "ver-A", "rel-arrC"));
        // REL-07 Version -> Generacion (restringido): ver-B tiene a gen-G1 y gen-G2, y al mapeo
        Crear("rel-mapeoM1", new Entity(MAP)
        {
            ["sanic_nombre"] = "ZZF-hoja!A1", ["sanic_hoja"] = "ZZF-hoja", ["sanic_celda"] = "A1", ["sanic_tipo"] = O(159460001), ["sanic_campo"] = O(159460001),
            ["sanic_versionplantillaid"] = Ref(VER, G("ver-B")), ["ownerid"] = Dueno(),
        });
        c.Add(Borrar("REL-07 (restringido): borrar la Version ver-B que tiene las Generaciones gen-G1 y gen-G2 (y el Mapeo rel-mapeoM1)", VER, "ver-B", "ver-B", "gen-G1", "gen-G2", "rel-mapeoM1"));
        // REL-06 Arreglo -> Generacion (parental): arr-A1 tiene gen-G1 y gen-G2 y nada mas
        c.Add(Borrar("REL-06 (parental, Cascade): borrar el Arreglo arr-A1 que tiene las Generaciones gen-G1 y gen-G2", ARR, "arr-A1", "arr-A1", "gen-G1", "gen-G2"));
        // REL-08 Version -> Mapeo (parental): ya sin generaciones, ver-B solo tiene el mapeo
        c.Add(Borrar("REL-08 (parental, Cascade): borrar la Version ver-B que ya solo tiene el Mapeo rel-mapeoM1", VER, "ver-B", "ver-B", "rel-mapeoM1"));
        // REL-05 Arreglo -> Cuota (parental)
        Crear("rel-cuotaQ2", new Entity(CUO)
        {
            ["sanic_nombre"] = "ZZF-d-C01", ["sanic_numero"] = 1, ["sanic_fechavencimiento"] = new DateTime(2026, 12, 1), ["sanic_monto"] = 150.04m,
            ["sanic_arregloid"] = Ref(ARR, G("arr-A2")), ["ownerid"] = Dueno(),
        });
        c.Add(Borrar("REL-05 (parental, Cascade): borrar el Arreglo arr-A2 que tiene la Cuota rel-cuotaQ2", ARR, "arr-A2", "arr-A2", "rel-cuotaQ2"));
        // REL-09 Conversacion -> Conversacion (quitar_vinculo)
        Crear("rel-convY", ConvE("RelY"));
        Crear("rel-convW", ConvE("RelW", e => e["sanic_unidaaid"] = Ref(CONV, G("rel-convY"))));
        c.Add(Intento("REL-09 (quitar_vinculo, RemoveLink): borrar la Conversacion rel-convY a la que apunta sanic_unidaaid de rel-convW", () =>
        {
            Svc.Delete(CONV, G("rel-convY"));
            var w = Leer(CONV, G("rel-convW"), "sanic_unidaaid");
            return new JsonObject { ["existen_despues"] = Existen("rel-convY", "rel-convW"), ["sanic_unidaaid_de_W_presente"] = w.Contains("sanic_unidaaid") };
        }));
        // NAT-01 Regarding de Email -> Conversacion
        Crear("rel-convZ", ConvE("RelZ"));
        Crear("rel-emailZ", EmailE("RelZ", false, e => e["regardingobjectid"] = Ref(CONV, G("rel-convZ"))));
        c.Add(Borrar("NAT-01 (Regarding nativa): borrar la Conversacion rel-convZ que tiene el Email sintetico rel-emailZ como Regarding", CONV, "rel-convZ", "rel-convZ", "rel-emailZ"));
        // una Conversacion con varios hijos de tipos distintos no se prueba: cada relacion ya se midio sola.
        c.Add(new JsonObject
        {
            ["caso"] = "REL-10 a REL-19 (padre systemuser, restringido)", ["resultado"] = "NO MEDIDO",
            ["motivo"] = "borrar un systemuser no se puede ejercitar con filas ZZF-: no se crean ni borran usuarios (REL-19 tiene como hija un email, pero el padre sigue siendo un usuario real)",
        });
        Escribir("20-borrado-por-relacion.json", new JsonObject
        {
            ["cabecera"] = Cabecera("20-borrado-por-relacion", "SINTETICAS ZZF-",
                "Punto (15): REL-01..REL-09 y NAT-01. Cada caso: Delete del padre; si funciona, que filas siguen existiendo (true/false); si falla, la falla completa y que filas siguen existiendo.",
                "Declarado Restrict: REL-01, REL-02, REL-03, REL-04, REL-07 (y REL-10..19, NO MEDIDAS). Declarado Cascade: REL-05, REL-06, REL-08. RemoveLink: REL-09. Regarding: NAT-01.",
                "Cuando un padre tiene a la vez hijos de dos relaciones, el mensaje de la falla es lo que dice cual relacion lo detuvo; cada caso se armo para que solo haya un hijo de la relacion probada."),
            ["contenido"] = new JsonObject { ["casos"] = c },
        });
    }

    private static JsonObject Also(this JsonObject o, Action<JsonObject> a) { a(o); return o; }
}
