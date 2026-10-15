using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Dbl.Medidor;

/// <summary>
/// Medidor DESCARTABLE del mini spike de verificacion de los dobles (proyecto 2026-003, diseno/03 4.5, lote 3).
/// Lee y escribe en Dev solo con filas sinteticas ZZF- (huella "ZZF-d-...", numero de producto "ZZF-d-...", nombres "ZZF-..."): cada fila se registra
/// en estado.json ANTES de crearse (GUID asignado por el cliente) o, si el id lo pone la plataforma, apenas existe, y se borra en el finally.
/// Los secretos llegan por variables de entorno del PROCESO; la cadena de conexion se arma en memoria. Nada de datos reales en la salida.
/// Modos: afuera (todo lo que se mide por IOrganizationService) | sonda (los escenarios que disparan el plugin sonda; requiere d30) | limpiar.
/// </summary>
public static partial class Program
{
    private const string OrigenTxt = "ServiceClient 1.2.26, cliente fuera del sandbox (net8.0)";
    private const string UrlAutorizada = "https://org36e60d9d.crm.dynamics.com";

    private const string CONV = "sanic_carp_tbl_conversacion";
    private const string ARR = "sanic_carp_tbl_arreglo";
    private const string CLI = "sanic_carp_tbl_cliente";
    private const string VER = "sanic_carp_tbl_versionplantilla";
    private const string CUO = "sanic_carp_tbl_cuota";
    private const string GEN = "sanic_carp_tbl_generacionformato";
    private const string MAP = "sanic_carp_tbl_mapeocelda";

    private static ServiceClient Cli = null!;
    private static IOrganizationService Svc = null!;
    private static Serializador S = null!;
    private static Anonimizador A = null!;
    private static JsonObject Base = null!;
    private static string DirSalida = "";
    private static readonly List<string> Incidencias = new();
    private static readonly Dictionary<Guid, string> Claves = new();
    private static readonly Dictionary<string, Guid> PorClave = new();

    private static Guid UserId, TeamDef, NoAdminId;

    private static readonly JsonSerializerOptions Opc = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

