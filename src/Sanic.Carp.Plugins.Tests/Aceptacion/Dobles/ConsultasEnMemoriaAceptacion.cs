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
    /// <c>RetrieveMultiple</c> con <c>QueryExpression</c> (diseno/03 4.5): condiciones <c>Equal</c>, <c>NotEqual</c>, <c>In</c>, <c>Null</c>,
    /// <c>NotNull</c>, <c>GreaterThan</c>, <c>GreaterEqual</c>, <c>LessThan</c>, <c>LessEqual</c>; <c>OrderExpression</c>; <c>TopCount</c>;
    /// <c>PageInfo</c> con cookie; sin joins. Qué esperar de cada caso, y por qué:
    /// <list type="bullet">
    /// <item><b>Tipos al comparar</b>: un <c>OptionSetValue</c> guardado se compara con el <c>int</c> de la condición, un <c>EntityReference</c> con el
    /// <c>Guid</c>, un <c>decimal</c> por valor (escala 2 de la condición contra escala 10 guardada), un <c>DateTime</c> con <c>Unspecified</c>
    /// como UTC y <c>Local</c> pasado a UTC, y el texto sin distinguir mayúsculas (la plataforma compara con la colación de la base: la cola
    /// del buzón se busca "sin distinguir mayúsculas", 0.4 punto 5; NO verificado con una muestra, y el doble no emula la insensibilidad a acentos).</item>
    /// <item><b>Una columna ausente</b> no cumple <c>Equal</c>, <c>In</c> ni ninguna comparación; sí cumple <c>Null</c>; no cumple <c>NotNull</c>.
    /// <c>NotEqual</c> sobre una fila donde la columna está ausente la <b>INCLUYE</b> (lote 3b, medido en Dev: <c>12-operadores-y-filtros.json</c> punto_4; antes lanzaba <see cref="InvalidOperationException"/>).</item>
    /// </list>
    /// </summary>
    public class ConsultasEnMemoriaAceptacion
    {
        private static readonly Guid F4 = new Guid("cccccccc-0000-4000-8000-000000000004");
        private static readonly Guid F5 = new Guid("cccccccc-0000-4000-8000-000000000005");
        private static readonly Guid F6 = new Guid("cccccccc-0000-4000-8000-000000000006");
        private static readonly Guid Team2 = new Guid("bbbbbbbb-0000-4000-8000-000000000002");

        private static DateTime Dia(int dia)
        {
            return new DateTime(2026, 10, dia, 15, 0, 0, DateTimeKind.Utc);
        }

        /// <summary>
        /// Seis filas, en este orden de entrada (la letra "R" es de la fila, no del orden):
        /// F1 numero 1, "Alfa", 10, día 1, opción 1, team 1, true, estado 1 ·
        /// F2 numero 2, "beta", 20,5, día 2, opción 2, team 2, false, estado 1 ·
        /// F3 numero 3, "Gamma", 30, día 3, opción 1, sin team, true, estado 2 ·
        /// F4 numero 3, estado 1 (todo lo demás ausente) ·
        /// F5 numero 5, "alfa", 20,5, día 3, opción 3, estado 2 ·
        /// F6 "Delta", estado 2 (sin numero).
        /// </summary>
        private static OrganizationServiceEnMemoria Sembrado()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(
                Zzf(Fila1, "zzf_numero", 1, "zzf_texto", "Alfa", "zzf_monto", 10.00m, "zzf_fecha", Dia(1), "zzf_opcion", new OptionSetValue(1),
                    "zzf_ref", new EntityReference("team", Team1), "zzf_flag", true, "zzf_estado", new OptionSetValue(1)),
                Zzf(Fila2, "zzf_numero", 2, "zzf_texto", "beta", "zzf_monto", 20.5m, "zzf_fecha", Dia(2), "zzf_opcion", new OptionSetValue(2),
                    "zzf_ref", new EntityReference("team", Team2), "zzf_flag", false, "zzf_estado", new OptionSetValue(1)),
                Zzf(Fila3, "zzf_numero", 3, "zzf_texto", "Gamma", "zzf_monto", 30m, "zzf_fecha", Dia(3), "zzf_opcion", new OptionSetValue(1),
                    "zzf_flag", true, "zzf_estado", new OptionSetValue(2)),
                Zzf(F4, "zzf_numero", 3, "zzf_estado", new OptionSetValue(1)),
                Zzf(F5, "zzf_numero", 5, "zzf_texto", "alfa", "zzf_monto", 20.50m, "zzf_fecha", Dia(3), "zzf_opcion", new OptionSetValue(3),
                    "zzf_estado", new OptionSetValue(2)),
                Zzf(F6, "zzf_texto", "Delta", "zzf_estado", new OptionSetValue(2)));
            return almacen;
        }

        private static QueryExpression Donde(string columna, ConditionOperator operador, params object[] valores)
        {
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition(columna, operador, valores);
            return consulta;
        }

        /// <summary>
        /// Los ids del resultado. <b>Vuelta 2 (hallazgo 4):</b> sin <c>OrderExpression</c> el orden del doble es adversario (inverso al de entrada, ver
        /// <c>OrdenYConsultasInvalidasAceptacion</c>) y el código no puede depender de él; por eso, cuando la consulta no ordena, esta ayuda devuelve los ids
        /// ORDENADOS (las pruebas de esta clase miden QUÉ filas salen, no en qué orden). Con <c>OrderExpression</c> devuelve el orden del doble tal cual.
        /// </summary>
        private static Guid[] Ids(OrganizationServiceEnMemoria almacen, QueryExpression consulta)
        {
            var ids = almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id);
            return consulta.Orders.Count == 0 ? ids.OrderBy(i => i).ToArray() : ids.ToArray();
        }

        /// <summary>
        /// <c>NotEqual</c> escrito de modo que NO dependa de los datos (vuelta 2, hallazgo 6): el mismo And lleva <c>NotNull</c> sobre la misma columna, que excluye las filas
        /// donde está ausente. Sin esa condición el doble lanza por la FORMA de la consulta, haya o no filas con la columna ausente.
        /// </summary>
        private static QueryExpression NoIgualYPresente(string columna, object valor)
        {
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition(columna, ConditionOperator.NotNull);
            consulta.Criteria.AddCondition(columna, ConditionOperator.NotEqual, valor);
            return consulta;
        }

        // ---- Equal ----

        [Fact]
        public void PA_QRY_01_EqualSobreUnEnteroEncuentraLasFilasQueLoTienenYNoLasAusentes()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { Fila3, F4 }, Ids(almacen, Donde("zzf_numero", ConditionOperator.Equal, 3)));
            Assert.Empty(Ids(almacen, Donde("zzf_numero", ConditionOperator.Equal, 99)));
            Assert.Empty(Ids(almacen, Donde("zzf_numero", ConditionOperator.Equal, 0)));
        }

        [Fact]
        public void PA_QRY_01b_EqualCompara_DecimalPorValor_TextoSinMayusculas_BoolOpcionConEnteroReferenciaConGuid_FechaYId()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { Fila2, F5 }, Ids(almacen, Donde("zzf_monto", ConditionOperator.Equal, 20.5m)));
            Assert.Equal(new[] { Fila2, F5 }, Ids(almacen, Donde("zzf_monto", ConditionOperator.Equal, 20.50m)));
            Assert.Equal(new[] { Fila1, F5 }, Ids(almacen, Donde("zzf_texto", ConditionOperator.Equal, "alfa")));
            Assert.Equal(new[] { Fila1, F5 }, Ids(almacen, Donde("zzf_texto", ConditionOperator.Equal, "ALFA")));
            Assert.Equal(new[] { Fila1, Fila3 }, Ids(almacen, Donde("zzf_flag", ConditionOperator.Equal, true)));
            Assert.Equal(new[] { Fila2 }, Ids(almacen, Donde("zzf_flag", ConditionOperator.Equal, false)));
            Assert.Equal(new[] { Fila1, Fila3 }, Ids(almacen, Donde("zzf_opcion", ConditionOperator.Equal, 1)));
            Assert.Equal(new[] { Fila1 }, Ids(almacen, Donde("zzf_ref", ConditionOperator.Equal, Team1)));
            Assert.Equal(new[] { Fila3, F5 }, Ids(almacen, Donde("zzf_fecha", ConditionOperator.Equal, Dia(3))));
            Assert.Equal(new[] { Fila2 }, Ids(almacen, Donde("zzf_filaid", ConditionOperator.Equal, Fila2)));
            Assert.Empty(Ids(almacen, Donde("zzf_texto", ConditionOperator.Equal, "Zeta")));
        }

        [Fact]
        public void PA_QRY_01c_UnaFechaSinKindCuentaComoUtcYUnaLocalSePasaAUtcAntesDeComparar()
        {
            var almacen = Sembrado();
            var sinKind = DateTime.SpecifyKind(Dia(3), DateTimeKind.Unspecified);
            var local = Dia(3).ToLocalTime();

            Assert.Equal(new[] { Fila3, F5 }, Ids(almacen, Donde("zzf_fecha", ConditionOperator.Equal, sinKind)));
            Assert.Equal(new[] { Fila3, F5 }, Ids(almacen, Donde("zzf_fecha", ConditionOperator.Equal, local)));
            Assert.Equal(new[] { Fila3, F5 }, Ids(almacen, Donde("zzf_fecha", ConditionOperator.GreaterEqual, local)));
        }

        // ---- NotEqual ----

        [Fact]
        public void PA_QRY_02_NotEqualSobreUnaColumnaSiempreLlenaExcluyeLasIguales()
        {
            var almacen = Sembrado();

            // Vuelta 2: NotEqual lleva NotNull sobre la misma columna (si no, lanza por la forma de la consulta: PA_CIN_01).
            Assert.Equal(new[] { Fila3, F5, F6 }, Ids(almacen, NoIgualYPresente("zzf_estado", 1)));
            Assert.Equal(new[] { Fila1, Fila2, F4 }, Ids(almacen, NoIgualYPresente("zzf_estado", 2)));
        }

        [Theory]
        [InlineData("zzf_numero", 3)]
        [InlineData("zzf_texto", "ALFA")]
        public void PA_QRY_02b_NotEqualSobreUnaFilaDondeLaColumnaEstaAusenteLaIncluye(string columna, object valor)
        {
            // lote 3b, medido en Dev: INVERTIDA (12-operadores-y-filtros.json, punto_4: 'NotEqual incluye los nulos'). Antes lanzaba InvalidOperationException "porque la plataforma no tiene respuesta verificada".
            var almacen = Sembrado();

            var ids = Ids(almacen, Donde(columna, ConditionOperator.NotEqual, valor));

            var esperados = columna == "zzf_numero" ? new[] { Fila1, Fila2, F5, F6 } : new[] { Fila2, Fila3, F4, F6 };   // F6 no tiene numero; F4 no tiene texto
            Assert.Equal(esperados.OrderBy(i => i).ToArray(), ids);
        }

        [Fact]
        public void PA_QRY_02c_NotEqualCompraTextoSinMayusculas()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_texto", "Alfa"), Zzf(Fila2, "zzf_texto", "beta"));

            Assert.Equal(new[] { Fila2 }, Ids(almacen, NoIgualYPresente("zzf_texto", "ALFA")));
        }

        // ---- Comparaciones ----

        [Fact]
        public void PA_QRY_03_MayorYMenorSobreEnteros_LasFilasAusentesNuncaCumplen()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { Fila3, F4, F5 }, Ids(almacen, Donde("zzf_numero", ConditionOperator.GreaterThan, 2)));
            Assert.Equal(new[] { Fila3, F4, F5 }, Ids(almacen, Donde("zzf_numero", ConditionOperator.GreaterEqual, 3)));
            Assert.Equal(new[] { Fila1, Fila2 }, Ids(almacen, Donde("zzf_numero", ConditionOperator.LessThan, 3)));
            Assert.Equal(new[] { Fila1, Fila2, Fila3, F4 }, Ids(almacen, Donde("zzf_numero", ConditionOperator.LessEqual, 3)));
        }

        [Fact]
        public void PA_QRY_03b_MayorYMenorSobreDecimalesComparanPorValorSinImportarLaEscala()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { Fila3 }, Ids(almacen, Donde("zzf_monto", ConditionOperator.GreaterThan, 20.50m)));
            Assert.Equal(new[] { Fila2, Fila3, F5 }, Ids(almacen, Donde("zzf_monto", ConditionOperator.GreaterEqual, 20.5m)));
            Assert.Equal(new[] { Fila1 }, Ids(almacen, Donde("zzf_monto", ConditionOperator.LessThan, 20.5m)));
            Assert.Equal(new[] { Fila1, Fila2, F5 }, Ids(almacen, Donde("zzf_monto", ConditionOperator.LessEqual, 20.5000m)));
        }

        [Fact]
        public void PA_QRY_03c_MayorYMenorSobreFechasTextoYOpciones()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { Fila2, Fila3, F5 }, Ids(almacen, Donde("zzf_fecha", ConditionOperator.GreaterEqual, Dia(2))));
            Assert.Equal(new[] { Fila1, Fila2 }, Ids(almacen, Donde("zzf_fecha", ConditionOperator.LessThan, Dia(3))));
            Assert.Equal(new[] { Fila2, Fila3, F6 }, Ids(almacen, Donde("zzf_texto", ConditionOperator.GreaterEqual, "BETA")));
            Assert.Equal(new[] { Fila2, F5 }, Ids(almacen, Donde("zzf_opcion", ConditionOperator.GreaterThan, 1)));
            Assert.Equal(new[] { Fila1, Fila2, Fila3 }, Ids(almacen, Donde("zzf_opcion", ConditionOperator.LessEqual, 2)));
        }

        // ---- In ----

        [Fact]
        public void PA_QRY_04_InAceptaLosValoresSueltosOUnaColeccion_ConLosMismosTiposQueEqual()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { Fila1, F5 }, Ids(almacen, Donde("zzf_numero", ConditionOperator.In, 1, 5)));
            Assert.Equal(new[] { Fila1, Fila3, F5 }, Ids(almacen, Donde("zzf_texto", ConditionOperator.In, "ALFA", "gamma")));
            Assert.Equal(new[] { Fila2, Fila3 }, Ids(almacen, Donde("zzf_filaid", ConditionOperator.In, Fila3, Fila2)));
            Assert.Equal(new[] { Fila2, F5 }, Ids(almacen, Donde("zzf_opcion", ConditionOperator.In, 2, 3)));
            Assert.Equal(new[] { Fila1, Fila2 }, Ids(almacen, Donde("zzf_ref", ConditionOperator.In, Team2, Team1)));
            Assert.Equal(new[] { Fila1, Fila3 }, Ids(almacen, Donde("zzf_monto", ConditionOperator.In, 10m, 30.000m)));

            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("zzf_filaid", ConditionOperator.In, new List<Guid> { Fila3, F6, Guid.NewGuid() });
            Assert.Equal(new[] { Fila3, F6 }, Ids(almacen, consulta));
        }

        [Fact]
        public void PA_QRY_04b_InNoEncuentraLasFilasConLaColumnaAusente()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { Fila1, Fila2, Fila3, F5 }, Ids(almacen, Donde("zzf_texto", ConditionOperator.In, "alfa", "beta", "gamma")));
            Assert.Empty(Ids(almacen, Donde("zzf_texto", ConditionOperator.In, "zeta")));
        }

        // ---- Null y NotNull ----

        [Fact]
        public void PA_QRY_05_NullEncuentraLasColumnasAusentesYNotNullLasPresentes_AunUnFalseOUnCeroEstanPresentes()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { F4 }, Ids(almacen, Donde("zzf_texto", ConditionOperator.Null)));
            Assert.Equal(new[] { F6 }, Ids(almacen, Donde("zzf_numero", ConditionOperator.Null)));
            Assert.Equal(new[] { Fila3, F4, F5, F6 }, Ids(almacen, Donde("zzf_ref", ConditionOperator.Null)));
            Assert.Equal(new[] { F4, F5, F6 }, Ids(almacen, Donde("zzf_flag", ConditionOperator.Null)));
            Assert.Equal(new[] { Fila1, Fila2, Fila3, F4, F5 }, Ids(almacen, Donde("zzf_numero", ConditionOperator.NotNull)));
            Assert.Equal(new[] { Fila1, Fila2, Fila3 }, Ids(almacen, Donde("zzf_flag", ConditionOperator.NotNull)));
        }

        // ---- Filtros ----

        /// <summary>
        /// REEMPLAZA a <c>PA_QRY_06_SinCriterioDevuelveTodasLasFilasEnOrdenDeEntrada</c> (vuelta 2, hallazgo 4, 2026-10-14): esa prueba exigía el orden de entrada y
        /// un doble que devuelve siempre el orden de inserción deja pasar un <c>.First()</c> sin orden que en Dev falla (QueryExpression no agrega un orden por
        /// la clave primaria, a diferencia de FetchXML: Learn, "Page results using QueryExpression", sección "Ordering and paging"). El orden con
        /// <c>OrderExpression</c> ordena por sus columnas y los EMPATES van en orden inverso al de entrada (PA_QRY_07*, reescritas); el del doble sin orden es el INVERSO al de entrada: <c>OrdenYConsultasInvalidasAceptacion</c>.
        /// </summary>
        [Fact]
        public void PA_QRY_06_SinCriterioDevuelveTodasLasFilasSinPerderNingunaYSinDependerDelOrden()
        {
            Assert.Equal(new[] { Fila1, Fila2, Fila3, F4, F5, F6 }, Ids(Sembrado(), new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() }));
        }

        [Fact]
        public void PA_QRY_06b_LasCondicionesDeUnFiltroAndSeCumplenTodasYLasDeUnOrAlMenosUna()
        {
            var almacen = Sembrado();

            var y = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            y.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 2);
            y.Criteria.AddCondition("zzf_numero", ConditionOperator.NotNull);
            Assert.Equal(new[] { Fila3, F5 }, Ids(almacen, y));

            var o = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            o.Criteria.FilterOperator = LogicalOperator.Or;
            o.Criteria.AddCondition("zzf_numero", ConditionOperator.Equal, 1);
            o.Criteria.AddCondition("zzf_texto", ConditionOperator.Equal, "Delta");
            Assert.Equal(new[] { Fila1, F6 }, Ids(almacen, o));
        }

        [Fact]
        public void PA_QRY_06c_LosFiltrosAnidadosSeEvaluanCadaUnoConSuOperador()
        {
            var almacen = Sembrado();

            // estado = 2 Y (numero = 3 O numero = 5)
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 2);
            var o = consulta.Criteria.AddFilter(LogicalOperator.Or);
            o.AddCondition("zzf_numero", ConditionOperator.Equal, 3);
            o.AddCondition("zzf_numero", ConditionOperator.Equal, 5);
            Assert.Equal(new[] { Fila3, F5 }, Ids(almacen, consulta));

            // (texto es nulo O flag = true) Y estado = 1
            var otra = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            var ramaO = otra.Criteria.AddFilter(LogicalOperator.Or);
            ramaO.AddCondition("zzf_texto", ConditionOperator.Null);
            ramaO.AddCondition("zzf_flag", ConditionOperator.Equal, true);
            otra.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 1);
            Assert.Equal(new[] { Fila1, F4 }, Ids(almacen, otra));

            // Y dentro de O dentro de Y
            var profunda = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            var nivel1 = profunda.Criteria.AddFilter(LogicalOperator.Or);
            var nivel2 = nivel1.AddFilter(LogicalOperator.And);
            nivel2.AddCondition("zzf_estado", ConditionOperator.Equal, 2);
            nivel2.AddCondition("zzf_flag", ConditionOperator.Equal, true);
            nivel1.AddCondition("zzf_numero", ConditionOperator.Equal, 1);
            Assert.Equal(new[] { Fila1, Fila3 }, Ids(almacen, profunda));
        }

        // ---- Orden ----

        private static QueryExpression Ordenada(params (string columna, OrderType tipo)[] ordenes)
        {
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            foreach (var orden in ordenes) consulta.AddOrder(orden.columna, orden.tipo);
            return consulta;
        }

        [Fact]
        public void PA_QRY_07_OrdenAscendenteDejaLasAusentesPrimeroYDescendenteLasDejaUltimas_ConEmpatesEnOrdenInversoAlDeEntrada()
        {
            var almacen = Sembrado();

            // REESCRITA en la vuelta 2 (decisión del orquestador sobre la tensión 1): los empates (Fila3 y F4 tienen numero 3) van en orden INVERSO al de entrada, como sin orden:
            // la plataforma no garantiza el orden de los empates (Learn, "Page results using QueryExpression": "Orders that don't include unique identifiers" es un anti-patrón y sin un
            // identificador único "there's no guarantee that the other two records on the first page aren't included in the second page"). Antes: { F6, Fila1, Fila2, Fila3, F4, F5 } y { F5, Fila3, F4, ... }.
            Assert.Equal(new[] { F6, Fila1, Fila2, F4, Fila3, F5 }, Ids(almacen, Ordenada(("zzf_numero", OrderType.Ascending))));
            Assert.Equal(new[] { F5, F4, Fila3, Fila2, Fila1, F6 }, Ids(almacen, Ordenada(("zzf_numero", OrderType.Descending))));
        }

        [Fact]
        public void PA_QRY_07b_ElOrdenDeTextoNoDistingueMayusculas()
        {
            var almacen = Sembrado();

            // REESCRITA en la vuelta 2: "Alfa" (Fila1) y "alfa" (F5) empatan sin distinguir mayúsculas y el empate va en orden inverso al de entrada (antes: F4, Fila1, F5, ...).
            Assert.Equal(new[] { F4, F5, Fila1, Fila2, F6, Fila3 }, Ids(almacen, Ordenada(("zzf_texto", OrderType.Ascending))));
        }

        [Fact]
        public void PA_QRY_07c_VariasColumnasDeOrdenSeAplicanEnSecuencia()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { Fila3, F5, Fila2, Fila1, F6, F4 }, Ids(almacen, Ordenada(("zzf_fecha", OrderType.Descending), ("zzf_numero", OrderType.Ascending))));
        }

        [Fact]
        public void PA_QRY_07d_ElOrdenSePuedeHacerPorDecimalFechaYOpcion()
        {
            var almacen = Sembrado();

            // REESCRITA en la vuelta 2: empatan las ausentes (F4, F6) y 20,5 (Fila2, F5); cada empate va en orden inverso al de entrada (antes: F4, F6, Fila1, Fila2, F5, Fila3).
            Assert.Equal(new[] { F6, F4, Fila1, F5, Fila2, Fila3 }, Ids(almacen, Ordenada(("zzf_monto", OrderType.Ascending))));
            Assert.Equal(new[] { F5, Fila2, Fila3, Fila1, F4, F6 }, Ids(almacen, Ordenada(("zzf_opcion", OrderType.Descending), ("zzf_numero", OrderType.Descending))));
        }

        // ---- TopCount ----

        [Fact]
        public void PA_QRY_08_TopCountSeAplicaDespuesDelOrdenYDelCriterio()
        {
            var almacen = Sembrado();

            var mayores = Ordenada(("zzf_numero", OrderType.Descending));
            mayores.TopCount = 2;
            // REESCRITA en la vuelta 2: tras F5 (5) empatan Fila3 y F4 (3) y el empate va en orden inverso al de entrada (antes: F5, Fila3).
            Assert.Equal(new[] { F5, F4 }, Ids(almacen, mayores));

            var conCriterio = Ordenada(("zzf_numero", OrderType.Ascending));
            conCriterio.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 2);
            conCriterio.TopCount = 2;
            Assert.Equal(new[] { F6, Fila3 }, Ids(almacen, conCriterio));

            var demasiadas = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet(), TopCount = 50 };
            Assert.Equal(6, Ids(almacen, demasiadas).Length);
        }

        [Fact]
        public void PA_QRY_08b_ConTopCountLaCookieVieneVaciaYNoHayMasRegistros()
        {
            var almacen = Sembrado();
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet(), TopCount = 2 };

            var resultado = almacen.RetrieveMultiple(consulta);

            Assert.Equal(2, resultado.Entities.Count);
            Assert.False(resultado.MoreRecords);
            Assert.Equal(string.Empty, resultado.PagingCookie);
            Assert.Equal(-1, resultado.TotalRecordCount);
        }

        // ---- Columnas ----

        [Fact]
        public void PA_QRY_09_ElResultadoTraeSoloLasColumnasPedidasMasElIdMasLasDelOrden_NuncaLasDelCriterio()
        {
            var almacen = Sembrado();
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet("zzf_texto") };
            consulta.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 2);
            consulta.AddOrder("zzf_numero", OrderType.Ascending);

            var filas = almacen.RetrieveMultiple(consulta).Entities;

            Assert.Equal(new[] { F6, Fila3, F5 }, filas.Select(f => f.Id).ToArray());
            string[] Claves(Entity f) { return f.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(); }
            Assert.Equal(new[] { "zzf_filaid", "zzf_texto" }, Claves(filas[0]));
            Assert.Equal(new[] { "zzf_filaid", "zzf_numero", "zzf_texto" }, Claves(filas[1]));
            Assert.Equal(new[] { "zzf_filaid", "zzf_numero", "zzf_texto" }, Claves(filas[2]));
        }

        [Fact]
        public void PA_QRY_09b_ConColumnSetVacioSoloTraeElId_YConTodasTraeTodasLasQueTieneCadaFila()
        {
            var almacen = Sembrado();

            var soloId = almacen.RetrieveMultiple(new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() }).Entities;
            Assert.All(soloId, f => Assert.Equal(new[] { "zzf_filaid" }, f.Attributes.Keys.ToArray()));

            var todas = almacen.RetrieveMultiple(new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet(true) }).Entities;
            // Vuelta 2: sin OrderExpression el orden es adversario; se busca cada fila por su id.
            Assert.Equal(9, todas.Single(e => e.Id == Fila1).Attributes.Count);
            Assert.Equal(3, todas.Single(e => e.Id == F4).Attributes.Count);
            Assert.All(todas, f => Assert.DoesNotContain(f.Attributes, par => par.Value == null));
        }

        // ---- Paginación ----

        private static QueryExpression Pagina(int cantidad, int numero, string cookie = null, bool total = false)
        {
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.AddOrder("zzf_numero", OrderType.Ascending);
            consulta.PageInfo = new PagingInfo { Count = cantidad, PageNumber = numero, PagingCookie = cookie, ReturnTotalRecordCount = total };
            return consulta;
        }

        [Fact]
        public void PA_QRY_10_LasPaginasRepartenTodasLasFilasSinRepetirNiPerder_YMoreRecordsSoloEsVerdaderoSiHayMas()
        {
            var almacen = Sembrado();
            string cookie = null;
            var vistas = new List<Guid>();
            var banderas = new List<bool>();

            for (int pagina = 1; pagina <= 3; pagina++)
            {
                var resultado = almacen.RetrieveMultiple(Pagina(2, pagina, cookie));
                vistas.AddRange(resultado.Entities.Select(e => e.Id));
                banderas.Add(resultado.MoreRecords);
                cookie = resultado.PagingCookie;
            }

            // REESCRITA en la vuelta 2: el empate Fila3/F4 (numero 3) va en orden inverso al de entrada, también al repartirse entre páginas (antes: ..., Fila3, F4, F5).
            Assert.Equal(new[] { F6, Fila1, Fila2, F4, Fila3, F5 }, vistas.ToArray());
            Assert.Equal(new[] { true, true, false }, banderas.ToArray());
        }

        [Fact]
        public void PA_QRY_10b_UnaPaginaExactaNoDejaMasRegistros_YUnaPaginaPasadaDelFinalVieneVaciaConCookieVacia()
        {
            var almacen = Sembrado();

            var exacta = almacen.RetrieveMultiple(Pagina(6, 1));
            Assert.Equal(6, exacta.Entities.Count);
            Assert.False(exacta.MoreRecords);

            var pasada = almacen.RetrieveMultiple(Pagina(2, 4));
            Assert.Empty(pasada.Entities);
            Assert.False(pasada.MoreRecords);
            Assert.Equal(string.Empty, pasada.PagingCookie);
            Assert.Equal(TablaZzf, pasada.EntityName);
        }

        [Fact]
        public void PA_QRY_10c_ElTotalSoloSeCalculaSiSePide_ElTotalEsDeTodoElCriterioNoDeLaPagina()
        {
            var almacen = Sembrado();

            Assert.Equal(-1, almacen.RetrieveMultiple(Pagina(2, 1)).TotalRecordCount);
            var conTotal = almacen.RetrieveMultiple(Pagina(2, 2, null, total: true));
            Assert.Equal(6, conTotal.TotalRecordCount);
            Assert.False(conTotal.TotalRecordCountLimitExceeded);

            var filtrada = Pagina(1, 1, null, total: true);
            filtrada.Criteria.AddCondition("zzf_estado", ConditionOperator.Equal, 2);
            Assert.Equal(3, almacen.RetrieveMultiple(filtrada).TotalRecordCount);
        }

        [Fact]
        public void PA_QRY_10d_ElNumeroDePaginaCeroEsLaPrimeraYUnaCuentaCeroEsSinPaginar()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { F6, Fila1 }, Ids(almacen, Pagina(2, 0)));
            Assert.Equal(6, Ids(almacen, Pagina(0, 0)).Length);
        }

        [Fact]
        public void PA_QRY_10e_LaCookieEsXmlConElNumeroDePaginaYSeAvanzaDeUnaEnUna()
        {
            var almacen = Sembrado();
            string cookie = null;

            for (int pagina = 1; pagina <= 3; pagina++)
            {
                var resultado = almacen.RetrieveMultiple(Pagina(2, pagina, cookie));
                var raiz = XElement.Parse(resultado.PagingCookie);
                Assert.Equal("cookie", raiz.Name.LocalName);
                Assert.Equal(pagina.ToString(System.Globalization.CultureInfo.InvariantCulture), raiz.Attribute("page").Value);
                var primario = raiz.Element("zzf_filaid");
                Assert.NotNull(primario);
                Assert.Equal("{" + resultado.Entities.First().Id + "}", primario.Attribute("first").Value);
                Assert.Equal("{" + resultado.Entities.Last().Id + "}", primario.Attribute("last").Value);
                Assert.NotNull(raiz.Element("zzf_numero"));
                cookie = resultado.PagingCookie;
            }
        }

        [Fact]
        public void PA_QRY_10f_SinPageInfoNiTopCountLaPaginaEsDeCincoMilFilas_YHayMasSiHayMas()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var filas = new Entity[OrganizationServiceEnMemoria.FilasPorPaginaPorDefecto + 1];
            for (int i = 0; i < filas.Length; i++) filas[i] = Zzf(Guid.NewGuid(), "zzf_numero", i);
            almacen.Sembrar(filas);

            var sinPaginar = almacen.RetrieveMultiple(new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() });

            Assert.Equal(5000, sinPaginar.Entities.Count);
            Assert.True(sinPaginar.MoreRecords);
            Assert.False(string.IsNullOrEmpty(sinPaginar.PagingCookie));

            var segunda = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet(), PageInfo = new PagingInfo { Count = 5000, PageNumber = 2, PagingCookie = sinPaginar.PagingCookie } };
            var resto = almacen.RetrieveMultiple(segunda);
            Assert.Single(resto.Entities);
            Assert.False(resto.MoreRecords);
        }

        // ---- Lo que no se simula ----

        [Fact]
        public void PA_QRY_11_UnaConsultaConLinkEntitiesLanzaYNoDevuelveResultado()
        {
            var almacen = Sembrado();
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.AddLink("zzf_otra", "zzf_ref", "zzf_otraid", JoinOperator.Inner);

            var error = Assert.Throws<NotSupportedException>(() => almacen.RetrieveMultiple(consulta));

            Assert.Contains("LinkEntities", error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(ConditionOperator.Like, "a%")]
        [InlineData(ConditionOperator.NotLike, "a%")]
        [InlineData(ConditionOperator.BeginsWith, "a")]
        [InlineData(ConditionOperator.DoesNotBeginWith, "a")]
        [InlineData(ConditionOperator.EndsWith, "a")]
        [InlineData(ConditionOperator.DoesNotEndWith, "a")]
        [InlineData(ConditionOperator.Contains, "a")]
        [InlineData(ConditionOperator.DoesNotContain, "a")]
        [InlineData(ConditionOperator.NotIn, "a")]
        [InlineData(ConditionOperator.Today, null)]
        [InlineData(ConditionOperator.Last7Days, null)]
        [InlineData(ConditionOperator.OlderThanXMonths, 3)]
        public void PA_QRY_11b_UnOperadorQueElDobleNoSimulaLanzaConSuNombre(ConditionOperator operador, object valor)
        {
            var almacen = Sembrado();
            var consulta = valor == null ? Donde("zzf_texto", operador) : Donde("zzf_texto", operador, valor);

            var error = Assert.Throws<NotSupportedException>(() => almacen.RetrieveMultiple(consulta));

            Assert.Contains(operador.ToString(), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA_QRY_11c_FetchXmlYLosDemasTiposDeConsultaLanzan()
        {
            var almacen = Sembrado();

            Assert.Throws<NotSupportedException>(() => almacen.RetrieveMultiple(new FetchExpression("<fetch><entity name='" + TablaZzf + "'/></fetch>")));
            Assert.Throws<NotSupportedException>(() => almacen.RetrieveMultiple(new QueryByAttribute(TablaZzf)));
        }

        // ---- Resultado vacío ----

        [Fact]
        public void PA_QRY_12_UnaConsultaSinFilasDevuelveUnaColeccionVaciaConLaFormaDeLaPlataforma()
        {
            var almacen = Sembrado();

            foreach (var resultado in new[]
            {
                almacen.RetrieveMultiple(Donde("zzf_numero", ConditionOperator.Equal, 99)),
                almacen.RetrieveMultiple(new QueryExpression("zzf_no_sembrada") { ColumnSet = new ColumnSet(true) })
            })
            {
                Assert.Empty(resultado.Entities);
                Assert.False(resultado.MoreRecords);
                Assert.Equal(-1, resultado.TotalRecordCount);
                Assert.False(resultado.TotalRecordCountLimitExceeded);
                Assert.Equal("-1", resultado.MinActiveRowVersion);
                Assert.Equal(string.Empty, resultado.PagingCookie);
            }
            Assert.Equal(TablaZzf, almacen.RetrieveMultiple(Donde("zzf_numero", ConditionOperator.Equal, 99)).EntityName);
            Assert.Equal("zzf_no_sembrada", almacen.RetrieveMultiple(new QueryExpression("zzf_no_sembrada")).EntityName);
        }

        [Fact]
        public void PA_QRY_13_UnaConsultaSoloVeLasFilasDeSuTabla()
        {
            var almacen = Sembrado();
            almacen.Sembrar(Fila("zzf_otra", Fila1, "zzf_numero", 3));

            Assert.Equal(new[] { Fila1 }, almacen.RetrieveMultiple(new QueryExpression("zzf_otra") { ColumnSet = new ColumnSet() }).Entities.Select(e => e.Id).ToArray());
            Assert.Equal(6, Ids(almacen, new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() }).Length);
        }
    }
}
