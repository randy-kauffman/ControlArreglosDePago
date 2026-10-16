using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <c>statecode</c> / <c>statuscode</c> como los deja la plataforma (diseno/03 4.5, filas "Autonumérico y estado inicial" y punto 5 del lote 3b; <c>13-estados.json</c> y <c>14-email.json</c>). Todo sale de
    /// <see cref="Estados"/>: el doble no repite los números de 0.7. Las tablas PROPIAS tienen los pares de 0.7 (Conversación y Arreglo) o (0, 1) y (1, 2) (las demás); el correo (<c>email</c>), la única nativa con
    /// estado medido, tiene los suyos. El resto de las nativas no pasa por acá: no se completa ni se valida su estado.
    /// </summary>
    internal static class EstadoDeLasTablas
    {
        // ---- Correo (email): 14-email.json. Estados: Open, Completed, Canceled. Los números son los de la plataforma (V-3 y la medición del spike), no los de un choice nuestro. ----
        private const int CorreoAbierto = 0;
        private const int CorreoCancelado = 2;
        private const int MotivoCorreoBorrador = 1;
        private const int MotivoCorreoAbiertoAlterno = 8;
        private const int MotivoCorreoCompletado = 2;
        private const int MotivoCorreoCancelado = 5;
        private const int MotivoCorreoPendienteDeEnvio = 6;
        private const int MotivoCorreoEnviando = 7;

        /// <summary>El estado en el que nace un alta que no trae estado (P-13): Conversación Abierta, Arreglo Borrador y, en las demás, Activo con su motivo por defecto.</summary>
        public static EstadoEfectivo Inicial(string tabla)
        {
            if (string.Equals(tabla, Columnas.TablaConversacion, StringComparison.Ordinal)) return Estados.De(EstadoDeConversacion.Abierta);
            if (string.Equals(tabla, Columnas.TablaArreglo, StringComparison.Ordinal)) return Estados.De(EstadoDeArreglo.Borrador);
            return new EstadoEfectivo(Estados.Activo, Estados.StatusPorDefecto(Estados.Activo));
        }

        /// <summary>
        /// Todos los pares válidos de una tabla PROPIA: las combinaciones de 0.7 en Conversación y Arreglo; en las demás, (0, 1) y (1, 2) ("estándar, sin motivos propios").
        /// </summary>
        public static IReadOnlyList<EstadoEfectivo> Pares(string tabla)
        {
            if (string.Equals(tabla, Columnas.TablaConversacion, StringComparison.Ordinal))
                return Enum.GetValues(typeof(EstadoDeConversacion)).Cast<EstadoDeConversacion>().Select(e => Estados.De(e)).ToList();
            if (string.Equals(tabla, Columnas.TablaArreglo, StringComparison.Ordinal))
                return Enum.GetValues(typeof(EstadoDeArreglo)).Cast<EstadoDeArreglo>().Select(e => Estados.De(e)).ToList();
            return new[]
            {
                new EstadoEfectivo(Estados.Activo, Estados.StatusPorDefecto(Estados.Activo)),
                new EstadoEfectivo(Estados.Inactivo, Estados.StatusPorDefecto(Estados.Inactivo))
            };
        }

        /// <summary>La tabla tiene ese <c>statecode</c>: solo 0 (Activo) y 1 (Inactivo), diccionario 3.2.</summary>
        public static bool TieneEstado(int stateCode)
        {
            return stateCode == Estados.Activo || stateCode == Estados.Inactivo;
        }

        /// <summary>
        /// El motivo que la plataforma pone cuando un <c>statecode</c> llega solo (V-5, medido en <c>13-estados.json</c>): el motivo por defecto del estado (<see cref="Estados.StatusPorDefecto"/>), también si el estado
        /// es el mismo que el de la fila. Un <c>statecode</c> que la tabla no tiene no se llama acá: el <c>Update</c> lanza <see cref="NotSupportedException"/> (no se midió).
        /// </summary>
        public static int StatusPorDefecto(int stateCode)
        {
            return Estados.StatusPorDefecto(stateCode);
        }

        /// <summary>A qué estado pertenece el motivo <paramref name="statusCode"/> en la tabla, o <c>null</c> si ningún estado de la tabla lo tiene (<c>999 is not a valid status code on …</c>).</summary>
        public static int? EstadoDelMotivo(string tabla, int statusCode)
        {
            foreach (var par in Pares(tabla)) if (par.StatusCode == statusCode) return par.StateCode;
            return null;
        }

        /// <summary>La combinación es válida para la tabla.</summary>
        public static bool Admite(string tabla, int stateCode, int statusCode)
        {
            return Pares(tabla).Any(p => p.StateCode == stateCode && p.StatusCode == statusCode);
        }

        /// <summary>El nombre del estado en el mensaje de la plataforma: <c>&lt;tabla&gt;State.Active</c> o <c>&lt;tabla&gt;State.Inactive</c>.</summary>
        public static string NombreDelEstado(string tabla, int stateCode)
        {
            return tabla + "State." + (stateCode == Estados.Activo ? "Active" : "Inactive");
        }

        // ---- Correo ----

        public static bool EsCorreo(string tabla)
        {
            return string.Equals(tabla, "email", StringComparison.Ordinal);
        }

        /// <summary>El estado en el que nace un correo, sin importar el <c>statecode</c> que se envíe (medido: <c>Create</c> sin estado y con <c>statecode</c> 0, 1 o 2 nacen en (0, 1)).</summary>
        public static EstadoEfectivo InicialDelCorreo()
        {
            return new EstadoEfectivo(CorreoAbierto, MotivoCorreoBorrador);
        }

        /// <summary>El motivo por defecto de un <c>statecode</c> de correo cuando llega solo: 0 ⇒ 1, 1 ⇒ 2, 2 ⇒ 5 (medido); <c>null</c> si el correo no tiene ese estado.</summary>
        public static int? MotivoPorDefectoDelCorreo(int stateCode)
        {
            switch (stateCode)
            {
                case CorreoAbierto: return MotivoCorreoBorrador;
                case Estados.StateCorreoCompletado: return MotivoCorreoCompletado;
                case CorreoCancelado: return MotivoCorreoCancelado;
                default: return null;
            }
        }

        /// <summary>Los pares que un <c>Update</c> acepta tal cual en un correo (medido): (0, 1), (0, 8), (1, 2), (1, 3), (1, 4) y (2, 5).</summary>
        public static bool AdmiteElCorreo(int stateCode, int statusCode)
        {
            switch (stateCode)
            {
                case CorreoAbierto: return statusCode == MotivoCorreoBorrador || statusCode == MotivoCorreoAbiertoAlterno;
                case Estados.StateCorreoCompletado: return statusCode == MotivoCorreoCompletado || statusCode == Estados.StatusCorreoEnviado || statusCode == Estados.StatusCorreoRecibido;
                case CorreoCancelado: return statusCode == MotivoCorreoCancelado;
                default: return false;
            }
        }

        /// <summary>
        /// Los pares que la plataforma acepta y REEMPLAZA por el motivo por defecto del estado (medido): (1, 1) pasa a (1, 2) y (2, 1) a (2, 5). Devuelve el motivo que queda, o <c>null</c> si el par no es de esos.
        /// </summary>
        public static int? MotivoQueReemplazaElCorreo(int stateCode, int statusCode)
        {
            if (statusCode != MotivoCorreoBorrador) return null;
            if (stateCode == Estados.StateCorreoCompletado || stateCode == CorreoCancelado) return MotivoPorDefectoDelCorreo(stateCode);
            return null;
        }

        /// <summary><c>statuscode</c> 6 (Pending Send) y 7 (Sending): los estados del envío, que el spike nunca midió (la regla era "nunca enviar un correo").</summary>
        public static bool EsMotivoDeEnvioNoMedido(int statusCode)
        {
            return statusCode == MotivoCorreoPendienteDeEnvio || statusCode == MotivoCorreoEnviando;
        }

        /// <summary>El nombre del estado de un correo en el mensaje de la plataforma: <c>EmailState.Open</c>, <c>EmailState.Completed</c> o <c>EmailState.Canceled</c>.</summary>
        public static string NombreDelEstadoDelCorreo(int stateCode)
        {
            switch (stateCode)
            {
                case CorreoAbierto: return "EmailState.Open";
                case Estados.StateCorreoCompletado: return "EmailState.Completed";
                default: return "EmailState.Canceled";
            }
        }
    }
}
