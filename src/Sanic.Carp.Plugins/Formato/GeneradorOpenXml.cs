using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Adaptador del puerto <see cref="IGeneradorDeFormato"/> sobre <see cref="GeneradorFormato"/> (diseno/03 4.3 punto 4).
    /// <para>
    /// Registra la <see cref="Exception.InnerException"/> antes de que el error salga (spike 1, hallazgo 6): el generador convierte
    /// cualquier excepción cruda del SDK en <see cref="PlantillaInvalidaException"/> y, si nadie traza la causa, un defecto propio queda
    /// disfrazado de "plantilla corrupta". La traza lleva el tipo y el mensaje de la causa, sin datos del arreglo ni del cliente ni
    /// traza de pila (1.1 paso 9). Después relanza LA MISMA excepción: el tipo no cambia y la Custom API la traduce a CARP-GEN-xxx.
    /// </para>
    /// <para>
    /// El generador no conoce Dataverse: la traza llega como un delegado (<c>Infraestructura</c> lo conecta al <c>ITracingService</c>).
    /// Con delegado nulo no se traza nada y el resto del comportamiento es el mismo.
    /// </para>
    /// </summary>
    internal sealed class GeneradorOpenXml : IGeneradorDeFormato
    {
        private readonly Action<string> _trazar;

        public GeneradorOpenXml(Action<string> trazar)
        {
            _trazar = trazar;
        }

        public ResultadoGeneracion Generar(byte[] plantilla, string huellaEsperada, IReadOnlyList<MapeoCelda> mapeo, DatosArreglo datos)
        {
            try
            {
                return GeneradorFormato.GenerarConInforme(plantilla, huellaEsperada, mapeo, datos);
            }
            catch (FormatoException ex)
            {
                // Solo los errores controlados del generador: lo demás (por ejemplo ArgumentNullException) sale sin tocar.
                TrazarCausa(ex);
                throw;
            }
        }

        public string CalcularHuella(byte[] contenido)
        {
            return GeneradorFormato.CalcularHuella(contenido);
        }

        // Una sola llamada con el tipo del error del generador y el tipo y el mensaje de la causa (4.3 punto 7), tal como vienen. Los
        // mensajes de la causa son los del SDK/BCL sobre la estructura del archivo; los datos del arreglo no pasan por ahí (la validación
        // de datos precede al SDK).
        private void TrazarCausa(FormatoException ex)
        {
            var causa = ex.InnerException;
            if (_trazar == null || causa == null) return;

            _trazar("Generador del formato: " + ex.GetType().Name + " con causa interna " + causa.GetType().Name + ": " + causa.Message);
        }
    }
}
