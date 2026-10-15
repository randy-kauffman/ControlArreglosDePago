using System.Globalization;
using System.Reflection;
using System.ServiceModel;
using System.Text.Json.Nodes;
using Microsoft.Xrm.Sdk;

namespace Dbl.Medidor;

/// <summary>
/// Entity / OrganizationResponse / excepciones -> JSON legible conservando el TIPO .NET de cada valor.
/// Cada atributo: {"tipo": "<nombre completo del tipo>", "valor": ...} (DateTime agrega "kind").
/// </summary>
public sealed class Serializador
{
    private readonly Anonimizador _a;
    public readonly SortedSet<string> TiposNoModelados = new();
    public readonly SortedSet<string> TiposVistos = new();

    public Serializador(Anonimizador a) => _a = a;
    public Anonimizador Anon => _a;

    private static JsonObject W(object v, JsonNode? valor) => new() { ["tipo"] = v.GetType().FullName, ["valor"] = valor };
    private static string Inv(IFormattable f) => f.ToString(null, CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ valores
    public JsonObject Valor(object? v, string atributo, string entidad)
    {
        if (v == null) return new JsonObject { ["tipo"] = null, ["valor"] = null };
        TiposVistos.Add(v.GetType().FullName ?? v.GetType().Name);
        switch (v)
        {
            case OptionSetValue o:
                return W(v, o.Value);
            case EntityReference er:
            {
                var d = new JsonObject
                {
                    ["logicalName"] = er.LogicalName,
                    ["id"] = _a.Guid(er.Id, er.LogicalName).ToString(),
                    ["name"] = er.Name == null ? null : _a.NombreDeEntidad(er.LogicalName, er.Name),
                };
                if (er.RowVersion != null) d["rowVersion"] = er.RowVersion;
                if (er.KeyAttributes.Count > 0) d["keyAttributes"] = KeyAttrs(er.KeyAttributes, er.LogicalName);
                return W(v, d);
            }
            case Money m:
                return W(v, Inv(m.Value));
            case decimal dec:
                return W(v, Inv(dec));
            case double db:
                return W(v, db.ToString("R", CultureInfo.InvariantCulture));
            case float fl:
                return W(v, fl.ToString("R", CultureInfo.InvariantCulture));
            case int i:
                return W(v, i);
            case long l:
                return W(v, l);
            case bool b:
                return W(v, b);
            case Guid g:
                return W(v, _a.Guid(g, atributo).ToString());
            case string s:
                return W(v, _a.Texto(atributo, s, entidad));
            case DateTime dt:
            {
                var o = W(v, dt.ToString("O", CultureInfo.InvariantCulture));
                o["kind"] = dt.Kind.ToString();
                return o;
            }
            case DateTimeOffset dto:
                return W(v, dto.ToString("O", CultureInfo.InvariantCulture));
            case AliasedValue av:
                return W(v, new JsonObject
                {
                    ["entityLogicalName"] = av.EntityLogicalName,
                    ["attributeLogicalName"] = av.AttributeLogicalName,
                    ["valor"] = Valor(av.Value, av.AttributeLogicalName, av.EntityLogicalName),
                });
            case BooleanManagedProperty bmp:
                return W(v, new JsonObject { ["Value"] = bmp.Value, ["CanBeChanged"] = bmp.CanBeChanged, ["ManagedPropertyLogicalName"] = bmp.ManagedPropertyLogicalName });
            case OptionSetValueCollection osc:
            {
                var arr = new JsonArray();
                foreach (var x in osc) arr.Add(Valor(x, atributo, entidad));
                return W(v, arr);
            }
            case EntityCollection ec:
                return W(v, Coleccion(ec));
            case Entity e:
                return W(v, Entidad(e));
            case byte[] bytes:
                return W(v, new JsonObject { ["longitud"] = bytes.Length });
            default:
                TiposNoModelados.Add(v.GetType().FullName ?? v.GetType().Name);
                return W(v, _a.Mensaje(v.ToString()));
        }
    }

    private JsonObject KeyAttrs(KeyAttributeCollection k, string entidad)
    {
        var o = new JsonObject();
        foreach (var kv in k.OrderBy(x => x.Key, StringComparer.Ordinal)) o[kv.Key] = Valor(kv.Value, kv.Key, entidad);
        return o;
    }

    // ------------------------------------------------------------------ filas
    public JsonObject Entidad(Entity e)
    {
        var attrs = new JsonObject();
        foreach (var k in e.Attributes.Keys.OrderBy(x => x, StringComparer.Ordinal)) attrs[k] = Valor(e.Attributes[k], k, e.LogicalName);

        var fv = new JsonObject();
        foreach (var kv in e.FormattedValues.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            // Etiquetas de choice/booleano y fechas formateadas son tecnicas; lo que viene de un lookup o de un texto se anonimiza.
            string txt = kv.Value;
            if (e.Attributes.TryGetValue(kv.Key, out var raw))
            {
                if (raw is EntityReference er) txt = er.Name == null ? txt : _a.NombreDeEntidad(er.LogicalName, kv.Value);
                else if (raw is string) txt = _a.Texto(kv.Key, kv.Value, e.LogicalName);
                else if (raw is AliasedValue av && av.Value is EntityReference er2) txt = _a.NombreDeEntidad(er2.LogicalName, kv.Value);
                else if (raw is AliasedValue av2 && av2.Value is string) txt = _a.Texto(av2.AttributeLogicalName, kv.Value, av2.EntityLogicalName);
            }
            fv[kv.Key] = txt;
        }

        return new JsonObject
        {
            ["tipo"] = e.GetType().FullName,
            ["LogicalName"] = e.LogicalName,
            ["Id"] = _a.Guid(e.Id, e.LogicalName).ToString(),
            ["RowVersion"] = e.RowVersion,
            ["KeyAttributes"] = KeyAttrs(e.KeyAttributes, e.LogicalName),
            ["EntityState"] = e.EntityState?.ToString(),
            ["atributos"] = attrs,
            ["FormattedValues"] = fv,
        };
    }

    public JsonObject Coleccion(EntityCollection ec)
    {
        var arr = new JsonArray();
        foreach (var e in ec.Entities) arr.Add(Entidad(e));
        return new JsonObject
        {
            ["tipo"] = ec.GetType().FullName,
            ["EntityName"] = ec.EntityName,
            ["MoreRecords"] = ec.MoreRecords,
            ["PagingCookie"] = ec.PagingCookie == null ? null : _a.Mensaje(ec.PagingCookie),
            ["TotalRecordCount"] = ec.TotalRecordCount,
            ["TotalRecordCountLimitExceeded"] = ec.TotalRecordCountLimitExceeded,
            ["MinActiveRowVersion"] = ec.MinActiveRowVersion,
            ["cantidad"] = ec.Entities.Count,
            ["Entities"] = arr,
        };
    }

    public JsonObject Respuesta(OrganizationResponse r)
    {
        var res = new JsonObject();
        foreach (var kv in r.Results.OrderBy(x => x.Key, StringComparer.Ordinal)) res[kv.Key] = Valor(kv.Value, kv.Key, "");
        return new JsonObject { ["tipo"] = r.GetType().FullName, ["ResponseName"] = r.ResponseName, ["Results"] = res };
    }

    // ------------------------------------------------------------------ fallas
    public JsonObject Falla(Exception ex)
    {
        var cadena = new JsonArray();
        FaultException<OrganizationServiceFault>? fault = null;
        int i = 0;
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            cadena.Add(new JsonObject
            {
                ["orden"] = i++,
                ["tipo"] = e.GetType().FullName,
                ["mensaje"] = _a.Mensaje(e.Message),
                ["HResult"] = e.HResult,
                ["propiedades_propias"] = Props(e),
            });
            if (fault == null && e is FaultException<OrganizationServiceFault> f) fault = f;
        }
        var o = new JsonObject
        {
            ["exterior"] = ex.GetType().FullName,
            ["cadena_de_tipos"] = new JsonArray(cadena.Select(c => (JsonNode?)JsonValue.Create(c!["tipo"]!.GetValue<string>())).ToArray()),
            ["cadena"] = cadena,
            ["el_exterior_es_FaultException_OrganizationServiceFault"] = ex is FaultException<OrganizationServiceFault>,
            ["hay_FaultException_OrganizationServiceFault_en_la_cadena"] = fault != null,
        };
        if (fault != null) o["fault"] = DetalleFault(fault.Detail);
        return o;
    }

