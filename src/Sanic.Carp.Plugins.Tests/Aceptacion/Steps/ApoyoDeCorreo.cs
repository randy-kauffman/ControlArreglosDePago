using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Apoyos de las pruebas de S-31, S-32 y S-33 (sublote 5f, vuelta 2A): el escenario con los steps de Email que se pidan, los correos y las partes (<c>activityparty</c>) que arman las pruebas,
    /// y la ejecución directa de un <c>Create</c> de correo. Vive en <c>Aceptacion/</c> para que el constructor no pueda tocarlo. Lo de S-34 y S-35 tiene su propio apoyo.
    /// </summary>
    internal static class ApoyoDeCorreo
    {
        public const string Correo = "email";
        public const string Conv = ApoyoDeConversacion.Conv;
        public const string Cola = "queue";
        public const string Usuario = "systemuser";
        public const string Parte = "activityparty";

        /// <summary>El id de la cola del área que el escenario siembra con el buzón configurado (<see cref="ConfiguracionSimulada.IdDeQueueRemitentePorDefecto"/>).</summary>
        public static Guid ColaDelArea
        {
            get { return ConfiguracionSimulada.IdDeQueueRemitentePorDefecto; }
        }

        /// <summary>El escenario del área con S-31 y/o S-32 y S-33 registrados tal como los declara <c>RegistrosEsperados</c>.</summary>
        public static EscenarioDeSteps EscenarioDeCorreos(bool remitente = false, bool soloSistema = false)
        {
            var e = new EscenarioDeSteps();
            if (remitente) e.Registrar(new RemitenteDelAreaStep());
            if (soloSistema) e.Registrar(new EmailSoloSistemaStep());
            return e;
        }

        /// <summary>Una conversación sembrada (el <c>regardingobjectid</c> de un correo tiene que apuntar a una fila que existe).</summary>
        public static Guid UnaConversacion(EscenarioDeSteps e)
        {
            var id = e.Sembrar(ConstructorDeDatos.UnaConversacion());
            e.Almacen.LimpiarRegistros();
            return id;
        }

        public static EntityReference RefCola(Guid id)
        {
            return new EntityReference(Cola, id);
        }

        public static EntityReference RefUsuario(Guid id)
        {
            return new EntityReference(Usuario, id);
        }

        /// <summary>Un <c>from</c>: una <c>EntityCollection</c> de <c>activityparty</c>, uno por referencia, con solo <c>partyid</c>.</summary>
        public static EntityCollection Partes(params EntityReference[] referencias)
        {
            return new EntityCollection(referencias.Select(r => new Entity(Parte) { ["partyid"] = r }).ToList());
        }

        /// <summary>Una parte sin <c>partyid</c>: solo una dirección escrita a mano.</summary>
        public static Entity ParteSoloConDireccion(string direccion)
        {
            return new Entity(Parte) { ["addressused"] = direccion };
        }

        /// <summary>Un correo saliente de una conversación (el <c>Create</c> que manda el botón Responder). <paramref name="desde"/> <c>null</c> = sin la clave <c>from</c>.</summary>
        public static Entity UnaSaliente(Guid conversacion, EntityCollection desde)
        {
            var correo = new Entity(Correo)
            {
                ["directioncode"] = true,
                ["subject"] = "ZZF-PRUEBA respuesta " + conversacion.ToString("N"),
                ["regardingobjectid"] = new EntityReference(Conv, conversacion)
            };
            if (desde != null) correo["from"] = desde;
            return correo;
        }

        /// <summary>Ejecuta el step como la plataforma lo haría para un <c>Create</c> de correo en PreOperation, sin pipeline: el step modifica el <paramref name="target"/> en el lugar.</summary>
        public static void EjecutarCreateDirecto(EscenarioDeSteps e, BaseDeStep paso, Entity target, Guid actor)
        {
            ApoyoDeSteps.Ejecutar(paso, e.Fabrica, ApoyoDeSteps.Contexto("Create", 20, Correo, actor, target));
        }

        /// <summary>Igual que <see cref="EjecutarCreateDirecto"/> para un <c>Update</c> de correo en PreOperation (S-37, cierre del sublote 5f): el <paramref name="target"/> lleva el id del correo.</summary>
        public static void EjecutarUpdateDirecto(EscenarioDeSteps e, BaseDeStep paso, Entity target, Guid actor)
        {
            ApoyoDeSteps.Ejecutar(paso, e.Fabrica, ApoyoDeSteps.Contexto("Update", 20, Correo, actor, target));
        }

        /// <summary>El proveedor de servicios de un <c>Update</c> de correo en PreOperation con un tracing espía.</summary>
        public static ProveedorConEspia ProveedorUpdateConEspia(EscenarioDeSteps e, Entity target, Guid actor)
        {
            return new ProveedorConEspia(ApoyoDeSteps.Contexto("Update", 20, Correo, actor, target), e.Fabrica);
        }

        /// <summary>El proveedor de servicios de un <c>Create</c> de correo en PreOperation con un tracing espía (<c>Espia.Lineas()</c>): sirve aunque el step lance.</summary>
        public static ProveedorConEspia ProveedorCreateConEspia(EscenarioDeSteps e, Entity target, Guid actor)
        {
            return new ProveedorConEspia(ApoyoDeSteps.Contexto("Create", 20, Correo, actor, target), e.Fabrica);
        }

        /// <summary>Las lecturas de un correo por id (<c>Retrieve</c>) que hizo el almacén.</summary>
        public static IReadOnlyList<ConsultaRegistrada> LecturasDeCorreo(EscenarioDeSteps e)
        {
            return e.Almacen.Consultas.Where(c => c.Operacion == OperacionDeDatos.Retrieve && string.Equals(c.Tabla, Correo, StringComparison.Ordinal)).ToList();
        }

        public static IReadOnlyList<EjecucionDeStep> EjecucionesDe<T>(EscenarioDeSteps e)
        {
            return e.Pipeline.Ejecuciones.Where(x => x.Step.Plugin is T).ToList();
        }

        /// <summary>El <c>from</c> del <paramref name="target"/> como lista de <c>partyid</c> (tabla, id), en su orden.</summary>
        public static IReadOnlyList<KeyValuePair<string, Guid>> PartesDe(Entity target)
        {
            var coleccion = (EntityCollection)target["from"];
            return coleccion.Entities.Select(p =>
            {
                var referencia = (EntityReference)p["partyid"];
                return new KeyValuePair<string, Guid>(referencia.LogicalName, referencia.Id);
            }).ToList();
        }
    }
}
