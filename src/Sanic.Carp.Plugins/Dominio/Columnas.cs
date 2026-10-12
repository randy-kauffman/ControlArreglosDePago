using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
            _soloSistema = new HashSet<string>(soloSistema, StringComparer.Ordinal);
            _deGesto = new HashSet<string>(deGesto, StringComparer.Ordinal);
            _datosDelAcuerdo = new HashSet<string>(datosDelAcuerdo, StringComparer.Ordinal);
            _libres = new HashSet<string>(libres, StringComparer.Ordinal);
            _aparte = new HashSet<string>(aparte, StringComparer.Ordinal);
        }

        private readonly HashSet<string> _soloSistema;
        private readonly HashSet<string> _deGesto;
        private readonly HashSet<string> _datosDelAcuerdo;
        private readonly HashSet<string> _libres;
        private readonly HashSet<string> _aparte;

        /// <summary>La categoría de una columna (nombre lógico, comparación ordinal y sensible a mayúsculas), o <c>null</c> si la tabla no la tiene.</summary>
        public CategoriaDeColumna? CategoriaDe(string columna)
        {
            if (columna == null) return null;
            // Ordinal y sensible a mayúsculas: los nombres lógicos son minúscula (HashSet con comparador ordinal).
            if (_soloSistema.Contains(columna)) return CategoriaDeColumna.SoloSistema;
            if (_deGesto.Contains(columna)) return CategoriaDeColumna.DeGesto;
            if (_datosDelAcuerdo.Contains(columna)) return CategoriaDeColumna.DatosDelAcuerdo;
            if (_libres.Contains(columna)) return CategoriaDeColumna.Libre;
            if (_aparte.Contains(columna)) return CategoriaDeColumna.Aparte;
            return null;
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
            get { return _nombresDeTablas; }
        }

        /// <summary>Las columnas de una de las seis tablas; otra tabla lanza <see cref="ArgumentException"/>.</summary>
        public static ColumnasDeTabla De(string tabla)
        {
            if (tabla == null) throw new ArgumentNullException(nameof(tabla));

            ColumnasDeTabla columnas;
            if (!_porTabla.TryGetValue(tabla, out columnas))
            {
                throw new ArgumentException("La tabla '" + tabla + "' no tiene categorías de columnas (diseno/03 2.0.8).", nameof(tabla));
            }
            return columnas;
        }

        private static readonly IReadOnlyList<string> _nombresDeTablas = new ReadOnlyCollection<string>(new[]
        {
            TablaConversacion, TablaArreglo, TablaCuota, TablaVersionDePlantilla, TablaMapeoDeCelda, TablaEmail
        });

        private static ReadOnlyCollection<string> Lista(params string[] columnas)
        {
            return new ReadOnlyCollection<string>(columnas);
        }

        // La tabla de diseno/03 2.0.8, columna por columna. Una prueba la contrasta con el diccionario de datos (PA-COL-01 y PA-COL-02).
        private static readonly Dictionary<string, ColumnasDeTabla> _porTabla = new Dictionary<string, ColumnasDeTabla>(StringComparer.Ordinal)
        {
            {
                TablaConversacion, new ColumnasDeTabla(TablaConversacion,
                    soloSistema: Lista("sanic_huellamessageid", "sanic_fechaprimercorreo", "sanic_asunto", "sanic_remitente", "sanic_sinrespuestadesde",
                        "sanic_correonuevoenfinalizada", "sanic_fechafinalizacion", "sanic_tiempoprimerarespuesta", "sanic_tiempototal",
                        "sanic_cantidadrespuestas", "sanic_unidaaid", "sanic_finalizadoporid", "sanic_nombre"),
                    deGesto: Lista("sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta"),
                    datosDelAcuerdo: Lista(),
                    libres: Lista("sanic_clienteid"),
                    aparte: Lista("sanic_oficialasignadoid", "ownerid", "statecode", "statuscode", "sanic_antiguedad"))
            },
            {
                TablaArreglo, new ColumnasDeTabla(TablaArreglo,
                    soloSistema: Lista("sanic_numeroarreglo", "sanic_numeroprovisional", "sanic_saldoremanente", "sanic_totalpagar",
                        "sanic_fechacongelamiento", "sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo",
                        "sanic_instantaneacelular", "sanic_fechaformalizado", "sanic_formalizadoporid", "sanic_fechadescarte",
                        "sanic_descartadoporid", "sanic_fechaanulacion", "sanic_anuladoporid", "sanic_fechareapertura", "sanic_reabiertoporid",
                        "sanic_cantidadreaperturas", "sanic_fechatermino", "sanic_versionplantillaid", "sanic_nombre"),
                    deGesto: Lista("sanic_motivodescarte", "sanic_motivoanulacion", "sanic_motivoreapertura"),
                    datosDelAcuerdo: Lista("sanic_producto", "sanic_numeroproducto", "sanic_tipo", "sanic_moneda", "sanic_canalorigen",
                        "sanic_saldoinicial", "sanic_montoabonado", "sanic_plazo", "sanic_cuotamensual", "sanic_fechainicio", "sanic_fechafin",
                        "sanic_cuentadebitar", "sanic_fechaformalizacion", "sanic_comentarios", "sanic_clienteid", "sanic_oficialid"),
                    libres: Lista("sanic_conversacionid"),
                    aparte: Lista("ownerid", "statecode", "statuscode"))
            },
            {
                TablaCuota, new ColumnasDeTabla(TablaCuota,
                    soloSistema: Lista("sanic_nombre", "sanic_numero", "sanic_fechavencimiento", "sanic_monto", "sanic_arregloid"),
                    deGesto: Lista(),
                    datosDelAcuerdo: Lista(),
                    libres: Lista("sanic_fechapago", "sanic_montopagado"),
                    aparte: Lista("statecode", "statuscode"))
            },
            {
                TablaVersionDePlantilla, new ColumnasDeTabla(TablaVersionDePlantilla,
                    soloSistema: Lista("sanic_huella", "sanic_fechaaprobacion", "sanic_aprobadoporid"),
                    deGesto: Lista("sanic_estado"),
                    datosDelAcuerdo: Lista(),
                    libres: Lista("sanic_nombre", "sanic_archivo", "sanic_capacidad"),
                    aparte: Lista("statecode", "statuscode"))
            },
            {
                TablaMapeoDeCelda, new ColumnasDeTabla(TablaMapeoDeCelda,
                    soloSistema: Lista("sanic_nombre", "sanic_versionplantillaid"),
                    deGesto: Lista(),
                    datosDelAcuerdo: Lista(),
                    libres: Lista("sanic_hoja", "sanic_celda", "sanic_tipo", "sanic_campo", "sanic_filasmaximas"),
                    aparte: Lista("statecode", "statuscode"))
            },
            {
                TablaEmail, new ColumnasDeTabla(TablaEmail,
                    soloSistema: Lista("sanic_tiemporespuesta", "sanic_respondidoporid"),
                    deGesto: Lista(),
                    datosDelAcuerdo: Lista(),
                    libres: Lista(),
                    aparte: Lista())
            }
        };
    }
}
