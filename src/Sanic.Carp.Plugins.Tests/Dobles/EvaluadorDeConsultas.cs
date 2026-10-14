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
    /// <para>
    /// <b>VUELTA 2 (hallazgo 6): ~~lo anterior depende de los datos~~ <c>NotEqual</c> lanza por la FORMA de la consulta</b>, con la tabla vacía o con todas las filas llenas, salvo que el mismo <c>And</c> tenga
    /// una condición sobre la misma columna que excluya los ausentes (<c>NotNull</c>, <c>Equal</c>, <c>In</c>, <c>Greater*</c>, <c>Less*</c>) o que la columna sea <c>statecode</c>, <c>statuscode</c> o el id primario.
    /// También lanzan por la forma: un operador de un solo valor con varios, un subfiltro vacío, <c>PageInfo.Count</c> mayor que 5000, <c>TopCount</c> con <c>PageInfo</c> y una tabla o columna
    /// que no existe en el esquema. Ver 4.5 y <c>OrdenYConsultasInvalidasAceptacion</c>.
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

        /// <summary>Máximo de filas por página de una tabla estándar (Learn, "Page results using QueryExpression").</summary>
        private const int MaximoPorPagina = 5000;

        /// <summary>Los operadores que comparan contra UN valor (<c>In</c> es el que admite varios).</summary>
        private static readonly HashSet<ConditionOperator> DeUnSoloValor = new HashSet<ConditionOperator>
        {
            ConditionOperator.Equal, ConditionOperator.NotEqual, ConditionOperator.GreaterThan, ConditionOperator.GreaterEqual, ConditionOperator.LessThan, ConditionOperator.LessEqual
        };

        /// <summary>Los operadores que, sobre la misma columna y en el mismo <c>And</c>, dejan afuera a las filas donde la columna está ausente.</summary>
        private static readonly HashSet<ConditionOperator> ExcluyenAusentes = new HashSet<ConditionOperator>
        {
            ConditionOperator.NotNull, ConditionOperator.Equal, ConditionOperator.In, ConditionOperator.GreaterThan, ConditionOperator.GreaterEqual, ConditionOperator.LessThan, ConditionOperator.LessEqual
        };

        /// <summary>
        /// Lanza <see cref="NotSupportedException"/> si la consulta usa algo que el doble no simula y <see cref="InvalidOperationException"/> si su FORMA es inválida o no tiene respuesta
        /// verificada (diseno/03 4.5, "Paginación", vuelta 2, hallazgo 6): nunca según los datos, así que una tabla vacía lanza igual.
        /// </summary>
        public static void Validar(QueryExpression consulta)
        {
            if (consulta.LinkEntities != null && consulta.LinkEntities.Count > 0)
                throw new NotSupportedException("El doble no simula LinkEntities (joins): una consulta con join pasa en el puesto y falla en el entorno (diseno/03 4.1, \"Regla de datos\"). Se hacen dos consultas.");
            if (consulta.Criteria != null) ValidarFiltro(consulta.Criteria);

            var paginada = consulta.PageInfo != null && (consulta.PageInfo.Count > 0 || consulta.PageInfo.PageNumber > 0);
            if (consulta.PageInfo != null && consulta.PageInfo.Count > MaximoPorPagina)
                throw new InvalidOperationException("PageInfo.Count " + consulta.PageInfo.Count.ToString(CultureInfo.InvariantCulture) + " supera el máximo de " + MaximoPorPagina.ToString(CultureInfo.InvariantCulture)
                    + " filas por página (Learn: \"The default and maximum page size is 5,000 rows for standard tables\").");
            if (consulta.TopCount.HasValue && paginada)
                throw new InvalidOperationException("TopCount no se usa junto con PageInfo: son dos formas de limitar el resultado y no son compatibles (Learn: \"Don't use the QueryExpression.TopCount property with paging\").");
            if (consulta.Criteria != null) ValidarForma(consulta.Criteria, true, consulta.EntityName);
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

        /// <summary>Las reglas que dependen de la forma del filtro: subfiltro vacío, varios valores en un operador de uno solo y <c>NotEqual</c> sobre una columna que puede estar ausente.</summary>
        private static void ValidarForma(FilterExpression filtro, bool esRaiz, string tabla)
        {
            if (!esRaiz && filtro.Conditions.Count == 0 && filtro.Filters.Count == 0)
                throw new InvalidOperationException("La consulta trae un subfiltro vacío dentro de otro filtro: no tiene respuesta verificada (¿se ignora, es verdadero, falla?) y el doble no adivina. Quite el subfiltro.");

            foreach (var condicion in filtro.Conditions)
            {
                if (DeUnSoloValor.Contains(condicion.Operator) && ValoresDe(condicion).Count > 1)
                    throw new InvalidOperationException("La condición " + condicion.Operator + " sobre '" + condicion.AttributeName + "' trae " + ValoresDe(condicion).Count.ToString(CultureInfo.InvariantCulture)
                        + " valores y compara contra uno solo: la plataforma no lo tiene verificado (¿lo trata como In?, ¿falla?) y el doble no ignora los demás. Use In.");
                if (condicion.Operator == ConditionOperator.NotEqual && !NotEqualEsSeguro(filtro, condicion, tabla))
                    throw new InvalidOperationException("La consulta usa NotEqual sobre '" + condicion.AttributeName + "' sin una condición NotNull, Equal, In, GreaterThan, GreaterEqual, LessThan o LessEqual sobre la misma "
                        + "columna en el mismo And: la plataforma no tiene verificado si NotEqual incluye las filas donde la columna está vacía (SQL las excluye, Dataverse puede incluirlas) y el doble no adivina. "
                        + "Escriba la consulta de modo que no dependa de eso (por ejemplo, agregue NotNull sobre la columna).");
            }
            foreach (var hijo in filtro.Filters) ValidarForma(hijo, false, tabla);
        }

        /// <summary>
        /// <c>NotEqual</c> no depende de cómo trate la plataforma a los vacíos si la columna siempre está llena (<c>statecode</c>, <c>statuscode</c> y el id primario) o si el mismo <c>And</c> lleva otra condición
        /// sobre esa columna que ya excluye los ausentes. Una condición de otra columna, o de un <c>Or</c>, no cuenta.
        /// </summary>
        private static bool NotEqualEsSeguro(FilterExpression filtro, ConditionExpression condicion, string tabla)
        {
            var columna = condicion.AttributeName;
            if (string.Equals(columna, "statecode", StringComparison.Ordinal) || string.Equals(columna, "statuscode", StringComparison.Ordinal)
                || string.Equals(columna, FormaDeLaPlataforma.ColumnaDeId(tabla), StringComparison.Ordinal)) return true;
            if (filtro.FilterOperator != LogicalOperator.And) return false;
            return filtro.Conditions.Any(otra => !ReferenceEquals(otra, condicion) && string.Equals(otra.AttributeName, columna, StringComparison.Ordinal) && ExcluyenAusentes.Contains(otra.Operator));
        }

        /// <summary>Todas las columnas que la consulta nombra: las del <c>ColumnSet</c> (si no es <c>AllColumns</c>), las de las condiciones —también anidadas— y las de los órdenes.</summary>
        public static List<string> ColumnasQueNombra(QueryExpression consulta)
        {
            var nombres = new List<string>();
            if (consulta.ColumnSet != null && !consulta.ColumnSet.AllColumns) nombres.AddRange(consulta.ColumnSet.Columns);
            if (consulta.Criteria != null) ColumnasDelFiltro(consulta.Criteria, nombres);
            if (consulta.Orders != null) nombres.AddRange(consulta.Orders.Select(o => o.AttributeName));
            return nombres;
        }

        private static void ColumnasDelFiltro(FilterExpression filtro, List<string> nombres)
        {
            nombres.AddRange(filtro.Conditions.Select(c => c.AttributeName));
            foreach (var hijo in filtro.Filters) ColumnasDelFiltro(hijo, nombres);
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

        /// <summary>
        /// Orden ADVERSARIO y determinista (diseno/03 4.5, "Paginación", vuelta 2, hallazgo 4 y decisión del orquestador): se parte del INVERSO del orden de entrada y se ordena de forma ESTABLE por las
        /// columnas pedidas, así que sin <c>OrderExpression</c> el resultado es el inverso de la entrada y los empates de un orden también van al inverso dentro de cada grupo. QueryExpression no agrega
        /// un orden por la clave primaria (Learn, "Page results using QueryExpression"), así que un <c>.First()</c> que acierta por el orden de inserción falla en Dev: acá falla también. Ascendente deja las
        /// ausentes primero y descendente, últimas.
        /// </summary>
        public static List<Entity> Ordenar(List<Entity> filas, IEnumerable<OrderExpression> ordenes, Func<string, bool> oculta)
        {
            var inversa = new List<Entity>(filas);
            inversa.Reverse();
            IOrderedEnumerable<Entity> ordenado = null;
            foreach (var orden in ordenes)
            {
                var columna = orden.AttributeName;
                var comparador = new ComparadorDeOrden(columna, oculta);
                if (ordenado == null)
                    ordenado = orden.OrderType == OrderType.Descending ? inversa.OrderByDescending(f => f, comparador) : inversa.OrderBy(f => f, comparador);
                else
                    ordenado = orden.OrderType == OrderType.Descending ? ordenado.ThenByDescending(f => f, comparador) : ordenado.ThenBy(f => f, comparador);
            }
            return ordenado == null ? inversa : ordenado.ToList();
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
