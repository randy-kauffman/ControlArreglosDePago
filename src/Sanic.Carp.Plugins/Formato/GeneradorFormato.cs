using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Genera el formato de arreglo de pagos rellenando en memoria la plantilla .xlsx de la versión (D-13, D-15, D-16; diseno/03 4.3).
    /// Sin dependencias de Dataverse. O devuelve el archivo completo o lanza una <see cref="FormatoException"/>: nunca un archivo parcial.
    /// Portado de <c>GeneradorFormato</c> del spike 1. La Custom API lo usa a través del adaptador <see cref="GeneradorOpenXml"/>.
    /// </summary>
    internal static class GeneradorFormato
    {
        /// <summary>SHA-256 en hexadecimal minúscula (64 caracteres). Lanza <see cref="ArgumentNullException"/> con <c>null</c>.</summary>
        public static string CalcularHuella(byte[] contenido)
        {
            throw new NotImplementedException("Lote 2: portar GeneradorFormato.CalcularHuella del spike 1.");
        }

        /// <summary>Validación pura del mapeo (sin plantilla): <see cref="ValidadorMapeo.Validar"/>.</summary>
        public static void ValidarMapeo(IReadOnlyList<MapeoCelda> mapeo)
        {
            throw new NotImplementedException("Lote 2: portar GeneradorFormato.ValidarMapeo del spike 1.");
        }

        public static byte[] Generar(byte[] plantilla, string huellaEsperada, IReadOnlyList<MapeoCelda> mapeo, DatosArreglo datos)
        {
            throw new NotImplementedException("Lote 2: portar GeneradorFormato.Generar del spike 1.");
        }

        public static ResultadoGeneracion GenerarConInforme(byte[] plantilla, string huellaEsperada, IReadOnlyList<MapeoCelda> mapeo, DatosArreglo datos)
        {
            throw new NotImplementedException("Lote 2: portar GeneradorFormato.GenerarConInforme del spike 1.");
        }
    }
}
