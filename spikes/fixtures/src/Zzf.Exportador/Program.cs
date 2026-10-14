using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace Zzf.Exportador;

/// <summary>
/// Exportador DESCARTABLE de fixtures de Dataverse (proyecto 2026-003, diseno/03 4.5, lote 3).
/// Lee de Dev; las unicas filas que crea llevan el prefijo ZZF- (o sanic_zzf...), se registran en estado.json al nacer y se borran en el finally.
/// Los secretos llegan por variables de entorno del PROCESO; la cadena de conexion se arma en memoria. Nada de datos reales en la salida.
/// </summary>
public static class Program
{
    private const string OrigenTxt = "ServiceClient 1.2.26, cliente fuera del sandbox";
    private const string UrlAutorizada = "https://org36e60d9d.crm.dynamics.com";

    private const string CONV = "sanic_carp_tbl_conversacion";
    private const string ARR = "sanic_carp_tbl_arreglo";
    private const string CLI = "sanic_carp_tbl_cliente";
    private const string VER = "sanic_carp_tbl_versionplantilla";

    private static IOrganizationService Svc = null!;
    private static Serializador S = null!;
    private static Anonimizador A = null!;
    private static JsonObject Base = null!;
    private static string DirSalida = "";
    private static readonly List<string> Incidencias = new();
    private static readonly List<string> ArchivosEscritos = new();

    private static Guid UserId, TeamDef;
    private static Guid EmailIn, EmailOut;
    private static readonly List<string> NotasEmail = new();
    private static Guid Cliente, Version, ConvA, ConvF, ConvU, ConvDup, ArrB, ArrV, EvDef, EvVal;

    private static readonly JsonSerializerOptions Opc = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

    public static int Main(string[] args)
    {
        Trace.Listeners.Clear();
        int rc = 0;
        ServiceClient? client = null;
        try
        {
            client = Conectar();
        }
        catch (Exception ex)
        {
            Console.WriteLine("ABORTADO al conectar: " + ex.GetType().FullName);
            return 2;
        }

        try
        {
            Svc = client;
            A = new Anonimizador();
            S = new Serializador(A);
            DirSalida = Path.Combine(Directory.GetCurrentDirectory(), "salida");
            Directory.CreateDirectory(DirSalida);
            Base = Estado.Leer()["linea_base"]!.AsObject();
            TeamDef = Guid.Parse(Base["team_def_id"]!.GetValue<string>());

            Verificar(client);
            Exportar();
            Autocontrol();
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR FATAL: " + Resumen(ex));
            rc = 1;
        }
        finally
        {
            try { Limpiar(); }
            catch (Exception ex) { Console.WriteLine("ERROR EN LIMPIEZA: " + ex.GetType().FullName); rc = 1; }
            client?.Dispose();
        }
        return rc;
    }