    public JsonObject DetalleFault(OrganizationServiceFault d)
    {
        var det = new JsonObject();
        foreach (var kv in d.ErrorDetails.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            det[kv.Key] = new JsonObject
            {
                ["tipo_del_valor"] = kv.Value?.GetType().FullName,
                ["valor"] = kv.Value switch
                {
                    null => null,
                    string s => _a.Mensaje(s),
                    Guid g => _a.Guid(g, kv.Key).ToString(),
                    IFormattable f => Inv(f),
                    _ => _a.Mensaje(kv.Value.ToString()),
                },
            };
        }
        var o = new JsonObject
        {
            ["tipo"] = d.GetType().FullName,
            ["ErrorCode_decimal"] = d.ErrorCode,
            ["ErrorCode_hex"] = "0x" + unchecked((uint)d.ErrorCode).ToString("X8"),
            ["Message"] = _a.Mensaje(d.Message),
            ["ErrorDetails"] = det,
            ["Timestamp"] = new JsonObject { ["iso"] = d.Timestamp.ToString("O", CultureInfo.InvariantCulture), ["kind"] = d.Timestamp.Kind.ToString() },
            ["TraceText_longitud"] = d.TraceText?.Length,
            ["InnerFault"] = d.InnerFault == null ? null : DetalleFault(d.InnerFault),
        };
        return o;
    }

    private JsonObject Props(Exception e)
    {
        var o = new JsonObject();
        foreach (var p in e.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            if (p.DeclaringType == typeof(Exception) || p.GetIndexParameters().Length > 0) continue;
            if (p.Name is "StackTrace" or "TargetSite" or "InnerException" or "Data" or "Source" or "HelpLink" or "Message" or "Detail") continue;
            try
            {
                var v = p.GetValue(e);
                string? txt = v switch
                {
                    null => null,
                    string s2 => _a.Mensaje(s2),
                    Guid g => _a.Guid(g, p.Name).ToString(),
                    Enum en => en.ToString(),
                    bool or int or long or short or byte => v.ToString(),
                    _ => "(" + v.GetType().FullName + ") " + _a.Mensaje(v.ToString()),
                };
                o[p.Name] = txt;
            }
            catch (Exception) { o[p.Name] = "(no legible)"; }
        }
        return o;
    }
}
