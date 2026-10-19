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
    /// <c>RemitenteDelAreaStep</c> (S-31, diseno/03 2.9, D-28; "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"). PA-EML-01 a PA-EML-08 son los del diseño con ese mismo ID; los que agrega el arquitecto llevan el
    /// prefijo <c>PA5F_REM_S</c>. Casi todos ejecutan el step directamente con el <c>Target</c> que mandaría el botón Responder (el <c>from</c> es una <c>EntityCollection</c> de <c>activityparty</c>); los de
    /// <c>PA5F_REM_S09</c> a <c>S15</c> pasan por el pipeline (el doble acepta <c>from</c> en un <c>Create</c> de correo: es una columna del esquema del correo, sin tipo validado).
    /// </summary>
    public class RemitenteDelAreaStepAceptacion
    {
        private static EscenarioDeSteps UnEscenario()
        {
            return EscenarioDeCorreos(remitente: true);
        }

        private static void AssertSinTocar(Entity target, EntityCollection desde)
        {
            Assert.Same(desde, target["from"]);
        }

        private static Guid OtraColaSembrada(EscenarioDeSteps e)
        {
            var id = e.Sembrar(ConstructorDeDatos.UnaCola("zzf.otra.cola@example.com"));
            e.Almacen.LimpiarRegistros();
            return id;
        }

        [Fact]
        public void PA_EML_01_ElUsuarioQueResponde_SeReemplazaPorLaColaDelArea()
        {
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var original = Partes(RefUsuario(e.Oficial));
            var target = UnaSaliente(conversacion, original);

            EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            var partes = PartesDe(target);
            var unica = Assert.Single(partes);
            Assert.Equal(Cola, unica.Key);
            Assert.Equal(ColaDelArea, unica.Value);
        }

        [Fact]
        public void PA_EML_02_SinFrom_SePoneLaColaDelArea()
        {
            var e = UnEscenario();
            var target = UnaSaliente(UnaConversacion(e), null);

            EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            var unica = Assert.Single(PartesDe(target));
            Assert.Equal(Cola, unica.Key);
            Assert.Equal(ColaDelArea, unica.Value);
        }

        [Fact]
        public void PA5F_REM_S02b_ConElFromVacio_SePoneLaColaDelArea()
        {
            var e = UnEscenario();
            var target = UnaSaliente(UnaConversacion(e), Partes());

            EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            var unica = Assert.Single(PartesDe(target));
            Assert.Equal(ColaDelArea, unica.Value);
        }

        [Fact]
        public void PA_EML_03_ConLaColaDelArea_ElTargetQuedaIgual_LaMismaColeccion()
        {
            var e = UnEscenario();
            var original = Partes(RefCola(ColaDelArea));
            var target = UnaSaliente(UnaConversacion(e), original);

            EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            AssertSinTocar(target, original);
            Assert.Equal(ColaDelArea, Assert.Single(PartesDe(target)).Value);
        }

        [Theory]
        [InlineData("otroUsuario")]
        [InlineData("colaYUsuario")]
        [InlineData("otraCola")]
        [InlineData("colaYCola")]
        [InlineData("usuarioYUsuario")]
        public void PA_EML_04_CualquierOtraCombinacion_CARP_EML_001_YElTargetQuedaIgual(string caso)
        {
            var e = UnEscenario();
            var otraCola = OtraColaSembrada(e);
            var conversacion = UnaConversacion(e);
            var original = caso == "otroUsuario" ? Partes(RefUsuario(e.OtroOficial))
                : caso == "colaYUsuario" ? Partes(RefCola(ColaDelArea), RefUsuario(e.Oficial))
                : caso == "otraCola" ? Partes(RefCola(otraCola))
                : caso == "colaYCola" ? Partes(RefCola(ColaDelArea), RefCola(ColaDelArea))
                : Partes(RefUsuario(e.Oficial), RefUsuario(e.Oficial));
            var target = UnaSaliente(conversacion, original);

            Rechaza(() => EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial), CodigosDeError.EML_001);

            AssertSinTocar(target, original);
        }

        [Fact]
        public void PA5F_REM_S13_UnaParteSinPartyId_EsOtraParte_CARP_EML_001()
        {
            var e = UnEscenario();
            var original = new EntityCollection(new[] { ParteSoloConDireccion("zzf.escrita.a.mano@example.com") });
            var target = UnaSaliente(UnaConversacion(e), original);

            Rechaza(() => EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial), CodigosDeError.EML_001);

            AssertSinTocar(target, original);
        }

        [Theory]
        [InlineData("entrante")]
        [InlineData("entranteConOtroFrom")]
        [InlineData("sinDirectioncode")]
        public void PA_EML_05_UnCorreoQueNoEsSaliente_NoTocaNada_ConCeroConsultasYCeroServicios(string caso)
        {
            // "directioncode" ausente es "no saliente" (2.9 punto 2). El `from` de los dos últimos es uno que la regla rechazaría.
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var original = Partes(RefUsuario(e.OtroOficial));
            var target = UnaSaliente(conversacion, original);
            if (caso == "entrante") target["directioncode"] = false;
            else if (caso == "sinDirectioncode") target.Attributes.Remove("directioncode");
            else target["directioncode"] = false;
            var servicios = e.Fabrica.ServiciosCreados.Count;

            EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            AssertSinTocar(target, original);
            Assert.Empty(e.Almacen.Consultas);
            Assert.Equal(servicios, e.Fabrica.ServiciosCreados.Count);
        }

        [Theory]
        [InlineData("sinRegarding")]
        [InlineData("regardingCuenta")]
        public void PA_EML_06_SalienteSinConversacion_NoTocaNada_ConCeroConsultasYCeroServicios(string caso)
        {
            var e = UnEscenario();
            var original = Partes(RefUsuario(e.OtroOficial));
            var target = new Entity(Correo) { ["directioncode"] = true, ["from"] = original };
            if (caso == "regardingCuenta") target["regardingobjectid"] = new EntityReference("account", Guid.NewGuid());
            var servicios = e.Fabrica.ServiciosCreados.Count;

            EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            AssertSinTocar(target, original);
            Assert.Empty(e.Almacen.Consultas);
            Assert.Equal(servicios, e.Fabrica.ServiciosCreados.Count);
        }

        [Theory]
        [InlineData("sinCola")]
        [InlineData("dosColas")]
        public void PA_EML_07_CeroColasODosConLaDireccionConfigurada_CFG_004_Y_CFG_005(string caso)
        {
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            if (caso == "sinCola") e.ComoSistema.Delete(Cola, ColaDelArea);
            else e.Sembrar(ConstructorDeDatos.UnaCola(EscenarioDeSteps.Buzon));
            var original = Partes(RefUsuario(e.Oficial));
            var target = UnaSaliente(conversacion, original);

            Rechaza(() => EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial), caso == "sinCola" ? CodigosDeError.CFG_004 : CodigosDeError.CFG_005);

            AssertSinTocar(target, original);
        }

        [Fact]
        public void PA_EML_08_ElBuzonConfiguradoConOtrasMayusculas_SeResuelve()
        {
            // La variable trae "Zzf.Buzon@Example.COM" y la cola tiene "zzf.buzon@example.com" (la comparación sin distinguir mayúsculas es de la configuración, ya medida en el lote 4).
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var valor = Assert.Single(e.Almacen.Todas("environmentvariablevalue"), v => string.Equals((string)v["schemaname"], Sanic.Carp.Plugins.Tests.Dobles.ConfiguracionSimulada.VariableBuzonArea, StringComparison.Ordinal));
            e.ComoSistema.Update(new Entity("environmentvariablevalue", valor.Id) { ["value"] = "Zzf.Buzon@Example.COM" });
            var target = UnaSaliente(conversacion, Partes(RefUsuario(e.Oficial)));

            EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            Assert.Equal(ColaDelArea, Assert.Single(PartesDe(target)).Value);
        }

        [Fact]
        public void PA5F_REM_S09_PorElPipeline_ElCreateDeUnaPersonaSeEscribeConLaCola_SinParticipationTypeMask_YConUnaColeccionNueva()
        {
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var original = Partes(RefUsuario(e.Oficial));

            var id = e.ComoOficial.Create(UnaSaliente(conversacion, original));

            var ejecucion = Assert.Single(EjecucionesDe<RemitenteDelAreaStep>(e));
            Assert.Null(ejecucion.Excepcion);
            var escritura = Assert.Single(EscriturasDe(e.Almacen, Correo, OperacionDeDatos.Create));
            var escrita = Assert.IsType<EntityCollection>(escritura.Valores["from"]);
            var parte = Assert.Single(escrita.Entities);
            Assert.Equal(Parte, parte.LogicalName);
            Assert.Equal(new[] { "partyid" }, parte.Attributes.Keys.ToArray());
            var partyid = Assert.IsType<EntityReference>(parte["partyid"]);
            Assert.Equal(Cola, partyid.LogicalName);
            Assert.Equal(ColaDelArea, partyid.Id);
            var guardada = Assert.IsType<EntityCollection>(e.Fila(Correo, id)["from"]);
            Assert.Equal(ColaDelArea, ((EntityReference)Assert.Single(guardada.Entities)["partyid"]).Id);
            // El resto del correo llega como lo mandó la persona (el step no agrega ni quita otra columna).
            Assert.Equal(new[] { "directioncode", "subject", "regardingobjectid", "from" }.OrderBy(c => c, StringComparer.Ordinal).ToArray(),
                escritura.ColumnasEnviadas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.Equal(escritura.ColumnasEnviadas.OrderBy(c => c, StringComparer.Ordinal).ToArray(), escritura.Columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
        }

        [Theory]
        [InlineData("colaPasa")]
        [InlineData("otroRechaza")]
        public void PA5F_REM_S10_ElSistemaTampocoPuedeEscribirOtroRemitente(string caso)
        {
            // 2.9 punto 2: el diseño no da excepción a SYSTEM para D-28.
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var desde = caso == "colaPasa" ? Partes(RefCola(ColaDelArea)) : Partes(RefUsuario(e.OtroOficial));

            if (caso == "colaPasa")
            {
                var id = e.ComoSistema.Create(UnaSaliente(conversacion, desde));
                Assert.Equal(ColaDelArea, ((EntityReference)Assert.Single(((EntityCollection)e.Fila(Correo, id)["from"]).Entities)["partyid"]).Id);
            }
            else
            {
                Rechaza(() => e.ComoSistema.Create(UnaSaliente(conversacion, desde)), CodigosDeError.EML_001);
                Assert.Empty(e.Almacen.Todas(Correo));
            }
        }

        [Fact]
        public void PA5F_REM_S11_LaTrazaDeUnRechazo_LlevaElCodigo_YNingunDatoDelCorreo()
        {
            // 0.5: solo ids y códigos. El asunto y la dirección escrita a mano son marcas que no pueden aparecer.
            var e = UnEscenario();
            var parte = ParteSoloConDireccion("secreto@zzf.example.com");
            var target = UnaSaliente(UnaConversacion(e), new EntityCollection(new[] { parte }));
            target["subject"] = "ZZF-ASUNTO-SECRETO";
            var proveedor = ProveedorCreateConEspia(e, target, e.Oficial);

            Rechaza(() => new RemitenteDelAreaStep().Execute(proveedor), CodigosDeError.EML_001);

            var lineas = proveedor.Espia.Lineas();
            Assert.Contains(lineas, l => l.Contains("rechazo CARP-EML-001"));
            Assert.DoesNotContain(lineas, l => l.Contains("ZZF-ASUNTO-SECRETO") || l.Contains("secreto@zzf.example.com"));
        }

        [Fact]
        public void PA5F_REM_S11b_LaTrazaDeUnaEjecucionCorrecta_NoLlevaElAsuntoNiLaDireccion()
        {
            var e = UnEscenario();
            var target = UnaSaliente(UnaConversacion(e), Partes(RefUsuario(e.Oficial)));
            target["subject"] = "ZZF-ASUNTO-SECRETO";
            var proveedor = ProveedorCreateConEspia(e, target, e.Oficial);

            new RemitenteDelAreaStep().Execute(proveedor);

            var lineas = proveedor.Espia.Lineas();
            Assert.NotEmpty(lineas);
            Assert.DoesNotContain(lineas, l => l.Contains("ZZF-ASUNTO-SECRETO"));
        }

        [Fact]
        public void PA5F_REM_S12_ElFromQueSeEscribe_EsUnaColeccionNueva_DeUnSoloActivityParty()
        {
            var e = UnEscenario();
            var original = Partes(RefUsuario(e.Oficial));
            var target = UnaSaliente(UnaConversacion(e), original);

            EjecutarCreateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            Assert.NotSame(original, target["from"]);
            var nueva = Assert.IsType<EntityCollection>(target["from"]);
            var parte = Assert.Single(nueva.Entities);
            Assert.Equal(Parte, parte.LogicalName);
            Assert.False(parte.Contains("participationtypemask"));
            Assert.Equal(new[] { "partyid" }, parte.Attributes.Keys.ToArray());
            Assert.False(target.Contains("parentactivityid"));
        }

        [Theory]
        [InlineData("sinCola")]
        [InlineData("dosColas")]
        public void PA5F_REM_S15_UnaFallaDeLaConfiguracionEnElCreate_NoDejaNingunCorreo(string caso)
        {
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            if (caso == "sinCola") e.ComoSistema.Delete(Cola, ColaDelArea);
            else e.Sembrar(ConstructorDeDatos.UnaCola(EscenarioDeSteps.Buzon));

            Rechaza(() => e.ComoOficial.Create(UnaSaliente(conversacion, Partes(RefUsuario(e.Oficial)))), caso == "sinCola" ? CodigosDeError.CFG_004 : CodigosDeError.CFG_005);

            Assert.Empty(e.Almacen.Todas(Correo));
        }
    }
}