    // ------------------------------------------------------------------ conexion y verificacion de entorno
    private static ServiceClient Conectar()
    {
        var url = (Environment.GetEnvironmentVariable("ZZF_URL") ?? "").TrimEnd('/');
        var id = Environment.GetEnvironmentVariable("ZZF_CLIENT_ID") ?? "";
        var secreto = Environment.GetEnvironmentVariable("ZZF_CLIENT_SECRET") ?? "";
        if (!string.Equals(url, UrlAutorizada, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("URL de entorno no autorizada");
        if (id.Length == 0 || secreto.Length == 0) throw new InvalidOperationException("faltan credenciales en el entorno del proceso");
        // La cadena vive solo en memoria. Sin logger (NullLogger) y sin escuchas de Trace: nada puede volcarla.
        var cs = $"AuthType=ClientSecret;Url={url};ClientId={id};ClientSecret={secreto};RequireNewInstance=true;LoginPrompt=Never";
        var c = new ServiceClient(cs, NullLogger.Instance);
        if (!c.IsReady) throw new InvalidOperationException("ServiceClient no esta listo");
        return c;
    }

    private static void Verificar(ServiceClient client)
    {
        var w = (WhoAmIResponse)Svc.Execute(new WhoAmIRequest());
        UserId = w.UserId;
        var orgEsperada = Guid.Parse(Base["organizationid"]!.GetValue<string>());
        bool orgOk = w.OrganizationId == orgEsperada;
        bool urlOk = client.ConnectedOrgPublishedEndpoints?.Values.All(v => v.Contains("org36e60d9d", StringComparison.OrdinalIgnoreCase)) ?? true;
        Console.WriteLine($"entorno: URL ok=True, organizacion de WhoAmI coincide con la de la linea base (Web API de la misma URL)={orgOk}, endpoints coinciden={urlOk}");
        if (!orgOk || !urlOk) throw new InvalidOperationException("ORGANIZACION NO COINCIDE: abortado");
    }

    // ------------------------------------------------------------------ utilidades
    private static string Resumen(Exception ex)
    {
        var tipos = new List<string>();
        for (Exception? e = ex; e != null; e = e.InnerException) tipos.Add(e.GetType().Name);
        string codigo = "";
        for (Exception? e = ex; e != null; e = e.InnerException)
            if (e is System.ServiceModel.FaultException<OrganizationServiceFault> f) { codigo = " ErrorCode=0x" + unchecked((uint)f.Detail.ErrorCode).ToString("X8"); break; }
        var msg = A.Mensaje(ex.Message);
        if (msg.Length > 220) msg = msg[..220];
        return string.Join(" > ", tipos) + codigo + " | " + msg;
    }

    private static void Paso(string nombre, Action a)
    {
        try { a(); Console.WriteLine($"[ok]    {nombre}"); }
        catch (Exception ex)
        {
            var r = Resumen(ex);
            Incidencias.Add($"{nombre}: {r}");
            Console.WriteLine($"[FALLA] {nombre}: {r}");
        }
    }

    private static Guid Crear(string clave, Entity e)
    {
        var id = Svc.Create(e);
        Estado.Registrar(clave, e.LogicalName, id);   // apenas existe
        return id;
    }

    private static EntityReference Ref(string tabla, Guid id) => new(tabla, id);
    private static EntityReference Dueno() => new("team", TeamDef);
    private static DateTime Ahora() { var n = DateTime.UtcNow; return new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, n.Second, DateTimeKind.Utc); }

    private static JsonObject Cabecera(string nombre, string naturaleza, params string[] notas)
    {
        var n = new JsonArray();
        foreach (var x in notas) n.Add(x);
        var asm = typeof(ServiceClient).Assembly;
        var info = (asm.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                       .FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute)?.InformationalVersion ?? asm.GetName().Version?.ToString();
        return new JsonObject
        {
            ["fixture"] = nombre,
            ["origen"] = OrigenTxt,
            ["ensamblado_cliente"] = (info ?? "").Split('+')[0],
            ["runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ["generado_utc"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["filas"] = naturaleza,
            ["advertencia"] = "Dentro de un plugin (sandbox) la deserializacion puede diferir, sobre todo DateTime.Kind. No se capturo desde un plugin.",
            ["notas"] = n,
        };
    }

    private static void Escribir(string archivo, JsonObject doc)
    {
        File.WriteAllText(Path.Combine(DirSalida, archivo), doc.ToJsonString(Opc));
        if (!ArchivosEscritos.Contains(archivo)) ArchivosEscritos.Add(archivo);
    }

    private static Entity Leer(string tabla, Guid id) => Svc.Retrieve(tabla, id, new ColumnSet(true));

    // ------------------------------------------------------------------ flujo
    private static void Exportar()
    {
        Preparar();
        Fx01Emails();
        Fx02Conversaciones();
        Fx03Arreglos();
        Fx04Equipos();
        Fx05VariablesDeEntorno();
        Fx06Usuarios();
        Fx08AliasedYAgregados();
        Fx07Fallas();

        var inc = new JsonObject { ["incidencias"] = new JsonArray(Incidencias.Select(i => (JsonNode?)JsonValue.Create(A.Mensaje(i))).ToArray()) };
        inc["tipos_vistos"] = new JsonArray(S.TiposVistos.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray());
        inc["tipos_no_modelados"] = new JsonArray(S.TiposNoModelados.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray());
        File.WriteAllText(Path.Combine(DirSalida, "00-incidencias-y-tipos.json"), inc.ToJsonString(Opc));
        Console.WriteLine($"incidencias: {Incidencias.Count} | tipos .NET distintos vistos: {S.TiposVistos.Count} | tipos no modelados: {S.TiposNoModelados.Count} | reemplazos de anonimizacion (unicos): {A.Reemplazos}");
    }

    // ------------------------------------------------------------------ preparacion de filas sinteticas ZZF-
    private static void Preparar()
    {
        Paso("crear cliente ZZF-", () =>
        {
            Cliente = Crear("cliente", new Entity(CLI)
            {
                ["sanic_nombre"] = "ZZF-cli-1", ["sanic_nombrecompleto"] = "ZZF-cliente 1", ["sanic_cedula"] = "ZZF-ced-cli", ["ownerid"] = Dueno(),
            });
        });
        Paso("crear version de plantilla ZZF-", () =>
        {
            Version = Crear("version", new Entity(VER)
            {
                ["sanic_nombre"] = "ZZF-v1", ["sanic_capacidad"] = 60, ["sanic_estado"] = new OptionSetValue(159460002),
                ["sanic_fechaaprobacion"] = Ahora(), ["sanic_aprobadoporid"] = Ref("systemuser", UserId), ["ownerid"] = Dueno(),
            });
        });

        Entity Conv(string huella, string sufijo) => new(CONV)
        {
            ["sanic_huellamessageid"] = huella, ["sanic_fechaprimercorreo"] = Ahora(), ["sanic_correonuevoenfinalizada"] = false, ["sanic_cantidadrespuestas"] = 0,
            ["sanic_asunto"] = "ZZF-asunto " + sufijo, ["sanic_remitente"] = "zzf-remitente@example.com", ["ownerid"] = Dueno(),
            ["sanic_clienteid"] = Cliente == Guid.Empty ? null : Ref(CLI, Cliente),
        };

        Paso("crear conversacion Abierta", () => ConvA = Crear("conv-abierta", Conv("ZZF-h-abierta", "abierta")));
        Paso("crear conversacion y pasarla a Finalizada", () =>
        {
            ConvF = Crear("conv-finalizada", Conv("ZZF-h-finalizada", "finalizada"));
            Svc.Update(new Entity(CONV, ConvF)
            {
                ["statecode"] = new OptionSetValue(1), ["statuscode"] = new OptionSetValue(2),
                ["sanic_resultado"] = new OptionSetValue(159460003), ["sanic_motivosinrespuesta"] = new OptionSetValue(159460004),
                ["sanic_detallesinrespuesta"] = "ZZF-detalle", ["sanic_fechafinalizacion"] = Ahora(), ["sanic_finalizadoporid"] = Ref("systemuser", UserId),
            });
        });
        Paso("crear conversacion y pasarla a Unida", () =>
        {
            ConvU = Crear("conv-unida", Conv("ZZF-h-unida", "unida"));
            var u = new Entity(CONV, ConvU)
            {
                ["statecode"] = new OptionSetValue(1), ["statuscode"] = new OptionSetValue(159460001), ["sanic_fechafinalizacion"] = Ahora(),
            };
            if (ConvA != Guid.Empty) u["sanic_unidaaid"] = Ref(CONV, ConvA);
            Svc.Update(u);
        });
        Paso("crear conversacion extra para K-01", () => ConvDup = Crear("conv-k01", Conv("ZZF-h-duplicada", "k01")));

        // Correos sinteticos (NO enviados) con Regarding a la conversacion Abierta: ningun correo real de Dev tiene regardingobjectid.
        EmailIn = EmailSintetico(true);
        EmailOut = EmailSintetico(false);

        Entity Arr(string sufijo) => new(ARR)
        {
            ["sanic_producto"] = new OptionSetValue(159460001), ["sanic_numeroproducto"] = "ZZF-" + sufijo, ["sanic_tipo"] = new OptionSetValue(159460001),
            ["sanic_moneda"] = new OptionSetValue(159460001), ["sanic_canalorigen"] = new OptionSetValue(159460001),
            ["sanic_saldoinicial"] = 1000.50m, ["sanic_montoabonado"] = 100.25m, ["sanic_saldoremanente"] = 900.25m, ["sanic_plazo"] = 6,
            ["sanic_cuotamensual"] = 150.04m, ["sanic_totalpagar"] = 900.25m,
            ["sanic_fechainicio"] = new DateTime(2026, 11, 1), ["sanic_fechafin"] = new DateTime(2027, 4, 1), ["sanic_fechaformalizacion"] = new DateTime(2026, 10, 10),
            ["sanic_cuentadebitar"] = "ZZF-cta-0001", ["sanic_cantidadreaperturas"] = 0, ["sanic_instantaneacedula"] = "ZZF-ced-0001",
            ["sanic_comentarios"] = "ZZF-comentario " + sufijo, ["ownerid"] = Dueno(),
            ["sanic_clienteid"] = Ref(CLI, Cliente), ["sanic_versionplantillaid"] = Ref(VER, Version), ["sanic_oficialid"] = Ref("systemuser", UserId),
            ["sanic_conversacionid"] = ConvA == Guid.Empty ? null : Ref(CONV, ConvA),
        };

        Paso("crear arreglo Borrador congelado", () =>
        {
            var e = Arr("borrador");
            e["sanic_fechacongelamiento"] = Ahora();
            e["sanic_instantaneanombre"] = "ZZF-cliente 1";
            e["sanic_instantaneacorreo"] = "zzf-cliente1@example.com";
            e["sanic_instantaneacelular"] = "ZZF-0000";
            ArrB = Crear("arreglo-borrador", e);
        });
        Paso("crear arreglo y pasarlo a Vigente", () =>
        {
            ArrV = Crear("arreglo-vigente", Arr("vigente"));
            Svc.Update(new Entity(ARR, ArrV)
            {
                ["statecode"] = new OptionSetValue(0), ["statuscode"] = new OptionSetValue(159460001), ["sanic_numeroarreglo"] = 1,
                ["sanic_fechaformalizado"] = Ahora(), ["sanic_formalizadoporid"] = Ref("systemuser", UserId),
            });
        });

        // Variables de entorno: no hay sanic_carp_ev_* (linea base) -> definicion descartable FUERA de la solucion.
        Paso("crear definicion y valor de variable de entorno sanic_zzf...", () =>
        {
            EvDef = Crear("ev-definicion", new Entity("environmentvariabledefinition")
            {
                ["schemaname"] = "sanic_zzfevtexto", ["displayname"] = "ZZF-ev texto", ["type"] = new OptionSetValue(100000000), ["defaultvalue"] = "ZZF-defecto",
            });
            EvVal = Crear("ev-valor", new Entity("environmentvariablevalue")
            {
                ["schemaname"] = "sanic_zzfevtexto_valor", ["value"] = "ZZF-valor", ["environmentvariabledefinitionid"] = Ref("environmentvariabledefinition", EvDef),
            });
        });
    }

    private static Guid EmailSintetico(bool entrante)
    {
        var etq = entrante ? "entrante" : "saliente";
        Guid id = Guid.Empty;
        Paso($"crear email sintetico {etq} (sin enviar)", () =>
        {
            var e = new Entity("email")
            {
                ["subject"] = "ZZF-asunto email " + etq, ["description"] = "ZZF-cuerpo " + etq, ["directioncode"] = !entrante,
                ["messageid"] = $"<zzf-sintetico-{etq}@example.com>", ["actualend"] = Ahora(), ["ownerid"] = Ref("systemuser", UserId),
            };
            if (ConvA != Guid.Empty) e["regardingobjectid"] = Ref(CONV, ConvA);
            if (entrante) e["inreplyto"] = "<zzf-sintetico-padre@example.com>";
            id = Crear("email-" + etq + "-sintetico", e);
            int estado = entrante ? 4 : 3;
            try { Svc.Update(new Entity("email", id) { ["statecode"] = new OptionSetValue(1), ["statuscode"] = new OptionSetValue(estado) }); NotasEmail.Add($"{etq}: statecode 1 / statuscode {estado} aplicado con Update."); }
            catch (Exception ex1)
            {
                NotasEmail.Add($"{etq}: Update de statecode/statuscode FALLO ({Resumen(ex1)})");
                try { Svc.Execute(new SetStateRequest { EntityMoniker = Ref("email", id), State = new OptionSetValue(1), Status = new OptionSetValue(estado) }); NotasEmail.Add($"{etq}: statecode 1 / statuscode {estado} aplicado con SetStateRequest."); }
                catch (Exception ex2) { NotasEmail.Add($"{etq}: SetStateRequest tambien FALLO ({Resumen(ex2)}); la fila queda como la creo la plataforma."); }
            }
        });
        return id;
    }

    // ------------------------------------------------------------------ 1. email
    private static void Fx01Emails()
    {
        Paso("fixture 01 email", () =>
        {
            var cont = new JsonObject();
            var cand = Base["emails_candidatos"]!.AsObject();
            var todos = new List<Guid>();
            foreach (var dir in new[] { "entrante", "saliente" })
            {
                var arr = new JsonArray();
                foreach (var g in cand[dir]!.AsArray())
                {
                    var id = Guid.Parse(g!.GetValue<string>());
                    todos.Add(id);
                    arr.Add(S.Entidad(Leer("email", id)));
                }
                cont["retrieve_columnset_true_" + dir] = arr;
            }
            // Mismas filas por RetrieveMultiple con las columnas del diseno (lo que el doble devolveria).
            var q = new QueryExpression("email")
            {
                ColumnSet = new ColumnSet("statuscode", "statecode", "directioncode", "messageid", "inreplyto", "actualend", "senton", "createdby", "regardingobjectid", "ownerid", "subject", "createdon"),
            };
            q.Criteria.AddCondition("activityid", ConditionOperator.In, todos.Cast<object>().ToArray());
            cont["retrievemultiple_columnas_del_diseno"] = S.Coleccion(Svc.RetrieveMultiple(q));
            // Sinteticos con Regarding a la conversacion y lo que les falta frente a un correo real (lo que pondria la sincronizacion con Exchange)
            var sint = new JsonObject();
            var notas = new JsonArray();
            foreach (var n in NotasEmail) notas.Add(A.Mensaje(n));
            foreach (var (clave, id, dir) in new[] { ("entrante", EmailIn, "entrante"), ("saliente", EmailOut, "saliente") })
            {
                if (id == Guid.Empty) continue;
                var sintetico = Leer("email", id);
                sint[clave] = S.Entidad(sintetico);
                var real = Leer("email", Guid.Parse(cand[dir]!.AsArray()[0]!.GetValue<string>()));
                var ausentes = real.Attributes.Keys.Where(k => !sintetico.Attributes.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal);
                var sobran = sintetico.Attributes.Keys.Where(k => !real.Attributes.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal);
                sint["columnas_del_real_ausentes_en_el_sintetico_" + clave] = new JsonArray(ausentes.Select(k => (JsonNode?)JsonValue.Create(k)).ToArray());
                sint["columnas_del_sintetico_ausentes_en_el_real_" + clave] = new JsonArray(sobran.Select(k => (JsonNode?)JsonValue.Create(k)).ToArray());
            }
            sint["notas_de_estado"] = notas;
            cont["sinteticos_con_regardingobjectid_a_la_conversacion"] = sint;
            Escribir("01-email.json", new JsonObject
            {
                ["cabecera"] = Cabecera("01-email", "REALES anonimizadas (correos que ya existian en Dev entre los buzones de prueba)",
                    "entrante = statuscode Received(4) y directioncode false; saliente = statuscode Sent(3) y directioncode true.",
                    "No se creo ni se envio ningun correo (no se uso SendEmail).",
                    "Dos por direccion; elegidas por tener regardingobjectid / inreplyto / messageid con <>. Ninguno de los reales tiene regardingobjectid.",
                    "Ademas hay un entrante y un saliente SINTETICOS ZZF- (creados sin enviar, sin SendEmail) con regardingobjectid a una conversacion ZZF-; les faltan from/to (activityparty), sender, torecipients, senton y todo lo que pondria la sincronizacion con Exchange."),
                ["contenido"] = cont,
            });
        });
    }

    // ------------------------------------------------------------------ 2. conversacion
    private static void Fx02Conversaciones()
    {
        Paso("fixture 02 conversacion", () =>
        {
            var cont = new JsonObject
            {
                ["abierta"] = S.Entidad(Leer(CONV, ConvA)),
                ["finalizada"] = S.Entidad(Leer(CONV, ConvF)),
                ["unida"] = S.Entidad(Leer(CONV, ConvU)),
            };
            Escribir("02-conversacion.json", new JsonObject
            {
                ["cabecera"] = Cabecera("02-conversacion", "SINTETICAS ZZF- (creadas por el exportador y borradas al final)",
                    "Abierta = statecode 0 / statuscode 1; Finalizada = 1 / 2; Unida = 1 / 159460001 con sanic_unidaaid apuntando a la Abierta.",
                    "Los steps del proyecto no estan registrados: ninguna regla de negocio actuo sobre estas filas.",
                    "sanic_nombre es autonumerico (CONV-nnnnnn); se deja tal cual porque solo existe en filas sinteticas."),
                ["contenido"] = cont,
            });
        });
    }

    // ------------------------------------------------------------------ 3. arreglo
    private static void Fx03Arreglos()
    {
        Paso("fixture 03 arreglo", () =>
        {
            var cont = new JsonObject
            {
                ["borrador_congelado"] = S.Entidad(Leer(ARR, ArrB)),
                ["vigente"] = S.Entidad(Leer(ARR, ArrV)),
                ["fila_relacionada_cliente"] = S.Entidad(Leer(CLI, Cliente)),
                ["fila_relacionada_version_de_plantilla"] = S.Entidad(Leer(VER, Version)),
            };
            Escribir("03-arreglo.json", new JsonObject
            {
                ["cabecera"] = Cabecera("03-arreglo", "SINTETICAS ZZF- (creadas por el exportador y borradas al final)",
                    "Borrador congelado = statecode 0 / statuscode 1 con sanic_fechacongelamiento e instantaneas; Vigente = 0 / 159460001 con sanic_numeroarreglo=1.",
                    "sanic_fechainicio, sanic_fechafin y sanic_fechaformalizacion son fecha DO (solo fecha); sanic_fechacongelamiento y sanic_fechaformalizado son fechahora UL.",
                    "Los montos son decimal(2) (tipo Decimal, no Money)."),
                ["contenido"] = cont,
            });
        });
    }

    // ------------------------------------------------------------------ 4. teammembership, team, queue
    private static void Fx04Equipos()
    {
        Paso("fixture 04 teammembership / team / queue", () =>
        {
            var cont = new JsonObject();
            var notas = new List<string>();

            var idsUsuarios = new List<(string clave, Guid id)> { ("usuario_conectado", UserId) };
            var na = Base["usuario_no_admin_id"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(na)) idsUsuarios.Add(("usuario_de_aplicacion_no_administrador", Guid.Parse(na)));
            foreach (var (clave, id) in idsUsuarios)
            {
                var q = new QueryExpression("teammembership") { ColumnSet = new ColumnSet(true) };
                q.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, id);
                cont["teammembership_de_" + clave] = S.Coleccion(Svc.RetrieveMultiple(q));
            }
            var qm = new QueryExpression("teammembership") { ColumnSet = new ColumnSet(true), TopCount = 5 };
            qm.Criteria.AddCondition("teamid", ConditionOperator.Equal, TeamDef);
            cont["teammembership_de_un_team_por_teamid"] = S.Coleccion(Svc.RetrieveMultiple(qm));

            var qt = new QueryExpression("team") { ColumnSet = new ColumnSet(true) };
            qt.Criteria.AddCondition("isdefault", ConditionOperator.Equal, true);
            qt.Criteria.AddCondition("businessunitid", ConditionOperator.Equal, Guid.Parse(Base["bu_id"]!.GetValue<string>()));
            cont["team_isdefault_de_la_BU_Arreglos_de_Pago"] = S.Coleccion(Svc.RetrieveMultiple(qt));

            var colas = Base["colas_con_emailaddress"]!.AsArray();
            if (colas.Count == 0) notas.Add("No hay ninguna cola con emailaddress en Dev.");
            var arrq = new JsonArray();
            foreach (var g in colas) arrq.Add(S.Entidad(Leer("queue", Guid.Parse(g!.GetValue<string>()))));
            cont["queue_con_emailaddress"] = arrq;

            Escribir("04-teammembership-team-queue.json", new JsonObject
            {
                ["cabecera"] = Cabecera("04-teammembership-team-queue", "REALES anonimizadas (solo lectura)",
                    new[] { "teammembership se consulta por RetrieveMultiple sobre la tabla intersect, sin join.", "El usuario de aplicacion no administrador esta deshabilitado en Dev (estado inicial de los spikes anteriores)." }.Concat(notas).ToArray()),
                ["contenido"] = cont,
            });
        });
    }

    // ------------------------------------------------------------------ 5. variables de entorno
    private static void Fx05VariablesDeEntorno()
    {
        Paso("fixture 05 variables de entorno", () =>
        {
            var cont = new JsonObject();
            var qd = new QueryExpression("environmentvariabledefinition") { ColumnSet = new ColumnSet(true) };
            qd.Criteria.AddCondition("schemaname", ConditionOperator.BeginsWith, "sanic_carp_ev_");
            cont["definiciones_del_proyecto_sanic_carp_ev_"] = S.Coleccion(Svc.RetrieveMultiple(qd));
            if (EvDef != Guid.Empty) cont["definicion_descartable"] = S.Entidad(Leer("environmentvariabledefinition", EvDef));
            if (EvVal != Guid.Empty) cont["valor_descartable"] = S.Entidad(Leer("environmentvariablevalue", EvVal));
            Escribir("05-environmentvariable.json", new JsonObject
            {
                ["cabecera"] = Cabecera("05-environmentvariable", "SINTETICAS sanic_zzf... (no existian variables sanic_carp_ev_*; se creo una definicion de texto descartable FUERA de la solucion y se borra al final)",
                    "type = OptionSetValue(100000000) = Texto.", "El valor y el valor por defecto son 'ZZF-...'."),
                ["contenido"] = cont,
            });
        });
    }

    // ------------------------------------------------------------------ 6. WhoAmI y usuarios
    private static void Fx06Usuarios()
    {
        Paso("fixture 06 WhoAmI / SYSTEM / usuario ordinario", () =>
        {
            var cont = new JsonObject { ["WhoAmIResponse_del_usuario_conectado"] = S.Respuesta(Svc.Execute(new WhoAmIRequest())) };
            var sysIds = Base["system_ids"]!.AsArray();
            cont["systemuser_SYSTEM"] = sysIds.Count > 0 ? S.Entidad(Leer("systemuser", Guid.Parse(sysIds[0]!.GetValue<string>()))) : null;
            cont["systemuser_ordinario_usuario_de_aplicacion_conectado"] = S.Entidad(Leer("systemuser", UserId));
            var na = Base["usuario_no_admin_id"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(na)) cont["systemuser_usuario_de_aplicacion_no_administrador"] = S.Entidad(Leer("systemuser", Guid.Parse(na)));
            var cols = new QueryExpression("systemuser") { ColumnSet = new ColumnSet("fullname", "isdisabled", "businessunitid", "accessmode"), TopCount = 1 };
            cols.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, UserId);
            cont["retrievemultiple_columnas_del_diseno_usuario_conectado"] = S.Coleccion(Svc.RetrieveMultiple(cols));
            Escribir("06-whoami-systemuser.json", new JsonObject
            {
                ["cabecera"] = Cabecera("06-whoami-systemuser", "REALES anonimizadas (solo lectura)",
                    "SYSTEM es la fila con fullname 'SYSTEM' (se conserva el texto SYSTEM). No se puede obtener 'WhoAmI elevado' desde un cliente; el SYSTEM del plugin es solo esta fila.",
                    "El usuario conectado es el usuario de aplicacion de administracion (PP_CLIENT_*)."),
                ["contenido"] = cont,
            });
        });
    }

