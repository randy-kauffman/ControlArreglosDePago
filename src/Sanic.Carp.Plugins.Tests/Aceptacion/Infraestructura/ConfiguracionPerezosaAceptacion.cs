using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Infraestructura
{
    /// <summary>
    /// <c>ConfiguracionDelArea.Perezosa</c> (diseno/03 0.4 y 4.1; sublote 5f y su cierre, 2026-10-19): la fábrica que pide el servicio de SYSTEM a una función la primera vez que hace falta consultar, y una sola
    /// vez. Hasta el cierre solo la cubría el camino de los steps, sin una prueba directa. IDs: <c>PA5F_CFP_*</c>. Todas VERDES A PROPÓSITO: fijan lo que la fábrica ya hace.
    /// </summary>
    public class ConfiguracionPerezosaAceptacion
    {
        private const string NombreBu = "ZZF BU ARP";
        private const string Buzon = "zzf.buzon@example.com";
        private const string TablaBu = "businessunit";

        /// <summary>El almacén con las variables y las tres filas que usan BuId, el team y la queue, y una función que cuenta cuántas veces le pidieron el servicio.</summary>
        private sealed class Origen
        {
            public readonly OrganizationServiceEnMemoria Almacen = new OrganizationServiceEnMemoria();
            public int Pedidos;
            public Guid Bu, Team, Queue;

            public Func<IOrganizationService> Funcion
            {
                get
                {
                    return () =>
                    {
                        Pedidos++;
                        return Almacen;
                    };
                }
            }
        }

        private static void Variable(OrganizationServiceEnMemoria almacen, string nombre, string valor)
        {
            var definicion = ConstructorDeDatos.UnaDefinicionDeVariable(nombre, null);
            almacen.Sembrar(definicion.Construir());
            almacen.Sembrar(ConstructorDeDatos.UnValorDeVariable(nombre, definicion.Id, valor).Construir());
        }

        private static Guid UnaBu(OrganizationServiceEnMemoria almacen)
        {
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(TablaBu, id) { ["name"] = NombreBu, ["isdisabled"] = false });
            return id;
        }

        private static Origen Completo(bool conLaBu = true)
        {
            var origen = new Origen();
            Variable(origen.Almacen, ConfiguracionSimulada.VariableNombreBu, NombreBu);
            Variable(origen.Almacen, ConfiguracionSimulada.VariableBuzonArea, Buzon);
            if (conLaBu)
            {
                origen.Bu = UnaBu(origen.Almacen);
                origen.Team = Sembrar(origen.Almacen, ConstructorDeDatos.UnTeam("ZZF equipo del sistema", origen.Bu, true));
            }
            origen.Queue = Sembrar(origen.Almacen, ConstructorDeDatos.UnaCola(Buzon));
            origen.Almacen.LimpiarRegistros();
            return origen;
        }

        [Fact]
        public void PA5F_CFP_01_CrearlaNoPideElServicioNiConsultaNada()
        {
            var origen = Completo();

            var configuracion = ConfiguracionDelArea.Perezosa(origen.Funcion);

            Assert.NotNull(configuracion);
            Assert.Equal(0, origen.Pedidos);
            Assert.Empty(origen.Almacen.Consultas);
        }

        [Fact]
        public void PA5F_CFP_02_PideElServicioUnaSolaVez_EntreLaBuElTeamYLaQueue()
        {
            var origen = Completo();
            var configuracion = ConfiguracionDelArea.Perezosa(origen.Funcion);

            var bu = configuracion.BuId;
            var team = configuracion.TeamPropietarioId;
            var queue = configuracion.QueueRemitenteId;
            var otraVezLaBu = configuracion.BuId;

            Assert.Equal(origen.Bu, bu);
            Assert.Equal(origen.Team, team);
            Assert.Equal(origen.Queue, queue);
            Assert.Equal(bu, otraVezLaBu);
            Assert.Equal(1, origen.Pedidos);
        }

        [Fact]
        public void PA5F_CFP_03_UnaFuncionNula_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ConfiguracionDelArea.Perezosa(null));
        }

        [Fact]
        public void PA5F_CFP_04_UnErrorDeNegocioNoSeMemoiza_ElSiguienteIntentoVuelveAConsultar()
        {
            var origen = Completo(conLaBu: false);
            var configuracion = ConfiguracionDelArea.Perezosa(origen.Funcion);

            var error = Assert.Throws<ErrorDeNegocio>(() => configuracion.BuId);
            Assert.Equal(CodigosDeError.CFG_001.Codigo, error.Codigo);
            var bu = UnaBu(origen.Almacen);

            Assert.Equal(bu, configuracion.BuId);
            Assert.Equal(1, origen.Pedidos);
        }

        [Fact]
        public void PA5F_CFP_05_UnaFallaDeLaFuncionNoSeMemoiza_ElSiguienteIntentoLaPideDeNuevo()
        {
            var origen = Completo();
            var intentos = 0;
            var configuracion = ConfiguracionDelArea.Perezosa(() =>
            {
                intentos++;
                if (intentos == 1) throw new InvalidOperationException("No se pudo crear el servicio de SYSTEM.");
                return origen.Almacen;
            });

            Assert.Throws<InvalidOperationException>(() => configuracion.BuId);
            var bu = configuracion.BuId;

            Assert.Equal(origen.Bu, bu);
            Assert.Equal(2, intentos);
        }
    }
}
