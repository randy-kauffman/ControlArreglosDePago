using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary>
    /// <c>LecturasDeCorreoDataverse</c> (diseno/03 2.9 y "Decisiones del arquitecto, 2026-10-19 (sublote 5f)", punto 1): el valor guardado de <c>sanic_tiemporespuesta</c> y <c>sanic_respondidoporid</c> con el
    /// servicio que se le dé. IDs: <c>PA5F_LCD_*</c>.
    /// </summary>
    public class LecturasDeCorreoDataverseAceptacion
    {
        private const string ColTiempo = "sanic_tiemporespuesta";
        private const string ColRespondido = "sanic_respondidoporid";

        private static LecturasDeCorreoDataverse Sobre(OrganizationServiceEnMemoria servicio)
        {
            return new LecturasDeCorreoDataverse(() => servicio);
        }

        private static void AssertLasDosClaves(IReadOnlyDictionary<string, object> valores)
        {
            Assert.Equal(2, valores.Count);
            Assert.True(valores.ContainsKey(ColTiempo));
            Assert.True(valores.ContainsKey(ColRespondido));
        }

        [Fact]
        public void PA5F_LCD_01_ConLasDosColumnas_DevuelveElEnteroYLaReferenciaNeutra()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var oficial = Guid.NewGuid();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Con(ColTiempo, 45).Con(ColRespondido, Ref(Usuario, oficial)));

            var valores = Sobre(almacen).ColumnasDelSistema(id);

            AssertLasDosClaves(valores);
            Assert.Equal(45, Assert.IsType<int>(valores[ColTiempo]));
            var respondido = Assert.IsType<ReferenciaDeEntidad>(valores[ColRespondido]);
            Assert.Equal(Usuario, respondido.Tabla);
            Assert.Equal(oficial, respondido.Id);
        }

        [Fact]
        public void PA5F_LCD_02_SinNingunaColumna_DevuelveLasDosClavesEnNulo()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());

            var valores = Sobre(almacen).ColumnasDelSistema(id);

            AssertLasDosClaves(valores);
            Assert.Null(valores[ColTiempo]);
            Assert.Null(valores[ColRespondido]);
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        public void PA5F_LCD_03_ConUnaSolaColumna_LaOtraLlegaEnNulo(string columna)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var oficial = Guid.NewGuid();
            var fila = ConstructorDeDatos.UnCorreoSaliente();
            if (columna == "tiempo") fila.Con(ColTiempo, 12);
            else fila.Con(ColRespondido, Ref(Usuario, oficial));
            var id = Sembrar(almacen, fila);

            var valores = Sobre(almacen).ColumnasDelSistema(id);

            AssertLasDosClaves(valores);
            if (columna == "tiempo")
            {
                Assert.Equal(12, valores[ColTiempo]);
                Assert.Null(valores[ColRespondido]);
            }
            else
            {
                Assert.Null(valores[ColTiempo]);
                Assert.Equal(oficial, Assert.IsType<ReferenciaDeEntidad>(valores[ColRespondido]).Id);
            }
        }

        [Fact]
        public void PA5F_LCD_04_UnCorreoQueNoExiste_DevuelveLasDosClavesEnNulo_SinLanzar()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var valores = Sobre(almacen).ColumnasDelSistema(Guid.NewGuid());

            AssertLasDosClaves(valores);
            Assert.Null(valores[ColTiempo]);
            Assert.Null(valores[ColRespondido]);
        }

        [Fact]
        public void PA5F_LCD_05_PideUnaSolaVez_SoloEsasDosColumnas_PorId()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Con(ColTiempo, 3));
            almacen.LimpiarRegistros();

            Sobre(almacen).ColumnasDelSistema(id);

            var consulta = Assert.Single(almacen.Consultas);
            Assert.Equal(OperacionDeDatos.Retrieve, consulta.Operacion);
            Assert.Equal(Correo, consulta.Tabla);
            Assert.Equal(id, consulta.Id);
            AssertColumnas(consulta, ColTiempo, ColRespondido);
        }

        [Fact]
        public void PA5F_LCD_06_SinAccesoAlCorreo_DevuelveLasDosClavesEnNulo_SinLanzar()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen);
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Con(ColTiempo, 3));
            almacen.NegarLectura(persona, Correo, id);
            var comoPersona = almacen.ComoUsuario(persona);

            var valores = Sobre(comoPersona).ColumnasDelSistema(id);

            AssertLasDosClaves(valores);
            Assert.Null(valores[ColTiempo]);
            Assert.Null(valores[ColRespondido]);
        }

        [Fact]
        public void PA5F_LCD_07_LaFuncionNoSeInvocaAlConstruir_YSeInvocaAlLeer()
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

            lecturas.ColumnasDelSistema(id);

            Assert.Equal(1, invocaciones);
        }

        [Fact]
        public void PA5F_LCD_08_LaFuncionNula_Lanza()
        {
            // VERDE A PROPÓSITO: el constructor ya valida.
            Assert.Throws<ArgumentNullException>(() => new LecturasDeCorreoDataverse(null));
        }

        [Fact]
        public void PA5F_LCD_09_UnaFallaDistintaDeNoExisteOSinAcceso_Escapa()
        {
            // El puerto: "una falla de lectura distinta de 'no existe' o 'sin acceso' escapa".
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Con(ColTiempo, 3));
            almacen.FallarEn(OperacionDeDatos.Retrieve, Correo, 1, CodigosDeFalla.TransaccionAbortada);

            AssertFalla(Falla(() => Sobre(almacen).ColumnasDelSistema(id)), CodigosDeFalla.TransaccionAbortada);
        }

        [Fact]
        public void PA5F_LCD_10_LeeConElServicioQueDevuelveLaFuncion_NoConOtro()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen);
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Con(ColTiempo, 3));
            almacen.LimpiarRegistros();

            Sobre(almacen.ComoUsuario(persona)).ColumnasDelSistema(id);

            Assert.Equal(persona, Assert.Single(almacen.Consultas).Identidad);
        }
    }
}
