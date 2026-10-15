using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// Valores de ejemplo por clase de columna del diccionario: uno VÁLIDO y variantes ERRÓNEAS (el tipo equivocado que un código distraído escribiría). Son el instrumento con
    /// que se mide el esquema del doble; salen del diccionario y de <c>Choices.g.cs</c>, nunca del doble.
    /// </summary>
    internal static class ValoresDeEjemplo
    {
        private static readonly Dictionary<string, Type> EnumsPorChoice = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            { "productocrediticio", typeof(ProductoCrediticio) }, { "tipoarreglo", typeof(TipoDeArreglo) }, { "moneda", typeof(Moneda) },
            { "canalorigen", typeof(CanalDeOrigen) }, { "resultadoconversacion", typeof(ResultadoDeConversacion) },
            { "motivosinrespuesta", typeof(MotivoDeSinRespuesta) }, { "estadoversionplantilla", typeof(EstadoDeVersionDePlantilla) },
            { "tipomapeo", typeof(TipoDeMapeo) }, { "campomapeable", typeof(CampoMapeable) }
        };

        public static int PrimerValorDelChoice(ColumnaDelDiccionario columna)
        {
            Type tipo;
            Assert.True(EnumsPorChoice.TryGetValue(columna.Choice, out tipo), columna.Tabla + "." + columna.Nombre + ": choice sin enum en esta prueba: " + columna.Choice);
            return Convert.ToInt32(Enum.GetValues(tipo).GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
        }

        public static object Valido(ColumnaDelDiccionario columna)
        {
            switch (columna.Clase)
            {
                case ClaseDeColumna.Texto: return columna.Largo.HasValue ? new string('z', Math.Min(columna.Largo.Value, 8)) : "zzf-ok";
                case ClaseDeColumna.Entero: return columna.Minimo.HasValue ? (int)columna.Minimo.Value : 1;
                case ClaseDeColumna.Decimal: return columna.Minimo.HasValue ? columna.Minimo.Value : 1m;
                case ClaseDeColumna.Choice: return new OptionSetValue(PrimerValorDelChoice(columna));
                case ClaseDeColumna.SiNo: return true;
                case ClaseDeColumna.FechaHoraUl: return new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);
                case ClaseDeColumna.FechaSolo: return new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);
                case ClaseDeColumna.Lookup: return new EntityReference(columna.Destino, Guid.NewGuid());
                default: throw new ArgumentException("clase sin valor de ejemplo: " + columna.Clase);
            }
        }

        /// <summary>El valor que se usa en una CONDICIÓN (primitivo: el rechazo de <c>OptionSetValue</c>/<c>EntityReference</c>/<c>Money</c> como valor de condición está PENDIENTE DE MUESTRA).</summary>
        public static object ParaCondicion(ColumnaDelDiccionario columna)
        {
            return ComoValorDeCondicion(Valido(columna));
        }

        /// <summary>El valor primitivo que va en una condición para comparar contra <paramref name="valido"/> (lo medido: una condición no acepta OptionSetValue ni EntityReference).</summary>
        public static object ComoValorDeCondicion(object valido)
        {
            var opcion = valido as OptionSetValue;
            if (opcion != null) return opcion.Value;
            var referencia = valido as EntityReference;
            if (referencia != null) return referencia.Id;
            return valido;
        }

        public static string[] Variantes(ClaseDeColumna clase)
        {
            switch (clase)
            {
                case ClaseDeColumna.Texto: return new[] { "entero", "opcion" };
                case ClaseDeColumna.Entero: return new[] { "decimal", "texto", "money" };
                case ClaseDeColumna.Decimal: return new[] { "money", "entero", "texto" };   // lote 3b, medido en Dev: un double en un decimal se ACEPTA (18-texto-y-esquema.json), ya no es una variante errónea (PA_MED_FAL_05)
                case ClaseDeColumna.Choice: return new[] { "entero", "inexistente", "texto" };
                case ClaseDeColumna.SiNo: return new[] { "entero", "texto" };
                case ClaseDeColumna.FechaHoraUl: return new[] { "texto", "entero" };
                case ClaseDeColumna.FechaSolo: return new[] { "texto", "entero" };
                case ClaseDeColumna.Lookup: return new[] { "guid", "otra-tabla", "texto" };
                default: return new string[0];
            }
        }

        /// <summary>
        /// lote 3b, medido en Dev: el <c>ErrorCode</c> que da cada tipo equivocado. Medidos (18-texto-y-esquema.json): int/string/Money en decimal y string en lookup = 0x80044354; decimal/string/long en entero,
        /// string en fecha, int en choice, int/OptionSetValue en texto = 0x80040203; choice inexistente = 0x8004431A; lookup a otra tabla = 0x80040217. Los demás cruces (Money en entero, string en choice o
        /// en sí/no, int en fecha, Guid en lookup) NO se midieron: el doble los trata como la familia más cercana (extensión declarada en 4.5).
        /// </summary>
        public static int CodigoEsperado(ColumnaDelDiccionario columna, string variante)
        {
            if (variante == "inexistente") return CodigosDeFalla.OpcionInexistente;
            if (variante == "otra-tabla") return CodigosDeFalla.FilaInexistente;
            if (columna.Clase == ClaseDeColumna.Decimal || columna.Clase == ClaseDeColumna.Lookup) return CodigosDeFalla.TipoDeAtributoInvalido;
            return CodigosDeFalla.SolicitudInvalida;
        }

        public static object Erroneo(ColumnaDelDiccionario columna, string variante)
        {
            switch (variante)
            {
                case "entero": return columna.Clase == ClaseDeColumna.Choice ? (object)PrimerValorDelChoice(columna) : 5;
                case "decimal": return 5m;
                case "money": return new Money(5m);
                case "double": return 5.0;
                case "texto": return "x";
                case "opcion": return new OptionSetValue(1);
                case "inexistente": return new OptionSetValue(2000000000);
                case "guid": return Guid.NewGuid();
                case "otra-tabla": return new EntityReference(columna.Destino == "team" ? "systemuser" : "team", Guid.NewGuid());
                default: throw new ArgumentException("variante desconocida: " + variante);
            }
        }
    }

    /// <summary>
    /// <b>Vuelta 2, hallazgo 3</b>: las escrituras y las lecturas se validan contra el ESQUEMA. Antes el doble creaba una tabla nueva con un nombre equivocado, aceptaba una columna
    /// que no existe, un <c>Money</c> o un <c>int</c> en una columna decimal, un lookup a la tabla equivocada, un texto más largo que la columna, un choice que no existe, y un
    /// <c>Condition Null</c> sobre una columna mal escrita devolvía todas las filas: el error de tipeo pasaba en el puesto y fallaba en Dev.
    /// <para>
    /// <b>Criterio (decisión del arquitecto, 2026-10-14, vuelta 2)</b>. <i>Tablas propias</i> (<c>sanic_carp_tbl_*</c>): esquema COMPLETO del diccionario (clase, destino del lookup, largo,
    /// choices) más las columnas de plataforma de una tabla propia (las que traen los fixtures 02 y 03 sin estar en el diccionario: <c>createdon</c>, <c>ownerid</c>, <c>statecode</c>…).
    /// <i>Tablas nativas con muestra</i> (<c>email</c>, <c>team</c>, <c>teammembership</c>, <c>queue</c>, <c>systemuser</c>, <c>environmentvariabledefinition</c>,
    /// <c>environmentvariablevalue</c>): los NOMBRES de columna son la unión de lo que traen sus fixtures (<c>Fixtures/Dataverse</c>) y de las columnas propias de la sección 5 del
    /// diccionario (<c>email.sanic_tiemporespuesta</c> y <c>email.sanic_respondidoporid</c>, estas dos además TIPADAS); los tipos de las demás columnas nativas no se validan.
    /// <i>Tablas nativas sin muestra</i> que el diseño nombra (<c>businessunit</c>, <c>systemuserroles</c>, <c>role</c>, <c>organization</c>, <c>mailbox</c>): la tabla se acepta y sus columnas no
    /// se validan (no hay muestra de qué columnas tienen: agregarlas al esquema cuando se exporten). <i>Tablas del propio doble</i> (prefijo <c>zzf_</c>, las que las pruebas inventan para medir el
    /// almacén): libres. <i>Cualquier otra tabla</i>: rechazada, para que usarla sea una decisión explícita del arquitecto.
    /// </para>
    /// <para>
    /// <b>Lote 3b (medido en Dev, 2026-10-15):</b> una escritura mal formada (<c>Create</c>, <c>Update</c>, <c>UpdateRequest</c>) y una consulta mal formada (<c>Retrieve</c>/<c>RetrieveMultiple</c>) lanzan
    /// <c>FaultException&lt;OrganizationServiceFault&gt;</c> con el <c>ErrorCode</c> del catálogo (<c>MedidoEnDevFallasAceptacion</c>), ya no <see cref="ArgumentException"/> ni <see cref="InvalidOperationException"/>;
    /// el destino de un lookup tiene que existir (0x80040217). <c>Sembrar</c> NO valida: declara datos conocidos (fixtures de Dev, constructores medidos contra el diccionario por
    /// <c>ConstructorDeDatosAceptacion</c>) y varias pruebas siembran filas parciales a propósito.
    /// </para>
    /// No se validan, y quedan como límite conocido de 4.5: rangos de los enteros, requeridos, tipos de las nativas y los lookups de las nativas (lote 3b: el destino de un lookup del esquema SÍ se valida y el rango del decimal también).
    /// </summary>
    public class EsquemaDeEscrituraAceptacion
    {
        private static readonly Lazy<Dictionary<string, List<ColumnaDelDiccionario>>> Diccionario =
            new Lazy<Dictionary<string, List<ColumnaDelDiccionario>>>(DiccionarioDeDatos.Leer);

        private static IEnumerable<ColumnaDelDiccionario> Escribibles()
        {
            return Diccionario.Value.SelectMany(p => p.Value).Where(c => c.Clase != ClaseDeColumna.Formula && c.Clase != ClaseDeColumna.Archivo);
        }

        public static IEnumerable<object[]> ColumnasEscribibles()
        {
            foreach (var c in Escribibles()) yield return new object[] { c.Tabla, c.Nombre };
        }

        public static IEnumerable<object[]> ColumnasConVariantesErroneas()
        {
            foreach (var c in Escribibles())
                foreach (var variante in ValoresDeEjemplo.Variantes(c.Clase)) yield return new object[] { c.Tabla, c.Nombre, variante };
        }

        public static IEnumerable<object[]> ColumnasDeTextoConLargo()
        {
            foreach (var c in Escribibles().Where(c => c.Clase == ClaseDeColumna.Texto && c.Largo.HasValue)) yield return new object[] { c.Tabla, c.Nombre, c.Largo.Value };
        }

        public static IEnumerable<object[]> TablasPropias()
        {
            foreach (var tabla in Diccionario.Value.Keys) yield return new object[] { tabla };
        }

        private static ColumnaDelDiccionario Columna(string tabla, string nombre)
        {
            return Diccionario.Value[tabla].First(c => c.Nombre == nombre);
        }

        /// <summary>lote 3b: un lookup escrito por Create o Update apunta a una fila que existe; sembrarla no valida (Sembrar declara datos conocidos).</summary>
        private static void SembrarDestinoDe(OrganizationServiceEnMemoria almacen, object valor)
        {
            var referencia = valor as EntityReference;
            if (referencia != null && !almacen.Existe(referencia.LogicalName, referencia.Id)) almacen.Sembrar(new Entity(referencia.LogicalName, referencia.Id));
        }

        private static Guid Sembrada(OrganizationServiceEnMemoria almacen, string tabla)
        {
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(tabla, id));
            return id;
        }

        // ---- Tablas propias: tipo, destino, largo, choice ----

        [Fact]
        public void PA_ESQ_00_ElDiccionarioTraeLasSieteTablasPropias_VERDE_A_PROPOSITO()
        {
            Assert.Equal(7, Diccionario.Value.Count);
            // Cierre del lote 3 (guarda): el conteo es EXACTO y por tabla. Salió de contar a mano las filas de cada tabla de la sección 4 del documento (sin las de fórmula ni archivo) más
            // los lookups de la sección 6.2 de cada hija, sin pasar por el doble ni por este lector; 89 en total al 2026-10-14. Si el diccionario cambia, esta lista cambia con él, a propósito.
            var esperadas = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "sanic_carp_tbl_cliente", 5 }, { "sanic_carp_tbl_conversacion", 18 }, { "sanic_carp_tbl_arreglo", 41 }, { "sanic_carp_tbl_cuota", 7 },
                { "sanic_carp_tbl_versionplantilla", 6 }, { "sanic_carp_tbl_mapeocelda", 7 }, { "sanic_carp_tbl_generacionformato", 5 }
            };
            var reales = Escribibles().GroupBy(c => c.Tabla).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

            Assert.Equal(esperadas.OrderBy(p => p.Key, StringComparer.Ordinal), reales.OrderBy(p => p.Key, StringComparer.Ordinal));
            Assert.Equal(89, Escribibles().Count());
        }

        [Theory]
        [MemberData(nameof(ColumnasEscribibles))]
        public void PA_ESQ_01_UnValorDelTipoCorrectoSeAceptaPorCreateYPorUpdate_YSeConservaElTipo(string tabla, string columna)
        {
            var valor = ValoresDeEjemplo.Valido(Columna(tabla, columna));
            var almacen = new OrganizationServiceEnMemoria();
            SembrarDestinoDe(almacen, valor);   // lote 3b (integridad referencial, 0x80040217): Create y Update exigen que el destino del lookup exista

            var creada = almacen.Create(new Entity(tabla) { [columna] = valor });
            var existente = Sembrada(almacen, tabla);
            almacen.Update(new Entity(tabla, existente) { [columna] = valor });

            Assert.Equal(valor.GetType(), almacen.Leer(tabla, creada)[columna].GetType());
            Assert.Equal(valor.GetType(), almacen.Leer(tabla, existente)[columna].GetType());
        }

        [Theory]
        [MemberData(nameof(ColumnasConVariantesErroneas))]
        public void PA_ESQ_02_UnValorDeOtroTipoSeRechazaPorCreateYPorUpdate_ConElErrorCodeMedidoEnDev(string tabla, string columna, string variante)
        {
            var malo = ValoresDeEjemplo.Erroneo(Columna(tabla, columna), variante);
            var codigo = ValoresDeEjemplo.CodigoEsperado(Columna(tabla, columna), variante);
            var almacen = new OrganizationServiceEnMemoria();

            // lote 3b, medido en Dev: ya no ArgumentException con la tabla y la columna; FaultException con el ErrorCode medido (el mensaje real de varios no nombra la columna: 'Incorrect attribute value type System.String').
            var deCreate = Falla(() => almacen.Create(new Entity(tabla) { [columna] = malo }));
            var existente = Sembrada(almacen, tabla);
            var deUpdate = Falla(() => almacen.Update(new Entity(tabla, existente) { [columna] = malo }));

            ApoyoMedido.AssertFalla(deCreate, codigo, null, tabla + "." + columna + " " + variante + " Create");
            ApoyoMedido.AssertFalla(deUpdate, codigo, null, tabla + "." + columna + " " + variante + " Update");
            Assert.Single(almacen.Todas(tabla));
            Assert.False(almacen.Leer(tabla, existente).Contains(columna), "la escritura rechazada no dejó nada");
        }

        [Theory]
        [MemberData(nameof(ColumnasDeTextoConLargo))]
        public void PA_ESQ_03_UnTextoDelLargoMaximoSeAcepta_YUnoMasLargoSeRechaza(string tabla, string columna, int largo)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, tabla);

            almacen.Update(new Entity(tabla, existente) { [columna] = new string('z', largo) });
            // lote 3b, medido en Dev: 0x80044331 (texto más largo que la columna), no ArgumentException.
            var deCreate = Falla(() => almacen.Create(new Entity(tabla) { [columna] = new string('z', largo + 1) }));
            var deUpdate = Falla(() => almacen.Update(new Entity(tabla, existente) { [columna] = new string('y', largo + 1) }));
            ApoyoMedido.AssertFalla(deCreate, CodigosDeFalla.TextoDemasiadoLargo);
            ApoyoMedido.AssertFalla(deUpdate, CodigosDeFalla.TextoDemasiadoLargo);

            Assert.Contains(columna, deCreate.Message, StringComparison.Ordinal);
            Assert.Contains(columna, deUpdate.Message, StringComparison.Ordinal);
            Assert.Equal(new string('z', largo), almacen.Leer(tabla, existente)[columna]);
        }

        [Theory]
        [MemberData(nameof(TablasPropias))]
        public void PA_ESQ_04_UnaColumnaQueNoExisteSeRechaza_AunSiEsElTipeoDeUnaReal(string tabla)
        {
            var real = Diccionario.Value[tabla].First(c => c.Clase != ClaseDeColumna.Formula && c.Clase != ClaseDeColumna.Archivo).Nombre;
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, tabla);

            foreach (var inventada in new[] { "sanic_columna_inexistente", real.Substring(0, real.Length - 1) + "9" })
            {
                // lote 3b, medido en Dev: 0x80041103 (columna inexistente), no ArgumentException.
                var deCreate = Falla(() => almacen.Create(new Entity(tabla) { [inventada] = "zzf" }));
                var deUpdate = Falla(() => almacen.Update(new Entity(tabla, existente) { [inventada] = "zzf" }));
                ApoyoMedido.AssertFalla(deCreate, CodigosDeFalla.ColumnaInexistente);
                ApoyoMedido.AssertFalla(deUpdate, CodigosDeFalla.ColumnaInexistente);
                Assert.Contains(inventada, deCreate.Message, StringComparison.Ordinal);
                Assert.Contains(inventada, deUpdate.Message, StringComparison.Ordinal);
            }
            Assert.Single(almacen.Todas(tabla));
        }

        [Fact]
        public void PA_ESQ_05_LasColumnasDePlataformaDeUnaTablaPropiaTienenSuTipo_StatecodeStatuscodeOpcionYPropietarioLookupAUsuarioOTeam()
        {
            var almacen = new OrganizationServiceEnMemoria();
            const string tabla = "sanic_carp_tbl_conversacion";

            // lote 3b, medido en Dev: faltas de tipo = 0x80040203 / 0x80044354 (extensión declarada para statecode, statuscode y ownerid: no se midió un int ahí), ownerid a otra tabla = 0x8004049E,
            // y el propietario tiene que EXISTIR (0x80040217): se siembran el team y el usuario.
            var team = Guid.NewGuid();
            var usuario = Guid.NewGuid();
            almacen.Sembrar(new Entity("team", team), new Entity("systemuser", usuario));
            ApoyoMedido.AssertFalla(Falla(() => almacen.Create(new Entity(tabla) { ["statecode"] = 0 })), CodigosDeFalla.SolicitudInvalida);
            ApoyoMedido.AssertFalla(Falla(() => almacen.Create(new Entity(tabla) { ["statuscode"] = "1" })), CodigosDeFalla.SolicitudInvalida);
            ApoyoMedido.AssertFalla(Falla(() => almacen.Create(new Entity(tabla) { ["ownerid"] = Guid.NewGuid() })), CodigosDeFalla.TipoDeAtributoInvalido);
            var deCliente = Falla(() => almacen.Create(new Entity(tabla) { ["ownerid"] = new EntityReference("sanic_carp_tbl_cliente", Guid.NewGuid()) }));
            ApoyoMedido.AssertFalla(deCliente, CodigosDeFalla.PropietarioInvalido);
            almacen.Create(new Entity(tabla) { ["ownerid"] = new EntityReference("team", team) });
            almacen.Create(new Entity(tabla) { ["ownerid"] = new EntityReference("systemuser", usuario) });
            Assert.Equal(2, almacen.Todas(tabla).Count);
        }

        [Fact]
        public void PA_ESQ_06_UnMoneyOUnEnteroEnUnaColumnaDecimalSeRechaza_InvierteLaPermisividadQueFijabaLaUnitariaUnMoneyEscritoSeGuarda()
        {
            // El unitario DoblesUnitarias.UnMoneyEscritoSeGuardaComoMoney_ElDobleNoAdivinaLaColumna fijaba que el doble guarda un Money en zzf_monto. Con una columna
            // REAL del diccionario (sanic_saldoinicial es decimal, nunca Money, fixture 03) el doble lo rechaza; zzf_ queda libre (PA_ESQ_08).
            var almacen = new OrganizationServiceEnMemoria();
            const string arreglo = "sanic_carp_tbl_arreglo";
            var existente = Sembrada(almacen, arreglo);

            // lote 3b, medido en Dev: 0x80044354 (Invalid Attribute Value Type), no ArgumentException.
            ApoyoMedido.AssertFalla(Falla(() => almacen.Create(new Entity(arreglo) { ["sanic_saldoinicial"] = new Money(5m) })), CodigosDeFalla.TipoDeAtributoInvalido);
            ApoyoMedido.AssertFalla(Falla(() => almacen.Create(new Entity(arreglo) { ["sanic_saldoinicial"] = 5 })), CodigosDeFalla.TipoDeAtributoInvalido);
            ApoyoMedido.AssertFalla(Falla(() => almacen.Update(new Entity(arreglo, existente) { ["sanic_cuotamensual"] = new Money(5m) })), CodigosDeFalla.TipoDeAtributoInvalido);
            almacen.Update(new Entity(arreglo, existente) { ["sanic_cuotamensual"] = 5m });
            Assert.Equal(10, EscalaDe((decimal)almacen.Leer(arreglo, existente)["sanic_cuotamensual"]));
        }

        // ---- Tablas ----

        [Fact]
        public void PA_ESQ_07_UnaTablaQueNoEsDelProyectoNiNativaConocidaNiZzfSeRechazaPorCreateYPorUpdate()
        {
            var almacen = new OrganizationServiceEnMemoria();

            // lote 3b, medido en Dev: 0x80041102 (tabla inexistente), no ArgumentException.
            var deCreate = Falla(() => almacen.Create(new Entity("sanic_carp_tbl_inventada") { ["sanic_nombre"] = "x" }));
            var deUpdate = Falla(() => almacen.Update(new Entity("sanic_carp_tbl_inventada", Fila1) { ["sanic_nombre"] = "x" }));
            var deNombreSinPrefijo = Falla(() => almacen.Create(new Entity("inventada_sin_prefijo")));

            ApoyoMedido.AssertFalla(deCreate, CodigosDeFalla.TablaInexistente, "sanic_carp_tbl_inventada");
            ApoyoMedido.AssertFalla(deUpdate, CodigosDeFalla.TablaInexistente, "sanic_carp_tbl_inventada");
            ApoyoMedido.AssertFalla(deNombreSinPrefijo, CodigosDeFalla.TablaInexistente, "inventada_sin_prefijo");
            Assert.Empty(almacen.Todas("sanic_carp_tbl_inventada"));
        }

        [Fact]
        public void PA_ESQ_08_LasTablasZzfSonLibres_SonLasQueLasPruebasInventanParaMedirElAlmacen_VERDE_HOY()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var id = almacen.Create(new Entity("zzf_cualquiera") { ["zzf_a"] = new Money(1m), ["zzf_b"] = "x", ["zzf_c"] = 3 });
            var consulta = new QueryExpression("zzf_cualquiera") { ColumnSet = new ColumnSet("zzf_que_no_existe") };
            consulta.Criteria.AddCondition("zzf_otra_que_no_existe", ConditionOperator.Null);

            Assert.Equal(new Money(1m).Value, ((Money)almacen.Leer("zzf_cualquiera", id)["zzf_a"]).Value);
            Assert.Single(almacen.RetrieveMultiple(consulta).Entities);
        }

        // ---- Tablas nativas ----

        [Fact]
        public void PA_ESQ_09_ElCorreoAceptaSusColumnasDeLosFixturesYLasDeLaSeccion5_ConLosTiposDelDiccionario()
        {
            var almacen = new OrganizationServiceEnMemoria();

            almacen.Create(new Entity("email") { ["subject"] = "ZZF-PRUEBA", ["messageid"] = "<zzf@example.com>", ["directioncode"] = true, ["regardingobjectid"] = new EntityReference("sanic_carp_tbl_conversacion", Guid.NewGuid()) });
            var oficial = Guid.NewGuid();
            almacen.Sembrar(new Entity("systemuser", oficial));   // lote 3b: el destino del lookup tiene que existir (0x80040217)
            almacen.Create(new Entity("email") { ["sanic_tiemporespuesta"] = 15, ["sanic_respondidoporid"] = new EntityReference("systemuser", oficial) });
            var deColumna = Falla(() => almacen.Create(new Entity("email") { ["sanic_inventada"] = "x" }));
            var deTipo = Falla(() => almacen.Create(new Entity("email") { ["sanic_tiemporespuesta"] = 15m }));
            var deLookup = Falla(() => almacen.Create(new Entity("email") { ["sanic_respondidoporid"] = new EntityReference("team", Guid.NewGuid()) }));

            // lote 3b, medido en Dev: columna 0x80041103, tipo 0x80040203, lookup a otra tabla 0x80040217 (nombra la tabla destino declarada).
            ApoyoMedido.AssertFalla(deColumna, CodigosDeFalla.ColumnaInexistente, "sanic_inventada");
            ApoyoMedido.AssertFalla(deTipo, CodigosDeFalla.SolicitudInvalida, "Incorrect attribute value type System.Decimal");
            ApoyoMedido.AssertFalla(deLookup, CodigosDeFalla.FilaInexistente, "Entity 'systemuser' With Id = ");
            Assert.Equal(2, almacen.Todas("email").Count);
        }

        [Theory]
        [InlineData("team", "name", "nombre")]
        [InlineData("queue", "emailaddress", "correo")]
        [InlineData("systemuser", "fullname", "nombrecompleto")]
        [InlineData("environmentvariabledefinition", "schemaname", "schema")]
        [InlineData("environmentvariablevalue", "value", "valor")]
        public void PA_ESQ_10_UnaTablaNativaConMuestraAceptaLasColumnasDeSusFixturesYRechazaLasDemas(string tabla, string deFixture, string inventada)
        {
            var almacen = new OrganizationServiceEnMemoria();

            almacen.Create(new Entity(tabla) { [deFixture] = "ZZF-PRUEBA" });
            var error = Falla(() => almacen.Create(new Entity(tabla) { [inventada] = "ZZF-PRUEBA" }));   // lote 3b, medido en Dev: 0x80041103

            ApoyoMedido.AssertFalla(error, CodigosDeFalla.ColumnaInexistente, inventada);
            Assert.Contains("'" + tabla + "' entity", error.Detail.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA_ESQ_11_UnaTablaNativaSinMuestraQueElDisenoNombraSeAceptaSinValidarSusColumnas_VERDE_HOY()
        {
            // Sin muestra de sus columnas (businessunit no se exportó): se acepta la tabla y cualquier columna; agregarla al esquema es una tarea de la próxima exportación.
            var almacen = new OrganizationServiceEnMemoria();

            var id = almacen.Create(new Entity("businessunit") { ["name"] = "ZZF BU", ["cualquier_columna"] = 1 });

            Assert.Equal("ZZF BU", almacen.Leer("businessunit", id)["name"]);
        }

        [Theory]
        [MemberData(nameof(FilasRealesDeLosFixtures))]
        public void PA_ESQ_12_TodasLasColumnasQueDevolvioDevSeAceptanAlLeer_ElEsquemaNoEsMasEstrictoQueLaPlataforma_VERDE_HOY(string archivo, string ruta)
        {
            var real = LectorDeFixtures.Abrir(archivo).Entidad(ruta.Split('/'));
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(real);

            var leida = almacen.Retrieve(real.LogicalName, real.Id, new ColumnSet(real.Attributes.Keys.ToArray()));

            Assert.Equal(real.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(), leida.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        public static IEnumerable<object[]> FilasRealesDeLosFixtures()
        {
            return DobleContraRespuestasRealesAceptacion.FilasReales();
        }

        // ---- Lecturas ----

        private static OrganizationServiceEnMemoria ConDosArreglos()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila("sanic_carp_tbl_arreglo", Fila1, "sanic_nombre", "ARR-000001"), Fila("sanic_carp_tbl_arreglo", Fila2, "sanic_nombre", "ARR-000002"));
            return almacen;
        }

        [Fact]
        public void PA_ESQ_13_UnaCondicionNullSobreUnaColumnaMalEscritaLanzaEnVezDeDevolverTodasLasFilas()
        {
            var almacen = ConDosArreglos();
            var consulta = new QueryExpression("sanic_carp_tbl_arreglo") { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("sanic_numeroarregl0", ConditionOperator.Null);

            var error = Falla(() => almacen.RetrieveMultiple(consulta));   // lote 3b, medido en Dev: 0x80041103

            ApoyoMedido.AssertFalla(error, CodigosDeFalla.ColumnaInexistente, "sanic_numeroarregl0");
        }

        [Fact]
        public void PA_ESQ_14_UnaColumnaQueNoExisteEnElColumnSetUnaCondicionOUnOrdenLanzaSiLaTablaEsDelEsquema()
        {
            var almacen = ConDosArreglos();
            QueryExpression Consulta() { return new QueryExpression("sanic_carp_tbl_arreglo") { ColumnSet = new ColumnSet() }; }
            var enColumnas = Consulta();
            enColumnas.ColumnSet = new ColumnSet("sanic_nombre", "sanic_columna_inexistente");
            var enCondicion = Consulta();
            enCondicion.Criteria.AddCondition("sanic_columna_inexistente", ConditionOperator.Equal, "x");
            var enFiltroAnidado = Consulta();
            enFiltroAnidado.Criteria.AddFilter(LogicalOperator.Or).AddCondition("sanic_columna_inexistente", ConditionOperator.NotNull);
            var enOrden = Consulta();
            enOrden.AddOrder("sanic_columna_inexistente", OrderType.Ascending);

            foreach (var consulta in new[] { enColumnas, enCondicion, enFiltroAnidado, enOrden })
            {
                var error = Falla(() => almacen.RetrieveMultiple(consulta));   // lote 3b, medido en Dev: 0x80041103
                ApoyoMedido.AssertFalla(error, CodigosDeFalla.ColumnaInexistente, "sanic_columna_inexistente");
            }
            var deRetrieve = Falla(() => almacen.Retrieve("sanic_carp_tbl_arreglo", Fila1, new ColumnSet("sanic_columna_inexistente")));
            ApoyoMedido.AssertFalla(deRetrieve, CodigosDeFalla.ColumnaInexistente, "sanic_columna_inexistente");
        }

        [Fact]
        public void PA_ESQ_15_UnaConsultaSobreUnaTablaQueNoExisteLanza_PeroLasConsultasConColumnasDelEsquemaNoLanzanAunConLaTablaVacia()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var inexistente = new QueryExpression("sanic_carp_tbl_inventada") { ColumnSet = new ColumnSet(true) };
            var valida = new QueryExpression("sanic_carp_tbl_conversacion") { ColumnSet = new ColumnSet("sanic_nombre", "statecode", "ownerid", "createdon") };
            valida.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.NotNull);
            valida.AddOrder("sanic_nombre", OrderType.Ascending);

            var error = Falla(() => almacen.RetrieveMultiple(inexistente));   // lote 3b, medido en Dev: 0x80041102

            ApoyoMedido.AssertFalla(error, CodigosDeFalla.TablaInexistente, "sanic_carp_tbl_inventada");
            Assert.Empty(almacen.RetrieveMultiple(valida).Entities);
        }
    }
}
