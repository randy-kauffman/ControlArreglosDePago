using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Tests.Aceptacion.Formato;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// El plugin <see cref="GenerarFormatoApi"/> de punta a punta (lote 6, vuelta 2, G1; diseno/03 1.1): lanzado como la plataforma (<c>EjecutarComoPlataforma</c>: transacción, contexto, servicios por identidad), con el
    /// generador REAL y la plantilla v07. Cubre PA-GEN-23, 25 y 27, PA6_GEN_06 a _11, _13 y _14 a _20 y el <c>CARP-GEN-012</c> (decisión 8(c) del orquestador).
    /// <para>
    /// <b>Un <c>-900</c> esperado no alcanza como prueba.</b> La base de las API convierte CUALQUIER excepción en el <c>-900</c> con el texto fijo del catálogo, también el <c>NotImplementedException</c> de un stub. Por eso cada prueba
    /// de <c>-900</c> afirma además que el código LLEGÓ hasta la falla (las lecturas, o la primera escritura, quedaron registradas) y que nada quedó escrito.
    /// </para>
    /// </summary>
    public class GenerarFormatoApiAceptacion
    {
        private static readonly int ErrorInesperado = unchecked((int)0x80040216);
        private static readonly int RechazoDeUnStep = unchecked((int)0x80040265);

        private static object V(Entity fila, string columna)
        {
            return EscenarioDeGenerarFormato.Valor(fila, columna);
        }

        // ---- PA-GEN-27 (real), de punta a punta ----

        private static ushort U16(byte[] b, int posicion)
        {
            return (ushort)(b[posicion] | (b[posicion + 1] << 8));
        }

        private static uint U32(byte[] b, int posicion)
        {
            return (uint)(b[posicion] | (b[posicion + 1] << 8) | (b[posicion + 2] << 16) | (b[posicion + 3] << 24));
        }

        /// <summary>Copia del ZIP con la fecha y la hora DOS de cada entrada (encabezado local y directorio central) en cero: lo único que el SDK de Open XML estampa con la hora de la generación.</summary>
        private static byte[] SinSellosDeTiempo(byte[] zip)
        {
            var copia = (byte[])zip.Clone();
            int fin = -1;
            for (int i = zip.Length - 22; i >= 0 && fin < 0; i--)
            {
                if (U32(zip, i) == 0x06054b50u) fin = i;
            }
            Assert.True(fin >= 0, "No se encontró el registro de fin de directorio central del ZIP.");
            int entradas = U16(zip, fin + 10);
            int posicion = (int)U32(zip, fin + 16);
            for (int n = 0; n < entradas; n++)
            {
                Assert.Equal(0x02014b50u, U32(zip, posicion));
                Array.Clear(copia, posicion + 12, 4);
                int local = (int)U32(zip, posicion + 42);
                Assert.Equal(0x04034b50u, U32(zip, local));
                Array.Clear(copia, local + 10, 4);
                posicion += 46 + U16(zip, posicion + 28) + U16(zip, posicion + 30) + U16(zip, posicion + 32);
            }
            return copia;
        }

        private static List<KeyValuePair<string, byte[]>> Componentes(byte[] zip)
        {
            var lista = new List<KeyValuePair<string, byte[]>>();
            using (var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
            {
                foreach (var entrada in archivo.Entries)
                {
                    using (var lectura = entrada.Open())
                    using (var memoria = new MemoryStream())
                    {
                        lectura.CopyTo(memoria);
                        lista.Add(new KeyValuePair<string, byte[]>(entrada.FullName, memoria.ToArray()));
                    }
                }
            }
            return lista;
        }

        private static List<string> TextosCompartidos(List<KeyValuePair<string, byte[]>> componentes)
        {
            var datos = componentes.Single(c => c.Key == "xl/sharedStrings.xml").Value;
            var documento = System.Xml.Linq.XDocument.Load(new MemoryStream(datos));
            return documento.Root.Elements().Select(si => si.Value).ToList();
        }

        /// <summary>La hoja con cada celda de cadena compartida reemplazada por su texto: independiente del orden de la tabla de cadenas.</summary>
        private static string HojaResuelta(byte[] xml, List<string> textos)
        {
            var documento = System.Xml.Linq.XDocument.Load(new MemoryStream(xml));
            foreach (var celda in documento.Descendants().Where(e => e.Name.LocalName == "c" && (string)e.Attribute("t") == "s").ToList())
            {
                var valor = celda.Elements().Single(e => e.Name.LocalName == "v");
                celda.SetAttributeValue("t", "resuelta");
                valor.Value = textos[int.Parse(valor.Value, System.Globalization.CultureInfo.InvariantCulture)];
            }
            return documento.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// El arreglo de <c>tools/Medicion/Program.cs</c> (spike 1b) sembrado en el almacén: Vigente congelado con la instantánea del cliente ficticio y el número 3, las cuotas del spike (con
        /// las 12 primeras pagadas, la 12 a medias, si <paramref name="conPagos"/>) y el oficial "Oficial de Prueba".
        /// </summary>
        private static EscenarioDeGenerarFormato ElDelSpike(int plazo, bool conPagos)
        {
            var inicio = new DateTime(2026, 11, 8);
            decimal cuota = plazo == 60 ? 2150.35m : 4200.10m;
            decimal total = cuota * plazo;
            var oficialDelSpike = Guid.NewGuid();
            var s = EscenarioDeGenerarFormato.Armar(EstadoDeArreglo.Vigente, congelado: true, plazo: plazo, cuotas: 0, real: true, ajustarArreglo: a => a
                .Con("sanic_oficialid", new EntityReference(EscenarioDeGenerarFormato.TablaUsuario, oficialDelSpike))
                .Con("sanic_numeroarreglo", 3).Sin("sanic_numeroprovisional")
                .Con("sanic_instantaneanombre", "Ana Lucia Martinez Ruiz (ficticio)").Con("sanic_instantaneacedula", "000-000000-0000X")
                .Con("sanic_instantaneacorreo", "cliente.ficticio@ejemplo.com").Con("sanic_instantaneacelular", "0000-0000")
                .Con("sanic_numeroproducto", "0000-0000-0000-0000")
                .Con("sanic_saldoinicial", 125000.75m).Con("sanic_montoabonado", 5000m).Con("sanic_saldoremanente", 120000.75m)
                .Con("sanic_cuotamensual", cuota).Con("sanic_totalpagar", total)
                .Con("sanic_fechainicio", inicio).Con("sanic_fechafin", inicio.AddMonths(plazo - 1))
                .Con("sanic_fechaformalizacion", new DateTime(2026, 10, 8))
                .Con("sanic_cuentadebitar", "0000000000000000")
                .Con("sanic_comentarios", "Datos ficticios del spike C-01. Plazo de " + plazo + " cuotas."));
            s.E.Sembrar(ConstructorDeDatos.UnUsuario(oficialDelSpike).Con("fullname", "Oficial de Prueba"));
            for (int n = 1; n <= plazo; n++)
            {
                var fecha = inicio.AddMonths(n - 1);
                bool pagada = conPagos && n <= 12;
                var fila = ConstructorDeDatos.UnaCuota()
                    .Con("sanic_arregloid", new EntityReference(EscenarioDeGenerarFormato.TablaArreglo, s.ArregloId))
                    .Con("sanic_numero", n)
                    .Con("sanic_nombre", s.NombreDelArreglo + "-C" + n.ToString("D2", System.Globalization.CultureInfo.InvariantCulture))
                    .Con("sanic_fechavencimiento", fecha)
                    .Con("sanic_monto", cuota);
                if (pagada) fila.Con("sanic_fechapago", fecha.AddDays(1)).Con("sanic_montopagado", n == 12 ? cuota / 2 : cuota);
                s.E.Sembrar(fila);
            }
            s.E.Almacen.LimpiarRegistros();
            return s;
        }

        [Theory]
        [InlineData(6, "salida-ejemplo-6-cuotas.xlsx", false)]
        [InlineData(60, "salida-ejemplo-60-cuotas.xlsx", true)]
        public void PA_GEN_27_ElArregloSembradoGeneradoPorLaApiConElGeneradorReal_SaleIgualAlSpike1b(int plazo, string salidaDelSpike, bool conPagos)
        {
            var s = ElDelSpike(plazo, conPagos);

            var contexto = s.Llamar();

            var obtenida = Convert.FromBase64String(Assert.IsType<string>(contexto.OutputParameters["archivo"]));
            var esperada = System.IO.File.ReadAllBytes(Path.Combine(System.AppContext.BaseDirectory, "Fixtures", "Spike1b", salidaDelSpike));
            // OJO (hallazgo): el orden de las filas de mapeo que devuelve la plataforma NO está definido (4.2 punto 6), y el orden en que el generador escribe las celdas decide el ORDEN de la tabla de
            // cadenas compartidas. Por eso aquí la identidad con el spike es SEMÁNTICA para las hojas y la tabla de cadenas (cada celda con su texto resuelto, las mismas cadenas) y BYTE A BYTE para los demás
            // componentes. La identidad byte a byte del archivo entero (ya fijada para el generador solo en SalidaIdenticaAlSpikeAceptacion) exigiría que la lógica ordene el mapeo de forma canónica: ver "Para el orquestador".
            var esperados = Componentes(esperada);
            var obtenidos = Componentes(obtenida);
            Assert.Equal(esperados.Select(c => c.Key).ToArray(), obtenidos.Select(c => c.Key).ToArray());
            var textosEsperados = TextosCompartidos(esperados);
            var textosObtenidos = TextosCompartidos(obtenidos);
            Assert.Equal(textosEsperados.OrderBy(t => t, StringComparer.Ordinal).ToArray(), textosObtenidos.OrderBy(t => t, StringComparer.Ordinal).ToArray());
            for (int i = 0; i < esperados.Count; i++)
            {
                var nombre = esperados[i].Key;
                if (nombre.StartsWith("xl/worksheets/", StringComparison.Ordinal) && nombre.EndsWith(".xml", StringComparison.Ordinal))
                    Assert.True(HojaResuelta(esperados[i].Value, textosEsperados) == HojaResuelta(obtenidos[i].Value, textosObtenidos),
                        "La hoja '" + nombre + "' de la salida de " + plazo + " cuotas, generada por la API desde el almacén, difiere del spike 1b (con las cadenas resueltas).");
                else if (nombre != "xl/sharedStrings.xml")
                    Assert.True(esperados[i].Value.SequenceEqual(obtenidos[i].Value),
                        "El componente '" + nombre + "' de la salida de " + plazo + " cuotas, generada por la API desde el almacén, difiere del spike 1b.");
            }
            Assert.Equal(0, contexto.OutputParameters["numeroprovisional"]);      // Vigente
            Assert.True(Assert.IsType<bool>(contexto.OutputParameters["congelado"]));
            Assert.Empty(s.EscriturasDe(EscenarioDeGenerarFormato.TablaArreglo, OperacionDeDatos.Update));
            Assert.Single(s.Generaciones());
        }

        // ---- Las salidas y las entradas ----

        [Fact]
        public void PA6_GEN_14_LaLlamadaNormalEscribeLasCincoSalidasConSusTipos_ConLosStepsDelPaqueteRegistrados()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true, conSteps: true);

            var contexto = s.Llamar();

            Assert.Equal(5, contexto.OutputParameters.Count);
            var archivo = Convert.FromBase64String(Assert.IsType<string>(contexto.OutputParameters["archivo"]));
            Assert.Equal(new byte[] { (byte)'P', (byte)'K' }, archivo.Take(2).ToArray());    // un .xlsx es un ZIP
            Assert.Equal("F-NIC-0000052-" + s.NombreDelArreglo + ".xlsx", Assert.IsType<string>(contexto.OutputParameters["nombrearchivo"]));
            Assert.Equal(1, Assert.IsType<int>(contexto.OutputParameters["numeroprovisional"]));
            Assert.True(Assert.IsType<bool>(contexto.OutputParameters["congelado"]));
            Assert.Equal(string.Empty, Assert.IsType<string>(contexto.OutputParameters["advertencias"]));
            // Los steps de Arreglo y de Generación aceptan las dos escrituras de SYSTEM (H-19 a): el arreglo queda congelado y hay una fila de bitácora.
            Assert.NotNull(V(s.Arreglo(), "sanic_fechacongelamiento"));
            Assert.Single(s.Generaciones());
        }

        [Fact]
        public void PA6_GEN_15_UnArregloIdVacioEsAusente_ElRechazoEsGen001_YUnaVersionPreviaVaciaEsAusente_LaLlamadaEsNormalYEscribe()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            var sinArreglo = s.ContextoApi(s.ArregloId);
            sinArreglo.InputParameters["arregloid"] = Guid.Empty;

            EscenarioDeGenerarFormato.RechazaLaApi(() => s.EjecutarApi(sinArreglo), CodigosDeError.GEN_001);
            s.AssertSinEscrituras();
            Assert.Empty(sinArreglo.OutputParameters);

            var previaVacia = s.ContextoApi(s.ArregloId);
            previaVacia.InputParameters["versionpreviaid"] = Guid.Empty;
            s.EjecutarApi(previaVacia);

            Assert.True(Assert.IsType<bool>(previaVacia.OutputParameters["congelado"]));
            Assert.NotEmpty(s.Escrituras);                                       // no fue vista previa
        }

        [Fact]
        public void PA6_GEN_16_SinArregloId_ElRechazoEsGen001_YNoSeEscribeNingunaSalida()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            var contexto = s.ContextoApi(null);

            EscenarioDeGenerarFormato.RechazaLaApi(() => s.EjecutarApi(contexto), CodigosDeError.GEN_001);

            Assert.Empty(contexto.OutputParameters);
            s.AssertSinEscrituras();
        }

        [Fact]
        public void PA6_GEN_17_LaVistaPreviaDePuntaAPunta_GeneraConLaPlantillaPreviaYLaHuellaCalculada_SinEscribirNada()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            var previa = s.UnaVersionDeVistaPrevia(Plantillas.V07, derechos: AccessRights.WriteAccess, mapeo: EscenarioDeGenerarFormato.MapeoDelSpike());

            var contexto = s.Llamar(previa);

            Assert.Equal(5, contexto.OutputParameters.Count);
            Assert.False(Assert.IsType<bool>(contexto.OutputParameters["congelado"]));
            Assert.Equal(1, contexto.OutputParameters["numeroprovisional"]);
            s.AssertSinEscrituras();
            Assert.Null(V(s.Arreglo(), "sanic_fechacongelamiento"));
            Assert.Empty(s.Generaciones());
        }

        // VERDE A PROPÓSITO: los miembros declarativos (nombre, privilegio, error transitorio, nombres de parámetros) ya están en la firma de la vuelta 1; la prueba fija que no se pierdan.
        [Fact]
        public void PA6_GEN_18_LaApiDeclaraSuNombreSuPrivilegioSuErrorTransitorioYSusParametros()
        {
            var api = new GenerarFormatoApi();

            Assert.Equal("sanic_carp_capi_generarformato", api.NombreDeLaApi);
            Assert.Equal("prvReadsanic_carp_tbl_generacionformato", api.PrivilegioDeEjecucion);
            Assert.Same(CodigosDeError.GEN_900, api.ErrorTransitorio);
            Assert.Equal("arregloid", GenerarFormatoApi.ParametroArregloId);
            Assert.Equal("versionpreviaid", GenerarFormatoApi.ParametroVersionPreviaId);
            Assert.Equal("archivo", GenerarFormatoApi.SalidaArchivo);
            Assert.Equal("nombrearchivo", GenerarFormatoApi.SalidaNombreArchivo);
            Assert.Equal("numeroprovisional", GenerarFormatoApi.SalidaNumeroProvisional);
            Assert.Equal("congelado", GenerarFormatoApi.SalidaCongelado);
            Assert.Equal("advertencias", GenerarFormatoApi.SalidaAdvertencias);
        }

        // ---- PA-GEN-13 de punta a punta y PA6_GEN_06: la huella ----

        [Fact]
        public void PA6_GEN_06_UnaVersionVigenteSinHuella_DaGen008_ConElGeneradorReal_SinEscribirNada()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true, conHuella: false);
            var contexto = s.ContextoApi(s.ArregloId);

            EscenarioDeGenerarFormato.RechazaLaApi(() => s.EjecutarApi(contexto), CodigosDeError.GEN_008);

            s.AssertSinEscrituras();
            Assert.Empty(contexto.OutputParameters);
            Assert.Null(V(s.Arreglo(), "sanic_fechacongelamiento"));
        }

        [Fact]
        public void PA6_GEN_06b_UnaPlantillaAlteradaDespuesDePublicar_DaGen008_ConElGeneradorReal()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            s.Modificar(EscenarioDeGenerarFormato.TablaVersion, s.VersionId, "sanic_huella", Plantillas.Sha256(new byte[] { 1, 2, 3 }));

            EscenarioDeGenerarFormato.RechazaLaApi(() => s.Llamar(), CodigosDeError.GEN_008);

            s.AssertSinEscrituras();
        }

        // ---- PA-GEN-23, PA6_GEN_08 y PA6_GEN_09: las fallas de la plataforma ----

        [Fact]
        public void PA_GEN_23_UnaEscrituraFallaALaMitad_ElRechazoEsGen900ConSuTextoFijo_YLaTransaccionRevierteElCongelamiento()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            s.E.Almacen.FallarEn(OperacionDeDatos.Create, EscenarioDeGenerarFormato.TablaGeneracion, 1, ErrorInesperado);
            var contexto = s.ContextoApi(s.ArregloId);

            EscenarioDeGenerarFormato.RechazaLaApi(() => s.EjecutarApi(contexto), CodigosDeError.GEN_900);

            // El código LLEGÓ a escribir el congelamiento (paso 10a) antes de fallar en la bitácora (10b): la escritura quedó registrada con SYSTEM...
            var congelamiento = Assert.Single(s.Escrituras);
            Assert.Equal(OperacionDeDatos.Update, congelamiento.Operacion);
            Assert.Equal(EscenarioDeGenerarFormato.TablaArreglo, congelamiento.Tabla);
            Assert.Equal(s.E.Almacen.IdDeSystem, congelamiento.Identidad);
            // ...y la transacción la deshizo.
            var arreglo = s.Arreglo();
            Assert.Null(V(arreglo, "sanic_fechacongelamiento"));
            Assert.Null(V(arreglo, "sanic_instantaneacedula"));
            Assert.Null(V(arreglo, "sanic_numeroprovisional"));
            Assert.Empty(s.Generaciones());
            Assert.Empty(contexto.OutputParameters);
        }

        [Theory]
        [InlineData(0)]   // la versión de fila del arreglo ya cambió (0x80060882) al congelar
        [InlineData(1)]   // la clave alterna de la bitácora (K-01, 0x80060892)
        public void PA6_GEN_08_UnaFallaDeConcurrenciaDeLaPlataforma_SaleComoGen900_SinElTextoDeLaPlataformaNiEscriturasPersistidas(int caso)
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            if (caso == 0) s.E.Almacen.FallarEn(OperacionDeDatos.Update, EscenarioDeGenerarFormato.TablaArreglo, 1, CodigosDeFalla.VersionDeFilaNoCoincide);
            else s.E.Almacen.FallarEn(OperacionDeDatos.Create, EscenarioDeGenerarFormato.TablaGeneracion, 1, CodigosDeFalla.ClaveDuplicada);
            var contexto = s.ContextoApi(s.ArregloId);

            var rechazo = EscenarioDeGenerarFormato.RechazaLaApi(() => s.EjecutarApi(contexto), CodigosDeError.GEN_900);

            Assert.DoesNotContain("Falla simulada", rechazo.Message);
            Assert.DoesNotContain(s.E.Fabrica.Tracing.Lineas, linea => linea.Contains("Falla simulada"));     // la traza lleva el tipo y el código, nunca el mensaje de la plataforma
            // Llegó hasta la escritura: leyó el arreglo con el llamador y la versión con SYSTEM.
            Assert.Contains(s.Consultas, c => c.Identidad == s.E.Oficial && c.Operacion == OperacionDeDatos.Retrieve && c.Tabla == EscenarioDeGenerarFormato.TablaArreglo);
            Assert.Contains(s.Consultas, c => c.Identidad == s.E.Almacen.IdDeSystem && c.Tabla == EscenarioDeGenerarFormato.TablaMapeo);
            if (caso == 1) Assert.Single(s.EscriturasDe(EscenarioDeGenerarFormato.TablaArreglo, OperacionDeDatos.Update));
            Assert.Null(V(s.Arreglo(), "sanic_fechacongelamiento"));
            Assert.Empty(s.Generaciones());
            Assert.Empty(contexto.OutputParameters);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void PA6_GEN_09_UnRechazoDeNegocioDeUnStepAnidado_SaleTalCual_NoComoGen900(int caso)
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            var tabla = caso == 0 ? EscenarioDeGenerarFormato.TablaArreglo : EscenarioDeGenerarFormato.TablaGeneracion;
            var operacion = caso == 0 ? OperacionDeDatos.Update : OperacionDeDatos.Create;
            s.E.Almacen.FallarEn(operacion, tabla, 1, RechazoDeUnStep);
            var contexto = s.ContextoApi(s.ArregloId);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => s.EjecutarApi(contexto));

            // El doble arma la falla con este texto (FallaPedida); la base lo relanza sin tocarlo.
            Assert.Equal("Falla simulada con FallarEn: " + operacion + " sobre '" + tabla + "' (código " + RechazoDeUnStep.ToString(System.Globalization.CultureInfo.InvariantCulture) + ").", rechazo.Message);
            Assert.NotEqual(CodigosDeError.GEN_900.ToString(), rechazo.Message);
            Assert.DoesNotContain("-900", rechazo.Message);
            Assert.Null(V(s.Arreglo(), "sanic_fechacongelamiento"));
            Assert.Empty(s.Generaciones());
            Assert.Empty(contexto.OutputParameters);
        }

        // ---- PA6_GEN_10, _11 y _13: quién es quién ----

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA6_GEN_10_UnServicioDeSystemYUnoDelLlamadorPorEjecucion_AunEnLaVistaPrevia(bool vistaPrevia)
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            Guid? previa = vistaPrevia ? (Guid?)s.UnaVersionDeVistaPrevia(Plantillas.V07, derechos: AccessRights.WriteAccess, mapeo: EscenarioDeGenerarFormato.MapeoDelSpike()) : null;
            var fabrica = s.E.Fabrica;
            int antes = fabrica.ServiciosCreados.Count;

            s.Llamar(previa);

            var creados = fabrica.ServiciosCreados.Skip(antes).ToList();
            Assert.Equal(2, creados.Count);
            Assert.Single(creados, id => id == s.E.Oficial);
            Assert.Single(creados, id => id == fabrica.IdDeSystem);
        }

        [Fact]
        public void PA6_GEN_11_ElActorYElLlamadorSonElInitiatingUserId_NoElUserId()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            var contexto = s.ContextoApi(s.ArregloId, iniciador: s.E.Oficial, usuario: s.E.OtroOficial);

            s.EjecutarApi(contexto);

            var generacion = Assert.Single(s.Generaciones());
            var generadoPor = Assert.IsType<EntityReference>(V(generacion, "sanic_generadoporid"));
            Assert.Equal(s.E.Oficial, generadoPor.Id);
            Assert.DoesNotContain(s.Consultas, c => c.Identidad == s.E.OtroOficial);
            Assert.DoesNotContain(s.E.Fabrica.ServiciosCreados, id => id == s.E.OtroOficial);
            Assert.Contains(s.Consultas, c => c.Identidad == s.E.Oficial && c.Operacion == OperacionDeDatos.Retrieve && c.Tabla == EscenarioDeGenerarFormato.TablaArreglo);
        }

        [Fact]
        public void PA6_GEN_13_LasEscriturasSonDeSystem_AunQueElLlamadorNoPuedaEscribirNiElArregloNiLaBitacora()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            var oficial = s.E.Oficial;
            s.E.Almacen.DenegarPrivilegio(oficial, EscenarioDeGenerarFormato.TablaArreglo, OperacionDeDatos.Update);
            s.E.Almacen.DenegarPrivilegio(oficial, EscenarioDeGenerarFormato.TablaGeneracion, OperacionDeDatos.Create);
            s.E.Almacen.DenegarEscrituraDeColumna(oficial, EscenarioDeGenerarFormato.TablaArreglo, "sanic_instantaneacedula");
            s.E.Almacen.DenegarEscrituraDeColumna(oficial, EscenarioDeGenerarFormato.TablaArreglo, "sanic_fechacongelamiento");

            var contexto = s.Llamar();

            Assert.True(Assert.IsType<bool>(contexto.OutputParameters["congelado"]));
            Assert.NotNull(V(s.Arreglo(), "sanic_fechacongelamiento"));
            Assert.Single(s.Generaciones());
            Assert.All(s.Escrituras, e => Assert.Equal(s.E.Almacen.IdDeSystem, e.Identidad));
        }

        // ---- PA-GEN-25: la traza ----

        [Fact]
        public void PA_GEN_25_NingunaLineaDeTrazaLlevaLaCedulaLaCuentaElNombreElCorreoNiElCelular_NiEnExitoNiEnFalla()
        {
            var exito = EscenarioDeGenerarFormato.Armar(real: true);
            exito.Llamar();

            var falla = EscenarioDeGenerarFormato.Armar(real: true);
            falla.E.Almacen.FallarEn(OperacionDeDatos.Create, EscenarioDeGenerarFormato.TablaGeneracion, 1, ErrorInesperado);
            Assert.Throws<InvalidPluginExecutionException>(() => falla.Llamar());

            Assert.NotEmpty(exito.E.Fabrica.Tracing.Lineas);                            // la traza existe (la entrada y la salida de la base)
            Assert.DoesNotContain(exito.E.Fabrica.Tracing.Lineas, linea => linea.Contains(exito.Marca));
            Assert.DoesNotContain(falla.E.Fabrica.Tracing.Lineas, linea => linea.Contains(falla.Marca));
        }

        // ---- CARP-GEN-012 (decisión 8(c) del orquestador, 2026-10-19) ----

        /// <summary>
        /// Deja al llamador SIN el perfil de datos sensibles para esa columna: no la ve (la lectura la trae AUSENTE, V-6) y tampoco puede escribirla (seguridad de columna). SYSTEM sí la ve.
        /// </summary>
        private static void SinPerfilDeColumna(EscenarioDeGenerarFormato s, string tabla, string columna)
        {
            s.E.Almacen.OcultarColumna(tabla, columna, s.E.Oficial);
            s.E.Almacen.DenegarEscrituraDeColumna(s.E.Oficial, tabla, columna);
        }

        [Theory]
        [InlineData(0)]   // la cuenta a debitar del arreglo, arreglo sin congelar
        [InlineData(1)]   // la cédula del Cliente, arreglo sin congelar
        [InlineData(2)]   // la cédula de la instantánea, arreglo congelado
        [InlineData(3)]   // la cuenta a debitar del arreglo, arreglo congelado
        public void PA6_GEN_19_UnLlamadorSinPerfilParaLaCuentaOLaCedulaQueLaFilaSiTiene_DaGen012_AntesDeCualquierEscritura(int caso)
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true, congelado: caso >= 2);
            switch (caso)
            {
                case 0:
                case 3:
                    SinPerfilDeColumna(s, EscenarioDeGenerarFormato.TablaArreglo, "sanic_cuentadebitar");
                    break;
                case 1:
                    SinPerfilDeColumna(s, EscenarioDeGenerarFormato.TablaCliente, "sanic_cedula");
                    break;
                default:
                    SinPerfilDeColumna(s, EscenarioDeGenerarFormato.TablaArreglo, "sanic_instantaneacedula");
                    break;
            }
            var contexto = s.ContextoApi(s.ArregloId);

            EscenarioDeGenerarFormato.RechazaLaApi(() => s.EjecutarApi(contexto), CodigosDeError.GEN_012);

            s.AssertSinEscrituras();
            Assert.Empty(contexto.OutputParameters);
            Assert.Empty(s.Generaciones());
            if (caso < 2) Assert.Null(V(s.Arreglo(), "sanic_fechacongelamiento"));
            // Leyó primero con el llamador y comprobó con SYSTEM; la comprobación pide SOLO las columnas sensibles (no devuelve nada de más a quien llama).
            Assert.Contains(s.Consultas, c => c.Identidad == s.E.Oficial && c.Operacion == OperacionDeDatos.Retrieve && c.Tabla == EscenarioDeGenerarFormato.TablaArreglo);
            var sensibles = new HashSet<string>(new[] { "sanic_cuentadebitar", "sanic_instantaneacedula", "sanic_cedula" }, StringComparer.Ordinal);
            var deSystem = s.Consultas.Where(c => c.Identidad == s.E.Almacen.IdDeSystem && c.Operacion == OperacionDeDatos.Retrieve
                && (c.Tabla == EscenarioDeGenerarFormato.TablaArreglo || c.Tabla == EscenarioDeGenerarFormato.TablaCliente)).ToList();
            Assert.NotEmpty(deSystem);
            Assert.All(deSystem, c =>
            {
                Assert.False(c.TodasLasColumnas);
                Assert.All(c.Columnas, columna => Assert.Contains(columna, sensibles));
            });
        }

        [Fact]
        public void PA6_GEN_19b_ElRechazoPorFaltaDePerfilNoLlevaNingunValorSensibleNiEnElMensajeNiEnLaTraza()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);
            SinPerfilDeColumna(s, EscenarioDeGenerarFormato.TablaArreglo, "sanic_cuentadebitar");

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => s.Llamar());

            Assert.Equal(CodigosDeError.GEN_012.ToString(), rechazo.Message);
            Assert.DoesNotContain(s.Marca, rechazo.Message);
            Assert.DoesNotContain(s.E.Fabrica.Tracing.Lineas, linea => linea.Contains(s.Marca));
        }

        [Fact]
        public void PA6_GEN_20_SiLaCuentaEstaVaciaEnLaFila_NoEsFaltaDePermiso_SigueLaReglaDeFaltantesYGenera()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true, ajustarArreglo: a => a.Sin("sanic_cuentadebitar"));
            SinPerfilDeColumna(s, EscenarioDeGenerarFormato.TablaArreglo, "sanic_cuentadebitar");

            var contexto = s.Llamar();

            Assert.True(Assert.IsType<bool>(contexto.OutputParameters["congelado"]));
            Assert.NotNull(V(s.Arreglo(), "sanic_fechacongelamiento"));
            Assert.Single(s.Generaciones());
        }

        [Fact]
        public void PA6_GEN_21_SiLaCedulaDelClienteEstaVaciaEnLaFila_NoEsFaltaDePermiso_SeCongelaSinCedulaYGenera()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true, ajustarCliente: c => c.Sin("sanic_cedula"));
            SinPerfilDeColumna(s, EscenarioDeGenerarFormato.TablaCliente, "sanic_cedula");

            var contexto = s.Llamar();

            Assert.True(Assert.IsType<bool>(contexto.OutputParameters["congelado"]));
            Assert.Null(V(s.Arreglo(), "sanic_instantaneacedula"));          // la cédula faltante queda vacía, no un texto en blanco
            Assert.NotNull(V(s.Arreglo(), "sanic_instantaneanombre"));
            Assert.Single(s.Generaciones());
        }

        [Fact]
        public void PA6_GEN_22_UnArregloCongeladoImprimeSuInstantanea_AunqueElLlamadorNoTengaPerfilParaLaCedulaDelCliente()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true, congelado: true);
            SinPerfilDeColumna(s, EscenarioDeGenerarFormato.TablaCliente, "sanic_cedula");

            var contexto = s.Llamar();

            Assert.True(Assert.IsType<bool>(contexto.OutputParameters["congelado"]));
            Assert.Single(s.Generaciones());
        }

        [Fact]
        public void PA6_GEN_23_UnLlamadorConPerfilCompleto_GeneraSinElRechazo()
        {
            var s = EscenarioDeGenerarFormato.Armar(real: true);

            var contexto = s.Llamar();

            Assert.True(Assert.IsType<bool>(contexto.OutputParameters["congelado"]));
            Assert.Equal(s.CedulaDelCliente, V(s.Arreglo(), "sanic_instantaneacedula"));
        }
    }
}
