using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeMapeo;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>MapeoCeldaEliminarStep</c> (diseno/03 2.8, S-30). PA-MAP-02 (Delete), PA-MAP-09 y PA-MAP-10 (Delete) son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5e llevan el
    /// prefijo <c>PA5E_MEL</c>. Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase (Delete · PreOperation · orden 10 · pre-imagen <c>PreImagen</c> con
    /// <c>sanic_versionplantillaid</c>). En un <c>Delete</c> el <c>Target</c> es una <c>EntityReference</c>: el step solo mira la pre-imagen. Si la plataforma ejecuta S-30 en el borrado en cascada de la
    /// versión no está medido (8.1, H-21): el doble no lo prueba.
    /// </summary>
    public class MapeoCeldaEliminarStepAceptacion
    {
        private static Guid UnMapeoEn(EscenarioDeSteps e, EstadoDeVersionDePlantilla estado, out Guid version)
        {
            version = UnaVersion(e, estado);
            var id = SembrarMapeo(e, version);
            e.Almacen.LimpiarRegistros();
            return id;
        }

        // ---------------------------------------------------------------- PA-MAP del diseño

        [Fact]
        public void PA_MAP_09_BorrarEnUnaVersionBorrador_Pasa_YLaFilaDesaparece()
        {
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            e.ComoOficial.Delete(TablaMapeo, id);

            Assert.False(e.Almacen.Existe(TablaMapeo, id));
        }

        [Theory]
        [MemberData(nameof(ApoyoDeMapeo.VersionesQueNoSonBorrador), MemberType = typeof(ApoyoDeMapeo))]
        public void PA_MAP_09_BorrarEnUnaVersionVigenteOHistorica_CARP_MAP_001_YLaFilaSigue(EstadoDeVersionDePlantilla estado)
        {
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, estado, out version);

            Rechaza(() => e.ComoOficial.Delete(TablaMapeo, id), CodigosDeError.MAP_001);

            Assert.True(e.Almacen.Existe(TablaMapeo, id));
            Assert.Empty(Escrituras(e, OperacionDeDatos.Delete));
        }

        [Fact]
        public void PA_MAP_10_ElSistemaBorraEnUnaVersionVigente_Pasa()
        {
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Vigente, out version);

            e.ComoSistema.Delete(TablaMapeo, id);

            Assert.False(e.Almacen.Existe(TablaMapeo, id));
        }

        // ---------------------------------------------------------------- lo que agrega el sublote 5e

        [Fact]
        public void PA5E_MEL_01_BorrarEnUnaVersionBorrador_NoConsultaLaIdentidad_YLeeElPadreUnaSolaVez()
        {
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Delete(TablaMapeo, id);

            var lectura = Assert.Single(ConsultasDeLaVersion(e));
            Assert.Equal(OperacionDeDatos.Retrieve, lectura.Operacion);
            Assert.Equal(version, lectura.Id);
            Assert.False(lectura.TodasLasColumnas);
            Assert.Equal(new[] { "sanic_estado" }, Ordenadas(lectura.Columnas));
        }

        [Fact]
        public void PA5E_MEL_02_LaPreImagenDelRegistro_TraeSoloLaVersionPadre()
        {
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            e.ComoOficial.Delete(TablaMapeo, id);

            var ejecucion = Assert.Single(EjecucionesDe<MapeoCeldaEliminarStep>(e));
            Assert.Null(ejecucion.Excepcion);
            Assert.Equal(new[] { "sanic_versionplantillaid" }, Ordenadas(ejecucion.Step.ColumnasDeImagenPrevia));
            Assert.Equal(Ordenadas(new[] { "sanic_versionplantillaid", "sanic_carp_tbl_mapeoceldaid" }), Ordenadas(ejecucion.Contexto.PreEntityImages["PreImagen"].Attributes.Keys));
        }

        [Fact]
        public void PA5E_MEL_03_LaVersionPadreSeLeeConSystem_AunqueElOficialNoPuedaLeerVersiones()
        {
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            e.Almacen.DenegarPrivilegio(e.Oficial, TablaVersion, OperacionDeDatos.Retrieve);
            e.Almacen.NegarLectura(e.Oficial, TablaVersion, version);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Delete(TablaMapeo, id);

            Assert.False(e.Almacen.Existe(TablaMapeo, id));
            Assert.All(ConsultasDeLaVersion(e), c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
        }

        [Fact]
        public void PA5E_MEL_04_UnRechazoConsultaLaIdentidadExactamenteUnaVez()
        {
            var comoMucho = SoloEliminar();
            Guid versionA;
            var idA = UnMapeoEn(comoMucho, EstadoDeVersionDePlantilla.Vigente, out versionA);
            ApoyoDeConversacion.ComoMuchoUnaConsultaDeIdentidad(comoMucho);

            var alMenos = SoloEliminar();
            Guid versionB;
            var idB = UnMapeoEn(alMenos, EstadoDeVersionDePlantilla.Vigente, out versionB);
            ApoyoDeConversacion.AlMenosUnaConsultaDeIdentidad(alMenos);

            Rechaza(() => comoMucho.ComoOficial.Delete(TablaMapeo, idA), CodigosDeError.MAP_001);
            var falla = Assert.Throws<InvalidPluginExecutionException>(() => alMenos.ComoOficial.Delete(TablaMapeo, idB));
            Assert.StartsWith("CARP-MAP-900", falla.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA5E_MEL_05_UnaPreImagenSinLaVersionPadre_CARP_MAP_001_SinLeerNada_EjecucionDirecta()
        {
            // El step NO completa la pre-imagen: ausente y nulo dan el mismo resultado (padre desconocido, falla cerrado).
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);
            var ausente = new Entity(TablaMapeo, id);
            var nula = new Entity(TablaMapeo, id);
            nula["sanic_versionplantillaid"] = null;

            Rechaza(() => EjecutarDirecto(e, new MapeoCeldaEliminarStep(), "Delete", id, e.Oficial, null, ausente), CodigosDeError.MAP_001);
            Rechaza(() => EjecutarDirecto(e, new MapeoCeldaEliminarStep(), "Delete", id, e.Oficial, null, nula), CodigosDeError.MAP_001);

            Assert.Empty(ConsultasDeLaVersion(e));
        }

        [Fact]
        public void PA5E_MEL_06_ElSistemaBorraSinVersionPadreEnLaPreImagen_Pasa_EjecucionDirecta()
        {
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            EjecutarDirecto(e, new MapeoCeldaEliminarStep(), "Delete", id, e.Almacen.IdDeSystem, null, new Entity(TablaMapeo, id));

            Assert.Empty(ConsultasDeLaVersion(e));
        }

        [Fact]
        public void PA5E_MEL_07_LaIdentidadEsElIniciador_NoUserId()
        {
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Vigente, out version);
            var sistema = e.Almacen.IdDeSystem;
            var previa = e.Almacen.ImagenPrevia(TablaMapeo, id, new[] { "sanic_versionplantillaid" }, sistema);

            // UserId = SYSTEM pero el iniciador es una persona (V-4): se evalúa como persona.
            Rechaza(() => EjecutarDirecto(e, new MapeoCeldaEliminarStep(), "Delete", id, e.Oficial, null, previa, c => c.UserId = sistema), CodigosDeError.MAP_001);
            // UserId = persona pero el iniciador es SYSTEM: se deja pasar.
            EjecutarDirecto(e, new MapeoCeldaEliminarStep(), "Delete", id, sistema, null, previa, c => c.UserId = e.Oficial);
        }

        [Fact]
        public void PA5E_MEL_08_ElStepNoEscribeNada_NiEnElTargetNiEnElAlmacen_MasAllaDelBorrado()
        {
            var e = SoloEliminar();
            Guid version;
            var id = UnMapeoEn(e, EstadoDeVersionDePlantilla.Borrador, out version);

            e.ComoOficial.Delete(TablaMapeo, id);

            Assert.Empty(Escrituras(e, OperacionDeDatos.Update));
            Assert.Empty(Escrituras(e, OperacionDeDatos.Create));
            Assert.Single(Escrituras(e, OperacionDeDatos.Delete));
        }
    }
}
