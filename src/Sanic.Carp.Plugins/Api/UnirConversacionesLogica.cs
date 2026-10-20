using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Dominio.Tiempos;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// La lógica de <c>sanic_carp_capi_unirconversaciones</c> (diseno/03 1.4 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Valida el acceso con el LLAMADOR y escribe como SYSTEM con el actor explícito. Dos
    /// servicios por ejecución (uno de cada tipo): LLAMADOR para <paramref name="conversacionesComoLlamador"/> (solo el paso 2); SYSTEM para <paramref name="conversacionesComoSistema"/>, <paramref name="correos"/> y
    /// <paramref name="arreglos"/> (cadenas, conteos y todas las escrituras).
    /// <para>
    /// <b>Contrato de <see cref="Ejecutar"/>, en orden</b> (el primer rechazo gana; TODAS las lecturas y validaciones preceden a la primera escritura):
    /// </para>
    /// <list type="number">
    /// <item><c>OrigenId</c> o <c>DestinoId</c> nulos ⇒ <c>CARP-UNI-001</c>; iguales ⇒ <c>CARP-UNI-002</c>. No toca ningún puerto.</item>
    /// <item>Con el llamador, <c>Leer</c> del origen y después del destino, con NUEVE columnas: [statecode, statuscode, sanic_unidaaid, sanic_clienteid, sanic_oficialasignadoid, sanic_fechaprimercorreo, sanic_sinrespuestadesde,
    /// sanic_cantidadrespuestas, sanic_tiempoprimerarespuesta] (las dos últimas las necesita el paso 6c para escribir en el destino solo lo que cambia; decisión del orquestador, 2026-10-19). Cualquiera <c>null</c> (no existe O sin
    /// acceso) ⇒ <c>CARP-UNI-003</c>, un solo texto. La fila del origen trae la <c>VersionDeFila</c> que se usa en el paso 8.</item>
    /// <item>Estados (par <c>statecode</c>/<c>statuscode</c>, <c>Estados.DeConversacion</c>): origen que no es Abierta (Finalizada, Unida o desconocida) ⇒ <c>CARP-UNI-004</c> (OA-11); destino que no es Abierta ⇒ <c>CARP-UNI-005</c>; ambos con
    /// <c>sanic_clienteid</c> y distintos ⇒ <c>CARP-UNI-010</c> (si uno no tiene cliente, o ninguno, la unión sigue).</item>
    /// <item>Cadenas y ciclos, EN ESTE ORDEN, con SYSTEM (una cadena puede pasar por filas que el llamador no lee; solo devuelve códigos): (a) ciclo: se sigue <c>sanic_unidaaid</c> desde el destino, hasta 10 saltos (un <c>Leer(id, [sanic_unidaaid])</c> por
    /// salto, el primero ya está en la fila del paso 2); llegar al origen, volver a una conversación ya visitada (incluido el destino apuntándose a sí mismo) o no terminar en 10 saltos ⇒ <c>CARP-UNI-007</c>; una fila que no se puede leer corta el recorrido
    /// sin error. (b) cadena: <c>sanic_unidaaid</c> del origen o del destino no nulos (aunque el estado diga Abierta: dato corrupto) ⇒ <c>CARP-UNI-006</c>. (c) <c>conversacionesComoSistema.HayUnidasA(origen)</c> ⇒ <c>CARP-UNI-008</c>.</item>
    /// <item>Correos con SYSTEM: <c>correos.DeConversacion(origen, [directioncode, sanic_respondidoporid, sanic_tiemporespuesta, senton, actualend], 1001)</c>; más de 1.000 ⇒ <c>CARP-UNI-009</c> (DC-16). Los del destino, con las mismas columnas y tope 5.000
    /// (lo máximo que admite el puerto), para el recálculo; si el destino devuelve 5.000 el recálculo no es confiable y se rechaza con <c>CARP-UNI-011</c>, antes de cualquier escritura (decisión del orquestador, 2026-10-19: un <c>-900</c> invitaría a reintentar algo que nunca va a pasar). Los ids de los
    /// arreglos: <c>arreglos.IdsDeConversacion(origen)</c> (TODOS los estados). Todavía no se escribió nada.</item>
    /// <item>Recálculo en memoria con <c>TiemposDeConversacion.RecalcularTrasUnion</c> sobre las dos conversaciones: <c>fechasDePrimerCorreo</c> y <c>sinRespuestaDesde</c> de las dos filas del paso 2; <c>respuestasProcesadas</c> = los correos de ORIGEN Y
    /// DESTINO con <c>directioncode</c> verdadero y <c>sanic_respondidoporid</c> no nulo, con <c>EnviadoEn</c> = <c>senton</c> (si falta, <c>actualend</c>; si faltan las dos, el correo no cuenta como respuesta con instante y se traza su id) y <c>Tiempo</c> =
    /// <c>sanic_tiemporespuesta</c> (nulo si falta).</item>
    /// <item>Escribe con SYSTEM, EN ESTE ORDEN, sin atrapar nada: (a) cada correo del origen: <c>correos.ApuntarA(id, destino)</c> (solo <c>regardingobjectid</c>; no se tocan dueño ni <c>parentactivityid</c>); (b) cada arreglo: <c>arreglos.Actualizar(id,
    /// {sanic_conversacionid = ReferenciaDeEntidad(conversación, destino)})</c>, SOLO esa columna, en cualquier estado y congelado o no (el estado, el congelamiento y los motivos no se tocan; V-2); (c) el destino, UN <c>Actualizar</c> sin versión de fila y solo con las
    /// columnas que CAMBIAN: <c>sanic_clienteid</c> y <c>sanic_oficialasignadoid</c> del origen solo si el destino los tiene vacíos, y el recálculo (<c>sanic_fechaprimercorreo</c>, <c>sanic_sinrespuestadesde</c>, <c>sanic_cantidadrespuestas</c>,
    /// <c>sanic_tiempoprimerarespuesta</c>; un valor recalculado nulo sobre uno guardado lo vacía); si no cambia ninguna, no hay <c>Update</c>; (d) el origen, con la <c>VersionDeFila</c> del paso 2 (<c>IfRowVersionMatches</c>): <c>statecode = 1</c>, <c>statuscode =
    /// Estados.De(EstadoDeConversacion.Unida)</c>, <c>sanic_unidaaid = destino</c>, <c>sanic_fechafinalizacion = reloj.AhoraUtc</c>, <c>sanic_finalizadoporid = ReferenciaDeEntidad("systemuser", ActorId)</c> y <c>sanic_sinrespuestadesde = null</c>. Una fila del origen sin
    /// <c>VersionDeFila</c> lanza <see cref="InvalidOperationException"/> ANTES de escribir nada (no se degrada la concurrencia en silencio).</item>
    /// <item>Resultado: <c>DestinoId</c>, <c>CorreosMovidos</c> = los del origen, <c>ArreglosRepunteados</c> = todos los arreglos actualizados.</item>
    /// </list>
    /// Toda la operación es UNA transacción: la versión de fila que no coincide (otro proceso tocó el origen) y cualquier otra falla de la plataforma NO se atrapan; la base los vuelve <c>CARP-UNI-900</c> y la plataforma revierte los correos movidos (PA-UNI-16,
    /// PA-UNI-21). Los steps de Conversación, Arreglo y Email aceptan estas escrituras por ser del sistema (DC-03; H-19 (a), sin medir). Traza solo ids y códigos.
    /// </para>
    /// </summary>
    internal sealed class UnirConversacionesLogica
    {
        /// <summary>Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/> (<paramref name="trazar"/> puede ser <c>null</c>). No consulta nada al construirse.</summary>
        public UnirConversacionesLogica(IConversaciones conversacionesComoLlamador, IConversaciones conversacionesComoSistema, ICorreos correos, IArreglos arreglos, IReloj reloj, Action<string> trazar)
        {
            if (conversacionesComoLlamador == null) throw new ArgumentNullException(nameof(conversacionesComoLlamador));
            if (conversacionesComoSistema == null) throw new ArgumentNullException(nameof(conversacionesComoSistema));
            if (correos == null) throw new ArgumentNullException(nameof(correos));
            if (arreglos == null) throw new ArgumentNullException(nameof(arreglos));
            if (reloj == null) throw new ArgumentNullException(nameof(reloj));
            _comoLlamador = conversacionesComoLlamador;
            _comoSistema = conversacionesComoSistema;
            _correos = correos;
            _arreglos = arreglos;
            _reloj = reloj;
            _trazar = trazar;
        }

        private const string ColEstado = "statecode";
        private const string ColMotivo = "statuscode";
        private const string ColUnidaA = "sanic_unidaaid";
        private const string ColCliente = "sanic_clienteid";
        private const string ColOficial = "sanic_oficialasignadoid";
        private const string ColFechaPrimerCorreo = "sanic_fechaprimercorreo";
        private const string ColSinRespuestaDesde = "sanic_sinrespuestadesde";
        private const string ColCantidadRespuestas = "sanic_cantidadrespuestas";
        private const string ColTiempoPrimeraRespuesta = "sanic_tiempoprimerarespuesta";

        // 1.4 paso 4: hasta 10 saltos; 5 y 6: topes de correos (DC-16) y el máximo que admite ICorreos.DeConversacion.
        private const int MaximoDeSaltos = 10;
        private const int MaximoDeCorreosDelOrigen = 1000;
        private const int MaximoDeCorreosLeidosDelDestino = 5000;

        // Las nueve del paso 2: las siete de validación y de herencia, más la cantidad y el tiempo ya guardados, que el paso 6c necesita para escribir solo lo que cambia.
        // De solo lectura de verdad: PA6_GRD_02 no admite un arreglo estático (se podría mutar).
        private static readonly IReadOnlyList<string> ColumnasDeLasConversaciones = new ReadOnlyCollection<string>(new[]
        {
            ColEstado, ColMotivo, ColUnidaA, ColCliente, ColOficial, ColFechaPrimerCorreo, ColSinRespuestaDesde, ColCantidadRespuestas, ColTiempoPrimeraRespuesta
        });

        private static readonly IReadOnlyList<string> SoloLaUnion = new ReadOnlyCollection<string>(new[] { ColUnidaA });
        private static readonly IReadOnlyList<string> ColumnasDeLosCorreos = new ReadOnlyCollection<string>(new[] { "directioncode", "sanic_respondidoporid", "sanic_tiemporespuesta", "senton", "actualend" });

        private readonly IConversaciones _comoLlamador;
        private readonly IConversaciones _comoSistema;
        private readonly ICorreos _correos;
        private readonly IArreglos _arreglos;
        private readonly IReloj _reloj;
        private readonly Action<string> _trazar;

        /// <summary>Contrato en la descripción de la clase. <paramref name="pedido"/> nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public ResultadoDeUnirConversaciones Ejecutar(PedidoDeUnirConversaciones pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            // Paso 1: nada se consulta.
            if (!pedido.OrigenId.HasValue || !pedido.DestinoId.HasValue) throw CodigosDeError.UNI_001.Crear();
            var origenId = pedido.OrigenId.Value;
            var destinoId = pedido.DestinoId.Value;
            if (origenId == destinoId) throw CodigosDeError.UNI_002.Crear();

            // Paso 2: con el llamador, el origen primero. Inexistente y sin acceso dan el mismo texto.
            var origen = _comoLlamador.Leer(origenId, ColumnasDeLasConversaciones);
            if (origen == null) throw CodigosDeError.UNI_003.Crear();
            var destino = _comoLlamador.Leer(destinoId, ColumnasDeLasConversaciones);
            if (destino == null) throw CodigosDeError.UNI_003.Crear();

            // Paso 3.
            if (EstadoDeFila.DeConversacion(origen) != EstadoDeConversacion.Abierta) throw CodigosDeError.UNI_004.Crear();
            if (EstadoDeFila.DeConversacion(destino) != EstadoDeConversacion.Abierta) throw CodigosDeError.UNI_005.Crear();
            var clienteDelOrigen = Referencia(origen, ColCliente);
            var clienteDelDestino = Referencia(destino, ColCliente);
            if (clienteDelOrigen != null && clienteDelDestino != null && clienteDelOrigen.Id != clienteDelDestino.Id) throw CodigosDeError.UNI_010.Crear();

            // Paso 4, en este orden: ciclo, cadena, uniones recibidas. El recorrido va con SYSTEM: puede pasar por filas que el llamador no lee.
            if (HayCiclo(origenId, destinoId, destino)) throw CodigosDeError.UNI_007.Crear();
            if (Referencia(origen, ColUnidaA) != null || Referencia(destino, ColUnidaA) != null) throw CodigosDeError.UNI_006.Crear();
            if (_comoSistema.HayUnidasA(origenId)) throw CodigosDeError.UNI_008.Crear();

            // Paso 5: todas las lecturas preceden a la primera escritura.
            var correosDelOrigen = _correos.DeConversacion(origenId, ColumnasDeLosCorreos, MaximoDeCorreosDelOrigen + 1);
            if (correosDelOrigen.Count > MaximoDeCorreosDelOrigen) throw CodigosDeError.UNI_009.Crear();
            var correosDelDestino = _correos.DeConversacion(destinoId, ColumnasDeLosCorreos, MaximoDeCorreosLeidosDelDestino);
            // Con 5.000 filas el puerto pudo truncar: el recálculo no es confiable y un -900 invitaría a reintentar algo que nunca va a pasar (1.4 decisión 5).
            if (correosDelDestino.Count >= MaximoDeCorreosLeidosDelDestino) throw CodigosDeError.UNI_011.Crear();
            var arreglosDelOrigen = _arreglos.IdsDeConversacion(origenId);

            // Sin versión de fila no se degrada a un Update común, y se avisa antes de mover nada.
            if (string.IsNullOrWhiteSpace(origen.VersionDeFila)) throw new InvalidOperationException("La conversación de origen leída no trae la versión de fila.");

            // Paso 5 bis: el recálculo, en memoria.
            var recalculo = TiemposDeConversacion.RecalcularTrasUnion(
                new[] { Fecha(origen, ColFechaPrimerCorreo), Fecha(destino, ColFechaPrimerCorreo) },
                new[] { Fecha(origen, ColSinRespuestaDesde), Fecha(destino, ColSinRespuestaDesde) },
                RespuestasProcesadas(correosDelOrigen, correosDelDestino));
            var cambiosDelDestino = CambiosDelDestino(origen, destino, recalculo);

            // Paso 6, sin atrapar nada: una falla de escritura atrapada aborta la transacción (0.5).
            foreach (var correo in correosDelOrigen) _correos.ApuntarA(correo.Id, destinoId);
            var nuevaConversacion = new ReferenciaDeEntidad(TablasDelArea.Conversacion, destinoId);
            foreach (var arreglo in arreglosDelOrigen)
                _arreglos.Actualizar(arreglo, new Dictionary<string, object> { { "sanic_conversacionid", nuevaConversacion } });
            if (cambiosDelDestino.Count > 0) _comoSistema.Actualizar(destinoId, cambiosDelDestino, null);

            var unida = Estados.De(EstadoDeConversacion.Unida);
            _comoSistema.Actualizar(origenId, new Dictionary<string, object>
            {
                { ColEstado, new ValorDeOpcion(unida.StateCode) },
                { ColMotivo, new ValorDeOpcion(unida.StatusCode) },
                { ColUnidaA, nuevaConversacion },
                { "sanic_fechafinalizacion", _reloj.AhoraUtc },
                { "sanic_finalizadoporid", new ReferenciaDeEntidad("systemuser", pedido.ActorId) },
                { ColSinRespuestaDesde, null }
            }, origen.VersionDeFila);

            Trazar("unidas correos=" + correosDelOrigen.Count.ToString(CultureInfo.InvariantCulture) + " arreglos=" + arreglosDelOrigen.Count.ToString(CultureInfo.InvariantCulture));
            return new ResultadoDeUnirConversaciones(destinoId, correosDelOrigen.Count, arreglosDelOrigen.Count);
        }

        // 1.4 paso 4 (a): se sigue sanic_unidaaid desde el destino. Llegar al origen, repetir una visitada o no terminar en 10 saltos es un ciclo; una fila ilegible corta sin error.
        private bool HayCiclo(Guid origenId, Guid destinoId, FilaDeDatos destino)
        {
            var visitadas = new HashSet<Guid> { destinoId };
            var siguiente = Referencia(destino, ColUnidaA);
            for (var salto = 0; salto < MaximoDeSaltos && siguiente != null; salto++)
            {
                if (siguiente.Id == origenId || !visitadas.Add(siguiente.Id)) return true;
                var fila = _comoSistema.Leer(siguiente.Id, SoloLaUnion);
                siguiente = fila == null ? null : Referencia(fila, ColUnidaA);
            }
            return siguiente != null;
        }

        // 1.4 paso 6c: solo las columnas que CAMBIAN. El cliente y el oficial del origen se heredan solo si el destino los tiene vacíos.
        private static Dictionary<string, object> CambiosDelDestino(FilaDeDatos origen, FilaDeDatos destino, ResultadoDeRecalculo recalculo)
        {
            var cambios = new Dictionary<string, object>();
            foreach (var columna in new[] { ColCliente, ColOficial })
            {
                var delOrigen = Referencia(origen, columna);
                if (delOrigen != null && Referencia(destino, columna) == null) cambios[columna] = delOrigen;
            }
            Proponer(cambios, destino, ColFechaPrimerCorreo, recalculo.FechaPrimerCorreo);
            Proponer(cambios, destino, ColSinRespuestaDesde, recalculo.SinRespuestaDesde);
            // Una cantidad ausente es cero: la plataforma no devuelve las columnas nulas.
            object guardada;
            var cantidadGuardada = destino.Valores.TryGetValue(ColCantidadRespuestas, out guardada) && guardada is int ? (int)guardada : 0;
            if (cantidadGuardada != recalculo.CantidadRespuestas) cambios[ColCantidadRespuestas] = recalculo.CantidadRespuestas;
            Proponer(cambios, destino, ColTiempoPrimeraRespuesta, recalculo.TiempoPrimeraRespuesta);
            return cambios;
        }

        private static void Proponer(Dictionary<string, object> cambios, FilaDeDatos destino, string columna, object recalculado)
        {
            object guardado;
            destino.Valores.TryGetValue(columna, out guardado);
            if (Cambios.ValoresDistintos(recalculado, guardado)) cambios[columna] = recalculado;
        }

        // 1.4 paso 5 bis: respuestas = salientes (directioncode verdadero) ya procesadas (sanic_respondidoporid no nulo), de origen y destino.
        private IReadOnlyList<RespuestaProcesada> RespuestasProcesadas(IReadOnlyList<FilaDeDatos> delOrigen, IReadOnlyList<FilaDeDatos> delDestino)
        {
            var respuestas = new List<RespuestaProcesada>();
            foreach (var lista in new[] { delOrigen, delDestino })
            {
                foreach (var correo in lista)
                {
                    object saliente;
                    if (!correo.Valores.TryGetValue("directioncode", out saliente) || !(saliente is bool) || !(bool)saliente) continue;
                    if (Referencia(correo, "sanic_respondidoporid") == null) continue;
                    var enviadoEn = Fecha(correo, "senton") ?? Fecha(correo, "actualend");
                    if (!enviadoEn.HasValue)
                    {
                        Trazar("correo procesado sin instante " + correo.Id.ToString("D"));
                        continue;
                    }
                    object tiempo;
                    correo.Valores.TryGetValue("sanic_tiemporespuesta", out tiempo);
                    respuestas.Add(new RespuestaProcesada(enviadoEn.Value, tiempo is int ? (int?)(int)tiempo : null));
                }
            }
            return respuestas;
        }

        private static ReferenciaDeEntidad Referencia(FilaDeDatos fila, string columna)
        {
            object valor;
            return fila.Valores.TryGetValue(columna, out valor) ? valor as ReferenciaDeEntidad : null;
        }

        private static DateTime? Fecha(FilaDeDatos fila, string columna)
        {
            object valor;
            return fila.Valores.TryGetValue(columna, out valor) && valor is DateTime ? (DateTime?)(DateTime)valor : null;
        }

        private void Trazar(string texto)
        {
            if (_trazar != null) _trazar(texto);
        }
    }
}
