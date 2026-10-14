using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using System.Text.Json;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Sanic.Carp.Plugins.Tests.Dobles;

namespace Sanic.Carp.Plugins.Tests.Apoyo
{
    /// <summary>
    /// Un archivo de <c>Fixtures/Dataverse/*.json</c> (respuestas REALES de Dev exportadas el 2026-10-14, diseno/03 4.5). Reconstruye
    /// el SDK a partir del JSON, sin "arreglar" nada de lo que llegó: ver <see cref="LectorDeFixtures"/> para las reglas.
    /// Cada método recibe la RUTA dentro de <c>contenido</c> como nombres de propiedad o índices de arreglo escritos como texto
    /// (<c>Entidad("retrieve_columnset_true_entrante", "0")</c>). Una ruta que no existe lanza <see cref="KeyNotFoundException"/> con la ruta completa.
    /// </summary>
    internal sealed class FixtureDeDataverse
    {
        private readonly string _archivo;
        private readonly JsonElement _raiz;

        internal FixtureDeDataverse(string archivo, JsonElement raiz)
        {
            _archivo = archivo;
            _raiz = raiz;
        }

        public string Archivo { get { return _archivo; } }

        /// <summary>El documento completo (con <c>cabecera</c> y <c>contenido</c>).</summary>
        public JsonElement Raiz { get { return _raiz; } }

        /// <summary>El nodo JSON de la ruta (desde <c>contenido</c>).</summary>
        public JsonElement Nodo(params string[] ruta)
        {
            JsonElement actual;
            if (!_raiz.TryGetProperty("contenido", out actual)) throw new KeyNotFoundException(_archivo + ": el documento no trae 'contenido'.");
            var recorrido = new List<string>();
            foreach (var paso in ruta)
            {
                recorrido.Add(paso);
                JsonElement siguiente;
                if (actual.ValueKind == JsonValueKind.Object && actual.TryGetProperty(paso, out siguiente))
                {
                    actual = siguiente;
                }
                else if (actual.ValueKind == JsonValueKind.Array && int.TryParse(paso, NumberStyles.None, CultureInfo.InvariantCulture, out var indice) && indice < actual.GetArrayLength())
                {
                    actual = actual[indice];
                }
                else
                {
                    throw new KeyNotFoundException(_archivo + ": no existe la ruta '" + string.Join("/", recorrido) + "' (de '" + string.Join("/", ruta) + "').");
                }
            }
            return actual;
        }

        /// <summary>El nodo es una <c>Microsoft.Xrm.Sdk.Entity</c>.</summary>
        public Entity Entidad(params string[] ruta)
        {
            return LectorDeFixtures.Entidad(Nodo(ruta));
        }

        /// <summary>El nodo es un arreglo de <c>Entity</c>.</summary>
        public IReadOnlyList<Entity> Entidades(params string[] ruta)
        {
            var nodo = Nodo(ruta);
            var entidades = new List<Entity>();
            foreach (var elemento in nodo.EnumerateArray()) entidades.Add(LectorDeFixtures.Entidad(elemento));
            return entidades;
        }

        /// <summary>El nodo es una <c>EntityCollection</c> (con <c>Entities</c>).</summary>
        public EntityCollection Coleccion(params string[] ruta)
        {
            return LectorDeFixtures.Coleccion(Nodo(ruta));
        }

        /// <summary>El nodo es un caso de falla (tiene <c>falla.fault</c>): la <c>FaultException&lt;OrganizationServiceFault&gt;</c> que llegó.</summary>
        public FaultException<OrganizationServiceFault> Falla(params string[] ruta)
        {
            return LectorDeFixtures.Falla(Nodo(ruta));
        }

        /// <summary>El nodo es una <c>OrganizationResponse</c> (<c>WhoAmIResponse</c> o <c>UpdateResponse</c>).</summary>
        public OrganizationResponse Respuesta(params string[] ruta)
        {
            return LectorDeFixtures.Respuesta(Nodo(ruta));
        }
    }

