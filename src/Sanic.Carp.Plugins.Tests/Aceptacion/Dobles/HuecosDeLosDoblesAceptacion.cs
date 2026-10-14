using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// Huecos que apareció al escribir una implementación de referencia de los dobles en una copia (lote 3, verificación del arquitecto, 2026-10-14): cada prueba mata una implementación
    /// plausible y equivocada que las demás pruebas de <c>Aceptacion/Dobles</c> dejaban pasar. Los nombres citan la mutación que matan (H1 a H11 del informe de la verificación; la H7,
    /// que dependía de la zona horaria de la máquina, se descartó).
    /// </summary>
    public class HuecosDeLosDoblesAceptacion
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

        // H1: una excepcion de PreValidation NO restaura (aun no hay transaccion): lo que un step de PreValidation escribio antes de lanzar queda.
        [Fact]
        public void PA_HDD_01_UnaExcepcionDePreValidationNoRestauraLoQueEscribioUnStepAnterior()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            pipeline.Registrar(new PluginEspia("escribe", accion: l => Elevado(l).Create(Fila("zzf_otra", Fila2))), "Create", TablaZzf, EtapaDeStep.PreValidation, orden: 1);
            pipeline.Registrar(new PluginEspia("falla", accion: l => { throw new InvalidPluginExecutionException("CARP-XXX-001"); }), "Create", TablaZzf, EtapaDeStep.PreValidation, orden: 2);

            Assert.Throws<InvalidPluginExecutionException>(() => fabrica.CreateOrganizationService(null).Create(Zzf(Fila1)));

            Assert.True(fabrica.Almacen.Existe("zzf_otra", Fila2), "PreValidation no tiene transaccion: lo escrito antes de la excepcion queda");
            Assert.False(fabrica.Almacen.Existe(TablaZzf, Fila1));
        }

        // H2: dos operaciones consecutivas no comparten SharedVariables.
        [Fact]
        public void PA_HDD_02_DosOperacionesSeguidasNoComparten_SharedVariables()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            object vistoEnLaSegunda = "sin ejecutar";
            int llamadas = 0;
            pipeline.Registrar(new PluginEspia("pre", accion: l =>
            {
                llamadas++;
                if (llamadas == 1) l.Contexto.SharedVariables["clave"] = "de la primera";
                else l.Contexto.SharedVariables.TryGetValue("clave", out vistoEnLaSegunda);
            }), "Create", TablaZzf, EtapaDeStep.PreOperation);
            var servicio = fabrica.CreateOrganizationService(null);

            servicio.Create(Zzf(Fila1));
            servicio.Create(Zzf(Fila2));

            Assert.Equal(2, llamadas);
            Assert.Null(vistoEnLaSegunda);
        }

        // H3: la pre-imagen se toma al INICIO de la operacion, antes de que un PreValidation (o PreOperation previo) cambie la fila.
        [Fact]
        public void PA_HDD_03_LaPreImagenSeTomaAlInicioAunqueUnStepAnteriorCambieLaFila()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            fabrica.Almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));
            pipeline.Registrar(new PluginEspia("cambia la fila", accion: l =>
            {
                if (l.Contexto.Depth == 1) Elevado(l).Update(Zzf(Fila1, "zzf_a", 100));
            }), "Update", TablaZzf, EtapaDeStep.PreValidation);
            var conImagen = new PluginEspia("con imagen");
            pipeline.Registrar(conImagen, "Update", TablaZzf, EtapaDeStep.PreOperation, nombreDeImagenPrevia: "PreImagen", columnasDeImagenPrevia: new[] { "zzf_a" });

            fabrica.CreateOrganizationService(null).Update(Zzf(Fila1, "zzf_b", 2));

            Assert.Equal(1, conImagen.Llamadas.Single(c => c.Contexto.Depth == 1).Contexto.PreEntityImages["PreImagen"]["zzf_a"]);
        }

        // H4: el orden de los empates es DETERMINISTA con mas de 16 filas empatadas (List.Sort deja de ser estable por encima de 16 elementos).
        // REESCRITA en la vuelta 2 (decision del orquestador): el empate va en orden INVERSO al de entrada (antes: el de entrada); sigue siendo exacto con 60 filas, asi que un
        // List.Sort inestable lo rompe igual (Learn, "Page results using QueryExpression": un orden sin identificador unico no garantiza el orden de los empates).
        [Fact]
        public void PA_HDD_04_ElEmpateDeOrdenVaEnOrdenInversoAlDeEntradaConMasDeDieciseisFilas()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var ids = Enumerable.Range(0, 60).Select(_ => Guid.NewGuid()).ToArray();
            almacen.Sembrar(ids.Select(id => Zzf(id, "zzf_grupo", 1)).ToArray());
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.AddOrder("zzf_grupo", OrderType.Ascending);

            var inversos = ids.Reverse().ToArray();
            Assert.Equal(inversos, almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).ToArray());
            consulta.Orders[0].OrderType = OrderType.Descending;
            Assert.Equal(inversos, almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).ToArray());
        }

        // H5: dos Construir() no comparten ninguno de los objetos mutables (tambien OptionSetValue).
        [Fact]
        public void PA_HDD_05_ConstruirNoComparteOptionSetValueEntreFilas()
        {
            var constructor = ConstructorDeDatos.UnArreglo();
            var a = constructor.Construir();
            var b = constructor.Construir();

            ((OptionSetValue)a["sanic_producto"]).Value = 1;
            ((OptionSetValue)a["statecode"]).Value = 1;

            Assert.NotEqual(1, ((OptionSetValue)b["sanic_producto"]).Value);
            Assert.Equal(0, ((OptionSetValue)b["statecode"]).Value);
        }

        // H6: los datos que son clave de negocio no se repiten entre instancias (cedula, numero de producto, cuenta).
        [Fact]
        public void PA_HDD_06_DosFilasDelMismoConstructorNoRepitenLosDatosQueSonClave()
        {
            Assert.NotEqual(ConstructorDeDatos.UnCliente().Construir()["sanic_cedula"], ConstructorDeDatos.UnCliente().Construir()["sanic_cedula"]);
            Assert.NotEqual(ConstructorDeDatos.UnArreglo().Construir()["sanic_numeroproducto"], ConstructorDeDatos.UnArreglo().Construir()["sanic_numeroproducto"]);
            Assert.NotEqual(ConstructorDeDatos.UnaCuota().Construir()["sanic_nombre"], ConstructorDeDatos.UnaCuota().Construir()["sanic_nombre"]);
            Assert.NotEqual(((EntityReference)ConstructorDeDatos.UnaCuota().Construir()["sanic_arregloid"]).Id, ((EntityReference)ConstructorDeDatos.UnaCuota().Construir()["sanic_arregloid"]).Id);
        }

        // H8: borrar una fila borra sus archivos (si no, el token de un archivo de una fila inexistente sigue sirviendo y SembrarArchivo de otro id se pisa).
        [Fact]
        public void PA_HDD_08_UnaFilaBorradaYVueltaACrearNoConservaElArchivoDeLaAnterior()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1));
            almacen.SembrarArchivo(TablaZzf, Fila1, "zzf_archivo", "a.xlsx", new byte[] { 1 });
            almacen.Delete(TablaZzf, Fila1);
            almacen.Create(Zzf(Fila1));

            var falla = Falla(() => almacen.Execute(new Microsoft.Crm.Sdk.Messages.InitializeFileBlocksDownloadRequest
            {
                Target = new EntityReference(TablaZzf, Fila1),
                FileAttributeName = "zzf_archivo"
            }));

            Assert.Equal(CodigosDeFalla.SolicitudInvalida, falla.Detail.ErrorCode);
        }

        // H9: lo que un asincrono escribio ANTES de lanzar queda (un asincrono no revierte nada).
        [Fact]
        public void PA_HDD_09_UnAsincronoQueEscribeYDespuesLanzaDejaSuEscrituraEnElAlmacen()
        {
            var fabrica = NuevaFabrica(out var pipeline);
            pipeline.Registrar(new PluginEspia("asincrono", accion: l =>
            {
                Elevado(l).Create(Fila("zzf_otra", Fila2));
                throw new InvalidOperationException("falla despues de escribir");
            }), "Create", TablaZzf, EtapaDeStep.PostOperation, modo: ModoDeStep.Asincrono);
            fabrica.CreateOrganizationService(null).Create(Zzf(Fila1));

            pipeline.EjecutarAsincronos();

            Assert.True(fabrica.Almacen.Existe("zzf_otra", Fila2), "un asincrono no revierte nada");
            Assert.NotNull(pipeline.Ejecuciones.Last().Excepcion);
        }

        // H10: una columna oculta vale como nula TAMBIEN para ordenar (no solo para filtrar): ordenar por ella no filtra el valor real.
        [Fact]
        public void PA_HDD_10_OrdenarPorUnaColumnaOcultaNoRevelaSuValorReal()
        {
            var almacen = new OrganizationServiceEnMemoria();
            // Orquestador, 2026-10-14 (vuelta 2): la columna oculta vale nula también para ordenar, así que para Usuario1 las dos filas
            // empatan y salen en orden adversario (inverso al de entrada, 4.5 "Paginación"). Se siembran con el valor real ascendente
            // en el orden de entrada, para que el orden adversario sea el CONTRARIO al que revelaría el valor real.
            almacen.Sembrar(Zzf(Fila1, "zzf_cedula", "A"), Zzf(Fila2, "zzf_cedula", "B"));
            almacen.OcultarColumna(TablaZzf, "zzf_cedula", ApoyoDeDobles.Usuario1);
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.AddOrder("zzf_cedula", OrderType.Ascending);

            Assert.Equal(new[] { Fila2, Fila1 }, almacen.ComoUsuario(ApoyoDeDobles.Usuario1).RetrieveMultiple(consulta).Entities.Select(e => e.Id).ToArray());
            Assert.Equal(new[] { Fila1, Fila2 }, almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).ToArray());
        }

        // H11: las filas negadas no cuentan para TopCount, ni para el total, ni para MoreRecords (la plataforma filtra antes de paginar).
        [Fact]
        public void PA_HDD_11_LasFilasNegadasNoCuentanParaTopCountNiParaElTotalNiParaMoreRecords()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1), Zzf(Fila2), Zzf(Fila3));
            almacen.NegarLectura(ApoyoDeDobles.Usuario1, TablaZzf, Fila1);
            var vista = almacen.ComoUsuario(ApoyoDeDobles.Usuario1);

            // Vuelta 2 (hallazgo 4): sin OrderExpression el orden del doble es adversario; estas dos consultas ordenan por el id para medir QUÉ filas salen.
            var primeraConsulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet(), TopCount = 1 };
            primeraConsulta.AddOrder("zzf_filaid", OrderType.Ascending);
            var primera = vista.RetrieveMultiple(primeraConsulta);
            var consultaDePagina = new QueryExpression(TablaZzf)
            {
                ColumnSet = new ColumnSet(),
                PageInfo = new PagingInfo { Count = 2, PageNumber = 1, ReturnTotalRecordCount = true }
            };
            consultaDePagina.AddOrder("zzf_filaid", OrderType.Ascending);
            var pagina = vista.RetrieveMultiple(consultaDePagina);

            Assert.Equal(new[] { Fila2 }, primera.Entities.Select(e => e.Id).ToArray());
            Assert.Equal(new[] { Fila2, Fila3 }, pagina.Entities.Select(e => e.Id).ToArray());
            Assert.Equal(2, pagina.TotalRecordCount);
            Assert.False(pagina.MoreRecords);
        }
    }
}
