using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Tiempos;

namespace Sanic.Carp.Plugins.Dominio.Conversacion
{
    /// <summary>
    /// Las reglas de la Conversación (diseno/03 2.3 y 2.4, firma de 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5b)"). Dominio puro: sin <c>Entity</c> ni SDK, sin estado
    /// (ningún campo mutable, ni estático: las listas son <see cref="ReadOnlyCollection{T}"/>), sin leer el reloj (entra en el pedido) y con la identidad perezosa (se consulta
    /// solo cuando la regla ya vio que hay algo que decidir).    /// </summary>
    public static class ReglasDeConversacion
    {
        private static readonly IReadOnlyList<string> PreImagenDeActualizacion = new ReadOnlyCollection<string>(new[]
        {
            "statecode", "statuscode", "sanic_huellamessageid", "sanic_fechaprimercorreo", "sanic_asunto", "sanic_remitente", "sanic_sinrespuestadesde",
            "sanic_correonuevoenfinalizada", "sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta", "sanic_fechafinalizacion",
            "sanic_tiempoprimerarespuesta", "sanic_tiempototal", "sanic_cantidadrespuestas", "sanic_unidaaid", "sanic_finalizadoporid",
            "sanic_clienteid", "sanic_nombre", "sanic_oficialasignadoid", "ownerid"
        });

        private static readonly IReadOnlyList<string> PreImagenDeAsignacion = new ReadOnlyCollection<string>(new[] { "sanic_oficialasignadoid" });

        /// <summary>
        /// Las 21 columnas de la pre-imagen de S-17 (sección 5 y 2.3), en el orden de la sección 5. Es la unión de las columnas solo-sistema, las de gesto, <c>statecode</c>,
        /// <c>statuscode</c> y las tres que se vigilan en una conversación inactiva (<c>sanic_clienteid</c>, <c>sanic_oficialasignadoid</c>, <c>ownerid</c>). El step declara su
        /// registro con esta lista; una prueba la contrasta con la sección 5 y con las categorías de <see cref="Columnas"/>.
        /// </summary>
        public static IReadOnlyList<string> ColumnasDeLaPreImagenDeActualizacion { get { return PreImagenDeActualizacion; } }

        /// <summary>La pre-imagen de S-18: solo <c>sanic_oficialasignadoid</c> (sección 5 y 2.4).</summary>
        public static IReadOnlyList<string> ColumnasDeLaPreImagenDeAsignacion { get { return PreImagenDeAsignacion; } }

        /// <summary>
        /// ¿Cumple <paramref name="nombre"/> el formato del autonumérico de Conversación, <c>^CONV-[0-9]{6,15}$</c> (D-41, 2.3)? Comparación ORDINAL y sensible a mayúsculas, sin recortar espacios:
        /// <c>"CONV-000001"</c> sí; <c>null</c>, el texto vacío, <c>"conv-000001"</c>, <c>"CONV-12345"</c> (5 dígitos), 16 dígitos (no cabe en la columna de 20), un espacio al principio o al final,
        /// un salto de línea al final y cualquier carácter que no sea un dígito ASCII (un dígito de ancho completo o arábigo-índico) no. Nunca lanza. Hay que recorrer los caracteres a
        /// mano o con un patrón que acabe en <c>\z</c>: en .NET el <c>$</c> de una expresión regular acepta un salto de línea final, y <c>\d</c> acepta dígitos Unicode.
        /// </summary>
        public static bool NombreTieneFormatoDeAutonumerico(string nombre)
        {
            if (nombre == null) return false;
            // Se recorre a mano (sublote 5b, punto 5): el `$` de .NET acepta un salto de linea final y `\d` acepta digitos de otros alfabetos.
            var prefijo = "CONV-";
            if (nombre.Length < prefijo.Length + MinimoDeDigitos || nombre.Length > prefijo.Length + MaximoDeDigitos) return false;
            if (!nombre.StartsWith(prefijo, StringComparison.Ordinal)) return false;
            for (int i = prefijo.Length; i < nombre.Length; i++)
                if (nombre[i] < '0' || nombre[i] > '9') return false;
            return true;
        }

