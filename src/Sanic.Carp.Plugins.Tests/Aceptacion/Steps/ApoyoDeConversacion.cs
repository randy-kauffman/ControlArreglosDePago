using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Apoyos de las pruebas de los steps de Conversación (sublote 5b): el escenario con los steps que correspondan, las filas de partida, el "cable de alarma" de la identidad y la
    /// ejecución directa de un step con la pre-imagen armada a mano. Vive en <c>Aceptacion/</c> para que el constructor no pueda tocarlo.
    /// <para>
    /// <b>El cable de alarma.</b> El almacén no cuenta los <c>WhoAmI</c>, pero sí puede hacer fallar el n-ésimo (<c>FallarEn</c>). "Ninguna consulta de identidad" es un cable en la primera llamada (si el step la
    /// hace, el guardado sale como <c>CARP-CNV-900</c>); "a lo sumo una" es un cable en la segunda; "exactamente una" son las dos pruebas juntas.
    /// </para>
    /// </summary>
    internal static class ApoyoDeConversacion
    {
        public const string Conv = "sanic_carp_tbl_conversacion";
        public const string Usuario = "systemuser";

        /// <summary>El escenario con el área sembrada y los steps de Conversación que se pidan registrados (el propietario, orden 5, solo si una prueba lo necesita).</summary>
        public static EscenarioDeSteps Escenario(bool propietario = false, bool actualizar = true, bool asignacion = false, bool crear = false)
        {
            var e = new EscenarioDeSteps();
            if (propietario)
            {
                e.Registrar(new PropietarioAlCrearStep());
                e.Registrar(new PropietarioAlActualizarStep());
            }
            if (crear) e.Registrar(new ConversacionCrearStep());
            if (actualizar) e.Registrar(new ConversacionActualizarStep());
            if (asignacion) e.Registrar(new ConversacionAsignacionStep());
            return e;
        }

        /// <summary>Una persona del área que pertenece al team de jefatura (la consulta de jefatura la encuentra).</summary>
        public static Guid UnJefe(EscenarioDeSteps e)
        {
            var jefe = Guid.NewGuid();
            e.Almacen.Sembrar(ConstructorDeDatos.UnUsuario(jefe).Construir());
            e.Almacen.Sembrar(ConstructorDeDatos.UnaMembresiaDeTeam(jefe, e.TeamDeJefatura).Construir());
            e.Almacen.LimpiarRegistros();
            return jefe;
        }

        public static IOrganizationService Como(EscenarioDeSteps e, Guid persona)
        {
            return e.Fabrica.CreateOrganizationService(persona);
        }

        public static EntityReference RefUsuario(Guid id)
        {
            return new EntityReference(Usuario, id);
        }

        public static OptionSetValue Opcion(int valor)
        {
            return new OptionSetValue(valor);
        }

        public static OptionSetValue State(EstadoDeConversacion estado)
        {
            return new OptionSetValue(Estados.De(estado).StateCode);
        }

        public static OptionSetValue Status(EstadoDeConversacion estado)
        {
            return new OptionSetValue(Estados.De(estado).StatusCode);
        }

        public static OptionSetValue Resultado(ResultadoDeConversacion resultado)
        {
            return new OptionSetValue((int)resultado);
        }

        public static OptionSetValue Motivo(MotivoDeSinRespuesta motivo)
        {
            return new OptionSetValue((int)motivo);
        }

        /// <summary>Una conversación Abierta de partida, con el número fijo de PA-CNV-44 y sin cliente ni oficial.</summary>
        public static ConstructorDeFila Abierta()
        {
            return ConstructorDeDatos.UnaConversacion().Con("sanic_nombre", "CONV-000123");
        }

        /// <summary>Una Finalizada de partida (resultado Sin respuesta con motivo y detalle; la fila del constructor trae fecha y finalizador). Se le suma el tiempo total para que Reabrir tenga algo que limpiar.</summary>
        public static ConstructorDeFila Finalizada()
        {
            return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada).Con("sanic_nombre", "CONV-000123").Con("sanic_tiempototal", 90);
        }

        public static ConstructorDeFila Unida()
        {
            return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_nombre", "CONV-000123");
        }

        /// <summary>La partida de cada estado.</summary>
        public static ConstructorDeFila De(EstadoDeConversacion estado)
        {
            switch (estado)
            {
                case EstadoDeConversacion.Abierta: return Abierta();
                case EstadoDeConversacion.Finalizada: return Finalizada();
                default: return Unida();
            }
        }

        /// <summary>Un cliente sembrado (el lookup tiene que apuntar a una fila que existe, la plataforma lo valida).</summary>
        public static Guid UnCliente(EscenarioDeSteps e)
        {
            return e.Sembrar(ConstructorDeDatos.UnCliente());
        }

        public static void SinConsultarLaIdentidad(EscenarioDeSteps e)
        {
            e.Almacen.FallarEn(OperacionDeDatos.WhoAmI, null, 1, CodigosDeFalla.TransaccionAbortada);
        }

        public static void ComoMuchoUnaConsultaDeIdentidad(EscenarioDeSteps e)
        {
            e.Almacen.FallarEn(OperacionDeDatos.WhoAmI, null, 2, CodigosDeFalla.TransaccionAbortada);
        }

        public static void AlMenosUnaConsultaDeIdentidad(EscenarioDeSteps e)
        {
            e.Almacen.FallarEn(OperacionDeDatos.WhoAmI, null, 1, CodigosDeFalla.TransaccionAbortada);
        }

        public static EscrituraRegistrada UnicaActualizacion(EscenarioDeSteps e)
        {
            return Assert.Single(ApoyoDeSteps.EscriturasDe(e.Almacen, Conv, OperacionDeDatos.Update));
        }

        /// <summary>Las columnas que un step agregó al <c>Target</c>: lo escrito menos lo que mandó el llamador (y menos el autonumérico, que agrega la plataforma).</summary>
        public static IReadOnlyList<string> AgregadasPorLosSteps(EscrituraRegistrada escritura)
        {
            return escritura.Columnas.Where(c => !escritura.ColumnasEnviadas.Contains(c, StringComparer.Ordinal) && !string.Equals(c, "sanic_nombre", StringComparison.Ordinal)).ToList();
        }

        public static string[] Ordenadas(IEnumerable<string> columnas)
        {
            return columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        }

        public static object Valor(EscenarioDeSteps e, Guid id, string columna)
        {
            return EscenarioDeSteps.Valor(e.Fila(Conv, id), columna);
        }

        public static int Entero(object opcion)
        {
            return Assert.IsType<OptionSetValue>(opcion).Value;
        }

        public static int ConsultasDeJefatura(EscenarioDeSteps e)
        {
            return e.Almacen.Consultas.Count(c => c.Tabla == "teammembership");
        }

        public static int ConsultasDeConfiguracion(EscenarioDeSteps e)
        {
            return e.Almacen.Consultas.Count(c => c.Tabla == "businessunit" || c.Tabla == "team" || c.Tabla == "queue"
                || c.Tabla == "environmentvariabledefinition" || c.Tabla == "environmentvariablevalue");
        }

        /// <summary>
        /// Ejecuta el step directamente (sin pipeline) con la pre-imagen <c>PreImagen</c> que el almacén daría a <paramref name="quien"/> y el <c>Target</c> dado, que el step modifica en el lugar.
        /// <paramref name="configurar"/> permite cambiar el contexto (por ejemplo <c>UserId</c> o la profundidad).
        /// </summary>
        public static void EjecutarDirecto(EscenarioDeSteps e, BaseDeStep paso, Guid id, Guid quien, Entity target, IEnumerable<string> columnasDeLaImagen, Action<ContextoDePluginSimulado> configurar = null)
        {
            var contexto = ApoyoDeSteps.Contexto("Update", 20, Conv, quien, target);
            contexto.PrimaryEntityId = id;
            if (columnasDeLaImagen != null) contexto.PreEntityImages["PreImagen"] = e.Almacen.ImagenPrevia(Conv, id, columnasDeLaImagen, quien);
            if (configurar != null) configurar(contexto);
            ApoyoDeSteps.Ejecutar(paso, e.Fabrica, contexto);
        }

        /// <summary>Un valor DISTINTO del de cualquier fila de partida, válido para el esquema, de cada columna vigilada de Conversación (el almacén valida tipo, largo y que el lookup exista).</summary>
        public static object ValorNuevoDe(EscenarioDeSteps e, string columna)
        {
            var ahora = DateTime.UtcNow;
            switch (columna)
            {
                case "sanic_huellamessageid": return "ZZF-otra-huella-" + Guid.NewGuid().ToString("N");
                case "sanic_fechaprimercorreo": return ahora.AddDays(-10);
                case "sanic_asunto": return "ZZF otro asunto";
                case "sanic_remitente": return "zzf-otro@example.com";
                case "sanic_sinrespuestadesde": return ahora.AddDays(-9);
                case "sanic_correonuevoenfinalizada": return true;
                case "sanic_resultado": return Resultado(ResultadoDeConversacion.ConArregloDePago);
                case "sanic_motivosinrespuesta": return Motivo(MotivoDeSinRespuesta.Spam);
                case "sanic_detallesinrespuesta": return "ZZF otro detalle";
                case "sanic_fechafinalizacion": return ahora.AddDays(-8);
                case "sanic_tiempoprimerarespuesta": return 77;
                case "sanic_tiempototal": return 78;
                case "sanic_cantidadrespuestas": return 79;
                case "sanic_unidaaid": return new EntityReference(Conv, e.Sembrar(ConstructorDeDatos.UnaConversacion()));
                case "sanic_finalizadoporid": return RefUsuario(e.OtroOficial);
                case "sanic_clienteid": return new EntityReference("sanic_carp_tbl_cliente", UnCliente(e));
                case "sanic_oficialasignadoid": return RefUsuario(e.OtroOficial);
                case "sanic_nombre": return "ZZV-NOMBRE";
                case "ownerid": return new EntityReference("team", e.TeamAjeno);
                default: throw new ArgumentOutOfRangeException(nameof(columna), columna, "Columna sin valor nuevo en el apoyo.");
            }
        }

        public static IEnumerable<object[]> ColumnasSoloSistema()
        {
            foreach (var columna in Columnas.De(Columnas.TablaConversacion).SoloSistema) yield return new object[] { columna };
        }

        public static IEnumerable<object[]> ColumnasDeGesto()
        {
            foreach (var columna in Columnas.De(Columnas.TablaConversacion).DeGesto) yield return new object[] { columna };
        }
    }
}
