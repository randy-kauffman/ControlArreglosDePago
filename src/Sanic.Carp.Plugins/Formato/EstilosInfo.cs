using System;
using DocumentFormat.OpenXml.Packaging;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Permite saber si el estilo (StyleIndex) de una celda tiene formato de fecha (spike 1, hallazgo 8).
    /// Lee <c>styles.xml</c> directo del paquete (sin el modelo del SDK) para no reescribir la parte: queda byte a byte igual.
    /// Todo número leído del archivo se interpreta con <c>TryParse</c>; un valor inválido es <see cref="PlantillaInvalidaException"/>.
    /// </summary>
    internal sealed class EstilosInfo
    {
        public int CantidadDeEstilos
        {
            get { throw new NotImplementedException("Lote 2: portar EstilosInfo del spike 1."); }
        }

        public static EstilosInfo Cargar(WorkbookPart libro)
        {
            throw new NotImplementedException("Lote 2: portar EstilosInfo.Cargar del spike 1.");
        }

        /// <summary>El StyleIndex tiene que existir en <c>cellXfs</c>; si no, la plantilla está corrupta (<see cref="PlantillaInvalidaException"/>).</summary>
        public void Verificar(uint estilo, string celda, string hoja)
        {
            throw new NotImplementedException("Lote 2: portar EstilosInfo.Verificar del spike 1.");
        }

        public bool EsFecha(uint estilo)
        {
            throw new NotImplementedException("Lote 2: portar EstilosInfo.EsFecha del spike 1.");
        }

        internal static bool CodigoEsDeFecha(string codigo)
        {
            throw new NotImplementedException("Lote 2: portar EstilosInfo.CodigoEsDeFecha del spike 1.");
        }
    }
}