        private const int MinimoDeDigitos = 6;
        private const int MaximoDeDigitos = 15;   // la columna mide 20 y `CONV-` ocupa 5 (2.3, paso 2)

        private const string ColNombre = "sanic_nombre";
        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";
        private const string ColResultado = "sanic_resultado";
        private const string ColMotivo = "sanic_motivosinrespuesta";
        private const string ColDetalle = "sanic_detallesinrespuesta";
        private const string ColOficial = "sanic_oficialasignadoid";
        private const string ColCliente = "sanic_clienteid";
        private const string ColPropietario = "ownerid";
        private const string ColSinRespuestaDesde = "sanic_sinrespuestadesde";
        private const string ColPrimerCorreo = "sanic_fechaprimercorreo";
        private const string ColFechaFinalizacion = "sanic_fechafinalizacion";
        private const string ColFinalizadoPor = "sanic_finalizadoporid";
        private const string ColTiempoTotal = "sanic_tiempototal";
        private const string ColCorreoNuevo = "sanic_correonuevoenfinalizada";
        private const string TablaDeUsuarios = "systemuser";

        /// <summary>
        /// Evalúa UN guardado. Nunca devuelve <c>null</c> ni lanza por una regla de negocio (eso es <see cref="ResultadoDeEvaluacion.Rechazar"/>); lanza
        /// <see cref="ArgumentNullException"/> si <paramref name="pedido"/> es nulo y <see cref="ArgumentException"/> si la pre-imagen de una actualización no trae <c>statecode</c> y
        /// <c>statuscode</c>, o trae un par que no existe (tabla 0.7). El resultado tiene una pregunta de identidad solo donde se dice abajo: un test cuenta las consultas.
        /// <para>
        /// <b>Convenciones comunes.</b> (a) "Cambió" es DC-01: la clave está en el <c>Target</c> Y su valor difiere del de la pre-imagen; <c>""</c> y <c>null</c> son lo mismo. (b)
        /// <b>Una columna AUSENTE de la pre-imagen vale nulo</b> (Conversación no tiene seguridad por columna y la plataforma no devuelve las columnas nulas; sin esto, reenviar una
        /// fila completa con sus nulos se vería como cambios que no existen). Es la excepción deliberada a DC-01 punto 1 ("ausente no es nulo"), que sigue valiendo donde hay seguridad por
        /// columna (Arreglo, Cliente). Las claves de la pre-imagen que no son columnas del diseño no cuentan. (c) El <c>Target</c> también trae columnas de la plataforma
        /// (<c>modifiedon</c>, <c>modifiedby</c>, el id primario…): como no son columnas vigiladas, no cuentan nunca. (d) <c>EsEscrituraDelSistema</c> se consulta UNA vez como máximo y solo
        /// después de ver que hay una columna vigilada que cambió (0.3); para el sistema la regla termina permitiendo sin escribir nada (DC-03).
        /// </para>
        /// <para>
        /// <b>Alta</b> (S-36, 2.3): <c>sanic_nombre</c> ausente, nulo o vacío ⇒ permitir. Si cumple <see cref="NombreTieneFormatoDeAutonumerico"/> ⇒ permitir SIN consultar la identidad. Si no (un
        /// texto de espacios tampoco cumple, DC-01): consultar la identidad; sistema ⇒ permitir; persona ⇒ <c>CARP-CNV-001</c> nombrando <c>sanic_nombre</c>. Nunca escribe nada. Un valor que no es
        /// texto no cumple el formato.
        /// </para>
        /// <para>
        /// <b>Asignación</b> (S-18, 2.4): (1) <c>sanic_oficialasignadoid</c> igual al de la pre-imagen (por el id de la referencia) o ausente del <c>Target</c> ⇒ permitir. (2) Sistema ⇒ permitir.
        /// (3) Actual vacío y nuevo = <c>Identidad.ActorId</c> ("Tomar") ⇒ permitir SIN consultar <c>EsJefatura()</c>. (4) Cualquier otro cambio ⇒ <c>EsJefatura()</c> (UNA llamada): verdadero ⇒
        /// permitir, falso ⇒ <c>CARP-CNV-008</c>. Nunca escribe nada. No mira el estado de la conversación: la inactiva la rechaza antes S-17 (orden 10).
        /// </para>
        /// <para>
        /// <b>Actualización</b> (S-17, 2.3), en este orden y ganando el primero que rechaza:
        /// </para>
        /// <list type="number">
        /// <item><b>Qué cambió.</b> Las columnas solo-sistema (<c>Columnas.De(Conversacion).SoloSistema</c>, incluida <c>sanic_nombre</c>) y de gesto que cambiaron; si la pre-imagen es Finalizada o Unida,
        /// también <c>sanic_clienteid</c>, <c>sanic_oficialasignadoid</c> y <c>ownerid</c>. <b>El estado cambió</b> si el estado DESTINO difiere del de la pre-imagen. El destino (DC-06 y DC-29, ver más
        /// abajo) es: con <c>statecode</c> y <c>statuscode</c> en el <c>Target</c>, ese par; con solo <c>statecode</c>, ese estado con su motivo POR DEFECTO (<c>Estados.StatusPorDefecto</c>), aunque el
        /// <c>statecode</c> sea el mismo que el de la pre-imagen; con solo <c>statuscode</c>, el <c>statecode</c> de la pre-imagen con ese motivo (la plataforma valida el motivo contra el estado
        /// actual, RESULTADO.md (6)); con ninguno de los dos, el de la pre-imagen. Un <c>statecode</c>/<c>statuscode</c> presente con valor <c>null</c> cuenta como ausente. Si no cambió el estado y no
        /// cambió ninguna columna ⇒ permitir sin consultar la identidad.</item>
        /// <item>Sistema ⇒ permitir sin escribir nada (DC-03; también para un estado que la plataforma reescribiría, ver DC-29).</item>
        /// <item><b>Conversación inactiva.</b> Pre-imagen Finalizada o Unida y cambió alguna columna (cualquiera de las del paso 1, sin contar el estado) ⇒ <c>CARP-CNV-009</c>, salvo el gesto Reabrir (pre-imagen
        /// Finalizada y destino Abierta), que sigue al 3 bis.</item>
        /// <item>3 bis. <b>Reabrir es puro (D-40).</b> Cambió alguna de <c>sanic_clienteid</c>, <c>sanic_nombre</c>, <c>sanic_oficialasignadoid</c>, <c>ownerid</c> ⇒ <c>CARP-CNV-010</c> nombrando las que cambiaron,
        /// en ese orden y separadas por coma y espacio. Gana a los pasos 4 y 5.</item>
        /// <item>Cambió alguna solo-sistema ⇒ <c>CARP-CNV-001</c> nombrando las que cambiaron (en el orden de <c>Columnas</c>, separadas por coma y espacio).</item>
        /// <item><b>Estado.</b> Sea <c>de</c> el de la pre-imagen y <c>a</c> el destino. Si cambió el estado: Abierta→Finalizada es <b>Finalizar</b>; Finalizada→Abierta es <b>Reabrir</b>; cualquier destino
        /// Unida (el par Inactivo/Unida) ⇒ <c>CARP-CNV-006</c>; desde Unida a cualquier otro ⇒ <c>CARP-CNV-002</c>; cualquier otra combinación (también un par que no existe) ⇒ <c>CARP-CNV-002</c>. Orden: primero
        /// el destino Unida, luego el origen Unida. Finalizar exige <c>sanic_resultado</c> en el <c>Target</c> (ausente, nulo o sin valor) ⇒ <c>CARP-CNV-003</c>; si es SinRespuesta exige en el <c>Target</c>
        /// <c>sanic_motivosinrespuesta</c> y <c>sanic_detallesinrespuesta</c> no vacíos (el detalle recortado) ⇒ <c>CARP-CNV-004</c>. Reabrir con resultado, motivo o detalle en el <c>Target</c> y distinto
        /// del de la pre-imagen ⇒ <c>CARP-CNV-005</c>. Si el estado NO cambió y el resultado, el motivo o el detalle cambiaron ⇒ <c>CARP-CNV-005</c>.</item>
        /// <item><b>Efectos</b> (<see cref="ResultadoDeEvaluacion.Escribir"/>, valores neutros). <b>Finalizar</b>: <c>sanic_finalizadoporid</c> = <c>ReferenciaDeEntidad("systemuser", Identidad.ActorId)</c>;
        /// <c>sanic_fechafinalizacion</c> = <c>AhoraUtc</c>; <c>sanic_tiempototal</c> = <c>TiemposDeConversacion.MinutosEntre(fechaprimercorreo de la pre-imagen, AhoraUtc)</c> (no se escribe si
        /// <c>sanic_fechaprimercorreo</c> es nula); <c>sanic_sinrespuestadesde</c> = <c>null</c>; si el resultado NO es SinRespuesta, <c>sanic_motivosinrespuesta</c> y <c>sanic_detallesinrespuesta</c> = <c>null</c> (si lo es,
        /// no se tocan). <b>Reabrir</b>: <c>sanic_resultado</c>, <c>sanic_motivosinrespuesta</c>, <c>sanic_detallesinrespuesta</c>, <c>sanic_fechafinalizacion</c>, <c>sanic_finalizadoporid</c>,
        /// <c>sanic_tiempototal</c> = <c>null</c> y <c>sanic_correonuevoenfinalizada</c> = <c>false</c>; NO se toca <c>sanic_sinrespuestadesde</c> (DC-10). En ningún caso se escribe <c>statuscode</c>.
        /// Cualquier otra cosa permitida (sin cambio de estado, o el sistema) no escribe nada.</item>
        /// </list>
        /// <para>
        /// <b>DC-29 (decisión del sublote 5b).</b> Reenviar el MISMO <c>statecode</c> sin <c>statuscode</c> no es "no cambio" cuando el motivo de la pre-imagen no es el de por defecto de ese estado: la plataforma lo
        /// resetea. En Conversación solo ocurre en una <b>Unida</b> (Abierta y Finalizada ya tienen el motivo por defecto): <c>statecode = 1</c> la convertiría en Finalizada. Se evalúa como el gesto que la plataforma
        /// produciría (el destino del paso 1), así que una persona recibe <c>CARP-CNV-002</c> (Unida→cualquiera, 3.1); si además cambia otras columnas gana el paso 3, <c>CARP-CNV-009</c>. En una Abierta o una
        /// Finalizada el reenvío sigue sin ser un cambio. Con el par completo y válido DC-01 sigue igual. El sistema (DC-03) no queda protegido: ninguna escritura del diseño manda solo <c>statecode</c> (0.7).
        /// </para>
        /// </summary>
        public static ResultadoDeEvaluacion Evaluar(PedidoDeConversacion pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));
            switch (pedido.Operacion)
            {
                case OperacionDeConversacion.Alta: return EvaluarAlta(pedido);
                case OperacionDeConversacion.Asignacion: return EvaluarAsignacion(pedido);
                default: return EvaluarActualizacion(pedido);
            }
        }

        // ------------------------------------------------------------------ apoyos

        private static object Valor(IReadOnlyDictionary<string, object> valores, string columna)
        {
            object valor;
            return valores != null && valores.TryGetValue(columna, out valor) ? valor : null;
        }

        // DC-01 con la excepcion del sublote 5b: una columna AUSENTE de la pre-imagen vale nulo (Conversacion no tiene seguridad por columna).
        private static bool Cambio(PedidoDeConversacion pedido, string columna)
        {
            object nuevo;
            if (!pedido.Target.TryGetValue(columna, out nuevo)) return false;
            return Cambios.ValoresDistintos(nuevo, Valor(pedido.PreImagen, columna));
        }

        private static List<string> Cambiadas(PedidoDeConversacion pedido, IEnumerable<string> columnas)
        {
            var cambiadas = new List<string>();
            foreach (var columna in columnas)
                if (Cambio(pedido, columna)) cambiadas.Add(columna);
            return cambiadas;
        }

        private static int? Entero(object valor)
        {
            var opcion = valor as ValorDeOpcion;
            if (opcion != null) return opcion.Valor;
            if (valor is int) return (int)valor;
            return null;
        }

        private static string Lista(IEnumerable<string> columnas)
        {
            return string.Join(", ", columnas);
        }

        // ------------------------------------------------------------------ alta (S-36, 2.3)

        private static ResultadoDeEvaluacion EvaluarAlta(PedidoDeConversacion pedido)
        {
            object valor;
            pedido.Target.TryGetValue(ColNombre, out valor);
            var texto = valor as string;
            if (valor == null || (texto != null && texto.Length == 0)) return ResultadoDeEvaluacion.Permitir();
            if (texto != null && NombreTieneFormatoDeAutonumerico(texto)) return ResultadoDeEvaluacion.Permitir();
            if (pedido.Identidad.EsEscrituraDelSistema) return ResultadoDeEvaluacion.Permitir();
            return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_001.Crear(ColNombre));
        }

        // ------------------------------------------------------------------ asignacion (S-18, 2.4)

        private static ResultadoDeEvaluacion EvaluarAsignacion(PedidoDeConversacion pedido)
        {
            if (!Cambio(pedido, ColOficial)) return ResultadoDeEvaluacion.Permitir();
            if (pedido.Identidad.EsEscrituraDelSistema) return ResultadoDeEvaluacion.Permitir();

            // "Tomar": el actual esta vacio y el nuevo es quien escribe. No se consulta la jefatura.
            var actualVacio = !Cambios.ValoresDistintos(Valor(pedido.PreImagen, ColOficial), null);
            var nuevo = Valor(pedido.Target, ColOficial) as ReferenciaDeEntidad;
            if (actualVacio && nuevo != null && nuevo.Id == pedido.Identidad.ActorId) return ResultadoDeEvaluacion.Permitir();

            return pedido.Identidad.EsJefatura() ? ResultadoDeEvaluacion.Permitir() : ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_008.Crear());
        }

        // ------------------------------------------------------------------ actualizacion (S-17, 2.3)

        private static ResultadoDeEvaluacion EvaluarActualizacion(PedidoDeConversacion pedido)
        {
            var origen = EstadoDeLaPreImagen(pedido.PreImagen);
            var destino = EstadoDestino(pedido, origen);
            var estadoCambio = !destino.Equals(Estados.De(origen));
            var inactiva = origen != EstadoDeConversacion.Abierta;

            // Paso 1: que cambio.
            var tabla = Columnas.De(Columnas.TablaConversacion);
            var soloSistema = Cambiadas(pedido, tabla.SoloSistema);
            var deGesto = Cambiadas(pedido, tabla.DeGesto);
            var deInactiva = inactiva ? Cambiadas(pedido, new[] { ColCliente, ColOficial, ColPropietario }) : new List<string>();
            var hayColumnas = soloSistema.Count + deGesto.Count + deInactiva.Count > 0;
            if (!estadoCambio && !hayColumnas) return ResultadoDeEvaluacion.Permitir();

            // Paso 2: el sistema no se revalida (DC-03, tambien para el estado que la plataforma reescribiria, DC-29).
            if (pedido.Identidad.EsEscrituraDelSistema) return ResultadoDeEvaluacion.Permitir();

            var esReabrir = origen == EstadoDeConversacion.Finalizada && destino.Equals(Estados.De(EstadoDeConversacion.Abierta));

            // Paso 3: conversacion inactiva (el gesto Reabrir sigue al 3 bis).
            if (inactiva && hayColumnas && !esReabrir) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_009.Crear());

            // Paso 3 bis: Reabrir es puro (D-40). El orden es el del diseno, no el de Columnas.
            if (esReabrir)
            {
                var otras = Cambiadas(pedido, new[] { ColCliente, ColNombre, ColOficial, ColPropietario });
                if (otras.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_010.Crear(Lista(otras)));
            }

            // Paso 4.
            if (soloSistema.Count > 0) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_001.Crear(Lista(soloSistema)));

            // Paso 5: el estado.
            if (!estadoCambio)
                return deGesto.Count > 0 ? ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_005.Crear()) : ResultadoDeEvaluacion.Permitir();

            if (destino.Equals(Estados.De(EstadoDeConversacion.Unida))) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_006.Crear());
            if (origen == EstadoDeConversacion.Unida) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_002.Crear());

            if (origen == EstadoDeConversacion.Abierta && destino.Equals(Estados.De(EstadoDeConversacion.Finalizada))) return Finalizar(pedido);
            if (esReabrir) return Reabrir(pedido);
            return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_002.Crear());
        }

        private static EstadoDeConversacion EstadoDeLaPreImagen(IReadOnlyDictionary<string, object> preImagen)
        {
            var estado = Entero(Valor(preImagen, ColEstado));
            var motivo = Entero(Valor(preImagen, ColMotivoDeEstado));
            if (!estado.HasValue || !motivo.HasValue)
                throw new ArgumentException("La pre-imagen de una actualizacion debe traer statecode y statuscode.", nameof(preImagen));
            var conocido = Estados.DeConversacion(new EstadoEfectivo(estado.Value, motivo.Value));
            if (!conocido.HasValue)
                throw new ArgumentException("La pre-imagen trae un par statecode/statuscode que no existe (diseno/03 0.7).", nameof(preImagen));
            return conocido.Value;
        }

        // DC-06 y DC-29: el estado que la plataforma dejaria, no solo el que el Target dice.
        private static EstadoEfectivo EstadoDestino(PedidoDeConversacion pedido, EstadoDeConversacion origen)
        {
            var actual = Estados.De(origen);
            var estado = Entero(Valor(pedido.Target, ColEstado));
            var motivo = Entero(Valor(pedido.Target, ColMotivoDeEstado));
            if (motivo.HasValue) return new EstadoEfectivo(estado ?? actual.StateCode, motivo.Value);
            if (!estado.HasValue) return actual;
            // Un statecode que no es 0 ni 1 no tiene motivo por defecto: queda como un par que no existe (-1) y se rechaza como transicion invalida.
            var porDefecto = estado.Value == Estados.Activo || estado.Value == Estados.Inactivo ? Estados.StatusPorDefecto(estado.Value) : -1;
            return new EstadoEfectivo(estado.Value, porDefecto);
        }

        private static ResultadoDeEvaluacion Finalizar(PedidoDeConversacion pedido)
        {
            // El resultado se lee del Target, no de la pre-imagen (sublote 5b, punto 4).
            var resultado = Entero(Valor(pedido.Target, ColResultado));
            if (!resultado.HasValue) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_003.Crear());

            var sinRespuesta = resultado.Value == (int)ResultadoDeConversacion.SinRespuesta;
            if (sinRespuesta)
            {
                var detalle = Valor(pedido.Target, ColDetalle) as string;
                if (!Entero(Valor(pedido.Target, ColMotivo)).HasValue || string.IsNullOrWhiteSpace(detalle))
                    return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_004.Crear());
            }

            var escribir = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ColFinalizadoPor, new ReferenciaDeEntidad(TablaDeUsuarios, pedido.Identidad.ActorId) },
                { ColFechaFinalizacion, pedido.AhoraUtc },
                { ColSinRespuestaDesde, null }   // DC-10
            };
            if (Valor(pedido.PreImagen, ColPrimerCorreo) is DateTime)
                escribir[ColTiempoTotal] = TiemposDeConversacion.MinutosEntre((DateTime)Valor(pedido.PreImagen, ColPrimerCorreo), pedido.AhoraUtc);
            if (!sinRespuesta)
            {
                escribir[ColMotivo] = null;
                escribir[ColDetalle] = null;
            }
            return ResultadoDeEvaluacion.Permitir(escribir);
        }

        private static ResultadoDeEvaluacion Reabrir(PedidoDeConversacion pedido)
        {
            if (Cambiadas(pedido, new[] { ColResultado, ColMotivo, ColDetalle }).Count > 0)
                return ResultadoDeEvaluacion.Rechazar(CodigosDeError.CNV_005.Crear());

            // No se toca sanic_sinrespuestadesde (DC-10) ni se escribe statuscode.
            var escribir = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { ColResultado, null },
                { ColMotivo, null },
                { ColDetalle, null },
                { ColFechaFinalizacion, null },
                { ColFinalizadoPor, null },
                { ColTiempoTotal, null },
                { ColCorreoNuevo, false }
            };
            return ResultadoDeEvaluacion.Permitir(escribir);
        }
    }
}
