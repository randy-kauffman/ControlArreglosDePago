using System;
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
    /// <b>Lote 3b: privilegios por operación y seguridad de columna en la escritura</b> (punto (a), <c>salida/22-privilegios.json</c>, medido el 2026-10-15) y <b>tablas nativas</b>
    /// (punto (13), <c>salida/19-nativas-sin-muestra.json</c>).
    /// <para>
    /// <b>Privilegios.</b> Sin el privilegio de una operación sobre una tabla: <c>0x80040220</c>, mensaje "Principal user (Id=…) is missing prv&lt;Op&gt;&lt;tabla&gt; privilege …", <c>InnerFault</c> de 1 nivel con
    /// el mismo código. Medido en tablas propias: Create (Conversación, Versión), Update (Versión; sobre Conversación SÍ funciona), Delete (Cliente, Conversación, Versión). La lectura denegada se midió sobre una
    /// tabla AJENA al proyecto (el rol heredado lee las siete propias) con el mismo código y formato. El doble lo declara con <c>DenegarPrivilegio(usuario, tabla, operacion)</c> y la falla ocurre antes de validar y de
    /// correr steps (decisión del arquitecto: la plataforma comprueba el privilegio al recibir la petición; el orden respecto de los steps de PreValidation no se midió). El privilegio de Assign NO se midió: se rechaza al declararlo.
    /// <b>Columna protegida (cédula).</b> Escribirla sin permiso: <c>Create</c> 0x8004F502, <c>Update</c> 0x8004F507; leerla no falla, la columna no viene (<c>OcultarColumna</c>, que ya existía). Las dos mitades
    /// se declaran por separado a propósito (PA_PIP_16 escribe como un usuario con la lectura oculta).
    /// </para>
    /// <para>
    /// <b>Nativas.</b> <c>systemuserroles</c> no admite <c>Retrieve</c> (<c>0x80040800</c>), solo <c>RetrieveMultiple</c>. <c>businessunit</c>, <c>role</c>, <c>organization</c> y <c>mailbox</c> NO se tipan:
    /// las filas exportadas traen solo las columnas no nulas (la <c>organization</c> real tiene muchas más de las 412 vistas y los dos <c>mailbox</c> tenían 66 y 59), así que la unión de columnas de una muestra
    /// es incompleta y rechazaría columnas válidas que esa fila tenía nulas: un falso rechazo cuesta más que un falso permiso. Siguen como nativas sin muestra (tabla aceptada, columnas sin validar).
    /// </para>
    /// </summary>
    public class MedidoEnDevPrivilegiosYNativasAceptacion
    {
        // ---------------------------------------------------------------------------------------------------------------------
        // Privilegios
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_PRV_01_SinElPrivilegioDeLecturaRetrieveYRetrieveMultipleFallan0x80040220_SoloParaEseUsuarioYEsaTabla()
        {
            // 22-privilegios.json (RetrieveMultiple y Retrieve sobre una tabla ajena): 0x80040220, 'is missing prvRead...'. Mata: simular solo NegarLectura por fila.
            var almacen = new OrganizationServiceEnMemoria();
            var id = UnCliente(almacen);
            var conversacion = Sembrada(almacen, Conversacion);
            almacen.DenegarPrivilegio(Usuario1, Cliente, OperacionDeDatos.Retrieve);
            almacen.DenegarPrivilegio(Usuario1, Cliente, OperacionDeDatos.RetrieveMultiple);

            var deRetrieve = Falla(() => almacen.ComoUsuario(Usuario1).Retrieve(Cliente, id, new ColumnSet()));
            var deConsulta = Falla(() => almacen.ComoUsuario(Usuario1).RetrieveMultiple(new QueryExpression(Cliente) { ColumnSet = new ColumnSet() }));

            foreach (var falla in new[] { deRetrieve, deConsulta })
            {
                AssertFalla(falla, CodigosDeFalla.SinPrivilegio, "is missing prvRead");
                Assert.Contains("Principal user (Id=" + Usuario1, falla.Detail.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("'" + Cliente + "'", falla.Detail.Message, StringComparison.Ordinal);
            }
            Assert.Equal(id, almacen.Retrieve(Cliente, id, new ColumnSet()).Id);
            Assert.Equal(id, almacen.ComoUsuario(Usuario2).Retrieve(Cliente, id, new ColumnSet()).Id);
            Assert.Equal(conversacion, almacen.ComoUsuario(Usuario1).Retrieve(Conversacion, conversacion, new ColumnSet()).Id);
        }

        [Theory]
        [InlineData("Create", "prvCreate")]
        [InlineData("Update", "prvWrite")]
        [InlineData("Delete", "prvDelete")]
        public void PA_MED_PRV_02_SinElPrivilegioDeEscrituraCreacionOBorradoLaOperacionFalla0x80040220_YNoTocaNada(string nombreDeOperacion, string privilegio)
        {
            var operacion = (OperacionDeDatos)Enum.Parse(typeof(OperacionDeDatos), nombreDeOperacion);
            // 22-privilegios.json: Create (Conversación, Versión), Update (Versión) y Delete (Cliente, Conversación, Versión) => 0x80040220 'is missing prvCreate|prvWrite|prvDelete<tabla>'.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Conversacion);
            almacen.DenegarPrivilegio(Usuario1, Conversacion, operacion);
            var persona = almacen.ComoUsuario(Usuario1);

            var falla = Falla(() =>
            {
                if (operacion == OperacionDeDatos.Create) persona.Create(new Entity(Conversacion));
                else if (operacion == OperacionDeDatos.Update) persona.Update(new Entity(Conversacion, existente) { ["sanic_asunto"] = "ZZF-PRUEBA" });
                else persona.Delete(Conversacion, existente);
            });

            AssertFalla(falla, CodigosDeFalla.SinPrivilegio, "is missing " + privilegio);
            Assert.Contains("'" + Conversacion + "'", falla.Detail.Message, StringComparison.Ordinal);
            Assert.Single(almacen.Todas(Conversacion));
            Assert.False(almacen.Leer(Conversacion, existente).Contains("sanic_asunto"));
            Assert.Empty(almacen.Escrituras);
        }

        [Fact]
        public void PA_MED_PRV_03_SoloFallaLaOperacionDenegada_LasDemasDeLaMismaTablaFuncionan()
        {
            // 22-privilegios.json: en Conversación Create falla y Update funciona. Mata: denegar toda la tabla.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Conversacion);
            almacen.DenegarPrivilegio(Usuario1, Conversacion, OperacionDeDatos.Create);
            var persona = almacen.ComoUsuario(Usuario1);

            persona.Update(new Entity(Conversacion, existente) { ["sanic_asunto"] = "ZZF-PRUEBA" });
            persona.Retrieve(Conversacion, existente, new ColumnSet());
            AssertFalla(Falla(() => persona.Create(new Entity(Conversacion))), CodigosDeFalla.SinPrivilegio);

            Assert.Equal("ZZF-PRUEBA", almacen.Leer(Conversacion, existente)["sanic_asunto"]);
        }

        [Fact]
        public void PA_MED_PRV_04_LaFallaDePrivilegioOcurreAntesDeValidarYAntesDeCorrerLosSteps()
        {
            // Decisión del arquitecto (no medida): se comprueba el privilegio al recibir la petición. Una escritura mal formada sin privilegio da SinPrivilegio, no ColumnaInexistente, y ningún step corre.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            var espia = new PluginEspia("preval");
            pipeline.Registrar(espia, "Create", Conversacion, EtapaDeStep.PreValidation);
            fabrica.Almacen.DenegarPrivilegio(Usuario1, Conversacion, OperacionDeDatos.Create);

            var falla = Falla(() => fabrica.CreateOrganizationService(Usuario1).Create(new Entity(Conversacion) { ["sanic_noexiste"] = "x" }));

            AssertFalla(falla, CodigosDeFalla.SinPrivilegio);
            Assert.Empty(espia.Llamadas);
        }

        [Fact]
        public void PA_MED_PRV_05_SystemNoSeVeAfectado_LaDenegacionEsPorUsuario()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.DenegarPrivilegio(Usuario1, Cliente, OperacionDeDatos.Create);

            almacen.Create(new Entity(Cliente));
            almacen.ComoUsuario(Usuario2).Create(new Entity(Cliente));
            almacen.ComoUsuario(OrganizationServiceEnMemoria.IdDeSystemPorDefecto).Create(new Entity(Cliente));

            Assert.Equal(3, almacen.Todas(Cliente).Count);
        }

        [Theory]
        [InlineData("Assign")]
        [InlineData("WhoAmI")]
        public void PA_MED_PRV_06_ElPrivilegioDeOperacionesNoMedidasSeRechazaAlDeclararlo_NotSupported(string nombreDeOperacion)
        {
            var operacion = (OperacionDeDatos)Enum.Parse(typeof(OperacionDeDatos), nombreDeOperacion);
            // Solo se midieron lectura, creación, escritura y borrado. Lanza antes que adivinar el nombre del privilegio de Assign (prvAssign...) y su comportamiento.
            var almacen = new OrganizationServiceEnMemoria();

            var error = Assert.Throws<NotSupportedException>(() => almacen.DenegarPrivilegio(Usuario1, Arreglo, operacion));

            Assert.Contains(operacion.ToString(), error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA_MED_PRV_07_NegarLecturaDeUnaFilaDaLaFallaDePrivilegioConLaFormaMedida()
        {
            // NegarLectura ya existía con un mensaje marcado "NO VERIFICADO"; el código 0x80040220 ahora es el medido y el mensaje toma su forma. Los demás contratos de NegarLectura no cambian (SeguridadSimuladaAceptacion).
            var almacen = new OrganizationServiceEnMemoria();
            var id = UnCliente(almacen);
            almacen.NegarLectura(Usuario1, Cliente, id);

            var falla = Falla(() => almacen.ComoUsuario(Usuario1).Retrieve(Cliente, id, new ColumnSet()));

            AssertFalla(falla, CodigosDeFalla.SinPrivilegio, "is missing prvRead");
            Assert.DoesNotContain("NO verificado", falla.Detail.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PA_MED_PRV_08_EscribirUnaColumnaProtegidaSinPermisoFalla0x8004F502EnCreateY0x8004F507EnUpdate()
        {
            // 22-privilegios.json: 'Caller user with Id ... does not have create|update permissions to a Cedula secured field on entity Cliente'. El doble no conoce los nombres para mostrar: nombra la columna y la tabla.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Cliente);
            almacen.DenegarEscrituraDeColumna(Usuario1, Cliente, "sanic_cedula");
            var persona = almacen.ComoUsuario(Usuario1);

            var deCreate = Falla(() => persona.Create(new Entity(Cliente) { ["sanic_cedula"] = "ZZF-ced-1" }));
            var deUpdate = Falla(() => persona.Update(new Entity(Cliente, existente) { ["sanic_cedula"] = "ZZF-ced-1" }));

            AssertFalla(deCreate, CodigosDeFalla.ColumnaProtegidaAlCrear, "does not have create permissions to a", "Create");
            AssertFalla(deUpdate, CodigosDeFalla.ColumnaProtegidaAlActualizar, "does not have update permissions to a", "Update");
            foreach (var falla in new[] { deCreate, deUpdate })
            {
                Assert.Contains("Caller user with Id " + Usuario1, falla.Detail.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("sanic_cedula secured field on entity " + Cliente, falla.Detail.Message, StringComparison.Ordinal);
            }
            Assert.Single(almacen.Todas(Cliente));
            Assert.False(almacen.Leer(Cliente, existente).Contains("sanic_cedula"));
        }

        [Fact]
        public void PA_MED_PRV_09_SinLaColumnaProtegidaLaEscrituraFunciona_YOtrosUsuariosYSystemTambienPuedenEscribirla()
        {
            // 22-privilegios.json: 'Create (sin la columna sanic_cedula)' ok, 'Update' ok. La denegación es por usuario y por columna.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Cliente);
            almacen.DenegarEscrituraDeColumna(Usuario1, Cliente, "sanic_cedula");

            almacen.ComoUsuario(Usuario1).Create(new Entity(Cliente) { ["sanic_celular"] = "88887777" });
            almacen.ComoUsuario(Usuario1).Update(new Entity(Cliente, existente) { ["sanic_celular"] = "88887777" });
            almacen.ComoUsuario(Usuario2).Update(new Entity(Cliente, existente) { ["sanic_cedula"] = "ZZF-ced-2" });
            almacen.Update(new Entity(Cliente, existente) { ["sanic_cedula"] = "ZZF-ced-3" });

            Assert.Equal("ZZF-ced-3", almacen.Leer(Cliente, existente)["sanic_cedula"]);
        }

        [Fact]
        public void PA_MED_PRV_10_LeerLaColumnaProtegidaNoFallaYLaColumnaNoVieneSoloConOcultarColumna_LasDosMitadesSonIndependientes()
        {
            // 22-privilegios.json: 'Retrieve de la columna sanic_cedula' => ok, columna_presente false. DenegarEscrituraDeColumna sola no oculta la lectura; OcultarColumna sola no impide la escritura (PA_PIP_16).
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrada(almacen, Cliente, "sanic_cedula", "ZZF-ced-1");
            almacen.DenegarEscrituraDeColumna(Usuario1, Cliente, "sanic_cedula");
            almacen.OcultarColumna(Cliente, "sanic_cedula", Usuario2);

            var conEscrituraDenegada = almacen.ComoUsuario(Usuario1).Retrieve(Cliente, id, new ColumnSet("sanic_cedula"));
            var conLecturaOculta = almacen.ComoUsuario(Usuario2).Retrieve(Cliente, id, new ColumnSet("sanic_cedula"));
            almacen.ComoUsuario(Usuario2).Update(new Entity(Cliente, id) { ["sanic_cedula"] = "ZZF-ced-9" });

            Assert.Equal("ZZF-ced-1", conEscrituraDenegada["sanic_cedula"]);
            Assert.False(conLecturaOculta.Contains("sanic_cedula"));
            Assert.Equal("ZZF-ced-9", almacen.Leer(Cliente, id)["sanic_cedula"]);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Nativas
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_NAT_01_UnRetrieveDeSystemuserrolesFalla0x80040800_SoloRetrieveMultipleFunciona()
        {
            // 19-nativas-sin-muestra.json: 'The 'Retrieve' method does not support entities of type 'systemuserroles'. MessageProcessorCache returned MessageProcessor.Empty.'; InnerFault 0.
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity("systemuserroles", id) { ["systemuserroleid"] = id, ["systemuserid"] = Usuario1, ["roleid"] = Guid.NewGuid() });

            var falla = Falla(() => almacen.Retrieve("systemuserroles", id, new ColumnSet(true)));
            var multiple = almacen.RetrieveMultiple(new QueryExpression("systemuserroles") { ColumnSet = new ColumnSet(true) });

            AssertFalla(falla, CodigosDeFalla.MensajeNoSoportado, "The 'Retrieve' method does not support entities of type 'systemuserroles'.");
            Assert.Single(multiple.Entities);
            Assert.IsType<Guid>(multiple.Entities[0]["systemuserid"]);
            Assert.Equal(Usuario1, multiple.Entities[0]["systemuserid"]);
        }

        [Fact]
        public void PA_MED_NAT_01b_ElRetrieveDeSystemuserrolesFallaAunqueLaFilaNoExista_YSeRegistraLaConsulta()
        {
            // El rechazo es del mensaje, no de la fila: no se confunde con 0x80040217. Se registra como cualquier Retrieve (Consultas incluye también las que fallan).
            var almacen = new OrganizationServiceEnMemoria();

            var falla = Falla(() => almacen.Retrieve("systemuserroles", Guid.NewGuid(), new ColumnSet()));

            AssertFalla(falla, CodigosDeFalla.MensajeNoSoportado);
            Assert.Single(almacen.Consultas);
        }

        [Theory]
        [InlineData("businessunit")]
        [InlineData("role")]
        [InlineData("organization")]
        [InlineData("mailbox")]
        public void PA_MED_NAT_02_LasNativasCuyasMuestrasSoloTraenColumnasNoNulasSiguenSinTiparse_ElDobleAceptaSusColumnas_VERDE_A_PROPOSITO(string tabla)
        {
            // VERDE_A_PROPOSITO. Límite declarado con su motivo (clase): las muestras de 19-nativas-sin-muestra.json omiten las columnas nulas, así que no alcanzan para saber qué columnas NO existen.
            var almacen = new OrganizationServiceEnMemoria();
            var id = almacen.Create(new Entity(tabla) { ["cualquier_columna_valida_pero_nula_en_la_muestra"] = 1 });

            var leida = almacen.Retrieve(tabla, id, new ColumnSet("cualquier_columna_valida_pero_nula_en_la_muestra", "otra"));

            Assert.Equal(1, leida["cualquier_columna_valida_pero_nula_en_la_muestra"]);
        }

        [Fact]
        public void PA_MED_NAT_03_RetrieveDeOtrasNativasSiguePermitido_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: 19-nativas-sin-muestra.json (businessunit, role, organization y mailbox admiten Retrieve por id): el rechazo es solo de systemuserroles.
            var almacen = new OrganizationServiceEnMemoria();
            var team = Sembrada(almacen, "team", "name", "ZZF Team");
            var unidad = Sembrada(almacen, "businessunit", "name", "ZZF BU");

            Assert.Equal("ZZF Team", almacen.Retrieve("team", team, new ColumnSet("name"))["name"]);
            Assert.Equal("ZZF BU", almacen.Retrieve("businessunit", unidad, new ColumnSet("name"))["name"]);
        }
    }
}
