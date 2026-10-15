using System.Text.Json.Nodes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Dbl.Medidor;

public static partial class Program
{
    // ------------------------------------------------------------------ consultas de medicion
    /// <summary>Ejecuta una QueryExpression sobre una tabla propia restringida a las filas ZZF-d- y devuelve cuantas filas y cuales (por clave, no por GUID).</summary>
    private static JsonObject Consulta(string caso, string tabla, Action<QueryExpression> arma, bool restringir = true, params string[] columnas)
    {
        return Intento(caso, () =>
        {
            var q = new QueryExpression(tabla) { ColumnSet = columnas.Length == 0 ? new ColumnSet(Pk(tabla)) : new ColumnSet(columnas) };
            if (restringir) q.Criteria.AddCondition(tabla == CONV ? "sanic_huellamessageid" : tabla == ARR ? "sanic_numeroproducto" : "sanic_nombre", ConditionOperator.BeginsWith, tabla == CONV || tabla == ARR ? "ZZF-d-" : "ZZF-");
            arma(q);
            var r = Svc.RetrieveMultiple(q);
            return new JsonObject { ["n"] = r.Entities.Count, ["claves"] = Arr(r.Entities.Select(e => K(e.Id)).OrderBy(x => x, StringComparer.Ordinal)), ["MoreRecords"] = r.MoreRecords };
        });
    }

    private static string H(string sufijo) => "ZZF-d-" + sufijo;

    // ------------------------------------------------------------------ (1)
    private static void CondicionesTipadas()
    {
        var c = new JsonArray();
        var cli = G("cli-A");
        void Conv(string caso, string col, ConditionOperator op, object valor) => c.Add(Consulta(caso, CONV, q => q.Criteria.AddCondition(col, op, valor)));
        void ConvMulti(string caso, string col, ConditionOperator op, params object[] valores) => c.Add(Consulta(caso, CONV, q => q.Criteria.AddCondition(col, op, valores)));
        void Arreglo(string caso, string col, ConditionOperator op, object valor) => c.Add(Consulta(caso, ARR, q => q.Criteria.AddCondition(col, op, valor)));

        // choices (control con int y luego OptionSetValue)
        Conv("control: statuscode Equal int 1", "statuscode", ConditionOperator.Equal, 1);
        Conv("OptionSetValue: statuscode Equal OptionSetValue(1)", "statuscode", ConditionOperator.Equal, new OptionSetValue(1));
        Conv("OptionSetValue: statecode Equal OptionSetValue(0)", "statecode", ConditionOperator.Equal, new OptionSetValue(0));
        Conv("control: sanic_resultado Equal int 159460002", "sanic_resultado", ConditionOperator.Equal, 159460002);
        Conv("OptionSetValue: sanic_resultado Equal OptionSetValue(159460002)", "sanic_resultado", ConditionOperator.Equal, new OptionSetValue(159460002));
        ConvMulti("OptionSetValue: sanic_resultado In [OptionSetValue, OptionSetValue]", "sanic_resultado", ConditionOperator.In, new OptionSetValue(159460002), new OptionSetValue(159460001));
        Conv("OptionSetValue: sanic_resultado NotEqual OptionSetValue(159460002)", "sanic_resultado", ConditionOperator.NotEqual, new OptionSetValue(159460002));
        Conv("OptionSetValue: sanic_resultado GreaterThan OptionSetValue(159460001)", "sanic_resultado", ConditionOperator.GreaterThan, new OptionSetValue(159460001));
        // lookups
        Conv("control: sanic_clienteid Equal Guid", "sanic_clienteid", ConditionOperator.Equal, cli);
        Conv("EntityReference: sanic_clienteid Equal EntityReference", "sanic_clienteid", ConditionOperator.Equal, Ref(CLI, cli));
        ConvMulti("EntityReference: sanic_clienteid In [EntityReference]", "sanic_clienteid", ConditionOperator.In, Ref(CLI, cli));
        Conv("EntityReference: sanic_clienteid NotEqual EntityReference", "sanic_clienteid", ConditionOperator.NotEqual, Ref(CLI, cli));
        Conv("EntityReference: sanic_clienteid Equal EntityReference a OTRA tabla (misma Guid)", "sanic_clienteid", ConditionOperator.Equal, Ref(CONV, cli));
        Conv("EntityReference: ownerid Equal EntityReference(team)", "ownerid", ConditionOperator.Equal, Dueno());
        Conv("control: ownerid Equal Guid del team", "ownerid", ConditionOperator.Equal, TeamDef);
        // dinero (las columnas del proyecto son decimal, no Money)
        Arreglo("control: sanic_saldoinicial Equal decimal 1000.5", "sanic_saldoinicial", ConditionOperator.Equal, 1000.5m);
        Arreglo("Money: sanic_saldoinicial Equal Money(1000.5)", "sanic_saldoinicial", ConditionOperator.Equal, new Money(1000.5m));
        Arreglo("Money: sanic_saldoinicial GreaterThan Money(1000)", "sanic_saldoinicial", ConditionOperator.GreaterThan, new Money(1000m));
        c.Add(Consulta("Money: sanic_saldoinicial In [Money]", ARR, q => q.Criteria.AddCondition("sanic_saldoinicial", ConditionOperator.In, new object[] { new Money(1000.5m) })));
        // valores de otro tipo en una columna: orientativo de como valida la plataforma el valor de la condicion
        Conv("tipo equivocado: sanic_cantidadrespuestas Equal string '5'", "sanic_cantidadrespuestas", ConditionOperator.Equal, "5");
        Conv("tipo equivocado: sanic_cantidadrespuestas Equal Money(5)", "sanic_cantidadrespuestas", ConditionOperator.Equal, new Money(5m));
        Conv("tipo equivocado: sanic_cantidadrespuestas Equal OptionSetValue(5)", "sanic_cantidadrespuestas", ConditionOperator.Equal, new OptionSetValue(5));

        Escribir("10-condiciones-valores-tipados.json", new JsonObject
        {
            ["cabecera"] = Cabecera("10-condiciones-valores-tipados", "SINTETICAS ZZF- (Conversacion y Arreglo)",
                "Punto (1): OptionSetValue, EntityReference y Money como VALOR de una ConditionExpression. Cada caso: cuantas filas devolvio (n), cuales (por clave del spike) o la falla completa.",
                "La tabla solo tiene filas ZZF-: cada consulta ademas se restringe a huella / numero de producto que empiezan con ZZF-d-."),
            ["contenido"] = new JsonObject { ["casos"] = c },
        });
    }