    /// <summary>
    /// Lector de fixtures (diseno/03 4.5, <c>Apoyo/</c>): reconstruye <see cref="Entity"/>, <see cref="EntityCollection"/>, respuestas y
    /// fallas del SDK a partir del JSON de <c>spikes/fixtures/salida</c> copiado a <c>Fixtures/Dataverse</c>. Es infraestructura de prueba con
    /// su propia aceptación (<c>LectorDeFixturesAceptacion</c>). Reglas:
    /// <list type="bullet">
    /// <item>El tipo .NET de cada valor es EXACTAMENTE el de <c>tipo</c> (<c>System.Guid</c>, <c>System.DateTime</c>, <c>System.Decimal</c>,
    /// <c>OptionSetValue</c>, <c>EntityReference</c>, <c>EntityCollection</c>, <c>AliasedValue</c>, <c>BooleanManagedProperty</c>, …). Un tipo que no
    /// conoce lanza <see cref="NotSupportedException"/> con su nombre: nunca lo convierte en texto.</item>
    /// <item><c>System.DateTime</c>: el <c>Kind</c> es el de <c>kind</c> (<c>Utc</c> o <c>Unspecified</c>) y los ticks son los del texto.</item>
    /// <item><c>System.Decimal</c>: la ESCALA es la de los decimales del texto (<c>"1000.5000000000"</c> ⇒ escala 10). Sin pasar por <c>double</c>.</item>
    /// <item><c>EntityReference</c>: <c>Name</c> es el que trae, <c>null</c> si trae <c>null</c>.</item>
    /// <item><c>Entity</c>: <c>LogicalName</c>, <c>Id</c>, <c>RowVersion</c> (texto o <c>null</c>), <c>EntityState</c> nulo, <c>KeyAttributes</c> vacío, y SOLO
    /// los atributos del JSON: ningún atributo nulo de más ni de menos. <c>FormattedValues</c> también.</item>
    /// <item><c>EntityCollection</c>: <c>EntityName</c>, <c>MoreRecords</c>, <c>PagingCookie</c> (nulo si es nulo; vacío si es vacío), <c>TotalRecordCount</c>,
    /// <c>TotalRecordCountLimitExceeded</c>, <c>MinActiveRowVersion</c> y las <c>Entities</c>.</item>
    /// <item>Falla: <c>FaultException&lt;OrganizationServiceFault&gt;</c> con <c>Detail.ErrorCode</c> (el decimal del JSON), <c>Detail.Message</c>,
    /// <c>Detail.ErrorDetails</c> con el tipo de cada valor (<c>tipo_del_valor</c>), <c>Detail.Timestamp</c> (con su <c>Kind</c>) y la cadena de
    /// <c>InnerFault</c>; el mensaje de la excepción es <c>Detail.Message</c> y NO tiene <c>InnerException</c>.</item>
    /// </list>
    /// </summary>
    internal static class LectorDeFixtures
    {
        /// <summary>Carpeta de los fixtures en la salida de la compilación (<c>Fixtures/Dataverse</c>).</summary>
        public static string Carpeta { get { return Path.Combine(System.AppContext.BaseDirectory, "Fixtures", "Dataverse"); } }

        /// <summary>Abre <c>Fixtures/Dataverse/&lt;archivo&gt;</c>. Si no existe lanza <see cref="System.IO.FileNotFoundException"/> con la ruta.</summary>
        public static FixtureDeDataverse Abrir(string archivo)
        {
            var ruta = Path.Combine(Carpeta, archivo);
            if (!File.Exists(ruta)) throw new FileNotFoundException("No existe el fixture '" + archivo + "' en '" + Carpeta + "'.", ruta);
            using (var documento = JsonDocument.Parse(File.ReadAllBytes(ruta)))
            {
                return new FixtureDeDataverse(archivo, documento.RootElement.Clone());
            }
        }

        /// <summary>Convierte un nodo <c>{ "tipo": …, "valor": …[, "kind": …] }</c> en el valor SDK.</summary>
        public static object Valor(JsonElement nodo)
        {
            var tipo = nodo.GetProperty("tipo").GetString();
            var valor = nodo.GetProperty("valor");
            switch (tipo)
            {
                case "System.Guid": return Guid.Parse(valor.GetString());
                case "System.String": return valor.GetString();
                case "System.Int32": return valor.GetInt32();
                case "System.Boolean": return valor.GetBoolean();
                case "System.Decimal":
                    // decimal.Parse conserva los ceros finales ("1000.5000000000" ⇒ escala 10); nada pasa por double.
                    return decimal.Parse(valor.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                case "System.DateTime": return Fecha(valor.GetString(), nodo.GetProperty("kind").GetString());
                case "Microsoft.Xrm.Sdk.OptionSetValue": return new OptionSetValue(valor.GetInt32());
                case "Microsoft.Xrm.Sdk.EntityReference": return Referencia(valor);
                case "Microsoft.Xrm.Sdk.EntityCollection": return Coleccion(valor);
                case "Microsoft.Xrm.Sdk.AliasedValue":
                    return new AliasedValue(valor.GetProperty("entityLogicalName").GetString(), valor.GetProperty("attributeLogicalName").GetString(), Valor(valor.GetProperty("valor")));
                case "Microsoft.Xrm.Sdk.BooleanManagedProperty":
                    return Copias.Administrada(valor.GetProperty("Value").GetBoolean(), valor.GetProperty("CanBeChanged").GetBoolean(),
                        valor.GetProperty("ManagedPropertyLogicalName").GetString());
                default:
                    throw new NotSupportedException("El lector de fixtures no conoce el tipo '" + tipo + "'; hay que agregarlo al lector y a su aceptación antes de usar el fixture.");
            }
        }

