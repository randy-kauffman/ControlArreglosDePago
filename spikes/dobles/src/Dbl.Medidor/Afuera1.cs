using System.Text.Json.Nodes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dbl.Medidor;

public static partial class Program
{

    /// <summary>Orquesta todo lo que se mide DESDE AFUERA. Cada seccion es un Paso: una falla de una no impide las demas.</summary>
    private static void Afuera()
    {
        Paso("preparar filas base", Preparar);
        Paso("(11) autonumerico", Autonumerico);
        Paso("(1) condiciones con valores tipados", CondicionesTipadas);
        Paso("(3)(4)(5) operadores y filtros", OperadoresYFiltros);
        Paso("(2)(e) fechas escritas", FechasEscritas);
        Paso("(6) estados de Conversacion y Arreglo", Estados);
        Paso("(7)(8) email", Emails);
        Paso("(9) Name del lookup", NameDelLookup);
        Paso("(12)(14)(d)(c)(i) texto y esquema", TextoYEsquema);
        Paso("(10) cookie de paginacion", CookieDePaginacion);
        Paso("(13) nativas sin muestra", NativasSinMuestra);
        Paso("(a) privilegios con CallerId", Privilegios);
        Paso("(15) borrado por relacion", BorradoPorRelacion);
        Console.WriteLine($"incidencias: {Incidencias.Count} | tipos .NET distintos vistos: {S.TiposVistos.Count} | no modelados: {S.TiposNoModelados.Count}");
    }

    // ------------------------------------------------------------------ fabricas de filas sinteticas ZZF-
    private static Entity ConvE(string sufijo, Action<Entity>? mas = null)
    {
        var e = new Entity(CONV)
        {
            ["sanic_huellamessageid"] = "ZZF-d-" + sufijo, ["sanic_fechaprimercorreo"] = Ahora(), ["sanic_correonuevoenfinalizada"] = false, ["sanic_cantidadrespuestas"] = 0,
            ["ownerid"] = Dueno(),
        };
        mas?.Invoke(e);
        return e;
    }

    private static Entity ArrE(string sufijo, Guid cliente, Guid version, Action<Entity>? mas = null)
    {
        var e = new Entity(ARR)
        {
            ["sanic_producto"] = new OptionSetValue(159460001), ["sanic_numeroproducto"] = "ZZF-d-" + sufijo, ["sanic_tipo"] = new OptionSetValue(159460001),
            ["sanic_moneda"] = new OptionSetValue(159460001), ["sanic_canalorigen"] = new OptionSetValue(159460001),
            ["sanic_saldoinicial"] = 1000.50m, ["sanic_montoabonado"] = 100.25m, ["sanic_saldoremanente"] = 900.25m, ["sanic_plazo"] = 6,
            ["sanic_cuotamensual"] = 150.04m, ["sanic_totalpagar"] = 900.25m,
            ["sanic_fechainicio"] = new DateTime(2026, 11, 1), ["sanic_fechafin"] = new DateTime(2027, 4, 1), ["sanic_fechaformalizacion"] = new DateTime(2026, 10, 10),
            ["sanic_cuentadebitar"] = "ZZF-cta-0001", ["sanic_cantidadreaperturas"] = 0, ["sanic_instantaneacedula"] = "ZZF-ced-0001",
            ["sanic_comentarios"] = "ZZF-comentario " + sufijo, ["ownerid"] = Dueno(),
            ["sanic_clienteid"] = Ref(CLI, cliente), ["sanic_versionplantillaid"] = Ref(VER, version), ["sanic_oficialid"] = Ref("systemuser", UserId),
        };
        mas?.Invoke(e);
        return e;
    }

    private static Entity VerE(string nombre) => new(VER)
    {
        ["sanic_nombre"] = nombre, ["sanic_capacidad"] = 60, ["sanic_estado"] = new OptionSetValue(159460002),
        ["sanic_fechaaprobacion"] = Ahora(), ["sanic_aprobadoporid"] = Ref("systemuser", UserId), ["ownerid"] = Dueno(),
    };

