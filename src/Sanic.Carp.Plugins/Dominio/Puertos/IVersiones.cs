using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Acceso a <c>sanic_carp_tbl_versionplantilla</c> y a sus mapeos <c>sanic_carp_tbl_mapeocelda</c> (diseno/03 4.2; reglas comunes en
    /// <see cref="IConversaciones"/>). Lo implementa <c>Datos/VersionesDataverse</c>.
    /// </summary>
    internal interface IVersiones
    {
        /// <summary>La versión con las <paramref name="columnas"/> pedidas, o <c>null</c> si no existe o no se puede leer.</summary>
        FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas);

        /// <summary>Las versiones con <c>sanic_estado</c> Vigente Y <c>statecode</c> 0 (2.5 paso 5). Pagina hasta agotar.</summary>
        IReadOnlyList<FilaDeDatos> Vigentes(IReadOnlyCollection<string> columnas);

        /// <summary>Las mismas que <see cref="Vigentes"/> salvo la de <paramref name="exceptoId"/> (2.8, Publicar paso 5).</summary>
        IReadOnlyList<FilaDeDatos> OtrasVigentes(Guid exceptoId, IReadOnlyCollection<string> columnas);

        /// <summary><c>Update</c> común con exactamente las columnas de <paramref name="cambios"/> (nulo ⇒ <see cref="ArgumentNullException"/>, vacío ⇒ <see cref="ArgumentException"/>).</summary>
        void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios);

        /// <summary>Los mapeos ACTIVOS (<c>statecode</c> 0) de la versión (1.1 paso 7, 2.8 Publicar paso 2). Sin orden; pagina hasta agotar.</summary>
        IReadOnlyList<FilaDeDatos> FilasDeMapeo(Guid versionId, IReadOnlyCollection<string> columnas);

        /// <summary>
        /// El archivo <c>sanic_archivo</c> de la versión, completo, o <c>null</c> si la versión no tiene archivo (la columna está ausente:
        /// <c>CARP-GEN-003</c>, <c>CARP-VER-005</c>). Lee primero esa columna (no adivina el código de falla de una descarga sin archivo, que no
        /// se midió) y luego <c>InitializeFileBlocksDownloadRequest</c> + <c>DownloadBlockRequest</c> en bloques de 4 MiB hasta el tamaño
        /// informado. Si la versión no existe devuelve <c>null</c> (regla común 3). Las fallas de la descarga escapan.
        /// </summary>
        byte[] DescargarArchivo(Guid versionId);
    }
}
