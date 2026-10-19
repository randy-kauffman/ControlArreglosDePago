using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeCorreo;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>EmailSoloSistemaStep</c> (S-32 en el <c>Create</c> y S-33 en el <c>Update</c>, diseno/03 2.9, D-30; "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"). PA-ESI-01 a PA-ESI-04 son los del diseño con ese
    /// mismo ID; los que agrega el arquitecto llevan el prefijo <c>PA5F_ESS_</c>. Todos pasan por el pipeline con el step registrado como lo declara <c>RegistrosEsperados</c> (S-33 con su filtro y SIN pre-imagen).
    /// </summary>
    public class EmailSoloSistemaStepAceptacion
    {
        private const string ColTiempo = "sanic_tiemporespuesta";
        private const string ColRespondido = "sanic_respondidoporid";

        private static EscenarioDeSteps UnEscenario()
        {
            return EscenarioDeCorreos(soloSistema: true);
        }

        private static Guid UnCorreoSembrado(EscenarioDeSteps e, int? tiempo = null, Guid? respondidoPor = null)
        {
            var fila = ConstructorDeDatos.UnCorreoSaliente();
            if (tiempo.HasValue) fila.Con(ColTiempo, tiempo.Value);
            if (respondidoPor.HasValue) fila.Con(ColRespondido, RefUsuario(respondidoPor.Value));
            var id = e.Sembrar(fila);
            e.Almacen.LimpiarRegistros();
            return id;
        }

        private static Entity Cambio(Guid id, params object[] paresNombreValor)
        {
            var cambio = new Entity(Correo, id);
            for (int i = 0; i < paresNombreValor.Length; i += 2) cambio[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return cambio;
        }

        private static object Guardado(EscenarioDeSteps e, Guid id, string columna)
        {
            return EscenarioDeSteps.Valor(e.Fila(Correo, id), columna);
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        public void PA_ESI_01_UnaPersonaQueCreaUnCorreoConUnaColumnaDelSistema_CARP_EML_003_YNoQuedaNingunaFila(string columna)
        {
            var e = UnEscenario();
            var correo = new Entity(Correo) { ["subject"] = "ZZF-PRUEBA alta" };
            correo[columna == "tiempo" ? ColTiempo : ColRespondido] = columna == "tiempo" ? (object)10 : RefUsuario(e.Oficial);

            Rechaza(() => e.ComoOficial.Create(correo), CodigosDeError.EML_003);

            Assert.Empty(e.Almacen.Todas(Correo));
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        public void PA_ESI_02_UnaPersonaQueActualizaUnaColumnaDelSistema_CARP_EML_003_YLaFilaQuedaIntacta(string columna)
        {
            var e = UnEscenario();
            var id = UnCorreoSembrado(e);
            var cambio = columna == "tiempo" ? Cambio(id, ColTiempo, 10) : Cambio(id, ColRespondido, RefUsuario(e.Oficial));

            Rechaza(() => e.ComoOficial.Update(cambio), CodigosDeError.EML_003);

            Assert.Null(Guardado(e, id, ColTiempo));
            Assert.Null(Guardado(e, id, ColRespondido));
        }

        [Theory]
        [InlineData("create")]
        [InlineData("update")]
        public void PA_ESI_03_ElSistemaEscribeLasDosColumnas_Pasa_YQuedanGuardadas(string mensaje)
        {
            var e = UnEscenario();
            var oficial = e.Oficial;
            Guid id;
            if (mensaje == "create")
                id = e.ComoSistema.Create(new Entity(Correo) { ["subject"] = "ZZF-PRUEBA alta", [ColTiempo] = 45, [ColRespondido] = RefUsuario(oficial) });
            else
            {
                id = UnCorreoSembrado(e);
                e.ComoSistema.Update(Cambio(id, ColTiempo, 45, ColRespondido, RefUsuario(oficial)));
            }

            Assert.Equal(45, Guardado(e, id, ColTiempo));
            Assert.Equal(oficial, Assert.IsType<EntityReference>(Guardado(e, id, ColRespondido)).Id);
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        [InlineData("ambas")]
        public void PA_ESI_04_UnaPersonaQueManda_NuloSobreUnaFilaQueYaLasTieneNulas_Pasa(string caso)
        {
            var e = UnEscenario();
            var id = UnCorreoSembrado(e);
            var cambio = caso == "tiempo" ? Cambio(id, ColTiempo, null) : caso == "respondido" ? Cambio(id, ColRespondido, null) : Cambio(id, ColTiempo, null, ColRespondido, null);

            e.ComoOficial.Update(cambio);

            Assert.Null(Guardado(e, id, ColTiempo));
            Assert.Null(Guardado(e, id, ColRespondido));
        }

        [Fact]
        public void PA5F_ESS_05_UnUpdateQueNoTrae_NingunaDeLasDosColumnas_NoDespiertaAS33()
        {
            // VERDE A PROPÓSITO: lo cumple el filtro del registro (el pipeline no despierta a S-33 sin esas columnas), no la lógica del step; fija que el registro lo declare.
            var e = UnEscenario();
            var id = UnCorreoSembrado(e);

            e.ComoOficial.Update(Cambio(id, "subject", "ZZF-PRUEBA otro asunto"));

            Assert.DoesNotContain(EjecucionesDe<EmailSoloSistemaStep>(e), x => x.Step.Mensaje == "Update");
            Assert.Equal("ZZF-PRUEBA otro asunto", Guardado(e, id, "subject"));
        }

        [Fact]
        public void PA5F_ESS_06_UnCreateOrdinarioSinLasColumnas_NoConsultaNiCreaServicios()
        {
            // El camino barato de todo correo: ni identidad (el cable de WhoAmI en la primera llamada no se activa), ni lectura, ni el servicio de SYSTEM.
            var e = UnEscenario();
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);
            var servicios = e.Fabrica.ServiciosCreados.Count;

            var id = e.ComoOficial.Create(new Entity(Correo) { ["subject"] = "ZZF-PRUEBA ordinario" });

            Assert.True(e.Almacen.Existe(Correo, id));
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Operacion != OperacionDeDatos.WhoAmI);
            Assert.Equal(servicios, e.Fabrica.ServiciosCreados.Count);
            Assert.Single(EjecucionesDe<EmailSoloSistemaStep>(e), x => x.Excepcion == null);
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        [InlineData("ambas")]
        public void PA5F_ESS_07_UnaPersonaQueMandaElMismoValorGuardado_Pasa_ConUnaSolaLecturaDelCorreoHechaPorSystem(string caso)
        {
            var e = UnEscenario();
            var id = UnCorreoSembrado(e, 5, e.OtroOficial);
            var cambio = caso == "tiempo" ? Cambio(id, ColTiempo, 5)
                : caso == "respondido" ? Cambio(id, ColRespondido, RefUsuario(e.OtroOficial))
                : Cambio(id, ColTiempo, 5, ColRespondido, RefUsuario(e.OtroOficial));

            e.ComoOficial.Update(cambio);

            var lectura = Assert.Single(LecturasDeCorreo(e));
            Assert.Equal(id, lectura.Id);
            Assert.Equal(e.Almacen.IdDeSystem, lectura.Identidad);
            ApoyoDeDatosColumnas(lectura);
        }

        private static void ApoyoDeDatosColumnas(ConsultaRegistrada consulta)
        {
            Assert.False(consulta.TodasLasColumnas);
            Assert.Equal(new[] { ColRespondido, ColTiempo }, consulta.Columnas.Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA5F_ESS_08_UnUpdateDelSistema_NoLeeElCorreo()
        {
            var e = UnEscenario();
            var id = UnCorreoSembrado(e, 5, e.OtroOficial);

            e.ComoSistema.Update(Cambio(id, ColTiempo, 99, ColRespondido, RefUsuario(e.Oficial)));

            Assert.Empty(LecturasDeCorreo(e));
            Assert.Equal(99, Guardado(e, id, ColTiempo));
        }

        [Theory]
        [InlineData("soloTiempo")]
        [InlineData("soloRespondido")]
        [InlineData("ambas")]
        public void PA5F_ESS_09_UnaPersonaQueMandaUnValorDistinto_CARP_EML_003_YLaFilaQuedaIntacta(string caso)
        {
            var e = UnEscenario();
            var id = UnCorreoSembrado(e, 5, e.OtroOficial);
            var cambio = caso == "soloTiempo" ? Cambio(id, ColTiempo, 6)
                : caso == "soloRespondido" ? Cambio(id, ColRespondido, RefUsuario(e.Oficial))
                : Cambio(id, ColTiempo, 6, ColRespondido, RefUsuario(e.Oficial));

            Rechaza(() => e.ComoOficial.Update(cambio), CodigosDeError.EML_003);

            Assert.Equal(5, Guardado(e, id, ColTiempo));
            Assert.Equal(e.OtroOficial, Assert.IsType<EntityReference>(Guardado(e, id, ColRespondido)).Id);
        }

        [Fact]
        public void PA5F_ESS_10_ElCreateDeUnaRespuesta_CorreS31Antes_QueS32()
        {
            // Sección 5: Create Pre 10 (S-31) y 20 (S-32) sobre email. Registrados los dos, S-31 corre primero.
            var e = EscenarioDeCorreos(remitente: true, soloSistema: true);
            var conversacion = UnaConversacion(e);

            e.ComoOficial.Create(UnaSaliente(conversacion, Partes(RefUsuario(e.Oficial))));

            var ejecuciones = e.Pipeline.Ejecuciones.Where(x => x.Step.Mensaje == "Create").ToList();
            Assert.Equal(new object[] { typeof(RemitenteDelAreaStep), typeof(EmailSoloSistemaStep) }, ejecuciones.Select(x => (object)x.Step.Plugin.GetType()).ToArray());
            Assert.Equal(new[] { 10, 20 }, ejecuciones.Select(x => x.Step.Orden).ToArray());
            Assert.All(ejecuciones, x => Assert.Null(x.Excepcion));
        }

        [Fact]
        public void PA5F_ESS_11_UnJefeTampocoPuede_CARP_EML_003()
        {
            // 2.9: "el actor no es del sistema". La jefatura no es el sistema.
            var e = UnEscenario();
            var jefe = ApoyoDeConversacion.UnJefe(e);
            var correo = new Entity(Correo) { ["subject"] = "ZZF-PRUEBA alta", [ColTiempo] = 10 };

            Rechaza(() => ApoyoDeConversacion.Como(e, jefe).Create(correo), CodigosDeError.EML_003);

            Assert.Empty(e.Almacen.Todas(Correo));
        }

        [Fact]
        public void PA5F_ESS_12_ElUpdateSeRegistraConSuFiltro_YElCreateSinFiltro()
        {
            // Un Update con solo `subject` no despierta a S-33 (ESS_05), uno con cualquiera de las dos columnas sí, aunque el valor no cambie.
            var e = UnEscenario();
            var id = UnCorreoSembrado(e, 5, e.OtroOficial);

            e.ComoOficial.Update(Cambio(id, ColRespondido, RefUsuario(e.OtroOficial)));

            Assert.Single(EjecucionesDe<EmailSoloSistemaStep>(e), x => x.Step.Mensaje == "Update" && x.Excepcion == null);
        }
    }
}
