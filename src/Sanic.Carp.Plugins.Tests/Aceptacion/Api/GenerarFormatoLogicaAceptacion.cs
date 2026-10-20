using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Aceptacion.Formato;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// <c>GenerarFormatoLogica</c> (lote 6, vuelta 2, G1; diseno/03 1.1 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"): PA-GEN-01 a PA-GEN-25 y PA6_GEN_01 a _07, _12, _13, _15 a _17. La lógica corre con los
    /// puertos de PRODUCCIÓN sobre el doble en memoria y el <see cref="GeneradorFalso"/>: se mide qué servicio (llamador o SYSTEM) hace cada lectura y cada escritura con <c>Consultas</c> y <c>Escrituras</c>.
    /// <para>
    /// <b>Ya cubiertos fuera de este archivo.</b> PA-GEN-26 (las 27 opciones y la clave <c>moneda</c>) vive en <c>ClavesDelGeneradorAceptacion</c> y <c>MonedaEnElGeneradorAceptacion</c>; PA-GEN-27 de punta a punta, en
    /// <c>GenerarFormatoApiAceptacion</c> (la prueba del generador solo está en <c>SalidaIdenticaAlSpikeAceptacion</c>). PA-GEN-23 y el resto de las fallas de la plataforma van por la API (la conversión a <c>-900</c> es de la base).
    /// </para>
    /// <para>
    /// <b>Qué NO se afirma</b> (el diseño no lo fija): el orden entre <c>CARP-GEN-012</c> y los demás rechazos, ni el <c>CARP-GEN-012</c> de una vista previa; ambos viven en <c>GenerarFormatoApiAceptacion</c> solo para lo que el
    /// diseño dice (antes de la primera escritura).
    /// </para>
    /// </summary>
    public class GenerarFormatoLogicaAceptacion
    {
        private static readonly string[] ColumnasQueCongelan =
        {
            "sanic_fechacongelamiento", "sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo", "sanic_instantaneacelular"
        };

        private static object V(Entity fila, string columna)
        {
            return EscenarioDeGenerarFormato.Valor(fila, columna);
        }

        private static Guid IdDeReferencia(Entity fila, string columna, string tablaEsperada)
        {
            var referencia = Assert.IsType<EntityReference>(V(fila, columna));
            Assert.Equal(tablaEsperada, referencia.LogicalName);
            return referencia.Id;
        }

        // ---- PA-GEN-01 a PA-GEN-05: el camino feliz y quién hace qué ----

        [Fact]
        public void PA_GEN_01_BorradorSinCongelar_GeneraCongelaYEscribeLaBitacora_LasLecturasSonDelLlamadorYLasEscriturasDeSystem()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var oficial = s.E.Oficial;
            var sistema = s.E.Almacen.IdDeSystem;
            // Una fila de mapeo inactiva de la versión y otra, activa, de OTRA versión: ninguna cuenta (statecode 0 y versión del arreglo).
            var otraVersion = ApoyoDeVersion.UnaVersion(s.E, EstadoDeVersionDePlantilla.Vigente, "v99");
            s.E.Sembrar(ConstructorDeDatos.UnMapeo().Con("sanic_versionplantillaid", new EntityReference(EscenarioDeGenerarFormato.TablaVersion, s.VersionId))
                .Con("sanic_celda", "Z98").Con("statecode", new OptionSetValue(Estados.Inactivo)).Con("statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo))));
            s.E.Sembrar(ConstructorDeDatos.UnMapeo().Con("sanic_versionplantillaid", new EntityReference(EscenarioDeGenerarFormato.TablaVersion, otraVersion)).Con("sanic_celda", "Z99"));
            s.E.Almacen.LimpiarRegistros();

            var r = s.Generar();

            Assert.Equal(Convert.ToBase64String(GeneradorFalso.ArchivoPorDefecto), r.Archivo);
            Assert.Equal("F-NIC-0000052-" + s.NombreDelArreglo + ".xlsx", r.NombreDelArchivo);
            Assert.True(r.Congelado);
            Assert.Equal(1, r.NumeroProvisional);
            Assert.Equal(string.Empty, r.Advertencias);

            var arreglo = s.Arreglo();
            Assert.Equal(s.Reloj.AhoraUtc, Assert.IsType<DateTime>(V(arreglo, "sanic_fechacongelamiento")));
            Assert.Equal(s.NombreDelCliente, V(arreglo, "sanic_instantaneanombre"));
            Assert.Equal(s.CedulaDelCliente, V(arreglo, "sanic_instantaneacedula"));
            Assert.Equal(s.CorreoDelCliente, V(arreglo, "sanic_instantaneacorreo"));
            Assert.Equal(s.CelularDelCliente, V(arreglo, "sanic_instantaneacelular"));
            Assert.Equal(1, V(arreglo, "sanic_numeroprovisional"));

            var generacion = Assert.Single(s.Generaciones());
            Assert.Equal(s.ArregloId, IdDeReferencia(generacion, "sanic_arregloid", EscenarioDeGenerarFormato.TablaArreglo));
            Assert.Equal(s.VersionId, IdDeReferencia(generacion, "sanic_versionplantillaid", EscenarioDeGenerarFormato.TablaVersion));
            Assert.Equal(oficial, IdDeReferencia(generacion, "sanic_generadoporid", EscenarioDeGenerarFormato.TablaUsuario));
            Assert.Equal(s.Reloj.AhoraUtc, Assert.IsType<DateTime>(V(generacion, "sanic_fechageneracion")));
            Assert.Equal(s.E.TeamDelArea, IdDeReferencia(generacion, "ownerid", "team"));

            // Las escrituras, en este orden y las dos con SYSTEM; ninguna con el llamador.
            Assert.Collection(s.Escrituras,
                primera =>
                {
                    Assert.Equal(OperacionDeDatos.Update, primera.Operacion);
                    Assert.Equal(EscenarioDeGenerarFormato.TablaArreglo, primera.Tabla);
                    Assert.Equal(s.ArregloId, primera.Id);
                    Assert.Equal(sistema, primera.Identidad);
                    Assert.True(new HashSet<string>(ColumnasQueCongelan.Concat(new[] { "sanic_numeroprovisional" }), StringComparer.Ordinal).SetEquals(primera.ColumnasEnviadas));
                },
                segunda =>
                {
                    Assert.Equal(OperacionDeDatos.Create, segunda.Operacion);
                    Assert.Equal(EscenarioDeGenerarFormato.TablaGeneracion, segunda.Tabla);
                    Assert.Equal(sistema, segunda.Identidad);
                    Assert.True(new HashSet<string>(new[] { "sanic_arregloid", "sanic_versionplantillaid", "sanic_generadoporid", "sanic_fechageneracion", "ownerid" }, StringComparer.Ordinal).SetEquals(segunda.ColumnasEnviadas));
                });

            // Las lecturas: el llamador lee arreglo (por id), cuotas, cliente y su propio nombre, y nada más; SYSTEM no toca cuotas ni usuarios.
            var tablasDelLlamador = s.Consultas.Where(c => c.Identidad == oficial).Select(c => c.Tabla).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            Assert.Equal(new[] { EscenarioDeGenerarFormato.TablaArreglo, EscenarioDeGenerarFormato.TablaCliente, EscenarioDeGenerarFormato.TablaCuota, EscenarioDeGenerarFormato.TablaUsuario }
                .OrderBy(t => t, StringComparer.Ordinal).ToList(), tablasDelLlamador);
            Assert.Contains(s.Consultas, c => c.Identidad == oficial && c.Operacion == OperacionDeDatos.Retrieve && c.Tabla == EscenarioDeGenerarFormato.TablaArreglo && c.Id == s.ArregloId);
            var tablasDeSistema = s.Consultas.Where(c => c.Identidad == sistema).Select(c => c.Tabla).Distinct().ToList();
            Assert.Contains(EscenarioDeGenerarFormato.TablaVersion, tablasDeSistema);
            Assert.Contains(EscenarioDeGenerarFormato.TablaMapeo, tablasDeSistema);
            Assert.Contains(EscenarioDeGenerarFormato.TablaArreglo, tablasDeSistema);   // los números del crédito, de TODOS los oficiales
            Assert.DoesNotContain(EscenarioDeGenerarFormato.TablaCuota, tablasDeSistema);
            Assert.DoesNotContain(EscenarioDeGenerarFormato.TablaUsuario, tablasDeSistema);

            // Lo que recibió el generador.
            Assert.Equal(1, s.Generador.LlamadasAGenerar);
            Assert.Equal(s.Archivo, s.Generador.UltimaPlantilla);
            Assert.Equal(ApoyoDeVersion.HuellaDePartida, s.Generador.UltimaHuellaEsperada);
            Assert.Equal(0, s.Generador.LlamadasACalcularHuella);
            var esperadas = ApoyoDeVersion.MapeoV07().Select(f => f.Hoja + "!" + f.Celda).OrderBy(x => x, StringComparer.Ordinal).ToList();
            Assert.Equal(esperadas, s.Generador.UltimoMapeo.Select(m => m.Hoja + "!" + m.Celda).OrderBy(x => x, StringComparer.Ordinal).ToList());
            Assert.Equal(s.NombreDelCliente, s.Dato("clienteNombre"));
            Assert.Equal(s.CedulaDelCliente, s.Dato("clienteCedula"));
            Assert.Equal(s.NombreDelOficial, s.Dato("oficial"));
            Assert.Equal(1, s.Dato("numeroArreglo"));
        }

        [Fact]
        public void PA_GEN_02_ElProvisionalEsElMaximoMasUnoDeTodosLosArreglosDelCredito_AunLosQueElLlamadorNoLeeYLosInactivos()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            s.OtroArregloDelCredito(1, EstadoDeArreglo.Vigente);
            s.OtroArregloDelCredito(2, EstadoDeArreglo.Terminado);
            var anulado = s.OtroArregloDelCredito(3, EstadoDeArreglo.Anulado);
            s.OtroArregloDelCredito(null, EstadoDeArreglo.Borrador);
            s.OtroArregloDelCredito(null, EstadoDeArreglo.Descartado);
            // Un número más alto en OTRO crédito y en el mismo número de producto pero de OTRO producto: no cuentan.
            s.E.Sembrar(ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Vigente).Con("sanic_numeroarreglo", 9));
            s.E.Sembrar(ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Vigente).Con("sanic_numeroproducto", s.NumeroDeProducto)
                .Con("sanic_producto", new OptionSetValue((int)ProductoCrediticio.Tarjeta)).Con("sanic_numeroarreglo", 8));
            s.E.Almacen.NegarLectura(s.E.Oficial, EscenarioDeGenerarFormato.TablaArreglo, anulado);
            s.E.Almacen.LimpiarRegistros();

            var r = s.Generar();

            Assert.Equal(4, r.NumeroProvisional);
            Assert.Equal(4, V(s.Arreglo(), "sanic_numeroprovisional"));
            Assert.Equal(4, s.Dato("numeroArreglo"));
        }

        [Fact]
        public void PA_GEN_03_BorradorYaCongelado_ImprimeLaInstantaneaNoElClienteNuevo_NoEscribeElArregloYAgregaOtraFilaDeBitacora()
        {
            var s = EscenarioDeGenerarFormato.Armar(congelado: true, ajustarArreglo: a => a.Con("sanic_numeroprovisional", 2));
            s.Modificar(EscenarioDeGenerarFormato.TablaCliente, s.ClienteId, "sanic_nombrecompleto", "Nuevo-" + s.Marca, "sanic_cedula", "CedNueva-" + s.Marca,
                "sanic_correo", "nuevo-" + s.Marca + "@example.com", "sanic_celular", "celNuevo-" + s.Marca);
            var congeladoAntes = V(s.Arreglo(), "sanic_fechacongelamiento");

            var r = s.Generar();

            Assert.Empty(s.EscriturasDe(EscenarioDeGenerarFormato.TablaArreglo, OperacionDeDatos.Update));
            Assert.Equal(congeladoAntes, V(s.Arreglo(), "sanic_fechacongelamiento"));
            Assert.Equal("Snap-" + s.NombreDelCliente, s.Dato("clienteNombre"));
            Assert.Equal("Snap-" + s.CedulaDelCliente, s.Dato("clienteCedula"));
            Assert.Equal("Snap-" + s.CorreoDelCliente, s.Dato("clienteCorreo"));
            Assert.Equal("Snap-" + s.CelularDelCliente, s.Dato("clienteCelular"));
            Assert.Equal(2, r.NumeroProvisional);
            Assert.True(r.Congelado);
            Assert.Single(s.Generaciones());
            Assert.DoesNotContain(s.Consultas, c => c.Tabla == EscenarioDeGenerarFormato.TablaCliente);   // un arreglo congelado no mira al Cliente

            s.Generar();

            Assert.Equal(2, s.Generaciones().Count);
        }

        [Fact]
        public void PA_GEN_04_VigenteCongelado_ImprimeSuNumeroDeArregloNoEscribeElArregloEscribeLaBitacoraYElProvisionalEsCero()
        {
            var s = EscenarioDeGenerarFormato.Armar(EstadoDeArreglo.Vigente, congelado: true, ajustarArreglo: a => a.Con("sanic_numeroarreglo", 3).Sin("sanic_numeroprovisional"));

            var r = s.Generar();

            Assert.Empty(s.EscriturasDe(EscenarioDeGenerarFormato.TablaArreglo, OperacionDeDatos.Update));
            Assert.Equal(3, s.Dato("numeroArreglo"));
            Assert.Equal(0, r.NumeroProvisional);
            Assert.True(r.Congelado);
            Assert.Single(s.Generaciones());
        }

        [Fact]
        public void PA_GEN_05_VigenteReabierto_SeCongelaConLaInstantaneaActualDelClienteYNoEscribeProvisional()
        {
            var s = EscenarioDeGenerarFormato.Armar(EstadoDeArreglo.Vigente);

            var r = s.Generar();

            var actualizacion = Assert.Single(s.EscriturasDe(EscenarioDeGenerarFormato.TablaArreglo, OperacionDeDatos.Update));
            Assert.True(new HashSet<string>(ColumnasQueCongelan, StringComparer.Ordinal).SetEquals(actualizacion.ColumnasEnviadas));
            var arreglo = s.Arreglo();
            Assert.Equal(s.NombreDelCliente, V(arreglo, "sanic_instantaneanombre"));
            Assert.Equal(s.CedulaDelCliente, V(arreglo, "sanic_instantaneacedula"));
            Assert.Null(V(arreglo, "sanic_numeroprovisional"));
            Assert.Equal(0, r.NumeroProvisional);
            Assert.True(r.Congelado);
            Assert.Equal(1, s.Dato("numeroArreglo"));       // el sanic_numeroarreglo de un Vigente
            Assert.Single(s.Generaciones());
        }

        // ---- PA-GEN-06 a PA-GEN-12: los rechazos de la entrada, el calendario y la versión ----

        [Fact]
        public void PA_GEN_06_SinArregloId_ElRechazoEsGen001_NoTocaNingunPuertoYNoEscribe()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var logica = s.Logica();

            EscenarioDeGenerarFormato.Rechaza(() => logica.Ejecutar(new PedidoDeGenerarFormato(null, null, s.E.Oficial)), CodigosDeError.GEN_001);

            Assert.Empty(s.Consultas);
            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        [Fact]
        public void PA_GEN_07_UnArregloQueNoExiste_ElRechazoEsGen002()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var inexistente = Guid.NewGuid();

            EscenarioDeGenerarFormato.Rechaza(() => s.Logica().Ejecutar(new PedidoDeGenerarFormato(inexistente, null, s.E.Oficial)), CodigosDeError.GEN_002);

            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        [Fact]
        public void PA_GEN_08_UnArregloQueElLlamadorNoPuedeLeer_DaElMismoGen002QueUnoInexistente()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            s.E.Almacen.NegarLectura(s.E.Oficial, EscenarioDeGenerarFormato.TablaArreglo, s.ArregloId);
            var inexistente = Guid.NewGuid();

            var sinAcceso = EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_002);
            var noExiste = EscenarioDeGenerarFormato.Rechaza(() => s.Logica().Ejecutar(new PedidoDeGenerarFormato(inexistente, null, s.E.Oficial)), CodigosDeError.GEN_002);

            Assert.Equal(noExiste.ToString(), sinAcceso.ToString());   // no se revela la existencia
            s.AssertSinEscrituras();
        }

        [Theory]
        [InlineData(5)]
        [InlineData(7)]
        [InlineData(0)]
        public void PA_GEN_09_ElCalendarioNoTieneTantasCuotasComoElPlazo_ElRechazoEsGen005ConElConteoYElPlazo_SinEscrituras(int cuotas)
        {
            var s = EscenarioDeGenerarFormato.Armar(plazo: 6, cuotas: cuotas);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_005, cuotas, 6);

            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        [Theory]
        [InlineData(3, 3)]     // 1,2,3,3,5,6: un número repetido
        [InlineData(5, 7)]     // 1,2,3,4,5,7: un hueco
        public void PA_GEN_10_LasCuotasNoEstanNumeradasDe1AlPlazo_ElRechazoEsGen005(int indiceDeLaCuotaQueCambia, int numeroNuevo)
        {
            var s = EscenarioDeGenerarFormato.Armar(plazo: 6);
            s.Modificar(EscenarioDeGenerarFormato.TablaCuota, s.CuotasIds[indiceDeLaCuotaQueCambia], "sanic_numero", numeroNuevo);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_005, 6, 6);

            s.AssertSinEscrituras();
        }

        [Fact]
        public void PA_GEN_11_ElPlazoSuperaLaCapacidadDeLaPlantilla_ElRechazoEsGen004ConLosDosNumeros_SinEscrituras()
        {
            var s = EscenarioDeGenerarFormato.Armar(plazo: 12, capacidad: 6);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_004, 12, 6);

            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        [Fact]
        public void PA_GEN_12_LaVersionNoTieneArchivo_ElRechazoEsGen003_SinEscrituras()
        {
            var s = EscenarioDeGenerarFormato.Armar(conArchivo: false);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_003);

            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        // ---- PA-GEN-13 y PA-GEN-14: las cuatro excepciones del generador ----

        [Fact]
        public void PA_GEN_13_LaHuellaNoCoincide_ElRechazoEsGen008_ElArregloNoSeCongelaYNoHayBitacora()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            s.Generador.Excepcion = new HuellaNoCoincideException("la huella no coincide");

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_008);

            s.AssertSinEscrituras();
            Assert.Null(V(s.Arreglo(), "sanic_fechacongelamiento"));
            Assert.Empty(s.Generaciones());
        }

        [Theory]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        public void PA_GEN_14_LasOtrasTresExcepcionesDelGeneradorSeTraducenAGen009Gen010YGen011_SinEscrituras(int codigo)
        {
            var s = EscenarioDeGenerarFormato.Armar();
            DefinicionDeError esperado;
            object[] marcadores = new object[0];
            switch (codigo)
            {
                case 9:
                    s.Generador.Excepcion = new PlantillaInvalidaException("no es un libro");
                    esperado = CodigosDeError.GEN_009;
                    break;
                case 10:
                    s.Generador.Excepcion = new MapeoInvalidoException(new[] { "error uno de la celda", "error dos de la celda" });
                    esperado = CodigosDeError.GEN_010;
                    marcadores = new object[] { "error uno de la celda; error dos de la celda" };
                    break;
                default:
                    s.Generador.Excepcion = new DatosInvalidosException("el campo plazo no tiene valor");
                    esperado = CodigosDeError.GEN_011;
                    marcadores = new object[] { "el campo plazo no tiene valor" };
                    break;
            }

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), esperado, marcadores);

            s.AssertSinEscrituras();
            Assert.Null(V(s.Arreglo(), "sanic_fechacongelamiento"));
        }

        // ---- PA-GEN-15 a PA-GEN-18: la vista previa ----

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA_GEN_15_LaVistaPrevia_GeneraConLaVersionPreviaYSusFilasDeMapeo_NoEscribeNadaYElCongeladoEsElEstadoQueYaTenia(bool arregloCongelado)
        {
            var s = EscenarioDeGenerarFormato.Armar(congelado: arregloCongelado);
            var bytesDeLaPrevia = Encoding.UTF8.GetBytes("previa-" + s.Marca);
            var previa = s.UnaVersionDeVistaPrevia(bytesDeLaPrevia, filasDeMapeo: 5, derechos: AccessRights.ReadAccess | AccessRights.WriteAccess);

            var r = s.Generar(previa);

            s.AssertSinEscrituras();
            Assert.Equal(arregloCongelado, r.Congelado);
            Assert.Equal(arregloCongelado, V(s.Arreglo(), "sanic_fechacongelamiento") != null);
            Assert.Equal(Convert.ToBase64String(GeneradorFalso.ArchivoPorDefecto), r.Archivo);
            Assert.Equal(1, s.Generador.LlamadasAGenerar);
            Assert.Equal(bytesDeLaPrevia, s.Generador.UltimaPlantilla);                       // el archivo de la versión previa, no el de la versión del arreglo
            Assert.Equal(5, s.Generador.UltimoMapeo.Count);                                    // y sus filas de mapeo, no las 27 de la versión del arreglo
            Assert.Equal(Plantillas.Sha256(bytesDeLaPrevia), s.Generador.UltimaHuellaEsperada); // el borrador puede no tener huella: se pasa la calculada del propio archivo
            Assert.Equal(1, s.Generador.LlamadasACalcularHuella);
            Assert.Empty(s.Generaciones());
        }

        [Fact]
        public void PA_GEN_16_LaVersionPreviaNoEstaEnBorrador_ElRechazoEsGen006_AunConElPermisoDeEscritura()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var vigente = ApoyoDeVersion.UnaVersion(s.E, EstadoDeVersionDePlantilla.Vigente, "v09", 60, Encoding.UTF8.GetBytes("otra-" + s.Marca));
            s.E.Almacen.ConfigurarAcceso(s.E.Oficial, EscenarioDeGenerarFormato.TablaVersion, vigente, AccessRights.WriteAccess);
            s.E.Almacen.LimpiarRegistros();

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(vigente), CodigosDeError.GEN_006);

            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, (int)AccessRights.ReadAccess)]
        [InlineData(true, (int)(AccessRights.ReadAccess | AccessRights.DeleteAccess | AccessRights.ShareAccess))]
        public void PA_GEN_17_ElLlamadorNoTieneEscrituraSobreLaVersionPrevia_ElRechazoEsGen007(bool configurarAcceso, int derechos)
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var previa = s.UnaVersionDeVistaPrevia(Encoding.UTF8.GetBytes("previa-" + s.Marca), derechos: configurarAcceso ? (AccessRights?)(AccessRights)derechos : null);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(previa), CodigosDeError.GEN_007);

            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        [Fact]
        public void PA_GEN_17b_LaEscrituraDeOtroUsuarioNoValeParaElLlamador()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var previa = s.UnaVersionDeVistaPrevia(Encoding.UTF8.GetBytes("previa-" + s.Marca), derechos: null);
            s.E.Almacen.ConfigurarAcceso(s.E.OtroOficial, EscenarioDeGenerarFormato.TablaVersion, previa, AccessRights.WriteAccess);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(previa), CodigosDeError.GEN_007);
        }

        [Fact]
        public void PA_GEN_18_LaVistaPreviaDeUnBorradorSinCongelar_ImprimeElMaximoMasUnoSinGuardarlo()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            s.OtroArregloDelCredito(1, EstadoDeArreglo.Vigente);
            s.OtroArregloDelCredito(2, EstadoDeArreglo.Terminado);
            var previa = s.UnaVersionDeVistaPrevia(Encoding.UTF8.GetBytes("previa-" + s.Marca));

            var r = s.Generar(previa);

            Assert.Equal(3, r.NumeroProvisional);
            Assert.Equal(3, s.Dato("numeroArreglo"));
            Assert.Null(V(s.Arreglo(), "sanic_numeroprovisional"));
            s.AssertSinEscrituras();
        }

        // ---- PA-GEN-19 a PA-GEN-22: los datos que recibe el generador ----

        [Theory]
        [InlineData((int)Moneda.Dolares, (int)TipoDeArreglo.Cancelacion, "Dólares", "Cancelación")]
        [InlineData((int)Moneda.Cordobas, (int)TipoDeArreglo.Temporal, "Córdobas", "Temporal")]
        public void PA_GEN_19_LaMonedaYElTipoLlevanElTextoFormalConOrtografia(int moneda, int tipo, string monedaEsperada, string tipoEsperado)
        {
            var s = EscenarioDeGenerarFormato.Armar(ajustarArreglo: a => a.Con("sanic_moneda", new OptionSetValue(moneda)).Con("sanic_tipo", new OptionSetValue(tipo)));

            s.Generar();

            Assert.Equal(monedaEsperada, s.Dato("moneda"));
            Assert.Equal(tipoEsperado, s.Dato("tipo"));
        }

        [Fact]
        public void PA_GEN_20_UnClienteSinCorreoNiCelularYSinComentarios_LlevaLasClavesConValorNuloYNoAusentes()
        {
            var s = EscenarioDeGenerarFormato.Armar(ajustarCliente: c => c.Sin("sanic_correo").Sin("sanic_celular"), ajustarArreglo: a => a.Sin("sanic_comentarios"));

            s.Generar();

            Assert.Null(s.Dato("clienteCorreo"));
            Assert.Null(s.Dato("clienteCelular"));
            Assert.Null(s.Dato("comentarios"));
            Assert.True(s.Generador.UltimosDatos.Valores.ContainsKey("clienteCorreo"));
            Assert.True(s.Generador.UltimosDatos.Valores.ContainsKey("clienteCelular"));
            Assert.True(s.Generador.UltimosDatos.Valores.ContainsKey("comentarios"));
            // El arreglo se congela con instantáneas vacías para esos dos datos: no se escribe un texto en blanco.
            Assert.Null(V(s.Arreglo(), "sanic_instantaneacorreo"));
            Assert.Null(V(s.Arreglo(), "sanic_instantaneacelular"));
        }

        [Fact]
        public void PA_GEN_21_LaFechaDeInicioGuardadaLlegaAlGeneradorSinCambiarDeZona()
        {
            var guardada = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Unspecified);
            var s = EscenarioDeGenerarFormato.Armar(ajustarArreglo: a => a.Con("sanic_fechainicio", guardada));

            s.Generar();

            var recibida = Assert.IsType<DateTime>(s.Dato("fechaInicio"));
            Assert.Equal(new DateTime(2026, 11, 5), recibida);
            Assert.Equal(DateTimeKind.Unspecified, recibida.Kind);
        }

        [Fact]
        public void PA_GEN_22_UnArregloTerminado_SeGeneraSinCongelarNiTocarElArreglo_ConBitacoraYSinProvisional()
        {
            var s = EscenarioDeGenerarFormato.Armar(EstadoDeArreglo.Terminado);

            var r = s.Generar();

            Assert.Empty(s.EscriturasDe(EscenarioDeGenerarFormato.TablaArreglo, OperacionDeDatos.Update));
            Assert.Null(V(s.Arreglo(), "sanic_fechacongelamiento"));
            Assert.Single(s.Generaciones());
            Assert.False(r.Congelado);
            Assert.Equal(0, r.NumeroProvisional);
            Assert.Equal(1, s.Dato("numeroArreglo"));
        }

        // ---- PA-GEN-24 y PA-GEN-25 ----

        [Fact]
        public void PA_GEN_24_LasAdvertenciasDelGeneradorSalenSeparadasPorSaltoDeLinea()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            s.Generador.Advertencias = new[] { "primera advertencia", "segunda advertencia" };

            var r = s.Generar();

            Assert.Equal("primera advertencia\nsegunda advertencia", r.Advertencias);
        }

        [Fact]
        public void PA_GEN_25_NingunaLineaDeTrazaLlevaLaCedulaLaCuentaElNombreNiElCorreo_NiCuandoSeRechaza()
        {
            var s = EscenarioDeGenerarFormato.Armar(congelado: false);
            s.Generar();
            var rechazado = EscenarioDeGenerarFormato.Armar(congelado: true);
            rechazado.Generador.Excepcion = new DatosInvalidosException("detalle con " + rechazado.Marca);
            EscenarioDeGenerarFormato.Rechaza(() => rechazado.Generar(), CodigosDeError.GEN_011, "detalle con " + rechazado.Marca);

            Assert.DoesNotContain(s.Trazas, t => t.Contains(s.Marca));
            Assert.DoesNotContain(rechazado.Trazas, t => t.Contains(rechazado.Marca));   // se traza el TIPO de la causa, nunca su mensaje
        }

        // ---- PA6_GEN: lo que el diseño de la vuelta 1 fijó ----

        [Fact]
        public void PA6_GEN_01_LaVistaPreviaNoConsultaLaConfiguracionDelArea_LaGeneracionNormalSiLeeElTeam()
        {
            var tablasDeConfiguracion = new[] { "team", "businessunit", "environmentvariabledefinition", "environmentvariablevalue", "queue" };
            var normal = EscenarioDeGenerarFormato.Armar();
            normal.Generar();
            Assert.Contains(normal.Consultas, c => tablasDeConfiguracion.Contains(c.Tabla));

            var vista = EscenarioDeGenerarFormato.Armar();
            var previa = vista.UnaVersionDeVistaPrevia(Encoding.UTF8.GetBytes("previa-" + vista.Marca));
            vista.Generar(previa);

            Assert.DoesNotContain(vista.Consultas, c => tablasDeConfiguracion.Contains(c.Tabla));
        }

        [Fact]
        public void PA6_GEN_02_ConUnaVersionPreviaVigenteYSinPermiso_ElRechazoEsGen006_ElEstadoSeMiraAntesQueElPermiso()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var vigente = ApoyoDeVersion.UnaVersion(s.E, EstadoDeVersionDePlantilla.Vigente, "v09", 60, Encoding.UTF8.GetBytes("otra-" + s.Marca));
            s.E.Almacen.LimpiarRegistros();

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(vigente), CodigosDeError.GEN_006);
        }

        [Fact]
        public void PA6_GEN_03_UnaVersionPreviaQueNoExiste_DaGen007_ElMismoMensajeQueSinPermiso()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var inexistente = Guid.NewGuid();

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(inexistente), CodigosDeError.GEN_007);

            s.AssertSinEscrituras();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA6_GEN_04_UnClienteQueElLlamadorNoPuedeLeerOUnArregloSinCliente_DaGen002_SinCongelarUnaInstantaneaEnBlanco(bool sinClienteEnElArreglo)
        {
            var s = EscenarioDeGenerarFormato.Armar(ajustarArreglo: a => { if (sinClienteEnElArreglo) a.Sin("sanic_clienteid"); });
            if (!sinClienteEnElArreglo) s.E.Almacen.NegarLectura(s.E.Oficial, EscenarioDeGenerarFormato.TablaCliente, s.ClienteId);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_002);

            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        [Fact]
        public void PA6_GEN_04b_UnArregloCongeladoNoNecesitaAlCliente_AunqueElLlamadorNoPuedaLeerlo()
        {
            var s = EscenarioDeGenerarFormato.Armar(congelado: true);
            s.E.Almacen.NegarLectura(s.E.Oficial, EscenarioDeGenerarFormato.TablaCliente, s.ClienteId);

            var r = s.Generar();

            Assert.True(r.Congelado);
            Assert.Equal("Snap-" + s.NombreDelCliente, s.Dato("clienteNombre"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA6_GEN_05_UnArregloSinVersionOConUnaQueNoSePuedeLeer_DaGen003(bool versionInexistente)
        {
            var inexistente = Guid.NewGuid();
            var s = EscenarioDeGenerarFormato.Armar(conVersion: false, ajustarArreglo: a =>
            {
                if (versionInexistente) a.Con("sanic_versionplantillaid", new EntityReference(EscenarioDeGenerarFormato.TablaVersion, inexistente));
            });

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_003);

            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        [Fact]
        public void PA6_GEN_06_UnaVersionNormalSinHuella_PasaAlGeneradorLaHuellaVaciaYNoNula()
        {
            var s = EscenarioDeGenerarFormato.Armar(conHuella: false);

            s.Generar();

            Assert.NotNull(s.Generador.UltimaHuellaEsperada);
            Assert.Equal(string.Empty, s.Generador.UltimaHuellaEsperada);     // no coincide con ninguna: el generador real da GEN-008 (ver GenerarFormatoApiAceptacion)
            Assert.Equal(0, s.Generador.LlamadasACalcularHuella);
        }

        [Fact]
        public void PA6_GEN_07_UnBorradorCongeladoSinProvisionalGuardado_ImprimeElCalculadoYNoLoGuarda()
        {
            var s = EscenarioDeGenerarFormato.Armar(congelado: true, ajustarArreglo: a => a.Sin("sanic_numeroprovisional"));
            s.OtroArregloDelCredito(1, EstadoDeArreglo.Vigente);
            s.OtroArregloDelCredito(2, EstadoDeArreglo.Terminado);

            var r = s.Generar();

            Assert.Equal(3, r.NumeroProvisional);
            Assert.Equal(3, s.Dato("numeroArreglo"));
            Assert.Empty(s.EscriturasDe(EscenarioDeGenerarFormato.TablaArreglo, OperacionDeDatos.Update));
            Assert.Null(V(s.Arreglo(), "sanic_numeroprovisional"));
            Assert.Single(s.Generaciones());
        }

        [Fact]
        public void PA6_GEN_12_UnVigenteReabiertoConUnProvisionalViejo_NoLoImprimeNiLoReescribeNiLoLimpia()
        {
            var s = EscenarioDeGenerarFormato.Armar(EstadoDeArreglo.Vigente, ajustarArreglo: a => a.Con("sanic_numeroprovisional", 7));

            var r = s.Generar();

            var actualizacion = Assert.Single(s.EscriturasDe(EscenarioDeGenerarFormato.TablaArreglo, OperacionDeDatos.Update));
            Assert.DoesNotContain("sanic_numeroprovisional", actualizacion.ColumnasEnviadas);
            Assert.Equal(7, V(s.Arreglo(), "sanic_numeroprovisional"));
            Assert.Equal(0, r.NumeroProvisional);
            Assert.Equal(1, s.Dato("numeroArreglo"));
        }

        [Fact]
        public void PA6_GEN_13_SiElSiguienteDelCreditoPasaDe9999_ElRechazoEsArr023_AntesDeGenerarYDeEscribir()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            s.OtroArregloDelCredito(9999, EstadoDeArreglo.Terminado);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.ARR_023);

            s.AssertSinEscrituras();
            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        [Fact]
        public void PA6_GEN_13b_UnBorradorConProvisionalGuardadoNoNecesitaElSiguiente_AunqueElCreditoEsteEn9999()
        {
            var s = EscenarioDeGenerarFormato.Armar(congelado: true, ajustarArreglo: a => a.Con("sanic_numeroprovisional", 5));
            s.OtroArregloDelCredito(9999, EstadoDeArreglo.Terminado);

            var r = s.Generar();

            Assert.Equal(5, r.NumeroProvisional);
        }

        // ---- PA6_GEN_15: el orden de los rechazos (el primero gana) ----

        [Fact]
        public void PA6_GEN_15a_ElCalendarioIncompletoSeRechazaAntesQueUnClienteIlegible()
        {
            var s = EscenarioDeGenerarFormato.Armar(plazo: 6, cuotas: 5);
            s.E.Almacen.NegarLectura(s.E.Oficial, EscenarioDeGenerarFormato.TablaCliente, s.ClienteId);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_005, 5, 6);
        }

        [Fact]
        public void PA6_GEN_15b_ElClienteIlegibleSeRechazaAntesQueLaFaltaDeVersion()
        {
            var s = EscenarioDeGenerarFormato.Armar(conVersion: false);
            s.E.Almacen.NegarLectura(s.E.Oficial, EscenarioDeGenerarFormato.TablaCliente, s.ClienteId);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_002);
        }

        [Fact]
        public void PA6_GEN_15c_LaFaltaDeArchivoSeRechazaAntesQueElPlazoMayorQueLaCapacidad()
        {
            var s = EscenarioDeGenerarFormato.Armar(plazo: 12, capacidad: 6, conArchivo: false);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_003);
        }

        [Fact]
        public void PA6_GEN_15d_ElPlazoMayorQueLaCapacidadSeRechazaAntesQueElCreditoLleno()
        {
            var s = EscenarioDeGenerarFormato.Armar(plazo: 12, capacidad: 6);
            s.OtroArregloDelCredito(9999, EstadoDeArreglo.Terminado);

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.GEN_004, 12, 6);
        }

        [Fact]
        public void PA6_GEN_15e_ElCreditoLlenoSeRechazaAntesQueLaExcepcionDelGenerador_YElGeneradorNoLlegaAUsarse()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            s.OtroArregloDelCredito(9999, EstadoDeArreglo.Terminado);
            s.Generador.Excepcion = new HuellaNoCoincideException("no coincide");

            EscenarioDeGenerarFormato.Rechaza(() => s.Generar(), CodigosDeError.ARR_023);

            Assert.Equal(0, s.Generador.LlamadasAGenerar);
        }

        // ---- PA6_GEN_16: los argumentos ----

        [Fact]
        public void PA6_GEN_16_ElConstructorRechazaCadaArgumentoNuloMenosLaTraza_YNoConsultaNadaAlConstruirse()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var llamador = s.E.ComoOficial;
            var sistema = s.E.ComoSistema;
            IArreglos arreglos = new ArreglosDataverse(llamador);
            ICuotas cuotas = new CuotasDataverse(llamador);
            IClientes clientes = new ClientesDataverse(llamador, sistema);
            IUsuarios usuarios = new UsuariosDataverse(llamador);
            IConsecutivos consecutivos = new ConsecutivosDataverse(sistema);
            IVersiones versiones = new VersionesDataverse(sistema);
            ILecturasDeVersion lecturas = new LecturasDeVersionDataverse(sistema);
            IAccesosDeFila accesos = new AccesosDeFilaDataverse(llamador);
            IGeneradorDeFormato generador = s.Generador;
            IConfiguracionDelArea configuracion = new ConfiguracionDelArea(sistema);
            IReloj reloj = s.Reloj;
            Action<string> trazar = s.Trazas.Add;

            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(null, arreglos, cuotas, clientes, usuarios, consecutivos, versiones, lecturas, accesos, generador, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, null, cuotas, clientes, usuarios, consecutivos, versiones, lecturas, accesos, generador, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, null, clientes, usuarios, consecutivos, versiones, lecturas, accesos, generador, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, cuotas, null, usuarios, consecutivos, versiones, lecturas, accesos, generador, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, cuotas, clientes, null, consecutivos, versiones, lecturas, accesos, generador, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, cuotas, clientes, usuarios, null, versiones, lecturas, accesos, generador, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, cuotas, clientes, usuarios, consecutivos, null, lecturas, accesos, generador, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, cuotas, clientes, usuarios, consecutivos, versiones, null, accesos, generador, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, cuotas, clientes, usuarios, consecutivos, versiones, lecturas, null, generador, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, cuotas, clientes, usuarios, consecutivos, versiones, lecturas, accesos, null, configuracion, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, cuotas, clientes, usuarios, consecutivos, versiones, lecturas, accesos, generador, null, reloj, trazar));
            Assert.Throws<ArgumentNullException>(() => new GenerarFormatoLogica(arreglos, arreglos, cuotas, clientes, usuarios, consecutivos, versiones, lecturas, accesos, generador, configuracion, null, trazar));

            var sinTraza = new GenerarFormatoLogica(arreglos, arreglos, cuotas, clientes, usuarios, consecutivos, versiones, lecturas, accesos, generador, configuracion, reloj, null);
            Assert.Empty(s.Consultas);          // construir no consulta nada
            Assert.NotNull(sinTraza.Ejecutar(s.Pedido()));      // y la traza nula se acepta
        }

        [Fact]
        public void PA6_GEN_17_UnPedidoNuloLanzaArgumentNullException()
        {
            var s = EscenarioDeGenerarFormato.Armar();
            var logica = s.Logica();

            Assert.Throws<ArgumentNullException>(() => logica.Ejecutar(null));
        }
    }
}
