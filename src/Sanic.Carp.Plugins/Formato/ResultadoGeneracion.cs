using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>Lo que devuelve el generador (diseno/03 4.2). Las advertencias llegan a la salida <c>advertencias</c> de la Custom API (1.1).</summary>
    internal sealed class ResultadoGeneracion
    {
        public byte[] Archivo { get; }
        /// <summary>Avisos que no impiden generar (por ejemplo, una fecha escrita en una celda cuyo formato no es de fecha).</summary>
        public IReadOnlyList<string> Advertencias { get; }
        public bool MarcadoRecalculoAlAbrir { get; }

        public ResultadoGeneracion(byte[] archivo, IReadOnlyList<string> advertencias, bool marcadoRecalculoAlAbrir)
        {
            Archivo = archivo;
            Advertencias = advertencias;
            MarcadoRecalculoAlAbrir = marcadoRecalculoAlAbrir;
        }
    }
}
