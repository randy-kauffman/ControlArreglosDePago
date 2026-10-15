using System.Text.Json.Nodes;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dbl.Medidor;

public static partial class Program
{
    private const string AJENA = "sanic_chtblsystemheartbeat";   // tabla de OTRO proyecto: el Oficial no tiene Read; solo se intenta leer su id, nunca se guarda ningun dato

    /// <summary>
    /// Modo "priv" (punto (a)): mide con CallerId = usuario de aplicacion no administrador las 5 operaciones sobre filas ZZF- de tablas propias, mas una lectura sobre una tabla ajena.
    /// El lanzador Python (d40) lo habilita antes y lo restaura despues; este programa NO toca usuarios, roles ni equipos.
    /// Las filas las crea la cuenta de servicio (GUID registrado en estado.json ANTES del Create). Los Create hechos por el usuario suplantado tambien se registran antes.
    /// </summary>
    private static void PrivilegiosPorEscenario()
    {
        var escenario = Environment.GetEnvironmentVariable("ZZF_ESCENARIO") ?? "sin_rol_directo";
        if (NoAdminId == Guid.Empty) throw new InvalidOperationException("no hay usuario de aplicacion no administrador en la linea base");

        // ---- filas sembradas por la cuenta de servicio (sin CallerId)
        Crear("priv-cli", new Entity(CLI) { ["sanic_nombre"] = "ZZF-priv-cli", ["sanic_nombrecompleto"] = "ZZF-priv-cli", ["sanic_cedula"] = "ZZF-ced-priv", ["ownerid"] = Dueno() });
        Crear("priv-cli-del", new Entity(CLI) { ["sanic_nombre"] = "ZZF-priv-del", ["sanic_nombrecompleto"] = "ZZF-priv-del", ["sanic_cedula"] = "ZZF-ced-pdel", ["ownerid"] = Dueno() });
        Crear("priv-ver", VerE("ZZF-pv"));
        Crear("priv-conv", ConvE("priv-conv"));
        Guid ajena = Guid.NewGuid();
        string ajenaFuente = "guid aleatorio (no se pudo leer un id de la tabla ajena)";
        try
        {
            var q = new QueryExpression(AJENA) { ColumnSet = new ColumnSet(false), TopCount = 1 };
            var r = Svc.RetrieveMultiple(q);
            if (r.Entities.Count > 0) { ajena = r.Entities[0].Id; ajenaFuente = "id de una fila existente leido por la cuenta de servicio (no se guarda)"; }
        }
        catch (Exception) { }

        var ops = new JsonArray();
        JsonObject Op(string tabla, string operacion, Func<JsonNode?> a)
        {
            var o = Intento($"{operacion} sobre {tabla}", a);
            o["tabla"] = tabla; o["operacion"] = operacion;
            ops.Add(o);
            return o;
        }
        // Create de un caso: GUID asignado y registrado ANTES (si la plataforma lo deja pasar, la limpieza lo borra)
        Guid CrearSuplantado(string clave, Entity e) { e.Id = Guid.NewGuid(); Estado.Registrar(clave, e.LogicalName, e.Id); Claves[e.Id] = clave; PorClave[clave] = e.Id; return Svc.Create(e); }

        var cab = new JsonObject();
        try
        {
            Cli.CallerId = NoAdminId;
            cab["WhoAmI"] = Intento("WhoAmI con CallerId", () => { var w = (WhoAmIResponse)Svc.Execute(new WhoAmIRequest()); return new JsonObject { ["UserId_es_el_suplantado"] = w.UserId == NoAdminId }; });

            // Cliente: el Oficial (rol del equipo por defecto) tiene Create, Read, Write y NO Delete
            Op(CLI, "Retrieve", () => V(Svc.Retrieve(CLI, G("priv-cli"), new ColumnSet("sanic_nombre")).Id != Guid.Empty));
            Op(CLI, "RetrieveMultiple", () => { var q = new QueryExpression(CLI) { ColumnSet = new ColumnSet(Pk(CLI)), TopCount = 1 }; q.Criteria.AddCondition("sanic_nombre", ConditionOperator.BeginsWith, "ZZF-priv"); return new JsonObject { ["n"] = Svc.RetrieveMultiple(q).Entities.Count }; });
            Op(CLI, "Create", () => { CrearSuplantado("priv-cli-creado", new Entity(CLI) { ["sanic_nombre"] = "ZZF-priv-nuevo", ["sanic_nombrecompleto"] = "ZZF-priv-nuevo", ["sanic_cedula"] = "ZZF-ced-pn", ["ownerid"] = Dueno() }); return null; });
            Op(CLI, "Create (sin la columna sanic_cedula)", () => { CrearSuplantado("priv-cli-creado2", new Entity(CLI) { ["sanic_nombre"] = "ZZF-priv-n2", ["sanic_nombrecompleto"] = "ZZF-priv-n2", ["ownerid"] = Dueno() }); return null; });
            Op(CLI, "Update", () => { Svc.Update(new Entity(CLI, G("priv-cli")) { ["sanic_nombrecompleto"] = "ZZF-priv-upd" }); return null; });
            Op(CLI, "Retrieve de la columna sanic_cedula (seguridad por columna)", () => { var e = Svc.Retrieve(CLI, G("priv-cli"), new ColumnSet("sanic_cedula")); return new JsonObject { ["columna_presente"] = e.Contains("sanic_cedula"), ["atributos_devueltos"] = Arr(e.Attributes.Keys.OrderBy(x => x)) }; });
            Op(CLI, "Update de la columna sanic_cedula (seguridad por columna)", () => { Svc.Update(new Entity(CLI, G("priv-cli")) { ["sanic_cedula"] = "ZZF-ced-upd" }); return null; });
            Op(CLI, "Delete", () => { Svc.Delete(CLI, G("priv-cli-del")); return null; });

            // Version de plantilla: el Oficial SOLO lee (Create, Write y Delete son del Jefe o de nadie)
            Op(VER, "Retrieve", () => V(Svc.Retrieve(VER, G("priv-ver"), new ColumnSet("sanic_nombre")).Id != Guid.Empty));
            Op(VER, "RetrieveMultiple", () => { var q = new QueryExpression(VER) { ColumnSet = new ColumnSet(Pk(VER)), TopCount = 1 }; q.Criteria.AddCondition("sanic_nombre", ConditionOperator.BeginsWith, "ZZF-pv"); return new JsonObject { ["n"] = Svc.RetrieveMultiple(q).Entities.Count }; });
            Op(VER, "Create", () => { CrearSuplantado("priv-ver-creada", VerE("ZZF-pvn")); return null; });
            Op(VER, "Update", () => { Svc.Update(new Entity(VER, G("priv-ver")) { ["sanic_nombre"] = "ZZF-pvu" }); return null; });
            Op(VER, "Delete", () => { Svc.Delete(VER, G("priv-ver")); return null; });

            // Conversacion: el Oficial lee y escribe, NO crea ni borra
            Op(CONV, "Retrieve", () => V(Svc.Retrieve(CONV, G("priv-conv"), new ColumnSet("sanic_huellamessageid")).Id != Guid.Empty));
            Op(CONV, "Create", () => { CrearSuplantado("priv-conv-creada", ConvE("priv-conv-n")); return null; });
            Op(CONV, "Update", () => { Svc.Update(new Entity(CONV, G("priv-conv")) { ["sanic_asunto"] = "ZZF-priv-asunto" }); return null; });
            Op(CONV, "Delete", () => { Svc.Delete(CONV, G("priv-conv")); return null; });

            // Tabla de otro proyecto: sin Read para el Oficial (lectura prohibida)
            Op("(tabla ajena al proyecto)", "RetrieveMultiple", () => { var q = new QueryExpression(AJENA) { ColumnSet = new ColumnSet(false), TopCount = 1 }; return new JsonObject { ["n"] = Svc.RetrieveMultiple(q).Entities.Count }; });
            Op("(tabla ajena al proyecto)", "Retrieve", () => V(Svc.Retrieve(AJENA, ajena, new ColumnSet(false)).Id != Guid.Empty));
        }
        finally { Cli.CallerId = Guid.Empty; }

        var control = Intento("control: Retrieve de la fila priv-cli SIN CallerId (cuenta de servicio)", () => V(Svc.Retrieve(CLI, G("priv-cli"), new ColumnSet("sanic_nombrecompleto")).Id != Guid.Empty));

        // ---- salida: se acumulan los escenarios en 22-privilegios.json
        var ruta = Path.Combine(DirSalida, "22-privilegios.json");
        JsonObject doc;
        if (File.Exists(ruta)) doc = JsonNode.Parse(File.ReadAllText(ruta))!.AsObject();
        else doc = new JsonObject
        {
            ["cabecera"] = Cabecera("22-privilegios", "SINTETICAS ZZF- (creadas por la cuenta de servicio) y una lectura sobre una tabla de otro proyecto sin guardar datos",
                "Punto (a). Usuario de aplicacion no administrador (el de spikes/fixtures/salida/06-whoami-systemuser.json), HABILITADO solo durante la medicion y restaurado despues; su GUID no se escribe.",
                "El usuario no tiene roles directos, pero es miembro del equipo por defecto de su unidad de negocio, que tiene el rol Oficial del proyecto: los privilegios efectivos son los del Oficial (diseno/04-matriz-privilegios.md).",
                "Cada caso: tabla, operacion, resultado (ok o falla) y la falla completa (tipo, HRESULT, ErrorCode, InnerFault, claves de ErrorDetails)."),
            ["contenido"] = new JsonObject { ["escenarios"] = new JsonObject() },
        };
        doc["contenido"]!["escenarios"]![escenario] = new JsonObject
        {
            ["descripcion"] = Environment.GetEnvironmentVariable("ZZF_ESCENARIO_DESC") ?? "usuario habilitado, sin roles directos",
            ["fuente_del_id_ajeno"] = ajenaFuente,
            ["suplantacion"] = cab,
            ["operaciones"] = ops,
            ["control"] = control,
        };
        File.WriteAllText(ruta, doc.ToJsonString(Opc));
    }
}