    // ------------------------------------------------------------------ (3)(4)(5)
    private static void OperadoresYFiltros()
    {
        var c3 = new JsonArray(); var c4 = new JsonArray(); var c5 = new JsonArray();
        string hA = H("Ca"), hB = H("Cb"), hC = H("Cc");
        var lista = new object[] { hA, hB, hC };
        void C(JsonArray dest, string caso, Action<QueryExpression> arma, bool restringir = true) => dest.Add(Consulta(caso, CONV, arma, restringir));

        // (3) varios valores en operadores de un solo valor
        C(c3, "control: huella In [Ca,Cb,Cc]", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.In, lista));
        C(c3, "control: huella In [Ca] (un solo valor)", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.In, new object[] { hA }));
        C(c3, "Equal con 2 valores [Ca,Cb] sobre huella", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.Equal, new object[] { hA, hB }));
        C(c3, "Equal con 3 valores [Ca,Cb,Cc] sobre huella", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.Equal, lista));
        C(c3, "Equal con 0 valores", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.Equal, new object[0]));
        C(c3, "In con 0 valores", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.In, new object[0]));
        C(c3, "NotEqual con 2 valores [Ca,Cb] sobre huella", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.NotEqual, new object[] { hA, hB }));
        C(c3, "GreaterThan con 2 valores [4,6] sobre sanic_cantidadrespuestas (Ca=0 Cb=5 Cc=7 Cd=3)", q => q.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.GreaterThan, new object[] { 4, 6 }));
        C(c3, "GreaterEqual con 2 valores [5,7]", q => q.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.GreaterEqual, new object[] { 5, 7 }));
        C(c3, "LessThan con 2 valores [4,6]", q => q.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.LessThan, new object[] { 4, 6 }));
        C(c3, "LessEqual con 2 valores [3,5]", q => q.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.LessEqual, new object[] { 3, 5 }));
        C(c3, "control: GreaterThan 4 (un valor)", q => q.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.GreaterThan, 4));
        C(c3, "Like con 2 valores ['%Ca','%Cb']", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.Like, new object[] { "%-Ca", "%-Cb" }));
        C(c3, "control: Like un valor '%-Ca'", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.Like, "%-Ca"));
        C(c3, "BeginsWith con 2 valores ['ZZF-d-Ca','ZZF-d-Cb']", q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.BeginsWith, new object[] { hA, hB }));
        C(c3, "Null con un valor sobrante ('x') sobre sanic_asunto", q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.Null, "x"));
        C(c3, "NotNull con un valor sobrante ('x') sobre sanic_asunto", q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotNull, "x"));
        C(c3, "control: Null sobre sanic_asunto", q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.Null));
        C(c3, "control: NotNull sobre sanic_asunto", q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotNull));
        C(c3, "Equal sin valores via constructor sin value (ConditionExpression(attr, Equal))", q => q.Criteria.AddCondition(new ConditionExpression("sanic_huellamessageid", ConditionOperator.Equal)));

        // (4) NotEqual sobre una columna nula. Filas: Ca asunto ausente, Cb 'ZZF-Alfa', Cc 'ZZF-Beta', Cd '' (enviado vacio), Ce 'ZZF-Gamma'; clientes: solo Cc; resultado: solo Cb
        C(c4, "referencia: todas las filas del conjunto", q => { });
        C(c4, "referencia: sanic_asunto Null", q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.Null));
        C(c4, "NotEqual 'ZZF-Alfa' sobre sanic_asunto (texto)", q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotEqual, "ZZF-Alfa"));
        C(c4, "NotIn ['ZZF-Alfa'] sobre sanic_asunto", q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotIn, new object[] { "ZZF-Alfa" }));
        C(c4, "NotEqual 'ZZF-Alfa' AND NotNull sobre sanic_asunto", q => { q.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotEqual, "ZZF-Alfa"); q.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotNull); });
        C(c4, "NotEqual 'ZZF-Alfa' OR Null sobre sanic_asunto", q => { var f = q.Criteria.AddFilter(LogicalOperator.Or); f.AddCondition("sanic_asunto", ConditionOperator.NotEqual, "ZZF-Alfa"); f.AddCondition("sanic_asunto", ConditionOperator.Null); });
        C(c4, "NotEqual Guid del cliente sobre sanic_clienteid (lookup nulo en 5 de 6 filas)", q => q.Criteria.AddCondition("sanic_clienteid", ConditionOperator.NotEqual, G("cli-A")));
        C(c4, "NotEqual 159460002 sobre sanic_resultado (choice nulo salvo Cb)", q => q.Criteria.AddCondition("sanic_resultado", ConditionOperator.NotEqual, 159460002));
        C(c4, "NotEqual 5 sobre sanic_tiempototal (entero nulo en todas)", q => q.Criteria.AddCondition("sanic_tiempototal", ConditionOperator.NotEqual, 5));
        C(c4, "NotEqual 5 sobre sanic_cantidadrespuestas (entero lleno)", q => q.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.NotEqual, 5));
        C(c4, "NotEqual '' (cadena vacia) sobre sanic_asunto", q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotEqual, ""));
        C(c4, "Equal '' (cadena vacia) sobre sanic_asunto", q => q.Criteria.AddCondition("sanic_asunto", ConditionOperator.Equal, ""));

        // (5) subfiltro vacio dentro de otro. Conjunto: 3 filas elegidas por huella (lista) de las que hay mas en la tabla
        QueryExpression Armar(QueryExpression q, LogicalOperator externo, Action<FilterExpression> poner)
        {
            q.Criteria = new FilterExpression(externo);
            poner(q.Criteria);
            return q;
        }
        void Vacio(string caso, LogicalOperator externo, LogicalOperator interno, bool conCondicion, bool dosVacios = false)
        {
            c5.Add(Consulta(caso, CONV, q =>
            {
                q.Criteria = new FilterExpression(externo);
                if (conCondicion) q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.In, lista);
                q.Criteria.AddFilter(interno);
                if (dosVacios) q.Criteria.AddFilter(interno);
            }, restringir: false));
        }
        c5.Add(Consulta("referencia: el conjunto completo ZZF-d- (todas las conversaciones)", CONV, q => { }, restringir: true));
        c5.Add(Consulta("referencia: solo la condicion In [Ca,Cb,Cc]", CONV, q => q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.In, lista), restringir: false));
        Vacio("AND { In[Ca,Cb,Cc], filtro AND vacio }", LogicalOperator.And, LogicalOperator.And, true);
        Vacio("AND { In[Ca,Cb,Cc], filtro OR vacio }", LogicalOperator.And, LogicalOperator.Or, true);
        Vacio("OR { In[Ca,Cb,Cc], filtro AND vacio }", LogicalOperator.Or, LogicalOperator.And, true);
        Vacio("OR { In[Ca,Cb,Cc], filtro OR vacio }", LogicalOperator.Or, LogicalOperator.Or, true);
        Vacio("OR { In[Ca,Cb,Cc], dos filtros vacios }", LogicalOperator.Or, LogicalOperator.And, true, dosVacios: true);
        Vacio("solo { filtro AND vacio } (sin condicion)", LogicalOperator.And, LogicalOperator.And, false);
        Vacio("solo { filtro OR vacio } (sin condicion)", LogicalOperator.Or, LogicalOperator.Or, false);
        c5.Add(Consulta("criterio raiz vacio (sin condiciones ni filtros), sin restriccion de huella", CONV, q => { }, restringir: false));
        c5.Add(Consulta("AND { In[Ca,Cb,Cc], filtro AND { filtro AND vacio } } (vacio anidado dos niveles)", CONV, q =>
        {
            q.Criteria = new FilterExpression(LogicalOperator.And);
            q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.In, lista);
            q.Criteria.AddFilter(LogicalOperator.And).AddFilter(LogicalOperator.And);
        }, restringir: false));
        c5.Add(Consulta("OR { In[Ca], filtro OR { Equal huella Cb, filtro vacio } }", CONV, q =>
        {
            q.Criteria = new FilterExpression(LogicalOperator.Or);
            q.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.Equal, hA);
            var f = q.Criteria.AddFilter(LogicalOperator.Or);
            f.AddCondition("sanic_huellamessageid", ConditionOperator.Equal, hB);
            f.AddFilter(LogicalOperator.And);
        }, restringir: false));

        Escribir("12-operadores-y-filtros.json", new JsonObject
        {
            ["cabecera"] = Cabecera("12-operadores-y-filtros", "SINTETICAS ZZF- (Conversaciones Ca, Cb, Cc, Cd, Ce, Cf...)",
                "Punto (3): operadores de un solo valor con varios valores. Punto (4): NotEqual sobre columnas nulas. Punto (5): subfiltro vacio dentro de otro (AND y OR).",
                "Datos: sanic_asunto: Ca ausente, Cb 'ZZF-Alfa', Cc 'ZZF-Beta', Cd '' (enviado como cadena vacia), Ce 'ZZF-Gamma', Cf ausente. sanic_cantidadrespuestas: Ca 0, Cb 5, Cc 7, Cd 3, Ce 0, Cf 0. sanic_clienteid: solo Cc. sanic_resultado: solo Cb.",
                "Las huellas son 'ZZF-d-<clave sin conv->'. Se informa el conjunto devuelto por clave del spike, no por GUID."),
            ["contenido"] = new JsonObject { ["punto_3_varios_valores"] = c3, ["punto_4_notequal_sobre_nulos"] = c4, ["punto_5_subfiltro_vacio"] = c5 },
        });
    }

    // ------------------------------------------------------------------ (2) y (e)
    private static JsonObject Dt(DateTime d) => new() { ["iso"] = d.ToString("O"), ["kind"] = d.Kind.ToString(), ["ticks"] = d.Ticks };

    private static JsonObject FechaCaso(string caso, string tabla, string clave, string col, DateTime valor)
    {
        return Intento(caso, () =>
        {
            var id = G(clave);
            var e = new Entity(tabla, id) { [col] = valor };
            Svc.Update(e);
            var r = Leer(tabla, id, col);
            var leido = r.Contains(col) ? (DateTime?)r.GetAttributeValue<DateTime>(col) : null;
            var o = new JsonObject { ["enviado"] = Dt(valor), ["leido"] = leido == null ? null : Dt(leido.Value) };
            if (leido != null) o["delta_leido_menos_enviado_ticks"] = leido.Value.Ticks - valor.Ticks;
            return o;
        });
    }

    private static void FechasEscritas()
    {
        var precision = new JsonArray(); var rango = new JsonArray(); var kinds = new JsonArray();
        DateTime U(int y, int mo, int d, int h = 0, int mi = 0, int s = 0, int ms = 0, long extraTicks = 0) => new DateTime(y, mo, d, h, mi, s, ms, DateTimeKind.Utc).AddTicks(extraTicks);
        DateTime Un(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Unspecified);

        // columnas: UL en Conversacion (sanic_fechafinalizacion) y en Arreglo (sanic_fechacongelamiento); DO en Arreglo (sanic_fechafin, DateOnly)
        var objetivos = new (string etiqueta, string tabla, string clave, string col, bool soloFecha)[]
        {
            ("UL conversacion sanic_fechafinalizacion", CONV, "conv-Ca", "sanic_fechafinalizacion", false),
            ("UL arreglo sanic_fechacongelamiento", ARR, "arr-A2", "sanic_fechacongelamiento", false),
            ("DO arreglo sanic_fechafin", ARR, "arr-A2", "sanic_fechafin", true),
        };
        foreach (var (etq, tabla, clave, col, solo) in objetivos)
        {
            Func<DateTime, DateTime> kind = d => solo ? Un(d) : d;   // en una DO se envia Unspecified (lo natural); en UL, Utc
            // precision
            precision.Add(FechaCaso($"{etq}: 13:45:30.789 (milisegundos)", tabla, clave, col, kind(U(2026, 3, 5, 13, 45, 30, 789))));
            precision.Add(FechaCaso($"{etq}: 13:45:30.1234567 (ticks, 7 digitos)", tabla, clave, col, kind(U(2026, 3, 5, 13, 45, 30, 0, 1234567))));
            precision.Add(FechaCaso($"{etq}: 13:45:30.9999999 (redondeo hacia arriba?)", tabla, clave, col, kind(U(2026, 3, 5, 13, 45, 30, 0, 9999999))));
            precision.Add(FechaCaso($"{etq}: 13:45:30.0000001 (1 tick)", tabla, clave, col, kind(U(2026, 3, 5, 13, 45, 30, 0, 1))));
            precision.Add(FechaCaso($"{etq}: 13:45:59.500 (medio segundo)", tabla, clave, col, kind(U(2026, 3, 5, 13, 45, 59, 500))));
            precision.Add(FechaCaso($"{etq}: 23:59:59.999 (fin del dia)", tabla, clave, col, kind(U(2026, 3, 5, 23, 59, 59, 999))));
            // rango
            rango.Add(FechaCaso($"{etq}: DateTime.MinValue (0001-01-01, Kind {(solo ? "Unspecified" : "Utc")})", tabla, clave, col, kind(U(1, 1, 1))));
            rango.Add(FechaCaso($"{etq}: 1752-12-31", tabla, clave, col, kind(U(1752, 12, 31))));
            rango.Add(FechaCaso($"{etq}: 1753-01-01", tabla, clave, col, kind(U(1753, 1, 1))));
            rango.Add(FechaCaso($"{etq}: 1753-01-01 00:00:00.001", tabla, clave, col, kind(U(1753, 1, 1, 0, 0, 0, 1))));
            rango.Add(FechaCaso($"{etq}: 1899-12-31", tabla, clave, col, kind(U(1899, 12, 31))));
            rango.Add(FechaCaso($"{etq}: 1900-01-01", tabla, clave, col, kind(U(1900, 1, 1))));
            rango.Add(FechaCaso($"{etq}: 9999-12-31 00:00:00", tabla, clave, col, kind(U(9999, 12, 31))));
            rango.Add(FechaCaso($"{etq}: 9999-12-31 23:59:59", tabla, clave, col, kind(U(9999, 12, 31, 23, 59, 59))));
            rango.Add(FechaCaso($"{etq}: DateTime.MaxValue", tabla, clave, col, kind(new DateTime(DateTime.MaxValue.Ticks, DateTimeKind.Utc))));
            // (e) Kind de lo ESCRITO, con hora distinta de 00:00: pared 13:45:30 y pared 23:30:00 (en UTC-6 el segundo cruza el dia)
            foreach (var (h, mi, s) in new[] { (13, 45, 30), (23, 30, 0) })
                foreach (var k in new[] { DateTimeKind.Local, DateTimeKind.Unspecified, DateTimeKind.Utc })
                {
                    var d = new DateTime(2026, 3, 5, h, mi, s, k);
                    kinds.Add(FechaCaso($"{etq}: {h:00}:{mi:00}:{s:00} Kind={k}", tabla, clave, col, d));
                }
        }
        Escribir("11-fechas-escritas.json", new JsonObject
        {
            ["cabecera"] = Cabecera("11-fechas-escritas", "SINTETICAS ZZF- (Update de columnas de fecha de una Conversacion y un Arreglo)",
                "Puntos (2) y (e). Cada caso: Update de la columna, Retrieve de la misma columna. 'enviado' y 'leido' llevan iso, Kind y ticks; 'delta' = leido - enviado en ticks (1 tick = 100 ns).",
                "UL = fechahora usuario local (sanic_fechafinalizacion, sanic_fechacongelamiento); DO = solo fecha (sanic_fechafin). El proceso corre en America/Managua (UTC-6): Local != UTC, asi que el caso Local es discriminante.",
                "El Kind de lo ESCRITO se mide con Update; un Create serializa la fecha igual (mismo DataContract) y no se repite para no consumir autonumerico."),
            ["contenido"] = new JsonObject { ["precision"] = precision, ["rango"] = rango, ["kind_escrito_e"] = kinds },
        });
    }
}
