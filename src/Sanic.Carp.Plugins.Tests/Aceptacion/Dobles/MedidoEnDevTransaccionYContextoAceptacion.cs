using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoMedido;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Lote 3b: transacción y contexto</b> (<c>salida/30-sonda-escenarios.json</c> tx0 a tx4, <c>41-sonda-resumen.json</c> f_ids y g_transaccion, <c>31-sonda-trazas.json</c>).
    /// <para>
    /// <b>(g) Regla medida.</b> Una falla de ESCRITURA (<c>Create</c>, <c>Update</c>, <c>Delete</c>) que un plugin sincrónico de PreOperation o PostOperation (o el plugin de una Custom API que corre con
    /// <c>EjecutarComoPlataforma</c>) ATRAPA y sigue: (1) revierte YA todo lo escrito en la transacción (un <c>Retrieve</c> de la fila creada antes da <c>0x80040217</c>); (2) la siguiente escritura falla con
    /// <c>0x8009000C</c>; (3) al terminar el plugin sin lanzar, la operación externa falla con <c>0x8009000C</c> y no queda nada escrito. Un <c>Retrieve</c> fallido atrapado NO aborta.
    /// <b>Límite declarado (no medido, el doble no aborta):</b> una falla atrapada en PreValidation (sin transacción) o en un asíncrono, y la variante con el servicio de SYSTEM de la sonda.
    /// Una excepción lanzada por un step ANIDADO y atrapada por el plugin externo se trata igual que una falla de la plataforma (extensión no medida de lo medido).
    /// </para>
    /// <para>
    /// <b>(f) Reglas medidas.</b> <c>Target.Id</c> y <c>PrimaryEntityId</c> de un <c>Create</c> NUNCA están vacíos, en ninguna etapa y con o sin id enviado; en PreValidation el <c>Target</c> no trae el atributo
    /// del id primario y en PreOperation y PostOperation sí; <c>OutputParameters["id"]</c> solo en PostOperation. <c>ParentContext</c> es nulo en PreValidation de profundidad 1 y NO nulo en PreOperation y
    /// PostOperation síncronos aun con <c>Depth</c> 1: es un contexto del mismo mensaje, tabla y profundidad (la operación principal). Cuántos saltos hay desde ahí hasta el contexto del llamador, en una
    /// escritura anidada, NO se midió: el doble garantiza que la cadena de <c>ParentContext</c> llega al llamador. El <c>Target</c> que agrega la plataforma (<c>createdon</c>, <c>statecode</c>, <c>modifiedon</c>,
    /// <c>timezoneruleversionnumber</c>...) y las claves propias de <c>SharedVariables</c> NO se reproducen (límite declarado, 4.5).
    /// </para>
    /// </summary>
    public class MedidoEnDevTransaccionYContextoAceptacion
    {
        private const string Externa = "zzf_externa";
        private const string MensajeDeAbortada = "ISV code reduced the open transaction count. Custom plug-ins should not catch exceptions from OrganizationService calls and continue processing.";

        private static IOrganizationService Elevado(LlamadaCapturada llamada)
        {
            return ((IOrganizationServiceFactory)llamada.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);
        }

        private static FaultException<OrganizationServiceFault> Atrapar(Action accion)
        {
            try
            {
                accion();
                return null;
            }
            catch (FaultException<OrganizationServiceFault> falla)
            {
                return falla;
            }
        }

        private sealed class Escenario
        {
            public FabricaDeServiciosSimulada Fabrica;
            public PipelineSimulado Pipeline;
            public readonly Dictionary<string, FaultException<OrganizationServiceFault>> Atrapadas = new Dictionary<string, FaultException<OrganizationServiceFault>>();
            public bool TerminoSinLanzar;
        }

        /// <summary>El plugin de la sonda (tx0 a tx4): A ok; la operación que falla (o el control); C después de la falla; releer A; D otra más; y termina SIN lanzar.</summary>
        private static Escenario Armar(EtapaDeStep etapa, Func<IOrganizationService, FaultException<OrganizationServiceFault>> operacionQueFalla)
        {
            var e = new Escenario { Fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria()) };
            e.Pipeline = new PipelineSimulado(e.Fabrica);
            e.Fabrica.Almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            e.Fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_huella", "h1"));
            e.Pipeline.Registrar(new PluginEspia("sonda", accion: l => Ejecutar(e, l, operacionQueFalla)), "Create", Externa, etapa);
            return e;
        }

        private static void Ejecutar(Escenario e, LlamadaCapturada llamada, Func<IOrganizationService, FaultException<OrganizationServiceFault>> operacionQueFalla)
        {
            var servicio = Elevado(llamada);
            servicio.Create(Zzf(Fila3, "zzf_huella", "hA"));
            e.Atrapadas["falla"] = operacionQueFalla(servicio);
            e.Atrapadas["despues"] = Atrapar(() => servicio.Create(Zzf(Guid.NewGuid(), "zzf_huella", "hC")));
            e.Atrapadas["releer"] = Atrapar(() => servicio.Retrieve(TablaZzf, Fila3, new Microsoft.Xrm.Sdk.Query.ColumnSet()));
            e.Atrapadas["otra"] = Atrapar(() => servicio.Create(Zzf(Guid.NewGuid(), "zzf_huella", "hD")));
            e.TerminoSinLanzar = true;
        }

        private static void AssertAbortada(Escenario e, FaultException<OrganizationServiceFault> externa, int codigoDeLaFalla)
        {
            AssertFalla(e.Atrapadas["falla"], codigoDeLaFalla);
            AssertFalla(e.Atrapadas["despues"], CodigosDeFalla.TransaccionAbortada, MensajeDeAbortada, "escritura siguiente");
            AssertFalla(e.Atrapadas["releer"], CodigosDeFalla.FilaInexistente, null, "releer la fila creada antes de la falla: ya se revirtió");
            AssertFalla(e.Atrapadas["otra"], CodigosDeFalla.TransaccionAbortada, MensajeDeAbortada, "otra escritura");
            Assert.True(e.TerminoSinLanzar, "el plugin terminó sin lanzar");
            AssertFalla(externa, CodigosDeFalla.TransaccionAbortada, MensajeDeAbortada, "operación externa");
            var almacen = e.Fabrica.Almacen;
            Assert.False(almacen.Existe(Externa, Fila1), "la fila externa no quedó");
            Assert.False(almacen.Existe(TablaZzf, Fila3), "la fila anidada creada antes de la falla no quedó");
            Assert.Single(almacen.Todas(TablaZzf));
            Assert.True(almacen.Existe(TablaZzf, Fila2), "lo sembrado antes sigue");
        }

        [Fact]
        public void PA_MED_TRX_01_UnaClaveDuplicadaAtrapadaEnPreOperationAbortaLaTransaccion()
        {
            // 30-sonda-escenarios.json tx1 (K-01 duplicada atrapada): la siguiente escritura 0x8009000C, el Retrieve de la primera 0x80040217, la otra 0x8009000C, el plugin termina y la externa falla 0x8009000C; no queda nada.
            // Mata: el límite "no emulado" del lote 3 (el plugin sigue y todo se confirma).
            var e = Armar(EtapaDeStep.PreOperation, s => Atrapar(() => s.Create(Zzf(Guid.NewGuid(), "zzf_huella", "h1"))));

            var externa = Falla(() => e.Fabrica.CreateOrganizationService(null).Create(Zzf(Fila1, "zzf_huella", "hExterna") .WithTabla(Externa)));

            AssertAbortada(e, externa, CodigosDeFalla.ClaveDuplicada);
        }

        [Theory]
        [InlineData("update")]
        [InlineData("delete")]
        public void PA_MED_TRX_02_UnUpdateOUnDeleteDeUnaFilaInexistenteAtrapadoAbortaLaTransaccion(string operacion)
        {
            // 30-sonda-escenarios.json tx3 (Update de fila inexistente atrapado). El Delete es la misma regla (es una escritura); no se midió aparte.
            var e = Armar(EtapaDeStep.PreOperation, s => Atrapar(() =>
            {
                if (operacion == "update") s.Update(Zzf(Guid.NewGuid(), "zzf_a", 1));
                else s.Delete(TablaZzf, Guid.NewGuid());
            }));

            var externa = Falla(() => e.Fabrica.CreateOrganizationService(null).Create(Zzf(Fila1).WithTabla(Externa)));

            AssertAbortada(e, externa, CodigosDeFalla.FilaInexistente);
        }

        [Fact]
        public void PA_MED_TRX_03_UnCreateConLookupInexistenteAtrapadoAbortaLaTransaccion()
        {
            // 30-sonda-escenarios.json tx4 (Create con lookup inexistente atrapado, 0x80040217).
            var e = Armar(EtapaDeStep.PreOperation, s => Atrapar(() => s.Create(new Entity(Conversacion) { ["sanic_clienteid"] = new EntityReference(Cliente, Guid.NewGuid()) })));

            var externa = Falla(() => e.Fabrica.CreateOrganizationService(null).Create(Zzf(Fila1).WithTabla(Externa)));

            AssertAbortada(e, externa, CodigosDeFalla.FilaInexistente);
        }

        [Fact]
        public void PA_MED_TRX_04_LoMismoEnPostOperation_LaFilaExternaYaCreadaTambienSeRevierte()
        {
            // El escenario medido fue PreOperation; PostOperation es la misma transacción (IsInTransaction verdadero en 41-sonda-resumen.json f_ids). La fila externa, ya escrita, también desaparece.
            var e = Armar(EtapaDeStep.PostOperation, s => Atrapar(() => s.Create(Zzf(Guid.NewGuid(), "zzf_huella", "h1"))));

            var externa = Falla(() => e.Fabrica.CreateOrganizationService(null).Create(Zzf(Fila1).WithTabla(Externa)));

            AssertAbortada(e, externa, CodigosDeFalla.ClaveDuplicada);
        }

        [Fact]
        public void PA_MED_TRX_05_UnRetrieveDeUnaFilaInexistenteAtrapadoNoAborta_TodoSeConfirma_VERDE_A_PROPOSITO()
        {
            // 30-sonda-escenarios.json tx2 (control): el Retrieve inexistente atrapado no aborta; queda lo escrito antes y después. Mata: abortar por cualquier falla.
            var e = new Escenario { Fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria()) };
            e.Pipeline = new PipelineSimulado(e.Fabrica);
            e.Pipeline.Registrar(new PluginEspia("sonda", accion: l =>
            {
                var servicio = Elevado(l);
                servicio.Create(Zzf(Fila3));
                e.Atrapadas["retrieve"] = Atrapar(() => servicio.Retrieve(TablaZzf, Guid.NewGuid(), new Microsoft.Xrm.Sdk.Query.ColumnSet()));
                servicio.Create(Zzf(Fila2));
                e.TerminoSinLanzar = true;
            }), "Create", Externa, EtapaDeStep.PreOperation);

            e.Fabrica.CreateOrganizationService(null).Create(Zzf(Fila1).WithTabla(Externa));

            AssertFalla(e.Atrapadas["retrieve"], CodigosDeFalla.FilaInexistente);
            Assert.True(e.Fabrica.Almacen.Existe(Externa, Fila1));
            Assert.True(e.Fabrica.Almacen.Existe(TablaZzf, Fila3));
            Assert.True(e.Fabrica.Almacen.Existe(TablaZzf, Fila2));
        }

        [Fact]
        public void PA_MED_TRX_06_UnPluginDeCustomApiQueAtrapaUnaFallaDeEscrituraTambienAborta_ElProveedorFalla0x8009000C()
        {
            // EjecutarComoPlataforma es la transacción de la plataforma para el plugin de una Custom API: misma regla que un step.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            fabrica.Almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_huella", "h1"));
            FaultException<OrganizationServiceFault> deLaSiguiente = null;
            var api = new PluginEspia("api", accion: l =>
            {
                var servicio = Elevado(l);
                servicio.Create(Zzf(Fila3, "zzf_huella", "hA"));
                Atrapar(() => servicio.Create(Zzf(Guid.NewGuid(), "zzf_huella", "h1")));
                deLaSiguiente = Atrapar(() => servicio.Create(Zzf(Guid.NewGuid(), "zzf_huella", "hC")));
            });

            var externa = Falla(() => fabrica.EjecutarComoPlataforma(api, new ContextoDePluginSimulado { MessageName = "sanic_carp_capi_tomarconversacion", Depth = 1 }));

            AssertFalla(deLaSiguiente, CodigosDeFalla.TransaccionAbortada, MensajeDeAbortada);
            AssertFalla(externa, CodigosDeFalla.TransaccionAbortada, MensajeDeAbortada);
            Assert.False(fabrica.Almacen.Existe(TablaZzf, Fila3));
            Assert.Single(fabrica.Almacen.Todas(TablaZzf));
        }

        [Fact]
        public void PA_MED_TRX_07_UnaExcepcionQueElPluginAtrapaYRelanzaSubeIgual_LaMismaInstancia_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: PA_PIP_25. Si el plugin RELANZA, lo que sube es su excepción, no 0x8009000C (esa falla es solo para el plugin que sigue como si nada).
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            fabrica.Almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            fabrica.Almacen.Sembrar(Zzf(Fila2, "zzf_huella", "h1"));
            pipeline.Registrar(new PluginEspia("relanza", accion: l =>
            {
                try { Elevado(l).Create(Zzf(Guid.NewGuid(), "zzf_huella", "h1")); }
                catch (FaultException<OrganizationServiceFault>) { throw; }
            }), "Create", Externa, EtapaDeStep.PreOperation);

            var lanzada = Falla(() => fabrica.CreateOrganizationService(null).Create(Zzf(Fila1).WithTabla(Externa)));

            Assert.Equal(CodigosDeFalla.ClaveDuplicada, lanzada.Detail.ErrorCode);
        }

        [Fact]
        public void PA_MED_TRX_08_SinNingunaFallaAtrapadaTodoSeConfirma_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: 30-sonda-escenarios.json tx0 (control sin falla).
            var e = new Escenario { Fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria()) };
            e.Pipeline = new PipelineSimulado(e.Fabrica);
            e.Pipeline.Registrar(new PluginEspia("sonda", accion: l => { Elevado(l).Create(Zzf(Fila3)); Elevado(l).Create(Zzf(Fila2)); }), "Create", Externa, EtapaDeStep.PreOperation);

            e.Fabrica.CreateOrganizationService(null).Create(Zzf(Fila1).WithTabla(Externa));

            Assert.True(e.Fabrica.Almacen.Existe(Externa, Fila1) && e.Fabrica.Almacen.Existe(TablaZzf, Fila3) && e.Fabrica.Almacen.Existe(TablaZzf, Fila2));
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (f) Id del Target y PrimaryEntityId
        // ---------------------------------------------------------------------------------------------------------------------

        private sealed class EtapasDeUnCreate
        {
            public PluginEspia PreValidation = new PluginEspia("preval");
            public PluginEspia PreOperation = new PluginEspia("preop");
            public PluginEspia PostOperation = new PluginEspia("post");
            public FabricaDeServiciosSimulada Fabrica;
            public Guid Id;
        }

        private static EtapasDeUnCreate CrearConEspias(Entity entidad)
        {
            var e = new EtapasDeUnCreate { Fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria()) };
            var pipeline = new PipelineSimulado(e.Fabrica);
            pipeline.Registrar(e.PreValidation, "Create", TablaZzf, EtapaDeStep.PreValidation);
            pipeline.Registrar(e.PreOperation, "Create", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(e.PostOperation, "Create", TablaZzf, EtapaDeStep.PostOperation);
            e.Id = e.Fabrica.CreateOrganizationService(null).Create(entidad);
            return e;
        }

        [Fact]
        public void PA_MED_CTX_01_EnUnCreateSinIdElTargetYElPrimaryEntityIdNuncaEstanVacios_EnNingunaEtapa()
        {
            // 41-sonda-resumen.json f_ids: Target_Id_vacio False y Target_Id_igual_a_PrimaryEntityId True en PreValidation, PreOperation y PostOperation, también sin id enviado (consola y Web API).
            // Invierte PA_PIP_13. Mata: vaciar el Id en las etapas Pre.
            var e = CrearConEspias(new Entity(TablaZzf) { ["zzf_a"] = 1 });

            foreach (var espia in new[] { e.PreValidation, e.PreOperation, e.PostOperation })
            {
                var llamada = espia.Llamadas.Single();
                Assert.NotEqual(Guid.Empty, llamada.IdDelTargetAlEntrar);
                Assert.Equal(llamada.IdDelTargetAlEntrar, llamada.Contexto.PrimaryEntityId);
                Assert.Equal(e.Id, llamada.IdDelTargetAlEntrar);
            }
        }

        [Fact]
        public void PA_MED_CTX_01b_EnUnCreateConIdEsElMismoEnTodasLasEtapas_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO (PA_PIP_14): con id enviado ya era así.
            var e = CrearConEspias(Zzf(Fila1, "zzf_a", 1));

            Assert.Equal(Fila1, e.Id);
            foreach (var espia in new[] { e.PreValidation, e.PreOperation, e.PostOperation })
            {
                Assert.Equal(Fila1, espia.Llamadas.Single().IdDelTargetAlEntrar);
                Assert.Equal(Fila1, espia.Llamadas.Single().Contexto.PrimaryEntityId);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA_MED_CTX_02_EnPreValidationElTargetNoTraeElAtributoDelIdPrimario_EnPreOperationYPostOperationSi(bool conIdEnviado)
        {
            // 41-sonda-resumen.json f_ids: Target_tiene_atributo_id_primario False en PreValidation y True en PreOperation y PostOperation, con y sin id enviado.
            var entidad = conIdEnviado ? Zzf(Fila1, "zzf_a", 1) : new Entity(TablaZzf) { ["zzf_a"] = 1 };

            var e = CrearConEspias(entidad);

            Assert.DoesNotContain("zzf_filaid", e.PreValidation.Llamadas.Single().ColumnasDelTargetAlEntrar);
            Assert.Contains("zzf_filaid", e.PreOperation.Llamadas.Single().ColumnasDelTargetAlEntrar);
            Assert.Contains("zzf_filaid", e.PostOperation.Llamadas.Single().ColumnasDelTargetAlEntrar);
            Assert.Equal(e.Id, e.PreOperation.Llamadas.Single().ClonDelTargetAlEntrar["zzf_filaid"]);
            Assert.Equal(e.Id, e.PostOperation.Llamadas.Single().ClonDelTargetAlEntrar["zzf_filaid"]);
        }

        [Fact]
        public void PA_MED_CTX_03_ElAtributoDelIdQueAgregaLaPlataformaNoCuentaComoLoQueElCodigoEscribio_NiSeMutaLaEntidadDelLlamador_VERDE_A_PROPOSITO()
        {
            // Decisión del arquitecto: Escrituras.Columnas sigue siendo "lo que el código escribió" (PA_PIP_12, 'el Update trae solo X'); el id primario inyectado por la plataforma no figura.
            var enviada = new Entity(TablaZzf) { ["zzf_a"] = 1 };

            var e = CrearConEspias(enviada);

            Assert.Equal(new[] { "zzf_a" }, e.Fabrica.Escrituras.Single().Columnas.ToArray());
            Assert.DoesNotContain("zzf_filaid", e.Fabrica.Escrituras.Single().Valores.Keys);
            Assert.Equal(new[] { "zzf_a" }, enviada.Attributes.Keys.ToArray());
            Assert.Equal(Guid.Empty, enviada.Id);
        }

        [Fact]
        public void PA_MED_CTX_04_OutputParametersIdSoloExisteEnPostOperation_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: 41-sonda-resumen.json f_ids (OutputParameters_id: ausente en Pre, presente_con_valor en Post).
            var e = CrearConEspias(new Entity(TablaZzf));

            Assert.False(e.PreValidation.Llamadas.Single().Contexto.OutputParameters.Contains("id"));
            Assert.False(e.PreOperation.Llamadas.Single().Contexto.OutputParameters.Contains("id"));
            Assert.Equal(e.Id, e.PostOperation.Llamadas.Single().Contexto.OutputParameters["id"]);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // ParentContext
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_CTX_05_ElParentContextEsNuloEnPreValidationDeProfundidadUno_YNoNuloEnPreOperationYPostOperation()
        {
            // 41-sonda-resumen.json f_ids: ParentContext 'no' en PreValidation y 'si:Create.sanic_carp_tbl_cliente.depth1' en PreOperation y PostOperation (Depth 1). Invierte PA_PIP_05 (Assert.Null).
            var e = CrearConEspias(new Entity(TablaZzf));

            Assert.Null(e.PreValidation.Llamadas.Single().Contexto.ParentContext);
            foreach (var espia in new[] { e.PreOperation, e.PostOperation })
            {
                var contexto = espia.Llamadas.Single().Contexto;
                Assert.NotNull(contexto.ParentContext);
                Assert.Equal("Create", contexto.ParentContext.MessageName);
                Assert.Equal(TablaZzf, contexto.ParentContext.PrimaryEntityName);
                Assert.Equal(1, contexto.ParentContext.Depth);
            }
        }

        private static bool Alcanza(IPluginExecutionContext desde, IPluginExecutionContext objetivo)
        {
            var salto = 0;
            for (var actual = desde == null ? null : desde.ParentContext; actual != null && salto < 10; actual = actual.ParentContext, salto++)
                if (ReferenceEquals(actual, objetivo)) return true;
            return false;
        }

        [Fact]
        public void PA_MED_CTX_06_EnUnaEscrituraAnidadaLaCadenaDeParentContextLlegaAlContextoDelLlamador_EnPreValidationPreOperationYPostOperation()
        {
            // 31-sonda-trazas.json: el PreValidation anidado (depth 2) tiene como padre al contexto de la cliente depth 1; el Pre/Post anidado tiene como padre un contexto de su propia operación (depth 2).
            // Cuántos saltos hay no se midió: se exige solo que el llamador sea alcanzable (reemplaza el Assert.Same de PA_PIP_07 y PA_PIP_10).
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            var externo = new PluginEspia("externo", accion: l => Elevado(l).Create(Zzf(Fila2)));
            var preval = new PluginEspia("preval anidado");
            var preop = new PluginEspia("preop anidado");
            var post = new PluginEspia("post anidado");
            pipeline.Registrar(externo, "Create", Externa, EtapaDeStep.PostOperation);
            pipeline.Registrar(preval, "Create", TablaZzf, EtapaDeStep.PreValidation);
            pipeline.Registrar(preop, "Create", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(post, "Create", TablaZzf, EtapaDeStep.PostOperation);

            fabrica.CreateOrganizationService(null).Create(new Entity(Externa));

            var llamador = externo.Llamadas.Single().Contexto;
            foreach (var espia in new[] { preval, preop, post })
            {
                var contexto = espia.Llamadas.Single().Contexto;
                Assert.Equal(2, contexto.Depth);
                Assert.True(Alcanza(contexto, llamador), espia.Nombre + ": la cadena de ParentContext no llega al contexto del llamador");
            }
            Assert.Same(llamador, preval.Llamadas.Single().Contexto.ParentContext);
            Assert.Equal(2, preop.Llamadas.Single().Contexto.ParentContext.Depth);
        }

        [Fact]
        public void PA_MED_CTX_07_UnStepAsincronoSigueSinParentContext_NoMedido_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO. La sonda solo registró steps sincrónicos: el ParentContext de un asíncrono no se midió y el doble conserva lo del lote 3 (nulo, PA_PIP_28). Límite declarado en 4.5.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            var asincrono = new PluginEspia("asincrono");
            pipeline.Registrar(asincrono, "Create", TablaZzf, EtapaDeStep.PostOperation, modo: ModoDeStep.Asincrono);

            fabrica.CreateOrganizationService(null).Create(new Entity(TablaZzf));
            pipeline.EjecutarAsincronos();

            Assert.Null(asincrono.Llamadas.Single().Contexto.ParentContext);
        }

        [Fact]
        public void PA_MED_CTX_08_IsInTransactionEsFalsoEnPreValidationYVerdaderoEnPreOperationYPostOperation_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: 41-sonda-resumen.json f_ids (IsInTransaction False, True, True).
            var e = CrearConEspias(new Entity(TablaZzf));

            Assert.False(e.PreValidation.Llamadas.Single().Contexto.IsInTransaction);
            Assert.True(e.PreOperation.Llamadas.Single().Contexto.IsInTransaction);
            Assert.True(e.PostOperation.Llamadas.Single().Contexto.IsInTransaction);
        }
    }

    internal static class EntidadesDeOtraTabla
    {
        /// <summary>La misma entidad (id y atributos) en otra tabla: para armar el <c>Create</c> externo de una prueba sin repetir el helper.</summary>
        public static Entity WithTabla(this Entity entidad, string tabla)
        {
            var copia = new Entity(tabla, entidad.Id);
            foreach (var par in entidad.Attributes) copia[par.Key] = par.Value;
            return copia;
        }
    }
}
