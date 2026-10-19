using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeMapeo;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>MapeoCeldaActualizarStep</c> (diseno/03 2.8, S-29). PA-MAP-02, -07, -08 y -11 a -21 son los del diseño con ese mismo ID (el Update de cada uno); los que agrega el arquitecto del sublote 5e
    /// llevan el prefijo <c>PA5E_MAC</c>. Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase (Update · PreOperation · orden 10 · SIN filtro · pre-imagen
    /// <c>PreImagen</c> de 9 columnas). El almacén da la pre-imagen SIN las columnas nulas (Dev las omite) y el step las completa: por eso un reenvío completo con nulos no es un cambio. Los valores
    /// exactos, el orden de las comprobaciones y la pereza de la identidad están en <c>ReglasDeMapeoAceptacion</c>.
    /// </summary>
    public class MapeoCeldaActualizarStepAceptacion
    {
        /// <summary>Un mapeo activo de valor simple en una versión padre del estado dado; devuelve el id del mapeo y deja el de la versión en <paramref name="version"/>.</summary>
        private static Guid UnMapeoEn(EscenarioDeSteps e, EstadoDeVersionDePlantilla estado, out Guid version)
        {
            version = UnaVersion(e, estado);
            var id = SembrarMapeo(e, version);
            e.Almacen.LimpiarRegistros();
            return id;
        }

        private static Guid UnBloqueEn(EscenarioDeSteps e, EstadoDeVersionDePlantilla estado, out Guid version)
        {
            version = UnaVersion(e, estado);
            var id = e.Sembrar(UnBloqueDe(version));
            e.Almacen.LimpiarRegistros();
            return id;
        }

        private static void AssertMapeoIntacto(EscenarioDeSteps e)
        {
            Assert.Empty(Escrituras(e, OperacionDeDatos.Update));
        }

        private static Entity DePares(Guid id, IDictionary<string, object> pares)
        {
            return Cambio(id, pares.SelectMany(p => new[] { p.Key, p.Value }).ToArray());
        }

        // ---------------------------------------------------------------- PA-MAP del diseño

        [Fact]
        public void PA_MAP_07_CambiarLaCeldaSinMandarElNombre_RecalculaElNombre_YNormalizaLaCelda()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            e.ComoOficial.Update(Cambio(id, "sanic_celda", "c9"));

            Assert.Equal("C9", Texto(e, id, "sanic_celda"));
            Assert.Equal(HojaReal + "!C9", Texto(e, id, "sanic_nombre"));
            Assert.Equal(HojaReal, Texto(e, id, "sanic_hoja"));
            var escritura = Assert.Single(Escrituras(e, OperacionDeDatos.Update));
            Assert.Equal(new[] { "sanic_nombre" }, Agregadas(escritura).ToArray());
        }

        [Fact]
        public void PA_MAP_08_CambiarLaVersionPadre_CARP_MAP_006_YElMapeoSigueEnSuVersion()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            var otra = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_versionplantillaid", RefVersion(otra))), CodigosDeError.MAP_006);

            Assert.Equal(version, Assert.IsType<EntityReference>(Valor(e, id, "sanic_versionplantillaid")).Id);
            AssertMapeoIntacto(e);
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "celda")]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "hoja")]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "desactivar")]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "nombre")]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "padre")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "celda")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "hoja")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "desactivar")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "nombre")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "padre")]
        public void PA_MAP_02_15_21_EnUnaVersionQueNoEsBorrador_TodoCambioDaCARP_MAP_001_AunElDeEstadoYElDeNombre(EstadoDeVersionDePlantilla estado, string caso)
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, estado, out version);
            var otra = UnaVersion(e);
            e.Almacen.LimpiarRegistros();
            var cambio = caso == "celda" ? Cambio(id, "sanic_celda", "C9")
                : caso == "hoja" ? Cambio(id, "sanic_hoja", "OTRA")
                : caso == "desactivar" ? Cambio(id, "statecode", Inactivo())
                : caso == "padre" ? Cambio(id, "sanic_versionplantillaid", RefVersion(otra))
                : Cambio(id, "sanic_nombre", "X!Z9");

            Rechaza(() => e.ComoOficial.Update(cambio), CodigosDeError.MAP_001);

            AssertMapeoIntacto(e);
            Assert.Equal(Estados.Activo, Assert.IsType<OptionSetValue>(Valor(e, id, "statecode")).Value);
        }

        [Theory]
        [InlineData("statecode")]
        [InlineData("statuscode")]
        [InlineData("ambos")]
        public void PA_MAP_11_UnJefeDesactivaUnMapeoActivo_CARP_MAP_007_YElMapeoSigueActivo(string caso)
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            var cambio = caso == "statecode" ? Cambio(id, "statecode", Inactivo())
                : caso == "statuscode" ? Cambio(id, "statuscode", MotivoDe(Estados.Inactivo))
                : Cambio(id, "statecode", Inactivo(), "statuscode", MotivoDe(Estados.Inactivo));

            Rechaza(() => e.ComoOficial.Update(cambio), CodigosDeError.MAP_007);

            Assert.Equal(Estados.Activo, Assert.IsType<OptionSetValue>(Valor(e, id, "statecode")).Value);
            AssertMapeoIntacto(e);
        }

        [Fact]
        public void PA_MAP_12_UnJefeReactivaUnMapeoInactivo_CARP_MAP_007_YSigueInactivo()
        {
            var e = SoloActualizar();
            var version = UnaVersion(e);
            var id = e.Sembrar(UnMapeoDe(version).Con("statecode", Inactivo()).Con("statuscode", MotivoDe(Estados.Inactivo)));
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statecode", Activo(), "statuscode", MotivoDe(Estados.Activo))), CodigosDeError.MAP_007);

            Assert.Equal(Estados.Inactivo, Assert.IsType<OptionSetValue>(Valor(e, id, "statecode")).Value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA_MAP_13_ElSistemaCambiaElEstado_Pasa_SinTocarElTarget(bool desdeInactivo)
        {
            var e = SoloActualizar();
            var version = UnaVersion(e);
            var origen = desdeInactivo ? Estados.Inactivo : Estados.Activo;
            var destino = desdeInactivo ? Estados.Activo : Estados.Inactivo;
            var id = e.Sembrar(UnMapeoDe(version).Con("statecode", new OptionSetValue(origen)).Con("statuscode", MotivoDe(origen)));
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "statecode", new OptionSetValue(destino), "statuscode", MotivoDe(destino)));

            Assert.Equal(destino, Assert.IsType<OptionSetValue>(Valor(e, id, "statecode")).Value);
            Assert.Empty(Agregadas(Assert.Single(Escrituras(e, OperacionDeDatos.Update))));
        }

        [Theory]
        [InlineData("statecode")]
        [InlineData("statuscode")]
        [InlineData("ambos")]
        public void PA_MAP_14_ReenviarElMismoEstadoYCambiarLaCelda_Pasa_RecalculaElNombre_YNoConsultaLaIdentidad(string reenvia)
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            SinConsultarLaIdentidad(e);
            var cambio = Cambio(id, "sanic_celda", "c9");
            if (reenvia != "statuscode") cambio["statecode"] = Activo();
            if (reenvia != "statecode") cambio["statuscode"] = MotivoDe(Estados.Activo);

            e.ComoOficial.Update(cambio);

            Assert.Equal("C9", Texto(e, id, "sanic_celda"));
            Assert.Equal(HojaReal + "!C9", Texto(e, id, "sanic_nombre"));
        }

        [Theory]
        [InlineData("X!Z9")]
        [InlineData("")]
        [InlineData((string)null)]
        public void PA_MAP_16_CambiarSoloElNombre_AOtroANuloOVacio_CARP_MAP_008_YElNombreNoCambia(string nombre)
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_nombre", nombre)), CodigosDeError.MAP_008);

            Assert.Equal(HojaReal + "!B7", Texto(e, id, "sanic_nombre"));
            AssertMapeoIntacto(e);
        }

        [Theory]
        [InlineData("otro")]
        [InlineData("calculado")]
        public void PA_MAP_17_CambiarLaCeldaYMandarUnNombre_CARP_MAP_008_AunSiEsElQueElPluginIbaACalcular_YNadaSeAplica(string nombre)
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            var mandado = nombre == "otro" ? "X!Z9" : HojaReal + "!C9";

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_celda", "c9", "sanic_nombre", mandado)), CodigosDeError.MAP_008);

            Assert.Equal("B7", Texto(e, id, "sanic_celda"));
            Assert.Equal(HojaReal + "!B7", Texto(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA_MAP_18_CambiarLaCeldaYReenviarElNombreDeLaPreImagen_Pasa_ElNombreSeRecalcula_SinConsultarLaIdentidad()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_celda", "c9", "sanic_nombre", HojaReal + "!B7"));

            Assert.Equal("C9", Texto(e, id, "sanic_celda"));
            Assert.Equal(HojaReal + "!C9", Texto(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA_MAP_19_ReenviarElMismoNombreYCambiarLasFilasMaximas_Pasa_SinRecalcular_YSinConsultarLaIdentidad()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnBloqueEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_nombre", HojaReal + "!B20", "sanic_filasmaximas", 48));

            Assert.Equal(48, Assert.IsType<int>(Valor(e, id, "sanic_filasmaximas")));
            Assert.Equal(HojaReal + "!B20", Texto(e, id, "sanic_nombre"));
            Assert.Equal("B20", Texto(e, id, "sanic_celda"));
            Assert.Empty(Agregadas(Assert.Single(Escrituras(e, OperacionDeDatos.Update))));
        }

        [Fact]
        public void PA_MAP_20_ElSistemaCambiaElNombre_Pasa_SinTocarElTarget()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            e.ComoSistema.Update(Cambio(id, "sanic_nombre", "NOMBRE DEL SISTEMA"));

            Assert.Equal("NOMBRE DEL SISTEMA", Texto(e, id, "sanic_nombre"));
            Assert.Empty(Agregadas(Assert.Single(Escrituras(e, OperacionDeDatos.Update))));
        }

        // ---------------------------------------------------------------- lo que agrega el sublote 5e

        [Fact]
        public void PA5E_MAC_01_ElOrdenDeLasComprobaciones_EsEstado_Nombre_Version_Celda_TipoYCampo_FilasMaximas_Hoja()
        {
            // Un Update con TODO mal sobre un bloque; cada ronda quita lo que acaba de ganar. Un rechazo restaura el almacén, así que el mismo mapeo sirve para todas las rondas.
            var e = SoloActualizar();
            Guid version;
            var id = UnBloqueEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            var otra = UnaVersion(e);
            var pares = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "statecode", Inactivo() }, { "sanic_nombre", "X!Z9" }, { "sanic_versionplantillaid", RefVersion(otra) }, { "sanic_celda", "B0" },
                { "sanic_tipo", Tipo(TipoDeMapeo.TotalCalculado) }, { "sanic_campo", Campo(CampoMapeable.CuotaNumero) }, { "sanic_filasmaximas", 0 }, { "sanic_hoja", "   " }
            };

            Rechaza(() => e.ComoOficial.Update(DePares(id, pares)), CodigosDeError.MAP_007);
            pares.Remove("statecode");
            Rechaza(() => e.ComoOficial.Update(DePares(id, pares)), CodigosDeError.MAP_008);
            pares.Remove("sanic_nombre");
            Rechaza(() => e.ComoOficial.Update(DePares(id, pares)), CodigosDeError.MAP_006);
            pares.Remove("sanic_versionplantillaid");
            Rechaza(() => e.ComoOficial.Update(DePares(id, pares)), CodigosDeError.MAP_002, "B0");
            pares["sanic_celda"] = "B20";
            Rechaza(() => e.ComoOficial.Update(DePares(id, pares)), CodigosDeError.MAP_003, "Cuota: número");
            pares["sanic_tipo"] = Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas);
            Rechaza(() => e.ComoOficial.Update(DePares(id, pares)), CodigosDeError.MAP_004);
            pares["sanic_filasmaximas"] = 60;
            Rechaza(() => e.ComoOficial.Update(DePares(id, pares)), CodigosDeError.MAP_005);
            pares["sanic_hoja"] = "HOJA";
            e.ComoOficial.Update(DePares(id, pares));

            Assert.Equal("HOJA", Texto(e, id, "sanic_hoja"));
            Assert.Equal("HOJA!B20", Texto(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA5E_MAC_02_LosValoresEfectivos_ElTargetSiLaClaveViene_AunqueSeaNulo_Y_SiNo_LaPreImagen()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnBloqueEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_tipo", Tipo(TipoDeMapeo.ValorSimple))), CodigosDeError.MAP_003, "Cuota: número");
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_filasmaximas", null)), CodigosDeError.MAP_004);
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_celda", string.Empty)), CodigosDeError.MAP_002, string.Empty);
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_hoja", null)), CodigosDeError.MAP_005);
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_hoja", "   ")), CodigosDeError.MAP_005);
            e.ComoOficial.Update(Cambio(id, "sanic_hoja", "HOJA"));

            Assert.Equal("HOJA!B20", Texto(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA5E_MAC_03_SoloElCampoCambia_ElTipoEfectivoEsElDeLaPreImagen()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_campo", Campo(CampoMapeable.CuotaMonto))), CodigosDeError.MAP_003, "Cuota: monto");
        }

        [Fact]
        public void PA5E_MAC_04_CambiarLaHoja_RecalculaElNombre_ConLaCeldaDeLaPreImagenEnMayuscula()
        {
            var e = SoloActualizar();
            var version = UnaVersion(e);
            var id = e.Sembrar(UnMapeoDe(version).Con("sanic_celda", "b7"));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_hoja", "OTRA"));

            Assert.Equal("B7", Texto(e, id, "sanic_celda"));
            Assert.Equal("OTRA!B7", Texto(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA5E_MAC_05_DeB7Ab7_EsUnCambio_PorqueElTextoSeCompareOrdinal_ElStepNormalizaYRecalcula()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            e.ComoOficial.Update(Cambio(id, "sanic_celda", "b7"));

            Assert.Equal("B7", Texto(e, id, "sanic_celda"));
            Assert.Equal(HojaReal + "!B7", Texto(e, id, "sanic_nombre"));
            Assert.Equal(new[] { "sanic_nombre" }, Agregadas(Assert.Single(Escrituras(e, OperacionDeDatos.Update))).ToArray());
        }

        [Fact]
        public void PA5E_MAC_06_ElStepCompletaLaPreImagenConNulos_UnaHojaNulaSobreUnaFilaSinHoja_NoEsCambio()
        {
            // Dev omite las columnas nulas de la pre-imagen: si el step no las completara, "ausente" contaría como cambio y la hoja nula daría CARP-MAP-005.
            var e = SoloActualizar();
            var version = UnaVersion(e);
            var sinHoja = e.Sembrar(UnMapeoDe(version).Sin("sanic_hoja"));
            var conHoja = SembrarMapeo(e, version);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(sinHoja, "sanic_hoja", null));
            Rechaza(() => e.ComoOficial.Update(Cambio(conHoja, "sanic_hoja", null)), CodigosDeError.MAP_005);
        }

        [Fact]
        public void PA5E_MAC_07_ElReenvioCompletoConSusNulos_NoEsCambio_AunEnUnaVersionVigente_NoLeeElPadre_NiConsultaLaIdentidad()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Vigente, out version);
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_versionplantillaid", RefVersion(version), "sanic_hoja", HojaReal, "sanic_celda", "B7",
                "sanic_tipo", Tipo(TipoDeMapeo.ValorSimple), "sanic_campo", Campo(CampoMapeable.FechaDeFormalizacion), "sanic_filasmaximas", null,
                "statecode", Activo(), "statuscode", MotivoDe(Estados.Activo), "sanic_nombre", HojaReal + "!B7"));

            Assert.Empty(ConsultasDeLaVersion(e));
            Assert.Empty(Agregadas(Assert.Single(Escrituras(e, OperacionDeDatos.Update))));
        }

        [Fact]
        public void PA5E_MAC_08_UnaColumnaFueraDeLasNueve_NoEsCambio_AunEnUnaVersionVigente()
        {
            // ownerid lo rige S-13 (propietario), no S-29: sin S-13 registrado el cambio pasa y no se lee el padre.
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Vigente, out version);
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "ownerid", new EntityReference("team", e.TeamAjeno)));

            Assert.Empty(ConsultasDeLaVersion(e));
        }

        [Fact]
        public void PA5E_MAC_09_ElPadreSeLeeUnaSolaVez_YLaIdentidadUnaSolaVez_AunConVariasViolaciones()
        {
            var comoMucho = SoloActualizar();
            Guid versionA;
            var idA = UnMapeoEn(comoMucho, EstadoDeVersionDePlantilla.Borrador, out versionA);
            ApoyoDeConversacion.ComoMuchoUnaConsultaDeIdentidad(comoMucho);

            var alMenos = SoloActualizar();
            Guid versionB;
            var idB = UnMapeoEn(alMenos, EstadoDeVersionDePlantilla.Borrador, out versionB);
            ApoyoDeConversacion.AlMenosUnaConsultaDeIdentidad(alMenos);

            Rechaza(() => comoMucho.ComoOficial.Update(Cambio(idA, "statecode", Inactivo(), "sanic_nombre", "X!Z9", "sanic_celda", "B0")), CodigosDeError.MAP_007);
            var falla = Assert.Throws<InvalidPluginExecutionException>(() => alMenos.ComoOficial.Update(Cambio(idB, "statecode", Inactivo())));

            Assert.StartsWith("CARP-MAP-900", falla.Message, StringComparison.Ordinal);
            var lectura = Assert.Single(ConsultasDeLaVersion(comoMucho));
            Assert.Equal(versionA, lectura.Id);
        }

        [Fact]
        public void PA5E_MAC_10_LaPreImagenDelRegistro_TraeLasNueveColumnas_YSinRecalculoElStepNoEscribeNada()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnBloqueEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            e.ComoOficial.Update(Cambio(id, "sanic_filasmaximas", 48));

            var ejecucion = Assert.Single(EjecucionesDe<MapeoCeldaActualizarStep>(e));
            Assert.Null(ejecucion.Excepcion);
            Assert.Equal(Ordenadas(ReglasDeMapeo.ColumnasDeLaPreImagen), Ordenadas(ejecucion.Step.ColumnasDeImagenPrevia));
            Assert.Equal(Ordenadas(new[] { "sanic_versionplantillaid", "sanic_hoja", "sanic_celda", "sanic_tipo", "sanic_campo", "sanic_filasmaximas", "statecode", "statuscode", "sanic_nombre",
                "sanic_carp_tbl_mapeoceldaid" }), Ordenadas(ejecucion.Contexto.PreEntityImages["PreImagen"].Attributes.Keys));
            Assert.Empty(Agregadas(Assert.Single(Escrituras(e, OperacionDeDatos.Update))));
            Assert.Equal(HojaReal + "!B20", Texto(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA5E_MAC_11_LaVersionPadreSeLeeConSystem_AunqueElOficialNoPuedaLeerVersiones()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            e.Almacen.DenegarPrivilegio(e.Oficial, TablaVersion, OperacionDeDatos.Retrieve);
            e.Almacen.NegarLectura(e.Oficial, TablaVersion, version);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_celda", "c9"));

            Assert.Equal("C9", Texto(e, id, "sanic_celda"));
            var lectura = Assert.Single(ConsultasDeLaVersion(e));
            Assert.Equal(e.Almacen.IdDeSystem, lectura.Identidad);
            Assert.Equal(new[] { "sanic_estado" }, Ordenadas(lectura.Columnas));
        }

        [Fact]
        public void PA5E_MAC_12_ElSistemaCambiaLoQueQuiera_EnUnaVersionVigente_Pasa_SinTocarElTarget()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Vigente, out version);
            var otra = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "sanic_versionplantillaid", RefVersion(otra), "sanic_celda", "B0", "statecode", Inactivo(), "statuscode", MotivoDe(Estados.Inactivo)));

            Assert.Equal("B0", Texto(e, id, "sanic_celda"));
            Assert.Empty(Agregadas(Assert.Single(Escrituras(e, OperacionDeDatos.Update))));
        }

        [Fact]
        public void PA5E_MAC_13_LaIdentidadEsElIniciador_NoUserId()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Vigente, out version);
            var sistema = e.Almacen.IdDeSystem;

            // UserId = SYSTEM pero el iniciador es una persona (V-4): se evalúa como persona.
            var previaDePersona = e.Almacen.ImagenPrevia(TablaMapeo, id, Sanic.Carp.Plugins.Dominio.Plantilla.ReglasDeMapeo.ColumnasDeLaPreImagen, e.Oficial);
            Rechaza(() => EjecutarDirecto(e, new MapeoCeldaActualizarStep(), "Update", id, e.Oficial, Cambio(id, "sanic_celda", "C9"), previaDePersona, c => c.UserId = sistema),
                CodigosDeError.MAP_001);
            // UserId = persona pero el iniciador es SYSTEM: se deja pasar.
            var previaDeSistema = e.Almacen.ImagenPrevia(TablaMapeo, id, Sanic.Carp.Plugins.Dominio.Plantilla.ReglasDeMapeo.ColumnasDeLaPreImagen, sistema);
            EjecutarDirecto(e, new MapeoCeldaActualizarStep(), "Update", id, sistema, Cambio(id, "sanic_celda", "C9"), previaDeSistema, c => c.UserId = e.Oficial);
        }

        [Fact]
        public void PA5E_MAC_14_UnaPreImagenSinElPadre_MAP_001_SinLeerNada_EjecucionDirecta()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            var sinPadre = new Entity(TablaMapeo, id);
            sinPadre["sanic_celda"] = "B7";

            Rechaza(() => EjecutarDirecto(e, new MapeoCeldaActualizarStep(), "Update", id, e.Oficial, Cambio(id, "sanic_celda", "C9"), sinPadre), CodigosDeError.MAP_001);

            Assert.Empty(ConsultasDeLaVersion(e));
        }

        [Fact]
        public void PA5E_MAC_15_ElRechazoNoDejaElTargetMutado_EjecucionDirecta()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            var previa = e.Almacen.ImagenPrevia(TablaMapeo, id, ReglasDeMapeo.ColumnasDeLaPreImagen, e.Oficial);
            var target = Cambio(id, "sanic_celda", "c9", "sanic_nombre", "X!Z9");

            Rechaza(() => EjecutarDirecto(e, new MapeoCeldaActualizarStep(), "Update", id, e.Oficial, target, previa), CodigosDeError.MAP_008);

            Assert.Equal("c9", target["sanic_celda"]);
            Assert.Equal("X!Z9", target["sanic_nombre"]);
        }

        [Fact]
        public void PA5E_MAC_16_ElStepEscribeEnElTargetExactamenteLoQueLaReglaDevuelve_EjecucionDirecta()
        {
            var e = SoloActualizar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            var previa = e.Almacen.ImagenPrevia(TablaMapeo, id, ReglasDeMapeo.ColumnasDeLaPreImagen, e.Oficial);
            var target = Cambio(id, "sanic_celda", "c9");

            EjecutarDirecto(e, new MapeoCeldaActualizarStep(), "Update", id, e.Oficial, target, previa);

            Assert.Equal(Ordenadas(new[] { "sanic_celda", "sanic_nombre" }), Ordenadas(target.Attributes.Keys));
            Assert.Equal("C9", target["sanic_celda"]);
            Assert.Equal(HojaReal + "!C9", target["sanic_nombre"]);
        }
    }
}
