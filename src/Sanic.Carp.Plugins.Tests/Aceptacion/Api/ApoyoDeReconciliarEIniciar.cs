using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// Apoyos de las pruebas de <c>reconciliarcorreos</c> e <c>iniciarconversacionnueva</c> (lote 6, vuelta 2, grupo G2). Vive en <c>Aceptacion/Api/</c> para que el constructor no pueda tocarlo.
    /// <para>
    /// <b>Condiciones del doble que este apoyo respeta.</b> (1) <c>inreplyto</c> se pierde en un <c>Create</c>: los correos se siembran con <c>Sembrar</c>. (2) El doble IGNORA <c>createdon</c> y
    /// <c>owningbusinessunit</c> al escribir: se siembran. (3) <b>El reloj.</b> La API en producción lee <c>DateTime.UtcNow</c> (<c>ContextoDeApi.Reloj</c>) y la prueba no puede fijarlo: las pruebas que pasan por el
    /// plugin (<c>Execute</c>) siembran los correos con <see cref="Antiguo"/> (octubre 1 de 2026, muy anterior a cualquier reloj real de esta suite), y las que dependen de la hora ("hace 3 minutos",
    /// bordes, tope de fecha) llaman a la lógica con un <see cref="RelojFijo"/>. (4) La transacción de la plataforma la pone <c>EjecutarComoPlataforma</c>: solo con ella una falla revierte lo escrito.
    /// </para>
    /// </summary>
    internal static class ApoyoDeReconciliarEIniciar
    {
        public const string NombreDeLaApiReconciliar = "sanic_carp_capi_reconciliarcorreos";
        public const string NombreDeLaApiIniciar = "sanic_carp_capi_iniciarconversacionnueva";

        public const string TablaCorreo = TablasDelArea.Correo;
        public const string TablaConv = TablasDelArea.Conversacion;
        public const string TablaCliente = TablasDelArea.Cliente;
        public const string TablaTeam = "team";
        public const string TablaBu = "businessunit";
        public const string TablaUsuario = "systemuser";

        /// <summary>El Message-ID de PA-ENT-01 y su huella (calculada a mano en el 5f, no con el código bajo prueba).</summary>
        public const string MessageIdDelDiseno = "<CAF+abc123@mail.example.com>";

        public const string HuellaDelDiseno = "093f09291642088a9522c5833b491aa367f7d28c10544c960eff9d3a8dd05d1d";

        /// <summary>Un instante del 2026-10-19 en UTC, para las pruebas que llaman a la lógica con un <see cref="RelojFijo"/>.</summary>
        public static DateTime Hora(int hora, int minuto, int segundo = 0)
        {
            return new DateTime(2026, 10, 19, hora, minuto, segundo, DateTimeKind.Utc);
        }

        /// <summary>Un instante de octubre 1 de 2026 más <paramref name="minutos"/>: anterior a cualquier reloj real de esta suite (el plugin lee <c>UtcNow</c>).</summary>
        public static DateTime Antiguo(int minutos)
        {
            return new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc).AddMinutes(minutos);
        }

        /// <summary>El escenario del área. <paramref name="cadena"/> registra los steps que ven las escrituras del sistema: S-02 (propietario al crear), S-36 y S-17 (conversación al crear y al actualizar).</summary>
        public static EscenarioDeSteps Escenario(bool cadena = false, int unidadesDeNegocio = 1)
        {
            var e = new EscenarioDeSteps(unidadesDeNegocio);
            if (cadena)
            {
                e.Registrar(new PropietarioAlCrearStep());
                e.Registrar(new ConversacionCrearStep());
                e.Registrar(new ConversacionActualizarStep());
            }
            return e;
        }

        // ---------------------------------------------------------------- Referencias

        public static EntityReference RefConv(Guid id)
        {
            return new EntityReference(TablaConv, id);
        }

        public static EntityReference RefUsuario(Guid id)
        {
            return new EntityReference(TablaUsuario, id);
        }

        public static EntityReference RefTeam(Guid id)
        {
            return new EntityReference(TablaTeam, id);
        }

        public static EntityReference RefBu(Guid id)
        {
            return new EntityReference(TablaBu, id);
        }

        public static EntityReference RefCliente(Guid id)
        {
            return new EntityReference(TablaCliente, id);
        }

        // ---------------------------------------------------------------- Filas de partida (reconciliar)

        /// <summary>
        /// Un correo entrante sin conversación, de la BU del área, recibido (fixture 01), con el <c>createdon</c> y el <c>actualend</c> dados. Message-ID, asunto y propietario los trae el constructor
        /// (distintos en cada fila); <paramref name="messageId"/> los fija cuando la prueba necesita que dos correos coincidan.
        /// </summary>
        public static ConstructorDeFila Candidato(EscenarioDeSteps e, DateTime creado, string messageId = null)
        {
            var fila = ConstructorDeDatos.UnCorreoEntrante()
                .Con("createdon", creado)
                .Con("actualend", creado)
                .Con("sender", "Cliente@Example.com")
                .Con("owningbusinessunit", RefBu(e.Bu));
            if (messageId != null) fila.Con("messageid", messageId);
            return fila;
        }

        /// <summary>Las conversaciones que hay en el almacén.</summary>
        public static IReadOnlyList<Entity> Conversaciones(EscenarioDeSteps e)
        {
            return e.Almacen.Todas(TablaConv);
        }

        /// <summary>Quita todo registro del Arrange: lo que sigue en <c>Escrituras</c> y <c>Consultas</c> es lo que hizo la API.</summary>
        public static void Limpiar(EscenarioDeSteps e)
        {
            e.Almacen.LimpiarRegistros();
        }

        // ---------------------------------------------------------------- Ejecución

        /// <summary>El contexto de la plataforma para <c>reconciliarcorreos</c>: Stage 30, MessageName = el nombre único, solo las entradas que se pasan (una Action no manda las ausentes).</summary>
        public static ContextoDePluginSimulado ContextoReconciliar(Guid actor, Guid? emailId = null, int? maximo = null, int? minutosMinimos = null)
        {
            var c = new ContextoDePluginSimulado { MessageName = NombreDeLaApiReconciliar, Stage = 30, UserId = actor, InitiatingUserId = actor };
            if (emailId.HasValue) c.InputParameters["emailid"] = emailId.Value;
            if (maximo.HasValue) c.InputParameters["maximo"] = maximo.Value;
            if (minutosMinimos.HasValue) c.InputParameters["minutosminimos"] = minutosMinimos.Value;
            return c;
        }

        /// <summary>El contexto de la plataforma para <c>iniciarconversacionnueva</c>. Un <paramref name="conversacion"/> nulo no manda la clave.</summary>
        public static ContextoDePluginSimulado ContextoIniciar(Guid actor, Guid? conversacion)
        {
            var c = new ContextoDePluginSimulado { MessageName = NombreDeLaApiIniciar, Stage = 30, UserId = actor, InitiatingUserId = actor };
            if (conversacion.HasValue) c.InputParameters["conversacionid"] = conversacion.Value;
            return c;
        }

        /// <summary>Corre el plugin como la plataforma (transacción incluida) y devuelve el contexto con sus salidas. Una excepción sube SIN atraparse.</summary>
        public static ContextoDePluginSimulado Ejecutar(EscenarioDeSteps e, IPlugin api, ContextoDePluginSimulado contexto)
        {
            e.Fabrica.EjecutarComoPlataforma(api, contexto);
            return contexto;
        }

        public static ContextoDePluginSimulado Reconciliar(EscenarioDeSteps e, Guid actor, Guid? emailId = null, int? maximo = null, int? minutosMinimos = null)
        {
            return Ejecutar(e, new ReconciliarCorreosApi(), ContextoReconciliar(actor, emailId, maximo, minutosMinimos));
        }

        public static ContextoDePluginSimulado Iniciar(EscenarioDeSteps e, Guid actor, Guid? conversacion)
        {
            return Ejecutar(e, new IniciarConversacionNuevaApi(), ContextoIniciar(actor, conversacion));
        }

        /// <summary>La lógica de reconciliar armada como la arma la API (todo SYSTEM) pero con el reloj de la prueba.</summary>
        public static ReconciliarCorreosLogica LogicaDeReconciliar(EscenarioDeSteps e, DateTime ahora, List<string> lineasDeTraza = null)
        {
            Action<string> trazar = lineasDeTraza == null ? (Action<string>)null : lineasDeTraza.Add;
            var s = e.ComoSistema;
            var configuracion = new ConfiguracionDelArea(s);
            var registrador = new RegistradorDeCorreo(new CorreosDataverse(s), new ConversacionesDataverse(s), new ClientesDataverse(s, s), configuracion, trazar);
            return new ReconciliarCorreosLogica(new CorreosDataverse(s), registrador, configuracion, new RelojFijo(ahora), trazar);
        }

        // ---------------------------------------------------------------- Salidas

        public static int Entero(ContextoDePluginSimulado c, string nombre)
        {
            Assert.True(c.OutputParameters.Contains(nombre), "No se escribió la salida '" + nombre + "'.");
            return Assert.IsType<int>(c.OutputParameters[nombre]);
        }

        public static bool Logico(ContextoDePluginSimulado c, string nombre)
        {
            Assert.True(c.OutputParameters.Contains(nombre), "No se escribió la salida '" + nombre + "'.");
            return Assert.IsType<bool>(c.OutputParameters[nombre]);
        }

        public static string Texto(ContextoDePluginSimulado c, string nombre)
        {
            Assert.True(c.OutputParameters.Contains(nombre), "No se escribió la salida '" + nombre + "'.");
            return Assert.IsType<string>(c.OutputParameters[nombre]);
        }

        public static Guid Identificador(ContextoDePluginSimulado c, string nombre)
        {
            Assert.True(c.OutputParameters.Contains(nombre), "No se escribió la salida '" + nombre + "'.");
            return Assert.IsType<Guid>(c.OutputParameters[nombre]);
        }

        // ---------------------------------------------------------------- El detalle JSON esperado (armado aparte del código bajo prueba)

        /// <summary>Una fila del <c>detalle</c> tal como lo fija el diseño: claves en orden, minúsculas, <c>null</c> sin comillas.</summary>
        public static string ItemJson(Guid email, string resultado, Guid? conversacion, string codigo)
        {
            return "{\"emailid\":\"" + email.ToString("D", CultureInfo.InvariantCulture) + "\",\"resultado\":\"" + resultado + "\",\"conversacionid\":"
                + (conversacion.HasValue ? "\"" + conversacion.Value.ToString("D", CultureInfo.InvariantCulture) + "\"" : "null")
                + ",\"codigo\":" + (codigo == null ? "null" : "\"" + codigo + "\"") + "}";
        }

        public static string Json(params string[] items)
        {
            return "[" + string.Join(",", items) + "]";
        }

        // ---------------------------------------------------------------- Aserciones compartidas

        public static void AssertSinEscrituras(EscenarioDeSteps e)
        {
            Assert.Empty(e.Almacen.Escrituras);
        }

        public static Guid IdDeRegarding(Entity fila)
        {
            var regarding = Assert.IsType<EntityReference>(EscenarioDeSteps.Valor(fila, "regardingobjectid"));
            Assert.Equal(TablaConv, regarding.LogicalName);
            return regarding.Id;
        }

        public static void AssertRegardingEs(Entity fila, Guid conversacion)
        {
            Assert.Equal(conversacion, IdDeRegarding(fila));
        }

        public static void AssertSinRegarding(Entity fila)
        {
            Assert.False(fila.Contains("regardingobjectid"), "El correo ya tiene Regarding.");
        }

        public static void AssertPropietarioEsElTeam(Entity fila, Guid team)
        {
            var propietario = Assert.IsType<EntityReference>(EscenarioDeSteps.Valor(fila, "ownerid"));
            Assert.Equal(TablaTeam, propietario.LogicalName);
            Assert.Equal(team, propietario.Id);
        }

        public static void AssertEstado(Entity conversacion, EstadoDeConversacion esperado)
        {
            var par = Estados.De(esperado);
            Assert.Equal(par.StateCode, Assert.IsType<OptionSetValue>(conversacion["statecode"]).Value);
            Assert.Equal(par.StatusCode, Assert.IsType<OptionSetValue>(conversacion["statuscode"]).Value);
        }

        /// <summary>El texto del rechazo de una Custom API: la excepción de la plataforma con el mensaje del catálogo, tal cual.</summary>
        public static InvalidPluginExecutionException AssertRechazo(Action accion, Sanic.Carp.Plugins.Dominio.Errores.DefinicionDeError esperado, params object[] marcadores)
        {
            return ApoyoDeSteps.Rechaza(accion, esperado, marcadores);
        }

        // ---------------------------------------------------------------- Filas de partida (iniciar)

        /// <summary>La partida de PA-INI-01: una Finalizada con el indicador, tres entrantes y un saliente, con cliente y oficial.</summary>
        public sealed class CasoDeIniciar
        {
            public EscenarioDeSteps E;

            /// <summary>La fecha de finalización de la anterior.</summary>
            public DateTime T;

            public Guid Cliente;
            public Guid Anterior;

            /// <summary>El oficial asignado a la anterior (NO es quien llama).</summary>
            public Guid OficialAsignado;

            /// <summary>Entrante ANTERIOR a la finalización: se queda.</summary>
            public Guid E1;

            /// <summary>Primer entrante posterior (T + 1 h), con el Message-ID del diseño.</summary>
            public Guid E2;

            /// <summary>Segundo entrante posterior (T + 2 h).</summary>
            public Guid E3;

            /// <summary>Saliente posterior (T + 30 min): se queda.</summary>
            public Guid S1;
        }

        public const string AsuntoDeE2 = "Arreglo nuevo del cliente";
        public const string RemitenteDeE2 = "  Ana@X.com ";
        public const string RemitenteNormalizadoDeE2 = "ana@x.com";

        /// <summary>Un entrante de la conversación <paramref name="conversacion"/> que llegó en <paramref name="llegada"/> (<c>actualend</c>; <c>null</c> deja la columna ausente y la llegada es <c>createdon</c>).</summary>
        public static ConstructorDeFila EntranteDe(Guid conversacion, DateTime? llegada, string messageId = null)
        {
            var fila = ConstructorDeDatos.UnCorreoEntrante()
                .Con("regardingobjectid", RefConv(conversacion))
                .Con("actualend", llegada);
            if (messageId != null) fila.Con("messageid", messageId);
            return fila;
        }

        /// <summary>La Finalizada de PA-INI-01 sin sus correos: indicador sí, finalizada en <paramref name="finalizada"/>, pendiente de <paramref name="pendiente"/>, con el cliente y el oficial dados.</summary>
        public static ConstructorDeFila FinalizadaConIndicador(DateTime? finalizada, DateTime? pendiente, Guid? cliente, Guid? oficial)
        {
            return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada)
                .Con("sanic_fechafinalizacion", finalizada)
                .Con("sanic_correonuevoenfinalizada", true)
                .Con("sanic_sinrespuestadesde", pendiente)
                .Con("sanic_clienteid", cliente.HasValue ? RefCliente(cliente.Value) : null)
                .Con("sanic_oficialasignadoid", oficial.HasValue ? RefUsuario(oficial.Value) : null);
        }

        /// <summary>PA-INI-01 sembrado y con los registros limpios.</summary>
        public static CasoDeIniciar CasoBase(bool cadena = false)
        {
            var e = Escenario(cadena);
            var caso = new CasoDeIniciar { E = e, T = Hora(9, 0) };
            caso.Cliente = e.Sembrar(ConstructorDeDatos.UnCliente());
            caso.OficialAsignado = e.OtroOficial;
            caso.Anterior = e.Sembrar(FinalizadaConIndicador(caso.T, caso.T.AddHours(1), caso.Cliente, caso.OficialAsignado));
            caso.E1 = e.Sembrar(EntranteDe(caso.Anterior, caso.T.AddHours(-1)));
            caso.E2 = e.Sembrar(EntranteDe(caso.Anterior, caso.T.AddHours(1), MessageIdDelDiseno).Con("subject", AsuntoDeE2).Con("sender", RemitenteDeE2));
            caso.E3 = e.Sembrar(EntranteDe(caso.Anterior, caso.T.AddHours(2)));
            caso.S1 = e.Sembrar(ConstructorDeDatos.UnCorreoSaliente().Con("regardingobjectid", RefConv(caso.Anterior)).Con("senton", caso.T.AddMinutes(30)));
            e.Almacen.LimpiarRegistros();
            return caso;
        }

        /// <summary>SHA-256 en hexadecimal minúscula de un texto, calculado con la BCL (nunca con el código bajo prueba).</summary>
        public static string Sha256(string texto)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(texto));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>Todo el texto de la traza de la ejecución, en una sola cadena.</summary>
        public static string TextoDeLaTraza(EscenarioDeSteps e)
        {
            return string.Join("\n", e.Fabrica.Tracing.Lineas);
        }

        /// <summary>La única ejecución en el pipeline de un step de tipo <typeparamref name="T"/>.</summary>
        public static EjecucionDeStep UnicaEjecucionDe<T>(EscenarioDeSteps e)
        {
            return Assert.Single(e.Pipeline.Ejecuciones, x => x.Step.Plugin is T);
        }

        /// <summary>Las filas de <c>Escrituras</c> como "Operación tabla", en orden.</summary>
        public static string[] Secuencia(EscenarioDeSteps e)
        {
            return e.Almacen.Escrituras.Select(w => w.Operacion + " " + w.Tabla).ToArray();
        }

        // ---------------------------------------------------------------- La carrera

        /// <summary>
        /// Una carrera real contra la clave K-01: un step de PreOperation sobre el <c>Create</c> de conversación que, ANTES de que se escriba la fila, crea otra con la misma huella (como lo haría otro hilo que ganó).
        /// La escritura de la API choca entonces con la clave alterna que el doble hace cumplir. Interviene una sola vez.
        /// </summary>
        public sealed class IntrusoDeHuella : IPlugin
        {
            private bool _intervino;

            public void Execute(IServiceProvider serviceProvider)
            {
                if (_intervino) return;
                _intervino = true;
                var contexto = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                var objetivo = (Entity)contexto.InputParameters["Target"];
                var fabrica = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                var otra = new Entity(TablaConv);
                foreach (var par in objetivo.Attributes)
                {
                    if (par.Key == "sanic_nombre" || par.Key == "sanic_carp_tbl_conversacionid") continue;
                    otra[par.Key] = par.Value;
                }
                fabrica.CreateOrganizationService(null).Create(otra);
            }
        }

        /// <summary>Conecta la carrera: clave alterna sobre la huella y el intruso en PreOperation del <c>Create</c> de conversación.</summary>
        public static void ConectarLaCarrera(EscenarioDeSteps e)
        {
            e.Almacen.AgregarClaveAlterna(TablaConv, "sanic_huellamessageid");
            e.Pipeline.Registrar(new IntrusoDeHuella(), "Create", TablaConv, EtapaDeStep.PreOperation, 5);
        }
    }
}
