using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>
    /// Nombres lógicos de las tablas del área (diseno/03 4.1 y 5, sublote 5a). Tipo neutro: no conoce el SDK. Los steps de <c>Steps/</c> declaran
    /// con estas constantes en qué tablas se registran (la base de los steps rechaza un registro en otra tabla, 4.1) y ninguno escribe el
    /// literal. Las seis que ya tiene <see cref="Columnas"/> se toman de allí para que haya una sola fuente.
    /// </summary>
    public static class TablasDelArea
    {
        public const string Cliente = "sanic_carp_tbl_cliente";
        public const string Conversacion = Columnas.TablaConversacion;
        public const string Arreglo = Columnas.TablaArreglo;
        public const string Cuota = Columnas.TablaCuota;
        public const string VersionDePlantilla = Columnas.TablaVersionDePlantilla;
        public const string MapeoDeCelda = Columnas.TablaMapeoDeCelda;
        public const string GeneracionDeFormato = "sanic_carp_tbl_generacionformato";

        /// <summary>La tabla estándar <c>email</c> (no es propia del área, pero tiene steps: 2.9 a 2.11).</summary>
        public const string Correo = Columnas.TablaEmail;

        /// <summary>Las siete tablas PROPIAS (propietario, 2.1), en el orden de S-01 a S-07: cliente, conversación, arreglo, cuota, versión, mapeo, generación.</summary>
        public static IReadOnlyList<string> LasSiete { get; } = new ReadOnlyCollection<string>(new[]
        {
            Cliente, Conversacion, Arreglo, Cuota, VersionDePlantilla, MapeoDeCelda, GeneracionDeFormato
        });
    }
}
