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
    /// <c>RemitenteDelAreaStep</c> en el <c>Update</c> (S-37, diseno/03 2.9 y "Decisiones del arquitecto, 2026-10-19 (cierre del sublote 5f)"; decisión del usuario, opción A). PA-EML-09 a PA-EML-12 son los del
    /// diseño con ese ID; los que agrega el arquitecto llevan el prefijo <c>PA5F_REM_V</c>. Pasan por el pipeline con el step registrado como lo declara <c>RegistrosEsperados</c> (S-31 en el <c>Create</c> y S-37 en el
    /// <c>Update</c>, con su filtro y SIN pre-imagen) o, para las formas que el almacén no deja escribir (un Regarding a una tabla que no existe), ejecutan el step directo con el <c>Target</c> que mandaría la plataforma.
    /// <para>
    /// <b>Condiciones del doble.</b> El filtro de atributos mira las claves del <c>Target</c> que mandó quien llamó (no las que los steps le agreguen) y el almacén acepta <c>from</c> en un <c>Update</c> como cualquier
    /// columna del esquema del correo, sin validar su tipo. Un <c>Retrieve</c> con columnas NO trae las partes de <c>from</c> (solo las trae <c>ColumnSet(true)</c>): por eso el <c>from</c> guardado no se puede
    /// observar con una lectura de columnas y S-37 no lo lee (2.9, punto 3). Lo que sí se observa es lo que el <c>Update</c> deja guardado.
    /// </para>
    /// </summary>
    public class RemitenteDelAreaActualizacionStepAceptacion
    {
        private static EscenarioDeSteps UnEscenario()
        {
            return EscenarioDeCorreos(remitente: true);
        }

        private static EntityReference DeConversacion(Guid conversacion)
        {
            return new EntityReference(Conv, conversacion);
        }

        /// <summary>
        /// Un correo que S-31 deja pasar porque está FUERA de su alcance, con <c>from</c> = <paramref name="remitente"/>. <c>soloRegarding</c>: saliente sin Regarding. <c>direccionYRegarding</c>: no saliente y sin Regarding.
        /// <c>soloDireccion</c>: no saliente pero con Regarding a la conversación.
        /// </summary>
        private static Guid UnBorrador(EscenarioDeSteps e, string forma, Guid conversacion, Guid remitente)
        {
            var borrador = new Entity(Correo) { ["subject"] = "ZZF-PRUEBA borrador", ["from"] = Partes(RefUsuario(remitente)) };
            borrador["directioncode"] = forma == "soloRegarding";
            if (forma == "soloDireccion") borrador["regardingobjectid"] = DeConversacion(conversacion);
            var id = e.ComoOficial.Create(borrador);
            e.Almacen.LimpiarRegistros();
            return id;
        }

        /// <summary>El <c>Update</c> que deja el borrador de <see cref="UnBorrador"/> saliente y dentro de una conversación; <paramref name="desde"/> <c>null</c> = sin la clave <c>from</c>.</summary>
        private static Entity PasarASaliente(string forma, Guid id, Guid conversacion, EntityCollection desde)
        {
            var cambio = new Entity(Correo, id);
            if (forma == "soloRegarding") cambio["regardingobjectid"] = DeConversacion(conversacion);
            else if (forma == "direccionYRegarding")
            {
                cambio["directioncode"] = true;
                cambio["regardingobjectid"] = DeConversacion(conversacion);
            }
            else cambio["directioncode"] = true;
            if (desde != null) cambio["from"] = desde;
            return cambio;
        }

        /// <summary>Un saliente que YA está en una conversación (sembrado: el <c>Create</c> de una persona lo pasaría por S-31 y le pondría la queue).</summary>
        private static Guid UnSalienteDeConversacion(EscenarioDeSteps e, Guid conversacion)
        {
            var id = e.Sembrar(ConstructorDeDatos.UnCorreoSaliente().Con("regardingobjectid", DeConversacion(conversacion)));
            e.Almacen.LimpiarRegistros();
            return id;
        }

        private static Guid ElFromGuardado(EscenarioDeSteps e, Guid id)
        {
            var coleccion = Assert.IsType<EntityCollection>(e.Fila(Correo, id)["from"]);
            return ((EntityReference)Assert.Single(coleccion.Entities)["partyid"]).Id;
        }

        private static void AssertIntacto(EscenarioDeSteps e, Guid id, Entity antes, Guid fromOriginal)
        {
            var ahora = e.Fila(Correo, id);
            Assert.Equal(antes.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(), ahora.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(EscenarioDeSteps.Valor(antes, "directioncode"), EscenarioDeSteps.Valor(ahora, "directioncode"));
            Assert.Equal((EscenarioDeSteps.Valor(antes, "regardingobjectid") as EntityReference)?.Id, (EscenarioDeSteps.Valor(ahora, "regardingobjectid") as EntityReference)?.Id);
            Assert.Equal(fromOriginal, ElFromGuardado(e, id));
        }

        // ---------------------------------------------------------------- PA-EML-09 a PA-EML-12

        [Theory]
        [InlineData("soloRegarding")]
        [InlineData("direccionYRegarding")]
        [InlineData("soloDireccion")]
        public void PA_EML_09_UnBorradorCreadoFueraDelAlcance_PasadoASalienteDeUnaConversacionConOtroRemitente_CARP_EML_001(string forma)
        {
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var id = UnBorrador(e, forma, conversacion, e.OtroOficial);
            var antes = e.Fila(Correo, id);

            Rechaza(() => e.ComoOficial.Update(PasarASaliente(forma, id, conversacion, Partes(RefUsuario(e.OtroOficial)))), CodigosDeError.EML_001);

            AssertIntacto(e, id, antes, e.OtroOficial);
        }

        [Fact]
        public void PA_EML_09b_SoloElFromEnElUpdate_DeUnSalienteQueYaEstabaEnLaConversacion_CARP_EML_001()
        {
            // El estado guardado deja el correo dentro del alcance: el Update trae solo `from` y la regla lee directioncode y regardingobjectid.
            var e = UnEscenario();
            var id = UnSalienteDeConversacion(e, UnaConversacion(e));
            var antes = e.Fila(Correo, id);

            Rechaza(() => e.ComoOficial.Update(new Entity(Correo, id) { ["from"] = Partes(RefUsuario(e.OtroOficial)) }), CodigosDeError.EML_001);

            Assert.Equal(EscenarioDeSteps.Valor(antes, "subject"), EscenarioDeSteps.Valor(e.Fila(Correo, id), "subject"));
            Assert.False(e.Fila(Correo, id).Contains("from"));
        }

        [Theory]
        [InlineData("soloRegarding")]
        [InlineData("direccionYRegarding")]
        [InlineData("soloDireccion")]
        public void PA_EML_10_ElMismoUpdateSinFrom_PoneLaColaDelArea_ElFromGuardadoNoSeLeeNiSeRechaza(string forma)
        {
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var id = UnBorrador(e, forma, conversacion, e.OtroOficial);

            e.ComoOficial.Update(PasarASaliente(forma, id, conversacion, null));

            Assert.Equal(ColaDelArea, ElFromGuardado(e, id));
            var correo = e.Fila(Correo, id);
            Assert.True(Assert.IsType<bool>(correo["directioncode"]));
            Assert.Equal(conversacion, Assert.IsType<EntityReference>(correo["regardingobjectid"]).Id);
            var escritura = Assert.Single(EscriturasDe(e.Almacen, Correo, OperacionDeDatos.Update));
            Assert.Contains("from", escritura.Columnas);
        }

        [Fact]
        // VERDE A PROPÓSITO: lo cumple el filtro del registro (el pipeline no despierta a S-37 sin esas columnas), no la lógica del step; fija que el registro lo declare.
        public void PA_EML_11_UnUpdateQueNoTraeFromDirectionCodeNiRegarding_NoDespiertaAS37_NiLeeNada()
        {
            var e = UnEscenario();
            var id = UnSalienteDeConversacion(e, UnaConversacion(e));
            var servicios = e.Fabrica.ServiciosCreados.Count;

            e.ComoOficial.Update(new Entity(Correo, id) { ["subject"] = "ZZF-PRUEBA otro asunto" });

            Assert.DoesNotContain(EjecucionesDe<RemitenteDelAreaStep>(e), x => x.Step.Mensaje == "Update");
            Assert.Empty(LecturasDeCorreo(e));
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Tabla == Cola);
            Assert.Equal(servicios, e.Fabrica.ServiciosCreados.Count);
            Assert.Equal("ZZF-PRUEBA otro asunto", EscenarioDeSteps.Valor(e.Fila(Correo, id), "subject"));
        }

        [Fact]
        public void PA_EML_12_SystemTampocoPuedeEscribirOtroRemitente_EnElUpdate()
        {
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var id = UnBorrador(e, "soloRegarding", conversacion, e.OtroOficial);
            var antes = e.Fila(Correo, id);

            Rechaza(() => e.ComoSistema.Update(PasarASaliente("soloRegarding", id, conversacion, Partes(RefUsuario(e.OtroOficial)))), CodigosDeError.EML_001);

            AssertIntacto(e, id, antes, e.OtroOficial);
        }

        // ---------------------------------------------------------------- El resto de la regla, igual que en el Create

        [Theory]
        [InlineData("actor")]
        [InlineData("vacio")]
        [InlineData("cola")]
        public void PA5F_REM_V01_DentroDelAlcance_ElUsuarioQueEscribeElVacioOLaColaMisma_QuedanComoLaCola(string caso)
        {
            var e = UnEscenario();
            var id = UnSalienteDeConversacion(e, UnaConversacion(e));
            var desde = caso == "actor" ? Partes(RefUsuario(e.Oficial)) : caso == "vacio" ? Partes() : Partes(RefCola(ColaDelArea));

            e.ComoOficial.Update(new Entity(Correo, id) { ["from"] = desde });

            Assert.Equal(ColaDelArea, ElFromGuardado(e, id));
        }

        [Theory]
        [InlineData("otraCola")]
        [InlineData("colaYUsuario")]
        [InlineData("colaYCola")]
        [InlineData("usuarioYUsuario")]
        [InlineData("sinPartyId")]
        public void PA5F_REM_V02_DentroDelAlcance_CualquierOtraCombinacion_CARP_EML_001(string caso)
        {
            var e = UnEscenario();
            var otraCola = e.Sembrar(ConstructorDeDatos.UnaCola("zzf.otra.cola@example.com"));
            var id = UnSalienteDeConversacion(e, UnaConversacion(e));
            var desde = caso == "otraCola" ? Partes(RefCola(otraCola))
                : caso == "colaYUsuario" ? Partes(RefCola(ColaDelArea), RefUsuario(e.Oficial))
                : caso == "colaYCola" ? Partes(RefCola(ColaDelArea), RefCola(ColaDelArea))
                : caso == "usuarioYUsuario" ? Partes(RefUsuario(e.Oficial), RefUsuario(e.Oficial))
                : new EntityCollection(new[] { ParteSoloConDireccion("zzf.escrita.a.mano@example.com") });

            Rechaza(() => e.ComoOficial.Update(new Entity(Correo, id) { ["from"] = desde }), CodigosDeError.EML_001);
        }

        [Theory]
        [InlineData("sinCola")]
        [InlineData("dosColas")]
        public void PA5F_REM_V03_UnaFallaDeLaConfiguracionEnElUpdate_CFG_004_O_CFG_005_NoDejaCambios(string caso)
        {
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var id = UnBorrador(e, "soloRegarding", conversacion, e.OtroOficial);
            if (caso == "sinCola") e.ComoSistema.Delete(Cola, ColaDelArea);
            else e.Sembrar(ConstructorDeDatos.UnaCola(EscenarioDeSteps.Buzon));
            var antes = e.Fila(Correo, id);

            Rechaza(() => e.ComoOficial.Update(PasarASaliente("soloRegarding", id, conversacion, null)), caso == "sinCola" ? CodigosDeError.CFG_004 : CodigosDeError.CFG_005);

            AssertIntacto(e, id, antes, e.OtroOficial);
        }

        // ---------------------------------------------------------------- Lecturas y servicios

        [Fact]
        public void PA5F_REM_V04_ElUpdateDeS34SobreUnEntrante_SoloTraeElRegarding_CuestaUnaLecturaDeDosColumnasConSystem_YNoTocaElFrom()
        {
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);
            var id = e.Sembrar(ConstructorDeDatos.UnCorreoEntrante());
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(new Entity(Correo, id) { ["regardingobjectid"] = DeConversacion(conversacion) });

            var lectura = Assert.Single(LecturasDeCorreo(e));
            Assert.Equal(id, lectura.Id);
            Assert.Equal(e.Almacen.IdDeSystem, lectura.Identidad);
            Assert.False(lectura.TodasLasColumnas);
            Assert.Equal(new[] { "directioncode", "regardingobjectid" }, lectura.Columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Tabla == Cola);
            Assert.False(e.Fila(Correo, id).Contains("from"));
            Assert.Equal(conversacion, Assert.IsType<EntityReference>(e.Fila(Correo, id)["regardingobjectid"]).Id);
        }

        [Theory]
        [InlineData("direccionFalsa")]
        [InlineData("regardingNulo")]
        [InlineData("regardingCuenta")]
        public void PA5F_REM_V05_SiElTargetYaDejaElCorreoFueraDelAlcance_NoLeeNiConsultaLaColaNiCreaServicios(string caso)
        {
            // Ejecucion directa: un Regarding a una cuenta no se puede escribir en el almacen (la tabla no existe), pero el step lo recibiria asi.
            var e = UnEscenario();
            var id = UnSalienteDeConversacion(e, UnaConversacion(e));
            var cambio = new Entity(Correo, id) { ["from"] = Partes(RefUsuario(e.OtroOficial)) };
            if (caso == "direccionFalsa") cambio["directioncode"] = false;
            else if (caso == "regardingNulo") cambio["regardingobjectid"] = null;
            else cambio["regardingobjectid"] = new EntityReference("account", Guid.NewGuid());
            var servicios = e.Fabrica.ServiciosCreados.Count;

            EjecutarUpdateDirecto(e, new RemitenteDelAreaStep(), cambio, e.Oficial);

            Assert.Empty(LecturasDeCorreo(e));
            Assert.Empty(e.Almacen.Consultas);
            Assert.Equal(servicios, e.Fabrica.ServiciosCreados.Count);
        }

        [Fact]
        public void PA5F_REM_V06_ElFromQueSePone_EsUnaColeccionNueva_DeUnSoloActivityParty_ElRestoDelTargetQuedaComoVenia()
        {
            var e = UnEscenario();
            var id = UnSalienteDeConversacion(e, UnaConversacion(e));
            var original = Partes(RefUsuario(e.Oficial));
            var target = new Entity(Correo, id) { ["from"] = original, ["subject"] = "ZZF-PRUEBA otro asunto" };

            EjecutarUpdateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            Assert.NotSame(original, target["from"]);
            var nueva = Assert.IsType<EntityCollection>(target["from"]);
            var parte = Assert.Single(nueva.Entities);
            Assert.Equal(Parte, parte.LogicalName);
            Assert.Equal(new[] { "partyid" }, parte.Attributes.Keys.ToArray());
            var partyid = Assert.IsType<EntityReference>(parte["partyid"]);
            Assert.Equal(Cola, partyid.LogicalName);
            Assert.Equal(ColaDelArea, partyid.Id);
            Assert.Equal(new[] { "from", "subject" }, target.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA5F_REM_V07_ConLaColaYaPuesta_ElTargetQuedaIgual_LaMismaColeccion()
        {
            var e = UnEscenario();
            var id = UnSalienteDeConversacion(e, UnaConversacion(e));
            var original = Partes(RefCola(ColaDelArea));
            var target = new Entity(Correo, id) { ["from"] = original };

            EjecutarUpdateDirecto(e, new RemitenteDelAreaStep(), target, e.Oficial);

            Assert.Same(original, target["from"]);
        }

        [Fact]
        public void PA5F_REM_V08_LaTrazaDeUnRechazoEnElUpdate_LlevaElCodigo_YNingunDatoDelCorreo()
        {
            var e = UnEscenario();
            var id = UnSalienteDeConversacion(e, UnaConversacion(e));
            var target = new Entity(Correo, id) { ["from"] = new EntityCollection(new[] { ParteSoloConDireccion("secreto@zzf.example.com") }), ["subject"] = "ZZF-ASUNTO-SECRETO" };
            var proveedor = ProveedorUpdateConEspia(e, target, e.Oficial);

            Rechaza(() => new RemitenteDelAreaStep().Execute(proveedor), CodigosDeError.EML_001);

            var lineas = proveedor.Espia.Lineas();
            Assert.Contains(lineas, l => l.Contains("rechazo CARP-EML-001"));
            Assert.DoesNotContain(lineas, l => l.Contains("ZZF-ASUNTO-SECRETO") || l.Contains("secreto@zzf.example.com"));
        }

        [Fact]
        public void PA5F_REM_V09_ElCreateSigueIgual_ElUpdateNoLoCambia_UnCreateConRegardingPoneLaCola()
        {
            // El Create no leyo ni lee lo guardado: 0 lecturas del correo por el pipeline.
            var e = UnEscenario();
            var conversacion = UnaConversacion(e);

            var id = e.ComoOficial.Create(UnaSaliente(conversacion, Partes(RefUsuario(e.Oficial))));

            Assert.Equal(ColaDelArea, ElFromGuardado(e, id));
            Assert.Empty(LecturasDeCorreo(e));
            Assert.DoesNotContain(EjecucionesDe<RemitenteDelAreaStep>(e), x => x.Step.Mensaje == "Update");
        }
    }
}
