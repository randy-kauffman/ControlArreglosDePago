using System;
using System.Collections.Generic;
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
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoMedido;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Cierre del lote 3b</b> (hallazgos H1 a H7 de la revisión adversarial del commit 774cdcd; diseno/03 4.5, párrafo "Cierre del lote 3b (2026-10-15)"). Parche acotado, sin otra vuelta completa:
    /// pruebas escritas primero y vistas en rojo (o marcadas <c>VERDE_A_PROPOSITO</c> con su motivo). Principio que sigue mandando: <b>lo no medido lanza o no se inventa</b>; por eso H1 fija SOLO lo que
    /// <c>spikes/dobles/salida/18-texto-y-esquema.json</c> midió y el resto se escala.
    /// <para>
    /// <b>Qué dice cada prueba.</b> H1: <c>Equal ''</c> devuelve la fila cuyo texto es solo espacios (medido). H2: toda excepción que escapa de una escritura dentro de una transacción abierta la aborta; los
    /// chequeos de uso previos y las lecturas no. H3: el privilegio de lectura es uno solo (<c>prvRead</c>). H4: una hora <c>Local</c> en una condición se convierte con <c>ZonaLocal</c>. H5: <c>Delete</c> de una
    /// tabla inexistente falla <c>0x80041102</c> (extrapolación consistente; Dev no lo midió para <c>Delete</c>). H6: una sola fuente para los motivos de <c>email</c>. H7: el mensaje de REL-10 a REL-19 dice
    /// que no está medido.
    /// </para>
    /// </summary>
    public class CierreDelLote3bAceptacion
    {
        private const string Externa = "zzf_externa";
        private const string Hija = "zzf_hija";
        private const string MensajeDeAbortada = "ISV code reduced the open transaction count. Custom plug-ins should not catch exceptions from OrganizationService calls and continue processing.";
        private const string MensajeDelStepAnidado = "ZZF: el step anidado rechaza el alta";

        /// <summary>Un <c>statecode</c> que las tablas propias no tienen (solo 0 y 1): el <c>Update</c> con él es no medido y lanza <see cref="NotSupportedException"/>.</summary>
        private const int EstadoQueLaTablaNoTiene = 2;

        private static IOrganizationService Elevado(LlamadaCapturada llamada)
        {
            return ((IOrganizationServiceFactory)llamada.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);
        }

        private static FaultException<OrganizationServiceFault> Atrapar(Action accion)
        {
            try
            {
                accion();
                return null;
            }
            catch (FaultException<OrganizationServiceFault> falla)
            {
                return falla;
            }
        }

        private static OperacionDeDatos Operacion(string nombre)
        {
            return (OperacionDeDatos)Enum.Parse(typeof(OperacionDeDatos), nombre);
        }

        // =====================================================================================================================
        // H1: la igualdad de texto (SOLO lo medido)
        // =====================================================================================================================

        private static List<Guid> IdsDeAsunto(OrganizationServiceEnMemoria almacen, ConditionOperator operador, params object[] valores)
        {
            var consulta = new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("sanic_asunto", operador, valores);
            return almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).OrderBy(g => g).ToList();
        }

        [Fact]
        public void CL3B_H1_01_EqualConLaCadenaVaciaDevuelveLaFilaCuyoTextoEsSoloEspacios_YNoLasDemas()
        {
            // 18-texto-y-esquema.json punto_d_cadena_vacia: "Update de sanic_asunto a '   ' ... relectura" => columna_presente true, largo 3; y "consulta: sanic_asunto Equal ''" => n 1, claves [conv-Ce]
            // (la fila de solo espacios). RESULTADO.md §2 fila (d): "Equal '' la devuelve (igualdad que ignora los espacios finales)". SOLO esto está medido: un texto con espacios finales distinto de
            // vacío (por ejemplo "ZZF-A ") NO se midió y por eso no se fija acá (se escala, 4.5 cierre del lote 3b). Mata: la comparación exacta de cadenas (hoy "   " no es igual a "").
            var almacen = new OrganizationServiceEnMemoria();
            var soloEspacios = almacen.Create(new Entity(Conversacion) { ["sanic_asunto"] = "   " });
            almacen.Create(new Entity(Conversacion) { ["sanic_asunto"] = "ZZF-A" });
            almacen.Create(new Entity(Conversacion));

            var devueltas = IdsDeAsunto(almacen, ConditionOperator.Equal, string.Empty);

            Assert.Equal(new[] { soloEspacios }, devueltas);
        }

        [Fact]
        public void CL3B_H1_02_UnaCadenaDeSoloEspaciosSeGuardaTalCual_NoEsNula_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: ya era así (FormaDeLaPlataforma.Guardar solo borra la cadena de largo 0). 18-texto-y-esquema.json punto_d: "   " se relee presente con largo 3 y la consulta Null NO
            // la devuelve (la lista de Null no trae conv-Ce). Mata: que el arreglo de H1 recorte el espacio al ESCRIBIR (la fila pasaría a ser nula).
            var almacen = new OrganizationServiceEnMemoria();
            var soloEspacios = almacen.Create(new Entity(Conversacion) { ["sanic_asunto"] = "   " });
            var conTexto = almacen.Create(new Entity(Conversacion) { ["sanic_asunto"] = "ZZF-A" });
            var sinAsunto = almacen.Create(new Entity(Conversacion));

            var guardado = (string)almacen.Retrieve(Conversacion, soloEspacios, new ColumnSet("sanic_asunto"))["sanic_asunto"];

            Assert.Equal(3, guardado.Length);
            Assert.Equal(new[] { sinAsunto }, IdsDeAsunto(almacen, ConditionOperator.Null));
            Assert.Equal(new[] { soloEspacios, conTexto }.OrderBy(g => g).ToList(), IdsDeAsunto(almacen, ConditionOperator.NotNull));
        }

        // =====================================================================================================================
        // H2: alcance del aborto de la transacción
        // =====================================================================================================================

        private sealed class Corrida
        {
            public FabricaDeServiciosSimulada Fabrica;
            public Guid IdCliente;
            public Guid IdConversacion;
            public Guid IdUsuario;
            public string TablaDelUsuario;
            public Exception Atrapada;
            public FaultException<OrganizationServiceFault> DeLaSiguiente;
            public FaultException<OrganizationServiceFault> AlReleer;
            public bool TerminoSinLanzar;
        }

        /// <summary>
        /// El plugin de la sonda del spike (tx0 a tx4), generalizado: crea A (Fila3), intenta <paramref name="operacion"/> y ATRAPA CUALQUIER excepción, escribe otra fila, relee A y termina sin lanzar.
        /// Sembrados: un cliente, una conversación abierta y un usuario con una hija en REL-10 (para el <c>Delete</c> no medido). Un step anidado de PreOperation sobre <c>zzf_hija</c> rechaza el alta con
        /// <see cref="InvalidPluginExecutionException"/>.
        /// </summary>
        private static Corrida Armar(Action<Corrida, IOrganizationService> operacion)
        {
            var c = new Corrida { Fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria()) };
            var pipeline = new PipelineSimulado(c.Fabrica);
            var almacen = c.Fabrica.Almacen;
            var abierta = Estados.De(EstadoDeConversacion.Abierta);
            c.IdCliente = UnCliente(almacen);
            c.IdConversacion = Sembrada(almacen, Conversacion, "statecode", new OptionSetValue(abierta.StateCode), "statuscode", new OptionSetValue(abierta.StatusCode));
            var relacion = CierreDelLote3Aceptacion.RelacionesConPadreUsuario().First();
            c.TablaDelUsuario = (string)relacion[2];
            c.IdUsuario = Sembrada(almacen, c.TablaDelUsuario);
            almacen.Sembrar(new Entity((string)relacion[3], Guid.NewGuid()) { [(string)relacion[4]] = new EntityReference(c.TablaDelUsuario, c.IdUsuario) });

            pipeline.Registrar(new PluginEspia("anidado", accion: l => { throw new InvalidPluginExecutionException(MensajeDelStepAnidado); }), "Create", Hija, EtapaDeStep.PreOperation);
            pipeline.Registrar(new PluginEspia("sonda", accion: llamada =>
            {
                var servicio = Elevado(llamada);
                servicio.Create(Zzf(Fila3));
                try
                {
                    operacion(c, servicio);
                }
                catch (Exception error)
                {
                    c.Atrapada = error;
                }
                c.DeLaSiguiente = Atrapar(() => servicio.Create(Zzf(Guid.NewGuid())));
                c.AlReleer = Atrapar(() => servicio.Retrieve(TablaZzf, Fila3, new ColumnSet()));
                c.TerminoSinLanzar = true;
            }), "Create", Externa, EtapaDeStep.PreOperation);
            return c;
        }

        private static FaultException<OrganizationServiceFault> CrearLaExterna(Corrida c)
        {
            return Atrapar(() => c.Fabrica.CreateOrganizationService(null).Create(new Entity(Externa, Fila1)));
        }

        private static void AssertAbortada(Corrida c, FaultException<OrganizationServiceFault> externa)
        {
            Assert.True(c.TerminoSinLanzar, "el plugin terminó sin lanzar");
            AssertFalla(c.DeLaSiguiente, CodigosDeFalla.TransaccionAbortada, MensajeDeAbortada, "la escritura siguiente a la excepción");
            AssertFalla(c.AlReleer, CodigosDeFalla.FilaInexistente, null, "releer la fila creada antes de la excepción: ya se revirtió");
            AssertFalla(externa, CodigosDeFalla.TransaccionAbortada, MensajeDeAbortada, "la operación externa");
            Assert.False(c.Fabrica.Almacen.Existe(Externa, Fila1), "la fila externa no quedó");
            Assert.False(c.Fabrica.Almacen.Existe(TablaZzf, Fila3), "la fila anidada creada antes de la excepción no quedó");
            Assert.Empty(c.Fabrica.Almacen.Todas(TablaZzf));
            Assert.True(c.Fabrica.Almacen.Existe(Cliente, c.IdCliente), "lo sembrado antes sigue");
        }

        private static void AssertConfirmada(Corrida c, FaultException<OrganizationServiceFault> externa)
        {
            Assert.True(c.TerminoSinLanzar, "el plugin terminó sin lanzar");
            Assert.Null(externa);
            Assert.Null(c.DeLaSiguiente);
            Assert.Null(c.AlReleer);
            Assert.True(c.Fabrica.Almacen.Existe(Externa, Fila1), "la fila externa quedó");
            Assert.True(c.Fabrica.Almacen.Existe(TablaZzf, Fila3), "la fila creada antes quedó");
            Assert.Equal(2, c.Fabrica.Almacen.Todas(TablaZzf).Count);
        }

        [Theory]
        [InlineData("create-argumentexception", typeof(ArgumentException))]
        [InlineData("update-argumentexception", typeof(ArgumentException))]
        [InlineData("updaterequest-argumentexception", typeof(ArgumentException))]
        [InlineData("create-notsupported", typeof(NotSupportedException))]
        [InlineData("update-notsupported", typeof(NotSupportedException))]
        [InlineData("delete-notsupported", typeof(NotSupportedException))]
        [InlineData("assign-fault", typeof(FaultException<OrganizationServiceFault>))]
        [InlineData("step-anidado-invalidplugin", typeof(InvalidPluginExecutionException))]
        public void CL3B_H2_01_ToDaExcepcionQueEscapaDeUnaEscrituraDentroDeLaTransaccionLaAborta_AunLaDelPropioDobleOLaDeUnStepAnidado_VERDE_A_PROPOSITO(string caso, Type tipoEsperado)
        {
            // VERDE_A_PROPOSITO: OrganizationServiceEnMemoria.EnEscritura ya hace 'catch' SIN filtro, así que todo lo que escapa de Create, Update, Delete, Assign o Execute de escritura aborta.
            // La prueba fija la decisión (4.5, cierre del lote 3b, H2) para que nadie la acote a FaultException. Respaldo: RESULTADO.md §2 fila (g) midió que TODA escritura rechazada por la plataforma
            // aborta (K-01, fila inexistente, lookup inexistente); los errores de uso del doble representan rechazos o comportamientos no medidos y lo conservador es abortar.
            // Mata: 'catch (FaultException<OrganizationServiceFault>)' en EnEscritura (el ArgumentException, el NotSupportedException o el InvalidPluginExecutionException del step anidado no abortarían).
            var c = Armar((corrida, s) => EscribirQueEscapa(caso, corrida, s));

            var externa = CrearLaExterna(c);

            Assert.NotNull(c.Atrapada);
            Assert.IsType(tipoEsperado, c.Atrapada);
            AssertAbortada(c, externa);
        }

        private static void EscribirQueEscapa(string caso, Corrida c, IOrganizationService s)
        {
            switch (caso)
            {
                case "create-argumentexception": s.Create(new Entity(Conversacion) { ["statecode"] = null }); break;
                case "update-argumentexception": s.Update(new Entity(Cliente, c.IdCliente) { ["statecode"] = null }); break;
                case "updaterequest-argumentexception": s.Execute(new UpdateRequest { Target = new Entity(Cliente, c.IdCliente) { ["statecode"] = null } }); break;
                case "create-notsupported": s.Create(new Entity(Correo) { ["statuscode"] = new OptionSetValue(Estados.StatusCorreoEnviado) }); break;
                case "update-notsupported": s.Update(new Entity(Conversacion, c.IdConversacion) { ["statecode"] = new OptionSetValue(EstadoQueLaTablaNoTiene) }); break;
                case "delete-notsupported": s.Delete(c.TablaDelUsuario, c.IdUsuario); break;
                case "assign-fault": s.Execute(new AssignRequest { Target = new EntityReference(Conversacion, c.IdConversacion), Assignee = new EntityReference(Cliente, c.IdCliente) }); break;
                case "step-anidado-invalidplugin": s.Create(new Entity(Hija)); break;
                default: throw new ArgumentException("caso desconocido: " + caso);
            }
        }

        [Fact]
        public void CL3B_H2_02_LaInvalidPluginExecutionExceptionDeUnStepAnidadoLlegaTalCualAlPluginExterno_AntesDeAbortar_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: control de CL3B_H2_01 (caso step-anidado): lo que atrapa el plugin externo es la MISMA excepción del step, con su mensaje, no una FaultException envuelta.
            // Mata: envolver la excepción del step en una falla de plataforma (el plugin externo ya no podría distinguirla).
            var c = Armar((corrida, s) => s.Create(new Entity(Hija)));

            CrearLaExterna(c);

            var error = Assert.IsType<InvalidPluginExecutionException>(c.Atrapada);
            Assert.Equal(MensajeDelStepAnidado, error.Message);
        }

        [Theory]
        [InlineData("create-null", typeof(ArgumentNullException))]
        [InlineData("create-sin-tabla", typeof(ArgumentException))]
        [InlineData("update-null", typeof(ArgumentNullException))]
        [InlineData("update-sin-id", typeof(ArgumentException))]
        [InlineData("updaterequest-sin-target", typeof(ArgumentNullException))]
        [InlineData("updaterequest-rowversion-ausente", typeof(InvalidOperationException))]
        [InlineData("delete-sin-tabla", typeof(ArgumentException))]
        [InlineData("assign-sin-assignee", typeof(ArgumentException))]
        public void CL3B_H2_03_UnChequeoDeUsoPrevioNoAbortaLaTransaccion_VERDE_A_PROPOSITO(string caso, Type tipoEsperado)
        {
            // VERDE_A_PROPOSITO: esos chequeos ocurren ANTES de entrar a EnEscritura. Son errores de quien llama (entidad nula, Id vacío, IfRowVersionMatches sin RowVersion): no representan un rechazo
            // de la plataforma, así que no cuentan como "escritura rechazada". Decisión del arquitecto (4.5, cierre del lote 3b, H2). Mata: mover esos chequeos adentro del 'try' de EnEscritura.
            var c = Armar((corrida, s) => UsoIncorrecto(caso, corrida, s));

            var externa = CrearLaExterna(c);

            Assert.NotNull(c.Atrapada);
            Assert.IsType(tipoEsperado, c.Atrapada);
            AssertConfirmada(c, externa);
        }

        private static void UsoIncorrecto(string caso, Corrida c, IOrganizationService s)
        {
            switch (caso)
            {
                case "create-null": s.Create(null); break;
                case "create-sin-tabla": s.Create(new Entity()); break;
                case "update-null": s.Update(null); break;
                case "update-sin-id": s.Update(new Entity(Cliente)); break;
                case "updaterequest-sin-target": s.Execute(new UpdateRequest()); break;
                case "updaterequest-rowversion-ausente": s.Execute(new UpdateRequest { Target = new Entity(Cliente, c.IdCliente), ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches }); break;
                case "delete-sin-tabla": s.Delete(string.Empty, Fila1); break;
                case "assign-sin-assignee": s.Execute(new AssignRequest { Target = new EntityReference(Conversacion, c.IdConversacion) }); break;
                default: throw new ArgumentException("caso desconocido: " + caso);
            }
        }

        [Theory]
        [InlineData("retrieve-columna-inexistente", typeof(FaultException<OrganizationServiceFault>))]
        [InlineData("retrievemultiple-like-no-simulado", typeof(NotSupportedException))]
        [InlineData("retrievemultiple-top-con-paginacion", typeof(InvalidOperationException))]
        public void CL3B_H2_04_UnaLecturaFallidaAtrapadaNuncaAborta_AunSiLaExcepcionEsDelPropioDoble_VERDE_A_PROPOSITO(string caso, Type tipoEsperado)
        {
            // VERDE_A_PROPOSITO: RESULTADO.md §2 fila (g), control tx2: un Retrieve atrapado no aborta. Se extiende a las lecturas con errores del propio doble (NotSupportedException, InvalidOperationException).
            // Mata: que el arreglo de H2 meta las lecturas dentro de EnEscritura.
            var c = Armar((corrida, s) => LecturaFallida(caso, corrida, s));

            var externa = CrearLaExterna(c);

            Assert.NotNull(c.Atrapada);
            Assert.IsType(tipoEsperado, c.Atrapada);
            AssertConfirmada(c, externa);
        }

        private static void LecturaFallida(string caso, Corrida c, IOrganizationService s)
        {
            switch (caso)
            {
                case "retrieve-columna-inexistente":
                    s.Retrieve(Cliente, c.IdCliente, new ColumnSet("sanic_noexiste"));
                    break;
                case "retrievemultiple-like-no-simulado":
                    var conLike = new QueryExpression(Cliente) { ColumnSet = new ColumnSet() };
                    conLike.Criteria.AddCondition("sanic_nombre", ConditionOperator.Like, "ZZF%");
                    s.RetrieveMultiple(conLike);
                    break;
                case "retrievemultiple-top-con-paginacion":
                    var conTopYPagina = new QueryExpression(Cliente) { ColumnSet = new ColumnSet(), TopCount = 1, PageInfo = new PagingInfo { Count = 1, PageNumber = 1 } };
                    s.RetrieveMultiple(conTopYPagina);
                    break;
                default: throw new ArgumentException("caso desconocido: " + caso);
            }
        }

        [Fact]
        public void CL3B_H2_05_EscrituraYConsultasSonElRegistroDeLoOcurrido_ElAbortoNoLosRevierte_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: Restaurar() devuelve filas y archivos, NO los registros (Registros.cs, "Transacción" de 4.5). Se fija para que el lote 4 no se sorprenda: tras una transacción abortada la
            // fila creada ya no existe pero Escrituras SIGUE mostrando el Create que la hizo (es lo que el código intentó y la plataforma aplicó antes de abortar) y Consultas muestra la lectura que falló.
            // Lo que no se registra nunca es la escritura que falló (contrato vigente: "NO incluye las que fallaron"). Mata: limpiar o recortar los registros al abortar o al Restaurar.
            var c = Armar((corrida, s) => s.Create(new Entity(Conversacion) { ["statecode"] = null }));

            var externa = CrearLaExterna(c);

            AssertAbortada(c, externa);
            var almacen = c.Fabrica.Almacen;
            Assert.Contains(almacen.Escrituras, e => e.Operacion == OperacionDeDatos.Create && e.Tabla == TablaZzf && e.Id == Fila3);
            Assert.DoesNotContain(almacen.Escrituras, e => e.Tabla == Conversacion && e.Operacion == OperacionDeDatos.Create);
            Assert.Contains(almacen.Consultas, q => q.Operacion == OperacionDeDatos.Retrieve && q.Tabla == TablaZzf && q.Id == Fila3);
        }

        // =====================================================================================================================
        // H3: el privilegio de lectura es uno solo
        // =====================================================================================================================

        [Theory]
        [InlineData("Retrieve")]
        [InlineData("RetrieveMultiple")]
        public void CL3B_H3_01_DenegarElPrivilegioDeLecturaConCualquieraDeLasDosOperacionesNiegaLasDos(string declarada)
        {
            // RESULTADO.md §2 fila (a): "en Retrieve y RetrieveMultiple el privilegio es prvRead…" (22-privilegios.json). En Dev prvRead es UNO solo: no existe 'leer una fila sí y consultar no'.
            // Decisión del arquitecto (4.5, cierre del lote 3b, H3): se declara con cualquiera de las dos y se niegan las dos; sin cambiar firmas ni sumar valores al enum.
            // Mata: guardar (usuario, tabla, operación) tal cual (la operación NO declarada sigue funcionando).
            var almacen = new OrganizationServiceEnMemoria();
            var id = UnCliente(almacen);
            var conversacion = Sembrada(almacen, Conversacion);
            almacen.DenegarPrivilegio(Usuario1, Cliente, Operacion(declarada));
            var persona = almacen.ComoUsuario(Usuario1);

            var deRetrieve = Falla(() => persona.Retrieve(Cliente, id, new ColumnSet()));
            var deConsulta = Falla(() => persona.RetrieveMultiple(new QueryExpression(Cliente) { ColumnSet = new ColumnSet() }));

            AssertFalla(deRetrieve, CodigosDeFalla.SinPrivilegio, "is missing prvRead", "Retrieve");
            AssertFalla(deConsulta, CodigosDeFalla.SinPrivilegio, "is missing prvRead", "RetrieveMultiple");
            // Solo ese usuario y solo esa tabla; las demás operaciones de la tabla siguen.
            Assert.Equal(id, almacen.Retrieve(Cliente, id, new ColumnSet()).Id);
            Assert.Equal(id, almacen.ComoUsuario(Usuario2).Retrieve(Cliente, id, new ColumnSet()).Id);
            Assert.Equal(conversacion, persona.Retrieve(Conversacion, conversacion, new ColumnSet()).Id);
            Assert.Single(persona.RetrieveMultiple(new QueryExpression(Conversacion) { ColumnSet = new ColumnSet() }).Entities);
            persona.Create(new Entity(Cliente));
            Assert.Equal(2, almacen.Todas(Cliente).Count);
        }

        // =====================================================================================================================
        // H4: una condición DateTime Local se convierte con la ZonaLocal del almacén
        // =====================================================================================================================

        /// <summary>Una zona sin horario de verano y con un desfase que casi ningún puesto tiene (UTC+5:45): la prueba no depende de la zona de la máquina.</summary>
        private static TimeZoneInfo ZonaDeNepal()
        {
            return TimeZoneInfo.CreateCustomTimeZone("zzf-utc-mas-5-45", new TimeSpan(5, 45, 0), "ZZF UTC+5:45", "ZZF UTC+5:45");
        }

        private sealed class FechasSembradas
        {
            public OrganizationServiceEnMemoria Almacen;
            public Guid Antes;
            public Guid Justo;
            public Guid Despues;
        }

        /// <summary>Tres filas en UTC: 06:14:59, 06:15:00 y 06:15:01. Las 12:00 hora local de UTC+5:45 son EXACTAMENTE las 06:15:00 UTC.</summary>
        private static FechasSembradas SembrarTresInstantes()
        {
            var f = new FechasSembradas { Almacen = new OrganizationServiceEnMemoria { ZonaLocal = ZonaDeNepal() } };
            f.Antes = Guid.NewGuid();
            f.Justo = Guid.NewGuid();
            f.Despues = Guid.NewGuid();
            f.Almacen.Sembrar(Zzf(f.Antes, "zzf_fecha", Utc(2026, 3, 1, 6, 14, 59)), Zzf(f.Justo, "zzf_fecha", Utc(2026, 3, 1, 6, 15, 0)), Zzf(f.Despues, "zzf_fecha", Utc(2026, 3, 1, 6, 15, 1)));
            return f;
        }

        private static List<Guid> IdsDeFecha(OrganizationServiceEnMemoria almacen, ConditionOperator operador, params object[] valores)
        {
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("zzf_fecha", operador, valores);
            return almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).OrderBy(g => g).ToList();
        }

        [Theory]
        [InlineData("Equal")]
        [InlineData("GreaterEqual")]
        [InlineData("LessThan")]
        [InlineData("In")]
        public void CL3B_H4_01_UnValorDeCondicionConKindLocalSeConvierteConLaZonaLocalDelAlmacen_NoConLaDeLaMaquina(string operador)
        {
            // 11-fechas-escritas.json / RESULTADO.md §2 (2): el cliente real serializa un Local con la zona del PROCESO y la plataforma lo toma como ese instante UTC; el doble centraliza esa zona en
            // ZonaLocal (4.5, lote 3b, punto 7). Una condición es lo mismo que una escritura: mismo Kind, misma zona. Mata: FormaDeLaPlataforma.AUtc(valor) con TimeZoneInfo.Local en la condición.
            var f = SembrarTresInstantes();
            var horaLocal = Local(2026, 3, 1, 12, 0, 0);

            var devueltas = IdsDeFecha(f.Almacen, (ConditionOperator)Enum.Parse(typeof(ConditionOperator), operador), horaLocal);

            switch (operador)
            {
                case "Equal":
                case "In": Assert.Equal(new[] { f.Justo }, devueltas); break;
                case "GreaterEqual": Assert.Equal(new[] { f.Justo, f.Despues }.OrderBy(g => g).ToList(), devueltas); break;
                default: Assert.Equal(new[] { f.Antes }, devueltas); break;
            }
        }

        [Fact]
        public void CL3B_H4_02_UnValorUtcOSinKindNoDependeDeLaZonaLocal_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: Utc va tal cual y Unspecified se toma como UTC (medido, 11-fechas-escritas.json): la zona solo mueve a los Local. Mata: aplicar ZonaLocal a todos los Kind.
            var f = SembrarTresInstantes();

            Assert.Equal(new[] { f.Justo }, IdsDeFecha(f.Almacen, ConditionOperator.Equal, Utc(2026, 3, 1, 6, 15, 0)));
            Assert.Equal(new[] { f.Justo }, IdsDeFecha(f.Almacen, ConditionOperator.Equal, SinKind(2026, 3, 1, 6, 15, 0)));
        }

        // =====================================================================================================================
        // H5: Delete de una tabla inexistente
        // =====================================================================================================================

        [Theory]
        [InlineData("sanic_carp_tbl_noexiste")]
        [InlineData("SANIC_CARP_TBL_CLIENTE")]
        public void CL3B_H5_01_UnDeleteDeUnaTablaInexistenteFalla0x80041102_ComoLasDemasOperaciones(string tabla)
        {
            // Create, Update, Retrieve y RetrieveMultiple de una tabla inexistente (o con el nombre en mayúsculas) fallan 0x80041102 (PA_MED_FAL_01, 18-texto-y-esquema.json). Dev NO midió el Delete:
            // EXTRAPOLACIÓN consistente de la regla (la plataforma resuelve la tabla antes de cualquier operación), declarada en 4.5. Hoy el doble da 0x80040217 (fila inexistente) porque busca la fila
            // antes que la tabla. Mata: validar la fila antes que el nombre de la tabla en Delete.
            var almacen = new OrganizationServiceEnMemoria();
            var cliente = UnCliente(almacen);

            var falla = Falla(() => almacen.Delete(tabla, Guid.NewGuid()));

            AssertFalla(falla, CodigosDeFalla.TablaInexistente, "The entity with a name = '" + tabla + "'");
            Assert.Empty(almacen.Escrituras);
            Assert.True(almacen.Existe(Cliente, cliente), "lo sembrado sigue");
        }

        // =====================================================================================================================
        // H6: una sola fuente para los motivos de email
        // =====================================================================================================================

        private static readonly Regex ConstanteDeEstados = new Regex(@"public\s+const\s+int\s+(?<nombre>(?:StateCorreo|StatusCorreo)(?<resto>[A-Za-z]+))\s*=\s*(?<valor>[0-9]+)\s*;", RegexOptions.CultureInvariant);
        private static readonly Regex DeclaracionDeConstante = new Regex(@"\bconst\s+(?:int|long)\s+(?<nombre>[A-Za-z_]\w*)\s*=\s*(?<valor>-?[0-9_]+)\s*;", RegexOptions.CultureInvariant);

        private static string CodigoSinComentario(string linea)
        {
            if (linea.TrimStart().StartsWith("//", StringComparison.Ordinal) || linea.TrimStart().StartsWith("///", StringComparison.Ordinal)) return string.Empty;
            var comentario = linea.IndexOf(" //", StringComparison.Ordinal);
            return comentario >= 0 ? linea.Substring(0, comentario) : linea;
        }

        /// <summary>Las constantes de <c>email</c> que <c>Estados.cs</c> ya declara (lo lee esta prueba, no el doble): nombre, valor y las palabras que un duplicado llevaría en su nombre.</summary>
        private static List<(string Nombre, int Valor, string[] Palabras)> ConstantesDeCorreoDeEstados()
        {
            var traducciones = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "Completado", new[] { "Completado", "Completed" } },
                { "Enviado", new[] { "Enviado", "Sent" } },
                { "Recibido", new[] { "Recibido", "Received" } }
            };
            var halladas = new List<(string, int, string[])>();
            foreach (var linea in ApoyoDeAceptacion.LineasDelDocumento("src/Sanic.Carp.Plugins/Dominio/Estados.cs"))
            {
                var m = ConstanteDeEstados.Match(CodigoSinComentario(linea));
                if (!m.Success) continue;
                var resto = m.Groups["resto"].Value;
                halladas.Add((m.Groups["nombre"].Value, int.Parse(m.Groups["valor"].Value, CultureInfo.InvariantCulture), traducciones.ContainsKey(resto) ? traducciones[resto] : new[] { resto }));
            }
            Assert.Equal(new[] { "StateCorreoCompletado", "StatusCorreoEnviado", "StatusCorreoRecibido" }, halladas.Select(h => h.Item1).OrderBy(n => n, StringComparer.Ordinal).ToArray());
            return halladas;
        }

        [Fact]
        public void CL3B_H6_01_NingunFuenteDeDoblesOApoyoRedeclaraUnaConstanteDeEmailQueEstadosYaTiene_LECTURA_DEL_FUENTE()
        {
            // Lote 3b, revisión adversarial H6: EstadoDeLasTablas declaraba MotivoCorreoEnviado = 3 y MotivoCorreoRecibido = 4 aunque Estados.StatusCorreoEnviado/Recibido ya existen (V-3). Una sola fuente.
            // La guarda exige EXACTAMENTE eso y nada más (lección del lote 3: una guarda que contradice la regla empuja a evadirla): no prohíbe las constantes de email que Estados NO tiene (el motivo
            // Completed = 2, Open = 0, Canceled = 5…); prohíbe declarar, en cualquier fuente de Dobles/ o Apoyo/, una 'const' cuyo VALOR sea el de una de las tres de Estados y cuyo NOMBRE diga lo mismo
            // (Enviado/Sent, Recibido/Received, Completado/Completed). Mata: volver a declarar la constante duplicada, con ese nombre o con su traducción.
            var deEstados = ConstantesDeCorreoDeEstados();
            var duplicadas = new List<string>();
            foreach (var ruta in EscanerDeLiterales.RutasDeLosFuentes())
            {
                var lineas = ApoyoDeAceptacion.LineasDelDocumento(ruta);
                for (int i = 0; i < lineas.Length; i++)
                {
                    var m = DeclaracionDeConstante.Match(CodigoSinComentario(lineas[i]));
                    if (!m.Success) continue;
                    var nombre = m.Groups["nombre"].Value;
                    var valor = int.Parse(m.Groups["valor"].Value.Replace("_", string.Empty), CultureInfo.InvariantCulture);
                    foreach (var original in deEstados)
                        if (valor == original.Valor && original.Palabras.Any(p => nombre.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0))
                            duplicadas.Add(ruta.Substring(ruta.LastIndexOf('/') + 1) + ":" + (i + 1).ToString(CultureInfo.InvariantCulture) + " " + nombre + " = " + valor.ToString(CultureInfo.InvariantCulture)
                                + " (ya está en Estados." + original.Nombre + ")");
                }
            }

            Assert.True(duplicadas.Count == 0, "constantes de email duplicadas (usar Estados): " + string.Join("; ", duplicadas));
        }

        [Fact]
        public void CL3B_H6_02_EstadoDeLasTablasUsaLasConstantesDeEmailDeEstados_LECTURA_DEL_FUENTE()
        {
            // Complemento de CL3B_H6_01: no basta con no duplicar, el archivo tiene que LEER la fuente. Mata: reemplazar la constante por un literal o por una expresión que no pase por Estados.
            var codigo = string.Join("\n", ApoyoDeAceptacion.LineasDelDocumento("src/Sanic.Carp.Plugins.Tests/Dobles/EstadoDeLasTablas.cs").Select(CodigoSinComentario));

            foreach (var constante in ConstantesDeCorreoDeEstados())
                Assert.True(codigo.Contains("Estados." + constante.Nombre), "EstadoDeLasTablas.cs no usa Estados." + constante.Nombre);
        }

        // =====================================================================================================================
        // H7: el mensaje de REL-10 a REL-19
        // =====================================================================================================================

        [Theory]
        [MemberData(nameof(CierreDelLote3Aceptacion.RelacionesConPadreUsuario), MemberType = typeof(CierreDelLote3Aceptacion))]
        public void CL3B_H7_01_ElMensajeDeRel10ARel19DiceQueNoEstaMedido_PadreSystemuser_YNoPrometeUnaEmulacionPendiente(string id, string nombre, string padre, string hija, string lookup)
        {
            // Lote 3b, revisión adversarial H7: el texto "el doble no emula Restrict/Cascade; pendiente de muestra (pendiente 15)" quedó viejo, porque REL-01 a REL-09 y NAT-01 SÍ se emulan. Lo cierto es
            // que estas diez (padre systemuser) no se midieron: no se crean ni borran usuarios. Sigue siendo NotSupportedException y nombra la relación. Mata: dejar el mensaje del lote 3.
            var almacen = new OrganizationServiceEnMemoria();
            var idDelPadre = Guid.NewGuid();
            var idDeLaHija = Guid.NewGuid();
            almacen.Sembrar(new Entity(padre, idDelPadre));
            almacen.Sembrar(new Entity(hija, idDeLaHija) { [lookup] = new EntityReference(padre, idDelPadre) });

            var error = Assert.Throws<NotSupportedException>(() => almacen.Delete(padre, idDelPadre));

            Assert.Contains(id, error.Message, StringComparison.Ordinal);
            Assert.Contains(nombre, error.Message, StringComparison.Ordinal);
            Assert.Contains("no está medido", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("systemuser", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("emula", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pendiente", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(almacen.Existe(padre, idDelPadre) && almacen.Existe(hija, idDeLaHija), "no se borró nada");
            Assert.Empty(almacen.Escrituras);
        }

        // =====================================================================================================================
        // 4.5: el párrafo de cierre y las correcciones de texto
        // =====================================================================================================================

        private static string Seccion45()
        {
            var texto = string.Join("\n", ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md"));
            var desde = texto.IndexOf("### 4.5 ", StringComparison.Ordinal);
            var hasta = texto.IndexOf("## 5. Lista cerrada", StringComparison.Ordinal);
            Assert.True(desde >= 0 && hasta > desde);
            return texto.Substring(desde, hasta - desde);
        }

        [Fact]
        public void CL3B_DOC_01_LaSeccion45TraeElParrafoDeCierreDelLote3bConLosSieteHallazgosYSusLimites_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: el arquitecto escribe el párrafo en este mismo parche; la prueba lo guarda. Cada hallazgo y cada límite declarado figuran por su palabra clave.
            var seccion = Seccion45();

            Assert.Contains("Cierre del lote 3b (2026-10-15)", seccion, StringComparison.Ordinal);
            foreach (var h in new[] { "H1", "H2", "H3", "H4", "H5", "H6", "H7" })
                Assert.Contains("**" + h, seccion, StringComparison.Ordinal);
            foreach (var palabra in new[] { "LÍMITES CONOCIDOS", "extrapolación", "prvRead", "ZonaLocal", "0x80041102", "no se revierten", "REGISTRO", "PreValidation anidado", "DenegarEscrituraDeColumna", "invariante", "336" })
                Assert.Contains(palabra, seccion, StringComparison.Ordinal);
        }

        [Fact]
        public void CL3B_DOC_02_LaSeccion45YaNoPideNormalizarAFormD_PorqueConGlobalizacionInvariantePorSiSoloNoDescompone_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: corrección de texto del arquitecto en este mismo parche. Con globalización invariante Normalize(FormD) NO descompone; el doble usa Normalize(FormD) MÁS una tabla explícita
            // (TextoSinAcentos). La frase vieja, "debe normalizar a FormD y quitar las marcas", empujaba a una implementación que no funciona en modo invariante.
            var seccion = Seccion45();

            Assert.DoesNotContain("debe normalizar a FormD", seccion, StringComparison.Ordinal);
            Assert.Contains("tabla explícita", seccion, StringComparison.Ordinal);
        }
    }
}
