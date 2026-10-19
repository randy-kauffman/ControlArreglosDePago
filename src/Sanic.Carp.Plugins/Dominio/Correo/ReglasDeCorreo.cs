using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Dominio.Correo
{
    /// <summary>
    /// Las reglas del Email que se evalúan sobre un guardado (diseno/03 2.9, firmas de 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"). Dominio puro: sin <c>Entity</c> ni SDK, sin
    /// estado, sin leer el reloj y con la identidad perezosa (0.3). Usa <c>Cambios</c>, <c>Columnas</c>, <c>CodigosDeError</c> y <c>ResultadoDeEvaluacion</c> sin reimplementarlos.
    /// </summary>
    public static class ReglasDeCorreo
    {
        /// <summary>Clave de <see cref="ResultadoDeEvaluacion.Escribir"/> con las partes de <c>from</c> que el step debe poner en el <c>Target</c>.</summary>
        public const string ColumnaFrom = "from";

        private const string TablaDeCola = "queue";
        private const string TablaDeUsuario = "systemuser";

        private static readonly IReadOnlyList<string> SoloSistema = new ReadOnlyCollection<string>(new[] { "sanic_tiemporespuesta", "sanic_respondidoporid" });

        /// <summary>
        /// Las dos columnas solo-sistema del Email, en este orden: <c>sanic_tiemporespuesta</c>, <c>sanic_respondidoporid</c> (2.0.8, 2.9; S-32 y S-33). Son exactamente
        /// <c>Columnas.De("email").SoloSistema</c>.
        /// </summary>
        public static IReadOnlyList<string> ColumnasSoloSistema { get { return SoloSistema; } }

        /// <summary>
        /// S-31 (2.9, <c>RemitenteDelAreaStep</c>). Permitido o rechazado; cuando hay que fijar el remitente, <c>Escribir</c> trae UNA clave, <see cref="ColumnaFrom"/>, con un
        /// <c>IReadOnlyList&lt;ReferenciaDeEntidad&gt;</c> de un solo elemento: <c>new ReferenciaDeEntidad("queue", queueId)</c> (el step lo convierte en la colección de <c>activityparty</c>).
        /// En este orden:
        /// <list type="number">
        /// <item>No es saliente ⇒ permite sin tocar nada. <b>0 consultas:</b> no se lee <c>QueueRemitenteId</c>.</item>
        /// <item><c>Regarding</c> nulo o de una tabla que no es <c>sanic_carp_tbl_conversacion</c> (ordinal) ⇒ permite sin tocar nada, igual de barato.</item>
        /// <item><c>Configuracion.QueueRemitenteId</c> (lanza <c>CARP-CFG-004</c> o <c>-005</c>, que sube tal cual: PA-EML-07).</item>
        /// <item><c>Remitentes</c> ausente (<c>null</c>) o vacío ⇒ <c>Escribir[from] = [queue]</c> (PA-EML-02).</item>
        /// <item>Una sola parte: la queue (tabla <c>queue</c> e id iguales, ordinal) ⇒ permite sin escribir (PA-EML-03); el usuario que escribe (tabla <c>systemuser</c> e id igual a
        /// <c>Identidad.ActorId</c>) ⇒ <c>Escribir[from] = [queue]</c> (DC-07, PA-EML-01); cualquier otra ⇒ <c>CARP-EML-001</c>.</item>
        /// <item>Dos o más partes (aunque una sea la queue, o estén repetidas) ⇒ <c>CARP-EML-001</c> (PA-EML-04).</item>
        /// </list>
        /// No consulta identidad por <c>EsEscrituraDelSistema</c> ni <c>EsJefatura()</c>: solo lee <c>ActorId</c>. <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeEvaluacion EvaluarRemitente(PedidoDeRemitente pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            // 2.9 puntos 1 y 2: un correo que no es saliente de una conversación no cuesta ni una consulta.
            if (!pedido.EsSaliente) return ResultadoDeEvaluacion.Permitir();
            var regarding = pedido.Regarding;
            if (regarding == null || !string.Equals(regarding.Tabla, Columnas.TablaConversacion, StringComparison.Ordinal)) return ResultadoDeEvaluacion.Permitir();

            var queueId = pedido.Configuracion.QueueRemitenteId;
            var remitentes = pedido.Remitentes;
            if (remitentes == null || remitentes.Count == 0) return PonerLaCola(queueId);
            if (remitentes.Count > 1) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.EML_001.Crear());

            var unica = remitentes[0];
            if (unica != null && string.Equals(unica.Tabla, TablaDeCola, StringComparison.Ordinal) && unica.Id == queueId) return ResultadoDeEvaluacion.Permitir();
            if (unica != null && string.Equals(unica.Tabla, TablaDeUsuario, StringComparison.Ordinal) && unica.Id == pedido.Identidad.ActorId) return PonerLaCola(queueId);
            return ResultadoDeEvaluacion.Rechazar(CodigosDeError.EML_001.Crear());
        }

        private static ResultadoDeEvaluacion PonerLaCola(Guid queueId)
        {
            var desde = new ReadOnlyCollection<ReferenciaDeEntidad>(new[] { new ReferenciaDeEntidad(TablaDeCola, queueId) });
            return ResultadoDeEvaluacion.Permitir(new[] { new KeyValuePair<string, object>(ColumnaFrom, desde) });
        }

        /// <summary>
        /// S-32 y S-33 (2.9, <c>EmailSoloSistemaStep</c>). Permitido sin escribir nada (<c>Escribir</c> siempre vacío), o <c>CARP-EML-003</c>. En este orden:
        /// <list type="number">
        /// <item>Columnas de <see cref="ColumnasSoloSistema"/> presentes en el <c>Target</c>. Ninguna ⇒ permite SIN consultar la identidad ni leer nada.</item>
        /// <item>Alta (<c>EsAlta</c>): si todas las presentes valen <c>null</c> o texto vacío (DC-01) no hay nada que proteger ⇒ permite sin consultar la identidad (decisión del 5f: en un
        /// <c>Create</c> no hay valor guardado, y un nulo no escribe nada).</item>
        /// <item><c>Identidad.EsEscrituraDelSistema</c> (una sola consulta) ⇒ permite (DC-03: los asíncronos S-35 escriben estas columnas con SYSTEM, PA-ESI-03).</item>
        /// <item>Alta de una persona con alguna columna con valor ⇒ <c>CARP-EML-003</c> (PA-ESI-01).</item>
        /// <item>Actualización de una persona: valores guardados con <c>Lecturas.ColumnasDelSistema(EmailId)</c> (UNA lectura); "cambió" = <c>Cambios.Distinto(Target, guardados, columna)</c> para cada
        /// columna presente (DC-01: el mismo valor no es un cambio, nulo contra nulo tampoco, PA-ESI-04). Alguna cambió ⇒ <c>CARP-EML-003</c> (PA-ESI-02); ninguna ⇒ permite.</item>
        /// </list>
        /// <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeEvaluacion EvaluarColumnasDelSistema(PedidoDeColumnasDelSistema pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            var presentes = new List<string>();
            foreach (var columna in SoloSistema)
            {
                if (pedido.Target.ContainsKey(columna)) presentes.Add(columna);
            }
            if (presentes.Count == 0) return ResultadoDeEvaluacion.Permitir();

            if (pedido.EsAlta && presentes.TrueForAll(c => EsNuloOVacio(pedido.Target[c]))) return ResultadoDeEvaluacion.Permitir();

            if (pedido.Identidad.EsEscrituraDelSistema) return ResultadoDeEvaluacion.Permitir();
            if (pedido.EsAlta) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.EML_003.Crear());

            var guardados = pedido.Lecturas.ColumnasDelSistema(pedido.EmailId);
            foreach (var columna in presentes)
            {
                if (Cambios.Distinto(pedido.Target, guardados, columna)) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.EML_003.Crear());
            }
            return ResultadoDeEvaluacion.Permitir();
        }

        private static bool EsNuloOVacio(object valor)
        {
            if (valor == null) return true;
            var texto = valor as string;
            return texto != null && texto.Length == 0;
        }
    }
}
