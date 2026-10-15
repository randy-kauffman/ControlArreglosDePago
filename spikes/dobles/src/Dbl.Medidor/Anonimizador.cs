using System.Text.RegularExpressions;

namespace Dbl.Medidor;

/// <summary>
/// Anonimizacion DETERMINISTA y SOLO EN MEMORIA: el mismo valor real da el mismo sintetico en todos los fixtures de la corrida
/// (las referencias cruzadas siguen coincidiendo). El mapeo no se guarda en ningun lado.
/// Regla de fondo: todo texto es sospechoso salvo los que empiezan con ZZF- (creados por el spike), los autonumericos del proyecto
/// y una lista corta de nombres de esquema. Lo que no se reconoce se reemplaza por un sintetico generico.
/// </summary>
public sealed class Anonimizador
{
    private readonly Dictionary<Guid, Guid> _guids = new();
    private readonly Dictionary<string, string> _mapa = new();
    private readonly Dictionary<string, int> _contadores = new();
    private int _siguienteGuid = 1;

    public const string UrlOrg = "https://org36e60d9d.crm.dynamics.com";
    private static readonly string[] _huespedesOrg = { "org36e60d9d.crm.dynamics.com", "org36e60d9d.api.crm.dynamics.com", "org36e60d9d" };

    private static readonly Regex ReGuid = new(@"\b[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}\b", RegexOptions.Compiled);
    private static readonly Regex ReCorreo = new(@"[A-Za-z0-9._%+\-']+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}", RegexOptions.Compiled);
    private static readonly Regex ReToken = new(@"<[^<>\s]+>", RegexOptions.Compiled);
    private static readonly Regex ReAutonumerico = new(@"^(CONV|ARR|GEN)-[0-9]{6,15}$", RegexOptions.Compiled);

    // Nombres de columna cuyo texto es un nombre de esquema/tecnico, no un dato de nadie.
    private static readonly HashSet<string> Mantener = new(StringComparer.OrdinalIgnoreCase)
    {
        "logicalname", "schemaname", "objecttypecode", "regardingobjecttypecode", "entityname", "primaryentityname",
    };

    public int Reemplazos { get; private set; }

    private string Numerar(string clase, string original, Func<int, string> forma)
    {
        var clave = clase + "\u0001" + original;
        if (_mapa.TryGetValue(clave, out var ya)) return ya;
        _contadores[clase] = _contadores.GetValueOrDefault(clase) + 1;
        var nuevo = forma(_contadores[clase]);
        _mapa[clave] = nuevo;
        Reemplazos++;
        return nuevo;
    }

    // ------------------------------------------------------------------ GUID
    private static int Categoria(string pista)
    {
        var p = (pista ?? "").ToLowerInvariant();
        if (p.Contains("systemuser") || p.Contains("user") || p == "createdby" || p == "modifiedby") return 1;
        if (p.Contains("team")) return 2;
        if (p.Contains("businessunit")) return 3;
        if (p.Contains("queue")) return 4;
        if (p.Contains("organization")) return 5;
        return 9;
    }

    /// <summary>GUID sintetico estable. La categoria (usuario, team, BU, cola, organizacion, otro) va en el primer bloque, solo para legibilidad.</summary>
    public Guid Guid(Guid real, string pista)
    {
        if (real == System.Guid.Empty) return real;
        if (_guids.TryGetValue(real, out var s)) return s;
        s = new Guid($"0fff000{Categoria(pista)}-0000-4000-8000-{_siguienteGuid++:x12}");
        _guids[real] = s;
        Reemplazos++;
        return s;
    }

