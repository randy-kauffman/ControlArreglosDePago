using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoMedido;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Lote 3b: las consultas como las midió Dev</b> (<c>salida/10-condiciones-valores-tipados.json</c>, <c>12-operadores-y-filtros.json</c>, <c>18-texto-y-esquema.json</c> punto_c).
    /// Lo no medido sigue como estaba: <c>Like</c>, <c>BeginsWith</c> y <c>NotIn</c> lanzan <see cref="NotSupportedException"/> (límite declarado, 4.5).
    /// </summary>
    public class MedidoEnDevConsultasAceptacion
    {
        private static QueryExpression Donde(string tabla, string columna, ConditionOperator operador, params object[] valores)
        {
            var consulta = new QueryExpression(tabla) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition(columna, operador, valores);
            return consulta;
        }

        private static Guid[] Ids(OrganizationServiceEnMemoria almacen, QueryExpression consulta)
        {
            return almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).OrderBy(i => i).ToArray();
        }

        private static Guid[] Esperados(params Guid[] ids)
        {
            return ids.OrderBy(i => i).ToArray();
        }

        /// <summary>
        /// Siete conversaciones como el spike: asunto: Ca ausente, Cb 'ZZF-Alfa', Cc 'ZZF-Beta', Cd '' (queda ausente), Ce 'ZZF-Gamma', Cf ausente; cantidad de respuestas: Ca 0, Cb 5, Cc 7, Cd 3, Ce 0, Cf 0;
        /// cliente solo en Cc; resultado solo en Cb.
        /// </summary>
        private sealed class Conversaciones
        {
            public readonly OrganizationServiceEnMemoria Almacen = new OrganizationServiceEnMemoria();
            public Guid Ca, Cb, Cc, Cd, Ce, Cf, Cliente_;

            public Conversaciones()
            {
                Cliente_ = UnCliente(Almacen);
                Ca = Sembrada(Almacen, Conversacion, "sanic_cantidadrespuestas", 0);
                Cb = Sembrada(Almacen, Conversacion, "sanic_asunto", "ZZF-Alfa", "sanic_cantidadrespuestas", 5, "sanic_resultado", new OptionSetValue(159460002));
                Cc = Sembrada(Almacen, Conversacion, "sanic_asunto", "ZZF-Beta", "sanic_cantidadrespuestas", 7, "sanic_clienteid", new EntityReference(ApoyoMedido.Cliente, Cliente_));
                Cd = Sembrada(Almacen, Conversacion, "sanic_cantidadrespuestas", 3);
                Ce = Sembrada(Almacen, Conversacion, "sanic_asunto", "ZZF-Gamma", "sanic_cantidadrespuestas", 0);
                Cf = Sembrada(Almacen, Conversacion, "sanic_cantidadrespuestas", 0);
            }
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (4) NotEqual incluye los nulos
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_QRY_01_NotEqualSobreUnTextoIncluyeLasFilasDondeLaColumnaEstaAusente()
        {
            // 12-operadores-y-filtros.json: 'NotEqual ZZF-Alfa sobre sanic_asunto' devolvió 6 de 7 (las 4 ausentes incluidas). Mata: lanzar (el doble de la vuelta 2) o excluir los nulos (SQL).
            var c = new Conversaciones();

            var ids = Ids(c.Almacen, Donde(Conversacion, "sanic_asunto", ConditionOperator.NotEqual, "ZZF-Alfa"));

            Assert.Equal(Esperados(c.Ca, c.Cc, c.Cd, c.Ce, c.Cf), ids);
        }

        [Fact]
        public void PA_MED_QRY_01b_NotEqualSobreLookupChoiceYEnteroIncluyeLosAusentes()
        {
            // 12-operadores-y-filtros.json: NotEqual sobre sanic_clienteid (lookup nulo en 5 de 6), sanic_resultado (choice) y sanic_tiempototal (entero nulo en todas).
            var c = new Conversaciones();

            Assert.Equal(Esperados(c.Ca, c.Cb, c.Cd, c.Ce, c.Cf), Ids(c.Almacen, Donde(Conversacion, "sanic_clienteid", ConditionOperator.NotEqual, c.Cliente_)));
            Assert.Equal(Esperados(c.Ca, c.Cc, c.Cd, c.Ce, c.Cf), Ids(c.Almacen, Donde(Conversacion, "sanic_resultado", ConditionOperator.NotEqual, 159460002)));
            Assert.Equal(Esperados(c.Ca, c.Cb, c.Cc, c.Cd, c.Ce, c.Cf), Ids(c.Almacen, Donde(Conversacion, "sanic_tiempototal", ConditionOperator.NotEqual, 5)));
        }

        [Fact]
        public void PA_MED_QRY_01c_NotEqualMasNotNullExcluyeLosNulos_YNotEqualOrNullEsIgualAlNotEqualSolo()
        {
            // 12-operadores-y-filtros.json: 'NotEqual AND NotNull' => {Cc, Ce}; 'NotEqual OR Null' => las mismas 6 que NotEqual solo.
            var c = new Conversaciones();
            var conNotNull = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() };
            conNotNull.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotEqual, "ZZF-Alfa");
            conNotNull.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotNull);
            var conNull = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() };
            conNull.Criteria.FilterOperator = LogicalOperator.Or;
            conNull.Criteria.AddCondition("sanic_asunto", ConditionOperator.NotEqual, "ZZF-Alfa");
            conNull.Criteria.AddCondition("sanic_asunto", ConditionOperator.Null);

            Assert.Equal(Esperados(c.Cc, c.Ce), Ids(c.Almacen, conNotNull));
            Assert.Equal(Esperados(c.Ca, c.Cc, c.Cd, c.Ce, c.Cf), Ids(c.Almacen, conNull));
        }

        [Fact]
        public void PA_MED_QRY_01d_NotEqualSinCompaniaYConLaTablaVaciaNoLanza_YUnaColumnaNulaEnUnaFilaDeZzfTambienEntra()
        {
            // Invierte las reglas "por la forma" de la vuelta 2 (PA_CIN_01, PA_CIN_03 y PA_CIN_05): ya no hay nada que lanzar. Mata: conservar la validación por la forma.
            var vacia = new OrganizationServiceEnMemoria();
            var conFilas = new OrganizationServiceEnMemoria();
            conFilas.Sembrar(Zzf(Fila1, "zzf_numero", 1), Zzf(Fila2, "zzf_otra", 2));

            Assert.Empty(vacia.RetrieveMultiple(Donde(TablaZzf, "zzf_numero", ConditionOperator.NotEqual, 3)).Entities);
            Assert.Equal(Esperados(Fila1, Fila2), Ids(conFilas, Donde(TablaZzf, "zzf_numero", ConditionOperator.NotEqual, 3)));
            Assert.Equal(Esperados(Fila2), Ids(conFilas, Donde(TablaZzf, "zzf_numero", ConditionOperator.NotEqual, 1)));
        }

        [Fact]
        public void PA_MED_QRY_01e_ElNotEqualDeUnaColumnaOcultaPorSeguridadVaTambienComoNulaYEntra()
        {
            // Consecuencia de (4) con DC-22: para el usuario sin perfil la columna vale como nula, así que NotEqual la incluye.
            var almacen = new OrganizationServiceEnMemoria();
            var a = Sembrada(almacen, Cliente, "sanic_cedula", "ZZF-ced-1");
            almacen.OcultarColumna(Cliente, "sanic_cedula", Usuario1);

            Assert.Equal(Esperados(a), Ids(almacen.ComoUsuario(Usuario1), Donde(Cliente, "sanic_cedula", ConditionOperator.NotEqual, "ZZF-ced-1")));
            Assert.Empty(almacen.RetrieveMultiple(Donde(Cliente, "sanic_cedula", ConditionOperator.NotEqual, "ZZF-ced-1")).Entities);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (5) subfiltro vacío
        // ---------------------------------------------------------------------------------------------------------------------

        [Theory]
        [InlineData(LogicalOperator.And)]
        [InlineData(LogicalOperator.Or)]
        public void PA_MED_QRY_02_UnSubfiltroVacioDentroDeOtroSeIgnora_NiVerdaderoNiFalso(LogicalOperator operadorDelPadre)
        {
            // 12-operadores-y-filtros.json (punto_5): AND/OR { In[Ca,Cb,Cc], filtro vacío } devolvió las 3 filas. Mata: lanzar, o tratar el vacío como verdadero (OR devolvería todas) o falso (AND devolvería 0).
            var c = new Conversaciones();
            var consulta = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() };
            consulta.Criteria.FilterOperator = operadorDelPadre;
            consulta.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.In, 5, 7, 3);
            consulta.Criteria.AddFilter(LogicalOperator.And);
            consulta.Criteria.AddFilter(LogicalOperator.Or);

            Assert.Equal(Esperados(c.Cb, c.Cc, c.Cd), Ids(c.Almacen, consulta));
        }

        [Fact]
        public void PA_MED_QRY_02b_UnCriterioConSoloFiltrosVaciosEquivaleANoTenerCriterio_YElVacioAnidadoSeIgnora()
        {
            var c = new Conversaciones();
            var soloVacios = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() };
            soloVacios.Criteria.AddFilter(LogicalOperator.And);
            soloVacios.Criteria.AddFilter(LogicalOperator.Or);
            var anidado = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() };
            anidado.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.In, 5, 7, 3);
            anidado.Criteria.AddFilter(LogicalOperator.And).AddFilter(LogicalOperator.Or);
            var orConEqual = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() };
            orConEqual.Criteria.FilterOperator = LogicalOperator.Or;
            orConEqual.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.Equal, 5);
            var interno = orConEqual.Criteria.AddFilter(LogicalOperator.Or);
            interno.AddCondition("sanic_cantidadrespuestas", ConditionOperator.Equal, 7);
            interno.AddFilter(LogicalOperator.And);

            Assert.Equal(6, c.Almacen.RetrieveMultiple(soloVacios).Entities.Count);
            Assert.Equal(Esperados(c.Cb, c.Cc, c.Cd), Ids(c.Almacen, anidado));
            Assert.Equal(Esperados(c.Cb, c.Cc), Ids(c.Almacen, orConEqual));
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (1)(3) valores de las condiciones
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> ValoresQueNoSonDeCondicion()
        {
            yield return new object[] { Conversacion, "statuscode", ConditionOperator.Equal, new OptionSetValue(1), "expected argument(s) of type 'System.Int32' but received 'Microsoft.Xrm.Sdk.OptionSetValue'" };
            yield return new object[] { Conversacion, "statecode", ConditionOperator.Equal, new OptionSetValue(0), "statecode values are expected to be passed as strings or int" };
            yield return new object[] { Conversacion, "sanic_resultado", ConditionOperator.Equal, new OptionSetValue(159460002), "expected argument(s) of type 'System.Int32' but received 'Microsoft.Xrm.Sdk.OptionSetValue'" };
            yield return new object[] { Conversacion, "sanic_resultado", ConditionOperator.NotEqual, new OptionSetValue(159460002), "expected argument(s) of type 'System.Int32' but received 'Microsoft.Xrm.Sdk.OptionSetValue'" };
            yield return new object[] { Conversacion, "sanic_resultado", ConditionOperator.GreaterThan, new OptionSetValue(159460001), "expected argument(s) of type 'System.Int32' but received 'Microsoft.Xrm.Sdk.OptionSetValue'" };
            yield return new object[] { Conversacion, "sanic_clienteid", ConditionOperator.Equal, new EntityReference(Cliente, Guid.NewGuid()), "expected argument(s) of type 'System.Guid' but received 'Microsoft.Xrm.Sdk.EntityReference'" };
            yield return new object[] { Conversacion, "ownerid", ConditionOperator.Equal, new EntityReference("team", Guid.NewGuid()), "expected argument(s) of type 'System.Guid' but received 'Microsoft.Xrm.Sdk.EntityReference'" };
            yield return new object[] { Arreglo, "sanic_saldoinicial", ConditionOperator.Equal, new Money(1000.5m), "expected argument(s) of type 'System.Decimal' but received 'Microsoft.Xrm.Sdk.Money'" };
            yield return new object[] { Arreglo, "sanic_saldoinicial", ConditionOperator.GreaterThan, new Money(1000m), "expected argument(s) of type 'System.Decimal' but received 'Microsoft.Xrm.Sdk.Money'" };
            yield return new object[] { Conversacion, "sanic_cantidadrespuestas", ConditionOperator.Equal, new Money(5m), "expected argument(s) of type 'System.Int32' but received 'Microsoft.Xrm.Sdk.Money'" };
            yield return new object[] { Conversacion, "sanic_cantidadrespuestas", ConditionOperator.Equal, new OptionSetValue(5), "expected argument(s) of type 'System.Int32' but received 'Microsoft.Xrm.Sdk.OptionSetValue'" };
        }

        [Theory]
        [MemberData(nameof(ValoresQueNoSonDeCondicion))]
        public void PA_MED_QRY_03_UnOptionSetValueUnEntityReferenceOUnMoneyComoValorDeUnaCondicionFalla0x80040203(string tabla, string columna, ConditionOperator operador, object valor, string fragmento)
        {
            // 10-condiciones-valores-tipados.json. Mata: aceptarlos (el doble solo conocía primitivos) o lanzar InvalidOperationException.
            var almacen = new OrganizationServiceEnMemoria();
            var esperado = "Condition for attribute '" + tabla + "." + columna + "': ";

            var falla = Falla(() => almacen.RetrieveMultiple(Donde(tabla, columna, operador, valor)));

            AssertFalla(falla, CodigosDeFalla.SolicitudInvalida, esperado);
            Assert.Contains(fragmento, falla.Detail.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA_MED_QRY_03b_ElOptionSetValueDentroDeUnInTambienFalla_YEnUnaTablaZzfSinEsquemaTambien()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var conIn = Donde(Conversacion, "sanic_resultado", ConditionOperator.In, new OptionSetValue(159460001), new OptionSetValue(159460002));

            AssertFalla(Falla(() => almacen.RetrieveMultiple(conIn)), CodigosDeFalla.SolicitudInvalida, "Condition for attribute 'sanic_carp_tbl_conversacion.sanic_resultado'");
            AssertFalla(Falla(() => almacen.RetrieveMultiple(Donde(TablaZzf, "zzf_opcion", ConditionOperator.Equal, new OptionSetValue(1)))), CodigosDeFalla.SolicitudInvalida);
            AssertFalla(Falla(() => almacen.RetrieveMultiple(Donde(TablaZzf, "zzf_ref", ConditionOperator.Equal, new EntityReference("team", Team1)))), CodigosDeFalla.SolicitudInvalida);
            AssertFalla(Falla(() => almacen.RetrieveMultiple(Donde(TablaZzf, "zzf_monto", ConditionOperator.Equal, new Money(1m)))), CodigosDeFalla.SolicitudInvalida);
        }

        [Fact]
        public void PA_MED_QRY_03c_IntGuidDecimalYUnaCadenaNumericaSobreUnEnteroSiFuncionan_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: 10-condiciones-valores-tipados.json (controles: statuscode Equal int, sanic_resultado Equal int, sanic_clienteid Equal Guid, ownerid Equal Guid, decimal 1000.5, '5' sobre un entero).
            var almacen = new OrganizationServiceEnMemoria();
            var cliente = UnCliente(almacen);
            var team = Sembrada(almacen, "team");
            var a = Sembrada(almacen, Conversacion, "sanic_cantidadrespuestas", 5, "sanic_resultado", new OptionSetValue(159460002), "sanic_clienteid", new EntityReference(Cliente, cliente),
                "ownerid", new EntityReference("team", team), "statuscode", new OptionSetValue(1));
            var b = Sembrada(almacen, Arreglo, "sanic_saldoinicial", 1000.5m);

            Assert.Equal(Esperados(a), Ids(almacen, Donde(Conversacion, "sanic_cantidadrespuestas", ConditionOperator.Equal, "5")));
            Assert.Equal(Esperados(a), Ids(almacen, Donde(Conversacion, "sanic_resultado", ConditionOperator.Equal, 159460002)));
            Assert.Equal(Esperados(a), Ids(almacen, Donde(Conversacion, "sanic_clienteid", ConditionOperator.Equal, cliente)));
            Assert.Equal(Esperados(a), Ids(almacen, Donde(Conversacion, "ownerid", ConditionOperator.Equal, team)));
            Assert.Equal(Esperados(a), Ids(almacen, Donde(Conversacion, "statuscode", ConditionOperator.Equal, 1)));
            Assert.Equal(Esperados(b), Ids(almacen, Donde(Arreglo, "sanic_saldoinicial", ConditionOperator.Equal, 1000.5m)));
        }

        public static IEnumerable<object[]> OperadoresDeUnSoloValor()
        {
            foreach (var operador in new[] { ConditionOperator.Equal, ConditionOperator.NotEqual, ConditionOperator.GreaterThan, ConditionOperator.GreaterEqual, ConditionOperator.LessThan, ConditionOperator.LessEqual })
                yield return new object[] { operador };
        }

        [Theory]
        [MemberData(nameof(OperadoresDeUnSoloValor))]
        public void PA_MED_QRY_04_UnOperadorDeUnSoloValorConDosOTresValoresFalla0x80040203(ConditionOperator operador)
        {
            // 12-operadores-y-filtros.json (punto_3): 'The ConditonOperator.<Op> requires 1 value/s, not N. Parameter Name: <col>' (la grafía "Conditon" es la de la plataforma).
            var almacen = new OrganizationServiceEnMemoria();

            var dos = Falla(() => almacen.RetrieveMultiple(Donde(Conversacion, "sanic_cantidadrespuestas", operador, 4, 6)));
            var tres = Falla(() => almacen.RetrieveMultiple(Donde(Conversacion, "sanic_cantidadrespuestas", operador, 4, 6, 8)));

            AssertFalla(dos, CodigosDeFalla.SolicitudInvalida, "The ConditonOperator." + operador + " requires 1 value/s, not 2. Parameter Name: sanic_cantidadrespuestas");
            AssertFalla(tres, CodigosDeFalla.SolicitudInvalida, "The ConditonOperator." + operador + " requires 1 value/s, not 3. Parameter Name: sanic_cantidadrespuestas");
        }

        [Theory]
        [MemberData(nameof(OperadoresDeUnSoloValor))]
        public void PA_MED_QRY_04b_UnOperadorDeUnSoloValorSinValoresFalla0x80040203(ConditionOperator operador)
        {
            var almacen = new OrganizationServiceEnMemoria();

            var falla = Falla(() => almacen.RetrieveMultiple(Donde(Conversacion, "sanic_cantidadrespuestas", operador)));

            AssertFalla(falla, CodigosDeFalla.SolicitudInvalida, "The ConditonOperator." + operador + " requires 1 value/s, not 0. Parameter Name: sanic_cantidadrespuestas");
        }

        [Theory]
        [InlineData(ConditionOperator.Null)]
        [InlineData(ConditionOperator.NotNull)]
        public void PA_MED_QRY_04c_NullYNotNullConUnValorSobranteFallan0x80040203(ConditionOperator operador)
        {
            // 12-operadores-y-filtros.json: "Condition operator 'Null' requires that no values are set. Values.Length: 1".
            var almacen = new OrganizationServiceEnMemoria();

            var falla = Falla(() => almacen.RetrieveMultiple(Donde(Conversacion, "sanic_asunto", operador, "x")));

            AssertFalla(falla, CodigosDeFalla.SolicitudInvalida, "Condition operator '" + operador + "' requires that no values are set. Values.Length: 1");
        }

        [Fact]
        public void PA_MED_QRY_04d_UnInSinValoresFalla0x80040203()
        {
            // 12-operadores-y-filtros.json: 'The value passed for ConditionOperator.In is empty. Attribute Name: sanic_huellamessageid'.
            var almacen = new OrganizationServiceEnMemoria();

            var falla = Falla(() => almacen.RetrieveMultiple(Donde(Conversacion, "sanic_huellamessageid", ConditionOperator.In)));

            AssertFalla(falla, CodigosDeFalla.SolicitudInvalida, "The value passed for ConditionOperator.In is empty. Attribute Name: sanic_huellamessageid");
        }

        [Fact]
        public void PA_MED_QRY_04e_UnaColeccionComoValorCuentaComoVariosValores_YLaFallaSeDaAunqueLaTablaEsteVacia()
        {
            // Absorbe a la unitaria UnValorQueEsUnaColeccionCuentaComoVariosValores...: la regla es de la FORMA de la consulta, no de los datos.
            var condicion = new ConditionExpression("sanic_cantidadrespuestas", ConditionOperator.Equal);
            condicion.Values.Add(new[] { 1, 2 });
            var consulta = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition(condicion);

            var falla = Falla(() => new OrganizationServiceEnMemoria().RetrieveMultiple(consulta));

            AssertFalla(falla, CodigosDeFalla.SolicitudInvalida, "The ConditonOperator.Equal requires 1 value/s, not 2.");
        }

        [Theory]
        [InlineData(ConditionOperator.Like)]
        [InlineData(ConditionOperator.BeginsWith)]
        [InlineData(ConditionOperator.NotIn)]
        public void PA_MED_QRY_05_LikeBeginsWithYNotInSiguenSinSimularse_VERDE_A_PROPOSITO(ConditionOperator operador)
        {
            // VERDE_A_PROPOSITO: Dev los acepta (12 y 18) pero el proyecto no los usa y el doble no los emula (límite declarado en 4.5): lanzan NotSupportedException, no adivinan.
            var almacen = new OrganizationServiceEnMemoria();

            Assert.Throws<NotSupportedException>(() => almacen.RetrieveMultiple(Donde(Conversacion, "sanic_asunto", operador, "ZZF%")));
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (c) Equal de texto ignora mayúsculas Y acentos
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> VariantesDeJose()
        {
            yield return new object[] { "zzf-josé" };
            yield return new object[] { "ZZF-JOSÉ" };
            yield return new object[] { "ZZF-Jose" };
            yield return new object[] { "ZZF-jose" };
            yield return new object[] { "ZZF-JOSE" };
            yield return new object[] { "ZZF-Josè" };
            yield return new object[] { "ZZF-José" };
        }

        [Theory]
        [MemberData(nameof(VariantesDeJose))]
        public void PA_MED_QRY_06_EqualDeTextoIgnoraMayusculasYAcentos(string buscado)
        {
            // 18-texto-y-esquema.json (punto_c): 'ZZF-José' se halló con zzf-josé, ZZF-JOSÉ, ZZF-Jose, ZZF-jose, ZZF-JOSE, ZZF-Josè y ZZF-Jose + combinante. Mata: OrdinalIgnoreCase (solo mayúsculas).
            var almacen = new OrganizationServiceEnMemoria();
            var jose = Sembrada(almacen, Cliente, "sanic_nombrecompleto", "ZZF-José");
            Sembrada(almacen, Cliente, "sanic_nombrecompleto", "ZZF-Maria");

            Assert.Equal(Esperados(jose), Ids(almacen, Donde(Cliente, "sanic_nombrecompleto", ConditionOperator.Equal, buscado)));
            Assert.Equal(Esperados(jose), Ids(almacen, Donde(Cliente, "sanic_nombrecompleto", ConditionOperator.In, buscado, "otro")));
        }

        [Fact]
        public void PA_MED_QRY_06b_NotEqualDeTextoUsaLaMismaComparacion_ElOtroNoSeConfundeConJose()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var jose = Sembrada(almacen, Cliente, "sanic_nombrecompleto", "ZZF-José");
            var maria = Sembrada(almacen, Cliente, "sanic_nombrecompleto", "ZZF-Maria");

            Assert.Equal(Esperados(maria), Ids(almacen, Donde(Cliente, "sanic_nombrecompleto", ConditionOperator.NotEqual, "zzf-JOSE")));
            Assert.Equal(Esperados(jose), Ids(almacen, Donde(Cliente, "sanic_nombrecompleto", ConditionOperator.NotEqual, "zzf-maria")));
        }

        [Fact]
        public void PA_MED_QRY_06c_LaComparacionDeTextoNoSeConvierteEnUnPrefijoNiIgnoraOtrosCaracteres_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: guarda contra una implementación que "normalice" de más (por ejemplo quitando todo lo que no es letra).
            var almacen = new OrganizationServiceEnMemoria();
            Sembrada(almacen, Cliente, "sanic_nombrecompleto", "ZZF-José Perez");

            Assert.Empty(almacen.RetrieveMultiple(Donde(Cliente, "sanic_nombrecompleto", ConditionOperator.Equal, "zzf-jose")).Entities);
            Assert.Empty(almacen.RetrieveMultiple(Donde(Cliente, "sanic_nombrecompleto", ConditionOperator.Equal, "zzfjose perez")).Entities);
        }
    }
}
