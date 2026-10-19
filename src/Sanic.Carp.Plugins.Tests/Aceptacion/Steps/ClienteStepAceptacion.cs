using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>ClienteStep</c> (diseno/03 2.2, S-15 y S-16). PA-CLI-01 a PA-CLI-22 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5a llevan el prefijo
    /// PA5A_CLI. Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase (filtro y pre-imagen <c>PreImagen</c> de la sección 5).
    /// "La consulta de unicidad" es un <c>RetrieveMultiple</c> sobre el cliente que filtra por <c>sanic_cedula</c>; "leer la cédula guardada" es un <c>Retrieve</c> por id.
    /// </summary>
    public class ClienteStepAceptacion
    {
        private const string CedulaA = "001-000000-0001A";
        private const string CedulaB = "001-000000-0002B";
        private const string CedulaC = "001-000000-0003C";

        private static EscenarioDeSteps UnEscenario()
        {
            var e = new EscenarioDeSteps();
            e.Registrar(new ClienteStep());
            return e;
        }

        /// <summary>Lo que manda un alta desde la pantalla. Una columna <c>null</c> no se manda (no existe en el <c>Target</c>).</summary>
        private static Entity UnAlta(string nombreCompleto, string cedula, string correo = null, string celular = null)
        {
            var fila = new Entity(Cliente);
            if (nombreCompleto != null) fila["sanic_nombrecompleto"] = nombreCompleto;
            if (cedula != null) fila["sanic_cedula"] = cedula;
            if (correo != null) fila["sanic_correo"] = correo;
            if (celular != null) fila["sanic_celular"] = celular;
            return fila;
        }

        private static ConstructorDeFila UnClienteConocido(string nombre, string cedula)
        {
            return ConstructorDeDatos.UnCliente()
                .Con("sanic_nombre", nombre)
                .Con("sanic_nombrecompleto", nombre)
                .Con("sanic_cedula", cedula)
                .Con("sanic_celular", "8888-0000");
        }

        private static ConstructorDeFila Inactivo(ConstructorDeFila cliente)
        {
            return cliente.Con("statecode", new OptionSetValue(Estados.Inactivo)).Con("statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo)));
        }

        private static int Estado(Entity fila, string columna)
        {
            return Assert.IsType<OptionSetValue>(fila[columna]).Value;
        }

        private static void AssertLeyoLaCedulaGuardadaConSystem(EscenarioDeSteps e, Guid id)
        {
            var lecturas = e.Almacen.Consultas
                .Where(c => c.Operacion == OperacionDeDatos.Retrieve && c.Tabla == Cliente && c.Id == id && c.Columnas.Contains("sanic_cedula"))
                .ToList();
            Assert.NotEmpty(lecturas);
            Assert.All(lecturas, l => Assert.Equal(e.Almacen.IdDeSystem, l.Identidad));
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Tabla == Cliente && c.Identidad != e.Almacen.IdDeSystem && c.Columnas.Contains("sanic_cedula"));
        }

        // ---------------------------------------------------------------- Create

        [Fact]
        public void PA_CLI_01_Alta_NormalizaLosCuatroDatos_YCalculaElNombre()
        {
            var e = UnEscenario();

            var id = e.ComoOficial.Create(UnAlta(" Ana  Pérez ", " 001-000000-0001a", "ANA@X.COM ", " 8888-0000"));

            var fila = e.Fila(Cliente, id);
            Assert.Equal("Ana Pérez", fila["sanic_nombrecompleto"]);
            Assert.Equal("001-000000-0001A", fila["sanic_cedula"]);
            Assert.Equal("ana@x.com", fila["sanic_correo"]);
            Assert.Equal("8888-0000", fila["sanic_celular"]);
            Assert.Equal("Ana Pérez", fila["sanic_nombre"]);
        }

        [Fact]
        public void PA_CLI_02_NombreVacio_CARP_CLI_001_YCedulaVacia_CARP_CLI_002()
        {
            var e = UnEscenario();

            Rechaza(() => e.ComoOficial.Create(UnAlta("", CedulaA)), CodigosDeError.CLI_001);
            Rechaza(() => e.ComoOficial.Create(UnAlta("Ana Pérez", "")), CodigosDeError.CLI_002);

            Assert.Empty(e.Almacen.Todas(Cliente));
        }

        [Fact]
        public void PA_CLI_03_CedulaYaExistente_CARP_CLI_003_ConLaCedulaNormalizada_ConSystem_SinNombrarAlOtro()
        {
            var e = UnEscenario();
            var otro = e.Sembrar(UnClienteConocido("ZZF Marcador del Otro Cliente", CedulaA));
            e.Almacen.LimpiarRegistros();

            var rechazo = Rechaza(() => e.ComoOficial.Create(UnAlta("Ana Pérez", "001-000000-0001a ")), CodigosDeError.CLI_003);

            Assert.DoesNotContain("ZZF Marcador del Otro Cliente", rechazo.Message);
            Assert.DoesNotContain(otro.ToString(), rechazo.Message);
            Assert.DoesNotContain(CedulaA, rechazo.Message);
            var unicidad = Assert.Single(ConsultasDeUnicidad(e.Almacen));
            Assert.Equal(e.Almacen.IdDeSystem, unicidad.Identidad);
            var condicion = Assert.Single(unicidad.Condiciones, c => c.Columna == "sanic_cedula");
            Assert.Equal(new object[] { CedulaA }, condicion.Valores.ToArray());          // la consulta usa la cédula YA normalizada
            Assert.Single(e.Almacen.Todas(Cliente));
        }

        [Fact]
        public void PA_CLI_04_ElDuplicadoInactivo_TambienCuenta()
        {
            var e = UnEscenario();
            e.Sembrar(Inactivo(UnClienteConocido("ZZF inactivo", CedulaA)));

            Rechaza(() => e.ComoOficial.Create(UnAlta("Ana Pérez", CedulaA)), CodigosDeError.CLI_003);
        }

        [Fact]
        public void PA_CLI_05_ConSeguridadPorColumna_ElDuplicadoSeEncuentraIgual_PorqueLaConsultaUsaSystem()
        {
            // spike 4, tarea A: sin el perfil de columna la cédula llega vacía al llamador y un duplicado pasaría en silencio.
            var e = UnEscenario();
            e.Sembrar(UnClienteConocido("ZZF con cédula protegida", CedulaA));
            e.Almacen.OcultarColumna(Cliente, "sanic_cedula", e.Oficial);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Create(UnAlta("Ana Pérez", CedulaA)), CodigosDeError.CLI_003);

            var consultas = ConsultasDeUnicidad(e.Almacen);
            Assert.NotEmpty(consultas);
            Assert.All(consultas, c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
        }

        [Fact]
        public void PA_CLI_06_AltaSinCorreoNiCelular_Pasa_ConEsasColumnasSinValor()
        {
            var e = UnEscenario();

            var id = e.ComoOficial.Create(UnAlta("Ana Pérez", CedulaA));

            var fila = e.Fila(Cliente, id);
            Assert.Null(EscenarioDeSteps.Valor(fila, "sanic_correo"));
            Assert.Null(EscenarioDeSteps.Valor(fila, "sanic_celular"));
            Assert.Equal("Ana Pérez", fila["sanic_nombre"]);
        }

        // ---------------------------------------------------------------- Update: cédula

        [Fact]
        public void PA_CLI_07_UpdateDeLaCedulaALaDeOtroCliente_CARP_CLI_003()
        {
            var e = UnEscenario();
            e.Sembrar(UnClienteConocido("ZZF primero", CedulaA));
            var segundo = e.Sembrar(UnClienteConocido("ZZF segundo", CedulaB));

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, segundo) { ["sanic_cedula"] = " 001-000000-0001a" }), CodigosDeError.CLI_003);

            Assert.Equal(CedulaB, e.Fila(Cliente, segundo)["sanic_cedula"]);
        }

        [Fact]
        public void PA_CLI_08_ReescribirLaPropiaCedulaConOtroFormato_Pasa_SinConsultaDeUnicidad()
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("ZZF propio", CedulaA));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_cedula"] = "001-000000-0001a" });

            Assert.Empty(ConsultasDeUnicidad(e.Almacen));
            Assert.Equal(CedulaA, e.Fila(Cliente, id)["sanic_cedula"]);
        }

        [Fact]
        public void PA_CLI_09_UpdateDelNombreCompleto_RecalculaElNombre()
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));

            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_nombrecompleto"] = " Ana  María Pérez " });

            var fila = e.Fila(Cliente, id);
            Assert.Equal("Ana María Pérez", fila["sanic_nombrecompleto"]);
            Assert.Equal("Ana María Pérez", fila["sanic_nombre"]);
        }

        [Fact]
        public void PA_CLI_10_UpdateDelCelular_NoConsultaLaUnicidad_NiLeeLaCedula_YEscribeSoloEsaColumna()
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_celular"] = " 8888-1111 " });

            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Tabla == Cliente);
            var escritura = Assert.Single(EscriturasDe(e.Almacen, Cliente, OperacionDeDatos.Update));
            Assert.Equal(new[] { "sanic_celular" }, escritura.Columnas.ToArray());          // 2.2: "solo las columnas presentes en el Target"
            Assert.Equal("8888-1111", e.Fila(Cliente, id)["sanic_celular"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PA_CLI_11_UpdateConCedulaVacia_CARP_CLI_002(string vacia)
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_cedula"] = vacia }), CodigosDeError.CLI_002);

            Assert.Equal(CedulaA, e.Fila(Cliente, id)["sanic_cedula"]);
        }

        [Fact]
        public void PA_CLI_12_SinPerfilDeColumna_ReenviarLaMismaCedulaNoEsUnCambio_ElStepLaLeeConSystem()
        {
            // DC-28: el llamador no ve su cédula guardada; si la comparara con "nada" vería un cambio que no existe.
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));
            e.Almacen.OcultarColumna(Cliente, "sanic_cedula", e.Oficial);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_cedula"] = "001-000000-0001a" });

            Assert.Empty(ConsultasDeUnicidad(e.Almacen));
            AssertLeyoLaCedulaGuardadaConSystem(e, id);
            Assert.Equal(CedulaA, e.Fila(Cliente, id)["sanic_cedula"]);
        }

        [Fact]
        public void PA_CLI_13_SinPerfilDeColumna_LaCedulaDeOtroClienteEsUnCambio_UnicidadConSystem_CARP_CLI_003()
        {
            var e = UnEscenario();
            e.Sembrar(UnClienteConocido("ZZF primero", CedulaA));
            var segundo = e.Sembrar(UnClienteConocido("ZZF segundo", CedulaB));
            e.Almacen.OcultarColumna(Cliente, "sanic_cedula", e.Oficial);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, segundo) { ["sanic_cedula"] = CedulaA }), CodigosDeError.CLI_003);

            var consultas = ConsultasDeUnicidad(e.Almacen);
            Assert.NotEmpty(consultas);
            Assert.All(consultas, c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
        }

        // ---------------------------------------------------------------- Update: estado nativo

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA_CLI_14_UnOficialNoDesactivaUnCliente_NiConSoloStatecodeNiConLasDosColumnas(bool conStatuscode)
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));
            var cambio = new Entity(Cliente, id) { ["statecode"] = new OptionSetValue(Estados.Inactivo) };
            if (conStatuscode) cambio["statuscode"] = new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo));

            Rechaza(() => e.ComoOficial.Update(cambio), CodigosDeError.CLI_004);

            Assert.Equal(Estados.Activo, Estado(e.Fila(Cliente, id), "statecode"));
        }

        [Fact]
        public void PA_CLI_15_UnOficialNoReactivaUnClienteInactivo()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Inactivo(UnClienteConocido("Ana Pérez", CedulaA)));

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id)
            {
                ["statecode"] = new OptionSetValue(Estados.Activo),
                ["statuscode"] = new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo))
            }), CodigosDeError.CLI_004);

            Assert.Equal(Estados.Inactivo, Estado(e.Fila(Cliente, id), "statecode"));
        }

        [Fact]
        public void PA_CLI_16_DesactivarYCambiarElCelularEnElMismoGuardado_CARP_CLI_004_GanaElPasoCero()
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id)
            {
                ["statecode"] = new OptionSetValue(Estados.Inactivo),
                ["statuscode"] = new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo)),
                ["sanic_celular"] = " 8888-9999 "
            }), CodigosDeError.CLI_004);

            var fila = e.Fila(Cliente, id);
            Assert.Equal(Estados.Activo, Estado(fila, "statecode"));
            Assert.Equal("8888-0000", fila["sanic_celular"]);
        }

        [Fact]
        public void PA_CLI_17_ElSistemaCambiaElEstado_Pasa_SinModificarElTarget()
        {
            var e = UnEscenario();
            var activo = e.Sembrar(UnClienteConocido("ZZF activo", CedulaA));
            var inactivo = e.Sembrar(Inactivo(UnClienteConocido("ZZF inactivo", CedulaB)));
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(new Entity(Cliente, activo)
            {
                ["statecode"] = new OptionSetValue(Estados.Inactivo),
                ["statuscode"] = new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo))
            });
            e.ComoSistema.Update(new Entity(Cliente, inactivo)
            {
                ["statecode"] = new OptionSetValue(Estados.Activo),
                ["statuscode"] = new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo))
            });

            Assert.Equal(Estados.Inactivo, Estado(e.Fila(Cliente, activo), "statecode"));
            Assert.Equal(Estados.Activo, Estado(e.Fila(Cliente, inactivo), "statecode"));
            var escrituras = EscriturasDe(e.Almacen, Cliente, OperacionDeDatos.Update);
            Assert.Equal(2, escrituras.Count);
            Assert.All(escrituras, escritura => Assert.Equal(new[] { "statecode", "statuscode" }, escritura.Columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray()));
        }

        [Fact]
        public void PA_CLI_18_ReenviarElMismoEstadoJuntoConUnCambioDeCelular_Pasa_SinLlamarAWhoAmI()
        {
            // DC-01: reenviar el mismo valor no es un cambio. El camino barato no consulta la identidad.
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));
            e.Almacen.FallarEn(OperacionDeDatos.WhoAmI, null, 1, CodigosDeFalla.TransaccionAbortada);

            e.ComoOficial.Update(new Entity(Cliente, id)
            {
                ["statecode"] = new OptionSetValue(Estados.Activo),
                ["statuscode"] = new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo)),
                ["sanic_celular"] = " 8888-5555 "
            });

            var fila = e.Fila(Cliente, id);
            Assert.Equal("8888-5555", fila["sanic_celular"]);
            Assert.Equal(Estados.Activo, Estado(fila, "statecode"));
        }

        [Fact]
        public void PA_CLI_19_ElDeleteDeUnClienteDeLaCuentaDePurga_NoCorreNingunStep()
        {
            // VERDE A PROPÓSITO: fija que ningún step de S-01 a S-16 se registra sobre Delete (2.2 paso 0: la purga elimina clientes con Delete y no pasa por este step).
            // Hoy pasa porque ningún registro declara Delete; si alguien agrega uno, falla.
            var e = new EscenarioDeSteps();
            e.Registrar(new PropietarioAlCrearStep());
            e.Registrar(new PropietarioAlActualizarStep());
            e.Registrar(new ClienteStep());
            var purga = Guid.NewGuid();
            e.Almacen.Sembrar(ConstructorDeDatos.UnUsuario(purga).Construir());
            var id = e.Sembrar(UnClienteConocido("ZZF a purgar", CedulaA));

            e.Fabrica.CreateOrganizationService(purga).Delete(Cliente, id);

            Assert.Empty(e.Pipeline.Ejecuciones);
            Assert.Empty(e.Almacen.Todas(Cliente));
        }

        // ---------------------------------------------------------------- Update: nombre mostrado

        [Fact]
        public void PA_CLI_20_UnOficialNoCambiaElNombreMostrado_CARP_CLI_005()
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_nombre"] = "Otro nombre" }), CodigosDeError.CLI_005);

            Assert.Equal("Ana Pérez", e.Fila(Cliente, id)["sanic_nombre"]);
        }

        [Fact]
        public void PA_CLI_21_ReenviarElMismoNombreMostrado_NoEsUnCambio_ElStepRecalculaElNombre()
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));

            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_nombrecompleto"] = "Ana María Pérez", ["sanic_nombre"] = "Ana Pérez" });

            var fila = e.Fila(Cliente, id);
            Assert.Equal("Ana María Pérez", fila["sanic_nombrecompleto"]);
            Assert.Equal("Ana María Pérez", fila["sanic_nombre"]);
        }

        [Fact]
        public void PA_CLI_22_UnNombreMostradoDistintoGanaAlRecalculo_ParaUnaPersona_YPasaParaElSistema()
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_nombrecompleto"] = "Ana María Pérez", ["sanic_nombre"] = "Distinto" }), CodigosDeError.CLI_005);
            var intacto = e.Fila(Cliente, id);
            Assert.Equal("Ana Pérez", intacto["sanic_nombrecompleto"]);                      // no se recalculó nada
            Assert.Equal("Ana Pérez", intacto["sanic_nombre"]);

            e.Almacen.LimpiarRegistros();
            e.ComoSistema.Update(new Entity(Cliente, id) { ["sanic_nombre"] = "Puesto por el sistema" });

            Assert.Equal("Puesto por el sistema", e.Fila(Cliente, id)["sanic_nombre"]);
            var escritura = Assert.Single(EscriturasDe(e.Almacen, Cliente, OperacionDeDatos.Update));
            Assert.Equal(new[] { "sanic_nombre" }, escritura.Columnas.ToArray());
        }

        // ---------------------------------------------------------------- Agregadas por el arquitecto del 5a

        [Fact]
        public void PA5A_CLI_01_UnNombreMostradoQueManda_UnaPersonaEnElAlta_SeSobrescribe()
        {
            // 2.2 Update paso 0 bis: "En el Create no hace falta regla: el paso 3 sobrescribe lo que haya mandado la persona."
            var e = UnEscenario();
            var alta = UnAlta(" Ana  Pérez ", CedulaA);
            alta["sanic_nombre"] = "Lo que mandó la persona";

            var id = e.ComoOficial.Create(alta);

            Assert.Equal("Ana Pérez", e.Fila(Cliente, id)["sanic_nombre"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PA5A_CLI_02_ElNombreCompletoAusenteOVacio_CARP_CLI_001(string nombre)
        {
            // Una columna ausente del Target de un Create es una columna vacía (nota de ClienteStep).
            var e = UnEscenario();

            Rechaza(() => e.ComoOficial.Create(UnAlta(nombre, CedulaA)), CodigosDeError.CLI_001);

            Assert.Empty(e.Almacen.Todas(Cliente));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PA5A_CLI_03_LaCedulaAusenteOVacia_CARP_CLI_002(string cedula)
        {
            var e = UnEscenario();

            Rechaza(() => e.ComoOficial.Create(UnAlta("Ana Pérez", cedula)), CodigosDeError.CLI_002);

            Assert.Empty(ConsultasDeUnicidad(e.Almacen));                                      // la unicidad necesita que la cédula exista (P-05)
        }

        [Fact]
        public void PA5A_CLI_04_ConNombreYCedulaVacios_GanaElNombre()
        {
            // 2.2 Create paso 2: "Nombre completo vacío ⇒ CARP-CLI-001. Cédula vacía ⇒ CARP-CLI-002", en ese orden.
            var e = UnEscenario();

            Rechaza(() => e.ComoOficial.Create(UnAlta("", "")), CodigosDeError.CLI_001);
        }

        [Fact]
        public void PA5A_CLI_05_UnAltaDelSistema_TambienNormalizaYComprueba_LaUnicidad()
        {
            // 2.2 no exceptúa al sistema en el Create (la excepción por DC-03 es del Update de estado).
            var e = UnEscenario();
            e.Sembrar(UnClienteConocido("ZZF existente", CedulaA));

            Rechaza(() => e.ComoSistema.Create(UnAlta("Ana Pérez", " 001-000000-0001a")), CodigosDeError.CLI_003);
            var id = e.ComoSistema.Create(UnAlta(" Ana  Pérez ", CedulaB, "ANA@X.COM"));

            var fila = e.Fila(Cliente, id);
            Assert.Equal("Ana Pérez", fila["sanic_nombre"]);
            Assert.Equal("ana@x.com", fila["sanic_correo"]);
        }

        [Fact]
        public void PA5A_CLI_06_UnaCedulaNuevaEnElUpdate_SeConsultaConSystem_ExcluyendoAlPropioRegistro_YQuedaNormalizada()
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_cedula"] = " 001-000000-0003c " });

            var unicidad = Assert.Single(ConsultasDeUnicidad(e.Almacen));
            Assert.Equal(e.Almacen.IdDeSystem, unicidad.Identidad);
            Assert.Equal(CedulaC, e.Fila(Cliente, id)["sanic_cedula"]);
        }

        [Fact]
        public void PA5A_CLI_07_UnaCedulaGuardadaSinNormalizar_AlReenviarlaNormalizada_NoSeChocaConsigoMisma()
        {
            // "excluyendo el propio registro" (2.2 Update paso 2): la consulta de Dataverse no distingue mayúsculas, así que una cédula guardada en minúscula
            // (de antes del plugin) coincidiría con ella misma. Cualquiera sea la forma de comparar, el guardado pasa y la cédula queda normalizada.
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", "001-000000-0001a"));

            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_cedula"] = "001-000000-0001A" });

            Assert.Equal(CedulaA, e.Fila(Cliente, id)["sanic_cedula"]);
        }

        [Fact]
        public void PA5A_CLI_08_UpdateDeCorreoYCelular_LosNormalizaYEscribeSoloEsasColumnas()
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_correo"] = "  Ana.Perez@Banco.COM ", ["sanic_celular"] = " 7777-0000 " });

            var fila = e.Fila(Cliente, id);
            Assert.Equal("ana.perez@banco.com", fila["sanic_correo"]);
            Assert.Equal("7777-0000", fila["sanic_celular"]);
            var escritura = Assert.Single(EscriturasDe(e.Almacen, Cliente, OperacionDeDatos.Update));
            Assert.Equal(new[] { "sanic_celular", "sanic_correo" }, escritura.Columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void PA5A_CLI_09_UpdateConNombreCompletoVacio_CARP_CLI_001(string vacio)
        {
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_nombrecompleto"] = vacio }), CodigosDeError.CLI_001);

            Assert.Equal("Ana Pérez", e.Fila(Cliente, id)["sanic_nombre"]);
        }

        [Fact]
        public void PA5A_CLI_10_ElOrdenDeLasReglasDelUpdate_GanaElPrimeroQueRechaza()
        {
            // 2.2: paso 0 (estado) gana sobre todo; paso 0 bis (nombre mostrado) antes del paso 1 (normalizar); paso 1 (nombre) antes del paso 2 (cédula).
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));
            var inactivar = new Func<Entity>(() => new Entity(Cliente, id) { ["statecode"] = new OptionSetValue(Estados.Inactivo) });
            var casos = new List<KeyValuePair<Entity, DefinicionDeError>>
            {
                new KeyValuePair<Entity, DefinicionDeError>(Con(inactivar(), "sanic_nombrecompleto", ""), CodigosDeError.CLI_004),
                new KeyValuePair<Entity, DefinicionDeError>(Con(inactivar(), "sanic_nombre", "Distinto"), CodigosDeError.CLI_004),
                new KeyValuePair<Entity, DefinicionDeError>(Con(Con(inactivar(), "sanic_cedula", ""), "sanic_nombre", "Distinto"), CodigosDeError.CLI_004),
                new KeyValuePair<Entity, DefinicionDeError>(Con(new Entity(Cliente, id), "sanic_nombre", "Distinto", "sanic_nombrecompleto", ""), CodigosDeError.CLI_005),
                new KeyValuePair<Entity, DefinicionDeError>(Con(new Entity(Cliente, id), "sanic_nombre", "Distinto", "sanic_cedula", ""), CodigosDeError.CLI_005),
                new KeyValuePair<Entity, DefinicionDeError>(Con(new Entity(Cliente, id), "sanic_nombrecompleto", "", "sanic_cedula", ""), CodigosDeError.CLI_001)
            };

            foreach (var caso in casos)
            {
                var cambio = caso.Key;
                Rechaza(() => e.ComoOficial.Update(cambio), caso.Value);
            }

            var fila = e.Fila(Cliente, id);
            Assert.Equal("Ana Pérez", fila["sanic_nombre"]);
            Assert.Equal(CedulaA, fila["sanic_cedula"]);
            Assert.Equal(Estados.Activo, Estado(fila, "statecode"));
        }

        private static Entity Con(Entity fila, params object[] paresNombreValor)
        {
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        [Fact]
        public void PA5A_CLI_11_UnUpdateRegistradoSinLaPreImagen_FallaPorRegistroEquivocado()
        {
            // Sección 5, S-16: el Update lleva pre-imagen PreImagen. Sin ella el step no puede saber qué cambió (DC-01: ausente es "desconocido").
            var e = UnEscenario();
            var id = e.Sembrar(UnClienteConocido("Ana Pérez", CedulaA));
            var contexto = Contexto("Update", 20, Cliente, e.Oficial, new Entity(Cliente, id) { ["sanic_celular"] = "8888-1111" });

            var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new ClienteStep(), e.Fabrica, contexto));

            Assert.Contains("ClienteStep", error.Message);
            Assert.Contains("PreImagen", error.Message);
            Assert.DoesNotContain("CARP-CLI-900", error.Message);
        }

        [Theory]
        [InlineData("sanic_carp_tbl_cuota")]
        [InlineData("email")]
        public void PA5A_CLI_12_ClienteStepSoloSeRegistraEnLaTablaDeClientes(string tabla)
        {
            var e = UnEscenario();
            var contexto = Contexto("Create", 20, tabla, e.Oficial, new Entity(tabla, Guid.NewGuid()));

            var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new ClienteStep(), e.Fabrica, contexto));

            Assert.Contains(tabla, error.Message);
            Assert.DoesNotContain("CARP-CLI-900", error.Message);
        }

        [Fact]
        public void PA5A_CLI_13_NingunaTrazaDelStepLlevaDatosDelCliente_NiEnElExitoNiEnElRechazo()
        {
            // 0.5: la traza nunca lleva datos personales ni valores del cliente. Los marcadores se arman una vez y se buscan sin distinguir mayúsculas
            // (el step escribe los valores normalizados: la cédula en mayúscula, el correo en minúscula).
            const string nombre = "Zoilo Marcador Unico";
            const string cedula = "009-111111-1111Q";
            const string correo = "marcador.unico@example.com";
            const string celular = "7777-1234";
            var e = UnEscenario();

            var id = e.ComoOficial.Create(UnAlta(nombre, cedula, correo, celular));
            Rechaza(() => e.ComoOficial.Create(UnAlta(nombre, cedula, correo, celular)), CodigosDeError.CLI_003);
            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_nombrecompleto"] = nombre + " Segundo", ["sanic_celular"] = celular + "9" });
            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["statecode"] = new OptionSetValue(Estados.Inactivo) }), CodigosDeError.CLI_004);

            var lineas = e.Fabrica.Tracing.Lineas;
            Assert.NotEmpty(lineas);
            foreach (var secreto in new[] { nombre, cedula, correo, celular })
                Assert.DoesNotContain(lineas, l => l.IndexOf(secreto, StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.All(lineas, l => Assert.StartsWith("[ClienteStep]", l, StringComparison.Ordinal));
        }
    }
}
