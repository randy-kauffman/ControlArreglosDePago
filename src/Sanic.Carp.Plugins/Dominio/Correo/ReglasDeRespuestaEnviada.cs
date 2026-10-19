using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio.Tiempos;

namespace Sanic.Carp.Plugins.Dominio.Correo
{
    /// <summary>
    /// Las piezas puras de la respuesta enviada (diseno/03 2.11, "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"). <c>RespuestaEnviadaStep</c> (S-35) las orquesta con los repositorios; acá no
    /// hay lecturas, escrituras ni reloj. El cálculo de los tiempos NO se reimplementa: es <see cref="TiemposDeConversacion.AplicarRespuestaEnviada"/> (2.0.6).
    /// </summary>
    public static class ReglasDeRespuestaEnviada
    {
        private static readonly IReadOnlyList<string> DelCorreo = new ReadOnlyCollection<string>(new[]
        {
            "directioncode", "statuscode", "regardingobjectid", "senton", "actualend", "createdby", "sanic_respondidoporid"
        });

        private static readonly IReadOnlyList<string> DeLaConversacion = new ReadOnlyCollection<string>(new[]
        {
            "statecode", "statuscode", "sanic_sinrespuestadesde", "sanic_cantidadrespuestas", "sanic_tiempoprimerarespuesta"
        });

        /// <summary>Las 7 columnas que S-35 pide al releer el Email (2.11 paso 1), en este orden: <c>directioncode</c>, <c>statuscode</c>, <c>regardingobjectid</c>, <c>senton</c>, <c>actualend</c>, <c>createdby</c>, <c>sanic_respondidoporid</c>.</summary>
        public static IReadOnlyList<string> ColumnasDelCorreo { get { return DelCorreo; } }

        /// <summary>Las 5 columnas de la conversación que S-35 necesita (2.11 pasos 3 a 6): <c>statecode</c>, <c>statuscode</c>, <c>sanic_sinrespuestadesde</c>, <c>sanic_cantidadrespuestas</c>, <c>sanic_tiempoprimerarespuesta</c>.</summary>
        public static IReadOnlyList<string> ColumnasDeLaConversacion { get { return DeLaConversacion; } }

        /// <summary>
        /// ¿Hay algo que procesar? (2.11 paso 1). Verdadero solo si, en <paramref name="correo"/> (los <see cref="FilaDeDatos.Valores"/> releídos; una columna ausente es nula): <c>directioncode</c> es
        /// <c>true</c> (saliente), <c>statuscode</c> es <c>Estados.StatusCorreoEnviado</c> (Sent; Failed, Pending Send, Sending, Draft y cualquier otro ⇒ falso, PA-RES-06), el
        /// <c>regardingobjectid</c> es una <see cref="ReferenciaDeEntidad"/> de <c>sanic_carp_tbl_conversacion</c> (sin Regarding o a otra tabla ⇒ falso, PA-RES-08) y
        /// <c>sanic_respondidoporid</c> está ausente o nulo (si ya está informado ⇒ falso: es la marca de "ya procesado", PA-RES-07). Un correo entrante ⇒ falso (PA-RES-08). Nulo ⇒
        /// <see cref="ArgumentNullException"/>.
        /// </summary>
        public static bool DebeProcesarse(IReadOnlyDictionary<string, object> correo)
        {
            if (correo == null) throw new ArgumentNullException(nameof(correo));

            // Cada condicion falla cerrado: una columna ausente no deja procesar (2.11 decision 1).
            if (!(Valor(correo, "directioncode") is bool) || !(bool)correo["directioncode"]) return false;

            var estado = Valor(correo, "statuscode") as ValorDeOpcion;
            if (estado == null || estado.Valor != Estados.StatusCorreoEnviado) return false;

            var regarding = Valor(correo, "regardingobjectid") as ReferenciaDeEntidad;
            if (regarding == null || !string.Equals(regarding.Tabla, TablasDelArea.Conversacion, StringComparison.Ordinal)) return false;

            return Valor(correo, "sanic_respondidoporid") == null;
        }

        private static object Valor(IReadOnlyDictionary<string, object> valores, string columna)
        {
            object valor;
            return valores.TryGetValue(columna, out valor) ? valor : null;
        }

        /// <summary>
        /// El instante del envío (2.11 paso 2): <paramref name="sentOn"/> y, si es nulo, <paramref name="actualEnd"/> (PA-RES-11). Los dos nulos ⇒ <c>null</c>: S-35 termina sin escribir (decisión
        /// del 5f; el servidor siempre pone <c>senton</c> al pasar a Sent, <c>RESULTADO.md</c> (8), así que es un dato corrupto y no se inventa una hora).
        /// </summary>
        public static DateTime? InstanteDelEnvio(DateTime? sentOn, DateTime? actualEnd)
        {
            return sentOn ?? actualEnd;
        }

        /// <summary>
        /// Las columnas de la conversación que cambia una respuesta enviada (2.11 paso 6), en neutro, a partir de lo que ella tiene hoy y de <paramref name="resultado"/>
        /// (<c>TiemposDeConversacion.AplicarRespuestaEnviada</c>): SIEMPRE <c>sanic_cantidadrespuestas</c> = <c>resultado.NuevaCantidad</c> (siempre sube en uno); <c>sanic_sinrespuestadesde</c> solo si
        /// <c>resultado.NuevoSinRespuestaDesde</c> difiere de <paramref name="sinRespuestaDesdeActual"/> (por valor; <c>null</c> explícito = vaciar la columna, PA-RES-01); y
        /// <c>sanic_tiempoprimerarespuesta</c> solo si <c>resultado.NuevaPrimeraRespuesta</c> difiere de <paramref name="primeraRespuestaActual"/> (PA-RES-01, PA-RES-04). Nunca toca el indicador
        /// <c>sanic_correonuevoenfinalizada</c> (OA-10, PA-RES-09). <paramref name="resultado"/> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static IReadOnlyDictionary<string, object> CambiosEnLaConversacion(DateTime? sinRespuestaDesdeActual, int? primeraRespuestaActual, ResultadoDeRespuesta resultado)
        {
            if (resultado == null) throw new ArgumentNullException(nameof(resultado));

            var cambios = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "sanic_cantidadrespuestas", resultado.NuevaCantidad }
            };
            // Un null explicito vacia la columna (PA-RES-01); Cambios compara fechas por ticks, sin mirar el Kind.
            if (Cambios.ValoresDistintos(resultado.NuevoSinRespuestaDesde, sinRespuestaDesdeActual))
            {
                cambios["sanic_sinrespuestadesde"] = resultado.NuevoSinRespuestaDesde;
            }
            if (Cambios.ValoresDistintos(resultado.NuevaPrimeraRespuesta, primeraRespuestaActual))
            {
                cambios["sanic_tiempoprimerarespuesta"] = resultado.NuevaPrimeraRespuesta;
            }
            return cambios;
        }
    }
}
