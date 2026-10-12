using System;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Acceso a filas y celdas de una hoja, creando lo que falte en el orden que exige el esquema.
    /// Los atributos del archivo (r, s, style) se leen como texto y se interpretan con <c>TryParse</c>: el .xlsx es contenido externo
    /// y cualquier valor inválido es <see cref="PlantillaInvalidaException"/>, nunca una excepción cruda del SDK (spike 1, hallazgo 6).
    /// </summary>
    internal sealed class HojaEditable
    {
        public string Nombre
        {
            get { throw new NotImplementedException("Lote 2: portar HojaEditable del spike 1."); }
        }

        public WorksheetPart Parte
        {
            get { throw new NotImplementedException("Lote 2: portar HojaEditable del spike 1."); }
        }

        public HojaEditable(string nombre, WorksheetPart parte)
        {
            throw new NotImplementedException("Lote 2: portar HojaEditable del spike 1.");
        }

        /// <summary>Devuelve la celda; si no existe la crea con el estilo de columna, si lo hay.</summary>
        public Cell ObtenerOCrearCelda(ReferenciaCelda r)
        {
            throw new NotImplementedException("Lote 2: portar HojaEditable.ObtenerOCrearCelda del spike 1.");
        }

        /// <summary>StyleIndex de la celda leído como texto; sin atributo <c>s</c> es el estilo 0.</summary>
        public uint EstiloDeCelda(Cell c)
        {
            throw new NotImplementedException("Lote 2: portar HojaEditable.EstiloDeCelda del spike 1.");
        }

        /// <summary>True si la celda está dentro de una combinación pero no es su primera celda (lo que se escriba ahí no se ve).</summary>
        public bool EsParteOcultaDeCombinada(ReferenciaCelda r, out string rango)
        {
            throw new NotImplementedException("Lote 2: portar HojaEditable.EsParteOcultaDeCombinada del spike 1.");
        }

        public bool TieneFormulas()
        {
            throw new NotImplementedException("Lote 2: portar HojaEditable.TieneFormulas del spike 1.");
        }

        /// <summary>Celdas de texto compartido (<c>t="s"</c>), leídas como texto del atributo.</summary>
        public int CantidadCeldasDeTextoCompartido()
        {
            throw new NotImplementedException("Lote 2: portar HojaEditable.CantidadCeldasDeTextoCompartido del spike 1.");
        }
    }
}
