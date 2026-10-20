using System;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Qué puede hacer un usuario con UNA fila concreta (diseno/03 1.1 paso 6 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Lo necesita la vista previa de
    /// <c>generarformato</c>: el llamador debe poder ESCRIBIR la versión de plantilla en Borrador (def. L342, V-7). Lo implementa <c>Datos/AccesosDeFilaDataverse</c> sobre el
    /// servicio que se le dé (la API le pasa el del llamador: V-7 midió que un no administrador puede preguntar por sí mismo). Puerto nuevo del lote 6: los diez del lote 4 no lo cubrían.
    /// </summary>
    internal interface IAccesosDeFila
    {
        /// <summary>
        /// ¿<paramref name="usuarioId"/> tiene <c>WriteAccess</c> sobre la fila <paramref name="id"/> de <paramref name="tabla"/>? Un <c>RetrievePrincipalAccessRequest</c> con
        /// <c>Target</c> = referencia a la fila y <c>Principal</c> = referencia a <c>systemuser</c> con <paramref name="usuarioId"/>; devuelve <c>true</c> solo si los derechos de la
        /// respuesta incluyen <c>AccessRights.WriteAccess</c>. Una consulta, sin lecturas extra. Tabla nula o vacía ⇒ <see cref="ArgumentException"/>; <paramref name="id"/> o
        /// <paramref name="usuarioId"/> vacíos ⇒ <see cref="ArgumentException"/>. Toda falla de la plataforma escapa tal cual (la API la traduce a su <c>-900</c>); en particular no
        /// se atrapa "no existe": quien llama comprueba la existencia antes.
        /// </summary>
        bool PuedeEscribir(string tabla, Guid id, Guid usuarioId);
    }
}
