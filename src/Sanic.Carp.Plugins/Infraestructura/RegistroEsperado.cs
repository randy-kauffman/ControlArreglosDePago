using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// Etapas en las que se registra un step (diseno/03 5, DC-27: solo PreOperation y PostOperation). Los números son los de la plataforma
    /// (<c>IPluginExecutionContext.Stage</c>). <see cref="PreValidation"/> existe solo para poder NOMBRAR la etapa 10 de un contexto mal
    /// registrado en un mensaje de error: <see cref="RegistroEsperado"/> la rechaza.
    /// </summary>
    internal enum EtapaDelPipeline
    {
        PreValidation = 10,
        PreOperation = 20,
        PostOperation = 40
    }

    /// <summary>
    /// UN registro que un step espera tener en la plataforma: mensaje, etapa, modo, tablas, orden, filtro de atributos y pre-imagen
    /// (diseno/03 4.1, "Decisiones del arquitecto, 2026-10-19 (sublote 5a)"). Es a la vez la fuente de la verificación de
    /// <see cref="BaseDeStep"/> (un registro equivocado falla con un mensaje claro) y de lo que el playbook debe registrar; una prueba lo
    /// contrasta con la tabla de la sección 5. Una clase de step con dos registros (<c>ClienteStep</c>: Create y Update) declara dos.
    /// <para>
    /// <b>FIRMA DEL ARQUITECTO.</b> El constructor lanza
    /// <see cref="System.ArgumentException"/> (nombre del parámetro) ante lo que DC-27 y la sección 5 no admiten: mensaje que no sea
    /// <c>Create</c>, <c>Update</c> o <c>Delete</c> (ordinal, sensible a mayúsculas); etapa <see cref="EtapaDelPipeline.PreValidation"/> o
    /// un número que no sea de la enumeración; asíncrono fuera de PostOperation; orden menor que 1; tablas vacías, con un nombre vacío o
    /// repetido; filtro no vacío fuera de <c>Update</c>, con un nombre vacío o repetido, o con el id primario de alguna de las tablas
    /// (<c>&lt;tabla&gt;id</c>, <c>activityid</c> en <c>email</c>); pre-imagen con columnas fuera de <c>Update</c> y <c>Delete</c>, o con un
    /// nombre vacío o repetido. Un filtro o una pre-imagen nulos equivalen a vacíos.
    /// </para>
    /// </summary>
    internal sealed class RegistroEsperado
    {
        public RegistroEsperado(string mensaje, EtapaDelPipeline etapa, bool asincrono, int orden,
            IEnumerable<string> tablas, IEnumerable<string> filtro, IEnumerable<string> columnasDePreImagen)
        {
            // Lo que DC-27 y la seccion 5 no admiten se rechaza aqui, al declarar el step: un registro mal escrito no debe llegar al playbook (4.1, punto 5).
            var esCreate = string.Equals(mensaje, "Create", StringComparison.Ordinal);
            var esUpdate = string.Equals(mensaje, "Update", StringComparison.Ordinal);
            var esDelete = string.Equals(mensaje, "Delete", StringComparison.Ordinal);
            if (!esCreate && !esUpdate && !esDelete)
                throw new ArgumentException("El mensaje debe ser Create, Update o Delete.", nameof(mensaje));
            if (etapa != EtapaDelPipeline.PreOperation && etapa != EtapaDelPipeline.PostOperation)
                throw new ArgumentException("Solo se registra en PreOperation o PostOperation (DC-27).", nameof(etapa));
            if (asincrono && etapa != EtapaDelPipeline.PostOperation)
                throw new ArgumentException("Un step asincrono solo se registra en PostOperation (DC-27).", nameof(asincrono));
            if (orden < 1)
                throw new ArgumentException("El orden de ejecucion empieza en 1.", nameof(orden));

            var copiaDeTablas = Copiar(tablas);
            if (copiaDeTablas.Count == 0)
                throw new ArgumentException("Hace falta al menos una tabla.", nameof(tablas));
            ValidarNombres(copiaDeTablas, nameof(tablas));

            var copiaDelFiltro = Copiar(filtro);
            if (copiaDelFiltro.Count > 0 && !esUpdate)
                throw new ArgumentException("El filtro de atributos solo existe en Update (DC-27).", nameof(filtro));
            ValidarNombres(copiaDelFiltro, nameof(filtro));
            foreach (var tabla in copiaDeTablas)
            {
                var id = AyudaDeConsultas.ColumnaDeId(tabla);
                if (ContieneOrdinal(copiaDelFiltro, id))
                    throw new ArgumentException("El filtro no puede nombrar el id primario (DC-27).", nameof(filtro));
            }

            var copiaDeLaImagen = Copiar(columnasDePreImagen);
            if (copiaDeLaImagen.Count > 0 && esCreate)
                throw new ArgumentException("La pre-imagen solo existe en Update y Delete (DC-27).", nameof(columnasDePreImagen));
            ValidarNombres(copiaDeLaImagen, nameof(columnasDePreImagen));

            Mensaje = mensaje;
            Etapa = etapa;
            Asincrono = asincrono;
            Orden = orden;
            Tablas = copiaDeTablas;
            Filtro = copiaDelFiltro;
            ColumnasDePreImagen = copiaDeLaImagen;
        }

        private static void ValidarNombres(IReadOnlyList<string> nombres, string parametro)
        {
            var vistos = new HashSet<string>(StringComparer.Ordinal);
            foreach (var nombre in nombres)
            {
                if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("Hay un nombre vacio.", parametro);
                if (!vistos.Add(nombre)) throw new ArgumentException("Hay un nombre repetido.", parametro);
            }
        }

        private static bool ContieneOrdinal(IReadOnlyList<string> nombres, string buscado)
        {
            foreach (var nombre in nombres)
                if (string.Equals(nombre, buscado, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary><c>Create</c>, <c>Update</c> o <c>Delete</c>.</summary>
        public string Mensaje { get; }

        public EtapaDelPipeline Etapa { get; }

        /// <summary>Solo PostOperation (DC-27).</summary>
        public bool Asincrono { get; }

        /// <summary>El orden de ejecución de la sección 5 (columna "Orden").</summary>
        public int Orden { get; }

        /// <summary>Las tablas en que se registra con este mismo mensaje y etapa. El step decide por <c>PrimaryEntityName</c> y solo acepta estas.</summary>
        public IReadOnlyList<string> Tablas { get; }

        /// <summary>Filtro de atributos (solo <c>Update</c>); vacío = sin filtro.</summary>
        public IReadOnlyList<string> Filtro { get; }

        /// <summary>Columnas de la pre-imagen <c>PreImagen</c> (solo <c>Update</c> y <c>Delete</c>); vacío = sin pre-imagen.</summary>
        public IReadOnlyList<string> ColumnasDePreImagen { get; }

        /// <summary>El registro exige que el contexto traiga la pre-imagen <c>PreImagen</c>.</summary>
        public bool LlevaPreImagen { get { return ColumnasDePreImagen.Count > 0; } }

        private static IReadOnlyList<string> Copiar(IEnumerable<string> valores)
        {
            return new ReadOnlyCollection<string>(valores == null ? new List<string>() : new List<string>(valores));
        }
    }
}
