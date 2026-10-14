using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Vuelta 2, hallazgo 7</b>: la profundidad se cuenta ENTRE saltos, también a través de los pasos asíncronos. Antes un asíncrono corría y lo que escribía era profundidad 1 sin
    /// importar de dónde viniera, así que (a) su profundidad era incoherente con la de la operación que lo encoló y (b) un asíncrono que dispara otro asíncrono nunca llegaba al corte de
    /// 8 y <c>EjecutarAsincronos</c> no terminaba (la prueba colgaba). Regla: el paso asíncrono corre con la <c>Depth</c> de la operación que lo encoló; lo que ESCRIBE es esa
    /// profundidad + 1 (con <c>ParentContext</c> nulo: no es una llamada anidada de un contexto vivo) y pasar de 8 lanza <see cref="InvalidOperationException"/>, que como toda
    /// excepción de un asíncrono queda en <c>Ejecuciones</c> y no sube ni revierte. Fuente del corte: la plataforma corta los ciclos en profundidad 8 (patrones.md §2; PA_PIP_11 para los síncronos).
    /// Las pruebas se frenan solas (el plugin deja de escribir pasadas 20 ejecuciones) para que, si el doble no corta, FALLEN en vez de colgar.
    /// </summary>
    public class AsincronosEncadenadosAceptacion
    {
        private static FabricaDeServiciosSimulada NuevaFabrica(out PipelineSimulado pipeline)
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            pipeline = new PipelineSimulado(fabrica);
            return fabrica;
        }

        private static IOrganizationService Elevado(LlamadaCapturada llamada)
        {
            return ((IOrganizationServiceFactory)llamada.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);
        }

        [Fact]
        public void PA_ASI_01_UnAsincronoCorreConLaProfundidadDeLaOperacionQueLoEncolaYLoQueEscribeEsUnaMas()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            // Operación de profundidad 1 (zzf_fila) cuyo PreOperation escribe a profundidad 2 (zzf_b); el PostOperation asíncrono de zzf_b nace de una operación de profundidad 2.
            pipeline.Registrar(new PluginEspia("encadena", accion: l => { if (l.Contexto.Depth == 1) Elevado(l).Create(Fila("zzf_b", Fila2)); }), "Create", TablaZzf, EtapaDeStep.PreOperation);
            var asincrono = new PluginEspia("asincrono", accion: l => Elevado(l).Create(Fila("zzf_c", Fila3)));
            pipeline.Registrar(asincrono, "Create", "zzf_b", EtapaDeStep.PostOperation, modo: ModoDeStep.Asincrono);
            var sincronoDeC = new PluginEspia("sincrono de c");
            pipeline.Registrar(sincronoDeC, "Create", "zzf_c", EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1));
            Assert.Equal(1, pipeline.PendientesAsincronos);
            pipeline.EjecutarAsincronos();

            Assert.Equal(2, asincrono.Llamadas.Single().Contexto.Depth);
            var deC = sincronoDeC.Llamadas.Single().Contexto;
            Assert.Equal(3, deC.Depth);
            Assert.Null(deC.ParentContext);
        }

        [Fact]
        public void PA_ASI_02_UnaCadenaDeAsincronosSeCortaEnLaProfundidadOcho_ElOctavoQuedaConLaExcepcion_YLaPruebaTermina()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var ejecuciones = 0;
            var asincrono = new PluginEspia("se dispara a si mismo", accion: l =>
            {
                ejecuciones++;
                if (ejecuciones <= 20) Elevado(l).Create(Zzf(Guid.NewGuid()));   // freno de la prueba: sin corte del doble, termina en 21 ejecuciones y la afirmación falla
            });
            pipeline.Registrar(asincrono, "Create", TablaZzf, EtapaDeStep.PostOperation, modo: ModoDeStep.Asincrono);

            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1));
            pipeline.EjecutarAsincronos();

            Assert.Equal(Enumerable.Range(1, 8).ToArray(), asincrono.Llamadas.Select(l => l.Contexto.Depth).ToArray());
            var todas = pipeline.Ejecuciones;
            Assert.Equal(8, todas.Count);
            Assert.All(todas.Take(7), e => Assert.Null(e.Excepcion));
            var corte = Assert.IsType<InvalidOperationException>(todas[7].Excepcion);
            Assert.Contains("8", corte.Message, StringComparison.Ordinal);
            Assert.Equal(0, pipeline.PendientesAsincronos);
            Assert.Equal(8, fabrica.Almacen.Todas(TablaZzf).Count);   // la primera, más las 7 que alcanzaron a escribirse; la octava escritura (profundidad 9) no se aplicó
        }
    }
}
