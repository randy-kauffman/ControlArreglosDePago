using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoMedido;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Lote 3b: las fallas REALES.</b> El doble deja de lanzar <see cref="ArgumentException"/> o <see cref="InvalidOperationException"/> inventadas para los rechazos que Dev da y lanza
    /// <c>FaultException&lt;OrganizationServiceFault&gt;</c> con el <c>ErrorCode</c> medido (<c>spikes/dobles/RESULTADO.md</c> sección 4, <c>salida/40-resumen-fallas.json</c>).
    /// <para>
    /// <b>Forma de la falla del doble (decisión del arquitecto, 2026-10-15).</b> La misma de siempre (excepción directa, sin <c>InnerException</c>, <c>Message</c> igual a <c>Detail.Message</c>,
    /// <c>Timestamp</c> Utc, <c>InnerFault</c> encadenado con el mismo código) con tres precisiones medidas: (1) el <c>ErrorCode</c> es el del catálogo; (2) el <c>Message</c> lleva la FORMA del
    /// mensaje real (las pruebas fijan un fragmento estable, nunca los GUID ni los OTC que el doble no puede conocer); (3) los niveles de <c>InnerFault</c> son los que Dev mostró por código
    /// (<see cref="ApoyoMedido"/>); un código no medido o pedido con <c>FallarEn</c> mantiene los 2 niveles totales de siempre. <c>ErrorDetails</c> puede ir vacío.
    /// </para>
    /// </summary>
    public class MedidoEnDevFallasAceptacion
    {
        // ---------------------------------------------------------------------------------------------------------------------
        // Guarda: las constantes de CodigosDeFalla son las del catálogo medido
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_FAL_00_LasConstantesNuevasDeCodigosDeFallaEstanEnElCatalogoMedido_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: son constantes (valores), no comportamiento. La prueba las contrasta con el catálogo del spike, no consigo mismas.
            var catalogo = string.Join("\n", ApoyoDeAceptacion.LineasDelDocumento("spikes/dobles/RESULTADO.md"));
            var codigos = new Dictionary<string, int>
            {
                { "CookieMalFormada", CodigosDeFalla.CookieMalFormada }, { "SolicitudInvalida", CodigosDeFalla.SolicitudInvalida }, { "TablaInexistente", CodigosDeFalla.TablaInexistente },
                { "ColumnaInexistente", CodigosDeFalla.ColumnaInexistente }, { "CookieDeOtraConsulta", CodigosDeFalla.CookieDeOtraConsulta }, { "FechaFueraDeRango", CodigosDeFalla.FechaFueraDeRango },
                { "PropietarioInvalido", CodigosDeFalla.PropietarioInvalido }, { "RestriccionDeRelacion", CodigosDeFalla.RestriccionDeRelacion }, { "MensajeNoSoportado", CodigosDeFalla.MensajeNoSoportado },
                { "OpcionInexistente", CodigosDeFalla.OpcionInexistente }, { "DecimalFueraDeRango", CodigosDeFalla.DecimalFueraDeRango }, { "TextoDemasiadoLargo", CodigosDeFalla.TextoDemasiadoLargo },
                { "TipoDeAtributoInvalido", CodigosDeFalla.TipoDeAtributoInvalido }, { "ParDeEstadoInvalido", CodigosDeFalla.ParDeEstadoInvalido }, { "ColumnaProtegidaAlCrear", CodigosDeFalla.ColumnaProtegidaAlCrear },
                { "ColumnaProtegidaAlActualizar", CodigosDeFalla.ColumnaProtegidaAlActualizar }, { "TransaccionAbortada", CodigosDeFalla.TransaccionAbortada }, { "SinPrivilegio", CodigosDeFalla.SinPrivilegio },
                { "ClaveDuplicada", CodigosDeFalla.ClaveDuplicada }, { "FilaInexistente", CodigosDeFalla.FilaInexistente }
            };

            foreach (var par in codigos)
                Assert.True(catalogo.Contains("`0x" + ((uint)par.Value).ToString("X8", CultureInfo.InvariantCulture) + "`", StringComparison.Ordinal), par.Key + " no está en el catálogo de RESULTADO.md");
            Assert.Equal(CodigosDeFalla.SinAcceso, CodigosDeFalla.SinPrivilegio);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Tabla y columna inexistentes
        // ---------------------------------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("sanic_carp_tbl_noexiste")]
        [InlineData("SANIC_CARP_TBL_CLIENTE")]
        public void PA_MED_FAL_01_UnaTablaInexistenteOConElNombreEnMayusculasFalla0x80041102_EnCreateUpdateRetrieveYRetrieveMultiple(string tabla)
        {
            // 18-texto-y-esquema.json (Retrieve y RetrieveMultiple de una tabla inexistente), 15-name-del-lookup.json (LogicalName en mayúsculas). Mata: seguir lanzando ArgumentException / InvalidOperationException.
            var almacen = new OrganizationServiceEnMemoria();
            var consulta = new QueryExpression(tabla) { ColumnSet = new ColumnSet() };
            var fragmento = "The entity with a name = '" + tabla + "'";

            AssertFalla(Falla(() => almacen.Create(new Entity(tabla))), CodigosDeFalla.TablaInexistente, fragmento, "Create");
            AssertFalla(Falla(() => almacen.Update(new Entity(tabla, Fila1))), CodigosDeFalla.TablaInexistente, fragmento, "Update");
            AssertFalla(Falla(() => almacen.Retrieve(tabla, Fila1, new ColumnSet())), CodigosDeFalla.TablaInexistente, fragmento, "Retrieve");
            AssertFalla(Falla(() => almacen.RetrieveMultiple(consulta)), CodigosDeFalla.TablaInexistente, fragmento, "RetrieveMultiple");
            Assert.Empty(almacen.Todas(tabla));
        }

        [Fact]
        public void PA_MED_FAL_02_UnaColumnaInexistenteFalla0x80041103_EnCreateUpdateColumnSetCondicionYOrden()
        {
            // 17b y 18-texto-y-esquema.json: 'entity doesn't contain attribute with Name = ...'. Mata: ArgumentException / InvalidOperationException.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = UnCliente(almacen);
            var enColumnas = new QueryExpression(Cliente) { ColumnSet = new ColumnSet("sanic_noexiste") };
            var enCondicion = new QueryExpression(Cliente) { ColumnSet = new ColumnSet() };
            enCondicion.Criteria.AddCondition("sanic_noexiste", ConditionOperator.Null);
            var enOrden = new QueryExpression(Cliente) { ColumnSet = new ColumnSet() };
            enOrden.AddOrder("sanic_noexiste", OrderType.Ascending);
            const string fragmento = "entity doesn't contain attribute with Name = 'sanic_noexiste'";

            AssertFalla(Falla(() => almacen.Create(new Entity(Cliente) { ["sanic_noexiste"] = "x" })), CodigosDeFalla.ColumnaInexistente, fragmento, "Create");
            AssertFalla(Falla(() => almacen.Update(new Entity(Cliente, existente) { ["sanic_noexiste"] = "x" })), CodigosDeFalla.ColumnaInexistente, fragmento, "Update");
            AssertFalla(Falla(() => almacen.Retrieve(Cliente, existente, new ColumnSet("sanic_noexiste"))), CodigosDeFalla.ColumnaInexistente, fragmento, "Retrieve");
            AssertFalla(Falla(() => almacen.RetrieveMultiple(enColumnas)), CodigosDeFalla.ColumnaInexistente, fragmento, "ColumnSet");
            AssertFalla(Falla(() => almacen.RetrieveMultiple(enCondicion)), CodigosDeFalla.ColumnaInexistente, fragmento, "condición");
            AssertFalla(Falla(() => almacen.RetrieveMultiple(enOrden)), CodigosDeFalla.ColumnaInexistente, fragmento, "orden");
            Assert.Single(almacen.Todas(Cliente));
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Tipos
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> ValoresEquivocadosEnUnDecimal()
        {
            yield return new object[] { 5, "Found: System.Int32" };
            yield return new object[] { "5", "Found: System.String" };
            yield return new object[] { new Money(5m), "Found: Microsoft.Xrm.Sdk.Money" };
        }

        [Theory]
        [MemberData(nameof(ValoresEquivocadosEnUnDecimal))]
        public void PA_MED_FAL_03_UnIntUnStringOUnMoneyEnUnDecimalFalla0x80044354_PorCreateYPorUpdate(object valor, string encontrado)
        {
            // 18-texto-y-esquema.json: 'Invalid Attribute Value Type for sanic_saldoinicial. Expected: ..., Found: <tipo>'. Mata: ArgumentException.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Arreglo);

            var deCreate = Falla(() => almacen.Create(new Entity(Arreglo) { ["sanic_saldoinicial"] = valor }));
            var deUpdate = Falla(() => almacen.Update(new Entity(Arreglo, existente) { ["sanic_saldoinicial"] = valor }));

            AssertFalla(deCreate, CodigosDeFalla.TipoDeAtributoInvalido, "Invalid Attribute Value Type for", "Create");
            AssertFalla(deUpdate, CodigosDeFalla.TipoDeAtributoInvalido, "Invalid Attribute Value Type for", "Update");
            Assert.Contains("sanic_saldoinicial", deUpdate.Detail.Message, StringComparison.Ordinal);
            Assert.Contains(encontrado, deUpdate.Detail.Message, StringComparison.Ordinal);
            Assert.Single(almacen.Todas(Arreglo));
        }

        [Fact]
        public void PA_MED_FAL_03b_UnStringEnUnLookupFalla0x80044354()
        {
            // 18-texto-y-esquema.json: 'Error converting attribute value to Property: Attribute [sanic_clienteid], Attribute type [lookup] ...'.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Arreglo);

            var falla = Falla(() => almacen.Update(new Entity(Arreglo, existente) { ["sanic_clienteid"] = "x" }));

            AssertFalla(falla, CodigosDeFalla.TipoDeAtributoInvalido, "sanic_clienteid");
        }

        public static IEnumerable<object[]> TiposEquivocadosQueDan0x80040203()
        {
            yield return new object[] { Arreglo, "sanic_plazo", 6.5m, "Incorrect attribute value type System.Decimal" };
            yield return new object[] { Arreglo, "sanic_plazo", "6", "Incorrect attribute value type System.String" };
            yield return new object[] { Arreglo, "sanic_plazo", 3000000000L, "Incorrect attribute value type System.Int64" };
            yield return new object[] { Arreglo, "sanic_fechafin", "2026-01-01", "Incorrect attribute value type System.String" };
            yield return new object[] { Arreglo, "sanic_tipo", 159460001, "Incorrect attribute value type System.Int32" };
            yield return new object[] { Cliente, "sanic_celular", 123, "Incorrect type of attribute value System.Int32" };
            yield return new object[] { Cliente, "sanic_celular", new OptionSetValue(1), "Incorrect type of attribute value Microsoft.Xrm.Sdk.OptionSetValue" };
        }

        [Theory]
        [MemberData(nameof(TiposEquivocadosQueDan0x80040203))]
        public void PA_MED_FAL_04_UnTipoEquivocadoEnEnteroFechaChoiceOTextoFalla0x80040203_PorCreateYPorUpdate(string tabla, string columna, object valor, string fragmento)
        {
            // 18-texto-y-esquema.json (punto_12_rechazos_de_esquema) y 17b (Create con sanic_cantidadrespuestas = '5'). Mata: ArgumentException.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, tabla);

            AssertFalla(Falla(() => almacen.Create(new Entity(tabla) { [columna] = valor })), CodigosDeFalla.SolicitudInvalida, fragmento, "Create");
            AssertFalla(Falla(() => almacen.Update(new Entity(tabla, existente) { [columna] = valor })), CodigosDeFalla.SolicitudInvalida, fragmento, "Update");
            Assert.Single(almacen.Todas(tabla));
        }

        [Fact]
        public void PA_MED_FAL_05_UnDoubleEnUnaColumnaDecimalSeAcepta_YQuedaComoDecimalConEscala10()
        {
            // 18-texto-y-esquema.json: 'Update double en columna decimal (5.5d)' => ok, relee System.Decimal 5.5000000000. Mata: rechazar el double.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Arreglo);

            almacen.Update(new Entity(Arreglo, existente) { ["sanic_saldoinicial"] = 5.5d });

            var leido = Assert.IsType<decimal>(almacen.Leer(Arreglo, existente)["sanic_saldoinicial"]);
            Assert.Equal(5.5m, leido);
            Assert.Equal(10, EscalaDe(leido));
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Texto, choice y decimal
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> TextosDeCelularQueCaben()
        {
            yield return new object[] { new string('z', 20), "20 ASCII" };
            yield return new object[] { new string('é', 20), "20 e con acento precompuesto (40 bytes UTF-8)" };
            yield return new object[] { string.Concat(Enumerable.Repeat("\U0001F600", 10)), "10 emoji = 20 unidades UTF-16" };
            yield return new object[] { string.Concat(Enumerable.Repeat("é", 10)), "10 pares e + acento combinante = 20 unidades" };
        }

        public static IEnumerable<object[]> TextosDeCelularQueNoCaben()
        {
            yield return new object[] { new string('z', 21), "21 ASCII" };
            yield return new object[] { string.Concat(Enumerable.Repeat("\U0001F600", 11)), "11 emoji = 22 unidades UTF-16 (10 puntos de código)" };
            yield return new object[] { string.Concat(Enumerable.Repeat("é", 20)), "20 pares combinantes = 40 unidades" };
        }

        [Theory]
        [MemberData(nameof(TextosDeCelularQueCaben))]
        public void PA_MED_FAL_06_ElLargoSeCuentaEnUnidadesUtf16_LosQueCabenSeAceptan_VERDE_A_PROPOSITO(string texto, string caso)
        {
            // 18-texto-y-esquema.json (punto_12_largo_del_texto): sanic_celular es T(20) y se cuenta string.Length, no bytes ni grafemas.
            var almacen = new OrganizationServiceEnMemoria();

            var id = almacen.Create(new Entity(Cliente) { ["sanic_celular"] = texto });

            Assert.True(texto == (string)almacen.Leer(Cliente, id)["sanic_celular"], caso);
        }

        [Theory]
        [MemberData(nameof(TextosDeCelularQueNoCaben))]
        public void PA_MED_FAL_06b_ElTextoMasLargoQueLaColumnaFalla0x80044331_PorCreateYPorUpdate(string texto, string caso)
        {
            // Mata: contar bytes o puntos de código (el emoji x 11 son 11 puntos de código y 22 unidades) y lanzar ArgumentException.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Cliente);
            const string fragmento = "exceeded the maximum allowed length of '20'";

            var deCreate = Falla(() => almacen.Create(new Entity(Cliente) { ["sanic_celular"] = texto }));
            var deUpdate = Falla(() => almacen.Update(new Entity(Cliente, existente) { ["sanic_celular"] = texto }));

            AssertFalla(deCreate, CodigosDeFalla.TextoDemasiadoLargo, fragmento, caso);
            AssertFalla(deUpdate, CodigosDeFalla.TextoDemasiadoLargo, fragmento, caso);
            Assert.Contains("'sanic_celular' attribute of the 'sanic_carp_tbl_cliente' entity", deCreate.Detail.Message, StringComparison.Ordinal);
            Assert.False(almacen.Leer(Cliente, existente).Contains("sanic_celular"));
        }

        [Fact]
        public void PA_MED_FAL_06c_LaHuellaDe65CaracteresFalla_ElLimiteEs64()
        {
            // 17b-autonumerico-otras-fallas.json: 'The length of the sanic_huellamessageid attribute ... maximum allowed length of 64'.
            var almacen = new OrganizationServiceEnMemoria();

            var falla = Falla(() => almacen.Create(new Entity(Conversacion) { ["sanic_huellamessageid"] = new string('a', 65) }));

            AssertFalla(falla, CodigosDeFalla.TextoDemasiadoLargo, "maximum allowed length of '64'");
            almacen.Create(new Entity(Conversacion) { ["sanic_huellamessageid"] = new string('a', 64) });
        }

        [Fact]
        public void PA_MED_FAL_07_UnChoiceInexistenteFalla0x8004431A_PorCreateYPorUpdate()
        {
            // 18-texto-y-esquema.json: 'The value 999 of sanic_tipo ... is outside the valid range. Accepted Values: ...'.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Arreglo);

            var deCreate = Falla(() => almacen.Create(new Entity(Arreglo) { ["sanic_producto"] = new OptionSetValue(999) }));
            var deUpdate = Falla(() => almacen.Update(new Entity(Arreglo, existente) { ["sanic_tipo"] = new OptionSetValue(999) }));

            AssertFalla(deCreate, CodigosDeFalla.OpcionInexistente, "The value 999 of 'sanic_producto' on record of type 'sanic_carp_tbl_arreglo' is outside the valid range. Accepted Values:", "Create");
            AssertFalla(deUpdate, CodigosDeFalla.OpcionInexistente, "The value 999 of 'sanic_tipo' on record of type 'sanic_carp_tbl_arreglo' is outside the valid range. Accepted Values:", "Update");
        }

        [Fact]
        public void PA_MED_FAL_08_UnDecimalFueraDelRangoDeLaColumnaFalla0x80044330()
        {
            // 18-texto-y-esquema.json: 'The value 1000000000000 of type System.Decimal is outside the valid range(0 to 999999999.99)'. El rango sale del diccionario (decimal(2) [0..999999999.99]).
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Arreglo);

            var falla = Falla(() => almacen.Update(new Entity(Arreglo, existente) { ["sanic_saldoinicial"] = 1000000000000m }));

            AssertFalla(falla, CodigosDeFalla.DecimalFueraDeRango, "outside the valid range(0 to 999999999.99)");
            Assert.Contains("sanic_carp_tbl_arreglo.sanic_saldoinicial", falla.Detail.Message, StringComparison.Ordinal);
            Assert.False(almacen.Leer(Arreglo, existente).Contains("sanic_saldoinicial"));
        }

        [Theory]
        [InlineData("5.555", "5.56")]
        [InlineData("5.554", "5.55")]
        public void PA_MED_FAL_09_UnDecimalConMasEscalaQueLaColumnaSeRedondeaALaEscalaDeLaColumna(string enviado, string guardado)
        {
            // 18-texto-y-esquema.json: 5.555 => 5.56 y 5.554 => 5.55 en decimal(2). Mata: guardar 5.555 (solo la escala 10 genérica).
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Arreglo);

            almacen.Update(new Entity(Arreglo, existente) { ["sanic_saldoinicial"] = decimal.Parse(enviado, CultureInfo.InvariantCulture) });

            var leido = (decimal)almacen.Leer(Arreglo, existente)["sanic_saldoinicial"];
            Assert.Equal(decimal.Parse(guardado, CultureInfo.InvariantCulture), leido);
            Assert.Equal(10, EscalaDe(leido));
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Lookups y propietario
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> LookupsAFilaInexistente()
        {
            yield return new object[] { "sanic_clienteid", Cliente, "Entity 'sanic_carp_tbl_cliente' With Id = " };
            yield return new object[] { "sanic_unidaaid", Conversacion, "Entity 'sanic_carp_tbl_conversacion' With Id = " };
            yield return new object[] { "sanic_oficialasignadoid", "systemuser", null };
            yield return new object[] { "ownerid", "team", "Entity 'team' With Id = " };
            yield return new object[] { "ownerid", "systemuser", "Entity 'systemuser' With Id = " };
        }

        [Theory]
        [MemberData(nameof(LookupsAFilaInexistente))]
        public void PA_MED_FAL_10_UnLookupAUnaFilaQueNoExisteFalla0x80040217_PorCreateYPorUpdate_NombrandoElDestino(string columna, string destino, string fragmento)
        {
            // 18-texto-y-esquema.json (punto_14_lookup_a_fila_inexistente) y 17-autonumerico.json. Mata: no validar la existencia (el ConstructorDeDatos arma lookups con Guid al azar).
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Conversacion);
            var inexistente = Guid.NewGuid();

            var deCreate = Falla(() => almacen.Create(new Entity(Conversacion) { [columna] = new EntityReference(destino, inexistente) }));
            var deUpdate = Falla(() => almacen.Update(new Entity(Conversacion, existente) { [columna] = new EntityReference(destino, inexistente) }));

            AssertFalla(deCreate, CodigosDeFalla.FilaInexistente, fragmento, "Create");
            AssertFalla(deUpdate, CodigosDeFalla.FilaInexistente, fragmento, "Update");
            foreach (var falla in new[] { deCreate, deUpdate })
            {
                Assert.Contains(inexistente.ToString(), falla.Detail.Message, StringComparison.OrdinalIgnoreCase);
                Assert.EndsWith("Does Not Exist", falla.Detail.Message, StringComparison.Ordinal);
            }
            Assert.Single(almacen.Todas(Conversacion));
            Assert.False(almacen.Leer(Conversacion, existente).Contains(columna));
        }

        [Fact]
        public void PA_MED_FAL_10b_UnLookupAUnaFilaQueExisteSeAcepta_AunqueSeaSembradaConSembrar_VERDE_A_PROPOSITO()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var cliente = UnCliente(almacen);
            var team = Sembrada(almacen, "team");

            var id = almacen.Create(new Entity(Conversacion) { ["sanic_clienteid"] = new EntityReference(Cliente, cliente), ["ownerid"] = new EntityReference("team", team) });

            Assert.Equal(cliente, ((EntityReference)almacen.Leer(Conversacion, id)["sanic_clienteid"]).Id);
        }

        [Fact]
        public void PA_MED_FAL_11_UnLookupALaTablaEquivocadaFalla0x80040217_NombrandoLaTablaDestinoReal_AunSiLaFilaExisteEnOtraTabla()
        {
            // 18-texto-y-esquema.json: Arreglo.sanic_clienteid = Conversacion => 'Entity 'sanic_carp_tbl_cliente' With Id = ... Does Not Exist' (la tabla DESTINO declarada, no la enviada).
            // Mata: seguir lanzando ArgumentException por el destino, o aceptar la referencia porque la fila existe en otra tabla.
            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = Sembrada(almacen, Conversacion);
            var arreglo = Sembrada(almacen, Arreglo);
            var equivocada = new EntityReference(Conversacion, conversacion);

            var deUpdate = Falla(() => almacen.Update(new Entity(Arreglo, arreglo) { ["sanic_clienteid"] = equivocada }));
            var deCreate = Falla(() => almacen.Create(new Entity(Arreglo) { ["sanic_clienteid"] = equivocada }));

            AssertFalla(deUpdate, CodigosDeFalla.FilaInexistente, "Entity 'sanic_carp_tbl_cliente' With Id = " + conversacion, "Update");
            AssertFalla(deCreate, CodigosDeFalla.FilaInexistente, "Entity 'sanic_carp_tbl_cliente' With Id = " + conversacion, "Create");
        }

        [Fact]
        public void PA_MED_FAL_12_UnLookupConGuidEmptyFalla0x80040203()
        {
            // 18-texto-y-esquema.json: 'ReferencingEntity=sanic_carp_tbl_conversacion has attribute sanic_clienteid value=Guid.Empty for relationship=...'.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Conversacion);

            var falla = Falla(() => almacen.Update(new Entity(Conversacion, existente) { ["sanic_clienteid"] = new EntityReference(Cliente, Guid.Empty) }));

            AssertFalla(falla, CodigosDeFalla.SolicitudInvalida, "has attribute sanic_clienteid value=Guid.Empty");
            Assert.Contains("ReferencingEntity=sanic_carp_tbl_conversacion", falla.Detail.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA_MED_FAL_13_UnOwneridAUnaTablaQueNoEsTeamNiUsuarioFalla0x8004049E_PorCreateYPorUpdate()
        {
            // 18-texto-y-esquema.json: 'Invalid PrincipalType=11194. Principal type must be either SystemUser, Team or Organization.' El doble no conoce el ObjectTypeCode: nombra la tabla.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Arreglo);
            var cliente = new EntityReference(Cliente, UnCliente(almacen));

            var deUpdate = Falla(() => almacen.Update(new Entity(Arreglo, existente) { ["ownerid"] = cliente }));
            var deCreate = Falla(() => almacen.Create(new Entity(Arreglo) { ["ownerid"] = cliente }));

            AssertFalla(deUpdate, CodigosDeFalla.PropietarioInvalido, "Principal type must be either SystemUser, Team or Organization.", "Update");
            AssertFalla(deCreate, CodigosDeFalla.PropietarioInvalido, "Principal type must be either SystemUser, Team or Organization.", "Create");
            Assert.Contains("PrincipalType=" + Cliente, deUpdate.Detail.Message, StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Cookie de paginación
        // ---------------------------------------------------------------------------------------------------------------------

        private static OrganizationServiceEnMemoria CincoConversaciones()
        {
            var almacen = new OrganizationServiceEnMemoria();
            for (var i = 1; i <= 5; i++) Sembrada(almacen, Conversacion, "sanic_asunto", "ZZF-" + i.ToString(CultureInfo.InvariantCulture));
            return almacen;
        }

        private static QueryExpression Pagina(string tabla, string ordenadaPor, int numero, string cookie)
        {
            var consulta = new QueryExpression(tabla) { ColumnSet = new ColumnSet() };
            consulta.AddOrder(ordenadaPor, OrderType.Ascending);
            consulta.PageInfo = new PagingInfo { Count = 2, PageNumber = numero, PagingCookie = cookie };
            return consulta;
        }

        [Fact]
        public void PA_MED_FAL_14_UnaCookieQueNoEsXmlFalla0x80040201()
        {
            // 16-paginacion-cookie.json: p2 con cookie basura 'xyz' => 'Malformed XML in the Paging Cookie'.
            var almacen = CincoConversaciones();

            var falla = Falla(() => almacen.RetrieveMultiple(Pagina(Conversacion, "sanic_asunto", 2, "xyz")));

            AssertFalla(falla, CodigosDeFalla.CookieMalFormada, "Malformed XML in the Paging Cookie");
        }

        [Fact]
        public void PA_MED_FAL_14b_UnaCookieDeOtraConsultaFalla0x80041129_ElOrdenNoCoincide()
        {
            // 16-paginacion-cookie.json: p2 de la consulta principal con la cookie de OTRA consulta (otro orden) => 'Order by columns do not match those in paging cookie.'
            var almacen = CincoConversaciones();
            var cookieAjena = almacen.RetrieveMultiple(Pagina(Conversacion, "sanic_remitente", 1, null)).PagingCookie;

            var falla = Falla(() => almacen.RetrieveMultiple(Pagina(Conversacion, "sanic_asunto", 2, cookieAjena)));

            AssertFalla(falla, CodigosDeFalla.CookieDeOtraConsulta, "Order by columns do not match those in paging cookie.");
        }

        [Fact]
        public void PA_MED_FAL_14c_UnaCookieConOtraCantidadDeColumnasFalla0x80041129()
        {
            // 16-paginacion-cookie.json: '<cookie page="9"/>' => 'Paging Cookie And Query Do Not Match. The counts are not equal.'
            var almacen = CincoConversaciones();

            var falla = Falla(() => almacen.RetrieveMultiple(Pagina(Conversacion, "sanic_asunto", 2, "<cookie page=\"9\"/>")));

            AssertFalla(falla, CodigosDeFalla.CookieDeOtraConsulta, "Paging Cookie And Query Do Not Match. The counts are not equal.");
        }

        [Fact]
        public void PA_MED_FAL_14d_UnaCookieDeOtraTablaFalla0x80041103_PorqueSuColumnaNoExisteEnLaTablaConsultada()
        {
            // 16-paginacion-cookie.json: cookie de Cliente (orden por sanic_nombre) en Conversacion => '... entity doesn't contain attribute with Name = 'sanic_carp_tbl_clienteid''.
            var almacen = CincoConversaciones();
            UnCliente(almacen); UnCliente(almacen); UnCliente(almacen);
            var cookieDeCliente = almacen.RetrieveMultiple(Pagina(Cliente, "sanic_nombre", 1, null)).PagingCookie;

            var falla = Falla(() => almacen.RetrieveMultiple(Pagina(Conversacion, "sanic_nombre", 2, cookieDeCliente)));

            AssertFalla(falla, CodigosDeFalla.ColumnaInexistente, "entity doesn't contain attribute with Name = 'sanic_carp_tbl_clienteid'");
        }

        [Fact]
        public void PA_MED_FAL_14e_LaCookieVaciaOLaDeLaMismaConsultaSiguenFuncionando_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: 16-paginacion-cookie.json (p2 sin cookie, con '' y con la cookie de p1 se comportan igual): la validación no puede romper el camino feliz.
            var almacen = CincoConversaciones();
            var primera = almacen.RetrieveMultiple(Pagina(Conversacion, "sanic_asunto", 1, null));

            var conCookie = almacen.RetrieveMultiple(Pagina(Conversacion, "sanic_asunto", 2, primera.PagingCookie));
            var vacia = almacen.RetrieveMultiple(Pagina(Conversacion, "sanic_asunto", 2, string.Empty));

            Assert.Equal(conCookie.Entities.Select(e => e.Id).ToArray(), vacia.Entities.Select(e => e.Id).ToArray());
            Assert.Equal("2", XElement.Parse(vacia.PagingCookie).Attribute("page").Value);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // La forma de la falla
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_FAL_15_TodaFallaDelDobleTieneLaFormaMedida_SinInnerException_MessageIgualADetailMessage_TimestampUtc_InnerFaultConElMismoCodigoYLosNivelesMedidos()
        {
            // 40-resumen-fallas.json (largo_cadena 1, HResult exterior, niveles_InnerFault por código). Recorre un caso de cada código que el doble ahora debe dar.
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrada(almacen, Arreglo);
            var cliente = UnCliente(almacen);
            var conversacion = Sembrada(almacen, Conversacion, "statecode", new OptionSetValue(0), "statuscode", new OptionSetValue(1));
            var casos = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>("tabla", () => almacen.Create(new Entity("sanic_carp_tbl_noexiste"))),
                new KeyValuePair<string, Action>("columna", () => almacen.Create(new Entity(Cliente) { ["sanic_noexiste"] = "x" })),
                new KeyValuePair<string, Action>("tipo", () => almacen.Update(new Entity(Arreglo, arreglo) { ["sanic_plazo"] = "6" })),
                new KeyValuePair<string, Action>("decimal", () => almacen.Update(new Entity(Arreglo, arreglo) { ["sanic_saldoinicial"] = 5 })),
                new KeyValuePair<string, Action>("texto", () => almacen.Create(new Entity(Cliente) { ["sanic_celular"] = new string('z', 21) })),
                new KeyValuePair<string, Action>("choice", () => almacen.Update(new Entity(Arreglo, arreglo) { ["sanic_tipo"] = new OptionSetValue(999) })),
                new KeyValuePair<string, Action>("rango", () => almacen.Update(new Entity(Arreglo, arreglo) { ["sanic_saldoinicial"] = 1000000000000m })),
                new KeyValuePair<string, Action>("estado", () => almacen.Update(new Entity(Conversacion, conversacion) { ["statuscode"] = new OptionSetValue(2) })),
                new KeyValuePair<string, Action>("owner", () => almacen.Update(new Entity(Arreglo, arreglo) { ["ownerid"] = new EntityReference(Cliente, cliente) })),
                new KeyValuePair<string, Action>("lookup", () => almacen.Update(new Entity(Arreglo, arreglo) { ["sanic_clienteid"] = new EntityReference(Cliente, Guid.NewGuid()) })),
                new KeyValuePair<string, Action>("fecha", () => almacen.Update(new Entity(Conversacion, conversacion) { ["sanic_fechafinalizacion"] = DateTime.MinValue.AddYears(1) })),
                new KeyValuePair<string, Action>("condicion", () => almacen.RetrieveMultiple(MalFormada()))
            };
            var codigos = new[]
            {
                CodigosDeFalla.TablaInexistente, CodigosDeFalla.ColumnaInexistente, CodigosDeFalla.SolicitudInvalida, CodigosDeFalla.TipoDeAtributoInvalido, CodigosDeFalla.TextoDemasiadoLargo,
                CodigosDeFalla.OpcionInexistente, CodigosDeFalla.DecimalFueraDeRango, CodigosDeFalla.ParDeEstadoInvalido, CodigosDeFalla.PropietarioInvalido, CodigosDeFalla.FilaInexistente,
                CodigosDeFalla.FechaFueraDeRango, CodigosDeFalla.SolicitudInvalida
            };

            for (var i = 0; i < casos.Count; i++) AssertFalla(Falla(casos[i].Value), codigos[i], null, casos[i].Key);
        }

        private static QueryExpression MalFormada()
        {
            var consulta = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("sanic_cantidadrespuestas", ConditionOperator.Equal, 1, 2);
            return consulta;
        }

        [Fact]
        public void PA_MED_FAL_16_UnaEscrituraMalFormadaSeRechazaAntesDeCorrerLosStepsDePreValidation()
        {
            // Decisión del lote 3 que se conserva (ahora con la falla real). Mata: validar al aplicar y correr los steps con un Target que la plataforma nunca habría aceptado.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            var espia = new PluginEspia("preval");
            pipeline.Registrar(espia, "Create", Arreglo, EtapaDeStep.PreValidation);
            var servicio = fabrica.CreateOrganizationService(null);

            AssertFalla(Falla(() => servicio.Create(new Entity(Arreglo) { ["sanic_noexiste"] = "x" })), CodigosDeFalla.ColumnaInexistente);
            AssertFalla(Falla(() => servicio.Create(new Entity(Arreglo) { ["sanic_saldoinicial"] = new Money(1m) })), CodigosDeFalla.TipoDeAtributoInvalido);

            Assert.Empty(espia.Llamadas);
            Assert.Empty(fabrica.Escrituras);
        }

        [Fact]
        public void PA_MED_FAL_17_UnStepQueAgregaUnaColumnaInexistenteAlTargetHaceFallarLaEscrituraAlAplicarlaConLaFallaReal()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            pipeline.Registrar(new PluginEspia("agrega", accion: l => ((Entity)l.Contexto.InputParameters["Target"])["sanic_noexiste"] = "x"), "Create", Arreglo, EtapaDeStep.PreOperation);

            var falla = Falla(() => fabrica.CreateOrganizationService(null).Create(new Entity(Arreglo)));

            AssertFalla(falla, CodigosDeFalla.ColumnaInexistente, "sanic_noexiste");
            Assert.Empty(fabrica.Almacen.Todas(Arreglo));
        }
    }
}
