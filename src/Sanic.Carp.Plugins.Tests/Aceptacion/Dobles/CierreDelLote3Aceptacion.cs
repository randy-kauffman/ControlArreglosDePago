using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Text.RegularExpressions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Cierre del lote 3, revisión vuelta 2</b> (hallazgos H1 a H10 y las dos guardas; diseno/03 4.5, párrafo "Cierre del lote 3 (2026-10-14)"). Parche acotado, con las pruebas escritas
    /// primero y vistas en rojo. Principio que sigue mandando: <b>el doble lanza antes que adivinar</b>.
    /// </summary>
    public class CierreDelLote3Aceptacion
    {
        private const string Arreglo = "sanic_carp_tbl_arreglo";
        private const string Cliente = "sanic_carp_tbl_cliente";
        private const string Conversacion = "sanic_carp_tbl_conversacion";

        // ===================================================================================================================
        // H1: Delete con hijas en una relación REL del diccionario
        // ===================================================================================================================

        private static readonly Dictionary<string, string> TablaDelNombreVisible = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Cliente", "sanic_carp_tbl_cliente" },
            { "Conversación", "sanic_carp_tbl_conversacion" },
            { "Arreglo", "sanic_carp_tbl_arreglo" },
            { "Cuota", "sanic_carp_tbl_cuota" },
            { "Versión de plantilla", "sanic_carp_tbl_versionplantilla" },
            { "Mapeo de celda", "sanic_carp_tbl_mapeocelda" },
            { "Generación de formato", "sanic_carp_tbl_generacionformato" },
            { "Usuario", "systemuser" },
            { "Email (estándar)", "email" }
        };

        /// <summary>Las 19 relaciones de la sección 6.2 del diccionario, leídas por esta prueba (no por el doble): id, nombre, tabla padre, tabla hija y lookup.</summary>
        public static IEnumerable<object[]> Relaciones()
        {
            var en62 = false;
            foreach (var linea in ApoyoDeAceptacion.LineasDelDocumento("diseno/02-diccionario-datos.md"))
            {
                if (linea.StartsWith("### 6.2", StringComparison.Ordinal)) { en62 = true; continue; }
                if (linea.StartsWith("### 6.3", StringComparison.Ordinal)) break;
                if (!en62 || !linea.StartsWith("| REL-", StringComparison.Ordinal)) continue;
                var celdas = linea.Split('|').Select(c => c.Trim()).ToArray();
                yield return new object[] { celdas[1], celdas[2].Trim('`'), TablaDelNombreVisible[celdas[3]], TablaDelNombreVisible[celdas[4]], celdas[5].Trim('`') };
            }
        }

        /// <summary>lote 3b: REL-10 a REL-19 (padre <c>systemuser</c>, declaradas Restrict y NO medidas: no se crean ni borran usuarios). Las otras nueve se miden en <c>MedidoEnDevRelacionesAceptacion</c>.</summary>
        public static IEnumerable<object[]> RelacionesConPadreUsuario()
        {
            return Relaciones().Where(r => (string)r[2] == "systemuser");
        }

        [Fact]
        public void CL3_H1_00_ElDiccionarioTrae19RelacionesYLaPruebaLasLee()
        {
            // cierre del lote 3, revisión vuelta 2, H1 (guarda: si el diccionario cambia, la teoría de abajo no puede quedarse corta en silencio)
            Assert.Equal(19, Relaciones().Count());
        }

        [Theory]
        [MemberData(nameof(RelacionesConPadreUsuario))]
        public void CL3_H1_01_DeleteDeUnaFilaConHijasEnLaRelacion_LanzaNotSupportedNombrandoLaRelacion_YNoBorraNada(string id, string nombre, string padre, string hija, string lookup)
        {
            // cierre del lote 3, revisión vuelta 2, H1. Lote 3b, medido en Dev: REL-01 a REL-09 y NAT-01 ya se emulan (MedidoEnDevRelacionesAceptacion); esta teoría queda SOLO para REL-10 a REL-19 (padre systemuser, no medidas).
            var almacen = new OrganizationServiceEnMemoria();
            var idDelPadre = Guid.NewGuid();
            var idDeLaHija = Guid.NewGuid();
            almacen.Sembrar(new Entity(padre, idDelPadre));
            almacen.Sembrar(new Entity(hija, idDeLaHija) { [lookup] = new EntityReference(padre, idDelPadre) });

            var error = Assert.Throws<NotSupportedException>(() => almacen.Delete(padre, idDelPadre));

            Assert.Contains(id, error.Message, StringComparison.Ordinal);
            Assert.Contains(nombre, error.Message, StringComparison.Ordinal);
            Assert.Contains("el doble no emula Restrict/Cascade; pendiente de muestra", error.Message, StringComparison.Ordinal);
            Assert.True(almacen.Existe(padre, idDelPadre), "la fila padre sigue ahí");
            Assert.True(almacen.Existe(hija, idDeLaHija), "la hija sigue ahí");
            Assert.Empty(almacen.Escrituras);

            // Sin hijas el Delete funciona: se borra la hija (que no es padre de nadie) y después el padre.
            almacen.Delete(hija, idDeLaHija);
            almacen.Delete(padre, idDelPadre);
            Assert.False(almacen.Existe(padre, idDelPadre));
        }

        [Fact]
        public void CL3_H1_02_UnDeleteSinHijasSigueFuncionando_YUnaHijaDeOtroPadreNoCuenta()
        {
            // cierre del lote 3, revisión vuelta 2, H1
            var almacen = new OrganizationServiceEnMemoria();
            var cliente = Guid.NewGuid();
            var otroCliente = Guid.NewGuid();
            almacen.Sembrar(new Entity(Cliente, cliente), new Entity(Cliente, otroCliente),
                new Entity(Conversacion, Guid.NewGuid()) { ["sanic_clienteid"] = new EntityReference(Cliente, otroCliente) });

            almacen.Delete(Cliente, cliente);

            Assert.False(almacen.Existe(Cliente, cliente));
            Assert.Single(almacen.Escrituras);
            // lote 3b, medido en Dev: REL-01 es Restrict => 0x80040227 (antes NotSupportedException).
            ApoyoMedido.AssertFalla(ApoyoDeDobles.Falla(() => almacen.Delete(Cliente, otroCliente)), CodigosDeFalla.RestriccionDeRelacion, "sanic_carp_cliente_conversacion");
        }

        [Fact]
        public void CL3_H1_03_UnDeleteDeUnaFilaInexistenteSigueSiendoLaFallaDeFilaInexistente()
        {
            // cierre del lote 3, revisión vuelta 2, H1
            var almacen = new OrganizationServiceEnMemoria();

            var falla = ApoyoDeDobles.Falla(() => almacen.Delete(Cliente, Guid.NewGuid()));

            Assert.Equal(CodigosDeFalla.FilaInexistente, falla.Detail.ErrorCode);
        }

        // ===================================================================================================================
        // H3: la constante del tipo de la variable de entorno y el escáner de literales
        // ===================================================================================================================

        [Fact]
        public void CL3_H3_01_TipoDeVariableTextoEsUnaSolaConstante_ConLaCitaDelFixture05_YSinUnComentarioEngañoso()
        {
            // cierre del lote 3, revisión vuelta 2, H3
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("src/Sanic.Carp.Plugins.Tests/Apoyo/ConstructorDeDatos.cs");
            var fuente = string.Join("\n", lineas);
            var fixture = string.Join("\n", ApoyoDeAceptacion.LineasDelDocumento("src/Sanic.Carp.Plugins.Tests/Fixtures/Dataverse/05-environmentvariable.json"));

            Assert.Contains("type = OptionSetValue(100000000) = Texto.", fixture, StringComparison.Ordinal);
            Assert.Single(Regex.Matches(fuente, @"const\s+int\s+TipoDeVariableTexto\s*=\s*100000000\s*;"));
            Assert.DoesNotContain("100_000_000", fuente, StringComparison.Ordinal);
            Assert.DoesNotContain("separador de dígitos", fuente, StringComparison.Ordinal);
            var indice = Array.FindIndex(lineas, l => l.Contains("const int TipoDeVariableTexto", StringComparison.Ordinal));
            var comentario = string.Join(" ", lineas.Skip(Math.Max(0, indice - 4)).Take(4));
            Assert.Contains("05-environmentvariable", comentario, StringComparison.Ordinal);
            Assert.Contains("Texto", comentario, StringComparison.Ordinal);
            // El número aparece una sola vez en el código (los comentarios no cuentan): el resto del constructor usa la constante.
            var enCodigo = lineas.Count(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal) && Regex.IsMatch(l, @"\b100000000\b"));
            Assert.Equal(1, enCodigo);
        }

        [Theory]
        [InlineData("var x = new OptionSetValue(0);")]
        [InlineData("var x = new OptionSetValue( 1 );")]
        [InlineData("var activa = ((estado as OptionSetValue) ?? algo).Value == 0;")]
        [InlineData("if (estado.Value != 1) return;")]
        [InlineData("if (StateCode == 1) return;")]
        [InlineData("return UnCorreo(true, 4);")]
        [InlineData("var x = 159460001;")]
        [InlineData("var x = 100_000_000;")]
        [InlineData("var x = 100000000;")]
        public void CL3_H3_02_ElEscanerRechazaLosLiteralesSueltosDeChoiceYDeEstado(string linea)
        {
            // cierre del lote 3, revisión vuelta 2, H3
            Assert.Single(EscanerDeLiterales.Hallazgos(new[] { linea }));
        }

        [Theory]
        [InlineData("private const int ModoDeAccesoNormal = 0;")]
        [InlineData("        internal const int TipoDeVariableTexto = 100000000;")]
        [InlineData("public const int Nombre = 159460001;")]
        [InlineData("var x = new OptionSetValue(Estados.Activo);")]
        [InlineData("var x = new OptionSetValue((int)estado);")]
        [InlineData("// new OptionSetValue(0) comentado")]
        [InlineData("if (lista.Count == 0) return;")]
        [InlineData("var activa = estado.Value == Estados.Activo;")]
        public void CL3_H3_03_ElEscanerPermiteLasDeclaracionesDeConstantesConNombre_YLoQueNoEsUnLiteralDeChoice(string linea)
        {
            // cierre del lote 3, revisión vuelta 2, H3
            Assert.Empty(EscanerDeLiterales.Hallazgos(new[] { linea }));
        }

        [Fact]
        public void CL3_H3_04_ElEscanerRecorreTodosLosFuentesDeDoblesYApoyo()
        {
            // cierre del lote 3, revisión vuelta 2, H3
            var rutas = EscanerDeLiterales.RutasDeLosFuentes();

            Assert.Contains("src/Sanic.Carp.Plugins.Tests/Dobles/OrganizationServiceEnMemoria.cs", rutas);
            Assert.Contains("src/Sanic.Carp.Plugins.Tests/Dobles/EsquemaDelAlmacen.cs", rutas);
            Assert.Contains("src/Sanic.Carp.Plugins.Tests/Apoyo/ConstructorDeDatos.cs", rutas);
            Assert.Contains("src/Sanic.Carp.Plugins.Tests/Apoyo/LectorDeFixtures.cs", rutas);
            Assert.True(rutas.Count >= 20, "Dobles/ y Apoyo/ suman más de 20 fuentes; llegaron " + rutas.Count.ToString(CultureInfo.InvariantCulture));
        }

        // ===================================================================================================================
        // H4: AssignRequest valida el ownerid como Create/Update
        // ===================================================================================================================

        private static AssignRequest Asignar(string tabla, Guid id, EntityReference a)
        {
            return new AssignRequest { Target = new EntityReference(tabla, id), Assignee = a };
        }

        [Fact]
        public void CL3_H4_01_AssignAUnaTablaQueNoEsTeamNiSystemuser_SeRechazaConLaMismaValidacionQueUpdate()
        {
            // cierre del lote 3, revisión vuelta 2, H4
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(Arreglo, id));
            var propietarioInvalido = new EntityReference(Cliente, Guid.NewGuid());

            // lote 3b, medido en Dev: 0x8004049E 'Invalid PrincipalType=... Principal type must be either SystemUser, Team or Organization.' (antes ArgumentException).
            var deUpdate = ApoyoDeDobles.Falla(() => almacen.Update(new Entity(Arreglo, id) { ["ownerid"] = propietarioInvalido }));
            var deAssign = ApoyoDeDobles.Falla(() => almacen.Execute(Asignar(Arreglo, id, propietarioInvalido)));

            ApoyoMedido.AssertFalla(deUpdate, CodigosDeFalla.PropietarioInvalido, "Principal type must be either SystemUser, Team or Organization.");
            ApoyoMedido.AssertFalla(deAssign, CodigosDeFalla.PropietarioInvalido, "Principal type must be either SystemUser, Team or Organization.");
            Assert.Equal(deUpdate.Detail.Message, deAssign.Detail.Message);
            Assert.False(almacen.Leer(Arreglo, id).Contains("ownerid"));
            Assert.Empty(almacen.Escrituras);
        }

        [Theory]
        [InlineData("team")]
        [InlineData("systemuser")]
        public void CL3_H4_02_AssignAUnTeamOAUnSystemuserSigueFuncionando(string tablaDelPropietario)
        {
            // cierre del lote 3, revisión vuelta 2, H4
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            var propietario = Guid.NewGuid();
            almacen.Sembrar(new Entity(Arreglo, id));

            almacen.Execute(Asignar(Arreglo, id, new EntityReference(tablaDelPropietario, propietario)));

            var asignado = (EntityReference)almacen.Leer(Arreglo, id)["ownerid"];
            Assert.Equal(tablaDelPropietario, asignado.LogicalName);
            Assert.Equal(propietario, asignado.Id);
        }

        [Fact]
        public void CL3_H4_03_ElPrincipalDebeExistirEnCreate_PeroAssignNoLoValida_LimiteConocido()
        {
            // cierre del lote 3, revisión vuelta 2, H4. Lote 3b, medido en Dev (punto 14): Create y Update validan que el team o el usuario EXISTAN (0x80040217); AssignRequest NO se midió y no lo valida (límite declarado).
            var almacen = new OrganizationServiceEnMemoria();
            var team = Guid.NewGuid();
            almacen.Sembrar(new Entity("team", team));

            ApoyoMedido.AssertFalla(ApoyoDeDobles.Falla(() => almacen.Create(new Entity(Arreglo) { ["ownerid"] = new EntityReference("team", Guid.NewGuid()) })), CodigosDeFalla.FilaInexistente, "Entity 'team' With Id = ");
            var id = almacen.Create(new Entity(Arreglo) { ["ownerid"] = new EntityReference("team", team) });
            almacen.Execute(Asignar(Arreglo, id, new EntityReference("systemuser", Guid.NewGuid())));

            Assert.Equal("systemuser", ((EntityReference)almacen.Leer(Arreglo, id)["ownerid"]).LogicalName);
        }

        // ===================================================================================================================
        // H5: null en statecode, statuscode, ownerid o el id primario
        // ===================================================================================================================

        public static IEnumerable<object[]> ColumnasQueNoAdmitenNulo()
        {
            foreach (var tabla in new[] { Arreglo, ApoyoDeDobles.TablaZzf })
            {
                yield return new object[] { tabla, "statecode" };
                yield return new object[] { tabla, "statuscode" };
                yield return new object[] { tabla, "ownerid" };
                yield return new object[] { tabla, tabla + "id" };
            }
        }

        [Theory]
        [MemberData(nameof(ColumnasQueNoAdmitenNulo))]
        public void CL3_H5_01_CreateYUpdateConNullEnEstaColumnaSeRechazan_SinTocarElAlmacen(string tabla, string columna)
        {
            // cierre del lote 3, revisión vuelta 2, H5
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Guid.NewGuid();
            almacen.Sembrar(new Entity(tabla, existente));

            var deCreate = Assert.Throws<ArgumentException>(() => almacen.Create(new Entity(tabla) { [columna] = null }));
            var deUpdate = Assert.Throws<ArgumentException>(() => almacen.Update(new Entity(tabla, existente) { [columna] = null }));
            var deRequest = Assert.Throws<ArgumentException>(() => almacen.Execute(new UpdateRequest { Target = new Entity(tabla, existente) { [columna] = null } }));

            foreach (var error in new[] { deCreate, deUpdate, deRequest })
            {
                Assert.Contains(columna, error.Message, StringComparison.Ordinal);
                Assert.Contains("null", error.Message, StringComparison.Ordinal);
            }
            Assert.Single(almacen.Todas(tabla));
            Assert.Empty(almacen.Escrituras);
        }

        [Fact]
        public void CL3_H5_02_UnNullEnOtraColumnaSigueBorrandola()
        {
            // cierre del lote 3, revisión vuelta 2, H5 (no se rompe el contrato de la regla 1: un null escrito borra la columna)
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(Cliente, id) { ["sanic_celular"] = "88887777" });

            almacen.Update(new Entity(Cliente, id) { ["sanic_celular"] = null });

            Assert.False(almacen.Leer(Cliente, id).Contains("sanic_celular"));
        }

        // ===================================================================================================================
        // H6: Update { statecode = X } con la fila ya en X
        // ===================================================================================================================

        [Fact]
        public void CL3_H6_01_UnUpdateConSoloStatecodeIgualAlDeLaFilaReseteaElMotivoAlPorDefecto()
        {
            // cierre del lote 3, revisión vuelta 2, H6. INVERTIDA en el lote 3b, medido en Dev (13-estados.json): 'R1 en 0/159460001 + statecode=0 (igual al actual) => 0/1'. Antes lanzaba NotSupportedException.
            var almacen = new OrganizationServiceEnMemoria();
            var vigente = Estados.De(EstadoDeArreglo.Vigente);
            var borrador = Estados.De(EstadoDeArreglo.Borrador);
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(Arreglo, id) { ["statecode"] = new OptionSetValue(vigente.StateCode), ["statuscode"] = new OptionSetValue(vigente.StatusCode) });

            almacen.Update(new Entity(Arreglo, id) { ["statecode"] = new OptionSetValue(vigente.StateCode) });

            Assert.Equal(borrador.StatusCode, ((OptionSetValue)almacen.Leer(Arreglo, id)["statuscode"]).Value);
            Assert.Single(almacen.Escrituras);
        }

        [Fact]
        public void CL3_H6_02_UnParCompletoEnElMismoEstadoSigueSiendoValido_YUnCambioDeEstadoSigueDandoElMotivoPorDefecto()
        {
            // cierre del lote 3, revisión vuelta 2, H6 (lo que no cambia)
            var almacen = new OrganizationServiceEnMemoria();
            var vigente = Estados.De(EstadoDeArreglo.Vigente);
            var borrador = Estados.De(EstadoDeArreglo.Borrador);
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(Arreglo, id) { ["statecode"] = new OptionSetValue(vigente.StateCode), ["statuscode"] = new OptionSetValue(vigente.StatusCode) });

            almacen.Update(new Entity(Arreglo, id) { ["statecode"] = new OptionSetValue(borrador.StateCode), ["statuscode"] = new OptionSetValue(borrador.StatusCode) });
            almacen.Update(new Entity(Arreglo, id) { ["statecode"] = new OptionSetValue(Estados.Inactivo) });

            Assert.Equal(Estados.StatusPorDefecto(Estados.Inactivo), ((OptionSetValue)almacen.Leer(Arreglo, id)["statuscode"]).Value);
        }

        // ===================================================================================================================
        // H7: una sola definición de "consulta paginada"
        // ===================================================================================================================

        private static QueryExpression Consulta(PagingInfo paginacion)
        {
            return new QueryExpression(ApoyoDeDobles.TablaZzf) { ColumnSet = new ColumnSet(false), PageInfo = paginacion };
        }

        [Fact]
        public void CL3_H7_01_PageInfoConPageNumberMayorQue1YCountEnCero_Lanza()
        {
            // cierre del lote 3, revisión vuelta 2, H7
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(ApoyoDeDobles.Zzf(ApoyoDeDobles.Fila1), ApoyoDeDobles.Zzf(ApoyoDeDobles.Fila2));

            var error = Assert.Throws<InvalidOperationException>(() => almacen.RetrieveMultiple(Consulta(new PagingInfo { PageNumber = 2, Count = 0 })));

            Assert.Contains("PageNumber", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CL3_H7_02_PageInfoConPageNumber1YCountEnCeroEsPaginada_ConTopCountLanza_SinTopCountDevuelveLaPrimeraPaginaConSuTotal()
        {
            // cierre del lote 3, revisión vuelta 2, H7
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(ApoyoDeDobles.Zzf(ApoyoDeDobles.Fila1), ApoyoDeDobles.Zzf(ApoyoDeDobles.Fila2), ApoyoDeDobles.Zzf(ApoyoDeDobles.Fila3));
            var conTop = Consulta(new PagingInfo { PageNumber = 1, Count = 0 });
            conTop.TopCount = 2;

            Assert.Throws<InvalidOperationException>(() => almacen.RetrieveMultiple(conTop));
            var resultado = almacen.RetrieveMultiple(Consulta(new PagingInfo { PageNumber = 1, Count = 0, ReturnTotalRecordCount = true }));

            Assert.Equal(3, resultado.Entities.Count);
            Assert.Equal(3, resultado.TotalRecordCount);
            Assert.False(resultado.MoreRecords);
            Assert.Contains("page=\"1\"", resultado.PagingCookie, StringComparison.Ordinal);
        }

        [Fact]
        public void CL3_H7_03_LaDefinicionDePaginadaEstaEnUnSoloLugar_LECTURA_DEL_FUENTE()
        {
            // cierre del lote 3, revisión vuelta 2, H7. Mutación que mata: volver a escribir "PageInfo.Count > 0" en Consultar además de en Validar.
            var definiciones = new List<string>();
            foreach (var ruta in EscanerDeLiterales.RutasDeLosFuentes().Where(r => r.Contains("/Dobles/", StringComparison.Ordinal)))
            {
                var lineas = ApoyoDeAceptacion.LineasDelDocumento(ruta);
                for (int i = 0; i < lineas.Length; i++)
                    if (!lineas[i].TrimStart().StartsWith("//", StringComparison.Ordinal) && Regex.IsMatch(lineas[i], @"PageInfo\.Count\s*>\s*0"))
                        definiciones.Add(ruta + ":" + (i + 1).ToString(CultureInfo.InvariantCulture));
            }

            Assert.True(definiciones.Count == 1, "'PageInfo.Count > 0' debe aparecer una sola vez (la definición); aparece en: " + string.Join(", ", definiciones));
            var servicio = string.Join("\n", ApoyoDeAceptacion.LineasDelDocumento("src/Sanic.Carp.Plugins.Tests/Dobles/OrganizationServiceEnMemoria.cs"));
            Assert.Contains("EvaluadorDeConsultas.EsPaginada(", servicio, StringComparison.Ordinal);
        }

        // ===================================================================================================================
        // H8 (parcial): columnas de solo lectura
        // ===================================================================================================================

        public static IEnumerable<object[]> ColumnasDeFormulaYArchivo()
        {
            foreach (var par in DiccionarioDeDatos.Leer())
                foreach (var c in par.Value.Where(c => c.Clase == ClaseDeColumna.Formula || c.Clase == ClaseDeColumna.Archivo))
                    yield return new object[] { c.Tabla, c.Nombre };
        }

        private static readonly Dictionary<string, object> ColumnasDePlataformaDeSoloLectura = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "createdon", new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc) },
            { "createdby", new EntityReference("systemuser", Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001")) },
            { "modifiedon", new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc) },
            { "modifiedby", new EntityReference("systemuser", Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001")) },
            { "owningbusinessunit", new EntityReference("businessunit", Guid.Parse("aaaaaaaa-0000-4000-8000-000000000009")) },
            { "owninguser", new EntityReference("systemuser", Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001")) },
            { "owningteam", new EntityReference("team", Guid.Parse("bbbbbbbb-0000-4000-8000-000000000001")) },
            { "versionnumber", 12345L }
        };

        /// <summary>lote 3b, medido en Dev: createdon, createdby, owningbusinessunit y versionnumber se IGNORAN sin fallar (PA_MED_ESC_01). Las otras cuatro no se midieron y siguen lanzando.</summary>
        private static readonly string[] ColumnasDePlataformaMedidas = { "createdon", "createdby", "owningbusinessunit", "versionnumber" };

        public static IEnumerable<object[]> ColumnasDePlataforma()
        {
            foreach (var tabla in DiccionarioDeDatos.Leer().Keys)
                foreach (var columna in ColumnasDePlataformaDeSoloLectura.Keys.Where(c => !ColumnasDePlataformaMedidas.Contains(c))) yield return new object[] { tabla, columna };
        }

        /// <summary>lote 3b: solo la columna de ARCHIVO sigue rechazada; la fórmula se IGNORA sin fallar (medido, <c>PA_MED_ESC_01</c>).</summary>
        public static IEnumerable<object[]> ColumnasDeArchivo()
        {
            return ColumnasDeFormulaYArchivo().Where(o => (string)o[1] != "sanic_antiguedad");
        }

        [Theory]
        [MemberData(nameof(ColumnasDeArchivo))]
        public void CL3_H8_01_UnaColumnaDeArchivoNoSeEscribeNiPorCreateNiPorUpdate(string tabla, string columna)
        {
            // cierre del lote 3, revisión vuelta 2, H8. Lote 3b: la fórmula sanic_antiguedad se ignora sin fallar (18-texto-y-esquema.json); sanic_archivo NO se midió y sigue lanzando (límite declarado).
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(tabla, id));

            var deCreate = Assert.Throws<ArgumentException>(() => almacen.Create(new Entity(tabla) { [columna] = 1m }));
            var deUpdate = Assert.Throws<ArgumentException>(() => almacen.Update(new Entity(tabla, id) { [columna] = 1m }));

            foreach (var error in new[] { deCreate, deUpdate })
            {
                Assert.Contains(columna, error.Message, StringComparison.Ordinal);
                Assert.Contains("solo lectura", error.Message, StringComparison.Ordinal);
            }
            Assert.Single(almacen.Todas(tabla));
        }

        [Fact]
        public void CL3_H8_02_LasColumnasDeFormulaYArchivoDelDiccionarioSonSanicAntiguedadYSanicArchivo()
        {
            // cierre del lote 3, revisión vuelta 2, H8 (guarda: si el diccionario suma una, la teoría la cubre sola; este es el conteo conocido al 2026-10-14)
            var columnas = ColumnasDeFormulaYArchivo().Select(o => (string)o[0] + "." + (string)o[1]).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            Assert.Equal(new[] { "sanic_carp_tbl_conversacion.sanic_antiguedad", "sanic_carp_tbl_versionplantilla.sanic_archivo" }, columnas);
        }

        [Theory]
        [MemberData(nameof(ColumnasDePlataforma))]
        public void CL3_H8_03_UnaColumnaDePlataformaDeSoloLecturaNoMedidaNoSeEscribeNiPorCreateNiPorUpdate(string tabla, string columna)
        {
            // cierre del lote 3, revisión vuelta 2, H8
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(tabla, id));
            var valor = ColumnasDePlataformaDeSoloLectura[columna];

            var deCreate = Assert.Throws<ArgumentException>(() => almacen.Create(new Entity(tabla) { [columna] = valor }));
            var deUpdate = Assert.Throws<ArgumentException>(() => almacen.Update(new Entity(tabla, id) { [columna] = valor }));

            foreach (var error in new[] { deCreate, deUpdate })
            {
                Assert.Contains(columna, error.Message, StringComparison.Ordinal);
                Assert.Contains("solo lectura", error.Message, StringComparison.Ordinal);
            }
            Assert.Single(almacen.Todas(tabla));
        }

        [Fact]
        public void CL3_H8_04_SembrarSigueAceptandoLasColumnasDeSoloLectura_PorqueDeclaraDatosQueLaPlataformaYaDevolvio()
        {
            // cierre del lote 3, revisión vuelta 2, H8 (Sembrar no pasa por la validación)
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            var fila = new Entity(Arreglo, id);
            foreach (var par in ColumnasDePlataformaDeSoloLectura) fila[par.Key] = par.Value;

            almacen.Sembrar(fila);

            var leida = almacen.Leer(Arreglo, id);
            foreach (var nombre in ColumnasDePlataformaDeSoloLectura.Keys) Assert.True(leida.Contains(nombre), nombre);
        }

        // ===================================================================================================================
        // H9 y H10
        // ===================================================================================================================

        [Fact]
        public void CL3_H9_01_ColumnasDeFechaSoloFechaEsInmutableDeVerdad()
        {
            // cierre del lote 3, revisión vuelta 2, H9
            Assert.IsType<ReadOnlyCollection<KeyValuePair<string, string>>>(ColumnasDeFecha.SoloFecha);
            var lista = (IList<KeyValuePair<string, string>>)ColumnasDeFecha.SoloFecha;
            var primera = lista[0];

            Assert.Throws<NotSupportedException>(() => lista[0] = new KeyValuePair<string, string>("otra", "otra"));
            Assert.Equal(primera, ColumnasDeFecha.SoloFecha[0]);
        }

        [Fact]
        public void CL3_H10_01_LosComentariosXmlDelDobleNoTraenElHistorialTachado_LECTURA_DEL_FUENTE()
        {
            // cierre del lote 3, revisión vuelta 2, H10: el historial vive en 4.5; el comentario dice la verdad actual.
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("src/Sanic.Carp.Plugins.Tests/Dobles/OrganizationServiceEnMemoria.cs");
            var tachadas = new List<string>();
            for (int i = 0; i < lineas.Length; i++)
                if (lineas[i].Contains("~~", StringComparison.Ordinal)) tachadas.Add("línea " + (i + 1).ToString(CultureInfo.InvariantCulture));

            Assert.True(tachadas.Count == 0, "texto tachado (~~) en los comentarios de OrganizationServiceEnMemoria: " + string.Join(", ", tachadas));
        }

        // ===================================================================================================================
        // 4.5: el párrafo de cierre
        // ===================================================================================================================

        [Fact]
        public void CL3_DOC_01_LaSeccion45TraeElParrafoDeCierreConLosLimitesConocidos_YLaNuevaPendiente15()
        {
            // cierre del lote 3, revisión vuelta 2 (documentación)
            var texto = string.Join("\n", ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md"));
            var desde = texto.IndexOf("### 4.5 ", StringComparison.Ordinal);
            var hasta = texto.IndexOf("## 5. Lista cerrada", StringComparison.Ordinal);
            Assert.True(desde >= 0 && hasta > desde);
            var seccion = texto.Substring(desde, hasta - desde);

            Assert.Contains("Cierre del lote 3 (2026-10-14)", seccion, StringComparison.Ordinal);
            Assert.Contains("LÍMITES CONOCIDOS", seccion, StringComparison.Ordinal);
            foreach (var limite in new[] { "H2", "UnaVersion", "990", "integridad referencial", "sin privilegios de escritura", "nativas sin muestra", "[1..60]" })
                Assert.Contains(limite, seccion, StringComparison.Ordinal);
            Assert.Contains("(15)", seccion, StringComparison.Ordinal);
            Assert.Contains("Restrict", seccion, StringComparison.Ordinal);
        }
    }
}