    public static int Main(string[] args)
    {
        Trace.Listeners.Clear();
        var modo = args.Length > 0 ? args[0] : "afuera";
        int rc = 0;
        ServiceClient? client = null;
        try { client = Conectar(); }
        catch (Exception ex)
        {
            Console.WriteLine("ABORTADO al conectar: " + ex.GetType().FullName);
            return 2;
        }

        try
        {
            Cli = client;
            Svc = client;
            A = new Anonimizador();
            S = new Serializador(A);
            DirSalida = Path.Combine(Directory.GetCurrentDirectory(), "salida");
            Directory.CreateDirectory(DirSalida);
            Base = Estado.Leer()["linea_base"]!.AsObject();
            TeamDef = Guid.Parse(Base["team_def_id"]!.GetValue<string>());
            var na = Base["usuario_no_admin_id"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(na)) NoAdminId = Guid.Parse(na);
            Verificar(client);
            switch (modo)
            {
                case "afuera": Afuera(); break;
                case "sonda": Sonda(); break;
                case "extra": Extra(); break;
                case "limpiar": break;   // solo el finally
                default: throw new InvalidOperationException("modo desconocido");
            }
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
        Console.WriteLine($"entorno: URL ok=True, organizacion de WhoAmI coincide con la de la linea base={orgOk}, endpoints coinciden={urlOk}, TZ local={TimeZoneInfo.Local.Id}");
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
        if (msg.Length > 160) msg = msg[..160];
        return string.Join(" > ", tipos) + codigo + " | " + msg;
    }

    private static string CodigoCorto(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
            if (e is System.ServiceModel.FaultException<OrganizationServiceFault> f) return "0x" + unchecked((uint)f.Detail.ErrorCode).ToString("X8");
        return ex.GetType().Name;
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

    /// <summary>Ejecuta un caso. Si funciona, devuelve su valor; si falla, la falla COMPLETA (tipo, HRESULT, InnerFault, claves de ErrorDetails). Nunca lanza.</summary>
    private static JsonObject Intento(string caso, Func<JsonNode?> accion)
    {
        var o = new JsonObject { ["caso"] = caso };
        try
        {
            o["resultado"] = "ok";
            o["valor"] = accion();
            Console.WriteLine($"   [ok]    {caso}");
        }
        catch (Exception ex)
        {
            o["resultado"] = "falla";
            o["falla"] = S.Falla(ex);
            o["codigo"] = CodigoCorto(ex);
            Console.WriteLine($"   [falla] {caso}: {CodigoCorto(ex)}");
        }
        return o;
    }

    private static string K(Guid id) => Claves.TryGetValue(id, out var k) ? k : "(fila-no-sembrada)";

    /// <summary>Crea una fila sintetica ZZF- esperando que funcione: GUID asignado ANTES y registrado en estado.json antes de crearla.</summary>
    private static Guid Crear(string clave, Entity e)
    {
        if (e.Id == Guid.Empty) e.Id = Guid.NewGuid();
        Estado.Registrar(clave, e.LogicalName, e.Id);
        Claves[e.Id] = clave; PorClave[clave] = e.Id;
        var id = Svc.Create(e);
        if (id != e.Id) { Estado.Registrar(clave + "-id-devuelto", e.LogicalName, id); Claves[id] = clave; }
        return id;
    }

    /// <summary>Create de un caso de medicion (puede fallar o no). Sin GUID previo: si funciona, se registra apenas existe.</summary>
    private static Guid CrearIntento(string clave, Entity e)
    {
        e.Id = Guid.Empty;
        var id = Svc.Create(e);
        Estado.Registrar(clave, e.LogicalName, id);
        Claves[id] = clave; PorClave[clave] = id;
        return id;
    }

    private static Guid G(string clave) => PorClave[clave];
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
            ["zona_horaria_local_del_proceso"] = TimeZoneInfo.Local.Id,
            ["desfase_local_respecto_de_UTC_horas"] = TimeZoneInfo.Local.BaseUtcOffset.TotalHours,
            ["generado_utc"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["filas"] = naturaleza,
            ["notas"] = n,
        };
    }

    private static void Escribir(string archivo, JsonObject doc)
    {
        File.WriteAllText(Path.Combine(DirSalida, archivo), doc.ToJsonString(Opc));
    }

    private static Entity Leer(string tabla, Guid id, params string[] columnas) =>
        Svc.Retrieve(tabla, id, columnas.Length == 0 ? new ColumnSet(true) : new ColumnSet(columnas));

    /// <summary>Un valor .NET como {"tipo","valor"} (con el Kind de las fechas), anonimizando lo que no sea ZZF-.</summary>
    private static JsonNode V(object? v, string atributo = "", string entidad = "") => S.Valor(v, atributo, entidad);

    private static JsonArray Arr(IEnumerable<string> xs) => new(xs.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());

    private static string Pk(string tabla) => tabla == "email" ? "activityid" : tabla + "id";

    private static string Hex(Exception ex) => CodigoCorto(ex);

    // ------------------------------------------------------------------ limpieza (finally)
    private static void Limpiar()
    {
        if (Svc == null) return;
        // la identidad de la suplantacion nunca debe sobrevivir
        try { Cli.CallerId = Guid.Empty; } catch { }
        List<(string clave, string tabla, Guid id)> filas;
        try { filas = Estado.Filas(); } catch { Console.WriteLine("LIMPIEZA: no se pudo leer estado.json"); return; }
        int borradas = 0, ya = 0, fallos = 0;
        var pendientes = new List<(string clave, string tabla, Guid id)>();
        for (int i = filas.Count - 1; i >= 0; i--) pendientes.Add(filas[i]);
        // varias pasadas: una fila padre nacida DESPUES que su hija (p. ej. un cliente creado a mitad de una prueba) falla en la primera y cae en la segunda
        for (int pasada = 1; pasada <= 4 && pendientes.Count > 0; pasada++)
        {
            var siguientes = new List<(string clave, string tabla, Guid id)>();
            foreach (var fila in pendientes)
            {
                var (clave, tabla, id) = fila;
                try { Svc.Delete(tabla, id); borradas++; }
                catch (Exception ex)
                {
                    bool noExiste = false;
                    for (Exception? e = ex; e != null; e = e.InnerException)
                        if (e is System.ServiceModel.FaultException<OrganizationServiceFault> f && unchecked((uint)f.Detail.ErrorCode) == 0x80040217) noExiste = true;
                    if (noExiste) ya++;
                    else if (pasada < 4) siguientes.Add(fila);
                    else { fallos++; Console.WriteLine($"LIMPIEZA: no se pudo borrar {clave} ({tabla}): {Resumen(ex)}"); }
                }
            }
            pendientes = siguientes;
        }
        Console.WriteLine($"LIMPIEZA (finally): registradas={filas.Count} borradas={borradas} ya_no_existian={ya} fallos={fallos}");
        if (Incidencias.Count > 0 && DirSalida != "")
        {
            var inc = new JsonObject { ["incidencias"] = Arr(Incidencias.Select(i => A.Mensaje(i))) };
            File.WriteAllText(Path.Combine(DirSalida, "00-incidencias-" + (Environment.GetCommandLineArgs().Length > 1 ? Environment.GetCommandLineArgs()[1] : "afuera") + ".json"), inc.ToJsonString(Opc));
        }
    }
}
