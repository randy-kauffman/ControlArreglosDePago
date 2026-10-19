using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
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
    /// <c>ConversacionAsignacionStep</c> (diseno/03 2.4, S-18). PA-ASG-01 a PA-ASG-10 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5b llevan el prefijo <c>PA5B_ASG</c>. Corren con los
    /// DOS steps de Conversación registrados (S-17 en el orden 10 y S-18 en el 20), como en la plataforma, y con las clases de producción de identidad y configuración: "consultar la jefatura" es un
    /// <c>RetrieveMultiple</c> sobre <c>teammembership</c> con el servicio de SYSTEM, y se cuenta en <c>Consultas</c> del almacén.
    /// </summary>
    public class ConversacionAsignacionStepAceptacion
    {
        private const string Oficial = "sanic_oficialasignadoid";

        private static EscenarioDeSteps UnEscenario()
        {
            return Escenario(asignacion: true);
        }

        private static Entity Asignar(Guid conversacion, Guid? a)
        {
            var cambio = new Entity(Conv, conversacion);
            cambio[Oficial] = a.HasValue ? RefUsuario(a.Value) : null;
            return cambio;
        }

        private static Guid? OficialDe(EscenarioDeSteps e, Guid id)
        {
            var valor = Valor(e, id, Oficial) as EntityReference;
            return valor == null ? (Guid?)null : valor.Id;
        }

        // ---------------------------------------------------------------- PA-ASG

        [Fact]
        public void PA_ASG_01_SinOficial_UnOficialSeAsignaASiMismo_Pasa_SinConsultarLaJefatura()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Abierta());
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Asignar(id, e.Oficial));

            Assert.Equal(e.Oficial, OficialDe(e, id));
            Assert.Equal(0, ConsultasDeJefatura(e));
            Assert.Equal(0, ConsultasDeConfiguracion(e));
        }

        [Fact]
        public void PA_ASG_02_SinOficial_UnOficialAsignaAOtro_CARP_CNV_008()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Abierta());

            Rechaza(() => e.ComoOficial.Update(Asignar(id, e.OtroOficial)), CodigosDeError.CNV_008);

            Assert.Null(OficialDe(e, id));
        }

        [Fact]
        public void PA_ASG_03_SinOficial_UnJefeAsignaAOtro_Pasa()
        {
            var e = UnEscenario();
            var jefe = UnJefe(e);
            var id = e.Sembrar(Abierta());

            Como(e, jefe).Update(Asignar(id, e.OtroOficial));

            Assert.Equal(e.OtroOficial, OficialDe(e, id));
        }

        [Fact]
        public void PA_ASG_04_AsignadaAUnOficial_OtroOficialSeAsigna_CARP_CNV_008_YElJefeLaReasigna_Pasa()
        {
            var e = UnEscenario();
            var jefe = UnJefe(e);
            var id = e.Sembrar(Abierta().Con(Oficial, RefUsuario(e.Oficial)));

            Rechaza(() => Como(e, e.OtroOficial).Update(Asignar(id, e.OtroOficial)), CodigosDeError.CNV_008);
            Assert.Equal(e.Oficial, OficialDe(e, id));

            Como(e, jefe).Update(Asignar(id, e.OtroOficial));
            Assert.Equal(e.OtroOficial, OficialDe(e, id));
        }

        [Fact]
        public void PA_ASG_05_AsignadaAUnOficial_UnOficialLaDejaVacia_CARP_CNV_008_YUnJefe_Pasa()
        {
            var e = UnEscenario();
            var jefe = UnJefe(e);
            var id = e.Sembrar(Abierta().Con(Oficial, RefUsuario(e.OtroOficial)));

            Rechaza(() => e.ComoOficial.Update(Asignar(id, null)), CodigosDeError.CNV_008);
            Assert.Equal(e.OtroOficial, OficialDe(e, id));

            Como(e, jefe).Update(Asignar(id, null));
            Assert.Null(OficialDe(e, id));
        }

        [Fact]
        public void PA_ASG_06_AsignadaAUnOficial_ReenviarAlMismo_Pasa_SinConsultarLaJefaturaNiLaIdentidad()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Abierta().Con(Oficial, RefUsuario(e.OtroOficial)));
            SinConsultarLaIdentidad(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Asignar(id, e.OtroOficial));

            Assert.Equal(e.OtroOficial, OficialDe(e, id));
            Assert.Equal(0, ConsultasDeJefatura(e));
        }

        [Fact]
        public void PA_ASG_07_ElSistemaTrasladaElOficialAlDestinoVacio_Pasa_SinConsultarLaJefatura()
        {
            // La API de unir (1.4 paso 6c) escribe como SYSTEM el oficial del origen en el destino vacío.
            var e = UnEscenario();
            var id = e.Sembrar(Abierta());
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Asignar(id, e.OtroOficial));

            Assert.Equal(e.OtroOficial, OficialDe(e, id));
            Assert.Equal(0, ConsultasDeJefatura(e));
        }

        [Fact]
        public void PA_ASG_08_UnTeamConElNombreDeJefaturaEnOtraBu_NoCuenta_CARP_CNV_008()
        {
            var e = UnEscenario();
            var otraBu = Guid.NewGuid();
            var teamHomonimo = Guid.NewGuid();
            e.Almacen.Sembrar(new Entity("businessunit", otraBu) { ["name"] = "ZZF otra BU", ["isdisabled"] = false });
            e.Almacen.Sembrar(ConstructorDeDatos.UnTeam(EscenarioDeSteps.NombreJefatura, otraBu, false).ConId(teamHomonimo).Construir());
            var intruso = Guid.NewGuid();
            e.Almacen.Sembrar(ConstructorDeDatos.UnUsuario(intruso).Construir());
            e.Almacen.Sembrar(ConstructorDeDatos.UnaMembresiaDeTeam(intruso, teamHomonimo).Construir());
            var id = e.Sembrar(Abierta());

            Rechaza(() => Como(e, intruso).Update(Asignar(id, e.OtroOficial)), CodigosDeError.CNV_008);

            Assert.Null(OficialDe(e, id));
        }

        [Fact]
        public void PA_ASG_09_UnaReasignacion_ConsultaLaPertenenciaUnaSolaVez_ConSystem()
        {
            var e = UnEscenario();
            var jefe = UnJefe(e);
            var id = e.Sembrar(Abierta().Con(Oficial, RefUsuario(e.Oficial)));
            e.Almacen.LimpiarRegistros();

            Como(e, jefe).Update(Asignar(id, e.OtroOficial));

            var consulta = Assert.Single(e.Almacen.Consultas, c => c.Tabla == "teammembership");
            Assert.Equal(e.Almacen.IdDeSystem, consulta.Identidad);
        }

        [Fact]
        public void PA_ASG_10_ElTeamDeJefaturaNoExiste_CARP_CFG_003()
        {
            var e = UnEscenario();
            e.ComoSistema.Delete("team", e.TeamDeJefatura);
            var id = e.Sembrar(Abierta());

            Rechaza(() => e.ComoOficial.Update(Asignar(id, e.OtroOficial)), CodigosDeError.CFG_003);

            Assert.Null(OficialDe(e, id));
        }

        // ---------------------------------------------------------------- agregadas por el arquitecto del 5b

        [Fact]
        public void PA5B_ASG_11_DejarVaciaUnaConversacionSinOficial_NoEsUnCambio_Pasa_SinConsultarNada()
        {
            // Una columna ausente de la pre-imagen vale nulo (decisión del 5b): reenviar el vacío no exige jefatura ni pregunta quién es.
            var e = UnEscenario();
            var id = e.Sembrar(Abierta());
            SinConsultarLaIdentidad(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Asignar(id, null));

            Assert.Null(OficialDe(e, id));
            Assert.Equal(0, ConsultasDeJefatura(e));
        }

        [Fact]
        public void PA5B_ASG_12_Tomar_ConUpdateRequestYRowVersion_PasaPorLosDosSteps_PA_TOM_09()
        {
            // 1.3 paso 6: la API escribe SOLO sanic_oficialasignadoid con IfRowVersionMatches y el servicio del llamador. Ninguno de los dos steps lo frena.
            var e = UnEscenario();
            var id = e.Sembrar(Abierta());
            var version = e.ComoOficial.Retrieve(Conv, id, new ColumnSet("statecode")).RowVersion;
            Assert.False(string.IsNullOrEmpty(version));
            e.Almacen.LimpiarRegistros();
            var cambio = Asignar(id, e.Oficial);
            cambio.RowVersion = version;

            e.ComoOficial.Execute(new Microsoft.Xrm.Sdk.Messages.UpdateRequest { Target = cambio, ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches });

            Assert.Equal(e.Oficial, OficialDe(e, id));
            Assert.Equal(new[] { Oficial }, UnicaActualizacion(e).Columnas.ToArray());
            Assert.Equal(new[] { typeof(ConversacionActualizarStep), typeof(ConversacionAsignacionStep) },
                e.Pipeline.Ejecuciones.Select(x => x.Step.Plugin.GetType()).ToArray());
            Assert.All(e.Pipeline.Ejecuciones, x => Assert.Null(x.Excepcion));
            Assert.Equal(0, ConsultasDeJefatura(e));
        }

        [Fact]
        public void PA5B_ASG_13_ElStepSoloCorreCuandoElTargetTraeElOficial_ElFiltroDeLaSeccion5()
        {
            // VERDE A PROPÓSITO: fija el filtro `sanic_oficialasignadoid` del registro (S-18). Hoy pasa porque el pipeline no lo ejecuta; si el filtro se pierde, el step corre.
            var e = UnEscenario();
            var id = e.Sembrar(Abierta());
            var cliente = UnCliente(e);

            e.ComoOficial.Update(new Entity(Conv, id) { ["sanic_clienteid"] = new EntityReference("sanic_carp_tbl_cliente", cliente) });

            Assert.DoesNotContain(e.Pipeline.Ejecuciones, x => x.Step.Plugin is ConversacionAsignacionStep);
        }

        [Fact]
        public void PA5B_ASG_14_UnaPersonaQueNoEsJefe_ConUnaConsultaDeJefaturaQueFalla_NoPasaEnSilencio()
        {
            // 0.3: una falla de la plataforma al consultar la jefatura escapa como CARP-CNV-900 (reintentable); nunca se asume "no es jefe" ni "es jefe".
            var e = UnEscenario();
            var id = e.Sembrar(Abierta());
            e.Almacen.FallarEn(OperacionDeDatos.RetrieveMultiple, "teammembership", 1, CodigosDeFalla.TransaccionAbortada);

            var error = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(Asignar(id, e.OtroOficial)));

            Assert.StartsWith("CARP-CNV-900: ", error.Message, StringComparison.Ordinal);
            Assert.Null(OficialDe(e, id));
        }

        [Fact]
        public void PA5B_ASG_15_UnUpdateRegistradoSinLaPreImagen_FallaPorRegistroEquivocado()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Abierta());
            var contexto = Contexto("Update", 20, Conv, e.Oficial, Asignar(id, e.Oficial));

            var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new ConversacionAsignacionStep(), e.Fabrica, contexto));

            Assert.Contains("ConversacionAsignacionStep", error.Message);
            Assert.Contains("PreImagen", error.Message);
            Assert.DoesNotContain("CARP-CNV-900", error.Message);
        }

        [Fact]
        public void PA5B_ASG_16_ElStepNuncaEscribeEnElTarget()
        {
            // Ejecución directa: el step solo permite o rechaza; el Target sale como entró.
            var e = UnEscenario();
            var id = e.Sembrar(Abierta());
            var target = Asignar(id, e.Oficial);

            EjecutarDirecto(e, new ConversacionAsignacionStep(), id, e.Oficial, target, ReglasDeConversacionColumnas(), null);

            Assert.Equal(new[] { Oficial }, target.Attributes.Keys.ToArray());
        }

        private static string[] ReglasDeConversacionColumnas()
        {
            return Sanic.Carp.Plugins.Dominio.Conversacion.ReglasDeConversacion.ColumnasDeLaPreImagenDeAsignacion.ToArray();
        }
    }
}