    // ------------------------------------------------------------------ 8. AliasedValue y agregados
    private static void Fx08AliasedYAgregados()
    {
        Paso("fixture 08 AliasedValue y agregados", () =>
        {
            var cont = new JsonObject();

            var q = new QueryExpression(ARR) { ColumnSet = new ColumnSet("sanic_nombre", "sanic_saldoinicial", "statuscode", "sanic_fechainicio") };
            q.Criteria.AddCondition("sanic_numeroproducto", ConditionOperator.BeginsWith, "ZZF-");
            var lc = q.AddLink(CLI, "sanic_clienteid", "sanic_carp_tbl_clienteid", JoinOperator.Inner);
            lc.EntityAlias = "cli"; lc.Columns = new ColumnSet("sanic_nombre", "createdby", "createdon", "statecode");
            var lv = q.AddLink(VER, "sanic_versionplantillaid", "sanic_carp_tbl_versionplantillaid", JoinOperator.Inner);
            lv.EntityAlias = "ver"; lv.Columns = new ColumnSet("sanic_capacidad", "sanic_estado", "sanic_fechaaprobacion", "sanic_aprobadoporid");
            var lcv = q.AddLink(CONV, "sanic_conversacionid", "sanic_carp_tbl_conversacionid", JoinOperator.LeftOuter);
            lcv.EntityAlias = "conv"; lcv.Columns = new ColumnSet("statecode", "statuscode", "sanic_fechaprimercorreo", "sanic_clienteid", "sanic_cantidadrespuestas");
            cont["querybyexpression_con_joins_alias_cli_ver_conv"] = S.Coleccion(Svc.RetrieveMultiple(q));

            var qe = new QueryExpression("environmentvariabledefinition") { ColumnSet = new ColumnSet("schemaname", "type") };
            qe.Criteria.AddCondition("schemaname", ConditionOperator.BeginsWith, "sanic_zzf");
            var le = qe.AddLink("environmentvariablevalue", "environmentvariabledefinitionid", "environmentvariabledefinitionid", JoinOperator.Inner);
            le.EntityAlias = "val"; le.Columns = new ColumnSet("value", "schemaname");
            cont["querybyexpression_definicion_con_valor_alias_val"] = S.Coleccion(Svc.RetrieveMultiple(qe));

            const string fx1 = @"<fetch aggregate='true'><entity name='sanic_carp_tbl_conversacion'>
<attribute name='sanic_carp_tbl_conversacionid' alias='total' aggregate='count'/>
<attribute name='sanic_fechaprimercorreo' alias='ultima' aggregate='max'/>
<attribute name='sanic_cantidadrespuestas' alias='suma' aggregate='sum'/>
<filter><condition attribute='sanic_huellamessageid' operator='like' value='ZZF-%'/></filter></entity></fetch>";
            cont["fetchxml_agregado_count_max_sum"] = S.Coleccion(Svc.RetrieveMultiple(new FetchExpression(fx1)));
            const string fx2 = @"<fetch aggregate='true'><entity name='sanic_carp_tbl_conversacion'>
<attribute name='statuscode' alias='estado' groupby='true'/>
<attribute name='sanic_carp_tbl_conversacionid' alias='cuantas' aggregate='count'/>
<filter><condition attribute='sanic_huellamessageid' operator='like' value='ZZF-%'/></filter></entity></fetch>";
            cont["fetchxml_agregado_groupby_statuscode"] = S.Coleccion(Svc.RetrieveMultiple(new FetchExpression(fx2)));

            Escribir("08-aliased-y-agregados.json", new JsonObject
            {
                ["cabecera"] = Cabecera("08-aliased-y-agregados", "SINTETICAS ZZF-",
                    "Extra al item 6 del diseno: AliasedValue de joins de QueryExpression (alias.columna) y de agregados de FetchXML.",
                    "El doble 'no usa joins'; este fixture documenta la forma por si se llega a necesitar."),
                ["contenido"] = cont,
            });
        });
    }

