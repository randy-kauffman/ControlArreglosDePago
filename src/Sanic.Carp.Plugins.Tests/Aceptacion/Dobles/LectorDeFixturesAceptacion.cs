using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text.Json;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Xunit;
using AppContext = System.AppContext;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// El lector de fixtures (<c>Apoyo/LectorDeFixtures</c>) reconstruye EXACTAMENTE los tipos y valores del JSON (diseno/03 4.5, lote 3).
    /// Si el lector se equivoca, <c>DobleContraRespuestasRealesAceptacion</c> mediría el doble contra un esperado falso; por eso el lector
    /// tiene su propia aceptación, y se mide contra el TEXTO del JSON, no contra otra lectura del lector:
    /// <list type="bullet">
    /// <item>el tipo .NET se compara por <c>GetType().FullName</c> contra el campo <c>tipo</c>;</item>
    /// <item>un <c>DateTime</c> se compara por su texto <c>yyyy-MM-ddTHH:mm:ss.fffffff</c> + <c>Z</c> si es Utc, y por su <c>Kind</c> contra <c>kind</c>;</item>
    /// <item>un <c>decimal</c> se compara por su texto invariante, que incluye la escala (<c>"1000.5000000000"</c>);</item>
    /// <item>las claves de <c>Attributes</c> se comparan como conjunto: ni una nula de más ni una de menos.</item>
    /// </list>
    /// Mutaciones que cada fila mata: leer el decimal con <c>double</c> o con <c>decimal.Parse</c> normalizado (PA-LFX-01, 03, 09: pierde la
    /// escala); dejar el <c>Kind</c> por defecto de <c>DateTime.Parse</c> (<c>Local</c>/<c>Utc</c> según el texto: PA-LFX-01, 03, 10); poner
    /// <c>null</c> en vez de ausente (PA-LFX-01); devolver <c>EntityReference</c> donde el JSON dice <c>System.Guid</c> (PA-LFX-01, 03);
    /// rellenar <c>Name</c> (PA-LFX-01, 11); perder la cadena de <c>InnerFault</c> o el tipo de <c>ErrorDetails</c> (PA-LFX-04).
    /// </summary>
    public class LectorDeFixturesAceptacion
    {
        private static readonly string[] Archivos =
        {
            "00-incidencias-y-tipos.json", "01-email.json", "02-conversacion.json", "03-arreglo.json", "04-teammembership-team-queue.json",
            "05-environmentvariable.json", "06-whoami-systemuser.json", "07-fallas.json", "08-aliased-y-agregados.json"
        };

        public static IEnumerable<object[]> TodosLosArchivos()
        {
            foreach (var archivo in Archivos) yield return new object[] { archivo };
        }

        /// <summary>Los que traen filas (el 00 solo lista tipos): sobre ellos el lector tiene que trabajar, y una prueba que no lo llama no puede quedar en verde.</summary>
        public static IEnumerable<object[]> ArchivosConEntidades()
        {
            foreach (var archivo in Archivos.Where(a => !a.StartsWith("00-", StringComparison.Ordinal))) yield return new object[] { archivo };
        }

        /// <summary>Los que traen alguna <c>EntityCollection</c> con <c>Entities</c>.</summary>
        public static IEnumerable<object[]> ArchivosConColecciones()
        {
            foreach (var archivo in new[] { "01-email.json", "04-teammembership-team-queue.json", "05-environmentvariable.json", "06-whoami-systemuser.json", "07-fallas.json", "08-aliased-y-agregados.json" })
                yield return new object[] { archivo };
        }

        private static JsonElement Leer(string archivo)
        {
            var ruta = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dataverse", archivo);
            using (var doc = JsonDocument.Parse(File.ReadAllBytes(ruta))) return doc.RootElement.Clone();
        }

        private static JsonElement Json(string texto)
        {
            using (var doc = JsonDocument.Parse(texto)) return doc.RootElement.Clone();
        }

        // ---- Recorrido independiente del JSON ----

        private static void Recorrer(JsonElement nodo, Action<JsonElement> alVisitar)
        {
            if (nodo.ValueKind == JsonValueKind.Object)
            {
                alVisitar(nodo);
                foreach (var propiedad in nodo.EnumerateObject()) Recorrer(propiedad.Value, alVisitar);
            }
            else if (nodo.ValueKind == JsonValueKind.Array)
            {
                foreach (var elemento in nodo.EnumerateArray()) Recorrer(elemento, alVisitar);
            }
        }

        private static string Tipo(JsonElement nodo)
        {
            JsonElement tipo;
            return nodo.ValueKind == JsonValueKind.Object && nodo.TryGetProperty("tipo", out tipo) && tipo.ValueKind == JsonValueKind.String ? tipo.GetString() : null;
        }

        private static string TextoDeFecha(DateTime fecha)
        {
            return fecha.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + (fecha.Kind == DateTimeKind.Utc ? "Z" : string.Empty);
        }

        // ---- Verificación de un valor contra su nodo ----

        private static void VerificarValor(string ruta, JsonElement nodo, object valor)
        {
            var tipo = nodo.GetProperty("tipo").GetString();
            Assert.True(valor != null, ruta + ": el lector devolvió null para un valor de tipo " + tipo + " (las columnas nulas llegan AUSENTES)");
            Assert.True(tipo == valor.GetType().FullName, ruta + ": tipo " + valor.GetType().FullName + " en vez de " + tipo);
            var v = nodo.GetProperty("valor");
            switch (tipo)
            {
                case "System.Guid": Assert.Equal(Guid.Parse(v.GetString()), (Guid)valor); break;
                case "System.String": Assert.Equal(v.GetString(), (string)valor); break;
                case "System.Int32": Assert.Equal(v.GetInt32(), (int)valor); break;
                case "System.Boolean": Assert.Equal(v.GetBoolean(), (bool)valor); break;
                case "System.DateTime":
                    {
                        var fecha = (DateTime)valor;
                        Assert.True(nodo.GetProperty("kind").GetString() == fecha.Kind.ToString(), ruta + ": Kind " + fecha.Kind + " en vez de " + nodo.GetProperty("kind").GetString());
                        Assert.True(v.GetString() == TextoDeFecha(fecha), ruta + ": " + TextoDeFecha(fecha) + " en vez de " + v.GetString());
                        break;
                    }
                case "System.Decimal":
                    Assert.True(v.GetString() == ((decimal)valor).ToString(CultureInfo.InvariantCulture),
                        ruta + ": " + ((decimal)valor).ToString(CultureInfo.InvariantCulture) + " en vez de " + v.GetString() + " (valor Y escala)");
                    break;
                case "Microsoft.Xrm.Sdk.OptionSetValue": Assert.Equal(v.GetInt32(), ((OptionSetValue)valor).Value); break;
                case "Microsoft.Xrm.Sdk.EntityReference":
                    {
                        var r = (EntityReference)valor;
                        Assert.Equal(v.GetProperty("logicalName").GetString(), r.LogicalName);
                        Assert.Equal(Guid.Parse(v.GetProperty("id").GetString()), r.Id);
                        var nombre = v.GetProperty("name");
                        Assert.Equal(nombre.ValueKind == JsonValueKind.Null ? null : nombre.GetString(), r.Name);
                        break;
                    }
                case "Microsoft.Xrm.Sdk.EntityCollection": VerificarColeccion(ruta, v, (EntityCollection)valor); break;
                case "Microsoft.Xrm.Sdk.AliasedValue":
                    {
                        var a = (AliasedValue)valor;
                        Assert.Equal(v.GetProperty("entityLogicalName").GetString(), a.EntityLogicalName);
                        Assert.Equal(v.GetProperty("attributeLogicalName").GetString(), a.AttributeLogicalName);
                        VerificarValor(ruta + ".Value", v.GetProperty("valor"), a.Value);
                        break;
                    }
                case "Microsoft.Xrm.Sdk.BooleanManagedProperty":
                    {
                        var b = (BooleanManagedProperty)valor;
                        Assert.Equal(v.GetProperty("Value").GetBoolean(), b.Value);
                        Assert.Equal(v.GetProperty("CanBeChanged").GetBoolean(), b.CanBeChanged);
                        Assert.Equal(v.GetProperty("ManagedPropertyLogicalName").GetString(), b.ManagedPropertyLogicalName);
                        break;
                    }
                default:
                    throw new Xunit.Sdk.XunitException(ruta + ": la verificación no contempla el tipo '" + tipo + "'; hay que agregarlo al lector y a esta prueba");
            }
        }

        private static void VerificarEntidad(string ruta, JsonElement nodo, Entity entidad)
        {
            Assert.Equal(nodo.GetProperty("LogicalName").GetString(), entidad.LogicalName);
            Assert.Equal(Guid.Parse(nodo.GetProperty("Id").GetString()), entidad.Id);
            var version = nodo.GetProperty("RowVersion");
            Assert.Equal(version.ValueKind == JsonValueKind.Null ? null : version.GetString(), entidad.RowVersion);
            Assert.Null(entidad.EntityState);
            Assert.Empty(entidad.KeyAttributes);

            var atributos = nodo.GetProperty("atributos");
            var esperadas = atributos.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var recibidas = entidad.Attributes.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.Equal(esperadas, recibidas);
            foreach (var propiedad in atributos.EnumerateObject()) VerificarValor(ruta + "." + propiedad.Name, propiedad.Value, entidad.Attributes[propiedad.Name]);

            var formateados = nodo.GetProperty("FormattedValues");
            Assert.Equal(formateados.EnumerateObject().Count(), entidad.FormattedValues.Count);
            foreach (var propiedad in formateados.EnumerateObject()) Assert.Equal(propiedad.Value.GetString(), entidad.FormattedValues[propiedad.Name]);
        }

        private static void VerificarColeccion(string ruta, JsonElement nodo, EntityCollection coleccion)
        {
            Assert.Equal(nodo.GetProperty("EntityName").GetString(), coleccion.EntityName);
            Assert.Equal(nodo.GetProperty("MoreRecords").GetBoolean(), coleccion.MoreRecords);
            var cookie = nodo.GetProperty("PagingCookie");
            Assert.Equal(cookie.ValueKind == JsonValueKind.Null ? null : cookie.GetString(), coleccion.PagingCookie);
            Assert.Equal(nodo.GetProperty("TotalRecordCount").GetInt32(), coleccion.TotalRecordCount);
            Assert.Equal(nodo.GetProperty("TotalRecordCountLimitExceeded").GetBoolean(), coleccion.TotalRecordCountLimitExceeded);
            Assert.Equal(nodo.GetProperty("MinActiveRowVersion").GetString(), coleccion.MinActiveRowVersion);
            var entidades = nodo.GetProperty("Entities");
            Assert.Equal(nodo.GetProperty("cantidad").GetInt32(), entidades.GetArrayLength());
            Assert.Equal(entidades.GetArrayLength(), coleccion.Entities.Count);
            for (int i = 0; i < coleccion.Entities.Count; i++) VerificarEntidad(ruta + "[" + i + "]", entidades[i], coleccion.Entities[i]);
        }

        // ---- Las filas ----

        [Theory]
        [MemberData(nameof(ArchivosConEntidades))]
        public void PA_LFX_01_TodaEntidadDeCadaFixtureSeReconstruyeConLosTiposLosKindLasEscalasYLasClavesDelJson(string archivo)
        {
            var raiz = Leer(archivo);
            int visitadas = 0;
            Recorrer(raiz, nodo =>
            {
                if (Tipo(nodo) != "Microsoft.Xrm.Sdk.Entity" || !nodo.TryGetProperty("atributos", out _)) return;
                var entidad = LectorDeFixtures.Entidad(nodo);
                VerificarEntidad(archivo + ":" + nodo.GetProperty("LogicalName").GetString(), nodo, entidad);
                visitadas++;
            });
            Assert.True(visitadas > 0, archivo + ": el recorrido no encontró ninguna entidad (prueba vacía)");
        }

        [Fact]
        public void PA_LFX_01b_ElRecorridoVeLasCantidadesDeEntidadesQueTieneCadaArchivo_VERDE_A_PROPOSITO()
        {
            // Contra una prueba vacía: si el recorrido no encontrara nodos, PA_LFX_01 pasaría sin medir nada. Los conteos salen de contar a mano
            // los nodos "Microsoft.Xrm.Sdk.Entity" con "atributos" del JSON (python, 2026-10-14).
            var esperadas = new Dictionary<string, int>
            {
                { "01-email.json", 18 }, { "02-conversacion.json", 3 }, { "03-arreglo.json", 4 }, { "04-teammembership-team-queue.json", 6 },
                { "05-environmentvariable.json", 2 }, { "06-whoami-systemuser.json", 4 }, { "07-fallas.json", 4 }, { "08-aliased-y-agregados.json", 7 }
            };
            foreach (var par in esperadas)
            {
                int visitadas = 0;
                Recorrer(Leer(par.Key), nodo => { if (Tipo(nodo) == "Microsoft.Xrm.Sdk.Entity" && nodo.TryGetProperty("atributos", out _)) visitadas++; });
                Assert.True(visitadas == par.Value, par.Key + ": " + visitadas + " entidades en el JSON y se esperaban " + par.Value);
            }
        }

        [Theory]
        [MemberData(nameof(ArchivosConColecciones))]
        public void PA_LFX_02_TodaColeccionConservaLaFormaDeLaPaginacion(string archivo)
        {
            int visitadas = 0;
            Recorrer(Leer(archivo), nodo =>
            {
                if (Tipo(nodo) != "Microsoft.Xrm.Sdk.EntityCollection" || !nodo.TryGetProperty("Entities", out _)) return;
                VerificarColeccion(archivo, nodo, LectorDeFixtures.Coleccion(nodo));
                visitadas++;
            });
            Assert.True(visitadas > 0, archivo + ": el recorrido no encontró ninguna colección (prueba vacía)");
        }

        [Fact]
        public void PA_LFX_02b_LasColeccionesDeLaPaginacionTraenCookieYTotales()
        {
            var fixture = LectorDeFixtures.Abrir("07-fallas.json");
            var pagina1 = fixture.Coleccion("paginacion", "pagina_1");
            Assert.True(pagina1.MoreRecords);
            Assert.Equal(4, pagina1.TotalRecordCount);
            Assert.StartsWith("<cookie page=\"1\">", pagina1.PagingCookie, StringComparison.Ordinal);
            Assert.Equal(2, pagina1.Entities.Count);
            Assert.Equal("-1", pagina1.MinActiveRowVersion);
            var pagina2 = fixture.Coleccion("paginacion", "pagina_2");
            Assert.False(pagina2.MoreRecords);
            Assert.StartsWith("<cookie page=\"2\">", pagina2.PagingCookie, StringComparison.Ordinal);

            var vacia = LectorDeFixtures.Abrir("05-environmentvariable.json").Coleccion("definiciones_del_proyecto_sanic_carp_ev_");
            Assert.Equal(string.Empty, vacia.PagingCookie);
            Assert.Empty(vacia.Entities);

            var activityparty = LectorDeFixtures.Abrir("01-email.json").Entidad("retrieve_columnset_true_entrante", "0");
            Assert.Null(((EntityCollection)activityparty["bcc"]).PagingCookie);
        }

        // ---- Valores pinneados a mano (el esperado está escrito en la prueba, no leído del JSON) ----

        [Fact]
        public void PA_LFX_03_ValoresPinneadosAMano_Conversacion()
        {
            var abierta = LectorDeFixtures.Abrir("02-conversacion.json").Entidad("abierta");
            Assert.Equal("sanic_carp_tbl_conversacion", abierta.LogicalName);
            Assert.Equal(new Guid("0fff0009-0000-4000-8000-000000000016"), abierta.Id);
            Assert.Equal("13059426", abierta.RowVersion);

            var creado = Assert.IsType<DateTime>(abierta["createdon"]);
            Assert.Equal(new DateTime(2026, 10, 14, 15, 6, 51, DateTimeKind.Utc), creado);
            Assert.Equal(DateTimeKind.Utc, creado.Kind);

            var propietario = Assert.IsType<EntityReference>(abierta["ownerid"]);
            Assert.Equal("team", propietario.LogicalName);
            Assert.Equal("ZZF Team 1", propietario.Name);

            var equipoPropietario = Assert.IsType<EntityReference>(abierta["owningteam"]);
            Assert.Null(equipoPropietario.Name);
            Assert.Equal("team", equipoPropietario.LogicalName);

            Assert.Equal(0, Assert.IsType<int>(abierta["sanic_cantidadrespuestas"]));
            Assert.False(Assert.IsType<bool>(abierta["sanic_correonuevoenfinalizada"]));
            Assert.Equal(1, Assert.IsType<OptionSetValue>(abierta["statuscode"]).Value);
            Assert.False(abierta.Contains("sanic_unidaaid"), "una columna nula llega AUSENTE");
            Assert.False(abierta.Contains("sanic_fechafinalizacion"));

            var unida = LectorDeFixtures.Abrir("02-conversacion.json").Entidad("unida");
            Assert.Equal(159460001, Assert.IsType<OptionSetValue>(unida["statuscode"]).Value);
            Assert.Equal("sanic_carp_tbl_conversacion", Assert.IsType<EntityReference>(unida["sanic_unidaaid"]).LogicalName);
        }

        [Fact]
        public void PA_LFX_03_ValoresPinneadosAMano_ArregloConFechasDeSoloFechaYDecimalesConEscala10()
        {
            var borrador = LectorDeFixtures.Abrir("03-arreglo.json").Entidad("borrador_congelado");

            var inicio = Assert.IsType<DateTime>(borrador["sanic_fechainicio"]);
            Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified), inicio);
            Assert.Equal(DateTimeKind.Unspecified, inicio.Kind);
            Assert.Equal(DateTimeKind.Unspecified, Assert.IsType<DateTime>(borrador["sanic_fechafin"]).Kind);

            var congelamiento = Assert.IsType<DateTime>(borrador["sanic_fechacongelamiento"]);
            Assert.Equal(DateTimeKind.Utc, congelamiento.Kind);

            var saldo = Assert.IsType<decimal>(borrador["sanic_saldoinicial"]);
            Assert.Equal(1000.5m, saldo);
            Assert.Equal("1000.5000000000", saldo.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(10, ApoyoDeDobles.EscalaDe(saldo));
            Assert.Equal("150.0400000000", Assert.IsType<decimal>(borrador["sanic_cuotamensual"]).ToString(CultureInfo.InvariantCulture));

            Assert.Equal(6, Assert.IsType<int>(borrador["sanic_plazo"]));
            Assert.False(borrador.Contains("sanic_numeroarreglo"), "el Borrador no tiene número de arreglo: ausente, no null");
            Assert.False(borrador.Contains("sanic_fechaformalizado"));
        }

        [Fact]
        public void PA_LFX_03_ValoresPinneadosAMano_TeammembershipConGuidCrudoYSystemDeshabilitado()
        {
            var membresia = LectorDeFixtures.Abrir("04-teammembership-team-queue.json").Entidad("teammembership_de_usuario_conectado", "Entities", "0");
            Assert.Equal("teammembership", membresia.LogicalName);
            Assert.Equal(new[] { "systemuserid", "teamid", "teammembershipid" }, membresia.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(new Guid("0fff0001-0000-4000-8000-000000000008"), Assert.IsType<Guid>(membresia["systemuserid"]));
            Assert.IsType<Guid>(membresia["teamid"]);
            Assert.IsType<Guid>(membresia["teammembershipid"]);

            var system = LectorDeFixtures.Abrir("06-whoami-systemuser.json").Entidad("systemuser_SYSTEM");
            Assert.Equal("SYSTEM", Assert.IsType<string>(system["fullname"]));
            Assert.True(Assert.IsType<bool>(system["isdisabled"]));
            Assert.Equal(0, Assert.IsType<OptionSetValue>(system["accessmode"]).Value);
        }

        [Fact]
        public void PA_LFX_03_ValoresPinneadosAMano_CorreoEntranteSinSentOnYConFechasUtc()
        {
            var fixture = LectorDeFixtures.Abrir("01-email.json");
            var entrante = fixture.Entidad("retrieve_columnset_true_entrante", "0");
            Assert.Equal("email", entrante.LogicalName);
            Assert.False(Assert.IsType<bool>(entrante["directioncode"]));
            Assert.Equal(4, Assert.IsType<OptionSetValue>(entrante["statuscode"]).Value);
            Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(entrante["actualend"]).Kind);
            Assert.False(entrante.Contains("senton"), "senton está ausente en un entrante");
            Assert.StartsWith("<", Assert.IsType<string>(entrante["messageid"]), StringComparison.Ordinal);
            var desde = Assert.IsType<EntityCollection>(entrante["from"]);
            Assert.Equal("activityparty", desde.EntityName);
            Assert.Single(desde.Entities);
            Assert.Equal("activitypointer", Assert.IsType<EntityReference>(desde.Entities[0]["activityid"]).LogicalName);

            var saliente = fixture.Entidad("retrieve_columnset_true_saliente", "0");
            Assert.True(Assert.IsType<bool>(saliente["directioncode"]));
            Assert.Equal(3, Assert.IsType<OptionSetValue>(saliente["statuscode"]).Value);
            Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(saliente["senton"]).Kind);
        }

        // ---- Falla, respuestas y alias ----

        [Theory]
        [InlineData("k01_violacion_de_clave_alterna", unchecked((int)0x80060892))]
        [InlineData("version_de_fila_no_coincide", unchecked((int)0x80060882))]
        [InlineData("retrieve_de_fila_inexistente", unchecked((int)0x80040217))]
        public void PA_LFX_04_LaFallaConservaTipoExteriorCodigoMensajeDetallesYCadena(string caso, int codigoEsperado)
        {
            var fixture = LectorDeFixtures.Abrir("07-fallas.json");
            var falla = fixture.Falla(caso);
            Assert.Equal(typeof(FaultException<OrganizationServiceFault>), falla.GetType());
            Assert.Equal(codigoEsperado, falla.Detail.ErrorCode);
            Assert.Null(falla.InnerException);
            Assert.False(string.IsNullOrEmpty(falla.Detail.Message));
            Assert.Equal(falla.Detail.Message, falla.Message);

            var json = fixture.Nodo(caso, "falla", "fault");
            Assert.Equal(json.GetProperty("Message").GetString(), falla.Detail.Message);
            Assert.Equal(json.GetProperty("ErrorCode_decimal").GetInt32(), falla.Detail.ErrorCode);
            Assert.Equal(DateTimeKind.Utc, falla.Detail.Timestamp.Kind);
            Assert.Equal(json.GetProperty("Timestamp").GetProperty("iso").GetString(), TextoDeFecha(falla.Detail.Timestamp));

            var detallesJson = json.GetProperty("ErrorDetails");
            Assert.Equal(detallesJson.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList(),
                falla.Detail.ErrorDetails.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList());
            foreach (var detalle in detallesJson.EnumerateObject())
            {
                var valor = falla.Detail.ErrorDetails[detalle.Name];
                var tipo = detalle.Value.GetProperty("tipo_del_valor").GetString();
                Assert.True(tipo == valor.GetType().FullName, detalle.Name + ": " + valor.GetType().FullName + " en vez de " + tipo);
                Assert.Equal(detalle.Value.GetProperty("valor").GetString(), Convert.ToString(valor, CultureInfo.InvariantCulture));
            }

            Assert.NotNull(falla.Detail.InnerFault);
            Assert.Equal(codigoEsperado, falla.Detail.InnerFault.ErrorCode);
            // La cadena de InnerFault del JSON mide 3 niveles en K-01 y 2 en los otros dos casos: el esperado sale del JSON, no de una constante.
            int nivelesEnJson = 0;
            for (var nivel = json; nivel.ValueKind != JsonValueKind.Null; nivel = nivel.GetProperty("InnerFault")) nivelesEnJson++;
            int nivelesLeidos = 0;
            for (OrganizationServiceFault nivel = falla.Detail; nivel != null; nivel = nivel.InnerFault) nivelesLeidos++;
            Assert.Equal(nivelesEnJson, nivelesLeidos);
        }

        [Fact]
        public void PA_LFX_04b_LosDetallesDeLaFallaK01TienenElTipoQueLlego()
        {
            var detalles = LectorDeFixtures.Abrir("07-fallas.json").Falla("k01_violacion_de_clave_alterna").Detail.ErrorDetails;
            Assert.IsType<Guid>(detalles["ApiStepKey"]);
            Assert.IsType<int>(detalles["ApiDepthKey"]);
            Assert.Equal(1, detalles["ApiDepthKey"]);
            Assert.IsType<string>(detalles["DuplicateAttributes"]);
            Assert.IsType<string>(detalles["0"]);
            Assert.Equal(412, detalles["ApiExceptionHttpStatusCode"]);
        }

        [Fact]
        public void PA_LFX_05_LasRespuestasConservanSuTipoSuNombreYSusResultados()
        {
            var quien = Assert.IsType<WhoAmIResponse>(LectorDeFixtures.Abrir("06-whoami-systemuser.json").Respuesta("WhoAmIResponse_del_usuario_conectado"));
            Assert.Equal("WhoAmI", quien.ResponseName);
            Assert.Equal(new[] { "BusinessUnitId", "OrganizationId", "UserId" }, quien.Results.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(new Guid("0fff0001-0000-4000-8000-000000000008"), Assert.IsType<Guid>(quien.Results["UserId"]));
            Assert.Equal(new Guid("0fff0001-0000-4000-8000-000000000008"), quien.UserId);
            Assert.Equal(new Guid("0fff0003-0000-4000-8000-000000000009"), quien.BusinessUnitId);
            Assert.Equal(new Guid("0fff0005-0000-4000-8000-000000000024"), quien.OrganizationId);

            var control = LectorDeFixtures.Abrir("07-fallas.json").Respuesta("version_de_fila_no_coincide", "control_con_version_vigente", "respuesta");
            Assert.IsType<UpdateResponse>(control);
            Assert.Equal("Update", control.ResponseName);
            Assert.Empty(control.Results);
        }

        [Fact]
        public void PA_LFX_06_LosAliasConservanElTipoDeSuValorOrigen()
        {
            var fixture = LectorDeFixtures.Abrir("08-aliased-y-agregados.json");
            var union = fixture.Entidad("querybyexpression_con_joins_alias_cli_ver_conv", "Entities", "0");
            var creadoPor = Assert.IsType<AliasedValue>(union["cli.createdby"]);
            Assert.Equal("sanic_carp_tbl_cliente", creadoPor.EntityLogicalName);
            Assert.Equal("createdby", creadoPor.AttributeLogicalName);
            Assert.IsType<EntityReference>(creadoPor.Value);
            var fecha = Assert.IsType<DateTime>(Assert.IsType<AliasedValue>(union["cli.createdon"]).Value);
            Assert.Equal(DateTimeKind.Utc, fecha.Kind);
            Assert.IsType<OptionSetValue>(Assert.IsType<AliasedValue>(union["cli.statecode"]).Value);
            Assert.IsType<int>(Assert.IsType<AliasedValue>(union["ver.sanic_capacidad"]).Value);
            Assert.IsType<string>(Assert.IsType<AliasedValue>(union["cli.createdby_createdbyyominame"]).Value);

            var agregado = fixture.Entidad("fetchxml_agregado_count_max_sum", "Entities", "0");
            Assert.Equal(4, Assert.IsType<AliasedValue>(agregado["total"]).Value);
            Assert.IsType<DateTime>(Assert.IsType<AliasedValue>(agregado["ultima"]).Value);
            Assert.Null(agregado.RowVersion);
            Assert.Equal(Guid.Empty, agregado.Id);
        }

        // ---- Casos de borde de la conversión (nodos escritos a mano) ----

        [Theory]
        [InlineData("150.0400000000", 10)]
        [InlineData("0.1", 1)]
        [InlineData("7", 0)]
        [InlineData("-0.0000000001", 10)]
        [InlineData("79228162514264337593543950335", 0)]
        [InlineData("1234567890123456.7890123456", 10)]
        public void PA_LFX_09_ElDecimalConservaValorYEscalaSinPasarPorDouble(string texto, int escala)
        {
            var valor = Assert.IsType<decimal>(LectorDeFixtures.Valor(Json("{\"tipo\":\"System.Decimal\",\"valor\":\"" + texto + "\"}")));
            Assert.Equal(texto, valor.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(escala, ApoyoDeDobles.EscalaDe(valor));
        }

        [Theory]
        [InlineData("2026-11-01T00:00:00.0000000", "Unspecified", 2026, 11, 1, 0, 0, 0, 0)]
        [InlineData("2026-10-14T15:06:51.0000000Z", "Utc", 2026, 10, 14, 15, 6, 51, 0)]
        [InlineData("2026-10-14T15:06:51.1234567Z", "Utc", 2026, 10, 14, 15, 6, 51, 1234567)]
        [InlineData("2026-12-31T23:59:59.9999999Z", "Utc", 2026, 12, 31, 23, 59, 59, 9999999)]
        public void PA_LFX_10_LaFechaConservaKindYTicksSinConvertirLaZona(string texto, string kind, int a, int m, int d, int h, int mi, int s, int fraccionEnTicks)
        {
            var valor = Assert.IsType<DateTime>(LectorDeFixtures.Valor(Json("{\"tipo\":\"System.DateTime\",\"valor\":\"" + texto + "\",\"kind\":\"" + kind + "\"}")));
            Assert.Equal(Enum.Parse<DateTimeKind>(kind), valor.Kind);
            Assert.Equal(new DateTime(a, m, d, h, mi, s, valor.Kind).AddTicks(fraccionEnTicks).Ticks, valor.Ticks);
        }

        [Fact]
        public void PA_LFX_11_LaReferenciaConNombreNuloQuedaConNombreNuloYUnTextoVacioNoSeConvierteEnNulo()
        {
            var nula = Assert.IsType<EntityReference>(LectorDeFixtures.Valor(Json(
                "{\"tipo\":\"Microsoft.Xrm.Sdk.EntityReference\",\"valor\":{\"logicalName\":\"team\",\"id\":\"0fff0002-0000-4000-8000-000000000018\",\"name\":null}}")));
            Assert.Null(nula.Name);
            Assert.Equal("team", nula.LogicalName);
            Assert.Equal(new Guid("0fff0002-0000-4000-8000-000000000018"), nula.Id);

            var vacio = Assert.IsType<string>(LectorDeFixtures.Valor(Json("{\"tipo\":\"System.String\",\"valor\":\"\"}")));
            Assert.Equal(string.Empty, vacio);
        }

        [Fact]
        public void PA_LFX_12_UnTipoQueElLectorNoConoceLanzaConSuNombre()
        {
            var error = Assert.Throws<NotSupportedException>(() => LectorDeFixtures.Valor(Json("{\"tipo\":\"System.Single\",\"valor\":1.5}")));
            Assert.Contains("System.Single", error.Message);
        }

        [Fact]
        public void PA_LFX_13_UnArchivoOUnaRutaQueNoExistenFallanConUnMensajeClaro()
        {
            var sinArchivo = Assert.Throws<FileNotFoundException>(() => LectorDeFixtures.Abrir("no-existe.json"));
            Assert.Contains("no-existe.json", sinArchivo.Message + sinArchivo.FileName);

            var fixture = LectorDeFixtures.Abrir("02-conversacion.json");
            var sinRuta = Assert.Throws<KeyNotFoundException>(() => fixture.Entidad("abierta", "no_existe"));
            Assert.Contains("no_existe", sinRuta.Message);
            Assert.Throws<KeyNotFoundException>(() => fixture.Nodo("no_existe"));
            Assert.Throws<KeyNotFoundException>(() => LectorDeFixtures.Abrir("01-email.json").Entidad("retrieve_columnset_true_entrante", "99"));
        }

        [Fact]
        public void PA_LFX_14_CadaLecturaDevuelveObjetosNuevos()
        {
            var fixture = LectorDeFixtures.Abrir("02-conversacion.json");
            var a = fixture.Entidad("abierta");
            var b = fixture.Entidad("abierta");
            Assert.NotSame(a, b);
            ((EntityReference)a["ownerid"]).Name = "mutado";
            Assert.Equal("ZZF Team 1", ((EntityReference)b["ownerid"]).Name);
        }

        // ---- Verde a propósito: guarda de que la copia no se tocó ----

        [Theory]
        [MemberData(nameof(TodosLosArchivos))]
        public void PA_LFX_15_LosFixturesCopiadosSonIdenticosByteAByteALosDelSpike_VERDE_A_PROPOSITO(string archivo)
        {
            // Guarda: los fixtures son respuestas reales exportadas; editarlos a mano falsearía la medición de los dobles.
            var copia = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dataverse", archivo));
            var directorio = new DirectoryInfo(AppContext.BaseDirectory);
            string original = null;
            while (directorio != null && original == null)
            {
                var candidato = Path.Combine(directorio.FullName, "spikes", "fixtures", "salida", archivo);
                if (File.Exists(candidato)) original = candidato;
                directorio = directorio.Parent;
            }
            Assert.True(original != null, "No se encontró spikes/fixtures/salida/" + archivo + " subiendo desde la salida de la compilación.");
            Assert.Equal(File.ReadAllBytes(original), copia);
        }
    }
}
