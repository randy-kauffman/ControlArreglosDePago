using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Vuelta 2, hallazgo 4</b>: SIN <c>OrderExpression</c> el doble devuelve un orden ADVERSARIO determinista: el INVERSO al de entrada. Antes devolvía el de inserción y un
    /// <c>.First()</c> sin orden acertaba siempre en el puesto y fallaba en Dev. Fuente: Learn, "Page results using QueryExpression", sección "Ordering and paging": "With FetchXml, if you
    /// don't add any order elements to your query, Dataverse adds an order based on the primary key of the table. However QueryExpression does not"; sin orden "might be returned in random order".
    /// Esta prueba REEMPLAZA a <c>PA_QRY_06</c> (que exigía el orden de entrada; ver <c>ConsultasEnMemoriaAceptacion</c>) y el texto de 4.5 "sin <c>OrderExpression</c> el orden es el de entrada" queda tachado.
    /// Con <c>OrderExpression</c> las filas se ordenan por sus columnas y <b>los EMPATES van TAMBIÉN en orden adversario: el inverso al de entrada dentro de cada grupo empatado</b>
    /// (decisión del orquestador sobre la tensión 1; mismo principio, la plataforma no garantiza el orden de los empates: Learn, "Page results using QueryExpression", "Orders that
    /// don't include unique identifiers" como anti-patrón y "there's no guarantee that the other two records on the first page aren't included in the second page"). Regla:
    /// se parte del inverso del orden de entrada y se ordena de forma ESTABLE, así la paginación sigue siendo coherente entre páginas. PA_ORD_05 a PA_ORD_07 matan el "desempate estable por inserción";
    /// PA_QRY_07*, PA_QRY_08, PA_QRY_10 y PA_HDD_04 se reescribieron con la misma regla.
    /// Los ids se siembran FUERA de orden (3, 1, 2) para que ni el orden de inserción, ni el de id ascendente, ni el de id descendente pasen por el inverso.
    /// </summary>
    public class OrdenAdversarioAceptacion
    {
        private static readonly Guid Fila4 = new Guid("cccccccc-0000-4000-8000-000000000004");
        private static readonly Guid Fila5 = new Guid("cccccccc-0000-4000-8000-000000000005");

        /// <summary>
        /// Entrada: A(Fila3, g=1), B(Fila1, g=2), C(Fila2, g=1), D(Fila4, g=2), E(Fila5, g=1). Grupo 1 = A, C, E; grupo 2 = B, D. Ni el orden de inserción (A, C, E), ni el de id ascendente
        /// (C, A, E), ni el de id descendente (E, A, C) coinciden con el inverso de entrada (E, C, A): cada mutación del desempate muere.
        /// </summary>
        private static OrganizationServiceEnMemoria ConEmpates()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila3, "zzf_g", 1), Zzf(Fila1, "zzf_g", 2), Zzf(Fila2, "zzf_g", 1), Zzf(Fila4, "zzf_g", 2), Zzf(Fila5, "zzf_g", 1));
            return almacen;
        }

        private static OrganizationServiceEnMemoria SembradoFueraDeOrden()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila3, "zzf_a", 3), Zzf(Fila1, "zzf_a", 1), Zzf(Fila2, "zzf_a", 2));
            return almacen;
        }

        private static Guid[] SinOrden(OrganizationServiceEnMemoria almacen, Action<QueryExpression> ajustar = null)
        {
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            if (ajustar != null) ajustar(consulta);
            return almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).ToArray();
        }

        [Fact]
        public void PA_ORD_01_SinOrderExpressionElOrdenEsElInversoAlDeEntrada_Determinista_YUnaFilaNuevaQuedaPrimera()
        {
            var almacen = SembradoFueraDeOrden();

            Assert.Equal(new[] { Fila2, Fila1, Fila3 }, SinOrden(almacen));
            Assert.Equal(new[] { Fila2, Fila1, Fila3 }, SinOrden(almacen));   // determinista: la misma consulta, el mismo orden
            almacen.Create(Zzf(Fila4, "zzf_a", 4));
            Assert.Equal(new[] { Fila4, Fila2, Fila1, Fila3 }, SinOrden(almacen));
            // El criterio filtra y el orden adversario se conserva sobre lo que queda.
            Assert.Equal(new[] { Fila2, Fila1 }, SinOrden(almacen, c => c.Criteria.AddCondition("zzf_a", ConditionOperator.LessThan, 3)));
        }

        [Fact]
        public void PA_ORD_02_UnTopCountSinOrderExpressionTomaDelOrdenAdversario_NoDeLaPrimeraInsertada()
        {
            var almacen = SembradoFueraDeOrden();

            var primera = SinOrden(almacen, c => c.TopCount = 1);
            var dos = SinOrden(almacen, c => c.TopCount = 2);

            Assert.Equal(new[] { Fila2 }, primera);
            Assert.Equal(new[] { Fila2, Fila1 }, dos);
        }

        [Fact]
        public void PA_ORD_03_UnaPaginaSinOrderExpressionPaginaElOrdenAdversario_SinRepetirNiPerder()
        {
            var almacen = SembradoFueraDeOrden();

            var pagina1 = SinOrden(almacen, c => c.PageInfo = new PagingInfo { Count = 2, PageNumber = 1 });
            var pagina2 = SinOrden(almacen, c => c.PageInfo = new PagingInfo { Count = 2, PageNumber = 2 });

            Assert.Equal(new[] { Fila2, Fila1 }, pagina1);
            Assert.Equal(new[] { Fila3 }, pagina2);
        }

        [Fact]
        public void PA_ORD_04_ConOrderExpressionSobreUnaColumnaUnicaElOrdenSigueLaColumna_VERDE_HOY()
        {
            var almacen = SembradoFueraDeOrden();

            Assert.Equal(new[] { Fila1, Fila2, Fila3 }, SinOrden(almacen, c => c.AddOrder("zzf_a", OrderType.Ascending)));
            Assert.Equal(new[] { Fila3, Fila2, Fila1 }, SinOrden(almacen, c => c.AddOrder("zzf_a", OrderType.Descending)));
        }

        [Fact]
        public void PA_ORD_05_LosEmpatesDeUnOrderExpressionVanEnOrdenInversoAlDeEntradaDentroDeCadaGrupo_AscendenteYDescendente()
        {
            var almacen = ConEmpates();

            // Ascendente: grupo 1 (E, C, A) y luego grupo 2 (D, B). Descendente: grupo 2 (D, B) y luego grupo 1 (E, C, A). Los grupos se ordenan por la columna; solo el desempate es adversario.
            Assert.Equal(new[] { Fila5, Fila2, Fila3, Fila4, Fila1 }, SinOrden(almacen, c => c.AddOrder("zzf_g", OrderType.Ascending)));
            Assert.Equal(new[] { Fila4, Fila1, Fila5, Fila2, Fila3 }, SinOrden(almacen, c => c.AddOrder("zzf_g", OrderType.Descending)));
            Assert.Equal(new[] { Fila5, Fila2, Fila3, Fila4, Fila1 }, SinOrden(almacen, c => c.AddOrder("zzf_g", OrderType.Ascending)));   // determinista
        }

        [Fact]
        public void PA_ORD_06_ElTopCountYLaPaginaTomanLosEmpatesEnOrdenAdversario_SinRepetirNiPerder()
        {
            var almacen = ConEmpates();
            QueryExpression Ordenada() { var c = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() }; c.AddOrder("zzf_g", OrderType.Ascending); return c; }
            var top = Ordenada(); top.TopCount = 2;
            var uno = Ordenada(); uno.PageInfo = new PagingInfo { Count = 2, PageNumber = 1 };
            var dos = Ordenada(); dos.PageInfo = new PagingInfo { Count = 2, PageNumber = 2 };
            var tres = Ordenada(); tres.PageInfo = new PagingInfo { Count = 2, PageNumber = 3 };

            Assert.Equal(new[] { Fila5, Fila2 }, almacen.RetrieveMultiple(top).Entities.Select(e => e.Id).ToArray());
            Assert.Equal(new[] { Fila5, Fila2 }, almacen.RetrieveMultiple(uno).Entities.Select(e => e.Id).ToArray());
            Assert.Equal(new[] { Fila3, Fila4 }, almacen.RetrieveMultiple(dos).Entities.Select(e => e.Id).ToArray());
            Assert.Equal(new[] { Fila1 }, almacen.RetrieveMultiple(tres).Entities.Select(e => e.Id).ToArray());
        }

        [Fact]
        public void PA_ORD_07_ConVariasColumnasSoloEmpataLoQueEmpataEnTodas_YLasFilasNuevasEmpatadasQuedanPrimeras()
        {
            var almacen = ConEmpates();
            QueryExpression Consulta() { var c = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() }; c.AddOrder("zzf_g", OrderType.Ascending); return c; }
            almacen.Update(Zzf(Fila3, "zzf_h", 1));
            almacen.Update(Zzf(Fila2, "zzf_h", 2));
            var porDosColumnas = Consulta(); porDosColumnas.AddOrder("zzf_h", OrderType.Descending);

            // g = 1: C (h 2) antes que A (h 1) por la segunda columna; E (h ausente) queda al final de su grupo en descendente. g = 2: B y D empatan en todo (h ausente) y van al inverso: D, B.
            Assert.Equal(new[] { Fila2, Fila3, Fila5, Fila4, Fila1 }, almacen.RetrieveMultiple(porDosColumnas).Entities.Select(e => e.Id).ToArray());
            var nueva = almacen.Create(Zzf(Guid.NewGuid(), "zzf_g", 1));
            Assert.Equal(nueva, almacen.RetrieveMultiple(Consulta()).Entities.First().Id);   // la fila nueva del grupo 1 queda primera: el empate va en orden inverso al de entrada
        }
    }

    /// <summary>
    /// <b>Vuelta 2, hallazgo 6</b>: consultas que Microsoft documenta como inválidas o cuya respuesta la plataforma no tiene verificada LANZAN <see cref="InvalidOperationException"/>
    /// según la FORMA de la consulta, nunca según los datos que haya (una prueba con la tabla vacía debe fallar igual que una con filas). Citas de Learn ("Page results using
    /// QueryExpression"): "The default and maximum page size is 5,000 rows for standard tables" y "Don't use the QueryExpression.TopCount property with paging. These different methods of
    /// limiting the results of a query are not compatible". Sobre la cookie: "PagingCookie is null for the first request" y "A paging cookie should be used when returned by the previous
    /// page", "Recommended" pero sin ella el modelo cae a paginación simple (límite de 50.000 filas): <b>decisión del arquitecto: el doble NO exige la cookie entrante</b> (PA_CIN_08).
    /// <b>Lote 3b (medido en Dev, 2026-10-15):</b> la prudencia de la vuelta 2 sobre <c>NotEqual</c> sobre columna ausente, <c>Equal</c> con varios valores y subfiltro vacío se resolvió con la medición: NotEqual INCLUYE los nulos,
    /// varios valores en un operador de uno solo dan 0x80040203 y el subfiltro vacío se ignora. Siguen siendo <see cref="InvalidOperationException"/> los de la documentación de Learn (tamaño de página y TopCount con PageInfo).
    /// </summary>
    public class ConsultasInvalidasAceptacion
    {
        private static readonly string[] Propias =
        {
            "sanic_carp_tbl_cliente", "sanic_carp_tbl_conversacion", "sanic_carp_tbl_arreglo", "sanic_carp_tbl_cuota", "sanic_carp_tbl_versionplantilla",
            "sanic_carp_tbl_mapeocelda", "sanic_carp_tbl_generacionformato"
        };

        public static IEnumerable<object[]> TablasPropias()
        {
            foreach (var tabla in Propias) yield return new object[] { tabla };
        }

        public static IEnumerable<object[]> ColumnasParaNotEqual()
        {
            foreach (var par in DiccionarioDeDatos.Leer())
                foreach (var c in par.Value.Where(c => c.Clase != ClaseDeColumna.Formula && c.Clase != ClaseDeColumna.Archivo))
                    yield return new object[] { c.Tabla, c.Nombre };
        }

        private static QueryExpression Consulta(string tabla = TablaZzf)
        {
            return new QueryExpression(tabla) { ColumnSet = new ColumnSet() };
        }

        private static OrganizationServiceEnMemoria ConTodasConEstado()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_estado", 1, "zzf_texto", "a"), Zzf(Fila2, "zzf_estado", 2, "zzf_texto", "b"), Zzf(Fila3, "zzf_estado", 1, "zzf_texto", "c"));
            return almacen;
        }

        // ---- NotEqual: por la forma, no por los datos ----

        [Fact]
        public void PA_CIN_01_NotEqualSinUnaCondicionQueExcluyaLosAusentesDevuelveLasFilasConLaColumnaAusente_ConLaTablaVaciaDevuelveNada()
        {
            // lote 3b, medido en Dev: INVERTIDA (12-operadores-y-filtros.json punto_4, 'NotEqual incluye los nulos'). Antes lanzaba InvalidOperationException por la FORMA de la consulta, haya o no filas.
            var consulta = Consulta();
            consulta.Criteria.AddCondition("zzf_texto", ConditionOperator.NotEqual, "a");
            var almacen = ConTodasConEstado();
            almacen.Sembrar(Zzf(Guid.NewGuid(), "zzf_estado", 1));   // sin zzf_texto: ausente, entra

            var conFilas = almacen.RetrieveMultiple(consulta).Entities;
            var conLaTablaVacia = new OrganizationServiceEnMemoria().RetrieveMultiple(consulta).Entities;

            Assert.Equal(3, conFilas.Count);   // b, c y la que no tiene texto; 'a' queda afuera
            Assert.Empty(conLaTablaVacia);
        }

        [Theory]
        [InlineData("NotNull")]
        [InlineData("Equal")]
        [InlineData("In")]
        [InlineData("GreaterEqual")]
        public void PA_CIN_02_NotEqualAcompanadoEnElMismoAndPorUnaCondicionSobreLaMismaColumnaQueExcluyeLosAusentesNoLanza(string compania)
        {
            var consulta = Consulta();
            switch (compania)
            {
                case "NotNull": consulta.Criteria.AddCondition("zzf_estado", ConditionOperator.NotNull); break;
                case "Equal": consulta.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 1); break;
                case "In": consulta.Criteria.AddCondition("zzf_estado", ConditionOperator.In, 1, 2); break;
                default: consulta.Criteria.AddCondition("zzf_estado", ConditionOperator.GreaterEqual, 1); break;
            }
            consulta.Criteria.AddCondition("zzf_estado", ConditionOperator.NotEqual, 2);

            var ids = ConTodasConEstado().RetrieveMultiple(consulta).Entities.Select(e => e.Id).OrderBy(i => i).ToArray();

            Assert.Equal(new[] { Fila1, Fila3 }, ids);
        }

        [Fact]
        public void PA_CIN_03_LaCompaniaDeOtraColumnaOLaDeUnOrYaNoCambianNada_NotEqualIncluyeLosAusentes()
        {
            // lote 3b, medido en Dev: INVERTIDA. Antes ni la compañía de otra columna ni la de un Or alcanzaban y la consulta lanzaba; ahora no hay nada que lanzar y se evalúa.
            var deOtraColumna = Consulta();
            deOtraColumna.Criteria.AddCondition("zzf_texto", ConditionOperator.NotNull);
            deOtraColumna.Criteria.AddCondition("zzf_estado", ConditionOperator.NotEqual, 2);
            var enUnOr = Consulta();
            var o = enUnOr.Criteria.AddFilter(LogicalOperator.Or);
            o.AddCondition("zzf_estado", ConditionOperator.NotNull);
            o.AddCondition("zzf_estado", ConditionOperator.NotEqual, 2);

            Assert.Equal(new[] { Fila1, Fila3 }, ConTodasConEstado().RetrieveMultiple(deOtraColumna).Entities.Select(e => e.Id).OrderBy(i => i).ToArray());
            Assert.Equal(3, ConTodasConEstado().RetrieveMultiple(enUnOr).Entities.Count);
        }

        [Theory]
        [MemberData(nameof(TablasPropias))]
        public void PA_CIN_04_NotEqualSobreStatecodeStatuscodeOElIdPrimarioNoNecesitaCompania_LaPlataformaSiempreLosLlena(string tabla)
        {
            var almacen = new OrganizationServiceEnMemoria();
            foreach (var columna in new[] { "statecode", "statuscode" })
            {
                var consulta = Consulta(tabla);
                consulta.Criteria.AddCondition(columna, ConditionOperator.NotEqual, 1);
                Assert.Empty(almacen.RetrieveMultiple(consulta).Entities);
            }
            var porId = Consulta(tabla);
            porId.Criteria.AddCondition(tabla + "id", ConditionOperator.NotEqual, Fila1);
            Assert.Empty(almacen.RetrieveMultiple(porId).Entities);
        }

        [Theory]
        [MemberData(nameof(ColumnasParaNotEqual))]
        public void PA_CIN_05_NotEqualSobreUnaColumnaPropiaSinCompaniaIncluyeLaFilaSinLaColumna_YExcluyeLaQueTieneElValor(string tabla, string columna)
        {
            // lote 3b, medido en Dev: INVERTIDA. Antes lanzaba con la tabla vacía para las 89 columnas. Ahora: la fila que no tiene la columna entra y la que tiene el valor comparado sale.
            var c = DiccionarioDeDatos.Leer()[tabla].First(x => x.Nombre == columna);
            var almacen = new OrganizationServiceEnMemoria();
            var sinLaColumna = Guid.NewGuid();
            var conElValor = Guid.NewGuid();
            // Orquestador, 2026-10-15 (lote 3b): el valor se arma UNA vez. Valido devuelve un Guid nuevo en cada llamada para los lookups, así que
            // ParaCondicion(c) comparaba contra otro Guid y ningún doble correcto podía cumplir la prueba en las 18 columnas lookup.
            var valor = ValoresDeEjemplo.Valido(c);
            almacen.Sembrar(new Entity(tabla, sinLaColumna), new Entity(tabla, conElValor) { [columna] = valor });
            var consulta = Consulta(tabla);
            consulta.Criteria.AddCondition(columna, ConditionOperator.NotEqual, ValoresDeEjemplo.ComoValorDeCondicion(valor));

            Assert.Empty(new OrganizationServiceEnMemoria().RetrieveMultiple(consulta).Entities);
            Assert.Equal(new[] { sinLaColumna }, almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).ToArray());
        }

        // ---- Varios valores ----

        [Theory]
        [InlineData(ConditionOperator.Equal)]
        [InlineData(ConditionOperator.NotEqual)]
        [InlineData(ConditionOperator.GreaterThan)]
        [InlineData(ConditionOperator.GreaterEqual)]
        [InlineData(ConditionOperator.LessThan)]
        [InlineData(ConditionOperator.LessEqual)]
        public void PA_CIN_06_UnOperadorDeUnSoloValorConVariosValoresFalla0x80040203_NoIgnoraLosDemas(ConditionOperator operador)
        {
            // lote 3b, medido en Dev (12-operadores-y-filtros.json punto_3): 'The ConditonOperator.<Op> requires 1 value/s, not N' => 0x80040203. Antes InvalidOperationException "por prudencia".
            var consulta = Consulta();
            consulta.Criteria.AddCondition("zzf_estado", ConditionOperator.NotNull);
            consulta.Criteria.AddCondition("zzf_estado", operador, 1, 2);

            var error = Falla(() => ConTodasConEstado().RetrieveMultiple(consulta));

            ApoyoMedido.AssertFalla(error, CodigosDeFalla.SolicitudInvalida, "The ConditonOperator." + operador + " requires 1 value/s, not 2. Parameter Name: zzf_estado");
        }

        [Fact]
        public void PA_CIN_06b_InAceptaVariosValoresYEqualUnoSolo_VERDE_HOY()
        {
            var conIn = Consulta();
            conIn.Criteria.AddCondition("zzf_estado", ConditionOperator.In, 1, 2);
            var conEqual = Consulta();
            conEqual.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 2);

            Assert.Equal(3, ConTodasConEstado().RetrieveMultiple(conIn).Entities.Count);
            Assert.Equal(new[] { Fila2 }, ConTodasConEstado().RetrieveMultiple(conEqual).Entities.Select(e => e.Id).ToArray());
        }

        // ---- Subfiltro vacío ----

        [Theory]
        [InlineData(LogicalOperator.Or)]
        [InlineData(LogicalOperator.And)]
        public void PA_CIN_07_UnSubfiltroVacioSeIgnoraVengaDondeVengaYLaFilaNoImporta(LogicalOperator operadorDelPadre)
        {
            // lote 3b, medido en Dev: INVERTIDA (12-operadores-y-filtros.json punto_5: el subfiltro vacío se ignora; ni verdadero ni falso). Antes lanzaba InvalidOperationException, también con la tabla vacía.
            var directo = Consulta();
            directo.Criteria.FilterOperator = operadorDelPadre;
            directo.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 1);
            directo.Criteria.AddFilter(LogicalOperator.And);
            var profundo = Consulta();
            profundo.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 1);
            profundo.Criteria.AddFilter(operadorDelPadre).AddFilter(LogicalOperator.Or);

            Assert.Empty(new OrganizationServiceEnMemoria().RetrieveMultiple(directo).Entities);
            Assert.Equal(new[] { Fila1, Fila3 }, ConTodasConEstado().RetrieveMultiple(directo).Entities.Select(e => e.Id).OrderBy(i => i).ToArray());
            Assert.Empty(new OrganizationServiceEnMemoria().RetrieveMultiple(profundo).Entities);
            Assert.Equal(new[] { Fila1, Fila3 }, ConTodasConEstado().RetrieveMultiple(profundo).Entities.Select(e => e.Id).OrderBy(i => i).ToArray());
        }

        [Fact]
        public void PA_CIN_07b_LaRaizVaciaEsUnaConsultaSinCriterio_YUnSubfiltroConCondicionesEsValido_VERDE_HOY()
        {
            var sinCriterio = Consulta();
            var conSubfiltro = Consulta();
            conSubfiltro.Criteria.AddFilter(LogicalOperator.Or).AddCondition("zzf_estado", ConditionOperator.Equal, 2);

            Assert.Equal(3, ConTodasConEstado().RetrieveMultiple(sinCriterio).Entities.Count);
            Assert.Equal(new[] { Fila2 }, ConTodasConEstado().RetrieveMultiple(conSubfiltro).Entities.Select(e => e.Id).ToArray());
        }

        // ---- Paginación ----

        [Theory]
        [InlineData(5000, false)]
        [InlineData(5001, true)]
        [InlineData(100000, true)]
        public void PA_CIN_08_UnTamanoDePaginaMayorQue5000Lanza_ElMaximoDeLearnEs5000(int cuenta, bool lanza)
        {
            var consulta = Consulta();
            consulta.PageInfo = new PagingInfo { Count = cuenta, PageNumber = 1 };

            if (lanza)
            {
                var error = Assert.Throws<InvalidOperationException>(() => new OrganizationServiceEnMemoria().RetrieveMultiple(consulta));
                Assert.Contains("5000", error.Message, StringComparison.Ordinal);
                Assert.Contains("PageInfo", error.Message, StringComparison.Ordinal);
            }
            else
            {
                Assert.Empty(new OrganizationServiceEnMemoria().RetrieveMultiple(consulta).Entities);
            }
        }

        [Fact]
        public void PA_CIN_09_TopCountJuntoConPageInfoLanza_LearnDiceQueNoSonCompatibles_PeroTopCountConElPageInfoPorDefectoEsValido()
        {
            var incompatible = Consulta();
            incompatible.TopCount = 5;
            incompatible.PageInfo = new PagingInfo { Count = 2, PageNumber = 1 };
            var soloTop = Consulta();
            soloTop.TopCount = 2;

            var error = Assert.Throws<InvalidOperationException>(() => ConTodasConEstado().RetrieveMultiple(incompatible));

            Assert.Contains("TopCount", error.Message, StringComparison.Ordinal);
            Assert.Contains("PageInfo", error.Message, StringComparison.Ordinal);
            Assert.Equal(2, ConTodasConEstado().RetrieveMultiple(soloTop).Entities.Count);
        }

        [Fact]
        public void PA_CIN_10_LaCookieEntranteNoSeExige_LaPaginaDosSinCookieEsLaMismaQueConLaCookieDeLaPaginaUno_VERDE_HOY()
        {
            // Decisión: Learn recomienda la cookie pero no la exige (sin ella cae a paginación simple). El doble pagina por PageNumber y no usa la cookie entrante.
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Enumerable.Range(1, 5).Select(i => Zzf(Guid.NewGuid(), "zzf_numero", i)).ToArray());
            QueryExpression Pagina(int numero, string cookie)
            {
                var consulta = Consulta();
                consulta.AddOrder("zzf_numero", OrderType.Ascending);
                consulta.PageInfo = new PagingInfo { Count = 2, PageNumber = numero, PagingCookie = cookie };
                return consulta;
            }

            var primera = almacen.RetrieveMultiple(Pagina(1, null));
            var conCookie = almacen.RetrieveMultiple(Pagina(2, primera.PagingCookie));
            var sinCookie = almacen.RetrieveMultiple(Pagina(2, null));

            Assert.Equal(conCookie.Entities.Select(e => e.Id).ToArray(), sinCookie.Entities.Select(e => e.Id).ToArray());
            Assert.Equal(2, sinCookie.Entities.Count);
            Assert.Equal("2", XElement.Parse(sinCookie.PagingCookie).Attribute("page").Value);
        }
    }
}
