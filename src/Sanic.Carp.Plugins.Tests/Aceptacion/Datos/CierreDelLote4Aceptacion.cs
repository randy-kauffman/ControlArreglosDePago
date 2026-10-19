using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary>
    /// Cierre del lote 4 (2026-10-19; diseno/03 4.2 puntos 5 y 6, y 4.5 "Cierre del lote 4"): la versión de fila vacía de
    /// <c>IConversaciones.Actualizar</c> y la paginación con <c>MoreRecords</c> y sin cookie. Las de <c>ColumnasEnviadas</c> están en
    /// <c>Aceptacion/Dobles/ColumnasEnviadasAceptacion.cs</c> y la de la jefatura duplicada, en <c>PA_CFG_16</c>.
    /// </summary>
    public class CierreDelLote4Aceptacion
    {
        // ---- Versión de fila vacía (4.2 punto 5) ----

        [Fact]
        public void PA_CL4_VER_01_Actualizar_ConUnaVersionVaciaODeSoloEspacios_LanzaArgumentException_SinEscribir()
        {
            // Decisión: null es la ÚNICA forma de decir "sin versión"; "" no se degrada en silencio a un Update común (perdería la concurrencia optimista, DC-15).
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarConversacion(almacen);
            var repo = new ConversacionesDataverse(almacen);
            var cambios = new Dictionary<string, object> { { "sanic_asunto", "ZZF-PRUEBA nuevo" } };

            var vacia = Assert.Throws<ArgumentException>(() => repo.Actualizar(id, cambios, string.Empty));
            var espacios = Assert.Throws<ArgumentException>(() => repo.Actualizar(id, cambios, "   "));

            Assert.Equal("versionDeFila", vacia.ParamName);
            Assert.Equal("versionDeFila", espacios.ParamName);
            Assert.Empty(almacen.Escrituras);
            Assert.NotEqual("ZZF-PRUEBA nuevo", almacen.Leer(Conversacion, id).GetAttributeValue<string>("sanic_asunto"));
        }

        // ---- Paginación sin cookie (4.2 punto 6) ----

        /// <summary>Un <c>IOrganizationService</c> mínimo que sirve páginas escritas en la prueba y anota lo que le llega. Más de 10 llamadas ⇒ lanza, para que un bucle falle en vez de colgar.</summary>
        private sealed class ServicioPaginado : IOrganizationService
        {
            public sealed class Pedido { public int Pagina; public string Cookie; public int Cantidad; public int? Tope; }

            private readonly Func<int, EntityCollection> _pagina;
            public readonly List<Pedido> Pedidos = new List<Pedido>();

            public ServicioPaginado(Func<int, EntityCollection> pagina)
            {
                _pagina = pagina;
            }

            public EntityCollection RetrieveMultiple(QueryBase query)
            {
                var consulta = (QueryExpression)query;
                Pedidos.Add(new Pedido { Pagina = consulta.PageInfo.PageNumber, Cookie = consulta.PageInfo.PagingCookie, Cantidad = consulta.PageInfo.Count, Tope = consulta.TopCount });
                if (Pedidos.Count > 10) throw new InvalidOperationException("Más de 10 páginas pedidas: el recorrido no termina.");
                return _pagina(consulta.PageInfo.PageNumber);
            }

            public Guid Create(Entity entity) { throw new NotSupportedException(); }
            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) { throw new NotSupportedException(); }
            public void Update(Entity entity) { throw new NotSupportedException(); }
            public void Delete(string entityName, Guid id) { throw new NotSupportedException(); }
            public OrganizationResponse Execute(OrganizationRequest request) { throw new NotSupportedException(); }
            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { throw new NotSupportedException(); }
            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { throw new NotSupportedException(); }
        }

        private static EntityCollection Pagina(bool hayMas, string cookie, params Guid[] ids)
        {
            var coleccion = new EntityCollection { MoreRecords = hayMas, PagingCookie = cookie };
            foreach (var id in ids) coleccion.Entities.Add(new Entity("zzf_fila", id));
            return coleccion;
        }

        [Fact]
        public void PA_CL4_PAG_01_MoreRecordsConCookieVaciaONula_NoEsUnError_SeSigueConElNumeroDePagina()
        {
            // VERDE A PROPÓSITO: el código ya hace esto. Fija la decisión de NO lanzar InvalidOperationException (diseno/03 4.2 punto 6a): RESULTADO.md fila 10
            // midió que la plataforma decide la página por PageNumber y que una cookie ausente o "" devuelve la página correcta. Mata "lanzar si falta la cookie".
            var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
            var servicio = new ServicioPaginado(pagina =>
                pagina == 1 ? Pagina(true, string.Empty, a, b) :
                pagina == 2 ? Pagina(true, null, c) :
                Pagina(false, string.Empty, d));

            var filas = AyudaDeConsultas.Todas(servicio, new QueryExpression("zzf_fila") { ColumnSet = new ColumnSet(false) });

            Assert.Equal(new[] { a, b, c, d }, filas.Select(f => f.Id).ToArray());
            Assert.Equal(new[] { 1, 2, 3 }, servicio.Pedidos.Select(p => p.Pagina).ToArray());
            Assert.All(servicio.Pedidos.Skip(1), p => Assert.True(string.IsNullOrEmpty(p.Cookie)));
            Assert.All(servicio.Pedidos, p => Assert.Equal(AyudaDeConsultas.MaximoPorPagina, p.Cantidad));
            Assert.All(servicio.Pedidos, p => Assert.Null(p.Tope));
        }

        [Fact]
        public void PA_CL4_PAG_02_UnaCookieNoVaciaSeReenviaEnLaPaginaSiguiente()
        {
            // VERDE A PROPÓSITO: ya lo hace; fija que la cookie que la plataforma devuelve es la que se manda (4.2 punto 6).
            var a = Guid.NewGuid(); var b = Guid.NewGuid();
            const string cookie = "<cookie page=\"1\"><zzf_filaid last=\"{x}\" first=\"{y}\" /></cookie>";
            var servicio = new ServicioPaginado(pagina => pagina == 1 ? Pagina(true, cookie, a) : Pagina(false, string.Empty, b));

            var filas = AyudaDeConsultas.Todas(servicio, new QueryExpression("zzf_fila") { ColumnSet = new ColumnSet(false) });

            Assert.Equal(new[] { a, b }, filas.Select(f => f.Id).ToArray());
            Assert.Null(servicio.Pedidos[0].Cookie);
            Assert.Equal(cookie, servicio.Pedidos[1].Cookie);
        }

        [Fact]
        public void PA_CL4_PAG_03_UnaPaginaSinFilasCortaElRecorridoAunqueMoreRecordsSeaVerdadero_SinLanzar()
        {
            // VERDE A PROPÓSITO: ya lo hace. No hay nada que pedir en una página vacía y seguir sería un bucle (4.2 punto 6a). El servicio lanza tras 10 llamadas.
            var a = Guid.NewGuid();
            var servicio = new ServicioPaginado(pagina => pagina == 1 ? Pagina(true, string.Empty, a) : Pagina(true, string.Empty));

            var filas = AyudaDeConsultas.Todas(servicio, new QueryExpression("zzf_fila") { ColumnSet = new ColumnSet(false) });

            Assert.Equal(new[] { a }, filas.Select(f => f.Id).ToArray());
            Assert.Equal(2, servicio.Pedidos.Count);
        }
    }
}
