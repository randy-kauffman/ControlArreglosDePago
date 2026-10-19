using System;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary><c>UsuariosDataverse</c> (diseno/03 4.2, 1.1 tabla de datos, 1.3 paso 5 y PA-TOM-04).</summary>
    public class UsuariosDataverseAceptacion
    {
        [Fact]
        public void PA_USD_01_NombreCompleto_DevuelveElFullname_ConUnRetrieveQuePideSoloEsaColumna()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnUsuario(Guid.NewGuid()).Con("fullname", "ZZF Ana PRUEBA"));

            var nombre = new UsuariosDataverse(almacen).NombreCompleto(id);

            Assert.Equal("ZZF Ana PRUEBA", nombre);
            var consulta = Assert.Single(almacen.Consultas);
            Assert.Equal(Usuario, consulta.Tabla);
            AssertColumnas(consulta, "fullname");
        }

        [Fact]
        public void PA_USD_02_NombreCompleto_SinFilaSinAccesoOSinNombre_DevuelveNull()
        {
            // PA-TOM-04: el nombre ilegible para el llamador es "otro oficial", no una excepción.
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen, UsuarioA);
            var ilegible = Sembrar(almacen, ConstructorDeDatos.UnUsuario(Guid.NewGuid()));
            var sinNombre = Sembrar(almacen, ConstructorDeDatos.UnUsuario(Guid.NewGuid()).Sin("fullname"));
            almacen.NegarLectura(persona, Usuario, ilegible);
            var repo = new UsuariosDataverse(almacen.ComoUsuario(persona));

            Assert.Null(repo.NombreCompleto(Guid.NewGuid()));
            Assert.Null(repo.NombreCompleto(ilegible));
            Assert.Null(repo.NombreCompleto(sinNombre));
        }

        [Fact]
        public void PA_USD_03_NombreCompleto_UnaFallaDistintaEscapa_YLaIdentidadEsLaDelServicioRecibido()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen, UsuarioA);
            var id = SembrarUsuario(almacen);
            var repo = new UsuariosDataverse(almacen.ComoUsuario(persona));

            repo.NombreCompleto(id);
            almacen.FallarEn(OperacionDeDatos.Retrieve, Usuario, 1, CodigosDeFalla.TransaccionAbortada);

            AssertFalla(Falla(() => repo.NombreCompleto(id)), CodigosDeFalla.TransaccionAbortada);
            Assert.All(almacen.Consultas, c => Assert.Equal(persona, c.Identidad));
        }
    }
}
