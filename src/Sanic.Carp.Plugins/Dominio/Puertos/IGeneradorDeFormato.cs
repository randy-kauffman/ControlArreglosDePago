using System.Collections.Generic;
using Sanic.Carp.Plugins.Formato;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Puerto del generador del F-NIC-0000052 (diseno/03 4.2). Lo implementa <see cref="GeneradorOpenXml"/> y lo sustituye
    /// <c>GeneradorFalso</c> en las pruebas de la Custom API (4.5).
    /// <para>
    /// Decisión del arquitecto del 2026-10-12 (4.3): el puerto usa directamente los tipos de datos del contrato del generador
    /// (<see cref="MapeoCelda"/>, <see cref="DatosArreglo"/>, <see cref="ResultadoGeneracion"/>), que no tienen lógica ni conocen
    /// Dataverse. Es la única arista <c>Dominio → Formato</c>: la que 4.1 reservaba para <c>ValidadorDePublicacion</c> no se usa
    /// (4.3, decisión sobre <c>FilaDeMapeo</c>). Se descartó duplicar los DTO como "tipos neutros" del dominio.
    /// </para>
    /// Los errores salen como subtipos de <see cref="FormatoException"/>: el puerto no los convierte en <c>ErrorDeNegocio</c>; lo hace
    /// la Custom API (1.1, paso 9).
    /// </summary>
    internal interface IGeneradorDeFormato
    {
        /// <summary>
        /// Rellena la plantilla. <see cref="HuellaNoCoincideException"/> (⇒ CARP-GEN-008), <see cref="PlantillaInvalidaException"/> (-009),
        /// <see cref="MapeoInvalidoException"/> (-010) o <see cref="DatosInvalidosException"/> (-011); nunca un archivo parcial.
        /// </summary>
        ResultadoGeneracion Generar(byte[] plantilla, string huellaEsperada, IReadOnlyList<MapeoCelda> mapeo, DatosArreglo datos);

        /// <summary>SHA-256 en hexadecimal minúscula (64 caracteres) del contenido.</summary>
        string CalcularHuella(byte[] contenido);
    }
}
