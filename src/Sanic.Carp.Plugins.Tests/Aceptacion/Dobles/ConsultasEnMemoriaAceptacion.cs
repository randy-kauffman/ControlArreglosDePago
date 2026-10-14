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
    /// <c>NotEqual</c> sobre una fila donde la columna está ausente <b>lanza <see cref="InvalidOperationException"/></b>: la plataforma no
    /// tiene una respuesta verificada (SQL excluye los nulos, Dataverse puede incluirlos) y un doble que adivina deja pasar en el puesto un
    /// resultado que en Dev es otro (D-59). Hasta tener una muestra real, la consulta se escribe de modo que no dependa de eso.</item>
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

        private static Guid[] Ids(OrganizationServiceEnMemoria almacen, QueryExpression consulta)
        {
            return almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).ToArray();
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

            Assert.Equal(new[] { Fila3, F5, F6 }, Ids(almacen, Donde("zzf_estado", ConditionOperator.NotEqual, 1)));
            Assert.Equal(new[] { Fila1, Fila2, F4 }, Ids(almacen, Donde("zzf_estado", ConditionOperator.NotEqual, 2)));
        }

        [Theory]
        [InlineData("zzf_numero", 3)]
        [InlineData("zzf_texto", "ALFA")]
        public void PA_QRY_02b_NotEqualSobreUnaFilaDondeLaColumnaEstaAusenteLanzaPorqueLaPlataformaNoTieneRespuestaVerificada(string columna, object valor)
        {
            var almacen = Sembrado();

            var error = Assert.Throws<InvalidOperationException>(() => almacen.RetrieveMultiple(Donde(columna, ConditionOperator.NotEqual, valor)));

            Assert.Contains("NotEqual", error.Message, StringComparison.Ordinal);
            Assert.Contains(columna, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA_QRY_02c_NotEqualCompraTextoSinMayusculas()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_texto", "Alfa"), Zzf(Fila2, "zzf_texto", "beta"));

            Assert.Equal(new[] { Fila2 }, Ids(almacen, Donde("zzf_texto", ConditionOperator.NotEqual, "ALFA")));
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

        [Fact]
        public void PA_QRY_06_SinCriterioDevuelveTodasLasFilasEnOrdenDeEntrada()
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
        public void PA_QRY_07_OrdenAscendenteDejaLasAusentesPrimeroYDescendenteLasDejaUltimas_ConEmpatesEnOrdenDeEntrada()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { F6, Fila1, Fila2, Fila3, F4, F5 }, Ids(almacen, Ordenada(("zzf_numero", OrderType.Ascending))));
            Assert.Equal(new[] { F5, Fila3, F4, Fila2, Fila1, F6 }, Ids(almacen, Ordenada(("zzf_numero", OrderType.Descending))));
        }

        [Fact]
        public void PA_QRY_07b_ElOrdenDeTextoNoDistingueMayusculas()
        {
            var almacen = Sembrado();

            Assert.Equal(new[] { F4, Fila1, F5, Fila2, F6, Fila3 }, Ids(almacen, Ordenada(("zzf_texto", OrderType.Ascending))));
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

            Assert.Equal(new[] { F4, F6, Fila1, Fila2, F5, Fila3 }, Ids(almacen, Ordenada(("zzf_monto", OrderType.Ascending))));
            Assert.Equal(new[] { F5, Fila2, Fila3, Fila1, F4, F6 }, Ids(almacen, Ordenada(("zzf_opcion", OrderType.Descending), ("zzf_numero", OrderType.Descending))));
        }

        // ---- TopCount ----

        [Fact]
        public void PA_QRY_08_TopCountSeAplicaDespuesDelOrdenYDelCriterio()
        {
            var almacen = Sembrado();

            var mayores = Ordenada(("zzf_numero", OrderType.Descending));
            mayores.TopCount = 2;
            Assert.Equal(new[] { F5, Fila3 }, Ids(almacen, mayores));

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
            Assert.Equal(9, todas[0].Attributes.Count);
            Assert.Equal(3, todas[3].Attributes.Count);
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

            Assert.Equal(new[] { F6, Fila1, Fila2, Fila3, F4, F5 }, vistas.ToArray());
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
