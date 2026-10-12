using System;

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
            return StateCode + "/" + StatusCode;
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

        /// <summary>
        /// Motivo por defecto de un <c>statecode</c> sin <c>statuscode</c> en el <c>Target</c> (DC-06, verificado en V-5): Activo ⇒ 1,
        /// Inactivo ⇒ 2. Cualquier otro <c>statecode</c> lanza <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public static int StatusPorDefecto(int stateCode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Estado efectivo del <c>Target</c> (DC-06): el <paramref name="stateCode"/> y el <paramref name="statusCode"/> que trae, y si
        /// falta el <c>statuscode</c> el que corresponde por defecto (<see cref="StatusPorDefecto"/>). Un <c>statuscode</c> presente se
        /// respeta tal cual, sin validarlo contra la tabla.
        /// </summary>
        public static EstadoEfectivo Efectivo(int stateCode, int? statusCode)
        {
            throw new NotImplementedException();
        }

        /// <summary>Los códigos de cada estado de la Conversación (tabla 0.7).</summary>
        public static EstadoEfectivo De(EstadoDeConversacion estado)
        {
            throw new NotImplementedException();
        }

        /// <summary>Los códigos de cada estado del Arreglo (tabla 0.7).</summary>
        public static EstadoEfectivo De(EstadoDeArreglo estado)
        {
            throw new NotImplementedException();
        }

        /// <summary>El estado de Conversación que corresponde a un par de códigos, o <c>null</c> si la combinación no existe.</summary>
        public static EstadoDeConversacion? DeConversacion(EstadoEfectivo estado)
        {
            throw new NotImplementedException();
        }

        /// <summary>El estado de Arreglo que corresponde a un par de códigos, o <c>null</c> si la combinación no existe.</summary>
        public static EstadoDeArreglo? DeArreglo(EstadoEfectivo estado)
        {
            throw new NotImplementedException();
        }
    }
}