    // ------------------------------------------------------------------ Texto de una columna
    public string Texto(string atributo, string v, string entidad)
    {
        if (string.IsNullOrEmpty(v)) return v;
        var a = (atributo ?? "").ToLowerInvariant();
        if (Mantener.Contains(a)) return v;
        if (v.StartsWith("ZZF-", StringComparison.Ordinal) || v.StartsWith("ZZF ", StringComparison.Ordinal) || v == "SYSTEM" || ReAutonumerico.IsMatch(v)) return v;

        if (a is "messageid" or "inreplyto" or "references") return Mensajes(a, v);
        if (System.Guid.TryParse(v, out var g)) return Guid(g, a).ToString();

        switch (a)
        {
            case "subject":
                return Numerar("asunto", v, n => $"ZZF-asunto {n}");
            case "description" or "body" or "bodyhtml" or "safedescription":
                return Numerar("cuerpo", v, n => $"ZZF-cuerpo {n}");
            case "fullname" or "yomifullname":
                return Numerar("usuario", v, n => $"ZZF Usuario {n}");
            case "firstname" or "yomifirstname":
                return Numerar("nombre", v, n => $"ZZFNombre{n}");
            case "lastname" or "yomilastname":
                return Numerar("apellido", v, n => $"ZZFApellido{n}");
            case "middlename" or "yomimiddlename" or "nickname" or "salutation" or "title" or "jobtitle":
                return Numerar("otronombre", v, n => $"ZZF-{a} {n}");
            case "name":
                return NombreDeEntidad(entidad, v);
        }
        if (v.Contains('@'))
        {
            var r = Correos(v);
            if (r != null) return r;
        }
        return Numerar("generico:" + a, v, n => $"ZZF-texto-{a} {n}");
    }

    /// <summary>Nombre visible de una entidad (columna name o Name de un EntityReference), segun el tipo de fila.</summary>
    public string NombreDeEntidad(string logicalName, string v)
    {
        if (string.IsNullOrEmpty(v)) return v;
        if (v.StartsWith("ZZF-", StringComparison.Ordinal) || v.StartsWith("ZZF ", StringComparison.Ordinal) || v == "SYSTEM" || ReAutonumerico.IsMatch(v)) return v;
        return (logicalName ?? "").ToLowerInvariant() switch
        {
            "systemuser" => Numerar("usuario", v, n => $"ZZF Usuario {n}"),
            "team" => Numerar("team", v, n => $"ZZF Team {n}"),
            "businessunit" => Numerar("bu", v, n => $"ZZF BU {n}"),
            "queue" => Numerar("cola", v, n => $"ZZF Cola {n}"),
            "organization" => Numerar("org", v, n => $"ZZF Org {n}"),
            _ => Numerar("generico:name:" + logicalName, v, n => $"ZZF-texto-{logicalName} {n}"),
        };
    }

    // ------------------------------------------------------------------ correos
    /// <summary>Reemplaza cada direccion por zzf-buzonN@example.com. Devuelve null si el texto tenia algo mas que direcciones y separadores (nombres propios).</summary>
    private string? Correos(string v)
    {
        var res = ReCorreo.Replace(v, m => MapearCorreo(m.Value));
        var resto = ReCorreo.Replace(v, "");
        resto = Regex.Replace(resto, @"[;,\s<>""']", "");
        return resto.Length == 0 ? res : null;
    }

    /// <summary>Los correos sinteticos que puso el propio spike (zzf-...@example.com) se conservan; el resto se numera.</summary>
    private string MapearCorreo(string correo)
    {
        var l = correo.ToLowerInvariant();
        if (l.StartsWith("zzf-", StringComparison.Ordinal) && l.EndsWith("@example.com", StringComparison.Ordinal)) return correo;
        return Numerar("correo", l, n => $"zzf-buzon{n}@example.com");
    }

    /// <summary>messageid / inreplyto / references: conserva la forma &lt;...@...&gt; con dominio example.com.</summary>
    private string Mensajes(string atributo, string v)
    {
        var tokens = ReToken.Matches(v);
        if (tokens.Count > 0)
        {
            var res = ReToken.Replace(v, m => m.Value.StartsWith("<zzf-", StringComparison.OrdinalIgnoreCase) && m.Value.EndsWith("@example.com>", StringComparison.OrdinalIgnoreCase)
                ? m.Value : Numerar("msgid", m.Value, n => $"<zzf-msg{n}@example.com>"));
            var resto = Regex.Replace(ReToken.Replace(v, ""), @"\s", "");
            if (resto.Length == 0) return res;
            return Numerar("generico:" + atributo, v, n => $"<zzf-msg-texto{n}@example.com>");
        }
        return Numerar("msgid-sin-corchetes", v, n => $"zzf-msg{n}@example.com");
    }

    // ------------------------------------------------------------------ mensajes de error y cookies (texto libre con GUID/correos/URL)
    public string Mensaje(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        s = ReGuid.Replace(s, m => Guid(System.Guid.Parse(m.Value), "x").ToString());
        s = ReCorreo.Replace(s, m => MapearCorreo(m.Value));
        foreach (var h in _huespedesOrg) s = s.Replace(h, "zzf-org.example.com", StringComparison.OrdinalIgnoreCase);
        return s;
    }
}
