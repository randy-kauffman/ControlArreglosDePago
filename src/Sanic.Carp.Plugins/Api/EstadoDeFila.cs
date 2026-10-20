using Sanic.Carp.Plugins.Dominio;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Lee el estado de una conversación leída (<c>statecode</c>/<c>statuscode</c>) para las lógicas de Tomar y Unir. Una columna ausente o de otro tipo da -1, que no
    /// coincide con ningún estado de la tabla 0.7, así que la fila se trata como "no abierta" (falla cerrado). Sin estado.
    /// </summary>
    internal static class EstadoDeFila
    {
        /// <summary>El estado de conversación de la fila, o <c>null</c> si el par no existe.</summary>
        public static EstadoDeConversacion? DeConversacion(FilaDeDatos fila)
        {
            return Estados.DeConversacion(new EstadoEfectivo(Entero(fila, "statecode"), Entero(fila, "statuscode")));
        }

        // Los choices llegan como ValorDeOpcion (conversor neutro).
        private static int Entero(FilaDeDatos fila, string columna)
        {
            object valor;
            if (!fila.Valores.TryGetValue(columna, out valor)) return -1;
            var opcion = valor as ValorDeOpcion;
            if (opcion != null) return opcion.Valor;
            return valor is int ? (int)valor : -1;
        }
    }
}
