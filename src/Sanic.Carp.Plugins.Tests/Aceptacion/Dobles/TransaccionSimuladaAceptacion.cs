using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// Transacción de la plataforma (diseno/03 4.5): <c>Instantanea()</c> y <c>Restaurar()</c>, y el ayudante <c>EjecutarComoPlataforma(plugin,
    /// contexto)</c>: "toma la instantánea, ejecuta y, si hay excepción, restaura: es la transacción de la plataforma". Límite conocido, NO
    /// emulado: una falla de datos que el plugin ATRAPA y sigue aborta la transacción en la plataforma real ("ISV code reduced the open
    /// transaction count", patrones.md §3); el doble no la aborta. Se escala en el informe del lote 3.
    /// </summary>
    public class TransaccionSimuladaAceptacion
    {
        // ---- Instantanea / Restaurar ----

        [Fact]
        public void PA_TRX_01_RestaurarDejaLasFilasComoEstabanConLasMismasRowVersion()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1), Zzf(Fila2, "zzf_a", 2));
            var antes = almacen.Todas(TablaZzf);
            var foto = almacen.Instantanea();

            almacen.Update(Zzf(Fila1, "zzf_a", 10));
            almacen.Delete(TablaZzf, Fila2);
            almacen.Create(Zzf(Fila3, "zzf_a", 3));
            almacen.Restaurar(foto);

            var despues = almacen.Todas(TablaZzf);
            Assert.Equal(antes.Select(f => f.Id).ToArray(), despues.Select(f => f.Id).ToArray());
            for (int i = 0; i < antes.Count; i++)
            {
                ComparadorDeFormas.AssertIgual(antes[i], despues[i], "fila " + i);
            }
            Assert.False(almacen.Existe(TablaZzf, Fila3));
        }

        [Fact]
        public void PA_TRX_02_LaFotoSirveParaRestaurarMasDeUnaVez()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));
            var foto = almacen.Instantanea();

            almacen.Update(Zzf(Fila1, "zzf_a", 2));
            almacen.Restaurar(foto);
            almacen.Update(Zzf(Fila1, "zzf_a", 3));
            almacen.Restaurar(foto);

            Assert.Equal(1, almacen.Leer(TablaZzf, Fila1)["zzf_a"]);
        }

        [Fact]
        public void PA_TRX_03_LaFotoEsIndependienteDeLoQueSeHagaDespuesConLasEntidadesDevueltas()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_ref", new EntityReference("team", ApoyoDeDobles.Team1) { Name = "Equipo" }));
            var foto = almacen.Instantanea();

            ((EntityReference)almacen.Retrieve(TablaZzf, Fila1, new ColumnSet(true))["zzf_ref"]).Name = "mutada";
            almacen.Update(Zzf(Fila1, "zzf_ref", new EntityReference("team", Guid.NewGuid())));
            almacen.Restaurar(foto);

            var restaurada = (EntityReference)almacen.Retrieve(TablaZzf, Fila1, new ColumnSet(true))["zzf_ref"];
            Assert.Equal("Equipo", restaurada.Name);
            Assert.Equal(ApoyoDeDobles.Team1, restaurada.Id);
        }

        [Fact]
        public void PA_TRX_04_RestaurarNoBorraLosRegistrosNiHaceRetrocederElContadorDeVersiones()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));
            var foto = almacen.Instantanea();
            almacen.Update(Zzf(Fila1, "zzf_a", 2));
            var versionDelUpdate = Version(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet()));
            almacen.Retrieve(TablaZzf, Fila1, new ColumnSet());

            almacen.Restaurar(foto);
            almacen.Update(Zzf(Fila1, "zzf_a", 3));

            Assert.Equal(2, almacen.Escrituras.Count);
            Assert.Equal(2, almacen.Consultas.Count);
            Assert.True(Version(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet())) > versionDelUpdate, "la versión nunca retrocede, ni siquiera tras restaurar");
        }

        [Fact]
        public void PA_TRX_05_LaFotoIncluyeLosArchivos()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1));
            almacen.SembrarArchivo(TablaZzf, Fila1, "zzf_archivo", "a.xlsx", new byte[] { 1, 2, 3 });
            var foto = almacen.Instantanea();
            almacen.SembrarArchivo(TablaZzf, Fila1, "zzf_archivo", "b.xlsx", new byte[] { 9, 9 });

            almacen.Restaurar(foto);

            var inicio = (Microsoft.Crm.Sdk.Messages.InitializeFileBlocksDownloadResponse)almacen.Execute(new Microsoft.Crm.Sdk.Messages.InitializeFileBlocksDownloadRequest
            {
                Target = new EntityReference(TablaZzf, Fila1),
                FileAttributeName = "zzf_archivo"
            });
            Assert.Equal("a.xlsx", inicio.FileName);
            Assert.Equal(3, inicio.FileSizeInBytes);
        }

        // ---- EjecutarComoPlataforma ----

        [Fact]
        public void PA_TRX_10_SiElPluginTerminaBienSusEscriturasQuedan()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var plugin = new PluginEspia("p", accion: llamada =>
            {
                var servicio = ((IOrganizationServiceFactory)llamada.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);
                servicio.Create(Zzf(Fila1, "zzf_a", 1));
            });

            fabrica.EjecutarComoPlataforma(plugin, new ContextoDePluginSimulado());

            Assert.True(fabrica.Almacen.Existe(TablaZzf, Fila1));
            Assert.Single(plugin.Llamadas);
        }

        [Fact]
        public void PA_TRX_11_SiElPluginLanzaSeRestauraTodoLoQueEscribioYSeRelanzaLaMismaExcepcion()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_a", 1));
            var error = new InvalidPluginExecutionException("CARP-XXX-001: no");
            var plugin = new PluginEspia("p", accion: llamada =>
            {
                var servicio = ((IOrganizationServiceFactory)llamada.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);
                servicio.Create(Zzf(Fila1, "zzf_a", 1));
                servicio.Update(Zzf(Fila2, "zzf_a", 99));
                servicio.Delete(TablaZzf, Fila2);
                throw error;
            });

            var lanzada = Assert.Throws<InvalidPluginExecutionException>(() => fabrica.EjecutarComoPlataforma(plugin, new ContextoDePluginSimulado()));

            Assert.Same(error, lanzada);
            Assert.False(fabrica.Almacen.Existe(TablaZzf, Fila1));
            Assert.Equal(1, fabrica.Almacen.Leer(TablaZzf, Fila2)["zzf_a"]);
            Assert.Equal(3, fabrica.Escrituras.Count);
        }

        [Fact]
        public void PA_TRX_12_CualquierTipoDeExcepcionRestauraYSeRelanzaSinEnvolver()
        {
            foreach (Exception error in new Exception[] { new InvalidOperationException("x"), new InvalidPluginExecutionException("y"), new NotSupportedException("z") })
            {
                var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
                var plugin = new PluginEspia("p", accion: llamada =>
                {
                    ((IOrganizationServiceFactory)llamada.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null).Create(Zzf(Fila1));
                    throw error;
                });

                var lanzada = Record.Exception(() => fabrica.EjecutarComoPlataforma(plugin, new ContextoDePluginSimulado()));

                Assert.Same(error, lanzada);
                Assert.Empty(fabrica.Almacen.Todas(TablaZzf));
            }
        }

        [Fact]
        public void PA_TRX_13_ElPluginRecibeUnProveedorConSuContextoLaFabricaYElTracing_QueSeDevuelveYLasTrazasSobrevivenALaExcepcion()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var contexto = new ContextoDePluginSimulado { MessageName = "sanic_carp_capi_generarformato" };
            IServiceProvider recibido = null;
            var plugin = new PluginEspia("p", accion: llamada =>
            {
                recibido = llamada.Proveedor;
                ((ITracingService)llamada.Proveedor.GetService(typeof(ITracingService))).Trace("paso {0}", 1);
                throw new InvalidOperationException("falla");
            });

            Assert.Throws<InvalidOperationException>(() => fabrica.EjecutarComoPlataforma(plugin, contexto));

            Assert.Same(contexto, recibido.GetService(typeof(IPluginExecutionContext)));
            Assert.Same(fabrica, recibido.GetService(typeof(IOrganizationServiceFactory)));
            Assert.Equal(new[] { "paso 1" }, fabrica.Tracing.Lineas.ToArray());
        }

        [Fact]
        public void PA_TRX_14_EjecutarComoPlataformaDevuelveElProveedorUsado()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var contexto = new ContextoDePluginSimulado();
            var plugin = new PluginEspia("p");

            var proveedor = fabrica.EjecutarComoPlataforma(plugin, contexto);

            Assert.Same(contexto, proveedor.Contexto);
            Assert.Same(fabrica, proveedor.Fabrica);
            Assert.Same(plugin.Llamadas.Single().Proveedor, proveedor);
        }

        [Fact]
        public void PA_TRX_15_LosArgumentosNulosSonErroresDeUso()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());

            Assert.Throws<ArgumentNullException>(() => fabrica.EjecutarComoPlataforma(null, new ContextoDePluginSimulado()));
            Assert.Throws<ArgumentNullException>(() => fabrica.EjecutarComoPlataforma(new PluginEspia("p"), null));
        }
    }
}
