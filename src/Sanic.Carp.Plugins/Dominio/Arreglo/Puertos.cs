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
