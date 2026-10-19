using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary>
    /// <c>LecturasDeCorreoDataverse.ColumnasDelRemitente</c> (<see cref="ILecturasDelRemitente"/>; diseno/03 2.9 y "Decisiones del arquitecto, 2026-10-19 (cierre del sublote 5f)", punto 2): el valor guardado de
    /// <c>directioncode</c> y <c>regardingobjectid</c> con el servicio que se le dé, para S-37. IDs: <c>PA5F_LRD_*</c>.
    /// </summary>
    public class LecturasDelRemitenteDataverseAceptacion
    {
        private const string ColDireccion = "directioncode";
        private const string ColRegarding = "regardingobjectid";
        private const string TablaConversacion = TablasDelArea.Conversacion;

        private static LecturasDeCorreoDataverse Sobre(OrganizationServiceEnMemoria servicio)
        {
            return new LecturasDeCorreoDataverse(() => servicio);
        }

        private static void AssertLasDosClaves(IReadOnlyDictionary<string, object> valores)
        {
            Assert.Equal(2, valores.Count);
            Assert.True(valores.ContainsKey(ColDireccion));
            Assert.True(valores.ContainsKey(ColRegarding));
        }

        [Fact]
        public void PA5F_LRD_01_UnSalienteConRegardingAUnaConversacion_DevuelveElBoolYLaReferenciaNeutra()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = Guid.NewGuid();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Con(ColRegarding, Ref(TablaConversacion, conversacion)));

            var valores = Sobre(almacen).ColumnasDelRemitente(id);

            AssertLasDosClaves(valores);
            Assert.True(Assert.IsType<bool>(valores[ColDireccion]));
            var regarding = Assert.IsType<ReferenciaDeEntidad>(valores[ColRegarding]);
            Assert.Equal(TablaConversacion, regarding.Tabla);
            Assert.Equal(conversacion, regarding.Id);
        }

        [Fact]
        public void PA5F_LRD_02_UnEntranteSinRegarding_DevuelveFalsoYNuloConLasDosClaves()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoEntrante());

            var valores = Sobre(almacen).ColumnasDelRemitente(id);

            AssertLasDosClaves(valores);
            Assert.False(Assert.IsType<bool>(valores[ColDireccion]));
            Assert.Null(valores[ColRegarding]);
        }

        [Fact]
        public void PA5F_LRD_03_SinLaColumnaDeDireccion_LaClaveLlegaEnNulo()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Sin(ColDireccion));

            var valores = Sobre(almacen).ColumnasDelRemitente(id);

            AssertLasDosClaves(valores);
            Assert.Null(valores[ColDireccion]);
        }

        [Fact]
        public void PA5F_LRD_04_UnCorreoQueNoExiste_DevuelveLasDosClavesEnNulo_SinLanzar()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var valores = Sobre(almacen).ColumnasDelRemitente(Guid.NewGuid());

            AssertLasDosClaves(valores);
            Assert.Null(valores[ColDireccion]);
            Assert.Null(valores[ColRegarding]);
        }

        [Fact]
        public void PA5F_LRD_05_PideUnaSolaVez_SoloEsasDosColumnas_PorId()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());
            almacen.LimpiarRegistros();

            Sobre(almacen).ColumnasDelRemitente(id);

            var consulta = Assert.Single(almacen.Consultas);
            Assert.Equal(OperacionDeDatos.Retrieve, consulta.Operacion);
            Assert.Equal(Correo, consulta.Tabla);
            Assert.Equal(id, consulta.Id);
            AssertColumnas(consulta, ColDireccion, ColRegarding);
        }

        [Fact]
        public void PA5F_LRD_06_SinAccesoAlCorreo_DevuelveLasDosClavesEnNulo_SinLanzar()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen);
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());
            almacen.NegarLectura(persona, Correo, id);

            var valores = Sobre(almacen.ComoUsuario(persona)).ColumnasDelRemitente(id);

            AssertLasDosClaves(valores);
            Assert.Null(valores[ColDireccion]);
            Assert.Null(valores[ColRegarding]);
        }

        [Fact]
        public void PA5F_LRD_07_LaFuncionNoSeInvocaAlConstruir_YSeInvocaAlLeer()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());
            var invocaciones = 0;

            var lecturas = new LecturasDeCorreoDataverse(() =>
            {
                invocaciones++;
                return almacen;
            });
            Assert.Equal(0, invocaciones);

            lecturas.ColumnasDelRemitente(id);

            Assert.Equal(1, invocaciones);
        }

        [Fact]
        public void PA5F_LRD_08_UnaFallaDistintaDeNoExisteOSinAcceso_Escapa()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());
            almacen.FallarEn(OperacionDeDatos.Retrieve, Correo, 1, CodigosDeFalla.TransaccionAbortada);

            AssertFalla(Falla(() => Sobre(almacen).ColumnasDelRemitente(id)), CodigosDeFalla.TransaccionAbortada);
        }

        [Fact]
        public void PA5F_LRD_09_LeeConElServicioQueDevuelveLaFuncion_NoConOtro()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen);
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());
            almacen.LimpiarRegistros();

            Sobre(almacen.ComoUsuario(persona)).ColumnasDelRemitente(id);

            Assert.Equal(persona, Assert.Single(almacen.Consultas).Identidad);
        }

        [Fact]
        // VERDE A PROPÓSITO: el adaptador implementa los dos puertos de lectura del correo (el mismo objeto sirve a S-33 y a S-37) y la Func nula ya se valida en el constructor.
        public void PA5F_LRD_10_ElAdaptadorImplementaLosDosPuertos_YLaFuncionNulaLanza()
        {
            var lecturas = Sobre(new OrganizationServiceEnMemoria());

            Assert.IsAssignableFrom<ILecturasDelRemitente>(lecturas);
            Assert.IsAssignableFrom<ILecturasDeCorreo>(lecturas);
            Assert.Throws<ArgumentNullException>(() => new LecturasDeCorreoDataverse(null));
        }
    }
}
