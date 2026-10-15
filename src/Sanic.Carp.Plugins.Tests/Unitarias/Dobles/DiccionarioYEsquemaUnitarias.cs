using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text.Json;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Sanic.Carp.Plugins.Tests.Aceptacion.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.Dobles
{
    /// <summary>
    /// Que el esquema del doble sale del diccionario y de las muestras REALES, y no de listas escritas a medida (orden de la vuelta 2). Las pruebas de aceptación miden el doble contra el diccionario
    /// verdadero; estas lo miden contra un diccionario y unas muestras INVENTADAS acá: si el doble tuviera el formato, las columnas o las plataformas escritas a mano, no pasarían.
    /// </summary>
    public class DiccionarioYEsquemaUnitarias
    {
        private static readonly string[] Documento =
        {
            "## 3. Choices globales y motivos de estado",
            "### 3.1 Choices globales (9)",
            "#### `sanic_carp_ch_color`",
            "| Valor | Etiqueta (1033 y 3082) | Descripción |",
            "|---|---|---|",
            "| 159460001 | Rojo | El rojo |",
            "| 159460007 | Azul | El azul |",
            "### 3.2 Motivos de estado de las tablas (`statecode` / `statuscode`)",
            "## 4. Tablas",
            "### 4.1 `sanic_carp_tbl_cliente`",
            "| Nombre lógico | Display name | Tipo y forma | Req | Aud | Seg | Sis | Descripción | Origen |",
            "|---|---|---|---|---|---|---|---|---|",
            "| `sanic_nombre` | Numero | primaria autonumérica `QRS-{SEQNUM:4}` (P-02) | No | Sí | No | No | x | y |",
            "| `sanic_edad` | Edad | entero [0..120] | No | No | No | No | x | y |",
            "| `sanic_apodo` | Apodo | texto T(5) | No | No | No | No | x | y |",
            "### 4.2 `sanic_carp_tbl_pedido`",
            "| Nombre lógico | Display name | Tipo y forma | Req | Aud | Seg | Sis | Descripción | Origen |",
            "|---|---|---|---|---|---|---|---|---|",
            "| `sanic_nombre` | Codigo | primaria calculada, texto T(30) | Sí | No | No | No | x | y |",
            "| `sanic_color` | Color | choice `sanic_carp_ch_color` | No | No | No | No | x | y |",
            "| `sanic_total` | Total | decimal(2) | No | No | No | No | x | y |",
            "| `sanic_entrega` | Entrega | fecha DO | No | No | No | No | x | y |",
            "Lookups (nacen con su relación, sección 6):",
            "| Nombre lógico | Display name | Destino | Req | Aud | Relación | Origen |",
            "| `sanic_clienteid` | Cliente | `sanic_carp_tbl_cliente` | No | No | REL-01 | x |",
            "## 5. Columnas propias sobre tablas estándar",
            "| Tabla | Nombre lógico | Display name | Tipo y forma | Req | Aud | Seg | Sis | Descripción | Origen |",
            "|---|---|---|---|---|---|---|---|---|---|",
            "| `email` | `sanic_puntaje` | Puntaje | entero [0..10] | No | No | No | No | x | y |",
            "| `email` | `sanic_atendidoporid` (lookup a `systemuser`) | Atendido por | lookup, destino `systemuser`; opcional; relación REL-02 | No | No | No | Sí | x | y |",
            "## 6. Relaciones 1:N",
            "### 6.2 Relaciones que se crean (2)",
            "| ID | Nombre de la relación | Padre | Hija | Lookup (en la hija) | Comportamiento | Req | Aud | Origen del comportamiento |",
            "|---|---|---|---|---|---|---|---|---|",
            "| REL-01 | `sanic_carp_cliente_pedido` | Cliente | Pedido | `sanic_clienteid` | restringido | No | No | x |",
            "| REL-02 | `sanic_carp_systemuser_pedido_vendedorid` | Usuario | Pedido | `sanic_vendedorid` | restringido | No | Sí | y |",
            "### 6.3 Relación nativa (no se crea)"
        };

        private static OrganizationServiceEnMemoria AlmacenCon(string[] documento, string muestrasJson = null)
        {
            var muestras = muestrasJson == null ? MuestrasDeFixtures.SinMuestras() : MuestrasDeFixtures.Leer(new[] { JsonDocument.Parse(muestrasJson).RootElement });
            var esquema = new EsquemaDelAlmacen(LectorDelDiccionario.Leer(documento), muestras);
            return new OrganizationServiceEnMemoria(new RelojFijo(), OrganizationServiceEnMemoria.IdDeSystemPorDefecto, esquema);
        }

        // ---- El lector del diccionario ----

        [Fact]
        public void Lector_ElFormatoAutonumericoSaleDelDocumento_NoDeUnaConstante()
        {
            var leido = LectorDelDiccionario.Leer(Documento);

            var formato = leido.Autonumericos["sanic_carp_tbl_cliente"];
            Assert.Equal("QRS", formato.Prefijo);
            Assert.Equal(4, formato.Digitos);
            Assert.Equal("QRS-0007", formato.Dar(7));
            Assert.Equal("QRS-123456", formato.Dar(123456));   // el relleno es un mínimo: nunca se trunca
            Assert.DoesNotContain("sanic_carp_tbl_pedido", leido.Autonumericos.Keys);
        }

        [Fact]
        public void Lector_ElDestinoDeUnLookupSaleDelNombreDeLaRelacion_PadreDeLaTablaOSystemuser()
        {
            var leido = LectorDelDiccionario.Leer(Documento);

            var columnas = leido.Tablas["sanic_carp_tbl_pedido"].ToDictionary(c => c.Nombre, StringComparer.Ordinal);
            Assert.Equal(TipoDeColumna.Lookup, columnas["sanic_clienteid"].Tipo);
            Assert.Equal("sanic_carp_tbl_cliente", columnas["sanic_clienteid"].Destino);
            Assert.Equal("systemuser", columnas["sanic_vendedorid"].Destino);   // el sufijo del nombre de la relación no cambia al padre
            Assert.DoesNotContain("sanic_clienteid", leido.Tablas["sanic_carp_tbl_cliente"].Select(c => c.Nombre));
        }

        [Fact]
        public void Lector_LeeClaseLargoYChoiceDeCadaColumna_YLosValoresDelChoiceDeLaSeccion31()
        {
            var leido = LectorDelDiccionario.Leer(Documento);

            var cliente = leido.Tablas["sanic_carp_tbl_cliente"].ToDictionary(c => c.Nombre, StringComparer.Ordinal);
            var pedido = leido.Tablas["sanic_carp_tbl_pedido"].ToDictionary(c => c.Nombre, StringComparer.Ordinal);
            Assert.Equal(TipoDeColumna.Entero, cliente["sanic_edad"].Tipo);
            Assert.Equal(TipoDeColumna.Texto, cliente["sanic_apodo"].Tipo);
            Assert.Equal(5, cliente["sanic_apodo"].Largo);
            Assert.Equal(30, pedido["sanic_nombre"].Largo);
            Assert.Equal(TipoDeColumna.Choice, pedido["sanic_color"].Tipo);
            Assert.Equal("color", pedido["sanic_color"].Choice);
            Assert.Equal(TipoDeColumna.Decimal, pedido["sanic_total"].Tipo);
            Assert.Equal(TipoDeColumna.FechaSolo, pedido["sanic_entrega"].Tipo);
            Assert.Equal(new[] { 159460001, 159460007 }, leido.Choices["color"].ToArray());
        }

        [Fact]
        public void Lector_LeeLasColumnasPropiasSobreTablasEstandarDeLaSeccion5_ConSuDestino()
        {
            var leido = LectorDelDiccionario.Leer(Documento);

            var correo = leido.ColumnasSobreEstandar["email"].ToDictionary(c => c.Nombre, StringComparer.Ordinal);
            Assert.Equal(TipoDeColumna.Entero, correo["sanic_puntaje"].Tipo);
            Assert.Equal(TipoDeColumna.Lookup, correo["sanic_atendidoporid"].Tipo);
            Assert.Equal("systemuser", correo["sanic_atendidoporid"].Destino);
        }

        [Fact]
        public void Lector_UnTipoQueNoConoceLanzaEnVezDeDejarLaColumnaSinValidar()
        {
            var conTipoRaro = Documento.Select(l => l.Contains("sanic_edad", StringComparison.Ordinal) ? "| `sanic_edad` | Edad | geometria | No | No | No | No | x | y |" : l);

            var error = Assert.Throws<InvalidOperationException>(() => LectorDelDiccionario.Leer(conTipoRaro));

            Assert.Contains("sanic_edad", error.Message, StringComparison.Ordinal);
            Assert.Contains("geometria", error.Message, StringComparison.Ordinal);
        }

        // ---- Las muestras de los fixtures ----

        private const string FixtureDeMuestra = @"{
          ""cabecera"": {},
          ""contenido"": {
            ""fila"": {
              ""tipo"": ""Microsoft.Xrm.Sdk.Entity"", ""LogicalName"": ""team"", ""Id"": ""0fff0002-0000-4000-8000-000000000018"",
              ""atributos"": {
                ""name"": { ""tipo"": ""System.String"", ""valor"": ""ZZF"" },
                ""createdby"": { ""tipo"": ""Microsoft.Xrm.Sdk.EntityReference"", ""valor"": { ""logicalName"": ""systemuser"", ""id"": ""0fff0001-0000-4000-8000-000000000008"", ""name"": null } },
                ""al.x"": { ""tipo"": ""Microsoft.Xrm.Sdk.AliasedValue"", ""valor"": {} },
                ""from"": { ""tipo"": ""Microsoft.Xrm.Sdk.EntityCollection"", ""valor"": { ""Entities"": [
                   { ""tipo"": ""Microsoft.Xrm.Sdk.Entity"", ""LogicalName"": ""activityparty"", ""Id"": ""0fff0002-0000-4000-8000-000000000019"", ""atributos"": { ""partyid"": { ""tipo"": ""System.Guid"", ""valor"": ""0fff0002-0000-4000-8000-000000000019"" } } } ] } }
              }
            },
            ""otra"": {
              ""tipo"": ""Microsoft.Xrm.Sdk.Entity"", ""LogicalName"": ""team"", ""Id"": ""0fff0002-0000-4000-8000-000000000020"",
              ""atributos"": { ""createdby"": { ""tipo"": ""Microsoft.Xrm.Sdk.EntityReference"", ""valor"": { ""logicalName"": ""team"", ""id"": ""0fff0002-0000-4000-8000-000000000021"", ""name"": null } } }
            }
          }
        }";

        [Fact]
        public void Muestras_UnaColumnaAliasadaYUnaEntidadAnidadaNoSonColumnasNiTablas()
        {
            var muestras = MuestrasDeFixtures.Leer(new[] { JsonDocument.Parse(FixtureDeMuestra).RootElement });

            Assert.Equal(new[] { "team" }, muestras.Tablas.Keys.ToArray());
            Assert.Equal(new[] { "createdby", "from", "name" }, muestras.Tablas["team"].Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void Muestras_UnLookupAcumulaLosDestinosDeTodasLasFilasObservadas_YGuardaElTipoNet()
        {
            var muestras = MuestrasDeFixtures.Leer(new[] { JsonDocument.Parse(FixtureDeMuestra).RootElement });

            var creadoPor = muestras.Tablas["team"]["createdby"];
            Assert.Equal("Microsoft.Xrm.Sdk.EntityReference", creadoPor.TipoNet);
            Assert.Equal(new[] { "systemuser", "team" }, creadoPor.Destinos.OrderBy(d => d, StringComparer.Ordinal).ToArray());
            Assert.Equal("System.String", muestras.Tablas["team"]["name"].TipoNet);
        }

        // ---- El esquema armado con ellos ----

        [Fact]
        public void Esquema_ElAutonumericoDeUnaTablaToma_ElFormatoDelDiccionarioQueSeLeDa()
        {
            var almacen = AlmacenCon(Documento);

            var primero = almacen.Create(new Entity("sanic_carp_tbl_cliente"));
            var segundo = almacen.Create(new Entity("sanic_carp_tbl_cliente"));

            Assert.Equal("QRS-0001", almacen.Leer("sanic_carp_tbl_cliente", primero)["sanic_nombre"]);
            Assert.Equal("QRS-0002", almacen.Leer("sanic_carp_tbl_cliente", segundo)["sanic_nombre"]);
            // La primaria de la otra tabla no es autonumérica en ESTE diccionario: no se completa.
            var pedido = almacen.Create(new Entity("sanic_carp_tbl_pedido"));
            Assert.False(almacen.Leer("sanic_carp_tbl_pedido", pedido).Contains("sanic_nombre"));
        }

        [Fact]
        public void Esquema_LasColumnasYLosTiposDeUnaTablaPropiaSonLosDelDiccionarioQueSeLeDa_NoLosDelProyecto()
        {
            var almacen = AlmacenCon(Documento);

            var cliente = Guid.NewGuid();
            almacen.Sembrar(new Entity("sanic_carp_tbl_cliente", cliente));   // lote 3b (integridad referencial, 0x80040217): el destino del lookup existe
            almacen.Create(new Entity("sanic_carp_tbl_pedido") { ["sanic_color"] = new OptionSetValue(159460007), ["sanic_total"] = 1m, ["sanic_clienteid"] = new EntityReference("sanic_carp_tbl_cliente", cliente) });
            // lote 3b, medido en Dev: FaultException con el ErrorCode medido (antes ArgumentException).
            var deChoice = ApoyoDeDobles.Falla(() => almacen.Create(new Entity("sanic_carp_tbl_pedido") { ["sanic_color"] = new OptionSetValue(159460002) }));
            var deColumnaDelProyecto = ApoyoDeDobles.Falla(() => almacen.Create(new Entity("sanic_carp_tbl_pedido") { ["sanic_fechainicio"] = new DateTime(2026, 1, 1) }));
            var deLargo = ApoyoDeDobles.Falla(() => almacen.Create(new Entity("sanic_carp_tbl_cliente") { ["sanic_apodo"] = "seis!!" }));

            ApoyoMedido.AssertFalla(deChoice, CodigosDeFalla.OpcionInexistente, "sanic_color");
            ApoyoMedido.AssertFalla(deColumnaDelProyecto, CodigosDeFalla.ColumnaInexistente, "sanic_fechainicio");
            ApoyoMedido.AssertFalla(deLargo, CodigosDeFalla.TextoDemasiadoLargo, "sanic_apodo");
        }

        [Fact]
        public void Esquema_LasColumnasDePlataformaDeUnaTablaPropiaSalenDeLasMuestras_SinMuestrasNoHay()
        {
            const string muestraConPlataforma = @"{ ""contenido"": { ""fila"": {
              ""tipo"": ""Microsoft.Xrm.Sdk.Entity"", ""LogicalName"": ""sanic_carp_tbl_cliente"", ""Id"": ""0fff0002-0000-4000-8000-000000000018"",
              ""atributos"": {
                ""createdon"": { ""tipo"": ""System.DateTime"", ""valor"": ""2026-10-14T15:06:51.0000000Z"", ""kind"": ""Utc"" },
                ""statecode"": { ""tipo"": ""Microsoft.Xrm.Sdk.OptionSetValue"", ""valor"": 0 },
                ""sanic_cedula"": { ""tipo"": ""System.String"", ""valor"": ""una columna del proyecto en la muestra no entra"" }
              } } } }";
            var conMuestra = AlmacenCon(Documento, muestraConPlataforma);
            var sinMuestra = AlmacenCon(Documento);

            // La columna de plataforma es CONOCIDA si se puede leer (no se escribe: createdon es de solo lectura para Create y Update, cierre del lote 3, H8). Se siembra la fila y se la pide en el ColumnSet.
            var id = Guid.NewGuid();
            conMuestra.Sembrar(new Entity("sanic_carp_tbl_cliente", id) { ["createdon"] = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc), ["statecode"] = new OptionSetValue(0) });
            Assert.True(conMuestra.Retrieve("sanic_carp_tbl_cliente", id, new ColumnSet("createdon", "statecode")).Contains("createdon"));
            // lote 3b, medido en Dev: faltas con ErrorCode (tipo 0x80040203; columna inexistente 0x80041103), antes ArgumentException e InvalidOperationException.
            var deTipo = ApoyoDeDobles.Falla(() => conMuestra.Create(new Entity("sanic_carp_tbl_cliente") { ["statecode"] = 0 }));
            var deColumna = ApoyoDeDobles.Falla(() => sinMuestra.Retrieve("sanic_carp_tbl_cliente", id, new ColumnSet("createdon")));
            var deProyecto = ApoyoDeDobles.Falla(() => conMuestra.Create(new Entity("sanic_carp_tbl_cliente") { ["sanic_cedula"] = "x" }));

            ApoyoMedido.AssertFalla(deTipo, CodigosDeFalla.SolicitudInvalida);
            ApoyoMedido.AssertFalla(deColumna, CodigosDeFalla.ColumnaInexistente, "createdon");
            ApoyoMedido.AssertFalla(deProyecto, CodigosDeFalla.ColumnaInexistente, "sanic_cedula");
        }

        [Fact]
        public void Esquema_LosNombresDeUnaTablaNativaSalenDeSusMuestrasYDeLaSeccion5_NoDeUnaListaFija()
        {
            const string muestraDeCorreo = @"{ ""contenido"": { ""fila"": {
              ""tipo"": ""Microsoft.Xrm.Sdk.Entity"", ""LogicalName"": ""email"", ""Id"": ""0fff0002-0000-4000-8000-000000000018"",
              ""atributos"": { ""subject"": { ""tipo"": ""System.String"", ""valor"": ""x"" } } } } }";
            var almacen = AlmacenCon(Documento, muestraDeCorreo);

            almacen.Create(new Entity("email") { ["subject"] = "ZZF", ["sanic_puntaje"] = 3 });
            // lote 3b, medido en Dev: columna 0x80041103, tipo 0x80040203, lookup a otra tabla 0x80040217 (nombra la tabla destino declarada).
            var deColumna = ApoyoDeDobles.Falla(() => almacen.Create(new Entity("email") { ["messageid"] = "<x@example.com>" }));
            var deTipo = ApoyoDeDobles.Falla(() => almacen.Create(new Entity("email") { ["sanic_puntaje"] = 3m }));
            var deDestino = ApoyoDeDobles.Falla(() => almacen.Create(new Entity("email") { ["sanic_atendidoporid"] = new EntityReference("team", Guid.NewGuid()) }));

            ApoyoMedido.AssertFalla(deColumna, CodigosDeFalla.ColumnaInexistente, "messageid");
            ApoyoMedido.AssertFalla(deTipo, CodigosDeFalla.SolicitudInvalida, "Incorrect attribute value type System.Decimal");
            ApoyoMedido.AssertFalla(deDestino, CodigosDeFalla.FilaInexistente, "Entity 'systemuser' With Id = ");
        }

        [Fact]
        public void Esquema_UnaTablaQueNiElDiccionarioNiLasMuestrasNombranSeRechazaPorLecturaYPorEscritura()
        {
            var almacen = AlmacenCon(Documento);

            // lote 3b, medido en Dev: 0x80041102 (tabla inexistente) en lectura y escritura.
            var deLectura = ApoyoDeDobles.Falla(() => almacen.Retrieve("email", Guid.NewGuid(), new ColumnSet(true)));
            var deEscritura = ApoyoDeDobles.Falla(() => almacen.Create(new Entity("email")));

            ApoyoMedido.AssertFalla(deLectura, CodigosDeFalla.TablaInexistente, "'email'");
            ApoyoMedido.AssertFalla(deEscritura, CodigosDeFalla.TablaInexistente, "'email'");
        }
    }
}
