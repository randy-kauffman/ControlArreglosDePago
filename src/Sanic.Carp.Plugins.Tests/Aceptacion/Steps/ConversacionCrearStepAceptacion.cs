using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeConversacion;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>ConversacionCrearStep</c> (diseno/03 2.3, D-41, S-36). PA-CNV-39 a PA-CNV-43 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5b llevan el prefijo <c>PA5B_CRE</c>.
    /// <para>
    /// <b>El autonumérico.</b> El almacén completa <c>sanic_nombre</c> ANTES de PreValidation (V-8, <c>AutonumericosAceptacion</c>): con la clave ausente, nula o vacía el step ve el número de la plataforma, que cumple el
    /// formato, así que esos tres casos del diseño se prueban aquí por el efecto (pasa sin consultar la identidad) y la rama "sin clave" del step se prueba en el dominio (<c>PA5B_RCV_ALT_01</c>).
    /// </para>
    /// </summary>
    public class ConversacionCrearStepAceptacion
    {
        private static readonly Regex FormatoDelAutonumerico = new Regex("^CONV-[0-9]{6,15}\\z", RegexOptions.CultureInvariant);

        private static EscenarioDeSteps UnEscenario(bool conPropietario = false)
        {
            return Escenario(propietario: conPropietario, actualizar: false, crear: true);
        }

        private static Entity UnAlta(params object[] paresNombreValor)
        {
            var fila = new Entity(Conv);
            fila["sanic_huellamessageid"] = "ZZF-huella-" + Guid.NewGuid().ToString("N");
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        [Fact]
        public void PA_CNV_39_SinNumeroNuloOVacio_OConElDeLaPlataforma_Pasa_SinConsultarLaIdentidad()
        {
            foreach (var alta in new[] { UnAlta(), UnAlta("sanic_nombre", null), UnAlta("sanic_nombre", string.Empty), UnAlta("sanic_nombre", "CONV-001002") })
            {
                var e = UnEscenario();
                SinConsultarLaIdentidad(e);

                var id = e.ComoOficial.Create(alta);

                var numero = Assert.IsType<string>(e.Fila(Conv, id)["sanic_nombre"]);
                Assert.Matches(FormatoDelAutonumerico, numero);
                var escritura = Assert.Single(EscriturasDe(e.Almacen, Conv, OperacionDeDatos.Create));
                Assert.Empty(AgregadasPorLosSteps(escritura));                                      // el step no modificó el Target
            }
        }

        [Theory]
        [InlineData("ZZV-NOMBRE")]
        [InlineData("conv-000001")]
        [InlineData("CONV-12345")]
        [InlineData("CONV-000001 ")]
        [InlineData("CONV-12A456")]
        [InlineData("   ")]
        [InlineData("CONV-000001\n")]
        [InlineData("CONV-００００００")]
        [InlineData("CONV-1234567890123456")]            // 16 dígitos: 21 caracteres, no cabe en la columna
        public void PA_CNV_40_UnNumeroSinElFormato_EnElAltaDeUnaPersona_CARP_CNV_001(string numero)
        {
            var e = UnEscenario();

            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_nombre", numero)), CodigosDeError.CNV_001, "sanic_nombre");

            Assert.Empty(e.Almacen.Todas(Conv));                                                    // ni siquiera quedó la fila
        }

        [Theory]
        [InlineData("CONV-777777")]
        [InlineData("CONV-000001")]
        [InlineData("CONV-123456789012345")]
        public void PA_CNV_41_UnNumeroConElFormatoCorrecto_Pasa_AunqueLoHayaEscritoLaPersona_SinConsultarLaIdentidad(string numero)
        {
            // Riesgo aceptado por el usuario: un número con el formato correcto es indistinguible del autonumérico de la plataforma.
            var e = UnEscenario();
            SinConsultarLaIdentidad(e);

            var id = e.ComoOficial.Create(UnAlta("sanic_nombre", numero));

            Assert.Equal(numero, e.Fila(Conv, id)["sanic_nombre"]);
        }

        [Fact]
        public void PA_CNV_42_ElSistemaEscribeUnNumeroSinElFormato_Pasa_ConUnaSolaConsultaDeIdentidad()
        {
            // DC-03. "Una sola": con el cable en la primera llamada el alta FALLA (preguntó), con el cable en la segunda PASA (no preguntó dos veces).
            var consultaba = UnEscenario();
            SinConsultarLaIdentidad(consultaba);
            var error = Assert.Throws<InvalidPluginExecutionException>(() => consultaba.ComoSistema.Create(UnAlta("sanic_nombre", "ZZV-NOMBRE")));
            Assert.StartsWith("CARP-CNV-900: ", error.Message, StringComparison.Ordinal);

            var unaVez = UnEscenario();
            ComoMuchoUnaConsultaDeIdentidad(unaVez);
            var id = unaVez.ComoSistema.Create(UnAlta("sanic_nombre", "ZZV-NOMBRE"));

            Assert.Equal("ZZV-NOMBRE", unaVez.Fila(Conv, id)["sanic_nombre"]);
            var escritura = Assert.Single(EscriturasDe(unaVez.Almacen, Conv, OperacionDeDatos.Create));
            Assert.Empty(AgregadasPorLosSteps(escritura));
        }

        [Fact]
        public void PA_CNV_43_LasDosAltasDelSistema_ConElPropietarioYElStepDeConversacion_NoConsultanDeMas()
        {
            // 2.10 paso 5 (entrada de correo, ruta C) e Iniciar conversación nueva (1.5 paso 5a) crean igual: SYSTEM, con el team del área y un Target que ya trae el número de la plataforma. S-02 (orden 5)
            // pregunta quién es UNA vez (DC-03); S-36 (orden 10) no pregunta nada porque el formato es válido: con el cable en la segunda llamada, el alta pasa.
            var e = UnEscenario(conPropietario: true);
            ComoMuchoUnaConsultaDeIdentidad(e);

            var id = e.ComoSistema.Create(UnAlta("sanic_nombre", "CONV-001002", "ownerid", new EntityReference("team", e.TeamDelArea)));

            Assert.Equal("CONV-001002", e.Fila(Conv, id)["sanic_nombre"]);
            Assert.Contains(e.Pipeline.Ejecuciones, x => x.Step.Plugin is ConversacionCrearStep && x.Excepcion == null);
        }

        [Fact]
        public void PA5B_CRE_01_ConElPropietarioYElStepDeConversacion_UnNumeroSinFormatoSeRechazaIgual()
        {
            // Create Pre de Conversación: 5 (S-02, propietario: reemplaza el team ajeno de una persona, 2.1 paso 1) y 10 (S-36). El rechazo del número no depende de lo que haga S-02.
            var e = UnEscenario(conPropietario: true);

            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_nombre", "ZZV-NOMBRE", "ownerid", new EntityReference("team", e.TeamAjeno))), CodigosDeError.CNV_001, "sanic_nombre");

            Assert.Empty(e.Almacen.Todas(Conv));
        }

        [Fact]
        public void PA5B_CRE_02_ElStepNuncaQuitaNiReemplazaElNumero_CuandoRechaza_NiCuandoPasa()
        {
            // V-8, caso B2: quitar o reemplazar sanic_nombre en PreOperation dejaría la primaria vacía. Ejecución directa: el Target sale como entró.
            var e = UnEscenario();
            // Orquestador, 2026-10-19 (cierre del 5b): cada caso afirma su resultado (el revisor vio que el try/catch tragaba cualquier rechazo y la
            // prueba seguía verde si el step rechazaba de más o dejaba de rechazar). Sin formato ⇒ CARP-CNV-001; con formato ⇒ pasa.
            foreach (var (numero, rechaza) in new[] { ("ZZV-NOMBRE", true), ("CONV-001002", false) })
            {
                var target = UnAlta("sanic_nombre", numero);
                var antes = target.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
                var contexto = Contexto("Create", 20, Conv, e.Oficial, target);

                var error = Record.Exception(() => Ejecutar(new ConversacionCrearStep(), e.Fabrica, contexto));
                if (rechaza)
                    Assert.StartsWith("CARP-CNV-001", Assert.IsType<InvalidPluginExecutionException>(error).Message, StringComparison.Ordinal);
                else
                    Assert.Null(error);

                Assert.Equal(antes, target.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
                Assert.Equal(numero, target["sanic_nombre"]);
            }
        }

        [Fact]
        public void PA5B_CRE_03_UnAltaDeUnaPersonaSinNumeroEnElTarget_PasaEnElStep_ConLaClaveAusente()
        {
            // 2.3 paso 1 ("Si el Target no trae sanic_nombre … terminar"): la rama que el almacén no deja ver porque completa el autonumérico antes. Ejecución directa, sin clave y con ella nula o vacía.
            var e = UnEscenario();
            SinConsultarLaIdentidad(e);
            foreach (var target in new[] { UnAlta(), UnAlta("sanic_nombre", null), UnAlta("sanic_nombre", string.Empty) })
                Ejecutar(new ConversacionCrearStep(), e.Fabrica, Contexto("Create", 20, Conv, e.Oficial, target));
        }

        [Fact]
        public void PA5B_CRE_04_ElStepSoloSeRegistraEnElCreateDeConversaciones()
        {
            var e = UnEscenario();
            var otraTabla = Contexto("Create", 20, "sanic_carp_tbl_arreglo", e.Oficial, new Entity("sanic_carp_tbl_arreglo", Guid.NewGuid()));
            var update = Contexto("Update", 20, Conv, e.Oficial, new Entity(Conv, Guid.NewGuid()));
            update.PreEntityImages["PreImagen"] = new Entity(Conv, update.PrimaryEntityId);
            var postOperation = Contexto("Create", 40, Conv, e.Oficial, new Entity(Conv, Guid.NewGuid()));

            foreach (var contexto in new[] { otraTabla, update, postOperation })
            {
                var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new ConversacionCrearStep(), e.Fabrica, contexto));
                Assert.DoesNotContain("CARP-CNV-900", error.Message);
            }
        }

        [Fact]
        public void PA5B_CRE_05_NingunaTrazaDelStepLlevaElNumeroEscrito()
        {
            const string marcador = "ZZV-MARCADOR-UNICO";
            var e = UnEscenario();

            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_nombre", marcador)), CodigosDeError.CNV_001, "sanic_nombre");
            e.ComoSistema.Create(UnAlta("sanic_nombre", marcador));

            var lineas = e.Fabrica.Tracing.Lineas;
            Assert.NotEmpty(lineas);
            Assert.DoesNotContain(lineas, l => l.IndexOf(marcador, StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.All(lineas, l => Assert.StartsWith("[ConversacionCrearStep]", l, StringComparison.Ordinal));
        }
    }
}
