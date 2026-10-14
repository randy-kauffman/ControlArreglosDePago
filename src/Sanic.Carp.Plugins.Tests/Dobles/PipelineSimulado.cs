using System;
using System.Collections.Generic;
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
        /// <summary>Se conecta al almacén de <paramref name="fabrica"/> (<c>Almacen.Pipeline = this</c>). Una fábrica ya conectada a otro pipeline lanza <see cref="InvalidOperationException"/>.</summary>
        public PipelineSimulado(FabricaDeServiciosSimulada fabrica) { throw new NotImplementedException(); }

        /// <summary>
        /// Registra un step. <paramref name="mensaje"/> es "Create", "Update", "Delete" o "Assign" (otro lanza
        /// <see cref="ArgumentException"/>). Un step asíncrono fuera de PostOperation lanza <see cref="ArgumentException"/>.
        /// <paramref name="orden"/> es el rank de ejecución (por defecto 1).
        /// </summary>
        public RegistroDeStep Registrar(IPlugin plugin, string mensaje, string tabla, EtapaDeStep etapa, int orden = 1,
            ModoDeStep modo = ModoDeStep.Sincrono, string[] filtro = null, string nombreDeImagenPrevia = null, string[] columnasDeImagenPrevia = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>Cada step ejecutado, en orden de INICIO de la ejecución (un step anidado figura después del que lo provocó), también los que lanzaron.</summary>
        public IReadOnlyList<EjecucionDeStep> Ejecuciones { get { throw new NotImplementedException(); } }

        /// <summary>Steps asíncronos esperando <see cref="EjecutarAsincronos"/>.</summary>
        public int PendientesAsincronos { get { throw new NotImplementedException(); } }

        /// <summary>Corre, en orden de llegada, los asíncronos en cola (y los que ellos encolen). Sus excepciones no suben.</summary>
        public void EjecutarAsincronos() { throw new NotImplementedException(); }

        void IPipelineDeEscrituras.Escribir(OrganizationServiceEnMemoria servicio, string mensaje, string tabla, ParameterCollection entradas, Func<Guid> aplicar)
        {
            throw new NotImplementedException();
        }

        IDisposable IPipelineDeEscrituras.Entrar(ContextoDePluginSimulado contexto)
        {
            throw new NotImplementedException();
        }
    }
}
