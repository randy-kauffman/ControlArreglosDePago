using System.Collections.Generic;

namespace ArreglosPago.Formato
{
    public sealed class ResultadoGeneracion
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
