using System;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <c>ContextoDePluginSimulado</c>, <c>ProveedorDeServiciosSimulado</c> y el tracing (diseno/03 4.5). El contexto expone
    /// <c>MessageName</c>, <c>Stage</c>, <c>Mode</c>, <c>Depth</c>, <c>UserId</c>, <c>InitiatingUserId</c>, <c>PrimaryEntityName</c>, <c>PrimaryEntityId</c>,
    /// <c>InputParameters</c> (con <c>Target</c>), <c>OutputParameters</c>, <c>PreEntityImages</c>, <c>OperationCreatedOn</c>, <c>ParentContext</c>.
    /// </summary>
    public class ContextoYProveedorSimuladosAceptacion
    {
        // ---- Contexto ----

        [Fact]
        public void PA_CTX_01_LosValoresPorDefectoSonLosDeLaClase()
        {
            var contexto = new ContextoDePluginSimulado();

            Assert.Equal(1, contexto.Depth);
            Assert.Equal(0, contexto.Mode);
            Assert.Equal(20, contexto.Stage);
            Assert.Equal("Update", contexto.MessageName);
            Assert.Equal(string.Empty, contexto.PrimaryEntityName);
            Assert.Equal(Guid.Empty, contexto.PrimaryEntityId);
            Assert.Equal(Guid.Empty, contexto.UserId);
            Assert.Equal(Guid.Empty, contexto.InitiatingUserId);
            Assert.Equal(new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc), contexto.OperationCreatedOn);
            Assert.Equal(DateTimeKind.Utc, contexto.OperationCreatedOn.Kind);
            Assert.NotEqual(Guid.Empty, contexto.CorrelationId);
            Assert.NotEqual(Guid.Empty, contexto.OperationId);
            Assert.Null(contexto.ParentContext);
            Assert.False(contexto.IsExecutingOffline);
            Assert.False(contexto.IsOfflinePlayback);
        }

        [Fact]
        public void PA_CTX_02_LasColeccionesNacenVaciasYNuncaNulas_YCadaInstanciaTieneLasSuyas()
        {
            var a = new ContextoDePluginSimulado();
            var b = new ContextoDePluginSimulado();

            Assert.NotNull(a.InputParameters);
            Assert.NotNull(a.OutputParameters);
            Assert.NotNull(a.SharedVariables);
            Assert.NotNull(a.PreEntityImages);
            Assert.NotNull(a.PostEntityImages);
            Assert.Empty(a.InputParameters);
            Assert.Empty(a.OutputParameters);
            Assert.Empty(a.SharedVariables);
            Assert.Empty(a.PreEntityImages);
            Assert.Empty(a.PostEntityImages);

            a.InputParameters["Target"] = new Entity("x");
            a.SharedVariables["k"] = 1;
            a.PreEntityImages["PreImagen"] = new Entity("x");
            Assert.Empty(b.InputParameters);
            Assert.Empty(b.SharedVariables);
            Assert.Empty(b.PreEntityImages);
            Assert.NotEqual(a.CorrelationId, b.CorrelationId);
            Assert.NotEqual(a.OperationId, b.OperationId);
        }

        [Fact]
        public void PA_CTX_03_TodasLasPropiedadesSeEscribenYSeLeenTalCual()
        {
            var padre = new ContextoDePluginSimulado { MessageName = "Create" };
            var usuario = Guid.NewGuid();
            var iniciador = Guid.NewGuid();
            var entidad = Guid.NewGuid();
            var fecha = new DateTime(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            var contexto = new ContextoDePluginSimulado
            {
                MessageName = "Delete",
                Stage = 40,
                Mode = 1,
                Depth = 3,
                UserId = usuario,
                InitiatingUserId = iniciador,
                PrimaryEntityName = "sanic_carp_tbl_cuota",
                PrimaryEntityId = entidad,
                OperationCreatedOn = fecha,
                ParentContext = padre,
                BusinessUnitId = usuario,
                OrganizationId = iniciador,
                IsInTransaction = true
            };

            Assert.Equal("Delete", contexto.MessageName);
            Assert.Equal(40, contexto.Stage);
            Assert.Equal(1, contexto.Mode);
            Assert.Equal(3, contexto.Depth);
            Assert.Equal(usuario, contexto.UserId);
            Assert.Equal(iniciador, contexto.InitiatingUserId);
            Assert.Equal("sanic_carp_tbl_cuota", contexto.PrimaryEntityName);
            Assert.Equal(entidad, contexto.PrimaryEntityId);
            Assert.Equal(fecha, contexto.OperationCreatedOn);
            Assert.Same(padre, contexto.ParentContext);
            Assert.Equal(usuario, contexto.BusinessUnitId);
            Assert.Equal(iniciador, contexto.OrganizationId);
            Assert.True(contexto.IsInTransaction);
        }

        [Fact]
        public void PA_CTX_04_ElTargetYLasImagenesSeLeenComoLoHaceUnPlugin()
        {
            var contexto = new ContextoDePluginSimulado();
            var target = new Entity("sanic_carp_tbl_arreglo", Guid.NewGuid()) { ["sanic_plazo"] = 6 };
            contexto.InputParameters["Target"] = target;
            contexto.PreEntityImages["PreImagen"] = new Entity("sanic_carp_tbl_arreglo", target.Id) { ["sanic_plazo"] = 3 };
            contexto.OutputParameters["archivo"] = "QUJD";

            var ejecucion = (IPluginExecutionContext)contexto;

            Assert.Same(target, ejecucion.InputParameters["Target"]);
            Entity previa;
            Assert.True(ejecucion.PreEntityImages.TryGetValue("PreImagen", out previa));
            Assert.Equal(3, previa["sanic_plazo"]);
            Assert.False(ejecucion.PostEntityImages.TryGetValue("PostImagen", out previa));
            Assert.Equal("QUJD", ejecucion.OutputParameters["archivo"]);
        }

        // ---- Proveedor ----

        [Fact]
        public void PA_PRO_01_ElProveedorEntregaElContextoLaFabricaYElTracing()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var contexto = new ContextoDePluginSimulado();
            var proveedor = new ProveedorDeServiciosSimulado(contexto, fabrica);

            Assert.Same(contexto, proveedor.GetService(typeof(IPluginExecutionContext)));
            Assert.Same(contexto, proveedor.GetService(typeof(IExecutionContext)));
            Assert.Same(fabrica, proveedor.GetService(typeof(IOrganizationServiceFactory)));
            Assert.Same(fabrica.Tracing, proveedor.GetService(typeof(ITracingService)));
            Assert.Same(fabrica.Tracing, proveedor.Tracing);
            Assert.Same(contexto, proveedor.Contexto);
            Assert.Same(fabrica, proveedor.Fabrica);
        }

        [Fact]
        public void PA_PRO_02_UnTipoQueNoSeOfreceDevuelveNullComoLaPlataforma_YUnTipoNuloEsUnErrorDeUso()
        {
            var proveedor = new ProveedorDeServiciosSimulado(new ContextoDePluginSimulado(), new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria()));

            Assert.Null(proveedor.GetService(typeof(IOrganizationService)));
            Assert.Null(proveedor.GetService(typeof(string)));
            Assert.Throws<ArgumentNullException>(() => proveedor.GetService(null));
        }

        [Fact]
        public void PA_PRO_03_ElConstructorRechazaArgumentosNulos()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());

            Assert.Throws<ArgumentNullException>(() => new ProveedorDeServiciosSimulado(null, fabrica));
            Assert.Throws<ArgumentNullException>(() => new ProveedorDeServiciosSimulado(new ContextoDePluginSimulado(), null));
            Assert.Throws<ArgumentNullException>(() => new FabricaDeServiciosSimulada(null));
        }

        [Fact]
        public void PA_PRO_04_ElTracingEsUnoPorFabricaYLoComparteTodoProveedorQueSeArme()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var uno = new ProveedorDeServiciosSimulado(new ContextoDePluginSimulado(), fabrica);
            var otro = new ProveedorDeServiciosSimulado(new ContextoDePluginSimulado(), fabrica);

            ((ITracingService)uno.GetService(typeof(ITracingService))).Trace("desde uno");
            ((ITracingService)otro.GetService(typeof(ITracingService))).Trace("desde otro");

            Assert.Equal(new[] { "desde uno", "desde otro" }, fabrica.Tracing.Lineas.ToArray());
            Assert.NotSame(fabrica.Tracing, new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria()).Tracing);
        }

        // ---- Tracing ----

        [Fact]
        public void PA_TRA_01_TraceFormateaComoElDeLaPlataformaYGuardaLaLinea()
        {
            var tracing = new TracingSimulado();

            tracing.Trace("registrando {0} de {1}", 3, "cuotas");
            tracing.Trace("sin argumentos");
            tracing.Trace("{0}", "texto con {llaves} intactas");

            Assert.Equal(new[] { "registrando 3 de cuotas", "sin argumentos", "texto con {llaves} intactas" }, tracing.Lineas.ToArray());
        }

        [Fact]
        public void PA_TRA_02_UnTextoConLlavesPasadoComoFormatoLanzaFormatException_ComoLaTrampaDelDiseno43Punto7()
        {
            var tracing = new TracingSimulado();

            Assert.Throws<FormatException>(() => tracing.Trace("el mapeo {hoja} falló"));
            Assert.Throws<FormatException>(() => tracing.Trace("JSON {\"a\":1}"));
            Assert.Empty(tracing.Lineas);
        }

        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion_Culturas))]
        public void PA_TRA_03_ElFormatoUsaLaCulturaInvariante(string cultura)
        {
            var tracing = new TracingSimulado();
            ApoyoDeAceptacion.ConCultura(cultura, () => tracing.Trace("monto {0} fecha {1}", 1234.5m, new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc)));

            Assert.Equal("monto 1234.5 fecha " + new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc).ToString(CultureInfo.InvariantCulture), tracing.Lineas.Single());
        }

        public static System.Collections.Generic.IEnumerable<object[]> ApoyoDeAceptacion_Culturas()
        {
            return ApoyoDeAceptacion.Culturas();
        }

        [Fact]
        public void PA_TRA_04_UnFormatoNuloOVacioGuardaUnaLineaVacia()
        {
            var tracing = new TracingSimulado();

            tracing.Trace(null);
            tracing.Trace(string.Empty);

            Assert.Equal(new[] { string.Empty, string.Empty }, tracing.Lineas.ToArray());
        }
    }
}
