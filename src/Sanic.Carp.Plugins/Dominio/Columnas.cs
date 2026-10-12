using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>Categorías de columna de diseno/03 2.0.8 (DC-26).</summary>
    public enum CategoriaDeColumna
    {
        /// <summary>Solo las escribe el sistema (SYSTEM); una persona que las cambie recibe el error de la tabla.</summary>
        SoloSistema,

        /// <summary>Se escriben junto con un gesto (Finalizar, Descartar, Anular, Reabrir, Publicar) y solo con él.</summary>
        DeGesto,

        /// <summary>Datos del acuerdo: se bloquean al congelar el arreglo (la instantánea y el formato los imprimen).</summary>
        DatosDelAcuerdo,

        /// <summary>Las edita libremente la persona (según el estado de la fila).</summary>
        Libre,

        /// <summary>Columnas que el diseño rige en otro lugar: asignación (2.4), propietario (2.1), estado (3), fórmulas no escribibles.</summary>
        Aparte
    }

    /// <summary>Las columnas de una tabla repartidas por categoría. Cada columna está en exactamente una categoría.</summary>
    public sealed class ColumnasDeTabla
    {
        public string Tabla { get; }
        public IReadOnlyCollection<string> SoloSistema { get; }
        public IReadOnlyCollection<string> DeGesto { get; }
        public IReadOnlyCollection<string> DatosDelAcuerdo { get; }
        public IReadOnlyCollection<string> Libres { get; }
        public IReadOnlyCollection<string> Aparte { get; }

        internal ColumnasDeTabla(string tabla, IReadOnlyCollection<string> soloSistema, IReadOnlyCollection<string> deGesto,
            IReadOnlyCollection<string> datosDelAcuerdo, IReadOnlyCollection<string> libres, IReadOnlyCollection<string> aparte)
        {
            Tabla = tabla;
            SoloSistema = soloSistema;
            DeGesto = deGesto;
            DatosDelAcuerdo = datosDelAcuerdo;
            Libres = libres;
            Aparte = aparte;
        }

        /// <summary>La categoría de una columna (nombre lógico, comparación ordinal y sensible a mayúsculas), o <c>null</c> si la tabla no la tiene.</summary>
        public CategoriaDeColumna? CategoriaDe(string columna)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Columnas por categoría de las seis tablas con reglas de columna (diseno/03 2.0.8), como datos. Una prueba
    /// (<c>CatalogoDeColumnasAceptacion</c>, PA-COL-01 y PA-COL-02) las contrasta con <c>diseno/02-diccionario-datos.md</c>, para que una
    /// columna nueva no quede fuera de la regla en silencio (D-63 de M-PPP).
    /// </summary>
    public static class Columnas
    {
        public const string TablaConversacion = "sanic_carp_tbl_conversacion";
        public const string TablaArreglo = "sanic_carp_tbl_arreglo";
        public const string TablaCuota = "sanic_carp_tbl_cuota";
        public const string TablaVersionDePlantilla = "sanic_carp_tbl_versionplantilla";
        public const string TablaMapeoDeCelda = "sanic_carp_tbl_mapeocelda";
        public const string TablaEmail = "email";

        /// <summary>Las seis tablas de este catálogo.</summary>
        public static IReadOnlyList<string> Tablas
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>Las columnas de una de las seis tablas; otra tabla lanza <see cref="ArgumentException"/>.</summary>
        public static ColumnasDeTabla De(string tabla)
        {
            throw new NotImplementedException();
        }
    }
}