    // ------------------------------------------------------------------ 7. fallas, versiones de fila y paginacion
    private static void Fx07Fallas()
    {
        Paso("fixture 07 fallas / RowVersion / paginacion", () =>
        {
            var cont = new JsonObject();

            // (a) K-01: segunda conversacion con la misma sanic_huellamessageid
            var k01 = new JsonObject { ["descripcion"] = "Create de una conversacion con sanic_huellamessageid igual a la de otra ya existente (clave alterna K-01)." };
            try
            {
                var dup = Crear("conv-k01-intento-duplicado", new Entity(CONV)
                {
                    ["sanic_huellamessageid"] = "ZZF-h-duplicada", ["sanic_fechaprimercorreo"] = Ahora(), ["sanic_correonuevoenfinalizada"] = false,
                    ["sanic_cantidadrespuestas"] = 0, ["ownerid"] = Dueno(),
                });
                k01["resultado"] = "SIN FALLA: la plataforma acepto el duplicado (inesperado)";
                Incidencias.Add("K-01: el Create duplicado NO fallo");
            }
            catch (Exception ex)
            {
                k01["resultado"] = "falla";
                k01["falla"] = S.Falla(ex);
                Console.WriteLine("K-01 recibida: " + Resumen(ex));
            }
            cont["k01_violacion_de_clave_alterna"] = k01;

            // (b) version de fila que no coincide
            var rv = new JsonObject { ["descripcion"] = "UpdateRequest con ConcurrencyBehavior.IfRowVersionMatches y un RowVersion viejo sobre una conversacion (concurrencia optimista activa)." };
            var actual = Leer(CONV, ConvA);
            string rv1 = actual.RowVersion;
            rv["rowversion_leida_antes_formato"] = rv1 == null ? null : Regex.Replace(rv1, "[0-9]", "9");
            Svc.Update(new Entity(CONV, ConvA) { ["sanic_detallesinrespuesta"] = "ZZF-d1" });
            var nueva = Svc.Retrieve(CONV, ConvA, new ColumnSet("sanic_detallesinrespuesta"));
            rv["rowversion_cambia_tras_update"] = nueva.RowVersion != rv1;
            try
            {
                var t = new Entity(CONV, ConvA) { ["sanic_detallesinrespuesta"] = "ZZF-d2" };
                t.RowVersion = rv1;
                var r = Svc.Execute(new UpdateRequest { Target = t, ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches });
                rv["resultado"] = "SIN FALLA con RowVersion viejo (inesperado)";
                rv["respuesta"] = S.Respuesta(r);
                Incidencias.Add("RowVersion: el UpdateRequest con RowVersion viejo NO fallo");
            }
            catch (Exception ex)
            {
                rv["resultado"] = "falla";
                rv["falla"] = S.Falla(ex);
                Console.WriteLine("RowVersion recibida: " + Resumen(ex));
            }
            // control: con la version vigente si pasa
            try
            {
                var vig = Svc.Retrieve(CONV, ConvA, new ColumnSet("sanic_detallesinrespuesta"));
                var t2 = new Entity(CONV, ConvA) { ["sanic_detallesinrespuesta"] = "ZZF-d3" };
                t2.RowVersion = vig.RowVersion;
                var r2 = Svc.Execute(new UpdateRequest { Target = t2, ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches });
                rv["control_con_version_vigente"] = new JsonObject { ["resultado"] = "ok", ["respuesta"] = S.Respuesta(r2) };
            }
            catch (Exception ex) { rv["control_con_version_vigente"] = new JsonObject { ["resultado"] = "falla", ["falla"] = S.Falla(ex) }; }
            cont["version_de_fila_no_coincide"] = rv;

            // (c) extra: fila inexistente
            var inex = new JsonObject { ["descripcion"] = "Retrieve de un id que no existe (lo usa la verificacion de limpieza)." };
            try { Svc.Retrieve(CONV, Guid.NewGuid(), new ColumnSet("sanic_nombre")); inex["resultado"] = "SIN FALLA (inesperado)"; }
            catch (Exception ex) { inex["resultado"] = "falla"; inex["falla"] = S.Falla(ex); }
            cont["retrieve_de_fila_inexistente"] = inex;

            // (d) paginacion con Count chico, solo sobre filas ZZF-
            var pag = new JsonObject { ["descripcion"] = "RetrieveMultiple con PageInfo.Count = 2 sobre conversaciones ZZF-; la pagina 2 usa el PagingCookie de la 1." };
            QueryExpression Q(int pagina, string? cookie)
            {
                var q = new QueryExpression(CONV) { ColumnSet = new ColumnSet("sanic_nombre", "statecode", "statuscode") };
                q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.BeginsWith, "ZZF-");
                q.AddOrder("createdon", OrderType.Ascending);
                q.PageInfo = new PagingInfo { Count = 2, PageNumber = pagina, PagingCookie = cookie, ReturnTotalRecordCount = true };
                return q;
            }
            var p1 = Svc.RetrieveMultiple(Q(1, null));
            pag["pagina_1"] = S.Coleccion(p1);
            if (p1.MoreRecords)
            {
                var p2 = Svc.RetrieveMultiple(Q(2, p1.PagingCookie));
                pag["pagina_2"] = S.Coleccion(p2);
            }
            var sinTotal = Q(1, null);
            sinTotal.PageInfo.ReturnTotalRecordCount = false;
            var p0 = Svc.RetrieveMultiple(sinTotal);
            pag["pagina_1_sin_ReturnTotalRecordCount"] = new JsonObject { ["MoreRecords"] = p0.MoreRecords, ["TotalRecordCount"] = p0.TotalRecordCount, ["tiene_PagingCookie"] = p0.PagingCookie != null };
            cont["paginacion"] = pag;

            Escribir("07-fallas.json", new JsonObject
            {
                ["cabecera"] = Cabecera("07-fallas", "SINTETICAS ZZF-",
                    "Se registra lo que llega tal cual: la cadena completa de excepciones (tipo de cada una, en orden) y, si hay un FaultException<OrganizationServiceFault>, su Detail.",
                    "ServiceClient puede envolver la falla en otra excepcion; no se 'arregla'."),
                ["contenido"] = cont,
            });
        });
    }

    // ------------------------------------------------------------------ autocontrol de anonimizacion (solo conteos)
    private static void Autocontrol()
    {
        var reCorreo = new Regex(@"[A-Za-z0-9._%+\-']+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}");
        int ajenos = 0, urls = 0, total = 0;
        foreach (var f in Directory.GetFiles(DirSalida, "*.json"))
        {
            var t = File.ReadAllText(f);
            foreach (Match m in reCorreo.Matches(t)) { total++; if (!m.Value.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase)) ajenos++; }
            urls += Regex.Matches(t, "crm\\.dynamics\\.com|org36e60d9d", RegexOptions.IgnoreCase).Count;
        }
        Console.WriteLine($"autocontrol .NET: correos={total} con dominio distinto de example.com={ajenos} | referencias a la URL u organizacion={urls}");
        if (ajenos > 0 || urls > 0) Incidencias.Add($"autocontrol: correos ajenos={ajenos}, urls={urls}");
    }

    // ------------------------------------------------------------------ limpieza (finally)
    private static void Limpiar()
    {
        if (Svc == null) return;
        List<(string clave, string tabla, Guid id)> filas;
        try { filas = Estado.Filas(); } catch { Console.WriteLine("LIMPIEZA: no se pudo leer estado.json"); return; }
        int borradas = 0, ya = 0, fallos = 0;
        for (int i = filas.Count - 1; i >= 0; i--)
        {
            var (clave, tabla, id) = filas[i];
            try { Svc.Delete(tabla, id); borradas++; }
            catch (Exception ex)
            {
                bool noExiste = false;
                for (Exception? e = ex; e != null; e = e.InnerException)
                    if (e is System.ServiceModel.FaultException<OrganizationServiceFault> f && unchecked((uint)f.Detail.ErrorCode) == 0x80040217) noExiste = true;
                if (noExiste) ya++; else { fallos++; Console.WriteLine($"LIMPIEZA: no se pudo borrar {clave}: {Resumen(ex)}"); }
            }
        }
        Console.WriteLine($"LIMPIEZA (finally): registradas={filas.Count} borradas={borradas} ya_no_existian={ya} fallos={fallos}");
    }
}
