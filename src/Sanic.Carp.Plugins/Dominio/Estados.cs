using System;
using System.Globalization;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>Estados de la Conversación (diseno/03 0.7 y 3.1).</summary>
    public enum EstadoDeConversacion
    {
        Abierta = 1,
        Finalizada = 2,
        Unida = 3
    }

    /// <summary>Estados del Arreglo (diseno/03 0.7 y 3.2).</summary>
    public enum EstadoDeArreglo
    {
        Borrador = 1,
        Vigente = 2,
        Terminado = 3,
        Anulado = 4,
        Descartado = 5
    }

    /// <summary>Par <c>statecode</c> / <c>statuscode</c>. Inmutable; dos pares con los mismos números son iguales.</summary>
    public sealed class EstadoEfectivo : IEquatable<EstadoEfectivo>
    {
        public int StateCode { get; }
        public int StatusCode { get; }

        public EstadoEfectivo(int stateCode, int statusCode)
        {
            StateCode = stateCode;
            StatusCode = statusCode;
        }

        public bool Equals(EstadoEfectivo otro)
        {
            return otro != null && StateCode == otro.StateCode && StatusCode == otro.StatusCode;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EstadoEfectivo);
        }

        public override int GetHashCode()
        {
            return unchecked((StateCode * 397) ^ StatusCode);
        }

        public override string ToString()
        {
            // Cultura invariante: los enteros en textos no dependen de la cultura del hilo (diseno/03 0.6).
            return StateCode.ToString(CultureInfo.InvariantCulture) + "/" + StatusCode.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Modelo de <c>statecode</c> / <c>statuscode</c> (diseno/03 0.7 y DC-06). Las combinaciones válidas de cada tabla son las de la
    /// tabla 0.7; la plataforma rechaza las demás con 400.
    /// </summary>
    public static class Estados
    {
        /// <summary><c>statecode</c> Activo.</summary>
        public const int Activo = 0;

        /// <summary><c>statecode</c> Inactivo.</summary>
        public const int Inactivo = 1;

        // Tabla de diseno/03 0.7. Son los únicos lugares donde aparecen estos números (conv. §6: nunca el literal suelto en otro código).
        private static readonly EstadoEfectivo Abierta = new EstadoEfectivo(0, 1);
        private static readonly EstadoEfectivo Finalizada = new EstadoEfectivo(1, 2);
        private static readonly EstadoEfectivo Unida = new EstadoEfectivo(1, 159460001);
        private static readonly EstadoEfectivo Borrador = new EstadoEfectivo(0, 1);
        private static readonly EstadoEfectivo Vigente = new EstadoEfectivo(0, 159460001);
        private static readonly EstadoEfectivo Terminado = new EstadoEfectivo(1, 2);
        private static readonly EstadoEfectivo Anulado = new EstadoEfectivo(1, 159460002);
        private static readonly EstadoEfectivo Descartado = new EstadoEfectivo(1, 159460003);

        /// <summary>
        /// Motivo por defecto de un <c>statecode</c> sin <c>statuscode</c> en el <c>Target</c> (DC-06, verificado en V-5): Activo ⇒ 1,
        /// Inactivo ⇒ 2. Cualquier otro <c>statecode</c> lanza <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public static int StatusPorDefecto(int stateCode)
        {
            switch (stateCode)
            {
                case Activo: return 1;
                case Inactivo: return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stateCode), stateCode, "statecode solo admite 0 (Activo) o 1 (Inactivo).");
            }
        }

        /// <summary>
        /// Estado efectivo del <c>Target</c> (DC-06): el <paramref name="stateCode"/> y el <paramref name="statusCode"/> que trae, y si
        /// falta el <c>statuscode</c> el que corresponde por defecto (<see cref="StatusPorDefecto"/>). Un <c>statuscode</c> presente se
        /// respeta tal cual, sin validarlo contra la tabla.
        /// </summary>
        public static EstadoEfectivo Efectivo(int stateCode, int? statusCode)
        {
            // El motivo por defecto solo se consulta si hace falta. Con statuscode en el Target se devuelve lo que trae, aunque la
            // combinación no exista: la regla del gesto (DeConversacion/DeArreglo devuelven null) es la que la rechaza con su código CARP.
            return new EstadoEfectivo(stateCode, statusCode.HasValue ? statusCode.Value : StatusPorDefecto(stateCode));
        }

        /// <summary>Los códigos de cada estado de la Conversación (tabla 0.7).</summary>
        public static EstadoEfectivo De(EstadoDeConversacion estado)
        {
            switch (estado)
            {
                case EstadoDeConversacion.Abierta: return Abierta;
                case EstadoDeConversacion.Finalizada: return Finalizada;
                case EstadoDeConversacion.Unida: return Unida;
                default:
                    throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado de conversación desconocido.");
            }
        }

        /// <summary>Los códigos de cada estado del Arreglo (tabla 0.7).</summary>
        public static EstadoEfectivo De(EstadoDeArreglo estado)
        {
            switch (estado)
            {
                case EstadoDeArreglo.Borrador: return Borrador;
                case EstadoDeArreglo.Vigente: return Vigente;
                case EstadoDeArreglo.Terminado: return Terminado;
                case EstadoDeArreglo.Anulado: return Anulado;
                case EstadoDeArreglo.Descartado: return Descartado;
                default:
                    throw new ArgumentOutOfRangeException(nameof(estado), estado, "Estado de arreglo desconocido.");
            }
        }

        /// <summary>El estado de Conversación que corresponde a un par de códigos, o <c>null</c> si la combinación no existe.</summary>
        public static EstadoDeConversacion? DeConversacion(EstadoEfectivo estado)
        {
            if (estado == null) throw new ArgumentNullException(nameof(estado));

            if (estado.Equals(Abierta)) return EstadoDeConversacion.Abierta;
            if (estado.Equals(Finalizada)) return EstadoDeConversacion.Finalizada;
            if (estado.Equals(Unida)) return EstadoDeConversacion.Unida;
            return null;
        }

        /// <summary>El estado de Arreglo que corresponde a un par de códigos, o <c>null</c> si la combinación no existe.</summary>
        public static EstadoDeArreglo? DeArreglo(EstadoEfectivo estado)
        {
            if (estado == null) throw new ArgumentNullException(nameof(estado));

            if (estado.Equals(Borrador)) return EstadoDeArreglo.Borrador;
            if (estado.Equals(Vigente)) return EstadoDeArreglo.Vigente;
            if (estado.Equals(Terminado)) return EstadoDeArreglo.Terminado;
            if (estado.Equals(Anulado)) return EstadoDeArreglo.Anulado;
            if (estado.Equals(Descartado)) return EstadoDeArreglo.Descartado;
            return null;
        }
    }
}
