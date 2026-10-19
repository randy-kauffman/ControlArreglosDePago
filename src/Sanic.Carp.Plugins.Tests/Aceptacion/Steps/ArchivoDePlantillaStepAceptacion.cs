using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeVersion;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>ArchivoDePlantillaStep</c> (diseno/03 2.8, V-4 y 5, S-27). PA-VER-15 y PA-VER-18 a PA-VER-21 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5e llevan el prefijo
    /// <c>PA5E_ARC</c>. TODAS corren el step DIRECTO con un <c>Target</c> armado a mano (<see cref="ApoyoDeVersion.EjecutarArchivo"/>): el doble NO deja escribir <c>sanic_archivo</c> por <c>Create</c> ni
    /// <c>Update</c> (<see cref="ArgumentException"/>, "se siembra con <c>SembrarArchivo</c>"), así que un <c>Update</c> con <c>sanic_archivo</c> no se puede provocar por el pipeline. Lo que no se
    /// puede probar aquí (que el rechazo aborte la subida real, y la subida por un usuario no administrador) es H-12.
    /// </summary>
    public class ArchivoDePlantillaStepAceptacion
    {
        private static EscenarioDeSteps SoloS27()
        {
            return Escenario(propietario: false, archivo: true);
        }

        // ---------------------------------------------------------------- PA-VER del diseño

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, true)]
        [InlineData(EstadoDeVersionDePlantilla.Historica, true)]
        [InlineData(EstadoDeVersionDePlantilla.Borrador, false)]
        public void PA_VER_15_UnaPersonaSubeElArchivo_EnVigenteEHistoricaCARP_VER_003_EnBorradorPasa(EstadoDeVersionDePlantilla estado, bool rechaza)
        {
            var e = SoloS27();
            var id = UnaVersion(e, estado, "v07");

            Action subir = () => EjecutarArchivo(e, id, e.Oficial, Guid.NewGuid());

            if (rechaza) Rechaza(subir, CodigosDeError.VER_003);
            else subir();
        }

        [Fact]
        public void PA_VER_18_ElReemplazoConPatchDirectoEnUnBorrador_DosUpdateSeguidos_AmbosPasan()
        {
            // (1) UserId = SYSTEM, InitiatingUserId = el Jefe, sanic_archivo = null; (2) el Jefe con el id nuevo.
            var e = SoloS27();
            var id = UnaVersion(e);

            EjecutarArchivo(e, id, e.Oficial, null, c => c.UserId = e.Almacen.IdDeSystem);
            EjecutarArchivo(e, id, e.Oficial, Guid.NewGuid());
        }

        [Fact]
        public void PA_VER_19_ElReemplazoEnUnaVigente_LaPrimeraPasada_CARP_VER_003_PorqueSeEvaluaConElIniciador()
        {
            var e = SoloS27();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");

            Rechaza(() => EjecutarArchivo(e, id, e.Oficial, null, c => c.UserId = e.Almacen.IdDeSystem), CodigosDeError.VER_003);
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA_VER_20_ConInitiatingUserIdSystem_PasaEnUnaQueNoEsBorrador_SinLeerNada(EstadoDeVersionDePlantilla estado)
        {
            var e = SoloS27();
            var id = UnaVersion(e, estado, "v07");
            e.Almacen.LimpiarRegistros();

            EjecutarArchivo(e, id, e.Almacen.IdDeSystem, Guid.NewGuid());

            Assert.Empty(e.Almacen.Consultas);
            Assert.Empty(e.Almacen.Escrituras);
        }

        [Theory]
        [InlineData(1, null)]
        [InlineData(2, null)]
        [InlineData(1, "CommitFileBlocksUpload")]
        [InlineData(2, "CommitFileBlocksUpload")]
        [InlineData(5, "Update")]
        [InlineData(8, "CommitFileBlocksUpload")]
        public void PA_VER_21_ElStepNoMiraLaProfundidadNiElMensajePadre_MismoResultadoEnVigenteYEnBorrador(int profundidad, string mensajePadre)
        {
            var e = SoloS27();
            var vigente = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            var borrador = UnaVersion(e);
            Action<ContextoDePluginSimulado> configurar = c =>
            {
                c.Depth = profundidad;
                if (mensajePadre != null) c.ParentContext = new ContextoDePluginSimulado { MessageName = mensajePadre, PrimaryEntityName = TablaVersion };
            };

            Rechaza(() => EjecutarArchivo(e, vigente, e.Oficial, Guid.NewGuid(), configurar), CodigosDeError.VER_003);
            EjecutarArchivo(e, borrador, e.Oficial, Guid.NewGuid(), configurar);
        }

        // ---------------------------------------------------------------- lo que agrega el sublote 5e

        [Fact]
        public void PA5E_ARC_06_UnaPersona_PreguntaLaIdentidadUnaVez_YLeeUnaVezSoloElEstado_ConSystem_PorId()
        {
            var e = SoloS27();
            var id = UnaVersion(e);
            e.Almacen.LimpiarRegistros();
            ApoyoDeConversacion.ComoMuchoUnaConsultaDeIdentidad(e);

            EjecutarArchivo(e, id, e.Oficial, Guid.NewGuid());

            var lectura = Assert.Single(e.Almacen.Consultas);
            Assert.Equal(OperacionDeDatos.Retrieve, lectura.Operacion);
            Assert.Equal(TablaVersion, lectura.Tabla);
            Assert.Equal(id, lectura.Id);
            Assert.Equal(e.Almacen.IdDeSystem, lectura.Identidad);
            Assert.False(lectura.TodasLasColumnas);
            Assert.Equal(new[] { "sanic_estado" }, lectura.Columnas.ToArray());
        }

        [Fact]
        public void PA5E_ARC_07_LaLecturaDelEstadoEsDeSystem_AunqueElOficialNoTengaPrivilegioDeLectura()
        {
            var e = SoloS27();
            var id = UnaVersion(e);
            e.Almacen.DenegarPrivilegio(e.Oficial, TablaVersion, OperacionDeDatos.Retrieve);
            e.Almacen.LimpiarRegistros();

            EjecutarArchivo(e, id, e.Oficial, Guid.NewGuid());

            Assert.NotEmpty(e.Almacen.Consultas);
            Assert.All(e.Almacen.Consultas, c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
        }

        [Fact]
        public void PA5E_ARC_07b_LaLecturaDeSystemDaElMismoRechazoQueSiElOficialPudieraLeer_EnUnaVigente()
        {
            // Una lectura denegada daría un rechazo distinto según el rol: la regla decide por el estado, no por lo que la persona ve.
            var e = SoloS27();
            var id = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente, "v07");
            e.Almacen.DenegarPrivilegio(e.Oficial, TablaVersion, OperacionDeDatos.Retrieve);

            Rechaza(() => EjecutarArchivo(e, id, e.Oficial, Guid.NewGuid()), CodigosDeError.VER_003);
        }

        [Fact]
        public void PA5E_ARC_08_UnaVersionInexistente_EsNoBorrador_CARP_VER_003()
        {
            var e = SoloS27();

            Rechaza(() => EjecutarArchivo(e, Guid.NewGuid(), e.Oficial, Guid.NewGuid()), CodigosDeError.VER_003);
        }

        [Fact]
        public void PA5E_ARC_09_ElStepNoEscribeNada_NiMiraElValorDelArchivo_NiModificaElTarget()
        {
            var e = SoloS27();
            var id = UnaVersion(e);
            var archivoNuevo = Guid.NewGuid();
            var target = new Entity(TablaVersion, id);
            target["sanic_archivo"] = archivoNuevo;
            var contexto = Contexto("Update", 20, TablaVersion, e.Oficial, target);
            contexto.PrimaryEntityId = id;
            e.Almacen.LimpiarRegistros();

            Ejecutar(new ArchivoDePlantillaStep(), e.Fabrica, contexto);
            EjecutarArchivo(e, id, e.Oficial, null);

            Assert.Empty(e.Almacen.Escrituras);
            Assert.Equal(new[] { "sanic_archivo" }, target.Attributes.Keys.ToArray());
            Assert.Equal(archivoNuevo, target["sanic_archivo"]);
        }

        [Fact]
        public void PA5E_ARC_11_UnUpdateDeOtraColumnaPorElPipeline_NoDisparaS27_PorqueSuFiltroEsSanicArchivo()
        {
            // VERDE A PROPÓSITO: el filtro de atributos es del registro (solo cuenta en Update, DC-27); aquí se mide que S-27 no corre si el Target no trae sanic_archivo.
            var e = SoloS27();
            var id = UnaVersion(e);

            e.ComoOficial.Update(Cambio(id, "sanic_capacidad", 48));

            Assert.Empty(EjecucionesDe<ArchivoDePlantillaStep>(e));
        }
    }
}
