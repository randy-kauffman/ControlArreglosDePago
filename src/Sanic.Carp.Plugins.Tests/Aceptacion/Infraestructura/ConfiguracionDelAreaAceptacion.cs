using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Infraestructura
{
    /// <summary><c>ConfiguracionDelArea</c> (diseno/03 0.4 y 4.1 "Regla de datos": la variable de entorno se lee en DOS consultas, sin <c>link</c>).</summary>
    public class ConfiguracionDelAreaAceptacion
    {
        private const string NombreBu = "ZZF BU ARP";
        private const string NombreJefatura = "ZZF Jefatura";
        private const string Buzon = "zzf.buzon@example.com";

        private const string TablaDefinicion = "environmentvariabledefinition";
        private const string TablaValor = "environmentvariablevalue";
        private const string TablaBu = "businessunit";
        private const string TablaQueue = "queue";

        private sealed class Escenario
        {
            public OrganizationServiceEnMemoria Almacen = new OrganizationServiceEnMemoria();
            public Guid Bu, TeamPropietario, TeamJefatura, Queue;
        }

        /// <summary>Define una variable: definición con valor por defecto opcional y valor opcional.</summary>
        private static void Variable(OrganizationServiceEnMemoria almacen, string nombre, string porDefecto, string valor)
        {
            var definicion = ConstructorDeDatos.UnaDefinicionDeVariable(nombre, porDefecto);
            almacen.Sembrar(definicion.Construir());
            if (valor != null) almacen.Sembrar(ConstructorDeDatos.UnValorDeVariable(nombre, definicion.Id, valor).Construir());
        }

        private static Guid UnaBu(OrganizationServiceEnMemoria almacen, string nombre, bool deshabilitada = false)
        {
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(TablaBu, id) { ["name"] = nombre, ["isdisabled"] = deshabilitada });
            return id;
        }

        private static Escenario Completo()
        {
            var e = new Escenario();
            Variable(e.Almacen, ConfiguracionSimulada.VariableNombreBu, "ZZF BU DEFECTO", NombreBu);              // el valor gana sobre el defecto
            Variable(e.Almacen, ConfiguracionSimulada.VariableNombreTeamJefatura, NombreJefatura, null);           // solo defecto
            Variable(e.Almacen, ConfiguracionSimulada.VariableBuzonArea, null, Buzon);                            // solo valor
            e.Bu = UnaBu(e.Almacen, NombreBu);
            e.TeamPropietario = Sembrar(e.Almacen, ConstructorDeDatos.UnTeam("ZZF equipo del sistema", e.Bu, true));
            e.TeamJefatura = Sembrar(e.Almacen, ConstructorDeDatos.UnTeam(NombreJefatura, e.Bu, false));
            e.Queue = Sembrar(e.Almacen, ConstructorDeDatos.UnaCola(Buzon));
            e.Almacen.LimpiarRegistros();
            return e;
        }

        private static ErrorDeNegocio Error(Func<Guid> lectura)
        {
            return Assert.Throws<ErrorDeNegocio>(() => lectura());
        }

        private static void AssertError(DefinicionDeError esperado, ErrorDeNegocio error, params object[] marcadores)
        {
            var igual = esperado.Crear(marcadores);
            Assert.Equal(igual.Codigo, error.Codigo);
            Assert.Equal(igual.Mensaje, error.Mensaje);
        }

        [Fact]
        public void PA_CFG_01_CrearElObjetoNoConsultaNada_ElServicioNuloLanza()
        {
            // 0.4: perezosa.
            var e = Completo();

            var configuracion = new ConfiguracionDelArea(e.Almacen);

            Assert.NotNull(configuracion);
            Assert.Empty(e.Almacen.Consultas);
            Assert.Throws<ArgumentNullException>(() => new ConfiguracionDelArea(null));
        }

        [Fact]
        public void PA_CFG_02_ResuelveLosCuatroMiembros_ElValorGanaSobreElDefectoYElDefectoSirveSinValor()
        {
            // 0.4 puntos 1 a 5.
            var e = Completo();
            var configuracion = new ConfiguracionDelArea(e.Almacen);

            Assert.Equal(e.Bu, configuracion.BuId);
            Assert.Equal(e.TeamPropietario, configuracion.TeamPropietarioId);
            Assert.Equal(e.TeamJefatura, configuracion.TeamJefaturaId);
            Assert.Equal(e.Queue, configuracion.QueueRemitenteId);
        }

        [Fact]
        public void PA_CFG_03_LaVariableSeLeeEnDosConsultas_DefinicionYValor_SinLinkNiColumnSetTrue()
        {
            // 4.1: "La variable de entorno se lee en dos consultas (definición y valor), no con el link de seguridad.md".
            var e = Completo();

            var bu = new ConfiguracionDelArea(e.Almacen).BuId;

            Assert.Equal(e.Bu, bu);
            var consultas = e.Almacen.Consultas;
            Assert.Equal(new[] { TablaDefinicion, TablaValor, TablaBu }, consultas.Select(c => c.Tabla).ToArray());
            AssertColumnas(consultas[0], "defaultvalue");
            AssertColumnas(consultas[1], "value");
            Assert.All(consultas, c => Assert.False(c.TodasLasColumnas));
        }

        [Fact]
        public void PA_CFG_04_EsPerezosaPorMiembro_LaJefaturaNoLeeLaQueueNiElTeamPropietario()
        {
            var e = Completo();

            var jefatura = new ConfiguracionDelArea(e.Almacen).TeamJefaturaId;

            Assert.Equal(e.TeamJefatura, jefatura);
            var consultas = e.Almacen.Consultas;
            Assert.Equal(6, consultas.Count);                                   // variable BU (2) + BU + variable jefatura (2) + team
            Assert.DoesNotContain(consultas, c => c.Tabla == TablaQueue);
            Assert.Equal(1, consultas.Count(c => c.Tabla == Team));
        }

        [Fact]
        public void PA_CFG_05_MemoizaDentroDeLaInstancia_NoRepiteLoYaResuelto()
        {
            // 0.4: "todo con servicio SYSTEM y memoizado dentro de la ejecución".
            var e = Completo();
            var configuracion = new ConfiguracionDelArea(e.Almacen);

            var primera = configuracion.BuId;
            var tras1 = e.Almacen.Consultas.Count;
            var segunda = configuracion.BuId;
            var tras2 = e.Almacen.Consultas.Count;
            var propietario = configuracion.TeamPropietarioId;
            var tras3 = e.Almacen.Consultas.Count;
            var otra = configuracion.TeamPropietarioId;

            Assert.Equal(primera, segunda);
            Assert.Equal(3, tras1);
            Assert.Equal(tras1, tras2);
            Assert.Equal(tras2 + 1, tras3);                                     // solo el team: la BU ya estaba resuelta
            Assert.Equal(propietario, otra);
            Assert.Equal(tras3, e.Almacen.Consultas.Count);
            Assert.Equal(1, e.Almacen.Consultas.Count(c => c.Tabla == TablaBu));
        }

        [Fact]
        public void PA_CFG_06_UnaVariableSinDefinicionSinValorNiDefecto_EsCfg006ConElNombreDeLaVariable()
        {
            // 0.4 punto 1: valor, si no defecto, si no CARP-CFG-006; un valor vacío cuenta como sin valor.
            var sinDefinicion = Completo();
            sinDefinicion.Almacen.Borrar_(TablaDefinicion);
            var sinNada = Completo();
            sinNada.Almacen.Borrar_(TablaDefinicion);
            Variable(sinNada.Almacen, ConfiguracionSimulada.VariableNombreBu, null, null);
            var vaciosAmbos = Completo();
            vaciosAmbos.Almacen.Borrar_(TablaDefinicion);
            Variable(vaciosAmbos.Almacen, ConfiguracionSimulada.VariableNombreBu, "", "");

            foreach (var e in new[] { sinDefinicion, sinNada, vaciosAmbos })
            {
                var error = Error(() => new ConfiguracionDelArea(e.Almacen).BuId);
                AssertError(CodigosDeError.CFG_006, error, ConfiguracionSimulada.VariableNombreBu);
                Assert.Contains(ConfiguracionSimulada.VariableNombreBu, error.Mensaje, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void PA_CFG_07_UnValorVacioCaeAlDefecto()
        {
            var e = Completo();
            e.Almacen.Borrar_(TablaDefinicion);
            Variable(e.Almacen, ConfiguracionSimulada.VariableNombreBu, NombreBu, "");

            Assert.Equal(e.Bu, new ConfiguracionDelArea(e.Almacen).BuId);
        }

        [Fact]
        public void PA_CFG_08_Cfg001_SinBuConEseNombre_BuDeshabilitada_OMasDeUna()
        {
            // 0.4 punto 2: "no deshabilitada. Debe haber exactamente una". El mensaje nombra la variable, nunca el valor.
            var ninguna = Completo();
            ninguna.Almacen.Borrar_(TablaBu);
            var deshabilitada = Completo();
            deshabilitada.Almacen.Borrar_(TablaBu);
            UnaBu(deshabilitada.Almacen, NombreBu, true);
            var dos = Completo();
            UnaBu(dos.Almacen, NombreBu);

            foreach (var e in new[] { ninguna, deshabilitada, dos })
            {
                var error = Error(() => new ConfiguracionDelArea(e.Almacen).BuId);
                AssertError(CodigosDeError.CFG_001, error);
                Assert.DoesNotContain(NombreBu, error.Mensaje, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void PA_CFG_09_UnaBuDeshabilitadaConElMismoNombreNoImpideResolverLaHabilitada()
        {
            var e = Completo();
            UnaBu(e.Almacen, NombreBu, true);

            Assert.Equal(e.Bu, new ConfiguracionDelArea(e.Almacen).BuId);
        }

        [Fact]
        public void PA_CFG_10_Cfg002_ElTeamPropietarioEsElDefaultDeLaBu_NoSeBuscaPorNombre()
        {
            // 0.4 punto 3 y conv. §2: exactamente uno con isdefault en la BU.
            var sinDefault = Completo();
            sinDefault.Almacen.Borrar_("team");
            Sembrar(sinDefault.Almacen, ConstructorDeDatos.UnTeam("ZZF otro", sinDefault.Bu, false));
            var dosDefault = Completo();
            Sembrar(dosDefault.Almacen, ConstructorDeDatos.UnTeam("ZZF segundo default", dosDefault.Bu, true));
            var soloDeOtraBu = Completo();
            soloDeOtraBu.Almacen.Borrar_("team");
            Sembrar(soloDeOtraBu.Almacen, ConstructorDeDatos.UnTeam("ZZF ajeno", Guid.NewGuid(), true));

            foreach (var e in new[] { sinDefault, dosDefault, soloDeOtraBu })
                AssertError(CodigosDeError.CFG_002, Error(() => new ConfiguracionDelArea(e.Almacen).TeamPropietarioId));

            var normal = Completo();
            Sembrar(normal.Almacen, ConstructorDeDatos.UnTeam("ZZF ajeno", Guid.NewGuid(), true));
            Sembrar(normal.Almacen, ConstructorDeDatos.UnTeam("ZZF no default", normal.Bu, false));
            Assert.Equal(normal.TeamPropietario, new ConfiguracionDelArea(normal.Almacen).TeamPropietarioId);
        }

        [Fact]
        public void PA_CFG_11_Cfg003_ElTeamDeJefaturaSeBuscaPorNombreDentroDeLaBu()
        {
            // 0.4 punto 4: nombre = valor y businessunitid = la BU; no exige isdefault.
            var ausente = Completo();
            ausente.Almacen.Borrar_("team");
            Sembrar(ausente.Almacen, ConstructorDeDatos.UnTeam("ZZF equipo del sistema", ausente.Bu, true));
            var deOtraBu = Completo();
            deOtraBu.Almacen.Borrar_("team");
            Sembrar(deOtraBu.Almacen, ConstructorDeDatos.UnTeam("ZZF equipo del sistema", deOtraBu.Bu, true));
            Sembrar(deOtraBu.Almacen, ConstructorDeDatos.UnTeam(NombreJefatura, Guid.NewGuid(), false));

            foreach (var e in new[] { ausente, deOtraBu })
                AssertError(CodigosDeError.CFG_003, Error(() => new ConfiguracionDelArea(e.Almacen).TeamJefaturaId));

            var normal = Completo();
            Sembrar(normal.Almacen, ConstructorDeDatos.UnTeam(NombreJefatura, Guid.NewGuid(), false));
            Assert.Equal(normal.TeamJefatura, new ConfiguracionDelArea(normal.Almacen).TeamJefaturaId);
        }

        [Fact]
        public void PA_CFG_12_Cfg004YCfg005_LaQueueSeBuscaPorCorreoSinMayusculasYSinInactivas()
        {
            // 0.4 punto 5: cero ⇒ -004; más de una ⇒ -005.
            var ninguna = Completo();
            ninguna.Almacen.Borrar_(TablaQueue);
            var soloInactiva = Completo();
            soloInactiva.Almacen.Borrar_(TablaQueue);
            Sembrar(soloInactiva.Almacen, ConstructorDeDatos.UnaCola(Buzon).Con("statecode", new OptionSetValue(Sanic.Carp.Plugins.Dominio.Estados.Inactivo))
                .Con("statuscode", new OptionSetValue(Sanic.Carp.Plugins.Dominio.Estados.StatusPorDefecto(Sanic.Carp.Plugins.Dominio.Estados.Inactivo))));
            var dos = Completo();
            Sembrar(dos.Almacen, ConstructorDeDatos.UnaCola(Buzon.ToUpperInvariant()));

            foreach (var e in new[] { ninguna, soloInactiva })
                AssertError(CodigosDeError.CFG_004, Error(() => new ConfiguracionDelArea(e.Almacen).QueueRemitenteId));
            AssertError(CodigosDeError.CFG_005, Error(() => new ConfiguracionDelArea(dos.Almacen).QueueRemitenteId));

            var mayusculas = Completo();
            mayusculas.Almacen.Borrar_(TablaQueue);
            var cola = Sembrar(mayusculas.Almacen, ConstructorDeDatos.UnaCola(Buzon.ToUpperInvariant()));
            Assert.Equal(cola, new ConfiguracionDelArea(mayusculas.Almacen).QueueRemitenteId);
            var conInactiva = Completo();
            Sembrar(conInactiva.Almacen, ConstructorDeDatos.UnaCola(Buzon).Con("statecode", new OptionSetValue(Sanic.Carp.Plugins.Dominio.Estados.Inactivo))
                .Con("statuscode", new OptionSetValue(Sanic.Carp.Plugins.Dominio.Estados.StatusPorDefecto(Sanic.Carp.Plugins.Dominio.Estados.Inactivo))));
            Assert.Equal(conInactiva.Queue, new ConfiguracionDelArea(conInactiva.Almacen).QueueRemitenteId);
        }

        [Fact]
        public void PA_CFG_13_SiFallaLaBu_LosMiembrosQueDependenDeEllaFallanConCfg001()
        {
            var e = Completo();
            e.Almacen.Borrar_(TablaBu);
            var configuracion = new ConfiguracionDelArea(e.Almacen);

            AssertError(CodigosDeError.CFG_001, Error(() => configuracion.TeamPropietarioId));
            AssertError(CodigosDeError.CFG_001, Error(() => configuracion.TeamJefaturaId));
            Assert.Equal(e.Queue, configuracion.QueueRemitenteId);              // la queue no depende de la BU
        }

        [Fact]
        public void PA_CFG_14_NingunMensajeRevelaElValorDeUnaVariable()
        {
            // 0.4: "nombran la variable o el elemento que falta, nunca un valor".
            var e = Completo();
            e.Almacen.Borrar_(TablaBu);
            e.Almacen.Borrar_("team");
            e.Almacen.Borrar_(TablaQueue);
            var configuracion = new ConfiguracionDelArea(e.Almacen);

            var errores = new[]
            {
                Error(() => configuracion.BuId),
                Error(() => configuracion.TeamPropietarioId),
                Error(() => configuracion.TeamJefaturaId),
                Error(() => configuracion.QueueRemitenteId)
            };

            foreach (var error in errores)
                foreach (var valor in new[] { NombreBu, NombreJefatura, Buzon })
                    Assert.DoesNotContain(valor, error.Mensaje, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PA_CFG_15_NingunaConsultaPideTodasLasColumnas()
        {
            var e = Completo();
            var configuracion = new ConfiguracionDelArea(e.Almacen);

            var todo = new[] { configuracion.BuId, configuracion.TeamPropietarioId, configuracion.TeamJefaturaId, configuracion.QueueRemitenteId };

            Assert.Equal(4, todo.Distinct().Count());
            Assert.All(e.Almacen.Consultas, c => Assert.False(c.TodasLasColumnas));
        }
    }

    /// <summary>Atajo de la prueba para dejar una tabla vacía del almacén (los datos de prueba no se borran por el servicio, para no ensuciar el registro).</summary>
    internal static class AlmacenParaConfiguracion
    {
        public static void Borrar_(this OrganizationServiceEnMemoria almacen, string tabla)
        {
            // Una variable son dos tablas: borrar las definiciones borra también sus valores (si no, quedarían valores huérfanos).
            var tablas = string.Equals(tabla, "environmentvariabledefinition", StringComparison.Ordinal) ? new[] { "environmentvariablevalue", tabla } : new[] { tabla };
            foreach (var t in tablas)
                foreach (var fila in almacen.Todas(t)) almacen.Delete(t, fila.Id);
            almacen.LimpiarRegistros();
        }
    }
}
