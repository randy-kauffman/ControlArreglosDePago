using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Apoyos de las pruebas de la entrada de un correo y de la respuesta enviada (sublote 5f, vuelta 2B: S-34 y S-35). Vive en <c>Aceptacion/</c> para que el constructor no pueda tocarlo.
    /// Es independiente de <c>ApoyoDeCorreo</c> (la otra mitad del sublote).
    /// <para>
    /// <b>Condiciones del doble que este apoyo respeta.</b> (1) <c>inreplyto</c> se pierde en un <c>Create</c>: solo <c>Sembrar</c> lo conserva. (2) Un <c>Create</c> de correo nace siempre (0, 1) y con otro
    /// <c>statuscode</c> lanza <see cref="NotSupportedException"/>. (3) Un <c>Update</c> de correo con solo <c>statuscode</c> lanza <see cref="NotSupportedException"/>: para despertar a S-35 se manda el par
    /// completo <c>(1, 3)</c> sobre un saliente sembrado YA en Sent (el doble no pisa un <c>senton</c> existente). (4) Los estados 6 y 7 no se pueden crear ni escribir: se siembran y el step se ejecuta directo.
    /// (5) El doble pone <c>senton</c> (truncado a segundos) solo al pasar a Sent una fila que no lo tiene.
    /// </para>
    /// </summary>
    internal static class ApoyoDeCorreoEntrante
    {
        public const string TablaCorreo = TablasDelArea.Correo;
        public const string TablaConv = TablasDelArea.Conversacion;
        public const string TablaCliente = TablasDelArea.Cliente;
        public const string TablaUsuario = "systemuser";
        public const string TablaTeam = "team";
        public const string TablaBu = "businessunit";

        /// <summary>El Message-ID de PA-ENT-01 y su huella (diseno/03 2.10).</summary>
        public const string MessageIdDelDiseno = "<CAF+abc123@mail.example.com>";

        public const string HuellaDelDiseno = "093f09291642088a9522c5833b491aa367f7d28c10544c960eff9d3a8dd05d1d";

        /// <summary>El Email de PA-ENT-18 (sin <c>messageid</c>) y la huella de respaldo que le toca.</summary>
        public static readonly Guid CorreoDeLaHuellaDeRespaldo = new Guid("11111111-1111-1111-1111-111111111111");

        public const string HuellaDeRespaldo = "bdc7ce0b320a83847a51efaa9e521460ec45b9a52d14d77914a562c933b99340";

        // Pares de estado de un correo que el doble acepta (medido en 14-email.json) y los dos que no (6 y 7).
        public const int EstadoAbierto = 0;
        public const int EstadoCancelado = 2;
        public const int MotivoBorrador = 1;
        public const int MotivoFallido = 8;
        public const int MotivoCompletado = 2;
        public const int MotivoCancelado = 5;
        public const int MotivoPendienteDeEnvio = 6;
        public const int MotivoEnviando = 7;

        /// <summary>Un instante del 2026-10-19 en UTC. Las pruebas de esta mitad siempre usan este día (el reloj del almacén es otro: 2026-10-14).</summary>
        public static DateTime Hora(int hora, int minuto, int segundo = 0)
        {
            return new DateTime(2026, 10, 19, hora, minuto, segundo, DateTimeKind.Utc);
        }

        /// <summary>
        /// El escenario con el área sembrada. <paramref name="cadena"/> registra los steps que ven las escrituras del job: S-02 (propietario al crear), S-36 (conversación al crear) y S-17 (conversación al
        /// actualizar). <paramref name="entrada"/> registra S-34 y <paramref name="respuesta"/> registra S-35. S-33 no se registra aquí: depende de la otra mitad del sublote.
        /// </summary>
        public static EscenarioDeSteps Escenario(bool entrada = false, bool respuesta = false, bool cadena = false, int unidadesDeNegocio = 1)
        {
            var e = new EscenarioDeSteps(unidadesDeNegocio);
            if (cadena)
            {
                e.Registrar(new PropietarioAlCrearStep());
                e.Registrar(new ConversacionCrearStep());
                e.Registrar(new ConversacionActualizarStep());
            }
            if (entrada) e.Registrar(new RegistrarCorreoEntranteStep());
            if (respuesta) e.Registrar(new RespuestaEnviadaStep());
            return e;
        }

        /// <summary>El registrador armado como lo arma S-34: todo con SYSTEM (<c>ComoSistema</c>) y el mismo servicio como llamador y como sistema de <c>ClientesDataverse</c>.</summary>
        public static RegistradorDeCorreo Registrador(EscenarioDeSteps e, List<string> lineasDeTraza = null, IConfiguracionDelArea configuracion = null)
        {
            Action<string> trazar = lineasDeTraza == null ? (Action<string>)null : lineasDeTraza.Add;
            return new RegistradorDeCorreo(new CorreosDataverse(e.ComoSistema), new ConversacionesDataverse(e.ComoSistema), new ClientesDataverse(e.ComoSistema, e.ComoSistema),
                configuracion ?? new ConfiguracionDelArea(e.ComoSistema), trazar);
        }

        // ---------------------------------------------------------------- Filas de partida

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

        /// <summary>
        /// Un correo entrante recibido (fixture 01) con el asunto y el remitente del diseño y la llegada pedida en <c>actualend</c>. El <c>createdon</c> queda en el que trae el constructor (2026-10-14), a propósito:
        /// una implementación que mire <c>createdon</c> en lugar de <c>actualend</c> no puede dar la llegada esperada. Sin Regarding ni <c>inreplyto</c>; su propietario es un usuario cualquiera (no el team).
        /// </summary>
        public static ConstructorDeFila Entrante(DateTime llegada, string messageId = MessageIdDelDiseno)
        {
            // Cierre del sublote 5f: S-34 solo procesa correos de la BU del area (2.10 punto 13). La plataforma fija owningbusinessunit al crear; el doble no la deriva, asi que se siembra.
            return ConstructorDeDatos.UnCorreoEntrante()
                .Con("messageid", messageId)
                .Con("subject", "Arreglo")
                .Con("sender", "Ana@X.com")
                .Con("actualend", llegada)
                .Con("owningbusinessunit", RefBu(ConfiguracionSimulada.IdDeBuPorDefecto));
        }

        public static EntityReference RefBu(Guid id)
        {
            return new EntityReference(TablaBu, id);
        }

        /// <summary>
        /// Crea un entrante por <c>Create</c> (así queda encolado el job de S-34) y lo deja como lo dejaría la plataforma: con <c>owningbusinessunit</c> = <paramref name="bu"/> (por defecto la BU del área).
        /// El doble IGNORA <c>owningbusinessunit</c> al escribir y no la deriva del propietario (<c>RESULTADO.md</c>, 18-texto-y-esquema), y el job relee la fila: por eso, después del <c>Create</c> se borra la
        /// fila y se siembra otra vez con el mismo id y la misma columna. El job que quedó encolado no cambia. Ensucia <c>Escrituras</c> (un <c>Delete</c>): llamar a <c>LimpiarRegistros</c> después.
        /// </summary>
        public static Guid CrearEntranteDelArea(EscenarioDeSteps e, IOrganizationService como, Entity entidad, Guid? bu = null)
        {
            var id = como.Create(entidad);
            var fila = e.Fila(TablaCorreo, id);
            e.ComoSistema.Delete(TablaCorreo, id);
            fila["owningbusinessunit"] = RefBu(bu ?? e.Bu);
            e.Almacen.Sembrar(fila);
            return id;
        }

        /// <summary>Una respuesta saliente ya en Sent (fixture 01): <c>senton</c> sembrado, <c>createdby</c> el autor, Regarding a la conversación y SIN <c>sanic_respondidoporid</c>.</summary>
        public static ConstructorDeFila Saliente(Guid conversacion, Guid autor, DateTime? enviadoEn)
        {
            return ConstructorDeDatos.UnCorreoSaliente()
                .Con("senton", enviadoEn)
                .Con("createdby", RefUsuario(autor))
                .Con("regardingobjectid", RefConv(conversacion));
        }

        /// <summary>Una conversación Abierta con pendiente (<c>sanic_sinrespuestadesde</c>) y cantidad dadas; <paramref name="pendiente"/> nulo = columna ausente.</summary>
        public static ConstructorDeFila Abierta(DateTime? pendiente, int cantidad = 0)
        {
            return ConstructorDeDatos.UnaConversacion().Con("sanic_sinrespuestadesde", pendiente).Con("sanic_cantidadrespuestas", cantidad);
        }

        // ---------------------------------------------------------------- Despertar a S-35

        /// <summary>
        /// El <c>Update</c> que despierta a S-35: el par <c>(statecode, statuscode)</c> completo (un <c>statuscode</c> solo lo rechaza el doble). Por defecto <c>(1, 3)</c>: sobre un saliente sembrado en Sent
        /// no cambia el estado ni el <c>senton</c>, y trae <c>statuscode</c> para el filtro del registro.
        /// </summary>
        public static void Despertar(IOrganizationService como, Guid correo, int estado = Estados.StateCorreoCompletado, int motivo = Estados.StatusCorreoEnviado)
        {
            como.Update(new Entity(TablaCorreo, correo)
            {
                ["statecode"] = new OptionSetValue(estado),
                ["statuscode"] = new OptionSetValue(motivo)
            });
        }

        // ---------------------------------------------------------------- Ejecución directa

        /// <summary>Ejecuta el step directo, como lo vería la plataforma en un asíncrono PostOperation de <paramref name="mensaje"/> sobre <c>email</c> (sin pipeline: no queda en <c>Ejecuciones</c>).</summary>
        public static void EjecutarDirecto(EscenarioDeSteps e, BaseDeStep paso, string mensaje, Guid correo, Guid actor)
        {
            var contexto = ApoyoDeSteps.Contexto(mensaje, 40, TablaCorreo, actor, new Entity(TablaCorreo, correo), true);
            ApoyoDeSteps.Ejecutar(paso, e.Fabrica, contexto);
        }

        /// <summary>Igual que <see cref="EjecutarDirecto"/> pero con un tracing espía: devuelve las líneas de traza (el formato aplicado a sus argumentos).</summary>
        public static IReadOnlyList<string> EjecutarDirectoConTraza(EscenarioDeSteps e, BaseDeStep paso, string mensaje, Guid correo, Guid actor)
        {
            var contexto = ApoyoDeSteps.Contexto(mensaje, 40, TablaCorreo, actor, new Entity(TablaCorreo, correo), true);
            var proveedor = new ProveedorConEspia(contexto, e.Fabrica);
            paso.Execute(proveedor);
            return proveedor.Espia.Lineas();
        }

        // ---------------------------------------------------------------- Lo que dejó el pipeline

        /// <summary>Las ejecuciones del pipeline de un step de tipo <typeparamref name="T"/>, en orden de inicio.</summary>
        public static IReadOnlyList<EjecucionDeStep> EjecucionesDe<T>(EscenarioDeSteps e)
        {
            return e.Pipeline.Ejecuciones.Where(x => x.Step.Plugin is T).ToList();
        }

        /// <summary>La única ejecución de un step de tipo <typeparamref name="T"/>.</summary>
        public static EjecucionDeStep UnicaEjecucionDe<T>(EscenarioDeSteps e)
        {
            return Assert.Single(EjecucionesDe<T>(e));
        }

        public static IReadOnlyList<EscrituraRegistrada> Escrituras(EscenarioDeSteps e, string tabla, OperacionDeDatos operacion)
        {
            return ApoyoDeSteps.EscriturasDe(e.Almacen, tabla, operacion);
        }

        /// <summary>Las conversaciones que hay en el almacén.</summary>
        public static IReadOnlyList<Entity> Conversaciones(EscenarioDeSteps e)
        {
            return e.Almacen.Todas(TablaConv);
        }

        /// <summary>Las consultas (RetrieveMultiple) hechas sobre una tabla.</summary>
        public static IReadOnlyList<ConsultaRegistrada> BusquedasEn(EscenarioDeSteps e, string tabla)
        {
            return e.Almacen.Consultas.Where(c => c.Operacion == OperacionDeDatos.RetrieveMultiple && string.Equals(c.Tabla, tabla, StringComparison.Ordinal)).ToList();
        }

        /// <summary>Las consultas de cualquier tabla que filtran por la columna dada.</summary>
        public static IReadOnlyList<ConsultaRegistrada> ConsultasPorColumna(EscenarioDeSteps e, string columna)
        {
            return e.Almacen.Consultas.Where(c => c.Condiciones.Any(x => string.Equals(x.Columna, columna, StringComparison.Ordinal))).ToList();
        }

        public static object Valor(EscenarioDeSteps e, string tabla, Guid id, string columna)
        {
            return EscenarioDeSteps.Valor(e.Fila(tabla, id), columna);
        }

        // ---------------------------------------------------------------- Aserciones compartidas

        /// <summary>El Regarding de la fila es una referencia a esa conversación (tabla y id).</summary>
        public static void AssertRegardingEs(Entity fila, Guid conversacion)
        {
            var regarding = Assert.IsType<EntityReference>(EscenarioDeSteps.Valor(fila, "regardingobjectid"));
            Assert.Equal(TablaConv, regarding.LogicalName);
            Assert.Equal(conversacion, regarding.Id);
        }

        public static void AssertPropietarioEsElTeam(Entity fila, Guid team)
        {
            var propietario = Assert.IsType<EntityReference>(EscenarioDeSteps.Valor(fila, "ownerid"));
            Assert.Equal(TablaTeam, propietario.LogicalName);
            Assert.Equal(team, propietario.Id);
        }

        /// <summary>No hubo ninguna escritura (ni Create, ni Update, ni Delete, ni Assign) desde el último <c>LimpiarRegistros</c>.</summary>
        public static void AssertSinEscrituras(EscenarioDeSteps e)
        {
            Assert.Empty(e.Almacen.Escrituras);
        }

        /// <summary>El estado de una conversación es el del enum (statecode y statuscode de la tabla 0.7).</summary>
        public static void AssertEstado(Entity conversacion, EstadoDeConversacion esperado)
        {
            var par = Estados.De(esperado);
            Assert.Equal(par.StateCode, Assert.IsType<OptionSetValue>(conversacion["statecode"]).Value);
            Assert.Equal(par.StatusCode, Assert.IsType<OptionSetValue>(conversacion["statuscode"]).Value);
        }

        /// <summary>El diccionario neutro tiene EXACTAMENTE esas claves (ordinal) y esos valores. Un <c>null</c> esperado exige la clave presente con <c>null</c>.</summary>
        public static void AssertColumnas(IReadOnlyDictionary<string, object> real, IDictionary<string, object> esperado)
        {
            Assert.NotNull(real);
            Assert.Equal(esperado.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(), real.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            foreach (var par in esperado) AssertValor(par.Key, par.Value, real[par.Key]);
        }

        private static void AssertValor(string columna, object esperado, object real)
        {
            var referencia = esperado as ReferenciaDeEntidad;
            if (referencia != null)
            {
                var recibida = Assert.IsType<ReferenciaDeEntidad>(real);
                Assert.Equal(referencia.Tabla, recibida.Tabla);
                Assert.Equal(referencia.Id, recibida.Id);
                return;
            }
            Assert.True(Equals(esperado, real), "La columna '" + columna + "' valía '" + (real ?? "null") + "' y se esperaba '" + (esperado ?? "null") + "'.");
            if (esperado != null) Assert.Equal(esperado.GetType(), real.GetType());
        }

        /// <summary>Un diccionario de columnas a partir de pares nombre, valor.</summary>
        public static Dictionary<string, object> Pares(params object[] paresNombreValor)
        {
            var columnas = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < paresNombreValor.Length; i += 2) columnas[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return columnas;
        }

        /// <summary>Un texto de <paramref name="largo"/> caracteres, todos distintos de su vecino y reconocibles, para comprobar CUÁLES se conservan al recortar.</summary>
        public static string TextoDeLargo(int largo, string raiz)
        {
            var texto = new System.Text.StringBuilder(largo);
            int n = 0;
            while (texto.Length < largo) texto.Append(raiz).Append(n++.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('-');
            return texto.ToString(0, largo);
        }
    }
}
