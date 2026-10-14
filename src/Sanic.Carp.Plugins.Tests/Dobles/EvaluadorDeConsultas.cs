using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// Evalúa una <see cref="QueryExpression"/> contra las filas en memoria (diseno/03 4.5, "Condiciones" y "Paginación"). Todo lo que no
    /// simula, lo dice con <see cref="NotSupportedException"/> y el nombre de lo que falta: un doble a medias deja pasar en el puesto lo
    /// que falla en Dev (decisión del arquitecto, 2026-10-14).
    /// <para>
    /// La lógica de las condiciones es de tres valores (cumple, no cumple, desconocido), como SQL: <c>NotEqual</c> sobre una columna
    /// AUSENTE es desconocido, porque no hay una respuesta verificada de la plataforma (¿incluye los nulos?). Si una fila termina
    /// desconocida, la consulta lanza <see cref="InvalidOperationException"/> en vez de adivinar (D-59); si otra condición ya la descarta
    /// (<c>And</c> con un "no cumple"), la fila no importa y no se lanza nada.
    /// </para>
    /// </summary>
    internal static class EvaluadorDeConsultas
    {
        private enum Resultado { No, Si, Desconocido }

        private static readonly HashSet<ConditionOperator> Soportados = new HashSet<ConditionOperator>
        {
            ConditionOperator.Equal, ConditionOperator.NotEqual, ConditionOperator.In, ConditionOperator.Null, ConditionOperator.NotNull,
            ConditionOperator.GreaterThan, ConditionOperator.GreaterEqual, ConditionOperator.LessThan, ConditionOperator.LessEqual
        };

        // ---- Validación y registro ----

        /// <summary>Lanza <see cref="NotSupportedException"/> si la consulta usa algo que el doble no simula.</summary>
        public static void Validar(QueryExpression consulta)
        {
            if (consulta.LinkEntities != null && consulta.LinkEntities.Count > 0)
                throw new NotSupportedException("El doble no simula LinkEntities (joins): una consulta con join pasa en el puesto y falla en el entorno (diseno/03 4.1, \"Regla de datos\"). Se hacen dos consultas.");
            if (consulta.Criteria != null) ValidarFiltro(consulta.Criteria);
        }

        private static void ValidarFiltro(FilterExpression filtro)
        {
            foreach (var condicion in filtro.Conditions)
            {
                if (!Soportados.Contains(condicion.Operator))
                    throw new NotSupportedException("El doble no simula el operador de condición " + condicion.Operator + " (columna '" + condicion.AttributeName + "').");
                if (condicion.EntityName != null && condicion.EntityName.Length > 0)
                    throw new NotSupportedException("El doble no simula condiciones sobre una entidad enlazada (LinkEntities).");
            }
            foreach (var hijo in filtro.Filters) ValidarFiltro(hijo);
        }

        /// <summary>Las condiciones aplanadas en orden (las del filtro y luego las de cada subfiltro), con los valores aplanados.</summary>
        public static List<CondicionRegistrada> Aplanar(FilterExpression filtro)
        {
            var resultado = new List<CondicionRegistrada>();
            if (filtro != null) Aplanar(filtro, resultado);
            return resultado;
        }

        private static void Aplanar(FilterExpression filtro, List<CondicionRegistrada> resultado)
        {
            foreach (var condicion in filtro.Conditions)
                resultado.Add(new CondicionRegistrada(condicion.AttributeName, condicion.Operator, ValoresDe(condicion)));
            foreach (var hijo in filtro.Filters) Aplanar(hijo, resultado);
        }

        private static IReadOnlyList<object> ValoresDe(ConditionExpression condicion)
        {
            var valores = new List<object>();
            if (condicion.Values == null) return valores;
            foreach (var valor in condicion.Values)
            {
                var coleccion = valor as IEnumerable;
                if (coleccion != null && !(valor is string) && !(valor is byte[]))
                {
                    foreach (var interno in coleccion) valores.Add(interno);
                }
                else
                {
                    valores.Add(valor);
                }
            }
            return valores;
        }

        // ---- Filtro ----

        /// <summary>Las filas que cumplen el criterio. <paramref name="oculta"/> dice qué columnas valen como nulas para quien consulta (DC-22).</summary>
        public static List<Entity> Filtrar(IEnumerable<Entity> filas, FilterExpression criterio, Func<string, bool> oculta)
        {
            var resultado = new List<Entity>();
            foreach (var fila in filas)
            {
                var cumple = criterio == null ? Resultado.Si : EvaluarFiltro(criterio, fila, oculta);
                if (cumple == Resultado.Si)
                {
                    resultado.Add(fila);
                }
                else if (cumple == Resultado.Desconocido)
                {
                    var columnas = new List<string>();
                    ColumnasAusentesConNotEqual(criterio, fila, oculta, columnas);
                    throw new InvalidOperationException("La consulta usa NotEqual sobre la columna AUSENTE " + string.Join(", ", columnas.Distinct(StringComparer.Ordinal))
                        + " en la fila " + fila.LogicalName + " " + fila.Id
                        + ": la plataforma no tiene una respuesta verificada (SQL excluye los nulos, Dataverse puede incluirlos) y el doble no adivina. "
                        + "Escriba la consulta de modo que no dependa de eso (por ejemplo, NotEqual junto con NotNull o con una columna siempre llena).");
                }
            }
            return resultado;
        }

        private static void ColumnasAusentesConNotEqual(FilterExpression filtro, Entity fila, Func<string, bool> oculta, List<string> columnas)
        {
            foreach (var condicion in filtro.Conditions)
                if (condicion.Operator == ConditionOperator.NotEqual && ValorDe(fila, condicion.AttributeName, oculta) == null) columnas.Add(condicion.AttributeName);
            foreach (var hijo in filtro.Filters) ColumnasAusentesConNotEqual(hijo, fila, oculta, columnas);
        }

        private static Resultado EvaluarFiltro(FilterExpression filtro, Entity fila, Func<string, bool> oculta)
        {
            var resultados = new List<Resultado>();
            foreach (var condicion in filtro.Conditions) resultados.Add(EvaluarCondicion(condicion, fila, oculta));
            foreach (var hijo in filtro.Filters) resultados.Add(EvaluarFiltro(hijo, fila, oculta));
            if (resultados.Count == 0) return Resultado.Si;

            if (filtro.FilterOperator == LogicalOperator.Or)
            {
                if (resultados.Contains(Resultado.Si)) return Resultado.Si;
                return resultados.Contains(Resultado.Desconocido) ? Resultado.Desconocido : Resultado.No;
            }
            if (resultados.Contains(Resultado.No)) return Resultado.No;
            return resultados.Contains(Resultado.Desconocido) ? Resultado.Desconocido : Resultado.Si;
        }

        private static object ValorDe(Entity fila, string columna, Func<string, bool> oculta)
        {
            if (oculta(columna)) return null;
            object valor;
            return fila.Attributes.TryGetValue(columna, out valor) ? valor : null;
        }

        private static Resultado EvaluarCondicion(ConditionExpression condicion, Entity fila, Func<string, bool> oculta)
        {
            var guardado = ValorDe(fila, condicion.AttributeName, oculta);
            var ausente = guardado == null;
            switch (condicion.Operator)
            {
                case ConditionOperator.Null: return ausente ? Resultado.Si : Resultado.No;
                case ConditionOperator.NotNull: return ausente ? Resultado.No : Resultado.Si;
            }

            var valores = ValoresDe(condicion);
            if (ausente) return condicion.Operator == ConditionOperator.NotEqual ? Resultado.Desconocido : Resultado.No;

            var izquierda = ParaComparar(guardado);
            switch (condicion.Operator)
            {
                case ConditionOperator.In:
                    foreach (var valor in valores)
                        if (Cumple(izquierda, valor, condicion, c => c == 0)) return Resultado.Si;
                    return Resultado.No;
                case ConditionOperator.Equal: return Cumple(izquierda, Primero(valores, condicion), condicion, c => c == 0) ? Resultado.Si : Resultado.No;
                case ConditionOperator.NotEqual: return Cumple(izquierda, Primero(valores, condicion), condicion, c => c != 0) ? Resultado.Si : Resultado.No;
                case ConditionOperator.GreaterThan: return Cumple(izquierda, Primero(valores, condicion), condicion, c => c > 0) ? Resultado.Si : Resultado.No;
                case ConditionOperator.GreaterEqual: return Cumple(izquierda, Primero(valores, condicion), condicion, c => c >= 0) ? Resultado.Si : Resultado.No;
                case ConditionOperator.LessThan: return Cumple(izquierda, Primero(valores, condicion), condicion, c => c < 0) ? Resultado.Si : Resultado.No;
                case ConditionOperator.LessEqual: return Cumple(izquierda, Primero(valores, condicion), condicion, c => c <= 0) ? Resultado.Si : Resultado.No;
                default:
                    throw new NotSupportedException("El doble no simula el operador de condición " + condicion.Operator + ".");
            }
        }

        private static object Primero(IReadOnlyList<object> valores, ConditionExpression condicion)
        {
            if (valores.Count == 0)
                throw new InvalidOperationException("La condición " + condicion.Operator + " sobre '" + condicion.AttributeName + "' no trae ningún valor.");
            return valores[0];
        }

        private static bool Cumple(object izquierda, object valor, ConditionExpression condicion, Func<int, bool> prueba)
        {
            var derecha = ParaComparar(valor);
            if (derecha == null) return false;
            if (!Coercionar(izquierda, ref derecha))
            {
                throw new InvalidOperationException("La condición " + condicion.Operator + " sobre '" + condicion.AttributeName + "' compara un valor de tipo "
                    + derecha.GetType().Name + " con una columna de tipo " + izquierda.GetType().Name + "; la plataforma no lo hace sin convertir.");
            }
            return prueba(Comparar(izquierda, derecha));
        }

        // ---- Comparación de valores ----

        /// <summary>Lleva el valor a su forma comparable: choice ⇒ número, referencia ⇒ Guid, Money ⇒ decimal, enteros ⇒ decimal, fecha ⇒ UTC.</summary>
        public static object ParaComparar(object valor)
        {
            if (valor == null) return null;
            var opcion = valor as OptionSetValue;
            if (opcion != null) return (decimal)opcion.Value;
            var referencia = valor as EntityReference;
            if (referencia != null) return referencia.Id;
            var dinero = valor as Money;
            if (dinero != null) return dinero.Value;
            if (valor is int || valor is long || valor is short || valor is byte) return Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
            if (valor is double || valor is float) return Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
            if (valor is DateTime) return FormaDeLaPlataforma.AUtc((DateTime)valor);
            return valor;
        }

        private static bool Coercionar(object guardado, ref object condicion)
        {
            if (guardado.GetType() == condicion.GetType()) return true;
            var texto = condicion as string;
            if (texto != null)
            {
                if (guardado is Guid)
                {
                    Guid g;
                    if (!Guid.TryParse(texto, out g)) return false;
                    condicion = g;
                    return true;
                }
                if (guardado is decimal)
                {
                    decimal d;
                    if (!decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out d)) return false;
                    condicion = d;
                    return true;
                }
                if (guardado is bool)
                {
                    bool b;
                    if (!bool.TryParse(texto, out b)) return false;
                    condicion = b;
                    return true;
                }
                if (guardado is DateTime)
                {
                    DateTime f;
                    if (!DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out f)) return false;
                    condicion = FormaDeLaPlataforma.AUtc(f);
                    return true;
                }
                return false;
            }
            if (condicion is Guid && guardado is string)
            {
                condicion = ((Guid)condicion).ToString();
                return true;
            }
            return false;
        }

        /// <summary>Compara dos valores ya comparables y del mismo tipo. El texto no distingue mayúsculas (la colación de la base; NO verificado).</summary>
        public static int Comparar(object a, object b)
        {
            var texto = a as string;
            if (texto != null) return string.Compare(texto, (string)b, StringComparison.OrdinalIgnoreCase);
            if (a is decimal) return ((decimal)a).CompareTo((decimal)b);
            if (a is DateTime) return ((DateTime)a).Ticks.CompareTo(((DateTime)b).Ticks);
            if (a is bool) return ((bool)a).CompareTo((bool)b);
            if (a is Guid) return ((Guid)a).CompareTo((Guid)b);
            throw new NotSupportedException("El doble no sabe comparar valores de tipo " + a.GetType().Name + ".");
        }

        /// <summary>Igualdad de dos valores guardados (clave alterna): distinto tipo ⇒ distintos.</summary>
        public static bool Iguales(object a, object b)
        {
            var izquierda = ParaComparar(a);
            var derecha = ParaComparar(b);
            if (izquierda == null || derecha == null || izquierda.GetType() != derecha.GetType()) return false;
            return Comparar(izquierda, derecha) == 0;
        }

        // ---- Orden, ventana y cookie ----

        /// <summary>Ordena de forma estable: los empates conservan el orden de entrada; ascendente deja las ausentes primero y descendente, últimas.</summary>
        public static List<Entity> Ordenar(List<Entity> filas, IEnumerable<OrderExpression> ordenes, Func<string, bool> oculta)
        {
            IOrderedEnumerable<Entity> ordenado = null;
            foreach (var orden in ordenes)
            {
                var columna = orden.AttributeName;
                var comparador = new ComparadorDeOrden(columna, oculta);
                if (ordenado == null)
                    ordenado = orden.OrderType == OrderType.Descending ? filas.OrderByDescending(f => f, comparador) : filas.OrderBy(f => f, comparador);
                else
                    ordenado = orden.OrderType == OrderType.Descending ? ordenado.ThenByDescending(f => f, comparador) : ordenado.ThenBy(f => f, comparador);
            }
            return ordenado == null ? filas : ordenado.ToList();
        }

        private sealed class ComparadorDeOrden : IComparer<Entity>
        {
            private readonly string _columna;
            private readonly Func<string, bool> _oculta;

            public ComparadorDeOrden(string columna, Func<string, bool> oculta)
            {
                _columna = columna;
                _oculta = oculta;
            }

            public int Compare(Entity x, Entity y)
            {
                var a = ParaComparar(ValorDe(x, _columna, _oculta));
                var b = ParaComparar(ValorDe(y, _columna, _oculta));
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                if (!Coercionar(a, ref b))
                    throw new InvalidOperationException("La columna '" + _columna + "' tiene valores de tipos distintos (" + a.GetType().Name + " y " + b.GetType().Name + "): no se puede ordenar.");
                return Comparar(a, b);
            }
        }

        /// <summary>
        /// La cookie de paginación (fixtures 04, 05, 06, 07, 08): <c>&lt;cookie page="n"&gt;</c> con un hijo por columna del orden (en su orden)
        /// y al final uno por el id primario, cada uno con <c>last</c> y <c>first</c> (primera y última fila DE LA PÁGINA). Vacía si no hay filas.
        /// </summary>
        public static string Cookie(int pagina, IReadOnlyList<Entity> filasDeLaPagina, IEnumerable<OrderExpression> ordenes, string columnaDeId, Func<string, bool> oculta)
        {
            if (filasDeLaPagina.Count == 0) return string.Empty;
            var primera = filasDeLaPagina[0];
            var ultima = filasDeLaPagina[filasDeLaPagina.Count - 1];
            var raiz = new XElement("cookie", new XAttribute("page", pagina.ToString(CultureInfo.InvariantCulture)));
            var nombres = new List<string>();
            foreach (var orden in ordenes)
                if (!nombres.Contains(orden.AttributeName, StringComparer.Ordinal) && !string.Equals(orden.AttributeName, columnaDeId, StringComparison.Ordinal)) nombres.Add(orden.AttributeName);
            nombres.Add(columnaDeId);
            foreach (var nombre in nombres)
            {
                var esId = string.Equals(nombre, columnaDeId, StringComparison.Ordinal);
                var textoUltima = esId ? "{" + ultima.Id.ToString() + "}" : FormaDeLaPlataforma.TextoDeCookie(ValorDe(ultima, nombre, oculta));
                var textoPrimera = esId ? "{" + primera.Id.ToString() + "}" : FormaDeLaPlataforma.TextoDeCookie(ValorDe(primera, nombre, oculta));
                raiz.Add(new XElement(nombre, new XAttribute("last", textoUltima), new XAttribute("first", textoPrimera)));
            }
            return raiz.ToString(SaveOptions.DisableFormatting);
        }
    }
}
