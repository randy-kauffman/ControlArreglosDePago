using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// Evalúa una <see cref="QueryExpression"/> contra las filas en memoria (diseno/03 4.5, "Condiciones" y "Paginación"). Lo que no simula, lo dice con <see cref="NotSupportedException"/> y el nombre de lo que
    /// falta: un doble a medias deja pasar en el puesto lo que falla en Dev (decisión del arquitecto, 2026-10-14). Lo que Dev rechaza, lo rechaza con la <c>FaultException</c> y el <c>ErrorCode</c> que midió
    /// (lote 3b, <c>spikes/dobles/RESULTADO.md</c>).
    /// <para>
    /// <b>Qué dice la plataforma (medido).</b> <c>NotEqual</c> sobre una columna AUSENTE incluye la fila (como <c>NotIn</c>); un subfiltro vacío dentro de otro se ignora (ni verdadero ni falso) y un criterio con
    /// solo filtros vacíos equivale a no tener criterio; un <c>OptionSetValue</c>, un <c>EntityReference</c> o un <c>Money</c> como valor de una condición, un operador de un solo valor con 0, 2 o más valores,
    /// <c>Null</c> o <c>NotNull</c> con un valor e <c>In</c> sin valores fallan <c>0x80040203</c>; el texto se compara sin distinguir mayúsculas NI acentos; una cookie entrante que no es XML falla
    /// <c>0x80040201</c> y la de otra consulta, <c>0x80041129</c> (o <c>0x80041103</c> si nombra una columna que la tabla no tiene). Nada de esto depende de los datos: una tabla vacía lanza igual.
    /// </para>
    /// <para>
    /// <b>Lo que sigue lanzando <see cref="InvalidOperationException"/></b> son los dos límites de la documentación de Learn, que Dev no se midió (<c>PageInfo.Count</c> mayor que 5000 y <c>TopCount</c> con
    /// <c>PageInfo</c>), <c>PageNumber</c> mayor que 1 con <c>Count</c> en 0 y comparar valores de tipos incompatibles en una columna que el doble no tipa. <c>Like</c>, <c>BeginsWith</c>, <c>NotIn</c> y los demás
    /// operadores no se simulan (<see cref="NotSupportedException"/>).
    /// </para>
    /// </summary>
    internal static class EvaluadorDeConsultas
    {
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

        /// <summary>
        /// Lanza <see cref="NotSupportedException"/> si la consulta usa algo que el doble no simula y <see cref="InvalidOperationException"/> si su FORMA rompe uno de los límites de Learn (diseno/03 4.5, "Paginación"):
        /// nunca según los datos, así que una tabla vacía lanza igual.
        /// </summary>
        public static void Validar(QueryExpression consulta)
        {
            if (consulta.LinkEntities != null && consulta.LinkEntities.Count > 0)
                throw new NotSupportedException("El doble no simula LinkEntities (joins): una consulta con join pasa en el puesto y falla en el entorno (diseno/03 4.1, \"Regla de datos\"). Se hacen dos consultas.");
            if (consulta.Criteria != null) ValidarFiltro(consulta.Criteria);

            var paginada = EsPaginada(consulta);
            if (consulta.PageInfo != null && consulta.PageInfo.Count > MaximoPorPagina)
                throw new InvalidOperationException("PageInfo.Count " + consulta.PageInfo.Count.ToString(CultureInfo.InvariantCulture) + " supera el máximo de " + MaximoPorPagina.ToString(CultureInfo.InvariantCulture)
                    + " filas por página (Learn: \"The default and maximum page size is 5,000 rows for standard tables\").");
            if (consulta.TopCount.HasValue && paginada)
                throw new InvalidOperationException("TopCount no se usa junto con PageInfo: son dos formas de limitar el resultado y no son compatibles (Learn: \"Don't use the QueryExpression.TopCount property with paging\").");
            if (paginada && consulta.PageInfo.Count <= 0 && consulta.PageInfo.PageNumber > 1)
                throw new InvalidOperationException("PageInfo trae PageNumber " + consulta.PageInfo.PageNumber.ToString(CultureInfo.InvariantCulture) + " con Count en 0: el tamaño de la página no está definido y la plataforma no tiene "
                    + "verificado cuál usaría (¿el de 5000 por defecto?). El doble no adivina: indique PageInfo.Count.");
        }

        /// <summary>
        /// LA definición de "consulta paginada" (la usan <see cref="Validar"/> y <c>Consultar</c>): trae un <c>PageInfo</c> con <c>Count</c> o con <c>PageNumber</c>. Un <c>PageInfo</c> vacío (por ejemplo
        /// solo para pedir el total) no pagina.
        /// </summary>
        public static bool EsPaginada(QueryExpression consulta)
        {
            return consulta.PageInfo != null && (consulta.PageInfo.Count > 0 || consulta.PageInfo.PageNumber > 0);
        }

        /// <summary>Filas por página de una consulta paginada: el <c>Count</c> pedido o, sin él, el de la plataforma (5000).</summary>
        public static int TamanoDePagina(QueryExpression consulta)
        {
            return consulta.PageInfo == null || consulta.PageInfo.Count <= 0 ? MaximoPorPagina : consulta.PageInfo.Count;
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

        /// <summary>
        /// La cantidad de valores y el tipo de cada valor de cada condición (<c>0x80040203</c>, <c>12-operadores-y-filtros.json</c> y <c>10-condiciones-valores-tipados.json</c>), también en filtros anidados. Depende de la
        /// FORMA de la consulta, no de los datos. Se llama después de comprobar que las columnas existen.
        /// </summary>
        public static void ValidarCondiciones(string tabla, FilterExpression filtro, EsquemaDelAlmacen esquema, DateTime ahora)
        {
            foreach (var condicion in filtro.Conditions)
            {
                var valores = ValoresDe(condicion);
                var columna = condicion.AttributeName;
                switch (condicion.Operator)
                {
                    case ConditionOperator.Null:
                    case ConditionOperator.NotNull:
                        if (valores.Count > 0)
                            throw FabricaDeFallas.SolicitudInvalida("Condition operator '" + condicion.Operator + "' requires that no values are set. Values.Length: " + valores.Count.ToString(CultureInfo.InvariantCulture), ahora);
                        break;
                    case ConditionOperator.In:
                        if (valores.Count == 0) throw FabricaDeFallas.SolicitudInvalida("The value passed for ConditionOperator.In is empty. Attribute Name: " + columna, ahora);
                        break;
                    default:
                        if (DeUnSoloValor.Contains(condicion.Operator) && valores.Count != 1)
                            throw FabricaDeFallas.SolicitudInvalida("The ConditonOperator." + condicion.Operator + " requires 1 value/s, not " + valores.Count.ToString(CultureInfo.InvariantCulture)
                                + ". Parameter Name: " + columna, ahora);
                        break;
                }
                foreach (var valor in valores) ValidarTipoDelValor(tabla, columna, valor, esquema, ahora);
            }
            foreach (var hijo in filtro.Filters) ValidarCondiciones(tabla, hijo, esquema, ahora);
        }

        private static void ValidarTipoDelValor(string tabla, string columna, object valor, EsquemaDelAlmacen esquema, DateTime ahora)
        {
            if (!(valor is OptionSetValue) && !(valor is EntityReference) && !(valor is Money)) return;
            // Para statecode la plataforma lo dice con otras palabras: los valores van como texto o entero.
            if (string.Equals(columna, "statecode", StringComparison.Ordinal))
                throw FabricaDeFallas.SolicitudInvalida("Condition for attribute '" + tabla + "." + columna + "': statecode values are expected to be passed as strings or int", ahora);
            throw FabricaDeFallas.SolicitudInvalida("Condition for attribute '" + tabla + "." + columna + "': expected argument(s) of type '" + esquema.TipoEsperadoDeCondicion(tabla, columna, valor)
                + "' but received '" + valor.GetType().FullName + "'.", ahora);
        }

        /// <summary>
        /// Una cookie entrante no vacía de una consulta paginada (<c>16-paginacion-cookie.json</c>): si no es XML, <c>0x80040201</c>; si nombra una columna que la tabla no tiene, <c>0x80041103</c>; si trae otra cantidad
        /// de columnas o columnas de orden distintas de las de la consulta, <c>0x80041129</c>. La página la decide <c>PageNumber</c>, no la cookie: el <c>page</c> editado de la cookie no se emula (no medido).
        /// </summary>
        public static void ValidarCookie(QueryExpression consulta, EsquemaDelAlmacen esquema, string columnaDeId, DateTime ahora)
        {
            if (!EsPaginada(consulta) || string.IsNullOrEmpty(consulta.PageInfo.PagingCookie)) return;
            XElement raiz;
            try
            {
                raiz = XElement.Parse(consulta.PageInfo.PagingCookie);
            }
            catch (XmlException)
            {
                throw FabricaDeFallas.CookieMalFormada(ahora);
            }

            var deLaCookie = raiz.Elements().Select(e => e.Name.LocalName).ToList();
            if (esquema.ClaseDe(consulta.EntityName) == ClaseDeTabla.Propia || esquema.ClaseDe(consulta.EntityName) == ClaseDeTabla.NativaConMuestra)
            {
                foreach (var nombre in deLaCookie)
                    if (!esquema.TieneColumna(consulta.EntityName, nombre)) throw FabricaDeFallas.ColumnaInexistente(consulta.EntityName, nombre, false, ahora);
            }
            var deLaConsulta = NombresDeLaCookie(consulta.Orders ?? (IList<OrderExpression>)new List<OrderExpression>(), columnaDeId);
            if (deLaCookie.Count != deLaConsulta.Count)
                throw FabricaDeFallas.CookieDeOtraConsulta("Paging Cookie And Query Do Not Match. The counts are not equal.", ahora);
            if (!deLaCookie.SequenceEqual(deLaConsulta, StringComparer.Ordinal))
                throw FabricaDeFallas.CookieDeOtraConsulta("Order by columns do not match those in paging cookie.", ahora);
        }

        /// <summary>Las columnas que lleva la cookie de una consulta: las del orden (sin repetir y sin el id primario, en su orden) y al final el id primario.</summary>
        private static List<string> NombresDeLaCookie(IEnumerable<OrderExpression> ordenes, string columnaDeId)
        {
            var nombres = new List<string>();
            foreach (var orden in ordenes)
                if (!nombres.Contains(orden.AttributeName, StringComparer.Ordinal) && !string.Equals(orden.AttributeName, columnaDeId, StringComparison.Ordinal)) nombres.Add(orden.AttributeName);
            nombres.Add(columnaDeId);
            return nombres;
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

        /// <summary>
        /// Las filas que cumplen el criterio. <paramref name="oculta"/> dice qué columnas valen como nulas para quien consulta (DC-22). <paramref name="zonaLocal"/> es la
        /// <see cref="OrganizationServiceEnMemoria.ZonaLocal"/> del almacén: convierte el valor <c>DateTime</c> de <c>Kind.Local</c> de una condición, igual que una escritura (4.5, cierre del lote 3b, H4).
        /// </summary>
        public static List<Entity> Filtrar(IEnumerable<Entity> filas, FilterExpression criterio, Func<string, bool> oculta, TimeZoneInfo zonaLocal)
        {
            var resultado = new List<Entity>();
            foreach (var fila in filas)
                if (criterio == null || EvaluarFiltro(criterio, fila, oculta, zonaLocal)) resultado.Add(fila);
            return resultado;
        }

        /// <summary>Un filtro sin condiciones y sin subfiltros con algo adentro: la plataforma lo ignora (medido, <c>12-operadores-y-filtros.json</c> punto_5).</summary>
        private static bool EsVacio(FilterExpression filtro)
        {
            return filtro.Conditions.Count == 0 && filtro.Filters.All(EsVacio);
        }

        private static bool EvaluarFiltro(FilterExpression filtro, Entity fila, Func<string, bool> oculta, TimeZoneInfo zonaLocal)
        {
            var resultados = new List<bool>();
            foreach (var condicion in filtro.Conditions) resultados.Add(EvaluarCondicion(condicion, fila, oculta, zonaLocal));
            foreach (var hijo in filtro.Filters)
                if (!EsVacio(hijo)) resultados.Add(EvaluarFiltro(hijo, fila, oculta, zonaLocal));
            if (resultados.Count == 0) return true;
            return filtro.FilterOperator == LogicalOperator.Or ? resultados.Contains(true) : !resultados.Contains(false);
        }

        private static object ValorDe(Entity fila, string columna, Func<string, bool> oculta)
        {
            if (oculta(columna)) return null;
            object valor;
            return fila.Attributes.TryGetValue(columna, out valor) ? valor : null;
        }

        private static bool EvaluarCondicion(ConditionExpression condicion, Entity fila, Func<string, bool> oculta, TimeZoneInfo zonaLocal)
        {
            var guardado = ValorDe(fila, condicion.AttributeName, oculta);
            var ausente = guardado == null;
            switch (condicion.Operator)
            {
                case ConditionOperator.Null: return ausente;
                case ConditionOperator.NotNull: return !ausente;
            }

            // NotEqual sobre una columna ausente INCLUYE la fila (medido): para la plataforma un nulo es distinto de cualquier valor. Lo demás sobre una columna ausente no cumple.
            if (ausente) return condicion.Operator == ConditionOperator.NotEqual;

            var valores = ValoresDe(condicion);
            var izquierda = ParaComparar(guardado);
            switch (condicion.Operator)
            {
                case ConditionOperator.In:
                    foreach (var valor in valores)
                        if (Cumple(izquierda, valor, condicion, zonaLocal, c => c == 0)) return true;
                    return false;
                case ConditionOperator.Equal: return Cumple(izquierda, Primero(valores, condicion), condicion, zonaLocal, c => c == 0);
                case ConditionOperator.NotEqual: return Cumple(izquierda, Primero(valores, condicion), condicion, zonaLocal, c => c != 0);
                case ConditionOperator.GreaterThan: return Cumple(izquierda, Primero(valores, condicion), condicion, zonaLocal, c => c > 0);
                case ConditionOperator.GreaterEqual: return Cumple(izquierda, Primero(valores, condicion), condicion, zonaLocal, c => c >= 0);
                case ConditionOperator.LessThan: return Cumple(izquierda, Primero(valores, condicion), condicion, zonaLocal, c => c < 0);
                case ConditionOperator.LessEqual: return Cumple(izquierda, Primero(valores, condicion), condicion, zonaLocal, c => c <= 0);
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

        private static bool Cumple(object izquierda, object valor, ConditionExpression condicion, TimeZoneInfo zonaLocal, Func<int, bool> prueba)
        {
            var derecha = ParaComparar(valor, zonaLocal);
            if (derecha == null) return false;
            if (!Coercionar(izquierda, ref derecha))
            {
                throw new InvalidOperationException("La condición " + condicion.Operator + " sobre '" + condicion.AttributeName + "' compara un valor de tipo "
                    + derecha.GetType().Name + " con una columna de tipo " + izquierda.GetType().Name + "; la plataforma no lo hace sin convertir.");
            }
            return prueba(Comparar(izquierda, derecha));
        }

        // ---- Comparación de valores ----

        /// <summary>
        /// Lleva un valor YA GUARDADO a su forma comparable. Un valor guardado nunca es <c>Local</c> (la escritura y la siembra lo llevan a UTC), así que la zona no interviene y no se lee la de la máquina.
        /// </summary>
        public static object ParaComparar(object valor)
        {
            return ParaComparar(valor, TimeZoneInfo.Utc);
        }

        /// <summary>
        /// Lleva el valor a su forma comparable: choice ⇒ número, referencia ⇒ Guid, Money ⇒ decimal, enteros ⇒ decimal, fecha ⇒ UTC. Un <c>DateTime</c> <c>Local</c> se convierte con <paramref name="zonaLocal"/>
        /// (la del almacén) y no con la de la máquina: una condición es lo mismo que una escritura (4.5, cierre del lote 3b, H4).
        /// </summary>
        public static object ParaComparar(object valor, TimeZoneInfo zonaLocal)
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
            if (valor is DateTime) return FormaDeLaPlataforma.AUtc((DateTime)valor, zonaLocal);
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

        /// <summary>
        /// Compara dos valores ya comparables y del mismo tipo. El texto no distingue mayúsculas NI acentos (medido, <c>18-texto-y-esquema.json</c> punto_c: <c>ZZF-José</c> se halla con <c>zzf-jose</c>,
        /// <c>ZZF-JOSÉ</c>, <c>ZZF-Josè</c> y <c>ZZF-Jose</c> más una marca combinante). Los ESPACIOS FINALES tampoco cuentan, como el relleno de las comparaciones de SQL Server (diseno/03 4.5, "Cierre del lote 3b", H1).
        /// <b>Lo medido es solo <c>Equal ''</c> contra una fila cuyo texto es <c>'   '</c></b> (<c>18-texto-y-esquema.json</c>, <c>punto_d_cadena_vacia</c>); que valga para cualquier texto, para <c>NotEqual</c>, <c>In</c>, el orden
        /// y la clave alterna es la extrapolación consistente con esa regla de SQL, sin medir. Solo se descartan espacios (U+0020) del final: los del principio y otros blancos cuentan.
        /// </summary>
        public static int Comparar(object a, object b)
        {
            var texto = a as string;
            if (texto != null) return string.Compare(SinEspaciosFinales(TextoSinAcentos.Quitar(texto)), SinEspaciosFinales(TextoSinAcentos.Quitar((string)b)), StringComparison.OrdinalIgnoreCase);
            if (a is decimal) return ((decimal)a).CompareTo((decimal)b);
            if (a is DateTime) return ((DateTime)a).Ticks.CompareTo(((DateTime)b).Ticks);
            if (a is bool) return ((bool)a).CompareTo((bool)b);
            if (a is Guid) return ((Guid)a).CompareTo((Guid)b);
            throw new NotSupportedException("El doble no sabe comparar valores de tipo " + a.GetType().Name + ".");
        }

        private static string SinEspaciosFinales(string texto)
        {
            return texto.TrimEnd(' ');
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
            foreach (var nombre in NombresDeLaCookie(ordenes, columnaDeId))
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
