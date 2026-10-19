using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary><c>ClientesDataverse(llamador, sistema)</c> (diseno/03 4.2, 2.2, 2.10 paso 5, DC-14, DC-22 y spike 4).</summary>
    public class ClientesDataverseAceptacion
    {
        private static ConstructorDeFila Inactivo(ConstructorDeFila cliente)
        {
            return cliente.Con("statecode", new OptionSetValue(Estados.Inactivo)).Con("statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo)));
        }

        [Fact]
        public void PA_CLD_01_Leer_UsaElServicioDelLlamador_SinElPerfilLaCedulaNoViene_YPideSoloLoPedido()
        {
            // 1.1 paso 5 y spike 4: un usuario sin perfil de columna recibe la fila sin la cédula.
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen, UsuarioA);
            var id = Sembrar(almacen, ConstructorDeDatos.UnCliente().Con("sanic_cedula", "001-000000-0001A").Con("sanic_celular", "8888-0000"));
            almacen.OcultarColumna(Cliente, "sanic_cedula", persona);

            var delLlamador = new ClientesDataverse(almacen.ComoUsuario(persona), almacen).Leer(id, Cols("sanic_cedula", "sanic_celular"));
            var delSistema = new ClientesDataverse(almacen, almacen).Leer(id, Cols("sanic_cedula", "sanic_celular"));

            Assert.False(delLlamador.Valores.ContainsKey("sanic_cedula"));
            Assert.Equal("8888-0000", delLlamador.Valores["sanic_celular"]);
            Assert.Equal("001-000000-0001A", delSistema.Valores["sanic_cedula"]);
            Assert.Equal(persona, almacen.Consultas[0].Identidad);
            Assert.Equal(almacen.IdDeSystem, almacen.Consultas[1].Identidad);
            Assert.All(almacen.Consultas, c => AssertColumnas(c, "sanic_cedula", "sanic_celular"));
        }

        [Fact]
        public void PA_CLD_02_Leer_SinFilaOSinAcceso_DevuelveNull()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen, UsuarioA);
            var id = SembrarCliente(almacen);
            almacen.NegarLectura(persona, Cliente, id);
            var repo = new ClientesDataverse(almacen.ComoUsuario(persona), almacen);

            Assert.Null(repo.Leer(id, Cols("sanic_celular")));
            Assert.Null(repo.Leer(Guid.NewGuid(), Cols("sanic_celular")));
        }

        [Fact]
        public void PA_CLD_03_BuscarPorCedula_UsaSiempreElServicioDeSystem_YEncuentraLaCedulaQueElLlamadorNoVe()
        {
            // DC-22 y PA-CLI-05: con el servicio del llamador la cédula protegida vale nula y el duplicado pasaría en silencio.
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen, UsuarioA);
            var existente = Sembrar(almacen, ConstructorDeDatos.UnCliente().Con("sanic_cedula", "001-000000-0001A"));
            almacen.OcultarColumna(Cliente, "sanic_cedula", persona);
            var repo = new ClientesDataverse(almacen.ComoUsuario(persona), almacen);

            var ids = repo.BuscarPorCedula("001-000000-0001A");

            Assert.Equal(existente, Assert.Single(ids));
            var consulta = Assert.Single(almacen.Consultas);
            Assert.Equal(almacen.IdDeSystem, consulta.Identidad);
            Assert.Equal(2, consulta.TopCount);
            AssertColumnas(consulta);
        }

        [Fact]
        public void PA_CLD_04_BuscarPorCedula_IncluyeInactivosYDevuelveHastaDos_ConLaCedulaExacta()
        {
            // 2.2: "activo o inactivo"; excluir el propio registro lo hace el llamador, por eso TopCount 2.
            var almacen = new OrganizationServiceEnMemoria();
            var activo = Sembrar(almacen, ConstructorDeDatos.UnCliente().Con("sanic_cedula", "001-000000-0001A"));
            var inactivo = Sembrar(almacen, Inactivo(ConstructorDeDatos.UnCliente().Con("sanic_cedula", "001-000000-0001A")));
            Sembrar(almacen, ConstructorDeDatos.UnCliente().Con("sanic_cedula", "001-000000-0002B"));
            var repo = new ClientesDataverse(almacen, almacen);

            Assert.Equal(new[] { activo, inactivo }.OrderBy(x => x).ToList(), repo.BuscarPorCedula("001-000000-0001A").OrderBy(x => x).ToList());
            Assert.Single(repo.BuscarPorCedula("001-000000-0002B"));
            Assert.Empty(repo.BuscarPorCedula("001-000000-9999Z"));
        }

        [Fact]
        public void PA_CLD_05_BuscarPorCedula_NulaOVacia_Lanza_SinConsultar()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var repo = new ClientesDataverse(almacen, almacen);

            Assert.Throws<ArgumentException>(() => repo.BuscarPorCedula(null));
            Assert.Throws<ArgumentException>(() => repo.BuscarPorCedula(""));
            Assert.Empty(almacen.Consultas);
        }

        [Fact]
        public void PA_CLD_06_BuscarActivosPorCorreo_SoloActivos_ConElTopeComoTopCount_YElServicioDelLlamador()
        {
            // DC-14: exactamente un cliente ACTIVO con ese correo; el llamador pide tope 2 para distinguir uno de varios.
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen, UsuarioA);
            var a = Sembrar(almacen, ConstructorDeDatos.UnCliente().Con("sanic_correo", "ana@example.com"));
            var b = Sembrar(almacen, ConstructorDeDatos.UnCliente().Con("sanic_correo", "ana@example.com"));
            Sembrar(almacen, Inactivo(ConstructorDeDatos.UnCliente().Con("sanic_correo", "ana@example.com")));
            var unico = Sembrar(almacen, ConstructorDeDatos.UnCliente().Con("sanic_correo", "luis@example.com"));
            Sembrar(almacen, ConstructorDeDatos.UnCliente().Con("sanic_correo", "otro@example.com"));
            var repo = new ClientesDataverse(almacen.ComoUsuario(persona), almacen);

            var dos = repo.BuscarActivosPorCorreo("ana@example.com", 2);
            var uno = repo.BuscarActivosPorCorreo("ana@example.com", 1);
            var luis = repo.BuscarActivosPorCorreo("luis@example.com", 2);
            var ninguno = repo.BuscarActivosPorCorreo("nadie@example.com", 2);

            Assert.Equal(new[] { a, b }.OrderBy(x => x).ToList(), dos.OrderBy(x => x).ToList());
            Assert.Single(uno);
            Assert.Equal(unico, Assert.Single(luis));
            Assert.Empty(ninguno);
            Assert.Equal(new int?[] { 2, 1, 2, 2 }, almacen.Consultas.Select(c => c.TopCount).ToArray());
            Assert.All(almacen.Consultas, c =>
            {
                Assert.Equal(persona, c.Identidad);
                AssertColumnas(c);
            });
        }

        [Fact]
        public void PA_CLD_07_BuscarActivosPorCorreo_ArgumentosInvalidos_Lanzan_SinConsultar()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var repo = new ClientesDataverse(almacen, almacen);

            Assert.Throws<ArgumentException>(() => repo.BuscarActivosPorCorreo(null, 2));
            Assert.Throws<ArgumentException>(() => repo.BuscarActivosPorCorreo("", 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => repo.BuscarActivosPorCorreo("a@example.com", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => repo.BuscarActivosPorCorreo("a@example.com", 5001));
            Assert.Empty(almacen.Consultas);
        }

        [Fact]
        public void PA_CLD_08_ElConstructor_ExigeLosDosServicios()
        {
            var almacen = new OrganizationServiceEnMemoria();

            Assert.Throws<ArgumentNullException>(() => new ClientesDataverse(null, almacen));
            Assert.Throws<ArgumentNullException>(() => new ClientesDataverse(almacen, null));
        }
    }
}
