using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text.Json;
using Microsoft.Xrm.Sdk;

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
        internal FixtureDeDataverse(string archivo, JsonElement raiz) { throw new NotImplementedException(); }

        public string Archivo { get { throw new NotImplementedException(); } }

        /// <summary>El documento completo (con <c>cabecera</c> y <c>contenido</c>).</summary>
        public JsonElement Raiz { get { throw new NotImplementedException(); } }

        /// <summary>El nodo JSON de la ruta (desde <c>contenido</c>).</summary>
        public JsonElement Nodo(params string[] ruta) { throw new NotImplementedException(); }

        /// <summary>El nodo es una <c>Microsoft.Xrm.Sdk.Entity</c>.</summary>
        public Entity Entidad(params string[] ruta) { throw new NotImplementedException(); }

        /// <summary>El nodo es un arreglo de <c>Entity</c>.</summary>
        public IReadOnlyList<Entity> Entidades(params string[] ruta) { throw new NotImplementedException(); }

        /// <summary>El nodo es una <c>EntityCollection</c> (con <c>Entities</c>).</summary>
        public EntityCollection Coleccion(params string[] ruta) { throw new NotImplementedException(); }

        /// <summary>El nodo es un caso de falla (tiene <c>falla.fault</c>): la <c>FaultException&lt;OrganizationServiceFault&gt;</c> que llegó.</summary>
        public FaultException<OrganizationServiceFault> Falla(params string[] ruta) { throw new NotImplementedException(); }

        /// <summary>El nodo es una <c>OrganizationResponse</c> (<c>WhoAmIResponse</c> o <c>UpdateResponse</c>).</summary>
        public OrganizationResponse Respuesta(params string[] ruta) { throw new NotImplementedException(); }
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
        public static string Carpeta { get { throw new NotImplementedException(); } }

        /// <summary>Abre <c>Fixtures/Dataverse/&lt;archivo&gt;</c>. Si no existe lanza <see cref="System.IO.FileNotFoundException"/> con la ruta.</summary>
        public static FixtureDeDataverse Abrir(string archivo) { throw new NotImplementedException(); }

        /// <summary>Convierte un nodo <c>{ "tipo": …, "valor": …[, "kind": …] }</c> en el valor SDK.</summary>
        public static object Valor(JsonElement nodo) { throw new NotImplementedException(); }

        public static Entity Entidad(JsonElement nodo) { throw new NotImplementedException(); }

        public static EntityCollection Coleccion(JsonElement nodo) { throw new NotImplementedException(); }

        /// <summary>El nodo es un <c>OrganizationServiceFault</c> (el objeto <c>fault</c>).</summary>
        public static OrganizationServiceFault Fault(JsonElement nodo) { throw new NotImplementedException(); }

        /// <summary>El nodo es un caso de falla (con <c>falla.fault</c>).</summary>
        public static FaultException<OrganizationServiceFault> Falla(JsonElement nodo) { throw new NotImplementedException(); }

        public static OrganizationResponse Respuesta(JsonElement nodo) { throw new NotImplementedException(); }
    }
}
