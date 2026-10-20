using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Los parámetros de entrada de <c>sanic_carp_capi_reconciliarcorreos</c> (diseno/03 1.2), ya tipados. Contenedor sin lógica. <c>null</c> significa "ausente" y es distinto de cero:
    /// <c>MinutosMinimos</c> ausente ⇒ 10, pero <c>0</c> explícito ⇒ sin espera. La API NO tiene actor: la cuenta de servicio que llama no se registra (0.2).
    /// </summary>
    internal sealed class PedidoDeReconciliarCorreos
    {
        /// <summary><c>emailid</c>: si tiene valor, reconcilia solo ese correo (reintento dirigido), sin el filtro de antigüedad.</summary>
        public Guid? EmailId { get; }

        /// <summary><c>maximo</c>: ausente o <c>0</c> ⇒ 20; más de 100 se recorta a 100 sin error; negativo ⇒ <c>CARP-REC-001</c>.</summary>
        public int? Maximo { get; }

        /// <summary><c>minutosminimos</c>: ausente ⇒ 10; <c>0</c> explícito ⇒ sin espera; negativo ⇒ <c>CARP-REC-002</c>.</summary>
        public int? MinutosMinimos { get; }

        public PedidoDeReconciliarCorreos(Guid? emailId, int? maximo, int? minutosMinimos)
        {
            EmailId = emailId;
            Maximo = maximo;
            MinutosMinimos = minutosMinimos;
        }
    }

    /// <summary>El valor de <c>resultado</c> en el <c>detalle</c> de la reconciliación (1.2): <c>creada</c>, <c>unida</c>, <c>yatenia</c> u <c>omitido</c>.</summary>
    internal enum ResultadoDeReconciliacion
    {
        Creada = 1,
        Unida = 2,
        YaTenia = 3,
        Omitido = 4
    }

    /// <summary>Una fila del <c>detalle</c> (1.2): SOLO ids y códigos, nunca asunto ni remitente. Contenedor sin lógica.</summary>
    internal sealed class ItemDeReconciliacion
    {
        public Guid EmailId { get; }
        public ResultadoDeReconciliacion Resultado { get; }

        /// <summary>La conversación final (creada, destino o la que ya tenía); <c>null</c> si se omitió.</summary>
        public Guid? ConversacionId { get; }

        /// <summary>El código <c>CARP-ENT-*</c> de la omisión; <c>null</c> si no se omitió.</summary>
        public string Codigo { get; }

        public ItemDeReconciliacion(Guid emailId, ResultadoDeReconciliacion resultado, Guid? conversacionId, string codigo)
        {
            EmailId = emailId;
            Resultado = resultado;
            ConversacionId = conversacionId;
            Codigo = codigo;
        }
    }

    /// <summary>Los parámetros de salida de <c>sanic_carp_capi_reconciliarcorreos</c> (1.2). Se escriben TODOS siempre que la llamada termina bien (0.1). Contenedor sin lógica; <c>Detalle</c> nulo se rechaza.</summary>
    internal sealed class ResultadoDeReconciliarCorreos
    {
        /// <summary><c>encontrados</c>: los candidatos procesados en este lote (sin el candidato extra con que se detecta <c>haymas</c>). Con <c>emailid</c> es 1.</summary>
        public int Encontrados { get; }

        /// <summary><c>registrados</c>: los que quedaron con conversación (<c>creada</c>, <c>unida</c> o <c>yatenia</c>).</summary>
        public int Registrados { get; }

        /// <summary><c>conerror</c>: los que se omitieron con un código <c>CARP-ENT-*</c>.</summary>
        public int ConError { get; }

        /// <summary><c>haymas</c>: hay más candidatos fuera del lote.</summary>
        public bool HayMas { get; }

        /// <summary><c>detalle</c>: el JSON de <see cref="DetalleDeReconciliacion.ComoJson"/>.</summary>
        public string Detalle { get; }

        public ResultadoDeReconciliarCorreos(int encontrados, int registrados, int conError, bool hayMas, string detalle)
        {
            if (detalle == null) throw new ArgumentNullException(nameof(detalle));
            Encontrados = encontrados;
            Registrados = registrados;
            ConError = conError;
            HayMas = hayMas;
            Detalle = detalle;
        }
    }

    /// <summary>Pieza pura de <c>reconciliarcorreos</c>: el <c>detalle</c> JSON (diseno/03 1.2 y "Decisiones del arquitecto, 2026-10-19 (lote 6)").</summary>
    internal static class DetalleDeReconciliacion
    {
        /// <summary>
        /// El arreglo JSON del <c>detalle</c>, sin espacios ni saltos y en el orden de la lista:
        /// <c>[{"emailid":"…","resultado":"creada","conversacionid":"…","codigo":null}]</c>. Claves exactamente <c>emailid</c>, <c>resultado</c>, <c>conversacionid</c>, <c>codigo</c>, en ese orden;
        /// los Guid en formato <c>D</c> minúscula entre comillas; <c>resultado</c> en minúscula (<c>creada</c>, <c>unida</c>, <c>yatenia</c>, <c>omitido</c>); un <c>conversacionid</c> o <c>codigo</c> sin valor es el
        /// literal <c>null</c> sin comillas; el código es el texto <c>CARP-ENT-nnn</c> entre comillas. Lista vacía ⇒ <c>[]</c>. Se escapan <c>"</c> y <c>\</c> y los controles por si un código futuro los trajera; no
        /// depende de la cultura. Lista nula ⇒ <see cref="ArgumentNullException"/>; un elemento nulo ⇒ <see cref="ArgumentException"/>; un <see cref="ResultadoDeReconciliacion"/> no definido ⇒
        /// <see cref="ArgumentOutOfRangeException"/>. No usa bibliotecas externas ni <c>DataContractJsonSerializer</c> (la salida debe ser byte a byte la misma en net462 y net8.0).
        /// </summary>
        public static string ComoJson(IReadOnlyList<ItemDeReconciliacion> items)
        {
            throw new NotImplementedException();
        }

        /// <summary>El <see cref="ItemDeReconciliacion"/> de un <c>Omitido</c> del registrador: <c>resultado</c> = <see cref="ResultadoDeReconciliacion.Omitido"/>, sin conversación y con <c>error.Codigo</c>.</summary>
        public static ItemDeReconciliacion DeOmision(Guid emailId, ErrorDeNegocio error)
        {
            throw new NotImplementedException();
        }
    }
}
