using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Apoyo;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>Etapa de un step. Los números son los de la plataforma (patrones.md §2, "Selección de stage").</summary>
    internal enum EtapaDeStep
    {
        PreValidation = 10,
        PreOperation = 20,
        PostOperation = 40
    }

    /// <summary>Modo de un step: <c>IPluginExecutionContext.Mode</c> 0 síncrono, 1 asíncrono (solo PostOperation).</summary>
    internal enum ModoDeStep
    {
        Sincrono = 0,
        Asincrono = 1
    }

    /// <summary>Un step registrado en el <see cref="PipelineSimulado"/>: lo que el Plugin Registration Tool guardaría.</summary>
    internal sealed class RegistroDeStep
    {
        public IPlugin Plugin { get; }
        public string Mensaje { get; }
        public string Tabla { get; }
        public EtapaDeStep Etapa { get; }
        public int Orden { get; }
        public ModoDeStep Modo { get; }

        /// <summary>Filtro de atributos (solo cuenta en <c>Update</c>, DC-27); vacío = sin filtro.</summary>
        public IReadOnlyList<string> Filtro { get; }

        /// <summary>Nombre con el que el step recibe su pre-imagen en <c>PreEntityImages</c>; <c>null</c> = sin imagen.</summary>
        public string NombreDeImagenPrevia { get; }

        /// <summary>Columnas de la pre-imagen (vacía = todas las que tenga la fila y el usuario pueda ver).</summary>
        public IReadOnlyList<string> ColumnasDeImagenPrevia { get; }

        public RegistroDeStep(IPlugin plugin, string mensaje, string tabla, EtapaDeStep etapa, int orden, ModoDeStep modo,
            IReadOnlyList<string> filtro, string nombreDeImagenPrevia, IReadOnlyList<string> columnasDeImagenPrevia)
        {
            Plugin = plugin;
            Mensaje = mensaje;
            Tabla = tabla;
            Etapa = etapa;
            Orden = orden;
            Modo = modo;
            Filtro = filtro;
            NombreDeImagenPrevia = nombreDeImagenPrevia;
            ColumnasDeImagenPrevia = columnasDeImagenPrevia;
        }
    }

    /// <summary>Una vez que el pipeline ejecutó un step: con qué contexto y cómo terminó.</summary>
    internal sealed class EjecucionDeStep
    {
        public RegistroDeStep Step { get; }

        /// <summary>El contexto que recibió el step (el mismo objeto; sus propiedades no se tocan después).</summary>
        public ContextoDePluginSimulado Contexto { get; }

        /// <summary>La excepción que lanzó el step, o <c>null</c> si terminó bien.</summary>
        public Exception Excepcion { get; }

        public EjecucionDeStep(RegistroDeStep step, ContextoDePluginSimulado contexto, Exception excepcion)
        {
            Step = step;
            Contexto = contexto;
            Excepcion = excepcion;
        }
    }

    /// <summary>
    /// El pipeline de la plataforma (diseno/03 4.5). Registra instancias de steps y, cuando un servicio del almacén escribe, ejecuta los
    /// steps que corresponden CON EL CONTEXTO QUE LA PLATAFORMA DARÍA. Sirve para las pruebas de cadena (la trampa A-23) y para comprobar
    /// que las escrituras del sistema no son rechazadas por nuestros propios steps.
    /// <para>
    /// <b>Qué dispara.</b> Create, Update, Delete y Assign del almacén (incluido <c>UpdateRequest</c> y <c>AssignRequest</c>). Un step
    /// corre si su mensaje y su tabla coinciden (ordinal); en <c>Update</c> con filtro, solo si el <c>Target</c> trae AL MENOS una de las
    /// columnas del filtro (aunque el valor no cambie, como la plataforma); el filtro no cuenta en Create ni Delete (DC-27).
    /// </para>
    /// <para>
    /// <b>Orden.</b> Etapa (PreValidation, PreOperation, [escritura], PostOperation) y dentro de la etapa por <c>Orden</c> ascendente, y a
    /// igual orden por registro. Los steps reciben como <c>Target</c> una COPIA del que mandó quien llamó, y ESA copia
    /// es la que se escribe: lo que un PreValidation o un PreOperation le agregue o le cambie se escribe (patrones.md §2), sin alterar el objeto del
    /// llamador. En Create, el <c>Id</c> del <c>Target</c>
    /// es el que trajo quien llamó (vacío si no trajo) hasta la escritura, y desde PostOperation es el de la fila creada
    /// (<c>PrimaryEntityId</c> y <c>OutputParameters["id"]</c> también). Un step de PostOperation recibe el <c>Target</c> que se escribió.
    /// </para>
    /// <para>
    /// <b>Contexto.</b> <c>MessageName</c>, <c>Stage</c> (10/20/40), <c>Mode</c>, <c>PrimaryEntityName</c>, <c>PrimaryEntityId</c> (el de la
    /// fila afectada; en Create antes de escribir, el que trajo el <c>Target</c>), <c>InputParameters["Target"]</c> (<c>Entity</c>; en Delete
    /// <c>EntityReference</c>; en Assign también "Assignee"), <c>OperationCreatedOn</c> = el reloj del almacén, <c>OrganizationId</c> y
    /// <c>BusinessUnitId</c> del almacén. <b>Identidad:</b> <c>UserId</c> = <c>InitiatingUserId</c> = la identidad del servicio que escribe
    /// (SYSTEM si la escritura salió del servicio de SYSTEM, patrones.md "Elevar a SYSTEM…"). <b>Profundidad:</b> una escritura pedida
    /// desde fuera de un step (o de un plugin lanzado con <c>EjecutarComoPlataforma</c>) tiene <c>Depth</c> 1; una pedida mientras un step
    /// (o ese plugin) se ejecuta, <c>Depth</c> del que se ejecuta + 1 y <c>ParentContext</c> = su contexto. Pasar de profundidad 8
    /// lanza <see cref="InvalidOperationException"/> (la plataforma corta los ciclos). <c>SharedVariables</c> son compartidas por las
    /// etapas de UNA misma operación y NO pasan a las escrituras anidadas (patrones.md: no llegan a los steps anidados).
    /// <b>Pre-imagen:</b> si el step declaró una, en <c>PreEntityImages[nombre]</c>, tomada ANTES de escribir con
    /// <see cref="OrganizationServiceEnMemoria.ImagenPrevia"/> para el usuario del contexto (V-6: las columnas ocultas a ese usuario
    /// están AUSENTES); solo en Update y Delete.
    /// </para>
    /// <para>
    /// <b>Transacción.</b> La operación de más afuera (profundidad 1) toma una <see cref="OrganizationServiceEnMemoria.Instantanea"/> antes
    /// de PreOperation; si cualquier step síncrono de PreOperation o PostOperation, o la propia escritura, lanza, se restaura el almacén
    /// y la excepción sube SIN cambios (la misma instancia). Una excepción de PreValidation sube sin restaurar nada (aún no hay
    /// transacción). Las escrituras anidadas comparten la transacción de la de más afuera.
    /// </para>
    /// <para>
    /// <b>Asíncronos.</b> Un step <see cref="ModoDeStep.Asincrono"/> (solo PostOperation) no corre dentro de la operación: queda en cola
    /// y corre en <see cref="EjecutarAsincronos"/>, con su propio contexto (<c>Mode</c> 1), y una excepción suya NO revierte nada: queda en
    /// <see cref="Ejecuciones"/>. Lo que escriba un asíncrono dispara sus steps (a profundidad 1, sin padre).
    /// </para>
    /// </summary>
    internal sealed class PipelineSimulado : IPipelineDeEscrituras
    {
        /// <summary>La plataforma corta los ciclos pasada la profundidad 8.</summary>
        private const int ProfundidadMaxima = 8;

        private static readonly string[] Mensajes = { "Create", "Update", "Delete", "Assign" };

        private readonly FabricaDeServiciosSimulada _fabrica;
        private readonly List<RegistroDeStep> _registros = new List<RegistroDeStep>();

        /// <summary>En orden de INICIO. Durante la ejecución de un step su lugar está reservado con <c>null</c> hasta que termina.</summary>
        private readonly List<EjecucionDeStep> _ejecuciones = new List<EjecucionDeStep>();

        private readonly List<StepEnCola> _cola = new List<StepEnCola>();

        /// <summary>Los contextos que se están ejecutando (steps y plugins de Custom API): una escritura pedida mientras tanto es anidada.</summary>
        private readonly Stack<ContextoDePluginSimulado> _enEjecucion = new Stack<ContextoDePluginSimulado>();

        private sealed class StepEnCola
        {
            public RegistroDeStep Step;
            public ContextoDePluginSimulado Contexto;
        }

        /// <summary>Lo que comparten las etapas de UNA operación (ni las anidadas ni las siguientes).</summary>
        private sealed class Operacion
        {
            public OrganizationServiceEnMemoria Servicio;
            public string Mensaje;
            public string Tabla;
            public ParameterCollection Entradas;
            public ParameterCollection Compartidas = new ParameterCollection();
            public ContextoDePluginSimulado Padre;
            public int Profundidad;
            public Guid IdPrimario;
            public Guid Correlacion = Guid.NewGuid();
            public Dictionary<RegistroDeStep, Entity> Imagenes = new Dictionary<RegistroDeStep, Entity>();
            public List<RegistroDeStep> Aplicables;
        }

        /// <summary>Se conecta al almacén de <paramref name="fabrica"/> (<c>Almacen.Pipeline = this</c>). Una fábrica ya conectada a otro pipeline lanza <see cref="InvalidOperationException"/>.</summary>
        public PipelineSimulado(FabricaDeServiciosSimulada fabrica)
        {
            if (fabrica == null) throw new ArgumentNullException(nameof(fabrica));
            if (fabrica.Almacen.Pipeline != null) throw new InvalidOperationException("El almacén de esta fábrica ya está conectado a un pipeline.");
            _fabrica = fabrica;
            fabrica.Almacen.Pipeline = this;
        }

        /// <summary>
        /// Registra un step. <paramref name="mensaje"/> es "Create", "Update", "Delete" o "Assign" (otro lanza
        /// <see cref="ArgumentException"/>). Un step asíncrono fuera de PostOperation lanza <see cref="ArgumentException"/>.
        /// <paramref name="orden"/> es el rank de ejecución (por defecto 1).
        /// </summary>
        public RegistroDeStep Registrar(IPlugin plugin, string mensaje, string tabla, EtapaDeStep etapa, int orden = 1,
            ModoDeStep modo = ModoDeStep.Sincrono, string[] filtro = null, string nombreDeImagenPrevia = null, string[] columnasDeImagenPrevia = null)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            if (mensaje == null || Array.IndexOf(Mensajes, mensaje) < 0)
                throw new ArgumentException("El pipeline solo simula los mensajes Create, Update, Delete y Assign; llegó '" + mensaje + "'.", nameof(mensaje));
            if (string.IsNullOrEmpty(tabla)) throw new ArgumentException("Falta la tabla del step.", nameof(tabla));
            if (modo == ModoDeStep.Asincrono && etapa != EtapaDeStep.PostOperation)
                throw new ArgumentException("Un step asíncrono solo se registra en PostOperation (DC-27).", nameof(modo));
            var registro = new RegistroDeStep(plugin, mensaje, tabla, etapa, orden, modo, filtro ?? new string[0], nombreDeImagenPrevia, columnasDeImagenPrevia ?? new string[0]);
            _registros.Add(registro);
            return registro;
        }

        /// <summary>Cada step ejecutado, en orden de INICIO de la ejecución (un step anidado figura después del que lo provocó), también los que lanzaron.</summary>
        public IReadOnlyList<EjecucionDeStep> Ejecuciones { get { return _ejecuciones.Where(e => e != null).ToList(); } }

        /// <summary>Steps asíncronos esperando <see cref="EjecutarAsincronos"/>.</summary>
        public int PendientesAsincronos { get { return _cola.Count; } }

        /// <summary>Corre, en orden de llegada, los asíncronos en cola (y los que ellos encolen). Sus excepciones no suben.</summary>
        public void EjecutarAsincronos()
        {
            while (_cola.Count > 0)
            {
                var pendiente = _cola[0];
                _cola.RemoveAt(0);
                // El asíncrono corre en su propia transacción y sin padre: lo que escriba es una operación de profundidad 1.
                Ejecutar(pendiente.Step, pendiente.Contexto, false, true);
            }
        }

        void IPipelineDeEscrituras.Escribir(OrganizationServiceEnMemoria servicio, string mensaje, string tabla, ParameterCollection entradas, Func<Guid> aplicar)
        {
            var padre = _enEjecucion.Count > 0 ? _enEjecucion.Peek() : null;
            var profundidad = padre == null ? 1 : padre.Depth + 1;
            if (profundidad > ProfundidadMaxima)
                throw new InvalidOperationException("Se superó la profundidad máxima de ejecución de la plataforma (" + ProfundidadMaxima + "): una cadena de steps que se disparan unos a otros.");

            object destino;
            entradas.TryGetValue("Target", out destino);
            var entidad = destino as Entity;
            var referencia = destino as EntityReference;
            var operacion = new Operacion
            {
                Servicio = servicio,
                Mensaje = mensaje,
                Tabla = tabla,
                Entradas = entradas,
                Padre = padre,
                Profundidad = profundidad,
                IdPrimario = entidad != null ? entidad.Id : (referencia != null ? referencia.Id : Guid.Empty)
            };

            // El filtro de atributos mira lo que trajo el llamador, no lo que los steps le agreguen; solo cuenta en Update (DC-27).
            var traidas = entidad != null ? entidad.Attributes.Keys.ToList() : new List<string>();
            operacion.Aplicables = _registros
                .Where(r => string.Equals(r.Mensaje, mensaje, StringComparison.Ordinal) && string.Equals(r.Tabla, tabla, StringComparison.Ordinal))
                .Where(r => mensaje != "Update" || r.Filtro.Count == 0 || traidas.Any(k => r.Filtro.Contains(k, StringComparer.Ordinal)))
                .ToList();

            // La pre-imagen se toma al INICIO de la operación, antes de cualquier step, con la seguridad por columna del usuario que escribe (V-6).
            if ((mensaje == "Update" || mensaje == "Delete") && servicio.Existe(tabla, operacion.IdPrimario))
            {
                foreach (var step in operacion.Aplicables.Where(r => r.NombreDeImagenPrevia != null))
                    operacion.Imagenes[step] = servicio.ImagenPrevia(tabla, operacion.IdPrimario, step.ColumnasDeImagenPrevia, servicio.EsSystem ? (Guid?)null : servicio.Identidad);
            }

            // PreValidation corre antes de que exista la transacción: lo que escriba y una excepción suya no se restauran.
            CorrerEtapa(operacion, EtapaDeStep.PreValidation);

            // La operación de más afuera abre la transacción; las anidadas comparten la suya.
            var instantanea = padre == null ? servicio.Instantanea() : null;
            var marcaDeCola = _cola.Count;
            try
            {
                CorrerEtapa(operacion, EtapaDeStep.PreOperation);
                var id = aplicar();
                if (mensaje == "Create")
                {
                    entidad.Id = id;
                    operacion.IdPrimario = id;
                }
                CorrerEtapa(operacion, EtapaDeStep.PostOperation);
            }
            catch
            {
                if (instantanea != null)
                {
                    servicio.Restaurar(instantanea);
                    // Un asíncrono es un trabajo de la misma transacción: si se revierte, no corre.
                    if (_cola.Count > marcaDeCola) _cola.RemoveRange(marcaDeCola, _cola.Count - marcaDeCola);
                }
                throw;
            }
        }

        IDisposable IPipelineDeEscrituras.Entrar(ContextoDePluginSimulado contexto)
        {
            _enEjecucion.Push(contexto);
            return new Entrada(this, _cola.Count, _fabrica.Almacen.Restauraciones);
        }

        private sealed class Entrada : IDisposable
        {
            private readonly PipelineSimulado _pipeline;
            private readonly int _marcaDeCola;
            private readonly int _restauraciones;
            private bool _liberada;

            public Entrada(PipelineSimulado pipeline, int marcaDeCola, int restauraciones)
            {
                _pipeline = pipeline;
                _marcaDeCola = marcaDeCola;
                _restauraciones = restauraciones;
            }

            public void Dispose()
            {
                if (_liberada) return;
                _liberada = true;
                _pipeline._enEjecucion.Pop();
                // Si el plugin de la Custom API revirtió la transacción (EjecutarComoPlataforma restaura antes de liberar), sus asíncronos no corren.
                if (_pipeline._fabrica.Almacen.Restauraciones != _restauraciones && _pipeline._cola.Count > _marcaDeCola)
                    _pipeline._cola.RemoveRange(_marcaDeCola, _pipeline._cola.Count - _marcaDeCola);
            }
        }

        private void CorrerEtapa(Operacion operacion, EtapaDeStep etapa)
        {
            foreach (var step in operacion.Aplicables.Where(r => r.Etapa == etapa).OrderBy(r => r.Orden).ToList())
            {
                var contexto = ConstruirContexto(operacion, step);
                if (step.Modo == ModoDeStep.Asincrono) _cola.Add(new StepEnCola { Step = step, Contexto = contexto });
                else Ejecutar(step, contexto, true, false);
            }
        }

        private ContextoDePluginSimulado ConstruirContexto(Operacion operacion, RegistroDeStep step)
        {
            var servicio = operacion.Servicio;
            var contexto = new ContextoDePluginSimulado
            {
                MessageName = operacion.Mensaje,
                Stage = (int)step.Etapa,
                Mode = (int)step.Modo,
                Depth = operacion.Profundidad,
                PrimaryEntityName = operacion.Tabla,
                PrimaryEntityId = operacion.IdPrimario,
                UserId = servicio.Identidad,
                InitiatingUserId = servicio.Identidad,
                OperationCreatedOn = servicio.Reloj.AhoraUtc,
                BusinessUnitId = servicio.UnidadDeNegocioDe(servicio.Identidad),
                OrganizationId = servicio.OrganizacionId,
                ParentContext = operacion.Padre,
                SharedVariables = operacion.Compartidas,
                CorrelationId = operacion.Correlacion,
                IsInTransaction = step.Modo == ModoDeStep.Sincrono && step.Etapa != EtapaDeStep.PreValidation
            };
            foreach (var clave in operacion.Entradas.Keys.ToList()) contexto.InputParameters[clave] = operacion.Entradas[clave];
            Entity imagen;
            if (step.NombreDeImagenPrevia != null && operacion.Imagenes.TryGetValue(step, out imagen)) contexto.PreEntityImages[step.NombreDeImagenPrevia] = imagen;
            if (operacion.Mensaje == "Create" && step.Etapa == EtapaDeStep.PostOperation) contexto.OutputParameters["id"] = operacion.IdPrimario;
            return contexto;
        }

        private void Ejecutar(RegistroDeStep step, ContextoDePluginSimulado contexto, bool anidaLasEscrituras, bool capturarLaExcepcion)
        {
            var indice = _ejecuciones.Count;
            _ejecuciones.Add(null);
            if (anidaLasEscrituras) _enEjecucion.Push(contexto);
            try
            {
                step.Plugin.Execute(new ProveedorDeServiciosSimulado(contexto, _fabrica));
            }
            catch (Exception error)
            {
                _ejecuciones[indice] = new EjecucionDeStep(step, contexto, error);
                if (!capturarLaExcepcion) throw;
                return;
            }
            finally
            {
                if (anidaLasEscrituras) _enEjecucion.Pop();
            }
            _ejecuciones[indice] = new EjecucionDeStep(step, contexto, null);
        }
    }
}