        private static DateTime Fecha(string texto, string kind)
        {
            var sinZona = texto.EndsWith("Z", StringComparison.Ordinal) ? texto.Substring(0, texto.Length - 1) : texto;
            var fecha = DateTime.ParseExact(sinZona, new[] { "yyyy-MM-dd'T'HH:mm:ss.fffffff", "yyyy-MM-dd'T'HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None);
            // El Kind es el del fixture; el texto no se convierte de zona (los ticks son los del texto).
            return DateTime.SpecifyKind(fecha, (DateTimeKind)Enum.Parse(typeof(DateTimeKind), kind));
        }

        private static EntityReference Referencia(JsonElement valor)
        {
            var nombre = valor.GetProperty("name");
            return new EntityReference(valor.GetProperty("logicalName").GetString(), Guid.Parse(valor.GetProperty("id").GetString()))
            {
                Name = nombre.ValueKind == JsonValueKind.Null ? null : nombre.GetString()
            };
        }

        public static Entity Entidad(JsonElement nodo)
        {
            var entidad = new Entity(nodo.GetProperty("LogicalName").GetString(), Guid.Parse(nodo.GetProperty("Id").GetString()));
            var version = nodo.GetProperty("RowVersion");
            entidad.RowVersion = version.ValueKind == JsonValueKind.Null ? null : version.GetString();
            // Solo los atributos del JSON: una columna nula nunca llega (la plataforma la deja ausente).
            foreach (var atributo in nodo.GetProperty("atributos").EnumerateObject()) entidad.Attributes[atributo.Name] = Valor(atributo.Value);
            foreach (var formateado in nodo.GetProperty("FormattedValues").EnumerateObject()) entidad.FormattedValues[formateado.Name] = formateado.Value.GetString();
            return entidad;
        }

        public static EntityCollection Coleccion(JsonElement nodo)
        {
            var cookie = nodo.GetProperty("PagingCookie");
            var coleccion = new EntityCollection
            {
                EntityName = nodo.GetProperty("EntityName").GetString(),
                MoreRecords = nodo.GetProperty("MoreRecords").GetBoolean(),
                PagingCookie = cookie.ValueKind == JsonValueKind.Null ? null : cookie.GetString(),
                TotalRecordCount = nodo.GetProperty("TotalRecordCount").GetInt32(),
                TotalRecordCountLimitExceeded = nodo.GetProperty("TotalRecordCountLimitExceeded").GetBoolean(),
                MinActiveRowVersion = nodo.GetProperty("MinActiveRowVersion").GetString()
            };
            foreach (var elemento in nodo.GetProperty("Entities").EnumerateArray()) coleccion.Entities.Add(Entidad(elemento));
            return coleccion;
        }

        /// <summary>El nodo es un <c>OrganizationServiceFault</c> (el objeto <c>fault</c>).</summary>
        public static OrganizationServiceFault Fault(JsonElement nodo)
        {
            var timestamp = nodo.GetProperty("Timestamp");
            var interna = nodo.GetProperty("InnerFault");
            var fault = new OrganizationServiceFault
            {
                ErrorCode = nodo.GetProperty("ErrorCode_decimal").GetInt32(),
                Message = nodo.GetProperty("Message").GetString(),
                Timestamp = Fecha(timestamp.GetProperty("iso").GetString(), timestamp.GetProperty("kind").GetString()),
                InnerFault = interna.ValueKind == JsonValueKind.Null ? null : Fault(interna)
            };
            foreach (var detalle in nodo.GetProperty("ErrorDetails").EnumerateObject())
                fault.ErrorDetails[detalle.Name] = DetalleDeFalla(detalle.Value.GetProperty("tipo_del_valor").GetString(), detalle.Value.GetProperty("valor").GetString());
            return fault;
        }

        private static object DetalleDeFalla(string tipo, string texto)
        {
            switch (tipo)
            {
                case "System.String": return texto;
                case "System.Guid": return Guid.Parse(texto);
                case "System.Int32": return int.Parse(texto, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                default: throw new NotSupportedException("El lector de fixtures no conoce el tipo '" + tipo + "' en los detalles de una falla.");
            }
        }

        /// <summary>El nodo es un caso de falla (con <c>falla.fault</c>).</summary>
        public static FaultException<OrganizationServiceFault> Falla(JsonElement nodo)
        {
            var detalle = Fault(nodo.GetProperty("falla").GetProperty("fault"));
            // Directa, sin envoltura: la cadena real es de largo 1 y el mensaje de la excepción es el del detalle.
            return new FaultException<OrganizationServiceFault>(detalle, new FaultReason(detalle.Message));
        }

        public static OrganizationResponse Respuesta(JsonElement nodo)
        {
            var tipo = nodo.GetProperty("tipo").GetString();
            OrganizationResponse respuesta;
            switch (tipo)
            {
                case "Microsoft.Crm.Sdk.Messages.WhoAmIResponse": respuesta = new WhoAmIResponse(); break;
                case "Microsoft.Xrm.Sdk.Messages.UpdateResponse": respuesta = new UpdateResponse(); break;
                default: throw new NotSupportedException("El lector de fixtures no conoce la respuesta '" + tipo + "'.");
            }
            respuesta.ResponseName = nodo.GetProperty("ResponseName").GetString();
            respuesta.Results = new ParameterCollection();
            foreach (var resultado in nodo.GetProperty("Results").EnumerateObject()) respuesta.Results[resultado.Name] = Valor(resultado.Value);
            return respuesta;
        }
    }
}
