using System;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// <c>AccesosDeFilaDataverse</c> / <c>IAccesosDeFila.PuedeEscribir</c> (lote 6, vuelta 2, G1; diseno/03 1.1 paso 6, V-7): ¿tiene el usuario <c>WriteAccess</c> sobre una fila? PA6_ACC_01 a _05. El doble responde
    /// <c>RetrievePrincipalAccess</c> con lo que se le configura con <c>ConfigurarAcceso(usuario, tabla, id, derechos)</c> y <c>AccessRights.None</c> si no se configuró nada: las pruebas configuran UNA combinación
    /// y comprueban que solo ella responde que sí (así se mide que el <c>Target</c> y el <c>Principal</c> del pedido son los que son).
    /// </summary>
    public class AccesosDeFilaAceptacion
    {
        private const string Tabla = "sanic_carp_tbl_versionplantilla";

        [Theory]
        [InlineData((int)AccessRights.WriteAccess)]
        [InlineData((int)(AccessRights.ReadAccess | AccessRights.WriteAccess))]
        [InlineData((int)(AccessRights.WriteAccess | AccessRights.AppendAccess | AccessRights.ShareAccess))]
        public void PA6_ACC_01_ConWriteAccessEntreLosDerechos_PuedeEscribir(int derechos)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var usuario = Guid.NewGuid();
            var fila = Guid.NewGuid();
            almacen.ConfigurarAcceso(usuario, Tabla, fila, (AccessRights)derechos);

            Assert.True(new AccesosDeFilaDataverse(almacen).PuedeEscribir(Tabla, fila, usuario));
        }

        [Theory]
        [InlineData((int)AccessRights.None)]
        [InlineData((int)AccessRights.ReadAccess)]
        [InlineData((int)(AccessRights.ReadAccess | AccessRights.DeleteAccess | AccessRights.ShareAccess | AccessRights.AppendAccess))]
        public void PA6_ACC_02_SinWriteAccessEntreLosDerechos_NoPuedeEscribir(int derechos)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var usuario = Guid.NewGuid();
            var fila = Guid.NewGuid();
            almacen.ConfigurarAcceso(usuario, Tabla, fila, (AccessRights)derechos);

            Assert.False(new AccesosDeFilaDataverse(almacen).PuedeEscribir(Tabla, fila, usuario));
        }

        [Fact]
        public void PA6_ACC_02b_SinNingunaConfiguracion_NoPuedeEscribir()
        {
            var almacen = new OrganizationServiceEnMemoria();

            Assert.False(new AccesosDeFilaDataverse(almacen).PuedeEscribir(Tabla, Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public void PA6_ACC_03_PreguntaPorLaFilaYElUsuarioQueSeLeDan_NoPorOtros()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var usuario = Guid.NewGuid();
            var otroUsuario = Guid.NewGuid();
            var fila = Guid.NewGuid();
            var otraFila = Guid.NewGuid();
            almacen.ConfigurarAcceso(usuario, Tabla, fila, AccessRights.WriteAccess);
            var accesos = new AccesosDeFilaDataverse(almacen);

            Assert.True(accesos.PuedeEscribir(Tabla, fila, usuario));
            Assert.False(accesos.PuedeEscribir(Tabla, fila, otroUsuario));                 // otro usuario sobre la misma fila
            Assert.False(accesos.PuedeEscribir(Tabla, otraFila, usuario));                 // el mismo usuario sobre otra fila
            Assert.False(accesos.PuedeEscribir("sanic_carp_tbl_arreglo", fila, usuario));  // la misma fila de otra tabla
            Assert.False(accesos.PuedeEscribir(Tabla, usuario, fila));                     // el id y el usuario cambiados de lugar
        }

        [Fact]
        public void PA6_ACC_03b_HaceUnaSolaConsultaDeAcceso_SinLecturasNiEscrituras()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var usuario = Guid.NewGuid();
            var fila = Guid.NewGuid();
            almacen.ConfigurarAcceso(usuario, Tabla, fila, AccessRights.WriteAccess);
            // Una falla en la SEGUNDA llamada: si PuedeEscribir pidiera el acceso dos veces, la primera pregunta fallaría.
            almacen.FallarEn(OperacionDeDatos.RetrievePrincipalAccess, Tabla, 2, CodigosDeFalla.FilaInexistente);

            Assert.True(new AccesosDeFilaDataverse(almacen).PuedeEscribir(Tabla, fila, usuario));
            Assert.Empty(almacen.Consultas);
            Assert.Empty(almacen.Escrituras);
        }

        [Theory]
        [InlineData(CodigosDeFalla.FilaInexistente)]
        [InlineData(CodigosDeFalla.SinAcceso)]
        [InlineData(CodigosDeFalla.SolicitudInvalida)]
        public void PA6_ACC_04_UnaFallaDeLaPlataforma_EscapaTalCual_NoSeAtrapaNiElNoExiste(int codigo)
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.FallarEn(OperacionDeDatos.RetrievePrincipalAccess, Tabla, 1, codigo);
            var accesos = new AccesosDeFilaDataverse(almacen);

            var falla = Assert.Throws<FaultException<OrganizationServiceFault>>(() => accesos.PuedeEscribir(Tabla, Guid.NewGuid(), Guid.NewGuid()));

            Assert.Equal(codigo, falla.Detail.ErrorCode);
        }

        [Fact]
        public void PA6_ACC_05_LosArgumentosVaciosSeRechazanConArgumentException_YUnServicioNuloConArgumentNull()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var accesos = new AccesosDeFilaDataverse(almacen);
            var fila = Guid.NewGuid();
            var usuario = Guid.NewGuid();

            Assert.ThrowsAny<ArgumentException>(() => accesos.PuedeEscribir(null, fila, usuario));
            Assert.ThrowsAny<ArgumentException>(() => accesos.PuedeEscribir(string.Empty, fila, usuario));
            Assert.ThrowsAny<ArgumentException>(() => accesos.PuedeEscribir(Tabla, Guid.Empty, usuario));
            Assert.ThrowsAny<ArgumentException>(() => accesos.PuedeEscribir(Tabla, fila, Guid.Empty));
            Assert.Throws<ArgumentNullException>(() => new AccesosDeFilaDataverse(null));
        }
    }
}