    private static Entity GenE(Guid arreglo, Guid version) => new(GEN)
    {
        ["sanic_fechageneracion"] = Ahora(), ["sanic_arregloid"] = Ref(ARR, arreglo), ["sanic_versionplantillaid"] = Ref(VER, version),
        ["sanic_generadoporid"] = Ref("systemuser", UserId), ["ownerid"] = Dueno(),
    };

    private static void Preparar()
    {
        Crear("cli-A", new Entity(CLI) { ["sanic_nombre"] = "ZZF-José", ["sanic_nombrecompleto"] = "ZZF-José", ["sanic_cedula"] = "ZZF-ced-A", ["ownerid"] = Dueno() });
        Crear("ver-A", VerE("ZZF-vA"));
        Crear("ver-B", VerE("ZZF-vB"));
    }

    // ------------------------------------------------------------------ (11) autonumerico
    private static int Num(string? nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return -1;
        var i = nombre.LastIndexOf('-');
        return int.TryParse(nombre[(i + 1)..], out var n) ? n : -1;
    }

    private static string? Nombre(string tabla, string clave) => Leer(tabla, G(clave), "sanic_nombre").GetAttributeValue<string>("sanic_nombre");

    private static void Autonumerico()
    {
        var casos = new JsonArray();
        var secuencia = new JsonArray();   // en orden cronologico: que operacion, que numero salio
        void Nota(string tabla, string que, string? nombre) => secuencia.Add(new JsonObject { ["tabla"] = tabla, ["operacion"] = que, ["sanic_nombre"] = nombre, ["numero"] = Num(nombre) });

        // ---- Conversacion (CONV-{SEQNUM:6}): 2 altas seguidas, K-01 fallida, alta, lookup inexistente, alta, par de estado invalido, alta
        Crear("conv-Ca", ConvE("Ca")); Nota(CONV, "alta Ca", Nombre(CONV, "conv-Ca"));
        Crear("conv-Cb", ConvE("Cb", e => { e["sanic_asunto"] = "ZZF-Alfa"; e["sanic_cantidadrespuestas"] = 5; e["sanic_resultado"] = new OptionSetValue(159460002); })); Nota(CONV, "alta Cb (seguida de Ca)", Nombre(CONV, "conv-Cb"));
        casos.Add(Intento("conv: Create con huella duplicada (K-01)", () => { CrearIntento("conv-dup-k01", ConvE("Ca")); return null; })); 
        Crear("conv-Cc", ConvE("Cc", e => { e["sanic_asunto"] = "ZZF-Beta"; e["sanic_cantidadrespuestas"] = 7; e["sanic_clienteid"] = Ref(CLI, G("cli-A")); })); Nota(CONV, "alta Cc (tras un Create fallido por K-01)", Nombre(CONV, "conv-Cc"));
        casos.Add(Intento("conv: Create con lookup a cliente inexistente", () => { CrearIntento("conv-lookup-inexistente", ConvE("x1", e => e["sanic_clienteid"] = Ref(CLI, Guid.NewGuid()))); return null; }));
        Crear("conv-Cd", ConvE("Cd", e => { e["sanic_asunto"] = ""; e["sanic_cantidadrespuestas"] = 3; })); Nota(CONV, "alta Cd (tras un Create fallido por lookup inexistente)", Nombre(CONV, "conv-Cd"));
        casos.Add(Intento("conv: Create con par statecode/statuscode invalido (0,2)", () => { CrearIntento("conv-par-invalido", ConvE("x2", e => { e["statecode"] = new OptionSetValue(0); e["statuscode"] = new OptionSetValue(2); })); return null; }));
        Crear("conv-Ce", ConvE("Ce", e => e["sanic_asunto"] = "ZZF-Gamma")); Nota(CONV, "alta Ce (tras un Create fallido por par invalido)", Nombre(CONV, "conv-Ce"));
        // un valor enviado se conserva y no consume
        Crear("conv-valor-enviado", ConvE("enviado", e => e["sanic_nombre"] = "ZZF-a-mano")); Nota(CONV, "alta con sanic_nombre enviado", Nombre(CONV, "conv-valor-enviado"));
        Crear("conv-Cf", ConvE("Cf")); Nota(CONV, "alta Cf (tras una con sanic_nombre enviado)", Nombre(CONV, "conv-Cf"));

        // ---- Arreglo (ARR-{SEQNUM:6}): K-02 = producto + numeroproducto + numeroarreglo
        var cli = G("cli-A"); var verA = G("ver-A");
        Crear("arr-A1", ArrE("p1", cli, verA, e => e["sanic_numeroarreglo"] = 1)); Nota(ARR, "alta A1", Nombre(ARR, "arr-A1"));
        Crear("arr-A2", ArrE("p2", cli, verA)); Nota(ARR, "alta A2 (seguida de A1)", Nombre(ARR, "arr-A2"));
        casos.Add(Intento("arr: Create con clave K-02 duplicada", () => { CrearIntento("arr-dup-k02", ArrE("p1", cli, verA, e => e["sanic_numeroarreglo"] = 1)); return null; }));
        Crear("arr-A3", ArrE("p3", cli, verA)); Nota(ARR, "alta A3 (tras un Create fallido por K-02)", Nombre(ARR, "arr-A3"));
        casos.Add(Intento("arr: Create con lookup a version inexistente", () => { CrearIntento("arr-lookup-inexistente", ArrE("x3", cli, Guid.NewGuid())); return null; }));
        Crear("arr-A4", ArrE("p4", cli, verA)); Nota(ARR, "alta A4 (tras un Create fallido por lookup inexistente)", Nombre(ARR, "arr-A4"));

        // ---- Generacion (GEN-{SEQNUM:8})
        Crear("gen-G1", GenE(G("arr-A1"), G("ver-B"))); Nota(GEN, "alta G1", Nombre(GEN, "gen-G1"));
        Crear("gen-G2", GenE(G("arr-A1"), G("ver-B"))); Nota(GEN, "alta G2 (seguida de G1)", Nombre(GEN, "gen-G2"));
        casos.Add(Intento("gen: Create con lookup a arreglo inexistente", () => { CrearIntento("gen-lookup-inexistente", GenE(Guid.NewGuid(), G("ver-B"))); return null; }));
        Crear("gen-G3", GenE(G("arr-A1"), G("ver-B"))); Nota(GEN, "alta G3 (tras un Create fallido por lookup inexistente)", Nombre(GEN, "gen-G3"));

        // ---- deltas entre altas exitosas consecutivas
        var deltas = new JsonArray();
        foreach (var tabla in new[] { CONV, ARR, GEN })
        {
            var buenas = secuencia.Where(n => n!["tabla"]!.GetValue<string>() == tabla && n["operacion"]!.GetValue<string>().StartsWith("alta") && !n["operacion"]!.GetValue<string>().StartsWith("alta con sanic_nombre")).ToList();
            for (int i = 1; i < buenas.Count; i++)
                deltas.Add(new JsonObject { ["tabla"] = tabla, ["de"] = buenas[i - 1]!["operacion"]!.GetValue<string>(), ["a"] = buenas[i]!["operacion"]!.GetValue<string>(), ["delta"] = buenas[i]!["numero"]!.GetValue<int>() - buenas[i - 1]!["numero"]!.GetValue<int>() });
        }
        Escribir("17-autonumerico.json", new JsonObject
        {
            ["cabecera"] = Cabecera("17-autonumerico", "SINTETICAS ZZF- (borradas al final). La parte del plugin sonda esta en 30-sonda-escenarios.json (escenarios rev y anidado)",
                "Punto (11): cuanto avanza la secuencia entre Creates seguidos; si un Create que falla consume numero (K-01 / lookup inexistente / par invalido); un valor enviado no consume.",
                "NO se uso SetAutoNumberSeed ni se cambio ningun formato. 'Que pasa pasado 999999' queda NO MEDIDO.",
                "'delta' = numero de la alta siguiente menos el de la alta anterior en la secuencia cronologica; 1 = no consume; 2 = el Create intermedio consumio uno."),
            ["contenido"] = new JsonObject { ["secuencia_cronologica"] = secuencia, ["deltas_entre_altas_exitosas_consecutivas"] = deltas, ["creates_fallidos"] = casos },
        });
    }
}
