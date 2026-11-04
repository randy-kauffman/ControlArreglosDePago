using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Arreglo
{
    /// <summary>
    /// Los números de arreglo ya usados por un crédito (diseno/03 2.0.5 y 2.6, gesto Formalizar). Lo implementa <c>Datos/ConsecutivosDataverse</c> sobre
    /// <c>IArreglos.NumerosDelCredito</c> con el servicio de SYSTEM: la consulta ve TODOS los arreglos del crédito, en cualquier estado, y el llamador de un
    /// <c>Update</c> puede no ver los de otros oficiales. La regla lo llama como máximo UNA vez por evaluación y solo en el gesto Formalizar.
    /// </summary>
    public interface IConsecutivos
    {
        /// <summary>Los <c>sanic_numeroarreglo</c> no nulos de todos los arreglos del crédito (<c>producto</c>, <c>numeroProducto</c> ya normalizado). Sin orden.</summary>
        IReadOnlyList<int> NumerosDelCredito(ProductoCrediticio producto, string numeroProducto);
    }

    /// <summary>
    /// Pertenencia a la jefatura (0.3). Lo implementa <see cref="JefaturaDeLaIdentidad"/> sobre <see cref="IIdentidadDeEjecucion.EsJefatura"/>, que consulta una vez por
    /// ejecución. La regla lo llama como máximo UNA vez por evaluación y solo en los gestos Reabrir (PA-ARR-44).
    /// </summary>
    public interface IJefatura
    {
        /// <summary>¿Quien escribe figura en el team de jefatura?</summary>
        bool EsJefatura();
    }

    /// <summary>
    /// Las dos lecturas con SYSTEM que la edición del arreglo necesita solo a veces (diseno/03 2.6, paso 4, puntos 4 y 6): se piden cuando la regla ya vio que hacen falta, para
    /// que un guardado sin cambios en esas columnas no consulte nada (0.3, camino barato). Lo implementa <c>Datos/LecturasDelArregloDataverse</c>. Una falla de lectura escapa.
    /// </summary>
    public interface ILecturasDelArreglo
    {
        /// <summary><c>sanic_capacidad</c> de la versión de plantilla, o <c>null</c> si la versión no existe. Una consulta por id con solo esa columna.</summary>
        int? CapacidadDeLaVersion(Guid versionId);

        /// <summary>¿Alguna cuota del arreglo tiene <c>sanic_fechapago</c>? (DC-21, <c>ICuotas.HayPagos</c>.)</summary>
        bool TienePagos(Guid arregloId);
    }

    /// <summary>
    /// Lo que la regla del documento firmado necesita saber del arreglo (diseno/03 2.6, "Documento firmado"): el estado, si está congelado, si tiene archivo y cómo se llama.
    /// <see cref="NombreDelArchivo"/> es <c>sanic_documentofirmado_name</c> (la plataforma lo completa al terminar la subida; puede estar vacío mientras la subida no cerró).
    /// </summary>
    public sealed class FichaDelDocumentoFirmado
    {
        /// <summary>El estado del arreglo; <c>null</c> si la fila trae un par statecode/statuscode que no existe (falla cerrado).</summary>
        public EstadoDeArreglo? Estado { get; }

        /// <summary>¿<c>sanic_fechacongelamiento</c> tiene valor?</summary>
        public bool Congelado { get; }

        /// <summary>¿La columna <c>sanic_documentofirmado</c> tiene un archivo?</summary>
        public bool TieneArchivo { get; }

        /// <summary>El nombre del archivo, o <c>null</c> si no hay.</summary>
        public string NombreDelArchivo { get; }

        /// <summary><c>sanic_fechacongelamiento</c>, o <c>null</c> si no hay.</summary>
        public DateTime? FechaDeCongelamiento { get; }

        /// <summary><c>sanic_fechareapertura</c> (la escribe Reabrir y nada la vacía), o <c>null</c> si el arreglo nunca se reabrió.</summary>
        public DateTime? FechaDeReapertura { get; }

        /// <summary>
        /// ¿Reabierto y vuelto a congelar? El congelamiento es POSTERIOR a la última reapertura: Reabrir vacía el congelamiento y el siguiente Generar formato lo escribe de nuevo, así que
        /// <c>FechaDeCongelamiento &gt; FechaDeReapertura</c> distingue un arreglo reabierto y re-congelado (hay un papel nuevo que firmar) de uno recién formalizado (nunca reabierto, o
        /// reabierto y todavía sin congelar). Sin las dos fechas es <c>false</c>.
        /// </summary>
        public bool ReabiertoYRecongelado
        {
            get { return FechaDeCongelamiento.HasValue && FechaDeReapertura.HasValue && FechaDeCongelamiento.Value > FechaDeReapertura.Value; }
        }

        public FichaDelDocumentoFirmado(EstadoDeArreglo? estado, bool congelado, bool tieneArchivo, string nombreDelArchivo,
            DateTime? fechaDeCongelamiento = null, DateTime? fechaDeReapertura = null)
        {
            Estado = estado;
            Congelado = congelado;
            TieneArchivo = tieneArchivo;
            NombreDelArchivo = nombreDelArchivo;
            FechaDeCongelamiento = fechaDeCongelamiento;
            FechaDeReapertura = fechaDeReapertura;
        }
    }

    /// <summary>
    /// La lectura con SYSTEM que <see cref="ReglasDeDocumentoFirmado"/> pide solo cuando ya vio que hay algo que decidir y quien escribe no es el sistema (camino barato, 0.3). Lo implementa
    /// <c>Datos/LecturasDelDocumentoDataverse</c>. Una falla de lectura escapa.
    /// </summary>
    public interface ILecturasDelDocumento
    {
        /// <summary>UNA consulta por id con las columnas de la ficha. <c>null</c> si el arreglo no existe.</summary>
        FichaDelDocumentoFirmado FichaDelArreglo(Guid arregloId);
    }

    /// <summary>La identidad de ejecución vista como <see cref="IJefatura"/>: delega en <see cref="IIdentidadDeEjecucion.EsJefatura"/> (no memoiza: la identidad ya lo hace).</summary>
    public sealed class JefaturaDeLaIdentidad : IJefatura
    {
        private readonly IIdentidadDeEjecucion _identidad;

        /// <summary><c>null</c> ⇒ <see cref="ArgumentNullException"/>. No consulta nada al construirse.</summary>
        public JefaturaDeLaIdentidad(IIdentidadDeEjecucion identidad)
        {
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            _identidad = identidad;
        }

        /// <inheritdoc />
        public bool EsJefatura()
        {
            return _identidad.EsJefatura();
        }
    }
}
