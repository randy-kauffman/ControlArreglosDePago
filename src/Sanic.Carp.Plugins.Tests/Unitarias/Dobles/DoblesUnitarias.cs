using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Aceptacion.Dobles;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Unitarias.Dobles
{
    /// <summary>
    /// Decisiones de implementación de los dobles (lote 3) que la aceptación del arquitecto no mide: el diseño (03 4.5) las deja abiertas
    /// y se fijan acá para que no se pierdan. Cada una dice POR QUÉ.
    /// </summary>
    public class DoblesUnitarias
    {
        private static FabricaDeServiciosSimulada NuevaFabrica(out PipelineSimulado pipeline)
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            pipeline = new PipelineSimulado(fabrica);
            return fabrica;
        }

        private static OrganizationServiceEnMemoria ConFilasSinNumero()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_estado", 1), Zzf(Fila2, "zzf_estado", 1, "zzf_numero", 5));
            return almacen;
        }

        // ---- Condiciones de tres valores ----

        // REEMPLAZA a NotEqualSobreAusente_JuntoAUnaCondicionQueYaDescartaLaFila_NoLanza (vuelta 2, hallazgo 6: NotEqual lanza por la FORMA de la consulta, no por los datos).
        [Fact]
        public void NotEqualSobreAusente_AunConUnaCondicionDeOtraColumnaQueDescartaLaFila_LanzaPorLaForma()
        {
            // Antes no lanzaba porque otra condición del And descartaba la fila. Ahora una condición de OTRA columna no cuenta (diseno/03 4.5, "Paginación", punto e): lanza igual.
            var almacen = ConFilasSinNumero();
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 2);
            consulta.Criteria.AddCondition("zzf_numero", ConditionOperator.NotEqual, 3);

            var conFilas = Assert.Throws<InvalidOperationException>(() => almacen.RetrieveMultiple(consulta));
            var conLaTablaVacia = Assert.Throws<InvalidOperationException>(() => new OrganizationServiceEnMemoria().RetrieveMultiple(consulta));

            Assert.Contains("zzf_numero", conFilas.Message, StringComparison.Ordinal);
            Assert.Contains("zzf_numero", conLaTablaVacia.Message, StringComparison.Ordinal);
        }

        // REEMPLAZA a NotEqualSobreAusente_EnUnOrConOtraCondicionVerdadera_NoLanza_PeroSinNingunaVerdaderaSi: la primera mitad (el Or con una condición verdadera) ya no pasa.
        [Fact]
        public void NotEqualSobreAusente_EnUnOr_LanzaConOSinOtraCondicionVerdadera()
        {
            var almacen = ConFilasSinNumero();
            var verdadera = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            verdadera.Criteria.FilterOperator = LogicalOperator.Or;
            verdadera.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 1);
            verdadera.Criteria.AddCondition("zzf_numero", ConditionOperator.NotEqual, 3);
            var sinVerdadera = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            sinVerdadera.Criteria.FilterOperator = LogicalOperator.Or;
            sinVerdadera.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 2);
            sinVerdadera.Criteria.AddCondition("zzf_numero", ConditionOperator.NotEqual, 3);

            var conVerdadera = Assert.Throws<InvalidOperationException>(() => almacen.RetrieveMultiple(verdadera));
            var error = Assert.Throws<InvalidOperationException>(() => almacen.RetrieveMultiple(sinVerdadera));

            Assert.Contains("zzf_numero", conVerdadera.Message, StringComparison.Ordinal);
            Assert.Contains("zzf_numero", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void UnGuidEscritoComoTextoSeComparaContraUnaReferenciaOUnGuid_PeroUnTipoIncompatibleLanza()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_ref", new EntityReference("team", Team1), "zzf_numero", 3));
            QueryExpression Donde(string columna, object valor)
            {
                var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
                consulta.Criteria.AddCondition(columna, ConditionOperator.Equal, valor);
                return consulta;
            }

            Assert.Single(almacen.RetrieveMultiple(Donde("zzf_ref", Team1.ToString())).Entities);
            Assert.Single(almacen.RetrieveMultiple(Donde("zzf_numero", "3")).Entities);
            // Comparar un número contra un Guid no tiene sentido en la plataforma: un doble que devolviera "ninguna fila" ocultaría el error de la consulta.
            Assert.Throws<InvalidOperationException>(() => almacen.RetrieveMultiple(Donde("zzf_numero", Team1)));
        }

        [Fact]
        public void UnOperadorNoSoportadoLanzaAunqueLaTablaEsteVacia()
        {
            // Se valida la consulta entera antes de mirar las filas: si no, la prueba pasa en el puesto con la tabla vacía y falla en Dev.
            var almacen = new OrganizationServiceEnMemoria();
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("zzf_texto", ConditionOperator.Like, "a%");

            Assert.Throws<NotSupportedException>(() => almacen.RetrieveMultiple(consulta));
        }

        // ---- Cookie ----

        [Fact]
        public void Cookie_OrdenarPorElIdPrimarioNoDuplicaElHijo_YUnValorAusenteDejaElHijoConPrimeraYUltima()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1), Zzf(Fila2, "zzf_numero", 1));
            var porId = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            porId.AddOrder("zzf_filaid", OrderType.Ascending);
            var porNumero = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            porNumero.AddOrder("zzf_numero", OrderType.Ascending);

            var cookieDeId = XElement.Parse(almacen.RetrieveMultiple(porId).PagingCookie);
            var cookieDeNumero = XElement.Parse(almacen.RetrieveMultiple(porNumero).PagingCookie);

            Assert.Equal(new[] { "zzf_filaid" }, cookieDeId.Elements().Select(e => e.Name.LocalName).ToArray());
            var numero = cookieDeNumero.Element("zzf_numero");
            Assert.NotNull(numero.Attribute("first"));
            Assert.Equal(string.Empty, numero.Attribute("first").Value);
            Assert.Equal("1", numero.Attribute("last").Value);
        }

        // ---- Fallas ----

        [Fact]
        public void FallarEn_CuentaLaLlamadaAunqueUnStepDePreOperationLaCorteAntesDeAplicarla()
        {
            // La llamada se cuenta al recibirla; la falla ocurre al aplicar (después de PreOperation). Si un step corta la primera, la segunda ya es la N+1.
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, 2, 4242);
            var corta = true;
            pipeline.Registrar(new PluginEspia("corta", accion: l => { if (corta) throw new InvalidPluginExecutionException("CARP-XXX-001"); }), "Create", TablaZzf, EtapaDeStep.PreOperation);
            var servicio = fabrica.CreateOrganizationService(null);

            Assert.Throws<InvalidPluginExecutionException>(() => servicio.Create(Zzf(Fila1)));
            corta = false;
            var falla = Falla(() => servicio.Create(Zzf(Fila2)));

            Assert.Equal(4242, falla.Detail.ErrorCode);
            Assert.False(fabrica.Almacen.Existe(TablaZzf, Fila2));
        }

        [Fact]
        public void ClaveAlterna_UnUpdateQueNoTocaLasColumnasDeLaClaveNoChocaConUnDuplicadoSembrado()
        {
            // Sembrar no aplica claves (datos conocidos): un duplicado sembrado a propósito no debe hacer fallar una escritura ajena a la clave, como en la plataforma.
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            almacen.Sembrar(Zzf(Fila1, "zzf_huella", "h"), Zzf(Fila2, "zzf_huella", "h"));

            almacen.Update(Zzf(Fila1, "zzf_otra", 1));

            Assert.Equal(1, almacen.Leer(TablaZzf, Fila1)["zzf_otra"]);
        }

        // ---- Asíncronos y transacción ----

        [Fact]
        public void UnAsincronoEncoladoPorUnaTransaccionRevertidaNoCorre()
        {
            // En la plataforma el trabajo asíncrono nace en la misma transacción: si se revierte, no existe.
            var fabrica = NuevaFabrica(out var pipeline);
            var asincrono = new PluginEspia("asincrono");
            pipeline.Registrar(asincrono, "Create", TablaZzf, EtapaDeStep.PostOperation, orden: 1, modo: ModoDeStep.Asincrono);
            pipeline.Registrar(new PluginEspia("falla", accion: l => { throw new InvalidPluginExecutionException("CARP-XXX-002"); }), "Create", TablaZzf, EtapaDeStep.PostOperation, orden: 2);

            Assert.Throws<InvalidPluginExecutionException>(() => fabrica.CreateOrganizationService(null).Create(Zzf(Fila1)));
            pipeline.EjecutarAsincronos();

            Assert.Equal(0, pipeline.PendientesAsincronos);
            Assert.Empty(asincrono.Llamadas);
        }

        [Fact]
        public void UnAsincronoEncoladoPorUnPluginDeCustomApiQueLanzaNoCorre_PeroSiTerminaBienCorre()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var asincrono = new PluginEspia("asincrono");
            pipeline.Registrar(asincrono, "Create", TablaZzf, EtapaDeStep.PostOperation, modo: ModoDeStep.Asincrono);
            IPlugin Api(bool lanza)
            {
                return new PluginEspia("api", accion: l =>
                {
                    ((IOrganizationServiceFactory)l.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null).Create(Zzf(Guid.NewGuid()));
                    if (lanza) throw new InvalidOperationException("falla");
                });
            }

            Assert.Throws<InvalidOperationException>(() => fabrica.EjecutarComoPlataforma(Api(true), new ContextoDePluginSimulado()));
            Assert.Equal(0, pipeline.PendientesAsincronos);
            fabrica.EjecutarComoPlataforma(Api(false), new ContextoDePluginSimulado());
            Assert.Equal(1, pipeline.PendientesAsincronos);
            pipeline.EjecutarAsincronos();

            Assert.Single(asincrono.Llamadas);
        }

        [Fact]
        public void LaUnidadDeNegocioDelContextoDeUnStepEsLaPropiaDelUsuarioQueEscribe()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var unidadPropia = Guid.NewGuid();
            fabrica.Almacen.RegistrarUsuario(Usuario1, unidadPropia);
            var espia = new PluginEspia("espia");
            pipeline.Registrar(espia, "Create", TablaZzf, EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(Usuario1).Create(Zzf(Fila1));
            fabrica.CreateOrganizationService(Usuario2).Create(Zzf(Fila2));

            Assert.Equal(unidadPropia, espia.Llamadas[0].Contexto.BusinessUnitId);
            Assert.Equal(fabrica.Almacen.UnidadDeNegocioId, espia.Llamadas[1].Contexto.BusinessUnitId);
        }

        // ---- Forma de lo guardado ----

        [Theory]
        [InlineData("1.123456789012", "1.1234567890")]
        [InlineData("0.00000000005", "0.0000000001")]
        [InlineData("79228162514264337593543950335", "79228162514264337593543950335")]
        public void UnDecimalConMasDeDiezDecimalesSeRedondeaAEscala10_YUnoQueDesbordaSeConservaTalCual(string escrito, string guardado)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var valor = decimal.Parse(escrito, System.Globalization.CultureInfo.InvariantCulture);

            almacen.Create(Zzf(Fila1, "zzf_monto", valor));

            Assert.Equal(guardado, Inv((decimal)almacen.Leer(TablaZzf, Fila1)["zzf_monto"]));
        }

        // REEMPLAZA a UnMoneyEscritoSeGuardaComoMoney_ElDobleNoAdivinaLaColumna (vuelta 2, hallazgo 3): fijaba que un Money se guardaba en una columna de una tabla zzf_; las
        // columnas del proyecto son decimal (fixture 03) y una tabla propia valida el tipo. Una zzf_ sigue libre (PA_ESQ_08), así que el caso se mide sobre una columna real del diccionario.
        [Fact]
        public void UnMoneyEnUnaColumnaDecimalDelDiccionarioSeRechaza_NoSeGuardaNiSeConvierteEnSilencio()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var error = Assert.Throws<ArgumentException>(() => almacen.Create(new Entity("sanic_carp_tbl_arreglo") { ["sanic_saldoinicial"] = new Money(5m) }));

            Assert.Contains("sanic_carp_tbl_arreglo", error.Message, StringComparison.Ordinal);
            Assert.Contains("sanic_saldoinicial", error.Message, StringComparison.Ordinal);
            Assert.Empty(almacen.Todas("sanic_carp_tbl_arreglo"));
        }
    }
}
