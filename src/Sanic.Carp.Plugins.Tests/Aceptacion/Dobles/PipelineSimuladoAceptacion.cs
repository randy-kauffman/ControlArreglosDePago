using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <c>PipelineSimulado</c> (diseno/03 4.5): "registra instancias de los steps (clase, mensaje, tabla, etapa, orden, filtro, pre-imagen) y,
    /// cuando un servicio escribe, ejecuta los steps que corresponden CON EL CONTEXTO QUE LA PLATAFORMA DARÍA: <c>UserId</c> e
    /// <c>InitiatingUserId</c> = SYSTEM si la escritura salió del servicio de SYSTEM, <c>Depth + 1</c>. Respeta el filtro de atributos. Sirve
    /// para las pruebas de cadena (la trampa A-23) y para comprobar que las escrituras del sistema no son rechazadas por nuestros propios
    /// steps". Las reglas finas (Id del Create, SharedVariables, transacción, asíncronos) están en la clase del doble.
    /// </summary>
    public class PipelineSimuladoAceptacion
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

        private static IOrganizationService Como(LlamadaCapturada llamada, Guid usuario)
        {
            return ((IOrganizationServiceFactory)llamada.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(usuario);
        }

        // ---- Qué se dispara ----

        [Fact]
        public void PA_PIP_01_UnCreateDisparaSoloLosStepsDeSuMensajeYSuTabla()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var pre = new PluginEspia("pre");
            var post = new PluginEspia("post");
            var deUpdate = new PluginEspia("update");
            var deOtraTabla = new PluginEspia("otra tabla");
            var deDelete = new PluginEspia("delete");
            pipeline.Registrar(pre, "Create", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(post, "Create", TablaZzf, EtapaDeStep.PostOperation);
            pipeline.Registrar(deUpdate, "Update", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(deOtraTabla, "Create", "zzf_otra", EtapaDeStep.PreOperation);
            pipeline.Registrar(deDelete, "Delete", TablaZzf, EtapaDeStep.PostOperation);

            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1, "zzf_a", 1));

            Assert.Single(pre.Llamadas);
            Assert.Single(post.Llamadas);
            Assert.Empty(deUpdate.Llamadas);
            Assert.Empty(deOtraTabla.Llamadas);
            Assert.Empty(deDelete.Llamadas);
        }

        [Fact]
        public void PA_PIP_02_LosStepsCorrenPorEtapaYDentroDeLaEtapaPorOrden_YLaEscrituraVaEntreElPreYElPost()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var bitacora = new List<string>();
            bool? existiaEnPre = null;
            bool? existiaEnPost = null;
            pipeline.Registrar(new PluginEspia("post-1", bitacora, l => existiaEnPost = fabrica.Almacen.Existe(TablaZzf, Fila1)), "Create", TablaZzf, EtapaDeStep.PostOperation, orden: 1);
            pipeline.Registrar(new PluginEspia("pre-20", bitacora), "Create", TablaZzf, EtapaDeStep.PreOperation, orden: 20);
            pipeline.Registrar(new PluginEspia("pre-10", bitacora, l => existiaEnPre = fabrica.Almacen.Existe(TablaZzf, Fila1)), "Create", TablaZzf, EtapaDeStep.PreOperation, orden: 10);
            pipeline.Registrar(new PluginEspia("preval", bitacora), "Create", TablaZzf, EtapaDeStep.PreValidation, orden: 99);
            pipeline.Registrar(new PluginEspia("pre-10-segundo", bitacora), "Create", TablaZzf, EtapaDeStep.PreOperation, orden: 10);
            pipeline.Registrar(new PluginEspia("post-0", bitacora), "Create", TablaZzf, EtapaDeStep.PostOperation, orden: 0);

            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1));

            Assert.Equal(new[] { "preval", "pre-10", "pre-10-segundo", "pre-20", "post-0", "post-1" }, bitacora.ToArray());
            Assert.False(existiaEnPre.Value, "en PreOperation la fila todavía no está");
            Assert.True(existiaEnPost.Value, "en PostOperation la fila ya está");
        }

        [Fact]
        public void PA_PIP_03_ElFiltroDeAtributosSoloCuentaEnUpdate_YBastaQueElTargetTraigaUnaColumnaDelFiltroAunqueNoCambie()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1, "zzf_a", 1, "zzf_b", 2, "zzf_c", 3));
            var conFiltro = new PluginEspia("filtro");
            var sinFiltro = new PluginEspia("sin filtro");
            pipeline.Registrar(conFiltro, "Update", TablaZzf, EtapaDeStep.PostOperation, filtro: new[] { "zzf_a", "zzf_b" });
            pipeline.Registrar(sinFiltro, "Update", TablaZzf, EtapaDeStep.PostOperation);
            var servicio = fabrica.CreateOrganizationService(null);

            servicio.Update(Zzf(Fila1, "zzf_c", 30));
            Assert.Empty(conFiltro.Llamadas);
            servicio.Update(Zzf(Fila1, "zzf_b", 2));
            Assert.Single(conFiltro.Llamadas);
            servicio.Update(Zzf(Fila1, "zzf_a", 10, "zzf_b", 20, "zzf_c", 30));
            Assert.Equal(2, conFiltro.Llamadas.Count);

            Assert.Equal(3, sinFiltro.Llamadas.Count);
        }

        [Fact]
        public void PA_PIP_04_ElFiltroNoCuentaEnCreateNiEnDelete()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var enCreate = new PluginEspia("create");
            var enDelete = new PluginEspia("delete");
            pipeline.Registrar(enCreate, "Create", TablaZzf, EtapaDeStep.PostOperation, filtro: new[] { "zzf_a" });
            pipeline.Registrar(enDelete, "Delete", TablaZzf, EtapaDeStep.PostOperation, filtro: new[] { "zzf_a" });
            var servicio = fabrica.CreateOrganizationService(null);

            servicio.Create(Zzf(Fila1, "zzf_otra_columna", 1));
            servicio.Delete(TablaZzf, Fila1);

            Assert.Single(enCreate.Llamadas);
            Assert.Single(enDelete.Llamadas);
        }

        // ---- El contexto ----

        [Fact]
        public void PA_PIP_05_ElContextoDeUnaEscrituraDeUnaPersonaTraeLoQueLaPlataformaDaria()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.UnidadDeNegocioId = new Guid("eeeeeeee-0000-4000-8000-000000000001");
            fabrica.Almacen.OrganizacionId = new Guid("eeeeeeee-0000-4000-8000-000000000002");
            var espia = new PluginEspia("espia");
            pipeline.Registrar(espia, "Update", TablaZzf, EtapaDeStep.PreOperation);
            fabrica.Almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));

            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Update(Zzf(Fila1, "zzf_a", 2));

            var contexto = espia.Llamadas.Single().Contexto;
            Assert.Equal("Update", contexto.MessageName);
            Assert.Equal(20, contexto.Stage);
            Assert.Equal(0, contexto.Mode);
            Assert.Equal(1, contexto.Depth);
            Assert.Equal(TablaZzf, contexto.PrimaryEntityName);
            Assert.Equal(Fila1, contexto.PrimaryEntityId);
            Assert.Equal(ApoyoDeDobles.Usuario1, contexto.UserId);
            Assert.Equal(ApoyoDeDobles.Usuario1, contexto.InitiatingUserId);
            Assert.Null(contexto.ParentContext);
            Assert.Equal(fabrica.Almacen.Reloj.AhoraUtc, contexto.OperationCreatedOn);
            Assert.Equal(fabrica.Almacen.UnidadDeNegocioId, contexto.BusinessUnitId);
            Assert.Equal(fabrica.Almacen.OrganizacionId, contexto.OrganizationId);
            var target = Assert.IsType<Entity>(contexto.InputParameters["Target"]);
            Assert.Equal(TablaZzf, target.LogicalName);
            Assert.Equal(Fila1, target.Id);
            Assert.Equal(new[] { "zzf_a" }, espia.Llamadas.Single().ColumnasDelTargetAlEntrar.ToArray());
        }

        [Fact]
        public void PA_PIP_06_UnaEscrituraDelServicioDeSystemTraeSystemEnUserIdEInitiatingUserId()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var espia = new PluginEspia("espia");
            pipeline.Registrar(espia, "Create", TablaZzf, EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1));

            var contexto = espia.Llamadas.Single().Contexto;
            Assert.Equal(fabrica.IdDeSystem, contexto.UserId);
            Assert.Equal(fabrica.IdDeSystem, contexto.InitiatingUserId);
            Assert.NotEqual(ApoyoDeDobles.Usuario1, contexto.InitiatingUserId);
        }

        [Fact]
        public void PA_PIP_07_LaCadenaDeLaTrampaA23_PersonaCreaYElPostEscribeComoSystem_ElStepAnidadoVeSystemYDepthDos()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_a", 0));
            var enCreate = new PluginEspia("post create", accion: l => Elevado(l).Update(Zzf(Fila2, "zzf_a", 1)));
            var enUpdate = new PluginEspia("pre update");
            pipeline.Registrar(enCreate, "Create", TablaZzf, EtapaDeStep.PostOperation);
            pipeline.Registrar(enUpdate, "Update", TablaZzf, EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Create(Zzf(Fila1));

            var externo = enCreate.Llamadas.Single().Contexto;
            var anidado = enUpdate.Llamadas.Single().Contexto;
            Assert.Equal(1, externo.Depth);
            Assert.Equal(ApoyoDeDobles.Usuario1, externo.InitiatingUserId);
            Assert.Equal(2, anidado.Depth);
            Assert.Same(externo, anidado.ParentContext);
            Assert.Equal(fabrica.IdDeSystem, anidado.UserId);
            Assert.Equal(fabrica.IdDeSystem, anidado.InitiatingUserId);
            Assert.Equal(new[] { fabrica.IdDeSystem }, fabrica.Escrituras.Where(e => e.Operacion == OperacionDeDatos.Update).Select(e => e.Identidad).ToArray());
        }

        [Fact]
        public void PA_PIP_08_UnaEscrituraAnidadaConElServicioDelLlamadorConservaAlLlamador_SoloElevarLoPierde()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_a", 0));
            var enCreate = new PluginEspia("post create", accion: l => Como(l, ApoyoDeDobles.Usuario1).Update(Zzf(Fila2, "zzf_a", 1)));
            var enUpdate = new PluginEspia("pre update");
            pipeline.Registrar(enCreate, "Create", TablaZzf, EtapaDeStep.PostOperation);
            pipeline.Registrar(enUpdate, "Update", TablaZzf, EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Create(Zzf(Fila1));

            var anidado = enUpdate.Llamadas.Single().Contexto;
            Assert.Equal(2, anidado.Depth);
            Assert.Equal(ApoyoDeDobles.Usuario1, anidado.UserId);
            Assert.Equal(ApoyoDeDobles.Usuario1, anidado.InitiatingUserId);
        }

        [Fact]
        public void PA_PIP_09_UnGuardianQueSoloDejaEscribirUnaColumnaASystemRechazaALaPersonaYDejaPasarLaCadenaDelSistema()
        {
            // "Sirve para comprobar que las escrituras del sistema no son rechazadas por nuestros propios steps" (4.5; 0.3, DC-03).
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_total", 0));
            var guardian = new PluginEspia("guardian", accion: l =>
            {
                var target = (Entity)l.Contexto.InputParameters["Target"];
                if (target.Contains("zzf_total") && l.Contexto.InitiatingUserId != fabrica.IdDeSystem)
                    throw new InvalidPluginExecutionException("CARP-XXX-001: solo el sistema escribe el total");
            });
            var calculador = new PluginEspia("calculador", accion: l => Elevado(l).Update(Zzf(Fila2, "zzf_total", 99)));
            pipeline.Registrar(guardian, "Update", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(calculador, "Create", TablaZzf, EtapaDeStep.PostOperation);

            Assert.Throws<InvalidPluginExecutionException>(() => fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Update(Zzf(Fila2, "zzf_total", 5)));
            Assert.Equal(0, fabrica.Almacen.Leer(TablaZzf, Fila2)["zzf_total"]);
            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Create(Zzf(Fila1));

            Assert.Equal(99, fabrica.Almacen.Leer(TablaZzf, Fila2)["zzf_total"]);
        }

        [Fact]
        public void PA_PIP_10_UnPluginDeCustomApiLanzadoConEjecutarComoPlataformaEscribeAProfundidadDos()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1, "zzf_a", 0));
            var paso = new PluginEspia("paso de update");
            pipeline.Registrar(paso, "Update", TablaZzf, EtapaDeStep.PostOperation);
            var api = new PluginEspia("custom api", accion: l => Elevado(l).Update(Zzf(Fila1, "zzf_a", 1)));
            var contextoDeLaApi = new ContextoDePluginSimulado { MessageName = "sanic_carp_capi_tomarconversacion", Depth = 1, UserId = ApoyoDeDobles.Usuario1, InitiatingUserId = ApoyoDeDobles.Usuario1 };

            fabrica.EjecutarComoPlataforma(api, contextoDeLaApi);

            var anidado = paso.Llamadas.Single().Contexto;
            Assert.Equal(2, anidado.Depth);
            Assert.Same(contextoDeLaApi, anidado.ParentContext);
            Assert.Equal(fabrica.IdDeSystem, anidado.InitiatingUserId);
        }

        [Fact]
        public void PA_PIP_11_PasarDeProfundidadOchoLanzaYRestauraTodo_ElPluginCorreEnLasProfundidadesUnoAOcho()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1));
            var recursivo = new PluginEspia("recursivo", accion: l => Elevado(l).Update(Zzf(Fila1, "zzf_n", l.Contexto.Depth)));
            pipeline.Registrar(recursivo, "Update", TablaZzf, EtapaDeStep.PostOperation);

            var error = Assert.Throws<InvalidOperationException>(() => fabrica.CreateOrganizationService(null).Update(Zzf(Fila1, "zzf_n", 0)));

            Assert.Contains("8", error.Message, StringComparison.Ordinal);
            Assert.Equal(Enumerable.Range(1, 8).ToArray(), recursivo.Llamadas.Select(l => l.Contexto.Depth).ToArray());
            Assert.False(fabrica.Almacen.Leer(TablaZzf, Fila1).Contains("zzf_n"), "la transacción de más afuera restauró todo");
        }

        // ---- Qué recibe el step ----

        [Fact]
        public void PA_PIP_12_LoQueUnPreOperationLeAgregueAlTargetSeEscribe_TambienLoDeUnPreValidation()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            pipeline.Registrar(new PluginEspia("preval", accion: l => ((Entity)l.Contexto.InputParameters["Target"])["zzf_desde_preval"] = 1), "Create", TablaZzf, EtapaDeStep.PreValidation);
            pipeline.Registrar(new PluginEspia("preop", accion: l => ((Entity)l.Contexto.InputParameters["Target"])["zzf_desde_preop"] = 2), "Create", TablaZzf, EtapaDeStep.PreOperation);
            var delLlamador = Zzf(Fila1, "zzf_a", 1);
            var id = fabrica.CreateOrganizationService(null).Create(delLlamador);

            var fila = fabrica.Almacen.Leer(TablaZzf, id);
            Assert.Equal(new[] { "zzf_a" }, delLlamador.Attributes.Keys.ToArray());

            Assert.Equal(1, fila["zzf_desde_preval"]);
            Assert.Equal(2, fila["zzf_desde_preop"]);
            Assert.Equal(1, fila["zzf_a"]);
            Assert.Equal(new[] { "zzf_a", "zzf_desde_preop", "zzf_desde_preval" }, fabrica.Escrituras.Single().Columnas.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA_PIP_13_EnCreateSinIdElTargetNoTraeIdEnLasEtapasPreYTrae_ElDeLaFilaCreada_EnPost()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var preval = new PluginEspia("preval");
            var preop = new PluginEspia("preop");
            var post = new PluginEspia("post");
            pipeline.Registrar(preval, "Create", TablaZzf, EtapaDeStep.PreValidation);
            pipeline.Registrar(preop, "Create", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(post, "Create", TablaZzf, EtapaDeStep.PostOperation);

            var id = fabrica.CreateOrganizationService(null).Create(new Entity(TablaZzf) { ["zzf_a"] = 1 });

            Assert.Equal(Guid.Empty, preval.Llamadas.Single().IdDelTargetAlEntrar);
            Assert.Equal(Guid.Empty, preop.Llamadas.Single().IdDelTargetAlEntrar);
            Assert.Equal(Guid.Empty, preop.Llamadas.Single().Contexto.PrimaryEntityId);
            Assert.Equal(id, post.Llamadas.Single().IdDelTargetAlEntrar);
            Assert.Equal(id, post.Llamadas.Single().Contexto.PrimaryEntityId);
            Assert.Equal(id, post.Llamadas.Single().Contexto.OutputParameters["id"]);
            Assert.NotEqual(Guid.Empty, id);
        }

        [Fact]
        public void PA_PIP_14_EnCreateConIdElIdEsElMismoEnTodasLasEtapas()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var pre = new PluginEspia("pre");
            var post = new PluginEspia("post");
            pipeline.Registrar(pre, "Create", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(post, "Create", TablaZzf, EtapaDeStep.PostOperation);

            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1));

            Assert.Equal(Fila1, pre.Llamadas.Single().IdDelTargetAlEntrar);
            Assert.Equal(Fila1, post.Llamadas.Single().IdDelTargetAlEntrar);
            Assert.Equal(Fila1, post.Llamadas.Single().Contexto.PrimaryEntityId);
        }

        // ---- Pre-imagen ----

        [Fact]
        public void PA_PIP_15_LaPreImagenSeTomaAntesDeEscribirConLasColumnasDeclaradas_YSoloEnUpdateYDelete()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1, "zzf_a", 1, "zzf_b", 2, "zzf_c", 3));
            var enUpdate = new PluginEspia("update");
            var enCreate = new PluginEspia("create");
            var enDelete = new PluginEspia("delete");
            pipeline.Registrar(enUpdate, "Update", TablaZzf, EtapaDeStep.PostOperation, nombreDeImagenPrevia: "PreImagen", columnasDeImagenPrevia: new[] { "zzf_a", "zzf_b" });
            pipeline.Registrar(enCreate, "Create", TablaZzf, EtapaDeStep.PreOperation, nombreDeImagenPrevia: "PreImagen", columnasDeImagenPrevia: new[] { "zzf_a" });
            pipeline.Registrar(enDelete, "Delete", TablaZzf, EtapaDeStep.PreOperation, nombreDeImagenPrevia: "Previa");
            var servicio = fabrica.CreateOrganizationService(null);

            servicio.Update(Zzf(Fila1, "zzf_a", 10));
            servicio.Create(Zzf(Fila2, "zzf_a", 1));
            servicio.Delete(TablaZzf, Fila1);

            var imagen = enUpdate.Llamadas.Single().Contexto.PreEntityImages["PreImagen"];
            Assert.Equal(1, imagen["zzf_a"]);
            Assert.Equal(2, imagen["zzf_b"]);
            Assert.False(imagen.Contains("zzf_c"));
            Assert.Equal(Fila1, imagen.Id);
            Assert.Empty(enCreate.Llamadas.Single().Contexto.PreEntityImages);
            var imagenDeDelete = enDelete.Llamadas.Single().Contexto.PreEntityImages["Previa"];
            Assert.Equal(10, imagenDeDelete["zzf_a"]);
            Assert.Equal(3, imagenDeDelete["zzf_c"]);
        }

        [Fact]
        public void PA_PIP_16_V6_LaPreImagenDeUnStepQueCorreConUnUsuarioSinElPerfilTraeLaColumnaOcultaAusente()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1, "zzf_nombre", "n", "zzf_cedula", "ZZF-000"));
            fabrica.Almacen.OcultarColumna(TablaZzf, "zzf_cedula", ApoyoDeDobles.Usuario1);
            var espia = new PluginEspia("update");
            pipeline.Registrar(espia, "Update", TablaZzf, EtapaDeStep.PreOperation, nombreDeImagenPrevia: "PreImagen", columnasDeImagenPrevia: new[] { "zzf_nombre", "zzf_cedula" });

            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Update(Zzf(Fila1, "zzf_cedula", "ZZF-000"));
            fabrica.CreateOrganizationService(null).Update(Zzf(Fila1, "zzf_cedula", "ZZF-000"));

            var comoPersona = espia.Llamadas[0].Contexto.PreEntityImages["PreImagen"];
            var comoSystem = espia.Llamadas[1].Contexto.PreEntityImages["PreImagen"];
            Assert.False(comoPersona.Contains("zzf_cedula"), "V-6: ausente, no nula");
            Assert.Equal("n", comoPersona["zzf_nombre"]);
            Assert.Equal("ZZF-000", comoSystem["zzf_cedula"]);
        }

        [Fact]
        public void PA_PIP_17_SinColumnasDeclaradasLaPreImagenTraeTodasLasQueTieneLaFila()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1, "zzf_a", 1, "zzf_b", 2));
            var espia = new PluginEspia("update");
            pipeline.Registrar(espia, "Update", TablaZzf, EtapaDeStep.PreOperation, nombreDeImagenPrevia: "PreImagen");

            fabrica.CreateOrganizationService(null).Update(Zzf(Fila1, "zzf_a", 5));

            var imagen = espia.Llamadas.Single().Contexto.PreEntityImages["PreImagen"];
            Assert.Equal(new[] { "zzf_a", "zzf_b", "zzf_filaid" }, imagen.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        // ---- Otros mensajes ----

        [Fact]
        public void PA_PIP_18_UnDeleteLlevaElTargetComoEntityReference()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1));
            var espia = new PluginEspia("delete");
            pipeline.Registrar(espia, "Delete", TablaZzf, EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(null).Delete(TablaZzf, Fila1);

            var contexto = espia.Llamadas.Single().Contexto;
            Assert.Equal("Delete", contexto.MessageName);
            var target = Assert.IsType<EntityReference>(contexto.InputParameters["Target"]);
            Assert.Equal(TablaZzf, target.LogicalName);
            Assert.Equal(Fila1, target.Id);
            Assert.Equal(Fila1, contexto.PrimaryEntityId);
            Assert.False(fabrica.Almacen.Existe(TablaZzf, Fila1));
        }

        [Fact]
        public void PA_PIP_19_UnAssignRequestDisparaLosStepsDeAssignConTargetYAssignee_NoLosDeUpdate()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1));
            var deAssign = new PluginEspia("assign");
            var deUpdate = new PluginEspia("update");
            pipeline.Registrar(deAssign, "Assign", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(deUpdate, "Update", TablaZzf, EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Execute(new AssignRequest
            {
                Target = new EntityReference(TablaZzf, Fila1),
                Assignee = new EntityReference("team", ApoyoDeDobles.Team1)
            });

            var contexto = deAssign.Llamadas.Single().Contexto;
            Assert.Equal("Assign", contexto.MessageName);
            Assert.Equal(Fila1, ((EntityReference)contexto.InputParameters["Target"]).Id);
            Assert.Equal(ApoyoDeDobles.Team1, ((EntityReference)contexto.InputParameters["Assignee"]).Id);
            Assert.Empty(deUpdate.Llamadas);
        }

        [Fact]
        public void PA_PIP_20_UnUpdateRequestDisparaLosStepsDeUpdate()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1));
            var espia = new PluginEspia("update");
            pipeline.Registrar(espia, "Update", TablaZzf, EtapaDeStep.PostOperation, filtro: new[] { "zzf_a" });

            fabrica.CreateOrganizationService(null).Execute(new UpdateRequest { Target = Zzf(Fila1, "zzf_a", 1) });

            Assert.Single(espia.Llamadas);
        }

        // ---- Excepciones y transacción ----

        [Fact]
        public void PA_PIP_21_UnaExcepcionDePreValidationSubeSinCambiosNiOtrosSteps()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var error = new InvalidPluginExecutionException("CARP-XXX-001: no");
            var siguiente = new PluginEspia("preop");
            pipeline.Registrar(new PluginEspia("preval", accion: l => { throw error; }), "Create", TablaZzf, EtapaDeStep.PreValidation);
            pipeline.Registrar(siguiente, "Create", TablaZzf, EtapaDeStep.PreOperation);

            var lanzada = Assert.Throws<InvalidPluginExecutionException>(() => fabrica.CreateOrganizationService(null).Create(Zzf(Fila1)));

            Assert.Same(error, lanzada);
            Assert.False(fabrica.Almacen.Existe(TablaZzf, Fila1));
            Assert.Empty(siguiente.Llamadas);
            Assert.Empty(fabrica.Escrituras);
        }

        [Fact]
        public void PA_PIP_22_UnaExcepcionDePreOperationRestauraLoQueHicieronLosStepsAnterioresYSubeLaMismaInstancia()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var error = new InvalidPluginExecutionException("CARP-XXX-002: no");
            pipeline.Registrar(new PluginEspia("escribe antes", accion: l => Elevado(l).Create(Fila("zzf_otra", Fila2))), "Create", TablaZzf, EtapaDeStep.PreOperation, orden: 1);
            pipeline.Registrar(new PluginEspia("falla", accion: l => { throw error; }), "Create", TablaZzf, EtapaDeStep.PreOperation, orden: 2);
            var post = new PluginEspia("post");
            pipeline.Registrar(post, "Create", TablaZzf, EtapaDeStep.PostOperation);

            var lanzada = Assert.Throws<InvalidPluginExecutionException>(() => fabrica.CreateOrganizationService(null).Create(Zzf(Fila1)));

            Assert.Same(error, lanzada);
            Assert.False(fabrica.Almacen.Existe("zzf_otra", Fila2));
            Assert.False(fabrica.Almacen.Existe(TablaZzf, Fila1));
            Assert.Empty(post.Llamadas);
        }

        [Fact]
        public void PA_PIP_23_UnaExcepcionDePostOperationRevierteLaPropiaEscrituraYLasAnidadas()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_a", 0));
            var error = new InvalidPluginExecutionException("CARP-XXX-003: no");
            pipeline.Registrar(new PluginEspia("escribe", accion: l => Elevado(l).Update(Zzf(Fila2, "zzf_a", 1))), "Create", TablaZzf, EtapaDeStep.PostOperation, orden: 1);
            pipeline.Registrar(new PluginEspia("falla", accion: l => { throw error; }), "Create", TablaZzf, EtapaDeStep.PostOperation, orden: 2);

            var lanzada = Assert.Throws<InvalidPluginExecutionException>(() => fabrica.CreateOrganizationService(null).Create(Zzf(Fila1)));

            Assert.Same(error, lanzada);
            Assert.False(fabrica.Almacen.Existe(TablaZzf, Fila1));
            Assert.Equal(0, fabrica.Almacen.Leer(TablaZzf, Fila2)["zzf_a"]);
        }

        [Fact]
        public void PA_PIP_24_UnaEscrituraQueFallaPorK01NoDejaLosEfectosDelPreOperationNiCorreElPost()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_huella", "h1"));
            pipeline.Registrar(new PluginEspia("pre escribe", accion: l => Elevado(l).Create(Fila("zzf_otra", Fila3))), "Create", TablaZzf, EtapaDeStep.PreOperation);
            var post = new PluginEspia("post");
            pipeline.Registrar(post, "Create", TablaZzf, EtapaDeStep.PostOperation);

            var falla = Falla(() => fabrica.CreateOrganizationService(null).Create(Zzf(Fila1, "zzf_huella", "h1")));

            Assert.Equal(CodigosDeFalla.ClaveDuplicada, falla.Detail.ErrorCode);
            Assert.False(fabrica.Almacen.Existe("zzf_otra", Fila3));
            Assert.Empty(post.Llamadas);
        }

        [Fact]
        public void PA_PIP_25_UnaExcepcionDeUnStepAnidadoSubeHastaElQueEscribioYRevierteTodoLoDeLaOperacionDeMasAfuera()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_a", 0));
            var error = new InvalidPluginExecutionException("CARP-XXX-004: no");
            var recibido = new List<Exception>();
            pipeline.Registrar(new PluginEspia("post create", accion: l =>
            {
                try { Elevado(l).Update(Zzf(Fila2, "zzf_a", 1)); }
                catch (Exception e) { recibido.Add(e); throw; }
            }), "Create", TablaZzf, EtapaDeStep.PostOperation);
            pipeline.Registrar(new PluginEspia("pre update falla", accion: l => { throw error; }), "Update", TablaZzf, EtapaDeStep.PreOperation);

            var lanzada = Assert.Throws<InvalidPluginExecutionException>(() => fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Create(Zzf(Fila1)));

            Assert.Same(error, lanzada);
            Assert.Same(error, Assert.Single(recibido));
            Assert.False(fabrica.Almacen.Existe(TablaZzf, Fila1));
            Assert.Equal(0, fabrica.Almacen.Leer(TablaZzf, Fila2)["zzf_a"]);
        }

        // ---- Asíncronos ----

        [Fact]
        public void PA_PIP_26_UnStepAsincronoNoCorreDentroDeLaOperacion_QuedaEnColaYCorreConEjecutarAsincronosConModoUno()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var asincrono = new PluginEspia("asincrono");
            pipeline.Registrar(asincrono, "Create", TablaZzf, EtapaDeStep.PostOperation, modo: ModoDeStep.Asincrono);

            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Create(Zzf(Fila1));

            Assert.Empty(asincrono.Llamadas);
            Assert.Equal(1, pipeline.PendientesAsincronos);
            pipeline.EjecutarAsincronos();
            Assert.Equal(0, pipeline.PendientesAsincronos);
            var contexto = asincrono.Llamadas.Single().Contexto;
            Assert.Equal(1, contexto.Mode);
            Assert.Equal(40, contexto.Stage);
            Assert.Equal(Fila1, contexto.PrimaryEntityId);
            Assert.Equal(ApoyoDeDobles.Usuario1, contexto.InitiatingUserId);
            pipeline.EjecutarAsincronos();
            Assert.Single(asincrono.Llamadas);
        }

        [Fact]
        public void PA_PIP_27_UnaExcepcionDeUnAsincronoNoSubeNiRevierte_QuedaEnEjecuciones()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var error = new InvalidOperationException("falló el asíncrono");
            pipeline.Registrar(new PluginEspia("asincrono", accion: l => { throw error; }), "Create", TablaZzf, EtapaDeStep.PostOperation, modo: ModoDeStep.Asincrono);
            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1));

            pipeline.EjecutarAsincronos();

            Assert.True(fabrica.Almacen.Existe(TablaZzf, Fila1));
            var ejecucion = pipeline.Ejecuciones.Single();
            Assert.Same(error, ejecucion.Excepcion);
            Assert.Equal(ModoDeStep.Asincrono, ejecucion.Step.Modo);
        }

        [Fact]
        public void PA_PIP_28_LoQueEscribeUnAsincronoDisparaSusStepsAProfundidadDosYSinPadre()
        {
            // AJUSTADA en la vuelta 2 (hallazgo 7, 2026-10-14): ANTES decía "profundidad uno". La profundidad se cuenta ENTRE saltos: el asíncrono nace de una operación de
            // profundidad 1 y lo que él escribe es profundidad 2; si no, un asíncrono que dispara otro asíncrono nunca llega al corte de 8 (AsincronosEncadenadosAceptacion).
            // El padre sigue siendo nulo: el trabajo asíncrono no es una llamada anidada de un contexto vivo.
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_a", 0));
            pipeline.Registrar(new PluginEspia("asincrono", accion: l => Elevado(l).Update(Zzf(Fila2, "zzf_a", 1))), "Create", TablaZzf, EtapaDeStep.PostOperation, modo: ModoDeStep.Asincrono);
            var deUpdate = new PluginEspia("update");
            pipeline.Registrar(deUpdate, "Update", TablaZzf, EtapaDeStep.PostOperation);
            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Create(Zzf(Fila1));

            pipeline.EjecutarAsincronos();

            var contexto = deUpdate.Llamadas.Single().Contexto;
            Assert.Equal(2, contexto.Depth);
            Assert.Null(contexto.ParentContext);
            Assert.Equal(fabrica.IdDeSystem, contexto.InitiatingUserId);
        }

        // ---- Compartidas, registro y validación ----

        [Fact]
        public void PA_PIP_29_LasSharedVariablesSeComparteEntreLasEtapasDeUnaOperacion_PeroNoPasanALasEscriturasAnidadas()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_a", 0));
            object visto = null;
            object vistoPorElAnidado = "sin ejecutar";
            pipeline.Registrar(new PluginEspia("pre", accion: l => l.Contexto.SharedVariables["clave"] = "valor"), "Create", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(new PluginEspia("post", accion: l =>
            {
                l.Contexto.SharedVariables.TryGetValue("clave", out visto);
                Elevado(l).Update(Zzf(Fila2, "zzf_a", 1));
            }), "Create", TablaZzf, EtapaDeStep.PostOperation);
            pipeline.Registrar(new PluginEspia("anidado", accion: l => l.Contexto.SharedVariables.TryGetValue("clave", out vistoPorElAnidado)), "Update", TablaZzf, EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1));

            Assert.Equal("valor", visto);
            Assert.Null(vistoPorElAnidado);
        }

        [Fact]
        public void PA_PIP_30_EjecucionesGuardaCadaStepEnOrdenConSuRegistroSuContextoYSuExcepcion()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var error = new InvalidPluginExecutionException("falla");
            var primero = pipeline.Registrar(new PluginEspia("primero"), "Create", TablaZzf, EtapaDeStep.PreOperation, orden: 1);
            var segundo = pipeline.Registrar(new PluginEspia("segundo", accion: l => { throw error; }), "Create", TablaZzf, EtapaDeStep.PreOperation, orden: 2);

            Assert.Throws<InvalidPluginExecutionException>(() => fabrica.CreateOrganizationService(null).Create(Zzf(Fila1)));

            Assert.Equal(new[] { primero, segundo }, pipeline.Ejecuciones.Select(e => e.Step).ToArray());
            Assert.Null(pipeline.Ejecuciones[0].Excepcion);
            Assert.Same(error, pipeline.Ejecuciones[1].Excepcion);
            Assert.All(pipeline.Ejecuciones, e => Assert.Equal("Create", e.Contexto.MessageName));
            Assert.Equal(TablaZzf, primero.Tabla);
            Assert.Equal("Create", primero.Mensaje);
            Assert.Equal(EtapaDeStep.PreOperation, primero.Etapa);
            Assert.Equal(1, primero.Orden);
            Assert.Equal(ModoDeStep.Sincrono, primero.Modo);
            Assert.Empty(primero.Filtro);
            Assert.Null(primero.NombreDeImagenPrevia);
        }

        [Fact]
        public void PA_PIP_31_RegistrarValidaSusArgumentos()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var plugin = new PluginEspia("p");

            Assert.Throws<ArgumentNullException>(() => pipeline.Registrar(null, "Create", TablaZzf, EtapaDeStep.PreOperation));
            Assert.ThrowsAny<ArgumentException>(() => pipeline.Registrar(plugin, "SetState", TablaZzf, EtapaDeStep.PreOperation));
            Assert.ThrowsAny<ArgumentException>(() => pipeline.Registrar(plugin, null, TablaZzf, EtapaDeStep.PreOperation));
            Assert.ThrowsAny<ArgumentException>(() => pipeline.Registrar(plugin, "Create", TablaZzf, EtapaDeStep.PreOperation, modo: ModoDeStep.Asincrono));
            Assert.ThrowsAny<ArgumentException>(() => pipeline.Registrar(plugin, "Create", TablaZzf, EtapaDeStep.PreValidation, modo: ModoDeStep.Asincrono));
            pipeline.Registrar(plugin, "Create", TablaZzf, EtapaDeStep.PostOperation, modo: ModoDeStep.Asincrono);
            Assert.Equal(10, (int)EtapaDeStep.PreValidation);
            Assert.Equal(20, (int)EtapaDeStep.PreOperation);
            Assert.Equal(40, (int)EtapaDeStep.PostOperation);
        }

        [Fact]
        public void PA_PIP_32_UnaFabricaYaConectadaNoAdmiteUnSegundoPipeline()
        {
            var fabrica = NuevaFabrica(out _);

            Assert.Throws<InvalidOperationException>(() => new PipelineSimulado(fabrica));
            Assert.Throws<ArgumentNullException>(() => new PipelineSimulado(null));
        }

        [Fact]
        public void PA_PIP_33_SinPipelineConectadoLasEscriturasNoDisparanNada()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());

            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1));

            Assert.True(fabrica.Almacen.Existe(TablaZzf, Fila1));
        }
    }
}
