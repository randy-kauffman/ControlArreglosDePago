using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Aceptacion.Formato;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeVersion;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>VersionPlantillaActualizarStep</c> (diseno/03 2.8, 3.3 y 5, S-26). PA-VER-03 a PA-VER-14, PA-VER-16, PA-VER-17, PA-VER-22 a PA-VER-26 y PA-VER-09 a PA-VER-13 son los del diseño con ese mismo ID; los que
    /// agrega el arquitecto del sublote 5e llevan el prefijo <c>PA5E_VAC</c>. Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase (sin filtro y pre-imagen
    /// <c>PreImagen</c> de 8 columnas) y, en Publicar, con el generador REAL y <c>Fixtures/F-NIC-0000052_v07.xlsx</c>. Las pruebas que mandan <c>sanic_archivo</c> corren DIRECTO (el doble no deja escribirlo).
    /// <para>
    /// <b>La hora.</b> El step lee <c>contexto.Reloj</c>, el reloj de producción: la fecha de aprobación se afirma contra una ventana [antes, después] tomada con <c>DateTime.UtcNow</c> alrededor de la llamada.
    /// </para>
    /// </summary>
    public class VersionPlantillaActualizarStepAceptacion
    {
        private static EscenarioDeSteps SoloS26()
        {
            return Escenario(propietario: false, actualizar: true);
        }

        private static byte[] NoEsUnLibro()
        {
            return System.Text.Encoding.UTF8.GetBytes("esto no es un libro de Excel");
        }

        /// <summary>Un valor DISTINTO del de la fila de partida, válido para el esquema, de cada columna vigilada.</summary>
        private static object ValorNuevo(EscenarioDeSteps e, string columna)
        {
            switch (columna)
            {
                case "statecode": return new OptionSetValue(Estados.Inactivo);
                case "statuscode": return new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo));
                case "sanic_nombre": return "v99";
                case "sanic_capacidad": return 48;
                case "sanic_huella": return "otra-huella-distinta";
                case "sanic_fechaaprobacion": return DateTime.UtcNow.AddDays(-1);
                case "sanic_aprobadoporid": return new EntityReference(ApoyoDeArreglo.Usuario, e.Oficial);
                default: throw new ArgumentOutOfRangeException(nameof(columna), columna, "Columna sin valor nuevo en el apoyo.");
            }
        }

        private static void AssertSinEscrituras(EscenarioDeSteps e)
        {
            Assert.Empty(Escrituras(e, TablaVersion));
        }

        // ---------------------------------------------------------------- PA-VER del diseño: Borrador

        [Theory]
        [InlineData("sanic_capacidad")]
        [InlineData("sanic_nombre")]
        public void PA_VER_12a_UnJefeEditaLaCapacidadOElNombreValidoDeUnBorrador_Pasa_ElStepNoAgregaColumnas(string columna)
        {
            var e = SoloS26();
            var id = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, columna, ValorNuevo(e, columna)));

            Assert.Equal(ValorNuevo(e, columna), Valor(e, id, columna));
            Assert.Empty(AgregadasPorLosSteps(Assert.Single(Escrituras(e, TablaVersion))));
        }

        [Theory]
        [InlineData("sanic_huella")]
        [InlineData("sanic_fechaaprobacion")]
        [InlineData("sanic_aprobadoporid")]
        public void PA_VER_12b_UnJefeEscribeHuellaFechaOAprobadorEnUnBorrador_CARP_VER_002_YLaFilaNoCambia(string columna)
        {
            var e = SoloS26();
            var id = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, columna, ValorNuevo(e, columna))), CodigosDeError.VER_002);

            Assert.Null(Valor(e, id, columna));
            AssertSinEscrituras(e);
        }

        [Theory]
        [InlineData("aHistorica")]
        [InlineData("aNulo")]
        public void PA_VER_12c_UnJefePasaUnBorradorAHistoricaOLoDejaSinEstado_CARP_VER_004(string caso)
        {
            var e = SoloS26();
            var id = UnaVersion(e);
            e.Almacen.LimpiarRegistros();
            var estado = caso == "aHistorica" ? Opcion(EstadoDeVersionDePlantilla.Historica) : null;

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_estado", estado)), CodigosDeError.VER_004);

            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_VER_12d_UnJefePasaUnaVigenteABorrador_CARP_VER_003()
        {
            var e = SoloS26();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_estado", Opcion(EstadoDeVersionDePlantilla.Borrador))), CodigosDeError.VER_003);

            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Estado(e, id));
        }

        // ---------------------------------------------------------------- PA-VER del diseño: Vigente e Histórica

        [Theory]
        [InlineData("statecode")]
        [InlineData("sanic_nombre")]
        [InlineData("sanic_capacidad")]
        [InlineData("sanic_huella")]
        [InlineData("sanic_fechaaprobacion")]
        [InlineData("sanic_aprobadoporid")]
        public void PA_VER_10_UnJefeCambiaCualquieraDeLasColumnasDeUnaVigente_CARP_VER_003_YLaFilaNoCambia(string columna)
        {
            var e = SoloS26();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var antes = e.Fila(TablaVersion, id);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, columna, ValorNuevo(e, columna))), CodigosDeError.VER_003);

            AssertSinEscrituras(e);
            Assert.Equal(EscenarioDeSteps.Valor(antes, columna), Valor(e, id, columna));
        }

        [Theory]
        [InlineData("capacidad", false)]
        [InlineData("desactivar", false)]
        [InlineData("reactivar", true)]
        public void PA_VER_11_25_UnJefeEditaLaCapacidadDesactivaOReactivaUnaHistorica_CARP_VER_003(string caso, bool inactiva)
        {
            var e = SoloS26();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Historica, "v06", inactiva: inactiva);
            e.Almacen.LimpiarRegistros();
            var cambio = caso == "capacidad" ? Cambio(id, "sanic_capacidad", 48)
                : caso == "desactivar" ? Cambio(id, "statecode", new OptionSetValue(Estados.Inactivo))
                : Cambio(id, "statecode", new OptionSetValue(Estados.Activo), "statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo)));

            Rechaza(() => e.ComoOficial.Update(cambio), CodigosDeError.VER_003);

            AssertSinEscrituras(e);
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Borrador)]
        public void PA_VER_22_UnJefeEscribeVigenteOBorradorEnUnaHistorica_CARP_VER_003(EstadoDeVersionDePlantilla destino)
        {
            var e = SoloS26();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Historica, "v06");
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_estado", Opcion(destino))), CodigosDeError.VER_003);

            Assert.Equal((int)EstadoDeVersionDePlantilla.Historica, Estado(e, id));
        }

        [Fact]
        public void PA_VER_23_UnJefeEscribeHistoricaEnUnaVigente_CARP_VER_003()
        {
            var e = SoloS26();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_estado", Opcion(EstadoDeVersionDePlantilla.Historica))), CodigosDeError.VER_003);

            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Estado(e, id));
        }

        // ---------------------------------------------------------------- PA-VER-24: el estado nativo

        [Theory]
        [InlineData("soloStatecode")]
        [InlineData("statecodeYStatuscode")]
        [InlineData("publicarConStatecode")]
        public void PA_VER_24_UnJefeDesactivaUnBorradorPorPatch_CARP_VER_014_EnLosTresCasos_YLaVersionSigueActivaYEnBorrador(string caso)
        {
            var e = SoloS26();
            var id = UnaVersion(e);
            e.Almacen.LimpiarRegistros();
            var inactivo = new OptionSetValue(Estados.Inactivo);
            var cambio = caso == "soloStatecode" ? Cambio(id, "statecode", inactivo)
                : caso == "statecodeYStatuscode" ? Cambio(id, "statecode", inactivo, "statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo)))
                : Cambio(id, "statecode", inactivo, "sanic_estado", Opcion(EstadoDeVersionDePlantilla.Vigente));

            Rechaza(() => e.ComoOficial.Update(cambio), CodigosDeError.VER_014);

            Assert.Equal(Estados.Activo, Assert.IsType<OptionSetValue>(Valor(e, id, "statecode")).Value);
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
            AssertSinEscrituras(e);
        }

        // ---------------------------------------------------------------- PA-VER-13 y PA-VER-26: el sistema

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA_VER_26_ElSistemaCambiaStatecodeYStatuscode_PasaSinValidar_EnLosTresEstados_SinTocarElTarget(EstadoDeVersionDePlantilla estado)
        {
            var e = SoloS26();
            var id = UnaVersion(e, estado, "v07");
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "statecode", new OptionSetValue(Estados.Inactivo), "statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo))));

            Assert.Equal(Estados.Inactivo, Assert.IsType<OptionSetValue>(Valor(e, id, "statecode")).Value);
            Assert.Empty(AgregadasPorLosSteps(Assert.Single(Escrituras(e, TablaVersion))));
        }

        [Fact]
        public void PA_VER_13_ElSistemaPasaVigenteAHistorica_PasaSinValidar()
        {
            var e = SoloS26();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "sanic_estado", Opcion(EstadoDeVersionDePlantilla.Historica)));

            Assert.Equal((int)EstadoDeVersionDePlantilla.Historica, Estado(e, id));
            Assert.Empty(ConsultasDe(e, TablaMapeo));
        }

        // ---------------------------------------------------------------- PA-VER-09: Publicar no admite otros cambios

        [Theory]
        [InlineData("capacidad")]
        [InlineData("nombreValido")]
        [InlineData("nombreInvalido")]
        public void PA_VER_09_PublicarYCambiarLaCapacidadOElNombreALaVez_CARP_VER_013_AunConUnNombreInvalido_ElBorradorNoCambia(string caso)
        {
            var e = SoloS26();
            var id = UnBorradorPublicable(e);
            e.Almacen.LimpiarRegistros();
            var cambio = Publicar(id);
            if (caso == "capacidad") cambio["sanic_capacidad"] = 48;
            else cambio["sanic_nombre"] = caso == "nombreValido" ? "v09" : "x";

            Rechaza(() => e.ComoOficial.Update(cambio), CodigosDeError.VER_013);

            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
            AssertSinEscrituras(e);
        }

        [Theory]
        [InlineData("valor")]
        [InlineData("nulo")]
        public void PA5E_VAC_38_PublicarYMandarSanicArchivoEnElMismoGuardado_CARP_VER_013_ConCualquierValor_Directo(string caso)
        {
            var e = SoloS26();
            var id = UnBorradorPublicable(e);
            var target = Publicar(id);
            target["sanic_archivo"] = caso == "valor" ? (object)Guid.NewGuid() : null;
            e.Almacen.LimpiarRegistros();

            Rechaza(() => EjecutarDirecto(e, new VersionPlantillaActualizarStep(), id, e.Oficial, target), CodigosDeError.VER_013);

            Assert.Empty(e.Almacen.Consultas);
            Assert.False(target.Contains("sanic_huella"));
        }

        // ---------------------------------------------------------------- Publicar con el generador real

        [Fact]
        public void PA_VER_03_PublicarLaV08_ConLaV07Vigente_FijaHuellaFechaYAprobador_LaV07PasaAHistorica_YQuedaUnaSolaVigente()
        {
            var e = Escenario(propietario: true, actualizar: true, archivo: true);
            var v07 = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var v08 = UnBorradorPublicable(e);
            var otroBorrador = UnaVersion(e, EstadoDeVersionDePlantilla.Borrador, "v09");
            e.Almacen.LimpiarRegistros();
            var antes = DateTime.UtcNow;

            e.ComoOficial.Update(Publicar(v08));

            var despues = DateTime.UtcNow;
            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Estado(e, v08));
            Assert.Equal(Plantillas.Sha256(Plantillas.V07), Assert.IsType<string>(Valor(e, v08, "sanic_huella")));
            var fecha = DateTime.SpecifyKind(Assert.IsType<DateTime>(Valor(e, v08, "sanic_fechaaprobacion")), DateTimeKind.Utc);
            Assert.InRange(fecha, antes.AddSeconds(-1), despues.AddSeconds(1));
            var aprobador = Assert.IsType<EntityReference>(Valor(e, v08, "sanic_aprobadoporid"));
            Assert.Equal(ApoyoDeArreglo.Usuario, aprobador.LogicalName);
            Assert.Equal(e.Oficial, aprobador.Id);
            Assert.Equal((int)EstadoDeVersionDePlantilla.Historica, Estado(e, v07));
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, otroBorrador));
            Assert.Equal(new[] { v08 }, VigentesActivas(e).ToArray());
            Assert.All(e.Pipeline.Ejecuciones, x => Assert.Null(x.Excepcion));
        }

        [Fact]
        public void PA_VER_04_LaPrimeraPublicacion_SinVigentePrevia_Pasa()
        {
            var e = SoloS26();
            var v08 = UnBorradorPublicable(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Publicar(v08));

            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Estado(e, v08));
            Assert.Equal(new[] { v08 }, VigentesActivas(e).ToArray());
            var escritura = Assert.Single(Escrituras(e, TablaVersion));
            Assert.Equal(new[] { "sanic_aprobadoporid", "sanic_estado", "sanic_fechaaprobacion", "sanic_huella" }, Ordenadas(escritura.Columnas));
        }

        [Fact]
        public void PA_VER_05a_PublicarSinArchivo_CARP_VER_005_ElBorradorSigueIgual()
        {
            var e = SoloS26();
            var id = UnaVersion(e);
            SembrarMapeo(e, id, MapeoV07());
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Publicar(id)), CodigosDeError.VER_005);

            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
            Assert.Null(Valor(e, id, "sanic_huella"));
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_VER_05b_PublicarUnArchivoQueNoEsUnLibro_CARP_VER_006()
        {
            var e = SoloS26();
            var id = UnaVersion(e, archivo: NoEsUnLibro());
            SembrarMapeo(e, id, MapeoV07());
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Publicar(id)), CodigosDeError.VER_006);

            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_VER_06a_PublicarConElMapeoVacio_CARP_VER_007()
        {
            var e = SoloS26();
            var id = UnaVersion(e, archivo: Plantillas.V07);
            e.Almacen.LimpiarRegistros();
            var errores = ValidadorDePublicacion.Validar(60, new List<FilaDeMapeo>());

            RechazaConTexto(() => e.ComoOficial.Update(Publicar(id)), errores[0].Codigo, string.Join("\n", errores.Select(x => x.Mensaje)));

            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
        }

        [Fact]
        public void PA_VER_06b_PublicarConDosCamposEnUnaCeldaYUnaCeldaMal_CARP_VER_007_ConTodosLosErrores()
        {
            var e = SoloS26();
            var id = UnaVersion(e, archivo: Plantillas.V07);
            var filas = MapeoV07();
            filas.Add(new FilaDeMapeo(HojaFormato, "B7", TipoDeMapeo.ValorSimple, CampoMapeable.Comentarios));
            filas.Add(new FilaDeMapeo(HojaFormato, "B0", TipoDeMapeo.ValorSimple, CampoMapeable.CuentaADebitar));
            SembrarMapeo(e, id, filas);
            e.Almacen.LimpiarRegistros();
            var errores = ValidadorDePublicacion.Validar(60, filas);
            Assert.True(errores.Count >= 2, "La guardia: el mapeo armado debe dar al menos dos errores del validador.");

            // Las filas del mapeo se leen "sin orden": el texto de cada error (cuál campo va primero en "dos campos en la misma celda") puede variar; el conjunto de errores, no.
            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(Publicar(id)));

            Assert.StartsWith(errores[0].Codigo + ": ", rechazo.Message);
            Assert.Contains("dos campos en la misma celda 'B7'", rechazo.Message);
            Assert.Contains("'B0'", rechazo.Message);
            Assert.Contains("mal escrita", rechazo.Message);
            Assert.Equal(errores.Count, rechazo.Message.Split('\n').Length);
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
        }

        [Fact]
        public void PA_VER_07_PublicarConCapacidad48YUnBloqueDe60_CARP_VER_008()
        {
            var e = SoloS26();
            var id = UnaVersion(e, capacidad: 48, archivo: Plantillas.V07);
            SembrarMapeo(e, id, MapeoV07(60));
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Publicar(id)), CodigosDeError.VER_008, 48);

            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
        }

        [Fact]
        public void PA_VER_08_PublicarConElMapeoApuntandoALaHojaFORMATO_CARP_VER_009_ConLasHojasRealesEntreComillas()
        {
            var e = SoloS26();
            var id = UnaVersion(e, archivo: Plantillas.V07);
            SembrarMapeo(e, id, MapeoV07().Select(f => f.Hoja == HojaFormato ? new FilaDeMapeo("FORMATO", f.Celda, f.Tipo, f.Campo, f.FilasMaximas) : f));
            e.Almacen.LimpiarRegistros();

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(Publicar(id)));

            Assert.StartsWith("CARP-VER-009: ", rechazo.Message);
            Assert.Contains("'FORMATO'", rechazo.Message);
            Assert.Contains("' FORMATO ARP ELECTRONICO'", rechazo.Message);
            Assert.Contains("'CONTROL DE PAGOS'", rechazo.Message);
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_VER_14_TrasPublicarQuedaOtraVigente_CARP_VER_010_ElBorradorSigueEnBorrador_YLaAnteriorVigente()
        {
            // Un StepDePrueba, después de S-26, crea otra Vigente cuando ve que la anterior pasa a Histórica: el dato inconsistente que la segunda consulta de OtrasVigentes tiene que ver.
            var e = SoloS26();
            var v07 = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var v08 = UnBorradorPublicable(e);
            var creada = RegistrarElQueCreaOtraVigente(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Publicar(v08)), CodigosDeError.VER_010);

            Assert.True(creada(), "La guardia: el StepDePrueba tenía que ver el paso de la anterior a Histórica.");
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, v08));
            Assert.Null(Valor(e, v08, "sanic_huella"));
            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Estado(e, v07));
            Assert.Equal(2, e.Almacen.Todas(TablaVersion).Count);
        }

        /// <summary>
        /// Registra un step de prueba, DESPUÉS de S-26, que crea otra Vigente (como SYSTEM) cuando ve que una versión pasa a Histórica: el dato inconsistente que la segunda consulta de
        /// <c>OtrasVigentes</c> tiene que ver. Devuelve una función que dice si llegó a crearla.
        /// </summary>
        private static Func<bool> RegistrarElQueCreaOtraVigente(EscenarioDeSteps e)
        {
            var creada = false;
            var registros = new[] { new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 30, new[] { TablaVersion }, null, null) };
            e.Registrar(new StepDePrueba("VER", registros, contexto =>
            {
                var estado = contexto.Target == null ? null : contexto.Target.GetAttributeValue<OptionSetValue>("sanic_estado");
                if (creada || estado == null || estado.Value != (int)EstadoDeVersionDePlantilla.Historica) return;
                creada = true;
                var otra = new Entity(TablaVersion);
                otra["sanic_nombre"] = "v99";
                otra["sanic_capacidad"] = 60;
                otra["sanic_estado"] = Opcion(EstadoDeVersionDePlantilla.Vigente);
                otra["sanic_huella"] = HuellaDePartida;
                otra["sanic_fechaaprobacion"] = DateTime.UtcNow;
                otra["sanic_aprobadoporid"] = new EntityReference(ApoyoDeArreglo.Usuario, e.OtroOficial);
                otra["ownerid"] = new EntityReference("team", e.TeamDelArea);
                contexto.ServicioComoSistema.Create(otra);
            }));
            return () => creada;
        }

        [Fact]
        public void PA5E_VAC_41_UnRechazoDelCierre_CARP_VER_010_NoDejaElTargetAMedioEscribir_Directo()
        {
            // Por eso el Target se escribe AL FINAL: no hay nada que revertir en él si el cierre de la anterior falla.
            var e = SoloS26();
            UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var v08 = UnBorradorPublicable(e);
            var creada = RegistrarElQueCreaOtraVigente(e);
            var target = Publicar(v08);

            Rechaza(() => EjecutarDirecto(e, new VersionPlantillaActualizarStep(), v08, e.Oficial, target), CodigosDeError.VER_010);

            Assert.True(creada(), "La guardia: el StepDePrueba tenía que ver el paso de la anterior a Histórica.");
            Assert.Equal(new[] { "sanic_estado" }, target.Attributes.Keys.ToArray());
        }

        [Fact]
        public void PA_VER_16_Publicar_NoEscribeNadaFueraDeLoDePublicacion_ElRegistroDelDobleLoDemuestra()
        {
            var e = SoloS26();
            var v07 = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var v08 = UnBorradorPublicable(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Publicar(v08));

            Assert.Equal(2, e.Almacen.Escrituras.Count);
            Assert.All(e.Almacen.Escrituras, x => Assert.Equal(OperacionDeDatos.Update, x.Operacion));
            Assert.All(e.Almacen.Escrituras, x => Assert.Equal(TablaVersion, x.Tabla));
            var propia = Assert.Single(e.Almacen.Escrituras, x => x.Id == v08);
            var anterior = Assert.Single(e.Almacen.Escrituras, x => x.Id == v07);
            Assert.Equal(e.Oficial, propia.Identidad);
            Assert.Equal(new[] { "sanic_aprobadoporid", "sanic_estado", "sanic_fechaaprobacion", "sanic_huella" }, Ordenadas(propia.Columnas));
            Assert.Equal(e.Almacen.IdDeSystem, anterior.Identidad);
            Assert.Equal(new[] { "sanic_estado" }, anterior.Columnas.ToArray());
        }

        [Theory]
        [InlineData("exito")]
        [InlineData("formato009")]
        [InlineData("plantilla006")]
        [InlineData("mapeo007")]
        public void PA_VER_17_LaTrazaDeLaPublicacion_NoLlevaDatosSinteticosNiNombresDeHoja_NiDeVersion(string caso)
        {
            var e = SoloS26();
            var archivo = caso == "plantilla006" ? NoEsUnLibro() : Plantillas.V07;
            var id = UnaVersion(e, nombre: "v81", archivo: archivo);
            var filas = MapeoV07();
            if (caso == "formato009") filas = filas.Select(f => f.Hoja == HojaFormato ? new FilaDeMapeo("FORMATO", f.Celda, f.Tipo, f.Campo, f.FilasMaximas) : f).ToList();
            if (caso == "mapeo007") filas = new List<FilaDeMapeo>();
            SembrarMapeo(e, id, filas);
            var contexto = ContextoDeActualizacion(e, id, e.Oficial, Publicar(id));
            var proveedor = new ProveedorConEspia(contexto, e.Fabrica);

            Record.Exception(() => new VersionPlantillaActualizarStep().Execute(proveedor));

            var lineas = proveedor.Espia.Lineas();
            Assert.NotEmpty(lineas);
            Assert.DoesNotContain(lineas, l => l.Contains("PRUEBA"));
            Assert.DoesNotContain(lineas, l => l.Contains("FORMATO"));
            Assert.DoesNotContain(lineas, l => l.Contains("CONTROL DE PAGOS"));
            Assert.DoesNotContain(lineas, l => l.Contains("v81"));
            if (caso == "exito") Assert.Contains(lineas, l => l.Contains("salida correcta"));
            else Assert.Contains(lineas, l => l.Contains("rechazo CARP-VER-"));
        }

        [Fact]
        public void PA5E_VAC_30_PublicarLeeUnaVezElArchivoYElMapeo_YDosVecesLasOtrasVigentes_TodoConSystem()
        {
            var e = SoloS26();
            UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var v08 = UnBorradorPublicable(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Publicar(v08));

            var mapeo = Assert.Single(ConsultasDe(e, TablaMapeo));
            Assert.Equal(OperacionDeDatos.RetrieveMultiple, mapeo.Operacion);
            var deLaVersion = ConsultasDe(e, TablaVersion);
            var archivo = Assert.Single(deLaVersion, c => c.Operacion == OperacionDeDatos.Retrieve);
            Assert.Equal(v08, archivo.Id);
            Assert.Equal(new[] { "sanic_archivo" }, archivo.Columnas.ToArray());
            Assert.Equal(2, deLaVersion.Count(c => c.Operacion == OperacionDeDatos.RetrieveMultiple));
            Assert.Equal(4, e.Almacen.Consultas.Count);
            Assert.All(e.Almacen.Consultas, c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
            Assert.All(e.Almacen.Consultas, c => Assert.False(c.TodasLasColumnas));
        }

        [Fact]
        public void PA5E_VAC_31_ConDosVigentesPrevias_AmbasPasanAHistorica_YQuedaSoloLaNueva()
        {
            var e = SoloS26();
            var a = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v05");
            var b = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v06");
            var v08 = UnBorradorPublicable(e);

            e.ComoOficial.Update(Publicar(v08));

            Assert.Equal((int)EstadoDeVersionDePlantilla.Historica, Estado(e, a));
            Assert.Equal((int)EstadoDeVersionDePlantilla.Historica, Estado(e, b));
            Assert.Equal(new[] { v08 }, VigentesActivas(e).ToArray());
        }

        [Fact]
        public void PA5E_VAC_32_LaEscrituraDeLaAnterior_DisparaS26AProfundidad2_ComoSystem_YLaDejaPasar()
        {
            var e = SoloS26();
            var v07 = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var v08 = UnBorradorPublicable(e);

            e.ComoOficial.Update(Publicar(v08));

            var ejecuciones = EjecucionesDe<VersionPlantillaActualizarStep>(e);
            Assert.Equal(2, ejecuciones.Count);
            Assert.All(ejecuciones, x => Assert.Null(x.Excepcion));
            Assert.Equal(1, ejecuciones[0].Contexto.Depth);
            Assert.Equal(e.Oficial, ejecuciones[0].Contexto.InitiatingUserId);
            Assert.Equal(v08, ejecuciones[0].Contexto.PrimaryEntityId);
            Assert.Equal(2, ejecuciones[1].Contexto.Depth);
            Assert.Equal(e.Almacen.IdDeSystem, ejecuciones[1].Contexto.InitiatingUserId);
            Assert.Equal(v07, ejecuciones[1].Contexto.PrimaryEntityId);
        }

        [Fact]
        public void PA5E_VAC_33_UnaVigenteInactiva_NoSeToca_NoCuentaComoOtraVigente()
        {
            var e = SoloS26();
            var inactiva = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v05", inactiva: true);
            var v08 = UnBorradorPublicable(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Publicar(v08));

            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Estado(e, inactiva));
            Assert.Equal(Estados.Inactivo, Assert.IsType<OptionSetValue>(Valor(e, inactiva, "statecode")).Value);
            Assert.DoesNotContain(Escrituras(e, TablaVersion), x => x.Id == inactiva);
            Assert.Equal(new[] { v08 }, VigentesActivas(e).ToArray());
        }

        [Fact]
        public void PA5E_VAC_34_ElSistemaPasaUnBorradorAVigente_NoValidaNiGeneraNiCierraLaAnterior()
        {
            // DC-03: una escritura del sistema que hace lo mismo que Publicar no lee el archivo ni el mapeo ni toca la otra Vigente.
            var e = SoloS26();
            var anterior = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var id = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Publicar(id));

            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Estado(e, id));
            Assert.Null(Valor(e, id, "sanic_huella"));
            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Estado(e, anterior));
            Assert.Empty(e.Almacen.Consultas);
            Assert.Single(e.Almacen.Escrituras);
        }

        [Fact]
        public void PA5E_VAC_35_UnRechazoDePublicar_NoDejaElTargetAMedioEscribir_Directo()
        {
            var e = SoloS26();
            var id = UnaVersion(e);
            SembrarMapeo(e, id, MapeoV07());
            var target = Publicar(id);

            Rechaza(() => EjecutarDirecto(e, new VersionPlantillaActualizarStep(), id, e.Oficial, target), CodigosDeError.VER_005);

            Assert.Equal(new[] { "sanic_estado" }, target.Attributes.Keys.ToArray());
        }

        [Fact]
        public void PA5E_VAC_36_PublicarEscribeElTargetAlFinal_ConLasTresColumnasYNingunaMas_Directo()
        {
            var e = SoloS26();
            var id = UnBorradorPublicable(e);
            var target = Publicar(id);

            EjecutarDirecto(e, new VersionPlantillaActualizarStep(), id, e.Oficial, target);

            Assert.Equal(new[] { "sanic_aprobadoporid", "sanic_estado", "sanic_fechaaprobacion", "sanic_huella" }, Ordenadas(target.Attributes.Keys));
            Assert.Equal(Plantillas.Sha256(Plantillas.V07), Assert.IsType<string>(target["sanic_huella"]));
            var aprobador = Assert.IsType<EntityReference>(target["sanic_aprobadoporid"]);
            Assert.Equal(ApoyoDeArreglo.Usuario, aprobador.LogicalName);
            Assert.Equal(e.Oficial, aprobador.Id);
            Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(target["sanic_fechaaprobacion"]).Kind);
        }

        [Fact]
        public void PA5E_VAC_37_UnaFallaAlEscribirLaAnterior_NoSeAtrapa_SaleCARP_VER_900_YNadaQuedaEscrito()
        {
            var e = SoloS26();
            var v07 = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var v08 = UnBorradorPublicable(e);
            e.Almacen.FallarEn(OperacionDeDatos.Update, TablaVersion, 2, CodigosDeFalla.TransaccionAbortada);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(Publicar(v08)));

            Assert.StartsWith("CARP-VER-900: ", rechazo.Message);
            Assert.Single(ConsultasDe(e, TablaMapeo));        // la falla vino del cierre de la anterior, DESPUÉS de validar el mapeo, y no de otro lado
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, v08));
            Assert.Null(Valor(e, v08, "sanic_huella"));
            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Estado(e, v07));
        }

        [Fact]
        public void PA5E_VAC_39_UnaVezPublicada_LaVersionEsInmutable_UnJefeNoPuedeEditarla()
        {
            var e = SoloS26();
            var v08 = UnBorradorPublicable(e);
            e.ComoOficial.Update(Publicar(v08));
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(v08, "sanic_capacidad", 48)), CodigosDeError.VER_003);

            Assert.Equal(60, Assert.IsType<int>(Valor(e, v08, "sanic_capacidad")));
        }

        // ---------------------------------------------------------------- lo que agrega el sublote 5e

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA5E_VAC_11_LaFilaCompletaReenviadaIgual_NoEsCambio_NoPreguntaLaIdentidadNiLeeNada(EstadoDeVersionDePlantilla estado)
        {
            var e = SoloS26();
            var id = UnaVersion(e, estado, "v07");
            var fila = e.Fila(TablaVersion, id);
            var reenvio = new Entity(TablaVersion, id);
            foreach (var columna in ReglasDeVersion.ColumnasDeLaPreImagen)
                if (fila.Contains(columna)) reenvio[columna] = fila[columna];
            e.Almacen.LimpiarRegistros();
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(reenvio);

            Assert.Empty(e.Almacen.Consultas);
            Assert.Equal((int)estado, Estado(e, id));
        }

        [Fact]
        public void PA5E_VAC_12_HuellaFechaYAprobadorEnNuloExplicito_NoSonCambio_EnUnBorrador_PorqueElStepCompletaLaPreImagen()
        {
            // Dev omite las columnas nulas de la pre-imagen (40_v6_pre_imagen.json): si el step no las completara con null, "ausente" contaría como cambio y daría CARP-VER-002.
            var e = SoloS26();
            var id = UnaVersion(e);
            e.Almacen.LimpiarRegistros();
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_huella", null, "sanic_fechaaprobacion", null, "sanic_aprobadoporid", null));

            Assert.Null(Valor(e, id, "sanic_huella"));
            Assert.Empty(AgregadasPorLosSteps(Assert.Single(Escrituras(e, TablaVersion))));
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Estado(e, id));
        }

        [Fact]
        public void PA5E_VAC_12b_UnNuloSobreUnaColumnaConValor_SiEsCambio_EnUnaVigente()
        {
            var e = SoloS26();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_huella", null)), CodigosDeError.VER_003);

            Assert.Equal(HuellaDePartida, Assert.IsType<string>(Valor(e, id, "sanic_huella")));
        }

        [Fact]
        public void PA5E_VAC_13_LaPreImagenDelRegistro_TraeLasColumnasQueTieneLaFila_SinLasNulas_YElStepNoEscribeNada()
        {
            var e = SoloS26();
            var id = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_capacidad", 48));

            var ejecucion = Assert.Single(EjecucionesDe<VersionPlantillaActualizarStep>(e));
            Assert.Null(ejecucion.Excepcion);
            var imagen = ejecucion.Contexto.PreEntityImages["PreImagen"];
            Assert.Equal(Ordenadas(new[] { "statecode", "statuscode", "sanic_nombre", "sanic_estado", "sanic_capacidad", "sanic_carp_tbl_versionplantillaid" }), Ordenadas(imagen.Attributes.Keys));   // las nulas (huella, fecha y aprobador) no vienen
            Assert.Empty(AgregadasPorLosSteps(Assert.Single(Escrituras(e, TablaVersion))));
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA5E_VAC_14_UnUpdateQueSoloTraeSanicArchivo_NoEsCambioParaS26_Pasa_Directo(EstadoDeVersionDePlantilla estado)
        {
            // El archivo lo decide S-27, no S-26 (su pre-imagen no lo incluye).
            var e = SoloS26();
            var id = UnaVersion(e, estado, "v07");
            var target = new Entity(TablaVersion, id);
            target["sanic_archivo"] = Guid.NewGuid();
            e.Almacen.LimpiarRegistros();

            EjecutarDirecto(e, new VersionPlantillaActualizarStep(), id, e.Oficial, target);

            Assert.Empty(e.Almacen.Consultas);
            Assert.Empty(e.Almacen.Escrituras);
            Assert.Equal(new[] { "sanic_archivo" }, target.Attributes.Keys.ToArray());
        }

        [Fact]
        public void PA5E_VAC_15_LaIdentidadEsElIniciador_NoUserId_Directo()
        {
            var e = SoloS26();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var sistema = e.Almacen.IdDeSystem;

            // UserId = SYSTEM pero el iniciador es una persona (V-4): se evalúa como persona.
            Rechaza(() => EjecutarDirecto(e, new VersionPlantillaActualizarStep(), id, e.Oficial, Cambio(id, "sanic_capacidad", 48), c => c.UserId = sistema), CodigosDeError.VER_003);
            // UserId = persona pero el iniciador es SYSTEM: se deja pasar.
            EjecutarDirecto(e, new VersionPlantillaActualizarStep(), id, sistema, Cambio(id, "sanic_capacidad", 48), c => c.UserId = e.Oficial);
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador, false)]
        [InlineData(EstadoDeVersionDePlantilla.Borrador, true)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, false)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, true)]
        [InlineData(EstadoDeVersionDePlantilla.Historica, false)]
        [InlineData(EstadoDeVersionDePlantilla.Historica, true)]
        public void PA5E_VAC_16_DC_29_NoAplica_ReenviarElMismoStatecodeOStatuscode_NoEsCambio_NoPreguntaLaIdentidad(EstadoDeVersionDePlantilla estado, bool conStatuscode)
        {
            var e = SoloS26();
            var id = UnaVersion(e, estado, "v07");
            var cambio = conStatuscode
                ? Cambio(id, "statecode", new OptionSetValue(Estados.Activo), "statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo)))
                : Cambio(id, "statecode", new OptionSetValue(Estados.Activo));
            e.Almacen.LimpiarRegistros();
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(cambio);

            Assert.Equal(Estados.Activo, Assert.IsType<OptionSetValue>(Valor(e, id, "statecode")).Value);
        }
    }
}
