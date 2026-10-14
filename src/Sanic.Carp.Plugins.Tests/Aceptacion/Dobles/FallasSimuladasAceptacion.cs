using System;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <c>FallarEn(operacion, tabla, ordinal, codigoDeError)</c> lanza una <c>FaultException</c> en la n-ésima llamada (diseno/03 4.5, "Fallas"),
    /// y la clave alterna (K-01, K-02). La FORMA de la falla (tipo exterior, <c>ErrorCode</c>, <c>InnerFault</c>) la mide
    /// <c>DobleContraRespuestasRealesAceptacion</c> contra <c>07-fallas.json</c>; aquí se mide CUÁNDO falla y qué deja sin tocar.
    /// </summary>
    public class FallasSimuladasAceptacion
    {
        private const int CodigoCualquiera = unchecked((int)0x80048D19);

        // ---- FallarEn ----

        [Fact]
        public void PA_FAL_01_LaPrimeraLlamadaFallaConElCodigoPedidoSinTocarElAlmacenNiRegistrarlaYLaSiguienteSale()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, 1, CodigoCualquiera);

            var falla = Falla(() => almacen.Create(Zzf(Fila1, "zzf_a", 1)));

            Assert.Equal(CodigoCualquiera, falla.Detail.ErrorCode);
            Assert.Empty(almacen.Todas(TablaZzf));
            Assert.Empty(almacen.Escrituras);
            almacen.Create(Zzf(Fila1, "zzf_a", 1));
            Assert.True(almacen.Existe(TablaZzf, Fila1));
            Assert.Single(almacen.Escrituras);
        }

        [Fact]
        public void PA_FAL_02_ElOrdinalEsLaNesimaLlamadaDeESaOperacionSobreESaTabla()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, 3, CodigoCualquiera);

            almacen.Create(Zzf(Fila1));
            almacen.Create(Fila("zzf_otra", Fila1));      // otra tabla: no cuenta
            almacen.Update(Zzf(Fila1, "zzf_a", 1));       // otra operación: no cuenta
            almacen.Create(Zzf(Fila2));
            var falla = Falla(() => almacen.Create(Zzf(Fila3)));
            almacen.Create(Zzf(Fila3));                   // la cuarta ya sale

            Assert.Equal(CodigoCualquiera, falla.Detail.ErrorCode);
            Assert.Equal(3, almacen.Todas(TablaZzf).Count);
        }

        [Fact]
        public void PA_FAL_03_ElOrdinalSeCuentaDesdeQueSeRegistraLaFalla_NoDesdeQueNacioElAlmacen()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1));
            almacen.Create(Zzf(Fila2));
            almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, 1, CodigoCualquiera);

            Falla(() => almacen.Create(Zzf(Fila3)));

            Assert.Equal(2, almacen.Todas(TablaZzf).Count);
        }

        [Theory]
        [InlineData("Update")]
        [InlineData("Delete")]
        [InlineData("Retrieve")]
        [InlineData("RetrieveMultiple")]
        [InlineData("Assign")]
        public void PA_FAL_04_CadaOperacionPuedeFallar(string nombreDeOperacion)
        {
            var operacion = (OperacionDeDatos)Enum.Parse(typeof(OperacionDeDatos), nombreDeOperacion);
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));
            almacen.FallarEn(operacion, TablaZzf, 1, CodigoCualquiera);

            Action llamada;
            switch (operacion)
            {
                case OperacionDeDatos.Update: llamada = () => almacen.Update(Zzf(Fila1, "zzf_a", 2)); break;
                case OperacionDeDatos.Delete: llamada = () => almacen.Delete(TablaZzf, Fila1); break;
                case OperacionDeDatos.Retrieve: llamada = () => almacen.Retrieve(TablaZzf, Fila1, new ColumnSet(true)); break;
                case OperacionDeDatos.RetrieveMultiple: llamada = () => almacen.RetrieveMultiple(new QueryExpression(TablaZzf)); break;
                default: llamada = () => almacen.Execute(new AssignRequest { Target = new EntityReference(TablaZzf, Fila1), Assignee = new EntityReference("team", ApoyoDeDobles.Team1) }); break;
            }

            Assert.Equal(CodigoCualquiera, Falla(llamada).Detail.ErrorCode);
            Assert.Equal(1, almacen.Leer(TablaZzf, Fila1)["zzf_a"]);
            Assert.False(almacen.Leer(TablaZzf, Fila1).Contains("ownerid"));
        }

        [Fact]
        public void PA_FAL_05_UnUpdateRequestCuentaComoUpdate()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));
            almacen.FallarEn(OperacionDeDatos.Update, TablaZzf, 2, CodigoCualquiera);

            almacen.Update(Zzf(Fila1, "zzf_a", 2));
            Falla(() => almacen.Execute(new UpdateRequest { Target = Zzf(Fila1, "zzf_a", 3) }));

            Assert.Equal(2, almacen.Leer(TablaZzf, Fila1)["zzf_a"]);
        }

        [Fact]
        public void PA_FAL_06_UnaFallaSinTablaSirveParaWhoAmI_YLaConfiguradaEnElAlmacenFallaDesdeCualquierVista()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.FallarEn(OperacionDeDatos.WhoAmI, null, 1, CodigoCualquiera);

            var falla = Falla(() => almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Execute(new WhoAmIRequest()));

            Assert.Equal(CodigoCualquiera, falla.Detail.ErrorCode);
            Assert.IsType<WhoAmIResponse>(almacen.Execute(new WhoAmIRequest()));
        }

        [Fact]
        public void PA_FAL_07_VariasFallasConfiguradasSonIndependientes()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, 1, 111);
            almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, 2, 222);
            almacen.FallarEn(OperacionDeDatos.Create, "zzf_otra", 1, 333);

            Assert.Equal(111, Falla(() => almacen.Create(Zzf(Fila1))).Detail.ErrorCode);
            Assert.Equal(333, Falla(() => almacen.Create(Fila("zzf_otra", Fila1))).Detail.ErrorCode);
            Assert.Equal(222, Falla(() => almacen.Create(Zzf(Fila1))).Detail.ErrorCode);
            almacen.Create(Zzf(Fila1));
            Assert.True(almacen.Existe(TablaZzf, Fila1));
        }

        [Fact]
        public void PA_FAL_08_UnOrdinalMenorQueUnoEsUnErrorDeUso()
        {
            var almacen = new OrganizationServiceEnMemoria();

            Assert.Throws<ArgumentOutOfRangeException>(() => almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, 0, CodigoCualquiera));
            Assert.Throws<ArgumentOutOfRangeException>(() => almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, -1, CodigoCualquiera));
        }

        [Fact]
        public void PA_FAL_09_UnaFallaNoEsUnaExcepcionDeNegocio_NoEsInvalidPluginExecutionException()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, 1, CodigoCualquiera);

            var error = Record.Exception(() => almacen.Create(Zzf(Fila1)));

            Assert.IsNotType<InvalidPluginExecutionException>(error);
            Assert.NotNull(error);
        }

        // ---- Clave alterna ----

        [Fact]
        public void PA_FAL_10_DosFilasConLaMismaClaveAlternaFallanConK01_ElCreateNoDejaNada()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            almacen.Create(Zzf(Fila1, "zzf_huella", "h1"));
            var escrituras = almacen.Escrituras.Count;

            var falla = Falla(() => almacen.Create(Zzf(Fila2, "zzf_huella", "h1", "zzf_a", 1)));

            Assert.Equal(CodigosDeFalla.ClaveDuplicada, falla.Detail.ErrorCode);
            Assert.Equal(unchecked((int)0x80060892), falla.Detail.ErrorCode);
            Assert.False(almacen.Existe(TablaZzf, Fila2));
            Assert.Equal(escrituras, almacen.Escrituras.Count);
            almacen.Create(Zzf(Fila2, "zzf_huella", "h2"));
        }

        [Fact]
        public void PA_FAL_11_LaClaveAlternaCompuestaFallaSoloSiCoincidenTodasLasColumnas()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_producto", "zzf_numero");
            almacen.Create(Zzf(Fila1, "zzf_producto", "p1", "zzf_numero", 1));

            almacen.Create(Zzf(Fila2, "zzf_producto", "p1", "zzf_numero", 2));
            almacen.Create(Zzf(Fila3, "zzf_producto", "p2", "zzf_numero", 1));
            var falla = Falla(() => almacen.Create(Zzf(Guid.NewGuid(), "zzf_producto", "p1", "zzf_numero", 2)));

            Assert.Equal(CodigosDeFalla.ClaveDuplicada, falla.Detail.ErrorCode);
            Assert.Equal(3, almacen.Todas(TablaZzf).Count);
        }

        [Fact]
        public void PA_FAL_12_ElTextoDeLaClaveSeComparaSinDistinguirMayusculas()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            almacen.Create(Zzf(Fila1, "zzf_huella", "ABC"));

            Assert.Equal(CodigosDeFalla.ClaveDuplicada, Falla(() => almacen.Create(Zzf(Fila2, "zzf_huella", "abc"))).Detail.ErrorCode);
        }

        [Fact]
        public void PA_FAL_13_UnaFilaConAlgunaColumnaDeLaClaveAusenteNoParticipa()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_producto", "zzf_numero");

            almacen.Create(Zzf(Fila1, "zzf_producto", "p1"));
            almacen.Create(Zzf(Fila2, "zzf_producto", "p1"));
            almacen.Create(Zzf(Fila3, "zzf_numero", 1));
            almacen.Create(Zzf(Guid.NewGuid(), "zzf_numero", 1));
            almacen.Create(Zzf(Guid.NewGuid()));
            almacen.Create(Zzf(Guid.NewGuid()));

            Assert.Equal(6, almacen.Todas(TablaZzf).Count);
        }

        [Fact]
        public void PA_FAL_14_UnUpdateQueDejariaDosFilasConLaMismaClaveFallaYElMismoRegistroNoChocaConsigo()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            almacen.Create(Zzf(Fila1, "zzf_huella", "h1"));
            almacen.Create(Zzf(Fila2, "zzf_huella", "h2"));

            var falla = Falla(() => almacen.Update(Zzf(Fila2, "zzf_huella", "h1")));
            almacen.Update(Zzf(Fila1, "zzf_huella", "h1", "zzf_a", 5));

            Assert.Equal(CodigosDeFalla.ClaveDuplicada, falla.Detail.ErrorCode);
            Assert.Equal("h2", almacen.Leer(TablaZzf, Fila2)["zzf_huella"]);
            Assert.Equal(5, almacen.Leer(TablaZzf, Fila1)["zzf_a"]);
        }

        [Fact]
        public void PA_FAL_15_LaClaveAlternaEsDeUnaTablaYNoAfectaALasOtras_YUnaFilaBorradaLiberaSuClave()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            almacen.Create(Zzf(Fila1, "zzf_huella", "h1"));

            almacen.Create(Fila("zzf_otra", Fila1, "zzf_huella", "h1"));
            almacen.Delete(TablaZzf, Fila1);
            almacen.Create(Zzf(Fila2, "zzf_huella", "h1"));

            Assert.True(almacen.Existe(TablaZzf, Fila2));
        }

        [Fact]
        public void PA_FAL_16_LaClaveAlternaTambienVigilaLoSembradoComoFilaExistente()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            almacen.Sembrar(Zzf(Fila1, "zzf_huella", "h1"));

            Assert.Equal(CodigosDeFalla.ClaveDuplicada, Falla(() => almacen.Create(Zzf(Fila2, "zzf_huella", "h1"))).Detail.ErrorCode);
        }

        [Fact]
        public void PA_FAL_17_LaFallaPorClaveDuplicadaTambienCuentaParaFallarEnYLasFallasConfiguradasGananAlaClave()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            almacen.Create(Zzf(Fila1, "zzf_huella", "h1"));
            almacen.FallarEn(OperacionDeDatos.Create, TablaZzf, 1, CodigoCualquiera);

            var falla = Falla(() => almacen.Create(Zzf(Fila2, "zzf_huella", "h1")));

            Assert.Equal(CodigoCualquiera, falla.Detail.ErrorCode);
            Assert.Equal(CodigosDeFalla.ClaveDuplicada, Falla(() => almacen.Create(Zzf(Fila2, "zzf_huella", "h1"))).Detail.ErrorCode);
        }
    }
}
