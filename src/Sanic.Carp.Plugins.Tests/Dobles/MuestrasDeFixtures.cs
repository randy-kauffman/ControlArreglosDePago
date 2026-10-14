using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sanic.Carp.Plugins.Tests.Apoyo;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>Lo observado en una columna de una fila real de Dev: el tipo .NET y, si es un lookup, las tablas a las que apuntó.</summary>
    internal sealed class MuestraDeColumna
    {
        public string TipoNet { get; set; }
        public HashSet<string> Destinos { get; } = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Las columnas de cada tabla tal como las trajeron las filas REALES de <c>Fixtures/Dataverse</c> (Dev, 2026-10-14). Es la fuente de las columnas de plataforma de una tabla propia
    /// y de los nombres de las tablas nativas con muestra (diseno/03 4.5, fila "Esquema"): nada de esto está escrito a mano en el doble.
    /// </summary>
    internal sealed class MuestrasDeFixtures
    {
        private const string TipoEntidad = "Microsoft.Xrm.Sdk.Entity";
        private const string TipoAlias = "Microsoft.Xrm.Sdk.AliasedValue";
        private const string TipoReferencia = "Microsoft.Xrm.Sdk.EntityReference";

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, MuestraDeColumna>> Tablas { get; private set; }

        public static MuestrasDeFixtures SinMuestras()
        {
            return Leer(new JsonElement[0]);
        }

        /// <summary>Todos los <c>*.json</c> de <c>Fixtures/Dataverse</c> de la salida de la compilación.</summary>
        public static MuestrasDeFixtures DeLaCarpeta()
        {
            var carpeta = LectorDeFixtures.Carpeta;
            if (!Directory.Exists(carpeta)) throw new DirectoryNotFoundException("No existe la carpeta de fixtures '" + carpeta + "': el esquema del doble sale de las filas reales de Dev.");
            var raices = new List<JsonElement>();
            foreach (var archivo in Directory.GetFiles(carpeta, "*.json").OrderBy(a => a, StringComparer.Ordinal))
            {
                using (var documento = JsonDocument.Parse(File.ReadAllBytes(archivo))) raices.Add(documento.RootElement.Clone());
            }
            return Leer(raices);
        }

        /// <summary>
        /// Recorre cada documento y anota las columnas de cada <c>Entity</c> que encuentra. No baja dentro de los atributos de una entidad: las partes de un correo
        /// (<c>activityparty</c>) son valores de una columna, no una tabla que se escriba. Una columna <c>AliasedValue</c> (un join o un agregado) es de la consulta, no de la tabla.
        /// </summary>
        public static MuestrasDeFixtures Leer(IEnumerable<JsonElement> raices)
        {
            if (raices == null) throw new ArgumentNullException(nameof(raices));
            var tablas = new Dictionary<string, Dictionary<string, MuestraDeColumna>>(StringComparer.Ordinal);
            foreach (var raiz in raices) Recorrer(raiz, tablas);
            return new MuestrasDeFixtures
            {
                Tablas = tablas.ToDictionary(p => p.Key, p => (IReadOnlyDictionary<string, MuestraDeColumna>)p.Value, StringComparer.Ordinal)
            };
        }

        private static void Recorrer(JsonElement nodo, Dictionary<string, Dictionary<string, MuestraDeColumna>> tablas)
        {
            if (nodo.ValueKind == JsonValueKind.Array)
            {
                foreach (var elemento in nodo.EnumerateArray()) Recorrer(elemento, tablas);
                return;
            }
            if (nodo.ValueKind != JsonValueKind.Object) return;

            JsonElement tipo;
            if (nodo.TryGetProperty("tipo", out tipo) && tipo.ValueKind == JsonValueKind.String && tipo.GetString() == TipoEntidad)
            {
                Anotar(nodo, tablas);
                return;
            }
            foreach (var propiedad in nodo.EnumerateObject()) Recorrer(propiedad.Value, tablas);
        }

        private static void Anotar(JsonElement entidad, Dictionary<string, Dictionary<string, MuestraDeColumna>> tablas)
        {
            var nombre = entidad.GetProperty("LogicalName").GetString();
            Dictionary<string, MuestraDeColumna> columnas;
            if (!tablas.TryGetValue(nombre, out columnas)) tablas[nombre] = columnas = new Dictionary<string, MuestraDeColumna>(StringComparer.Ordinal);
            foreach (var atributo in entidad.GetProperty("atributos").EnumerateObject())
            {
                var tipo = atributo.Value.GetProperty("tipo").GetString();
                if (tipo == TipoAlias) continue;
                MuestraDeColumna muestra;
                if (!columnas.TryGetValue(atributo.Name, out muestra)) columnas[atributo.Name] = muestra = new MuestraDeColumna { TipoNet = tipo };
                if (tipo == TipoReferencia) muestra.Destinos.Add(atributo.Value.GetProperty("valor").GetProperty("logicalName").GetString());
            }
        }
    }
}
