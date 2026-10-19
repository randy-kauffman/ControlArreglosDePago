using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Plantilla
{
    /// <summary>
    /// Las lecturas con SYSTEM que los steps de Versión de plantilla y de Mapeo de celda piden (diseno/03 2.8 y "Decisiones del arquitecto, 2026-10-19 (sublote 5e)").
    /// Lo implementa <c>Datos/LecturasDeVersionDataverse</c> sobre <c>VersionesDataverse</c> con el servicio de SYSTEM. Son SYSTEM y no el llamador porque la regla decide por el
    /// estado de la versión, no por lo que la persona vea: una lectura denegada daría un rechazo distinto según el rol. Una falla de lectura distinta de "no existe" o "sin acceso"
    /// escapa (no se traga).
    /// </summary>
    public interface ILecturasDeVersion
    {
        /// <summary>
        /// El <c>sanic_estado</c> de la versión (UNA consulta por id, solo esa columna), o <c>null</c> si la versión no existe, no se puede leer, no tiene estado o el valor no es una
        /// opción definida de <see cref="EstadoDeVersionDePlantilla"/>. Para las reglas, <c>null</c> es "no es Borrador" (falla cerrado).
        /// </summary>
        EstadoDeVersionDePlantilla? EstadoDeLaVersion(Guid versionId);

        /// <summary>
        /// El archivo de plantilla completo (<c>VersionesDataverse.DescargarArchivo</c>), o <c>null</c> si la versión no existe o no tiene archivo (⇒ <c>CARP-VER-005</c> en Publicar).
        /// </summary>
        byte[] ArchivoDeLaVersion(Guid versionId);

        /// <summary>
        /// Los mapeos ACTIVOS (<c>statecode</c> 0) de la versión como <see cref="FilaDeMapeo"/> (UNA consulta con las cinco columnas <c>sanic_hoja</c>, <c>sanic_celda</c>, <c>sanic_tipo</c>,
        /// <c>sanic_campo</c> y <c>sanic_filasmaximas</c>), sin orden. Una columna ausente de la fila (Dev omite las nulas) llega como <c>null</c> en hoja y celda, como opción no definida (0)
        /// en tipo y campo y como <c>null</c> en filas máximas: <see cref="ValidadorDePublicacion"/> las rechaza, no las ignora.
        /// </summary>
        IReadOnlyList<FilaDeMapeo> FilasDeMapeoActivas(Guid versionId);
    }

    /// <summary>Por qué la generación de prueba de la publicación (2.8, Publicar, paso 3) no produjo un libro.</summary>
    public enum FallaDeLaGeneracionDePrueba
    {
        /// <summary>El archivo no es un libro de Excel utilizable (⇒ <c>CARP-VER-006</c>).</summary>
        PlantillaInvalida = 1,

        /// <summary>La plantilla y el mapeo no producen un formato (hoja inexistente, mapeo o datos inválidos para esa plantilla; ⇒ <c>CARP-VER-009</c> con el detalle).</summary>
        FormatoInvalido = 2
    }

    /// <summary>El resultado neutro de <see cref="IPruebaDeGeneracion.Probar"/>: éxito, o la falla con su detalle. Sin tipos de <c>Formato</c>: el dominio no los conoce.</summary>
    public sealed class ResultadoDePruebaDeGeneracion
    {
        /// <summary><c>true</c>: la generación de prueba produjo un libro.</summary>
        public bool Exitosa { get; }

        /// <summary>La falla; <c>null</c> si <see cref="Exitosa"/>.</summary>
        public FallaDeLaGeneracionDePrueba? Falla { get; }

        /// <summary>El texto del generador para la falla (nunca datos del cliente: la generación de prueba usa datos sintéticos); vacío si <see cref="Exitosa"/> o si la falla no trae detalle.</summary>
        public string Detalle { get; }

        private ResultadoDePruebaDeGeneracion(bool exitosa, FallaDeLaGeneracionDePrueba? falla, string detalle)
        {
            Exitosa = exitosa;
            Falla = falla;
            Detalle = detalle ?? string.Empty;
        }

        /// <summary>La generación de prueba funcionó.</summary>
        public static ResultadoDePruebaDeGeneracion Exito()
        {
            return new ResultadoDePruebaDeGeneracion(true, null, null);
        }

        /// <summary>El archivo no es un libro utilizable.</summary>
        public static ResultadoDePruebaDeGeneracion DePlantillaInvalida(string detalle)
        {
            return new ResultadoDePruebaDeGeneracion(false, FallaDeLaGeneracionDePrueba.PlantillaInvalida, detalle);
        }

        /// <summary>La plantilla y el mapeo no producen un formato.</summary>
        public static ResultadoDePruebaDeGeneracion DeFormatoInvalido(string detalle)
        {
            return new ResultadoDePruebaDeGeneracion(false, FallaDeLaGeneracionDePrueba.FormatoInvalido, detalle);
        }
    }

    /// <summary>
    /// La generación de prueba de Publicar (DC-19, 2.8): rellena la plantilla con datos sintéticos para atrapar una hoja inexistente antes de que un Vigente falle con un cliente real.
    /// Lo implementa <c>Formato/PruebaDeGeneracionOpenXml</c> sobre <c>GeneradorOpenXml</c>, <c>ClavesDelGenerador.DeFila</c> y <c>DatosDePrueba.Crear(capacidad)</c>. El dominio no conoce
    /// <c>Formato</c>: este puerto convierte las excepciones del generador en un resultado neutro.
    /// </summary>
    public interface IPruebaDeGeneracion
    {
        /// <summary>SHA-256 del contenido en hexadecimal minúscula (64 caracteres), igual a <c>IGeneradorDeFormato.CalcularHuella</c>.</summary>
        string CalcularHuella(byte[] contenido);

        /// <summary>
        /// Genera un libro de prueba con <paramref name="archivo"/>, <paramref name="huella"/> (la recién calculada), el <paramref name="mapeo"/> convertido con <c>ClavesDelGenerador.DeFila</c> y
        /// <c>DatosDePrueba.Crear(capacidad)</c>. No escribe nada ni guarda el libro. <c>PlantillaInvalidaException</c> ⇒ <see cref="FallaDeLaGeneracionDePrueba.PlantillaInvalida"/>;
        /// <c>MapeoInvalidoException</c> y <c>DatosInvalidosException</c> ⇒ <see cref="FallaDeLaGeneracionDePrueba.FormatoInvalido"/> con el texto de la excepción. Cualquier otra
        /// excepción (incluida <c>HuellaNoCoincideException</c>, que con la huella recién calculada sería un defecto) escapa.
        /// </summary>
        ResultadoDePruebaDeGeneracion Probar(byte[] archivo, string huella, IReadOnlyList<FilaDeMapeo> mapeo, int capacidad);
    }
}
