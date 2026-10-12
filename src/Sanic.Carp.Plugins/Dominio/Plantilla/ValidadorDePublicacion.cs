using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Dominio.Plantilla
{
    /// <summary>
    /// Una fila de la tabla Mapeo de celda tal como la necesita la validación de publicación (diseno/03 2.0.7). Es un tipo del dominio
    /// y no el <c>MapeoCelda</c> del generador (<c>Formato/</c>, aún no portado, 4.3): el dominio no puede depender de Dataverse y la
    /// dependencia <c>Dominio → Formato</c> de 4.1 se pospone hasta que ese código exista. Se usan los choices de <c>Choices.g.cs</c>.
    /// </summary>
    public sealed class FilaDeMapeo
    {
        /// <summary>Nombre exacto de la hoja, tal cual (puede empezar con un espacio, spike 1 hallazgo 1). No se recorta.</summary>
        public string Hoja { get; }

        /// <summary>Celda en notación A1 (la de la primera cuota, para un bloque).</summary>
        public string Celda { get; }

        public TipoDeMapeo Tipo { get; }
        public CampoMapeable Campo { get; }

        /// <summary><c>sanic_filasmaximas</c>; solo lo llevan las columnas de bloque.</summary>
        public int? FilasMaximas { get; }

        public FilaDeMapeo(string hoja, string celda, TipoDeMapeo tipo, CampoMapeable campo, int? filasMaximas = null)
        {
            Hoja = hoja;
            Celda = celda;
            Tipo = tipo;
            Campo = campo;
            FilasMaximas = filasMaximas;
        }
    }

    /// <summary>Validación de publicación de una versión de plantilla (diseno/03 2.0.7; D-15, def. ítem 15).</summary>
    public static class ValidadorDePublicacion
    {
        /// <summary>
        /// Devuelve TODOS los errores, no solo el primero; lista vacía si el mapeo se puede publicar.
        /// <list type="number">
        /// <item>Mapeo nulo o vacío ⇒ <c>CARP-VER-007</c>.</item>
        /// <item>Reglas del generador (<c>ValidarMapeo</c> del spike 1): celda A1 bien formada (A1 a XFD1048576), sin dos campos en la
        /// misma celda de la misma hoja, bloque de cuotas con las cinco columnas, campo coherente con el tipo ⇒ <c>CARP-VER-007</c>,
        /// un error por problema.</item>
        /// <item>Tabla de compatibilidad de dicc. §3.1 (columna 4): las 19 primeras opciones y <c>Moneda</c> solo con ValorSimple;
        /// <c>Cuota: …</c> (5) solo con ColumnaDeBloqueDeCuotas; <c>Total: …</c> (2) solo con TotalCalculado ⇒ <c>CARP-VER-007</c>.</item>
        /// <item>Las filas máximas de las columnas del bloque no coinciden con <paramref name="capacidad"/> ⇒ UN solo error
        /// <c>CARP-VER-008</c> (decisión del arquitecto: el texto no nombra la columna).</item>
        /// </list>
        /// Cada error de la regla 1 a 3 es <c>CodigosDeError.VER_007</c> con el problema concreto en <c>{errores}</c>; el de la regla 4,
        /// <c>CodigosDeError.VER_008</c> con la capacidad.
        /// </summary>
        public static IReadOnlyList<ErrorDeNegocio> Validar(int capacidad, IReadOnlyList<FilaDeMapeo> filasDeMapeo)
        {
            throw new NotImplementedException();
        }
    }
}
