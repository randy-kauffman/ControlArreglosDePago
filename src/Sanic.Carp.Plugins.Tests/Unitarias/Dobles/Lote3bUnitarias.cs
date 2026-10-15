using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Aceptacion.Dobles;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Unitarias.Dobles
{
    /// <summary>
    /// Decisiones de implementación del lote 3b que la aceptación del arquitecto no mide (4.5 las deja abiertas o las nombra como "extensión no medida"). Cada una dice POR QUÉ.
    /// </summary>
    public class Lote3bUnitarias
    {
        private const string Alfa = "sanic_carp_tbl_alfa";
        private const string Beta = "sanic_carp_tbl_beta";
        private const string Gamma = "sanic_carp_tbl_gamma";

        // Un diccionario inventado: Alfa -> Beta es parental (Cascade) y Beta -> Gamma es restringido. El modelo real no tiene una cadena así y no se puede probar ahí.
        private static readonly string[] Cadena =
        {
            "## 3. Choices globales y motivos de estado",
            "## 4. Tablas",
            "### 4.1 `sanic_carp_tbl_alfa`",
            "| Nombre lógico | Display name | Tipo y forma | Req | Aud | Seg | Sis | Descripción | Origen |",
            "|---|---|---|---|---|---|---|---|---|",
            "| `sanic_codigo` | Codigo | texto T(5) | No | No | No | No | x | y |",
            "### 4.2 `sanic_carp_tbl_beta`",
            "| Nombre lógico | Display name | Tipo y forma | Req | Aud | Seg | Sis | Descripción | Origen |",
            "|---|---|---|---|---|---|---|---|---|",
            "| `sanic_codigo` | Codigo | texto T(5) | No | No | No | No | x | y |",
            "### 4.3 `sanic_carp_tbl_gamma`",
            "| Nombre lógico | Display name | Tipo y forma | Req | Aud | Seg | Sis | Descripción | Origen |",
            "|---|---|---|---|---|---|---|---|---|",
            "| `sanic_codigo` | Codigo | texto T(5) | No | No | No | No | x | y |",
            "## 6. Relaciones 1:N",
            "### 6.2 Relaciones que se crean (2)",
            "| ID | Nombre de la relación | Padre | Hija | Lookup (en la hija) | Comportamiento | Req | Aud | Origen del comportamiento |",
            "|---|---|---|---|---|---|---|---|---|",
            "| REL-01 | `sanic_carp_alfa_beta` | Alfa | Beta | `sanic_alfaid` | parental | No | No | x |",
            "| REL-02 | `sanic_carp_beta_gamma` | Beta | Gamma | `sanic_betaid` | restringido | No | No | x |",
            "### 6.3 Relación nativa (no se crea)"
        };

        private static OrganizationServiceEnMemoria AlmacenConLaCadena()
        {
            var esquema = new EsquemaDelAlmacen(LectorDelDiccionario.Leer(Cadena), MuestrasDeFixtures.SinMuestras());
            return new OrganizationServiceEnMemoria(new RelojFijo(), OrganizationServiceEnMemoria.IdDeSystemPorDefecto, esquema);
        }

        private static Guid Fila(OrganizationServiceEnMemoria almacen, string tabla, string lookup = null, string padre = null, Guid? idDelPadre = null)
        {
            var id = Guid.NewGuid();
            var fila = new Entity(tabla, id);
            if (lookup != null) fila[lookup] = new EntityReference(padre, idDelPadre.Value);
            almacen.Sembrar(fila);
            return id;
        }

        // ---- Borrado por relación ----

        [Fact]
        public void UnRestrictDeUnNietoImpideElBorradoDelAbuelo_AunqueElHijoSeaCascade_YNoBorraNada()
        {
            // El plan de borrado baja por los Cascade: si algo restringe más abajo, el Delete entero falla y no queda nada a medias (la plataforma lo hace en una sola transacción).
            var almacen = AlmacenConLaCadena();
            var alfa = Fila(almacen, Alfa);
            var beta = Fila(almacen, Beta, "sanic_alfaid", Alfa, alfa);
            var gamma = Fila(almacen, Gamma, "sanic_betaid", Beta, beta);

            var falla = Falla(() => almacen.Delete(Alfa, alfa));

            ApoyoMedido.AssertFalla(falla, CodigosDeFalla.RestriccionDeRelacion, "sanic_carp_beta_gamma");
            Assert.True(almacen.Existe(Alfa, alfa) && almacen.Existe(Beta, beta) && almacen.Existe(Gamma, gamma));
        }

        [Fact]
        public void UnCascadeBajaPorLasHijas_YBorraLosNietosCascadeDeSusHijas()
        {
            var almacen = AlmacenConLaCadena();
            var alfa = Fila(almacen, Alfa);
            var beta = Fila(almacen, Beta, "sanic_alfaid", Alfa, alfa);

            almacen.Delete(Alfa, alfa);

            Assert.False(almacen.Existe(Alfa, alfa) || almacen.Existe(Beta, beta));
        }

        // ---- Transacción ----

        [Fact]
        public void UnaExcepcionDeUnStepAnidadoQueElPluginExternoAtrapaYSigueAbortaLaTransaccionComoUnaFallaDeEscritura()
        {
            // Extensión de lo medido (4.5, punto 8): una excepción que sube por una escritura anidada se trata igual que una falla de la plataforma. Mata: marcar la transacción solo con FaultException.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            pipeline.Registrar(new PluginEspia("rechaza", accion: l => { throw new InvalidPluginExecutionException("CARP-XXX-001"); }), "Create", "zzf_hija", EtapaDeStep.PreOperation);
            pipeline.Registrar(new PluginEspia("externo", accion: l =>
            {
                var servicio = ((IOrganizationServiceFactory)l.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);
                servicio.Create(Zzf(Fila2));
                try { servicio.Create(new Entity("zzf_hija")); }
                catch (InvalidPluginExecutionException) { /* atrapada: el plugin sigue como si nada */ }
            }), "Create", "zzf_madre", EtapaDeStep.PreOperation);

            var falla = Falla(() => fabrica.CreateOrganizationService(null).Create(new Entity("zzf_madre", Fila1)));

            ApoyoMedido.AssertFalla(falla, CodigosDeFalla.TransaccionAbortada);
            Assert.Empty(fabrica.Almacen.Todas(TablaZzf));
            Assert.False(fabrica.Almacen.Existe("zzf_madre", Fila1));
        }

        // ---- Estado ----

        [Fact]
        public void UnCreateDeCorreoConStatuscodeNoSeMidio_LanzaNotSupportedEnVezDeAdivinar()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var error = Assert.Throws<NotSupportedException>(() => almacen.Create(new Entity("email") { ["statuscode"] = new OptionSetValue(Sanic.Carp.Plugins.Dominio.Estados.StatusCorreoRecibido) }));

            Assert.Contains("no medido", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(almacen.Todas("email"));
        }

        [Fact]
        public void UnUpdateConUnStatuscodeQueNingunEstadoTieneDaElMensajeSinEstado()
        {
            // 13-estados.json midió 'N is not a valid status code on <tabla> with Id …' en el Create; el Update con solo statuscode usa la misma redacción cuando el motivo no existe en ningún estado.
            var almacen = new OrganizationServiceEnMemoria();
            var id = almacen.Create(new Entity("sanic_carp_tbl_conversacion"));

            var falla = Falla(() => almacen.Update(new Entity("sanic_carp_tbl_conversacion", id) { ["statuscode"] = new OptionSetValue(999) }));

            ApoyoMedido.AssertFalla(falla, CodigosDeFalla.ParDeEstadoInvalido, "999 is not a valid status code on sanic_carp_tbl_conversacion with Id " + id);
            Assert.DoesNotContain("for state code", falla.Detail.Message, StringComparison.Ordinal);
        }

        // ---- Valores ----

        [Theory]
        [InlineData("0", true)]
        [InlineData("999999999.99", true)]
        [InlineData("-0.01", false)]
        [InlineData("1000000000", false)]
        public void ElRangoDelDecimalDelDiccionarioIncluyeSusExtremos(string valor, bool cabe)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Guid.NewGuid();
            almacen.Sembrar(new Entity("sanic_carp_tbl_arreglo", existente));
            var escrito = decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture);

            if (cabe) almacen.Update(new Entity("sanic_carp_tbl_arreglo", existente) { ["sanic_saldoinicial"] = escrito });
            else ApoyoMedido.AssertFalla(Falla(() => almacen.Update(new Entity("sanic_carp_tbl_arreglo", existente) { ["sanic_saldoinicial"] = escrito })), CodigosDeFalla.DecimalFueraDeRango);

            Assert.Equal(cabe, almacen.Leer("sanic_carp_tbl_arreglo", existente).Contains("sanic_saldoinicial"));
        }

        [Fact]
        public void UnLocalEnElBordeDelRangoNoDesbordaAlConvertir_ElMinimoFallaPorFechaYElMaximoSeAcepta()
        {
            // La conversión a UTC de DateTime.MinValue o MaxValue con una zona distinta de UTC desborda el tipo: se acota (y MinValue cae bajo 1753, 0x80040239).
            var almacen = new OrganizationServiceEnMemoria { ZonaLocal = ApoyoMedido.ZonaMenosSeis() };

            var deMinimo = Falla(() => almacen.Create(new Entity("sanic_carp_tbl_conversacion") { ["sanic_fechafinalizacion"] = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Local) }));
            var id = almacen.Create(new Entity("sanic_carp_tbl_conversacion") { ["sanic_fechafinalizacion"] = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Local) });

            ApoyoMedido.AssertFalla(deMinimo, CodigosDeFalla.FechaFueraDeRango);
            Assert.Equal(ApoyoMedido.Utc(9999, 12, 31, 23, 59, 59), almacen.Leer("sanic_carp_tbl_conversacion", id)["sanic_fechafinalizacion"]);
        }

        [Fact]
        public void ElTargetDeUnUpdateTambienLlegaConLasFechasEnUtc()
        {
            var almacen = new OrganizationServiceEnMemoria { ZonaLocal = ApoyoMedido.ZonaMenosSeis() };
            var fabrica = new FabricaDeServiciosSimulada(almacen);
            var pipeline = new PipelineSimulado(fabrica);
            var espia = new PluginEspia("preop");
            pipeline.Registrar(espia, "Update", "sanic_carp_tbl_conversacion", EtapaDeStep.PreOperation);
            var id = almacen.Create(new Entity("sanic_carp_tbl_conversacion"));

            fabrica.CreateOrganizationService(null).Update(new Entity("sanic_carp_tbl_conversacion", id) { ["sanic_fechafinalizacion"] = ApoyoMedido.Local(2026, 3, 5, 13, 45, 32) });

            var visto = (DateTime)espia.Llamadas.Single().ClonDelTargetAlEntrar["sanic_fechafinalizacion"];
            Assert.Equal(ApoyoMedido.Utc(2026, 3, 5, 19, 45, 32), visto);
            Assert.Equal(DateTimeKind.Utc, visto.Kind);
        }

        [Fact]
        public void UnNameEnviadoEnUnLookupNoValidadoDeUnaNativaSeDescarta_YQuedaNuloSiElDestinoNoEstaEnElAlmacen()
        {
            // 15-name-del-lookup.json: la plataforma ignora el Name enviado. email.regardingobjectid no se tipa ni se valida, pero el Name igual se descarta (en una tabla zzf_ se conserva).
            var almacen = new OrganizationServiceEnMemoria();

            var id = almacen.Create(new Entity("email") { ["regardingobjectid"] = new EntityReference("sanic_carp_tbl_conversacion", Guid.NewGuid()) { Name = "enviado" } });

            Assert.Null(((EntityReference)almacen.Retrieve("email", id, new ColumnSet("regardingobjectid"))["regardingobjectid"]).Name);
        }

        // ---- Consultas ----

        [Fact]
        public void LasCondicionesMalFormadasDeUnSubfiltroAnidadoTambienFallan()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var consulta = new QueryExpression("sanic_carp_tbl_conversacion") { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddFilter(LogicalOperator.Or).AddCondition("sanic_asunto", ConditionOperator.Null, "x");

            ApoyoMedido.AssertFalla(Falla(() => almacen.RetrieveMultiple(consulta)), CodigosDeFalla.SolicitudInvalida, "requires that no values are set");
        }

        [Fact]
        public void LaCookieSoloSeValidaEnUnaConsultaPaginada_YEnUnaTablaSinEsquemaSoloPorSuXmlYSusColumnasDeOrden()
        {
            var almacen = new OrganizationServiceEnMemoria();
            QueryExpression Consulta(PagingInfo paginacion)
            {
                var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet(), PageInfo = paginacion };
                consulta.AddOrder("zzf_a", OrderType.Ascending);
                return consulta;
            }

            Assert.Empty(almacen.RetrieveMultiple(Consulta(new PagingInfo { PagingCookie = "xyz" })).Entities);                  // sin Count ni PageNumber no pagina: la cookie no se mira
            ApoyoMedido.AssertFalla(Falla(() => almacen.RetrieveMultiple(Consulta(new PagingInfo { Count = 2, PageNumber = 2, PagingCookie = "xyz" }))), CodigosDeFalla.CookieMalFormada);
            ApoyoMedido.AssertFalla(Falla(() => almacen.RetrieveMultiple(Consulta(new PagingInfo { Count = 2, PageNumber = 2, PagingCookie = "<cookie page=\"1\"><zzf_otra last=\"1\" first=\"1\"/><zzf_filaid last=\"{a}\" first=\"{a}\"/></cookie>" }))),
                CodigosDeFalla.CookieDeOtraConsulta, "Order by columns do not match those in paging cookie.");
        }

        // ---- Texto sin acentos ----

        [Fact]
        public void LaTablaExplicitaDeAcentosEstaAlineada_CadaLetraConAcentoDaSuBase_YCoincideConFormDCuandoLaPlataformaNormaliza()
        {
            Assert.Equal(TextoSinAcentos.ConAcento.Length, TextoSinAcentos.Base.Length);
            var normalizaLaPlataforma = "é".Normalize(System.Text.NormalizationForm.FormD).Length == 2;
            for (var i = 0; i < TextoSinAcentos.ConAcento.Length; i++)
            {
                var letra = TextoSinAcentos.ConAcento[i].ToString();
                Assert.Equal(TextoSinAcentos.Base[i].ToString(), TextoSinAcentos.QuitarConLaTabla(letra));
                if (normalizaLaPlataforma) Assert.Equal(letra.Normalize(System.Text.NormalizationForm.FormD)[0], TextoSinAcentos.Base[i]);
            }
        }

        [Fact]
        public void QuitarConLaTablaNoNecesitaQueLaPlataformaNormalice_YDejaLoQueNoTieneDescomposicion()
        {
            // Con la globalización invariante en Linux string.Normalize devuelve el texto tal cual: la tabla y el rango de marcas combinantes tienen que alcanzar solos.
            Assert.Equal("ZZF-Jose Jose Jose Pena", TextoSinAcentos.QuitarConLaTabla("ZZF-José Josè José Peña"));
            Assert.Equal("øæß", TextoSinAcentos.QuitarConLaTabla("øæß"));
        }
    }
}
