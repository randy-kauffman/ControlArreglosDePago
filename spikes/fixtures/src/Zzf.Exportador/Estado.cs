using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zzf.Exportador;

/// <summary>estado.json: SOLO GUIDs, conteos y banderas (la linea base la escribe Python; las filas creadas las registra este programa apenas existen).</summary>
public static class Estado
{
    public static string Ruta => Path.Combine(Directory.GetCurrentDirectory(), "estado.json");

    public static JsonObject Leer() => JsonNode.Parse(File.ReadAllText(Ruta))!.AsObject();

    public static void Registrar(string clave, string tabla, Guid id)
    {
        var e = Leer();
        var filas = e["filas"]?.AsArray() ?? new JsonArray();
        filas.Add(new JsonArray(clave, tabla, id.ToString()));
        e["filas"] = filas;
        File.WriteAllText(Ruta, e.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() }));
    }

    public static List<(string clave, string tabla, Guid id)> Filas()
    {
        var res = new List<(string, string, Guid)>();
        foreach (var f in Leer()["filas"]!.AsArray())
            res.Add((f![0]!.GetValue<string>(), f[1]!.GetValue<string>(), Guid.Parse(f[2]!.GetValue<string>())));
        return res;
    }
}
