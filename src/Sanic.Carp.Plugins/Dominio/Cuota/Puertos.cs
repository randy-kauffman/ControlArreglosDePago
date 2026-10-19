using System;

namespace Sanic.Carp.Plugins.Dominio.Cuota
{
    /// <summary>
    /// Las lecturas con SYSTEM que los steps de Cuota piden (diseno/03 2.7 y "Decisiones del arquitecto, 2026-10-19 (sublote 5d)"). Lo implementa <c>Datos/LecturasDeCuotaDataverse</c>.
    /// Son SYSTEM y no el llamador porque la regla no depende de que la persona vea el arreglo: lo que decide es su estado (<c>CARP-CUO-002</c>), y una lectura denegada daría un rechazo
    /// distinto según el rol. Una falla de lectura distinta de "no existe" o "sin acceso" escapa (no se traga: sería seguir tras una falla de lectura que la plataforma no pidió ignorar).
    /// </summary>
    public interface ILecturasDeCuota
    {
        /// <summary>
        /// El estado del arreglo (<c>statecode</c> y <c>statuscode</c> leídos con UNA consulta por id y solo esas dos columnas, <c>Estados.DeArreglo</c>), o <c>null</c> si el arreglo no existe, no se puede
        /// leer o su par de estado no es ninguno de los cinco de 0.7.
        /// </summary>
        EstadoDeArreglo? EstadoDelArreglo(Guid arregloId);

        /// <summary>
        /// El <c>sanic_arregloid</c> de la cuota (UNA consulta por id y solo esa columna), o <c>null</c> si la cuota no existe, no se puede leer o no trae el lookup. Lo pide <c>CuotaTerminarArregloStep</c>
        /// (S-24 no tiene pre-imagen y el <c>Target</c> de un <c>Update</c> de cuota no trae el arreglo).
        /// </summary>
        Guid? ArregloDeLaCuota(Guid cuotaId);
    }
}
