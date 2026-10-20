using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sanic.Carp.Plugins.Dominio.Correo
{
    /// <summary>
    /// Las piezas puras de la entrada de un correo en una conversación (diseno/03 2.10, "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"). <see cref="RegistradorDeCorreo"/> las orquesta con los
    /// puertos; acá no hay lecturas, escrituras ni reloj. Todo texto con cultura invariante y comparación ordinal (0.6).
    /// </summary>
    public static class ReglasDeEntradaDeCorreo
    {
        /// <summary>Largo máximo de <c>sanic_asunto</c> (dicc. 4.2).</summary>
        public const int LargoMaximoDelAsunto = 400;

        /// <summary>Largo máximo de <c>sanic_remitente</c> (dicc. 4.2).</summary>
        public const int LargoMaximoDelRemitente = 320;

        /// <summary>Saltos que se siguen por <c>sanic_unidaaid</c> antes de rendirse con <c>CARP-ENT-003</c> (2.10 paso 4, PA-ENT-10).</summary>
        public const int SaltosDeUnion = 3;

        private static readonly IReadOnlyList<string> DelCorreo = new ReadOnlyCollection<string>(new[]
        {
            "directioncode", "regardingobjectid", "messageid", "inreplyto", "subject", "sender", "actualend", "createdon", "ownerid", "owningbusinessunit"
        });

        private static readonly IReadOnlyList<string> DeLaConversacion = new ReadOnlyCollection<string>(new[]
        {
            "statecode", "statuscode", "sanic_sinrespuestadesde", "sanic_correonuevoenfinalizada"
        });

        /// <summary>Las 10 columnas que <see cref="RegistradorDeCorreo.Registrar"/> pide al releer el Email (2.10 paso 1 y punto 13), en este orden: <c>directioncode</c>, <c>regardingobjectid</c>, <c>messageid</c>, <c>inreplyto</c>, <c>subject</c>, <c>sender</c>, <c>actualend</c>, <c>createdon</c>, <c>ownerid</c>, <c>owningbusinessunit</c>.</summary>
        public static IReadOnlyList<string> ColumnasDelCorreo { get { return DelCorreo; } }

        /// <summary>Las 4 columnas de la conversación destino que el registrador necesita para decidir (2.10 paso 6): <c>statecode</c>, <c>statuscode</c>, <c>sanic_sinrespuestadesde</c>, <c>sanic_correonuevoenfinalizada</c>.</summary>
        public static IReadOnlyList<string> ColumnasDeLaConversacion { get { return DeLaConversacion; } }

        /// <summary>
        /// Fecha de llegada (2.10 paso 2, DC-13): <paramref name="actualEnd"/> y, si es nula, <paramref name="createdOn"/> (PA-ENT-16). Las dos nulas ⇒
        /// <see cref="InvalidOperationException"/> (la plataforma siempre llena <c>createdon</c>: es un dato corrupto, no un caso de negocio; la base lo vuelve <c>-900</c>).
        /// </summary>
        public static DateTime Llegada(DateTime? actualEnd, DateTime? createdOn)
        {
            if (actualEnd.HasValue) return actualEnd.Value;
            if (createdOn.HasValue) return createdOn.Value;
            throw new InvalidOperationException("El correo no trae actualend ni createdon.");
        }

        /// <summary>
        /// Las variantes del <c>In-Reply-To</c> para buscar el correo al que responde (2.10 paso 3.B, DC-08), sin repetir (ordinal) y en este orden: (1) el texto recortado tal cual, (2) el
        /// contenido sin <c>&lt;</c> inicial ni <c>&gt;</c> final (UNO de cada extremo, y recortado) envuelto en <c>&lt;</c> <c>&gt;</c>, (3) ese contenido sin corchetes. No cambia mayúsculas:
        /// la comparación de Dataverse no las distingue (spike 3; PA-ENT-11). Nulo, vacío, de solo espacios o que queda vacío sin corchetes (<c>"&lt;&gt;"</c>) ⇒ lista vacía.
        /// </summary>
        public static IReadOnlyList<string> VariantesDeMessageId(string inReplyTo)
        {
            var vacia = new ReadOnlyCollection<string>(new string[0]);
            if (inReplyTo == null) return vacia;
            string tal = inReplyTo.Trim();
            if (tal.Length == 0) return vacia;

            // UN corchete de cada extremo (2.10 decision 5), igual que la huella (2.0.2).
            string contenido = tal;
            if (contenido.StartsWith("<", StringComparison.Ordinal)) contenido = contenido.Substring(1);
            if (contenido.EndsWith(">", StringComparison.Ordinal)) contenido = contenido.Substring(0, contenido.Length - 1);
            contenido = contenido.Trim();
            if (contenido.Length == 0) return vacia;

            var variantes = new List<string> { tal };
            foreach (string candidata in new[] { "<" + contenido + ">", contenido })
            {
                if (!variantes.Contains(candidata, StringComparer.Ordinal)) variantes.Add(candidata);
            }
            return new ReadOnlyCollection<string>(variantes);
        }

        /// <summary><c>sanic_asunto</c> de la conversación nueva (2.10 paso 5): el <c>subject</c> recortado a <see cref="LargoMaximoDelAsunto"/> (PA-ENT-17); nulo o vacío ⇒ <c>null</c> (la columna no se envía).</summary>
        public static string AsuntoDeLaConversacion(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return null;
            return subject.Length > LargoMaximoDelAsunto ? subject.Substring(0, LargoMaximoDelAsunto) : subject;
        }

        /// <summary>
        /// <c>sanic_remitente</c> de la conversación nueva (2.10 paso 5): <c>NormalizadorDeTexto.Correo(sender)</c> (sin espacios, minúscula) y DESPUÉS recortado a
        /// <see cref="LargoMaximoDelRemitente"/> (PA-ENT-17). Nulo, vacío o de solo espacios ⇒ <c>null</c> (la columna no se envía).
        /// </summary>
        public static string RemitenteDeLaConversacion(string sender)
        {
            // Primero se normaliza y despues se recorta (PA-ENT-17): al reves se perderian caracteres utiles.
            string correo = NormalizadorDeTexto.Correo(sender);
            if (correo == null) return null;
            return correo.Length > LargoMaximoDelRemitente ? correo.Substring(0, LargoMaximoDelRemitente) : correo;
        }

        /// <summary>
        /// Las columnas del <c>Create</c> de la conversación nueva (2.10 paso 5, PA-ENT-01), en neutro y con EXACTAMENTE estas claves: <c>sanic_huellamessageid</c>, <c>sanic_fechaprimercorreo</c> (=
        /// <paramref name="llegada"/>), <c>sanic_asunto</c> y <c>sanic_remitente</c> (solo si no son <c>null</c>), <c>sanic_sinrespuestadesde</c> (= <paramref name="llegada"/>),
        /// <c>sanic_cantidadrespuestas</c> = 0, <c>sanic_correonuevoenfinalizada</c> = <c>false</c>, <c>ownerid</c> = <c>ReferenciaDeEntidad("team", teamId)</c> y <c>sanic_clienteid</c> =
        /// <c>ReferenciaDeEntidad("sanic_carp_tbl_cliente", clienteId)</c> (solo si <paramref name="clienteId"/> tiene valor). NO lleva <c>sanic_nombre</c> ni estado (los completa la plataforma,
        /// D-41). Huella nula o vacía ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        public static IReadOnlyDictionary<string, object> ColumnasDeLaConversacionNueva(string huella, DateTime llegada, string asunto, string remitente, Guid teamId, Guid? clienteId)
        {
            if (string.IsNullOrEmpty(huella)) throw new ArgumentException("La huella es obligatoria.", nameof(huella));

            var columnas = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "sanic_huellamessageid", huella },
                { "sanic_fechaprimercorreo", llegada },
                { "sanic_sinrespuestadesde", llegada },
                { "sanic_cantidadrespuestas", 0 },
                { "sanic_correonuevoenfinalizada", false },
                { "ownerid", new ReferenciaDeEntidad("team", teamId) }
            };
            if (asunto != null) columnas["sanic_asunto"] = asunto;
            if (remitente != null) columnas["sanic_remitente"] = remitente;
            if (clienteId.HasValue) columnas["sanic_clienteid"] = new ReferenciaDeEntidad(TablasDelArea.Cliente, clienteId.Value);
            return columnas;
        }

        /// <summary>
        /// Lo que la llegada de un correo cambia en una conversación que ya existe (2.10 paso 6, P-09, DC-10): las columnas a escribir, o un diccionario vacío si no hay nada que cambiar (en cuyo
        /// caso el registrador NO escribe la conversación, PA-ENT-05 y PA-ENT-08).
        /// <list type="bullet">
        /// <item><see cref="EstadoDeConversacion.Abierta"/>: <c>sanic_sinrespuestadesde</c> = el menor entre el actual (si lo hay) y <paramref name="llegada"/>; solo se escribe si el valor cambia (PA-ENT-04 a PA-ENT-06).</item>
        /// <item><see cref="EstadoDeConversacion.Finalizada"/>: lo mismo para <c>sanic_sinrespuestadesde</c>, MÁS <c>sanic_correonuevoenfinalizada</c> = <c>true</c> si todavía no lo era (PA-ENT-07, PA-ENT-08). No toca el estado (RF-15).</item>
        /// <item><see cref="EstadoDeConversacion.Unida"/> ⇒ <see cref="ArgumentException"/>: el registrador la redirige antes (DC-09).</item>
        /// </list>
        /// Las fechas se comparan por valor (ticks, sin mirar el <c>Kind</c>). Nunca toca cantidad, primera respuesta ni tiempos (2.11).
        /// </summary>
        public static IReadOnlyDictionary<string, object> AplicarLlegada(EstadoDeConversacion estado, DateTime? sinRespuestaDesde, bool correoNuevoEnFinalizada, DateTime llegada)
        {
            if (estado != EstadoDeConversacion.Abierta && estado != EstadoDeConversacion.Finalizada)
            {
                throw new ArgumentException("Una conversacion Unida se redirige antes de aplicar la llegada (DC-09).", nameof(estado));
            }

            var cambios = new Dictionary<string, object>(StringComparer.Ordinal);

            // El menor entre el actual y la llegada; se escribe solo si cambia, por ticks (sin mirar el Kind).
            if (!sinRespuestaDesde.HasValue || llegada.Ticks < sinRespuestaDesde.Value.Ticks)
            {
                cambios["sanic_sinrespuestadesde"] = llegada;
            }
            if (estado == EstadoDeConversacion.Finalizada && !correoNuevoEnFinalizada)
            {
                cambios["sanic_correonuevoenfinalizada"] = true;
            }
            return cambios;
        }
    }
}
