using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Xml.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// LA PRUEBA CENTRAL DEL LOTE 3 (diseno/03 4.5, "Validación contra respuestas reales"; <c>pruebas.md</c> §1; defecto D-59 de M-PPP:
    /// 1104 pruebas en verde con un doble que repetía el mismo error del código).
    /// <para>
    /// Para cada respuesta REAL exportada de Dev (<c>Fixtures/Dataverse/*.json</c>, 2026-10-14) se siembra el doble y se comprueba que lo que
    /// devuelve (<c>Retrieve</c>, <c>RetrieveMultiple</c>, <c>Execute(WhoAmI)</c>, fallas, paginación) tiene LOS MISMOS TIPOS Y LA MISMA
    /// FORMA que la respuesta real: tipo .NET de cada atributo, ausencia de nulos, <c>Kind</c>, escala del decimal, <c>Guid</c> crudo en
    /// <c>teammembership</c>, tipo exterior y <c>ErrorCode</c> de cada falla y forma de la paginación. El esperado sale del JSON (por el
    /// lector, que tiene su propia aceptación); NUNCA del doble ni de una constante que repita lo que el doble hace.
    /// </para>
    /// <para>
    /// <b>Mutación que mata cada fila</b> (una mutación = un cambio plausible y equivocado del doble o de sus constructores):
    /// </para>
    /// <list type="table">
    /// <item><term>PA-DOB-01 (22 filas reales)</term><description>La copia del almacén cambia el <c>Kind</c> de una fecha (serialización,
    /// <c>ToLocalTime</c>), pasa un decimal por <c>double</c> (pierde la escala 10), convierte un <c>Guid</c> en <c>EntityReference</c> o al
    /// revés, rellena o pierde el <c>Name</c> de un <c>EntityReference</c>, devuelve <c>FormattedValues</c>, deja un <c>EntityState</c>, o
    /// pierde un atributo (por ejemplo las cinco <c>EntityCollection</c> de <c>activityparty</c> del email).</description></item>
    /// <item><term>PA-DOB-02</term><description>El doble devuelve <c>null</c> en vez de ausente: al escribir un <c>null</c>, al pedir una
    /// columna que la fila no tiene, o al sembrar una fila con un valor nulo.</description></item>
    /// <item><term>PA-DOB-03 (UL y DO)</term><description>Una fecha de SOLO FECHA sale con <c>Kind=Utc</c> (o con hora distinta de 00:00); una
    /// fecha y hora de usuario local sale con <c>Kind=Unspecified</c> o <c>Local</c>; el almacén no distingue las dos clases de columna.</description></item>
    /// <item><term>PA-DOB-04</term><description>Un decimal escrito con escala 2 vuelve con escala 2 (sin normalizar), o como <c>double</c>/<c>Money</c>.</description></item>
    /// <item><term>PA-DOB-05 (email, 12 columnas del diseño)</term><description>No filtra el <c>ColumnSet</c>; no agrega el id primario
    /// (<c>activityid</c>); devuelve <c>regardingobjectid</c> como <c>null</c> aunque se pidió; cambia <c>PagingCookie</c> a <c>null</c>,
    /// <c>TotalRecordCount</c> a 0 o <c>MinActiveRowVersion</c> a otra cosa.</description></item>
    /// <item><term>PA-DOB-06 (teammembership)</term><description>El doble o su constructor cambia <c>systemuserid</c>/<c>teamid</c>/<c>teammembershipid</c>
    /// a <c>EntityReference</c> (el defecto D-59); el almacén acepta ese <c>EntityReference</c> sin quejarse; la consulta con <c>TopCount</c> devuelve
    /// cookie con contenido en vez de vacía.</description></item>
    /// <item><term>PA-DOB-07</term><description>La condición <c>Equal</c> sobre un <c>EntityReference</c> con un <c>Guid</c>, o sobre un <c>bool</c>, no encuentra la fila.</description></item>
    /// <item><term>PA-DOB-08</term><description>La proyección de un <c>RetrieveMultiple</c> con <c>TopCount</c> deja de agregar el id primario o devuelve la cookie con datos.</description></item>
    /// <item><term>PA-DOB-09</term><description>Las columnas <c>OptionSetValue</c>, <c>BooleanManagedProperty</c> o <c>Decimal</c> de una consulta de columnas explícitas cambian de tipo.</description></item>
    /// <item><term>PA-DOB-10 (WhoAmI)</term><description>La respuesta lleva <c>EntityReference</c>/texto en vez de <c>Guid</c>, falta una de las tres claves,
    /// el <c>ResponseName</c> no es "WhoAmI", o devuelve la identidad de SYSTEM a un usuario.</description></item>
    /// <item><term>PA-DOB-11 (SYSTEM)</term><description>El constructor de SYSTEM lo deja habilitado (<c>isdisabled</c> falso), o lo pone como <c>int</c>/<c>OptionSetValue</c>.</description></item>
    /// <item><term>PA-DOB-12 (fallas)</term><description>La falla sale como <c>InvalidPluginExecutionException</c> o <c>FaultException</c> no genérica, envuelta en otra excepción,
    /// con el <c>ErrorCode</c> de K-01 y el de versión de fila intercambiados o en cero, sin <c>InnerFault</c>, con el <c>Message</c> de la excepción distinto del del detalle.
    /// La respuesta del <c>UpdateRequest</c> correcto no es <c>UpdateResponse</c> o trae <c>Results</c>.</description></item>
    /// <item><term>PA-DOB-13 (paginación)</term><description><c>MoreRecords</c> mal calculado, el total en 0 en vez de -1 cuando no se pide, la cookie sin número de página o
    /// sin la columna del orden, la página 2 que repite la 1, la columna del <c>OrderExpression</c> que falta en la fila (la plataforma la agrega).</description></item>
    /// <item><term>PA-DOB-14 (constructores)</term><description>Un constructor pone una columna que la fila real de ese estado no tiene (por ejemplo <c>senton</c> en un entrante),
    /// un <c>Money</c> donde la plataforma da <c>decimal</c>, un <c>Kind=Utc</c> en una fecha de solo fecha, un <c>EntityReference</c> donde hay un <c>Guid</c>.</description></item>
    /// <item><term>PA-DOB-15 (joins)</term><description>El doble acepta una consulta con <c>LinkEntities</c> (la que produjo los alias del fixture 08) y devuelve algo.</description></item>
    /// <item><term>PA-DOB-16</term><description>El almacén acepta un <c>EntityReference</c> en una columna de una tabla intersect (<c>teammembership</c>, <c>systemuserroles</c>).</description></item>
    /// </list>
    /// </summary>
    public class DobleContraRespuestasRealesAceptacion
    {
        private const string Conversacion = "sanic_carp_tbl_conversacion";
        private const string Arreglo = "sanic_carp_tbl_arreglo";

        private static string[] Ruta(string ruta)
        {
            return ruta.Split('/');
        }

        private static OrganizationServiceEnMemoria Almacen(params Entity[] filas)
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(filas);
            return almacen;
        }

        // =====================================================================================================================
        // PA-DOB-01: cada fila real, sembrada, vuelve IGUAL por Retrieve(ColumnSet(true))
        // =====================================================================================================================

        public static IEnumerable<object[]> FilasReales()
        {
            yield return new object[] { "01-email.json", "retrieve_columnset_true_entrante/0" };
            yield return new object[] { "01-email.json", "retrieve_columnset_true_entrante/1" };
            yield return new object[] { "01-email.json", "retrieve_columnset_true_saliente/0" };
            yield return new object[] { "01-email.json", "retrieve_columnset_true_saliente/1" };
            yield return new object[] { "01-email.json", "sinteticos_con_regardingobjectid_a_la_conversacion/entrante" };
            yield return new object[] { "01-email.json", "sinteticos_con_regardingobjectid_a_la_conversacion/saliente" };
            yield return new object[] { "02-conversacion.json", "abierta" };
            yield return new object[] { "02-conversacion.json", "finalizada" };
            yield return new object[] { "02-conversacion.json", "unida" };
            yield return new object[] { "03-arreglo.json", "borrador_congelado" };
            yield return new object[] { "03-arreglo.json", "vigente" };
            yield return new object[] { "03-arreglo.json", "fila_relacionada_cliente" };
            yield return new object[] { "03-arreglo.json", "fila_relacionada_version_de_plantilla" };
            yield return new object[] { "04-teammembership-team-queue.json", "teammembership_de_usuario_conectado/Entities/0" };
            yield return new object[] { "04-teammembership-team-queue.json", "team_isdefault_de_la_BU_Arreglos_de_Pago/Entities/0" };
            yield return new object[] { "04-teammembership-team-queue.json", "queue_con_emailaddress/0" };
            yield return new object[] { "05-environmentvariable.json", "definicion_descartable" };
            yield return new object[] { "05-environmentvariable.json", "valor_descartable" };
            yield return new object[] { "06-whoami-systemuser.json", "systemuser_SYSTEM" };
            yield return new object[] { "06-whoami-systemuser.json", "systemuser_ordinario_usuario_de_aplicacion_conectado" };
            yield return new object[] { "06-whoami-systemuser.json", "systemuser_usuario_de_aplicacion_no_administrador" };
            yield return new object[] { "04-teammembership-team-queue.json", "queue_con_emailaddress/1" };
        }

        [Theory]
        [MemberData(nameof(FilasReales))]
        public void PA_DOB_01_UnaFilaRealSembradaVuelveIgualPorRetrieveConTodasLasColumnas(string archivo, string ruta)
        {
            var real = LectorDeFixtures.Abrir(archivo).Entidad(Ruta(ruta));
            var almacen = Almacen(real);

            var devuelta = almacen.ComoUsuario(null).Retrieve(real.LogicalName, real.Id, new ColumnSet(true));

            ComparadorDeFormas.AssertIgual(real, devuelta, archivo + ":" + ruta);
            Assert.DoesNotContain(devuelta.Attributes, par => par.Value == null);
        }

        // =====================================================================================================================
        // PA-DOB-02: ausente, no nulo
        // =====================================================================================================================

        [Fact]
        public void PA_DOB_02_UnaColumnaNulaEsAusenteAlSembrarAlCrearAlActualizarYAlPedirla()
        {
            var fixture = LectorDeFixtures.Abrir("01-email.json");
            var entrante = fixture.Entidad("retrieve_columnset_true_entrante", "0");
            Assert.False(entrante.Contains("senton"), "precondición del fixture: un entrante no tiene senton");

            // Sembrar una fila con un null explícito: no queda la clave.
            var conNulo = fixture.Entidad("retrieve_columnset_true_entrante", "0");
            conNulo["senton"] = null;
            conNulo["regardingobjectid"] = null;
            var almacen = Almacen(conNulo);
            var leida = almacen.Retrieve("email", entrante.Id, new ColumnSet(true));
            Assert.False(leida.Contains("senton"));
            Assert.False(leida.Contains("regardingobjectid"));

            // Pedir una columna que la fila no tiene: ausente, no null.
            var parcial = almacen.Retrieve("email", entrante.Id, new ColumnSet("senton", "subject", "regardingobjectid"));
            Assert.Equal(new[] { "activityid", "subject" }, parcial.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());

            // Update con null: borra la columna, no la deja en nulo.
            almacen.Update(new Entity("email", entrante.Id) { ["inreplyto"] = null, ["subject"] = "otro asunto" });
            var actualizada = almacen.Retrieve("email", entrante.Id, new ColumnSet(true));
            Assert.False(actualizada.Contains("inreplyto"));
            Assert.Equal("otro asunto", actualizada["subject"]);
            Assert.DoesNotContain(actualizada.Attributes, par => par.Value == null);

            // Create con nulos explícitos: tampoco quedan.
            var id = almacen.Create(new Entity(Conversacion) { ["sanic_asunto"] = null, ["sanic_remitente"] = "zzf@example.com", ["sanic_unidaaid"] = null });
            var creada = almacen.Retrieve(Conversacion, id, new ColumnSet(true));
            Assert.False(creada.Contains("sanic_asunto"));
            Assert.False(creada.Contains("sanic_unidaaid"));
            Assert.DoesNotContain(creada.Attributes, par => par.Value == null);
        }

        // =====================================================================================================================
        // PA-DOB-03: Kind de las fechas
        // =====================================================================================================================

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void PA_DOB_03_UnaFechaHoraDeUsuarioLocalSaleSiempreConKindUtc(DateTimeKind kindDeEntrada)
        {
            var fixture = LectorDeFixtures.Abrir("02-conversacion.json");
            var real = fixture.Entidad("abierta")["sanic_fechaprimercorreo"];
            Assert.Equal(DateTimeKind.Utc, ((DateTime)real).Kind);

            var entrada = DateTime.SpecifyKind(new DateTime(2026, 10, 14, 15, 6, 51), kindDeEntrada);
            var esperado = kindDeEntrada == DateTimeKind.Local ? entrada.ToUniversalTime() : DateTime.SpecifyKind(entrada, DateTimeKind.Utc);
            var almacen = new OrganizationServiceEnMemoria();

            if (kindDeEntrada != DateTimeKind.Utc)
            {
                // AJUSTADA en la vuelta 2 (hallazgo 5, 2026-10-14): ANTES una UL con Kind Local o Unspecified se aceptaba "como UTC" al escribir. El reloj del proyecto es UTC
                // (diseno/03 0.6) y un código que escribe una hora sin Kind está confundiendo zonas: Create y Update la RECHAZAN con ArgumentException (detalle en
                // FechasDeEscrituraAceptacion). Sembrar sigue normalizando (declara datos ya conocidos, que la plataforma devuelve siempre en Utc).
                Assert.ThrowsAny<ArgumentException>(() => almacen.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = entrada }));
                Assert.Empty(almacen.Todas(Conversacion));
                var sembradaAparte = Almacen(new Entity(Conversacion, Guid.NewGuid()) { ["sanic_fechaprimercorreo"] = entrada });
                var normalizada = sembradaAparte.Todas(Conversacion).Single()["sanic_fechaprimercorreo"];
                ComparadorDeFormas.AssertValor("sembrar.sanic_fechaprimercorreo", real, normalizada, estricto: false);
                Assert.Equal(esperado.Ticks, ((DateTime)normalizada).Ticks);
                return;
            }

            var id = almacen.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = entrada });
            var devuelta = almacen.Retrieve(Conversacion, id, new ColumnSet("sanic_fechaprimercorreo"))["sanic_fechaprimercorreo"];

            ComparadorDeFormas.AssertValor("create.sanic_fechaprimercorreo", real, devuelta, estricto: false);
            Assert.Equal(esperado.Ticks, ((DateTime)devuelta).Ticks);

            almacen.Update(new Entity(Conversacion, id) { ["sanic_fechafinalizacion"] = entrada });
            var actualizada = almacen.Retrieve(Conversacion, id, new ColumnSet("sanic_fechafinalizacion"))["sanic_fechafinalizacion"];
            ComparadorDeFormas.AssertValor("update.sanic_fechafinalizacion", real, actualizada, estricto: false);
            Assert.Equal(esperado.Ticks, ((DateTime)actualizada).Ticks);

            var sembrada = Almacen(new Entity(Conversacion, Guid.NewGuid()) { ["sanic_fechaprimercorreo"] = entrada });
            var deSembrar = sembrada.Todas(Conversacion).Single()["sanic_fechaprimercorreo"];
            ComparadorDeFormas.AssertValor("sembrar.sanic_fechaprimercorreo", real, deSembrar, estricto: false);
            Assert.Equal(esperado.Ticks, ((DateTime)deSembrar).Ticks);
        }

        [Theory]
        [InlineData(DateTimeKind.Utc, 0)]
        [InlineData(DateTimeKind.Utc, 13)]
        [InlineData(DateTimeKind.Local, 13)]
        [InlineData(DateTimeKind.Unspecified, 0)]
        [InlineData(DateTimeKind.Unspecified, 13)]
        public void PA_DOB_03_UnaFechaDeSoloFechaSaleUnspecifiedALas0000SinConvertirLaZona(DateTimeKind kindDeEntrada, int hora)
        {
            var real = LectorDeFixtures.Abrir("03-arreglo.json").Entidad("borrador_congelado")["sanic_fechainicio"];
            Assert.Equal(DateTimeKind.Unspecified, ((DateTime)real).Kind);
            Assert.Equal(TimeSpan.Zero, ((DateTime)real).TimeOfDay);

            var entrada = DateTime.SpecifyKind(new DateTime(2026, 11, 1, hora, 45, 0), kindDeEntrada);
            var esperado = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var almacen = new OrganizationServiceEnMemoria();

            var idArreglo = almacen.Create(new Entity(Arreglo) { ["sanic_fechainicio"] = entrada, ["sanic_fechaformalizacion"] = entrada });
            var devuelta = almacen.Retrieve(Arreglo, idArreglo, new ColumnSet("sanic_fechainicio", "sanic_fechaformalizacion"));
            foreach (var columna in new[] { "sanic_fechainicio", "sanic_fechaformalizacion" })
            {
                ComparadorDeFormas.AssertValor("arreglo.create." + columna, real, devuelta[columna], estricto: false);
                Assert.Equal(esperado, devuelta[columna]);
            }

            var idCuota = almacen.Create(new Entity("sanic_carp_tbl_cuota"));
            almacen.Update(new Entity("sanic_carp_tbl_cuota", idCuota) { ["sanic_fechapago"] = entrada, ["sanic_fechavencimiento"] = entrada });
            var cuota = almacen.Retrieve("sanic_carp_tbl_cuota", idCuota, new ColumnSet(true));
            ComparadorDeFormas.AssertValor("cuota.update.sanic_fechapago", real, cuota["sanic_fechapago"], estricto: false);
            Assert.Equal(esperado, cuota["sanic_fechapago"]);
            Assert.Equal(esperado, cuota["sanic_fechavencimiento"]);

            var sembrada = Almacen(new Entity(Arreglo, Guid.NewGuid()) { ["sanic_fechafin"] = entrada });
            var deSembrar = sembrada.Todas(Arreglo).Single()["sanic_fechafin"];
            ComparadorDeFormas.AssertValor("arreglo.sembrar.sanic_fechafin", real, deSembrar, estricto: false);
            Assert.Equal(esperado, deSembrar);
        }

        // =====================================================================================================================
        // PA-DOB-04: decimales con escala 10
        // =====================================================================================================================

        [Theory]
        [InlineData("1000.50", "1000.5000000000")]
        [InlineData("150.04", "150.0400000000")]
        [InlineData("999999999.99", "999999999.9900000000")]
        [InlineData("0.01", "0.0100000000")]
        [InlineData("0", "0.0000000000")]
        [InlineData("7", "7.0000000000")]
        public void PA_DOB_04_UnDecimalEscritoConEscala2VuelveConEscala10_IgualEnValorYDistintoEnTexto(string escrito, string devuelto)
        {
            var fixture = LectorDeFixtures.Abrir("03-arreglo.json").Entidad("borrador_congelado");
            var real = fixture["sanic_saldoinicial"];
            Assert.Equal(10, ApoyoDeDobles.EscalaDe((decimal)real));
            var valor = decimal.Parse(escrito, System.Globalization.CultureInfo.InvariantCulture);
            var almacen = new OrganizationServiceEnMemoria();

            var id = almacen.Create(new Entity(Arreglo) { ["sanic_saldoinicial"] = valor });
            almacen.Update(new Entity(Arreglo, id) { ["sanic_cuotamensual"] = valor });
            var desdeCreate = almacen.Retrieve(Arreglo, id, new ColumnSet("sanic_saldoinicial", "sanic_cuotamensual"));

            foreach (var columna in new[] { "sanic_saldoinicial", "sanic_cuotamensual" })
            {
                ComparadorDeFormas.AssertValor("arreglo." + columna, real, desdeCreate[columna], estricto: false);
                Assert.Equal(valor, (decimal)desdeCreate[columna]);
                Assert.Equal(devuelto, ApoyoDeDobles.Inv((decimal)desdeCreate[columna]));
            }

            var sembrada = Almacen(new Entity(Arreglo, Guid.NewGuid()) { ["sanic_montoabonado"] = valor });
            var deSembrar = sembrada.Todas(Arreglo).Single()["sanic_montoabonado"];
            ComparadorDeFormas.AssertValor("arreglo.sembrar.sanic_montoabonado", real, deSembrar, estricto: false);
            Assert.Equal(devuelto, ApoyoDeDobles.Inv((decimal)deSembrar));
        }

        // =====================================================================================================================
        // PA-DOB-05: RetrieveMultiple del email con las columnas del diseño (la consulta del exportador, ejecutada contra el doble)
        // =====================================================================================================================

        private static QueryExpression ConsultaDeCorreosDelDiseno(IEnumerable<Guid> ids)
        {
            var consulta = new QueryExpression("email")
            {
                ColumnSet = new ColumnSet("statuscode", "statecode", "directioncode", "messageid", "inreplyto", "actualend", "senton", "createdby",
                    "regardingobjectid", "ownerid", "subject", "createdon")
            };
            consulta.Criteria.AddCondition("activityid", ConditionOperator.In, ids.Cast<object>().ToArray());
            return consulta;
        }

        private static void AssertColeccionIgual(EntityCollection real, EntityCollection actual, bool respetarOrden, string donde)
        {
            Assert.Equal(real.EntityName, actual.EntityName);
            Assert.Equal(real.MoreRecords, actual.MoreRecords);
            Assert.Equal(real.TotalRecordCount, actual.TotalRecordCount);
            Assert.Equal(real.TotalRecordCountLimitExceeded, actual.TotalRecordCountLimitExceeded);
            Assert.Equal(real.MinActiveRowVersion, actual.MinActiveRowVersion);
            Assert.Equal(real.Entities.Count, actual.Entities.Count);

            if (respetarOrden)
            {
                for (int i = 0; i < real.Entities.Count; i++) ComparadorDeFormas.AssertIgual(real.Entities[i], actual.Entities[i], donde + "[" + i + "]");
            }
            else
            {
                foreach (var fila in real.Entities)
                {
                    var igual = actual.Entities.SingleOrDefault(e => e.Id == fila.Id);
                    Assert.True(igual != null, donde + ": falta la fila " + fila.Id);
                    ComparadorDeFormas.AssertIgual(fila, igual, donde + ":" + fila.Id);
                }
            }
            AssertCookie(real.PagingCookie, actual, respetarOrden, donde);
        }

        /// <summary>
        /// La cookie vacía (con <c>TopCount</c>, sin filas o agregada) es "" y no null (fixtures 04, 05, 06, 08). La no vacía es XML
        /// <c>&lt;cookie page="n"&gt;</c> con un hijo por columna de orden y uno por el id primario, cada uno con <c>first</c> y
        /// <c>last</c>: el del id primario son la primera y la última fila DE LA PÁGINA devuelta (con llaves, minúscula); los demás se
        /// comparan con los reales solo si el orden está definido (sin <c>OrderExpression</c> la plataforma ordena como quiere).
        /// </summary>
        private static void AssertCookie(string real, EntityCollection actual, bool ordenDefinido, string donde)
        {
            if (string.IsNullOrEmpty(real))
            {
                Assert.True(real == actual.PagingCookie, donde + ": la cookie debía ser " + (real == null ? "null" : "\"\"") + " y fue " + (actual.PagingCookie == null ? "null" : "\"" + actual.PagingCookie + "\""));
                return;
            }
            Assert.False(string.IsNullOrEmpty(actual.PagingCookie), donde + ": la cookie no puede ser nula ni vacía");
            var esperada = XElement.Parse(real);
            var obtenida = XElement.Parse(actual.PagingCookie);
            Assert.Equal("cookie", obtenida.Name.LocalName);
            Assert.Equal(esperada.Attribute("page").Value, obtenida.Attribute("page").Value);
            Assert.Equal(esperada.Elements().Select(e => e.Name.LocalName).ToList(), obtenida.Elements().Select(e => e.Name.LocalName).ToList());
            var primeraYUltima = actual.Entities;
            var primario = esperada.Elements().Last();
            foreach (var hijo in esperada.Elements())
            {
                var par = obtenida.Element(hijo.Name);
                Assert.True(par.Attribute("first") != null && par.Attribute("last") != null, donde + ": " + hijo.Name.LocalName + " sin first/last");
                if (hijo.Name == primario.Name)
                {
                    Assert.Equal("{" + primeraYUltima.First().Id.ToString() + "}", par.Attribute("first").Value);
                    Assert.Equal("{" + primeraYUltima.Last().Id.ToString() + "}", par.Attribute("last").Value);
                }
                else if (ordenDefinido)
                {
                    Assert.Equal(hijo.Attribute("first").Value, par.Attribute("first").Value);
                    Assert.Equal(hijo.Attribute("last").Value, par.Attribute("last").Value);
                }
            }
        }

        [Fact]
        public void PA_DOB_05_LaConsultaDeCorreosDelDisenoDevuelveLoMismoQueLaPlataforma()
        {
            var fixture = LectorDeFixtures.Abrir("01-email.json");
            var filas = new[]
            {
                fixture.Entidad("retrieve_columnset_true_entrante", "0"), fixture.Entidad("retrieve_columnset_true_entrante", "1"),
                fixture.Entidad("retrieve_columnset_true_saliente", "0"), fixture.Entidad("retrieve_columnset_true_saliente", "1")
            };
            var almacen = Almacen(filas);

            var obtenida = almacen.RetrieveMultiple(ConsultaDeCorreosDelDiseno(filas.Select(f => f.Id)));

            var real = fixture.Coleccion("retrievemultiple_columnas_del_diseno");
            Assert.Equal(4, real.Entities.Count);
            Assert.All(real.Entities, e => Assert.False(e.Contains("regardingobjectid")));
            AssertColeccionIgual(real, obtenida, respetarOrden: false, donde: "email RM");
            Assert.Equal(-1, obtenida.TotalRecordCount);
            Assert.False(obtenida.TotalRecordCountLimitExceeded);
            Assert.Equal("-1", obtenida.MinActiveRowVersion);
            Assert.All(obtenida.Entities, e => Assert.False(e.Contains("regardingobjectid"), "se pidió regardingobjectid y la fila no lo tiene: ausente, no null"));
            Assert.All(obtenida.Entities, e => Assert.False(e.Contains("from"), "con columnas explícitas no llegan las EntityCollection de activityparty (solo con ColumnSet(true))"));
        }

        // =====================================================================================================================
        // PA-DOB-06 y 07: teammembership (intersect, Guid crudo), team, queue
        // =====================================================================================================================

        [Fact]
        public void PA_DOB_06_LaMembresiaDeTeamSalePorConsultaConGuidCrudoYLaCookieDeLaPlataforma()
        {
            var fixture = LectorDeFixtures.Abrir("04-teammembership-team-queue.json");
            var deUsuario = fixture.Coleccion("teammembership_de_usuario_conectado");
            var deAplicacion = fixture.Coleccion("teammembership_de_usuario_de_aplicacion_no_administrador");
            var almacen = Almacen(deUsuario.Entities[0], deAplicacion.Entities[0]);
            var usuario = (Guid)deUsuario.Entities[0]["systemuserid"];

            var consulta = new QueryExpression("teammembership") { ColumnSet = new ColumnSet(true) };
            consulta.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, usuario);
            var obtenida = almacen.ComoUsuario(usuario).RetrieveMultiple(consulta);

            AssertColeccionIgual(deUsuario, obtenida, respetarOrden: true, donde: "teammembership por usuario");
            Assert.IsType<Guid>(obtenida.Entities[0]["systemuserid"]);
            Assert.IsType<Guid>(obtenida.Entities[0]["teamid"]);
            Assert.IsType<Guid>(obtenida.Entities[0]["teammembershipid"]);
        }

        [Fact]
        public void PA_DOB_06_LaConsultaDeMembresiasConTopCountDevuelveLaCookieVacia()
        {
            var fixture = LectorDeFixtures.Abrir("04-teammembership-team-queue.json");
            var deAplicacion = fixture.Coleccion("teammembership_de_usuario_de_aplicacion_no_administrador");
            var porTeam = fixture.Coleccion("teammembership_de_un_team_por_teamid");
            var almacen = Almacen(deAplicacion.Entities[0]);
            Assert.Equal(string.Empty, porTeam.PagingCookie);

            var consulta = new QueryExpression("teammembership") { ColumnSet = new ColumnSet(true), TopCount = 5 };
            consulta.Criteria.AddCondition("teamid", ConditionOperator.Equal, (Guid)porTeam.Entities[0]["teamid"]);
            var obtenida = almacen.RetrieveMultiple(consulta);

            AssertColeccionIgual(porTeam, obtenida, respetarOrden: true, donde: "teammembership por team");
            Assert.Equal(string.Empty, obtenida.PagingCookie);
        }

        [Fact]
        public void PA_DOB_06_ElConstructorDeMembresiasProduceLosTresGuidCrudosDeLaPlataforma()
        {
            var real = LectorDeFixtures.Abrir("04-teammembership-team-queue.json").Entidad("teammembership_de_usuario_conectado", "Entities", "0");
            var usuario = Guid.NewGuid();
            var team = Guid.NewGuid();
            var almacen = new OrganizationServiceEnMemoria();
            var membresia = ConstructorDeDatos.UnaMembresiaDeTeam(usuario, team).Construir();
            almacen.Sembrar(membresia);

            var devuelta = almacen.Retrieve("teammembership", membresia.Id, new ColumnSet(true));

            ComparadorDeFormas.AssertMismaForma(real, devuelta, "UnaMembresiaDeTeam");
            Assert.Equal(real.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(), devuelta.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(usuario, Assert.IsType<Guid>(devuelta["systemuserid"]));
            Assert.Equal(team, Assert.IsType<Guid>(devuelta["teamid"]));
            Assert.Equal(membresia.Id, Assert.IsType<Guid>(devuelta["teammembershipid"]));
        }

        [Fact]
        public void PA_DOB_07_ElTeamPredeterminadoDeLaUnidadDeNegocioSeEncuentraPorBoolYPorGuidContraUnEntityReference()
        {
            var fixture = LectorDeFixtures.Abrir("04-teammembership-team-queue.json");
            var real = fixture.Coleccion("team_isdefault_de_la_BU_Arreglos_de_Pago");
            var unidad = ((EntityReference)real.Entities[0]["businessunitid"]).Id;
            var otroTeam = ConstructorDeDatos.UnTeam("ZZF otro team", unidad, predeterminado: false).Construir();
            var otraUnidad = ConstructorDeDatos.UnTeam("ZZF team de otra unidad", Guid.NewGuid(), predeterminado: true).Construir();
            var almacen = Almacen(otraUnidad, real.Entities[0], otroTeam);

            var consulta = new QueryExpression("team") { ColumnSet = new ColumnSet(true) };
            consulta.Criteria.AddCondition("isdefault", ConditionOperator.Equal, true);
            consulta.Criteria.AddCondition("businessunitid", ConditionOperator.Equal, unidad);
            var obtenida = almacen.RetrieveMultiple(consulta);

            AssertColeccionIgual(real, obtenida, respetarOrden: true, donde: "team predeterminado");
            Assert.IsType<bool>(obtenida.Entities[0]["isdefault"]);
            Assert.IsType<EntityReference>(obtenida.Entities[0]["businessunitid"]);
        }

        [Fact]
        public void PA_DOB_07_LaColaDeUnBuzonSeEncuentraPorElCorreoSinDistinguirMayusculas()
        {
            var fixture = LectorDeFixtures.Abrir("04-teammembership-team-queue.json");
            var colas = new[] { fixture.Entidad("queue_con_emailaddress", "0"), fixture.Entidad("queue_con_emailaddress", "1") };
            var almacen = Almacen(colas);
            var correo = (string)colas[1]["emailaddress"];

            var consulta = new QueryExpression("queue") { ColumnSet = new ColumnSet("name", "emailaddress", "statecode") };
            consulta.Criteria.AddCondition("emailaddress", ConditionOperator.Equal, correo.ToUpperInvariant());
            consulta.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            var obtenida = almacen.RetrieveMultiple(consulta);

            var fila = Assert.Single(obtenida.Entities);
            Assert.Equal(colas[1].Id, fila.Id);
            Assert.Equal(correo, Assert.IsType<string>(fila["emailaddress"]));
            Assert.Equal(0, Assert.IsType<OptionSetValue>(fila["statecode"]).Value);
            Assert.Equal(new[] { "emailaddress", "name", "queueid", "statecode" }, fila.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        // =====================================================================================================================
        // PA-DOB-08 y 09: proyecciones de columnas explícitas (systemuser con TopCount, definición de variable, arreglo)
        // =====================================================================================================================

        [Fact]
        public void PA_DOB_08_ElUsuarioConectadoConSusCuatroColumnasYTopCountSaleComoLoDevolvioLaPlataforma()
        {
            var fixture = LectorDeFixtures.Abrir("06-whoami-systemuser.json");
            var usuario = fixture.Entidad("systemuser_ordinario_usuario_de_aplicacion_conectado");
            var real = fixture.Coleccion("retrievemultiple_columnas_del_diseno_usuario_conectado");
            var almacen = Almacen(usuario, fixture.Entidad("systemuser_SYSTEM"));

            var consulta = new QueryExpression("systemuser") { ColumnSet = new ColumnSet("fullname", "isdisabled", "businessunitid", "accessmode"), TopCount = 1 };
            consulta.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, usuario.Id);
            var obtenida = almacen.RetrieveMultiple(consulta);

            AssertColeccionIgual(real, obtenida, respetarOrden: true, donde: "systemuser TopCount");
            Assert.Equal(string.Empty, obtenida.PagingCookie);
            Assert.Equal(new[] { "accessmode", "businessunitid", "fullname", "isdisabled", "systemuserid" },
                obtenida.Entities[0].Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA_DOB_09_LasProyeccionesDeLaDefinicionDeVariableYDelArregloSalenConLosTiposDeLaPlataforma()
        {
            var entorno = LectorDeFixtures.Abrir("05-environmentvariable.json");
            var alias = LectorDeFixtures.Abrir("08-aliased-y-agregados.json");
            var definicion = entorno.Entidad("definicion_descartable");
            var borrador = LectorDeFixtures.Abrir("03-arreglo.json").Entidad("borrador_congelado");
            var almacen = Almacen(definicion, entorno.Entidad("valor_descartable"), borrador);

            // Definición: schemaname y type (OptionSetValue). Se descartan las columnas "val.*" del join, que el doble no produce.
            var esperadaDefinicion = alias.Entidad("querybyexpression_definicion_con_valor_alias_val", "Entities", "0");
            foreach (var clave in esperadaDefinicion.Attributes.Keys.Where(k => k.Contains(".")).ToList()) esperadaDefinicion.Attributes.Remove(clave);
            var porDefinicion = new QueryExpression("environmentvariabledefinition") { ColumnSet = new ColumnSet("schemaname", "type") };
            var devolvioDefinicion = almacen.RetrieveMultiple(porDefinicion).Entities.Single();
            ComparadorDeFormas.AssertIgual(esperadaDefinicion, devolvioDefinicion, "definicion de variable");
            Assert.IsType<OptionSetValue>(devolvioDefinicion["type"]);

            // La definición completa, buscada por nombre de esquema (dos consultas, sin join: 4.1).
            var porNombre = new QueryExpression("environmentvariabledefinition") { ColumnSet = new ColumnSet(true) };
            porNombre.Criteria.AddCondition("schemaname", ConditionOperator.Equal, (string)definicion["schemaname"]);
            ComparadorDeFormas.AssertIgual(definicion, almacen.RetrieveMultiple(porNombre).Entities.Single(), "definicion por schemaname");
            Assert.IsType<BooleanManagedProperty>(definicion["iscustomizable"]);

            // Arreglo: fecha de solo fecha, decimal con escala 10, choice. Se descartan las columnas con alias de los tres joins.
            var esperadoArreglo = alias.Entidad("querybyexpression_con_joins_alias_cli_ver_conv", "Entities", "0");
            foreach (var clave in esperadoArreglo.Attributes.Keys.Where(k => k.Contains(".")).ToList()) esperadoArreglo.Attributes.Remove(clave);
            var porArreglo = new QueryExpression(Arreglo) { ColumnSet = new ColumnSet("sanic_nombre", "sanic_saldoinicial", "statuscode", "sanic_fechainicio") };
            var devolvioArreglo = almacen.RetrieveMultiple(porArreglo).Entities.Single();
            ComparadorDeFormas.AssertIgual(esperadoArreglo, devolvioArreglo, "arreglo sin joins");
            Assert.Equal(DateTimeKind.Unspecified, ((DateTime)devolvioArreglo["sanic_fechainicio"]).Kind);
            Assert.Equal(10, ApoyoDeDobles.EscalaDe((decimal)devolvioArreglo["sanic_saldoinicial"]));
        }

        // =====================================================================================================================
        // PA-DOB-10: WhoAmI
        // =====================================================================================================================

        [Fact]
        public void PA_DOB_10_WhoAmIDevuelveUnWhoAmIResponseConLosTresGuidDelUsuarioQueLlama()
        {
            var fixture = LectorDeFixtures.Abrir("06-whoami-systemuser.json");
            var real = (WhoAmIResponse)fixture.Respuesta("WhoAmIResponse_del_usuario_conectado");
            var almacen = new OrganizationServiceEnMemoria { UnidadDeNegocioId = real.BusinessUnitId, OrganizacionId = real.OrganizationId };

            var respuesta = almacen.ComoUsuario(real.UserId).Execute(new WhoAmIRequest());

            var quien = Assert.IsType<WhoAmIResponse>(respuesta);
            Assert.Equal(real.ResponseName, quien.ResponseName);
            Assert.Equal(real.Results.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(), quien.Results.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            foreach (var par in real.Results) Assert.Equal(par.Value.GetType(), quien.Results[par.Key].GetType());
            Assert.Equal(real.UserId, quien.UserId);
            Assert.Equal(real.BusinessUnitId, quien.BusinessUnitId);
            Assert.Equal(real.OrganizationId, quien.OrganizationId);
        }

        [Fact]
        public void PA_DOB_10_WhoAmIElevadoEsElIdDeSystemYNoElDelLlamador()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var fabrica = new FabricaDeServiciosSimulada(almacen);

            var comoSystem = Assert.IsType<WhoAmIResponse>(fabrica.CreateOrganizationService(null).Execute(new WhoAmIRequest()));
            var comoLlamador = Assert.IsType<WhoAmIResponse>(fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Execute(new WhoAmIRequest()));

            Assert.Equal(almacen.IdDeSystem, comoSystem.UserId);
            Assert.Equal(ApoyoDeDobles.Usuario1, comoLlamador.UserId);
            Assert.NotEqual(comoSystem.UserId, comoLlamador.UserId);
            Assert.Equal(comoSystem.BusinessUnitId, comoSystem.Results["BusinessUnitId"]);
        }

        // =====================================================================================================================
        // PA-DOB-11: SYSTEM está deshabilitado
        // =====================================================================================================================

        [Fact]
        public void PA_DOB_11_ElSystemSembradoEstaDeshabilitadoComoElReal_YUnUsuarioNo()
        {
            var fixture = LectorDeFixtures.Abrir("06-whoami-systemuser.json");
            var realSystem = fixture.Entidad("systemuser_SYSTEM");
            var realPersona = fixture.Entidad("systemuser_ordinario_usuario_de_aplicacion_conectado");
            Assert.True((bool)realSystem["isdisabled"]);
            Assert.False((bool)realPersona["isdisabled"]);
            var almacen = Almacen(ConstructorDeDatos.UnSystem(IdDeSystemPorDefecto()).Construir(), ConstructorDeDatos.UnUsuario(ApoyoDeDobles.Usuario1).Construir());

            var system = almacen.Retrieve("systemuser", IdDeSystemPorDefecto(), new ColumnSet(true));
            var persona = almacen.Retrieve("systemuser", ApoyoDeDobles.Usuario1, new ColumnSet(true));

            ComparadorDeFormas.AssertMismaForma(realSystem, system, "SYSTEM");
            ComparadorDeFormas.AssertMismaForma(realPersona, persona, "persona");
            Assert.True(Assert.IsType<bool>(system["isdisabled"]));
            Assert.Equal("SYSTEM", system["fullname"]);
            Assert.Equal(0, Assert.IsType<OptionSetValue>(system["accessmode"]).Value);
            Assert.False(Assert.IsType<bool>(persona["isdisabled"]));
        }

        private static Guid IdDeSystemPorDefecto()
        {
            return OrganizationServiceEnMemoria.IdDeSystemPorDefecto;
        }

        // =====================================================================================================================
        // PA-DOB-12: fallas
        // =====================================================================================================================

        private static int NivelesDeFalla(OrganizationServiceFault falla)
        {
            int niveles = 0;
            for (var nivel = falla; nivel != null; nivel = nivel.InnerFault) niveles++;
            return niveles;
        }

        private static void AssertFallaComoLaReal(FaultException<OrganizationServiceFault> real, FaultException<OrganizationServiceFault> actual, string donde)
        {
            // Tipo exterior EXACTO: FaultException<OrganizationServiceFault> directa, sin envoltura (la cadena real es de largo 1).
            Assert.Equal(real.GetType(), actual.GetType());
            Assert.Null(actual.InnerException);
            Assert.Equal(real.Detail.ErrorCode, actual.Detail.ErrorCode);
            Assert.False(string.IsNullOrEmpty(actual.Detail.Message), donde + ": el detalle debe traer mensaje");
            Assert.Equal(actual.Detail.Message, actual.Message);
            Assert.NotNull(actual.Detail.InnerFault);
            Assert.Equal(real.Detail.ErrorCode, actual.Detail.InnerFault.ErrorCode);
            // La cadena de InnerFault de la falla real mide 3 niveles en K-01 y 2 en versión de fila e inexistente (fixture 07): el esperado sale de la real.
            Assert.Equal(NivelesDeFalla(real.Detail), NivelesDeFalla(actual.Detail));
            Assert.Equal(DateTimeKind.Utc, actual.Detail.Timestamp.Kind);
            Assert.NotNull(actual.Detail.ErrorDetails);
        }

        [Fact]
        public void PA_DOB_12_LaViolacionDeLaClaveAlternaK01SaleComoLaFallaRealConSuErrorCode()
        {
            var real = LectorDeFixtures.Abrir("07-fallas.json").Falla("k01_violacion_de_clave_alterna");
            Assert.Equal(unchecked((int)0x80060892), real.Detail.ErrorCode);
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(Conversacion, "sanic_huellamessageid");
            almacen.Create(ConstructorDeDatos.UnaConversacion().Con("sanic_huellamessageid", "ZZF-h-duplicada").Construir());
            var repetida = ConstructorDeDatos.UnaConversacion().ConId(Guid.NewGuid()).Con("sanic_huellamessageid", "ZZF-h-duplicada").Construir();

            var actual = ApoyoDeDobles.Falla(() => almacen.Create(repetida));

            AssertFallaComoLaReal(real, actual, "K-01");
            Assert.Single(almacen.Todas(Conversacion));
        }

        [Fact]
        public void PA_DOB_12_UnUpdateRequestConVersionDeFilaVieja_SaleComoLaFallaRealYConLaVersionVigenteComoLaRespuestaReal()
        {
            var fixture = LectorDeFixtures.Abrir("07-fallas.json");
            var real = fixture.Falla("version_de_fila_no_coincide");
            var respuestaReal = fixture.Respuesta("version_de_fila_no_coincide", "control_con_version_vigente", "respuesta");
            Assert.Equal(unchecked((int)0x80060882), real.Detail.ErrorCode);
            Assert.True(fixture.Nodo("version_de_fila_no_coincide", "rowversion_cambia_tras_update").GetBoolean());

            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = ConstructorDeDatos.UnaConversacion().Construir();
            almacen.Sembrar(conversacion);
            var versionVieja = almacen.Retrieve(Conversacion, conversacion.Id, new ColumnSet("sanic_asunto")).RowVersion;
            almacen.Update(new Entity(Conversacion, conversacion.Id) { ["sanic_asunto"] = "cambiado por otro" });
            var versionVigente = almacen.Retrieve(Conversacion, conversacion.Id, new ColumnSet("sanic_asunto")).RowVersion;
            Assert.NotEqual(versionVieja, versionVigente);
            Assert.Matches("^[0-9]+$", versionVigente);

            var conLaVieja = new UpdateRequest
            {
                Target = new Entity(Conversacion, conversacion.Id) { ["sanic_asunto"] = "mío", RowVersion = versionVieja },
                ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches
            };
            var actual = ApoyoDeDobles.Falla(() => almacen.Execute(conLaVieja));
            AssertFallaComoLaReal(real, actual, "versión de fila");
            Assert.Equal("cambiado por otro", almacen.Retrieve(Conversacion, conversacion.Id, new ColumnSet("sanic_asunto"))["sanic_asunto"]);

            var conLaVigente = new UpdateRequest
            {
                Target = new Entity(Conversacion, conversacion.Id) { ["sanic_asunto"] = "mío", RowVersion = versionVigente },
                ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches
            };
            var respuesta = almacen.Execute(conLaVigente);
            Assert.Equal(respuestaReal.GetType(), respuesta.GetType());
            Assert.Equal(respuestaReal.ResponseName, respuesta.ResponseName);
            Assert.Equal(respuestaReal.Results.Count, respuesta.Results.Count);
            Assert.Equal("mío", almacen.Retrieve(Conversacion, conversacion.Id, new ColumnSet("sanic_asunto"))["sanic_asunto"]);
            Assert.NotEqual(versionVigente, almacen.Retrieve(Conversacion, conversacion.Id, new ColumnSet("sanic_asunto")).RowVersion);
        }

        [Fact]
        public void PA_DOB_12_UnRetrieveDeUnaFilaQueNoExisteSaleComoLaFallaReal()
        {
            var real = LectorDeFixtures.Abrir("07-fallas.json").Falla("retrieve_de_fila_inexistente");
            Assert.Equal(unchecked((int)0x80040217), real.Detail.ErrorCode);
            var almacen = new OrganizationServiceEnMemoria();
            var inexistente = Guid.NewGuid();

            var actual = ApoyoDeDobles.Falla(() => almacen.Retrieve(Conversacion, inexistente, new ColumnSet("sanic_nombre")));

            AssertFallaComoLaReal(real, actual, "fila inexistente");
            Assert.Contains(inexistente.ToString(), actual.Detail.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Conversacion, actual.Detail.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA_DOB_12_UnaFallaConfiguradaConFallarEnTambienSaleConElTipoYLaFormaDeLaReal()
        {
            var real = LectorDeFixtures.Abrir("07-fallas.json").Falla("k01_violacion_de_clave_alterna");
            var almacen = new OrganizationServiceEnMemoria();
            almacen.FallarEn(OperacionDeDatos.Create, Conversacion, 1, real.Detail.ErrorCode);

            var actual = ApoyoDeDobles.Falla(() => almacen.Create(new Entity(Conversacion)));

            AssertFallaComoLaReal(real, actual, "FallarEn");
            Assert.Empty(almacen.Todas(Conversacion));
        }

        // =====================================================================================================================
        // PA-DOB-13: paginación
        // =====================================================================================================================

        private static QueryExpression ConsultaPaginada(int pagina, string cookie, bool pedirTotal)
        {
            var consulta = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet("sanic_nombre", "statecode", "statuscode") };
            consulta.AddOrder("createdon", OrderType.Ascending);
            consulta.PageInfo = new PagingInfo { Count = 2, PageNumber = pagina, PagingCookie = cookie, ReturnTotalRecordCount = pedirTotal };
            return consulta;
        }

        [Fact]
        public void PA_DOB_13_LaPaginacionDeDosEnDosDevuelveLasMismasPaginasLaMismaCookieYLosMismosTotalesQueLaPlataforma()
        {
            var fixture = LectorDeFixtures.Abrir("07-fallas.json");
            var pagina1 = fixture.Coleccion("paginacion", "pagina_1");
            var pagina2 = fixture.Coleccion("paginacion", "pagina_2");
            // AJUSTADA en la vuelta 2 (empates adversarios): dos pares de filas empatan en createdon (15:06:51 y 15:06:52) y Dev las devolvió en el orden de su clave primaria. El doble ordena
            // los empates al INVERSO del orden de entrada, así que las filas se siembran en orden inverso al real para que, tras el empate adversario, salgan en el orden de Dev.
            var almacen = Almacen(pagina1.Entities.Concat(pagina2.Entities).Reverse().ToArray());
            Assert.True(pagina1.MoreRecords);
            Assert.False(pagina2.MoreRecords);
            Assert.All(pagina1.Entities.Concat(pagina2.Entities), e => Assert.True(e.Contains("createdon"), "la plataforma agrega la columna del OrderExpression aunque no se pida"));

            var primera = almacen.RetrieveMultiple(ConsultaPaginada(1, null, pedirTotal: true));
            AssertColeccionIgual(pagina1, primera, respetarOrden: true, donde: "página 1");
            Assert.True(primera.MoreRecords);
            Assert.Equal(4, primera.TotalRecordCount);

            var segunda = almacen.RetrieveMultiple(ConsultaPaginada(2, primera.PagingCookie, pedirTotal: true));
            AssertColeccionIgual(pagina2, segunda, respetarOrden: true, donde: "página 2");
            Assert.False(segunda.MoreRecords);
            Assert.Empty(primera.Entities.Select(e => e.Id).Intersect(segunda.Entities.Select(e => e.Id)));
        }

        [Fact]
        public void PA_DOB_13_SinPedirElTotalElTotalEsMenosUnoYAunAsiHayCookie()
        {
            var fixture = LectorDeFixtures.Abrir("07-fallas.json");
            var sinTotal = fixture.Nodo("paginacion", "pagina_1_sin_ReturnTotalRecordCount");
            Assert.Equal(-1, sinTotal.GetProperty("TotalRecordCount").GetInt32());
            Assert.True(sinTotal.GetProperty("tiene_PagingCookie").GetBoolean());
            Assert.True(sinTotal.GetProperty("MoreRecords").GetBoolean());
            var almacen = Almacen(fixture.Coleccion("paginacion", "pagina_1").Entities.Concat(fixture.Coleccion("paginacion", "pagina_2").Entities).ToArray());

            var obtenida = almacen.RetrieveMultiple(ConsultaPaginada(1, null, pedirTotal: false));

            Assert.Equal(-1, obtenida.TotalRecordCount);
            Assert.False(obtenida.TotalRecordCountLimitExceeded);
            Assert.True(obtenida.MoreRecords);
            Assert.NotNull(obtenida.PagingCookie);
            Assert.NotEqual(string.Empty, obtenida.PagingCookie);
        }

        // =====================================================================================================================
        // PA-DOB-14: los constructores de datos producen la forma de la fila real del mismo estado
        // =====================================================================================================================

        public static IEnumerable<object[]> ConstructoresYFilasReales()
        {
            yield return new object[] { "conversacion-abierta", "02-conversacion.json", "abierta" };
            yield return new object[] { "conversacion-finalizada", "02-conversacion.json", "finalizada" };
            yield return new object[] { "conversacion-unida", "02-conversacion.json", "unida" };
            yield return new object[] { "arreglo-borrador", "03-arreglo.json", "borrador_congelado" };
            yield return new object[] { "arreglo-vigente", "03-arreglo.json", "vigente" };
            yield return new object[] { "cliente", "03-arreglo.json", "fila_relacionada_cliente" };
            yield return new object[] { "version-vigente", "03-arreglo.json", "fila_relacionada_version_de_plantilla" };
            yield return new object[] { "correo-entrante", "01-email.json", "retrieve_columnset_true_entrante/0" };
            yield return new object[] { "correo-saliente", "01-email.json", "retrieve_columnset_true_saliente/0" };
            yield return new object[] { "membresia", "04-teammembership-team-queue.json", "teammembership_de_usuario_conectado/Entities/0" };
            yield return new object[] { "team", "04-teammembership-team-queue.json", "team_isdefault_de_la_BU_Arreglos_de_Pago/Entities/0" };
            yield return new object[] { "cola", "04-teammembership-team-queue.json", "queue_con_emailaddress/0" };
            yield return new object[] { "usuario", "06-whoami-systemuser.json", "systemuser_ordinario_usuario_de_aplicacion_conectado" };
            yield return new object[] { "system", "06-whoami-systemuser.json", "systemuser_SYSTEM" };
            yield return new object[] { "definicion-de-variable", "05-environmentvariable.json", "definicion_descartable" };
            yield return new object[] { "valor-de-variable", "05-environmentvariable.json", "valor_descartable" };
        }

        private static ConstructorDeFila Constructor(string clave)
        {
            switch (clave)
            {
                case "conversacion-abierta": return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Abierta);
                case "conversacion-finalizada": return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada);
                case "conversacion-unida": return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida);
                case "arreglo-borrador": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Borrador);
                case "arreglo-vigente": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Vigente);
                case "cliente": return ConstructorDeDatos.UnCliente();
                case "version-vigente": return ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente);
                case "correo-entrante": return ConstructorDeDatos.UnCorreoEntrante();
                case "correo-saliente": return ConstructorDeDatos.UnCorreoSaliente();
                case "membresia": return ConstructorDeDatos.UnaMembresiaDeTeam(Guid.NewGuid(), Guid.NewGuid());
                case "team": return ConstructorDeDatos.UnTeam("ZZF Team prueba", Guid.NewGuid(), predeterminado: true);
                case "cola": return ConstructorDeDatos.UnaCola("zzf-cola@example.com");
                case "usuario": return ConstructorDeDatos.UnUsuario(Guid.NewGuid());
                case "system": return ConstructorDeDatos.UnSystem(Guid.NewGuid());
                case "definicion-de-variable": return ConstructorDeDatos.UnaDefinicionDeVariable("sanic_zzf_prueba", "ZZF-defecto");
                case "valor-de-variable": return ConstructorDeDatos.UnValorDeVariable("sanic_zzf_prueba", Guid.NewGuid(), "ZZF-valor");
                default: throw new ArgumentException("clave desconocida: " + clave);
            }
        }

        [Theory]
        [MemberData(nameof(ConstructoresYFilasReales))]
        public void PA_DOB_14_UnaFilaDelConstructorDeDatosTieneLaFormaDeLaFilaRealDelMismoEstado(string clave, string archivo, string ruta)
        {
            var real = LectorDeFixtures.Abrir(archivo).Entidad(Ruta(ruta));
            var propia = Constructor(clave).Construir();
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(propia);

            var devuelta = almacen.Retrieve(propia.LogicalName, propia.Id, new ColumnSet(true));

            ComparadorDeFormas.AssertMismaForma(real, devuelta, clave);
            Assert.True(devuelta.Attributes.Count >= 3, clave + ": un constructor sin columnas no mide nada");
            Assert.DoesNotContain(devuelta.Attributes, par => par.Value is Money);
        }

        [Fact]
        public void PA_DOB_14_ElCorreoEntranteNoTieneSentOnYElSalienteSi()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var entrante = ConstructorDeDatos.UnCorreoEntrante().Construir();
            var saliente = ConstructorDeDatos.UnCorreoSaliente().Construir();
            almacen.Sembrar(entrante, saliente);

            var deEntrante = almacen.Retrieve("email", entrante.Id, new ColumnSet(true));
            var deSaliente = almacen.Retrieve("email", saliente.Id, new ColumnSet(true));

            Assert.False(deEntrante.Contains("senton"));
            Assert.False((bool)deEntrante["directioncode"]);
            Assert.Equal(4, ((OptionSetValue)deEntrante["statuscode"]).Value);
            Assert.True(deSaliente.Contains("senton"));
            Assert.True((bool)deSaliente["directioncode"]);
            Assert.Equal(3, ((OptionSetValue)deSaliente["statuscode"]).Value);
            Assert.All(new[] { deEntrante, deSaliente }, c => Assert.StartsWith("<", (string)c["messageid"], StringComparison.Ordinal));
        }

        // =====================================================================================================================
        // PA-DOB-15: sin joins, y por eso sin AliasedValue
        // =====================================================================================================================

        [Fact]
        public void PA_DOB_15_LaConsultaConLosTresJoinsQueProdujoLosAliasDelFixture08LanzaYNoDevuelveNada()
        {
            var borrador = LectorDeFixtures.Abrir("03-arreglo.json").Entidad("borrador_congelado");
            var almacen = Almacen(borrador);
            var consulta = new QueryExpression(Arreglo) { ColumnSet = new ColumnSet("sanic_nombre", "sanic_saldoinicial", "statuscode", "sanic_fechainicio") };
            var cliente = consulta.AddLink("sanic_carp_tbl_cliente", "sanic_clienteid", "sanic_carp_tbl_clienteid", JoinOperator.LeftOuter);
            cliente.EntityAlias = "cli";
            cliente.Columns = new ColumnSet("sanic_nombre", "createdby", "createdon", "statecode");
            var version = consulta.AddLink("sanic_carp_tbl_versionplantilla", "sanic_versionplantillaid", "sanic_carp_tbl_versionplantillaid", JoinOperator.LeftOuter);
            version.EntityAlias = "ver";

            var error = Assert.Throws<NotSupportedException>(() => almacen.RetrieveMultiple(consulta));

            Assert.Contains("LinkEntities", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA_DOB_15_UnFetchXmlAgregadoLanzaPorqueElDobleNoLoSimula()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var fetch = "<fetch aggregate='true'><entity name='" + Conversacion + "'><attribute name='" + Conversacion + "id' alias='total' aggregate='count'/></entity></fetch>";

            Assert.Throws<NotSupportedException>(() => almacen.RetrieveMultiple(new FetchExpression(fetch)));
        }

        // =====================================================================================================================
        // PA-DOB-16: tablas intersect con Guid crudo, y el Name del EntityReference
        // =====================================================================================================================

        [Theory]
        [InlineData("teammembership", "systemuserid")]
        [InlineData("teammembership", "teamid")]
        [InlineData("teammembership", "teammembershipid")]
        [InlineData("systemuserroles", "systemuserid")]
        [InlineData("systemuserroles", "roleid")]
        [InlineData("systemuserroles", "systemuserroleid")]
        public void PA_DOB_16_UnaTablaIntersectRechazaUnEntityReferenceAlSembrarAlCrearYAlActualizar(string tabla, string columna)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var equivocada = new EntityReference("systemuser", Guid.NewGuid());

            Assert.ThrowsAny<ArgumentException>(() => almacen.Sembrar(new Entity(tabla, Guid.NewGuid()) { [columna] = equivocada }));
            Assert.ThrowsAny<ArgumentException>(() => almacen.Create(new Entity(tabla) { [columna] = equivocada }));
            var id = almacen.Create(new Entity(tabla) { ["systemuserid"] = Guid.NewGuid() });
            Assert.ThrowsAny<ArgumentException>(() => almacen.Update(new Entity(tabla, id) { [columna] = equivocada }));
            Assert.DoesNotContain(almacen.Todas(tabla), f => f.Contains(columna) && f[columna] is EntityReference);
            Assert.IsType<Guid>(almacen.Retrieve(tabla, id, new ColumnSet(true))["systemuserid"]);
        }

        [Fact]
        public void PA_DOB_16_ElDobleNoRellenaElNameDeUnEntityReference_PuedeSerNulo()
        {
            var real = LectorDeFixtures.Abrir("02-conversacion.json").Entidad("abierta");
            Assert.Null(((EntityReference)real["owningteam"]).Name);
            var almacen = Almacen(real);
            var id = almacen.Create(new Entity(Conversacion) { ["ownerid"] = new EntityReference("team", ApoyoDeDobles.Team1) });

            var sembrada = almacen.Retrieve(Conversacion, real.Id, new ColumnSet("owningteam", "ownerid"));
            var creada = almacen.Retrieve(Conversacion, id, new ColumnSet("ownerid"));

            Assert.Null(((EntityReference)sembrada["owningteam"]).Name);
            Assert.Equal("ZZF Team 1", ((EntityReference)sembrada["ownerid"]).Name);
            Assert.Null(((EntityReference)creada["ownerid"]).Name);
            Assert.Equal(ApoyoDeDobles.Team1, ((EntityReference)creada["ownerid"]).Id);
        }
    }
}
